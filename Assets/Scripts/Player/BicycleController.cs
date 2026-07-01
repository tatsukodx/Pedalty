using UnityEngine;
using TMPro; // ★UI（TextMeshPro）を扱うために必要！

public class BicycleController : MonoBehaviour
{
    [Header("移動速度")]
    public float maxSpeed = 12f;      
    [Header("加速の勢い")]
    public float acceleration = 8f;   
    [Header("慣性の残り具合（地面にいるとき）")]
    public float deceleration = 3f;   
    [Header("回転速度")]
    public float turnSpeed = 90f;

    [Header("--- ジャンプ・空中設定 ---")]
    public float jumpForce = 5f;
    [Header("空中でのペダリングの効きやすさ（0.2 = 地面の20%の推進力）")]
    [Range(0f, 1f)]
    public float airPropulsionInfluence = 0.2f;

    [Header("--- 壁の跳ね返り設定 ---")]
    public float wallBounceForce = 3.0f;

    [Header("--- リスポーン設定 ---")]
    public float respawnThresholdY = -10.0f;

    [Header("--- UI設定 ---")]
    [Header("速度を表示するTextMeshProテキスト")]
    public TextMeshProUGUI speedText; // ★ここにインスペクターから文字オブジェクトを入れます

    [Header("--- 自転車の可動パーツ ---")]
    public Transform handlebar;
    public float maxSteerAngle = 35f;
    public Transform frontWheel;
    public Transform backWheel;
    public float wheelRotationSpeed = 10f;

    private Rigidbody rb;
    private float currentSpeed = 0f;  
    private float currentSteerAngle = 0f; 
    private BoxCollider boxCollider; 

    private Vector3 airVelocityVector;
    private bool wasGroundedLastFrame = true;
    private bool isBraking = false; // ★外部から制御するブレーキフラグ

    private Vector3 startPosition;
    private Quaternion startRotation;

    public void ApplyBrake(bool brake)
    {
        isBraking = brake;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Update()
    {
        bool grounded = IsGrounded();

        float moveInput = Input.GetAxis("Vertical"); 
        float turnInput = Input.GetAxis("Horizontal"); 

        // --- 1. 自転車本体の左右旋回（向き変更） ---
        if (Mathf.Abs(currentSpeed) > 0.1f || !grounded)
        {
            float turnRotation = turnInput * turnSpeed * Time.deltaTime;
            if (grounded && currentSpeed < 0) turnRotation *= -1; 

            transform.Rotate(0, turnRotation, 0);
        }

        // --- 2. 着地した「瞬間」の速度引き継ぎ処理 ---
        if (grounded && !wasGroundedLastFrame)
        {
            float speedInForwardDirection = Vector3.Dot(airVelocityVector, transform.forward);
            currentSpeed = Mathf.Clamp(speedInForwardDirection, -maxSpeed, maxSpeed);
        }

        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        float realHorizontalSpeed = Vector3.Dot(flatVelocity, flatForward);

        // --- 3. 慣性・移動・空中推進力の計算 ---
        if (grounded)
        {
            if (isBraking)
            {
                // ★ブレーキ時は通常の3倍の減速率で速度を0に近づける
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * 3f * Time.deltaTime);
            }
            else if (moveInput != 0)
            {
                float targetSpeed = moveInput * maxSpeed;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);
            }

            // 地面での壁衝突時の速度同期
            if (Mathf.Abs(currentSpeed) > 1.5f && Mathf.Abs(realHorizontalSpeed) < Mathf.Abs(currentSpeed) - 1.5f)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, realHorizontalSpeed, acceleration * 3f * Time.deltaTime);
            }

            Vector3 moveDirection = transform.forward * currentSpeed;
            rb.linearVelocity = new Vector3(moveDirection.x, rb.linearVelocity.y, moveDirection.z);
        }
        else
        {
            if (isBraking)
            {
                // ★空中でもブレーキが少し効くようにする（必要に応じて調整）
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * 2f * Time.deltaTime);
            }
            else if (moveInput != 0)
            {
                Vector3 airForce = transform.forward * moveInput * (acceleration * airPropulsionInfluence) * Time.deltaTime;
                airVelocityVector += airForce;

                float targetSpeed = moveInput * maxSpeed;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
            }

            // 空中での壁衝突判定
            if (airVelocityVector.magnitude > 1.5f && flatVelocity.magnitude < airVelocityVector.magnitude - 1.5f)
            {
                airVelocityVector = Vector3.MoveTowards(airVelocityVector, flatVelocity, acceleration * 4f * Time.deltaTime);
                currentSpeed = Mathf.MoveTowards(currentSpeed, realHorizontalSpeed, acceleration * 4f * Time.deltaTime);
            }

            rb.linearVelocity = new Vector3(airVelocityVector.x, rb.linearVelocity.y, airVelocityVector.z);
        }

        // --- 4. スペースキーでジャンプ ---
        if (Input.GetKeyDown(KeyCode.Space) && grounded)
        {
            airVelocityVector = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        // --- 5. マップ外への落下チェック（リスポーン） ---
        if (transform.position.y < respawnThresholdY)
        {
            Respawn();
        }

        // --- 6. ハンドルの回転ギミック ---
        if (handlebar != null)
        {
            float targetSteerAngle = turnInput * maxSteerAngle;
            currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteerAngle, turnSpeed * 2f * Time.deltaTime);
            handlebar.localRotation = Quaternion.Euler(0, currentSteerAngle, 0);
        }

        // --- 7. タイヤの回転ギミック ---
        float wheelRotation = currentSpeed * wheelRotationSpeed * Time.deltaTime * Mathf.Rad2Deg;

        if (frontWheel != null)
        {
            frontWheel.localRotation *= Quaternion.AngleAxis(wheelRotation, Vector3.right);
        }
        if (backWheel != null)
        {
            backWheel.localRotation *= Quaternion.AngleAxis(wheelRotation, Vector3.right);
        }

        // --- ★新機能！速度メーターのテキスト更新★ ---
        if (speedText != null)
        {
            // 実際の速度（絶対値）をきれいに四捨五入して整数（km/h風）にする
            // 今回は分かりやすく実際の物理的な最高速に合わせて10倍などの補正をかけてもOKです
            int displaySpeed = Mathf.RoundToInt(Mathf.Abs(currentSpeed) * 3.0f); // 3倍してそれっぽい速度感に
            speedText.text = "SPEED: " + displaySpeed + " km/h";
        }

        wasGroundedLastFrame = grounded;
    }

    private void Respawn()
    {
        transform.position = startPosition;
        transform.rotation = startRotation;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero; 

        currentSpeed = 0f;
        airVelocityVector = Vector3.zero;
    }

    private void OnCollisionEnter(Collision collision)
    {
        ContactPoint contact = collision.contacts[0];
        Vector3 bounceDirection = contact.normal;

        if (Mathf.Abs(bounceDirection.y) > 0.5f) return;

        bounceDirection.y = 0;
        bounceDirection.Normalize();

        float impactSpeed = Mathf.Max(Mathf.Abs(currentSpeed), 2.0f);
        Vector3 bounceForce = bounceDirection * impactSpeed * wallBounceForce;

        rb.AddForce(bounceForce, ForceMode.Impulse);

        currentSpeed = -currentSpeed * 0.2f; 
        airVelocityVector = bounceDirection * (airVelocityVector.magnitude * 0.2f);
    }

    private bool IsGrounded()
    {
        if (boxCollider == null) return false;

        float rayLength = 0.2f; 
        Vector3 rayStart = boxCollider.bounds.center; 
        float rayDistance = boxCollider.bounds.extents.y + rayLength; 

        return Physics.Raycast(rayStart, Vector3.down, rayDistance);
    }
}