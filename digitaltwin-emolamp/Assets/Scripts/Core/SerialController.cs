using UnityEngine;
using System;
using System.IO.Ports;
using System.Threading;

/// <summary>
/// Arduino와 Serial 통신을 담당하는 컨트롤러
/// 조도 센서값 수신 및 LED 색상 명령 전송
/// </summary>
public class SerialController : MonoBehaviour
{
    [Header("Serial Settings")]
    [SerializeField] private string portName = "COM3";
    [SerializeField] private int baudRate = 9600;
    [SerializeField] private bool autoConnect = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // 이벤트
    public event Action OnConnected;
    public event Action OnDisconnected;
    
    // 상태
    public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

    private SerialPort _serialPort;
    private Thread _readThread;
    private bool _isRunning = false;
    private readonly object _lock = new object();

    // 수신 버퍼
    private string _receivedData = "";
    private bool _hasNewData = false;

    #region Unity Lifecycle

    private void Start()
    {
        if (autoConnect)
        {
            Connect();
        }
    }

    private void Update()
    {
        // 메인 스레드에서 수신 데이터 처리
        ProcessReceivedData();
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    #endregion

    #region Connection

    /// <summary>
    /// Serial 포트 연결
    /// </summary>
    public bool Connect()
    {
        if (IsConnected)
        {
            Log("Already connected");
            return true;
        }

        try
        {
            _serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 100,
                WriteTimeout = 100,
                DtrEnable = true,
                RtsEnable = true
            };

            _serialPort.Open();
            _isRunning = true;

            // 읽기 스레드 시작
            _readThread = new Thread(ReadThread);
            _readThread.Start();

            Log($"Success Connection: {portName}");
            OnConnected?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            LogError($"Failed Connection: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Serial 포트 연결 해제
    /// </summary>
    public void Disconnect()
    {
        _isRunning = false;

        if (_readThread != null && _readThread.IsAlive)
        {
            _readThread.Join(500);
        }

        if (_serialPort != null && _serialPort.IsOpen)
        {
            try
            {
                _serialPort.Close();
                Log("Disconnected");
            }
            catch (Exception e)
            {
                LogError($"Disconnected Error: {e.Message}");
            }
        }

        OnDisconnected?.Invoke();
    }

    /// <summary>
    /// 사용 가능한 포트 목록 반환
    /// </summary>
    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    /// <summary>
    /// 포트 변경
    /// </summary>
    public void SetPort(string newPort)
    {
        bool wasConnected = IsConnected;
        
        if (wasConnected)
        {
            Disconnect();
        }

        portName = newPort;

        if (wasConnected)
        {
            Connect();
        }
    }

    #endregion

    #region Send Commands

    /// <summary>
    /// HSV 값으로 LED 제어 명령 전송
    /// </summary>
    public void SendHSV(float h, float s, float v)
    {
        // HSV → RGB 변환
        Color color = Color.HSVToRGB(h / 360f, s / 100f, v / 100f);
        SendRGB(
            Mathf.RoundToInt(color.r * 255),
            Mathf.RoundToInt(color.g * 255),
            Mathf.RoundToInt(color.b * 255)
        );
    }

    /// <summary>
    /// RGB 값으로 LED 제어 명령 전송
    /// </summary>
    public void SendRGB(int r, int g, int b)
    {
        string command = $"RGB:{r},{g},{b}\n";
        SendCommand(command);
    }

    /// <summary>
    /// 원시 명령 전송
    /// </summary>
    public void SendCommand(string command)
    {
        if (!IsConnected)
        {
            LogError("Cannot send command, not connected");
            return;
        }

        try
        {
            lock (_lock)
            {
                _serialPort.Write(command);
            }
            Log($"Send: {command.Trim()}");
        }
        catch (Exception e)
        {
            LogError($"Sending Error: {e.Message}");
        }
    }

    #endregion

    #region Read Thread

    private void ReadThread()
    {
        while (_isRunning)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen && _serialPort.BytesToRead > 0)
                {
                    string line = _serialPort.ReadLine().Trim();
                    
                    lock (_lock)
                    {
                        _receivedData = line;
                        _hasNewData = true;
                    }
                }
            }
            catch (TimeoutException)
            {
                // 타임아웃은 정상
            }
            catch (Exception e)
            {
                if (_isRunning)
                {
                    Debug.LogWarning($"[SerialController] Reading Error: {e.Message}");
                }
            }

            Thread.Sleep(10);
        }
    }

    private void ProcessReceivedData()
    {
        string data = null;

        lock (_lock)
        {
            if (_hasNewData)
            {
                data = _receivedData;
                _hasNewData = false;
            }
        }

        if (data != null)
        {
            ParseData(data);
        }
    }

    private void ParseData(string data)
    {
        Log($"Recieve: {data}");

        // 형식: STATUS:OK
        if (data.StartsWith("STATUS:"))
        {
            Log($"Arduino Status: {data.Substring(7)}");
        }
        // 형식: DEBUG:...
        else if (data.StartsWith("DEBUG:"))
        {
            Log($"Arduino Debug: {data.Substring(6)}");
        }
    }

    #endregion

    #region Debug

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SerialController] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[SerialController] {message}");
    }

    #endregion
}