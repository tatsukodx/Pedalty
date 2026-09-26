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
    }

    private readonly List<IntersectionArea> intersections = new List<IntersectionArea>();
    private Transform player;
    private bool playerInsideIntersection;

    private RoadAreaType previousArea = RoadAreaType.None;
    private RoadSide previousSide = RoadSide.None;
    private bool previousBikeLaneExistsNearby = false;
    private bool previousParkedCarNearby = false;
    private bool previousSidewalkRidingAllowed = false;

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

            intersections.Add(new IntersectionArea
            {
                manager = pair.Key,
                center = sum / pair.Value.Count
            });
        }
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

            if (inside && !intersection.playerWasInside && IsRedForPlayerDirection(intersection.manager))
            {
                ReportViolationById("traffic_light");
            }

            intersection.playerWasInside = inside;
        }
    }

    private bool IsRedForPlayerDirection(TrafficLightManager manager)
    {
        Vector3 forward = player.forward;
        bool travelingNS = Mathf.Abs(forward.z) >= Mathf.Abs(forward.x);

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

        if (playerInsideIntersection) return;

        RoadAreaType currentArea = laneDetector.currentArea;
        RoadSide currentSide = laneDetector.currentSide;
        bool bikeLaneExistsNearby = laneDetector.bikeLaneExistsNearby;
        bool parkedCarNearby = laneDetector.parkedCarNearby;
        bool sidewalkRidingAllowed = laneDetector.sidewalkRidingAllowed;

        if (GameDebugMode.IsEnabled)
        {

            previousArea = currentArea;
            previousSide = currentSide;
            previousBikeLaneExistsNearby = bikeLaneExistsNearby;
            previousParkedCarNearby = parkedCarNearby;
            previousSidewalkRidingAllowed = sidewalkRidingAllowed;
            return;
        }

        if (currentArea != previousArea || currentSide != previousSide || bikeLaneExistsNearby != previousBikeLaneExistsNearby || parkedCarNearby != previousParkedCarNearby || sidewalkRidingAllowed != previousSidewalkRidingAllowed)
        {
            CheckViolation(currentArea, currentSide, bikeLaneExistsNearby, parkedCarNearby, sidewalkRidingAllowed);
            previousArea = currentArea;
            previousSide = currentSide;
            previousBikeLaneExistsNearby = bikeLaneExistsNearby;
            previousParkedCarNearby = parkedCarNearby;
            previousSidewalkRidingAllowed = sidewalkRidingAllowed;
        }
    }

    private void CheckViolation(RoadAreaType area, RoadSide side, bool bikeLaneExistsNearby, bool parkedCarNearby, bool sidewalkRidingAllowed)
    {
        if (area == RoadAreaType.Road && side == RoadSide.Left && !bikeLaneExistsNearby)
        {
            return;
        }

        // 路上駐車を避けるための一時的な歩道通行は道路交通法上の除外対象
        if (area == RoadAreaType.Sidewalk && parkedCarNearby)
        {
            return;
        }

        // 「自転車及び歩行者専用」標識がある区間の歩道は通行できる
        if (area == RoadAreaType.Sidewalk && sidewalkRidingAllowed)
        {
            return;
        }

        if (violationsByCondition.TryGetValue((area, side), out ViolationInfo violation))
        {
            ReportViolation(violation);
            return;
        }

        if (violationsByCondition.TryGetValue((area, RoadSide.None), out violation))
        {
            ReportViolation(violation);
        }
    }

    public void ReportViolationById(string id)
    {
        if (!violationsById.TryGetValue(id, out ViolationInfo violation))
        {
            Debug.LogWarning($"[TrafficViolationDetector] 違反ID \"{id}\" がviolations.jsonにありません。");
            return;
        }

        ReportViolation(violation);
    }

    private void ReportViolation(ViolationInfo violation)
    {
        if (penaltyController == null)
        {
            Debug.LogError("[TrafficViolationDetector] PenaltyControllerが設定されていません。");
            return;
        }

        penaltyController.ShowViolationPopup(violation);
    }
}
