using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 전체 UI 및 시스템 통합 관리 매니저
/// 모든 컴포넌트를 연결하고 데이터 흐름 제어
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("=== Managers ===")]
    [SerializeField] private WeatherManager weatherManager;
    [SerializeField] private ClaudeManager claudeManager;
    [SerializeField] private FirebaseManager firebaseManager;
    [SerializeField] private MQTTManager mqttManager;
    [SerializeField] private HSVController hsvController;
    [SerializeField] private LampController lampController;
    [SerializeField] private BackgroundController backgroundController;
    [SerializeField] private SerialController serialController;

    [Header("=== Status Bar UI ===")]
    [SerializeField] private TMP_Text weatherText;       // 🌤️ 맑음 8C
    [SerializeField] private TMP_Text emotionText;       // 😊 기쁨
    [SerializeField] private TMP_Text summaryText;       // AI 요약 메시지

    [Header("=== Input UI ===")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button analyzeButton;
    [SerializeField] private TMP_Text analyzeButtonText;

    [Header("=== Mode UI ===")]
    [SerializeField] private Toggle autoModeToggle;
    [SerializeField] private Toggle manualModeToggle;
    [SerializeField] private GameObject colorPickerPanel;
    [SerializeField] private Image colorPreview;
    [SerializeField] private Slider hueSlider;
    [SerializeField] private Slider saturationSlider;
    [SerializeField] private Slider brightnessSlider;

    [Header("=== Connection Status ===")]
    [SerializeField] private Image serialStatusIcon;
    [SerializeField] private Image firebaseStatusIcon;
    [SerializeField] private TMP_Text connectionText;

    [Header("=== Colors ===")]
    [SerializeField] private Color connectedColor = Color.green;
    [SerializeField] private Color disconnectedColor = Color.red;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // 상태
    private bool _isAnalyzing = false;
    private bool _isManualMode = false;

    #region Unity Lifecycle

    private void Start()
    {
        InitializeComponents();
        SetupEventListeners();
        SetupUIListeners();

        // 초기 상태
        SetAutoMode();
        UpdateConnectionStatus();

        // 초기 날씨 로드 후 분석
        StartCoroutine(InitialLoadCoroutine());
    }

    private void OnDestroy()
    {
        RemoveEventListeners();
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        // 컴포넌트 자동 찾기
        if (weatherManager == null) weatherManager = FindObjectOfType<WeatherManager>();
        if (claudeManager == null) claudeManager = FindObjectOfType<ClaudeManager>();
        if (firebaseManager == null) firebaseManager = FindObjectOfType<FirebaseManager>();
        if (mqttManager == null) mqttManager = FindObjectOfType<MQTTManager>();
        if (hsvController == null) hsvController = FindObjectOfType<HSVController>();
        if (lampController == null) lampController = FindObjectOfType<LampController>();
        if (backgroundController == null) backgroundController = FindObjectOfType<BackgroundController>();
        if (serialController == null) serialController = FindObjectOfType<SerialController>();

        Log("컴포넌트 초기화 완료");
    }

    private void SetupEventListeners()
    {
        // Weather
        if (weatherManager != null)
        {
            weatherManager.OnWeatherUpdated += OnWeatherUpdated;
        }

        // Claude
        if (claudeManager != null)
        {
            claudeManager.OnEmotionAnalyzed += OnEmotionAnalyzed;
            claudeManager.OnAnalysisError += OnAnalysisError;
        }

        // Firebase
        if (firebaseManager != null)
        {
            firebaseManager.OnRemoteStateChanged += OnRemoteStateChanged;
            firebaseManager.OnConnectionChanged += OnFirebaseConnectionChanged;
        }

        // MQTT - Cloudtype 브라이트니스 수신
        if (mqttManager != null)
        {
            mqttManager.OnConnected += OnMqttConnected;        // ← 추가
            mqttManager.OnDisconnected += OnMqttDisconnected;  // ← 추가
        }

        // Serial (조도 센서 제거됨 - 연결 상태만 확인)
        if (serialController != null)
        {
            serialController.OnConnected += OnSerialConnected;
            serialController.OnDisconnected += OnSerialDisconnected;
        }

        // HSV
        if (hsvController != null)
        {
            hsvController.OnColorChanged += OnHSVColorChanged;
        }
    }

    private void SetupUIListeners()
    {
        // 분석 버튼
        if (analyzeButton != null)
        {
            analyzeButton.onClick.AddListener(OnAnalyzeButtonClicked);
        }

        // 입력 필드 엔터키
        if (inputField != null)
        {
            inputField.onSubmit.AddListener((_) => OnAnalyzeButtonClicked());
        }

        // 모드 토글
        if (autoModeToggle != null)
        {
            autoModeToggle.onValueChanged.AddListener((isOn) => { if (isOn) SetAutoMode(); });
        }
        if (manualModeToggle != null)
        {
            manualModeToggle.onValueChanged.AddListener((isOn) => { if (isOn) SetManualMode(); });
        }

        // 컬러 슬라이더
        if (hueSlider != null)
        {
            hueSlider.onValueChanged.AddListener(OnManualColorChanged);
        }
        if (saturationSlider != null)
        {
            saturationSlider.onValueChanged.AddListener(OnManualColorChanged);
        }
        if (brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.AddListener(OnManualColorChanged);
        }
    }

    private void RemoveEventListeners()
    {
        if (weatherManager != null) weatherManager.OnWeatherUpdated -= OnWeatherUpdated;
        if (claudeManager != null)
        {
            claudeManager.OnEmotionAnalyzed -= OnEmotionAnalyzed;
            claudeManager.OnAnalysisError -= OnAnalysisError;
        }
        if (firebaseManager != null) 
        {
            firebaseManager.OnRemoteStateChanged -= OnRemoteStateChanged;
            firebaseManager.OnConnectionChanged -= OnFirebaseConnectionChanged;  // ← 추가
        }
        if (serialController != null)
        {
            serialController.OnConnected -= OnSerialConnected;
            serialController.OnDisconnected -= OnSerialDisconnected;
        }
        if (hsvController != null) hsvController.OnColorChanged -= OnHSVColorChanged;
    }

    private IEnumerator InitialLoadCoroutine()
    {
        // 날씨 로드 대기
        yield return new WaitForSeconds(2f);

        // 기본 메시지로 분석
        if (!_isAnalyzing && weatherManager != null && weatherManager.CurrentWeather != null)
        {
            AnalyzeWithText("Start today");
        }
    }

    #endregion

    #region Mode Management

    private void SetAutoMode()
    {
        _isManualMode = false;
        
        if (hsvController != null)
        {
            hsvController.SetManualMode(false);
        }

        if (colorPickerPanel != null)
        {
            colorPickerPanel.SetActive(false);
        }

        if (autoModeToggle != null) autoModeToggle.isOn = true;
        if (manualModeToggle != null) manualModeToggle.isOn = false;

        Log("Active AUTO Mode");
        SaveCurrentState();
    }

    private void SetManualMode()
    {
        _isManualMode = true;

        if (hsvController != null)
        {
            hsvController.SetManualMode(true);
        }

        if (colorPickerPanel != null)
        {
            colorPickerPanel.SetActive(true);
        }

        if (autoModeToggle != null) autoModeToggle.isOn = false;
        if (manualModeToggle != null) manualModeToggle.isOn = true;

        // 현재 색상으로 슬라이더 초기화
        if (hsvController != null)
        {
            if (hueSlider != null) hueSlider.value = hsvController.Hue;
            if (saturationSlider != null) saturationSlider.value = hsvController.Saturation;
            if (brightnessSlider != null) brightnessSlider.value = hsvController.Brightness;
        }

        Log("Active MANUAL Mode");
        SaveCurrentState();
    }
    
    private void OnManualColorChanged(float _)
    {
        if (!_isManualMode) return;

        float h = hueSlider != null ? hueSlider.value : 120;
        float s = saturationSlider != null ? saturationSlider.value : 70;
        float v = brightnessSlider != null ? brightnessSlider.value : 70;

        Color color = Color.HSVToRGB(h / 360f, s / 100f, v / 100f);
        string colorHex = ColorUtility.ToHtmlStringRGB(color); // 💡 Hex값 추출 추가

        if (hsvController != null)
        {
            hsvController.SetManualColor(color);
        }

        if (colorPreview != null)
        {
            colorPreview.color = color;
        }
        
        // ★★★ 여기에 요청하신 LogManualState 호출 로직을 추가합니다 ★★★
        if (mqttManager != null)
        {
            // ColorUtility.ToHtmlStringRGB를 통해 얻은 Hex 값은 #이 빠져있으므로, #을 붙여줍니다.
            mqttManager.LogManualState(
                Mathf.RoundToInt(h), 
                Mathf.RoundToInt(s), 
                Mathf.RoundToInt(v), 
                "#" + colorHex // Hex 값 전송
            );
        }

        SaveCurrentState();
    }

    #endregion

    #region Analysis

    private void OnAnalyzeButtonClicked()
    {
        if (_isAnalyzing) return;

        string text = inputField != null ? inputField.text.Trim() : "";
        
        if (string.IsNullOrEmpty(text))
        {
            text = "Present emotion";  // 기본값
        }

        AnalyzeWithText(text);
    }

    private void AnalyzeWithText(string text)
    {
        if (_isAnalyzing) return;

        _isAnalyzing = true;
        UpdateAnalyzeButton(true);

        Log($"Start Analysis: {text}");

        // Claude가 다국어를 직접 이해하므로 바로 분석
        PerformAnalysis(text);
    }

    private void PerformAnalysis(string text)
    {
        if (claudeManager == null)
        {
            LogError("There is no ClaudeManager.");
            _isAnalyzing = false;
            UpdateAnalyzeButton(false);
            return;
        }

        // 날씨 정보 포함
        string weatherInfo = "";
        if (weatherManager != null && weatherManager.CurrentWeather != null)
        {
            var w = weatherManager.CurrentWeather;
            weatherInfo = $"{w.description}, {w.temperature}C";
        }

        claudeManager.AnalyzeEmotion(text, weatherInfo);
    }

    #endregion

    #region Event Handlers

    private void OnWeatherUpdated(WeatherData data)
    {
        Log($"Weather Update: {data.GetIcon()} {data.description} {data.temperature}C");

        // UI 업데이트
        if (weatherText != null)
        {
            weatherText.text = $"{data.GetIcon()} {data.conditionText} {data.temperature}C";
        }

        // HSV 채도 업데이트 (AUTO 모드일 때만)
        if (!_isManualMode && hsvController != null)
        {
            hsvController.SetHueFromTemperature(data.temperature);      // ← 변경
            hsvController.SetBrightnessFromWeather(data.condition);     // ← 변경
            // 채도는 유지하거나 별도 설정
        }

        SaveCurrentState();
    }

    private void OnEmotionAnalyzed(EmotionResult result)
    {
        Log($"Finish Analysis Emotion: {result.GetEmoji()} {result.GetEmotionKorean()} (H:{result.hue})");

        _isAnalyzing = false;
        UpdateAnalyzeButton(false);

        // UI 업데이트
        if (emotionText != null)
        {
            emotionText.text = $"{result.GetEmoji()} {result.GetEmotionKorean()}";
        }

        if (summaryText != null)
        {
            summaryText.text = $"{result.summary}";
        }

        // HSV 색감 업데이트 (AUTO 모드일 때만)
        if (!_isManualMode && hsvController != null)
        {
            hsvController.SetHue(result.hue);
        }

        // 입력창 초기화
        if (inputField != null)
        {
            inputField.text = "";
        }

        SaveCurrentState();
    }

    private void OnAnalysisError(string error)
    {
        LogError($"Analysis Error: {error}");

        _isAnalyzing = false;
        UpdateAnalyzeButton(false);

        if (summaryText != null)
        {
            summaryText.text = "error occurred during analysis.";
        }
    }

    private void OnRemoteStateChanged(LampState state)
    {
        Log($"Change Remote Status: mode={state.mode}");

        // 모드 동기화
        if (state.mode == "MANUAL" && !_isManualMode)
        {
            SetManualMode();
            
            // 원격 색상 적용
            if (hsvController != null)
            {
                hsvController.SetManualColorHex(state.manualColorHex);
            }
        }
        else if (state.mode == "AUTO" && _isManualMode)
        {
            SetAutoMode();
        }
    }

    private void OnHSVColorChanged(Color color)
    {
        if (colorPreview != null)
        {
            colorPreview.color = color;
        }
    }

    private void OnSerialConnected()
    {
        Log("Serial Connected");
        UpdateConnectionStatus();
    }

    private void OnSerialDisconnected()
    {
        Log("Serial Disconnected");
        UpdateConnectionStatus();
    }

    private void OnFirebaseConnectionChanged(bool connected)
    {
        Log($"Firebase Connection Changed: {connected}");
        UpdateConnectionStatus();
    }

    private void OnMqttConnected()
    {
        Log("MQTT Connected");
        UpdateConnectionStatus();
    }

    private void OnMqttDisconnected()
    {
        Log("MQTT Disconnected");
        UpdateConnectionStatus();
    }

    #endregion

    #region UI Updates

    private void UpdateAnalyzeButton(bool analyzing)
    {
        if (analyzeButton != null)
        {
            analyzeButton.interactable = !analyzing;
        }

        if (analyzeButtonText != null)
        {
            analyzeButtonText.text = analyzing ? "Analysis..." : "Analysis";
        }
    }

    private void UpdateConnectionStatus()
    {
        // 1. 연결 상태 확인
        bool serialConnected = serialController != null && serialController.IsConnected;
        bool firebaseConnected = firebaseManager != null && firebaseManager.IsConnected;
        bool mqttConnected = mqttManager != null && mqttManager.IsConnected;  // ← MQTT 상태 추가

        // 2. 아이콘 색상 업데이트 (Serial과 Firebase만 있으므로, MQTT 아이콘은 생략)
        if (serialStatusIcon != null)
        {
            serialStatusIcon.color = serialConnected ? connectedColor : disconnectedColor;
        }

        if (firebaseStatusIcon != null)
        {
            firebaseStatusIcon.color = firebaseConnected ? connectedColor : disconnectedColor;
        }

        // 3. 텍스트 상태 업데이트
        if (connectionText != null)
        {
            string status = "";
            
            // Serial 상태
            status += serialConnected ? "● Serial   " : "○ Serial   ";
            
            // Firebase 상태
            status += firebaseConnected ? "● Firebase   " : "○ Firebase   ";
            
            // MQTT 상태 추가
            status += mqttConnected ? "● MQTT" : "○ MQTT";  // ← MQTT 상태 추가
            
            connectionText.text = status;
        }
    }

    #endregion

    #region State Management

    private void SaveCurrentState()
    {
        if (firebaseManager == null || hsvController == null) return;

        LampState state = hsvController.GetLampState();
        
        if (weatherManager != null && weatherManager.CurrentWeather != null)
        {
            state.weather = weatherManager.CurrentWeather.description;
        }

        if (claudeManager != null && claudeManager.LastResult != null)
        {
            state.emotion = claudeManager.LastResult.emotion.ToString().ToLower();
            state.summary = claudeManager.LastResult.summary;
        }

        firebaseManager.SaveState(state);
    }

    #endregion

    #region Debug

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[UIManager] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[UIManager] {message}");
    }

    #endregion
}