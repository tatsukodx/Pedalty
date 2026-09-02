// TrafficLightManager.cs
// 交差点全体の信号サイクルを管理します
// 各停止ゾーンはこのスクリプトのプロパティを参照して停止/発進を判断します

using System.Collections;
using UnityEngine;

public class TrafficLightManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  インスペクター設定
    // ─────────────────────────────────────────
    [Header("モード選択")]
    public CycleMode cycleMode = CycleMode.Alternating;

    [Header("車道の時間設定（秒）")]
    public float greenDuration  = 30f;
    public float yellowDuration = 3f;

    [Header("歩車分離モードの設定（秒）")]
    public float allRedDuration    = 2f;
    public float pedGreenDuration  = 15f;
    public float pedBlinkDuration  = 5f;

    [Header("交互モードの歩行者青時間（秒）")]
    [Tooltip("交互モードで NS と EW の切り替え時に挿入する歩行者青フェーズの長さ")]
    public float altPedGreenDuration = 0f; // 0 = 歩行者フェーズなし（純粋な交互）

    [Header("車道信号機（4方向）")]
    public TrafficLight northLight;
    public TrafficLight southLight;
    public TrafficLight eastLight;
    public TrafficLight westLight;

    [Header("歩行者信号機（省略可 / 歩車分離モードで使用）")]
    public TrafficLight pedNorthLight;
    public TrafficLight pedSouthLight;
    public TrafficLight pedEastLight;
    public TrafficLight pedWestLight;

    // ─────────────────────────────────────────
    //  停止ゾーンが参照するプロパティ
    // ─────────────────────────────────────────

    /// <summary>南北方向の車が進んでよいか（青のときだけtrue）</summary>
    public bool IsNS_CarGreen { get; private set; }

    /// <summary>東西方向の車が進んでよいか（青のときだけtrue）</summary>
    public bool IsEW_CarGreen { get; private set; }

    /// <summary>南北方向の車が完全に赤か（青にも黄にも該当しない）</summary>
    public bool IsNS_CarRed { get; private set; }

    /// <summary>東西方向の車が完全に赤か（青にも黄にも該当しない）</summary>
    public bool IsEW_CarRed { get; private set; }

    /// <summary>
    /// 歩行者が進んでよいか。
    /// ・Scrambleモード: 歩行者専用フェーズのみ true
    /// ・Alternatingモード: NS青時は EW道路横断が可、EW青時は NS道路横断が可
    ///   （方向別判断は IntersectionNode 側で IsNS_CarRed / IsEW_CarRed を見て行う）
    /// </summary>
    public bool IsPedestrianGreen { get; private set; }

    /// <summary>現在のフェーズ（デバッグ・ゾーン参照用）</summary>
    public TrafficLightPhase CurrentPhase { get; private set; }

    // ─────────────────────────────────────────
    //  内部
    // ─────────────────────────────────────────
    void Start()
    {
        // 初期状態: 全赤
        ApplyPhase(TrafficLightPhase.NS_Green);
        StartCoroutine(RunCycle());
    }

    IEnumerator RunCycle()
    {
        while (true)
        {
            // ── NS 青 ──
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.NS_Green,  greenDuration));

            // ── NS 黄 ──
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.NS_Yellow, yellowDuration));

            // ── 全赤（バッファ）── NS→EW切り替え時も必ず挟む
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.AllRed, allRedDuration));

            // ── EW 青 ──
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.EW_Green,  greenDuration));

            // ── EW 黄 ──
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.EW_Yellow, yellowDuration));

            // ── 全赤（バッファ）── EW→NS切り替え時も必ず挟む
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.AllRed, allRedDuration));

            if (cycleMode == CycleMode.Scramble)
            {
                // ── 歩行者 青 ──
                yield return StartCoroutine(EnterPhase(TrafficLightPhase.Pedestrian_Green, pedGreenDuration));

                // ── 歩行者 点滅 ──
                yield return StartCoroutine(EnterPhase(TrafficLightPhase.Pedestrian_Blink, pedBlinkDuration));

                // ── 全赤（バッファ）── 歩行者フェーズ後、車道が動き出す前に挟む
                yield return StartCoroutine(EnterPhase(TrafficLightPhase.AllRed, allRedDuration));
            }
            else if (altPedGreenDuration > 0f)
            {
                // 交互モードで歩行者フェーズを挿入する場合（0秒ならスキップ）
                yield return StartCoroutine(EnterPhase(TrafficLightPhase.Pedestrian_Green, altPedGreenDuration));
                yield return StartCoroutine(EnterPhase(TrafficLightPhase.AllRed,           allRedDuration));
            }
        }
    }

    IEnumerator EnterPhase(TrafficLightPhase phase, float duration)
    {
        ApplyPhase(phase);
        yield return new WaitForSeconds(duration);
    }

    // ─────────────────────────────────────────
    //  フェーズ適用
    // ─────────────────────────────────────────
    void ApplyPhase(TrafficLightPhase phase)
    {
        CurrentPhase      = phase;
        IsNS_CarGreen     = false;
        IsEW_CarGreen     = false;
        IsPedestrianGreen = false;

        // 青でも黄でもない限り「完全に赤」として扱う（歩行者はこちらを見て渡る）
        IsNS_CarRed = true;
        IsEW_CarRed = true;

        Debug.Log($"[TrafficLight:{name}] → {phase}");

        switch (phase)
        {
            case TrafficLightPhase.NS_Green:
                IsNS_CarGreen = true;
                IsNS_CarRed   = false;
                // 交互モードでは NS青時に EW道路横断（東西の横断歩道）の歩行者が進める
                if (cycleMode == CycleMode.Alternating) IsPedestrianGreen = true;
                SetCarLights(TrafficLightState.Green, TrafficLightState.Red);
                SetPedLights(TrafficLightState.Red);
                break;

            case TrafficLightPhase.NS_Yellow:
                IsNS_CarRed = false; // 黄色はまだ赤ではない＝歩行者はまだ渡ってはいけない
                SetCarLights(TrafficLightState.Yellow, TrafficLightState.Red);
                SetPedLights(TrafficLightState.Red);
                break;

            case TrafficLightPhase.EW_Green:
                IsEW_CarGreen = true;
                IsEW_CarRed   = false;
                // 交互モードでは EW青時に NS道路横断（南北の横断歩道）の歩行者が進める
                if (cycleMode == CycleMode.Alternating) IsPedestrianGreen = true;
                SetCarLights(TrafficLightState.Red, TrafficLightState.Green);
                SetPedLights(TrafficLightState.Red);
                break;

            case TrafficLightPhase.EW_Yellow:
                IsEW_CarRed = false; // 黄色はまだ赤ではない＝歩行者はまだ渡ってはいけない
                SetCarLights(TrafficLightState.Red, TrafficLightState.Yellow);
                SetPedLights(TrafficLightState.Red);
                break;

            case TrafficLightPhase.AllRed:
                // IsNS_CarRed / IsEW_CarRed は両方とも既定値のtrueのまま
                SetCarLights(TrafficLightState.Red, TrafficLightState.Red);
                SetPedLights(TrafficLightState.Red);
                break;

            case TrafficLightPhase.Pedestrian_Green:
                IsPedestrianGreen = true;
                SetCarLights(TrafficLightState.Red, TrafficLightState.Red);
                SetPedLights(TrafficLightState.Green);
                break;

            case TrafficLightPhase.Pedestrian_Blink:
                IsPedestrianGreen = true;   // 点滅中もまだ渡ってよい
                SetCarLights(TrafficLightState.Red, TrafficLightState.Red);
                pedNorthLight?.StartBlink();
                pedSouthLight?.StartBlink();
                pedEastLight?.StartBlink();
                pedWestLight?.StartBlink();
                break;
        }
    }

    // ─────────────────────────────────────────
    //  ユーティリティ
    // ─────────────────────────────────────────
    void SetCarLights(TrafficLightState ns, TrafficLightState ew)
    {
        northLight?.SetState(ns);
        southLight?.SetState(ns);
        eastLight?.SetState(ew);
        westLight?.SetState(ew);
    }

    void SetPedLights(TrafficLightState state)
    {
        pedNorthLight?.SetState(state);
        pedSouthLight?.SetState(state);
        pedEastLight?.SetState(state);
        pedWestLight?.SetState(state);
    }
}