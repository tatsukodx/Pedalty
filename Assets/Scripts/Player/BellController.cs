using UnityEngine;

public class BellController : MonoBehaviour
{
    [Header("ベルの音")]
    [Tooltip("鳴らしたい音声ファイル（.wav / .mp3 / .ogg）。未設定だとベルは無音になります")]
    public AudioClip bellClip;

    private AudioSource audioSource;

    void Awake()
    {
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
        Debug.Log("チリンチリン！ (ベルが鳴りました)");

        if (bellClip != null) audioSource.PlayOneShot(bellClip, audioSource.volume);
    }
}
