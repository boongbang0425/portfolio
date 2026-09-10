using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 대화 파이프라인 상태(F-013 3.3 · 6종)를 화면 UI가 아니라 이음이 캐릭터 자체의 발광·모션으로
/// 표현한다. 화면 HUD(IeumiHud)와 병행 동작하는 월드 피드백이며, HUD 제거는 검증 후 별도로 한다.
///
/// - 발광: 자식 렌더러의 머티리얼을 인스턴스화해 <c>_EMISSION</c> 키워드를 켜고
///   <c>_EmissionColor</c>(HDR)를 매 프레임 보간한다. URP Lit은 키워드가 꺼져 있으면 색을 무시하고,
///   MaterialPropertyBlock으로는 키워드를 못 켜므로 인스턴스에 직접 쓰는 것이 유일한 경로다
///   (<see cref="Board3DButton"/> 선례). 이음이는 씬에 1체라 인스턴스 비용은 무시 가능하다.
///   Play 씬은 Bloom이 켜져 있어(threshold 1) 세기 2~4의 HDR 발광이 실제로 번진다.
/// - 발광 제외: <see cref="excludeRendererNames"/>에 오른 렌더러는 수집·인스턴스화 대상에서 제외해
///   원본 머티리얼을 유지한다. 기본값은 망토(스카프) 메시다 — iumi.fbx에서 스카프 천(scarf.png 텍스처의
///   Material_2.001)을 쓰는 노드는 Object_1(본체)·Object_6/Object_9(좌우 자락)이고, 나머지
///   (Object_3·4·14=목재 몸통, ScrollR=두루마리, quill_pen_*=머리 잎)는 신체·소품이다.
///   부분 일치가 아니라 정확 일치로 비교한다 — "Object_1"이 "Object_14"를 삼키면 안 된다.
/// - Idle/Listening 대비: "대화 가능"(Idle)은 어두운 상시등(세기 0.15)으로 낮추고, "듣는 중"(Listening)은
///   마이크 입력과 무관하게 항상 도는 브리딩 펄스(0.8s, ±40%)와 진입 백색 플래시(0.2s)를 얹어
///   무음에도 두 상태가 색·밝기·리듬 모두로 구분되게 한다. Thinking 펄스(1.2s)와 주기를 달리 둔다.
/// - 모션: <see cref="EeumBehaviour.SetMotionFeedback"/> 훅으로 듣는 중 접근(추종 거리 가산),
///   생각 중 부유 가속을 얹는다. 값은 여기서 보간해 넘긴다 — 추종 거리가 스텝으로 튀면 스팟이
///   순간이동해 SmoothDamp 완충으로도 홱 꺾이는 그림이 남는다.
/// - 릴레이 억제: Idle·Unavailable 밖 상태에서 <see cref="EeumBehaviour.SuppressIdleRelay"/>를 켜
///   듣는 중·생각 중에 맥락 없는 절레·백덤블링이 나오지 않게 한다.
///
/// 상태 원천은 <see cref="AiConversationManager.StateChanged"/> 하나다. 매니저는 부트스트랩
/// 싱글톤이라 씬 로드 시점에 없을 수 있어, 구독은 잡힐 때까지 Update에서 재시도한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EeumStateFeedback : MonoBehaviour
{
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("발광 제외")]
    [SerializeField]
    [Tooltip("발광에서 제외할 렌더러의 GameObject 이름(정확 일치, 대소문자 무시). 기본값은 망토(스카프) 메시 3개다.")]
    string[] excludeRendererNames = { "Object_1", "Object_6", "Object_9" };

    [Header("발광 팔레트 (HUD 승계)")]
    [SerializeField]
    [Tooltip("질문 가능(Idle) 색. 은은한 하늘색.")]
    Color idleColor = new Color32(0x4F, 0xCB, 0xFF, 0xFF);

    // 세기 필드 2종은 2026-08-27에 이름을 바꿨다(idleIntensity→idleEmissionIntensity,
    // listeningIntensity→listeningBaseIntensity). Play 씬에 구 기본값(0.6/1.6)이 직렬화돼 있어
    // 기본값만 바꾸면 씬 값이 이기므로, FormerlySerializedAs 없이 개명해 구 값을 의도적으로 버리고
    // 새 기본값이 적용되게 했다. 당시 씬 값은 전부 코드 기본값 그대로라 잃는 튜닝은 없다.
    [SerializeField]
    [Tooltip("Idle 발광 세기. '대화 가능'은 어두운 상시등으로 둬 Listening과 확실히 대비시킨다.")]
    float idleEmissionIntensity = 0.15f;

    [SerializeField]
    [Tooltip("듣는 중(Listening) 색. 웜 레드.")]
    Color listeningColor = new Color32(0xFF, 0x76, 0x76, 0xFF);

    [SerializeField]
    [Tooltip("Listening 기본 발광 세기.")]
    float listeningBaseIntensity = 2.5f;

    [SerializeField]
    [Tooltip("마이크 입력 레벨(0..1)에 비례해 가산되는 발광 세기.")]
    float microphoneGain = 2.5f;

    [SerializeField]
    [Tooltip("Listening 브리딩 펄스 한 주기(초). Thinking 펄스 주기와 다르게 둬 리듬으로도 구분되게 한다.")]
    float listeningPulsePeriodSeconds = 0.8f;

    [SerializeField]
    [Tooltip("Listening 기본 세기에 곱하는 브리딩 펄스 비율. 0.4면 기본 세기의 60%~140%를 오간다. 마이크 가산과 무관하게 항상 돈다.")]
    float listeningPulseRatio = 0.4f;

    [SerializeField]
    [Tooltip("켜면 Listening 진입 순간 짧은 백색 플래시로 상태 전환을 알린다.")]
    bool flashOnListeningEnter = true;

    [SerializeField]
    [Tooltip("Listening 진입 플래시 지속 시간(초).")]
    float listeningFlashSeconds = 0.2f;

    [SerializeField]
    [Tooltip("Listening 진입 플래시의 백색 발광 세기(HDR). 감쇠하며 0으로 떨어진다.")]
    float listeningFlashIntensity = 3f;

    [SerializeField]
    [Tooltip("변환 중(Transcribing)·생각 중(Thinking) 색. 앰버.")]
    Color processingColor = new Color32(0xFF, 0xCD, 0x60, 0xFF);

    [SerializeField]
    [Tooltip("Transcribing·Thinking 기본 발광 세기.")]
    float processingIntensity = 1.4f;

    [SerializeField]
    [Tooltip("Transcribing·Thinking 사인파 펄스의 가산 진폭.")]
    float pulseAmplitude = 1.2f;

    [SerializeField]
    [Tooltip("펄스 한 주기(초).")]
    float pulsePeriodSeconds = 1.2f;

    [SerializeField]
    [Tooltip("답변 중(Speaking) 색. 그린.")]
    Color speakingColor = new Color32(0x7E, 0xEA, 0xA8, 0xFF);

    [SerializeField]
    [Tooltip("Speaking 기본 발광 세기.")]
    float speakingIntensity = 1.6f;

    [SerializeField]
    [Tooltip("TTS 출력 진폭(0..1)에 비례해 가산되는 발광 세기.")]
    float voiceGain = 3.0f;

    [Header("전이")]
    [SerializeField]
    [Tooltip("상태 전이 시 발광·모션이 목표값에 도달하는 대략의 시간(초).")]
    float transitionSeconds = 0.25f;

    [Header("모션")]
    [SerializeField]
    [Tooltip("Listening에서 추종 거리에 가산할 값(m). 음수면 플레이어에게 다가온다.")]
    float listeningFollowDistanceOffset = -2f;

    [SerializeField]
    [Tooltip("Thinking에서 부유(봅) 속도에 곱할 배율.")]
    float thinkingBobSpeedMultiplier = 1.6f;

    [Header("애니메이션")]
    [SerializeField]
    [Tooltip("켜면 Speaking 진입 시 인사 리액션을 1회 재생한다. 대기 릴레이와의 큐 경합을 피해 기본 off다.")]
    bool playGreetOnSpeaking;

    [SerializeField]
    [Tooltip("Speaking 진입 인사 리액션의 재생 시간(초).")]
    float greetSeconds = 2.0f;

    AiConversationManager _manager;
    EeumBehaviour _behaviour;
    Material[] _materials;

    AiConversationState _state = AiConversationState.Unavailable;
    Color _emission = Color.black;
    float _listeningFlashEndTime = float.NegativeInfinity;

    // 모션 훅으로 넘기는 값의 보간 상태. 복원값(1·1·0)에서 출발한다.
    float _bobSpeedMultiplier = 1f;
    float _followDistanceOffset;

    void Awake()
    {
        _behaviour = GetComponent<EeumBehaviour>();
        if (_behaviour == null)
            Debug.LogWarning("[EeumStateFeedback] EeumBehaviour가 없어 모션 채널 없이 발광만 동작합니다.", this);

        CollectMaterials();
    }

    /// <summary>
    /// 제외 목록에 없는 자식 렌더러의 머티리얼을 인스턴스화하고 발광 키워드를 켠다. renderer.materials
    /// 접근이 인스턴스를 만들므로(Board3DButton 선례) 공유 머티리얼 자산은 건드리지 않는다.
    /// 제외 렌더러는 materials 접근 자체를 하지 않아 인스턴스가 생기지 않고 원본이 유지된다.
    /// </summary>
    void CollectMaterials()
    {
        var collected = new List<Material>();
        foreach (var childRenderer in GetComponentsInChildren<Renderer>(true))
        {
            if (IsExcluded(childRenderer.gameObject.name)) continue;

            foreach (var material in childRenderer.materials)
            {
                if (material == null) continue;
                material.EnableKeyword("_EMISSION");
                collected.Add(material);
            }
        }

        _materials = collected.ToArray();
        if (_materials.Length == 0)
            Debug.LogWarning("[EeumStateFeedback] 발광을 적용할 렌더러가 없습니다.", this);
    }

    /// <summary>
    /// 렌더러 이름이 제외 목록과 정확히 일치하는지 본다(대소문자 무시). 부분 일치를 쓰지 않는 이유는
    /// "Object_1"이 "Object_14"까지 삼키는 오탐 때문이다.
    /// </summary>
    bool IsExcluded(string rendererName)
    {
        if (excludeRendererNames == null) return false;
        foreach (var excluded in excludeRendererNames)
            if (!string.IsNullOrEmpty(excluded) &&
                string.Equals(rendererName, excluded.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    void OnEnable() => TrySubscribe();

    void OnDisable()
    {
        if (_manager != null)
        {
            _manager.StateChanged -= OnStateChanged;
            _manager = null;
        }

        // 비활성화 시 이음이를 평상시로 되돌린다. 발광도 꺼서 마지막 상태 색이 얼어붙지 않게 한다.
        _state = AiConversationState.Unavailable;
        _bobSpeedMultiplier = 1f;
        _followDistanceOffset = 0f;
        _emission = Color.black;
        _listeningFlashEndTime = float.NegativeInfinity;
        ApplyEmission(_emission);

        if (_behaviour != null)
        {
            _behaviour.SuppressIdleRelay = false;
            _behaviour.SetMotionFeedback(1f, 1f, 0f);
        }
    }

    void OnDestroy()
    {
        // CollectMaterials의 renderer.materials 접근이 만든 인스턴스를 파괴한다 (Board3DButton 선례).
        if (_materials == null) return;
        foreach (var material in _materials)
            if (material != null)
                Destroy(material);
    }

    void Update()
    {
        // 매니저는 부트스트랩 순서에 따라 늦게 생길 수 있다. 잡힐 때까지 재시도한다.
        TrySubscribe();

        // transitionSeconds 안에 목표의 약 95%에 도달하는 지수 보간 계수.
        var blend = 1f - Mathf.Exp(-3f * Time.deltaTime / Mathf.Max(0.01f, transitionSeconds));

        _emission = Color.Lerp(_emission, TargetEmission(), blend);
        ApplyEmission(_emission + ListeningFlash());

        TickMotion(blend);
    }

    void TrySubscribe()
    {
        if (_manager != null || !AiConversationManager.HasInstance) return;

        _manager = AiConversationManager.Instance;
        _manager.StateChanged += OnStateChanged;
        OnStateChanged(_manager.State);
    }

    void OnStateChanged(AiConversationState state)
    {
        var entered = state != _state;
        _state = state;

        if (entered && state == AiConversationState.Listening && flashOnListeningEnter)
            _listeningFlashEndTime = Time.time + Mathf.Max(0f, listeningFlashSeconds);

        if (_behaviour == null) return;

        _behaviour.SuppressIdleRelay =
            state != AiConversationState.Idle && state != AiConversationState.Unavailable;

        if (entered && state == AiConversationState.Speaking && playGreetOnSpeaking)
            _behaviour.PlayGreetAnim(greetSeconds);
    }

    /// <summary>
    /// 현재 상태의 목표 발광색(HDR). 마이크·펄스·TTS 진폭 같은 동적 성분도 목표에 포함시키고
    /// 보간은 한 곳(Update)에서만 한다 — 전이 완충과 맥동 평활을 같은 시간 상수가 겸한다.
    /// </summary>
    Color TargetEmission()
    {
        switch (_state)
        {
            case AiConversationState.Idle:
                return idleColor * idleEmissionIntensity;

            case AiConversationState.Listening:
            {
                // 브리딩 펄스는 마이크 가산과 별개로 항상 돈다 — 무음에도 "빨갛게 숨쉬는" 상태가
                // 보여야 Idle 상시등과 구분된다. Time.time이라 일시정지 중에는 함께 멈춘다.
                var phase = Time.time * (2f * Mathf.PI) / Mathf.Max(0.1f, listeningPulsePeriodSeconds);
                var breath = 1f + listeningPulseRatio * Mathf.Sin(phase);
                var mic = _manager != null ? Mathf.Clamp01(_manager.MicrophoneLevel) : 0f;
                return listeningColor * (listeningBaseIntensity * breath + mic * microphoneGain);
            }

            case AiConversationState.Transcribing:
            case AiConversationState.Thinking:
            {
                // Time.time이라 일시정지(timeScale 0) 중에는 펄스도 함께 멈춘다.
                var phase = Time.time * (2f * Mathf.PI) / Mathf.Max(0.1f, pulsePeriodSeconds);
                var pulse = 0.5f + 0.5f * Mathf.Sin(phase);
                return processingColor * (processingIntensity + pulse * pulseAmplitude);
            }

            case AiConversationState.Speaking:
            {
                var voice = AiVoicePlayer.Current != null ? AiVoicePlayer.Current.CurrentLevel : 0f;
                return speakingColor * (speakingIntensity + voice * voiceGain);
            }

            default:
                return Color.black; // Unavailable: 발광 없음.
        }
    }

    /// <summary>
    /// Listening 진입 플래시의 가산 성분. 보간(_emission)을 거치면 0.2s 스파이크가 뭉개지므로
    /// 보간 뒤에 더한다. 남은 시간에 비례해 선형 감쇠한다.
    /// </summary>
    Color ListeningFlash()
    {
        var remaining = _listeningFlashEndTime - Time.time;
        if (remaining <= 0f || listeningFlashSeconds <= 0.001f) return Color.black;
        return Color.white * (listeningFlashIntensity * Mathf.Clamp01(remaining / listeningFlashSeconds));
    }

    void ApplyEmission(Color color)
    {
        if (_materials == null) return;
        for (var i = 0; i < _materials.Length; i++)
            if (_materials[i] != null)
                _materials[i].SetColor(EmissionColorId, color);
    }

    void TickMotion(float blend)
    {
        if (_behaviour == null) return;

        var targetBobSpeed = _state == AiConversationState.Thinking ? thinkingBobSpeedMultiplier : 1f;
        var targetOffset = _state == AiConversationState.Listening ? listeningFollowDistanceOffset : 0f;

        _bobSpeedMultiplier = Mathf.Lerp(_bobSpeedMultiplier, targetBobSpeed, blend);
        _followDistanceOffset = Mathf.Lerp(_followDistanceOffset, targetOffset, blend);
        _behaviour.SetMotionFeedback(_bobSpeedMultiplier, 1f, _followDistanceOffset);
    }
}
