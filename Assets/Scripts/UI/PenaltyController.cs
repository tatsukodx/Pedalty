using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PenaltyController : MonoBehaviour
{
    [Header("違反ポップアップ")]
    [SerializeField] private GameObject violationPopup;
    [SerializeField] private TMP_Text categoryText;
    [SerializeField] private TMP_Text violationNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text popupPenaltyAmountText;
    [SerializeField] private Button closeButton;

    [Header("ポップアップの演出")]
    [Tooltip("上から落ちてくるまでの時間[秒]")]
    [SerializeField] private float dropDuration = 0.35f;
    [Tooltip("着地後に画面が揺れる時間[秒]")]
    [SerializeField] private float shakeDuration = 0.4f;
    [Tooltip("ポップアップの揺れ幅[px]")]
    [SerializeField] private float popupShakeAmount = 18f;
    [Tooltip("画面（カメラ）の揺れ幅[m]")]
    [SerializeField] private float cameraShakeAmount = 0.12f;

    private RectTransform popupRect;
    private Vector2 popupHomePosition;
    private Coroutine popupAnimation;

    private FineDisplayUI fineDisplay;
    private InputManager inputManager;
    private int violationCount;

    public int CurrentViolationCount => violationCount;

    public bool IsViolationPopupVisible => violationPopup != null && violationPopup.activeSelf;

    private void Start()
    {
        fineDisplay = FindAnyObjectByType<FineDisplayUI>();
        if (fineDisplay == null)
        {
            Debug.LogError("[PenaltyController] FineDisplayUIが見つかりません。");
        }

        if (violationPopup != null)
        {
            popupRect = violationPopup.GetComponent<RectTransform>();
            if (popupRect != null) popupHomePosition = popupRect.anchoredPosition;
            violationPopup.SetActive(false);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideViolationPopup);
        }

        // Arduinoの物理ボタンはキーボード入力として届かないため、InputManager経由で受け取る
        inputManager = FindAnyObjectByType<InputManager>();
        inputManager?.OnViolationPopupBack?.AddListener(HideViolationPopup);
    }

    private void OnDestroy()
    {
        inputManager?.OnViolationPopupBack?.RemoveListener(HideViolationPopup);
    }

    public void AddPenalty(int amount)
    {
        if (GameDebugMode.IsEnabled) return;
        if (fineDisplay == null) return;
        fineDisplay.SetFineAmount(fineDisplay.CurrentFineAmount + amount);
    }

    public int GetCurrentPenalty()
    {
        return fineDisplay != null ? fineDisplay.CurrentFineAmount : 0;
    }

    public void ShowViolationPopup(ViolationInfo violation)
    {
        if (GameDebugMode.IsEnabled) return;

        if (violationPopup == null)
        {
            Debug.LogError("[PenaltyController] violationPopupが設定されていません。");
            return;
        }

        if (categoryText != null) categoryText.text = violation.category;
        if (violationNameText != null) violationNameText.text = violation.violationName;
        if (descriptionText != null) descriptionText.text = violation.description;
        if (popupPenaltyAmountText != null) popupPenaltyAmountText.text = $"¥ {violation.penaltyAmount:N0}";

        violationCount++;
        AddPenalty(violation.penaltyAmount);
        violationPopup.SetActive(true);
        PlayPopupAnimation();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    /// <summary>
    /// デバッグ開始時に残っている違反画面だけを閉じる。
    /// カウントダウン中の停止状態を保つため、Time.timeScaleは変更しない。
    /// </summary>
    public void ClearViolationPopupForDebugMode()
    {
        StopPopupAnimation();
        if (violationPopup != null)
        {
            violationPopup.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void PlayPopupAnimation()
    {
        StopPopupAnimation();
        if (popupRect != null) popupAnimation = StartCoroutine(DropAndShake());
    }

    void StopPopupAnimation()
    {
        if (popupAnimation != null)
        {
            StopCoroutine(popupAnimation);
            popupAnimation = null;
        }
        if (popupRect != null) popupRect.anchoredPosition = popupHomePosition;
        CameraController.ShakeOffset = Vector3.zero;
    }

    // 違反画面中は Time.timeScale = 0 なので、実時間（unscaled）で動かす
    IEnumerator DropAndShake()
    {
        // 画面の上の外から落とす
        RectTransform parent = popupRect.parent as RectTransform;
        float screenHeight = parent != null ? parent.rect.height : 1080f;
        Vector2 start = popupHomePosition + new Vector2(0f, screenHeight + popupRect.rect.height);

        float t = 0f;
        while (t < dropDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dropDuration);
            // 加速しながら落ちる
            popupRect.anchoredPosition = Vector2.LerpUnclamped(start, popupHomePosition, k * k);
            yield return null;
        }
        popupRect.anchoredPosition = popupHomePosition;

        // 着地の衝撃で画面とポップアップを揺らす（だんだん弱く）
        t = 0f;
        while (t < shakeDuration)
        {
            t += Time.unscaledDeltaTime;
            float strength = 1f - Mathf.Clamp01(t / shakeDuration);
            strength *= strength;

            Vector2 popupOffset = Random.insideUnitCircle * popupShakeAmount * strength;
            popupRect.anchoredPosition = popupHomePosition + popupOffset;

            Vector2 cameraOffset = Random.insideUnitCircle * cameraShakeAmount * strength;
            CameraController.ShakeOffset = new Vector3(cameraOffset.x, cameraOffset.y, 0f);
            yield return null;
        }

        popupRect.anchoredPosition = popupHomePosition;
        CameraController.ShakeOffset = Vector3.zero;
        popupAnimation = null;
    }

    public void HideViolationPopup()
    {
        if (violationPopup == null) return;
        StopPopupAnimation();
        violationPopup.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }

    public void ResetRunStatistics()
    {
        violationCount = 0;
    }
}
