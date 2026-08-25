// PedestrianStopZone.cs
// 横断歩道手前に配置するトリガーコライダー用スクリプト（歩行者用）
// Box Collider の IsTrigger = true にして横断歩道の手前（歩行者信号前）に置いてください
//
// 【方向の考え方】
//   crossesNSRoad = true  → 南北道路を横断する横断歩道（東西の歩道上の歩行者）
//                           交互モードでは EW車が青のときに渡れる
//   crossesNSRoad = false → 東西道路を横断する横断歩道（南北の歩道上の歩行者）
//                           交互モードでは NS車が青のときに渡れる

using System.Collections.Generic;
using UnityEngine;

public class PedestrianStopZone : MonoBehaviour
{
    [Header("この停止ゾーンを管理する交差点マネージャー")]
    public TrafficLightManager manager;

    [Header("横断方向の設定")]
    [Tooltip("true = 南北道路を横断 / false = 東西道路を横断")]
    public bool crossesNSRoad = true;

    // 現在ゾーン内にいる歩行者のリスト
    private readonly List<NPCWalker> walkersInZone = new List<NPCWalker>();

    void Update()
    {
        if (manager == null) return;

        bool shouldStop = !CanWalkNow();

        for (int i = walkersInZone.Count - 1; i >= 0; i--)
        {
            if (walkersInZone[i] == null)
            {
                walkersInZone.RemoveAt(i);
                continue;
            }
            walkersInZone[i].SetTrafficStop(shouldStop);
        }
    }

    /// <summary>現在の信号フェーズで歩行者が渡れるかを返します</summary>
    bool CanWalkNow()
    {
        if (manager.cycleMode == CycleMode.Scramble)
        {
            // 歩車分離: 専用フェーズのみ渡れる
            return manager.IsPedestrianGreen;
        }
        else
        {
            // 交互方式: 横断する道路の垂直方向の車が青のときに渡れる
            // crossesNSRoad: 南北道路横断 → EW車が通行中に渡れる
            // !crossesNSRoad: 東西道路横断 → NS車が通行中に渡れる
            return crossesNSRoad ? manager.IsEW_CarGreen : manager.IsNS_CarGreen;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        NPCWalker walker = other.GetComponent<NPCWalker>();
        if (walker != null && !walkersInZone.Contains(walker))
        {
            walkersInZone.Add(walker);
        }
    }

    void OnTriggerExit(Collider other)
    {
        NPCWalker walker = other.GetComponent<NPCWalker>();
        if (walker != null)
        {
            walker.SetTrafficStop(false);   // ゾーンを出たら必ず解放
            walkersInZone.Remove(walker);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = crossesNSRoad ? new Color(0f, 1f, 0.2f, 0.3f)
                                     : new Color(1f, 0f, 0.8f, 0.3f);
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(bc.center, bc.size);
        }
    }
#endif
}