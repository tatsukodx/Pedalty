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

    [Header("交互モードの歩行者青時間（秒）")]
    [Tooltip("交互モードで NS と EW の切り替え時に挿入する歩行者青フェーズの長さ")]
    public float altPedGreenDuration = 0f;

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
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.NS_Green,  greenDuration));
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.NS_Yellow, yellowDuration));
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.AllRed, allRedDuration));
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.EW_Green,  greenDuration));
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.EW_Yellow, yellowDuration));
            yield return StartCoroutine(EnterPhase(TrafficLightPhase.AllRed, allRedDuration));

            if (cycleMode == CycleMode.Scramble)
            {
                yield return StartCoroutine(EnterPhase(TrafficLightPhase.Pedestrian_Green, pedGreenDuration));
                yield return StartCoroutine(EnterPhase(TrafficLightPhase.Pedestrian_Blink, pedBlinkDuration));
                yield return StartCoroutine(EnterPhase(TrafficLightPhase.AllRed, allRedDuration));
            }
            else if (altPedGreenDuration > 0f)
            {
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
                if (cycleMode == CycleMode.Alternating) IsPedestrianGreen = true;
                SetCarLights(TrafficLightState.Green, TrafficLightState.Red);
                SetPedLights(TrafficLightState.Red);
                break;

            case TrafficLightPhase.NS_Yellow:
                IsNS_CarRed = false; 
                SetCarLights(TrafficLightState.Yellow, TrafficLightState.Red);
                SetPedLights(TrafficLightState.Red);
                break;

            case TrafficLightPhase.EW_Green:
                IsEW_CarGreen = true;
                IsEW_CarRed   = false;
                if (cycleMode == CycleMode.Alternating) IsPedestrianGreen = true;
                SetCarLights(TrafficLightState.Red, TrafficLightState.Green);
                SetPedLights(TrafficLightState.Red);
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
}