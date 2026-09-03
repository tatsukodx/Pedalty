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

    [Header("横断歩道の位置（省略可、Intersection_01のCrosswalk_1〜4をドラッグ）")]
    public Transform crosswalkNorth;
    public Transform crosswalkSouth;
    public Transform crosswalkEast;
    public Transform crosswalkWest;

    [Header("横断歩道の判定範囲（横断歩道のTransformのローカル軸基準）")]
    [Tooltip("横断歩道と平行な方向（人が横切る向き）の幅")]
    public float crosswalkWidth = 6f;
    [Tooltip("車の進行方向にあたる奥行き（横断歩道の厚み）")]
    public float crosswalkDepth = 3f;
    [Tooltip("判定する高さ（上下方向）")]
    public float crosswalkHeight = 3f;

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

        Vector3 entryPosition = carTransform.position;
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

        Transform entryCrosswalk = GetCrosswalkForDirection(-currentDir);
        Transform exitCrosswalk = GetCrosswalkForDirection(nextDirection);

        bool isLeftTurn = (choice == 2);

        if (entryCrosswalk != null || exitCrosswalk != null)
        {
            car.SetPedestrianStop(true, isLeftTurn);


            while (car != null && (!IsCrosswalkClear(entryCrosswalk) || !IsCrosswalkClear(exitCrosswalk)))
            {
                yield return null;
            }

            if (car == null) yield break;

            car.SetPedestrianStop(false);

            float traveledWhileWaiting = Vector3.Distance(entryPosition, carTransform.position);
            targetDistance = Mathf.Max(0.01f, targetDistance - traveledWhileWaiting);
        }

        bool isNSAxis = Mathf.Abs(currentDir.x) < Mathf.Abs(currentDir.z);
        CarYieldManager relevantManager = isNSAxis ? nsYieldManager : ewYieldManager;
        bool isLaneA = isNSAxis ? currentDir.z >= 0f : currentDir.x >= 0f;

        if (relevantManager != null)
        {
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

    Transform GetCrosswalkForDirection(Vector3 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
        {
            return dir.x >= 0f ? crosswalkEast : crosswalkWest;
        }
        else
        {
            return dir.z >= 0f ? crosswalkNorth : crosswalkSouth;
        }
    }

    bool IsCrosswalkClear(Transform crosswalk)
    {
        if (crosswalk == null) return true;

        Vector3 halfExtents = new Vector3(crosswalkWidth * 0.5f, crosswalkHeight * 0.5f, crosswalkDepth * 0.5f);
        Collider[] hits = Physics.OverlapBox(crosswalk.position, halfExtents, crosswalk.rotation, ~0, QueryTriggerInteraction.Ignore);


        foreach (Collider hit in hits)
        {
            if (hit.GetComponentInParent<NPCWalker>() != null) return false;
        }

        return true;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Vector3 size = new Vector3(crosswalkWidth, crosswalkHeight, crosswalkDepth);

        DrawCrosswalkGizmo(crosswalkNorth, size);
        DrawCrosswalkGizmo(crosswalkSouth, size);
        DrawCrosswalkGizmo(crosswalkEast, size);
        DrawCrosswalkGizmo(crosswalkWest, size);
    }

    void DrawCrosswalkGizmo(Transform crosswalk, Vector3 size)
    {
        if (crosswalk == null) return;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(crosswalk.position, crosswalk.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, size);
        Gizmos.matrix = oldMatrix;
    }
#endif
}