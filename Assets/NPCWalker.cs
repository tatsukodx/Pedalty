using UnityEngine;

public class NPCWalker : MonoBehaviour
{
    public float moveSpeed = 2f; 
    private Vector3 targetDirection; 
    private Vector3 currentMoveDirection; 
    private Rigidbody rb;
    private bool isHit = false; 

    [Header("避けるための設定")]
    public float sensorDistance = 1.5f; 
    public float avoidForce = 0.5f;    

    [Header("安全設定")]
    public float birthSafetyTime = 0.5f; // すり替え処理が完了するのを待つための短い安全時間
    private float ageTimer = 0f;

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

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false; 
        rb.useGravity = true;
    }

    void FixedUpdate()
    {
        if (isHit) return;

        // 生まれてからの時間をカウント
        ageTimer += Time.fixedDeltaTime;

        // 見た目のすり替え（1フレーム待機など）が終わるまではその場で少し待つ
        if (ageTimer < birthSafetyTime)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        // 前方のNPCを避ける処理
        AvoidOtherNPCs();

        // 進行方向に向かってスムーズに向きを変える
        if (currentMoveDirection != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentMoveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 5f);
        }

        // 移動速度を計算して適用
        Vector3 moveVelocity = currentMoveDirection * moveSpeed;
        rb.linearVelocity = new Vector3(moveVelocity.x, rb.linearVelocity.y, moveVelocity.z);
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
        // マップ外に落ちた時の即消滅判定
        if (transform.position.y < -10f)
        {
            Destroy(gameObject);
        }
    }

    // 自転車にぶつかった時の処理
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

                // 3秒後に消滅
                Destroy(gameObject, 3f);
            }
        }
    }

    // DeadZoneに触れた時の消滅処理
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Respawn") || (other.transform.parent != null && other.transform.parent.name == "DeadZone"))
        {
            Destroy(gameObject);
        }
    }
}