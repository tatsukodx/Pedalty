using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrafficStopZone : MonoBehaviour
{
    [Header("この停止ゾーンを管理する交差点マネージャー（信号、省略可）")]
    public TrafficLightManager manager;

    [Header("この停止ゾーンの方向（信号用）")]
    [Tooltip("true = 南北方向の車が通る停止線 / false = 東西方向の車が通る停止線")]
    public bool isNSDirection = true;

    [Header("対向車線との譲り合い（省略可）")]
    public CarYieldManager yieldManager;

    private readonly List<CarController> carsInZone = new List<CarController>();

    private readonly Dictionary<CarController, Coroutine> waitingCars = new Dictionary<CarController, Coroutine>();

    private bool ComputeIsLaneA(Vector3 dir)
    {
        bool isNSAxis = Mathf.Abs(dir.x) < Mathf.Abs(dir.z);
        return isNSAxis ? dir.z >= 0f : dir.x >= 0f;
    }

    void Update()
    {
        if (manager == null) return;

        bool shouldStop = isNSDirection ? !manager.IsNS_CarGreen : !manager.IsEW_CarGreen;

        for (int i = carsInZone.Count - 1; i >= 0; i--)
        {
            if (carsInZone[i] == null)
            {
                carsInZone.RemoveAt(i);
                continue;
            }

            // 既に交差点への進入を開始した車は、信号が変わってもここで止めない
            if (carsInZone[i].HasEnteredIntersection) continue;

            carsInZone[i].SetTrafficStop(shouldStop);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        CarController car = other.GetComponentInParent<CarController>();
        if (car == null) return;

        if (!carsInZone.Contains(car))
        {
            carsInZone.Add(car);
        }

        if (yieldManager != null)
        {
            bool isLaneA = ComputeIsLaneA(car.transform.forward);
            yieldManager.ReportStopZoneEnter(isLaneA);

            if (!waitingCars.ContainsKey(car))
            {
                Coroutine c = StartCoroutine(WaitUntilCanEnter(car, isLaneA));
                waitingCars[car] = c;
            }
        }
    }

    private IEnumerator WaitUntilCanEnter(CarController car, bool isLaneA)
    {
        car.SetYieldStop(true);

        while (car != null && !yieldManager.CanEnter(isLaneA))
        {
            yield return null;
        }

        if (car != null)
        {
            car.SetYieldStop(false);
        }

        waitingCars.Remove(car);
    }

    void OnTriggerExit(Collider other)
    {
        CarController car = other.GetComponentInParent<CarController>();
        if (car != null)
        {
            car.SetTrafficStop(false);
            carsInZone.Remove(car);

            if (waitingCars.TryGetValue(car, out Coroutine c))
            {
                waitingCars.Remove(car);

                if (c != null)
                {
                    StopCoroutine(c);
                }

                car.SetYieldStop(false);
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = isNSDirection ? new Color(0f, 0.5f, 1f, 0.3f)
                                     : new Color(1f, 0.5f, 0f, 0.3f);
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(bc.center, bc.size);
        }
    }
#endif
}