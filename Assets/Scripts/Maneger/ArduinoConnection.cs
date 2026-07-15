using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class ArduinoConnection : MonoBehaviour
{
    [Header("シリアル通信設定")]
    const string portName = "COM4";
    [SerializeField] int baudRate = 115200;

    [Header("モード設定")]
    [SerializeField] public bool isArduinoMode = true;
    // true  → Arduinoから受信（実機使用時）
    // false → キーボード入力で代替（Arduino未接続時）
    // ※Arduinoモードで起動してもポートが開けなければ自動的にキーボードモードに切り替わる

    SerialPort serialPort;
    Thread readThread;
    volatile bool isRunning = false;
    string readBuffer = "";

    // 最新のボタン状態を保持（InputManagerから参照される）
    public volatile bool RightPressed = false;
    public volatile bool LeftPressed = false;
    public volatile int MagnetInterval = 0; // 0=停止、それ以外=ms間隔
    [NonSerialized] public volatile int MagnetPulseCount = 0;

    void Start()
    {
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
        try
        {
            serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            serialPort.ReadTimeout = 100;
            serialPort.NewLine = "\r\n";
            serialPort.DtrEnable = true;
            serialPort.RtsEnable = true;
            serialPort.Open();

            Debug.Log("ポートを開きました。Arduinoの起動を待ちます…");
            Thread.Sleep(2000);

            isRunning = true;
            readThread = new Thread(ReadSerialLoop);
            readThread.IsBackground = true;
            readThread.Start();
            Debug.Log($"Arduinoと接続しました ({portName} : {baudRate}bps)");
            Debug.Log("読み取りスレッドを開始しました。IsAlive=" + readThread.IsAlive);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Arduino接続失敗 → キーボードモードに切り替えます: " + e.Message);
            isArduinoMode = false; // 接続失敗時は自動的にキーボードモードへ
        }
    }

    void Update()
    {
        // キーボードモードのときだけキー入力でRightPressed/LeftPressedを更新する
        // Arduinoモードのときはスレッド側で更新するのでここでは何もしない
        if (!isArduinoMode)
        {
            RightPressed = Input.GetKey(KeyCode.K); // 右ボタン代替（ベル/次へ）
            LeftPressed  = Input.GetKey(KeyCode.J); // 左ボタン代替（ブレーキ/戻る）
        }
    }

    void ReadSerialLoop()
    {
        Debug.Log("ReadSerialLoopが開始されました");
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
            }
            catch (TimeoutException) { }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogWarning("読み取りエラー: " + e.Message);
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
    }

    void ParseLine(string line)
    {
        line = line.Trim();
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
        if (btnParts.Length >= 2)
        {
            if (int.TryParse(btnParts[0], out int r)) RightPressed = (r == 1);
            if (int.TryParse(btnParts[1], out int l)) LeftPressed  = (l == 1);
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (readThread != null && readThread.IsAlive) readThread.Join(200);
        if (serialPort != null && serialPort.IsOpen)  serialPort.Close();
    }
}
