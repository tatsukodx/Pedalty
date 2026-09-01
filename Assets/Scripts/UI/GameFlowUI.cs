using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        Rules,
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
    TextMeshProUGUI rulesTitleText;
    TextMeshProUGUI rulesBodyText;
    TextMeshProUGUI rulesPageText;
    TextMeshProUGUI rulesLeftActionText;
    TextMeshProUGUI rulesRightActionText;
    GameObject rulesMapPlaceholder;
    Coroutine countdownCoroutine;
    FlowState state;
    int rulesPageIndex;

    static bool startWithCountdownAfterReload;

    static readonly string[] RuleTitles =
    {
        "ゲームの目的",
        "マップとルート",
        "交通ルールと罰金",
        "ゲームフィールド",
        "自転車の操作"
    };

    static readonly string[] RuleBodies =
    {
        "このゲームは、スタート地点から黄色いピンで示されたゴール地点までのタイムを競う自転車ゲームです。\n\n" +
        "ただ速く走るだけではなく、自転車の交通ルールを守ることも大切です。違反すると内容が表示され、罰金額が加算されます。\n\n" +
        "交通ルールを守りながら、速いタイムと少ない罰金額でのゴールを目指しましょう。",

        "黄色いピンがゴール地点です。走行画面には、ゴールの方向とゴールまでの距離が表示されます。\n" +
        "交通ルールを守っていれば、どの道を通ってゴールへ向かっても構いません。",

        "自転車は車道の左側を走ることが基本です。道路標識や信号など、ゲーム内で示される交通ルールを守りましょう。\n\n" +
        "交通違反をすると、違反内容と加算される罰金額が画面に表示されます。現在の罰金総額は画面左上で確認できます。\n\n" +
        "速さだけでなく、罰金額をできるだけ低くすることも大切です。",

        "建物や道路が配置されている範囲がゲームフィールドです。フィールドの外へ出ることは禁止されています。\n\n" +
        "道路から大きく外れないように注意してください。フィールド外へ落ちた場合は、安全のためスタート地点へ戻されます。",

        "実際に自転車をこぐと、ゲーム内の自転車も前へ進みます。こぐ速さはゲーム内の速度に反映されます。\n\n" +
        "右ボタン：ベルを鳴らす\n左ボタン：ゲーム内のブレーキ\n\n" +
        "実物自転車のブレーキレバーはゲーム内のブレーキに連動していません。止まりたいときは、ハンドルに取り付けられた左ボタンを押してください。"
    };

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
        BuildRulesPanel();
        BuildCountdownPanel();
        BuildResultsPanel();

        inputManager.OnMenuNext?.AddListener(HandleRightButton);
        inputManager.OnMenuBack?.AddListener(HandleLeftButton);
        gameTimer.Finished += HandleGoal;

        if (startWithCountdownAfterReload)
        {
            startWithCountdownAfterReload = false;
            StartCountdown();
        }
        else
        {
            ShowStartMenu();
        }
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

    void BuildRulesPanel()
    {
        rulesPanel = CreateFullScreenPanel("RulesScreen", new Color(0.01f, 0.02f, 0.04f, 0.94f));
        GameObject card = CreateWindow(rulesPanel.transform, "RulesWindow", new Vector2(680f, 520f));

        rulesTitleText = CreateText(card.transform, "RulesTitle", new Vector2(0f, 205f),
            new Vector2(620f, 52f), 32f, TextAlignmentOptions.Center, Yellow);

        rulesPageText = CreateText(card.transform, "RulesPage", new Vector2(0f, 167f),
            new Vector2(600f, 24f), 13f, TextAlignmentOptions.Center, new Color(0.65f, 0.7f, 0.77f, 1f));

        rulesBodyText = CreateText(card.transform, "RulesBody", new Vector2(0f, 30f),
            new Vector2(590f, 245f), 18f, TextAlignmentOptions.TopLeft, Color.white);
        rulesBodyText.textWrappingMode = TextWrappingModes.Normal;
        rulesBodyText.lineSpacing = 5f;

        rulesMapPlaceholder = new GameObject("MapImagePlaceholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        rulesMapPlaceholder.transform.SetParent(card.transform, false);
        RectTransform mapRect = rulesMapPlaceholder.GetComponent<RectTransform>();
        mapRect.anchorMin = new Vector2(0.5f, 0.5f);
        mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapRect.pivot = new Vector2(0.5f, 0.5f);
        mapRect.anchoredPosition = new Vector2(0f, -22f);
        mapRect.sizeDelta = new Vector2(330f, 145f);
        rulesMapPlaceholder.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.12f, 1f);
        Outline mapOutline = rulesMapPlaceholder.GetComponent<Outline>();
        mapOutline.effectColor = new Color(0.55f, 0.6f, 0.67f, 1f);
        mapOutline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI mapLabel = CreateText(rulesMapPlaceholder.transform, "MapPlaceholderLabel", Vector2.zero,
            new Vector2(300f, 100f), 18f, TextAlignmentOptions.Center, new Color(0.65f, 0.7f, 0.77f, 1f));
        mapLabel.text = "MAP IMAGE\nマップ画像をここに配置";

        rulesLeftActionText = CreateChoiceCard(card.transform, "RulesLeft", new Vector2(-150f, -197f), Cyan,
            "左ボタン", "前のページへ", "左クリック / J / ←");
        rulesRightActionText = CreateChoiceCard(card.transform, "RulesRight", new Vector2(150f, -197f), Yellow,
            "右ボタン", "次のページへ", "K / →");
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

    TextMeshProUGUI CreateChoiceCard(Transform parent, string objectName, Vector2 position, Color accent,
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
        return action;
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
            case FlowState.Rules:
                if (rulesPageIndex < RuleTitles.Length - 1)
                {
                    rulesPageIndex++;
                    UpdateRulesPage();
                }
                else
                {
                    ShowStartMenu();
                }
                break;
            case FlowState.Results:
                ReloadGameScene(true);
                break;
        }
    }

    void HandleLeftButton()
    {
        switch (state)
        {
            case FlowState.StartMenu:
                ShowRules();
                break;
            case FlowState.Rules:
                if (rulesPageIndex > 0)
                {
                    rulesPageIndex--;
                    UpdateRulesPage();
                }
                else
                {
                    ShowStartMenu();
                }
                break;
            case FlowState.Results:
                ReloadGameScene(false);
                break;
        }
    }

    void ShowStartMenu()
    {
        StopCountdownIfNeeded();
        ResetRunData();
        state = FlowState.StartMenu;
        inputManager.isMenuState = true;
        Time.timeScale = 0f;
        SetOnlyPanel(startPanel);
    }

    void ShowRules()
    {
        state = FlowState.Rules;
        inputManager.isMenuState = true;
        Time.timeScale = 0f;
        rulesPageIndex = 0;
        UpdateRulesPage();
        SetOnlyPanel(rulesPanel);
    }

    void UpdateRulesPage()
    {
        rulesTitleText.text = RuleTitles[rulesPageIndex];
        rulesBodyText.text = RuleBodies[rulesPageIndex];
        rulesPageText.text = $"{rulesPageIndex + 1} / {RuleTitles.Length}";

        bool isMapPage = rulesPageIndex == 1;
        rulesMapPlaceholder.SetActive(isMapPage);
        RectTransform bodyRect = rulesBodyText.rectTransform;
        bodyRect.anchoredPosition = isMapPage ? new Vector2(0f, 103f) : new Vector2(0f, 30f);
        bodyRect.sizeDelta = isMapPage ? new Vector2(590f, 100f) : new Vector2(590f, 245f);

        rulesLeftActionText.text = rulesPageIndex == 0 ? "スタート画面へ戻る" : "前のページへ";
        rulesRightActionText.text = rulesPageIndex == RuleTitles.Length - 1 ? "説明を終了する" : "次のページへ";
    }

    void StartCountdown()
    {
        StopCountdownIfNeeded();
        ResetRunData();
        state = FlowState.Countdown;
        inputManager.isMenuState = true;
        Time.timeScale = 0f;
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
        Time.timeScale = 1f;
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
        Time.timeScale = 0f;

        FineDisplayUI fineDisplay = FindAnyObjectByType<FineDisplayUI>();
        int finalFine = fineDisplay != null ? fineDisplay.CurrentFineAmount : 0;

        resultTimeText.text = "TIME  " + GameTimer.FormatTime(finalTime);
        resultFineText.text = $"現在の罰金総額  ￥{finalFine:N0}";
        SetOnlyPanel(resultsPanel);
    }

    void ReloadGameScene(bool beginWithCountdown)
    {
        state = FlowState.Countdown;
        inputManager.isMenuState = true;
        bicycle.SetControlEnabled(false);
        startWithCountdownAfterReload = beginWithCountdown;
        Time.timeScale = 1f;

        Scene activeScene = SceneManager.GetActiveScene();
        int buildIndex = activeScene.buildIndex >= 0 ? activeScene.buildIndex : 0;
        SceneManager.LoadScene(buildIndex);
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
        Time.timeScale = 1f;

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
