using UnityEngine;
using System.Collections;

public class CarIntersectionNode : MonoBehaviour
{
    public float straightDistance = 6f;
    public float leftTurnDistance = 6f;
    public float rightTurnDistance = 10f;

    [Header("対向車線との譲り合い（省略可）")]
    public CarYieldManager yieldManager;
    public bool isLaneA = true;

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
        if (car != null && yieldManager != null)
        {
            yieldManager.ReportIntersectionExit(isLaneA);
        }
    }

    private IEnumerator TurnSmoothly(CarController car, Transform carTransform)
    {
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

        if (yieldManager != null)
        {
            car.SetYieldStop(true);

            while (car != null && !yieldManager.CanEnter(isLaneA))
            {
                yield return null;
            }

            if (car == null) yield break;

            car.SetYieldStop(false);
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
}