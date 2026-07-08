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

    enum ActiveButton { None, Right, Left }
    ActiveButton activeButton = ActiveButton.None;

    bool prevRight = false;
    bool prevLeft = false;

    void Update()
    {
        if (arduino == null) return;

        bool curRight = arduino.RightPressed;
        bool curLeft = arduino.LeftPressed;

        // エディタデバッグ用：キーボード入力も受け付ける
#if UNITY_EDITOR
        if (Input.GetKey(KeyCode.RightArrow)) curRight = true;
        if (Input.GetKey(KeyCode.LeftArrow)) curLeft = true;
#endif

        bool rightEdgeOn = curRight && !prevRight;
        bool leftEdgeOn = curLeft && !prevLeft;

        // 何もロックされていない場合のみ新規入力を受け付ける
        if (activeButton == ActiveButton.None)
        {
            // 真の同時押しの場合は左を優先
            if (leftEdgeOn)
            {
                activeButton = ActiveButton.Left;
                if (isMenuState)
                {
                    OnMenuBack?.Invoke();
                }
                else
                {
                    OnBrake?.Invoke(true); // ブレーキ開始
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
                    OnBellRing?.Invoke(); // ベルを鳴らす
                }
            }
        }

        // 両方離されたらロック解除
        if (!curRight && !curLeft && activeButton != ActiveButton.None)
        {
            if (activeButton == ActiveButton.Left && !isMenuState)
            {
                // 左ボタン（ブレーキ）が離された時の処理
                OnBrake?.Invoke(false); // ブレーキ終了
            }
            activeButton = ActiveButton.None;
        }

        prevRight = curRight;
        prevLeft = curLeft;
    }
}
