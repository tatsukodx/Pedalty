using UnityEngine;
using TMPro;
using UnityEngine.UI;

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

    [Header("ゴール接近表示")]
    [Tooltip("この距離以内では画面端のピンを隠し、ゴール地点のピンへ切り替えます")]
    [SerializeField] private float approachDistance = 60f;
    [SerializeField] private float finalApproachDistance = 12f;

    private Vector2 maximumIndicatorSize;
    private GameObject proximityPanel;
    private RectTransform proximityPanelRect;
    private TextMeshProUGUI proximityTitle;
    private TextMeshProUGUI proximityMessage;
    private Outline proximityOutline;
    private TMP_FontAsset displayFont;
    private GoalTrigger goalTrigger;

    private void Awake()
    {
        displayFont = FindDisplayFont();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (goal != null)
        {
            goalTrigger = goal.GetComponent<GoalTrigger>();
        }

        if (indicator == null)
            Debug.LogError("[GoalDirectionIndicator] Indicatorが設定されていません。");

        if (distanceText == null && indicator != null)
            distanceText = CreateDistanceText();

        if (indicator != null)
            maximumIndicatorSize = indicator.sizeDelta;

        CreateProximityPanel();
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

        bool isGoalVisibleOnScreen = screenPosition.z > 0f &&
            screenPosition.x >= 0f && screenPosition.x <= Screen.width &&
            screenPosition.y >= 0f && screenPosition.y <= Screen.height;

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
        float displayedDistanceMeters = GetDisplayedDistance(distanceMeters);
        UpdateIndicatorVisibility(distanceMeters, isGoalVisibleOnScreen);
        UpdateDistanceText(displayedDistanceMeters);
        UpdateIndicatorSize(distanceMeters);
        UpdateProximityPanel(distanceMeters, displayedDistanceMeters);
    }

    private float GetHorizontalDistance()
    {
        if (player == null || goal == null)
            return 0f;

        Vector3 difference = goal.position - player.position;
        difference.y = 0f;
        return difference.magnitude;
    }

    private float GetDisplayedDistance(float measuredDistanceMeters)
    {
        if (goalTrigger != null && goalTrigger.IsWithinFinishDistance(player))
        {
            return 0f;
        }

        return measuredDistanceMeters;
    }

    private void UpdateIndicatorVisibility(float distanceMeters, bool isGoalVisibleOnScreen)
    {
        bool isNearGoal = distanceMeters <= approachDistance;
        bool showFarIndicator = player != null && (!isNearGoal || !isGoalVisibleOnScreen);
        if (indicator.gameObject.activeSelf != showFarIndicator)
        {
            indicator.gameObject.SetActive(showFarIndicator);
        }

        if (distanceText != null)
        {
            bool showDistanceBesidePin = showFarIndicator && !isNearGoal;
            if (distanceText.gameObject.activeSelf != showDistanceBesidePin)
            {
                distanceText.gameObject.SetActive(showDistanceBesidePin);
            }
        }
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
        text.font = displayFont;
        text.text = "0 m";
        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;

        return text;
    }

    private void CreateProximityPanel()
    {
        GameObject panelObject = new GameObject(
            "GoalProximityPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(Shadow)
        );
        panelObject.transform.SetParent(transform, false);

        proximityPanel = panelObject;
        proximityPanelRect = panelObject.GetComponent<RectTransform>();
        proximityPanelRect.anchorMin = new Vector2(0.5f, 0f);
        proximityPanelRect.anchorMax = new Vector2(0.5f, 0f);
        proximityPanelRect.pivot = new Vector2(0.5f, 0f);
        proximityPanelRect.anchoredPosition = new Vector2(0f, 32f);
        proximityPanelRect.sizeDelta = new Vector2(430f, 88f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.015f, 0.025f, 0.06f, 0.95f);
        panelImage.raycastTarget = false;

        proximityOutline = panelObject.GetComponent<Outline>();
        proximityOutline.effectColor = new Color(1f, 0.76f, 0.08f, 0.95f);
        proximityOutline.effectDistance = new Vector2(3f, -3f);

        Shadow shadow = panelObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(0f, -7f);

        CreateAccentBar(panelObject.transform, "LeftAccent", new Vector2(-211f, 0f));
        CreateAccentBar(panelObject.transform, "RightAccent", new Vector2(211f, 0f));

        proximityTitle = CreatePanelText(panelObject.transform, "GoalProximityTitle",
            new Vector2(0f, 20f), new Vector2(390f, 32f), 24f, new Color(1f, 0.79f, 0.14f, 1f));
        proximityMessage = CreatePanelText(panelObject.transform, "GoalProximityMessage",
            new Vector2(0f, -18f), new Vector2(390f, 26f), 15f, Color.white);

        proximityPanel.SetActive(false);
    }

    private void CreateAccentBar(Transform parent, string objectName, Vector2 position)
    {
        GameObject barObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barObject.transform.SetParent(parent, false);

        RectTransform rect = barObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(7f, 72f);

        Image image = barObject.GetComponent<Image>();
        image.color = new Color(1f, 0.76f, 0.08f, 1f);
        image.raycastTarget = false;
    }

    private TextMeshProUGUI CreatePanelText(Transform parent, string objectName, Vector2 position,
        Vector2 size, float fontSize, Color color)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = displayFont;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.outlineWidth = 0.14f;
        text.outlineColor = Color.black;
        return text;
    }

    private static TMP_FontAsset FindDisplayFont()
    {
        GameObject timeObject = GameObject.Find("TimeText");
        TextMeshProUGUI timeText = timeObject != null ? timeObject.GetComponent<TextMeshProUGUI>() : null;
        return timeText != null ? timeText.font : TMP_Settings.defaultFontAsset;
    }

    private void UpdateProximityPanel(float measuredDistanceMeters, float displayedDistanceMeters)
    {
        if (proximityPanel == null)
        {
            return;
        }

        bool shouldShow = player != null && measuredDistanceMeters <= approachDistance;
        if (proximityPanel.activeSelf != shouldShow)
        {
            proximityPanel.SetActive(shouldShow);
        }

        if (!shouldShow)
        {
            return;
        }

        int displayedMeters = Mathf.RoundToInt(displayedDistanceMeters);
        bool isFinalApproach = measuredDistanceMeters <= finalApproachDistance;
        proximityTitle.text = isFinalApproach ? "FINISH POINT" : "GOAL ZONE";
        proximityMessage.text = isFinalApproach
            ? $"黄色いピンの中心へ   {displayedMeters} m"
            : $"ゴール地点へ接近中   {displayedMeters} m";

        float pulse = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * 0.5f;
        proximityPanelRect.localScale = Vector3.one * Mathf.Lerp(0.99f, 1.025f, pulse);
        proximityOutline.effectColor = Color.Lerp(
            new Color(1f, 0.55f, 0.04f, 0.8f),
            new Color(1f, 0.94f, 0.45f, 1f),
            pulse
        );
    }
}
