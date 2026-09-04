using UnityEngine;

/// <summary>
/// 現在の走行が違反判定なしのデバッグモードかを共有する。
/// </summary>
public static class GameDebugMode
{
    public static bool IsEnabled { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetState()
    {
        IsEnabled = false;
    }

    public static void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        Debug.Log(enabled
            ? "[GameDebugMode] デバッグモードを開始しました。違反判定と罰金加算は無効です。"
            : "[GameDebugMode] 通常モードです。違反判定は有効です。");
    }
}
