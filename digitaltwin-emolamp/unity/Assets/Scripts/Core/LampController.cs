using UnityEngine;
using System;

/// <summary>
/// 3D 램프 모델과 물리 LED를 동기화하는 컨트롤러
/// HSVController의 색상을 받아 적용
/// </summary>
public class LampController : MonoBehaviour
{
    [Header("3D Model References")]
    [SerializeField] private Transform lampModel;
    [SerializeField] private Renderer bulbRenderer;
    [SerializeField] private Light bulbLight;
    [SerializeField] private int materialIndex = 0;

    [Header("Light Settings")]
    [SerializeField] private float lightIntensityMultiplier = 2f;
    [SerializeField] private float emissionIntensity = 2f;

    [Header("Components")]
    [SerializeField] private HSVController hsvController;
    [SerializeField] private SerialController serialController;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // 이벤트
    public event Action<Color> OnLampColorChanged;

    // 상태
    public Color CurrentColor { get; private set; } = Color.green;

    private Material _bulbMaterial;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    #region Unity Lifecycle

    private void Awake()
    {
        // Material 인스턴스 생성 (공유 방지)
        if (bulbRenderer != null)
        {
            Material[] mats = bulbRenderer.materials;
            if (materialIndex < mats.Length)
            {
                _bulbMaterial = mats[materialIndex];
            }
            else
            {
                _bulbMaterial = bulbRenderer.material;
            }

            // Emission 활성화
            _bulbMaterial.EnableKeyword("_EMISSION");
        }
    }

    private void Start()
    {
        // HSVController 자동 찾기
        if (hsvController == null)
        {
            hsvController = FindObjectOfType<HSVController>();
        }

        // SerialController 자동 찾기
        if (serialController == null)
        {
            serialController = FindObjectOfType<SerialController>();
        }

        // 이벤트 구독
        if (hsvController != null)
        {
            hsvController.OnColorChanged += OnHSVColorChanged;
        }

        // 초기 색상 적용
        ApplyColor(Color.green);
    }

    private void OnDestroy()
    {
        if (hsvController != null)
        {
            hsvController.OnColorChanged -= OnHSVColorChanged;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 색상 직접 설정
    /// </summary>
    public void SetColor(Color color)
    {
        ApplyColor(color);
    }

    /// <summary>
    /// RGB 값으로 색상 설정 (0-255)
    /// </summary>
    public void SetColorRGB(int r, int g, int b)
    {
        Color color = new Color(r / 255f, g / 255f, b / 255f);
        ApplyColor(color);
    }

    /// <summary>
    /// HEX 값으로 색상 설정
    /// </summary>
    public void SetColorHex(string hex)
    {
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (ColorUtility.TryParseHtmlString(hex, out Color color))
        {
            ApplyColor(color);
        }
    }

    /// <summary>
    /// HSV 값으로 색상 설정
    /// </summary>
    public void SetColorHSV(float h, float s, float v)
    {
        Color color = Color.HSVToRGB(h / 360f, s / 100f, v / 100f);
        ApplyColor(color);
    }

    /// <summary>
    /// 물리 LED만 업데이트 (3D 모델 제외)
    /// </summary>
    public void UpdatePhysicalLED(int r, int g, int b)
    {
        if (serialController != null && serialController.IsConnected)
        {
            serialController.SendRGB(r, g, b);
            Log($"Physical LED Send: RGB({r},{g},{b})");
        }
    }

    /// <summary>
    /// 전구 밝기 설정
    /// </summary>
    public void SetBrightness(float brightness01)
    {
        if (bulbLight != null)
        {
            bulbLight.intensity = brightness01 * lightIntensityMultiplier;
        }
    }

    #endregion

    #region Private Methods

    private void OnHSVColorChanged(Color color)
    {
        ApplyColor(color);
    }

    private void ApplyColor(Color color)
    {
        CurrentColor = color;

        // 1. 3D 모델 Emission 업데이트
        if (_bulbMaterial != null)
        {
            _bulbMaterial.SetColor(EmissionColor, color * emissionIntensity);
        }

        // 2. Point Light 색상 업데이트
        if (bulbLight != null)
        {
            bulbLight.color = color;
        }

        // 3. 물리 LED 업데이트
        int r = Mathf.RoundToInt(color.r * 255);
        int g = Mathf.RoundToInt(color.g * 255);
        int b = Mathf.RoundToInt(color.b * 255);

        if (serialController != null && serialController.IsConnected)
        {
            serialController.SendRGB(r, g, b);
        }

        // 4. 이벤트 발생
        OnLampColorChanged?.Invoke(color);

        Log($"Apply lamp color: RGB({r},{g},{b}) / HEX: #{ColorUtility.ToHtmlStringRGB(color)}");
    }

    #endregion

    #region Debug & Gizmos

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[LampController] {message}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 에디터에서 전구 위치 표시
        if (bulbRenderer != null)
        {
            Gizmos.color = CurrentColor;
            Gizmos.DrawWireSphere(bulbRenderer.bounds.center, 0.1f);
        }
    }

    #endregion
}
