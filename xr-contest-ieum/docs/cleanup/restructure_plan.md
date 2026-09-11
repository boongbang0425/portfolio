# Assets 재배치 계획 (실행 금지 — 승인용 초안)

작성: 2026-09-11 · 기준 상태: 1·2차 삭제 완료 (`Assets/` 750.7 MB / 2,829 파일)
근거: `docs/cleanup_audit.md`, `docs/folder_delete_list.md`, `docs/cleanup_log.md`

**이 문서는 계획서입니다. 파일을 하나도 옮기지 않았습니다.**

---

## 0. 목표 구조

```
Assets/
├─ @Scripts/     런타임·에디터 C#
├─ @Scenes/      씬 (+ @Scenes/Dev)
├─ @Prefabs/     프리팹
├─ @Art/         Characters / Environment / Props / Materials / Textures / Fonts
├─ @Audio/       Resources/Sfx 포함
├─ @UI/          uxml / uss / tss / PanelSettings
├─ @Data/        json 데이터 (Addressables 등록분)
├─ @Tests/       테스트 asmdef
├─ ThirdParty/   외부 에셋
└─ (이동 금지) StreamingAssets, Plugins, Samples, XR, XRI, Settings,
               AddressableAssetsData, Resources, CompositionLayers
```

### 이동 금지 폴더와 사유

| 폴더 | 사유 |
|---|---|
| `StreamingAssets` | `Assets` 바로 아래 고정. 안의 파일명을 코드가 문자열로 읽음 |
| `Samples` | Package Manager가 `Samples/<패키지>/<버전>/` 경로로 추적. asmdef 4개 포함 |
| `Plugins` | 네이티브·에디터 DLL 로딩 규칙 |
| `Resources` | 이름 고정. 하위 경로가 `Resources.Load` 문자열 |
| `Settings`, `AddressableAssetsData` | `ProjectSettings/*`가 GUID로 참조 (`cleanup_audit.md` 1-14절) |
| `XR`, `XRI`, `CompositionLayers` | 패키지 자동 생성·재생성 |

> `Assets/Resources/`에는 `DOTweenSettings.asset` 하나뿐이고, `@Developers/RYU/Audio/Resources/Sfx/`는
> **상위만 바뀌고 `Resources/Sfx/` 구조가 유지되면** `Resources.Load("Sfx/…")`가 그대로 동작합니다.

---

## 1. 현재 문자열 경로 분포 (이동 난이도의 근거)

`Assets/` 전체 `*.cs`,`*.json`에서 `"Assets/…"` 리터럴 **93건**입니다.

| 접두사 | 건수 | 이번 계획의 처분 |
|---|---:|---|
| `Assets/@Developers/…` | **30** | 해체 → **2차로 미룸** |
| `Assets/@UI/…` | 21 | 유지 (수정 0) |
| `Assets/@Scenes/…` | 14 | 유지 (수정 0) |
| `Assets/@Scripts/…` | 7 | 유지 (수정 0) |
| `Assets/@GameAssets/…` | 6 | 1차에서 일부 이동 |
| `Assets/@AddressableAssets/…` | 6 | `@Data`로 이동 |
| `Assets/QuickOutline/…` | 3 | `ThirdParty/`로 이동 |
| `Assets/TextMesh Pro/…` | 2 | 유지 |
| `Assets/Prefabs/`, `Assets/warehouseFin/`, `Assets/tools/` | 각 1 | 1차에서 이동 |

**핵심**: `@Developers/RYU`가 30건으로 최다입니다. `cleanup_audit.md` 1-1절의 지적대로,
RYU 해체가 이 재배치에서 가장 비싼 작업이므로 **2차로 분리**했습니다.

---

## 2. 1차 — 문자열 경로 수정이 거의 없는 이동

수정할 리터럴 **총 12줄**, 빌드 씬 영향 **없음**.

### 2-1. 이동표

| # | 원래 경로 | 새 경로 | 수정할 문자열 | 빌드 씬 영향 |
|---|---|---|---|---|
| 1 | `Assets/Scripts/` (4 cs) | `Assets/@Scripts/Board/` | 없음 | 없음 |
| 2 | `Assets/Editor/MeasureBounds.cs` | `Assets/@Scripts/Editor/` | 없음 | 없음 |
| 3 | `Assets/Editor/TriplanarMaterialGenerator.cs` | `Assets/@Scripts/Editor/` | `Editor/TriplanarMaterialGenerator.cs:185` (아래 6번과 함께) | 없음 |
| 4 | `Assets/Editor/UnusedAssetReport.cs` | `Assets/@Scripts/Editor/` | 없음 | 없음 |
| 5 | `Assets/GazeSystem/*.cs` (13) | `Assets/@Scripts/Gaze/` | 없음 | 없음 |
| 6 | `Assets/GazeSystem/*.md` (2) | `docs/` (Assets 밖) | 없음 | 없음 |
| 7 | `Assets/SimpleFreeCamera.cs` | `Assets/@Scripts/Dev/` | 없음 | 없음 |
| 8 | `Assets/@GameAssets/Sungnyemun/숭례문조립/BuildingStageDirector.cs` | `Assets/@Scripts/Sungnyemun/` | 없음 | 없음 |
| 9 | `Assets/Prefabs/` (7) | `Assets/@Prefabs/` | **`@Scripts/Editor/PlayWorkshopBuilder.cs:633`** | 없음 |
| 10 | `Assets/@GameAssets/Prefabs/` (3) | `Assets/@Prefabs/` | **`BoardHudBuilder.cs:22`, `PauseHudBuilder.cs:18,19`** | 없음 |
| 11 | `Assets/wood.unity` | `Assets/@Scenes/wood.unity` | 없음 (참조 0, 빌드 씬 아님) | 없음 |
| 12 | `Assets/iumi/` | `Assets/@Art/Characters/Ieumi/` | 없음 | 없음 |
| 13 | `Assets/legenooldman/` | `Assets/@Art/Characters/Nojang/` | 없음 | 없음 |
| 14 | `Assets/warehouseFin/` | `Assets/@Art/Environment/Workshop/` | **`Editor/TriplanarMaterialGenerator.cs:185`** | 없음 |
| 15 | `Assets/tools/` | `Assets/@Art/Props/Tools/` | **`@Scripts/Editor/PlayWorkshopBuilder.cs:287`** | 없음 |
| 16 | `Assets/@GameAssets/*Part.fbx` (4) | `Assets/@Art/Props/WoodParts/` | 없음 | 없음 |
| 17 | `Assets/@GameAssets/Sungnyemun/` | `Assets/@Art/Environment/Sungnyemun/` | **`SungnyemunImportBuilder.cs:28,29,30`** | 없음 |
| 18 | `Assets/@GameAssets/Materials/`, `Textures/` | `Assets/@Art/Materials/`, `@Art/Textures/` | 없음 | 없음 |
| 19 | `Assets/Textures/` (5 mat) | `Assets/@Art/Materials/` | 없음 | 없음 |
| 20 | `Assets/Original Wood Textures/` | `Assets/@Art/Textures/Wood/` | 없음 | 없음 |
| 21 | `Assets/options.fbx`, `Assets/hammer.fbx` | `Assets/@Art/Props/` | 없음 | 없음 |
| 22 | `Assets/@AddressableAssets/` | `Assets/@Data/` | **6줄** (아래 2-2) | 없음 |
| 23 | `Assets/QuickOutline/` | `Assets/ThirdParty/QuickOutline/` | **`CoreLoopContractTests.cs:430,432,433`** | 없음 |
| 24 | `Assets/ADG_Textures/`, `SkySeries Freebie/`, `UnityTechnologies/`, `VRTemplateAssets/` | `Assets/ThirdParty/` | 없음 | 없음 |
| 25 | `Assets/@Documents/` (33) | `docs/` (Assets 밖) | 없음 | 없음 |

> 22번: Addressables 주소는 `ium/data/static/*` 형태의 **추상 주소**라 폴더를 옮겨도 그룹 등록이
> GUID로 유지됩니다 (`cleanup_audit.md` 1-7절). 깨지는 건 json을 **파일 경로로 직접 읽는** 6줄뿐입니다.
>
> 23번: `Outline.cs`의 `Resources.Load("Materials/OutlineMask")`는 `QuickOutline/Resources/` 구조가
> 통째로 따라가므로 무해합니다.

### 2-2. 1차에서 수정할 문자열 경로 — 전체 12줄

| 파일:줄 | 현재 값 | 원인 |
|---|---|---|
| `Assets/@Scripts/Editor/PlayWorkshopBuilder.cs:633` | `Assets/Prefabs/Line.prefab` | #9 |
| `Assets/@Scripts/Editor/PlayWorkshopBuilder.cs:287` | `Assets/tools/darkwood.mat` | #15 |
| `Assets/@Scripts/Editor/BoardHudBuilder.cs:22` | `Assets/@GameAssets/Prefabs/BoardRoot.prefab` | #10 |
| `Assets/@Scripts/Editor/PauseHudBuilder.cs:18` | `Assets/@GameAssets/Prefabs/Pause.prefab` | #10 |
| `Assets/@Scripts/Editor/PauseHudBuilder.cs:19` | `Assets/@GameAssets/Prefabs/Option.prefab` | #10 |
| `Assets/Editor/TriplanarMaterialGenerator.cs:185` | `Assets/warehouseFin/Materials` | #3, #14 |
| `Assets/@Scripts/Editor/SungnyemunImportBuilder.cs:28` | `…/Sungnyemun/texture/modedprefab 1.prefab` | #17 |
| `Assets/@Scripts/Editor/SungnyemunImportBuilder.cs:29` | `…/Sungnyemun/texture/SungnyemunDirector.prefab` | #17 |
| `Assets/@Scripts/Editor/SungnyemunImportBuilder.cs:30` | `Assets/@GameAssets/Sungnyemun` (`FindAssets` 루트, 313줄에서 사용) | #17 |
| `…/Tests/Editor/CoreLoopContractTests.cs:430` | `Assets/QuickOutline/Scripts/Outline.cs` | #23 |
| `…/Tests/Editor/CoreLoopContractTests.cs:432` | `Assets/QuickOutline/Resources/Materials/OutlineMask.mat` | #23 |
| `…/Tests/Editor/CoreLoopContractTests.cs:433` | `Assets/QuickOutline/Resources/Materials/OutlineFill.mat` | #23 |

그리고 `@AddressableAssets` → `@Data` 6줄:

| 파일:줄 | 현재 값 |
|---|---|
| `…/Tests/Editor/CoreLoopContractTests.cs:33` | `Assets/@AddressableAssets/Data/Static/flow.json` |
| `…/CoreLoopContractTests.cs:34` | `…/process.json` |
| `…/CoreLoopContractTests.cs:36` | `…/quest.json` |
| `…/CoreLoopContractTests.cs:37` | `…/dialogue.json` |
| `…/CoreLoopContractTests.cs:38` | `…/cutscene.json` |
| `Assets/@Scripts/Quest/Editor/QuestGraphWindow.cs:17` | `Assets/@AddressableAssets/Data/Static/quest.json` |

추가로 `Assets/Editor/UnusedAssetReport.cs`의 `p.Contains("/@AddressableAssets/")` 도 함께 바꿔야 합니다.

### 2-3. 1차의 빌드 씬 영향

**없습니다.** `EditorBuildSettings.asset`에 등록된 9개 씬은 전부 `@Scenes/` 또는 `@Developers/RYU/Scenes/`에 있고,
1차에서는 둘 다 건드리지 않습니다.

---

## 3. 2차 — RYU 해체 (리터럴 30줄 + 빌드 씬 7개)

### 3-1. 이동표

| 원래 경로 | 새 경로 | 수정할 문자열 |
|---|---|---|
| `@Developers/RYU/Scenes/Main/`, `Cutscene/` | `Assets/@Scenes/` | `DevSceneBuilder.cs:28`, `SungnyemunImportBuilder.cs:33` |
| `@Developers/RYU/Scenes/Dev/` | `Assets/@Scenes/Dev/` | `DevSceneBuilder.cs:21`, `CoreLoopContractTests.cs:39`, `TutorialImportBuilder.cs:32` |
| `@Developers/RYU/Scenes/__*Temp.unity` | `Assets/@Scenes/Dev/` | `PlayNpcBuilder.cs:36`, `PlayWorkshopBuilder.cs:50`, `TutorialImportBuilder.cs:33` |
| `@Developers/RYU/Prefabs/`, `UI/` | `Assets/@Prefabs/` | `PlaySystemsBuilder.cs:28`(29·30 파생), `QuestBoardBuilder.cs:22`, `BoardHudBuilder.cs:23`, `PauseHudBuilder.cs:20` |
| `@Developers/RYU/Quest/UI/` | `Assets/@UI/Quest/` | `DevSceneBuilder.cs:35,36`, `QuestBoardBuilder.cs:25,26`, `StartSceneReworkBuilder.cs:36`, `CoreLoopContractTests.cs:434,435,436,446` |
| `@Developers/RYU/Audio/Core/`, `Integration/` | `Assets/@Scripts/Audio/` | 없음 |
| `@Developers/RYU/Audio/Resources/Sfx/` | `Assets/@Audio/Resources/Sfx/` | 없음 — `Resources/Sfx/` 구조 유지되므로 `ClipRoot="Sfx/"` 유효 |
| `@Developers/RYU/ProcessIntegration/*.cs` | `Assets/@Scripts/Process/` | `CoreLoopContractTests.cs:375` |
| `@Developers/RYU/ProcessIntegration/Tests/` (asmdef째) | `Assets/@Tests/` | `CoreLoopContractTests.cs:32` |
| `@Developers/RYU/Models/` | `Assets/@Art/Props/QuestBoard/` | `QuestBoardBuilder.cs:20,21,143,149,152` |
| `@Developers/RYU/Woodworking/` | `Assets/@Art/Materials/Woodworking/` | `PlayWorkshopBuilder.cs:329,332` |
| `@Developers/RYU/Start/FixedUI/Scripts/`, `Start/Rework/Scripts/` | `Assets/@Scripts/Start/` | 없음 |
| `@Developers/RYU/Start/FixedUI/meterials/` (132.8 MB) | `Assets/@Art/Materials/StartRoom/` | 없음 (오타 `meterials` 동시 해소) |
| `@Developers/RYU/Start/FixedUI/Monitor1~3.fbx`, `Lighting/`, `Settings/` | `Assets/@Art/Environment/StartRoom/` | 없음 |

### 3-2. 2차의 빌드 씬 영향 — **7개 / 9개**

`ProjectSettings/EditorBuildSettings.asset`의 `path:` 7줄이 바뀝니다.

| 현재 등록 경로 | 이동 후 |
|---|---|
| `Assets/@Developers/RYU/Scenes/Cutscene/Cutscene_Prologue.unity` | `Assets/@Scenes/Cutscene_Prologue.unity` |
| `…/Scenes/Cutscene/Cutscene_Ending.unity` | `Assets/@Scenes/Cutscene_Ending.unity` |
| `…/Scenes/Dev/FlowTest.unity` | `Assets/@Scenes/Dev/FlowTest.unity` |
| `…/Scenes/Dev/FreePlayTest.unity` | `Assets/@Scenes/Dev/FreePlayTest.unity` |
| `…/Scenes/Dev/TutorialScene.unity` | `Assets/@Scenes/Dev/TutorialScene.unity` |
| `…/Scenes/Main/MainPlayScene.unity` | `Assets/@Scenes/MainPlayScene.unity` |
| `…/Scenes/Cutscene_SungnyemunBuild.unity` | `Assets/@Scenes/Cutscene_SungnyemunBuild.unity` |

`Assets/@Scenes/StartScene.unity`와 `Assets/@Scenes/Play.unity` 2개는 그대로입니다.

> `EditorBuildSettings.asset`은 `path:`와 `guid:`를 함께 들고 있어 **Unity 에디터 Project 창에서 옮기면
> 자동 갱신**됩니다. 탐색기로 옮기면 이 파일을 손으로 고쳐야 합니다.

### 3-3. 2차에서 먼저 결정할 것

| 항목 | 내용 |
|---|---|
| `@Scenes/GongpoScene.unity` | 빌드 씬 미등록인데 에디터 도구 3개(`PlayNpcBuilder.cs:35`, `PlayWorkshopBuilder.cs:49`, `StartScenePlayerSwapTool.cs:32`)가 참조. 살릴지 버릴지 먼저 정해야 이동 대상이 확정됨 |
| AudioManager 이중 운영 | `@Scripts/Cores/AudioManager`(전역)와 `Core.Audio.AudioManager`(RYU) 둘 다 현역. ISSUE-002/015 마이그레이션이 끝나기 전에는 **폴더만 옮기고 코드는 그대로** (`cleanup_audit.md` 1-2절) |
| FixedUI | `StartScene.unity`가 실제로 참조하는 살아 있는 화면입니다 (`folder_delete_list.md`). 삭제가 아니라 이동 대상 |

---

## 4. 조건4로 남은 미사용 스크립트·셰이더

1·2차 삭제에서 `*.cs`/`*.asmdef`/`*.shader`는 조건4로 전부 보호했습니다. 현재 **318개**가 남아 있고,
그중 참조가 확인되지 않는 것이 **60개**입니다.

판정 기준: `.meta` GUID가 어떤 씬·프리팹·에셋·`ProjectSettings`에도 없고, 파일이 선언한
**모든 타입명**이 다른 `.cs`에서 쓰이지 않으며, `[MenuItem]`·`[Test]`·`[InitializeOnLoad]`·
`[RuntimeInitializeOnLoadMethod]`·`[CustomEditor]`·`[CreateAssetMenu]` 같은 **진입점 속성도 없는** 것.

### 4-1. 팀 코드 (7개) — 검토 후 삭제 가능

| 크기 | 경로 | 비고 |
|---:|---|---|
| 15.8 KB | `Assets/GazeSystem/PlayerSceneInitializer.cs` | |
| 3.5 KB | `Assets/GazeSystem/CameraHorizontalAligner.cs` | |
| 3.4 KB | `Assets/@Scripts/Dialogue/DialogueDebugController.cs` | 디버그용 |
| 2.9 KB | `Assets/@Scripts/Editor/ReleaseBuilder.cs` | **주의** — APK 빌드 스크립트. CI에서 `-executeMethod`로 부르면 참조가 안 잡힘. 지우기 전 확인 필수 |
| 1.7 KB | `Assets/Scripts/MouseRayTester.cs` | |
| 0.7 KB | `Assets/@Developers/RYU/Audio/Core/AudioHandle.cs` | |
| 0.6 KB | `Assets/@Scripts/Cores/DomainSingleton.cs` | |
| 0.2 KB | `…/Tests/Editor/IUM.CoreLoopVerification.Tests.asmdef` | **오탐** — asmdef는 원래 참조되지 않음. 테스트 어셈블리 정의이므로 유지 |

### 4-2. 서드파티 (52개) — 폴더 정책으로 처리

| 폴더 | 개수 | 처리 |
|---|---:|---|
| `VRTemplateAssets/Scripts`, `Shaders`, `Materials` | 15 | 폴더 전체가 사실상 미사용. `Pointer Outline.mat` 하나만 살아 있음 |
| `TextMesh Pro/Shaders` | 11 | TMP 셰이더 변종. 폰트 에셋이 셰이더를 GUID로 물므로 **삭제 금지** 권장 |
| `Plugins/Demigiant` (DOTween 모듈) | 10 | DOTween 자체 모듈. 유료 에셋 원본이므로 건드리지 말 것 |
| `UnityTechnologies/ParticlePack` | 8 | 2차에서 대부분 정리됨. 남은 것은 `WoodImpacts.prefab` 의존성과 무관 |
| `Samples/XR Interaction Toolkit`, `XR Hands` | 8 | Package Manager 추적 대상 — **삭제 금지** |

### 4-3. 참조는 없지만 진입점이 있어 살아 있는 것 (20개)

아래는 **미사용이 아닙니다.** GUID·심볼 참조가 0이라도 Unity가 속성으로 직접 호출합니다.

```
MenuItem (14) : AiLocalSttSetup, BoardHudBuilder, EeumFeedbackBuilder, GoogleOAuthSetup,
                PlaySystemsBuilder, QuestBoardBuilder, StartScenePlayerSwapTool,
                SungnyemunImportBuilder, TutorialRealSwapTool, UserDataTools,
                QuestGraphWindow, MeasureBounds, TriplanarMaterialGenerator, UnusedAssetReport
RuntimeInit (2): WoodworkingSfxBootstrap, StartMenuXRPointerBootstrap
Test (1)       : CoreLoopVerificationLauncher
InitializeOnLoad(2) / CustomEditor(1) : XRI 샘플 검증 2개, RampAssetEditor
```

---

## 5. 권장 순서

1. **`GongpoScene.unity` 존폐 결정** — 이게 정해져야 `iumi`, `Prefabs/Line.prefab`, ADG/SkySeries 잔여분의 행선지가 확정됩니다
2. **`Prologue_1/2.mp4` 복원 또는 `cutscene.json` 롤백** — 안 하면 `CoreLoopContractTests`를 회귀 판정 기준으로 못 씁니다 (`cleanup_audit.md` 6-1절)
3. **1차 이동** — Unity 에디터 Project 창 안에서 폴더 하나씩. 매번 StartScene → Play 통과 확인
4. **1차 문자열 12줄 수정** + `UnusedAssetReport.cs` 1줄
5. **4-1의 팀 코드 7개 검토** (`ReleaseBuilder.cs`는 CI 확인 후)
6. **2차 RYU 해체** — 리터럴 30줄 + `EditorBuildSettings` 7줄. 에디터 안에서 옮기면 후자는 자동
7. **이름 정리** — `재질_2.xxx` 222개, 한글 경로, `modedprefab 1.prefab`. `SungnyemunImportBuilder.cs:28`이 이 이름을 박아 쓰므로 함께 수정

### 이동 중 확인 방법

- Unity **Project 창 안에서** 옮기면 GUID가 유지돼 씬·프리팹 참조는 깨지지 않습니다
- 탐색기로 옮길 때는 Unity를 끄고 `.meta`를 **반드시 함께** 옮깁니다
- 폴더 하나 옮길 때마다: StartScene부터 플레이 1회 + Test Runner의 `CoreLoopContractTests`
- 문자열 경로는 이동 직후 같은 커밋에서 고칩니다 (중간 상태로 두지 않기)
