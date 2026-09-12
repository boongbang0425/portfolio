using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 클립보드 종이 위의 퀘스트 표시 (F-013). 한 프리팹이 두 모드를 겸한다 — 씬에 두 장을 놓고
/// 한쪽만 <see cref="BoardMode.TaskList"/>로 바꾸는 사용을 전제한다.
///
/// 현재 진행은 <see cref="AiProcessContextRegistry"/>를 읽는다. 러너(공정·퀘스트)의 상태는
/// ProcessContextBridge가 이미 그 레지스트리로 흘려보내고 있으므로, 여기서 러너를 다시 구독하면
/// 같은 정보의 배선이 두 벌이 된다. 보드는 소비자 한 명으로 남는다.
///
/// 갱신은 전부 이벤트 구동이다. 종이 위 텍스트는 초당 몇 번씩 바뀔 이유가 없고, VR에서 매 프레임
/// 텍스트 재조립은 낭비다.
/// </summary>
public sealed class QuestBoardView : MonoBehaviour
{
    public enum BoardMode
    {
        /// <summary>지금 하는 공정과 단계 하나를 크게 보여 준다.</summary>
        CurrentProgress,

        /// <summary>전체 공정 목록과 완료 여부를 줄 단위로 보여 준다.</summary>
        TaskList
    }

    /// <summary>해야 할 일 목록에 올리는 공정. 컷씬(프롤로그·엔딩)은 작업이 아니므로 뺀다.</summary>
    static readonly ProcessId[] TaskOrder =
    {
        ProcessId.Tutorial,
        ProcessId.ShortPart,
        ProcessId.MediumPart,
        ProcessId.LongPart,
        ProcessId.GongpoPuzzle
    };

    [Header("모드")]
    [SerializeField] BoardMode mode = BoardMode.CurrentProgress;

    [Header("텍스트 계층 (빌더가 채운다)")]
    [SerializeField] TMP_Text header;
    [SerializeField] RectTransform listContainer;
    [Tooltip("비활성 원본. 줄이 필요할 때마다 복제한다.")]
    [SerializeField] TMP_Text rowTemplate;
    [SerializeField] TMP_Text footer;

    [Header("진행 수량")]
    // 왼쪽 위 디버그 오버레이(MainPlayProcessBridge)가 보여 주던 "신호 done/required"를 월드 보드에
    // 올린 것이다. 출처를 같은 곳(ProcessSignalBus + 목표 amount)으로 두어 두 표시가 어긋날 수 없다.
    [Tooltip("현재 목표의 진행 수량을 (1/3) 형식으로 함께 표시한다. 신호 기반이 아닌 목표는 생략한다.")]
    [SerializeField] bool showObjectiveCount = true;

    [Tooltip("수량 폴링 간격(초). 신호 버스에는 변경 이벤트가 없어 값을 직접 확인한다.")]
    [SerializeField, Min(0.05f)] float countPollSeconds = 0.25f;

    [Header("먹색")]
    [Tooltip("본문 기본색. 종이 위 붓글씨 느낌의 진회갈색.")]
    [SerializeField] Color inkColor = new(0.17f, 0.13f, 0.10f);
    [Tooltip("완료·대기 줄처럼 뒤로 물러날 텍스트의 알파.")]
    [SerializeField, Range(0f, 1f)] float dimmedAlpha = 0.45f;

    readonly List<TMP_Text> _rows = new();

    ProcessRunner _runner;
    QuestManager _quest;
    bool _flowReadyHooked;

    /// <summary>마지막으로 그린 수량 문자열. 값이 바뀔 때만 다시 그린다.</summary>
    string _countText = string.Empty;

    float _nextPollAt;

    public BoardMode Mode => mode;

    void Awake()
    {
        // 단계 순번(n/전체)은 레지스트리 컨텍스트에 없어 러너에서 직접 읽는다. 표시 전용이라
        // 이벤트 구독은 하지 않는다 — 갱신 신호는 레지스트리 Changed 하나로 충분하다.
        _runner = FindAnyObjectByType<ProcessRunner>(FindObjectsInactive.Include);

        // 목표 진행 수량도 같은 이유로 직접 읽는다. 분자는 ProcessSignalBus, 분모는 목표의 amount다.
        _quest = FindAnyObjectByType<QuestManager>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// 수량 폴링. <see cref="ProcessSignalBus"/>에는 변경 이벤트가 없고 존 완료는 초당 몇 번씩
    /// 일어나지 않으므로, 낮은 주기로 값만 확인하고 바뀐 경우에만 다시 그린다.
    /// 일시정지 중에는 <see cref="PauseService.Now"/>가 멈춰 폴링도 함께 멈춘다.
    /// </summary>
    void Update()
    {
        if (!showObjectiveCount) return;
        if (PauseService.Now < _nextPollAt) return;

        _nextPollAt = PauseService.Now + countPollSeconds;

        var text = DescribeObjectiveCount();
        if (text == _countText) return;

        Refresh();
    }

    void OnEnable()
    {
        AiProcessContextRegistry.Changed += OnContextChanged;

        // 저장 데이터가 아직 로드 전이면 목록을 채울 수 없다. GameFlow 준비 시점에 한 번 더
        // 그린다. 씬 중간에 켜지는 경우에는 이미 준비돼 있어 이 훅이 그냥 지나간다.
        if (GameFlow.HasInstance && !GameFlow.Instance.IsReady)
        {
            GameFlow.Instance.Ready += OnFlowReady;
            _flowReadyHooked = true;
        }

        Refresh();
    }

    void OnDisable()
    {
        AiProcessContextRegistry.Changed -= OnContextChanged;
        UnhookFlowReady();
    }

    void OnContextChanged(AiProcessContext _) => Refresh();

    void OnFlowReady()
    {
        UnhookFlowReady();
        Refresh();
    }

    void UnhookFlowReady()
    {
        if (!_flowReadyHooked) return;
        _flowReadyHooked = false;
        if (GameFlow.HasInstance) GameFlow.Instance.Ready -= OnFlowReady;
    }

    void Refresh()
    {
        if (header == null || listContainer == null || rowTemplate == null) return;

        // 그리기 직전에 한 번만 계산해 두 모드가 같은 값을 쓰고, 폴링 비교 기준도 여기서 갱신한다.
        _countText = DescribeObjectiveCount();

        if (mode == BoardMode.CurrentProgress) RenderCurrent();
        else RenderTaskList();
    }

    // ---- 현재 진행 ----

    void RenderCurrent()
    {
        var context = AiProcessContextRegistry.Current;

        header.text = "현재 작업";

        var step = string.IsNullOrWhiteSpace(context.StepName) ? "준비 중" : context.StepName;
        if (!string.IsNullOrEmpty(_countText)) step = $"{step}  {_countText}";

        EnsureRows(2);
        SetRow(0, context.ProcessLabel, 1f, 1.35f);
        SetRow(1, step, 1f);

        SetFooter(DescribeStepIndex());
    }

    /// <summary>단계 순번. 러너가 쉬는 동안(컷씬·튜토리얼)은 표기를 비운다.</summary>
    string DescribeStepIndex()
    {
        if (_runner == null || !_runner.IsRunning) return string.Empty;
        if (_runner.StepIndex < 0 || _runner.StepCount <= 0) return string.Empty;
        return $"단계 {_runner.StepIndex + 1} / {_runner.StepCount}";
    }

    /// <summary>
    /// 현재 목표의 진행 수량을 <c>(1/3)</c> 형식으로 만든다. 출처는
    /// <see cref="MainPlayProcessBridge"/>의 디버그 오버레이와 같다 — 분자는
    /// <see cref="ProcessSignalBus.Read"/>, 분모는 목표의 <see cref="ProcessStepData.Amount"/>다.
    /// 같은 출처를 쓰므로 오버레이와 보드가 어긋날 수 없다.
    ///
    /// 신호 기반이 아닌 목표(튜토리얼의 둘러보기·이동 등)는 셀 대상이 없어 빈 문자열이다 —
    /// 그 경우 보드는 종전처럼 단계 이름만 보여 준다.
    /// </summary>
    string DescribeObjectiveCount()
    {
        if (!showObjectiveCount || _quest == null || !_quest.IsRunning) return string.Empty;

        var objective = _quest.CurrentObjective;
        if (objective == null || objective.Condition != StepCondition.Signal) return string.Empty;
        if (string.IsNullOrWhiteSpace(objective.Target)) return string.Empty;

        var required = Mathf.Max(1, Mathf.RoundToInt(objective.Amount));
        var done = Mathf.Clamp(Mathf.RoundToInt(ProcessSignalBus.Read(objective.Target)), 0, required);
        return $"({done}/{required})";
    }

    // ---- 해야 할 일 목록 ----

    void RenderTaskList()
    {
        header.text = "해야 할 일";

        var progress = DataManager.HasInstance && DataManager.Instance.IsReady
            ? DataManager.Instance.Progress
            : null;

        EnsureRows(TaskOrder.Length);

        var done = 0;
        for (var i = 0; i < TaskOrder.Length; i++)
        {
            var process = TaskOrder[i];
            var state = DescribeState(progress, process);
            if (state == TaskState.Done) done++;

            var current = state == TaskState.Current;

            // 진행 중인 줄에만 수량을 붙인다. 대기·완료 줄에는 셀 것이 없다.
            var text = $"{Label(process)} — {StateText(state)}";
            if (current && !string.IsNullOrEmpty(_countText)) text = $"{text} {_countText}";

            SetRow(i,
                text,
                state == TaskState.Pending || state == TaskState.Done ? dimmedAlpha : 1f,
                current ? 1.1f : 1f);
        }

        SetFooter(progress == null ? "저장 확인 중" : $"완료 {done} / {TaskOrder.Length}");
    }

    enum TaskState { Pending, Current, Done }

    static TaskState DescribeState(UserProgressData progress, ProcessId process)
    {
        if (progress == null) return TaskState.Pending;
        if (progress.IsCompleted(process)) return TaskState.Done;
        return progress.NextProcess == process ? TaskState.Current : TaskState.Pending;
    }

    static string StateText(TaskState state) => state switch
    {
        TaskState.Done => "완료",
        TaskState.Current => "진행 중",
        _ => "대기"
    };

    /// <summary>이음이 컨텍스트와 같은 한국어 공정명을 쓴다. 두 시스템이 다른 이름을 부르면 안 된다.</summary>
    static string Label(ProcessId process) =>
        new AiProcessContext { Process = process }.ProcessLabel;

    // ---- 줄 관리 ----

    void EnsureRows(int count)
    {
        while (_rows.Count < count)
        {
            var row = Instantiate(rowTemplate, listContainer);
            row.gameObject.SetActive(true);
            _rows.Add(row);
        }

        for (var i = 0; i < _rows.Count; i++)
            _rows[i].gameObject.SetActive(i < count);
    }

    void SetRow(int index, string text, float alpha, float sizeScale = 1f)
    {
        var row = _rows[index];
        row.text = text;
        row.color = new Color(inkColor.r, inkColor.g, inkColor.b, alpha);
        row.fontSize = rowTemplate.fontSize * sizeScale;
    }

    void SetFooter(string text)
    {
        if (footer != null) footer.text = text;
    }
}
