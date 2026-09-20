using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EditorScreenshot
{
  [MenuItem("Tools/Screenshot")]
  private static void TakeScreenshot()
  {
    var now = DateTime.Now;
    // マニュアル用の画像は、Assetsの外にまとめて保存する。
    var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "context", "screenshots"));
    Directory.CreateDirectory(directory);
    var savePath = Path.Combine(directory, $"Screenshot{now.ToFileTime()}.png");
    ScreenCapture.CaptureScreenshot(savePath);
  }
}
