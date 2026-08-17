using UnityEngine;

// ポテンショメータの生値(0-1023)を BicycleController の操舵入力(-1..1)へ変換する。
// WheelSpeedConverter（速度系）と対になるクラス。
public class HandleAngleConverter : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] ArduinoConnection arduino;
    [SerializeField] BicycleController bicycle;

    [Header("キャリブレーション（settings.json から読み込み）")]
    public int potMin = 200;
    public int potCenter = 512;
    public int potMax = 820;
    public bool invert = false;

    [Header("味付け")]
    [Tooltip("中央の不感帯。手放し時のふらつきを吸収する")]
    [Range(0f, 0.2f)] public float deadZone = 0.05f;

    [Tooltip("1.0=リニア。大きいほど中央が鈍く、端で目一杯効く")]
    [Range(1f, 3f)] public float expo = 1.5f;

    [Tooltip("追従の時定数[秒]。小さいほどキビキビ、大きいほど滑らか")]
    [Range(0.01f, 0.3f)] public float smoothTau = 0.06f;

    [Header("デバッグ表示（読み取り専用）")]
    [SerializeField] int dbgRaw;
    [SerializeField] float dbgNormalized;
    [SerializeField] float dbgOutput;

    float steer;

    // SteeringCalibrator の画面表示から参照する
    public int DebugRaw => dbgRaw;
    public float DebugNormalized => dbgNormalized;
    public float DebugOutput => dbgOutput;

    void Start()
    {
        if (bicycle == null) bicycle = GetComponent<BicycleController>();

        // 保険の自動検出。Inspector で割り当ててあればここは動かない。
        // 発動したら警告を出すので、割り当て忘れに気付ける
        if (arduino == null)
        {
            arduino = FindAnyObjectByType<ArduinoConnection>();
            if (arduino != null)
                Debug.LogWarning($"[HandleAngle] Arduino が未割り当てのため自動検出しました（{arduino.name}）。Inspector で割り当ててください");
        }

        if (arduino == null)
        {
            Debug.LogError($"[HandleAngle] ArduinoConnection が見つかりません（{name}）。" +
                           "シーンに ArduinoConnection を持つオブジェクトがあるか確認してください。ハンドル操舵は無効です");
            enabled = false;
            return;
        }
        if (bicycle == null)
        {
            Debug.LogError($"[HandleAngle] BicycleController が未割り当てです（{name}）。ハンドル操舵は無効です");
            enabled = false;
            return;
        }

        LoadCalibration();
    }

    // 較正完了時にも呼ぶ
    public void LoadCalibration()
    {
        var c = AppSettings.I;
        potMin = c.potMin;
        potCenter = c.potCenter;
        potMax = c.potMax;
        invert = c.potInvert;
    }

    void Update()
    {
        if (arduino == null || bicycle == null) return;

        if (!arduino.PotAvailable)
        {
            bicycle.useExternalSteer = false;
            steer = 0f;
            return;
        }

        dbgRaw = arduino.PotRaw;

        float t = Normalize(dbgRaw);
        dbgNormalized = t;

        t = ApplyCurve(t);

        // Lerp(a, b, speed * dt) は dt に対して非線形でフレームレートに依存する。
        // 1 - exp(-dt/tau) なら時定数が保証される
        float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(smoothTau, 1e-4f));
        steer = Mathf.Lerp(steer, t, k);

        dbgOutput = steer;

        bicycle.externalSteerInput = steer;
        bicycle.useExternalSteer = true;
    }

    // 中心から左右で別々のスケールを使うため、取り付けが左右非対称でも「直進 = 0」が保たれる
    public float Normalize(int raw)
    {
        float t = (raw >= potCenter)
            ? (raw - potCenter) / Mathf.Max(1f, potMax - potCenter)
            : (raw - potCenter) / Mathf.Max(1f, potCenter - potMin);

        t = Mathf.Clamp(t, -1f, 1f);
        return invert ? -t : t;
    }

    float ApplyCurve(float t)
    {
        float a = Mathf.Abs(t);
        if (a < deadZone) return 0f;

        // 再スケールしないと不感帯の境界で出力が 0 から deadZone へ不連続に跳ぶ
        a = (a - deadZone) / (1f - deadZone);
        a = Mathf.Pow(a, expo);
        return a * Mathf.Sign(t);
    }
}
