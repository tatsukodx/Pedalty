using UnityEngine;

public class CarController : MonoBehaviour
{
    public float moveSpeed = 8f;
    public float acceleration = 6f;
    public float turnLerpSpeed = 4f;
    public float laneCorrectionSpeed = 4f;
    public float laneSensorRadius = 1f;
    public float obstacleCheckDistance = 6f; // 最低確保する検知距離（低速時の下限として使用）
    public float obstacleCheckRadius = 1.2f;
    public float mass = 1000f;

    [Header("車体・停止余裕の設定")]
    public float vehicleLength = 6f;      // Cars.prefab の BoxCollider.size.z と合わせる
    public float brakingSafetyBuffer = 2f; // 制動距離に追加で確保する余裕（m）

    bool isLightStopped = false;
    bool isYieldStopped = false;
    bool isPedestrianStopped = false;

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

    public void SetPedestrianStop(bool stop)
    {
        isPedestrianStopped = stop;
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

        float target = (HasObstacleAhead() || isLightStopped || isYieldStopped || isPedestrianStopped) ? 0f : moveSpeed;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, acceleration * Time.fixedDeltaTime);

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
        // 中心ではなく前バンパー付近を起点にする
        float frontOffset = vehicleLength * 0.5f;
        Vector3 origin = transform.position + Vector3.up * 0.5f + transform.forward * frontOffset;

        // 現在速度から物理的に必要な制動距離 + 余裕分を動的に確保する
        float brakingDistance = (currentSpeed * currentSpeed) / (2f * Mathf.Max(acceleration, 0.01f));
        float checkDistance = Mathf.Max(obstacleCheckDistance, brakingDistance + brakingSafetyBuffer);

        RaycastHit hit;

        if (Physics.SphereCast(origin, obstacleCheckRadius, transform.forward, out hit, checkDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform)) return false;
            if (hit.collider.GetComponentInParent<CarController>() != null) return true;
            if (hit.collider.GetComponentInParent<BicycleController>() != null) return true;
            if (hit.collider.GetComponentInParent<NPCWalker>() != null) return true;
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