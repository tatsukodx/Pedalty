using UnityEngine;
using System.Collections;

public class CarIntersectionNode : MonoBehaviour
{
    public float straightDelay = 0.5f;
    public float leftTurnDelay = 0.5f;
    public float rightTurnDelay = 1f;

    private void OnTriggerEnter(Collider other)
    {
        CarController car = other.GetComponent<CarController>();
        if (car != null)
        {
            StartCoroutine(TurnWithDelay(car, other.transform));
        }
    }

    private IEnumerator TurnWithDelay(CarController car, Transform carTransform)
    {
        Vector3 currentDir = carTransform.forward;
        Vector3 rightDir = Quaternion.Euler(0, 90, 0) * currentDir;
        Vector3 leftDir = Quaternion.Euler(0, -90, 0) * currentDir;

        Vector3 nextDirection = currentDir;
        float delay = straightDelay;
        int choice = Random.Range(0, 3);

        switch (choice)
        {
            case 0:
                nextDirection = currentDir;
                delay = straightDelay;
                break;
            case 1:
                nextDirection = rightDir;
                delay = rightTurnDelay;
                break;
            case 2:
                nextDirection = leftDir;
                delay = leftTurnDelay;
                break;
        }

        yield return new WaitForSeconds(delay);

        if (car != null)
        {
            car.SetDirection(nextDirection);
        }
    }
}