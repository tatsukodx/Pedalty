using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面左上に現在の罰金額を表示する。
/// 将来の罰金処理からSetFineAmountを呼ぶことで表示額を更新できる。
/// </summary>
public sealed class FineDisplayUI : MonoBehaviour
{
    const int InitialFineAmount = 0;

    TextMeshProUGUI amountText;
    int currentFineAmount;

    public int CurrentFineAmount => currentFineAmount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateForGameScene()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        InputManager input = FindAnyObjectByType<InputManager>();

        if (canvas == null || input == null || FindAnyObjectByType<FineDisplayUI>() != null)
        {
            return;
        }

        GameObject displayObject = new GameObject("FineDisplayUI", typeof(RectTransform));
        displayObject.transform.SetParent(canvas.transform, false);
        FineDisplayUI display = displayObject.AddComponent<FineDisplayUI>();
        display.BuildVisuals();
        display.SetFineAmount(InitialFineAmount);
    }

    void BuildVisuals()
    {
        RectTransform root = (RectTransform)transform;
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(12f, -12f);
        root.sizeDelta = new Vector2(150f, 64f);

        Image background = gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.78f);
        background.raycastTarget = false;

        Outline border = gameObject.AddComponent<Outline>();
        border.effectColor = Color.white;
        border.effectDistance = new Vector2(2f, -2f);
        border.useGraphicAlpha = true;

        TMP_FontAsset displayFont = FindDisplayFont();

        TextMeshProUGUI labelText = CreateText(
            "FineLabel",
            new Vector2(8f, -4f),
            new Vector2(134f, 22f),
            14f,
            TextAlignmentOptions.Left,
            displayFont);
        labelText.text = "現在の罰金総額";
        labelText.color = new Color(1f, 0.78f, 0.18f, 1f);

        amountText = CreateText(
            "FineAmountText",
            new Vector2(8f, -25f),
            new Vector2(134f, 32f),
            24f,
            TextAlignmentOptions.Right,
            displayFont);
        amountText.color = new Color(1f, 0.78f, 0.18f, 1f);
    }

    TextMeshProUGUI CreateText(
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment,
        TMP_FontAsset font)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        if (font != null)
        {
            text.font = font;
        }
        return text;
    }

    static TMP_FontAsset FindDisplayFont()
    {
        GameObject timeTextObject = GameObject.Find("TimeText");
        return timeTextObject != null
            ? timeTextObject.GetComponent<TextMeshProUGUI>()?.font
            : null;
    }

    public void SetFineAmount(int amount)
    {
        currentFineAmount = Mathf.Max(0, amount);
        if (amountText != null)
        {
            amountText.text = $"￥{currentFineAmount:N0}";
        }
    }
}
