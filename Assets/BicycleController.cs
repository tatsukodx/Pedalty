using UnityEngine;

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
    [Header("この高さより下に落ちたらリスポーンする")]
    public float respawnThresholdY = -10.0f;

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

    // ★ゲーム開始時の「初期位置」と「初期の向き」を覚えておくための変数
    private Vector3 startPosition;
    private Quaternion startRotation;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();

        // ★ゲームが始まった瞬間の立ち位置と向きを記憶しておく
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Update()
    {
        bool grounded = IsGrounded();

        // キーボード入力を取得
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

        // 共通で使う「水平方向の正面ベクトル」をあらかじめ計算
        Vector3 flatVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        float realHorizontalSpeed = Vector3.Dot(flatVelocity, flatForward);

        // --- 3. 慣性・移動・空中推進力の計算 ---
        if (grounded)
        {
            // 【地面にいるとき】通常の加減速処理
            if (moveInput != 0)
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

            // 地面の移動速度をRigidbodyに適用
            Vector3 moveDirection = transform.forward * currentSpeed;
            rb.velocity = new Vector3(moveDirection.x, rb.velocity.y, moveDirection.z);
        }
        else
        {
            // 【空中にいるとき】
            if (moveInput != 0)
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

            // 空中慣性ベクトルを物理の横移動に適用
            rb.velocity = new Vector3(airVelocityVector.x, rb.velocity.y, airVelocityVector.z);
        }

        // --- 4. スペースキーでジャンプ ---
        if (Input.GetKeyDown(KeyCode.Space) && grounded)
        {
            airVelocityVector = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        // --- 5. 【新機能】マップ外への落下チェック（リスポーン） ---
        // もし現在の高さ（transform.position.y）が設定値（-10mなど）より低くなったら
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

        wasGroundedLastFrame = grounded;
    }

    // --- ★新機能！リスポーン（初期位置へワープ）処理★ ---
    private void Respawn()
    {
        // 1. 位置と向きをゲーム開始時の状態に戻す
        transform.position = startPosition;
        transform.rotation = startRotation;

        // 2. 物理的な勢い（落下速度や進む速度）を完全にゼロにする
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero; // 回転の勢いもストップ

        // 3. 内部の速度データや空中慣性もリセットしてその場に静止させる
        currentSpeed = 0f;
        airVelocityVector = Vector3.zero;
    }

    // 地面に着地しているかチェックする関数
    private bool IsGrounded()
    {
        if (boxCollider == null) return false;

        float rayLength = 0.2f; 
        Vector3 rayStart = boxCollider.bounds.center; 
        float rayDistance = boxCollider.bounds.extents.y + rayLength; 

        return Physics.Raycast(rayStart, Vector3.down, rayDistance);
    }
}