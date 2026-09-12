# 이음 (IUM) — 숭례문 목공 VR

Meta Quest 3에서 전통 목공 도구로 부재를 가공하고 숭례문 공포를 조립하는 VR 체험.

## 개요

- 목적: 전통 목공 공정(먹줄·톱질·대패·자귀·끌)을 VR로 체험하고, 가공한 부재로 숭례문 공포를 조립
- 대회·수업명: (작성 예정)
- 기간: (작성 예정)

## 시스템 구성

```mermaid
flowchart TB
  START["StartScene<br/>시작 화면"] --> CUT["Cutscene_Prologue"]
  CUT --> PLAY["Play.unity<br/>본편"]
  PLAY --> TUT["TutorialScene"]
  PLAY --> WOOD["목재 가공<br/>GongpoAssemble/Woodworking"]
  WOOD --> SNAP["SnapSystem<br/>부재 조립"]
  SNAP --> SUNG["Cutscene_SungnyemunBuild"]
  SUNG --> ENDING["Cutscene_Ending"]

  DATA[("Data/Static/*.json<br/>Addressables")] -.->|"flow · process · quest · dialogue · cutscene · app"| PLAY
  AI["AI 대화<br/>Scripts/AI"] -.-> PLAY
  AUDIO["오디오<br/>AudioManager + Core.Audio 버스"] -.-> PLAY
```

데이터는 Addressables 추상 주소(`ium/data/static/*`)로 로드합니다. 흐름·공정·퀘스트·대사·컷씬 정의가 모두 JSON입니다.

## 기술 스택

**하드웨어**

- Meta Quest 3

**소프트웨어**

- Unity 6000.3.9f1, URP 17.3
- XR Interaction Toolkit 3.4.1, XR Hands 1.7.3, OpenXR 1.16.1, Meta OpenXR 2.5.0
- Addressables 2.8.1
- UI Toolkit (uxml/uss/tss)
- DOTween Pro (외부 에셋)
- 로컬 STT: `com.eitan.sherpa-onnx-unity`

**서버·인프라**

- 없음. 단말 내 실행. AI 대화는 외부 API 키를 `StreamingAssets`의 설정 파일로 읽습니다(저장소 제외).

## 폴더 구조

```text
xr-contest-ieum/
├─ IUM/                    Unity 프로젝트
│  └─ Assets/
│     ├─ Art/              Characters(Ieumi, Nojang) / Environment(Sungnyemun, Workshop) / Props / Materials / Textures
│     ├─ Data/             Addressables 등록 JSON (Static/, manifest.json)
│     ├─ Developers/RYU/   오디오 · 컷씬 · 퀘스트 · 시작 화면 · 공정 통합 테스트
│     ├─ Prefabs/
│     ├─ Scenes/           StartScene, Play, GongpoScene, wood
│     ├─ Scripts/          AI, Cores, Cutscene, Dialogue, Interaction, Player, Process, Quest, UI, Editor …
│     ├─ Settings/         URP 파이프라인 · 볼륨 프로필 · Input Actions
│     ├─ UI/               uxml, uss, PanelSettings
│     ├─ StreamingAssets/  런타임 설정 · 영상
│     ├─ ThirdParty/       외부 에셋 (저장소 제외)
│     └─ (패키지 관리) AddressableAssetsData, CompositionLayers, Plugins, Resources, Samples, TextMesh Pro, XR, XRI
└─ docs/
   ├─ cleanup/             정리 작업 기록 (감사 · 삭제 목록 · 이동표 · 검증 결과)
   ├─ game-design/         기획 문서, 이슈 로그
   └─ gaze-system/         시선 시스템 매뉴얼
```

`Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`은 Unity·IDE가 자동으로 만드는 파일이라 저장소에 없습니다.

## 실행 방법

필요 버전: **Unity 6000.3.9f1** (`IUM/ProjectSettings/ProjectVersion.txt`와 일치해야 함)

1. **외부 에셋을 먼저 넣어야 합니다.** `Assets/ThirdParty/`, `Assets/Plugins/Demigiant/`, `Assets/Resources/DOTweenSettings.asset`이 저장소에 없습니다. 이 중 DOTween Pro와 QuickOutline은 **없으면 컴파일 자체가 실패합니다.** 아래 "외부 에셋" 절을 먼저 보십시오.
2. `IUM/` 폴더를 Unity Hub에서 엽니다. `Library/`가 없으므로 첫 실행 때 전체 임포트가 오래 걸립니다.
3. AI 대화 기능을 쓰려면 설정 파일을 만듭니다.

```bash
cd IUM/Assets/StreamingAssets
cp ai_secrets.sample.json ai_secrets.json
# ai_secrets.json 에 발급받은 키를 넣습니다. 이 파일은 .gitignore 대상입니다.
```

4. 빌드 타깃을 Android로 바꾸고 Quest 3를 연결해 빌드합니다.

## 외부 에셋

재배포 문제와 용량 때문에 저장소에서 제외했습니다. Release에 배포한 **`ImportFile` zip을 받아 넣어야 프로젝트가 열립니다.**

### 받는 곳

- Release

### 넣는 방법

받은 `ImportFile/Assets` 를 **`IUM/` 안에 그대로 덮어씁니다.** 폴더 구조가 같아 병합만 하면 되고, `.meta` 가 함께 들어 있어 GUID와 씬 참조가 그대로 유지됩니다. **Unity 를 닫은 상태에서 넣으십시오.**

```
ImportFile/Assets/Plugins/Demigiant/                 →  IUM/Assets/Plugins/Demigiant/
ImportFile/Assets/Resources/DOTweenSettings.asset    →  IUM/Assets/Resources/DOTweenSettings.asset
ImportFile/Assets/ThirdParty/                        →  IUM/Assets/ThirdParty/
```

Asset Store에서 직접 받아도 되지만, 그때는 아래 경로에 맞춰 넣어야 합니다(원본 그대로 임포트하면 경로가 달라집니다).

### 목록

**필수 — 없으면 컴파일이 실패해 에디터가 Play를 막습니다.**

| 이름 | 넣을 경로 | 출처 | 없으면 |
|---|---|---|---|
| DOTween Pro | `Assets/Plugins/Demigiant/`, `Assets/Resources/DOTweenSettings.asset` | Unity Asset Store (**유료**) | `using DG.Tweening` 을 쓰는 `ScreenFader`, `PauseService`, `SceneController`, `CutsceneStage` 컴파일 실패 |
| QuickOutline | `Assets/ThirdParty/QuickOutline/` | Unity Asset Store (무료) | `Outline` 타입을 쓰는 `TutorialOutlineGuide`, `ProcessGuideService`, `TutorialRealSwapTool` 컴파일 실패 |

**선택 — 없어도 컴파일은 되지만 해당 씬의 비주얼이 빠집니다.**

| 이름 | 넣을 경로 | 출처 | 사용 씬 |
|---|---|---|---|
| SkySeries Freebie | `Assets/ThirdParty/SkySeries Freebie/` | Unity Asset Store (무료) | `GongpoScene.unity` — `6SidedMegaSun.mat` |
| ADG Ground Textures vol.1 | `Assets/ThirdParty/ADG_Textures/` | Unity Asset Store | `Play.unity` — `ground_vol1/ground1/ground1.mat` |
| Unity Particle Pack | `Assets/ThirdParty/UnityTechnologies/ParticlePack/` | Unity Asset Store (무료, Unity Technologies) | `Play.unity`, `GongpoScene.unity` — `WoodImpacts.prefab` |
| VR Template Assets | `Assets/ThirdParty/VRTemplateAssets/` | Unity VR 프로젝트 템플릿 | `Play.unity`, `GongpoScene.unity`, `MainPlayScene.unity` — `Pointer Outline.mat` |

`ParticlePack`에 들어 있던 `URP.asset`, `URP_Renderer.asset`은 `ProjectSettings/GraphicsSettings.asset`이 GUID로 참조하므로 `Assets/Settings/`로 옮겨 저장소에 포함했습니다.

## 팀 구성 및 내 역할

(작성 예정)

## 결과

(작성 예정)

## 알려진 제한

**누락 파일**

- `StreamingAssets/Prologue_1.mp4`, `Prologue_2.mp4`가 없습니다. `Data/Static/cutscene.json`이 이 두 파일을 참조하므로 **프롤로그 영상 컷씬이 재생되지 않습니다.** 인게임 연출본(`prologue_scene`)으로 되돌리려면 `cutscene.json`에서 두 항목의 `id`를 맞바꿉니다.
- 시연 영상 `devcontest.mov`(75.6 MB)는 저장소에서 제외했습니다.

**실패 테스트**

- EditMode 테스트 10개 중 `CoreLoopContractTests.TutorialOutlineGuide_CoversEveryObjectInteractionStep` 1개가 실패합니다(`Sequence contains no matching element`). 정리 작업 이전부터 실패하던 항목입니다.
- `Developers/RYU/Scenes/Main/MainPlayScene.unity`의 `planer` 오브젝트에 Missing Script 1건이 있습니다. 역시 정리 이전부터 있던 문제입니다.

**구조**

- `Developers/RYU/`는 해체하지 않았습니다. 코드의 문자열 경로 30여 줄과 빌드 씬 7개가 그 아래에 있습니다. 해체 계획은 `docs/cleanup/restructure_plan.md` 2차 항목 참조.
- `Art/Characters/Ieumi/iumi_legacy.fbx`는 같은 폴더의 `iumi.fbx`와 바이트 동일한 중복입니다. `iumi프리펩.prefab`이 두 사본을 모두 참조해 삭제하지 않았습니다.
- `Scenes/GongpoScene.unity`는 에디터 도구 3개가 참조하지만 Build Settings에 등록되어 있지 않아 런타임에 이름으로 로드할 수 없습니다.
- 두 개의 `AudioManager`(전역 / `Core.Audio`)가 공존합니다. 마이그레이션 진행 중입니다(ISSUE-002, ISSUE-015).
- 텍스처 22장이 4096 원본이지만 임포트 Max Size가 2048이라 빌드에는 2048로 들어갑니다.
