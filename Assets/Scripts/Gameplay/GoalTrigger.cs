using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    [Header("ゲームのタイマー")]
    [SerializeField] private GameTimer gameTimer;

    [Header("ゴール判定")]
    [Tooltip("ゴール中心から、この距離以内に入ると到着になります")]
    [SerializeField, Min(0f)] private float finishDistanceMeters = 0.49f;

    private void OnTriggerEnter(Collider other)
    {
        TryFinish(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryFinish(other);
    }

    private void TryFinish(Collider other)
    {
        BicycleController bicycle = other.GetComponentInParent<BicycleController>();

        if (bicycle == null)
        {
            return;
        }

        if (gameTimer == null)
        {
            Debug.LogError("[GoalTrigger] GameTimerが設定されていません。");
            return;
        }

        Vector3 difference = transform.position - bicycle.transform.position;
        difference.y = 0f;

        if (difference.sqrMagnitude > finishDistanceMeters * finishDistanceMeters)
        {
            return;
        }

        gameTimer.Finish();
    }
}
