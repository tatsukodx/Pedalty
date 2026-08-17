using UnityEngine;
using System.Collections;

public class CarIntersectionNode : MonoBehaviour
{
    public float straightDistance = 6f;
    public float leftTurnDistance = 6f;
    public float rightTurnDistance = 10f;

    private void OnTriggerEnter(Collider other)
    {
        CarController car = other.GetComponent<CarController>();
        if (car != null)
        {
            StartCoroutine(TurnAfterDistance(car, other.transform));
        }
    }

    private IEnumerator TurnAfterDistance(CarController car, Transform carTransform)
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

        Vector3 startPosition = carTransform.position;

        while (car != null && Vector3.Distance(startPosition, carTransform.position) < targetDistance)
        {
            yield return null;
        }

        if (car != null)
        {
            car.SetDirection(nextDirection);
        }
    }
}