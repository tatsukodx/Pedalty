using System;
using System.IO;
using UnityEngine;

// 実行ファイル（エディタではプロジェクトフォルダ）の隣の settings.json から設定を読み書きする
[Serializable]
public class AppSettings
{
    [Header("シリアル通信")]
    public string portName = "AUTO";           // "AUTO" で自動検出。"COM7" 等で固定
    public int baudRate = 115200;
    public bool forceKeyboardMode = false;
    public bool logSerialLines = false;

    [Header("速度計算")]
    public float wheelCircumference = 2.096f;  // タイヤ周長(m)
    public int magnetsPerWheel = 1;
    public float speedMultiplier = 2.0f;       // 実速度 → ゲーム速度の倍率
    public float maxSpeedKmh = 40f;
    public float smoothFactor = 3f;
    public float pulseTimeoutSec = 2.5f;       // この秒数パルスが来なければ停止扱い

    [Header("ハンドル角（ポテンショメータ）")]
    public int potMin = 200;        // 切れ角一方の生値
    public int potCenter = 512;     // 直進時の生値
    public int potMax = 820;        // 切れ角他方の生値
    public bool potInvert = false;  // 左右反転（較正時に自動判定）

    [Header("ベル")]
    public float bellVolume = 1.0f;

    static AppSettings _i;
    public static AppSettings I => _i ??= Load();

    public static string FilePath =>
        Path.Combine(Directory.GetParent(Application.dataPath).FullName, "settings.json");

    static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonUtility.FromJson<AppSettings>(File.ReadAllText(FilePath));
                if (s != null)
                {
                    Debug.Log($"[Settings] 読込 {FilePath} / port={s.portName} 周長={s.wheelCircumference}m 倍率={s.speedMultiplier} pot={s.potMin}/{s.potCenter}/{s.potMax}");
                    return s;
                }
            }
        }
        catch (Exception e) { Debug.LogWarning($"[Settings] 読込失敗: {e.Message}"); }

        var def = new AppSettings();
        def.Save();
        return def;
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
            Debug.Log($"[Settings] 保存 {FilePath}");
        }
        catch (Exception e) { Debug.LogWarning($"[Settings] 保存失敗: {e.Message}"); }
    }

    public static void Reload() { _i = null; }

    // エディタでは静的フィールドがPlay終了後も残るので、Play開始のたびに読み直す
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() { _i = null; }
}
