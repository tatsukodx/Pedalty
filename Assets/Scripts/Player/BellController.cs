using UnityEngine;

public class BellController : MonoBehaviour
{
    [Header("ベルの音")]
    [Tooltip("鳴らしたい音声ファイル（.wav / .mp3 / .ogg）。未設定だとベルは無音になります")]
    public AudioClip bellClip;

    [Header("警音器使用制限違反の判定")]
    [Tooltip("この距離内に歩行者や車がいるときは危険回避のためのベルとみなし、違反にしない")]
    public float hazardCheckRadius = 3f;

    private AudioSource audioSource;
    private PenaltyController penaltyController;

    void Awake()
    {
        penaltyController = FindAnyObjectByType<PenaltyController>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = Mathf.Clamp01(AppSettings.I.bellVolume);

        if (bellClip == null)
            Debug.LogWarning($"[Bell] bellClip が未設定です（{name}）。ベルは無音になります");
    }

    public void RingBell()
    {
        // 違反ポップアップ表示中も入力は届くため、押すたびに罰金が加算されないよう無視する
        if (penaltyController != null && penaltyController.IsViolationPopupVisible) return;

        Debug.Log("チリンチリン！ (ベルが鳴りました)");

        if (bellClip != null) audioSource.PlayOneShot(bellClip, audioSource.volume);

        // 警音器は危険を防止するためやむを得ない場合以外は鳴らせない
        if (!IsHazardNearby())
        {
            TrafficViolationDetector.Instance?.ReportViolationById("bell_misuse");
        }
    }

    bool IsHazardNearby()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, hazardCheckRadius);
        foreach (Collider hit in hits)
        {
            if (hit.GetComponentInParent<NPCWalker>() != null) return true;
            if (hit.GetComponentInParent<CarController>() != null) return true;
        }
        return false;
    }
}
