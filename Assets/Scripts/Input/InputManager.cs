using UnityEngine;
using UnityEngine.Events;

public class InputManager : MonoBehaviour
{
    [Header("Arduino連携")]
    public ArduinoConnection arduino;

    [Header("状態設定 (テスト用)")]
    [Tooltip("チェックを入れるとメニュー状態として動作し、Next/Backイベントが発火します")]
    public bool isMenuState = false;

    [Header("走行中イベント")]
    public UnityEvent OnBellRing;
    public UnityEvent<bool> OnBrake;

    [Header("ブレーキ制御")]
    [SerializeField] BicycleController bicycleController;

    [Header("違反ポップアップ中のイベント")]
    public UnityEvent OnViolationPopupBack;

    PenaltyController penaltyController;

    // 違反ポップアップ表示中もUpdateは動き続けるため、押しても走行中のイベントは発火させない
    bool IsRideInputBlocked => penaltyController != null && penaltyController.IsViolationPopupVisible;

    public bool IsBraking { get; private set; }
    public bool AnyButtonPressed { get; private set; }

    [Header("メニュー中イベント")]
    public UnityEvent OnMenuNext;
    public UnityEvent OnMenuBack;

    [Header("安全設定")]
    [Tooltip("この秒数を超えて押しっぱなしのボタンは「信号の張り付き」とみなして無視する（0で無効）")]
    public float stuckHoldSeconds = 6f;

    enum ActiveButton { None, Right, Left }
    ActiveButton activeButton = ActiveButton.None;

    [Header("車のスタック解消（左右同時押し）")]
    [Tooltip("左右のボタンをこの秒数同時に押し続けると発動する")]
    public float bothHoldSeconds = 0.5f;
    float bothHoldTime = 0f;
    bool bothTriggered = false;

    bool prevRight = false;
    bool prevLeft = false;

    float rightHoldTime = 0f;
    float leftHoldTime = 0f;
    bool rightStuck = false;
    bool leftStuck = false;

    void Awake()
    {
        if (bicycleController == null)
        {
            bicycleController = FindAnyObjectByType<BicycleController>();
        }

        penaltyController = FindAnyObjectByType<PenaltyController>();
    }

    void Update()
    {
        bool isArduinoActive = arduino != null && arduino.isArduinoMode;
        bool curRight = isArduinoActive && arduino.RightPressed;
        bool curLeft = isArduinoActive && arduino.LeftPressed;

        if (!isArduinoActive)
        {
            // 矢印キーはハンドル操作(Input.GetAxis("Horizontal"))に使われるため、ボタンには割り当てない
            curLeft = Input.GetMouseButton(0) || Input.GetKey(KeyCode.J);
            curRight = Input.GetKey(KeyCode.K);
        }

        if (isArduinoActive)
        {
            curRight = FilterStuck(curRight, ref rightHoldTime, ref rightStuck, "右(D3)");
            curLeft = FilterStuck(curLeft, ref leftHoldTime, ref leftStuck, "左(D4)");
        }
        else
        {
            ResetStuckState();
        }

        AnyButtonPressed = curRight || curLeft;

        // 走行中に左右同時押しを少し続けると、最寄りの交差点で止まっている車を消す（車のスタック解消用）
        if (curRight && curLeft && !isMenuState && !IsRideInputBlocked)
        {
            bothHoldTime += Time.unscaledDeltaTime;
            if (!bothTriggered && bothHoldTime >= bothHoldSeconds)
            {
                bothTriggered = true;
                StuckCarClearer.ClearNearestIntersection(bicycleController);
            }
        }
        else
        {
            bothHoldTime = 0f;
            bothTriggered = false;
        }

        bool rightEdgeOn = curRight && !prevRight;
        bool leftEdgeOn = curLeft && !prevLeft;

        if (activeButton == ActiveButton.None)
        {
            if (leftEdgeOn)
            {
                activeButton = ActiveButton.Left;
                if (isMenuState)
                {
                    OnMenuBack?.Invoke();
                }
                else if (IsRideInputBlocked)
                {
                    // ポップアップを閉じるだけ。ブレーキを掛けるとUIが点灯したまま残る
                    OnViolationPopupBack?.Invoke();
                }
                else
                {
                    SetBrakeState(true);
                }
            }
            else if (rightEdgeOn)
            {
                activeButton = ActiveButton.Right;
                if (isMenuState)
                {
                    OnMenuNext?.Invoke();
                }
                else if (!IsRideInputBlocked)
                {
                    OnBellRing?.Invoke();
                }
            }
        }

        bool activeReleased = (activeButton == ActiveButton.Right && !curRight)
                           || (activeButton == ActiveButton.Left && !curLeft);

        if (activeReleased)
        {
            if (activeButton == ActiveButton.Left && !isMenuState)
            {
                SetBrakeState(false);
            }
            activeButton = ActiveButton.None;
        }

        prevRight = curRight;
        prevLeft = curLeft;
    }

    void SetBrakeState(bool braking)
    {
        if (IsBraking == braking) return;

        IsBraking = braking;
        bicycleController?.ApplyBrake(braking);
        OnBrake?.Invoke(braking);
    }

    void ResetStuckState()
    {
        rightHoldTime = 0f;
        leftHoldTime = 0f;
        rightStuck = false;
        leftStuck = false;
    }

    void OnDisable()
    {
        AnyButtonPressed = false;
        if (IsBraking)
        {
            SetBrakeState(false);
        }
    }

    bool FilterStuck(bool pressed, ref float holdTime, ref bool isStuck, string label)
    {
        if (!pressed)
        {
            holdTime = 0f;
            isStuck = false;
            return false;
        }

        holdTime += Time.deltaTime;

        if (stuckHoldSeconds > 0f && holdTime > stuckHoldSeconds && !isStuck)
        {
            isStuck = true;
            Debug.LogWarning($"{label}ボタンが{stuckHoldSeconds:F0}秒以上押されたままです。配線・プルアップ設定を確認してください（この入力は無視します）");
        }

        return !isStuck;
    }
}
