using TMPro;
using UnityEngine;

public class GameTimer : MonoBehaviour
{
    [Header("プレイヤー")]
    [SerializeField] private Transform player;

    [Header("タイム表示")]
    [SerializeField] private TextMeshProUGUI timeText;

    [Header("スタート判定")]
    [Tooltip("初期位置からこの距離以上進むと計測を開始する")]
    [SerializeField] private float startDistance = 0.5f;

    private Vector3 startPosition;
    private float elapsedTime;

    private bool isRunning;
    private bool hasStarted;
    private bool hasFinished;

    public bool HasStarted => hasStarted;
    public bool HasFinished => hasFinished;
    public float ElapsedTime => elapsedTime;

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError("[GameTimer] Playerが設定されていません。");
            enabled = false;
            return;
        }

        if (timeText == null)
        {
            Debug.LogError("[GameTimer] Time Textが設定されていません。");
            enabled = false;
            return;
        }

        // 現在置かれている自転車の位置をスタート位置として記録する
        startPosition = player.position;

        elapsedTime = 0f;
        isRunning = false;
        hasStarted = false;
        hasFinished = false;

        UpdateTimeText();
    }

    private void Update()
    {
        if (!hasStarted)
        {
            CheckStart();
        }

        if (isRunning)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimeText();
        }
    }

    private void CheckStart()
    {
        // 高低差ではスタートしないよう、XとZ方向の移動量だけを使用する
        Vector3 movement = player.position - startPosition;
        movement.y = 0f;

        if (movement.magnitude >= startDistance)
        {
            hasStarted = true;
            isRunning = true;

            Debug.Log("[GameTimer] タイム計測を開始しました。");
        }
    }

    public void Finish()
    {
        // スタート前、またはすでにゴール済みなら何もしない
        if (!hasStarted || hasFinished)
        {
            return;
        }

        isRunning = false;
        hasFinished = true;

        UpdateTimeText();

        Debug.Log($"[GameTimer] ゴールしました。タイム: {FormatTime(elapsedTime)}");
    }

    private void UpdateTimeText()
    {
        timeText.text = "TIME: " + FormatTime(elapsedTime);
    }

    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        int centiseconds = Mathf.FloorToInt((time * 100f) % 100f);

        return $"{minutes:00}:{seconds:00}.{centiseconds:00}";
    }
}