using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 企画書のレイアウトに合わせ、画面右下にブレーキ状態を表示する。
/// InputManagerがあるシーンで自動的に生成されるため、Inspectorでの配線は不要。
/// </summary>
public sealed class BrakeIndicatorUI : MonoBehaviour
{
    static readonly Color InactiveIconColor = new Color(0.58f, 0.62f, 0.66f, 1f);
    static readonly Color ActiveIconColor = new Color(1f, 0.08f, 0.04f, 1f);

    InputManager inputManager;
    Image panelImage;
    Image iconImage;
    TextMeshProUGUI stateText;
    Sprite brakeOffSprite;
    Sprite brakeOnSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateForGameScene()
    {
        InputManager input = FindAnyObjectByType<InputManager>();
        Canvas canvas = FindAnyObjectByType<Canvas>();

        if (input == null || canvas == null || FindAnyObjectByType<BrakeIndicatorUI>() != null)
        {
            return;
        }

        GameObject indicatorObject = new GameObject("BrakeIndicatorUI", typeof(RectTransform));
        indicatorObject.transform.SetParent(canvas.transform, false);
        BrakeIndicatorUI indicator = indicatorObject.AddComponent<BrakeIndicatorUI>();
        indicator.Initialize(input);
    }

    void Initialize(InputManager input)
    {
        inputManager = input;
        BuildVisuals();
        inputManager.OnBrake.AddListener(SetBrakeState);
        SetBrakeState(inputManager.IsBraking);
    }

    void BuildVisuals()
    {
        RectTransform root = (RectTransform)transform;
        root.anchorMin = new Vector2(1f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(1f, 0f);
        root.anchoredPosition = new Vector2(-12f, 12f);
        root.sizeDelta = new Vector2(76f, 76f);

        panelImage = gameObject.AddComponent<Image>();
        panelImage.color = Color.clear;
        panelImage.raycastTarget = false;

        brakeOnSprite = Resources.Load<Sprite>("UI/BrakeIcon");
        brakeOffSprite = Resources.Load<Sprite>("UI/BrakeIconOff");

        GameObject iconObject = new GameObject("BrakeIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(68f, 68f);

        iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = brakeOffSprite;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        GameObject textObject = new GameObject("BrakeStateText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, -18f);
        textRect.sizeDelta = new Vector2(58f, 24f);

        stateText = textObject.GetComponent<TextMeshProUGUI>();
        stateText.alignment = TextAlignmentOptions.Center;
        stateText.fontSize = 10f;
        stateText.fontStyle = FontStyles.Bold;
        stateText.lineSpacing = -8f;
        stateText.outlineWidth = 0.25f;
        stateText.outlineColor = Color.black;
        stateText.raycastTarget = false;
    }

    public void SetBrakeState(bool braking)
    {
        if (panelImage == null || iconImage == null || stateText == null) return;

        panelImage.color = Color.clear;
        iconImage.sprite = braking ? brakeOnSprite : brakeOffSprite;
        iconImage.color = braking ? ActiveIconColor : InactiveIconColor;
        stateText.color = Color.white;
        stateText.text = braking ? "BRAKE\nON" : "BRAKE\nOFF";
    }

    void OnDestroy()
    {
        if (inputManager != null)
        {
            inputManager.OnBrake.RemoveListener(SetBrakeState);
        }
    }
}
