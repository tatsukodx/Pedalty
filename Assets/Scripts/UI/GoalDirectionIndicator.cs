using UnityEngine;
using TMPro;

public class GoalDirectionIndicator : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Transform goal;
    [SerializeField] private Transform player;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private RectTransform indicator;

    [Header("距離表示")]
    [SerializeField] private TextMeshProUGUI distanceText;

    [Header("画面端からの余白")]
    [SerializeField] private float screenPadding = 70f;

    [Header("距離によるピンサイズ")]
    [Tooltip("この距離以内で現在の大きさ（最大）になる")]
    [SerializeField] private float nearDistance = 30f;
    [Tooltip("この距離以上で最小サイズになる")]
    [SerializeField] private float farDistance = 500f;
    [Range(0.1f, 1f)]
    [SerializeField] private float minimumSizeRatio = 0.5f;

    private Vector2 maximumIndicatorSize;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (indicator == null)
            Debug.LogError("[GoalDirectionIndicator] Indicatorが設定されていません。");

        if (distanceText == null && indicator != null)
            distanceText = CreateDistanceText();

        if (indicator != null)
            maximumIndicatorSize = indicator.sizeDelta;
    }

    private void LateUpdate()
    {
        if (goal == null ||
            targetCamera == null ||
            indicator == null)
        {
            return;
        }

        Vector3 screenPosition =
            targetCamera.WorldToScreenPoint(goal.position);

        // カメラの後方にある場合は、反対側の画面端へ表示する。
        if (screenPosition.z < 0f)
        {
            screenPosition.x =
                Screen.width - screenPosition.x;

            screenPosition.y =
                Screen.height - screenPosition.y;
        }

        screenPosition.x = Mathf.Clamp(
            screenPosition.x,
            screenPadding,
            Screen.width - screenPadding
        );

        screenPosition.y = Mathf.Clamp(
            screenPosition.y,
            screenPadding,
            Screen.height - screenPadding
        );

        indicator.position = screenPosition;

        float distanceMeters = GetHorizontalDistance();
        UpdateDistanceText(distanceMeters);
        UpdateIndicatorSize(distanceMeters);
    }

    private float GetHorizontalDistance()
    {
        if (player == null || goal == null)
            return 0f;

        Vector3 difference = goal.position - player.position;
        difference.y = 0f;
        return difference.magnitude;
    }

    private void UpdateDistanceText(float distanceMeters)
    {
        if (distanceText == null || player == null || goal == null)
            return;

        distanceText.text = distanceMeters < 1000f
            ? $"{Mathf.RoundToInt(distanceMeters)} m"
            : $"{distanceMeters / 1000f:F1} km";
    }

    private void UpdateIndicatorSize(float distanceMeters)
    {
        if (indicator == null)
            return;

        // farDistanceで最小、nearDistanceで最大（現在の35 x 45）になる。
        float proximity = Mathf.InverseLerp(farDistance, nearDistance, distanceMeters);
        float sizeRatio = Mathf.Lerp(minimumSizeRatio, 1f, proximity);
        indicator.sizeDelta = maximumIndicatorSize * sizeRatio;
    }

    private TextMeshProUGUI CreateDistanceText()
    {
        GameObject textObject = new GameObject(
            "GoalDistanceText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(indicator, false);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 6f);
        rect.sizeDelta = new Vector2(120f, 32f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = "0 m";
        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;

        return text;
    }
}
