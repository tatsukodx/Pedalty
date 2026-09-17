using UnityEngine;
using System.Collections;

public class CarTurnDecisionZone : MonoBehaviour
{
    [Header("対応する交差点ノード")]
    public CarIntersectionNode intersection;

    [Header("停止位置")]
    [Tooltip("ゾーンに触れてからこの距離だけ進んだ位置で、歩行者待ちの停止をかける")]
    public float stopApproachDistance = 3f;

    private void OnTriggerEnter(Collider other)
    {
        CarController car = other.GetComponent<CarController>();
        if (car == null || intersection == null) return;

        if (car.PlannedTurnChoice != -1) return;

        StartCoroutine(DecideAndWait(car, other.transform));
    }

    private IEnumerator DecideAndWait(CarController car, Transform carTransform)
    {
        Vector3 currentDir = carTransform.forward;
        Vector3 rightDir = Quaternion.Euler(0, 90, 0) * currentDir;
        Vector3 leftDir = Quaternion.Euler(0, -90, 0) * currentDir;

        int choice = Random.Range(0, 3);
        car.SetPlannedTurn(choice);

        if (choice == 0) yield break;

        Vector3 nextDirection = (choice == 1) ? rightDir : leftDir;
        bool isLeftTurn = (choice == 2);

        Transform exitCrosswalk = intersection.GetCrosswalkForDirection(nextDirection);
        if (exitCrosswalk == null) yield break;

        // ゾーンに触れた瞬間に止めると停止線の手前すぎる位置で止まるため、
        // 指定距離だけ進んだ位置で止まるようにする。
        // 実際にはブレーキ距離があるので、その分だけ手前で停止指示を出す。
        Vector3 approachStart = carTransform.position;

        while (car != null && !intersection.IsCrosswalkClear(exitCrosswalk))
        {
            float traveled = Vector3.Distance(approachStart, carTransform.position);
            float remaining = stopApproachDistance - traveled;

            if (remaining <= car.EstimatedBrakingDistance) break;

            yield return null;
        }

        if (car == null) yield break;

        car.SetPedestrianStop(true, isLeftTurn);

        while (car != null && !intersection.IsCrosswalkClear(exitCrosswalk))
        {
            yield return null;
        }

        if (car == null) yield break;

        car.SetPedestrianStop(false);

        bool isLocked = false;

        try
        {
            // 交差点に入ってからでは間に合わないため、この段階でロックする。
            // ただし信号待ちや対向車線待ちで足止めされている間は、
            // 車がすぐに進めないので歩行者を通せるよう一時的に解除する。
            while (car != null && !car.HasEnteredIntersection)
            {
                bool shouldHoldLock = !car.IsWaitingForNonPedestrianReason;

                if (shouldHoldLock && !isLocked)
                {
                    intersection.LockCrosswalk(exitCrosswalk);
                    isLocked = true;
                }
                else if (!shouldHoldLock && isLocked)
                {
                    intersection.UnlockCrosswalk(exitCrosswalk);
                    isLocked = false;
                }

                yield return null;
            }
        }
        finally
        {
            // 交差点に入った後のロックはCarIntersectionNodeが引き継ぐため、ここでは必ず解除する
            if (isLocked)
            {
                intersection.UnlockCrosswalk(exitCrosswalk);
            }
        }
    }
}