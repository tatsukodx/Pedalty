using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面右下のブレーキ表示の上に、ベルの鳴動状態を表示する。
/// InputManagerがあるシーンで自動生成されるため、Inspectorでの配線は不要。
/// </summary>
public sealed class BellIndicatorUI : MonoBehaviour
{
    const float RingDisplaySeconds = 0.65f;

    static readonly Color InactiveIconColor = new Color(0.58f, 0.62f, 0.66f, 1f);
    static readonly Color ActiveIconColor = new Color(1f, 0.72f, 0.08f, 1f);

    InputManager inputManager;
    Image iconImage;
    TextMeshProUGUI stateText;
    Sprite idleSprite;
    Sprite ringingSprite;
    Coroutine resetCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateForGameScene()
    {
        InputManager input = FindAnyObjectByType<InputManager>();
        Canvas canvas = FindAnyObjectByType<Canvas>();

        if (input == null || canvas == null || FindAnyObjectByType<BellIndicatorUI>() != null)
        {
            return;
        }

        GameObject indicatorObject = new GameObject("BellIndicatorUI", typeof(RectTransform));
        indicatorObject.transform.SetParent(canvas.transform, false);
        BellIndicatorUI indicator = indicatorObject.AddComponent<BellIndicatorUI>();
        indicator.Initialize(input);
    }

    void Initialize(InputManager input)
    {
        inputManager = input;
        BuildVisuals();
        inputManager.OnBellRing?.AddListener(ShowRinging);
        ShowIdle();
    }

    void BuildVisuals()
    {
        RectTransform root = (RectTransform)transform;
        root.anchorMin = new Vector2(1f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(1f, 0f);
        root.anchoredPosition = new Vector2(-12f, 96f);
        root.sizeDelta = new Vector2(76f, 76f);

        idleSprite = Resources.Load<Sprite>("UI/BellIconIdle");
        ringingSprite = Resources.Load<Sprite>("UI/BellIconRinging");

        GameObject iconObject = new GameObject("BellIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(68f, 68f);

        iconImage = iconObject.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        GameObject textObject = new GameObject("BellStateText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
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

    public void ShowRinging()
    {
        if (iconImage == null || stateText == null) return;

        iconImage.sprite = ringingSprite;
        iconImage.color = ActiveIconColor;
        stateText.color = Color.white;
        stateText.text = "BELL\nRING";

        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
        }
        resetCoroutine = StartCoroutine(ResetAfterDelay());
    }

    IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSecondsRealtime(RingDisplaySeconds);
        resetCoroutine = null;
        ShowIdle();
    }

    void ShowIdle()
    {
        if (iconImage == null || stateText == null) return;

        iconImage.sprite = idleSprite;
        iconImage.color = InactiveIconColor;
        stateText.color = Color.white;
        stateText.text = "BELL\nREADY";
    }

    void OnDestroy()
    {
        if (inputManager != null)
        {
            inputManager.OnBellRing?.RemoveListener(ShowRinging);
        }
    }
}
