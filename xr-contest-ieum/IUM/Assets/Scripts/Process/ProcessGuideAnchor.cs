using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시각 공정 가이드가 강조할 지점을 씬에 적어 두는 등록표. 로직은 없고 데이터만 들고 있으며,
/// 실제 강조와 표시 시간은 <see cref="ProcessGuideService"/>가 맡는다.
///
/// 이 가이드는 F-012의 "이음이가 조작을 말로 안내"를 대체한다. 이음이는 원칙과 격려만 말하고,
/// 어디서 무엇을 다뤄야 하는지는 화면 강조로 알린다. 그래서 '무엇을 강조할지'는 대사 데이터가
/// 아니라 씬 오브젝트가 들고 있어야 하고, 그 자리가 여기다.
///
/// 부착은 <see cref="ProcessGuideAnchor"/> 하나만 보면 되도록 에디터 도구
/// (Tools/PROJECT 이음/공정 가이드 배치)가 자동으로 한다. 손으로 붙여도 동작은 같다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProcessGuideAnchor : MonoBehaviour
{
    [Tooltip("이 앵커가 속한 공정.")]
    [SerializeField] ProcessId process = ProcessId.Tutorial;

    [Tooltip("특정 목표에서만 쓸 앵커면 퀘스트 노드 ID를 적는다. 비우면 공정 전체용이다.")]
    [SerializeField] string objectiveId;

    [Tooltip("강조할 오브젝트. 비우면 이 오브젝트 자신을 강조한다.")]
    [SerializeField] List<GameObject> highlightTargets = new();

    [Tooltip("에디터용 메모. 런타임에서는 읽지 않는다.")]
    [SerializeField, TextArea] string note;

    public ProcessId Process => process;

    /// <summary>빈 값이면 공정 전체용 앵커다.</summary>
    public string ObjectiveId => objectiveId;

    /// <summary>비어 있으면 이 오브젝트 자신이 대상이다.</summary>
    public IReadOnlyList<GameObject> HighlightTargets => highlightTargets;

    public string Note => note;
}
