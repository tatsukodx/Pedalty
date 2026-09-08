using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    [Header("ゲームのタイマー")]
    [SerializeField] private GameTimer gameTimer;

    [Header("ゴール判定")]
    [Tooltip("ゴール中心から進行方向へ、この距離以内に入ると到着になります")]
    [SerializeField, Min(0f)] private float finishDistanceMeters = 5f;

    [Tooltip("ゴール中心から道路の横方向へ、この距離以内に入ると到着になります")]
    [SerializeField, Min(0f)] private float finishHalfWidthMeters = 10f;

    public bool IsWithinFinishDistance(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        Vector3 difference = target.position - transform.position;
        difference.y = 0f;

        float longitudinalDistance = Mathf.Abs(Vector3.Dot(difference, transform.right));
        float lateralDistance = Mathf.Abs(Vector3.Dot(difference, transform.forward));
        return longitudinalDistance <= finishDistanceMeters &&
            lateralDistance <= finishHalfWidthMeters;
    }

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

        if (!IsWithinFinishDistance(bicycle.transform))
        {
            return;
        }

        gameTimer.Finish();
    }
}
