using UnityEngine;

public enum RoadAreaType
{
    None,
    Road,
    Sidewalk,
    BikeLane
}

public class PlayerLaneDetector : MonoBehaviour
{
    public float sensorRadius = 1f;
    public RoadAreaType currentArea = RoadAreaType.None;

    void FixedUpdate()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, sensorRadius, ~0, QueryTriggerInteraction.Collide);
        RoadAreaType detected = RoadAreaType.None;
        float closestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            RoadAreaType type = GetAreaType(hit);
            if (type == RoadAreaType.None) continue;

            float distance = Vector3.Distance(transform.position, hit.ClosestPoint(transform.position));
            if (distance < closestDistance)
            {
                closestDistance = distance;
                detected = type;
            }
        }

        currentArea = detected;
    }

    RoadAreaType GetAreaType(Collider hit)
    {
        if (hit.CompareTag("Road_L") || hit.CompareTag("Road_R")) return RoadAreaType.Road;
        if (hit.CompareTag("BIkeLane_L") || hit.CompareTag("BikeLane_R")) return RoadAreaType.BikeLane;
        if (hit.CompareTag("Sidewalk_L") || hit.CompareTag("Sidewalk_R")) return RoadAreaType.Sidewalk;
        return RoadAreaType.None;
    }
}