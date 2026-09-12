using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시작 화면의 원형 플레이 버튼(▶) 메뉴. 클릭하면 버튼 위로 "처음부터"·"이어하기" 두 버튼이
/// 나타난다. 이어하기는 저장이 있을 때만 활성화한다 (F-001 1.5).
///
/// 저장이 있는 상태의 "처음부터"는 중앙 모니터 화면 위 월드 확인 패널(ConfirmCanvas)을 먼저 열고,
/// 확인 후에만 <see cref="StartMenuController.RequestStartNewGameConfirmed"/>로 진입한다. 패널이
/// 배선되지 않았으면 종전대로 <see cref="StartMenuController"/>의 UXML 확인 창(#confirm-panel)이
/// 폴백으로 동작한다. 저장 판단과 실제 진입은 계속 StartMenuController 한 곳에 남겨 두 메뉴 구현이
/// 서로 다른 규칙을 갖지 않게 한다 (FixedUIStartMenuAdapter 선례). 옵션·나가기 진입 UI는 이번
/// 개편에서 보류다.
///
/// 버튼은 uGUI(Image+Button)라 마우스(GraphicRaycaster)와 크로스헤어
/// (<see cref="StartScenePlayerInteraction"/>의 onClick.Invoke) 양쪽에서 동작한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class StartPlayMenu : MonoBehaviour
{
    [Tooltip("메뉴 전체 루트(월드 캔버스). 프롤로그 연출·컷씬·씬 전환·확인 패널 중에는 통째로 숨긴다.")]
    [SerializeField] GameObject menuRoot;
    [SerializeField] Button playButton;
    [Tooltip("처음부터·이어하기 두 버튼을 담는 그룹. 평소에는 꺼져 있다.")]
    [SerializeField] GameObject optionsRoot;
    [SerializeField] Button newGameButton;
    [SerializeField] Button continueButton;
    [SerializeField] StartMonitorPrologue prologue;

    [Header("덮어쓰기 확인 (모니터 월드 패널)")]
    [Tooltip("저장이 있을 때 처음부터를 누르면 여는 확인 패널. 비어 있으면 UXML 확인 창이 폴백으로 뜬다.")]
    [SerializeField] GameObject confirmRoot;
    [SerializeField] Button confirmAcceptButton;
    [SerializeField] Button confirmCancelButton;

    StartMenuController _startMenu;

    void Awake()
    {
        // 씬에 영구 이벤트를 남기지 않고 코드에서 배선한다. 크로스헤어 경로도 onClick.Invoke를
        // 부르므로 두 입력이 같은 리스너를 지난다.
        if (playButton != null) playButton.onClick.AddListener(ToggleOptions);
        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGame);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
        if (confirmAcceptButton != null) confirmAcceptButton.onClick.AddListener(OnConfirmAccept);
        if (confirmCancelButton != null) confirmCancelButton.onClick.AddListener(OnConfirmCancel);

        if (optionsRoot != null) optionsRoot.SetActive(false);
        if (confirmRoot != null) confirmRoot.SetActive(false);
    }

    void Update()
    {
        // StartMenuController는 다른 오브젝트라 활성 시점이 보장되지 않으므로 폴링으로 잡는다
        // (FixedUIStartMenuAdapter 선례).
        if (_startMenu == null) _startMenu = FindFirstObjectByType<StartMenuController>();

        // 프롤로그·컷씬·전환이 시작되면 열려 있던 확인 패널도 접는다 — 전환 중의 확인 클릭은
        // 이중 진입이 된다.
        var flowHidden = ShouldHideForFlow();
        if (flowHidden && IsConfirmOpen) confirmRoot.SetActive(false);

        // 확인 패널이 모달로 떠 있는 동안에는 플레이 메뉴도 물린다 — 확인 캔버스보다 플레이어에
        // 가까운 플레이 버튼이 크로스헤어에 먼저 잡히는 것을 막는다.
        var hidden = flowHidden || IsConfirmOpen;
        if (menuRoot != null && menuRoot.activeSelf == hidden) menuRoot.SetActive(!hidden);

        if (continueButton != null)
            continueButton.interactable = _startMenu != null && _startMenu.CanContinue;
    }

    bool IsConfirmOpen => confirmRoot != null && confirmRoot.activeSelf;

    /// <summary>
    /// 모니터 프롤로그·컷씬·씬 전환 중에는 메뉴를 숨긴다. 영상 위에 버튼이 떠 있으면 시청을
    /// 가리고, 전환 중 클릭은 이중 진입이 된다.
    /// </summary>
    bool ShouldHideForFlow() =>
        (prologue != null && prologue.IsRunning) ||
        (CutsceneDirector.HasInstance && CutsceneDirector.Instance.IsPlaying) ||
        SceneController.IsTransitioning;

    void ToggleOptions()
    {
        if (optionsRoot != null) optionsRoot.SetActive(!optionsRoot.activeSelf);
    }

    void OnNewGame()
    {
        if (_startMenu == null) return;
        if (optionsRoot != null) optionsRoot.SetActive(false);

        // 저장이 있으면 모니터 화면 위 월드 확인 패널부터 연다 (F-001 1.4의 덮어쓰기 확인).
        // 패널이 없으면 StartMenuController가 종전대로 UXML 확인 창을 띄운다(폴백).
        if (confirmRoot != null && confirmAcceptButton != null && confirmCancelButton != null &&
            _startMenu.CanContinue)
        {
            confirmRoot.SetActive(true);
            return;
        }

        _startMenu.RequestStartNewGame();
    }

    void OnConfirmAccept()
    {
        if (confirmRoot != null) confirmRoot.SetActive(false);
        if (_startMenu == null) return;

        // 확인은 이미 받았으므로 UXML 확인 창을 거치지 않는 진입점을 쓴다. 실제 진입은 종전대로
        // NewGameHandler(모니터 프롤로그)로 위임된다.
        _startMenu.RequestStartNewGameConfirmed();
    }

    void OnConfirmCancel()
    {
        if (confirmRoot != null) confirmRoot.SetActive(false);
    }

    void OnContinue()
    {
        if (_startMenu == null) return;
        if (optionsRoot != null) optionsRoot.SetActive(false);

        // 이어하기는 영상 없이 기존 경로 그대로 저장된 공정으로 진입한다.
        _startMenu.RequestContinue();
    }

    void OnDestroy()
    {
        if (playButton != null) playButton.onClick.RemoveListener(ToggleOptions);
        if (newGameButton != null) newGameButton.onClick.RemoveListener(OnNewGame);
        if (continueButton != null) continueButton.onClick.RemoveListener(OnContinue);
        if (confirmAcceptButton != null) confirmAcceptButton.onClick.RemoveListener(OnConfirmAccept);
        if (confirmCancelButton != null) confirmCancelButton.onClick.RemoveListener(OnConfirmCancel);
    }
}
