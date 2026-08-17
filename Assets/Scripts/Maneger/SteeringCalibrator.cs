using System.Text;
using TMPro;
using UnityEngine;

// ゲーム内でハンドル角の較正を行う。3ステップの指示を出して生値を記録する。
// TMP_Text を割り当てていない場合は OnGUI のオーバーレイに表示するので、
// Canvas を組まなくても机上テストと較正がそのまま行える。
public class SteeringCalibrator : MonoBehaviour
{
    public enum Step { Idle, Left, Right, Center, Done, Error }

    [Header("参照（未割り当てなら自動探索する）")]
    [SerializeField] ArduinoConnection arduino;
    [SerializeField] HandleAngleConverter converter;

    [Header("UI（任意。未割り当てなら画面左上のオーバーレイに出す）")]
    [SerializeField] TMP_Text messageText;
    [SerializeField] TMP_Text rawText;

    [Header("操作キー")]
    [Tooltip("較正を開始するキー")]
    [SerializeField] KeyCode startKey = KeyCode.F1;
    [Tooltip("各ステップを確定するキー")]
    [SerializeField] KeyCode confirmKey = KeyCode.Space;

    [Header("デバッグ表示")]
    [Tooltip("画面左上に生値・正規化値・操舵出力を常時表示する（机上テスト用）")]
    [SerializeField] bool showOverlay = true;
    [Tooltip("オーバーレイの表示/非表示を切り替えるキー")]
    [SerializeField] KeyCode toggleOverlayKey = KeyCode.F2;

    Step step = Step.Idle;
    int rawL, rawR, rawC;
    string message = "";
    GUIStyle overlayStyle;

    void Start()
    {
        // 保険の自動検出。Inspector で割り当ててあればここは動かない。
        // 発動したら警告を出すので、割り当て忘れに気付ける
        if (arduino == null)
        {
            arduino = FindAnyObjectByType<ArduinoConnection>();
            if (arduino != null)
                Debug.LogWarning($"[Calibrator] Arduino が未割り当てのため自動検出しました（{arduino.name}）。Inspector で割り当ててください");
        }
        if (converter == null)
        {
            converter = FindAnyObjectByType<HandleAngleConverter>();
            if (converter != null)
                Debug.LogWarning($"[Calibrator] Converter が未割り当てのため自動検出しました（{converter.name}）。Inspector で割り当ててください");
        }

        if (arduino == null)
            Debug.LogError("[Calibrator] ArduinoConnection が見つかりません。較正はできません");
        if (converter == null)
            Debug.LogError("[Calibrator] HandleAngleConverter が見つかりません。較正値を反映できません");
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleOverlayKey)) showOverlay = !showOverlay;

        if (arduino != null && arduino.PotAvailable && rawText != null)
            rawText.text = $"RAW: {arduino.PotRaw}";

        switch (step)
        {
            case Step.Idle:
                Show($"[{startKey}] でハンドルの較正を開始します");
                if (Input.GetKeyDown(startKey))
                {
                    if (arduino == null || !arduino.PotAvailable)
                    {
                        step = Step.Error;
                        Show("ハンドルセンサが検出できません。USB 接続を確認してください");
                        break;
                    }
                    step = Step.Left;
                }
                break;

            case Step.Left:
                Show($"ハンドルを【左いっぱい】に回して [{confirmKey}]");
                if (Input.GetKeyDown(confirmKey)) { rawL = arduino.PotRaw; step = Step.Right; }
                break;

            case Step.Right:
                Show($"ハンドルを【右いっぱい】に回して [{confirmKey}]");
                if (Input.GetKeyDown(confirmKey)) { rawR = arduino.PotRaw; step = Step.Center; }
                break;

            case Step.Center:
                Show($"ハンドルを【まっすぐ】に戻して [{confirmKey}]");
                if (Input.GetKeyDown(confirmKey)) { rawC = arduino.PotRaw; Commit(); }
                break;

            case Step.Done:
            case Step.Error:
                if (Input.GetKeyDown(confirmKey)) step = Step.Idle;
                break;
        }
    }

    void Commit()
    {
        int min = Mathf.Min(rawL, rawR);
        int max = Mathf.Max(rawL, rawR);

        if (max - min < 100)
        {
            step = Step.Error;
            Show($"ハンドルの可動範囲が検出できません（左={rawL} 右={rawR}）\n配線とポテンショメータの取り付けを確認してください");
            return;
        }
        if (rawC <= min || rawC >= max)
        {
            step = Step.Error;
            Show($"中央位置が範囲外です（中央={rawC} 範囲={min}〜{max}）\nもう一度やり直してください");
            return;
        }
        if (Mathf.Min(rawC - min, max - rawC) < 30)
        {
            step = Step.Error;
            Show("片側の可動範囲が極端に狭くなっています。\n取り付け位置を調整してください");
            return;
        }

        var c = AppSettings.I;
        c.potMin = min;
        c.potMax = max;
        c.potCenter = rawC;
        c.potInvert = (rawL > rawR); // 左の方が大きい値なら端子1/3が逆に繋がっている
        c.Save();

        if (converter != null) converter.LoadCalibration();

        step = Step.Done;
        Show($"較正が完了しました\nmin={min} center={rawC} max={max} invert={c.potInvert}");
    }

    void Show(string msg)
    {
        message = msg;
        if (messageText != null) messageText.text = msg;
    }

    void OnGUI()
    {
        if (!showOverlay) return;

        if (overlayStyle == null)
        {
            overlayStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(12, 12, 10, 10),
                fontSize = 14,
                richText = true,
                wordWrap = false
            };
            overlayStyle.normal.textColor = Color.white;
        }

        bool live = arduino != null && arduino.PotAvailable;
        var sb = new StringBuilder();

        sb.Append("<b>ハンドル入力</b>   ");
        sb.AppendLine(live
            ? $"<color=#7CFC7C>受信中</color> ({arduino.ConnectedPortName})"
            : "<color=#FF8080>未受信</color>  ポテンショメータ/USB を確認");

        sb.AppendLine($"RAW      : {(arduino != null ? arduino.PotRaw : -1)}");

        if (converter != null)
        {
            sb.AppendLine($"正規化   : {converter.DebugNormalized,7:F3}");
            sb.AppendLine($"操舵出力 : {converter.DebugOutput,7:F3}  {Bar(converter.DebugOutput)}");
            sb.AppendLine($"較正値   : min={converter.potMin} center={converter.potCenter} max={converter.potMax} invert={converter.invert}");
        }
        else
        {
            sb.AppendLine("<color=#FFD070>HandleAngleConverter が見つかりません</color>");
        }

        sb.Append($"\n{message}\n\n<size=12>[{toggleOverlayKey}] 表示切替　[{startKey}] 較正開始</size>");

        GUI.Box(new Rect(10, 10, 560, 178), sb.ToString(), overlayStyle);
    }

    // 操舵出力を横棒で可視化する。中央が | 、現在値が #
    static string Bar(float v)
    {
        const int half = 15;
        char[] c = new char[half * 2 + 1];
        for (int i = 0; i < c.Length; i++) c[i] = '.';
        c[half] = '|';
        int idx = Mathf.Clamp(half + Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * half), 0, c.Length - 1);
        c[idx] = '#';
        return "[" + new string(c) + "]";
    }
}
