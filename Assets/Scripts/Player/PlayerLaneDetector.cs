using UnityEngine;

public enum RoadAreaType
{
    None,
    Road,
    Sidewalk,
    BikeLane
}

// 進行方向に対する左右。各区間の中央線(Visual/CenterLine)基準で判定する。
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

    [Tooltip("路上駐車車両を避けるための歩道通行許可を判定する範囲")]
    public float parkedCarCheckRadius = 3f;

    public RoadAreaType currentArea = RoadAreaType.None;
    public RoadSide currentSide = RoadSide.None;
    public bool bikeLaneExistsNearby = false;
    public bool parkedCarNearby = false;

    [Tooltip("この速さ(m/s)未満のときは進行方向が不安定なため、直前に判定した進行方向をそのまま使う")]
    public float minSpeedForDirection = 0.5f;

    private Rigidbody rb;
    private Vector3 lastMovingDirection = Vector3.forward;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        UpdateMovingDirection();

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
        parkedCarNearby = DetectParkedCarNearby();
    }

    void UpdateMovingDirection()
    {
        if (rb == null) return;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;

        if (velocity.magnitude >= minSpeedForDirection)
        {
            lastMovingDirection = velocity.normalized;
        }
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

    bool DetectParkedCarNearby()
    {
        Collider[] wideHits = Physics.OverlapSphere(transform.position, parkedCarCheckRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider hit in wideHits)
        {
            if (hit.GetComponent<ParkedCarZone>() != null)
            {
                return true;
            }
        }
        return false;
    }

    RoadAreaType GetAreaType(Collider hit, out RoadSide side)
    {
        RoadAreaType area;
        if (hit.CompareTag("Road_L") || hit.CompareTag("Road_R")) area = RoadAreaType.Road;
        else if (hit.CompareTag("BIkeLane_L") || hit.CompareTag("BikeLane_R")) area = RoadAreaType.BikeLane;
        else if (hit.CompareTag("Sidewalk_L") || hit.CompareTag("Sidewalk_R")) area = RoadAreaType.Sidewalk;
        else
        {
            side = RoadSide.None;
            return RoadAreaType.None;
        }

        side = DetermineSideByCenterLine(hit.transform);
        return area;
    }

    // Uターン等で進行方向が道路の基準方向と逆になっている場合は左右を反転する
    RoadSide DetermineSideByCenterLine(Transform hitTransform)
    {
        Transform centerLine = FindCenterLine(hitTransform);
        if (centerLine == null) return RoadSide.None;

        Vector3 toPlayer = transform.position - centerLine.position;
        toPlayer.y = 0f;

        Vector3 right = centerLine.right;
        right.y = 0f;

        if (toPlayer.sqrMagnitude < 0.0001f || right.sqrMagnitude < 0.0001f) return RoadSide.None;

        float dot = Vector3.Dot(toPlayer.normalized, right.normalized);
        RoadSide side = dot >= 0f ? RoadSide.Right : RoadSide.Left;

        Vector3 roadForward = centerLine.forward;
        roadForward.y = 0f;

        if (roadForward.sqrMagnitude > 0.0001f)
        {
            float forwardDot = Vector3.Dot(lastMovingDirection.normalized, roadForward.normalized);
            if (forwardDot < 0f)
            {
                side = (side == RoadSide.Right) ? RoadSide.Left : RoadSide.Right;
            }
        }

        return side;
    }

    // hitTransform: <RoadSectionRoot>/Areas/Left(or Right)/Aria_XXX_L(or R)
    Transform FindCenterLine(Transform hitTransform)
    {
        Transform areas = hitTransform.parent != null ? hitTransform.parent.parent : null;
        if (areas == null || areas.name != "Areas") return null;

        Transform roadSectionRoot = areas.parent;
        if (roadSectionRoot == null) return null;

        Transform visual = roadSectionRoot.Find("Visual");
        return visual != null ? visual.Find("CenterLine") : null;
    }
}