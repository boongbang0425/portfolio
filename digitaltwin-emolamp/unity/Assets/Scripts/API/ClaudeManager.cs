using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;

/// <summary>
/// Claude API를 통해 텍스트의 감정을 분석하는 매니저
/// 감정에 따라 색감(H) 값 결정
/// </summary>
public class ClaudeManager : MonoBehaviour
{
    [Header("API Settings")]
    [SerializeField] private string apiKey = "your_api_key_here"; // 실제 API 키로 교체 필요
    [SerializeField] private string model = "claude-sonnet-4-20250514";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // 이벤트
    public event Action<EmotionResult> OnEmotionAnalyzed;
    public event Action<string> OnAnalysisError;

    public bool IsAnalyzing { get; private set; } = false;
    public EmotionResult LastResult { get; private set; }

    private const string API_URL = "https://api.anthropic.com/v1/messages";

    #region Public Methods

    /// <summary>
    /// 텍스트 감정 분석
    /// </summary>
    public void AnalyzeEmotion(string text, string weatherInfo = "")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            LogError("There are No Text.");
            return;
        }

        StartCoroutine(AnalyzeEmotionCoroutine(text, weatherInfo));
    }

    /// <summary>
    /// API 키 설정
    /// </summary>
    public void SetApiKey(string key)
    {
        apiKey = key;
    }

    /// <summary>
    /// 감정에 따른 Hue(색감) 반환 (0-360)
    /// </summary>
    public static int GetHueFromEmotion(EmotionType emotion)
    {
        return emotion switch
        {
            EmotionType.Joy => 50,       // 골드/노랑
            EmotionType.Sadness => 220,  // 블루
            EmotionType.Anger => 0,      // 레드
            EmotionType.Calm => 120,     // 그린
            EmotionType.Excited => 30,   // 오렌지
            EmotionType.Fear => 280,     // 퍼플
            EmotionType.Surprise => 60,  // 밝은 노랑
            _ => 120                     // 기본: 그린
        };
    }

    #endregion

    #region API Call

    private IEnumerator AnalyzeEmotionCoroutine(string text, string weatherInfo)
    {
        IsAnalyzing = true;
        Log($"Start Emotion Analysis: {text}");

        // 프롬프트 구성
        string prompt = BuildPrompt(text, weatherInfo);

        // 요청 JSON 생성
        string requestJson = BuildRequestJson(prompt);
        Log($"Request JSON: {requestJson}");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);

        using (UnityWebRequest request = new UnityWebRequest(API_URL, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-api-key", apiKey);
            request.SetRequestHeader("anthropic-version", "2023-06-01");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Log($"Response: {request.downloadHandler.text}");
                EmotionResult result = ParseResponse(request.downloadHandler.text);
                
                if (result != null)
                {
                    LastResult = result;
                    Log($"Finish Analysis: {result.emotion} (H:{result.hue})");
                    OnEmotionAnalyzed?.Invoke(result);
                }
                else
                {
                    LogError("Failed Parse Response");
                    SetDefaultResult(text);
                }
            }
            else
            {
                LogError($"API error: {request.error}");
                LogError($"Response: {request.downloadHandler.text}");
                OnAnalysisError?.Invoke(request.error);
                SetDefaultResult(text);
            }
        }

        IsAnalyzing = false;
    }

    private string BuildPrompt(string text, string weatherInfo)
    {
        string weather = string.IsNullOrEmpty(weatherInfo) ? "No Info" : weatherInfo;

        return $@"You are an emotion-analysis AI.
Analyze the current mood by combining the given text and weather information.

Input:
- Text: {text}
- Weather: {weather}

You must respond only in the JSON format below. Output JSON only, with no additional explanation.

{{
  ""emotion"": ""joy|sadness|anger|calm|excited|fear|surprise"",
  ""hue"": 0-360,
  ""summary"": ""English summary within 20 characters""
}}

emotion–hue mapping:
- joy: 50 (gold)
- sadness: 220 (blue)
- anger: 0 (red)
- calm: 120 (green)
- excited: 30 (orange)
- fear: 280 (purple)
- surprise: 60 (bright yellow)";
    }

    private string BuildRequestJson(string prompt)
    {
        // JSON 이스케이프
        string escapedPrompt = prompt
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");

        return $@"{{
  ""model"": ""{model}"",
  ""max_tokens"": 200,
  ""messages"": [
    {{
      ""role"": ""user"",
      ""content"": ""{escapedPrompt}""
    }}
  ]
}}";
    }

    private EmotionResult ParseResponse(string json)
    {
        try
        {
            // Claude 응답에서 content 텍스트 추출
            // 형식: {"content":[{"text":"..."}],...}
            int textIndex = json.IndexOf("\"text\":\"");
            if (textIndex < 0)
            {
                LogError("Text Field Not Found");
                return null;
            }

            int startIndex = textIndex + 8;
            int endIndex = FindJsonStringEnd(json, startIndex);
            if (endIndex < 0)
            {
                LogError("Text Field End Not Found");
                return null;
            }

            string contentText = json.Substring(startIndex, endIndex - startIndex);
            // 이스케이프 문자 복원
            contentText = contentText
                .Replace("\\n", "\n")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");

            Log($"Claude Response Text: {contentText}");

            // JSON 부분만 추출
            int jsonStart = contentText.IndexOf('{');
            int jsonEnd = contentText.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd < 0)
            {
                LogError("JSON Not Found in Content");
                return null;
            }

            string emotionJson = contentText.Substring(jsonStart, jsonEnd - jsonStart + 1);
            Log($"Emotion JSON: {emotionJson}");

            // 감정 결과 파싱
            return ParseEmotionJson(emotionJson);
        }
        catch (Exception e)
        {
            LogError($"Parsing Error : {e.Message}");
            return null;
        }
    }

    private int FindJsonStringEnd(string json, int startIndex)
    {
        bool escaped = false;
        for (int i = startIndex; i < json.Length; i++)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (json[i] == '\\')
            {
                escaped = true;
                continue;
            }

            if (json[i] == '"')
            {
                return i;
            }
        }
        return -1;
    }

    private EmotionResult ParseEmotionJson(string json)
    {
        EmotionResult result = new EmotionResult();

        // emotion 추출
        int emotionIndex = json.IndexOf("\"emotion\"");
        if (emotionIndex >= 0)
        {
            int colonIndex = json.IndexOf(':', emotionIndex);
            int quoteStart = json.IndexOf('"', colonIndex + 1);
            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteStart >= 0 && quoteEnd > quoteStart)
            {
                string emotionStr = json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                result.emotion = ParseEmotionType(emotionStr);
            }
        }

        // hue 추출
        int hueIndex = json.IndexOf("\"hue\"");
        if (hueIndex >= 0)
        {
            int colonIndex = json.IndexOf(':', hueIndex);
            string hueStr = "";
            for (int i = colonIndex + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (char.IsDigit(c))
                {
                    hueStr += c;
                }
                else if (hueStr.Length > 0)
                {
                    break;
                }
            }
            if (int.TryParse(hueStr, out int hue))
            {
                result.hue = Mathf.Clamp(hue, 0, 360);
            }
        }

        // summary 추출
        int summaryIndex = json.IndexOf("\"summary\"");
        if (summaryIndex >= 0)
        {
            int colonIndex = json.IndexOf(':', summaryIndex);
            int quoteStart = json.IndexOf('"', colonIndex + 1);
            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteStart >= 0 && quoteEnd > quoteStart)
            {
                result.summary = json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            }
        }

        // 기본값 처리
        if (result.hue == 0 && result.emotion != EmotionType.Anger)
        {
            result.hue = GetHueFromEmotion(result.emotion);
        }

        if (string.IsNullOrEmpty(result.summary))
        {
            result.summary = GetDefaultSummary(result.emotion);
        }

        result.timestamp = DateTime.Now;
        return result;
    }

    private EmotionType ParseEmotionType(string emotion)
    {
        return emotion.ToLower() switch
        {
            "joy" => EmotionType.Joy,
            "sadness" => EmotionType.Sadness,
            "anger" => EmotionType.Anger,
            "calm" => EmotionType.Calm,
            "excited" => EmotionType.Excited,
            "fear" => EmotionType.Fear,
            "surprise" => EmotionType.Surprise,
            _ => EmotionType.Calm
        };
    }

    private string GetDefaultSummary(EmotionType emotion)
    {
        return emotion switch
        {
            EmotionType.Joy => "Bright and positive atmosphere",
            EmotionType.Sadness => "Calm and gloomy atmosphere",
            EmotionType.Anger => "Agitated and angry atmosphere",
            EmotionType.Calm => "Peaceful and stable atmosphere",
            EmotionType.Excited => "Lively and excited atmosphere",
            EmotionType.Fear => "Anxious and fearful atmosphere",
            EmotionType.Surprise => "Surprising and refreshing atmosphere",
            _ => "Peaceful atmosphere"
        };
    }

    private void SetDefaultResult(string text)
    {
        LastResult = new EmotionResult
        {
            emotion = EmotionType.Calm,
            hue = 120,
            summary = "Basic Analysis Result",
            timestamp = DateTime.Now
        };
        OnEmotionAnalyzed?.Invoke(LastResult);
    }

    #endregion

    #region Debug

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ClaudeManager] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[ClaudeManager] {message}");
    }

    #endregion
}

/// <summary>
/// 감정 분석 결과
/// </summary>
[Serializable]
public class EmotionResult
{
    public EmotionType emotion;
    public int hue;           // 0-360
    public string summary;
    public DateTime timestamp;

    public string GetEmoji()
    {
        return emotion switch
        {
            EmotionType.Joy => ":)",
            EmotionType.Sadness => ":(",
            EmotionType.Anger => ">:(",
            EmotionType.Calm => "-_-",
            EmotionType.Excited => ":D",
            EmotionType.Fear => "D:",
            EmotionType.Surprise => ":O",
            _ => ":|"
        };
    }

    public string GetEmotionKorean()
    {
        return emotion switch
        {
            EmotionType.Joy => "Joy",
            EmotionType.Sadness => "Sadness",
            EmotionType.Anger => "Anger",
            EmotionType.Calm => "Calm",
            EmotionType.Excited => "Excitement",
            EmotionType.Fear => "Fear",
            EmotionType.Surprise => "Surprise",
            _ => "Neutral"
        };
    }
}

/// <summary>
/// 감정 타입 열거형
/// </summary>
public enum EmotionType
{
    Joy,        // 기쁨
    Sadness,    // 슬픔
    Anger,      // 분노
    Calm,       // 평온
    Excited,    // 설렘
    Fear,       // 두려움
    Surprise    // 놀람
}
