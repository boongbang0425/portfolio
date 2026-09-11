# 사후 검증 (1차 구조 정리 후)

측정: 2026-09-11 10:44–10:46 · 기준선(`docs/verify_before.md`)과 **동일한 방법**
Unity: `C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe`

---

## 결론

> **기준선 대비 새로 생긴 에러: 0건.**

| 지표 | 기준선 | 사후 | 차이 |
|---|---:|---:|---:|
| 컴파일 에러 (`error CS`) | 0 | **0** | — |
| EditMode 테스트 전체 | 10 | **10** | — |
| 테스트 통과 | 9 | **9** | — |
| 테스트 실패 | 1 | **1** | — |
| 연 빌드 씬 | 9 | **9** | — |
| Missing Script | 1 | **1** | — |
| Missing Prefab | 0 | **0** | — |
| 씬 콘솔 에러 | 0 | **0** | — |
| 프리팹 에셋 내 Missing Script | 0 | **0** | — |

### 새로 생긴 에러 표

| # | 항목 | 내용 |
|---|---|---|
| — | — | **없음** |

기존 실패 2건은 기준선과 **완전히 동일한 항목**이며, 이번 작업으로 생긴 것이 아닙니다.

| 항목 | 기준선 | 사후 | 판정 |
|---|---|---|---|
| `CoreLoopContractTests.TutorialOutlineGuide_CoversEveryObjectInteractionStep` | 실패 (`Sequence contains no matching element`) | 실패 (동일 메시지) | 변동 없음 |
| `MainPlayScene.unity` / `planer` Missing Script | 1건 | 1건 (같은 오브젝트) | 변동 없음 |

---

## 1. 컴파일 에러

```
"...\6000.3.9f1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath IUM ^
  -executeMethod ProjectDiagnostics.Run -diagOut Diagnostics_after.txt -logFile after_diag.log
```

`error CS` **0건**, 종료 코드 0.

문자열 경로 19줄을 고친 뒤에도 컴파일이 깨끗합니다.

## 2. EditMode 테스트

```
"...\Unity.exe" -batchmode -nographics -projectPath IUM ^
  -runTests -testPlatform EditMode -testResults tests_after.xml -logFile after_tests.log
```

| 항목 | 기준선 | 사후 |
|---|---:|---:|
| 전체 | 10 | 10 |
| 통과 | 9 | 9 |
| 실패 | 1 | 1 |

`CoreLoopContractTests`는 `@Data`·`ThirdParty/QuickOutline`로 바뀐 경로를 전부 정상적으로 찾았습니다.
경로 수정이 누락됐다면 `FileNotFound` 계열로 추가 실패가 났을 텐데, 실패 수가 그대로 1건입니다.

## 3. 빌드 씬 진단

| 씬 | GameObject | MissingScript | MissingPrefab | 에러 |
|---|---:|---:|---:|---:|
| `Assets/@Scenes/StartScene.unity` | 83 | 0 | 0 | 0 |
| `…/RYU/Scenes/Cutscene/Cutscene_Prologue.unity` | 14 | 0 | 0 | 0 |
| `…/RYU/Scenes/Cutscene/Cutscene_Ending.unity` | 11 | 0 | 0 | 0 |
| `…/RYU/Scenes/Dev/FlowTest.unity` | 11 | 0 | 0 | 0 |
| `…/RYU/Scenes/Dev/FreePlayTest.unity` | 19 | 0 | 0 | 0 |
| `…/RYU/Scenes/Dev/TutorialScene.unity` | 25 | 0 | 0 | 0 |
| `…/RYU/Scenes/Main/MainPlayScene.unity` | 224 | 1 | 0 | 0 |
| `Assets/@Scenes/Play.unity` | 1,281 | 0 | 0 | 0 |
| `…/RYU/Scenes/Cutscene_SungnyemunBuild.unity` | 2 | 0 | 0 | 0 |

GameObject 수가 씬마다 기준선과 **1개도 다르지 않습니다.** `AssetDatabase.MoveAsset`으로만 옮겨
GUID가 보존됐다는 실증입니다. 273개 아트 에셋이 `@Art/` 아래로 옮겨갔는데 `Play.unity`(1,281 오브젝트)에
Missing이 하나도 없습니다.

---

## 4. 작업 중 발생한 문제와 처리

### 4-1. AssetMover 1차 실행 실패 → 수정 후 재실행

첫 실행에서 52건이 `Cannot move asset ...: Parent directory is not in asset database`로 실패했습니다.

**원인**: `AssetDatabase.StartAssetEditing()` 배치 안에서 `CreateFolder`로 만든 폴더가
AssetDatabase에 즉시 등록되지 않아, 뒤따르는 `ValidateMoveAsset`이 부모 폴더를 못 찾았습니다.
게다가 `CreateFolder`는 대상이 이미 디스크에 있으면 `"@Art 1"`처럼 **이름 뒤에 번호를 붙여 새로 만듭니다.**
그 결과 `@Art 1`~`@Art 25`, `@Prefabs 1`~`8`, `ThirdParty 1`~`5` 등 **빈 중복 폴더 82개**가 생겼습니다.

**수정**: 배치 래퍼를 제거하고 폴더 생성을 **Phase 1**으로 분리한 뒤 `AssetDatabase.Refresh()`를 호출하고,
**Phase 2**에서 이동하도록 `AssetMover.cs`를 고쳤습니다.

**재실행 결과**: `moved=52 skipped=7 failed=0` (skipped 7건은 1차에서 이미 옮겨진 것).
중복 폴더 82개는 전부 비어 있어 후속 정리에서 제거했습니다. **에셋 손실 0건** — 위 GameObject 수가 증거입니다.

### 4-2. `Prologue_1.mp4.meta` / `Prologue_2.mp4.meta` — 유지하지 못함

지시는 "짝 없는 `.meta` 삭제. 단 Prologue 2개는 유지"였는데, **유지하지 못했습니다.**

**원인**: 0번 기준선 측정에서 Unity를 처음 열 때(`Library/` 없는 상태의 전체 임포트) Unity가
본체 없는 `.meta`를 자동으로 정리했습니다. 로그에 남아 있습니다.

```
A meta data file (.meta) exists but its asset 'Assets/StreamingAssets/Prologue_1.mp4'
can't be found. When moving or deleting files outside of Unity, please ensure that the
corresponding .meta file is moved or deleted along with it.
```

즉 **제 정리 단계가 실행되기 전에 이미 사라져 있었고**, 실제로 2번 단계의 짝 없는 `.meta` 스캔은 0건을 보고했습니다.
`git 명령 실행 금지` 지시가 있어 이력에서 복원하지 않았습니다.

**영향**: 기능상 없습니다. `StreamingAssets`의 파일은 GUID가 아니라 파일명 문자열로 읽히고
(`cutscene.json:14`의 `"videos": ["Prologue_1.mp4","Prologue_2.mp4"]`), 이 두 GUID를 참조하는 에셋은 없었습니다.
다만 **mp4가 빠져 있다는 표식**은 사라졌습니다. `cleanup_audit.md` 6-1절의 프롤로그 영상 미복원 문제는 그대로입니다.

### 4-3. `Assets/StreamingAssets~` (빈 폴더)

첫 AssetMover 실행 후 나타났고, 정리해도 Unity를 다시 열면 재생성됩니다. 파일 0개이며
**이름이 `~`로 끝나 Unity가 임포트 대상에서 제외**하므로 무해합니다. 그대로 뒀습니다.

### 4-4. `Assets/iumi.fbx` (7.09 MB) — 루트에 남음

이동표에 넣지 않았습니다. `@Art/Characters/Ieumi/iumi.fbx`와 **바이트 동일한 중복**인데
파일명이 같아 그대로 옮기면 충돌하고, 이름 변경은 이번 범위 밖입니다.
`cleanup_audit.md` 6-3절대로 `iumi프리펩.prefab`이 두 사본을 모두 참조하므로,
프리팹 슬롯을 정리한 뒤 처리해야 합니다. **삭제·이동하지 않았습니다.**

### 4-5. `Assets/Editor` 존치

`MeasureBounds.cs`·`TriplanarMaterialGenerator.cs`는 `@Scripts/Editor/`로 옮겼고,
작업용 도구 3개(`AssetMover.cs`, `ProjectDiagnostics.cs`, `UnusedAssetReport.cs`)만 남겼습니다.
정리가 끝나면 `@Scripts/Editor/`로 합치거나 삭제하십시오.
