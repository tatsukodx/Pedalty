using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道路端の既存Triggerに追加され、自転車の接触状態だけを管理する。
/// </summary>
public sealed class RoadEndBoundaryTrigger : MonoBehaviour
{
    readonly HashSet<Collider> bicycleContacts = new();

    RoadEndBoundarySystem system;
    Vector3 outwardDirection;

    internal void Initialize(RoadEndBoundarySystem owner, Vector3 outward)
    {
        system = owner;
        outwardDirection = outward;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<BicycleController>() == null)
        {
            return;
        }

        bool wasEmpty = bicycleContacts.Count == 0;
        bicycleContacts.Add(other);
        if (wasEmpty && bicycleContacts.Count > 0)
        {
            system.SetBoundaryActive(this, outwardDirection, true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!bicycleContacts.Remove(other) || bicycleContacts.Count > 0)
        {
            return;
        }

        system.SetBoundaryActive(this, outwardDirection, false);
    }

    internal void ResetContacts()
    {
        bicycleContacts.Clear();
    }

    void OnDisable()
    {
        if (system != null && bicycleContacts.Count > 0)
        {
            bicycleContacts.Clear();
            system.SetBoundaryActive(this, outwardDirection, false);
        }
    }
}
