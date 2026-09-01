using System;
using TMPro;
using UnityEngine;

public class GameTimer : MonoBehaviour
{
    [Header("プレイヤー")]
    [SerializeField] private Transform player;

    [Header("タイム表示")]
    [SerializeField] private TextMeshProUGUI timeText;

    private float elapsedTime;

    private bool isRunning;
    private bool hasStarted;
    private bool hasFinished;

    public bool HasStarted => hasStarted;
    public bool HasFinished => hasFinished;
    public float ElapsedTime => elapsedTime;
    public event Action<float> Finished;

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

        ResetTimer();
    }

    private void Update()
    {
        if (isRunning)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimeText();
        }
    }

    public void BeginTiming()
    {
        elapsedTime = 0f;
        hasStarted = true;
        hasFinished = false;
        isRunning = true;
        UpdateTimeText();
        Debug.Log("[GameTimer] カウントダウン完了。タイム計測を開始しました。");
    }

    public void ResetTimer()
    {
        elapsedTime = 0f;
        isRunning = false;
        hasStarted = false;
        hasFinished = false;
        UpdateTimeText();
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
        Finished?.Invoke(elapsedTime);
    }

    private void UpdateTimeText()
    {
        timeText.text = "TIME: " + FormatTime(elapsedTime);
    }

    public static string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        int centiseconds = Mathf.FloorToInt((time * 100f) % 100f);

        return $"{minutes:00}:{seconds:00}.{centiseconds:00}";
    }
}
