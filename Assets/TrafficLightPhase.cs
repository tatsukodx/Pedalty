// TrafficLightPhase.cs
// 信号機システムで使用するenum定義

/// <summary>個別ランプの点灯状態</summary>
public enum TrafficLightState { Red, Yellow, Green }

/// <summary>交差点全体の信号フェーズ</summary>
public enum TrafficLightPhase
{
    NS_Green,
    NS_Yellow,
    EW_Green,
    EW_Yellow,
    AllRed,
    Pedestrian_Green,
    Pedestrian_Blink
}

/// <summary>信号サイクルのモード</summary>
public enum CycleMode
{
    Alternating,    // 交互方式
    Scramble        // 歩車分離式
}