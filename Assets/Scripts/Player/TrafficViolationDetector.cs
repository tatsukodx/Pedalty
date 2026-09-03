using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ViolationInfo
{
    public string id;
    public string triggerArea;
    // "Left" / "Right" / 空文字列（左右を問わない）
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

    // (エリア, 左右) の完全一致で登録。左右を問わない違反は (エリア, None) に登録する。
    private readonly Dictionary<(RoadAreaType, RoadSide), ViolationInfo> violationsByCondition = new Dictionary<(RoadAreaType, RoadSide), ViolationInfo>();

    private RoadAreaType previousArea = RoadAreaType.None;
    private RoadSide previousSide = RoadSide.None;
    private bool previousBikeLaneExistsNearby = false;

    private void Awake()
    {
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

        if (currentArea != previousArea || currentSide != previousSide || bikeLaneExistsNearby != previousBikeLaneExistsNearby)
        {
            CheckViolation(currentArea, currentSide, bikeLaneExistsNearby);
            previousArea = currentArea;
            previousSide = currentSide;
            previousBikeLaneExistsNearby = bikeLaneExistsNearby;
        }
    }

    private void CheckViolation(RoadAreaType area, RoadSide side, bool bikeLaneExistsNearby)
    {
        // 自転車レーンが無い区間では、車道の左側を通行するのが正しい走行方法なので違反にしない
        if (area == RoadAreaType.Road && side == RoadSide.Left && !bikeLaneExistsNearby)
        {
            return;
        }

        // 左右を区別する違反（例: 逆走）を優先し、無ければ左右不問の違反を探す
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
