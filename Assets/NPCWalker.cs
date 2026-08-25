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



    public void SetTrafficStop(bool stop)
    {
        isTrafficStopped = stop;
        // 停止中は kinematic にして他の歩行者に押されないようにする
        if (rb != null)
        {
            rb.isKinematic = stop;
        }
    }

    public void SetCrossing(bool crossing)
    {
        isCrossing = crossing;
    }

    public void SetDirection(Vector3 direction)
    {
        if (isHit) return;

        targetDirection = SnapToCardinal(direction);  // ← ここを変更
        currentMoveDirection = targetDirection;

        if (targetDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(targetDirection);
        }
    }

    /// <summary>方向を最も近い東西南北にスナップします</summary>
    Vector3 SnapToCardinal(Vector3 dir)
    {
        if (dir == Vector3.zero) return dir;

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.z))
            return dir.x >= 0 ? Vector3.right : Vector3.left;    // 東 or 西
        else
            return dir.z >= 0 ? Vector3.forward : Vector3.back;  // 北 or 南
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        // 歩行者同士の物理衝突を無効化（すり抜ける）
        // AvoidOtherNPCs() のレイキャスト回避は引き続き動作する
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
        Vector3 moveVelocity = (currentMoveDirection * moveSpeed) + sidewalkCorrection;
        rb.linearVelocity = new Vector3(moveVelocity.x, rb.linearVelocity.y, moveVelocity.z);
    }

    Vector3 ComputeSidewalkCorrection()
    {
        if (isCrossing) return Vector3.zero;
        // 周囲の歩道コライダーを探す
        Collider[] hits = Physics.OverlapSphere(
            transform.position, sidewalkSearchRadius, ~0, QueryTriggerInteraction.Collide);
        Collider nearestSidewalk = null;
        float nearestDist = float.MaxValue;
        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Sidewalk_L") && !hit.CompareTag("Sidewalk_R")) continue;
            // 水平距離だけで判定（Y軸の誤差を無視）
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
        // 歩道が見つからない → 補正不能
        if (nearestSidewalk == null) return Vector3.zero;
        // 歩道の上にいる → 補正不要
        if (nearestDist < onPathSensorRadius) return Vector3.zero;
        // 歩道からずれている → 最寄りの歩道へ補正
        Vector3 direction = nearestSidewalk.ClosestPoint(transform.position) - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return Vector3.zero;
        Debug.DrawRay(transform.position, direction.normalized * 2f, Color.magenta);
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

    // ── OnCollisionEnter に歩行者同士の衝突無視を追加 ──
    private void OnCollisionEnter(Collision collision)
    {
        // 歩行者同士はすり抜けるよう動的に設定（後からスポーンした歩行者対策）
        NPCWalker otherWalker = collision.gameObject.GetComponent<NPCWalker>();
        if (otherWalker != null)
        {
            Physics.IgnoreCollision(GetComponent<Collider>(), collision.collider);
            return;
        }
        // 自転車との衝突（既存のまま）
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