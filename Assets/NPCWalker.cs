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
    public float birthSafetyTime = 0.5f;
    private float ageTimer = 0f;

    [Header("歩道の軌道修正")]
    public float onPathSensorRadius = 0.6f;
    public float sidewalkSearchRadius = 5f;
    public float sidewalkCorrectionSpeed = 2f;

    private bool isAtIntersection = false;
    public bool IsAtIntersection => isAtIntersection;

    public void SetAtIntersection(bool value)
    {
        isAtIntersection = value;
    }

    public void SetDirection(Vector3 direction)
    {
        if (isHit) return; 

        targetDirection = SnapToCardinal(direction);
        currentMoveDirection = targetDirection; 
        
        if (targetDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(targetDirection);
        }
    }

    public void SetTrafficStop(bool stop)
    {
        isTrafficStopped = stop;

        if (rb != null)
        {
            rb.isKinematic = stop;
        }
    }

    public void SetCrossing(bool crossing)
    {
        isCrossing = crossing;
    }

    public void SnapAcrossPath(Vector3 nodePosition, Vector3 travelDirection, Vector3 newDirection, float maxOffset = 0.4f)
    {
        Vector3 newPos = transform.position;
        if (Mathf.Abs(travelDirection.x) > Mathf.Abs(travelDirection.z))
        {
            newPos.x = nodePosition.x;  // 東西に進んでいる → Xをそろえる
        }
        else
        {
            newPos.z = nodePosition.z;  // 南北に進んでいる → Zをそろえる
        }
        Vector3 perpendicular = Vector3.Cross(Vector3.up, newDirection).normalized;
        float offset = Random.Range(-maxOffset, maxOffset);
        newPos += perpendicular * offset;
        transform.position = newPos;
    }

    Vector3 SnapToCardinal(Vector3 dir)
    {
        if (dir == Vector3.zero) return dir;

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.z))
            return dir.x >= 0 ? Vector3.right : Vector3.left;
        else
            return dir.z >= 0 ? Vector3.forward : Vector3.back;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false; 
        rb.useGravity = true;

        // 歩行者同士の物理衝突を無効化
        Collider myCol = GetComponent<Collider>();
        if (myCol != null)
        {
            NPCWalker[] allWalkers = FindObjectsByType<NPCWalker>(FindObjectsSortMode.None);
            foreach (NPCWalker other in allWalkers)
            {
                if (other == this) continue;
                Collider otherCol = other.GetComponent<Collider>();
                if (otherCol != null)
                    Physics.IgnoreCollision(myCol, otherCol);
            }
        }
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
        Vector3 intendedVelocity = (currentMoveDirection * moveSpeed) + sidewalkCorrection;
        Vector3 allowedVelocity = ClampToSidewalk(intendedVelocity);

        rb.linearVelocity = new Vector3(allowedVelocity.x, rb.linearVelocity.y, allowedVelocity.z);
    }

    Vector3 ClampToSidewalk(Vector3 intendedVelocity)
    {
        if (isCrossing) return intendedVelocity;

        // 今すでに歩道の外にいるなら、移動を制限しない（補正で戻れるように）
        if (!IsOnSidewalk(transform.position)) return intendedVelocity;

        // 歩道の上にいる → 歩道から出ないように制限
        Vector3 fullMove = intendedVelocity * Time.fixedDeltaTime;
        Vector3 fullPos = transform.position + fullMove;

        if (IsOnSidewalk(fullPos)) return intendedVelocity;

        Vector3 xOnlyPos = transform.position + new Vector3(fullMove.x, 0f, 0f);
        Vector3 zOnlyPos = transform.position + new Vector3(0f, 0f, fullMove.z);

        Vector3 allowed = Vector3.zero;
        if (IsOnSidewalk(xOnlyPos)) allowed.x = intendedVelocity.x;
        if (IsOnSidewalk(zOnlyPos)) allowed.z = intendedVelocity.z;

        return allowed;
    }

    bool IsOnSidewalk(Vector3 position)
    {
        Collider[] hits = Physics.OverlapSphere(position, onPathSensorRadius, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Sidewalk_L") || hit.CompareTag("Sidewalk_R"))
            {
                return true;
            }
        }

        return false;
    }

    Vector3 ComputeSidewalkCorrection()
    {
        if (isCrossing) return Vector3.zero;

        Collider[] hits = Physics.OverlapSphere(
            transform.position, sidewalkSearchRadius, ~0, QueryTriggerInteraction.Collide);

        Collider nearestSidewalk = null;
        float nearestDist = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Sidewalk_L") && !hit.CompareTag("Sidewalk_R")) continue;

            Vector3 closest = hit.ClosestPoint(transform.position);
            float dist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(closest.x, closest.z));

            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestSidewalk = hit;
            }
        }

        if (nearestSidewalk == null) return Vector3.zero;

        // 歩道の上にいる → 補正不要
        if (nearestDist < onPathSensorRadius) return Vector3.zero;

        // 歩道からずれている → 最寄りの歩道へ補正
        Vector3 direction = nearestSidewalk.ClosestPoint(transform.position) - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return Vector3.zero;
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
        // 歩行者同士はすり抜ける（後からスポーンした歩行者対策）
        NPCWalker otherWalker = collision.gameObject.GetComponent<NPCWalker>();
        if (otherWalker != null)
        {
            Physics.IgnoreCollision(GetComponent<Collider>(), collision.collider);
            return;
        }

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