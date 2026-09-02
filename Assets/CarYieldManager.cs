using UnityEngine;

public class CarYieldManager : MonoBehaviour
{
    private int laneACount = 0;
    private int laneBCount = 0;
    private float laneAFirstArrival = float.MaxValue;
    private float laneBFirstArrival = float.MaxValue;

    public void ReportStopZoneEnter(bool isLaneA)
    {
        if (isLaneA)
        {
            if (laneACount == 0) laneAFirstArrival = Time.time;
            laneACount++;
        }
        else
        {
            if (laneBCount == 0) laneBFirstArrival = Time.time;
            laneBCount++;
        }
    }

    public void ReportIntersectionExit(bool isLaneA)
    {
        if (isLaneA)
        {
            laneACount = Mathf.Max(0, laneACount - 1);
            if (laneACount == 0) laneAFirstArrival = float.MaxValue;
        }
        else
        {
            laneBCount = Mathf.Max(0, laneBCount - 1);
            if (laneBCount == 0) laneBFirstArrival = float.MaxValue;
        }
    }

    public bool CanEnter(bool isLaneA)
    {
        bool otherBlocking = isLaneA ? laneBCount > 0 : laneACount > 0;
        if (!otherBlocking) return true;

        float ownArrival = isLaneA ? laneAFirstArrival : laneBFirstArrival;
        float otherArrival = isLaneA ? laneBFirstArrival : laneAFirstArrival;

        return ownArrival <= otherArrival;
    }
}