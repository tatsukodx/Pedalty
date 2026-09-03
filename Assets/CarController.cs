using UnityEngine;

public class CarController : MonoBehaviour
{
    public float moveSpeed = 8f;
    public float acceleration = 6f;
    public float turnLerpSpeed = 4f;
    public float laneCorrectionSpeed = 4f;
    public float laneSensorRadius = 1f;
    public float obstacleCheckDistance = 6f;
    public float obstacleCheckRadius = 1.2f;
    public float mass = 1000f;

    [Header("車体・停止余裕の設定")]
    public float vehicleLength = 6f;
    public float brakingSafetyBuffer = 2f;

    [Header("計画的な停止（信号・譲り合い・歩行者待ち）専用の減速度")]
    [Tooltip("あらかじめ止まるとわかっている場合は、障害物回避より強めにブレーキをかけて、手前で止まれるようにする")]
    public float voluntaryStopDeceleration = 14f;

    [Header("左折時に歩行者を待つ場合の減速度")]
    [Tooltip("左折は奥の横断歩道（対向側）を確認するため、通常の歩行者待ちよりさらに強めにブレーキをかけて、より手前で停止させる")]
    public float leftTurnPedestrianStopDeceleration = 22f;

    bool isLightStopped = false;
    bool isYieldStopped = false;
    bool isPedestrianStopped = false;
    bool isLeftTurnPedestrianStop = false;

    Rigidbody rb;
    Vector3 targetDirection;
    float currentSpeed;

    public void SetDirection(Vector3 direction)
    {
        targetDirection = direction.normalized;

        if (targetDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(targetDirection);
        }
    }

    public void SetTrafficStop(bool stop)
    {
        isLightStopped = stop;
    }

    public void SetYieldStop(bool stop)
    {
        isYieldStopped = stop;
    }

    public void SetPedestrianStop(bool stop, bool isLeftTurn = false)
    {
        isPedestrianStopped = stop;
        isLeftTurnPedestrianStop = stop && isLeftTurn;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = mass;
        targetDirection = transform.forward;
    }

    void FixedUpdate()
    {

        Quaternion lookRot = Quaternion.LookRotation(targetDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.fixedDeltaTime * turnLerpSpeed);

        bool obstacleAhead = HasObstacleAhead();
        bool voluntaryStop = isLightStopped || isYieldStopped || isPedestrianStopped;

        float target = (obstacleAhead || voluntaryStop) ? 0f : moveSpeed;

        // 障害物回避は物理的な制動距離を確保した緩やかな減速、
        // 信号待ち・譲り合い・歩行者待ちは、あらかじめ分かっている停止なので強めに減速して手前で止める
        // 左折時の歩行者待ちは、奥の横断歩道を見て判断するため、通常よりさらに強く減速してより手前で止める
        float voluntaryDecel = isLeftTurnPedestrianStop ? leftTurnPedestrianStopDeceleration : voluntaryStopDeceleration;
        float decelRate = (!obstacleAhead && voluntaryStop) ? voluntaryDecel : acceleration;

        currentSpeed = Mathf.MoveTowards(currentSpeed, target, decelRate * Time.fixedDeltaTime);

        Vector3 forwardVel = transform.forward * currentSpeed;
        Vector3 lateralVel = ComputeLaneCorrection();
        Vector3 totalVel = forwardVel + lateralVel;

        rb.linearVelocity = new Vector3(totalVel.x, rb.linearVelocity.y, totalVel.z);
        rb.angularVelocity = Vector3.zero;
    }

    Vector3 ComputeLaneCorrection()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, laneSensorRadius, ~0, QueryTriggerInteraction.Collide);
        Vector3 correction = Vector3.zero;
        bool onCorrectSideRoad = false;
        bool onWrongSideRoad = false;

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("BIkeLane_L") || hit.CompareTag("Sidewalk_L"))

            {
                correction += transform.right;
            }
            else if (hit.CompareTag("BikeLane_R") || hit.CompareTag("Sidewalk_R"))
            {
                correction -= transform.right;
            }
            else if (hit.CompareTag("Road_L") || hit.CompareTag("Road_R"))
            {
                bool travelingWithRoadForward = Vector3.Dot(transform.forward, hit.transform.forward) >= 0f;
                bool isLeftTag = hit.CompareTag("Road_L");
                bool correctLane = travelingWithRoadForward ? isLeftTag : !isLeftTag;

                if (correctLane)
                {
                    onCorrectSideRoad = true;
                }
                else
                {
                    onWrongSideRoad = true;
                }
            }
        }

        if (onWrongSideRoad && !onCorrectSideRoad)
        {
            correction -= transform.right;
        }

        if (correction == Vector3.zero) return Vector3.zero;
        return correction.normalized * laneCorrectionSpeed;
    }


    bool HasObstacleAhead()
    {
        float frontOffset = vehicleLength * 0.5f;
        Vector3 origin = transform.position + Vector3.up * 0.5f + transform.forward * frontOffset;

        float brakingDistance = (currentSpeed * currentSpeed) / (2f * Mathf.Max(acceleration, 0.01f));
        float checkDistance = Mathf.Max(obstacleCheckDistance, brakingDistance + brakingSafetyBuffer);

        // 旧実装は SphereCast（最も手前のヒット1件のみ）を使っていたため、
        // 交差点付近にある無関係なコライダー（縁石・標識・停止線マーカーなど）に
        // 一番手前で当たると、その奥で歩行者待ち・信号待ちをしている車を
        // 検知できずに素通りしてしまうバグがあった。
        // SphereCastAll で経路上の全ヒットを手前から順に調べ、
        // 無関係な物はすり抜けて、車・自転車・歩行者が見つかった時点で
        // 「障害物あり」と判定するように修正。
        RaycastHit[] hits = Physics.SphereCastAll(origin, obstacleCheckRadius, transform.forward, checkDistance, ~0, QueryTriggerInteraction.Ignore);

        if (hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue; // 自分自身は無視して奥を確認
                if (hit.collider.GetComponentInParent<CarController>() != null) return true;
                if (hit.collider.GetComponentInParent<BicycleController>() != null) return true;
                if (hit.collider.GetComponentInParent<NPCWalker>() != null) return true;
                // 関係のない物体（縁石・看板など）はここでは判定せず、次のヒットを確認する
            }
        }

        return false;
    }

    void Update()
    {
        if (transform.position.y < -10f)
        {
            Destroy(gameObject);
        }
    }
}