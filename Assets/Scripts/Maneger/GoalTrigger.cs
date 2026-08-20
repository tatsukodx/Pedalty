using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    [Header("ゲームのタイマー")]
    [SerializeField] private GameTimer gameTimer;

    private void OnTriggerEnter(Collider other)
    {
        // 接触したCollider自身、またはその親に
        // BicycleControllerが付いているか確認する
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