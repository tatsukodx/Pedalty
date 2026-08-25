using UnityEngine;

public class NPCWalker : MonoBehaviour
{
    public float moveSpeed = 2f;
    private Vector3 targetDirection; 
    private Vector3 currentMoveDirection; 
    private Rigidbody rb;
    private bool isHit = false; 
    private bool isTrafficStopped = false;
    private bool isCrossing = false;

    [Header("避けるための設定")]
    public float sensorDistance = 1.5f; 
    public float avoidForce = 0.5f;    

    [Header("安全設定")]
    public float birthSafetyTime = 0.5f; // 見た目の差し替えが終わるまで動かさない時間
    private float ageTimer = 0f;

    [Header("歩道の軌道修正")]
    public float onPathSensorRadius = 0.6f;
    public float sidewalkSearchRadius = 5f;
    public float sidewalkCorrectionSpeed = 2f;

    public void SetDirection(Vector3 direction)
    {
        if (isHit) return; 

        targetDirection = direction.normalized;
        currentMoveDirection = targetDirection; 
        
        if (targetDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(targetDirection);
        }
    }

    public void SetTrafficStop(bool stop)
    {
        isTrafficStopped = stop;
    }

    public void SetCrossing(bool crossing)
    {
        isCrossing = crossing;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false; 
        rb.useGravity = true;
    }

    void FixedUpdate()
    {
        if (isHit) return;

        ageTimer += Time.fixedDeltaTime;

        if (ageTimer < birthSafetyTime)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        if (isTrafficStopped)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        AvoidOtherNPCs();

        if (currentMoveDirection != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentMoveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 5f);
        }

        Vector3 sidewalkCorrection = ComputeSidewalkCorrection();
        Vector3 moveVelocity = (currentMoveDirection * moveSpeed) + sidewalkCorrection;
        rb.linearVelocity = new Vector3(moveVelocity.x, rb.linearVelocity.y, moveVelocity.z);
    }

    Vector3 ComputeSidewalkCorrection()
    {
        if (isCrossing) return Vector3.zero;

        Collider[] nearHits = Physics.OverlapSphere(transform.position, onPathSensorRadius, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider hit in nearHits)
        {
            if (hit.CompareTag("Sidewalk_L") || hit.CompareTag("Sidewalk_R") ||
                hit.CompareTag("Road_L") || hit.CompareTag("Road_R"))
            {
                return Vector3.zero;
            }
        }

        Collider[] wideHits = Physics.OverlapSphere(transform.position, sidewalkSearchRadius, ~0, QueryTriggerInteraction.Collide);
        Collider nearestSidewalk = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in wideHits)
        {
            if (!hit.CompareTag("Sidewalk_L") && !hit.CompareTag("Sidewalk_R")) continue;

            float distance = Vector3.Distance(transform.position, hit.ClosestPoint(transform.position));
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestSidewalk = hit;
            }
        }

        if (nearestSidewalk == null) return Vector3.zero;

        Vector3 direction = nearestSidewalk.ClosestPoint(transform.position) - transform.position;
        direction.y = 0f;

        if (direction == Vector3.zero) return Vector3.zero;
        return direction.normalized * sidewalkCorrectionSpeed;
    }

    void AvoidOtherNPCs()
    {
        currentMoveDirection = targetDirection;

        RaycastHit hit;
        if (Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f, transform.forward, out hit, sensorDistance))
        {
            NPCWalker otherNPC = hit.collider.GetComponent<NPCWalker>();
            
            if (otherNPC != null && otherNPC != this && !otherNPC.isHit)
            {
                Vector3 relativePos = transform.InverseTransformPoint(hit.collider.transform.position);
                Vector3 avoidDir = transform.right;
                if (relativePos.x > 0)
                {
                    avoidDir = -transform.right; 
                }
                currentMoveDirection = (targetDirection + avoidDir * avoidForce).normalized;
            }
        }
    }

    void Update()
    {
        if (transform.position.y < -10f)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<BicycleController>() != null)
        {
            if (!isHit)
            {
                isHit = true; 
                rb.constraints = RigidbodyConstraints.None;

                Rigidbody bikeRb = collision.gameObject.GetComponent<Rigidbody>();
                if (bikeRb != null)
                {
                    Vector3 flyDirection = bikeRb.linearVelocity;
                    flyDirection.y = Mathf.Max(flyDirection.y, 5f); 
                    rb.AddForce(flyDirection * 2.0f, ForceMode.Impulse);
                }

                Destroy(gameObject, 3f);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Respawn") || (other.transform.parent != null && other.transform.parent.name == "DeadZone"))
        {
            Destroy(gameObject);
        }
    }
}