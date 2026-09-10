using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 공정 데이터 (F-012 2.3). This is the only thing gameplay has to tell 이음이 about,
/// so the 3~6단계 systems can be written later without touching the AI pipeline.
/// </summary>
public sealed class AiProcessContext
{
    /// <summary>Number of consecutive failures after which 노장 시연 allows a direct answer (F-012 2.4).</summary>
    public const int DirectAnswerFailureCount = 3;

    public ProcessId Process { get; set; } = ProcessId.Tutorial;

    /// <summary>Short label of the current step, e.g. "먹선 긋기".</summary>
    public string StepName { get; set; }

    /// <summary>What the player is being asked to do right now. Fed to the model verbatim.</summary>
    public string StepDescription { get; set; }

    /// <summary>직전 실패 원인. Empty when the last attempt succeeded or none was made.</summary>
    public string LastFailureReason { get; set; }

    /// <summary>Consecutive failures on the current step.</summary>
    public int FailureCount { get; set; }

    /// <summary>Extra retrieval keywords supplied by the process, on top of the question text.</summary>
    public List<string> FocusKeywords { get; } = new();

    /// <summary>True once 노장 has demonstrated, which is when concrete instructions are allowed.</summary>
    public bool AllowDirectAnswer => FailureCount >= DirectAnswerFailureCount;

    public string ProcessLabel => Process switch
    {
        ProcessId.Prologue => "프롤로그",
        ProcessId.Tutorial => "조작 튜토리얼",
        ProcessId.ShortPart => "짧은 부재 가공",
        ProcessId.MediumPart => "중간 부재 가공",
        ProcessId.LongPart => "긴 부재 가공",
        ProcessId.GongpoPuzzle => "도리 설치와 공포 조립",
        ProcessId.Ending => "엔딩",
        _ => "전체 진행"
    };

    public AiProcessContext Clone()
    {
        var clone = new AiProcessContext
        {
            Process = Process,
            StepName = StepName,
            StepDescription = StepDescription,
            LastFailureReason = LastFailureReason,
            FailureCount = FailureCount
        };
        clone.FocusKeywords.AddRange(FocusKeywords);
        return clone;
    }
}

/// <summary>Lets a future process system own the context instead of pushing into the registry.</summary>
public interface IAiProcessContextProvider
{
    AiProcessContext GetContext();
}

/// <summary>
/// Single hand-off point between gameplay and 이음이. Until 3~6단계 exist, the registry
/// answers from saved progress so the pipeline is still testable end to end.
/// </summary>
public static class AiProcessContextRegistry
{
    static readonly AiProcessContext Fallback = new();
    static IAiProcessContextProvider _provider;
    static bool _hasExplicitProcess;

    /// <summary>Raised whenever gameplay changes the context, so the HUD can refresh suggestions.</summary>
    public static event Action<AiProcessContext> Changed;

    public static AiProcessContext Current
    {
        get
        {
            var context = _provider?.GetContext();
            if (context != null) return context;

            // No process system yet: derive the chapter from the save file.
            if (!_hasExplicitProcess && DataManager.HasInstance && DataManager.Instance.IsReady)
            {
                var next = DataManager.Instance.Progress.NextProcess;
                Fallback.Process = next == ProcessId.Completed ? ProcessId.Ending : next;
            }

            return Fallback;
        }
    }

    /// <summary>Hands ownership to a gameplay system. Pass null to return to the registry state.</summary>
    public static void SetProvider(IAiProcessContextProvider provider)
    {
        _provider = provider;
        Changed?.Invoke(Current);
    }

    /// <summary>
    /// 자기가 등록한 프로바이더일 때만 해제한다. 씬 전환 중 새 프로바이더가 먼저 등록되는
    /// 순서에서도, 나중에 파괴되는 이전 프로바이더가 남의 등록을 지우지 못한다.
    /// </summary>
    public static void ClearProvider(IAiProcessContextProvider provider)
    {
        if (_provider == null || !ReferenceEquals(_provider, provider)) return;
        _provider = null;
        Changed?.Invoke(Current);
    }

    /// <summary>
    /// 프로바이더 소유 상태의 변경 통지. 폴백 경로는 Set·Report가 스스로 통지하지만, 프로바이더가
    /// 컨텍스트를 소유하면 레지스트리가 변경 시점을 모르므로 프로바이더가 직접 알린다.
    /// </summary>
    public static void NotifyChanged() => Changed?.Invoke(Current);

    public static void SetProcess(ProcessId process, string stepName = null, string stepDescription = null)
    {
        _hasExplicitProcess = true;
        Fallback.Process = process;
        Fallback.StepName = stepName;
        Fallback.StepDescription = stepDescription;
        Fallback.LastFailureReason = null;
        Fallback.FailureCount = 0;
        Fallback.FocusKeywords.Clear();
        Changed?.Invoke(Current);
    }

    public static void SetStep(string stepName, string stepDescription = null)
    {
        Fallback.StepName = stepName;
        if (stepDescription != null) Fallback.StepDescription = stepDescription;
        Fallback.LastFailureReason = null;
        Fallback.FailureCount = 0;
        Changed?.Invoke(Current);
    }

    /// <summary>Call on every failed attempt: the third one unlocks concrete explanations.</summary>
    public static void ReportFailure(string reason)
    {
        Fallback.FailureCount++;
        Fallback.LastFailureReason = reason;
        Changed?.Invoke(Current);
    }

    public static void ReportSuccess()
    {
        Fallback.FailureCount = 0;
        Fallback.LastFailureReason = null;
        Changed?.Invoke(Current);
    }

    public static void SetFocusKeywords(params string[] keywords)
    {
        Fallback.FocusKeywords.Clear();
        if (keywords != null) Fallback.FocusKeywords.AddRange(keywords);
        Changed?.Invoke(Current);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _provider = null;
        _hasExplicitProcess = false;
        Changed = null;
        Fallback.Process = ProcessId.Tutorial;
        Fallback.StepName = null;
        Fallback.StepDescription = null;
        Fallback.LastFailureReason = null;
        Fallback.FailureCount = 0;
        Fallback.FocusKeywords.Clear();
    }
}
