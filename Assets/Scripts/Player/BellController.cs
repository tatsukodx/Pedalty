using UnityEngine;

public class BellController : MonoBehaviour
{
    [Header("ベルの音")]
    [Tooltip("鳴らしたい音声ファイル。未設定なら合成音（チリンチリン）を自動生成します")]
    public AudioClip bellClip;

    [Tooltip("音声ファイルが無いときに合成音を鳴らす")]
    public bool useGeneratedSound = true;

    private AudioSource audioSource;
    private AudioClip generatedClip;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = Mathf.Clamp01(AppSettings.I.bellVolume);
    }

    public void RingBell()
    {
        Debug.Log("チリンチリン！ (ベルが鳴りました)");

        AudioClip clip = bellClip;
        if (clip == null && useGeneratedSound)
        {
            if (generatedClip == null) generatedClip = CreateBellClip();
            clip = generatedClip;
        }

        if (clip != null) audioSource.PlayOneShot(clip, audioSource.volume);
    }

    // 金属を2回叩いたような音を合成する
    AudioClip CreateBellClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.75f;
        const float strikeGap = 0.20f;  // 1打目と2打目の間隔(秒)
        const float decay = 11f;
        const float baseFreq = 2500f;

        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // 金属的な響きになるよう、整数倍でない倍音を重ねる
        float[] partials = { 1f, 2.76f, 5.40f };
        float[] gains = { 1f, 0.45f, 0.2f };

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float value = 0f;

            for (int strike = 0; strike < 2; strike++)
            {
                float st = t - strike * strikeGap;
                if (st < 0f) continue;

                float envelope = Mathf.Exp(-decay * st);
                for (int p = 0; p < partials.Length; p++)
                {
                    value += Mathf.Sin(2f * Mathf.PI * baseFreq * partials[p] * st) * gains[p] * envelope;
                }
            }

            samples[i] = Mathf.Clamp(value * 0.25f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("BellGenerated", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
