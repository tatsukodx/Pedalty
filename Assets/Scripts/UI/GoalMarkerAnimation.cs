using UnityEngine;

public class GoalMarkerAnimation : MonoBehaviour
{
    [Header("上下移動")]
    [SerializeField] private float moveHeight = 0.5f;
    [SerializeField] private float moveSpeed = 2f;

    [Header("回転")]
    [SerializeField] private float rotationSpeed = 40f;

    private Vector3 initialPosition;

    private void Start()
    {
        initialPosition = transform.position;
    }

    private void Update()
    {
        float offset =
            Mathf.Sin(Time.time * moveSpeed) * moveHeight;

        transform.position =
            initialPosition + Vector3.up * offset;

        transform.Rotate(
            Vector3.up,
            rotationSpeed * Time.deltaTime,
            Space.World
        );
    }
}