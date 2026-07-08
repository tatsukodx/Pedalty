using UnityEngine;

public class WheelSpeedConverter : MonoBehaviour
{
    [Header("連携先の設定")]
    public ArduinoConnection arduinoConnection;
    public BicycleController bicycleController;

    [Header("計算用パラメータ")]
    public float wheelCircumference = 1.5f; // タイヤ周長(m)
    public float maxSpeedKmh = 36f;         // 上限速度(km/h)
    public float smoothFactor = 3f;         // 滑らか係数

    private float smoothedTargetKmh = 0f;

    void Start()
    {
        if (bicycleController == null)
            bicycleController = GetComponent<BicycleController>();
    }

    void Update()
    {
        if (arduinoConnection == null || bicycleController == null) return;

        // モードを BicycleController に伝える
        bicycleController.useExternalInput = arduinoConnection.isArduinoMode;

        if (arduinoConnection.isArduinoMode)
        {
            float targetKmh = 0f;

            if (arduinoConnection.MagnetInterval > 0)
            {
                float intervalSec = arduinoConnection.MagnetInterval / 1000f;
                float speedMs = wheelCircumference / intervalSec;
                targetKmh = speedMs * 3.6f;
            }

            // 0 のとき（停止中）もスムーズに0へ近づける
            smoothedTargetKmh = Mathf.Lerp(smoothedTargetKmh, targetKmh, smoothFactor * Time.deltaTime);

            // [0, 1] の範囲に正規化して moveInput 相当の値にする
            float normalizedInput = Mathf.Clamp01(smoothedTargetKmh / maxSpeedKmh);

            bicycleController.externalMoveInput = normalizedInput;
        }
        else
        {
            // キーボードモード時は内部状態をリセット
            smoothedTargetKmh = 0f;
            bicycleController.externalMoveInput = 0f;
        }
    }
}
