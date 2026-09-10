using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

/// <summary>
/// OpenWeatherMap API를 통해 날씨 정보를 가져오는 매니저
/// 날씨 상태에 따라 채도(S) 값 결정
/// </summary>
public class WeatherManager : MonoBehaviour
{
    [Header("API Settings")]
    [SerializeField] private string apiKey = "YOUR_API_KEY";
    [SerializeField] private string cityName = "Seoul";
    [SerializeField] private float updateInterval = 600f; // 10분

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // 이벤트
    public event Action<WeatherData> OnWeatherUpdated;

    // 현재 날씨 데이터
    public WeatherData CurrentWeather { get; private set; }
    public bool IsLoading { get; private set; } = false;

    private const string API_URL = "https://api.openweathermap.org/data/2.5/weather";

    #region Unity Lifecycle

    private void Start()
    {
        // 시작 시 날씨 가져오기
        FetchWeather();

        // 주기적 업데이트
        if (updateInterval > 0)
        {
            InvokeRepeating(nameof(FetchWeather), updateInterval, updateInterval);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 날씨 정보 가져오기
    /// </summary>
    public void FetchWeather()
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_API_KEY")
        {
            LogError("Please set the API key.");
            // 테스트용 기본값 설정
            SetDefaultWeather();
            return;
        }

        StartCoroutine(FetchWeatherCoroutine());
    }

    /// <summary>
    /// API 키 설정
    /// </summary>
    public void SetApiKey(string key)
    {
        apiKey = key;
    }

    /// <summary>
    /// 도시 설정
    /// </summary>
    public void SetCity(string city)
    {
        cityName = city;
        FetchWeather();
    }

    /// <summary>
    /// 날씨 상태에 따른 채도(S) 반환 (0-100)
    /// </summary>
    public int GetSaturationFromWeather()
    {
        if (CurrentWeather == null) return 70;

        return CurrentWeather.condition switch
        {
            WeatherCondition.Clear => 100,
            WeatherCondition.Clouds => 70,
            WeatherCondition.Overcast => 50,
            WeatherCondition.Rain => 40,
            WeatherCondition.Snow => 90,
            WeatherCondition.Fog => 30,
            WeatherCondition.Storm => 60,
            _ => 70
        };
    }

    #endregion

    #region API Call

    private IEnumerator FetchWeatherCoroutine()
    {
        IsLoading = true;
        Log($"request weather INFO: {cityName}");

        string url = $"{API_URL}?q={cityName}&appid={apiKey}&units=metric&lang=kr";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ParseWeatherResponse(request.downloadHandler.text);
            }
            else
            {
                LogError($"Weather API error: {request.error}");
                SetDefaultWeather();
            }
        }

        IsLoading = false;
    }

    private void ParseWeatherResponse(string json)
    {
        try
        {
            Log($"Response: {json}");

            // 간단한 JSON 파싱 (Newtonsoft 없이)
            WeatherData data = new WeatherData();

            // 온도 추출
            int tempIndex = json.IndexOf("\"temp\":");
            if (tempIndex >= 0)
            {
                string tempStr = json.Substring(tempIndex + 7, 10);
                int commaIndex = tempStr.IndexOf(',');
                if (commaIndex > 0) tempStr = tempStr.Substring(0, commaIndex);
                if (float.TryParse(tempStr, out float temp))
                {
                    data.temperature = Mathf.RoundToInt(temp);
                }
            }

            // 습도 추출
            int humidIndex = json.IndexOf("\"humidity\":");
            if (humidIndex >= 0)
            {
                string humidStr = json.Substring(humidIndex + 11, 5);
                int commaIndex = humidStr.IndexOf(',');
                if (commaIndex > 0) humidStr = humidStr.Substring(0, commaIndex);
                if (int.TryParse(humidStr, out int humidity))
                {
                    data.humidity = humidity;
                }
            }

            // 날씨 상태 추출
            int mainIndex = json.IndexOf("\"main\":");
            if (mainIndex >= 0)
            {
                string mainStr = json.Substring(mainIndex + 8, 20);
                int quoteIndex = mainStr.IndexOf('"');
                if (quoteIndex > 0) mainStr = mainStr.Substring(0, quoteIndex);
                data.conditionText = mainStr;
                data.condition = ParseCondition(mainStr);
            }

            // 설명 추출
            int descIndex = json.IndexOf("\"description\":\"");
            if (descIndex >= 0)
            {
                string descStr = json.Substring(descIndex + 15, 50);
                int quoteIndex = descStr.IndexOf('"');
                if (quoteIndex > 0) descStr = descStr.Substring(0, quoteIndex);
                data.description = descStr;
            }

            // 도시 이름
            data.cityName = cityName;
            data.timestamp = DateTime.Now;

            CurrentWeather = data;
            Log($"Weather Parsing Error: {data.temperature}C, {data.conditionText}, {data.description}");

            OnWeatherUpdated?.Invoke(data);
        }
        catch (Exception e)
        {
            LogError($"Weather Parsing Error: {e.Message}");
            SetDefaultWeather();
        }
    }

    private WeatherCondition ParseCondition(string main)
    {
        return main.ToLower() switch
        {
            "clear" => WeatherCondition.Clear,
            "clouds" => WeatherCondition.Clouds,
            "overcast" => WeatherCondition.Overcast,
            "rain" => WeatherCondition.Rain,
            "drizzle" => WeatherCondition.Rain,
            "snow" => WeatherCondition.Snow,
            "mist" => WeatherCondition.Fog,
            "fog" => WeatherCondition.Fog,
            "haze" => WeatherCondition.Fog,
            "thunderstorm" => WeatherCondition.Storm,
            _ => WeatherCondition.Clouds
        };
    }

    private void SetDefaultWeather()
    {
        CurrentWeather = new WeatherData
        {
            temperature = 15,
            humidity = 50,
            condition = WeatherCondition.Clouds,
            conditionText = "Clouds",
            description = "No Cloud",
            cityName = cityName,
            timestamp = DateTime.Now
        };

        Log("Set default weather data.");
        OnWeatherUpdated?.Invoke(CurrentWeather);
    }

    #endregion

    #region Debug

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[WeatherManager] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[WeatherManager] {message}");
    }

    #endregion
}

/// <summary>
/// 날씨 데이터 클래스
/// </summary>
[Serializable]
public class WeatherData
{
    public int temperature;
    public int humidity;
    public WeatherCondition condition;
    public string conditionText;
    public string description;
    public string cityName;
    public DateTime timestamp;

    public string GetIcon()
    {
        return condition switch
        {
            WeatherCondition.Clear => "[Sun]",
            WeatherCondition.Clouds => "[Cloud]",
            WeatherCondition.Overcast => "[Overcast]",
            WeatherCondition.Rain => "[Rain]",
            WeatherCondition.Snow => "[Snow]",
            WeatherCondition.Fog => "[Fog]",
            WeatherCondition.Storm => "[Storm]",
            _ => "[Weather]"
        };
    }
}

/// <summary>
/// 날씨 상태 열거형
/// </summary>
public enum WeatherCondition
{
    Clear,      // 맑음
    Clouds,     // 구름 조금
    Overcast,   // 흐림
    Rain,       // 비
    Snow,       // 눈
    Fog,        // 안개
    Storm       // 폭풍
}