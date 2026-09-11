# cleanup_plan.md 검증 감사

대상: `IUM/` (Unity 6000.3.9f1, Quest 3) — 실제 파일 대조 결과
작성일: 2026-09-10
에셋은 하나도 수정·이동·삭제하지 않았습니다. 새로 만든 파일은 `IUM/Assets/Editor/UnusedAssetReport.cs`와 이 문서뿐입니다.

## 0. 검증 방법과 한 가지 예외

| 항목 | 방법 |
|---|---|
| 문자열 경로 | `Assets` 아래 `*.cs`,`*.json`에 `"Assets/`, `Resources.Load`, `LoadAssetAtPath`, `OpenScene`, `FindAssets`, `Shader.Find`, `streamingAssetsPath`, `.mp4`, `.webm` 검색 → 225줄 |
| 중복 | `.meta` 제외 2,363개 파일 MD5 → 39개 그룹 |
| 참조 방향 | 각 후보의 `.meta` GUID를 모든 `*.unity/*.prefab/*.mat/*.asset/*.controller/*.anim` 본문에서 역검색 |
| 빌드 씬 | `ProjectSettings/EditorBuildSettings.asset` |
| Addressables | `AddressableAssetsData/AssetGroups/Default Local Group.asset` |
| 미사용 에셋 | `UnusedAssetReport.Run`을 Unity 배치모드로 실행 → `IUM/UnusedAssets.txt` (roots 78 / 전체 2,362 / 사용 708 / **미사용 1,540개 = 1,233.6 MB**) |

**예외 — Unity 버전**: 계획서가 지정한 `6000.3.9f1`은 이 PC에 설치돼 있지 않습니다. 설치된 것은
`2022.3.62f2`, `2022.3.62f3`, `6000.0.60f1`, `6000.3.19f1`, `6000.4.5f1`입니다.
같은 6000.3 계열의 `6000.3.19f1`로 돌렸고, **원본을 건드리지 않으려고 프로젝트를 임시 폴더로 복사한 뒤 사본에서 실행**했습니다
(다른 버전으로 열면 `ProjectVersion.txt`가 바뀌고 임포터가 에셋을 재직렬화할 수 있습니다).
사본이므로 원본 `IUM/`에는 `Library/`도, 변경도 생기지 않았습니다.
정확한 재현이 필요하면 Unity Hub에서 6000.3.9f1을 설치한 뒤 같은 명령을 원본에 돌리면 됩니다.

---

## 1. 계획에서 틀린 점

### 1-1. "문자열 경로 수정량이 가장 적다"는 근거가 뒤집힘 — 가장 큰 오류

계획서 3절은 목표 구조를 이렇게 정당화합니다.

> 새 규칙을 만들기보다 기존 @ 접두사를 그대로 쓰고 흩어진 것만 흡수합니다. 문자열 경로 수정량이 가장 적습니다.

실제로는 **반대**입니다. 코드가 박아 쓰는 `"Assets/..."` 리터럴을 세어 보면:

| 경로 접두사 | 리터럴 개수 | 계획서의 처분 |
|---|---:|---|
| `Assets/@Developers/RYU/...` | **31** | @Scripts·@Scenes·@Prefabs·@UI·@Tests로 **해체** |
| `Assets/@UI/...` | 17 | 그대로 둠 |
| `Assets/@Scenes/...` | 13 | 그대로 둠 |
| `Assets/@AddressableAssets/...` | 6 | `@Data`로 이동 |
| `Assets/@GameAssets/...` | 5 | `@Art`·`@Prefabs`로 분해 |
| `Assets/QuickOutline/...` | 3 | `ThirdParty/`로 이동 |
| `Assets/@Scripts/...` | 5 | 그대로 둠 |
| `Assets/tools/`,`Assets/Prefabs/`,`Assets/warehouseFin/` | 3 | 이동 |

`@Developers/RYU`는 지금 **경로 리터럴이 가장 많이 걸린 폴더**인데, 계획은 이걸 5개 폴더로 쪼갭니다.
게다가 `EditorBuildSettings.asset`의 등록된 씬 9개 중 **7개가 `Assets/@Developers/RYU/Scenes/` 아래**입니다.
"@ 접두사를 그대로 쓰니 수정이 적다"가 아니라, **RYU 해체가 이 계획에서 가장 비싼 단일 작업**입니다.

RYU를 그 자리에 두고 이름만 `@Developers/RYU` → `@Game`처럼 한 번 바꾸면
리터럴 31개를 한 번의 문자열 치환으로 끝낼 수 있습니다. 5개 폴더로 흩는 쪽은 그게 안 됩니다.

### 1-2. AudioManager / Singleton은 "중복"이 아니라 진행 중인 이중 운영

계획서는 두 파일을 비교해 보라고만 했는데, 비교 결과는 "합쳐라"가 아니라 "건드리지 마라"에 가깝습니다.

| | `@Scripts/Cores/AudioManager.cs` | `@Developers/RYU/Audio/Core/AudioManager.cs` |
|---|---|---|
| 크기 | 12,700 B | 21,283 B |
| 네임스페이스 | 전역(없음) | `Core.Audio` |
| 기반 | `Singleton<T>`(전역) | `Core.Foundation.Singleton<T>` |
| 씬·프리팹 참조 | StartScene, MainPlayScene, FlowTest, FreePlayTest, TutorialScene, CoreSystems.prefab | `Dev/CoreAudioTest.unity` 1개뿐 |

네임스페이스가 달라 공존한다는 계획서의 추측은 맞습니다. 그런데 **둘 다 현역이고, 그게 의도된 상태**입니다.
`@Scripts/Data/DataManager.cs:78-91`에 그대로 적혀 있습니다.

```
/// 두 곳에 싣는다. 구 <see cref="AudioManager"/>는 효과음·배경음 재생을 아직 맡고 있고,
/// <c>Core.Audio</c> 버스는 대사와 영상 음량의 권한이다 (ISSUE-002, ISSUE-015).
```
```csharp
var audio = AudioManager.Instance;                                   // 전역 쪽
if (!Core.Audio.AudioManager.TryGetInstance(out var buses)) return;  // RYU 쪽
```

`Core.Audio`를 쓰는 파일이 7개(`@Scripts/Cores/AudioBusVolume.cs`가 `using CoreAudioManager = Core.Audio.AudioManager;`로 별칭까지 걸어 씀),
`Core.Foundation`을 쓰는 파일이 4개입니다.
합치는 건 정리가 아니라 **ISSUE-002/015 마이그레이션 완료**라는 별도 작업입니다. 폴더 정리와 같은 PR에 넣으면 안 됩니다.

### 1-3. 중복 파일의 "어느 쪽이 진짜인가"가 계획의 직관과 반대

계획서는 `blackline.fbx ↔ blackline 1.fbx`, `darkwood.png ↔ darkwood 1.png`처럼 나열만 했습니다.
GUID 역검색을 하면 **살아 있는 쪽은 대체로 `" 1"`이 붙은 파일**입니다. 이름만 보고 지우면 참조가 깨집니다.

| 파일 | 참조 수 |
|---|---:|
| `tools/darkwood 1.png` | **2** (`darkwood.mat`, `darkwood 1.mat`) |
| `tools/darkwood.png` | 0 |
| `tools/blackline 1.fbx` | **2** (`blackline 1.prefab`, `blackline.prefab`) |
| `tools/blackline.fbx` | 0 |
| `tools/자귀.fbx` | **1** (`자귀배대패/자귀.prefab`) |
| `tools/자귀배대패/자귀.fbx` | 0 |
| `tools/bellyplane.fbx` | **1** (`자귀배대패/bellyplane.prefab`) |
| `tools/자귀배대패/bellyplane.fbx` | 0 |

`자귀`·`bellyplane`은 계획서가 묶은 방향("`tools/` 것과 `tools/자귀배대패/` 것")과도 **살아남는 쪽이 반대**입니다.
프리팹은 `자귀배대패/` 안에 있는데 참조하는 FBX는 `tools/` 바로 아래 것입니다.

### 1-4. `unity_all_assets.txt`(0 byte)는 존재하지 않음

`portfolio/` 전체를 뒤져도 이 이름의 파일이 없습니다. 계획서 1절의 이 항목은 삭제 대상 목록에서 빼야 합니다.

### 1-5. `*_Displacement.jpg`는 "머티리얼에 연결되지 않았다면"이 아니라 **연결돼 있음**

계획서 4절: "*_Displacement.jpg: 머티리얼에 연결되지 않았다면 삭제합니다."
전수 검사 결과 **7개 머티리얼이 실제로 물고 있습니다**.

```
@Developers/RYU/Start/FixedUI/meterials/backwood/backwood.mat      → PaintedWood009C_2K-JPG_Displacement.jpg
@Developers/RYU/Start/FixedUI/meterials/buttonwood/buttonwood.mat  → Wood036_2K-JPG_Displacement.jpg
@Developers/RYU/Start/FixedUI/meterials/Metal029_2K-JPG/Metal.mat  → Metal029_2K-JPG_Displacement.jpg
@Developers/RYU/Start/FixedUI/meterials/optionmetal/oldmetal.mat   → Metal022_2K-JPG_Displacement.jpg
@Developers/RYU/Start/FixedUI/meterials/Carpet.mat                 → Carpet012_2K-JPG_Displacement.jpg
@Developers/RYU/Start/FixedUI/meterials/Fabric.mat                 → Fabric042_2K-JPG_Displacement.jpg
WoodFloor004_2K-JPG/test.mat                                       → WoodFloor004_2K-JPG_Displacement.jpg
```

반면 `*_NormalDX.jpg` 9장은 **참조가 0**입니다. 계획서의 "머티리얼이 DX 쪽을 참조하고 있다면 GL로 바꾼 뒤" 걱정은 이 프로젝트에는 해당 없습니다.

따라서 85.6MB를 뭉뚱그릴 게 아니라 이렇게 나뉩니다.

| 분류 | 개수 | 용량 | 안전도 |
|---|---:|---:|---|
| `*_NormalDX.jpg` | 9 | **65.1 MB** | 참조 0 — 바로 삭제 가능 |
| `*_Displacement.jpg` | 9 | 20.5 MB | 머티리얼 7개가 참조 — 슬롯을 먼저 비워야 함 |
| `.mtlx`/`.tres`/`.usdc` | 26 | 0.04 MB | 용량 이득 거의 없음 |

### 1-6. ambientCG 세트는 8곳이 아니라 9곳 — `Assets/WoodFloor004_2K-JPG/`가 빠짐

계획서는 `RYU/Start/FixedUI/meterials`의 8세트만 셉니다.
`Assets/WoodFloor004_2K-JPG/`(15.7 MB, 10파일)도 같은 ambientCG 패키지이고 `.mtlx`/`.tres`/`.usdc`/`_NormalDX`/`_Displacement`를 똑같이 갖고 있습니다.

### 1-7. Addressables 주소는 경로가 아니라서 "옛 경로가 남는" 문제가 없음

계획서 2절: "@AddressableAssets: 옮겨도 로드는 되지만, 주소 문자열이 옛 경로 그대로 남습니다."
실제 그룹에 등록된 엔트리 7개의 주소는 전부 추상 주소입니다.

```
ium/data/manifest, ium/data/static/app, ium/data/static/cutscene,
ium/data/static/dialogue, ium/data/static/flow, ium/data/static/process, ium/data/static/quest
```

파일 경로가 주소로 쓰인 엔트리는 하나도 없습니다. Addressables 관점에서 `@AddressableAssets` → `@Data` 이동은 **무해**합니다.
깨지는 건 Addressables가 아니라 이 json들을 **파일 경로로 직접 읽는 테스트·에디터 창** 6줄입니다(3절 표 참조).

### 1-8. `ReleaseBuilder`가 경로를 박아 쓸 거라는 추측은 틀림

계획서 2절이 지목한 4개 빌더 중 `ReleaseBuilder.cs`만은 리터럴 경로가 **0개**입니다.
`EditorBuildSettings.scenes`를 읽어 enabled만 거릅니다(`ReleaseBuilder.cs:58-69`). 이동해도 안전합니다.
나머지 3개(`DevSceneBuilder`, `PlayWorkshopBuilder`, `SungnyemunImportBuilder`)는 추측대로 리터럴 투성이입니다.

### 1-9. Dev 씬은 "Build Settings에 없는" 게 아니라 **9개 중 3개가 등록·활성 상태**

계획서 3절 ②는 미사용 목록의 오탐 사유로 "Build Settings에 없는 Dev 씬 전용 자산"을 듭니다.
그런데 `FlowTest`, `FreePlayTest`, `TutorialScene`은 **enabled로 등록돼 있습니다**.

정작 등록돼 있지 않으면서 위험한 건 **`Assets/@Scenes/GongpoScene.unity`** 입니다. 계획서에 한 번도 등장하지 않습니다.
Dev 폴더에 있지도 않고, 에디터 빌더 2개가 상수로 물고 있으며(`PlayNpcBuilder.cs:35`, `PlayWorkshopBuilder.cs:49`, `StartScenePlayerSwapTool.cs:32`),
`iumi/iumi.fbx`·`iumi프리펩.prefab`·`Prefabs/Line.prefab`이 여기서도 쓰입니다.
빌드 씬이 아니므로 **미사용 목록에 GongpoScene 전용 에셋이 통째로 올라옵니다**. 오탐 사유 목록에 반드시 추가해야 합니다.

### 1-10. .gitignore 진단이 실제 저장소 구조와 어긋남

계획서 5절은 `.gitignore`가 "portfolio 루트 한 곳에만 있습니다"라고 했지만, **두 개**입니다.

| 파일 | 줄수 | 역할 |
|---|---:|---|
| `portfolio/.gitignore` | 51 | 저장소 루트. `bin/`(14줄), `*.obj`(16줄) — **계획서 지적이 맞는 쪽** |
| `xr-contest-ieum/.gitignore` | 129 | Unity 표준 ignore. `bin/`·`*.obj` **없음** |

그리고 계획서가 "추가할 내용"으로 제시한 항목 대부분은 **이미 `xr-contest-ieum/.gitignore`에 들어 있습니다.**

| 계획서가 추가하라는 항목 | 실제 |
|---|---|
| `[Rr]ecordings/` | 이미 있음 (27줄 `/[Rr]ecordings/`) |
| `*.apk` / `*.aab` / `*.unitypackage` | 이미 있음 (74-77줄) |
| `**/[Ss]treamingAssets/ai_secrets.json*` | 이미 있음 (114-115줄, 경로 고정형) |
| `**/[Ss]treamingAssets/ai_oauth_client.json*` | 이미 있음 (118-119줄) |
| `**/[Ss]treamingAssets/sherpa-onnx*` | 이미 있음 (124-125줄) |
| `**/[Ss]treamingAssets/aa/` | **실효 없음** — 88줄이 `/[Aa]ssets/StreamingAssets/aa*`로 **저장소 루트에 앵커**돼 있어 `IUM/Assets/...`에 매칭되지 않음. 계획서의 `**/` 형태가 맞음 |
| `__pycache__/`, `.venv/`, `.env.*`, `*.pem`, `*.key` | 없음 — 추가 필요 |

즉 계획서 5절 "누락" 항목 중 **실제로 누락인 것은 `aa/`(앵커 버그), 파이썬 캐시, 키 파일**뿐입니다.

### 1-11. `.gitattributes`는 "첫 커밋 전에" 넣을 수 없음 — 이미 늦음

계획서: "`.gitattributes`: 저장소 루트에 두고, 첫 커밋 전에 적용해야 합니다."

실제 상태:
- Git 루트는 `xr-contest-ieum/`이 아니라 **`portfolio/`** (모노레포, remote `github.com/boongbang0425/portfolio.git`)
- `portfolio/.gitattributes`가 **이미 존재**하며 내용은 `* text=auto` 한 줄뿐 (LFS 필터 없음)
- `git lfs ls-files` 결과 **비어 있음** — LFS 미사용
- **`.git`이 이미 1.6 GB**, 추적 파일 5,144개. 커밋 `7192402 포트폴리오 최초 업로드`로 아트가 전부 들어가 있음

그러므로 지금 `.gitattributes`에 LFS 필터를 추가해도 **과거 이력의 바이너리는 그대로 남아 저장소 크기가 줄지 않습니다.**
실제로 줄이려면 `git lfs migrate import --include="*.fbx,*.png,..." --everything` 같은 **이력 재작성**이 필요하고,
이미 push된 원격이라 force-push와 협업자 재클론을 동반합니다. 계획서에 이 비용이 빠져 있습니다.

추가로 지금의 `* text=auto` 한 줄은 바이너리에 줄바꿈 정규화가 잘못 걸릴 수 있는 형태라, LFS를 넣든 안 넣든
`*.fbx binary` 류의 명시가 있는 편이 안전합니다.

### 1-12. 용량 추정치 보정과 누락 항목

| 항목 | 계획서 추정 | 실측 | 비고 |
|---|---:|---:|---|
| ADG_Textures | 약 390 MB | **373.1 MB** | |
| SkySeries Freebie | 약 390 MB | **373.2 MB** (FreebieHdri만 373.0) | |
| RYU/Start/FixedUI/meterials | 약 235 MB | **225.3 MB** | |
| Sungnyemun | 약 220 MB | **208.8 MB** (texture 폴더만 201.5) | |
| ParticlePack | 약 150 MB 이상 | **188.8 MB** | 과소평가 |
| warehouse 계열 | 약 100 MB | **warehouseFin 폴더 160.7 MB** + `@GameAssets/warehouse_2.fbx` 57.2 MB = **217.9 MB** | 크게 과소평가 |
| VRTemplateAssets | 약 50 MB | **55.7 MB** | |
| legenooldman | 약 45 MB | **52.7 MB** | |

계획서 표에 **아예 없는** 항목:

| 항목 | 실측 |
|---|---:|
| `Assets/iumi/` | 21.0 MB |
| `Assets/Prefabs/` | 18.3 MB (`finalmesh11.fbx` 하나가 17.6 MB) |
| `Assets/WoodFloor004_2K-JPG/` | 15.7 MB |
| `Assets/Original Wood Textures/` | 13.4 MB |
| `Assets/Samples/` (XRI·XR Hands 샘플) | 14.2 MB |
| `Assets/TextMesh Pro/` | 11.5 MB (Examples & Extras 5.5 MB) |
| `Assets/tools/` | 11.5 MB |

`@GameAssets/warehouse_2.fbx`는 57.15 MiB로, GitHub 경고선(50 MiB)을 넘는 게 맞습니다(계획서의 59.9MB는 십진 MB 표기).

### 1-13. 목표 구조에 자리가 없는 폴더들

계획서 3절 목표 구조를 실제 폴더에 대보면 **행선지가 지정되지 않은 것**이 남습니다.

- `@Developers/RYU/Start/FixedUI/` — 225 MB의 `meterials`, `Monitor1~3.fbx`, `Lighting`, `Settings`, `Scripts` 4개
- `@Developers/RYU/Start/Rework/Scripts/` — `StartMonitorPrologue.cs` 등 4개
- `@Developers/RYU/Models/` — `Clipboard.fbx`, `Materials`, `Textures` (QuestBoardBuilder가 경로로 참조)
- `@Developers/RYU/Woodworking/` — `WoodTriplanarCompat.shader` (PlayWorkshopBuilder가 경로로 참조)
- `Assets/Original Wood Textures/`, `Assets/WoodFloor004_2K-JPG/`, `Assets/Textures/`, `Assets/meterials/`, `Assets/options.fbx`, `Assets/CompositionLayers/`
- `Assets/@GameAssets/Materials`, `Assets/@GameAssets/Textures`

또 `ThirdParty/`에 `UnityTechnologies/ParticlePack`, `SkySeries`, `ADG`, `QuickOutline`만 넣기로 했는데
`VRTemplateAssets`(55.7 MB, Unity VR 템플릿 잔재)와 `Plugins/Demigiant`(DOTween Pro, 유료)가 빠져 있습니다.

### 1-14. 미사용 목록 스크립트에 오탐 방지 장치가 없음 — 지우면 렌더링이 깨짐

계획서의 `UnusedAssetReport`는 루트를 **빌드 씬 + Resources + StreamingAssets + @AddressableAssets**로만 잡습니다.
그런데 Unity에는 **`ProjectSettings/`에서만 참조되는 에셋**이 따로 있고, 이건 어떤 씬의 의존성에도 안 잡힙니다.
실행 결과 아래가 전부 "미사용"으로 올라왔습니다. **지우면 프로젝트가 망가집니다.**

| 미사용으로 뜬 경로 | 실제 참조처 |
|---|---|
| `Assets/Settings/Mobile_RPAsset.asset`, `Mobile_Renderer.asset`, `UniversalRenderPipelineGlobalSettings.asset`, `Project Configuration/Quest_URP.asset` 등 11개 | `ProjectSettings/GraphicsSettings.asset`, `QualitySettings.asset`, `URPProjectSettings.asset` |
| `Assets/DefaultVolumeProfile.asset`, `Assets/Settings/DefaultVolumeProfile.asset` | URP Global Settings |
| `Assets/InputSystem_Actions.inputactions` | `EditorBuildSettings.m_configObjects: com.unity.input.settings.actions` |
| `Assets/AddressableAssetsData/` 13개 전부 | `EditorBuildSettings.m_configObjects: com.unity.addressableassets` |
| `Assets/XR/...`, `Assets/XRI/...` 일부 | XR Management / OpenXR 설정 |

계획서 3절 ②는 오탐 사유를 3가지("Dev 씬 전용 / 빌더가 부르는 / Addressables 등록")만 듭니다.
**"ProjectSettings에서만 참조되는 것"이 네 번째로 들어가야 하고, 실제로는 이게 가장 위험합니다.**
스크립트에 최소한 `Assets/Settings/`, `Assets/AddressableAssetsData/`, `*.inputactions`, `Assets/XR*`를
제외하는 필터를 넣거나, 출력에 경고를 붙이는 편이 맞습니다.

### 1-15. 맞은 것들

공정하게, 아래는 계획서가 정확했습니다.

- `StreamingAssets/Prologue_1.mp4`, `Prologue_2.mp4`가 **없고 `.meta`만 있음** — 확인됨 (아래 5절에서 심각도 상향)
- `@Documents/`가 `Assets` 안에 있어 Unity가 임포트함 — 확인됨 (33파일, 1.2 MB, `.pdf`/`.jpg` 포함)
- 오타 `meterials` 2곳 — 확인됨 (`Assets/meterials/`, `@Developers/RYU/Start/FixedUI/meterials/`)
- `w미ㅣ.mat` — 확인됨 (`@GameAssets/Sungnyemun/texture/w미ㅣ.mat`)
- `bbbb.png`(3.0 MB), `Object_14.png`(2.8 MB), `finalmesh11.fbx`(17.6 MB), `moded.fbx`, `modedprefab 1.prefab`, `warehouse_2.fbx` — 전부 존재
- SampleScene 3개 (`@Scenes/`, `Scenes/`, `warehouseFin/Scenes/`) — 확인됨
- `Readme.asset`, `TutorialInfo/`, `TextMesh Pro/Examples & Extras/`, `SkySeries Freebie/ExampleScenes`, `QuickOutline/Samples`, `Plugins/Demigiant/DOTweenPro Examples` — 전부 존재
- ambientCG 부속 파일 삭제로 "약 85MB 감소" — 실측 85.6 MB로 정확
- GUID 기반이라 Unity Project 창 안에서 옮기면 안 깨진다는 원리 — 맞음
- `Resources` 하위 경로 유지 원칙 — 맞음. `RYU/Audio/Resources/Sfx/*.mp3` 6개를 `@Audio/Resources/Sfx`로 옮기면 `WoodworkingSfxBootstrap.cs:30`의 `ClipRoot = "Sfx/"`가 그대로 유효
- `Samples/`는 Package Manager가 버전 경로로 추적하므로 옮기지 말 것 — 맞음 (`Samples/XR Interaction Toolkit/3.4.1/...`에 asmdef 4개)
- `ADG_Textures/Demo`가 잔재인 건 맞지만 실측 0.0 MB / 1파일이라 용량 이득은 없음

---

## 2. 삭제 후보

`UNUSED`는 배치모드 `UnusedAssetReport` 결과(`IUM/UnusedAssets.txt`), `REF=0`은 GUID 역검색 결과입니다.

### 2-0. 미사용 보고서 요약

전체 2,362개 중 **1,540개(1,233.6 MB)** 가 빌드 씬·Resources·StreamingAssets·Addressables 어디에서도 안 닿습니다.
상위 폴더별 미사용 용량은 이렇습니다(1-14절의 오탐 포함).

| 폴더 | 미사용 | 폴더 전체 | 미사용 비율 |
|---|---:|---:|---:|
| `SkySeries Freebie` | **365.2 MB** (68) | 373.2 MB | 98% — `FreebieHdri` 34장 **전부** 미사용 |
| `ADG_Textures` | **364.0 MB** (81) | 373.1 MB | 98% — `ground_vol1` 14세트 대부분 |
| `UnityTechnologies/ParticlePack` | **181.3 MB** (393) | 188.8 MB | 96% |
| `@Developers` | 104.5 MB (62) | 250.5 MB | 42% — **104.3 MB가 `RYU/Start/FixedUI` 한 곳** |
| `@GameAssets` | 64.3 MB (120) | 279.3 MB | 23% — 57.15 MB가 `warehouse_2.fbx` |
| `VRTemplateAssets` | **55.4 MB** (185) | 55.7 MB | 99% |
| `warehouseFin` | 43.1 MB (33) | 160.7 MB | 27% |
| `WoodFloor004_2K-JPG` | **15.7 MB** (10) | 15.7 MB | 100% |
| `Samples` | 13.6 MB (219) | 14.2 MB | 96% (건드리면 안 됨) |
| `legenooldman` | 7.0 MB (10) | 52.7 MB | 13% |
| `@Documents` | 1.2 MB (33) | 1.2 MB | 100% |

**보고서가 계획서를 뒤집은 지점 세 가지**

- `Assets/Prefabs/finalmesh11.fbx`(17.6 MB)는 **사용 중**입니다. 계획서가 "의미 없는 이름"으로 묶었지만 삭제 대상이 아닙니다
- `Assets/Original Wood Textures/`(13.4 MB)와 `@GameAssets/Sungnyemun/texture/`(201.5 MB)는 **전부 사용 중**입니다. Sungnyemun 텍스처는 삭제가 아니라 축소로만 접근해야 합니다
- `Assets/VRTemplateAssets/`가 **99% 미사용**(55.4/55.7 MB)입니다. 계획서는 "참조 확인"으로만 적었는데, 답은 "거의 안 씀"입니다. 폴더째 제거가 가장 깔끔하고, 3-3절의 서드파티 중복 5.6 MB도 같이 해결됩니다

**새로 드러난 것: `@Developers/RYU/Start/FixedUI/`가 104.3 MB / 52파일 미사용**

`StartScene.unity`는 빌드 씬인데도 FixedUI 자산 대부분이 안 닿습니다.
`RYU/Start/Rework/`(`StartMonitorPrologue.cs`, `StartPlayMenu.cs` 등)로 시작 화면이 교체되면서
FixedUI 쪽이 남은 것으로 보입니다. `ambientCG` 8세트 225 MB가 여기 있으므로 **확인 우선순위가 높습니다.**

### 2-1. 참조 0 — 바로 지워도 되는 것

| 경로 | 크기 | 근거 |
|---|---:|---|
| `Assets/@GameAssets/warehouse_2.fbx` | 57.15 MB | 어떤 씬·프리팹·머티리얼도 GUID를 참조하지 않음(REF=0). 실제로 쓰이는 창고는 `warehouseFin/Models/warehousetextureFin.fbx`(37.3 MB) → `warehousetextureFin.prefab` → `@Scenes/Play.unity`(빌드 씬). 계획서의 "둘 중 하나만 쓰는지 확인"에 대한 답 |
| `*_NormalDX.jpg` 9장 | 65.1 MB | 머티리얼 전수 검사에서 참조 0. Unity는 NormalGL을 씀 |
| `Assets/tools/darkwood.png` | 2.47 MB | REF=0. 살아 있는 건 `darkwood 1.png` |
| `Assets/tools/blackline.fbx` | 0.13 MB | REF=0. 살아 있는 건 `blackline 1.fbx` |
| `Assets/tools/자귀배대패/자귀.fbx` | 0.12 MB | REF=0. 살아 있는 건 `tools/자귀.fbx` |
| `Assets/tools/자귀배대패/bellyplane.fbx` | 0.05 MB | REF=0. 살아 있는 건 `tools/bellyplane.fbx` |
| `Assets/iumi/iumhair/` 5파일 | 0.02 MB | 5개 전부 REF=0. `iumi/` 직하 사본만 쓰임 |
| `.../meterials/NightSkyHDRI008.png` + `.../NightSkyHDRI008_2K (1)/NightSkyHDRI008.png` | 0.56 MB | **양쪽 다 REF=0** — 둘 다 삭제 가능 |
| `Assets/Readme.asset` | ~0 | REF=0. Unity 템플릿 잔재 |

### 2-2. 템플릿·샘플 잔재

| 경로 | 크기 | 근거 |
|---|---:|---|
| `Assets/TutorialInfo/` | 0.04 MB | Unity 템플릿. `ReadmeEditor.cs`가 `Assets/TutorialInfo`를 문자열로 씀 — 폴더째 지우면 스크립트도 같이 사라져 무해 |
| `Assets/TextMesh Pro/Examples & Extras/` | 5.53 MB | 136파일, 씬 31개. TMP 본체(`TextMesh Pro/Resources`)는 유지 필요 |
| `Assets/SkySeries Freebie/ExampleScenes/` | 0.19 MB | 데모 씬 15개 |
| `Assets/QuickOutline/Samples/` | 0.02 MB | |
| `Assets/Plugins/Demigiant/DOTweenPro Examples/` | 0.24 MB | 계획서는 `DOTweenPro Examples`가 아니라 `DOTweenPro/Examples`로 적었는데, 실제 이름은 전자 |
| `Assets/ADG_Textures/Demo/` | 0.00 MB | 1파일뿐, 용량 이득 없음 |
| `Assets/Scenes/SampleScene.unity` | 0.84 MB | 3개 SampleScene 중 |
| `Assets/@Scenes/SampleScene.unity` + `@Scenes/SampleScene/`(라이트맵) | 0.53 + 6.21 MB | |
| `Assets/warehouseFin/Scenes/SampleScene.unity` | 0.06 MB | |
| `Assets/@Scenes/BasicScene/` | 0.01 MB | |

### 2-3. 축소 대상 (삭제가 아니라 리사이즈)

| 경로 | 크기 | 조치 |
|---|---:|---|
| `@GameAssets/Sungnyemun/texture/*.fbx.png` | 201.5 MB (최대 `1floor_type1.fbx.png` 24.19 MB) | **전부 사용 중** — 삭제 불가, 4K 이하로 축소만. 같은 파일명·같은 확장자로 덮으면 GUID 유지 |
| `Assets/legenooldman/` | 52.65 MB 중 45.6 MB 사용 중 | 텍스처 축소 |
| `Assets/Prefabs/finalmesh11.fbx` | 17.64 MB | **사용 중** — 삭제 불가. 이름만 정리 대상 |
| `Assets/Original Wood Textures/` | 13.36 MB | **전부 사용 중** — 이동 대상일 뿐 |

### 2-4. 사용분만 남기기 — 보고서 결과로 대부분 판정됨

| 경로 | 폴더 크기 | 미사용 | 판정 |
|---|---:|---:|---|
| `Assets/SkySeries Freebie/FreebieHdri/` | 372.96 MB (34) | **373.0 MB 전부** | HDRI 34장이 하나도 안 쓰임. 하늘은 `.mat`(Skybox 머티리얼) 쪽에서 다른 소스를 쓰는 것으로 보임 — **폴더째 제거 후보** |
| `Assets/ADG_Textures/ground_vol1/` | 373.1 MB (86) | **364.0 MB** | 14세트 중 사용분은 9 MB 남짓 |
| `Assets/UnityTechnologies/ParticlePack/` | 188.80 MB (410) | **181.3 MB** | 쓰는 이펙트는 7.5 MB 정도 |
| `Assets/VRTemplateAssets/` | 55.70 MB (203) | **55.4 MB (99%)** | 사실상 전체 미사용 — **폴더째 제거 후보** |
| `Assets/WoodFloor004_2K-JPG/` | 15.72 MB (10) | **15.7 MB 전부** | `test.mat` 포함 전부 미사용 |
| `Assets/@Developers/RYU/Start/FixedUI/` | — | **104.3 MB (52)** | 시작 화면이 `Start/Rework`로 교체된 흔적. 2-0절 참조 |

> 이 6개만 정리하면 **약 1.09 GB**가 빠지고, 문자열 경로는 한 줄도 안 고쳐도 됩니다.
> 다만 "미사용"은 **활성 빌드 씬 9개 기준**이므로, 지우기 전에 위 4번째 오탐 사유(1-14절)와
> `GongpoScene`(6-2절)을 한 번 더 대조하십시오.

---

## 3. 중복 — 어느 쪽을 남길지

MD5 기준 중복 그룹 39개, **총 낭비 18.36 MB**입니다.
계획서가 암시하는 것보다 훨씬 작습니다. 중복 제거는 용량 대책이 아니라 혼동 제거로 봐야 합니다.

### 3-1. 판정이 끝난 것

| 유지 | 삭제 | 크기 | 이유 |
|---|---|---:|---|
| `iumi/iumi.fbx` | `Assets/iumi.fbx` | 7.09 MB | `iumi/iumi.fbx`는 Play, GongpoScene, `iumiController.controller`, `iumi프리펩.prefab`에서 4회 참조. 루트 사본은 `iumi프리펩.prefab` 1회뿐 — **단 이 프리팹이 양쪽을 다 물고 있어 바로 삭제 불가**(5절 위험 항목) |
| `tools/darkwood 1.png` | `tools/darkwood.png` | 2.47 MB | 살아 있는 쪽이 `" 1"` |
| `tools/blackline 1.fbx` | `tools/blackline.fbx` | 0.13 MB | 살아 있는 쪽이 `" 1"` |
| `tools/자귀.fbx` | `tools/자귀배대패/자귀.fbx` | 0.12 MB | 프리팹은 하위 폴더, FBX는 상위 것을 참조 |
| `tools/bellyplane.fbx` | `tools/자귀배대패/bellyplane.fbx` | 0.05 MB | 위와 동일 |
| `iumi/iumi_hair_custom_sage*.mat` (4개) | `iumi/iumhair/` 같은 이름 4개 | ~0 | `iumhair/` 쪽 전부 REF=0. 실제로 쓰이는 건 `iumi/iumi_hair_custom_sage_high_glow.mat` 하나(→`iumi프리펩.prefab`) |
| `iumi/iumi_hair_custom_sage.png` | `iumi/iumhair/iumi_hair_custom_sage.png` | ~0 | 전자는 머티리얼 8개가 참조, 후자 0 |
| (둘 다 삭제) | `meterials/NightSkyHDRI008.png`, `meterials/NightSkyHDRI008_2K (1)/NightSkyHDRI008.png` | 0.56 MB | 양쪽 REF=0 |

### 3-2. 양쪽 다 살아 있어 재배선이 필요한 것

| A | B | 크기 | 상황 |
|---|---|---:|---|
| `hammer/woodtooltexture1.png` | `tools/woodtooltexture1.png` | 2.35 MB | **둘 다 참조 1회**. A←`hammer/hammer.mat`, B←`tools/New Material.mat`. 한쪽을 지우려면 머티리얼 슬롯을 먼저 옮겨야 함 |

### 3-3. 서드파티끼리 겹치는 것 — 손대지 말 것

`Samples/XR Interaction Toolkit/3.4.1/Starter Assets/` ↔ `VRTemplateAssets/`가 8쌍 겹칩니다
(`Concrete_Albedo.tif` 2.12 MB, `Concrete_Normal.tif` 1.63 MB, `DefaultMaterial_AO.png` 0.85 MB,
`Concrete_Metallic.tif` 0.62 MB, `UniversalController.fbx` 0.30 MB, `BlinkVisual.fbx`, `ButtonClick.wav`, 스프라이트 몇 장 — 합계 **약 5.6 MB**).
계획서에 없는 클러스터이고, 이번 중복 낭비 18.36 MB의 3분의 1입니다.

`Samples/` 쪽은 Package Manager가 관리하므로 건드리면 안 됩니다.
`VRTemplateAssets/`(55.7 MB) **폴더 전체가 필요한지**를 먼저 판단하는 게 맞습니다. 필요 없으면 통째로 빠지면서 중복도 같이 해결됩니다.

`Plugins/Demigiant/` 내부 중복(`readme.txt` 2개, `DOTweenDeAudio.cs`↔`DOTweenDeUnityExtended.cs`, DemiLib 아이콘 3쌍)과
`ParticlePack` 내부 중복(`DustPuffSmall.png` 3개, `TinyStones.png` 3개 등)도 서드파티 배포 원본이므로 그대로 둡니다.

### 3-4. MD5로는 안 잡히지만 구조적으로 중복인 것

- `재질_2.xxx.mat`이 **222개** — `@GameAssets/Sungnyemun/newtexture/`(약 110)와 `warehouseFin/Materials/`(약 108)에 거의 같은 번호대로 존재. 바이트는 다르므로 MD5 중복이 아니지만, Blender 자동 이름이 두 모델에 걸쳐 겹칩니다
- `tools/재질_2.056.mat` ↔ `tools/재질_2.056 1.mat` 같은 `" 1"` 쌍이 4쌍
- `warehouseFin/Models/재질_2.091.mat`이 `warehouseFin/Materials/재질_2.091.mat`과 별개로 존재

---

## 4. 이동표 (원래 경로 → 새 경로)

계획서 목표 구조를 실제 폴더에 대입한 것입니다. **행선지 없음**은 계획서에 지정이 없어 이번에 보완한 항목입니다.

### 4-1. 계획서에 있는 이동

| 원래 경로 | 새 경로 | 문자열 경로 영향 |
|---|---|---|
| `Assets/Scripts/` (4 cs) | `Assets/@Scripts/Board/` | 없음 |
| `Assets/Editor/MeasureBounds.cs` | `Assets/@Scripts/Editor/` | 없음 |
| `Assets/Editor/TriplanarMaterialGenerator.cs` | `Assets/@Scripts/Editor/` | 자기 자신을 `FindAssets`로 찾음(178줄) — 이동해도 동작하나 185줄 `Assets/warehouseFin/Materials` 수정 필요 |
| `Assets/GazeSystem/*.cs` (13) | `Assets/@Scripts/Gaze/` | 없음 |
| `Assets/GazeSystem/*.md` (2) | `docs/` | 없음 |
| `Assets/SimpleFreeCamera.cs` | `Assets/@Scripts/Dev/` | 없음 |
| `@GameAssets/Sungnyemun/숭례문조립/BuildingStageDirector.cs` | `Assets/@Scripts/Sungnyemun/` | 없음. 빈 한글 폴더 제거됨 |
| `Assets/wood.unity` | `Assets/@Scenes/wood.unity` | 없음 (REF=0, 빌드 씬 아님) |
| `Assets/Prefabs/` (7) | `Assets/@Prefabs/` | **`PlayWorkshopBuilder.cs:633`** |
| `@Developers/RYU/Audio/Resources/Sfx/` (6 mp3) | `Assets/@Audio/Resources/Sfx/` | 없음 — `Resources/` 하위가 `Sfx/`로 유지되므로 `ClipRoot="Sfx/"` 그대로 유효 |
| `@Developers/RYU/ProcessIntegration/Tests/` (asmdef째) | `Assets/@Tests/` | **`CoreLoopContractTests.cs:32`** |
| `@AddressableAssets/` | `Assets/@Data/` | **6줄** (아래 5절 표) |
| `@Documents/` (33) | `xr-contest-ieum/docs/` | 없음. `.meta` 33개도 같이 사라짐 |
| `Assets/tools/`, `Assets/hammer/` | `Assets/@Art/Props/Tools/` | **`PlayWorkshopBuilder.cs:287`** |
| `Assets/iumi/` | `Assets/@Art/Characters/Ieumi/` | 없음 |
| `Assets/legenooldman/` | `Assets/@Art/Characters/Nojang/` | 없음 |
| `Assets/warehouseFin/` | `Assets/@Art/Environment/Workshop/` | **`TriplanarMaterialGenerator.cs:185`** |
| `@GameAssets/Sungnyemun/` | `Assets/@Art/Environment/Sungnyemun/` | **`SungnyemunImportBuilder.cs:28,29,30`** |
| `@GameAssets/*Part.fbx`, `testPart.fbx` | `Assets/@Art/Props/WoodParts/` | 없음 |
| `@GameAssets/Prefabs/` (3) | `Assets/@Prefabs/` | **`BoardHudBuilder.cs:22`, `PauseHudBuilder.cs:18,19`** |
| `@Developers/RYU/Quest/UI/` | `Assets/@UI/Quest/` | **8줄** |
| `@Developers/RYU/Prefabs/`, `@Developers/RYU/UI/` | `Assets/@Prefabs/` | **`PlaySystemsBuilder.cs:28`(29,30 파생), `BoardHudBuilder.cs:23`, `PauseHudBuilder.cs:20`, `QuestBoardBuilder.cs:22`** |
| `@Developers/RYU/Scenes/Main/`, `Cutscene/` | `Assets/@Scenes/` | **`DevSceneBuilder.cs:28`, `SungnyemunImportBuilder.cs:33`** + `EditorBuildSettings` 4줄 |
| `@Developers/RYU/Scenes/Dev/` | `Assets/@Scenes/Dev/` | **`DevSceneBuilder.cs:21`, `CoreLoopContractTests.cs:39`, `TutorialImportBuilder.cs:32`** + `EditorBuildSettings` 3줄 |
| `@Developers/RYU/Audio/`, `ProcessIntegration/*.cs` | `Assets/@Scripts/Audio/`, `@Scripts/Process/` | **`CoreLoopContractTests.cs:375`** |
| `UnityTechnologies/ParticlePack`, `SkySeries Freebie`, `ADG_Textures` | `Assets/ThirdParty/` | 없음 |
| `Assets/QuickOutline/` | `Assets/ThirdParty/QuickOutline/` | **`CoreLoopContractTests.cs:430,432,433`**. `Outline.cs`의 `Resources.Load("Materials/OutlineMask")`는 `Resources/` 하위가 유지되므로 무해 |

### 4-2. 계획서에 행선지가 없어 보완한 것

| 원래 경로 | 제안 경로 | 문자열 경로 영향 |
|---|---|---|
| `@Developers/RYU/Start/FixedUI/meterials/` (225.3 MB) | `Assets/@Art/Materials/StartRoom/` | 없음 (오타 `meterials` 동시 해소) |
| `@Developers/RYU/Start/FixedUI/Monitor1~3.fbx`, `Lighting`, `Settings` | `Assets/@Art/Environment/StartRoom/` | 없음 |
| `@Developers/RYU/Start/FixedUI/Scripts/`, `Start/Rework/Scripts/` | `Assets/@Scripts/Start/` | 없음 |
| `@Developers/RYU/Models/` | `Assets/@Art/Props/QuestBoard/` | **`QuestBoardBuilder.cs:20,21,143,149,152`** |
| `@Developers/RYU/Woodworking/` | `Assets/@Art/Materials/Woodworking/` | **`PlayWorkshopBuilder.cs:329,332`** |
| `Assets/Textures/` (6), `Assets/meterials/Fill.mat` | `Assets/@Art/Materials/` | 없음 |
| `Assets/Original Wood Textures/`, `Assets/WoodFloor004_2K-JPG/` | `Assets/@Art/Textures/Wood/` | 없음 |
| `Assets/options.fbx` | `Assets/@Art/Props/` | 없음 |
| `@GameAssets/Materials/`, `@GameAssets/Textures/` | `Assets/@Art/Materials/`, `@Art/Textures/` | 없음 |
| `Assets/VRTemplateAssets/` | `Assets/ThirdParty/` 또는 삭제 | 없음 (`VideoPlayerRenderTexture.cs`는 `Shader.Find`만 씀) |
| `Assets/Plugins/Demigiant/` | 그대로 (계획서대로 고정) | — |

### 4-3. 옮기면 안 되는 것

`StreamingAssets`(이름·위치 고정), `Plugins`, `TextMesh Pro`, `Samples`(asmdef 4개·PM 추적), `XR`, `XRI`,
`Settings`, `AddressableAssetsData`, `Resources/DOTweenSettings.asset`, `CompositionLayers`.

---

## 5. 수정해야 할 문자열 경로 (파일:줄)

전체 225줄 중 **이번 이동으로 실제로 깨지는 것만** 추렸습니다.
(서드파티 `Samples/`, `TextMesh Pro/Examples`, `TutorialInfo/` 내부 참조는 그 폴더가 통째로 삭제·고정되므로 제외.)

### `@AddressableAssets` → `@Data`
| 파일:줄 | 현재 값 |
|---|---|
| `@Developers/RYU/ProcessIntegration/Tests/Editor/CoreLoopContractTests.cs:33` | `Assets/@AddressableAssets/Data/Static/flow.json` |
| 〃 `:34` | `.../process.json` |
| 〃 `:36` | `.../quest.json` |
| 〃 `:37` | `.../dialogue.json` |
| 〃 `:38` | `.../cutscene.json` |
| `@Scripts/Quest/Editor/QuestGraphWindow.cs:17` | `Assets/@AddressableAssets/Data/Static/quest.json` |
| `Editor/UnusedAssetReport.cs` (신규) | `p.Contains("/@AddressableAssets/")` |

### `RYU/Quest/UI` → `@UI/Quest`
| 파일:줄 | 현재 값 |
|---|---|
| `@Scripts/Editor/DevSceneBuilder.cs:35` | `Assets/@Developers/RYU/Quest/UI/QuestHud.uxml` |
| 〃 `:36` | `.../QuestHudPanelSettings.asset` |
| `@Scripts/Editor/QuestBoardBuilder.cs:25` | `.../Fonts/Giants-Bold.ttf` |
| 〃 `:26` | `.../Fonts/Giants-Bold Dynamic SDF.asset` |
| `@Scripts/Editor/StartSceneReworkBuilder.cs:36` | `.../Fonts/Giants-Bold.ttf` |
| `CoreLoopContractTests.cs:434` | `.../QuestHud.uxml` |
| 〃 `:435` | `.../QuestHud.uss` |
| 〃 `:436` | `.../Fonts/Giants-Bold.ttf` |
| 〃 `:446` | `.../QuestHud.uxml` |

### `RYU/Scenes` → `@Scenes`
| 파일:줄 | 현재 값 |
|---|---|
| `@Scripts/Editor/DevSceneBuilder.cs:21` | `Assets/@Developers/RYU/Scenes/Dev` |
| 〃 `:28` | `Assets/@Developers/RYU/Scenes/Cutscene` |
| `@Scripts/Editor/SungnyemunImportBuilder.cs:33` | `Assets/@Developers/RYU/Scenes` |
| `@Scripts/Editor/PlayNpcBuilder.cs:36` | `.../Scenes/__NpcImportTemp.unity` |
| `@Scripts/Editor/PlayWorkshopBuilder.cs:50` | `.../Scenes/__PlayWorkshopImportTemp.unity` |
| `@Scripts/Editor/TutorialImportBuilder.cs:32` | `.../Scenes/Dev/TutorialScene.unity` |
| 〃 `:33` | `.../Scenes/__TutorialImportTemp.unity` |
| `CoreLoopContractTests.cs:39` | `.../Scenes/Dev/TutorialScene.unity` |
| **`ProjectSettings/EditorBuildSettings.asset`** | 등록된 9개 중 **7개**의 `path:` (Cutscene_Prologue, Cutscene_Ending, FlowTest, FreePlayTest, TutorialScene, MainPlayScene, Cutscene_SungnyemunBuild) |

> `EditorBuildSettings.asset`은 `guid:`도 같이 들고 있어 Unity 에디터 안에서 옮기면 자동 갱신됩니다.
> 탐색기로 옮겼다면 이 파일을 손으로 고쳐야 합니다.

### `RYU/Prefabs`, `RYU/UI` → `@Prefabs`
| 파일:줄 | 현재 값 |
|---|---|
| `@Scripts/Editor/PlaySystemsBuilder.cs:28` | `Assets/@Developers/RYU/Prefabs` (29·30줄 `CoreSystemsPath`/`PlayLoopPath`가 여기서 파생) |
| `@Scripts/Editor/QuestBoardBuilder.cs:22` | `.../RYU/Prefabs/QuestBoard.prefab` |
| `@Scripts/Editor/BoardHudBuilder.cs:23` | `Assets/@Developers/RYU/UI` |
| `@Scripts/Editor/PauseHudBuilder.cs:20` | `Assets/@Developers/RYU/UI` |

### `RYU/Models`, `RYU/Woodworking`, `RYU/ProcessIntegration`
| 파일:줄 | 현재 값 |
|---|---|
| `@Scripts/Editor/QuestBoardBuilder.cs:20` | `Assets/@Developers/RYU/Models/Clipboard.fbx` |
| 〃 `:21` | `.../Models/Materials` |
| 〃 `:143` | `AssetDatabase.CreateFolder("Assets/@Developers/RYU/Models", "Materials")` |
| 〃 `:149` | `.../Models/Textures/ClipboardMDF.png` |
| 〃 `:152` | `.../Models/Textures/ClipboardPaper.png` |
| `@Scripts/Editor/PlayWorkshopBuilder.cs:329` | `.../Woodworking/WoodTriplanarCompat.shader` |
| 〃 `:332` | `.../Woodworking` |
| `CoreLoopContractTests.cs:32` | `.../ProcessIntegration/Tests/CoreLoopVerificationProfile.json` |
| 〃 `:375` | `.../ProcessIntegration/MainPlayProcessBridge.cs` |

### 루트 폴더 이동
| 파일:줄 | 현재 값 | 원인 |
|---|---|---|
| `@Scripts/Editor/PlayWorkshopBuilder.cs:633` | `Assets/Prefabs/Line.prefab` | `Prefabs/` → `@Prefabs/` |
| `@Scripts/Editor/PlayWorkshopBuilder.cs:287` | `Assets/tools/darkwood.mat` | `tools/` → `@Art/Props/Tools/` |
| `Editor/TriplanarMaterialGenerator.cs:185` | `Assets/warehouseFin/Materials` | `warehouseFin/` → `@Art/Environment/Workshop/` |
| `@Scripts/Editor/SungnyemunImportBuilder.cs:28` | `Assets/@GameAssets/Sungnyemun/texture/modedprefab 1.prefab` | Sungnyemun 이동 |
| 〃 `:29` | `.../texture/SungnyemunDirector.prefab` | 〃 |
| 〃 `:30` | `Assets/@GameAssets/Sungnyemun` (`FindAssets`의 검색 루트, 313줄) | 〃 |
| `@Scripts/Editor/BoardHudBuilder.cs:22` | `Assets/@GameAssets/Prefabs/BoardRoot.prefab` | `@GameAssets/Prefabs` 이동 |
| `@Scripts/Editor/PauseHudBuilder.cs:18` | `Assets/@GameAssets/Prefabs/Pause.prefab` | 〃 |
| 〃 `:19` | `Assets/@GameAssets/Prefabs/Option.prefab` | 〃 |
| `CoreLoopContractTests.cs:430` | `Assets/QuickOutline/Scripts/Outline.cs` | QuickOutline → ThirdParty |
| 〃 `:432` | `Assets/QuickOutline/Resources/Materials/OutlineMask.mat` | 〃 |
| 〃 `:433` | `Assets/QuickOutline/Resources/Materials/OutlineFill.mat` | 〃 |

### 이름 변경 시 같이 깨지는 것
계획서 1절 "의미 없는 이름"을 실행하면 아래도 함께 고쳐야 합니다.

| 파일:줄 | 값 | 계획서가 바꾸려는 이름 |
|---|---|---|
| `@Scripts/Editor/SungnyemunImportBuilder.cs:28` | `.../texture/modedprefab 1.prefab` | `modedprefab 1.prefab` |
| `@Scripts/Editor/PlayWorkshopBuilder.cs:287` | `Assets/tools/darkwood.mat` | (`darkwood 1.png`를 참조하는 머티리얼) |

### 이동해도 안전한 것 (참고)
- `Shader.Find(...)` 14곳 — 셰이더 이름 기반이라 파일 이동과 무관
- `Resources.Load` 계열 — `Resources/` 하위 상대 경로 유지가 조건. 해당 이동(`RYU/Audio/Resources/Sfx`, `QuickOutline/Resources`) 모두 하위 구조 보존이라 무해
- `@Scripts/Editor/*Builder.cs`의 `Assets/@UI/...` 17줄, `Assets/@Scenes/...` 13줄 — 해당 폴더를 안 옮기므로 무해
- `@Scripts/AI/AiConfigLoader.cs:43`, `GoogleOAuthSetup.cs:40,234,251` — `Application.streamingAssetsPath` 기반, StreamingAssets 고정이므로 무해
- `ReleaseBuilder.cs` — 리터럴 경로 0개

---

## 6. 불확실·위험 항목

### 6-1. Prologue 영상 2개가 실제로 없음 — "확인 필요"가 아니라 **런타임 실패**

계획서는 "일부러 뺀 것인지 확인이 필요합니다"로 적었습니다. 실제 상태는 그보다 심각합니다.

```
StreamingAssets/Prologue_1.mp4.meta   ← .meta만 있음
StreamingAssets/Prologue_2.mp4.meta   ← .meta만 있음
StreamingAssets/OnboardingVideoVRT.webm  ← 이건 실물 있음
```

그런데 `@AddressableAssets/Data/Static/cutscene.json:14`가 이 둘을 **정상 경로로 지정**하고 있습니다.

```json
"videos": ["Prologue_1.mp4", "Prologue_2.mp4"],
```

같은 파일 12줄의 주석은 "H.264 960x720, 총 3분 1초 … 두 파일로 분할되어 videos 목록을 순서대로 이어 재생한다"고
**있다는 전제로** 쓰여 있습니다. `CutsceneVideoSurface.cs:294`가 `streamingAssetsPath`에 이어 붙여 여는 구조라
지금 빌드하면 **프롤로그 컷씬이 재생되지 않습니다.**
또 `CoreLoopContractTests.cs:91`이 `Path.Combine(Application.streamingAssetsPath, cutscene.video)`를 검사하므로
계획서 3절 ④가 요구하는 `CoreLoopContractTests` 통과 자체가 지금 상태에서 실패할 가능성이 있습니다.

**정리 작업 전에 먼저 결정할 일**: 영상을 복원할 것인가, `cutscene.json`을 `prologue_scene`으로 되돌릴 것인가.
(주석에 "되돌리려면 두 항목의 id를 맞바꾼다"고 방법이 적혀 있습니다.)
`.mp4`가 `.gitignore`에 걸려 빠졌을 가능성이 있는데, 현재 `xr-contest-ieum/.gitignore`에 `*.mp4`는 없으므로
로컬에서 지웠거나 처음부터 커밋되지 않은 쪽입니다.

### 6-2. `@Scenes/GongpoScene.unity`가 빌드 씬이 아님

Dev 폴더 밖에 있고 이름도 프로덕션급인데 `EditorBuildSettings`에 등록돼 있지 않습니다.
반면 에디터 도구 3개가 상수로 물고 있고(`PlayNpcBuilder.cs:35`, `PlayWorkshopBuilder.cs:49`, `StartScenePlayerSwapTool.cs:32`),
`iumi프리펩.prefab`·`Prefabs/Line.prefab`·`iumi/iumi.fbx`가 여기서도 쓰입니다.

**보고서로 완화된 부분**: 배치모드 실행 결과 `Assets/@Scenes/GongpoScene.unity`는 **의존성 폐포(`used`) 안에 들어와 있습니다.**
즉 이 씬의 에셋들은 이미 순회됐고, 우려했던 "GongpoScene 전용 에셋이 통째로 미사용으로 뜨는" 대량 오탐은 발생하지 않았습니다.

**다만 경로가 불명확한 채로 남습니다.** 이 씬의 GUID(`99c9720ab356a0642a771bea13969a05`)를
`Assets` 전체에서 문자열로 찾으면 자기 `.meta`와 `Settings/Project Configuration/BasicScene.scenetemplate` 두 곳뿐인데,
그 scenetemplate 자체는 미사용 목록에 있습니다. Unity 내부 의존성 그래프가 어떤 경로로 이 씬을 끌어왔는지는
이번 검증으로 특정하지 못했습니다. **삭제 판단의 근거로 쓰기 전에 에디터에서 직접 확인하십시오.**

별개로, 빌드 씬에 없으므로 **런타임에 이름으로 로드할 수 없습니다.** 살아 있는 씬이라면 등록이 필요합니다.

### 6-3. `iumi프리펩.prefab`이 `iumi.fbx` 두 사본을 모두 참조

`Assets/iumi.fbx`(REF=1)와 `iumi/iumi.fbx`(REF=4)가 바이트 동일한데,
**`iumi프리펩.prefab` 하나가 양쪽 GUID를 다 들고 있습니다.**
루트 사본을 그냥 지우면 이 프리팹의 어떤 슬롯이 Missing이 됩니다.
프리팹을 열어 루트 `iumi.fbx`를 쓰는 부분(메시·아바타·애니메이션 중 무엇인지)을 확인하고 `iumi/iumi.fbx`로 다시 물린 뒤에 삭제해야 합니다.
`iumi프리펩.prefab`은 `@Scenes/Play.unity`(빌드 씬)와 `GongpoScene.unity`에서 쓰이므로 파급이 큽니다.

### 6-4. 두 AudioManager 통합은 이번 정리 범위 밖

1-2절 참조. 폴더 이동만 하고 코드는 그대로 두는 것을 권합니다.
특히 `@Scripts/Cores/Singleton.cs`(`where T : MonoBehaviour`)와 `Core.Foundation.Singleton`(`where T : Singleton<T>`)은
제약이 달라 기계적으로 합칠 수 없습니다.

### 6-5. Unity 버전 불일치

0절 참조. `6000.3.9f1`이 없어 `6000.3.19f1` + 프로젝트 사본으로 검증했습니다.
같은 6000.3 계열이라 에셋 의존성 그래프는 동일할 가능성이 높지만,
실제 정리 작업은 **6000.3.9f1을 설치한 뒤** 진행하십시오.
다른 버전으로 원본을 열면 `ProjectVersion.txt`가 바뀌고 임포터가 에셋을 재직렬화해 diff가 대량 발생합니다.

### 6-6. `.git`이 이미 1.6 GB — LFS 도입은 이력 재작성 동반

1-11절 참조. 계획서 4절의 "첫 커밋 전에 적용" 전제가 이미 깨졌습니다.
선택지는 셋입니다.

1. **이력 재작성** — `git lfs migrate import --everything`. 저장소는 줄지만 force-push 필요, 공유 중이면 협업자 재클론
2. **새 저장소** — 포트폴리오용 저장소를 새로 파고 코드·문서·ProjectSettings만 올림. 계획서 4절 "권장"이 사실상 이것
3. **현상 유지** — 1.6 GB는 GitHub 권장(1 GB)을 넘지만 하드 리밋(5 GB)은 아님. `warehouse_2.fbx` 57 MiB가 경고선을 넘었을 뿐 차단선(100 MiB)은 아님

또 현재 `portfolio/.gitattributes`가 `* text=auto` 한 줄뿐이라, LFS와 무관하게 바이너리 정규화 사고 여지가 있습니다.

### 6-7. 삭제하면 안 되는데 미사용으로 보이는 것들

- `Assets/Resources/DOTweenSettings.asset` — 런타임 자동 로드
- `Assets/QuickOutline/Resources/Materials/Outline*.mat`, `Shaders/Outline*.shader` — `Outline.cs`가 `Resources.Load`로 문자열 로드
- `@Developers/RYU/Audio/Resources/Sfx/*.mp3` 6개 — `WoodworkingSfxBootstrap.cs`가 `"Sfx/" + 이름`으로 로드
- `StreamingAssets/*` 전부 — AssetDatabase 의존성 그래프 밖
- `TextMesh Pro/Resources/TMP Settings.asset` — XRI 샘플 검증 스크립트 2곳이 존재를 확인함

### 6-8. 한글 경로

`숭례문조립`, `자귀배대패`, `천.png`, `iumi프리펩.prefab`, `w미ㅣ.mat`, `성벽.mat`, `재질_2.xxx.mat` 222개.
계획서의 ASCII 권고에 동의합니다. 다만 `재질_2.xxx` 222개는 Blender에서 다시 뽑지 않는 한 일괄 개명이
`.mat` 이름과 FBX 머티리얼 슬롯 이름의 대응을 끊을 수 있어, **재임포트 시 슬롯 재연결이 필요할 수 있습니다.** 우선순위를 뒤로 미루십시오.

### 6-9. 서드파티 재배포

`Plugins/Demigiant/DOTween Pro`는 유료 에셋입니다. 현재 공개 저장소(`github.com/boongbang0425/portfolio`)에
원본이 올라가 있고, 이미 커밋 이력에도 남아 있습니다. 계획서 4절의 지적이 맞으며, 이건 정리보다 우선순위가 높은 사안입니다.

---

## 7. 권장 순서 (계획서 3절 대체)

계획서 순서는 "① 백업 → ② 미사용 목록 → ③ 삭제 → ④ 이동"인데, ④가 가장 비싸고 되돌리기 어렵습니다.
검증 결과에 맞춰 아래를 제안합니다.

1. **백업** (계획서와 동일)
2. **6-1 결정**: Prologue 영상 복원 또는 `cutscene.json` 롤백. 이게 안 되면 `CoreLoopContractTests`가 회귀 판정 기준으로 못 씀
3. **6-2 결정**: `GongpoScene.unity`를 살릴지 버릴지. 살린다면 `EditorBuildSettings`에 등록
4. **미사용 목록에서 오탐 4종 걸러내기** (1-14절): `Assets/Settings/`, `AddressableAssetsData/`, `*.inputactions`, `XR*`
5. **참조 0 삭제** (2-1절): `warehouse_2.fbx` 57 MB + `NormalDX` 65 MB + 소소한 중복 = **약 125 MB**, 문자열 경로 수정 0
6. **템플릿·샘플 삭제** (2-2절): 약 13 MB, 문자열 경로 수정 0
7. **미사용 폴더 정리** (2-4절): SkySeries HDRI 373 MB + ADG 364 MB + ParticlePack 181 MB + VRTemplateAssets 55 MB + WoodFloor004 16 MB + RYU/Start/FixedUI 104 MB = **약 1.09 GB**. 여기가 진짜 용량 대책이고, 문자열 경로 수정 0
8. **텍스처 축소** (2-3절): Sungnyemun 201 MB(전부 사용 중이라 축소만), legenooldman 46 MB
9. **`.gitignore` 보정**: `aa/` 앵커 버그, `__pycache__/`, `.venv/`, `.env.*`, `*.pem`, `*.key`
10. **이동** — 여기까지 오면 옮길 대상이 크게 줄어 있습니다. RYU 해체는 1-1절 근거로 재검토하십시오
11. **개명** — 마지막. `재질_2.xxx` 222개는 별도 판단

5~8단계만으로 **1.2 GB 이상**이 빠지고(전체 1.9 GB → 약 0.7 GB) 문자열 경로는 한 줄도 안 고쳐도 됩니다.
계획서가 가장 먼저 하려던 폴더 재배치는 **용량 이득이 0이면서 위험이 가장 큰 작업**입니다.

---

## 8. 생성한 파일

| 경로 | 내용 |
|---|---|
| `IUM/Assets/Editor/UnusedAssetReport.cs` | 계획서 코드를 `public static Run()`으로 고치고, 존재하지 않는 루트 경로 방어(`Where(File.Exists)`)와 진단 출력 2개를 추가 |
| `IUM/UnusedAssets.txt` | 배치모드 실행 결과 1,540줄 (크기 내림차순) |
| `docs/cleanup_audit.md` | 이 문서 |

에셋은 하나도 수정·이동·삭제하지 않았습니다.
Unity 배치모드는 `IUM/`의 **사본**(임시 폴더)에서 실행했으므로 원본에는 `Library/`도 생기지 않았습니다.
