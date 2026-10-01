using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ViolationInfo
{
    public string id;
    public string triggerArea;
    public string triggerSide;
    public string category;
    public string violationName;
    [TextArea(3, 10)]
    public string description;
    public int penaltyAmount;
}

[System.Serializable]
public class ViolationInfoList
{
    public ViolationInfo[] violations;
}

public class TrafficViolationDetector : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerLaneDetector laneDetector;
    [SerializeField] private PenaltyController penaltyController;

    [Header("違反データ（JSON）")]
    [SerializeField] private TextAsset violationDataJson;

    [Header("信号無視の判定")]
    [Tooltip("交差点の中心からこの距離に入った時点で、進行方向の信号が赤なら信号無視とする")]
    [SerializeField] private float intersectionRadius = 8f;
    
    public static TrafficViolationDetector Instance { get; private set; }

    private readonly Dictionary<(RoadAreaType, RoadSide), ViolationInfo> violationsByCondition = new Dictionary<(RoadAreaType, RoadSide), ViolationInfo>();
    private readonly Dictionary<string, ViolationInfo> violationsById = new Dictionary<string, ViolationInfo>();

    private class IntersectionArea
    {
        public TrafficLightManager manager;
        public Vector3 center;
        public bool playerWasInside;
        public CarIntersectionNode node;
        public bool passedBikeCrossing;
        public Vector3 entryPosition;
        public Vector3 entryForward;
    }

    [Header("自転車横断帯の通行判定")]
    [Tooltip("CarIntersectionNodeをこの距離内で探して、交差点に紐付ける")]
    [SerializeField] private float nodeSearchRadius = 15f;
    [Tooltip("自転車横断帯の判定ゾーンを少し広げる余裕[m]")]
    [SerializeField] private float bikeCrossingMargin = 0.5f;

    private readonly List<IntersectionArea> intersections = new List<IntersectionArea>();
    private Transform player;
    private bool playerInsideIntersection;

    [Header("レーン違反の猶予")]
    [Tooltip("違反レーンにこの秒数い続けたらアウトにする")]
    [SerializeField] private float laneViolationGraceSeconds = 2f;

    private ViolationInfo pendingViolation;
    private float pendingTimer;
    private bool pendingReported;

    // 違反していない場所を走っているか（テレポート先の記録に使う）
    public bool IsPlayerInLegalLane { get; private set; }

    private void Awake()
    {
        Instance = this;
        LoadViolationData();
    }

    private void Start()
    {
        BicycleController bicycle = FindAnyObjectByType<BicycleController>();
        if (bicycle != null) player = bicycle.transform;

        BuildIntersections();
    }

    private void BuildIntersections()
    {
        Dictionary<TrafficLightManager, List<Vector3>> zonesByManager = new Dictionary<TrafficLightManager, List<Vector3>>();

        foreach (TrafficStopZone zone in FindObjectsByType<TrafficStopZone>(FindObjectsSortMode.None))
        {
            if (zone.manager == null) continue;

            if (!zonesByManager.TryGetValue(zone.manager, out List<Vector3> positions))
            {
                positions = new List<Vector3>();
                zonesByManager[zone.manager] = positions;
            }

            positions.Add(zone.transform.position);
        }

        foreach (KeyValuePair<TrafficLightManager, List<Vector3>> pair in zonesByManager)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 position in pair.Value) sum += position;

            Vector3 center = sum / pair.Value.Count;
            intersections.Add(new IntersectionArea
            {
                manager = pair.Key,
                center = center,
                node = FindNearestNode(center)
            });
        }
    }

    private CarIntersectionNode FindNearestNode(Vector3 center)
    {
        CarIntersectionNode nearest = null;
        float best = nodeSearchRadius;

        foreach (CarIntersectionNode node in FindObjectsByType<CarIntersectionNode>(FindObjectsSortMode.None))
        {
            Vector3 d = node.transform.position - center;
            d.y = 0f;
            if (d.magnitude < best)
            {
                best = d.magnitude;
                nearest = node;
            }
        }

        return nearest;
    }

    private void CheckIntersectionEntry()
    {
        playerInsideIntersection = false;

        if (player == null) return;

        Vector3 playerPosition = player.position;

        foreach (IntersectionArea intersection in intersections)
        {
            float dx = playerPosition.x - intersection.center.x;
            float dz = playerPosition.z - intersection.center.z;
            bool inside = dx * dx + dz * dz <= intersectionRadius * intersectionRadius;

            if (inside) playerInsideIntersection = true;

            if (inside && !intersection.playerWasInside)
            {
                intersection.entryPosition = playerPosition;
                intersection.entryForward = player.forward;

                // 自転車歩行者専用信号がある交差点は専用の違反として扱う
                string lightId = intersection.manager.bicycleFollowsPedestrianSignal ? "bicycle_signal" : "traffic_light";
                if (!GameDebugMode.IsEnabled && IsRedForPlayerDirection(intersection.manager)
                    && violationsById.TryGetValue(lightId, out ViolationInfo lightViolation))
                {
                    // 交差点に入る前の位置へ戻す
                    ReportViolation(lightViolation, true, intersection.center, intersectionRadius + 2f);
                }
            }

            // 横断帯ゾーンは交差点の判定半径より外側に置かれていることもあるため、少し広い範囲で見る
            bool nearIntersection = dx * dx + dz * dz <= (intersectionRadius + 6f) * (intersectionRadius + 6f);
            if (nearIntersection && intersection.node != null && intersection.node.IsInBikeCrossing(playerPosition, bikeCrossingMargin))
            {
                intersection.passedBikeCrossing = true;
            }

            // 交差点を渡りきった（入った所から離れた所へ抜けた）のに、自転車横断帯を一度も通っていなければ違反
            if (!inside && intersection.playerWasInside)
            {
                Vector3 crossed = playerPosition - intersection.entryPosition;
                crossed.y = 0f;
                bool recovering = player.GetComponent<BicycleRecovery>() is BicycleRecovery r && r.IsRecovering;

                if (!GameDebugMode.IsEnabled && !recovering && !intersection.passedBikeCrossing
                    && intersection.node != null && intersection.node.HasBikeCrossings
                    && crossed.magnitude >= intersectionRadius
                    && IsStraightCrossing(intersection.entryForward, crossed)
                    && violationsById.TryGetValue("bike_crossing", out ViolationInfo crossingViolation))
                {
                    ReportViolation(crossingViolation, true, intersection.center, intersectionRadius + 2f);
                }
            }

            // 交差点から十分離れたら、次の横断に備えて通過記録を消す
            if (!nearIntersection)
            {
                intersection.passedBikeCrossing = false;
            }

            intersection.playerWasInside = inside;
        }
    }

    // 入った時の向きと抜けた方向がほぼ同じなら「道路を横断した」とみなす（左折で角を曲がっただけの場合は除外）
    private bool IsStraightCrossing(Vector3 entryForward, Vector3 crossed)
    {
        entryForward.y = 0f;
        if (entryForward.sqrMagnitude < 0.0001f) return false;
        return Vector3.Angle(entryForward, crossed) < 45f;
    }

    private bool IsRedForPlayerDirection(TrafficLightManager manager)
    {
        Vector3 forward = player.forward;
        bool travelingNS = Mathf.Abs(forward.z) >= Mathf.Abs(forward.x);

        // 自転車歩行者専用信号がある交差点は歩行者信号に従う（点滅中の進入は許容）
        if (manager.bicycleFollowsPedestrianSignal)
        {
            TrafficLightPhase phase = manager.CurrentPhase;
            bool pedOn = manager.IsPedestrianGreen || manager.IsPedestrianBlinking;
            bool rightPhase = phase == TrafficLightPhase.Pedestrian_Green || phase == TrafficLightPhase.Pedestrian_Blink
                || (travelingNS ? phase == TrafficLightPhase.NS_Green : phase == TrafficLightPhase.EW_Green);
            return !(pedOn && rightPhase);
        }

        return travelingNS ? manager.IsNS_CarRed : manager.IsEW_CarRed;
    }

    private void LoadViolationData()
    {
        if (violationDataJson == null)
        {
            Debug.LogError("[TrafficViolationDetector] violationDataJsonが設定されていません。");
            return;
        }

        ViolationInfoList list = JsonUtility.FromJson<ViolationInfoList>(violationDataJson.text);
        if (list == null || list.violations == null)
        {
            Debug.LogError("[TrafficViolationDetector] 違反データJSONの読み込みに失敗しました。");
            return;
        }

        foreach (ViolationInfo info in list.violations)
        {
            violationsById[info.id] = info;

            if (string.IsNullOrEmpty(info.triggerArea)) continue;

            if (!System.Enum.TryParse(info.triggerArea, out RoadAreaType area))
            {
                Debug.LogWarning($"[TrafficViolationDetector] 不明なtriggerArea \"{info.triggerArea}\" (id={info.id}) をスキップしました。");
                continue;
            }

            RoadSide side = RoadSide.None;
            if (!string.IsNullOrEmpty(info.triggerSide) && !System.Enum.TryParse(info.triggerSide, out side))
            {
                Debug.LogWarning($"[TrafficViolationDetector] 不明なtriggerSide \"{info.triggerSide}\" (id={info.id}) をスキップしました。");
                continue;
            }

            violationsByCondition[(area, side)] = info;
        }
    }

    private void Update()
    {
        CheckIntersectionEntry();

        if (laneDetector == null) return;

        ViolationInfo current = playerInsideIntersection ? null : FindLaneViolation(
            laneDetector.currentArea, laneDetector.currentSide,
            laneDetector.bikeLaneExistsNearby, laneDetector.parkedCarNearby, laneDetector.sidewalkRidingAllowed);

        IsPlayerInLegalLane = !playerInsideIntersection && current == null && laneDetector.currentArea != RoadAreaType.None;

        BicycleRecovery recovery = player != null ? player.GetComponent<BicycleRecovery>() : null;
        bool recovering = recovery != null && recovery.IsRecovering;

        if (GameDebugMode.IsEnabled || recovering || current == null || current != pendingViolation)
        {
            pendingViolation = current;
            pendingTimer = 0f;
            pendingReported = false;
            return;
        }

        // 一定時間続けて違反レーンにいた場合のみアウトにする（多少のはみ出しや逆走は許容）
        pendingTimer += Time.deltaTime;
        if (!pendingReported && pendingTimer >= laneViolationGraceSeconds)
        {
            pendingReported = true;
            ReportViolation(pendingViolation, true, null, 0f);
        }
    }

    private ViolationInfo FindLaneViolation(RoadAreaType area, RoadSide side, bool bikeLaneExistsNearby, bool parkedCarNearby, bool sidewalkRidingAllowed)
    {
        if (area == RoadAreaType.None) return null;

        if (area == RoadAreaType.Road && side == RoadSide.Left && !bikeLaneExistsNearby)
        {
            return null;
        }

        // 路上駐車を避けるための一時的な歩道・車道通行は除外対象
        if (parkedCarNearby && area != RoadAreaType.BikeLane)
        {
            return null;
        }

        // 「自転車及び歩行者専用」標識がある区間の歩道は通行できる
        if (area == RoadAreaType.Sidewalk && sidewalkRidingAllowed)
        {
            return null;
        }

        if (violationsByCondition.TryGetValue((area, side), out ViolationInfo violation)) return violation;
        if (violationsByCondition.TryGetValue((area, RoadSide.None), out violation)) return violation;
        return null;
    }

    public void ReportViolationById(string id)
    {
        if (!violationsById.TryGetValue(id, out ViolationInfo violation))
        {
            Debug.LogWarning($"[TrafficViolationDetector] 違反ID \"{id}\" がviolations.jsonにありません。");
            return;
        }

        ReportViolation(violation, false, null, 0f);
    }

    private void ReportViolation(ViolationInfo violation, bool teleport, Vector3? avoidCenter, float avoidRadius)
    {
        BicycleRecovery recovery = player != null ? player.GetComponent<BicycleRecovery>() : null;
        if (teleport && recovery != null)
        {
            // 暗転して違反前の位置へ戻してから違反画面を出す
            recovery.RecoverFromViolation(avoidCenter, avoidRadius, () => ShowPopup(violation));
            return;
        }

        ShowPopup(violation);
    }

    private void ShowPopup(ViolationInfo violation)
    {
        if (penaltyController == null)
        {
            Debug.LogError("[TrafficViolationDetector] PenaltyControllerが設定されていません。");
            return;
        }

        penaltyController.ShowViolationPopup(violation);
    }
}
