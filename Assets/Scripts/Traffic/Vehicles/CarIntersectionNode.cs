using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CarIntersectionNode : MonoBehaviour
{
    public float straightDistance = 6f;
    public float leftTurnDistance = 6f;
    public float rightTurnDistance = 10f;

    [Header("歩行者待ちで距離を消費しても、カーブが一瞬で終わらないための最低残り距離")]
    public float minimumTurnDistance = 2f;

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

    [Header("自転車道の位置（省略可、判定用の空オブジェクトを配置してドラッグ）")]
    public Transform bikeLaneNorth;
    public Transform bikeLaneSouth;
    public Transform bikeLaneEast;
    public Transform bikeLaneWest;

    [Header("自転車道の判定範囲（TransformのローカルZ軸を車の進行方向とする）")]
    [Tooltip("自転車が走る向きに沿った幅")]
    public float bikeLaneWidth = 6f;
    [Tooltip("車の進行方向にあたる奥行き")]
    public float bikeLaneDepth = 2f;
    [Tooltip("判定する高さ（上下方向）")]
    public float bikeLaneHeight = 3f;

    private class ActiveCarInfo
    {

        public CarYieldManager manager;
        public bool isLaneA;
    }

    private readonly Dictionary<CarController, ActiveCarInfo> activeCars = new Dictionary<CarController, ActiveCarInfo>();

    // 旋回中の車がいる横断歩道を「使用中」としてロックするためのカウンター。
    // 同じ横断歩道に向かって複数台が同時に旋回することもあるため、
    // bool ではなく参照カウントで管理する。
    private readonly Dictionary<Transform, int> lockedCrosswalkCounts = new Dictionary<Transform, int>();

    private readonly HashSet<CarController> carsInTrigger = new HashSet<CarController>();

    public bool IsCrosswalkLocked(Transform crosswalk)
    {
        if (crosswalk == null) return false;
        return lockedCrosswalkCounts.TryGetValue(crosswalk, out int count) && count > 0;
    }

    public void LockCrosswalk(Transform crosswalk)
    {
        if (crosswalk == null) return;
        lockedCrosswalkCounts.TryGetValue(crosswalk, out int count);
        lockedCrosswalkCounts[crosswalk] = count + 1;
    }

    public void UnlockCrosswalk(Transform crosswalk)
    {
        if (crosswalk == null) return;
        if (lockedCrosswalkCounts.TryGetValue(crosswalk, out int count))
        {
            lockedCrosswalkCounts[crosswalk] = Mathf.Max(0, count - 1);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CarController car = other.GetComponent<CarController>();
        if (car != null)
        {
            // 交差点のトリガーに触れた時点で「進入を開始した」ものとして扱い、
            // 以降 TrafficStopZone が信号の変化で止めてしまわないようにする
            car.SetIntersectionEntered(true);
            carsInTrigger.Add(car);
            StartCoroutine(TurnSmoothly(car, other.transform));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CarController car = other.GetComponent<CarController>();
        if (car == null) return;

        carsInTrigger.Remove(car);

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

        bool preDecided = car.PlannedTurnChoice != -1;
        int choice = preDecided ? car.PlannedTurnChoice : Random.Range(0, 3);
        if (preDecided)
        {
            car.ClearPlannedTurn();
        }

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

        // 曲がる際に横切る自転車道（直進する場合は横切らない）
        Transform exitBikeLane = choice == 0 ? null : GetBikeLaneForDirection(nextDirection);

        bool isLeftTurn = (choice == 2);
        bool crosswalkNeedsLock = choice != 0 && exitCrosswalk != null;

        // 交差点に入った時点でロックする（手前のCarTurnDecisionZoneから引き継ぐ形になる）。
        // ここから先は既に交差点内なので、歩行者を通すわけにはいかない。
        bool hasLockedCrosswalk = false;

        try
        {
            if (crosswalkNeedsLock)
            {
                LockCrosswalk(exitCrosswalk);
                hasLockedCrosswalk = true;
            }

            bool needsBikeLaneCheck = exitBikeLane != null;
            bool needsEntryCheck = entryCrosswalk != null;
            bool needsExitCheck = !preDecided && exitCrosswalk != null;

            if (needsEntryCheck || needsExitCheck || needsBikeLaneCheck)
            {
                car.SetPedestrianStop(true, isLeftTurn);

                while (car != null && ((needsEntryCheck && !IsCrosswalkClear(entryCrosswalk))
                    || (needsExitCheck && !IsCrosswalkClear(exitCrosswalk))
                    || !IsBikeLaneClear(exitBikeLane)))
                {
                    yield return null;
                }

                if (car == null) yield break;

                car.SetPedestrianStop(false);

                float traveledWhileWaiting = Vector3.Distance(entryPosition, carTransform.position);
                targetDistance = Mathf.Max(minimumTurnDistance, targetDistance - traveledWhileWaiting);
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

                // 直進でも、交差点のトリガーを抜けきるまでは進入済み扱いを維持する
                while (car != null && carsInTrigger.Contains(car))
                {
                    yield return new WaitForFixedUpdate();
                }

                yield break;
            }

            Vector3 startPosition = carTransform.position;
            Quaternion startRot = Quaternion.LookRotation(currentDir);
            Quaternion endRot = Quaternion.LookRotation(nextDirection);

            while (car != null)
            {
                // 歩行者がいる間は停止指示を出すが、向きの更新は止めない。
                // 減速して止まりきるまでの間も進んだ距離に応じて曲がり続けることで、
                // 停止解除の瞬間に角度が飛ぶのを防ぐ。
                bool blockedByPedestrian = (exitCrosswalk != null && !IsCrosswalkClear(exitCrosswalk))
                    || !IsBikeLaneClear(exitBikeLane);
                car.SetPedestrianStop(blockedByPedestrian, isLeftTurn);

                float traveled = Vector3.Distance(startPosition, carTransform.position);
                float t = Mathf.Clamp01(traveled / targetDistance);

                Vector3 interpolatedDir = Quaternion.Slerp(startRot, endRot, t) * Vector3.forward;
                car.SetDirection(interpolatedDir);

                if (t >= 1f && !blockedByPedestrian) break;

                yield return new WaitForFixedUpdate();
            }

            // 旋回が終わっても、交差点のトリガーを抜けきるまでは進入済み扱いを維持する
            while (car != null && carsInTrigger.Contains(car))
            {
                yield return new WaitForFixedUpdate();
            }
        }
        finally
        {
            if (car != null)
            {
                car.SetPedestrianStop(false);
                car.SetIntersectionEntered(false);
            }

            if (hasLockedCrosswalk)
            {
                UnlockCrosswalk(exitCrosswalk);
            }
        }
    }

    public Transform GetCrosswalkForDirection(Vector3 dir)
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

    // 歩行者側で使用：進行方向ではなく「現在位置に一番近い横断歩道」を返す。
    // 歩行者は道路を横切る向き（車の進行方向とはほぼ直角）に進むため、
    // GetCrosswalkForDirection にその向きを渡すと車側とズレた横断歩道を参照してしまう。
    // 位置ベースで判定することで、車側がロックしている横断歩道と必ず一致させる。
    public Transform GetNearestCrosswalk(Vector3 position)
    {
        Transform nearest = null;
        float nearestSqrDist = float.MaxValue;

        void Check(Transform cw)
        {
            if (cw == null) return;
            float sqrDist = (cw.position - position).sqrMagnitude;
            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = cw;
            }
        }

        Check(crosswalkNorth);
        Check(crosswalkSouth);
        Check(crosswalkEast);
        Check(crosswalkWest);

        return nearest;
    }

    public Transform GetBikeLaneForDirection(Vector3 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
        {
            return dir.x >= 0f ? bikeLaneEast : bikeLaneWest;
        }
        else
        {
            return dir.z >= 0f ? bikeLaneNorth : bikeLaneSouth;
        }
    }

    public bool IsBikeLaneClear(Transform bikeLane)
    {
        if (bikeLane == null) return true;

        Vector3 halfExtents = new Vector3(bikeLaneWidth * 0.5f, bikeLaneHeight * 0.5f, bikeLaneDepth * 0.5f);
        Collider[] hits = Physics.OverlapBox(bikeLane.position, halfExtents, bikeLane.rotation, ~0, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit.GetComponentInParent<BicycleController>() != null) return false;
        }

        return true;
    }

    public bool IsCrosswalkClear(Transform crosswalk)
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
    void OnDrawGizmos()
    {
        Vector3 size = new Vector3(crosswalkWidth, crosswalkHeight, crosswalkDepth);

        Vector3 bikeSize = new Vector3(bikeLaneWidth, bikeLaneHeight, bikeLaneDepth);
        DrawBikeLaneGizmo(bikeLaneNorth, bikeSize);
        DrawBikeLaneGizmo(bikeLaneSouth, bikeSize);
        DrawBikeLaneGizmo(bikeLaneEast, bikeSize);
        DrawBikeLaneGizmo(bikeLaneWest, bikeSize);

        DrawCrosswalkGizmo(crosswalkNorth, size);
        DrawCrosswalkGizmo(crosswalkSouth, size);
        DrawCrosswalkGizmo(crosswalkEast, size);
        DrawCrosswalkGizmo(crosswalkWest, size);
    }

    void DrawBikeLaneGizmo(Transform bikeLane, Vector3 size)
    {
        if (bikeLane == null) return;

        bool occupied = !IsBikeLaneClear(bikeLane);

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(bikeLane.position, bikeLane.rotation, Vector3.one);

        Gizmos.color = occupied ? Color.red : new Color(0.3f, 0.8f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, size);

        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.15f);
        Gizmos.DrawCube(Vector3.zero, size);

        Gizmos.matrix = oldMatrix;
    }

    void DrawCrosswalkGizmo(Transform crosswalk, Vector3 size)
    {
        if (crosswalk == null) return;

        bool occupied = !IsCrosswalkClear(crosswalk);
        bool locked = IsCrosswalkLocked(crosswalk);

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(crosswalk.position, crosswalk.rotation, Vector3.one);

        if (occupied)
        {
            Gizmos.color = Color.red;
        }
        else if (locked)
        {
            Gizmos.color = Color.cyan;
        }
        else
        {
            Gizmos.color = Color.yellow;
        }

        Gizmos.DrawWireCube(Vector3.zero, size);

        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.15f);
        Gizmos.DrawCube(Vector3.zero, size);

        Gizmos.matrix = oldMatrix;
    }
#endif
}