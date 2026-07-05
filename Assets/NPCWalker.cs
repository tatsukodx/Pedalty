using UnityEngine;

public class NPCWalker : MonoBehaviour
{
    public float moveSpeed = 2f; 
    private Vector3 targetDirection; 
    private Rigidbody rb;
    private bool isHit = false; 

    // 外部（Spawnerや交差点）から進む方向を指示する関数
    public void SetDirection(Vector3 direction)
    {
        if (isHit) return; // 吹っ飛んでいる時は無視

        targetDirection = direction.normalized;
        
        if (targetDirection != Vector3.zero)
        {
            // パッと一瞬で向くのではなく、少しスムーズに向きを変える
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

        Vector3 moveVelocity = targetDirection * moveSpeed;
        rb.linearVelocity = new Vector3(moveVelocity.x, rb.linearVelocity.y, moveVelocity.z);
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

    // ★修正：トリガー（案内板やマップの端）に触れた時の処理
    private void OnTriggerEnter(Collider other)
    {
        // 道路の端っこ（ゴール地点）に触れたら消滅
        if (other.CompareTag("Respawn") || other.name.Contains("DeadZone") || other.name.Contains("MapEdge"))
        {
            Destroy(gameObject);
        }
    }
}