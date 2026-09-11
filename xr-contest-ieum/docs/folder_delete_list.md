# 폴더 단위 삭제 판정 (보고 전용 — 아무것도 삭제하지 않았습니다)

작성: 2026-09-11 09:28:11 · 삭제 실행 **후** 상태 기준

## 검사 방법

1. 폴더 안 모든 에셋의 `.meta` GUID 수집
2. 그 GUID를 **폴더 밖** 소비자 파일 823개에서 검색 (`Assets/` 전역 `*.unity/*.prefab/*.asset/*.mat/*.controller/*.anim/*.json/*.uxml/*.uss/*.tss/*.scenetemplate/*.lighting` 등 + `ProjectSettings/*` + `Packages/*`)
3. 참조처를 **살아 있는 파일**과 **그 자체도 미사용인 파일**로 구분

3번이 핵심입니다. 참조처가 그 자체로 죽은 파일이면 실질적으로는 삭제 가능하기 때문입니다.
특히 `Assets/Settings/Project Configuration/*.scenetemplate` 2개는 조건4 제외 폴더에 있어 살아남았지만,
**빌드 씬이 아니라 "새 씬 만들기" 템플릿**이라 이것만 참조하는 에셋은 런타임에 필요 없습니다.

## 요약

| 폴더 | 현재 용량 | 에셋 | 살아있는 참조 | 죽은 참조만 | 판정 |
|---|---:|---:|---:|---:|---|
| `Assets/SkySeries Freebie` | 373.0 MB | 56 | 1 | 6 | 부분 삭제 — 1개 유지 |
| `Assets/ADG_Textures` | 326.1 MB | 68 | 1 | 0 | 부분 삭제 — 1개 유지 |
| `Assets/UnityTechnologies/ParticlePack` | 153.3 MB | 354 | 2 | 9 | 부분 삭제 — 2개 유지 |
| `Assets/VRTemplateAssets` | 44.8 MB | 153 | 1 | 102 | 부분 삭제 — 1개 유지 |
| `Assets/TextMesh Pro/Examples & Extras` | 4.3 MB | 90 | 0 | 0 | **폴더 전체 삭제 후보** |
| `Assets/WoodFloor004_2K-JPG` | 10.1 MB | 5 | 0 | 0 | **폴더 전체 삭제 후보** |
| `Assets/@Developers/RYU/Start/FixedUI` | 154.7 MB | 67 | 25 | 23 | 부분 삭제 — 25개 유지 |
| `Assets/TutorialInfo` | 0.0 MB | 4 | 0 | 2 | **폴더 전체 삭제 후보** |
| `Assets/QuickOutline/Samples` | 0.0 MB | 2 | 0 | 0 | **폴더 전체 삭제 후보** |
| `Assets/Plugins/Demigiant/DOTweenPro Examples` | 0.2 MB | 7 | 0 | 0 | **폴더 전체 삭제 후보** |
| `Assets/hammer` | 2.4 MB | 3 | 0 | 0 | **폴더 전체 삭제 후보** |
| `Assets/@Scenes/BasicScene` | 0.0 MB | 2 | 0 | 0 | **폴더 전체 삭제 후보** |
| `Assets/Original Wood Textures` | 13.4 MB | 5 | 3 | 2 | 부분 삭제 — 3개 유지 |
| `Assets/Textures` | 0.0 MB | 5 | 5 | 0 | 부분 삭제 — 5개 유지 |
| `Assets/meterials` | 0.0 MB | 1 | 0 | 0 | **폴더 전체 삭제 후보** |

**폴더 전체 삭제 후보 합계: 17.1 MB / 에셋 114개**

## A. 폴더 전체 삭제 후보 — 살아 있는 참조 0

| 폴더 | 용량 | 에셋 | 비고 |
|---|---:|---:|---|
| `Assets/WoodFloor004_2K-JPG` | 10.1 MB | 5 | 폴더 밖 참조 전혀 없음 |
| `Assets/TextMesh Pro/Examples & Extras` | 4.3 MB | 90 | 폴더 밖 참조 전혀 없음 |
| `Assets/hammer` | 2.4 MB | 3 | 폴더 밖 참조 전혀 없음 |
| `Assets/Plugins/Demigiant/DOTweenPro Examples` | 0.2 MB | 7 | 폴더 밖 참조 전혀 없음 |
| `Assets/TutorialInfo` | 0.0 MB | 4 | 폴더 밖 참조 2개가 있으나 참조처가 전부 미사용 파일 |
| `Assets/QuickOutline/Samples` | 0.0 MB | 2 | 폴더 밖 참조 전혀 없음 |
| `Assets/@Scenes/BasicScene` | 0.0 MB | 2 | 폴더 밖 참조 전혀 없음 |
| `Assets/meterials` | 0.0 MB | 1 | 폴더 밖 참조 전혀 없음 |

## B. 부분 삭제안 — 살아 있는 파일이 참조

### `Assets/SkySeries Freebie` — 373.0 MB, 에셋 56개

- **유지 필요: 1개** (아래 표)
- 삭제 가능 후보: 나머지 55개 중, 그 유지 대상들의 의존성을 뺀 것
- 참조처가 전부 미사용이라 실질 삭제 가능: 6개

| 유지해야 할 에셋 | 살아 있는 참조처 |
|---|---|
| `Assets/SkySeries Freebie/6SidedMegaSun.mat` | `Assets/@Scenes/GongpoScene.unity` |

### `Assets/ADG_Textures` — 326.1 MB, 에셋 68개

- **유지 필요: 1개** (아래 표)
- 삭제 가능 후보: 나머지 67개 중, 그 유지 대상들의 의존성을 뺀 것

| 유지해야 할 에셋 | 살아 있는 참조처 |
|---|---|
| `Assets/ADG_Textures/ground_vol1/ground1/ground1.mat` | `Assets/@Scenes/Play.unity` |

### `Assets/@Developers/RYU/Start/FixedUI` — 154.7 MB, 에셋 67개

- **유지 필요: 25개** (아래 표)
- 삭제 가능 후보: 나머지 42개 중, 그 유지 대상들의 의존성을 뺀 것
- 참조처가 전부 미사용이라 실질 삭제 가능: 23개

| 유지해야 할 에셋 | 살아 있는 참조처 |
|---|---|
| `Assets/@Developers/RYU/Start/FixedUI/Fixedoption.fbx` | `Assets/@GameAssets/Prefabs/BoardRoot.prefab` |
| `Assets/@Developers/RYU/Start/FixedUI/Lighting/Global Volume Profile.asset` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/Lighting/LightingData.asset` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/backwood/backwood.mat` | `Assets/@GameAssets/Prefabs/BoardRoot.prefab` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/black.mat` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/BrightButtons.mat` | `Assets/@GameAssets/Prefabs/BoardRoot.prefab` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/Brightness.mat` | `Assets/@GameAssets/Prefabs/BoardRoot.prefab`<br>`Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/buttonwood/buttonwood.mat` | `Assets/@GameAssets/Prefabs/BoardRoot.prefab` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/Carpet.mat` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/Fabric.mat` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/Metal029_2K-JPG/Metal.mat` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/Monitor.mat` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/Monitor1.fbx` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/Monitor2.fbx` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/Monitor3.fbx` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/New Lighting Settings.lighting` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/NightSkyHDRI008_2K (1)/Materials/NightSkyHDRI008_2K_HDR.mat` | `Assets/@Scenes/Play.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/NightSkyHDRI008_2K (1)/NightSky.mat` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/optionmetal/oldmetal.mat` | `Assets/@GameAssets/Prefabs/BoardRoot.prefab` |
| `Assets/@Developers/RYU/Start/FixedUI/meterials/Rubber.mat` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/news.fbx` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/pause.fbx` | `Assets/@GameAssets/Prefabs/BoardRoot.prefab` |
| `Assets/@Developers/RYU/Start/FixedUI/Scripts/FixedUIStartMenuAdapter.cs` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/Scripts/StartScenePlayerInteraction.cs` | `Assets/@Scenes/StartScene.unity` |
| `Assets/@Developers/RYU/Start/FixedUI/Settings/PC_RPAsset.asset` | `Assets/@Scenes/StartScene.unity` |

### `Assets/UnityTechnologies/ParticlePack` — 153.3 MB, 에셋 354개

- **유지 필요: 2개** (아래 표)
- 삭제 가능 후보: 나머지 352개 중, 그 유지 대상들의 의존성을 뺀 것
- 참조처가 전부 미사용이라 실질 삭제 가능: 9개

| 유지해야 할 에셋 | 살아 있는 참조처 |
|---|---|
| `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Prefabs/WoodImpacts.prefab` | `Assets/@Scenes/GongpoScene.unity`<br>`Assets/@Scenes/Play.unity` |
| `Assets/UnityTechnologies/ParticlePack/URP.asset` | `ProjectSettings/GraphicsSettings.asset` |

### `Assets/VRTemplateAssets` — 44.8 MB, 에셋 153개

- **유지 필요: 1개** (아래 표)
- 삭제 가능 후보: 나머지 152개 중, 그 유지 대상들의 의존성을 뺀 것
- 참조처가 전부 미사용이라 실질 삭제 가능: 102개

| 유지해야 할 에셋 | 살아 있는 참조처 |
|---|---|
| `Assets/VRTemplateAssets/Materials/Pointer/Pointer Outline.mat` | `Assets/@Developers/RYU/Scenes/Main/MainPlayScene.unity`<br>`Assets/@Scenes/GongpoScene.unity`<br>`Assets/@Scenes/Play.unity` |

### `Assets/Original Wood Textures` — 13.4 MB, 에셋 5개

- **유지 필요: 3개** (아래 표)
- 삭제 가능 후보: 나머지 2개 중, 그 유지 대상들의 의존성을 뺀 것
- 참조처가 전부 미사용이라 실질 삭제 가능: 2개

| 유지해야 할 에셋 | 살아 있는 참조처 |
|---|---|
| `Assets/Original Wood Textures/Wood Texture 02/Materials/Wood Texture 02 diffuse.mat` | `Assets/@Developers/RYU/Scenes/Main/MainPlayScene.unity`<br>`Assets/@Scenes/GongpoScene.unity` |
| `Assets/Original Wood Textures/Wood Texture 05/Wood Texture 05 diffuse.tga` | `Assets/Textures/Wood Texture 05 diffuse.mat` |
| `Assets/Original Wood Textures/Wood Texture 05/Wood Texture 05 normal.tga` | `Assets/Textures/Wood Texture 05 diffuse.mat` |

### `Assets/Textures` — 0.0 MB, 에셋 5개

- **유지 필요: 5개** (아래 표)
- 삭제 가능 후보: 나머지 0개 중, 그 유지 대상들의 의존성을 뺀 것

| 유지해야 할 에셋 | 살아 있는 참조처 |
|---|---|
| `Assets/Textures/_Generic#1.mat` | `Assets/@Developers/RYU/Scenes/Main/MainPlayScene.unity`<br>`Assets/@Scenes/GongpoScene.unity`<br>`Assets/@Scenes/Play.unity` |
| `Assets/Textures/_Generic.mat` | `Assets/@Developers/RYU/Scenes/Main/MainPlayScene.unity`<br>`Assets/@Scenes/GongpoScene.unity`<br>`Assets/Prefabs/Line.prefab` |
| `Assets/Textures/CorrectMat.mat` | `Assets/@Developers/RYU/Scenes/Main/MainPlayScene.unity`<br>`Assets/@Scenes/GongpoScene.unity`<br>`Assets/@Scenes/Play.unity` |
| `Assets/Textures/Wood Texture 05 diffuse.mat` | `Assets/@Developers/RYU/Scenes/Main/MainPlayScene.unity`<br>`Assets/@Scenes/GongpoScene.unity` |
| `Assets/Textures/WrongMat.mat` | `Assets/@Developers/RYU/Scenes/Main/MainPlayScene.unity`<br>`Assets/@Scenes/GongpoScene.unity`<br>`Assets/@Scenes/Play.unity` |

## 주의

- GUID 문자열 참조만 봅니다. `Resources.Load`/`Shader.Find` 같은 문자열 로드는 잡지 못합니다.
- `Assets/Plugins`, `Assets/Samples`, `Assets/XR`, `Assets/XRI`는 조건4 제외 대상이라 참고용입니다.
- 서드파티 원본을 지우면 Asset Store에서 다시 받아야 합니다. 재배포 문제(`cleanup_audit.md` 6-9절)와 같이 판단하십시오.

