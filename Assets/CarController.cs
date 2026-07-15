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
    public float groundCheckDistance = 3f;
    public float gravity = -20f;

    Rigidbody rb;
    Vector3 targetDirection;
    float currentSpeed;
    float verticalVelocity;

    public void SetDirection(Vector3 direction)
    {
        targetDirection = direction.normalized;
        if (targetDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(targetDirection);
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        targetDirection = transform.forward;
    }

    void FixedUpdate()
    {
        Quaternion lookRot = Quaternion.LookRotation(targetDirection);
        Quaternion newRot = Quaternion.Slerp(transform.rotation, lookRot, Time.fixedDeltaTime * turnLerpSpeed);

        float target = HasObstacleAhead() ? 0f : moveSpeed;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, acceleration * Time.fixedDeltaTime);

        Vector3 forwardMove = transform.forward * currentSpeed;
        Vector3 lateralMove = ComputeLaneCorrection();
        Vector3 horizontalMove = (forwardMove + lateralMove) * Time.fixedDeltaTime;

        Vector3 nextPosition = rb.position + horizontalMove;

        Vector3 groundOrigin = rb.position + Vector3.up * 3f;
        bool grounded = false;
        RaycastHit[] groundHits = Physics.RaycastAll(groundOrigin, Vector3.down, groundCheckDistance + 3f, ~0, QueryTriggerInteraction.Ignore);
        float closestDistance = float.MaxValue;
        float groundY = 0f;

        foreach (RaycastHit groundHit in groundHits)
        {
            if (groundHit.collider.transform.IsChildOf(transform)) continue;
            if (groundHit.distance < closestDistance)
            {
                closestDistance = groundHit.distance;
                groundY = groundHit.point.y;
                grounded = true;
            }
        }

        if (grounded)
        {
            verticalVelocity = 0f;
            nextPosition.y = groundY;
        }
        else
        {
            verticalVelocity += gravity * Time.fixedDeltaTime;
            nextPosition.y += verticalVelocity * Time.fixedDeltaTime;
        }

        rb.MoveRotation(newRot);
        rb.MovePosition(nextPosition);
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
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        RaycastHit hit;

        if (Physics.SphereCast(origin, obstacleCheckRadius, transform.forward, out hit, obstacleCheckDistance, ~0, QueryTriggerInteraction.Ignore))
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