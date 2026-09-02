using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CarIntersectionNode : MonoBehaviour
{
    public float straightDistance = 6f;
    public float leftTurnDistance = 6f;
    public float rightTurnDistance = 10f;

    [Header("南北方向の対向車線マネージャー（省略可）")]
    public CarYieldManager nsYieldManager;

    [Header("東西方向の対向車線マネージャー（省略可）")]
    public CarYieldManager ewYieldManager;

    private class ActiveCarInfo
    {
        public CarYieldManager manager;
        public bool isLaneA;
    }

    private readonly Dictionary<CarController, ActiveCarInfo> activeCars = new Dictionary<CarController, ActiveCarInfo>();

    private void OnTriggerEnter(Collider other)
    {
        CarController car = other.GetComponent<CarController>();
        if (car != null)
        {
            StartCoroutine(TurnSmoothly(car, other.transform));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CarController car = other.GetComponent<CarController>();
        if (car == null) return;

        if (activeCars.TryGetValue(car, out ActiveCarInfo info))
        {
            if (info.manager != null)
            {
                info.manager.ReportIntersectionExit(info.isLaneA);
            }
            activeCars.Remove(car);
        }
    }

    private IEnumerator TurnSmoothly(CarController car, Transform carTransform)
    {
        Vector3 currentDir = carTransform.forward;
        Vector3 rightDir = Quaternion.Euler(0, 90, 0) * currentDir;
        Vector3 leftDir = Quaternion.Euler(0, -90, 0) * currentDir;

        Vector3 nextDirection = currentDir;
        float targetDistance = straightDistance;
        int choice = Random.Range(0, 3);

        switch (choice)
        {
            case 0:
                nextDirection = currentDir;
                targetDistance = straightDistance;
                break;
            case 1:
                nextDirection = rightDir;
                targetDistance = rightTurnDistance;
                break;
            case 2:
                nextDirection = leftDir;
                targetDistance = leftTurnDistance;
                break;
        }

        // 進入方向から南北軸/東西軸と、軸内のどちら向きか（レーンA/B）を動的に判定する
        // ※ここでの待機は行わない。譲り合い待機は TrafficStopZone 側ですでに解消済みの前提。
        bool isNSAxis = Mathf.Abs(currentDir.x) < Mathf.Abs(currentDir.z);
        CarYieldManager relevantManager = isNSAxis ? nsYieldManager : ewYieldManager;
        bool isLaneA = isNSAxis ? currentDir.z >= 0f : currentDir.x >= 0f;

        if (relevantManager != null)
        {
            // OnTriggerExit で ReportIntersectionExit を呼ぶための登録のみ行う
            activeCars[car] = new ActiveCarInfo { manager = relevantManager, isLaneA = isLaneA };
        }

        if (choice == 0 || targetDistance <= 0.01f)
        {
            if (car != null) car.SetDirection(nextDirection);
            yield break;
        }

        Vector3 startPosition = carTransform.position;
        Quaternion startRot = Quaternion.LookRotation(currentDir);
        Quaternion endRot = Quaternion.LookRotation(nextDirection);

        while (car != null)
        {
            float traveled = Vector3.Distance(startPosition, carTransform.position);
            float t = Mathf.Clamp01(traveled / targetDistance);

            Vector3 interpolatedDir = Quaternion.Slerp(startRot, endRot, t) * Vector3.forward;
            car.SetDirection(interpolatedDir);

            if (t >= 1f) yield break;

            yield return new WaitForFixedUpdate();
        }
    }
}