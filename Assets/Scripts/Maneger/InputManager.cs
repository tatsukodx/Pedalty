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
    public UnityEvent<bool> OnBrake; // true: ブレーキ開始, false: ブレーキ終了

    [Header("メニュー中イベント")]
    public UnityEvent OnMenuNext;
    public UnityEvent OnMenuBack;

    [Header("安全設定")]
    [Tooltip("この秒数を超えて押しっぱなしのボタンは「信号の張り付き」とみなして無視する（0で無効）")]
    public float stuckHoldSeconds = 6f;

    enum ActiveButton { None, Right, Left }
    ActiveButton activeButton = ActiveButton.None;

    bool prevRight = false;
    bool prevLeft = false;

    float rightHoldTime = 0f;
    float leftHoldTime = 0f;
    bool rightStuck = false;
    bool leftStuck = false;

    void Update()
    {
        if (arduino == null) return;

        bool curRight = arduino.RightPressed;
        bool curLeft = arduino.LeftPressed;

#if UNITY_EDITOR
        if (Input.GetKey(KeyCode.RightArrow)) curRight = true;
        if (Input.GetKey(KeyCode.LeftArrow)) curLeft = true;
#endif

        curRight = FilterStuck(curRight, ref rightHoldTime, ref rightStuck, "右(D3)");
        curLeft = FilterStuck(curLeft, ref leftHoldTime, ref leftStuck, "左(D4)");

        bool rightEdgeOn = curRight && !prevRight;
        bool leftEdgeOn = curLeft && !prevLeft;

        // 何もロックされていない場合のみ新規入力を受け付ける
        if (activeButton == ActiveButton.None)
        {
            // 同時押しの場合は左を優先
            if (leftEdgeOn)
            {
                activeButton = ActiveButton.Left;
                if (isMenuState)
                {
                    OnMenuBack?.Invoke();
                }
                else
                {
                    OnBrake?.Invoke(true);
                }
            }
            else if (rightEdgeOn)
            {
                activeButton = ActiveButton.Right;
                if (isMenuState)
                {
                    OnMenuNext?.Invoke();
                }
                else
                {
                    OnBellRing?.Invoke();
                }
            }
        }

        // 解除条件を「両方離されたら」にすると、片方の信号が1に張り付いた時に
        // もう片方が永久に効かなくなるので、押している側だけを見る
        bool activeReleased = (activeButton == ActiveButton.Right && !curRight)
                           || (activeButton == ActiveButton.Left && !curLeft);

        if (activeReleased)
        {
            if (activeButton == ActiveButton.Left && !isMenuState)
            {
                OnBrake?.Invoke(false);
            }
            activeButton = ActiveButton.None;
        }

        prevRight = curRight;
        prevLeft = curLeft;
    }

    // 長時間 true のままのボタンを張り付きとみなして false を返す。一度 false に戻れば復帰する
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
