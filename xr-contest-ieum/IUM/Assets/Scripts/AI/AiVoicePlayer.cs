using System;
using UnityEngine;

/// <summary>
/// Plays 이음이's answer and owns the subtitle timing. When TTS produced no clip (mock or a
/// failed synthesis) the subtitle is still held for the estimated speaking time, so the
/// 답변 중 state lasts the same as it would with real audio.
/// </summary>
public sealed class AiVoicePlayer
{
    readonly AudioSource _source;
    readonly AiConversationConfig _config;

    AudioClip _clip;
    float _startTime;
    float _endTime;

    // TTS 출력 진폭 캐시. 프레임당 한 번만 GetOutputData를 돌리기 위한 것이다 (CurrentLevel).
    float[] _levelSamples;
    int _levelFrame = -1;
    float _currentLevel;

    /// <summary>
    /// 살아 있는 플레이어 인스턴스. 이음이 캐릭터 피드백(EeumStateFeedback)이 TTS 출력 진폭을
    /// 읽는 통로다 — 매니저 내부 필드(_voice)를 공개하지 않고 여기서 자기 등록한다. 파이프라인당
    /// 플레이어는 1개뿐이라 정적 참조로 충분하다.
    /// </summary>
    public static AiVoicePlayer Current { get; private set; }

    public AiVoicePlayer(Transform parent, AiConversationConfig config)
    {
        _config = config ?? new AiConversationConfig();
        Current = this;

        var host = new GameObject("IeumiVoice");
        host.transform.SetParent(parent, false);
        _source = host.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f; // 이음이 speaks to the player, not from a world position
        ApplyVolume();
    }

    public bool IsSpeaking { get; private set; }

    /// <summary>
    /// 현재 답변의 발화 길이(초, 자막 꼬리 제외). 재생 중이 아니면 0. 자막 페이지 전환
    /// (AiConversationHud)이 진행률 계산에 쓴다. 클립이 없으면 추정 길이다.
    /// </summary>
    public float Duration { get; private set; }

    /// <summary>발화 시작 이후 경과(초). 일시정지 구간은 흐르지 않는다 (PauseService.Now).</summary>
    public float Elapsed => IsSpeaking ? Mathf.Clamp(PauseService.Now - _startTime, 0f, Duration) : 0f;

    /// <summary>
    /// 현재 TTS 출력 진폭(0..1, RMS). 프레임당 한 번만 계산해 캐싱한다. 재생 중이 아니거나
    /// 클립 없는 추정 재생(목 서비스·합성 실패의 자막 유지 구간)에서는 0이다.
    /// </summary>
    public float CurrentLevel
    {
        get
        {
            if (!IsSpeaking || _source == null || !_source.isPlaying) return 0f;
            if (Time.frameCount == _levelFrame) return _currentLevel;

            _levelFrame = Time.frameCount;
            _levelSamples ??= new float[256];
            _source.GetOutputData(_levelSamples, 0);

            var sum = 0f;
            for (var i = 0; i < _levelSamples.Length; i++)
                sum += _levelSamples[i] * _levelSamples[i];

            _currentLevel = Mathf.Clamp01(Mathf.Sqrt(sum / _levelSamples.Length));
            return _currentLevel;
        }
    }

    /// <summary>Raised on the main thread when the answer finished playing or was stopped.</summary>
    public event Action Finished;

    /// <summary>
    /// 이음이 음성도 대사이므로 DIALOGUE 버스를 지난다 (ISSUE-015). 노장 대사와 같은 슬라이더가
    /// 걸리고, 마스터 볼륨과 뮤트도 함께 적용된다.
    /// </summary>
    public void ApplyVolume() => _source.volume = AudioBusVolume.Resolve(Core.Audio.AudioBus.Dialogue);

    public void Play(string text, AudioClip clip)
    {
        Stop(false);
        ApplyVolume();

        var duration = clip != null
            ? clip.length
            : EstimateSeconds(text);

        _clip = clip;
        if (clip != null)
        {
            _source.clip = clip;
            _source.Play();
        }

        IsSpeaking = true;
        Duration = duration;

        // PauseService.Now는 일시정지 구간을 뺀 시각이다 (ISSUE-008). Time.unscaledTime을 쓰면
        // 메뉴를 여는 동안에도 시계가 흘러, AudioListener.pause로 멈춰 선 음성보다 자막이 먼저
        // 끝난다.
        _startTime = PauseService.Now;
        _endTime = _startTime + duration + _config.SubtitleTailSeconds;
    }

    public void Stop(bool raiseFinished = true)
    {
        var wasSpeaking = IsSpeaking;
        IsSpeaking = false;
        Duration = 0f;

        if (_source != null)
        {
            _source.Stop();
            _source.clip = null;
        }

        // Clips are created per answer by the decoder, so they must be released explicitly.
        if (_clip != null)
        {
            UnityEngine.Object.Destroy(_clip);
            _clip = null;
        }

        if (wasSpeaking && raiseFinished) Finished?.Invoke();
    }

    public void Tick()
    {
        if (!IsSpeaking) return;
        if (PauseService.Now < _endTime) return;
        Stop();
    }

    public void Dispose()
    {
        Stop(false);
        if (Current == this) Current = null;
        if (_source != null) UnityEngine.Object.Destroy(_source.gameObject);
    }

    float EstimateSeconds(string text) =>
        string.IsNullOrWhiteSpace(text) ? 0f : text.Length / Mathf.Max(1f, _config.SpeechCharsPerSecond);
}
