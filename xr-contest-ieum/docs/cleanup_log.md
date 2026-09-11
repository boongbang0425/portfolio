# 삭제 실행 기록

실행: 2026-09-11 09:20:34
근거: `docs/delete_list.md` 4조건 통과분. 승인 범위 = 447개 중 **B04(Dev 씬 3개) 제외 → 444개**.
보호: 문서류 34개, `tools/darkwood.png`, `tools/blackline.fbx`, `hammer/woodtooltexture1.png` 는 대상에서 뺐습니다.
각 파일은 `.meta`와 함께 삭제했습니다. git 명령은 실행하지 않았습니다.

삭제 전 `Assets/` 총량: 1,947.5 MB / 5110 파일 (.meta 포함)

## 묶음별 결과

| 묶음 | 대상 | 파일 | .meta | 삭제 용량 | 결과 |
|---|---|---:|---:|---:|---|
| B01 | `Assets/@GameAssets/warehouse_2.fbx` | 1 | 1 | 57.15 MB | OK |
| B02 | `Assets/ADG_Textures/` | 18 | 18 | 47.07 MB | OK |
| B03 | `Assets/@Developers/RYU/Start/FixedUI/` | 49 | 49 | 89.53 MB | OK |
| B05 | `Assets/warehouseFin/` | 33 | 33 | 43.16 MB | OK |
| B06 | `Assets/UnityTechnologies/ParticlePack/` | 56 | 56 | 35.63 MB | OK |
| B07 | `Assets/VRTemplateAssets/` | 50 | 50 | 10.96 MB | OK |
| B08 | `Assets/legenooldman/` | 10 | 10 | 6.98 MB | OK |
| B09 | `Assets/WoodFloor004_2K-JPG/` | 5 | 5 | 5.60 MB | OK |
| B10 | `Assets/@Scenes/ + Assets/Scenes/` | 6 | 6 | 7.06 MB | OK |
| B11 | `Assets/TextMesh Pro/Examples & Extras/` | 52 | 52 | 1.51 MB | OK |
| B12 | `Assets/SkySeries Freebie/` | 19 | 19 | 0.15 MB | OK |
| B13 | `Assets/@GameAssets/Sungnyemun/newtexture/` | 112 | 112 | 0.44 MB | OK |
| B14 | `Assets/@GameAssets/ (그 외)` | 1 | 1 | 0.02 MB | OK |
| B15 | `Assets/iumi/` | 17 | 17 | 0.07 MB | OK |
| B16 | `Assets/tools/` | 12 | 12 | 0.22 MB | OK |
| B18 | `기타 (Assets 최상위)` | 3 | 3 | 0.70 MB | OK |
| **합계** | | **444** | **444** | **306.25 MB** | |

삭제 후 `Assets/` 총량: **1,641.2 MB** / 4222 파일 — 306.2 MB 감소

## 삭제하지 않은 것 (승인 범위 밖)

| 항목 | 개수 | 사유 |
|---|---:|---|
| B04 Dev 씬 | 3 | 승인에서 제외 (`GongpoTest`,`CutsceneTest`,`InteractionTest`) |
| 문서류 | 34 | `@Documents/` 29, `GazeSystem/*.md` 2, RYU README/WorkLog 3 — 이동 대상 |
| `tools/darkwood.png` | 1 | 조건3 (이름이 `PlayWorkshopBuilder.cs`에 등장) |
| `tools/blackline.fbx` | 1 | 조건3 |
| `hammer/woodtooltexture1.png` | 1 | 조건2 (`hammer.mat`이 참조) |
| 조건2 실패 | 611 | 미사용 파일끼리 참조 — `docs/folder_delete_list.md` 참조 |

## 짝 없는 `.meta` 검사 (삭제 후 전수)

`Assets/` 전체 4,222파일 / 379폴더를 훑었습니다. 에셋 1,920 · `.meta` 2,302.

### 본체 없는 `.meta` — 7개 (**전부 이번 삭제와 무관한 기존 항목**)

삭제한 444개는 모두 `.meta`를 쌍으로 지웠으므로 **이번 작업이 새로 만든 orphan은 0개**입니다.
아래 7개는 삭제 전부터 있던 것으로, 매니페스트 444개 경로 어디에도 해당하지 않습니다.

| `.meta` | 없어진 대상 | 성격 |
|---|---|---|
| `Assets/StreamingAssets/Prologue_1.mp4.meta` | `Prologue_1.mp4` | `cleanup_audit.md` 6-1절 — `cutscene.json`이 참조하는데 실물 없음. **런타임 영향 있음** |
| `Assets/StreamingAssets/Prologue_2.mp4.meta` | `Prologue_2.mp4` | 위와 동일 |
| `Assets/@Developers/RYU/Audio/Editor.meta` | `Editor/` 폴더 | 빈 폴더 메타 잔재 |
| `Assets/@Documents/Temp.meta` | `Temp/` 폴더 | 빈 폴더 메타 잔재 |
| `Assets/CompositionLayers/UserSettings.meta` | `UserSettings/` 폴더 | 빈 폴더 메타 잔재 |
| `Assets/Scenes/SampleScene.meta` | `SampleScene/` 폴더 | 라이트맵 폴더 메타 잔재 (`.unity` 쪽은 이번에 정상 삭제) |
| `Assets/VRTemplateAssets/Videos.meta` | `Videos/` 폴더 | 빈 폴더 메타 잔재 |

> 이 7개는 Unity를 열면 자동으로 정리됩니다. 삭제 승인 범위 밖이라 **손대지 않았습니다.**

### `.meta`가 없는 파일 — 4개

| 파일 | 성격 |
|---|---|
| `Assets/Editor/UnusedAssetReport.cs` | 이번에 내가 만든 스크립트. 원본 프로젝트를 Unity로 연 적이 없어 아직 `.meta` 미생성 |
| `Assets/StreamingAssets/devcontest.mov` | **지난 턴 이후 새로 추가된 파일** (이전 조사 때는 없었음). 임포트 전 상태 |
| `Assets/@Documents/09_협업_설치_가이드.md` | 기존. `cleanup_audit.md` 1-11절에서 키 제거 이력으로 언급된 파일 |
| `Assets/VRTemplateAssets/Fonts/Inter/.gitattributes` | 점파일 — Unity가 무시하므로 정상 |

폴더 중 `.meta` 없는 것: **0개**

### 삭제로 비워진 폴더 — 12개

폴더와 폴더 `.meta`는 승인 범위 밖이라 **그대로 뒀습니다.** Unity를 열면 정리하거나, 다음 라운드에서 처리하십시오.

```
Assets/@Developers/RYU/Start/FixedUI/meterials/buttonwood/Materials
Assets/@Developers/RYU/Start/FixedUI/meterials/optionmetal/Materials
Assets/ADG_Textures/Demo
Assets/CompositionLayers                    (삭제 전부터 비어 있었음)
Assets/iumi/iumhair
Assets/Scenes
Assets/VRTemplateAssets/Models/Marks
Assets/VRTemplateAssets/Models/Poke
Assets/VRTemplateAssets/Prefabs/Blaster
Assets/VRTemplateAssets/Prefabs/Blink
Assets/VRTemplateAssets/Prefabs/Cursors
Assets/warehouseFin/Scenes
```


---

# 2차 삭제 (폴더 단위)

실행: 2026-09-11 09:49:04
근거: `docs/folder_delete_list.md`. git 명령은 실행하지 않았습니다.

## 2-1. 폴더 전체 삭제 — 8곳 (폴더 밖 살아있는 참조 0)

폴더 안 파일 + 각 `.meta` + 빈 폴더 + 폴더 `.meta`까지 삭제했습니다.
아래 수치는 1차에서 이미 지운 분을 뺀 **2차 실제 삭제분**입니다.

| 폴더 | 파일 | `.meta` | 용량 | 결과 |
|---|---:|---:|---:|---|
| `Assets/TextMesh Pro/Examples & Extras` | 90 | 103 | 4.37 MB | OK |
| `Assets/WoodFloor004_2K-JPG` | 5 | 6 | 10.14 MB | OK |
| `Assets/hammer` | 3 | 4 | 2.36 MB | OK |
| `Assets/TutorialInfo` | 4 | 8 | 0.05 MB | OK |
| `Assets/QuickOutline/Samples` | 2 | 5 | 0.03 MB | OK |
| `Assets/Plugins/Demigiant/DOTweenPro Examples` | 7 | 9 | 0.24 MB | OK |
| `Assets/@Scenes/BasicScene` | 2 | 3 | 0.01 MB | OK |
| `Assets/meterials` | 1 | 2 | 0.00 MB | OK |
| **소계** | **114** | **140** | **17.21 MB** | |

> `Assets/TextMesh Pro/Examples & Extras`의 `.cs` 34개와 `TutorialInfo`의 `.cs` 2개를 포함해 총 36개 스크립트가 사라졌습니다.
> 삭제 전에 각 클래스명을 프로젝트 전체 `.cs`에서 역검색해 **외부 참조 0건**을 확인했습니다.

## 2-2. 부분 삭제 — 서드파티 4곳

살아 있는 파일이 참조하는 에셋을 **뿌리**로 잡고, 뿌리가 참조하는 폴더 내부 에셋을 재귀로 따라가 **유지 집합**을 만들었습니다.
의존성은 에셋 본문과 `.meta`(`externalObjects` 리맵 포함)의 GUID 양쪽에서 뽑았습니다.

안전장치: 폴더 안의 `.cs`, `.asmdef`, `.asmref`, `.shader`, `.cginc`, `.hlsl`, `.shadergraph`, `.shadersubgraph` 는 용량이 미미한 반면 삭제 시 컴파일·`Shader.Find` 파손 위험이 있어 **전부 보존**했습니다.

| 폴더 | 삭제 전 | 뿌리 | 유지 | 보존(코드/셰이더) | 삭제 | 삭제 후 |
|---|---:|---:|---:|---:|---:|---:|
| `Assets/SkySeries Freebie` | 373.1 MB | 1 | 7 | 0 | 49개 / 365.1 MB | **8.0 MB** |
| `Assets/ADG_Textures` | 326.1 MB | 1 | 5 | 0 | 63개 / 317.1 MB | **9.0 MB** |
| `Assets/UnityTechnologies/ParticlePack` | 153.3 MB | 2 | 12 | 16 | 326개 / 146.2 MB | **7.5 MB** |
| `Assets/VRTemplateAssets` | 44.8 MB | 1 | 2 | 19 | 132개 / 44.9 MB | **0.2 MB** |
| **소계** | | | | | **570개 / 873.3 MB** | |

### 유지된 에셋 (뿌리 + 의존성 / 코드·셰이더 제외)

**`Assets/SkySeries Freebie`** — 7개

- `Assets/SkySeries Freebie/6SidedMegaSun.mat` **(뿌리)**
- `Assets/SkySeries Freebie/FreebieHdri/MegaSunBack.hdr`
- `Assets/SkySeries Freebie/FreebieHdri/MegaSunBottom.hdr`
- `Assets/SkySeries Freebie/FreebieHdri/MegaSunFront.hdr`
- `Assets/SkySeries Freebie/FreebieHdri/MegaSunLeft.hdr`
- `Assets/SkySeries Freebie/FreebieHdri/MegaSunRight.hdr`
- `Assets/SkySeries Freebie/FreebieHdri/MegaSunTop.hdr`

**`Assets/ADG_Textures`** — 5개

- `Assets/ADG_Textures/ground_vol1/ground1/ground1.mat` **(뿌리)**
- `Assets/ADG_Textures/ground_vol1/ground1/ground1_Ambient_Occlusion.tga`
- `Assets/ADG_Textures/ground_vol1/ground1/ground1_Diffuse.tga`
- `Assets/ADG_Textures/ground_vol1/ground1/ground1_Height.tga`
- `Assets/ADG_Textures/ground_vol1/ground1/ground1_Normal.tga`

**`Assets/UnityTechnologies/ParticlePack`** — 12개

- `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Materials/DustPuffParticle.mat`
- `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Materials/WoodSplintersParticle.mat`
- `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Materials/WoodSurface.mat`
- `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Prefabs/WoodImpacts.prefab` **(뿌리)**
- `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Textures/DustPuffSmallParticleSheet.png`
- `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Textures/WoodAlbedo.tif`
- `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Textures/WoodNormals.tif`
- `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Textures/WoodOcclusion.tif`
- `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Textures/woodSplintersAlbedo.tif`
- `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Textures/woodSplintersNormal.tif`
- `Assets/UnityTechnologies/ParticlePack/URP.asset` **(뿌리)**
- `Assets/UnityTechnologies/ParticlePack/URP_Renderer.asset`

**`Assets/VRTemplateAssets`** — 1개

- `Assets/VRTemplateAssets/Materials/Pointer/Pointer Outline.mat` **(뿌리)**

## 2-3. 2차 합계

| | 파일 | `.meta` | 용량 |
|---|---:|---:|---:|
| 2-1 폴더 전체 (8곳) | 114 | 140 | 17.21 MB |
| 2-2 부분 삭제 (4곳) | 570 | 569 | 873.34 MB |
| **2차 합계** | **684** | **709** | **890.54 MB** |

`Assets/` 최종: **750.7 MB** / 2829 파일

## 2-4. 짝 없는 `.meta` 재검사 (2차 삭제 후)

`Assets/` 전체 2,829파일 / 353폴더. 에셋 1,236 · `.meta` 1,593.

### 본체 없는 `.meta` — 7개 (1차 검사와 **동일**, 2차로 늘지 않음)

2차에서 지운 684개도 전부 `.meta`를 쌍으로 처리했습니다. 폴더 삭제 시 폴더 `.meta`까지 함께 지웠습니다.
남은 7개는 모두 1차 이전부터 있던 항목입니다.

```
Assets/StreamingAssets/Prologue_1.mp4.meta      ← cutscene.json이 참조하는데 실물 없음 (런타임 영향)
Assets/StreamingAssets/Prologue_2.mp4.meta      ← 위와 동일
Assets/@Developers/RYU/Audio/Editor.meta        ← 빈 폴더 메타 잔재
Assets/@Documents/Temp.meta                     ← 빈 폴더 메타 잔재
Assets/CompositionLayers/UserSettings.meta      ← 빈 폴더 메타 잔재
Assets/Scenes/SampleScene.meta                  ← 라이트맵 폴더 메타 잔재
Assets/VRTemplateAssets/Videos.meta             ← 빈 폴더 메타 잔재
```

### `.meta` 없는 파일 — 3개 (1차의 4개에서 1개 감소)

| 파일 | 성격 |
|---|---|
| `Assets/Editor/UnusedAssetReport.cs` | 감사용으로 추가한 스크립트. Unity로 연 적 없어 `.meta` 미생성 |
| `Assets/StreamingAssets/devcontest.mov` | 최근 추가된 75.6 MB 영상. 임포트 전 상태 |
| `Assets/@Documents/09_협업_설치_가이드.md` | 기존. **아래 보안 항목 참조** |

`VRTemplateAssets/Fonts/Inter/.gitattributes`는 2차에서 폴더째 삭제돼 목록에서 빠졌습니다.
폴더 중 `.meta` 없는 것: **0개**

### 빈 폴더 — 105개 (1차 12개 → 105개)

폴더 자체는 승인 범위 밖이라 **그대로 뒀습니다.** Unity를 열면 정리되거나, 3차에서 일괄 처리하십시오.
분포: `VRTemplateAssets` 38, `UnityTechnologies/ParticlePack` 34, `ADG_Textures` 15, `SkySeries Freebie` 2, 기타 16.

## 2-5. 후속 확인이 필요한 부수 효과

| 항목 | 내용 |
|---|---|
| `Assets/Readme.asset` | `TutorialInfo/Scripts/Readme.cs`(guid `fcf7219b…`)를 `m_Script`로 물고 있었는데 그 스크립트가 2-1에서 삭제됐습니다. 이제 **스크립트 없는 ScriptableObject**입니다. 원래도 미사용·템플릿 잔재라 함께 지우는 것이 맞지만, 승인 범위(폴더 8곳) 밖이라 남겨 뒀습니다 |
| `Assets/hammer/` 전체 삭제 | 1차 때 보호했던 `hammer/woodtooltexture1.png`가 여기 포함돼 사라졌습니다. 1차 보호 사유는 "`hammer.mat`이 참조"였는데, 이번엔 `hammer.mat`을 포함한 폴더 전체가 폴더 밖 참조 0으로 확인돼 함께 삭제했습니다. 중복 짝이던 `tools/woodtooltexture1.png`는 그대로 남아 있습니다 |


---

# 3차 정리 (Readme / 빈 폴더 / 짝 없는 .meta / devcontest.mov)

실행: 2026-09-11 10:37:09

## 3-1. `Assets/Readme.asset` 삭제

- 삭제 `Assets/Readme.asset`
- 삭제 `Assets/Readme.asset.meta`

2차에서 `TutorialInfo/Scripts/Readme.cs`가 사라져 스크립트 없는 ScriptableObject가 된 파일입니다. 2개 / 0.00 MB

## 3-2. 짝 없는 `.meta` 삭제 (Prologue 2개는 유지)

| `.meta` | 처리 |
|---|---|

삭제 0개 / 유지 2개

## 3-3. `StreamingAssets/devcontest.mov` 이동

- 폴더 생성 `C:\Users\ybang\OneDrive\Desktop\portfolio_media`
- 이동 `Assets/StreamingAssets/devcontest.mov` → `C:\Users\ybang\OneDrive\Desktop\portfolio_media\devcontest.mov` (75.56 MB)
- `.meta` 삭제 (원래 없었으나 임포트로 생성됐을 경우)

## 3-4. 빈 폴더 삭제 (폴더 `.meta` 포함)

삭제한 빈 폴더: **109개**

```
Assets/@Developers/RYU/Audio/Editor
Assets/@Developers/RYU/Start/FixedUI/meterials/buttonwood/Materials
Assets/@Developers/RYU/Start/FixedUI/meterials/optionmetal/Materials
Assets/@Documents/Temp
Assets/ADG_Textures/Demo
Assets/ADG_Textures/ground_vol1/ground10
Assets/ADG_Textures/ground_vol1/ground11
Assets/ADG_Textures/ground_vol1/ground12
Assets/ADG_Textures/ground_vol1/ground13
Assets/ADG_Textures/ground_vol1/ground14
Assets/ADG_Textures/ground_vol1/ground2
Assets/ADG_Textures/ground_vol1/ground3
Assets/ADG_Textures/ground_vol1/ground4
Assets/ADG_Textures/ground_vol1/ground5
Assets/ADG_Textures/ground_vol1/ground6
Assets/ADG_Textures/ground_vol1/ground7
Assets/ADG_Textures/ground_vol1/ground8
Assets/ADG_Textures/ground_vol1/ground9
Assets/iumi/iumhair
Assets/Scenes
Assets/Scenes/SampleScene
Assets/SkySeries Freebie/ExampleScenes
Assets/SkySeries Freebie/ExampleScenes/SceneObjects
Assets/StreamingAssets~
Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects
Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Materials
Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Prefabs
Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Textures
Assets/UnityTechnologies/ParticlePack/EffectExamples/Goop Effects
Assets/UnityTechnologies/ParticlePack/EffectExamples/Goop Effects/Materials
Assets/UnityTechnologies/ParticlePack/EffectExamples/Goop Effects/Prefabs
Assets/UnityTechnologies/ParticlePack/EffectExamples/Goop Effects/Textures
Assets/UnityTechnologies/ParticlePack/EffectExamples/Legacy Particles
Assets/UnityTechnologies/ParticlePack/EffectExamples/Legacy Particles/Materials
Assets/UnityTechnologies/ParticlePack/EffectExamples/Legacy Particles/Prefabs
Assets/UnityTechnologies/ParticlePack/EffectExamples/Legacy Particles/Textures
Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Materials
Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Models
Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Prefabs
Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Textures
Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Animations
Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Materials
Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Models
Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Prefabs
Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Textures
Assets/UnityTechnologies/ParticlePack/EffectExamples/Smoke & Steam Effects
Assets/UnityTechnologies/ParticlePack/EffectExamples/Smoke & Steam Effects/Materials
Assets/UnityTechnologies/ParticlePack/EffectExamples/Smoke & Steam Effects/Models
Assets/UnityTechnologies/ParticlePack/EffectExamples/Smoke & Steam Effects/Prefabs
Assets/UnityTechnologies/ParticlePack/EffectExamples/Smoke & Steam Effects/Textures
Assets/UnityTechnologies/ParticlePack/EffectExamples/Water Effects
Assets/UnityTechnologies/ParticlePack/EffectExamples/Water Effects/Materials
Assets/UnityTechnologies/ParticlePack/EffectExamples/Water Effects/Models
Assets/UnityTechnologies/ParticlePack/EffectExamples/Water Effects/Prefabs
Assets/UnityTechnologies/ParticlePack/EffectExamples/Water Effects/Textures
Assets/UnityTechnologies/ParticlePack/Scenes
Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles
Assets/UnityTechnologies/ParticlePack/Scenes/Profiles
Assets/UnityTechnologies/ParticlePack/Settings
Assets/UnityTechnologies/ParticlePack/Shared/Environment
Assets/UnityTechnologies/ParticlePack/Shared/Environment/Materials
Assets/UnityTechnologies/ParticlePack/Shared/Environment/Models
Assets/UnityTechnologies/ParticlePack/Shared/Environment/Prefabs
Assets/UnityTechnologies/ParticlePack/Shared/Environment/Textures
Assets/UnityTechnologies/ParticlePack/Shared/Prefabs
Assets/UnityTechnologies/ParticlePack/Shared/Sprites
Assets/UnityTechnologies/ParticlePack/TutorialInfo
Assets/UnityTechnologies/ParticlePack/TutorialInfo/Icons
Assets/VRTemplateAssets/Audio
Assets/VRTemplateAssets/Fonts
Assets/VRTemplateAssets/Fonts/Inter
Assets/VRTemplateAssets/Graphics
Assets/VRTemplateAssets/Materials/Anchor Materials
Assets/VRTemplateAssets/Materials/Controller
Assets/VRTemplateAssets/Materials/Environment
Assets/VRTemplateAssets/Materials/Locomotion
Assets/VRTemplateAssets/Materials/Particles
Assets/VRTemplateAssets/Materials/Primitive
Assets/VRTemplateAssets/Materials/UI
Assets/VRTemplateAssets/Models
Assets/VRTemplateAssets/Models/Anchor
Assets/VRTemplateAssets/Models/Blink
Assets/VRTemplateAssets/Models/Controllers
Assets/VRTemplateAssets/Models/Cursors
Assets/VRTemplateAssets/Models/Environment
Assets/VRTemplateAssets/Models/Marks
Assets/VRTemplateAssets/Models/Poke
Assets/VRTemplateAssets/Models/Primitives
Assets/VRTemplateAssets/Models/UI
Assets/VRTemplateAssets/Prefabs
Assets/VRTemplateAssets/Prefabs/Affordance
Assets/VRTemplateAssets/Prefabs/Blaster
Assets/VRTemplateAssets/Prefabs/Blink
Assets/VRTemplateAssets/Prefabs/Controller
Assets/VRTemplateAssets/Prefabs/Cursors
Assets/VRTemplateAssets/Prefabs/Interactables
Assets/VRTemplateAssets/Prefabs/Setup
Assets/VRTemplateAssets/Prefabs/Teleport
Assets/VRTemplateAssets/Prefabs/TutorialPlayer
Assets/VRTemplateAssets/Prefabs/UI
Assets/VRTemplateAssets/Sprites
Assets/VRTemplateAssets/Sprites/CoachingCards
Assets/VRTemplateAssets/Sprites/Icons
Assets/VRTemplateAssets/Sprites/UI
Assets/VRTemplateAssets/Themes
Assets/VRTemplateAssets/Tutorial
Assets/VRTemplateAssets/Tutorial/Images
Assets/VRTemplateAssets/Videos
Assets/warehouseFin/Scenes
```

## 3-5. 결과

`Assets/` : 750.7 MB / 2835 파일 → **675.1 MB / 2723 파일**

## 4-1. `@Documents` 를 `xr-contest-ieum/docs/` 로 이동 (D)

`AssetDatabase.MoveAsset` 은 `Assets/` 밖으로 옮길 수 없어, Unity를 닫은 상태에서 파일시스템 이동을 했습니다.
`.meta` 는 지시대로 함께 옮기지 않고 삭제했습니다 (Assets 밖에서는 의미가 없음).

- 이동 33개 → `docs/game-design/` · 버린 `.meta` 35개 · 폴더와 폴더 `.meta` 삭제

## 4-2. `GazeSystem/*.md` 를 `docs/` 로 이동 (1차 #6)

- `Assets/GazeSystem/GazeSystem_Manual.md` → `docs/gaze-system/GazeSystem_Manual.md`
- `Assets/GazeSystem/NPC_Library_Manual.md` → `docs/gaze-system/NPC_Library_Manual.md`

## 4-3. `StreamingAssets~` (빈 폴더) 삭제

## 4-4. 빈 폴더 정리

- 삭제한 빈 폴더 **90개** (그중 1차 AssetMover 버그가 만든 `' N'` 중복 폴더 **82개**)

```
Assets/@AddressableAssets
Assets/@Art 1
Assets/@Art 10
Assets/@Art 11
Assets/@Art 12
Assets/@Art 13
Assets/@Art 14
Assets/@Art 15
Assets/@Art 16
Assets/@Art 17
Assets/@Art 18
Assets/@Art 19
Assets/@Art 2
Assets/@Art 20
Assets/@Art 21
Assets/@Art 22
Assets/@Art 23
Assets/@Art 24
Assets/@Art 25
Assets/@Art 3
Assets/@Art 4
Assets/@Art 5
Assets/@Art 6
Assets/@Art 7
Assets/@Art 8
Assets/@Art 9
Assets/@Art/Characters 1
Assets/@Art/Characters 2
Assets/@Art/Environment 1
Assets/@Art/Environment 2
Assets/@Art/Environment/Sungnyemun/숭례문조립
Assets/@Art/Materials 1
Assets/@Art/Materials 2
Assets/@Art/Materials 3
Assets/@Art/Materials 4
Assets/@Art/Materials 5
Assets/@Art/Materials 6
Assets/@Art/Props 1
Assets/@Art/Props 2
Assets/@Art/Props 3
Assets/@Art/Props 4
Assets/@Art/Props 5
Assets/@Art/Props 6
Assets/@Art/Props 7
Assets/@Art/Props 8
Assets/@Art/Props/WoodParts 1
Assets/@Art/Props/WoodParts 2
Assets/@Art/Props/WoodParts 3
Assets/@Art/Props/WoodParts 4
Assets/@Art/Textures 1
Assets/@Art/Textures 2
Assets/@GameAssets
Assets/@GameAssets/Prefabs
Assets/@Prefabs 1
Assets/@Prefabs 2
Assets/@Prefabs 3
Assets/@Prefabs 4
Assets/@Prefabs 5
Assets/@Prefabs 6
Assets/@Prefabs 7
Assets/@Prefabs 8
Assets/@Scripts/Board 1
Assets/@Scripts/Board 2
Assets/@Scripts/Board 3
Assets/@Scripts/Board 4
Assets/@Scripts/Dev 1
Assets/@Scripts/Gaze 1
Assets/@Scripts/Gaze 10
Assets/@Scripts/Gaze 11
Assets/@Scripts/Gaze 12
Assets/@Scripts/Gaze 13
Assets/@Scripts/Gaze 14
Assets/@Scripts/Gaze 2
Assets/@Scripts/Gaze 3
Assets/@Scripts/Gaze 4
Assets/@Scripts/Gaze 5
Assets/@Scripts/Gaze 6
Assets/@Scripts/Gaze 7
Assets/@Scripts/Gaze 8
Assets/@Scripts/Gaze 9
Assets/@Scripts/Sungnyemun 1
Assets/GazeSystem
Assets/Prefabs
Assets/Scripts
Assets/Textures
Assets/ThirdParty 1
Assets/ThirdParty 2
Assets/ThirdParty 3
Assets/ThirdParty 4
Assets/ThirdParty 5
```

## 4-5. 이동 후 짝 없는 .meta: 0개

- 없음

