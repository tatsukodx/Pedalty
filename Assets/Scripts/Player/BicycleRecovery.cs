using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 違反のない状態で走っていた座標を記憶しておき、衝突・違反時に暗転して
// 「最後に違反していなかった位置」へ戻す。向きはそのレーンの進行方向へ揃える。
public class BicycleRecovery : MonoBehaviour
{
    [Tooltip("安全な座標を記録する間隔[秒]")]
    public float sampleInterval = 0.2f;

    [Tooltip("記録しておく最大数（sampleInterval × この数 = 遡れる秒数）")]
    public int maxSamples = 150;

    [Tooltip("衝突時は衝突地点からこの距離以上手前の記録へ戻す")]
    public float collisionBackDistance = 3f;

    public float fadeDuration = 0.3f;

    struct SafePoint
    {
        public Vector3 position;
        public Vector3 laneForward;
    }

    readonly List<SafePoint> history = new List<SafePoint>();
    BicycleController bicycle;
    PlayerLaneDetector laneDetector;
    Rigidbody rb;
    Image fadeImage;
    float sampleTimer;

    public bool IsRecovering { get; private set; }

    void Awake()
    {
        bicycle = GetComponent<BicycleController>();
        laneDetector = GetComponent<PlayerLaneDetector>();
        rb = GetComponent<Rigidbody>();
        CreateFader();
    }

    void CreateFader()
    {
        GameObject canvasObject = new GameObject("RecoveryFader");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        GameObject imageObject = new GameObject("Fade");
        imageObject.transform.SetParent(canvasObject.transform, false);
        fadeImage = imageObject.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;
        RectTransform rt = fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // リスタート時に呼ぶ。途中の暗転も解除する
    public void CancelAndClear()
    {
        StopAllCoroutines();
        IsRecovering = false;
        if (fadeImage != null) fadeImage.color = new Color(0f, 0f, 0f, 0f);
        history.Clear();
        sampleTimer = 0f;
    }

    void Update()
    {
        if (IsRecovering || bicycle == null || !bicycle.ControlEnabled) return;

        sampleTimer += Time.deltaTime;
        if (sampleTimer < sampleInterval) return;
        sampleTimer = 0f;

        TrafficViolationDetector detector = TrafficViolationDetector.Instance;
        if (detector == null || !detector.IsPlayerInLegalLane) return;
        if (laneDetector == null || laneDetector.currentLaneForward == Vector3.zero) return;

        history.Add(new SafePoint { position = transform.position, laneForward = laneDetector.currentLaneForward });
        if (history.Count > maxSamples) history.RemoveAt(0);
    }

    // 衝突時: 衝突地点から一定距離手前の安全な位置へ戻す
    public void RecoverFromCollision(Action onFinished = null)
    {
        Recover(transform.position, collisionBackDistance, null, 0f, onFinished);
    }

    // 違反時: 違反前の安全な位置へ戻す。avoidCenter を指定するとその範囲（交差点など）の外の記録を選ぶ
    public void RecoverFromViolation(Vector3? avoidCenter, float avoidRadius, Action onFinished = null)
    {
        Recover(transform.position, 0f, avoidCenter, avoidRadius, onFinished);
    }

    void Recover(Vector3 from, float minBackDistance, Vector3? avoidCenter, float avoidRadius, Action onFinished)
    {
        if (IsRecovering)
        {
            onFinished?.Invoke();
            return;
        }

        StartCoroutine(RecoverRoutine(from, minBackDistance, avoidCenter, avoidRadius, onFinished));
    }

    int FindTargetIndex(Vector3 from, float minBackDistance, Vector3? avoidCenter, float avoidRadius)
    {
        for (int i = history.Count - 1; i >= 0; i--)
        {
            Vector3 p = history[i].position;
            if (minBackDistance > 0f && FlatDistance(p, from) < minBackDistance) continue;
            if (avoidCenter.HasValue && FlatDistance(p, avoidCenter.Value) <= avoidRadius) continue;
            return i;
        }
        return history.Count > 0 ? 0 : -1;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    IEnumerator RecoverRoutine(Vector3 from, float minBackDistance, Vector3? avoidCenter, float avoidRadius, Action onFinished)
    {
        IsRecovering = true;
        bool wasEnabled = bicycle.ControlEnabled;
        bicycle.SetControlEnabled(false);

        yield return Fade(0f, 1f);

        int index = FindTargetIndex(from, minBackDistance, avoidCenter, avoidRadius);
        if (index >= 0)
        {
            SafePoint point = history[index];
            history.RemoveRange(index + 1, history.Count - index - 1);
            bicycle.TeleportTo(point.position, Quaternion.LookRotation(point.laneForward));
        }
        else
        {
            bicycle.TeleportTo(bicycle.StartPosition, bicycle.StartRotation);
        }
        Physics.SyncTransforms();

        yield return new WaitForSecondsRealtime(0.15f);
        yield return Fade(1f, 0f);

        if (wasEnabled) bicycle.SetControlEnabled(true);
        IsRecovering = false;
        onFinished?.Invoke();
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / fadeDuration));
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, to);
    }
}
