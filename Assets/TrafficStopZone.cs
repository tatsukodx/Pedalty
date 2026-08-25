// TrafficStopZone.cs
// 停止線に配置するトリガーコライダー用スクリプト（車用）
// Box Collider の IsTrigger = true にして停止線の位置に置いてください

using System.Collections.Generic;
using UnityEngine;

public class TrafficStopZone : MonoBehaviour
{
    [Header("この停止ゾーンを管理する交差点マネージャー")]
    public TrafficLightManager manager;

    [Header("この停止ゾーンの方向")]
    [Tooltip("true = 南北方向の車が通る停止線 / false = 東西方向の車が通る停止線")]
    public bool isNSDirection = true;

    // 現在ゾーン内にいる車のリスト
    private readonly List<CarController> carsInZone = new List<CarController>();

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
        if (car != null && !carsInZone.Contains(car))
        {
            carsInZone.Add(car);
        }
    }

    void OnTriggerExit(Collider other)
    {
        CarController car = other.GetComponentInParent<CarController>();
        if (car != null)
        {
            car.SetTrafficStop(false);  // ゾーンを出たら必ず解放
            carsInZone.Remove(car);
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