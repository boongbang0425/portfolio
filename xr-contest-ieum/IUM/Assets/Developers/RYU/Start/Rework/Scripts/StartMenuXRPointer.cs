using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// 시작 화면 월드 uGUI 버튼을 VR 컨트롤러 레이로 누르게 하는 입력 어댑터.
///
/// 왜 필요한가 — StartScene의 클릭 경로는 두 개뿐이었다. 마우스(<c>GraphicRaycaster</c>)와
/// 크로스헤어(<see cref="StartScenePlayerInteraction"/>). 크로스헤어는 카메라 화면 중앙에서만
/// 레이를 쏘므로 VR에서는 "보고 있는 것"만 눌린다. 컨트롤러를 겨눠도 시선 밖이면 아무 일도
/// 일어나지 않는다. 씬에 XRI 리그(<c>XROrigin</c>·<c>XRRayInteractor</c>)가 없고 EventSystem의
/// <c>InputSystemUIInputModule</c>에 tracked device를 먹여 주는 것도 없으므로, XRI 경로를 새로
/// 세우는 대신 프로젝트가 이미 쓰는 <see cref="WorldMenuPointer"/> 방식을 그대로 따른다.
///
/// 격리 원칙 — 게임 로직(<see cref="StartPlayMenu"/>)에는 손대지 않는다. 이 컴포넌트가 유일한
/// XR 의존 지점이고, <see cref="XRSettings.isDeviceActive"/>가 false면 <see cref="LateUpdate"/>
/// 첫 줄에서 통째로 빠져나간다. 즉 데스크톱(마우스·크로스헤어) 경로는 코드 경로 자체가 실행되지
/// 않으므로 회귀가 발생할 수 없다.
///
/// 레이 정의 — <see cref="GrabHandModule.GetRayRotation"/>을 그대로 쓴다. 그 모듈이 손 앵커의
/// LineRenderer(빨간 레이저)를 같은 식으로 그리기 때문에, 보이는 레이저와 실제 판정이 어긋나지
/// 않는다. Quest 컨트롤러의 <c>deviceRotation</c>은 그립 축이라 앞을 그대로 쓰면 40도 아래를
/// 가리키는데, 그 보정이 이미 그 함수 안에 있다. (<see cref="WorldMenuPointer"/>는 보정 없이
/// <c>anchor.forward</c>를 쓴다 — 그쪽은 별도 확인 항목이다.)
///
/// 판정 방식 — 월드 캔버스 평면과 컨트롤러 레이의 교점을 구하고, 그 월드 좌표를 캔버스의
/// eventCamera 화면 좌표로 되돌려 <c>GraphicRaycaster.Raycast</c>에 넣는다. 월드→화면→레이
/// 왕복이 같은 카메라 행렬을 쓰므로 스테레오 렌더 여부와 무관하게 자기 일관적이고,
/// raycastTarget·CanvasGroup·마스크·정렬 같은 uGUI 규칙을 하나도 다시 구현하지 않아도 된다.
///
/// 실기기 미검증 — 개발 환경에 HMD가 없다. 첫 XR 프레임에 진단 로그를 한 번 남긴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class StartMenuXRPointer : MonoBehaviour
{
    [Tooltip("컨트롤러 레이가 UI를 잡는 최대 거리(m).")]
    [SerializeField, Min(0.1f)] float maxDistance = 10f;

    [Tooltip("UI를 겨누는 동안 손 레이저를 이 색으로 바꾸고 끝점을 UI 접점에 맞춘다.")]
    [SerializeField] Color uiAimColor = new(0.98f, 0.86f, 0.55f, 0.9f);

    [Tooltip("겨누던 버튼이 바뀔 때의 햅틱. 0이면 끈다.")]
    [SerializeField, Range(0f, 1f)] float hoverHaptic = 0.15f;

    [Tooltip("눌렀을 때의 햅틱. 0이면 끈다.")]
    [SerializeField, Range(0f, 1f)] float clickHaptic = 0.35f;

    [Tooltip("월드 캔버스 목록을 다시 훑는 주기(초). 런타임에 생기는 캔버스를 잡기 위한 것이다.")]
    [SerializeField, Min(0.1f)] float rescanInterval = 1f;

    readonly List<GraphicRaycaster> _raycasters = new();
    readonly List<RaycastResult> _results = new();
    readonly HandState[] _hands =
    {
        new(XRHandSide.Left),
        new(XRHandSide.Right)
    };

    PointerEventData _pointer;
    Player _player;
    StartScenePlayerInteraction _gazePath;
    bool _gazeSuppressed;
    bool _logged;
    float _nextRescan;
    float _nextPlayerLookup;
    int _clickedFrame = -1;

    sealed class HandState
    {
        public readonly XRHandSide Hand;
        public Button Hovered;
        public bool WasPressed;

        public HandState(XRHandSide hand) => Hand = hand;
    }

    /// <summary>HMD가 세션을 돌리고 있지 않으면 이 어댑터는 존재하지 않는 것처럼 동작한다.</summary>
    public bool IsActivePath => XRSettings.isDeviceActive;

    void Awake()
    {
        _gazePath = GetComponent<StartScenePlayerInteraction>();

        // 여기서 미리 끊는다. 이 컴포넌트는 런타임에 나중에 붙으므로 LateUpdate 순서가 뒤라,
        // 첫 프레임에 크로스헤어 경로가 한 번 먼저 도는 창이 생긴다.
        if (IsActivePath) SuppressGazePath();
    }

    void OnDisable()
    {
        ClearHover();
        RestoreGazePath();
    }

    // Update가 아니라 LateUpdate다. 손 앵커 포즈와 손 레이저는 Player의 모듈이 Update에서
    // 갱신하므로, 그 뒤에서 읽어야 이번 프레임의 실제 레이와 판정이 일치한다.
    void LateUpdate()
    {
        if (!IsActivePath)
        {
            // 실행 중에 HMD가 빠지면 크로스헤어 경로를 돌려준다.
            RestoreGazePath();
            return;
        }

        if (!ResolvePlayer()) return;

        SuppressGazePath();
        RefreshRaycasters();
        LogOnce();

        for (var i = 0; i < _hands.Length; i++)
            UpdateHand(_hands[i]);
    }

    void UpdateHand(HandState state)
    {
        var pressed = ReadPressed(state.Hand);
        var pressedThisFrame = pressed && !state.WasPressed;
        state.WasPressed = pressed;

        if (!TryBuildRay(state.Hand, out var ray))
        {
            SetHovered(state, null);
            return;
        }

        var hit = TryHitUI(ray, out var button, out var worldPoint);
        SetHovered(state, hit ? button : null);
        if (hit) DrawUiAim(state.Hand, worldPoint);

        if (!pressedThisFrame) return;

        // 크로스헤어 경로는 PlayerCommands를 지나며 Interact 잠금을 받았다. UserInput을 직접
        // 읽는 이 경로도 같은 규칙을 지켜야 프롤로그·컷씬 중에 클릭이 새지 않는다.
        // (WorldMenuPointer가 이 잠금을 무시하는 것은 그 메뉴 자신이 Menu 잠금을 쥐기 때문이고,
        //  시작 화면 메뉴는 어떤 잠금도 쥐지 않으므로 여기서는 존중하는 쪽이 맞다.)
        if (InputLockService.IsLocked(InputLockFlags.Interact)) return;

        // 같은 프레임에 양손 트리거가 동시에 내려가도 한 번만 실행한다.
        if (_clickedFrame == Time.frameCount) return;

        if (!hit)
        {
            // 컨트롤러가 UI를 겨누지 않았으면 종전 크로스헤어 판정으로 넘긴다.
            if (TryGazeFallback()) _clickedFrame = Time.frameCount;
            return;
        }

        _clickedFrame = Time.frameCount;
        Pulse(state.Hand, clickHaptic, 0.05f);

        // 크로스헤어 경로와 같은 방식으로 실행한다. onClick만 부르므로 씬에 영구 이벤트가
        // 없는 StartPlayMenu의 코드 배선이 그대로 동작한다.
        button.Select();
        button.onClick.Invoke();
    }

    /// <summary>
    /// 컨트롤러가 UI를 겨누지 않았을 때 쓰던 크로스헤어 실행. 종전 동작을 그대로 남겨,
    /// 실기기에서 컨트롤러 레이가 어긋나더라도 최소한 지금과 같은 조작이 가능하게 한다.
    /// </summary>
    bool TryGazeFallback() => _gazePath != null && _gazePath.TryActivateFocusedButton();

    bool TryBuildRay(XRHandSide hand, out Ray ray)
    {
        ray = default;

        var anchor = _player.GetHandAnchor(hand);
        if (anchor == null) return false;

        // 무언가를 쥔 손의 트리거는 그 물체의 Activate다. UI와 겹치게 두지 않는다.
        var module = _player.GetHand(hand);
        if (module == null || module.Held != null) return false;

        // 추적이 끊긴 손의 앵커는 마지막 포즈에 멈춰 있다 — 그 방향으로 판정하면 안 된다.
        if (_player.Input == null || !_player.Input.IsHandTracked(hand)) return false;

        ray = new Ray(anchor.position, module.GetRayRotation() * Vector3.forward);
        return true;
    }

    /// <summary>
    /// 레이가 가장 먼저 닿는 월드 캔버스의 uGUI 버튼. 캔버스 평면 교점을 그 캔버스의
    /// eventCamera 화면 좌표로 바꿔 <c>GraphicRaycaster</c>에 그대로 넘긴다.
    /// </summary>
    bool TryHitUI(Ray ray, out Button button, out Vector3 worldPoint)
    {
        button = null;
        worldPoint = default;

        var pointer = ResolvePointerData();
        if (pointer == null) return false;

        var best = float.MaxValue;

        for (var i = 0; i < _raycasters.Count; i++)
        {
            var raycaster = _raycasters[i];
            if (raycaster == null || !raycaster.isActiveAndEnabled) continue;

            var canvas = raycaster.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) continue;
            if (canvas.transform is not RectTransform rect) continue;

            var camera = raycaster.eventCamera;
            if (camera == null) continue;

            var plane = new Plane(rect.forward, rect.position);
            if (!plane.Raycast(ray, out var enter)) continue;
            if (enter <= 0f || enter > maxDistance || enter >= best) continue;

            var world = ray.GetPoint(enter);

            // 카메라 뒤로 넘어간 점은 WorldToScreenPoint가 뒤집힌 좌표를 준다. 이 경우만
            // 판정을 포기한다 — 화면 밖(앞쪽)은 ScreenPointToRay가 절두체를 연장하므로 정상이다.
            var screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f) continue;

            pointer.position = screen;

            _results.Clear();
            raycaster.Raycast(pointer, _results);

            for (var r = 0; r < _results.Count; r++)
            {
                var candidate = _results[r].gameObject.GetComponentInParent<Button>();
                if (candidate == null || !candidate.IsActive() || !candidate.IsInteractable()) continue;

                button = candidate;
                worldPoint = world;
                best = enter;
                break;
            }
        }

        return button != null;
    }

    void SetHovered(HandState state, Button target)
    {
        if (state.Hovered == target) return;

        var pointer = ResolvePointerData();

        if (state.Hovered != null && pointer != null)
            ExecuteEvents.Execute(state.Hovered.gameObject, pointer, ExecuteEvents.pointerExitHandler);

        state.Hovered = target;

        if (target == null) return;

        if (pointer != null)
            ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerEnterHandler);

        Pulse(state.Hand, hoverHaptic, 0.02f);
    }

    void ClearHover()
    {
        for (var i = 0; i < _hands.Length; i++)
        {
            var state = _hands[i];
            if (state.Hovered == null) continue;

            var pointer = ResolvePointerData();
            if (pointer != null)
                ExecuteEvents.Execute(state.Hovered.gameObject, pointer, ExecuteEvents.pointerExitHandler);

            state.Hovered = null;
        }
    }

    /// <summary>
    /// 손 레이저의 끝점을 UI 접점으로 당기고 색을 바꾼다. LineRenderer 자체는
    /// <see cref="GrabHandModule"/> 소유이고 Update에서 매 프레임 다시 그리므로, 여기서
    /// 덮어쓰는 값은 이번 프레임에만 남는다 — 상태를 망가뜨리지 않는다.
    /// </summary>
    void DrawUiAim(XRHandSide hand, Vector3 worldPoint)
    {
        var anchor = _player.GetHandAnchor(hand);
        if (anchor == null) return;

        var line = anchor.GetComponent<LineRenderer>();
        if (line == null || !line.enabled || !line.useWorldSpace || line.positionCount < 2) return;

        line.SetPosition(1, worldPoint);
        line.startColor = uiAimColor;
        line.endColor = uiAimColor;
    }

    bool ReadPressed(XRHandSide hand) =>
        UserInput.HasInstance && UserInput.Instance.GetXRButton(hand, XRInputButton.Select);

    void Pulse(XRHandSide hand, float amplitude, float seconds)
    {
        if (amplitude <= 0f || seconds <= 0f || !UserInput.HasInstance) return;
        UserInput.Instance.SendHapticImpulse(hand, amplitude, seconds);
    }

    /// <summary>
    /// 크로스헤어 경로를 끈다. 켜 두면 트리거 한 번에 "보던 버튼"과 "겨눈 버튼"이 둘 다 눌린다.
    /// 컴포넌트를 끄기만 하므로 Awake에서 이미 끝난 카메라 인수인계는 그대로 남고,
    /// <see cref="TryGazeFallback"/>으로 그 기능은 계속 부를 수 있다.
    /// </summary>
    void SuppressGazePath()
    {
        if (_gazeSuppressed || _gazePath == null) return;
        _gazePath.enabled = false;
        _gazeSuppressed = true;
    }

    void RestoreGazePath()
    {
        if (!_gazeSuppressed || _gazePath == null) return;
        _gazePath.enabled = true;
        _gazeSuppressed = false;
    }

    bool ResolvePlayer()
    {
        if (_player != null) return true;
        if (Time.unscaledTime < _nextPlayerLookup) return false;

        _nextPlayerLookup = Time.unscaledTime + 0.5f;
        _player = FindAnyObjectByType<Player>();
        return _player != null;
    }

    PointerEventData ResolvePointerData()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return null;

        _pointer ??= new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };

        return _pointer;
    }

    /// <summary>
    /// 비활성 캔버스까지 포함해 훑고 매 프레임 <c>isActiveAndEnabled</c>로 거른다.
    /// ConfirmCanvas처럼 필요할 때만 켜지는 캔버스를 놓치지 않기 위한 것이다.
    /// </summary>
    void RefreshRaycasters()
    {
        if (Time.unscaledTime < _nextRescan && _raycasters.Count > 0) return;
        _nextRescan = Time.unscaledTime + rescanInterval;

        _raycasters.Clear();
        var found = FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < found.Length; i++)
        {
            var canvas = found[i].GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                _raycasters.Add(found[i]);
        }
    }

    // 실기기 검증 항목이라 첫 XR 프레임의 상태를 한 번 남긴다. 대상 캔버스가 0개면 이 어댑터는
    // 아무것도 하지 못하므로 그 경우만 경고로 올린다.
    void LogOnce()
    {
        if (_logged) return;
        _logged = true;

        var message = $"[StartMenuXRPointer] XR 활성. 월드 캔버스 {_raycasters.Count}개, " +
                      $"크로스헤어 경로 {(_gazeSuppressed ? "중지" : "유지")}, " +
                      $"레이 기준 GrabHandModule.GetRayRotation. 실기기 확인 필요.";

        if (_raycasters.Count == 0) Debug.LogWarning(message, this);
        else Debug.Log(message, this);
    }
}
