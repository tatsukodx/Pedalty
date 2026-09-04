// TrafficStopZone.cs
// 停止線に配置するトリガーコライダー用スクリプト（車用）
// Box Collider の IsTrigger = true にして停止線の位置に置いてください

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
    public bool isLaneA = true;

    // 現在ゾーン内にいる車のリスト
    private readonly List<CarController> carsInZone = new List<CarController>();

    // 譲り合い待機中の車とそのコルーチン
    private readonly Dictionary<CarController, Coroutine> waitingCars = new Dictionary<CarController, Coroutine>();

    void Update()
    {
        if (manager == null) return;

        bool shouldStop = isNSDirection ? !manager.IsNS_CarGreen : !manager.IsEW_CarGreen;

        // リストを後ろから走査してnullを除去しながら更新
        for (int i = carsInZone.Count - 1; i >= 0; i--)
        {
            if (carsInZone[i] == null)
            {
                carsInZone.RemoveAt(i);
                continue;
            }
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
            yieldManager.ReportStopZoneEnter(isLaneA);

            // すでに待機中でなければ、通ってよくなるまで待つコルーチンを開始
            if (!waitingCars.ContainsKey(car))
            {
                Coroutine c = StartCoroutine(WaitUntilCanEnter(car));
                waitingCars[car] = c;
            }
        }
    }

    private IEnumerator WaitUntilCanEnter(CarController car)
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
            car.SetTrafficStop(false);  // ゾーンを出たら必ず解放
            carsInZone.Remove(car);

            // まだ待機コルーチンが残っていれば停止し、譲り合い停止も解除しておく
            if (waitingCars.TryGetValue(car, out Coroutine c))
            {
                waitingCars.Remove(car);

                // コルーチンがすでに完了・破棄されている場合は停止処理を呼ばない
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
