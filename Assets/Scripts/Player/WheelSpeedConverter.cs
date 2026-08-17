using UnityEngine;

// マグネットセンサのパルス間隔から実速度を求め、BicycleController に渡す。
//   実速度[km/h] = (周長[m] ÷ 磁石の数) ÷ パルス間隔[s] × 3.6
// 画面上で遅く見えるため、ゲーム内では speedMultiplier 倍した速度で走らせる。
public class WheelSpeedConverter : MonoBehaviour
{
    [Header("連携先の設定")]
    public ArduinoConnection arduinoConnection;
    public BicycleController bicycleController;

    // 以下は settings.json から読み込む
    float wheelCircumference = 2.096f;
    int magnetsPerWheel = 1;
    float speedMultiplier = 2f;
    float maxSpeedKmh = 40f;
    float smoothFactor = 3f;
    float pulseTimeoutSec = 2.5f;

    public float RealSpeedKmh { get; private set; }
    public float GameSpeedKmh { get; private set; }

    private float smoothedGameKmh = 0f;
    private int lastPulseCount = 0;
    private float lastPulseTime = 0f;

    void Start()
    {
        if (bicycleController == null)
            bicycleController = GetComponent<BicycleController>();

        var settings = AppSettings.I;
        wheelCircumference = settings.wheelCircumference;
        magnetsPerWheel = Mathf.Max(1, settings.magnetsPerWheel);
        speedMultiplier = settings.speedMultiplier;
        maxSpeedKmh = settings.maxSpeedKmh;
        smoothFactor = settings.smoothFactor;
        pulseTimeoutSec = settings.pulseTimeoutSec;

        // 上限速度(km/h)と物理速度(m/s)の対応を合わせ、currentSpeed × 3.6 = km/h が成り立つようにする
        if (bicycleController != null)
            bicycleController.maxSpeed = maxSpeedKmh / 3.6f;

        lastPulseTime = Time.time;
    }

    void Update()
    {
        if (arduinoConnection == null || bicycleController == null) return;

        bicycleController.useExternalInput = arduinoConnection.isArduinoMode;

        if (!arduinoConnection.isArduinoMode)
        {
            smoothedGameKmh = 0f;
            RealSpeedKmh = 0f;
            GameSpeedKmh = 0f;
            bicycleController.externalMoveInput = 0f;
            return;
        }

        if (arduinoConnection.MagnetPulseCount != lastPulseCount)
        {
            lastPulseCount = arduinoConnection.MagnetPulseCount;
            lastPulseTime = Time.time;

            Debug.Log($"磁気センサ検出: 間隔={arduinoConnection.MagnetInterval}ms, 実速度={CalcRealKmh(arduinoConnection.MagnetInterval):F1}km/h");
        }

        int intervalMs = arduinoConnection.MagnetInterval;

        // MAGNET,0 が届かなかった場合の保険
        bool timedOut = Time.time - lastPulseTime > pulseTimeoutSec;

        RealSpeedKmh = (intervalMs > 0 && !timedOut) ? CalcRealKmh(intervalMs) : 0f;

        float targetGameKmh = Mathf.Min(RealSpeedKmh * speedMultiplier, maxSpeedKmh);

        // 停止中(0)もスムーズに近づける
        smoothedGameKmh = Mathf.Lerp(smoothedGameKmh, targetGameKmh, smoothFactor * Time.deltaTime);
        GameSpeedKmh = smoothedGameKmh;

        bicycleController.externalMoveInput = Mathf.Clamp01(smoothedGameKmh / maxSpeedKmh);
    }

    float CalcRealKmh(int intervalMs)
    {
        if (intervalMs <= 0) return 0f;

        float intervalSec = intervalMs / 1000f;
        float distancePerPulse = wheelCircumference / magnetsPerWheel;
        return (distancePerPulse / intervalSec) * 3.6f;
    }
}
