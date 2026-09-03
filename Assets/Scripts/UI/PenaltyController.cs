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

    private FineDisplayUI fineDisplay;

    private void Start()
    {
        fineDisplay = FindAnyObjectByType<FineDisplayUI>();
        if (fineDisplay == null)
        {
            Debug.LogError("[PenaltyController] FineDisplayUIが見つかりません。");
        }

        if (violationPopup != null)
        {
            violationPopup.SetActive(false);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideViolationPopup);
        }
    }

    public void AddPenalty(int amount)
    {
        if (fineDisplay == null) return;
        fineDisplay.SetFineAmount(fineDisplay.CurrentFineAmount + amount);
    }

    public int GetCurrentPenalty()
    {
        return fineDisplay != null ? fineDisplay.CurrentFineAmount : 0;
    }

    public void ShowViolationPopup(ViolationInfo violation)
    {
        if (violationPopup == null)
        {
            Debug.LogError("[PenaltyController] violationPopupが設定されていません。");
            return;
        }

        if (categoryText != null) categoryText.text = violation.category;
        if (violationNameText != null) violationNameText.text = violation.violationName;
        if (descriptionText != null) descriptionText.text = violation.description;
        if (popupPenaltyAmountText != null) popupPenaltyAmountText.text = $"¥ {violation.penaltyAmount:N0}";

        AddPenalty(violation.penaltyAmount);
        violationPopup.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void HideViolationPopup()
    {
        if (violationPopup == null) return;
        violationPopup.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }
}
