# 삭제 대상 목록 (승인 대기)

`docs/cleanup_audit.md` 기준. **이 문서 작성 시점까지 아무것도 삭제하지 않았습니다.** 승인 후 진행합니다.

## 검사 방법

| 조건 | 판정 방법 |
|---|---|
| 1 | `IUM/UnusedAssets.txt` 1,540줄에 존재 |
| 2 | `.meta`의 `guid`를 IUM 전체 소비자 파일 **1,057개**(`*.unity/*.prefab/*.asset/*.mat/*.controller/*.anim/*.json` + `ProjectSettings/*` + `Packages/*`)에서 검색 → 0건 |
| 3 | 파일명(확장자 포함/제외)과 에셋 경로를 `*.cs`,`*.json` **332개**(2,537,482자)에서 검색 → 0건 |
| 4 | `Assets/Settings`, `AddressableAssetsData`, `*.inputactions`, `*/Resources/*`, `StreamingAssets`, `Plugins`, `Samples`, `XR`, `XRI`, `*.cs`, `*.asmdef`, `*.shader` 에 해당하지 않음 |

## 결과 요약

| 구분 | 개수 | 용량 |
|---|---:|---:|
| 후보 (UnusedAssets.txt 전체) | 1,540 | 1,233.6 MB |
| 조건4 제외 | 415 | — |
| 조건2 실패 (GUID 참조 있음) | 613 | — |
| 조건3 실패 (이름이 코드에 등장) | 30 | — |
| **4조건 모두 통과** | **481** | **306.05 MB** |
| ↳ 문서류로 판단해 **삭제 제외** | 34 | 0.9 MB |
| ↳ **삭제 제안** | **447** | **305.15 MB** |

## 승인 전에 확인해 주실 것 3가지

4조건은 기계적으로 통과했지만, 기계가 판단할 수 없는 항목입니다. **답을 주시면 그대로 반영합니다.**

1. **B04 — Dev 테스트 씬 3개** (`GongpoTest.unity`, `CutsceneTest.unity`, `InteractionTest.unity`)
   참조는 0이고 빌드 씬도 아니지만, 개발용으로 일부러 남겨 둔 씬일 수 있습니다.
   같은 `Scenes/Dev/`의 `FlowTest`·`FreePlayTest`·`TutorialScene`은 빌드 씬이라 목록에 없고,
   `AiVoiceTest`·`CoreAudioTest`는 조건3에 걸려 이미 제외됐습니다. **기본값: 삭제하지 않음.**

2. **B03 — `RYU/Start/FixedUI/` 49개, 89.4 MB**
   `cleanup_audit.md` 2-0절에서 "시작 화면이 `Start/Rework`로 교체된 흔적"으로 본 그 폴더입니다.
   FixedUI 시작 화면으로 되돌릴 계획이 있으면 보류해야 합니다. **기본값: 삭제.**

3. **문서류 34개 (0.9 MB) 제외** — `@Documents/` 29개, `GazeSystem/*.md` 2개, RYU README/WorkLog 3개.
   `cleanup_audit.md` 4-1절이 "삭제가 아니라 저장소 루트 `docs/`로 이동"으로 판정한 것들입니다.
   이번 라운드는 이동이 금지돼 있어 **그대로 둡니다.** 삭제를 원하시면 말씀해 주십시오.

## 삭제 제안 — 폴더 단위 묶음

`.meta`는 각 파일과 함께 지웁니다. 묶음 순서는 용량 큰 것부터입니다.

| 묶음 | 파일 | 용량 |
|---|---:|---:|
| B03  Assets/@Developers/RYU/Start/FixedUI/ | 49 | 89.39 MB |
| B01  Assets/@GameAssets/warehouse_2.fbx | 1 | 57.15 MB |
| B02  Assets/ADG_Textures/ | 18 | 47.04 MB |
| B05  Assets/warehouseFin/ | 33 | 43.08 MB |
| B06  Assets/UnityTechnologies/ParticlePack/ | 56 | 35.47 MB |
| B07  Assets/VRTemplateAssets/ | 50 | 10.79 MB |
| B10  Assets/@Scenes/ + Assets/Scenes/ | 6 | 7.03 MB |
| B08  Assets/legenooldman/ | 10 | 6.99 MB |
| B09  Assets/WoodFloor004_2K-JPG/ | 5 | 5.59 MB |
| B11  Assets/TextMesh Pro/Examples & Extras/ | 52 | 1.47 MB |
| B18  기타 (Assets 최상위) | 3 | 0.70 MB |
| B16  Assets/tools/ | 12 | 0.17 MB |
| B12  Assets/SkySeries Freebie/ | 19 | 0.16 MB |
| B04  Assets/@Developers/ (그 외) | 3 | 0.10 MB |
| B14  Assets/@GameAssets/ (그 외) | 1 | 0.02 MB |
| B13  Assets/@GameAssets/Sungnyemun/newtexture/ | 112 | 0.00 MB |
| B15  Assets/iumi/ | 17 | 0.00 MB |
| **합계** | **447** | **305.15 MB** |

### B03  Assets/@Developers/RYU/Start/FixedUI/

49개 / 89.39 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 9.95 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Carpet/Carpet012_2K-JPG_NormalDX.jpg` |
| 9.37 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Fabric042_2K-JPG/Fabric042_2K-JPG_NormalDX.jpg` |
| 8.92 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Rubber004_2K-JPG/Rubber004_2K-JPG_NormalGL.jpg` |
| 8.92 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Rubber004_2K-JPG/Rubber004_2K-JPG_NormalDX.jpg` |
| 7.74 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/buttonwood/Wood036_2K-JPG_NormalDX.jpg` |
| 7.56 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/backwood/PaintedWood009C_2K-JPG_NormalDX.jpg` |
| 6.86 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/optionmetal/Metal022_2K-JPG_NormalDX.jpg` |
| 6.09 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Metal029_2K-JPG/Metal029_2K-JPG_NormalDX.jpg` |
| 3.29 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Fabric042_2K-JPG/FixedRoughness.png` |
| 3.29 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Wood095_2K-JPG/Wood095_2K-JPG_NormalGL.jpg` |
| 3.29 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Wood095_2K-JPG/Wood095_2K-JPG_NormalDX.jpg` |
| 2.93 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Rubber004_2K-JPG/Rubber004_2K-JPG_Roughness.jpg` |
| 2.78 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Rubber004_2K-JPG/Rubber004_2K-JPG_Displacement.jpg` |
| 2.42 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Fabric042_2K-JPG/Fabric042_2K-JPG_Roughness.jpg` |
| 2.07 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Carpet/Carpet012_2K-JPG_Roughness.jpg` |
| 1.23 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Wood095_2K-JPG/Wood095_2K-JPG_Displacement.jpg` |
| 1.05 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Wood095_2K-JPG/Wood095_2K-JPG_Roughness.jpg` |
| 0.30 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/backwood/PaintedWood009C.png` |
| 0.28 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/NightSkyHDRI008_2K (1)/NightSkyHDRI008.png` |
| 0.28 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/NightSkyHDRI008.png` |
| 0.28 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Wood095_2K-JPG/Wood095.png` |
| 0.26 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Fabric042_2K-JPG/Fabric042.png` |
| 0.23 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Metal029_2K-JPG/Metal029.png` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/buttonwood/Wood036_2K-JPG.mtlx` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/NightSkyHDRI008_2K (1)/NightSkyHDRI008_2K.usdc` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/backwood/PaintedWood009C_2K-JPG.tres` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Rubber004_2K-JPG/Rubber004_2K-JPG.mtlx` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Fabric042_2K-JPG/Fabric042_2K-JPG.mtlx` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Wood095_2K-JPG/Wood095_2K-JPG.mtlx` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Fabric042_2K-JPG/Fabric042_2K-JPG.tres` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/buttonwood/Wood036_2K-JPG.tres` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Wood095_2K-JPG/Wood095_2K-JPG.tres` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/NightSkyHDRI008_2K (1)/NightSkyHDRI008_2K.tres` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/optionmetal/Metal022_2K-JPG.tres` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Metal029_2K-JPG/Metal029_2K-JPG.tres` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Rubber004_2K-JPG/Rubber004_2K-JPG.tres` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Wood095_2K-JPG/Wood095_2K-JPG.usdc` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/backwood/PaintedWood009C_2K-JPG.usdc` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/optionmetal/Metal022_2K-JPG.usdc` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Wood095_2K-JPG/brightwood.mat` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/buttonwood/Materials/Wood036.mat` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/optionmetal/Materials/Metal022.mat` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Metal029_2K-JPG/Metal029_2K-JPG.usdc` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/optionmetal/Metal022_2K-JPG.mtlx` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Metal029_2K-JPG/Metal029_2K-JPG.mtlx` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/backwood/PaintedWood009C_2K-JPG.mtlx` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Fabric042_2K-JPG/Fabric042_2K-JPG.usdc` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/Rubber004_2K-JPG/Rubber004_2K-JPG.usdc` |
| 0.00 MB | `Assets/@Developers/RYU/Start/FixedUI/meterials/buttonwood/Wood036_2K-JPG.usdc` |

### B01  Assets/@GameAssets/warehouse_2.fbx

1개 / 57.15 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 57.15 MB | `Assets/@GameAssets/warehouse_2.fbx` |

### B02  Assets/ADG_Textures/

18개 / 47.04 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 4.00 MB | `Assets/ADG_Textures/ground_vol1/ground7/ground7_Metallic.tga` |
| 4.00 MB | `Assets/ADG_Textures/ground_vol1/ground6/ground6_Metallic.tga` |
| 4.00 MB | `Assets/ADG_Textures/ground_vol1/ground13/ground13_Metallic.tga` |
| 4.00 MB | `Assets/ADG_Textures/ground_vol1/ground11/ground11_Metallic.tga` |
| 4.00 MB | `Assets/ADG_Textures/ground_vol1/ground5/ground5_Metallic.tga` |
| 4.00 MB | `Assets/ADG_Textures/ground_vol1/ground9/ground9_Metallic.tga` |
| 4.00 MB | `Assets/ADG_Textures/ground_vol1/ground8/ground8_Metallic.tga` |
| 4.00 MB | `Assets/ADG_Textures/ground_vol1/ground12/ground12_Metallic.tga` |
| 4.00 MB | `Assets/ADG_Textures/ground_vol1/ground14/ground14_Metallic.tga` |
| 4.00 MB | `Assets/ADG_Textures/ground_vol1/ground10/ground10_Metallic.tga` |
| 1.00 MB | `Assets/ADG_Textures/ground_vol1/ground2/ground2_Metallic.tga` |
| 1.00 MB | `Assets/ADG_Textures/ground_vol1/ground2/ground2_Height.tga` |
| 1.00 MB | `Assets/ADG_Textures/ground_vol1/ground3/ground3_Height.tga` |
| 1.00 MB | `Assets/ADG_Textures/ground_vol1/ground1/ground1_Metallic.tga` |
| 1.00 MB | `Assets/ADG_Textures/ground_vol1/ground3/ground3_Metallic.tga` |
| 1.00 MB | `Assets/ADG_Textures/ground_vol1/ground4/ground4_Metallic.tga` |
| 1.00 MB | `Assets/ADG_Textures/ground_vol1/ground4/ground4_Height.tga` |
| 0.04 MB | `Assets/ADG_Textures/Demo/DemoScene.unity` |

### B05  Assets/warehouseFin/

33개 / 43.08 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 5.98 MB | `Assets/warehouseFin/textures/Workbench_Normal_OpenGL.png` |
| 5.49 MB | `Assets/warehouseFin/textures/Workbench_Base_Color.png` |
| 4.99 MB | `Assets/warehouseFin/textures/Tools_Composite_Normal_1.png` |
| 4.39 MB | `Assets/warehouseFin/textures/Tools_Composite_Base_Color_1.png` |
| 3.42 MB | `Assets/warehouseFin/textures/woodboardnormal.png` |
| 3.31 MB | `Assets/warehouseFin/textures/woodboard.png` |
| 2.10 MB | `Assets/warehouseFin/textures/Tools_Composite_Roughness_1.png` |
| 1.88 MB | `Assets/warehouseFin/textures/Workbench_Mixed_AO.png` |
| 1.44 MB | `Assets/warehouseFin/textures/Workbench_Roughness.png` |
| 1.41 MB | `Assets/warehouseFin/textures/Tool_Cabinet_Base_Color.png` |
| 1.36 MB | `Assets/warehouseFin/textures/Vice_Normal_OpenGL.png` |
| 1.06 MB | `Assets/warehouseFin/textures/Tools_Composite_Metalic_1.png` |
| 0.88 MB | `Assets/warehouseFin/textures/Tool_Cabinet_Normal_OpenGL.png` |
| 0.78 MB | `Assets/warehouseFin/textures/Tools_Composite_AO_1.png` |
| 0.66 MB | `Assets/warehouseFin/textures/Light_Fixture_Normal_OpenGL.png` |
| 0.56 MB | `Assets/warehouseFin/textures/Tool_Cabinet_Roughness.png` |
| 0.47 MB | `Assets/warehouseFin/textures/Pegboard_Roughness.png` |
| 0.43 MB | `Assets/warehouseFin/textures/Pegboard_Mixed_AO.png` |
| 0.43 MB | `Assets/warehouseFin/textures/Light_Fixture_Roughness.png` |
| 0.40 MB | `Assets/warehouseFin/textures/Vice_Roughness.png` |
| 0.33 MB | `Assets/warehouseFin/textures/Vice_Metallic.png` |
| 0.24 MB | `Assets/warehouseFin/textures/Tool_Cabinet_Metallic.png` |
| 0.23 MB | `Assets/warehouseFin/textures/Light_Fixture_Emissive.png` |
| 0.22 MB | `Assets/warehouseFin/textures/Light_Fixture_Mixed_AO.png` |
| 0.20 MB | `Assets/warehouseFin/textures/Vice_Mixed_AO.png` |
| 0.18 MB | `Assets/warehouseFin/textures/Tool_Cabinet_Mixed_AO.png` |
| 0.10 MB | `Assets/warehouseFin/textures/Light_Fixture_Metallic.png` |
| 0.06 MB | `Assets/warehouseFin/Scenes/SampleScene.unity` |
| 0.05 MB | `Assets/warehouseFin/textures/Pegboard_Metallic.png` |
| 0.02 MB | `Assets/warehouseFin/textures/internal_ground_ao_texture.jpeg` |
| 0.01 MB | `Assets/warehouseFin/textures/Workbench_Metallic.png` |
| 0.00 MB | `Assets/warehouseFin/Models/Procedural_Glass.mat` |
| 0.00 MB | `Assets/warehouseFin/Models/Procedural_RustMetal.mat` |

### B06  Assets/UnityTechnologies/ParticlePack/

56개 / 35.47 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 9.42 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Textures/HexagonPattern_Normal.tif` |
| 5.21 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Textures/Clouds02.png` |
| 3.76 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Smoke & Steam Effects/Textures/WispySmokeNormal.tif` |
| 2.37 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Textures/SmokeySteam.tif` |
| 2.31 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/Lightmap-0_comp_light.exr` |
| 1.90 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles.unity` |
| 1.67 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/Lightmap-1_comp_light.exr` |
| 1.12 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/Lightmap-2_comp_light.exr` |
| 0.83 MB | `Assets/UnityTechnologies/ParticlePack/Shared/Environment/Textures/MetalTrim_MetallicSmooth.tif` |
| 0.82 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Textures/HexagonPattern_Opt.tif` |
| 0.55 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/Lightmap-0_comp_dir.png` |
| 0.43 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Legacy Particles/Prefabs/SparksEffect.prefab` |
| 0.36 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/Lightmap-1_comp_dir.png` |
| 0.34 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Prefabs/DissolveSolidHorizontal.prefab` |
| 0.31 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/ReflectionProbe-0.exr` |
| 0.31 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/ReflectionProbe-2.exr` |
| 0.30 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/ReflectionProbe-9.exr` |
| 0.30 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/ReflectionProbe-5.exr` |
| 0.30 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/ReflectionProbe-3.exr` |
| 0.30 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/ReflectionProbe-6.exr` |
| 0.30 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/ReflectionProbe-4.exr` |
| 0.30 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/ReflectionProbe-7.exr` |
| 0.30 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/ReflectionProbe-1.exr` |
| 0.29 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/ReflectionProbe-8.exr` |
| 0.27 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/Lightmap-2_comp_dir.png` |
| 0.21 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Prefabs/SandSwirlsEffect.prefab` |
| 0.20 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Goop Effects/Prefabs/GoopSprayEffect.prefab` |
| 0.19 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Main Scene with Legacy Particles/ReflectionProbe-10.exr` |
| 0.11 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Smoke & Steam Effects/Prefabs/HeatDistortion.prefab` |
| 0.11 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Legacy Particles/Prefabs/ElectricalSparksEffect.prefab` |
| 0.10 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Goop Effects/Textures/SplatAlbedo.tif` |
| 0.08 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Textures/RoundSoftParticle.tif` |
| 0.04 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Goop Effects/Textures/GoopStreamAlbedo.tif` |
| 0.02 MB | `Assets/UnityTechnologies/ParticlePack/TutorialInfo/Icons/Help_Icon.png` |
| 0.02 MB | `Assets/UnityTechnologies/ParticlePack/Scenes/Profiles/DefaultProfile.asset` |
| 0.02 MB | `Assets/UnityTechnologies/ParticlePack/UniversalRenderPipelineGlobalSettings.asset` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/Shared/Environment/Materials/LinesWhite.mat` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Materials/RockDebris.mat` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Materials/MediumFlame02.mat` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Materials/Flame03.mat` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Materials/WaterStreamParticle.mat` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Prefabs/Readme_Dissolve_Respawn_.txt` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/Main Camera Profile.asset` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/Main Camera Profile(URP).asset` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Materials/Flame02.mat` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/Shared/Ramps/RampBaker.asset` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/Settings/Simple_PipelineAsset.asset` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/Settings/Fast_PipelineAsset.asset` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/Settings/Fastest_PipelineAsset.asset` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Water Effects/Materials/Splash.mat` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/Settings/Beautiful_PipelineAsset.asset` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Materials/GoopSplat2.mat` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Weapon Effects/Materials/BulletDecalWood.mat` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Materials/Flame04.mat` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/Settings/Good_PipelineAsset.asset` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/EffectExamples/Legacy Particles/Materials/SparkParticle.mat` |

### B07  Assets/VRTemplateAssets/

50개 / 10.79 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 2.12 MB | `Assets/VRTemplateAssets/Materials/Environment/Concrete_Albedo.tif` |
| 2.12 MB | `Assets/VRTemplateAssets/Materials/Environment/wall2_Normal.png` |
| 1.72 MB | `Assets/VRTemplateAssets/Materials/Environment/wall_Normal.png` |
| 1.62 MB | `Assets/VRTemplateAssets/Materials/Primitive/torus_Height.png` |
| 1.33 MB | `Assets/VRTemplateAssets/Models/Marks/Marks.fbx` |
| 0.84 MB | `Assets/VRTemplateAssets/Materials/Environment/wall2_Height.png` |
| 0.27 MB | `Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular.ttf` |
| 0.23 MB | `Assets/VRTemplateAssets/Models/Poke/PokePointer.fbx` |
| 0.15 MB | `Assets/VRTemplateAssets/Materials/Environment/wall2_Roughness.png` |
| 0.14 MB | `Assets/VRTemplateAssets/Materials/Environment/wall_Roughness.png` |
| 0.12 MB | `Assets/VRTemplateAssets/Prefabs/Blaster/Confetti.prefab` |
| 0.05 MB | `Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab` |
| 0.02 MB | `Assets/VRTemplateAssets/Sprites/UI/CircleMask.png` |
| 0.02 MB | `Assets/VRTemplateAssets/Materials/Primitive/fabric_Height.png` |
| 0.01 MB | `Assets/VRTemplateAssets/Materials/Primitive/fabric_Normal.png` |
| 0.01 MB | `Assets/VRTemplateAssets/Prefabs/Blink/Blink Visuals.prefab` |
| 0.01 MB | `Assets/VRTemplateAssets/Sprites/UI/Joystick BG.png` |
| 0.01 MB | `Assets/VRTemplateAssets/Materials/Primitive/torus_Normal.png` |
| 0.00 MB | `Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular SDF Overlay Material.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular SDF Overlay Outline Material.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Tutorial/VRTutorialContainer.asset` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Skybox/Hub Skybox Blue 2.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular SDF Material XRay Blue.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Locomotion/BlinkLine.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular SDF Overlay Outline Thick Material.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Primitive/torus_Roughness.png` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Environment/FauxBackgroundBlur.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Primitive/torus_Metallic.png` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Primitive/Sticky.physicMaterial` |
| 0.00 MB | `Assets/VRTemplateAssets/Sprites/Icons/Checkmark.png` |
| 0.00 MB | `Assets/VRTemplateAssets/Shaders/TexturedStableFresnelCommon.cginc` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/UI/Blue.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Tutorial/VRTutorialProjectSettings.asset` |
| 0.00 MB | `Assets/VRTemplateAssets/Themes/BlasterAudioAffordanceTheme.asset` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Primitive/Torus.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Prefabs/Controller/Right Controller.prefab` |
| 0.00 MB | `Assets/VRTemplateAssets/Sprites/UI/Circle_60x60 Outline.png` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Primitive/Cube_Fabric.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Prefabs/Cursors/Torus Cursor.prefab` |
| 0.00 MB | `Assets/VRTemplateAssets/Sprites/UI/Circle_60x60 Outline 4.png` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Locomotion/Angle Indicator.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Locomotion/Blue Standard.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Environment/Grey.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Environment/Dark Green.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Locomotion/Standard White.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Primitive/Green.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Primitive/Interactables 5.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Primitive/Interactables 2.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Prefabs/Controller/Left Controller.prefab` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Primitive/Interactables 3.mat` |

### B10  Assets/@Scenes/ + Assets/Scenes/

6개 / 7.03 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 5.53 MB | `Assets/@Scenes/SampleScene/Lightmap-0_comp_light.exr` |
| 0.84 MB | `Assets/Scenes/SampleScene.unity` |
| 0.27 MB | `Assets/@Scenes/SampleScene/ReflectionProbe-1.exr` |
| 0.25 MB | `Assets/@Scenes/SampleScene/Lightmap-0_comp_dir.png` |
| 0.12 MB | `Assets/@Scenes/SampleScene/ReflectionProbe-0.exr` |
| 0.02 MB | `Assets/@Scenes/SampleScene/Lightmap-0_comp_shadowmask.png` |

### B08  Assets/legenooldman/

10개 / 6.99 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 1.18 MB | `Assets/legenooldman/animation/HumanM@HammeringGround01_R - Stop.anim` |
| 1.12 MB | `Assets/legenooldman/animation/HumanM@HammeringGround01_R - Begin.anim` |
| 1.05 MB | `Assets/legenooldman/animation/HumanM@HammeringGround01_R - Loop 1.anim` |
| 1.01 MB | `Assets/legenooldman/animation/Armature_mixamo.com.anim` |
| 0.96 MB | `Assets/legenooldman/animation/Armature_walking.anim` |
| 0.52 MB | `Assets/legenooldman/animation/Armature_Greet.anim` |
| 0.44 MB | `Assets/legenooldman/animation/Armature_standToCrouch.anim` |
| 0.38 MB | `Assets/legenooldman/animation/Armature_crouchToStand.anim` |
| 0.33 MB | `Assets/legenooldman/animation/Armature_crouchingIdle.anim` |
| 0.00 MB | `Assets/legenooldman/oldman.mat` |

### B09  Assets/WoodFloor004_2K-JPG/

5개 / 5.59 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 5.30 MB | `Assets/WoodFloor004_2K-JPG/WoodFloor004_2K-JPG_NormalDX.jpg` |
| 0.29 MB | `Assets/WoodFloor004_2K-JPG/WoodFloor004.png` |
| 0.00 MB | `Assets/WoodFloor004_2K-JPG/WoodFloor004_2K-JPG.tres` |
| 0.00 MB | `Assets/WoodFloor004_2K-JPG/WoodFloor004_2K-JPG.mtlx` |
| 0.00 MB | `Assets/WoodFloor004_2K-JPG/WoodFloor004_2K-JPG.usdc` |

### B11  Assets/TextMesh Pro/Examples & Extras/

52개 / 1.47 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 0.26 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/18 - ScrollRect & Masking & Layout.unity` |
| 0.26 MB | `Assets/TextMesh Pro/Shaders/TMP_SDF-URP Unlit.shadergraph` |
| 0.09 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/26 - Dropdown Placeholder Example.unity` |
| 0.08 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/24 - Surface Shader Example URP.unity` |
| 0.07 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/24 - Surface Shader Example.unity` |
| 0.06 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/28 - HDRP Shader Example.unity` |
| 0.06 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/20 - Input Field with Scrollbar.unity` |
| 0.04 MB | `Assets/TextMesh Pro/Examples & Extras/Textures/Mask Zig-n-Zag.psd` |
| 0.03 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/16 - Linked text overflow mode example.unity` |
| 0.03 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/05 - Style Tags.unity` |
| 0.03 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/09 - Margin Tag Example.unity` |
| 0.03 MB | `Assets/TextMesh Pro/Examples & Extras/Textures/Wipe Pattern - Radial Quad.psd` |
| 0.03 MB | `Assets/TextMesh Pro/Examples & Extras/Textures/Wipe Pattern - Radial Double.psd` |
| 0.03 MB | `Assets/TextMesh Pro/Examples & Extras/Textures/Wipe Pattern - Diagonal.psd` |
| 0.02 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/25 - Sunny Days Example.unity` |
| 0.02 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/10 - Bullets & Numbered List Example.unity` |
| 0.02 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/15 - Inline Graphics & Sprites.unity` |
| 0.02 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/08 - Improved Text Alignment.unity` |
| 0.02 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/19 - Masking Texture & Soft Mask.unity` |
| 0.02 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/12a - Text Interactions.unity` |
| 0.02 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/03 - Line Justification.unity` |
| 0.02 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/12 - Link Example.unity` |
| 0.02 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/06 - Extra Rich Text Examples.unity` |
| 0.02 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/17 - Old Computer Terminal.unity` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/04 - Word Wrapping.unity` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/01-  Single Line TextMesh Pro.unity` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/07 - Superscript & Subscript Example.unity` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/02 - Multi-line TextMesh Pro.unity` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Prefabs/TextMeshPro - Prefab 2.prefab` |
| 0.01 MB | `Assets/TextMesh Pro/Shaders/TMPro_Mobile.cginc` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/21 - Script Example.unity` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Prefabs/TextMeshPro - Prefab 1.prefab` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Textures/Gradient Vertical (Color).jpg` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/14 - Multi Font & Sprites.unity` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/23 - Animating Vertex Attributes.unity` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/11 - The Style Tag.unity` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/27 - Double Pass Shader Example.unity` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/Benchmark (Floating Text).unity` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold - AFL.txt` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/13 - Soft Hyphenation.unity` |
| 0.01 MB | `Assets/TextMesh Pro/Examples & Extras/Scenes/22 - Basic Scripting Example.unity` |
| 0.00 MB | `Assets/TextMesh Pro/Examples & Extras/Materials/Small Crate_diffuse.mat` |
| 0.00 MB | `Assets/TextMesh Pro/Shaders/TMPro_Surface.cginc` |
| 0.00 MB | `Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc` |
| 0.00 MB | `Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold - License.txt` |
| 0.00 MB | `Assets/TextMesh Pro/Sprites/EmojiOne Attribution.txt` |
| 0.00 MB | `Assets/TextMesh Pro/Examples & Extras/Fonts/Unity - OFL.txt` |
| 0.00 MB | `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt` |
| 0.00 MB | `Assets/TextMesh Pro/Examples & Extras/Materials/Ground - Logo Scene.mat` |
| 0.00 MB | `Assets/TextMesh Pro/Examples & Extras/Fonts/Oswald-Bold - OFL.txt` |
| 0.00 MB | `Assets/TextMesh Pro/Examples & Extras/Fonts/Anton OFL.txt` |
| 0.00 MB | `Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers - OFL.txt` |

### B18  기타 (Assets 최상위)

3개 / 0.70 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 0.35 MB | `Assets/Prefabs/RockingHorseSample.fbx` |
| 0.25 MB | `Assets/Textures/RockingB.bmp` |
| 0.10 MB | `Assets/Prefabs/gongpo_sample.prefab` |

### B16  Assets/tools/

12개 / 0.17 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 0.12 MB | `Assets/tools/자귀배대패/자귀.fbx` |
| 0.05 MB | `Assets/tools/자귀배대패/bellyplane.fbx` |
| 0.00 MB | `Assets/tools/재질_2.058.mat` |
| 0.00 MB | `Assets/tools/재질_2.056.mat` |
| 0.00 MB | `Assets/tools/재질_2.062.mat` |
| 0.00 MB | `Assets/tools/hammer Variant.prefab` |
| 0.00 MB | `Assets/tools/blackline 1.prefab` |
| 0.00 MB | `Assets/tools/재질_2.056 1.mat` |
| 0.00 MB | `Assets/tools/재질_2.063 1.mat` |
| 0.00 MB | `Assets/tools/재질_2.063.mat` |
| 0.00 MB | `Assets/tools/재질_2.058 1.mat` |
| 0.00 MB | `Assets/tools/재질_2.062 1.mat` |

### B12  Assets/SkySeries Freebie/

19개 / 0.16 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 0.02 MB | `Assets/SkySeries Freebie/ExampleScenes/SkyhighFreebieDemo.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/SunlessFreebieDemo.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/AmbienceExposure.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/CosmicCoolCloud.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/DarkStorm.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/MidNightFreebieDemo.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/HighFantasy.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/CasualDay.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/UnEarthlyRed.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/PlanetaryFreebie.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/SundownFreebieDemo.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/DayInTheClouds.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/CloudyMorning.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/Daylightfreebiedemo.unity` |
| 0.01 MB | `Assets/SkySeries Freebie/ExampleScenes/UnderTheSea.unity` |
| 0.00 MB | `Assets/SkySeries Freebie/CloudedSunGlow.mat` |
| 0.00 MB | `Assets/SkySeries Freebie/Hdri setup.txt` |
| 0.00 MB | `Assets/SkySeries Freebie/6sidedCosmicCoolCloud.mat` |
| 0.00 MB | `Assets/SkySeries Freebie/6SidedFluffball.mat` |

### B04  Assets/@Developers/ (그 외)

3개 / 0.10 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 0.04 MB | `Assets/@Developers/RYU/Scenes/Dev/GongpoTest.unity` |
| 0.03 MB | `Assets/@Developers/RYU/Scenes/Dev/CutsceneTest.unity` |
| 0.03 MB | `Assets/@Developers/RYU/Scenes/Dev/InteractionTest.unity` |

### B14  Assets/@GameAssets/ (그 외)

1개 / 0.02 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 0.02 MB | `Assets/@GameAssets/testPart.fbx` |

### B13  Assets/@GameAssets/Sungnyemun/newtexture/

112개 / 0.00 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.064.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.070.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.054.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.047.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.065.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.100.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.074.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.062.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.048.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.030.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.017.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.101.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.023.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.043.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.012.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.024.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.096.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.020.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.051.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.022.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.076.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.067.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.078.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.081.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.058.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.061.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.069.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.099.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.036.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.038.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.034.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.082.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.031.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.029.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.106.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.097.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.053.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.007.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.032.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/Material.001.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.068.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.079.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.042.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.027.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.040.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.039.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.046.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.077.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.041.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.028.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.057.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.052.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.015.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.059.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.105.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.014.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.004.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.044.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.050.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.090.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.002.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.001.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.083.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.005.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.035.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.086.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.049.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.085.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.084.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.009.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.016.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.037.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.006.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.108.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.107.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.026.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.091.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.089.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.088.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.087.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.019.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.013.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.094.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.075.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.018.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/grass.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/Hue Saturation Value.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.011.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.025.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.063.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.010.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.080.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.056.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.021.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.104.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.055.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.071.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.110.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.092.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.045.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.060.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.033.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.003.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.111.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.098.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.073.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.103.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.066.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.102.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/재질_2.072.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/groundstone.mat` |

### B15  Assets/iumi/

17개 / 0.00 MB — 4조건 전부 통과 (C1 미사용 · C2 GUID참조 0 · C3 코드언급 0 · C4 제외폴더 아님)

| 크기 | 경로 |
|---:|---|
| 0.00 MB | `Assets/iumi/iumi_glow_green_soft.mat` |
| 0.00 MB | `Assets/iumi/iumi_glow_green_mint.mat` |
| 0.00 MB | `Assets/iumi/iumi_glow_green_scarf_soft.mat` |
| 0.00 MB | `Assets/iumi/iumi_glow_green_neon.mat` |
| 0.00 MB | `Assets/iumi/New Material 2.mat` |
| 0.00 MB | `Assets/iumi/iumhair/iumi_hair_custom_sage.png` |
| 0.00 MB | `Assets/iumi/iumi_glow_green_scarf_vibrant.mat` |
| 0.00 MB | `Assets/iumi/iumi_glow_green_lime.mat` |
| 0.00 MB | `Assets/iumi/iumi_glow_green_scarf_match.mat` |
| 0.00 MB | `Assets/iumi/iumhair/iumi_hair_custom_sage_high_glow.mat` |
| 0.00 MB | `Assets/iumi/iumhair/iumi_hair_custom_sage_mid_glow.mat` |
| 0.00 MB | `Assets/iumi/iumi_hair_custom_sage_ultra_glow.mat` |
| 0.00 MB | `Assets/iumi/iumhair/iumi_hair_custom_sage_ultra_glow.mat` |
| 0.00 MB | `Assets/iumi/iumhair/iumi_hair_custom_sage.mat` |
| 0.00 MB | `Assets/iumi/iumi_hair_moss_green.mat` |
| 0.00 MB | `Assets/iumi/iumi_hair_custom_sage_mid_glow.mat` |
| 0.00 MB | `Assets/iumi/iumi_hair_custom_sage.mat` |

## 삭제하지 않는 것 — 판단 근거

### A. 문서류 34개 (0.9 MB) — 4조건은 통과하지만 제외

`cleanup_audit.md` 4-1절은 `@Documents/`를 **저장소 루트 `docs/`로 이동**하라고 판정했습니다. 삭제가 아닙니다.
이번 작업은 이동이 금지돼 있으므로, **그대로 두고 다음 라운드로 넘깁니다.** 지우면 기획·이슈 이력이 사라집니다.

| 크기 | 경로 |
|---:|---|
| 0.00 MB | `Assets/@Developers/RYU/Audio/README.md` |
| 0.01 MB | `Assets/@Developers/RYU/ProcessIntegration/Documentation/2026-08-20_CoreLoopIntegration_WorkLog.md` |
| 0.00 MB | `Assets/@Developers/RYU/ProcessIntegration/Tests/README.md` |
| 0.00 MB | `Assets/@Documents/00_문서_개요.md` |
| 0.01 MB | `Assets/@Documents/01_공통_규칙.md` |
| 0.01 MB | `Assets/@Documents/02_화면_흐름_튜토리얼.md` |
| 0.01 MB | `Assets/@Documents/03_오브젝트_부재_가공.md` |
| 0.00 MB | `Assets/@Documents/04_평가_설치_퍼즐.md` |
| 0.00 MB | `Assets/@Documents/05_NPC_AI_음성대화.md` |
| 0.01 MB | `Assets/@Documents/06_UI_저장_사운드_복구.md` |
| 0.00 MB | `Assets/@Documents/07_개발_관리.md` |
| 0.00 MB | `Assets/@Documents/08_개발_로드맵.md` |
| 0.01 MB | `Assets/@Documents/09_UI_종류_및_개발_상태.md` |
| 0.60 MB | `Assets/@Documents/Overview.pdf` |
| 0.00 MB | `Assets/@Documents/walkthrough.md` |
| 0.11 MB | `Assets/@Documents/대사/062e012e5a10bfc1.jpg` |
| 0.00 MB | `Assets/@Documents/목재가공_도구_구현_테스크.md` |
| 0.01 MB | `Assets/@Documents/목재가공_도구_구현플랜.md` |
| 0.00 MB | `Assets/@Documents/이슈_로그/00_기록_규칙.md` |
| 0.01 MB | `Assets/@Documents/이슈_로그/2026-08-02_대사_시스템_1차.md` |
| 0.01 MB | `Assets/@Documents/이슈_로그/2026-08-02_일시정지_옵션.md` |
| 0.01 MB | `Assets/@Documents/이슈_로그/2026-08-02_컷씬_시스템_2차.md` |
| 0.01 MB | `Assets/@Documents/이슈_로그/2026-08-02_흐름_저장_메인화면.md` |
| 0.01 MB | `Assets/@Documents/이슈_로그/2026-08-08_오디오_모듈_검증.md` |
| 0.00 MB | `Assets/@Documents/이슈_로그/2026-08-08_이슈_정리.md` |
| 0.01 MB | `Assets/@Documents/이슈_로그/2026-08-12_대사_TTS_볼륨_버스.md` |
| 0.01 MB | `Assets/@Documents/이슈_로그/2026-08-12_대사_데이터_영상_컷씬.md` |
| 0.00 MB | `Assets/@Documents/이슈_로그/2026-08-12_싱글턴_종료_예외.md` |
| 0.01 MB | `Assets/@Documents/이슈_로그/2026-08-12_영상_볼륨_옵션.md` |
| 0.00 MB | `Assets/@Documents/이슈_로그/2026-08-21_월드공간_일시정지_HUD.md` |
| 0.01 MB | `Assets/@Documents/이슈_로그/2026-08-24_퀘스트_목표_분할_초안_대사.md` |
| 0.01 MB | `Assets/@Documents/이슈_로그/2026-08-26_공정_이식_UI_개편.md` |
| 0.01 MB | `Assets/GazeSystem/GazeSystem_Manual.md` |
| 0.01 MB | `Assets/GazeSystem/NPC_Library_Manual.md` |

### B. 조건2 실패 — 미사용 파일끼리만 참조 (611개, 908.0 MB)

규칙대로 **삭제하지 않습니다.** 다만 성격을 정확히 적어 둡니다: 이들은 *사용 중이라서* 남는 게 아니라,
**같은 폴더 안의 다른 미사용 파일이 참조하고 있어서** GUID 검색이 0이 아닌 것입니다.
(예: `SkySeries Freebie/*.mat`이 `FreebieHdri/*.hdr`을 참조 — 그 `.mat` 자체도 미사용)
폴더 단위로 통째 삭제한다면 함께 사라지는 것들이라, **별도 승인 항목**으로 남깁니다.

| 폴더 | 파일 | 용량 |
|---|---:|---:|
| `SkySeries Freebie/FreebieHdri` | 28 | 365.0 MB |
| `ADG_Textures/ground_vol1` | 63 | 317.0 MB |
| `UnityTechnologies/ParticlePack` | 332 | 145.8 MB |
| `VRTemplateAssets/Materials` | 47 | 38.1 MB |
| `@Developers/RYU` | 3 | 14.9 MB |
| `@GameAssets/Sungnyemun` | 3 | 6.8 MB |
| `WoodFloor004_2K-JPG/WoodFloor004_2K-JPG_NormalGL.jpg` | 1 | 5.3 MB |
| `WoodFloor004_2K-JPG/WoodFloor004_2K-JPG_Color.jpg` | 1 | 3.4 MB |
| `VRTemplateAssets/Sprites` | 23 | 2.6 MB |
| `hammer/woodtooltexture1.png` | 1 | 2.4 MB |
| `VRTemplateAssets/Models` | 14 | 1.7 MB |
| `VRTemplateAssets/Fonts` | 2 | 1.1 MB |
| `WoodFloor004_2K-JPG/WoodFloor004_2K-JPG_Displacement.jpg` | 1 | 1.1 MB |
| `VRTemplateAssets/Prefabs` | 24 | 0.7 MB |

### C. 조건2 실패 — 실제 사용 중인 파일이 참조 (2개)

미사용 목록에 올랐지만 **살아 있는 파일이 참조**합니다. `cleanup_audit.md` 1-14절이 경고한 오탐입니다.

| 경로 | 참조처 |
|---|---|
| `Assets/UnityTechnologies/ParticlePack/URP.asset` | `ProjectSettings/GraphicsSettings.asset` |
| `Assets/TextMesh Pro/Examples & Extras/Fonts/Unity.ttf` | `Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Unity SDF.asset` |

### D. 조건3 실패 — 이름/경로가 코드 문자열에 등장 (30개)

| 크기 | 경로 |
|---:|---|
| 2.47 MB | `Assets/tools/darkwood.png` |
| 0.62 MB | `Assets/options.fbx` |
| 0.18 MB | `Assets/@Documents/대사/chapter1.jpg` |
| 0.13 MB | `Assets/tools/blackline.fbx` |
| 0.12 MB | `Assets/@Documents/대사/chapter2_.jpeg` |
| 0.06 MB | `Assets/wood.unity` |
| 0.02 MB | `Assets/QuickOutline/Samples/Scenes/QuickOutline.unity` |
| 0.02 MB | `Assets/Prefabs/Player.prefab` |
| 0.02 MB | `Assets/@Developers/RYU/UI/PauseHud.prefab` |
| 0.02 MB | `Assets/@Developers/RYU/Scenes/Dev/AiVoiceTest.unity` |
| 0.01 MB | `Assets/tools/chisel_low.prefab` |
| 0.01 MB | `Assets/@Documents/DataSystem.md` |
| 0.01 MB | `Assets/TutorialInfo/Layout.wlt` |
| 0.01 MB | `Assets/@Developers/RYU/Scenes/Dev/CoreAudioTest.unity` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Controller/White.mat` |
| 0.00 MB | `Assets/@Developers/RYU/ProcessIntegration/Tests/CoreLoopVerificationProfile.json` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Controller/Controller.mat` |
| 0.00 MB | `Assets/TextMesh Pro/Shaders/TMPro.cginc` |
| 0.00 MB | `Assets/QuickOutline/Readme.txt` |
| 0.00 MB | `Assets/VRTemplateAssets/Sprites/Icons/Forward.png` |
| 0.00 MB | `Assets/UnityTechnologies/ParticlePack/Readme.asset` |
| 0.00 MB | `Assets/Readme.asset` |
| 0.00 MB | `Assets/TextMesh Pro/Sprites/EmojiOne.json` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Primitive/Interactables.mat` |
| 0.00 MB | `Assets/@Scenes/BasicScene/Grid.mat` |
| 0.00 MB | `Assets/WoodFloor004_2K-JPG/test.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Environment/Chrome.mat` |
| 0.00 MB | `Assets/meterials/Fill.mat` |
| 0.00 MB | `Assets/@GameAssets/Sungnyemun/newtexture/Material.mat` |
| 0.00 MB | `Assets/VRTemplateAssets/Materials/Environment/Concrete.mat` |

### E. 조건4 제외 (415개)

| 사유 | 개수 |
|---|---:|
| Samples | 219 |
| Plugins | 149 |
| AddressableAssetsData | 13 |
| *.shader | 12 |
| Assets/Settings | 12 |
| XR | 8 |
| XRI | 1 |
| *.inputactions | 1 |

