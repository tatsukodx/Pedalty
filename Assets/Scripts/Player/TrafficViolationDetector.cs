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

    // 動的生成されるNPCなど、Inspectorで参照を張れない側から違反を報告するためのアクセサ
    public static TrafficViolationDetector Instance { get; private set; }

    private readonly Dictionary<(RoadAreaType, RoadSide), ViolationInfo> violationsByCondition = new Dictionary<(RoadAreaType, RoadSide), ViolationInfo>();
    private readonly Dictionary<string, ViolationInfo> violationsById = new Dictionary<string, ViolationInfo>();

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

            // triggerAreaが空の違反は通行区分では判定できないため、各所からID指定で報告する
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
        if (laneDetector == null) return;

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
