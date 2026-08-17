using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class ArduinoConnection : MonoBehaviour
{
  // シリアル設定は settings.json から読む
  string portName = "AUTO";
  int baudRate = 115200;

  [Header("モード設定")]
  [SerializeField] public bool isArduinoMode = true;
  // ポートが開けなければ自動的にキーボードモードに切り替わる

  [Header("デバッグ")]
  [Tooltip("ボタンの状態が変わるたびにConsoleへ出力する")]
  public bool logButtonChanges = true;

  bool logSerialLines = false;

  SerialPort serialPort;
  Thread readThread;
  volatile bool isRunning = false;
  string readBuffer = "";

  // 最新のボタン状態を保持（InputManagerから参照される）
  public volatile bool RightPressed = false;
  public volatile bool LeftPressed = false;
  public volatile int MagnetInterval = 0; // 0=停止、それ以外=ms間隔
  [NonSerialized] public volatile int MagnetPulseCount = 0;

  public string ConnectedPortName { get; private set; } = "";

  int readErrorCount = 0;
  string lastReadErrorMessage = "";
  float nextErrorLogTime = 0f;

  // 読み取りスレッドから書き込み、メインスレッドで出力する
  readonly object logLock = new object();
  readonly List<string> pendingLogs = new List<string>();

  void Start()
  {
    var settings = AppSettings.I;
    portName = string.IsNullOrWhiteSpace(settings.portName) ? "AUTO" : settings.portName.Trim();
    baudRate = settings.baudRate;
    logSerialLines = settings.logSerialLines;

    if (settings.forceKeyboardMode)
    {
      isArduinoMode = false;
      Debug.Log("settings.json の forceKeyboardMode が true のためキーボードモードで起動します");
      return;
    }

    if (isArduinoMode)
    {
      ConnectArduino();
    }
    else
    {
      Debug.Log("キーボードモードで起動しました（Arduino未使用）");
    }
  }

  void ConnectArduino()
  {
    string[] candidates;
    if (portName.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
    {
      candidates = SerialPort.GetPortNames();
      Array.Sort(candidates);
      Debug.Log(candidates.Length == 0
        ? "シリアルポートが1つも見つかりません"
        : $"ポート自動検出: {string.Join(", ", candidates)}");
    }
    else
    {
      candidates = new[] { portName };
    }

    foreach (string candidate in candidates)
    {
      if (TryOpen(candidate)) return;
    }

    Debug.LogWarning("Arduinoに接続できませんでした → キーボードモードに切り替えます");
    isArduinoMode = false;
  }

  bool TryOpen(string candidate)
  {
    SerialPort port = null;
    try
    {
      port = new SerialPort(candidate, baudRate, Parity.None, 8, StopBits.One);
      port.ReadTimeout = 100;
      port.NewLine = "\r\n";
      port.DtrEnable = true;
      port.RtsEnable = true;
      port.Open();

      // Arduinoはポートを開くとリセットされるので、起動を待ってから確認する
      Thread.Sleep(2000);
      if (!HasValidData(port))
      {
        Debug.LogWarning($"{candidate} を開けましたが、想定した形式のデータが来ませんでした");
        port.Close();
        return false;
      }

      serialPort = port;
      ConnectedPortName = candidate;
      isRunning = true;
      readThread = new Thread(ReadSerialLoop) { IsBackground = true };
      readThread.Start();
      Debug.Log($"Arduinoと接続しました ({candidate} : {baudRate}bps)");
      return true;
    }
    catch (Exception e)
    {
      Debug.LogWarning($"{candidate} への接続失敗: {e.Message}");
      try { port?.Close(); } catch { }
      return false;
    }
  }

  // こちらのプロトコルらしい行が来るか短時間だけ確認する
  bool HasValidData(SerialPort port)
  {
    string buffer = "";
    DateTime deadline = DateTime.Now.AddSeconds(1.5);

    while (DateTime.Now < deadline)
    {
      try
      {
        if (port.BytesToRead > 0) buffer += port.ReadExisting();
      }
      catch (TimeoutException) { }
      catch (Exception) { }

      foreach (string raw in buffer.Split('\n'))
      {
        string line = raw.Trim();
        if (line.StartsWith("MAGNET,")) return true;

        string[] parts = line.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _)) return true;
      }

      Thread.Sleep(20);
    }
    return false;
  }

  void Update()
  {
    // Arduinoモードではスレッド側が更新するのでここでは何もしない
    if (!isArduinoMode)
    {
      RightPressed = Input.GetKey(KeyCode.K);
      LeftPressed = Input.GetKey(KeyCode.J);
    }

    FlushLogs();
  }

  void FlushLogs()
  {
    lock (logLock)
    {
      foreach (string message in pendingLogs) Debug.Log(message);
      pendingLogs.Clear();
    }

    // 読み取りエラーは1秒に1回だけ、件数付きでまとめて出す
    if (readErrorCount > 0 && Time.unscaledTime >= nextErrorLogTime)
    {
      Debug.LogWarning($"読み取りエラー（直近1秒で{readErrorCount}件）: {lastReadErrorMessage}");
      readErrorCount = 0;
      nextErrorLogTime = Time.unscaledTime + 1f;
    }
  }

  void QueueLog(string message)
  {
    lock (logLock)
    {
      if (pendingLogs.Count < 64) pendingLogs.Add(message);
    }
  }

  void ReadSerialLoop()
  {
    while (isRunning)
    {
      try
      {
        if (serialPort != null && serialPort.IsOpen)
        {
          if (serialPort.BytesToRead > 0)
          {
            string data = serialPort.ReadExisting();
            if (!string.IsNullOrEmpty(data))
            {
              readBuffer += data;
              ProcessBuffer();
            }
          }
          else
          {
            Thread.Sleep(10);
          }
        }
        else
        {
          Thread.Sleep(50);
        }
      }
      catch (TimeoutException) { }
      catch (Exception e)
      {
        if (isRunning)
        {
          lastReadErrorMessage = e.Message;
          readErrorCount++;
          Thread.Sleep(5);
        }
      }
    }
  }

  void ProcessBuffer()
  {
    int newLineIndex = readBuffer.IndexOf('\n');
    while (newLineIndex >= 0)
    {
      string line = readBuffer.Substring(0, newLineIndex).Trim();
      readBuffer = readBuffer.Substring(newLineIndex + 1);

      if (!string.IsNullOrEmpty(line))
      {
        ParseLine(line);
      }
      newLineIndex = readBuffer.IndexOf('\n');
    }

    // 化けたデータでバッファが際限なく膨らまないようにする
    if (readBuffer.Length > 512) readBuffer = "";
  }

  void ParseLine(string line)
  {
    line = line.Trim();
    if (logSerialLines) QueueLog($"[Serial] {line}");

    if (line.StartsWith("MAGNET,"))
    {
      string[] parts = line.Split(',');
      if (parts.Length >= 2 && int.TryParse(parts[1], out int ms))
      {
        MagnetInterval = ms;
        if (ms > 0) MagnetPulseCount++;
      }
      return;
    }

    string[] btnParts = line.Split(',');
    if (btnParts.Length == 2
        && int.TryParse(btnParts[0], out int r)
        && int.TryParse(btnParts[1], out int l))
    {
      bool right = (r == 1);
      bool left = (l == 1);

      if (logButtonChanges && (right != RightPressed || left != LeftPressed))
      {
        QueueLog($"ボタン状態: 右(D3)={(right ? "押" : "離")} 左(D4)={(left ? "押" : "離")}");
      }

      RightPressed = right;
      LeftPressed = left;
    }
  }

  void OnDisable() { Shutdown(); }
  void OnApplicationQuit() { Shutdown(); }

  void Shutdown()
  {
    if (!isRunning && serialPort == null) return;

    isRunning = false;
    if (readThread != null && readThread.IsAlive) readThread.Join(300);
    readThread = null;

    try { if (serialPort != null && serialPort.IsOpen) serialPort.Close(); } catch { }
    serialPort = null;
  }
}
