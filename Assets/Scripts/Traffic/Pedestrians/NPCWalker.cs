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
    public bool IsCrossing => isCrossing;

    [Header("車と衝突した際の吹っ飛び倍率")]
    public float carHitForceMultiplier = 2.0f;

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

    [Header("旋回")]
    [Tooltip("曲がるときの旋回の速さ。大きいほど小回りが利く")]
    public float turnSpeed = 6f;
    [Tooltip("この角度以上の方向転換は、その場で止まってから回る")]
    public float turnInPlaceAngle = 150f;
    [Tooltip("その場で回るときの回転の速さ（度/秒）")]
    public float turnInPlaceSpeed = 180f;

    private bool isTurningInPlace = false;

    public void SetDirection(Vector3 direction)
    {
        if (isHit) return; 

        Vector3 newDirection = SnapToCardinal(direction);
        if (newDirection == Vector3.zero) return;

        // 真後ろへの転換など大きく向きを変える場合は、
        // 歩きながら曲がると歩道からはみ出すため、その場で止まってから回る
        float angle = Vector3.Angle(transform.forward, newDirection);
        isTurningInPlace = angle >= turnInPlaceAngle;

        targetDirection = newDirection;
        currentMoveDirection = targetDirection;
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

    [Header("進路の矯正")]
    [Tooltip("矯正時に目標位置へ寄せる速さ（1秒あたりのユニット数）")]
    public float snapCorrectionSpeed = 3f;
    [Tooltip("この距離まで近づいたら矯正完了とみなす")]
    public float snapArriveThreshold = 0.05f;

    private bool isSnapping = false;
    private Vector3 snapTargetPosition;

    public bool IsSnapping => isSnapping;

    public void BeginSnapTo(Vector3 targetPosition)
    {
        snapTargetPosition = targetPosition;
        isSnapping = true;
    }

    public void SnapAcrossPath(Vector3 nodePosition, Vector3 travelDirection, Vector3 newDirection, float maxOffset = 0.4f)
    {
        Vector3 newPos = transform.position;
        if (Mathf.Abs(travelDirection.x) > Mathf.Abs(travelDirection.z))
        {
            newPos.x = nodePosition.x;
        }
        else
        {
            newPos.z = nodePosition.z;
        }
        Vector3 perpendicular = Vector3.Cross(Vector3.up, newDirection).normalized;
        float offset = Random.Range(-maxOffset, maxOffset);
        newPos += perpendicular * offset;

        // 位置を直接書き換えると瞬間移動して見えるため、
        // 目標だけ決めてFixedUpdateで少しずつ寄せる
        snapTargetPosition = newPos;
        isSnapping = true;
    }

    Vector3 ComputeSnapCorrectionVelocity()
    {
        if (!isSnapping) return Vector3.zero;

        Vector3 diff = snapTargetPosition - transform.position;
        diff.y = 0f;

        // 進行方向の成分は取り除き、横方向のズレだけを埋める。
        // そうしないと、歩き進むほど目標が後方に取り残されて引き戻されてしまう。
        Vector3 facing = transform.forward;
        facing.y = 0f;

        if (facing.sqrMagnitude > 0.0001f)
        {
            diff -= Vector3.Project(diff, facing.normalized);
        }

        if (diff.magnitude <= snapArriveThreshold)
        {
            isSnapping = false;
            return Vector3.zero;
        }

        // 目標へ向かう速度を返す。位置を直接書き換えないので瞬間移動にならない。
        Vector3 step = Vector3.ClampMagnitude(diff, snapCorrectionSpeed * Time.fixedDeltaTime);
        return step / Time.fixedDeltaTime;
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

        currentMoveDirection = targetDirection;

        if (currentMoveDirection == Vector3.zero)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        Quaternion targetRot = Quaternion.LookRotation(currentMoveDirection);

        if (isTurningInPlace)
        {
            // その場で止まって回りきるまで移動しない
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnInPlaceSpeed * Time.fixedDeltaTime);

            if (Quaternion.Angle(transform.rotation, targetRot) > 1f)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                return;
            }

            isTurningInPlace = false;
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * turnSpeed);
        }

        // 実際に向いている方向へ進むことで、旋回中は弧を描いて曲がる
        Vector3 moveDirection = transform.forward;
        moveDirection.y = 0f;
        moveDirection.Normalize();

        Vector3 sidewalkCorrection = ComputeSidewalkCorrection();
        Vector3 snapCorrection = ComputeSnapCorrectionVelocity();
        Vector3 intendedVelocity = (moveDirection * moveSpeed) + sidewalkCorrection + snapCorrection;
        Vector3 allowedVelocity = ClampToSidewalk(intendedVelocity);

        rb.linearVelocity = new Vector3(allowedVelocity.x, rb.linearVelocity.y, allowedVelocity.z);
    }

    Vector3 ClampToSidewalk(Vector3 intendedVelocity)
    {
        if (isCrossing) return intendedVelocity;
        if (isSnapping) return intendedVelocity;

        if (!IsOnSidewalk(transform.position)) return intendedVelocity;

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
        if (isSnapping) return Vector3.zero;

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

        if (nearestDist < onPathSensorRadius) return Vector3.zero;

        Vector3 direction = nearestSidewalk.ClosestPoint(transform.position) - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return Vector3.zero;
        return direction.normalized * sidewalkCorrectionSpeed;
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
                rb.isKinematic = false;
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
            return;
        }

        if (collision.gameObject.GetComponent<CarController>() != null)
        {
            if (!isHit)
            {
                isHit = true;
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.None;

                Rigidbody carRb = collision.gameObject.GetComponent<Rigidbody>();
                if (carRb != null)
                {
                    Vector3 flyDirection = carRb.linearVelocity;
                    flyDirection.y = Mathf.Max(flyDirection.y, 5f);
                    rb.AddForce(flyDirection * carHitForceMultiplier, ForceMode.Impulse);
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