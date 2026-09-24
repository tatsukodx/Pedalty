using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EditorScreenshot
{
  [MenuItem("Tools/Screenshot _p")]
  private static void TakeScreenshot()
  {
    var now = DateTime.Now;
    var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "context", "screenshots"));
    Directory.CreateDirectory(directory);
    var savePath = Path.Combine(directory, $"Screenshot{now.ToFileTime()}.png");
    ScreenCapture.CaptureScreenshot(savePath);
  }
}
