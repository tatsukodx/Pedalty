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

    [Tooltip("画面左下に現在のレーン判定を表示する（調査用）")]
    public bool showDebugOverlay = true;
    private string debugHits = "";

    [Tooltip("自転車レーンの有無を判定する範囲。車道の反対端からでも隣接する自転車レーンを検知できるよう、sensorRadiusより広めに設定する")]
    public float bikeLaneCheckRadius = 6f;

    [Tooltip("路上駐車車両を避けるための歩道通行許可を判定する範囲")]
    public float parkedCarCheckRadius = 5f;

    public RoadAreaType currentArea = RoadAreaType.None;
    public RoadSide currentSide = RoadSide.None;
    public bool bikeLaneExistsNearby = false;
    public bool parkedCarNearby = false;
    public bool sidewalkRidingAllowed = false;

    // 現在いるレーンの進行方向（自転車の向きに近い側へ揃えたもの）。テレポート時の向き補正に使う
    public Vector3 currentLaneForward = Vector3.zero;

    // 現在いる道路区間（RoadSection のルート）
    public Transform currentRoadSection;

    [Tooltip("この速さ(m/s)未満のときは進行方向が不安定なため、直前に判定した進行方向をそのまま使う")]
    public float minSpeedForDirection = 0.5f;

    [Tooltip("道路の向きに対してこの角度[度]以上向きを変えたときだけ逆向き走行とみなす。交差点で信号待ちのために横を向いても逆走扱いにしないため、90度より大きくする")]
    [Range(90f, 180f)] public float reverseAngleThreshold = 135f;

    private Rigidbody rb;

    // 道路ごとに「道路の基準方向と逆向きに走っているか」を記憶する（ヒステリシス用）
    private readonly System.Collections.Generic.Dictionary<Transform, bool> reversedByCenterLine = new System.Collections.Generic.Dictionary<Transform, bool>();
    private readonly System.Collections.Generic.HashSet<Transform> touchedCenterLines = new System.Collections.Generic.HashSet<Transform>();
    private readonly System.Collections.Generic.List<Transform> staleCenterLines = new System.Collections.Generic.List<Transform>();
    private Vector3 lastMovingDirection = Vector3.forward;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        SyncRoadAreasWithVisual();
    }

    // 自転車道を外した道路では見た目の車道(Visual/Road_L,R)だけ幅が広げられ、
    // 判定用の箱(Areas/*/Aria_Road_L,R)が狭いまま残っていることがあるため、起動時に見た目へ合わせる
    static void SyncRoadAreasWithVisual()
    {
        foreach (Collider area in FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            bool isLeft = area.CompareTag("Road_L");
            if (!isLeft && !area.CompareTag("Road_R")) continue;

            Transform areas = area.transform.parent != null ? area.transform.parent.parent : null;
            if (areas == null || areas.name != "Areas" || areas.parent == null) continue;

            Transform visual = areas.parent.Find("Visual");
            Transform visualRoad = visual != null ? visual.Find(isLeft ? "Road_L" : "Road_R") : null;
            if (visualRoad == null) continue;

            Transform t = area.transform;
            t.localPosition = new Vector3(visualRoad.localPosition.x, t.localPosition.y, t.localPosition.z);
            t.localScale = new Vector3(visualRoad.localScale.x, t.localScale.y, t.localScale.z);
        }
    }

    void FixedUpdate()
    {
        UpdateMovingDirection();

        Collider[] hits = Physics.OverlapSphere(transform.position, sensorRadius, ~0, QueryTriggerInteraction.Collide);
        RoadAreaType detectedArea = RoadAreaType.None;
        RoadSide detectedSide = RoadSide.None;
        bool detectedRidingAllowed = false;
        Vector3 detectedLaneForward = Vector3.zero;
        Transform detectedRoadSection = null;
        float closestDistance = float.MaxValue;
        System.Text.StringBuilder debugBuilder = showDebugOverlay ? new System.Text.StringBuilder() : null;

        foreach (Collider hit in hits)
        {
            RoadAreaType type = GetAreaType(hit, out RoadSide side);
            if (type == RoadAreaType.None) continue;

            // 高さの差を含めると、路面より高い歩道の判定箱の方が近くなり、
            // 車道の端を走っていても歩道判定になるため、水平距離で比べる。
            // 水平距離が同じ（両方の範囲内）なら、上下の差が小さい方を優先する
            Vector3 position = transform.position;
            Vector3 probe = new Vector3(position.x, hit.bounds.center.y, position.z);
            Vector3 closest = hit.ClosestPoint(probe);
            float flatDistance = new Vector2(closest.x - probe.x, closest.z - probe.z).magnitude;
            float verticalDistance = Mathf.Abs(position.y - Mathf.Clamp(position.y, hit.bounds.min.y, hit.bounds.max.y));
            float distance = flatDistance * 100f + verticalDistance;
            debugBuilder?.AppendLine($"  {hit.name} ({hit.transform.root.name}) {type}/{side} 水平{flatDistance:F2} 上下{verticalDistance:F2}");
            if (distance < closestDistance)
            {
                closestDistance = distance;
                detectedArea = type;
                detectedSide = side;
                detectedRidingAllowed = type == RoadAreaType.Sidewalk && hit.GetComponent<SidewalkRidingAllowed>() != null;
                detectedLaneForward = GetLaneForward(hit.transform);
                detectedRoadSection = GetRoadSectionRoot(hit.transform);
            }
        }

        currentArea = detectedArea;
        currentSide = detectedSide;
        sidewalkRidingAllowed = detectedRidingAllowed;
        currentLaneForward = detectedLaneForward;
        currentRoadSection = detectedRoadSection;
        if (debugBuilder != null) debugHits = debugBuilder.ToString();

        // センサー範囲から外れた道路の向き記憶は捨てる（次に入った時は改めて判定する）
        staleCenterLines.Clear();
        foreach (Transform key in reversedByCenterLine.Keys)
        {
            if (!touchedCenterLines.Contains(key)) staleCenterLines.Add(key);
        }
        foreach (Transform key in staleCenterLines) reversedByCenterLine.Remove(key);
        touchedCenterLines.Clear();

        bikeLaneExistsNearby = DetectBikeLaneNearby();
        parkedCarNearby = DetectParkedCarNearby();
    }

    void OnGUI()
    {
        if (!showDebugOverlay) return;

        string text = $"判定: {currentArea} / {currentSide}  自転車道近く:{bikeLaneExistsNearby}  駐車近く:{parkedCarNearby}\n位置: {transform.position}\n{debugHits}";
        GUIStyle style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 16 };
        GUI.Box(new Rect(10, Screen.height - 230, 720, 220), text, style);
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

    Vector3 GetLaneForward(Transform hitTransform)
    {
        Transform centerLine = FindCenterLine(hitTransform);
        if (centerLine == null) return Vector3.zero;

        Vector3 f = centerLine.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) return Vector3.zero;
        f.Normalize();

        // 自転車の向きではなく、レーンの位置から「逆走にならない向き」を返す。
        // 左側通行なので、中央線の左側にあるレーンは道路の基準方向、右側にあるレーンはその逆向きが正しい。
        // （向きに合わせると、逆を向いた状態で記録した位置へ戻った時に逆走方向へ矯正されてしまう）
        Vector3 right = centerLine.right;
        right.y = 0f;
        Vector3 toLane = hitTransform.position - centerLine.position;
        toLane.y = 0f;
        bool onLeftHalf = Vector3.Dot(toLane, right) < 0f;
        return onLeftHalf ? f : -f;
    }

    // 向きが道路方向から reverseAngleThreshold 以上ずれたら逆向き、(180 - しきい値)以内に戻ったら順向きに切り替える。
    // その間（真横を向いている状態など）は直前の判定を維持する
    bool IsReversed(Transform centerLine, Vector3 roadForward)
    {
        float angle = Vector3.Angle(FacingDirection(), roadForward);

        if (!reversedByCenterLine.TryGetValue(centerLine, out bool reversed))
        {
            reversed = angle > 90f;
        }
        else if (angle >= reverseAngleThreshold)
        {
            reversed = true;
        }
        else if (angle <= 180f - reverseAngleThreshold)
        {
            reversed = false;
        }

        reversedByCenterLine[centerLine] = reversed;
        touchedCenterLines.Add(centerLine);
        return reversed;
    }

    // 衝突で押し戻された時など、一時的に後ろへ動いただけで左右が反転しないよう、
    // 速度ではなく車体の向きを進行方向として使う
    Vector3 FacingDirection()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : lastMovingDirection;
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
            if (IsReversed(centerLine, roadForward.normalized))
            {
                side = (side == RoadSide.Right) ? RoadSide.Left : RoadSide.Right;
            }
        }

        return side;
    }

    // hitTransform: <RoadSectionRoot>/Areas/Left(or Right)/Aria_XXX_L(or R)
    public static Transform GetRoadSectionRoot(Transform hitTransform)
    {
        Transform areas = hitTransform.parent != null ? hitTransform.parent.parent : null;
        if (areas == null || areas.name != "Areas") return null;
        return areas.parent;
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