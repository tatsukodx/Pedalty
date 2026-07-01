using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class ArduinoConnection : MonoBehaviour
{
    [Header("シリアル通信設定")]
    [SerializeField] string portName = "COM3";
    [SerializeField] int baudRate = 115200;

    SerialPort serialPort;
    Thread readThread;
    volatile bool isRunning = false;
    string readBuffer = "";

    // 最新のボタン状態を保持
    public volatile bool RightPressed = false;
    public volatile bool LeftPressed = false;

    void Start()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);
            serialPort.ReadTimeout = 100;
            serialPort.NewLine = "\r\n";
            serialPort.DtrEnable = true; // 必須：Arduino UNO等のリセット・通信開始用
            serialPort.RtsEnable = true; // 念のため有効化
            serialPort.Open();

            Debug.Log("ポートを開きました。Arduinoの起動を待ちます…");
            System.Threading.Thread.Sleep(2000); // Arduinoの自動リセット待ち（2秒)

            isRunning = true;
            readThread = new Thread(ReadSerialLoop);
            readThread.IsBackground = true;
            readThread.Start();
            Debug.Log($"Arduinoと接続しました ({portName} : {baudRate}bps)");
            Debug.Log("読み取りスレッドを開始しました。IsAlive=" + readThread.IsAlive);
        }
        catch (Exception e)
        {
            Debug.LogError("シリアルポートを開けませんでした: " + e.Message);
        }
    }

    void ReadSerialLoop()
    {
        Debug.Log("ReadSerialLoopが開始されました");
        while (isRunning)
        {
            try
            {
                // BytesToReadが0より大きい場合のみ読み取り
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
                        // データがない場合はスレッドを少し待機（CPU負荷軽減）
                        Thread.Sleep(10);
                    }
                }
            }
            catch (TimeoutException)
            {
                // タイムアウトは想定内なので無視
            }
            catch (Exception e)
            {
                if (isRunning) // 終了時の意図的なエラーは無視
                {
                    Debug.LogWarning("読み取りエラー: " + e.Message);
                }
            }
        }
    }

    void ProcessBuffer()
    {
        // 改行コード '\n' を区切り文字としてバッファから1行ずつ取り出す
        int newLineIndex = readBuffer.IndexOf('\n');
        while (newLineIndex >= 0)
        {
            string line = readBuffer.Substring(0, newLineIndex).Trim();
            readBuffer = readBuffer.Substring(newLineIndex + 1);

            if (!string.IsNullOrEmpty(line))
            {
                Debug.Log("受信データ: [" + line + "]");
                ParseLine(line);
            }
            newLineIndex = readBuffer.IndexOf('\n');
        }
    }

    void ParseLine(string line)
    {
        // データのフォーマット: "右ボタン,左ボタン" (0 or 1)
        // 将来的にセンサ値が増えてもカンマ区切りで対応可能
        string[] parts = line.Trim().Split(',');
        if (parts.Length >= 2)
        {
            if (int.TryParse(parts[0], out int r)) RightPressed = (r == 1);
            if (int.TryParse(parts[1], out int l)) LeftPressed = (l == 1);
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;

        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(200); // 最大200ms待機
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }
    }
}