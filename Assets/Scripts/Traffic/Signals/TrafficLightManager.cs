using System.Collections;
using UnityEngine;

public class TrafficLightManager : MonoBehaviour
{
    [Header("モード選択")]
    public CycleMode cycleMode = CycleMode.Alternating;

    [Header("車道の時間設定（秒）")]
    public float greenDuration  = 30f;
    public float yellowDuration = 3f;

    [Header("歩車分離モードの設定（秒）")]
    public float allRedDuration    = 2f;
    public float pedGreenDuration  = 15f;
    public float pedBlinkDuration  = 5f;

    [Header("交互モードの歩行者信号設定（秒）")]
    [Tooltip("自動車信号が黄になる何秒前に歩行者信号の点滅を始めるか")]
    public float pedBlinkLeadTime = 10f;

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
    public bool IsNS_CarGreen { get; private set; }
    public bool IsEW_CarGreen { get; private set; }
    public bool IsNS_CarRed { get; private set; }
    public bool IsEW_CarRed { get; private set; }
    public bool IsPedestrianGreen { get; private set; }
    public bool IsPedestrianBlinking { get; private set; }
    public TrafficLightPhase CurrentPhase { get; private set; }

    void Start()
    {
        ApplyPhase(TrafficLightPhase.NS_Green);
        StartCoroutine(RunCycle());
    }

    IEnumerator RunCycle()
    {
        while (true)
        {
            yield return StartCoroutine(EnterCarGreenPhase(TrafficLightPhase.NS_Green, greenDuration, pedEastLight, pedWestLight));
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.NS_Yellow, yellowDuration));
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.AllRed, allRedDuration));
            yield return StartCoroutine(EnterCarGreenPhase(TrafficLightPhase.EW_Green, greenDuration, pedNorthLight, pedSouthLight));
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.EW_Yellow, yellowDuration));
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.AllRed, allRedDuration));

            if (cycleMode == CycleMode.Scramble)
            {
                yield return StartCoroutine(EnterPhase(TrafficLightPhase.Pedestrian_Green, pedGreenDuration));
                yield return StartCoroutine(EnterPhase(TrafficLightPhase.Pedestrian_Blink, pedBlinkDuration));
                yield return StartCoroutine(EnterPhase(TrafficLightPhase.AllRed, allRedDuration));
            }
        }
    }

    IEnumerator EnterPhase(TrafficLightPhase phase, float duration)
    {
        ApplyPhase(phase);
        yield return new WaitForSeconds(duration);
    }

    IEnumerator EnterCarGreenPhase(TrafficLightPhase phase, float duration, TrafficLight pedA, TrafficLight pedB)
    {
        ApplyPhase(phase);

        float blinkStart = Mathf.Max(0f, duration - pedBlinkLeadTime);
        float blinkLength = Mathf.Min(pedBlinkDuration, duration - blinkStart);

        yield return new WaitForSeconds(blinkStart);

        IsPedestrianGreen = false;
        IsPedestrianBlinking = true;
        pedA?.StartBlink();
        pedB?.StartBlink();

        yield return new WaitForSeconds(blinkLength);

        IsPedestrianBlinking = false;
        SetPedPair(pedA, pedB, TrafficLightState.Red);

        yield return new WaitForSeconds(duration - blinkStart - blinkLength);
    }

    void ApplyPhase(TrafficLightPhase phase)
    {
        CurrentPhase      = phase;
        IsNS_CarGreen     = false;
        IsEW_CarGreen     = false;
        IsPedestrianGreen = false;
        IsNS_CarRed = true;
        IsEW_CarRed = true;

        Debug.Log($"[TrafficLight:{name}] → {phase}");

        switch (phase)
        {
            case TrafficLightPhase.NS_Green:
                IsNS_CarGreen = true;
                IsNS_CarRed   = false;
                IsPedestrianGreen = true;
                SetCarLights(TrafficLightState.Green, TrafficLightState.Red);
                SetPedPair(pedEastLight, pedWestLight, TrafficLightState.Green);
                SetPedPair(pedNorthLight, pedSouthLight, TrafficLightState.Red);
                break;

            case TrafficLightPhase.NS_Yellow:
                IsNS_CarRed = false; 
                SetCarLights(TrafficLightState.Yellow, TrafficLightState.Red);
                SetPedLights(TrafficLightState.Red);
                break;

            case TrafficLightPhase.EW_Green:
                IsEW_CarGreen = true;
                IsEW_CarRed   = false;
                IsPedestrianGreen = true;
                SetCarLights(TrafficLightState.Red, TrafficLightState.Green);
                SetPedPair(pedNorthLight, pedSouthLight, TrafficLightState.Green);
                SetPedPair(pedEastLight, pedWestLight, TrafficLightState.Red);
                break;

            case TrafficLightPhase.EW_Yellow:
                IsEW_CarRed = false; 
                SetCarLights(TrafficLightState.Red, TrafficLightState.Yellow);
                SetPedLights(TrafficLightState.Red);
                break;

            case TrafficLightPhase.AllRed:
                SetCarLights(TrafficLightState.Red, TrafficLightState.Red);
                SetPedLights(TrafficLightState.Red);
                break;

            case TrafficLightPhase.Pedestrian_Green:
                IsPedestrianGreen = true;
                SetCarLights(TrafficLightState.Red, TrafficLightState.Red);
                SetPedLights(TrafficLightState.Green);
                break;

            case TrafficLightPhase.Pedestrian_Blink:
                IsPedestrianGreen = true;
                SetCarLights(TrafficLightState.Red, TrafficLightState.Red);
                pedNorthLight?.StartBlink();
                pedSouthLight?.StartBlink();
                pedEastLight?.StartBlink();
                pedWestLight?.StartBlink();
                break;
        }
    }
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

    void SetPedPair(TrafficLight a, TrafficLight b, TrafficLightState state)
    {
        a?.SetState(state);
        b?.SetState(state);
    }
}