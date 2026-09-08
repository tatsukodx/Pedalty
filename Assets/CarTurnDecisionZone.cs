using UnityEngine;
using System.Collections;

public class CarTurnDecisionZone : MonoBehaviour
{
    [Header("対応する交差点ノード")]
    public CarIntersectionNode intersection;

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

        car.SetPedestrianStop(true, isLeftTurn);

        while (car != null && !intersection.IsCrosswalkClear(exitCrosswalk))
        {
            yield return null;
        }

        if (car == null) yield break;

        car.SetPedestrianStop(false);
    }
}