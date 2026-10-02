using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 「間違ったレーンです」の赤い警告を、画面の上からのぞかせるように表示する（実行時に自動生成）
public class WrongLaneWarningUI : MonoBehaviour
{
    const float BannerHeight = 120f;
    const float SlideDuration = 0.7f;
    const float HoldDuration = 1.8f;

    static WrongLaneWarningUI instance;

    RectTransform banner;
    Coroutine routine;

    public static void Show(string message = "間違ったレーンです")
    {
        if (instance == null) instance = Create();
        instance.Play(message);
    }

    static WrongLaneWarningUI Create()
    {
        GameObject root = new GameObject("WrongLaneWarningUI");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100; // 暗転(1000)より手前に出す
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        WrongLaneWarningUI ui = root.AddComponent<WrongLaneWarningUI>();

        GameObject bannerObject = new GameObject("Banner");
        bannerObject.transform.SetParent(root.transform, false);
        Image image = bannerObject.AddComponent<Image>();
        image.color = new Color(0.85f, 0.05f, 0.05f, 0.92f);
        image.raycastTarget = false;
        ui.banner = image.rectTransform;
        ui.banner.anchorMin = new Vector2(0.5f, 1f);
        ui.banner.anchorMax = new Vector2(0.5f, 1f);
        ui.banner.pivot = new Vector2(0.5f, 0f);
        ui.banner.sizeDelta = new Vector2(900f, BannerHeight);
        ui.banner.anchoredPosition = Vector2.zero;

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(bannerObject.transform, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        GameObject timeObject = GameObject.Find("TimeText");
        TextMeshProUGUI timeText = timeObject != null ? timeObject.GetComponent<TextMeshProUGUI>() : null;
        if (timeText != null) text.font = timeText.font;
        text.fontSize = 56f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return ui;
    }

    void Play(string message)
    {
        banner.GetComponentInChildren<TextMeshProUGUI>().text = message;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PlayRoutine());
    }

    // 画面の上の外から下りてきて、しばらく表示したら上へ戻る（違反画面中も動くよう実時間で動かす）
    IEnumerator PlayRoutine()
    {
        yield return Slide(0f, -BannerHeight - 20f);
        yield return new WaitForSecondsRealtime(HoldDuration);
        yield return Slide(-BannerHeight - 20f, 0f);
        routine = null;
    }

    IEnumerator Slide(float from, float to)
    {
        float t = 0f;
        while (t < SlideDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / SlideDuration);
            // ゆっくり動き出してゆっくり止まる（滑らかに出入りさせる）
            k = k * k * (3f - 2f * k);
            banner.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, to, k));
            yield return null;
        }
        banner.anchoredPosition = new Vector2(0f, to);
    }
}
