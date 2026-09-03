using UnityEngine;

public enum RoadAreaType
{
    None,
    Road,
    Sidewalk,
    BikeLane
}

// 進行方向に対する左右。道路プレハブは進行方向(ローカルZ軸)基準で
// _L/_Rタグが左右対称に配置されているため、タグからそのまま判定できる。
public enum RoadSide
{
    None,
    Left,
    Right
}

public class PlayerLaneDetector : MonoBehaviour
{
    public float sensorRadius = 1f;

    [Tooltip("自転車レーンの有無を判定する範囲。車道の反対端からでも隣接する自転車レーンを検知できるよう、sensorRadiusより広めに設定する")]
    public float bikeLaneCheckRadius = 6f;

    public RoadAreaType currentArea = RoadAreaType.None;
    public RoadSide currentSide = RoadSide.None;

    // 現在地の近くに自転車レーンが存在するか（存在しない道路区間では車道の左側走行を違反にしないため）
    public bool bikeLaneExistsNearby = false;

    void FixedUpdate()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, sensorRadius, ~0, QueryTriggerInteraction.Collide);
        RoadAreaType detectedArea = RoadAreaType.None;
        RoadSide detectedSide = RoadSide.None;
        float closestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            RoadAreaType type = GetAreaType(hit, out RoadSide side);
            if (type == RoadAreaType.None) continue;

            float distance = Vector3.Distance(transform.position, hit.ClosestPoint(transform.position));
            if (distance < closestDistance)
            {
                closestDistance = distance;
                detectedArea = type;
                detectedSide = side;
            }
        }

        currentArea = detectedArea;
        currentSide = detectedSide;

        bikeLaneExistsNearby = DetectBikeLaneNearby();
    }

    bool DetectBikeLaneNearby()
    {
        Collider[] wideHits = Physics.OverlapSphere(transform.position, bikeLaneCheckRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider hit in wideHits)
        {
            if (hit.CompareTag("BIkeLane_L") || hit.CompareTag("BikeLane_R"))
            {
                return true;
            }
        }
        return false;
    }

    RoadAreaType GetAreaType(Collider hit, out RoadSide side)
    {
        if (hit.CompareTag("Road_L")) { side = RoadSide.Left; return RoadAreaType.Road; }
        if (hit.CompareTag("Road_R")) { side = RoadSide.Right; return RoadAreaType.Road; }
        if (hit.CompareTag("BIkeLane_L")) { side = RoadSide.Left; return RoadAreaType.BikeLane; }
        if (hit.CompareTag("BikeLane_R")) { side = RoadSide.Right; return RoadAreaType.BikeLane; }
        if (hit.CompareTag("Sidewalk_L")) { side = RoadSide.Left; return RoadAreaType.Sidewalk; }
        if (hit.CompareTag("Sidewalk_R")) { side = RoadSide.Right; return RoadAreaType.Sidewalk; }

        side = RoadSide.None;
        return RoadAreaType.None;
    }
}