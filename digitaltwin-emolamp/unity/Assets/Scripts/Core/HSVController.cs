using UnityEngine;
using System;

/// <summary>
/// HSV 색상 모델을 관리하는 컨트롤러
/// H(색감) = 감정, S(채도) = 날씨, V(명도) = 조도
/// </summary>
public class HSVController : MonoBehaviour
{
    [Header("Current HSV Values")]
    [SerializeField, Range(0, 360)] private float hue = 120f;        // 색감 (감정)
    [SerializeField, Range(0, 100)] private float saturation = 70f;  // 채도 (날씨)
    [SerializeField, Range(0, 100)] private float brightness = 70f;  // 명도 (조도)

    [Header("Manual Override")]
    [SerializeField] private bool manualMode = false;
    [SerializeField] private Color manualColor = Color.white;

    [Header("Smoothing")]
    [SerializeField] private float smoothSpeed = 3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // 이벤트
    public event Action<Color> OnColorChanged;
    public event Action<float, float, float> OnHSVChanged;  // H, S, V

    // 프로퍼티
    public float Hue => hue;
    public float Saturation => saturation;
    public float Brightness => brightness;
    public bool IsManualMode => manualMode;
    public Color CurrentColor => manualMode ? manualColor : GetRGBColor();

    // 타겟 값 (스무딩용)
    private float _targetHue;
    private float _targetSaturation;
    private float _targetBrightness;

    #region Unity Lifecycle

    private void Start()
    {
        _targetHue = hue;
        _targetSaturation = saturation;
        _targetBrightness = brightness;
    }

    private void Update()
    {
        if (!manualMode)
        {
            // 부드러운 보간
            bool changed = false;

            if (Mathf.Abs(hue - _targetHue) > 0.5f)
            {
                // Hue는 원형이므로 최단 거리 계산
                float diff = Mathf.DeltaAngle(hue, _targetHue);
                hue = Mathf.MoveTowardsAngle(hue, _targetHue, smoothSpeed * 30f * Time.deltaTime);
                if (hue < 0) hue += 360;
                if (hue >= 360) hue -= 360;
                changed = true;
            }

            if (Mathf.Abs(saturation - _targetSaturation) > 0.5f)
            {
                saturation = Mathf.MoveTowards(saturation, _targetSaturation, smoothSpeed * 20f * Time.deltaTime);
                changed = true;
            }

            if (Mathf.Abs(brightness - _targetBrightness) > 0.5f)
            {
                brightness = Mathf.MoveTowards(brightness, _targetBrightness, smoothSpeed * 20f * Time.deltaTime);
                changed = true;
            }

            if (changed)
            {
                NotifyColorChanged();
            }
        }
    }

    #endregion

    #region Public Methods - HSV Setting

    /// <summary>
    /// H (색감) 설정 - 감정 기반
    /// </summary>
    public void SetHue(float h)
    {
        _targetHue = Mathf.Clamp(h, 0f, 360f);
        Log($"Hue setting: {_targetHue} (Emotion)");
    }

    /// <summary>
    /// S (채도) 설정 - 날씨 기반
    /// </summary>
    public void SetSaturation(float s)
    {
        _targetSaturation = Mathf.Clamp(s, 0f, 100f);
        Log($"Saturation setting: {_targetSaturation}% (Weather)");
    }

    /// <summary>
    /// V (명도) 설정 - 조도 기반
    /// </summary>
    public void SetBrightness(float v)
    {
        _targetBrightness = Mathf.Clamp(v, 0f, 100f);
        Log($"Brightness setting: {_targetBrightness}% (Light)");
    }

    /// <summary>
    /// HSV 한번에 설정
    /// </summary>
    public void SetHSV(float h, float s, float v)
    {
        SetHue(h);
        SetSaturation(s);
        SetBrightness(v);
    }

    /// <summary>
    /// 감정에서 Hue 설정
    /// </summary>
    public void SetHueFromEmotion(EmotionType emotion)
    {
        float h = emotion switch
        {
            EmotionType.Joy => 50f,       // 골드/노랑
            EmotionType.Sadness => 220f,  // 블루
            EmotionType.Anger => 0f,      // 레드
            EmotionType.Calm => 120f,     // 그린
            EmotionType.Excited => 30f,   // 오렌지
            EmotionType.Fear => 280f,     // 퍼플
            EmotionType.Surprise => 60f,  // 밝은 노랑
            _ => 120f
        };
        SetHue(h);
    }

    /// <summary>
    /// 날씨에서 Saturation 설정
    /// </summary>
    public void SetSaturationFromWeather(WeatherCondition weather)
    {
        float s = weather switch
        {
            WeatherCondition.Clear => 100f,
            WeatherCondition.Clouds => 70f,
            WeatherCondition.Overcast => 50f,
            WeatherCondition.Rain => 40f,
            WeatherCondition.Snow => 90f,
            WeatherCondition.Fog => 30f,
            WeatherCondition.Storm => 60f,
            _ => 70f
        };
        SetSaturation(s);
    }

    /// <summary>
    /// 조도에서 Brightness 설정 (0-100 입력)
    /// </summary>
    public void SetBrightnessFromLight(int lightPercent)
    {
        // 최소 30%, 최대 100%
        float v = Mathf.Lerp(30f, 100f, lightPercent / 100f);
        SetBrightness(v);
    }
    
    /// <summary>
    /// 온도 기반 Hue 설정
    /// </summary>
    public void SetHueFromTemperature(int temperature)
    {
        float h;
        
        if (temperature <= -10)
        {
            // 영하 10도 이하: 진한 파란색
            h = 240f;
        }
        else if (temperature <= 0)
        {
            // -10도 ~ 0도: 파란색 (220-240)
            float t = Mathf.InverseLerp(-10f, 0f, temperature);
            h = Mathf.Lerp(240f, 200f, t);
        }
        else if (temperature <= 10)
        {
            // 0도 ~ 10도: 시안 (200-160)
            float t = Mathf.InverseLerp(0f, 10f, temperature);
            h = Mathf.Lerp(200f, 160f, t);
        }
        else if (temperature <= 20)
        {
            // 10도 ~ 20도: 녹색 (160-120)
            float t = Mathf.InverseLerp(10f, 20f, temperature);
            h = Mathf.Lerp(160f, 120f, t);
        }
        else if (temperature <= 28)
        {
            // 20도 ~ 28도: 노란색 (120-50)
            float t = Mathf.InverseLerp(20f, 28f, temperature);
            h = Mathf.Lerp(120f, 50f, t);
        }
        else
        {
            // 28도 이상: 주황~빨강 (50-0)
            float t = Mathf.InverseLerp(28f, 35f, Mathf.Min(temperature, 35f));
            h = Mathf.Lerp(50f, 0f, t);
        }

        SetHue(h);
        Log($"Temperature {temperature}°C → Hue {h:F0}°");
    }

    /// <summary>
    /// 날씨 기반 Brightness 설정
    /// </summary>
    public void SetBrightnessFromWeather(WeatherCondition weather)
    {
        float v = weather switch
        {
            WeatherCondition.Clear => 100f,
            WeatherCondition.Clouds => 85f,
            WeatherCondition.Overcast => 65f,
            WeatherCondition.Rain => 50f,
            WeatherCondition.Snow => 80f,
            WeatherCondition.Fog => 45f,
            WeatherCondition.Storm => 40f,
            _ => 70f
        };
        
        SetBrightness(v);
        Log($"Weather {weather} → Brightness {v}%");
    }

    #endregion

    #region Public Methods - Manual Mode

    /// <summary>
    /// 수동 모드 설정
    /// </summary>
    public void SetManualMode(bool enabled)
    {
        manualMode = enabled;
        Log($"Customize Mode: {(enabled ? "ON" : "OFF")}");
        NotifyColorChanged();
    }

    /// <summary>
    /// 수동 색상 설정
    /// </summary>
    public void SetManualColor(Color color)
    {
        manualColor = color;
        if (manualMode)
        {
            NotifyColorChanged();
        }
        Log($"Costomize Color: {ColorUtility.ToHtmlStringRGB(color)}");
    }

    /// <summary>
    /// HEX 문자열로 수동 색상 설정
    /// </summary>
    public void SetManualColorHex(string hex)
    {
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (ColorUtility.TryParseHtmlString(hex, out Color color))
        {
            SetManualColor(color);
        }
    }

    #endregion

    #region Public Methods - Get Values

    /// <summary>
    /// 현재 RGB 색상 반환
    /// </summary>
    public Color GetRGBColor()
    {
        return Color.HSVToRGB(hue / 360f, saturation / 100f, brightness / 100f);
    }

    /// <summary>
    /// 현재 RGB 값 반환 (0-255)
    /// </summary>
    public (int r, int g, int b) GetRGB255()
    {
        Color c = CurrentColor;
        return (
            Mathf.RoundToInt(c.r * 255),
            Mathf.RoundToInt(c.g * 255),
            Mathf.RoundToInt(c.b * 255)
        );
    }

    /// <summary>
    /// 현재 HEX 색상 반환
    /// </summary>
    public string GetHexColor()
    {
        return "#" + ColorUtility.ToHtmlStringRGB(CurrentColor);
    }

    /// <summary>
    /// LampState 형태로 반환
    /// </summary>
    public LampState GetLampState()
    {
        return new LampState
        {
            mode = manualMode ? "MANUAL" : "AUTO",
            hue = Mathf.RoundToInt(hue),
            saturation = Mathf.RoundToInt(saturation),
            brightness = Mathf.RoundToInt(brightness),
            colorHex = GetHexColor(),
            manualColorHex = "#" + ColorUtility.ToHtmlStringRGB(manualColor)
        };
    }

    #endregion

    #region Private Methods

    private void NotifyColorChanged()
    {
        Color color = CurrentColor;
        OnColorChanged?.Invoke(color);
        OnHSVChanged?.Invoke(hue, saturation, brightness);
    }

    #endregion

    #region Debug

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[HSVController] {message}");
        }
    }

    #endregion
}