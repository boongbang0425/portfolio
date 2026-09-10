using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;

/// <summary>
/// Firebase Realtime Databaseì™€ ì—°ë™í•˜ëŠ” ë§¤ë‹ˆì €
/// ìƒíƒœ ì €ìž¥ ë° ì›ê²© ì œì–´ ì§€ì›
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    [Header("Firebase Settings")]
    [SerializeField] private string databaseUrl = "https://emolamp-default-rtdb.firebaseio.com";
    [SerializeField] private float syncInterval = 5f;
    [SerializeField] private bool autoSync = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // ì´ë²¤íŠ¸
    public event Action<LampState> OnRemoteStateChanged;
    public event Action<string> OnSyncError;
    public event Action<bool> OnConnectionChanged;
    private bool _isConnected = false;
    public bool IsConnected 
    { 
        get => _isConnected;
        private set
        {
            if (_isConnected != value)
            {
                _isConnected = value;
                OnConnectionChanged?.Invoke(value);
            }
        }
    }
    public LampState CurrentState { get; private set; }

    private LampState _lastSyncedState;
    private Coroutine _listenerCoroutine;

    #region Unity Lifecycle

    private void Start()
    {
        CurrentState = new LampState();

        if (autoSync)
        {
            StartListening();
        }
    }

    private void OnDestroy()
    {
        StopListening();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Database URL ì„¤ì •
    /// </summary>
    public void SetDatabaseUrl(string url)
    {
        databaseUrl = url.TrimEnd('/');
    }

    /// <summary>
    /// í˜„ìž¬ ìƒíƒœë¥¼ Firebaseì— ì €ìž¥
    /// </summary>
    public void SaveState(LampState state)
    {
        CurrentState = state;
        StartCoroutine(SaveStateCoroutine(state));
    }

    /// <summary>
    /// ë¶€ë¶„ ì—…ë°ì´íŠ¸ (íŠ¹ì • í•„ë“œë§Œ)
    /// </summary>
    public void UpdateField(string field, object value)
    {
        StartCoroutine(UpdateFieldCoroutine(field, value));
    }

    /// <summary>
    /// Firebaseì—ì„œ í˜„ìž¬ ìƒíƒœ ì½ê¸°
    /// </summary>
    public void LoadState(Action<LampState> callback)
    {
        StartCoroutine(LoadStateCoroutine(callback));
    }

    /// <summary>
    /// ì‹¤ì‹œê°„ ë¦¬ìŠ¤ë‹ ì‹œìž‘
    /// </summary>
    public void StartListening()
    {
        if (_listenerCoroutine != null)
        {
            StopCoroutine(_listenerCoroutine);
        }
        
        // ì¦‰ì‹œ ì—°ê²° í™•ì¸
        StartCoroutine(CheckConnectionCoroutine());
        
        _listenerCoroutine = StartCoroutine(ListenForChangesCoroutine());
        Log("Start Listening");
    }

    /// <summary>
    /// ì´ˆê¸° ì—°ê²° ìƒíƒœ í™•ì¸
    /// </summary>
    private IEnumerator CheckConnectionCoroutine()
    {
        string url = $"{databaseUrl}/emolamp.json?shallow=true";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                IsConnected = true;
                Log("Firebase ì—°ê²° ì„±ê³µ");
            }
            else
            {
                IsConnected = false;
                LogError($"Firebase ì—°ê²° ì‹¤íŒ¨: {request.error}");
            }
        }
    }

    /// <summary>
    /// ì‹¤ì‹œê°„ ë¦¬ìŠ¤ë‹ ì¤‘ì§€
    /// </summary>
    public void StopListening()
    {
        if (_listenerCoroutine != null)
        {
            StopCoroutine(_listenerCoroutine);
            _listenerCoroutine = null;
        }
        Log("Stop Listening");
    }

    #endregion

    #region Firebase API Calls

    private IEnumerator SaveStateCoroutine(LampState state)
    {
        string url = $"{databaseUrl}/emolamp/state.json";
        string json = StateToJson(state);

        Log($"Request Save: {json}");

        using (UnityWebRequest request = new UnityWebRequest(url, "PUT"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Log("Finish Save");
                _lastSyncedState = state.Clone();
                IsConnected = true;
            }
            else
            {
                LogError($"Failed Save: {request.error}");
                OnSyncError?.Invoke(request.error);
                IsConnected = false;
            }
        }
    }

    private IEnumerator UpdateFieldCoroutine(string field, object value)
    {
        string url = $"{databaseUrl}/emolamp/state/{field}.json";
        string json = value is string ? $"\"{value}\"" : value.ToString().ToLower();

        using (UnityWebRequest request = new UnityWebRequest(url, "PUT"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Log($"Field Update Success: {field} = {value}");
            }
            else
            {
                LogError($"Field Update Failed: {request.error}");
            }
        }
    }

    private IEnumerator LoadStateCoroutine(Action<LampState> callback)
    {
        string url = $"{databaseUrl}/emolamp/state.json";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                Log($"Load Response: {json}");

                if (json != "null" && !string.IsNullOrEmpty(json))
                {
                    LampState state = JsonToState(json);
                    CurrentState = state;
                    callback?.Invoke(state);
                    IsConnected = true;
                }
                else
                {
                    Log("No Saved State Found");
                    callback?.Invoke(new LampState());
                }
            }
            else
            {
                LogError($"Failed Load: {request.error}");
                OnSyncError?.Invoke(request.error);
                callback?.Invoke(null);
                IsConnected = false;
            }
        }
    }

    private IEnumerator ListenForChangesCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(syncInterval);

            string url = $"{databaseUrl}/emolamp/state.json";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    
                    if (json != "null" && !string.IsNullOrEmpty(json))
                    {
                        LampState remoteState = JsonToState(json);

                        // ì›ê²©ì—ì„œ ë³€ê²½ëœ ê²½ìš°ì—ë§Œ ì´ë²¤íŠ¸ ë°œìƒ
                        if (HasRemoteChanges(remoteState))
                        {
                            Log("Remote State Changed!");
                            CurrentState = remoteState;
                            _lastSyncedState = remoteState.Clone();
                            OnRemoteStateChanged?.Invoke(remoteState);
                        }
                    }

                    IsConnected = true;
                }
                else
                {
                    IsConnected = false;
                }
            }
        }
    }

    private bool HasRemoteChanges(LampState remote)
    {
        if (_lastSyncedState == null) return true;

        // mode ë˜ëŠ” manualColorê°€ ë³€ê²½ë˜ì—ˆëŠ”ì§€ í™•ì¸
        return remote.mode != _lastSyncedState.mode ||
               remote.manualColorHex != _lastSyncedState.manualColorHex;
    }

    #endregion

    #region JSON Serialization

    private string StateToJson(LampState state)
    {
        return $@"{{
  ""mode"": ""{state.mode}"",
  ""emotion"": ""{state.emotion}"",
  ""hue"": {state.hue},
  ""saturation"": {state.saturation},
  ""brightness"": {state.brightness},
  ""colorHex"": ""{state.colorHex}"",
  ""manualColorHex"": ""{state.manualColorHex}"",
  ""summary"": ""{EscapeJson(state.summary)}"",
  ""weather"": ""{EscapeJson(state.weather)}"",
  ""timestamp"": {DateTimeOffset.Now.ToUnixTimeSeconds()}
}}";
    }

    private LampState JsonToState(string json)
    {
        LampState state = new LampState();

        try
        {
            // mode
            state.mode = ExtractStringValue(json, "mode") ?? "AUTO";

            // emotion
            state.emotion = ExtractStringValue(json, "emotion") ?? "calm";

            // hue
            string hueStr = ExtractNumberValue(json, "hue");
            if (int.TryParse(hueStr, out int hue)) state.hue = hue;

            // saturation
            string satStr = ExtractNumberValue(json, "saturation");
            if (int.TryParse(satStr, out int sat)) state.saturation = sat;

            // brightness
            string brightStr = ExtractNumberValue(json, "brightness");
            if (int.TryParse(brightStr, out int bright)) state.brightness = bright;

            // colorHex
            state.colorHex = ExtractStringValue(json, "colorHex") ?? "#50C878";

            // manualColorHex
            state.manualColorHex = ExtractStringValue(json, "manualColorHex") ?? "#FFFFFF";

            // summary
            state.summary = ExtractStringValue(json, "summary") ?? "";

            // weather
            state.weather = ExtractStringValue(json, "weather") ?? "";
        }
        catch (Exception e)
        {
            LogError($"JSON Parsing Error: {e.Message}");
        }

        return state;
    }

    private string ExtractStringValue(string json, string key)
    {
        string pattern = $"\"{key}\":\"";
        int index = json.IndexOf(pattern);
        if (index < 0) return null;

        int startIndex = index + pattern.Length;
        int endIndex = json.IndexOf('"', startIndex);
        if (endIndex < 0) return null;

        return json.Substring(startIndex, endIndex - startIndex);
    }

    private string ExtractNumberValue(string json, string key)
    {
        string pattern = $"\"{key}\":";
        int index = json.IndexOf(pattern);
        if (index < 0) return null;

        int startIndex = index + pattern.Length;
        string result = "";

        for (int i = startIndex; i < json.Length; i++)
        {
            char c = json[i];
            if (char.IsDigit(c) || c == '-' || c == '.')
            {
                result += c;
            }
            else if (result.Length > 0)
            {
                break;
            }
        }

        return result;
    }

    private string EscapeJson(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    #endregion

    #region Debug

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[FirebaseManager] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[FirebaseManager] {message}");
    }

    #endregion
}

/// <summary>
/// ëž¨í”„ ìƒíƒœ ë°ì´í„° í´ëž˜ìŠ¤
/// </summary>
[Serializable]
public class LampState
{
    public string mode = "AUTO";           // AUTO, MANUAL
    public string emotion = "calm";
    public int hue = 120;                  // 0-360
    public int saturation = 70;            // 0-100
    public int brightness = 70;            // 0-100
    public string colorHex = "#50C878";
    public string manualColorHex = "#FFFFFF";
    public string summary = "";
    public string weather = "";

    public LampState Clone()
    {
        return new LampState
        {
            mode = this.mode,
            emotion = this.emotion,
            hue = this.hue,
            saturation = this.saturation,
            brightness = this.brightness,
            colorHex = this.colorHex,
            manualColorHex = this.manualColorHex,
            summary = this.summary,
            weather = this.weather
        };
    }

    public Color GetColor()
    {
        return Color.HSVToRGB(hue / 360f, saturation / 100f, brightness / 100f);
    }

    public Color GetManualColor()
    {
        if (ColorUtility.TryParseHtmlString(manualColorHex, out Color color))
        {
            return color;
        }
        return Color.white;
    }
}