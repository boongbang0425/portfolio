using System;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Play order of every process. Progress is stored as the process to play next,
/// so completion of earlier processes is a comparison instead of a saved flag.
///
/// 공정 단위가 도구(먹매김·톱질·끌질)에서 <b>부재</b>로 바뀌었다. 한 부재를 먹매김부터 끌질까지
/// 끝까지 가공한 뒤 다음 부재로 넘어가고, 마지막에 도리 설치와 공포 조립을 <see cref="GongpoPuzzle"/>
/// 한 단계에서 이어서 한다. 작은 부재부터 큰 부재로 가며 새 도구가 하나씩 늘어난다.
///
/// 값은 명시하지 않는다 — <see cref="UserProgressData.Complete"/>가 <c>process + 1</c>로 다음 공정을
/// 구하므로 선언 순서가 곧 진행 순서이고 연속이어야 한다.
/// </summary>
public enum ProcessId
{
    Prologue,
    Tutorial,

    /// <summary>짧은 부재. 먹통·톱·평대패·끌을 처음 쥔다.</summary>
    ShortPart,

    /// <summary>중간 부재. 자귀와 배대패가 새로 등장한다.</summary>
    MediumPart,

    /// <summary>긴 부재. 새 도구 없이 배운 것만으로 완주하는 숙련 단계다.</summary>
    LongPart,

    /// <summary>조립. 도리를 걸고 이어서 공포 서른일곱 조각을 짜 올린다.</summary>
    GongpoPuzzle,
    Ending,
    Completed
}

/// <summary>Ordered worst to best so the higher value wins when keeping a best grade.</summary>
public enum ProcessGrade
{
    None,
    Fail,
    Assisted,
    Pass,
    Excellent
}

[Serializable]
public sealed class UserDataDocument
{
    public UserSettingsData Settings { get; set; } = new();
    public UserProgressData Progress { get; set; } = new();
}

[Serializable]
public sealed class UserSettingsData
{
    public float MasterVolume { get; set; } = 1f;
    public float MusicVolume { get; set; } = 0.7f;
    public float DialogueVolume { get; set; } = 1f;
    public float EnvironmentVolume { get; set; } = 0.8f;

    /// <summary>
    /// mp4 컷씬 재생 음량 (F-002 2.2). 영상은 배경음악·내레이션·효과음이 이미 하나로 믹스되어
    /// 있어 나머지 세 분류로 나눌 수 없다. 쪼갤 수 없으므로 분류를 하나 늘렸다 (ISSUE-020).
    ///
    /// 인게임 연출 컷씬은 이 항목과 무관하다. 그쪽 소리는 대사·환경 볼륨을 그대로 탄다.
    /// </summary>
    public float VideoVolume { get; set; } = 1f;

    public void Clamp()
    {
        MasterVolume = Mathf.Clamp01(MasterVolume);
        MusicVolume = Mathf.Clamp01(MusicVolume);
        DialogueVolume = Mathf.Clamp01(DialogueVolume);
        EnvironmentVolume = Mathf.Clamp01(EnvironmentVolume);
        VideoVolume = Mathf.Clamp01(VideoVolume);
    }
}

/// <summary>
/// 이어하기로 돌아왔을 때 되돌릴 플레이어 자세. 어느 씬에서 저장했는지 함께 들고 있어, 좌표가
/// 엉뚱한 씬에 적용되는 일이 없다.
///
/// Flat floats rather than Vector3 and Quaternion: Newtonsoft walks Unity's derived properties on
/// those types (normalized, magnitude, eulerAngles) and writes a bloated, self-referential document.
///
/// Yaw only. Pitch and roll belong to the head — the HMD's in VR, the look module's on the desktop —
/// and restoring them would fight whatever is driving the view on the next run.
/// </summary>
[Serializable]
public sealed class PlayerPoseData
{
    public string Scene { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }

    [JsonIgnore] public bool IsValid => !string.IsNullOrWhiteSpace(Scene);

    [JsonIgnore]
    public Vector3 Position
    {
        get => new(X, Y, Z);
        set
        {
            X = value.x;
            Y = value.y;
            Z = value.z;
        }
    }

    public bool Matches(string sceneName) =>
        IsValid && string.Equals(Scene, sceneName, StringComparison.OrdinalIgnoreCase);
}

[Serializable]
public sealed class UserProgressData
{
    public ProcessId NextProcess { get; set; } = ProcessId.Prologue;

    /// <summary>
    /// 마지막으로 서 있던 자리. Null until a scene reports one, and cleared with the rest of the
    /// progress. 공정 자체는 처음부터 다시 시작하되 위치만 되돌린다.
    /// </summary>
    public PlayerPoseData PlayerPose { get; set; }
    public ProcessGrade ShortPartGrade { get; set; } = ProcessGrade.None;
    public ProcessGrade MediumPartGrade { get; set; } = ProcessGrade.None;
    public ProcessGrade LongPartGrade { get; set; } = ProcessGrade.None;
    public ProcessGrade GongpoPuzzleGrade { get; set; } = ProcessGrade.None;

    [JsonIgnore] public bool HasSaveData => NextProcess != ProcessId.Prologue;

    public bool IsCompleted(ProcessId process) => NextProcess > process;

    public ProcessGrade GetGrade(ProcessId process) => process switch
    {
        ProcessId.ShortPart => ShortPartGrade,
        ProcessId.MediumPart => MediumPartGrade,
        ProcessId.LongPart => LongPartGrade,
        ProcessId.GongpoPuzzle => GongpoPuzzleGrade,
        _ => ProcessGrade.None
    };

    /// <summary>
    /// Moves to the next process. A retried process keeps its best grade (CR-11),
    /// and finishing an already-passed process never rewinds progress.
    /// </summary>
    public void Complete(ProcessId process, ProcessGrade grade = ProcessGrade.None)
    {
        if (grade > GetGrade(process))
            SetGrade(process, grade);

        if (NextProcess <= process)
            NextProcess = process < ProcessId.Completed ? process + 1 : ProcessId.Completed;
    }

    /// <summary>Clears progress for a new game. Settings are intentionally untouched.</summary>
    public void Reset()
    {
        NextProcess = ProcessId.Prologue;
        PlayerPose = null;
        ShortPartGrade = ProcessGrade.None;
        MediumPartGrade = ProcessGrade.None;
        LongPartGrade = ProcessGrade.None;
        GongpoPuzzleGrade = ProcessGrade.None;
    }

    void SetGrade(ProcessId process, ProcessGrade grade)
    {
        switch (process)
        {
            case ProcessId.ShortPart: ShortPartGrade = grade; break;
            case ProcessId.MediumPart: MediumPartGrade = grade; break;
            case ProcessId.LongPart: LongPartGrade = grade; break;
            case ProcessId.GongpoPuzzle: GongpoPuzzleGrade = grade; break;
        }
    }
}
