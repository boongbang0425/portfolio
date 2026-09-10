using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 조도 센서값에 따라 Unity 배경 밝기를 조절하는 컨트롤러
/// </summary>
public class BackgroundController : MonoBehaviour
{
    [Header("Background References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Light directionalLight;

    [Header("Colors")]
    [SerializeField] private Color brightColor = new Color(0.2f, 0.3f, 0.5f);   // 밝을 때
    [SerializeField] private Color darkColor = new Color(0.05f, 0.08f, 0.15f);  // 어두울 때

    [Header("Settings")]
    [SerializeField] private float smoothSpeed = 2f;
    [SerializeField, Range(0, 100)] private int currentBrightness = 50;

    // SerialController 제거됨 - Cloudtype 연동으로 대체
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // 상태
    public int CurrentBrightness => currentBrightness;

    private float _targetBrightness = 0.5f;
    private float _currentBrightness = 0.5f;

    #region Unity Lifecycle

    private void Start()
    {
        // Camera 자동 찾기
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // 초기값 적용
        ApplyBrightness(_currentBrightness);
    }

    private void Update()
    {
        // 부드러운 보간
        if (Mathf.Abs(_currentBrightness - _targetBrightness) > 0.01f)
        {
            _currentBrightness = Mathf.MoveTowards(_currentBrightness, _targetBrightness, smoothSpeed * Time.deltaTime);
            ApplyBrightness(_currentBrightness);
        }
    }

    private void OnDestroy()
    {
        // 조도 센서 제거됨 - 별도 해제 불필요
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 밝기 설정 (0-100)
    /// </summary>
    public void SetBrightness(int brightness)
    {
        currentBrightness = Mathf.Clamp(brightness, 0, 100);
        _targetBrightness = currentBrightness / 100f;
        Log($"Brightness setting: {currentBrightness}%");
    }

    /// <summary>
    /// 밝기 설정 (0-1)
    /// </summary>
    public void SetBrightnessNormalized(float brightness01)
    {
        SetBrightness(Mathf.RoundToInt(brightness01 * 100));
    }

    /// <summary>
    /// 테스트용: 밝기 직접 적용
    /// </summary>
    public void SetBrightnessImmediate(int brightness)
    {
        currentBrightness = Mathf.Clamp(brightness, 0, 100);
        _targetBrightness = currentBrightness / 100f;
        _currentBrightness = _targetBrightness;
        ApplyBrightness(_currentBrightness);
    }

    #endregion

    #region Private Methods

    // 조도 센서 제거됨 - Cloudtype에서 직접 SetBrightness() 호출

    private void ApplyBrightness(float t)
    {
        // 배경색 보간
        Color bgColor = Color.Lerp(darkColor, brightColor, t);

        // Camera 배경색
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = bgColor;
        }

        // UI Image 배경
        if (backgroundImage != null)
        {
            backgroundImage.color = bgColor;
        }

        // Directional Light 강도
        if (directionalLight != null)
        {
            directionalLight.intensity = Mathf.Lerp(0.3f, 1f, t);
        }
    }

    #endregion

    #region Debug

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[BackgroundController] {message}");
        }
    }

    #endregion
}
