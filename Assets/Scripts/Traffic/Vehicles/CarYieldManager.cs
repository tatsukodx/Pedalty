using System.Collections.Generic;
using UnityEngine;

// 対向車線どうしの譲り合いを管理する。
// 以前は「台数」を数えていたが、同じ車を二重に数えたり、途中で消えた車が残ったりして
// 長時間動かすと数がずれ、居ない車を待ち続けて詰まることがあった。
// 実際の車を1台ずつ覚える方式にして、消えた車や長く居座っている車は自動で外す。
public class CarYieldManager : MonoBehaviour
{
    [Tooltip("停止ゾーンに入ってからこの秒数を過ぎた車は、待つ対象から外す（詰まり防止）")]
    public float staleSeconds = 45f;

    // 車 → 停止ゾーンに入った時刻
    private readonly Dictionary<CarController, float> laneA = new Dictionary<CarController, float>();
    private readonly Dictionary<CarController, float> laneB = new Dictionary<CarController, float>();
    private readonly List<CarController> removeBuffer = new List<CarController>();

    public void ReportStopZoneEnter(bool isLaneA, CarController car)
    {
        if (car == null) return;

        // 反対側の記録が残っていたら消す（1台は片方にだけ属する）
        (isLaneA ? laneB : laneA).Remove(car);

        Dictionary<CarController, float> lane = isLaneA ? laneA : laneB;
        if (!lane.ContainsKey(car)) lane[car] = Time.time;
    }

    public void ReportIntersectionExit(CarController car)
    {
        if (car == null) return;
        laneA.Remove(car);
        laneB.Remove(car);
    }

    public bool CanEnter(bool isLaneA)
    {
        Prune(laneA);
        Prune(laneB);

        Dictionary<CarController, float> own = isLaneA ? laneA : laneB;
        Dictionary<CarController, float> other = isLaneA ? laneB : laneA;

        if (other.Count == 0) return true;

        // 先に着いた側が先に進む
        return FirstArrival(own) <= FirstArrival(other);
    }

    public void ResetCounts()
    {
        laneA.Clear();
        laneB.Clear();
    }

    // 消えた車と、長く居座っている車を外す
    private void Prune(Dictionary<CarController, float> lane)
    {
        removeBuffer.Clear();
        foreach (KeyValuePair<CarController, float> pair in lane)
        {
            if (pair.Key == null || Time.time - pair.Value > staleSeconds) removeBuffer.Add(pair.Key);
        }
        foreach (CarController car in removeBuffer) lane.Remove(car);
    }

    private static float FirstArrival(Dictionary<CarController, float> lane)
    {
        float first = float.MaxValue;
        foreach (float t in lane.Values) if (t < first) first = t;
        return first;
    }
}
