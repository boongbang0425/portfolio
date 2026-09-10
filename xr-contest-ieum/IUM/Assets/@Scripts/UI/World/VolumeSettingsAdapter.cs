using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 볼륨 옵션의 읽기·적용·저장 (F-002). Same behaviour as <see cref="VolumeOptionsPanel"/> but with no
/// UIElements dependency, so the world-space 옵션 패널 can drive it from 3D sliders.
///
/// Kept as a separate class rather than folded into <see cref="VolumeOptionsPanel"/>: that one is
/// still bound to the UXML screens, and refactoring a shared core out of it would change files this
/// task was not asked to touch.
///
/// 슬라이더는 0~1을 다루고 저장도 0~1이다 (DataSystem 4.1).
/// </summary>
public sealed class VolumeSettingsAdapter
{
    public enum Channel
    {
        Music,
        Dialogue,
        Environment,
        Video
    }

    readonly float _debounceSeconds;

    bool _savePending;
    float _saveAt;

    public VolumeSettingsAdapter(float debounceSeconds = 0.35f) =>
        _debounceSeconds = Mathf.Max(0f, debounceSeconds);

    // Settings is a pass-through to User.Settings and stays null until the user file is loaded, so
    // IsReady alone is not enough to dereference it.
    public static bool IsDataReady =>
        DataManager.HasInstance && DataManager.Instance.IsReady && DataManager.Instance.Settings != null;

    public bool TryRead(Channel channel, out float value)
    {
        value = 0f;
        if (!IsDataReady) return false;

        var settings = DataManager.Instance.Settings;
        value = channel switch
        {
            Channel.Music => settings.MusicVolume,
            Channel.Dialogue => settings.DialogueVolume,
            Channel.Environment => settings.EnvironmentVolume,
            _ => settings.VideoVolume
        };
        return true;
    }

    /// <summary>Applies the change immediately and schedules the file write on a debounce.</summary>
    public void Write(Channel channel, float value)
    {
        if (!IsDataReady)
        {
            // The panel stays usable before data loads, but there is nothing to write into yet.
            Debug.LogWarning("[Options] 저장 데이터가 아직 준비되지 않아 볼륨 변경을 반영하지 못했습니다.");
            return;
        }

        var settings = DataManager.Instance.Settings;
        var normalized = Mathf.Clamp01(value);

        switch (channel)
        {
            case Channel.Music: settings.MusicVolume = normalized; break;
            case Channel.Dialogue: settings.DialogueVolume = normalized; break;
            case Channel.Environment: settings.EnvironmentVolume = normalized; break;
            default: settings.VideoVolume = normalized; break;
        }

        // 변경된 소리를 즉시 미리 듣기 (F-002 2.3).
        DataManager.Instance.ApplyAudioSettings();

        _savePending = true;
        _saveAt = Time.unscaledTime + _debounceSeconds;
    }

    /// <summary>Runs the debounce. Unscaled so it still works while the game is paused.</summary>
    public void Tick()
    {
        if (!_savePending || Time.unscaledTime < _saveAt) return;
        _ = FlushAsync();
    }

    /// <summary>옵션 변경은 즉시 저장한다 (F-002 2.5). Closing the panel must not drop a pending write.</summary>
    public void Flush()
    {
        if (_savePending) _ = FlushAsync();
    }

    async Task FlushAsync()
    {
        if (!_savePending || !IsDataReady) return;
        _savePending = false;

        try
        {
            await DataManager.Instance.SaveUserAsync();
        }
        catch (Exception exception)
        {
            // Retried rather than dropped: the setting is already applied in memory, so losing the
            // write would only show up as a reverted option next launch.
            _savePending = true;
            _saveAt = Time.unscaledTime + 1f;
            Debug.LogError($"[Options] 볼륨 저장 실패: {exception.Message}");
        }
    }
}
