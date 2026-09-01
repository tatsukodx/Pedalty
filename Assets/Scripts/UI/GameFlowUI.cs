using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スタート、カウントダウン、ゴール結果の画面遷移を管理する。
/// UIは実行時に生成され、左右ボタンとキーボードの代替操作に対応する。
/// </summary>
public sealed class GameFlowUI : MonoBehaviour
{
    enum FlowState
    {
        StartMenu,
        RulesPlaceholder,
        Countdown,
        Playing,
        Results
    }

    static readonly Color Yellow = new Color(1f, 0.78f, 0.18f, 1f);
    static readonly Color Cyan = new Color(0.18f, 0.74f, 0.92f, 1f);
    static readonly Color PanelBlack = new Color(0.025f, 0.035f, 0.055f, 0.94f);

    InputManager inputManager;
    GameTimer gameTimer;
    BicycleController bicycle;
    TMP_FontAsset displayFont;

    GameObject startPanel;
    GameObject rulesPanel;
    GameObject countdownPanel;
    GameObject resultsPanel;
    TextMeshProUGUI countdownText;
    TextMeshProUGUI resultTimeText;
    TextMeshProUGUI resultFineText;
    Coroutine countdownCoroutine;
    FlowState state;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateForGameScene()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        InputManager input = FindAnyObjectByType<InputManager>();
        GameTimer timer = FindAnyObjectByType<GameTimer>();
        BicycleController player = FindAnyObjectByType<BicycleController>();

        if (canvas == null || input == null || timer == null || player == null ||
            FindAnyObjectByType<GameFlowUI>() != null)
        {
            return;
        }

        GameObject flowObject = new GameObject("GameFlowUI", typeof(RectTransform));
        flowObject.transform.SetParent(canvas.transform, false);
        GameFlowUI flow = flowObject.AddComponent<GameFlowUI>();
        flow.Initialize(input, timer, player);
    }

    void Initialize(InputManager input, GameTimer timer, BicycleController player)
    {
        inputManager = input;
        gameTimer = timer;
        bicycle = player;
        displayFont = FindDisplayFont();

        BuildRoot();
        BuildStartPanel();
        BuildRulesPlaceholder();
        BuildCountdownPanel();
        BuildResultsPanel();

        inputManager.OnMenuNext?.AddListener(HandleRightButton);
        inputManager.OnMenuBack?.AddListener(HandleLeftButton);
        gameTimer.Finished += HandleGoal;

        ShowStartMenu();
    }

    void Start()
    {
        // ほかの実行時生成HUDより後ろへ回らないよう、初期化完了後に最前面へ置く。
        transform.SetAsLastSibling();
    }

    void BuildRoot()
    {
        RectTransform root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();
    }

    void BuildStartPanel()
    {
        startPanel = CreateFullScreenPanel("StartScreen", new Color(0.01f, 0.02f, 0.04f, 0.88f));
        GameObject card = CreateWindow(startPanel.transform, "StartWindow", new Vector2(620f, 420f));

        TextMeshProUGUI title = CreateText(card.transform, "Title", new Vector2(0f, 137f),
            new Vector2(570f, 72f), 58f, TextAlignmentOptions.Center, Yellow);
        title.text = "PEDALTY";

        TextMeshProUGUI subtitle = CreateText(card.transform, "Subtitle", new Vector2(0f, 76f),
            new Vector2(560f, 42f), 21f, TextAlignmentOptions.Center, Color.white);
        subtitle.text = "交通ルールを守って 最速ゴールを目指せ";

        TextMeshProUGUI prompt = CreateText(card.transform, "Prompt", new Vector2(0f, 22f),
            new Vector2(520f, 34f), 17f, TextAlignmentOptions.Center, new Color(0.78f, 0.82f, 0.88f, 1f));
        prompt.text = "左右どちらかのボタンを押してください";

        CreateChoiceCard(card.transform, "RulesChoice", new Vector2(-145f, -85f), Cyan,
            "左ボタン", "ルール説明を読む", "左クリック / J / ←");
        CreateChoiceCard(card.transform, "StartChoice", new Vector2(145f, -85f), Yellow,
            "右ボタン", "ゲームを始める", "K / →");

        TextMeshProUGUI note = CreateText(card.transform, "StartNote", new Vector2(0f, -171f),
            new Vector2(560f, 26f), 13f, TextAlignmentOptions.Center, new Color(0.66f, 0.7f, 0.76f, 1f));
        note.text = "カウントダウンのSTART表示と同時に操作できます";
    }

    void BuildRulesPlaceholder()
    {
        rulesPanel = CreateFullScreenPanel("RulesPlaceholder", new Color(0.01f, 0.02f, 0.04f, 0.92f));
        GameObject card = CreateWindow(rulesPanel.transform, "RulesWindow", new Vector2(620f, 350f));

        TextMeshProUGUI title = CreateText(card.transform, "RulesTitle", new Vector2(0f, 105f),
            new Vector2(560f, 55f), 34f, TextAlignmentOptions.Center, Yellow);
        title.text = "ルール説明";

        TextMeshProUGUI body = CreateText(card.transform, "RulesBody", new Vector2(0f, 15f),
            new Vector2(520f, 105f), 19f, TextAlignmentOptions.Center, Color.white);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.text = "ルールの紙芝居画面は次の実装で追加します。\n今回は開始・カウントダウン・終了画面を確認できます。";

        CreateChoiceCard(card.transform, "RulesBack", new Vector2(-145f, -108f), Cyan,
            "左ボタン", "スタート画面へ戻る", "左クリック / J / ←");

        TextMeshProUGUI rightNote = CreateText(card.transform, "RulesRightNote", new Vector2(145f, -108f),
            new Vector2(250f, 72f), 15f, TextAlignmentOptions.Center, new Color(0.58f, 0.62f, 0.68f, 1f));
        rightNote.text = "右ボタン：今後\n次のページへ進む操作に使用";
    }

    void BuildCountdownPanel()
    {
        countdownPanel = CreateFullScreenPanel("CountdownScreen", new Color(0f, 0f, 0f, 0.48f));
        countdownText = CreateText(countdownPanel.transform, "CountdownText", Vector2.zero,
            new Vector2(700f, 220f), 150f, TextAlignmentOptions.Center, Color.white);
        countdownText.outlineWidth = 0.25f;
        countdownText.outlineColor = Color.black;
    }

    void BuildResultsPanel()
    {
        resultsPanel = CreateFullScreenPanel("ResultScreen", new Color(0.01f, 0.02f, 0.04f, 0.9f));
        GameObject card = CreateWindow(resultsPanel.transform, "ResultWindow", new Vector2(620f, 440f));

        TextMeshProUGUI title = CreateText(card.transform, "GoalTitle", new Vector2(0f, 148f),
            new Vector2(570f, 65f), 52f, TextAlignmentOptions.Center, Yellow);
        title.text = "GOAL!";

        TextMeshProUGUI caption = CreateText(card.transform, "ResultCaption", new Vector2(0f, 101f),
            new Vector2(520f, 28f), 15f, TextAlignmentOptions.Center, new Color(0.72f, 0.77f, 0.84f, 1f));
        caption.text = "走行結果";

        resultTimeText = CreateText(card.transform, "ResultTime", new Vector2(0f, 46f),
            new Vector2(510f, 50f), 32f, TextAlignmentOptions.Center, Color.white);

        resultFineText = CreateText(card.transform, "ResultFine", new Vector2(0f, -12f),
            new Vector2(510f, 50f), 29f, TextAlignmentOptions.Center, Yellow);

        CreateChoiceCard(card.transform, "HomeChoice", new Vector2(-145f, -116f), Cyan,
            "左ボタン", "スタート画面へ戻る", "左クリック / J / ←");
        CreateChoiceCard(card.transform, "RetryChoice", new Vector2(145f, -116f), Yellow,
            "右ボタン", "リトライ", "K / →");

        TextMeshProUGUI note = CreateText(card.transform, "RetryNote", new Vector2(0f, -194f),
            new Vector2(560f, 22f), 12f, TextAlignmentOptions.Center, new Color(0.66f, 0.7f, 0.76f, 1f));
        note.text = "リトライするとタイムと罰金額が0に戻ります";
    }

    GameObject CreateFullScreenPanel(string objectName, Color color)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    GameObject CreateWindow(Transform parent, string objectName, Vector2 size)
    {
        GameObject window = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        window.transform.SetParent(parent, false);
        RectTransform rect = window.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        window.GetComponent<Image>().color = PanelBlack;
        Outline outline = window.GetComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
        return window;
    }

    void CreateChoiceCard(Transform parent, string objectName, Vector2 position, Color accent,
        string buttonLabel, string actionLabel, string keyboardLabel)
    {
        GameObject card = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        card.transform.SetParent(parent, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(250f, 112f);

        card.GetComponent<Image>().color = new Color(accent.r * 0.16f, accent.g * 0.16f, accent.b * 0.16f, 0.97f);
        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = accent;
        outline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI button = CreateText(card.transform, "ButtonLabel", new Vector2(0f, 34f),
            new Vector2(220f, 26f), 16f, TextAlignmentOptions.Center, accent);
        button.text = buttonLabel;

        TextMeshProUGUI action = CreateText(card.transform, "ActionLabel", new Vector2(0f, 2f),
            new Vector2(226f, 34f), 20f, TextAlignmentOptions.Center, Color.white);
        action.text = actionLabel;

        TextMeshProUGUI keyboard = CreateText(card.transform, "KeyboardLabel", new Vector2(0f, -36f),
            new Vector2(220f, 22f), 11f, TextAlignmentOptions.Center, new Color(0.68f, 0.72f, 0.78f, 1f));
        keyboard.text = keyboardLabel;
    }

    TextMeshProUGUI CreateText(Transform parent, string objectName, Vector2 position,
        Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
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
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static TMP_FontAsset FindDisplayFont()
    {
        GameObject timeObject = GameObject.Find("TimeText");
        TextMeshProUGUI timeText = timeObject != null ? timeObject.GetComponent<TextMeshProUGUI>() : null;
        return timeText != null ? timeText.font : TMP_Settings.defaultFontAsset;
    }

    void HandleRightButton()
    {
        switch (state)
        {
            case FlowState.StartMenu:
                StartCountdown();
                break;
            case FlowState.Results:
                StartCountdown();
                break;
        }
    }

    void HandleLeftButton()
    {
        switch (state)
        {
            case FlowState.StartMenu:
                ShowRulesPlaceholder();
                break;
            case FlowState.RulesPlaceholder:
            case FlowState.Results:
                ShowStartMenu();
                break;
        }
    }

    void ShowStartMenu()
    {
        StopCountdownIfNeeded();
        ResetRunData();
        state = FlowState.StartMenu;
        inputManager.isMenuState = true;
        SetOnlyPanel(startPanel);
    }

    void ShowRulesPlaceholder()
    {
        state = FlowState.RulesPlaceholder;
        inputManager.isMenuState = true;
        SetOnlyPanel(rulesPanel);
    }

    void StartCountdown()
    {
        StopCountdownIfNeeded();
        ResetRunData();
        state = FlowState.Countdown;
        inputManager.isMenuState = true;
        SetOnlyPanel(countdownPanel);
        countdownCoroutine = StartCoroutine(CountdownSequence());
    }

    IEnumerator CountdownSequence()
    {
        string[] numbers = { "3", "2", "1" };
        foreach (string number in numbers)
        {
            countdownText.text = number;
            countdownText.color = Color.white;
            yield return new WaitForSecondsRealtime(1f);
        }

        countdownText.text = "START";
        countdownText.color = Yellow;
        countdownText.fontSize = 105f;

        state = FlowState.Playing;
        inputManager.isMenuState = false;
        bicycle.SetControlEnabled(true);
        gameTimer.BeginTiming();

        yield return new WaitForSecondsRealtime(0.7f);
        countdownPanel.SetActive(false);
        countdownText.fontSize = 150f;
        countdownCoroutine = null;
    }

    void HandleGoal(float finalTime)
    {
        if (state != FlowState.Playing) return;

        state = FlowState.Results;
        bicycle.SetControlEnabled(false);
        inputManager.isMenuState = true;

        FineDisplayUI fineDisplay = FindAnyObjectByType<FineDisplayUI>();
        int finalFine = fineDisplay != null ? fineDisplay.CurrentFineAmount : 0;

        resultTimeText.text = "TIME  " + GameTimer.FormatTime(finalTime);
        resultFineText.text = $"現在の罰金総額  ￥{finalFine:N0}";
        SetOnlyPanel(resultsPanel);
    }

    void ResetRunData()
    {
        bicycle.SetControlEnabled(false);
        bicycle.ResetToStart();
        gameTimer.ResetTimer();

        FineDisplayUI fineDisplay = FindAnyObjectByType<FineDisplayUI>();
        fineDisplay?.SetFineAmount(0);
    }

    void SetOnlyPanel(GameObject visiblePanel)
    {
        startPanel.SetActive(visiblePanel == startPanel);
        rulesPanel.SetActive(visiblePanel == rulesPanel);
        countdownPanel.SetActive(visiblePanel == countdownPanel);
        resultsPanel.SetActive(visiblePanel == resultsPanel);
        transform.SetAsLastSibling();
    }

    void StopCountdownIfNeeded()
    {
        if (countdownCoroutine == null) return;

        StopCoroutine(countdownCoroutine);
        countdownCoroutine = null;
        countdownText.fontSize = 150f;
    }

    void OnDestroy()
    {
        if (inputManager != null)
        {
            inputManager.OnMenuNext?.RemoveListener(HandleRightButton);
            inputManager.OnMenuBack?.RemoveListener(HandleLeftButton);
        }

        if (gameTimer != null)
        {
            gameTimer.Finished -= HandleGoal;
        }
    }
}
