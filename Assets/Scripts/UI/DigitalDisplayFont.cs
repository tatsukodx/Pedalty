using TMPro;
using UnityEngine;

// SPEED / TIME の表示へレトロゲーム風フォントを適用する。
public class DigitalDisplayFont : MonoBehaviour
{
    [SerializeField] private Font sourceFont;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI timeText;

    private TMP_FontAsset runtimeFontAsset;

    private void Awake()
    {
        if (sourceFont == null || speedText == null || timeText == null)
        {
            Debug.LogError("[DigitalDisplayFont] フォントまたは表示テキストが設定されていません。");
            return;
        }

        runtimeFontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
        runtimeFontAsset.name = "DotGothic16 (Runtime)";

        speedText.font = runtimeFontAsset;
        timeText.font = runtimeFontAsset;
    }

    private void OnDestroy()
    {
        if (runtimeFontAsset != null)
            Destroy(runtimeFontAsset);
    }
}
