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
        // 最後に信号を判定した進行軸（0=未判定, 1=南北, 2=東西）。二段階右折などで軸が変わったら再判定する
        public int checkedAxis;
        public Vector3 entryPosition;
        public Vector3 entryForward;
        public bool isBicyclePedestrian;
        public List<Bounds> bicycleCrosswalks = new List<Bounds>();
        // 専用信号の交差点で、4本の横断帯の内側に囲まれた中央の車道部分（自転車が直接走ってはいけない範囲）
        public bool hasCore;
        public Rect core;
        public float coreTravel = -1f;
        public bool coreReported;
        public Vector3 lastCorePosition;
        public Rect outer;
        public bool inCrossingNS;
        public bool inCrossingEW;
        public Transform currentCrosswalk;
        public float lastSignalCheckTime = -10f;
    }

    [Header("自転車横断帯の通行判定")]
    [Tooltip("CarIntersectionNodeをこの距離内で探して、交差点に紐付ける")]
    [SerializeField] private float nodeSearchRadius = 15f;
    [Tooltip("自転車横断帯の判定ゾーンを少し広げる余裕[m]")]
    [SerializeField] private float bikeCrossingMargin = 0.5f;
    [Tooltip("専用信号の交差点で、横断帯の内側（中央の車道部分）をこの距離[m]以上走ったら横断帯通行義務違反にする。角を少しかすめた程度は許容する")]
    [SerializeField] private float bicyclePedCoreMaxTravel = 3f;
    [Tooltip("専用信号の交差点で信号を判定し始める位置。中央範囲からこの幅[m]（自転車道の幅）だけ内側の、車が走る部分に入った時に判定する")]
    [SerializeField] private float bicyclePedLaneInset = 1.5f;
    [Tooltip("専用信号の交差点で、横断帯へ寄るために歩道や車道（歩行者用横断歩道の上など）にいてもよい範囲。交差点の外側へこの距離[m]まで")]
    [SerializeField] private float bicyclePedApproachAllowance = 3f;

    private readonly List<IntersectionArea> intersections = new List<IntersectionArea>();
    private Transform player;
    private Rigidbody playerRb;

    [Header("交差点内での方向転換（二段階右折など）の信号判定")]
    [Tooltip("この速さ[m/s]以上で、交差点を横切る向きに走り出したら、その向きの信号を判定する")]
    [SerializeField] private float turnCheckMinSpeed = 1f;
    private bool playerInsideIntersection;
    // 専用信号の交差点の横断帯へ向かうため、交差点手前の歩道部分を通っている
    private bool playerNearBicyclePedCrossing;

    [Header("レーン違反の猶予")]
    [Tooltip("違反レーンにこの秒数い続けたらアウトにする")]
    [SerializeField] private float laneViolationGraceTime = 1f;

    private ViolationInfo pendingViolation;
    private float pendingTimer;
    private bool pendingReported;

    // 違反していない場所を走っているか（テレポート先の記録に使う）
    public bool IsPlayerInLegalLane { get; private set; }

    [Header("外側のレーン（車・歩行者のスポーン用）への進入")]
    [Tooltip("スポーン地点のある道路区間にこの秒数いたら、警告を出して交差点の手前へ戻す")]
    [SerializeField] private float wrongLaneDelay = 0.3f;

    // 車や歩行者のスポーン地点を含む道路区間（自転車が入るとUターンできず詰むため進入禁止）
    private readonly HashSet<Transform> outerRoadSections = new HashSet<Transform>();
    private float wrongLaneTimer;

    private void Awake()
    {
        Instance = this;
        LoadViolationData();
    }

    private void Start()
    {
        BicycleController bicycle = FindAnyObjectByType<BicycleController>();
        if (bicycle != null)
        {
            player = bicycle.transform;
            playerRb = bicycle.GetComponent<Rigidbody>();
        }

        BuildIntersections();
        BuildOuterRoadSections();
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

        SetupBicyclePedestrianIntersections();
    }

    // IntersectionNode(歩行者用)が BicyclePedestrian の交差点を自転車歩行者専用信号の交差点とし、
    // 近くにある自転車専用横断帯(Crosswalk_bicycle*)の描画範囲を通行判定に使う
    private void SetupBicyclePedestrianIntersections()
    {
        IntersectionNode[] pedNodes = FindObjectsByType<IntersectionNode>(FindObjectsSortMode.None);
        List<Transform> bikeCrosswalks = new List<Transform>();
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name.StartsWith("Crosswalk_bicycle")) bikeCrosswalks.Add(t);
        }

        foreach (IntersectionArea intersection in intersections)
        {
            if (intersection.manager.bicycleFollowsPedestrianSignal) intersection.isBicyclePedestrian = true;

            foreach (IntersectionNode pedNode in pedNodes)
            {
                if (pedNode.signalType != IntersectionNode.SignalType.BicyclePedestrian) continue;
                bool sameManager = pedNode.manager != null && pedNode.manager == intersection.manager;
                bool sameNode = pedNode.carIntersectionNode != null && pedNode.carIntersectionNode == intersection.node;
                if (sameManager || sameNode) intersection.isBicyclePedestrian = true;
            }

            if (!intersection.isBicyclePedestrian) continue;

            foreach (Transform crosswalk in bikeCrosswalks)
            {
                Vector3 d = crosswalk.position - intersection.center;
                d.y = 0f;
                if (d.magnitude > nodeSearchRadius) continue;

                Renderer[] renderers = crosswalk.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;

                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                intersection.bicycleCrosswalks.Add(b);
            }

            BuildCore(intersection);

            Debug.Log($"[TrafficViolationDetector] 自転車歩行者専用信号の交差点 {intersection.manager.name}: 自転車横断帯 {intersection.bicycleCrosswalks.Count} 箇所 / 中央範囲 {(intersection.hasCore ? intersection.core.ToString() : "なし")}");
        }
    }

    // 横断帯の内側の辺で囲まれた中央の範囲を求める（南北に長い横断帯→東西の境界、東西に長い横断帯→南北の境界）
    private void BuildCore(IntersectionArea intersection)
    {
        if (intersection.bicycleCrosswalks.Count == 0) return;

        Bounds all = intersection.bicycleCrosswalks[0];
        foreach (Bounds b in intersection.bicycleCrosswalks) all.Encapsulate(b);
        Vector3 c = all.center;

        float minX = all.min.x, maxX = all.max.x, minZ = all.min.z, maxZ = all.max.z;
        bool foundX = false, foundZ = false;
        foreach (Bounds b in intersection.bicycleCrosswalks)
        {
            if (b.size.z > b.size.x)
            {
                // 南北に長い横断帯: 交差点の西端か東端にある
                if (b.center.x < c.x) minX = Mathf.Max(minX, b.max.x);
                else maxX = Mathf.Min(maxX, b.min.x);
                foundX = true;
            }
            else
            {
                if (b.center.z < c.z) minZ = Mathf.Max(minZ, b.max.z);
                else maxZ = Mathf.Min(maxZ, b.min.z);
                foundZ = true;
            }
        }

        if (!foundX || !foundZ || maxX <= minX || maxZ <= minZ) return;

        intersection.hasCore = true;
        intersection.core = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        intersection.outer = Rect.MinMaxRect(all.min.x, all.min.z, all.max.x, all.max.z);
    }

    // 専用信号の交差点: 交差点の円に入った時ではなく、実際に道路の横断部分へ入った瞬間に信号を判定する。
    // 南北へ進む自転車は「東西の道路の幅（中央範囲のZ幅）」に入った時、東西へ進む自転車はその逆。
    // 手前の横断帯（直交する方向のもの）の上や、横断帯の手前の角で待っている間は判定しない
    private void CheckBicyclePedSignal(IntersectionArea intersection, Vector3 p)
    {
        const float rearmMargin = 0.7f;
        // 中央範囲には自転車道の延長部分も含まれるため、自転車道の幅だけ内側に縮めた「車が走る部分」で判定する。
        // 自転車道から横断帯へ寄るための横移動では、ここに入らない
        Rect core = intersection.core;
        core.xMin += bicyclePedLaneInset; core.xMax -= bicyclePedLaneInset;
        core.yMin += bicyclePedLaneInset; core.yMax -= bicyclePedLaneInset;
        bool inOuter = intersection.outer.Contains(new Vector2(p.x, p.z));

        bool inZ = inOuter && p.z > core.yMin && p.z < core.yMax;
        bool inX = inOuter && p.x > core.xMin && p.x < core.xMax;

        // 境界付近で止まっていて揺れても繰り返し判定しないよう、少し離れるまでは「入った」状態を保つ
        bool nearZ = p.z > core.yMin - rearmMargin && p.z < core.yMax + rearmMargin
            && p.x > intersection.outer.xMin - rearmMargin && p.x < intersection.outer.xMax + rearmMargin;
        bool nearX = p.x > core.xMin - rearmMargin && p.x < core.xMax + rearmMargin
            && p.z > intersection.outer.yMin - rearmMargin && p.z < intersection.outer.yMax + rearmMargin;

        Vector3 heading = player.forward;
        if (playerRb != null)
        {
            Vector3 v = playerRb.linearVelocity;
            v.y = 0f;
            if (v.magnitude > 0.5f) heading = v;
        }
        bool headingNS = AxisOf(heading) == 1;

        bool recovering = player.GetComponent<BicycleRecovery>() is BicycleRecovery r && r.IsRecovering;

        if (headingNS && inZ && !intersection.inCrossingNS)
        {
            intersection.inCrossingNS = true;
            if (!recovering) ReportBicyclePedSignalIfRed(intersection, true, heading);
        }
        else if (!headingNS && inX && !intersection.inCrossingEW)
        {
            intersection.inCrossingEW = true;
            if (!recovering) ReportBicyclePedSignalIfRed(intersection, false, heading);
        }

        if (!nearZ) intersection.inCrossingNS = false;
        if (!nearX) intersection.inCrossingEW = false;
    }

    private void ReportBicyclePedSignalIfRed(IntersectionArea intersection, bool travelingNS, Vector3 heading)
    {
        if (GameDebugMode.IsEnabled) return;

        // 直進方向の歩行者信号（自転車の進行方向と同じ向きに渡る横断歩道の信号）の表示で判定する。
        // 横断歩道の両端の信号は連動しているので、連動グループの並びが進行方向と同じ向きのものを選ぶ。
        // 見つからない場合は、信号機の内部状態（同じ方向の車両用信号のフェーズ）で判定する
        TrafficLight governing = FindStraightPedLight(intersection.manager, travelingNS);
        bool red = governing != null
            ? governing.CurrentState != TrafficLightState.Green
            : IsRedForPlayerDirection(intersection.manager, true, travelingNS);
        Debug.Log($"[TrafficViolationDetector] 専用信号の判定: 進行方向={(travelingNS ? "南北" : "東西")} 参照した信号={(governing != null ? governing.name + "(" + governing.CurrentState + ")" : "見つからず→信号機の状態で判定")} 赤={red}");
        if (!red) return;
        if (!violationsById.TryGetValue("bicycle_signal", out ViolationInfo violation)) return;

        // 横断を始める前（待っていた位置など）へ戻す
        ReportViolation(violation, true, null, 0f);
    }

    private TrafficLight FindStraightPedLight(TrafficLightManager manager, bool travelingNS)
    {
        TrafficLight best = null;
        float bestDistance = float.MaxValue;

        foreach (TrafficLight head in new[] { manager.pedNorthLight, manager.pedSouthLight, manager.pedEastLight, manager.pedWestLight })
        {
            if (head == null) continue;

            // 連動している信号（横断歩道の反対側の端など）を集める
            List<TrafficLight> group = new List<TrafficLight>();
            CollectLinked(head, group);
            if (group.Count < 2) continue;

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            Vector3 sum = Vector3.zero;
            foreach (TrafficLight l in group)
            {
                Vector3 p = l.transform.position;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                sum += p;
            }

            float spreadX = maxX - minX, spreadZ = maxZ - minZ;
            if (Mathf.Max(spreadX, spreadZ) < 2f) continue;

            // 並びの向き＝その横断歩道を渡る向き
            bool groupNS = spreadZ > spreadX;
            if (groupNS != travelingNS) continue;

            // 同じ向きの横断歩道は交差点の両側に2本あるので、自転車に近い方を使う
            Vector3 center = sum / group.Count;
            Vector3 d = center - player.position;
            d.y = 0f;
            if (d.magnitude < bestDistance)
            {
                bestDistance = d.magnitude;
                best = head;
            }
        }
        return best;
    }

    private static void CollectLinked(TrafficLight light, List<TrafficLight> group)
    {
        if (light == null || group.Contains(light)) return;
        group.Add(light);
        if (light.linkedLights != null) foreach (TrafficLight l in light.linkedLights) CollectLinked(l, group);
    }

    private TrafficLight FindGoverningPedLight(TrafficLightManager manager, Vector3 heading)
    {
        heading.y = 0f;
        if (heading.sqrMagnitude < 0.0001f) return null;
        heading.Normalize();

        List<TrafficLight> lights = new List<TrafficLight>();
        void Add(TrafficLight l)
        {
            if (l == null || lights.Contains(l)) return;
            lights.Add(l);
            if (l.linkedLights != null) foreach (TrafficLight linked in l.linkedLights) Add(linked);
        }
        Add(manager.pedNorthLight);
        Add(manager.pedSouthLight);
        Add(manager.pedEastLight);
        Add(manager.pedWestLight);

        TrafficLight best = null;
        float bestScore = float.MaxValue;
        foreach (TrafficLight light in lights)
        {
            Vector3 to = light.transform.position - player.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 1f || distance > 25f) continue;

            // 前方30度以内にあるものの中で、進行方向からのずれが小さく近いものを選ぶ
            float angle = Vector3.Angle(heading, to);
            if (angle > 30f) continue;

            float score = angle * 0.5f + distance;

            // 角の柱には直交する横断歩道用の信号も同じ位置に付いているため、
            // 進行方向に正対している（正面か真後ろを向いている）信号を優先する
            Vector3 face = light.transform.forward;
            face.y = 0f;
            float alignment = face.sqrMagnitude > 0.0001f ? Mathf.Abs(Vector3.Dot(face.normalized, heading)) : 0f;
            if (alignment < 0.7f) score += 1000f;
            if (score < bestScore)
            {
                bestScore = score;
                best = light;
            }
        }
        return best;
    }

    // 専用信号の交差点: 中央の車道部分を一定距離以上走ったら違反（横断帯を通って迂回すれば入らない）
    private void CheckBicyclePedCore(IntersectionArea intersection, Vector3 playerPosition, bool nearIntersection)
    {
        if (!nearIntersection)
        {
            intersection.coreTravel = -1f;
            intersection.coreReported = false;
            return;
        }

        bool inCore = intersection.core.Contains(new Vector2(playerPosition.x, playerPosition.z));
        if (!inCore)
        {
            intersection.coreTravel = -1f;
            return;
        }

        // 中央範囲に入った地点（lastCorePosition）からどれだけ離れたかで見る。
        // 走った距離を足し合わせると、縁で止まって待っているだけでも車体の小さな揺れが積もって違反になってしまう
        if (intersection.coreTravel < 0f) intersection.lastCorePosition = playerPosition;
        Vector3 moved = playerPosition - intersection.lastCorePosition;
        moved.y = 0f;
        intersection.coreTravel = moved.magnitude;

        bool recovering = player.GetComponent<BicycleRecovery>() is BicycleRecovery r && r.IsRecovering;
        if (intersection.coreReported || recovering || GameDebugMode.IsEnabled) return;

        if (intersection.coreTravel >= bicyclePedCoreMaxTravel
            && violationsById.TryGetValue("bike_crossing", out ViolationInfo crossingViolation))
        {
            intersection.coreReported = true;
            Debug.Log($"[TrafficViolationDetector] 横断帯違反: 自転車の位置{playerPosition} 中央範囲に入った位置{intersection.lastCorePosition} 中央範囲{intersection.core}");
            ReportViolation(crossingViolation, true, intersection.center, intersectionRadius + 2f);
        }
    }

    private bool IsInBicycleCrosswalk(IntersectionArea intersection, Vector3 position)
    {
        // 直進すると直交する横断帯（横方向に渡るためのもの）の上も通るため、
        // 横断帯の長い辺の向きと自転車の進行方向が一致するものだけを数える
        Vector3 forward = player.forward;
        bool headingX = Mathf.Abs(forward.x) > Mathf.Abs(forward.z);

        foreach (Bounds b in intersection.bicycleCrosswalks)
        {
            bool crosswalkAlongX = b.size.x > b.size.z;
            if (crosswalkAlongX != headingX) continue;

            if (position.x >= b.min.x - bikeCrossingMargin && position.x <= b.max.x + bikeCrossingMargin
                && position.z >= b.min.z - bikeCrossingMargin && position.z <= b.max.z + bikeCrossingMargin)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsInCrossingZone(IntersectionArea intersection, Vector3 position)
    {
        // 専用信号の交差点は交差点の外側にある自転車専用横断帯、それ以外は自転車道の判定ゾーンで見る
        if (intersection.isBicyclePedestrian && intersection.bicycleCrosswalks.Count > 0)
        {
            return IsInBicycleCrosswalk(intersection, position);
        }
        return intersection.node != null && intersection.node.IsInBikeCrossing(position, bikeCrossingMargin);
    }

    private bool HasCrossingZone(IntersectionArea intersection)
    {
        if (intersection.isBicyclePedestrian && intersection.bicycleCrosswalks.Count > 0) return true;
        return intersection.node != null && intersection.node.HasBikeCrossings;
    }

    private void BuildOuterRoadSections()
    {
        // 道路区間ごとに、レーン判定の箱をまとめた範囲を作る
        Dictionary<Transform, Bounds> sectionBounds = new Dictionary<Transform, Bounds>();
        foreach (Collider c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (!(c.CompareTag("Road_L") || c.CompareTag("Road_R") || c.CompareTag("Sidewalk_L") || c.CompareTag("Sidewalk_R")
                || c.CompareTag("BIkeLane_L") || c.CompareTag("BikeLane_R"))) continue;

            Transform root = PlayerLaneDetector.GetRoadSectionRoot(c.transform);
            if (root == null) continue;

            if (sectionBounds.TryGetValue(root, out Bounds b)) { b.Encapsulate(c.bounds); sectionBounds[root] = b; }
            else sectionBounds[root] = c.bounds;
        }

        List<Vector3> spawnPositions = new List<Vector3>();
        foreach (CarSpawner spawner in FindObjectsByType<CarSpawner>(FindObjectsSortMode.None))
            if (spawner.spawnPoints != null) foreach (Transform t in spawner.spawnPoints) if (t != null) spawnPositions.Add(t.position);
        foreach (NPCSpawner spawner in FindObjectsByType<NPCSpawner>(FindObjectsSortMode.None))
            if (spawner.spawnPoints != null) foreach (Transform t in spawner.spawnPoints) if (t != null) spawnPositions.Add(t.position);

        // スタート地点・ゴールのある区間は対象外にする
        List<Vector3> protectedPositions = new List<Vector3>();
        if (player != null) protectedPositions.Add(player.position);
        foreach (GoalTrigger goal in FindObjectsByType<GoalTrigger>(FindObjectsSortMode.None)) protectedPositions.Add(goal.transform.position);

        foreach (KeyValuePair<Transform, Bounds> pair in sectionBounds)
        {
            if (ContainsAnyXZ(pair.Value, protectedPositions, 2f)) continue;
            if (ContainsAnyXZ(pair.Value, spawnPositions, 2f)) outerRoadSections.Add(pair.Key);
        }

        Debug.Log($"[TrafficViolationDetector] 外側のレーン（スポーン用）として扱う道路区間: {outerRoadSections.Count} 箇所");
    }

    private static bool ContainsAnyXZ(Bounds b, List<Vector3> positions, float margin)
    {
        foreach (Vector3 p in positions)
        {
            if (p.x >= b.min.x - margin && p.x <= b.max.x + margin && p.z >= b.min.z - margin && p.z <= b.max.z + margin) return true;
        }
        return false;
    }

    // 外側のレーンに入ったら、赤い警告を出して交差点の手前（最後に違反していなかった位置）へ戻す
    private bool CheckWrongLane()
    {
        bool onOuterRoad = laneDetector.currentRoadSection != null && outerRoadSections.Contains(laneDetector.currentRoadSection);
        BicycleRecovery recovery = player != null ? player.GetComponent<BicycleRecovery>() : null;

        if (!onOuterRoad || playerInsideIntersection || recovery == null || recovery.IsRecovering)
        {
            wrongLaneTimer = 0f;
            return onOuterRoad;
        }

        wrongLaneTimer += Time.deltaTime;
        if (wrongLaneTimer >= wrongLaneDelay)
        {
            wrongLaneTimer = 0f;
            // 暗転→テレポート→暗転解除のあとに警告を出す
            recovery.RecoverFromViolation(null, 0f, () => WrongLaneWarningUI.Show());
        }
        return true;
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
        playerNearBicyclePedCrossing = false;

        if (player == null) return;

        Vector3 playerPosition = player.position;

        foreach (IntersectionArea intersection in intersections)
        {
            float dx = playerPosition.x - intersection.center.x;
            float dz = playerPosition.z - intersection.center.z;
            bool inside = dx * dx + dz * dz <= intersectionRadius * intersectionRadius;

            if (inside) playerInsideIntersection = true;

            if (inside && !intersection.playerWasInside && !intersection.hasCore)
            {
                intersection.entryPosition = playerPosition;
                intersection.entryForward = player.forward;
                intersection.checkedAxis = AxisOf(player.forward);

                // 自転車歩行者専用信号がある交差点は専用の違反として扱う
                // （横断歩道の判定範囲がある普通の交差点は、下の横断歩道進入時の判定を使う）
                string lightId = intersection.isBicyclePedestrian ? "bicycle_signal" : "traffic_light";
                if (!UsesCrosswalkSignalCheck(intersection) && !GameDebugMode.IsEnabled && IsRedForPlayerDirection(intersection.manager, intersection.isBicyclePedestrian, intersection.checkedAxis == 1)
                    && violationsById.TryGetValue(lightId, out ViolationInfo lightViolation))
                {
                    // 交差点に入る前の位置へ戻す
                    ReportViolation(lightViolation, true, intersection.center, intersectionRadius + 2f);
                }
            }

            // 横断帯ゾーンは交差点の判定半径より外側に置かれていることもあるため、少し広い範囲で見る
            bool nearIntersection = dx * dx + dz * dz <= (intersectionRadius + 6f) * (intersectionRadius + 6f);

            if (nearIntersection && UsesCrosswalkSignalCheck(intersection))
            {
                CheckCrosswalkSignal(intersection, playerPosition);
            }
            if (nearIntersection && IsInCrossingZone(intersection, playerPosition))
            {
                intersection.passedBikeCrossing = true;
            }

            if (intersection.hasCore)
            {
                Rect approach = intersection.core;
                // 横断帯へ寄るための歩道通行だけを許す範囲（交差点の外側へ1mまで）。
                // 広げすぎると、左折した先の歩道まで違反にならなくなる
                float m = bicyclePedApproachAllowance;
                approach.xMin = intersection.outer.xMin - m; approach.yMin = intersection.outer.yMin - m;
                approach.xMax = intersection.outer.xMax + m; approach.yMax = intersection.outer.yMax + m;
                if (approach.Contains(new Vector2(playerPosition.x, playerPosition.z))) playerNearBicyclePedCrossing = true;

                CheckBicyclePedSignal(intersection, playerPosition);
                CheckBicyclePedCore(intersection, playerPosition, nearIntersection);
            }

            // 交差点を渡りきった（入った所から離れた所へ抜けた）のに、自転車横断帯を一度も通っていなければ違反
            // （専用信号の交差点は上の中央範囲の判定を使う）
            if (!intersection.hasCore && !inside && intersection.playerWasInside)
            {
                Vector3 crossed = playerPosition - intersection.entryPosition;
                crossed.y = 0f;
                bool recovering = player.GetComponent<BicycleRecovery>() is BicycleRecovery r && r.IsRecovering;

                if (!GameDebugMode.IsEnabled && !recovering && !intersection.passedBikeCrossing
                    && HasCrossingZone(intersection)
                    && crossed.magnitude >= intersectionRadius
                    && IsStraightCrossing(intersection.entryForward, crossed)
                    && violationsById.TryGetValue("bike_crossing", out ViolationInfo crossingViolation))
                {
                    ReportViolation(crossingViolation, true, intersection.center, intersectionRadius + 2f);
                }
            }

            // 角で向きを変えて、交差点を横切る向きに走り出した（二段階右折の2回目の横断など）場合も信号を判定する
            if (nearIntersection && !intersection.hasCore)
            {
                CheckTurnInsideIntersection(intersection, playerPosition);
            }

            // 交差点から十分離れたら、次の横断に備えて通過記録を消す
            if (!nearIntersection)
            {
                intersection.passedBikeCrossing = false;
                intersection.checkedAxis = 0;
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

    // 普通の交差点（専用信号ではない）で、横断歩道の判定範囲が設定されているもの
    private bool UsesCrosswalkSignalCheck(IntersectionArea intersection)
    {
        return !intersection.isBicyclePedestrian && intersection.node != null && intersection.node.HasCrosswalks;
    }

    // 赤信号のまま、自分のレーンの手前にある横断歩道（歩行者横断中の確認範囲）まで進んだら信号無視
    private void CheckCrosswalkSignal(IntersectionArea intersection, Vector3 playerPosition)
    {
        const float exitMargin = 0.7f;

        if (intersection.currentCrosswalk != null)
        {
            // 少し離れるまでは同じ横断歩道の中にいる扱いにして、境界で止まっていても繰り返し判定しない
            if (intersection.node.GetCrosswalkAt(playerPosition, exitMargin) == intersection.currentCrosswalk) return;
            intersection.currentCrosswalk = null;
        }

        Transform crosswalk = intersection.node.GetCrosswalkAt(playerPosition, 0f);
        if (crosswalk == null) return;
        intersection.currentCrosswalk = crosswalk;

        Vector3 heading = player.forward;
        if (playerRb != null)
        {
            Vector3 v = playerRb.linearVelocity;
            v.y = 0f;
            if (v.magnitude > 0.5f) heading = v;
        }
        heading.y = 0f;
        if (heading.sqrMagnitude < 0.0001f) return;
        heading.Normalize();

        // 横断歩道を通り抜ける向きに進んでいて、交差点の中心へ向かっている時だけ（交差点から出ていく時は判定しない）
        Vector3 passAxis = intersection.node.GetCrosswalkPassAxis(crosswalk);
        if (Mathf.Abs(Vector3.Dot(heading, passAxis)) < 0.7f) return;

        Vector3 toCenter = intersection.center - playerPosition;
        toCenter.y = 0f;
        if (Vector3.Dot(heading, toCenter) <= 0f) return;

        int axis = AxisOf(heading);
        intersection.checkedAxis = axis;
        intersection.lastSignalCheckTime = Time.time;

        BicycleRecovery recovery = player.GetComponent<BicycleRecovery>();
        if (GameDebugMode.IsEnabled || (recovery != null && recovery.IsRecovering)) return;

        if (IsRedForPlayerDirection(intersection.manager, false, axis == 1)
            && violationsById.TryGetValue("traffic_light", out ViolationInfo lightViolation))
        {
            // 横断歩道の手前へ戻す
            ReportViolation(lightViolation, true, intersection.center, intersectionRadius + 2f);
        }
    }

    private static int AxisOf(Vector3 direction)
    {
        return Mathf.Abs(direction.z) >= Mathf.Abs(direction.x) ? 1 : 2;
    }

    private void CheckTurnInsideIntersection(IntersectionArea intersection, Vector3 playerPosition)
    {
        if (playerRb == null || intersection.checkedAxis == 0) return;

        Vector3 velocity = playerRb.linearVelocity;
        velocity.y = 0f;
        if (velocity.magnitude < turnCheckMinSpeed) return;

        int axis = AxisOf(velocity);
        if (axis == intersection.checkedAxis) return;

        // 交差点の中心側へ向かって走り出した時だけ横断とみなす（左折して交差点から離れていく場合は除外）
        Vector3 toCenter = intersection.center - playerPosition;
        toCenter.y = 0f;
        if (Vector3.Dot(velocity, toCenter) <= 0f) return;

        intersection.checkedAxis = axis;

        // 直前に横断歩道進入時の判定をしたばかりなら二重に判定しない
        if (Time.time - intersection.lastSignalCheckTime < 1f) return;
        intersection.lastSignalCheckTime = Time.time;

        BicycleRecovery recovery = player.GetComponent<BicycleRecovery>();
        if (GameDebugMode.IsEnabled || (recovery != null && recovery.IsRecovering)) return;

        string lightId = intersection.isBicyclePedestrian ? "bicycle_signal" : "traffic_light";
        if (IsRedForPlayerDirection(intersection.manager, intersection.isBicyclePedestrian, axis == 1)
            && violationsById.TryGetValue(lightId, out ViolationInfo lightViolation))
        {
            // 横断を始める前（角で待っていた位置など）へ戻す
            ReportViolation(lightViolation, true, null, 0f);
        }
    }

    private bool IsRedForPlayerDirection(TrafficLightManager manager, bool followsPedestrianSignal, bool travelingNS)
    {
        // 自転車歩行者専用信号がある交差点は歩行者信号に従う（点滅中の進入は許容）
        if (followsPedestrianSignal)
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

        // 外側のレーンにいる間はレーン違反を出さず、テレポート先としても記録しない
        bool onOuterRoad = !GameDebugMode.IsEnabled && CheckWrongLane();

        ViolationInfo current = (playerInsideIntersection || onOuterRoad) ? null : FindLaneViolation(
            laneDetector.currentArea, laneDetector.currentSide,
            laneDetector.bikeLaneExistsNearby, laneDetector.parkedCarNearby, laneDetector.sidewalkRidingAllowed);

        // 歩道は例外（路上駐車・横断帯へ寄る時など）で違反にならない場合でも、テレポート先としては記録しない。
        // 「自転車及び歩行者専用」の歩道だけは通常の通行場所なので記録する
        bool onSidewalk = laneDetector.currentArea == RoadAreaType.Sidewalk && !laneDetector.sidewalkRidingAllowed;
        IsPlayerInLegalLane = !playerInsideIntersection && !onOuterRoad && !onSidewalk && !playerNearBicyclePedCrossing && current == null && laneDetector.currentArea != RoadAreaType.None;

        BicycleRecovery recovery = player != null ? player.GetComponent<BicycleRecovery>() : null;
        bool recovering = recovery != null && recovery.IsRecovering;

        if (GameDebugMode.IsEnabled || recovering || current == null)
        {
            pendingViolation = null;
            pendingTimer = 0f;
            pendingReported = false;
            return;
        }

        // 一定時間続けて違反レーンにいた場合のみアウトにする（多少のはみ出しや逆走は許容）。
        // 道路の途中で反対側へ横断する時のように、違反の種類が次々に変わってもカウントは続け、
        // 違反のない場所へ戻った時だけリセットする。報告するのはその時点の違反
        pendingViolation = current;
        pendingTimer += Time.deltaTime;
        if (!pendingReported && pendingTimer >= laneViolationGraceTime)
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
        // 専用信号の交差点では、横断帯へ向かうため（横断帯の手前で待つため）に交差点手前の歩道や車道にいてよい
        if ((area == RoadAreaType.Sidewalk || area == RoadAreaType.Road) && playerNearBicyclePedCrossing)
        {
            return null;
        }

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

#if UNITY_EDITOR
    // プレイ中の Scene ビューに、専用信号の交差点の判定範囲を表示する
    // 水色: 自転車横断帯として認識した範囲 / 赤: 中央の車道部分（ここを3m以上進むと横断帯違反）
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        foreach (IntersectionArea intersection in intersections)
        {
            Gizmos.color = Color.cyan;
            foreach (Bounds b in intersection.bicycleCrosswalks)
            {
                Gizmos.DrawWireCube(new Vector3(b.center.x, b.center.y + 0.3f, b.center.z), new Vector3(b.size.x, 0.6f, b.size.z));
            }

            if (intersection.hasCore)
            {
                Gizmos.color = Color.red;
                Rect c = intersection.core;
                Gizmos.DrawWireCube(new Vector3(c.center.x, intersection.center.y + 0.3f, c.center.y), new Vector3(c.width, 0.6f, c.height));
            }
        }
    }
#endif
}
