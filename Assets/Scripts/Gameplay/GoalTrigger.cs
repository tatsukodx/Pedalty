using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    [Header("ゲームのタイマー")]
    [SerializeField] private GameTimer gameTimer;

    private void OnTriggerEnter(Collider other)
    {
        BicycleController bicycle =
            other.GetComponentInParent<BicycleController>();

        if (bicycle == null)
        {
            return;
        }

        if (gameTimer == null)
        {
            Debug.LogError("[GoalTrigger] GameTimerが設定されていません。");
            return;
        }

        gameTimer.Finish();
    }
}