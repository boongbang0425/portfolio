# 용량 리포트 (삭제 후 · 보고 전용)

작성: 2026-09-11 09:53:48

분류 방침: **외부 에셋은 `.gitignore`로 제외, 팀 제작 아트는 Git LFS로 업로드**를 전제로 나눴습니다.

## 총계

| 구분 | 파일 | 용량 |
|---|---:|---:|
| 팀 제작 아트 (LFS 대상) | 357 | 554.2 MB |
| 외부 에셋 (gitignore 대상) | 582 | 192.0 MB |
| 코드·텍스트 | 270 | 2.2 MB |
| 프로젝트 설정 에셋 | 27 | 1.1 MB |
| `.meta` | 1593 | 1.2 MB |
| **Assets 합계** | **2829** | **750.7 MB** |

## 1. 외부 에셋 — 폴더와 추정 출처

| 폴더 | 용량 | 파일 | 추정 출처 |
|---|---:|---:|---|
| `Assets/@Developers` | 132.8 MB | 40 | ambientCG 텍스처 세트 (CC0) |
| `Assets/Samples` | 14.3 MB | 263 | Package Manager 샘플 (XR Interaction Toolkit 3.4.1, XR Hands 1.7.3) |
| `Assets/Original Wood Textures` | 13.4 MB | 5 | Asset Store — Original Wood Textures 추정 |
| `Assets/ADG_Textures` | 9.0 MB | 5 | Asset Store — ADG Ground Textures vol.1 |
| `Assets/SkySeries Freebie` | 8.0 MB | 7 | Asset Store — SkySeries Freebie (무료 HDRI 스카이박스) |
| `Assets/UnityTechnologies` | 7.5 MB | 28 | Asset Store — Unity Technologies Particle Pack (무료) |
| `Assets/TextMesh Pro` | 5.7 MB | 33 | Unity 내장 패키지 리소스 (TMP Essentials + Examples) |
| `Assets/Plugins` | 1.2 MB | 159 | Asset Store — DOTween / DOTween Pro (**유료**) |
| `Assets/VRTemplateAssets` | 0.2 MB | 21 | Unity VR Template 프로젝트 템플릿 잔재 |
| `Assets/XR` | 0.1 MB | 10 | XR 패키지 자동 생성 |
| `Assets/QuickOutline` | 0.0 MB | 6 | Asset Store — QuickOutline (무료) |
| `Assets/XRI` | 0.0 MB | 4 | XR Interaction Toolkit 자동 생성 |
| `Assets/Readme.asset` | 0.0 MB | 1 | Unity 3D 템플릿 잔재 |
| **소계** | **192.0 MB** | 582 | |

> `Plugins/Demigiant`(DOTween Pro)는 **유료 에셋**입니다. 공개 저장소에 원본을 두면 재배포에 해당합니다 (`cleanup_audit.md` 6-9절).

## 2. 팀 제작 아트 — 확장자별 (LFS 대상)

| 확장자 | 파일 | 용량 |
|---|---:|---:|
| `.png` | 86 | 346.90 MB |
| `.fbx` | 32 | 100.80 MB |
| `.mov` | 1 | 75.56 MB |
| `.asset` | 15 | 14.81 MB |
| `.webm` | 1 | 4.11 MB |
| `.unity` | 17 | 3.65 MB |
| `.exr` | 2 | 2.29 MB |
| `.anim` | 3 | 1.99 MB |
| `.ttf` | 1 | 1.19 MB |
| `.prefab` | 23 | 1.12 MB |
| `.mat` | 163 | 0.60 MB |
| `.pdf` | 1 | 0.60 MB |
| `.jpg` | 2 | 0.29 MB |
| `.mp3` | 6 | 0.15 MB |
| `.jpeg` | 1 | 0.12 MB |
| `.controller` | 2 | 0.02 MB |
| `.lighting` | 1 | 0.00 MB |
| **소계** | **357** | **554.2 MB** |

폴더별로도 봅니다.

| 폴더 | 파일 | 용량 |
|---|---:|---:|
| `Assets/@GameAssets` | 59 | 221.65 MB |
| `Assets/warehouseFin` | 152 | 117.59 MB |
| `Assets/StreamingAssets` | 2 | 79.67 MB |
| `Assets/legenooldman` | 8 | 45.67 MB |
| `Assets/@Developers` | 58 | 28.03 MB |
| `Assets/iumi` | 21 | 20.93 MB |
| `Assets/Prefabs` | 5 | 17.82 MB |
| `Assets/tools` | 23 | 11.32 MB |
| `Assets/iumi.fbx` | 1 | 7.09 MB |
| `Assets/@Scenes` | 5 | 2.67 MB |
| `Assets/@Documents` | 4 | 1.00 MB |
| `Assets/options.fbx` | 1 | 0.62 MB |
| `Assets/wood.unity` | 1 | 0.06 MB |
| `Assets/hammer.fbx` | 1 | 0.03 MB |
| `Assets/Textures` | 5 | 0.02 MB |
| `Assets/DefaultVolumeProfile.asset` | 1 | 0.02 MB |
| `Assets/@UI` | 9 | 0.01 MB |
| `Assets/Resources` | 1 | 0.00 MB |

## 3. 코드·텍스트

| 확장자 | 파일 | 용량 |
|---|---:|---:|
| `.cs` | 207 | 1.80 MB |
| `.md` | 34 | 0.22 MB |
| `.json` | 12 | 0.10 MB |
| `.inputactions` | 1 | 0.04 MB |
| `.uss` | 6 | 0.02 MB |
| `.shader` | 2 | 0.02 MB |
| `.uxml` | 6 | 0.01 MB |
| `.asmdef` | 1 | 0.00 MB |
| `.tss` | 1 | 0.00 MB |
| **소계** | **270** | **2.22 MB** |

## 4. 50 MB 초과 파일

| 크기 | 분류 | 경로 |
|---:|---|---|
| 75.6 MB | 팀아트 | `Assets/StreamingAssets/devcontest.mov` |

## 5. 사용 중이면서 4K를 초과하는 텍스처

판정: `UnusedAssets.txt`에 없고(=사용 중) 가로·세로 중 **하나라도 4096을 넘는** 이미지.
헤더를 직접 읽어 해상도를 구했습니다 (png/jpg/tga/tif/hdr/exr/psd/bmp).

**없습니다.** 사용 중인 텍스처 중 4096을 넘는 것은 발견되지 않았습니다.

참고 — 사용 중이고 최대변이 2048 초과 4096 이하인 텍스처: **23장 / 226.9 MB**

| 해상도 | 용량 | 경로 |
|---|---:|---|
| 4096 x 4096 | 24.19 MB | `Assets/@GameAssets/Sungnyemun/texture/1floor_type1.fbx.png` |
| 4096 x 4096 | 22.41 MB | `Assets/@GameAssets/Sungnyemun/texture/1floor_type2.fbx.png` |
| 4096 x 4096 | 19.34 MB | `Assets/@GameAssets/Sungnyemun/texture/2floor_type1.fbx.png` |
| 4096 x 4096 | 19.01 MB | `Assets/@GameAssets/Sungnyemun/texture/wall.fbx.png` |
| 4096 x 4096 | 17.45 MB | `Assets/@GameAssets/Sungnyemun/texture/1floor_type3.fbx.png` |
| 4096 x 4096 | 17.21 MB | `Assets/@GameAssets/Sungnyemun/texture/1floor_hat.fbx.png` |
| 4096 x 4096 | 16.71 MB | `Assets/legenooldman/diffuse.png` |
| 4096 x 4096 | 15.81 MB | `Assets/@GameAssets/Sungnyemun/texture/2floor_hat.png` |
| 2803 x 4096 | 12.22 MB | `Assets/@GameAssets/Sungnyemun/texture/stone_stair_and_cover.fbx 1.png` |
| 4096 x 4096 | 11.50 MB | `Assets/@GameAssets/Sungnyemun/texture/bush_stair.fbx.png` |
| 4096 x 4096 | 9.51 MB | `Assets/@GameAssets/Sungnyemun/texture/1floor.fbx.png` |
| 4096 x 4096 | 7.47 MB | `Assets/@GameAssets/Sungnyemun/texture/bush.fbx.png` |
| 2103 x 2207 | 6.14 MB | `Assets/@GameAssets/Sungnyemun/texture/성벽.png` |
| 4096 x 4096 | 5.87 MB | `Assets/@GameAssets/Sungnyemun/texture/2floor.fbx.png` |
| 4056 x 4056 | 5.17 MB | `Assets/iumi/ScrollR.png` |
| 4096 x 4096 | 5.00 MB | `Assets/@GameAssets/Sungnyemun/texture/1floor_pillar.fbx.png` |
| 4096 x 4096 | 3.15 MB | `Assets/@GameAssets/Sungnyemun/texture/1floor_bar.fbx.png` |
| 4056 x 4056 | 3.03 MB | `Assets/iumi/bbbb.png` |
| 4096 x 4096 | 2.77 MB | `Assets/@GameAssets/Sungnyemun/texture/1floor_type5.fbx.png` |
| 4056 x 4056 | 2.77 MB | `Assets/iumi/Object_14.png` |

## 6. 2차 삭제 반영 — 확인 항목 3가지

### 6-1. `StreamingAssets/devcontest.mov` (75.6 MB) — 참조 0건

`Assets/`와 `ProjectSettings/`의 `*.cs`,`*.json`,`*.unity`,`*.prefab`,`*.asset`,`*.uxml` 전체에서
`devcontest` 문자열과 `.mov` 확장자를 찾았습니다. **참조가 하나도 없습니다.** `.meta`도 없습니다.

단서 하나: `@AddressableAssets/Data/Static/cutscene.json:12` 주석에
"실제 프롤로그 영상(H.264 960x720, 총 3분 1초, **드라이브 원본 mov를 무손실 리먹싱**)" 이라고 적혀 있습니다.
`devcontest.mov`가 그 **원본 mov**일 가능성이 큽니다. 즉 결과물(`Prologue_1/2.mp4`)은 빠지고 원본만 들어온 상태입니다.

| 파일 | 상태 |
|---|---|
| `Prologue_1.mp4`, `Prologue_2.mp4` | **없음** (`.meta`만 존재). `cutscene.json:14`가 참조 → 프롤로그 재생 실패 |
| `devcontest.mov` 75.6 MB | 존재하나 **아무 데서도 참조 안 함** |
| `OnboardingVideoVRT.webm` | 존재하고 `cutscene.json:51`, `StartMonitorPrologue.cs:65`에서 참조 |

판단: 리포지토리에서 빼고(`.gitignore`) 원본은 외부 보관, 배포본 `Prologue_1/2.mp4`를 복원하는 것이 맞습니다.

### 6-2. 4096 텍스처 22장의 Max Size — **이미 2048로 제한되어 있음**

`.meta`의 `platformSettings`를 전수 파싱했습니다.

| 항목 | 값 |
|---|---|
| 대상 | 사용 중이고 최대변 4000 이상인 텍스처 **22장 / 220.8 MB** |
| `DefaultTexturePlatform` maxTextureSize | **2048** (22장 전부 동일) |
| `Android` 플랫폼 오버라이드 | **블록 자체가 없음 — 0/22** |
| 존재하는 플랫폼 블록 | `DefaultTexturePlatform`, `Standalone` 둘뿐 |
| textureCompression / format | `1`(Normal Quality) / `-1`(Auto) |

**중요한 함의**: 빌드 타깃이 Android(Quest 3)인데 Android 오버라이드가 없으므로 Default가 그대로 적용됩니다.
즉 **런타임·APK에는 이미 2048로 줄여서 들어가고 있습니다.**
4096 원본을 줄이는 작업은 **런타임 성능·APK 용량에는 아무 효과가 없고, 저장소/LFS 용량만 줄입니다.**
`cleanup_audit.md` 2-3절의 "4K 이하로 축소" 권고는 이 맥락으로 한정해 읽어야 합니다.

별도 제안(이번 범위 밖): Quest 3라면 Android 오버라이드를 명시하고 ASTC 6x6 정도를 지정하는 편이
Auto 선택에 맡기는 것보다 예측 가능합니다.

| 해상도 | 용량 | Default | Android | 경로 |
|---|---:|---:|---|---|
| 4096x4096 | 24.19 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/1floor_type1.fbx.png` |
| 4096x4096 | 22.41 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/1floor_type2.fbx.png` |
| 4096x4096 | 19.34 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/2floor_type1.fbx.png` |
| 4096x4096 | 19.01 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/wall.fbx.png` |
| 4096x4096 | 17.45 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/1floor_type3.fbx.png` |
| 4096x4096 | 17.21 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/1floor_hat.fbx.png` |
| 4096x4096 | 16.71 MB | 2048 | 오버라이드 없음 | `Assets/legenooldman/diffuse.png` |
| 4096x4096 | 15.81 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/2floor_hat.png` |
| 2803x4096 | 12.22 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/stone_stair_and_cover.fbx 1.png` |
| 4096x4096 | 11.50 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/bush_stair.fbx.png` |
| 4096x4096 | 9.51 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/1floor.fbx.png` |
| 4096x4096 | 7.47 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/bush.fbx.png` |
| 4096x4096 | 5.87 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/2floor.fbx.png` |
| 4056x4056 | 5.17 MB | 2048 | 오버라이드 없음 | `Assets/iumi/ScrollR.png` |
| 4096x4096 | 5.00 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/1floor_pillar.fbx.png` |
| 4096x4096 | 3.15 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/1floor_bar.fbx.png` |
| 4056x4056 | 3.03 MB | 2048 | 오버라이드 없음 | `Assets/iumi/bbbb.png` |
| 4056x4056 | 2.77 MB | 2048 | 오버라이드 없음 | `Assets/iumi/Object_14.png` |
| 4096x4096 | 2.77 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/1floor_type5.fbx.png` |
| 4096x4096 | 0.08 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/top.fbx 2.png` |
| 4096x4096 | 0.07 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/tunnel.fbx.png` |
| 4096x4096 | 0.07 MB | 2048 | 오버라이드 없음 | `Assets/@GameAssets/Sungnyemun/texture/Granite.fbx.png` |

### 6-3. 삭제 후 용량 재계산

| 구분 | 1차 후 | **2차 후** | 감소 |
|---|---:|---:|---:|
| 외부 에셋 (gitignore 대상) | 1,079.3 MB | **192.0 MB** | −887.3 MB |
| 팀 제작 아트 (LFS 대상) | 556.6 MB | **554.2 MB** | −2.4 MB |
| 코드·텍스트 | 2.2 MB | 2.2 MB | — |
| 프로젝트 설정 에셋 | 1.1 MB | 1.1 MB | — |
| `.meta` | 2.1 MB | 1.2 MB | −0.9 MB |
| **Assets 합계** | 1,641.2 MB | **750.7 MB** | **−890.5 MB** |

2차 삭제가 거의 전부 외부 에셋에서 나왔습니다. 팀 제작 아트는 `hammer/` 2.4 MB만 줄었습니다.

남은 외부 에셋 192.0 MB 중 **132.8 MB가 `@Developers/RYU/Start/FixedUI/meterials/`의 ambientCG 세트**입니다.
이건 팀 폴더 안에 섞여 있고 `StartScene.unity`가 실제로 참조하므로 삭제 대상이 아니며,
`.gitignore` 경로도 폴더 단위로 깔끔하게 잡히지 않습니다 (`repo_setup_plan.md` 참조).

팀 제작 아트 554.2 MB의 내역:

| 항목 | 용량 | 비고 |
|---|---:|---|
| `devcontest.mov` | 75.6 MB | 참조 0 — 저장소에서 빼는 것을 권장 |
| 4096 텍스처 22장 | 220.8 MB | 전부 사용 중. 2048로 줄이면 약 55 MB |
| 나머지 (fbx, 기타 텍스처, 씬, 프리팹) | 약 258 MB | |

위 둘을 처리하면 LFS 대상이 **554.2 MB → 약 313 MB**가 됩니다.

