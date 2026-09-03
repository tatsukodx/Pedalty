using UnityEngine;
using TMPro;

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

    [Header("--- デバッグ（机上テスト用） ---")]
    [Tooltip("停止中でもハンドル操作で車体を旋回させる。ペダルを漕がずにポテンショメータを確認するための設定。\n本番では必ずオフに戻すこと（停止中に旋回するのは自転車として不自然なため）")]
    public bool allowSteerWhileStopped = false;

    [Header("--- UI設定 ---")]
    [Header("速度を表示するTextMeshProテキスト")]
    public TextMeshProUGUI speedText;

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
    private bool isBraking = false; // InputManagerのOnBrakeから制御する

    [HideInInspector] public float externalMoveInput = 0f;
    [HideInInspector] public bool useExternalInput = false;

    [HideInInspector] public float externalSteerInput = 0f; // -1..1
    [HideInInspector] public bool useExternalSteer = false;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool controlEnabled = true;
    private bool initialized;
    private bool isAtRoadEnd;
    private Vector3 roadEndOutwardDirection;

    public bool ControlEnabled => controlEnabled;

    public void ApplyBrake(bool brake)
    {
        isBraking = brake;
    }

    /// <summary>
    /// 道路端の外向き方向を登録する。接触中でも内側へ戻る移動と旋回は許可する。
    /// </summary>
    public void SetRoadEndBoundary(bool active, Vector3 outwardDirection)
    {
        isAtRoadEnd = active;
        roadEndOutwardDirection = outwardDirection;
        roadEndOutwardDirection.y = 0f;

        if (roadEndOutwardDirection.sqrMagnitude > 0.001f)
        {
            roadEndOutwardDirection.Normalize();
        }
    }

    void Awake()
    {
        InitializeIfNeeded();
    }

    void InitializeIfNeeded()
    {
        if (initialized) return;

        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();

        startPosition = transform.position;
        startRotation = transform.rotation;
        initialized = true;
    }

    void Update()
    {
        InitializeIfNeeded();

        if (!controlEnabled)
        {
            StopMovement();
            UpdateSpeedText();
            return;
        }

        bool grounded = IsGrounded();

        float moveInput = useExternalInput ? externalMoveInput : Input.GetAxis("Vertical"); 
        float turnInput = useExternalSteer ? externalSteerInput : Input.GetAxis("Horizontal");

        // 停止中は旋回しない（実際の自転車と同じ）。ただし机上テスト時のみ解除できる
        if (Mathf.Abs(currentSpeed) > 0.1f || !grounded || allowSteerWhileStopped)
        {
            float turnRotation = turnInput * turnSpeed * Time.deltaTime;
            if (grounded && currentSpeed < 0) turnRotation *= -1; 

            transform.Rotate(0, turnRotation, 0);
        }

        // 着地した瞬間に空中の速度を引き継ぐ
        if (grounded && !wasGroundedLastFrame)
        {
            float speedInForwardDirection = Vector3.Dot(airVelocityVector, transform.forward);
            currentSpeed = Mathf.Clamp(speedInForwardDirection, -maxSpeed, maxSpeed);
        }

        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        float realHorizontalSpeed = Vector3.Dot(flatVelocity, flatForward);

        if (grounded)
        {
            if (isBraking)
            {
                // ブレーキ時は通常の3倍の減速率
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

            bool blockedByRoadEnd = IsBlockedByRoadEnd(transform.forward * currentSpeed);

            // 道路端で漕いでいる間は車輪の回転用速度を残す。
            // それ以外の壁では、実速度が落ちたぶんを currentSpeed へ反映する。
            if (!blockedByRoadEnd && Mathf.Abs(currentSpeed) > 1.5f &&
                Mathf.Abs(realHorizontalSpeed) < Mathf.Abs(currentSpeed) - 1.5f)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, realHorizontalSpeed, acceleration * 3f * Time.deltaTime);
            }

            Vector3 moveDirection = transform.forward * currentSpeed;
            if (IsBlockedByRoadEnd(moveDirection))
            {
                moveDirection = Vector3.zero;
            }
            rb.linearVelocity = new Vector3(moveDirection.x, rb.linearVelocity.y, moveDirection.z);
        }
        else
        {
            if (isBraking)
            {
                // 空中でもブレーキを少しだけ効かせる
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * 2f * Time.deltaTime);
            }
            else if (moveInput != 0)
            {
                Vector3 airForce = transform.forward * moveInput * (acceleration * airPropulsionInfluence) * Time.deltaTime;
                airVelocityVector += airForce;

                float targetSpeed = moveInput * maxSpeed;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
            }

            bool blockedByRoadEnd = IsBlockedByRoadEnd(airVelocityVector);

            // 空中での壁衝突判定
            if (!blockedByRoadEnd && airVelocityVector.magnitude > 1.5f &&
                flatVelocity.magnitude < airVelocityVector.magnitude - 1.5f)
            {
                airVelocityVector = Vector3.MoveTowards(airVelocityVector, flatVelocity, acceleration * 4f * Time.deltaTime);
                currentSpeed = Mathf.MoveTowards(currentSpeed, realHorizontalSpeed, acceleration * 4f * Time.deltaTime);
            }

            Vector3 airMovement = blockedByRoadEnd ? Vector3.zero : airVelocityVector;
            rb.linearVelocity = new Vector3(airMovement.x, rb.linearVelocity.y, airMovement.z);
        }

        if (Input.GetKeyDown(KeyCode.Space) && grounded)
        {
            airVelocityVector = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        if (transform.position.y < respawnThresholdY)
        {
            Respawn();
        }

        if (handlebar != null)
        {
            float targetSteerAngle = turnInput * maxSteerAngle;
            currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteerAngle, turnSpeed * 2f * Time.deltaTime);
            handlebar.localRotation = Quaternion.Euler(0, currentSteerAngle, 0);
        }

        float wheelRotation = currentSpeed * wheelRotationSpeed * Time.deltaTime * Mathf.Rad2Deg;

        if (frontWheel != null)
        {
            frontWheel.localRotation *= Quaternion.AngleAxis(wheelRotation, Vector3.right);
        }
        if (backWheel != null)
        {
            backWheel.localRotation *= Quaternion.AngleAxis(wheelRotation, Vector3.right);
        }

        // currentSpeed は m/s なので 3.6 倍が km/h になる
        UpdateSpeedText();

        wasGroundedLastFrame = grounded;
    }

    public void ResetToStart()
    {
        InitializeIfNeeded();
        transform.position = startPosition;
        transform.rotation = startRotation;

        StopMovement();
        currentSteerAngle = 0f;
        isBraking = false;
        SetRoadEndBoundary(false, Vector3.zero);

        if (handlebar != null)
        {
            handlebar.localRotation = Quaternion.identity;
        }
        UpdateSpeedText();
    }

    public void SetControlEnabled(bool enabled)
    {
        InitializeIfNeeded();
        controlEnabled = enabled;
        if (!enabled)
        {
            StopMovement();
            UpdateSpeedText();
        }
    }

    void StopMovement()
    {
        if (rb == null) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        currentSpeed = 0f;
        airVelocityVector = Vector3.zero;
    }

    void UpdateSpeedText()
    {
        if (speedText == null) return;

        int displaySpeed = Mathf.RoundToInt(Mathf.Abs(currentSpeed) * 3.6f);
        speedText.text = "SPEED: " + displaySpeed + " km/h";
    }

    bool IsBlockedByRoadEnd(Vector3 movement)
    {
        if (!isAtRoadEnd || roadEndOutwardDirection.sqrMagnitude < 0.001f)
        {
            return false;
        }

        movement.y = 0f;
        return Vector3.Dot(movement, roadEndOutwardDirection) > 0.01f;
    }

    private void Respawn()
    {
        ResetToStart();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!controlEnabled) return;

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
