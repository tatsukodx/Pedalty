using System.Collections.Generic;
using UnityEngine;

public class PedestrianStopZone : MonoBehaviour
{
    [Header("この停止ゾーンを管理する交差点マネージャー")]
    public TrafficLightManager manager;

    [Header("横断方向の設定")]
    [Tooltip("true = 南北道路を横断 / false = 東西道路を横断")]
    public bool crossesNSRoad = true;

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

    bool CanWalkNow()
    {
        if (manager.cycleMode == CycleMode.Scramble)
        {
            return manager.IsPedestrianGreen;
        }
        else
        {
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
            walker.SetTrafficStop(false);  
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