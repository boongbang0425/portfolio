# 기준선 측정 (변경 전)

측정: 2026-09-11 10:33–10:34
Unity: `C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe` (프로젝트 버전과 일치)
프로젝트: `xr-contest-ieum/IUM` · `Assets/` 750.7 MB / 2,829 파일
`Library/`가 없던 상태에서 전체 재임포트 후 측정했습니다.

---

## 1. 컴파일 에러

```
"C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" -batchmode -quit -nographics ^
  -projectPath IUM -executeMethod ProjectDiagnostics.Run -diagOut Diagnostics_before.txt -logFile before_diag.log
```

| 항목 | 값 |
|---|---:|
| `error CS` 발생 수 | **0** |
| 배치모드 종료 코드 | 0 |

## 2. EditMode 테스트

```
"...\Unity.exe" -batchmode -nographics -projectPath IUM ^
  -runTests -testPlatform EditMode -testResults tests_before.xml -logFile before_tests.log
```
`-quit`는 함께 쓰지 않았습니다.

| 항목 | 값 |
|---|---:|
| 전체 | 10 |
| 통과 | **9** |
| 실패 | **1** |
| 건너뜀 / 미결정 | 0 / 0 |
| 소요 | 0.255초 |

### 기준선에서 이미 실패 중인 테스트 1건

| 테스트 | 메시지 |
|---|---|
| `IUM.CoreLoopVerification.Tests.CoreLoopContractTests.TutorialOutlineGuide_CoversEveryObjectInteractionStep` | `System.InvalidOperationException : Sequence contains no matching element` |

**이 실패는 이번 작업 이전부터 존재합니다.** 사후 검증에서 "새로 생긴 에러"로 세지 않습니다.

## 3. 빌드 씬 진단

`Assets/Editor/ProjectDiagnostics.cs`를 새로 만들어 실행했습니다.
빌드 씬을 `OpenScene(Single)`로 하나씩 열어 컴포넌트 `null`(Missing Script),
`PrefabInstanceStatus.MissingAsset`(Missing Prefab), `LogType.Error/Exception/Assert`를 셌습니다.
프로젝트 전체 프리팹 에셋의 Missing Script도 따로 셌습니다.

| 항목 | 값 |
|---|---:|
| 빌드 씬(등록/활성) | 9 / 9 |
| 연 씬 | **9** |
| **Missing Script** | **1** |
| **Missing Prefab** | **0** |
| **콘솔 에러** | **0** |
| 프리팹 에셋 내 Missing Script | 0 |

### 씬별

| 씬 | GameObject | MissingScript | MissingPrefab | 에러 |
|---|---:|---:|---:|---:|
| `Assets/@Scenes/StartScene.unity` | 83 | 0 | 0 | 0 |
| `…/RYU/Scenes/Cutscene/Cutscene_Prologue.unity` | 14 | 0 | 0 | 0 |
| `…/RYU/Scenes/Cutscene/Cutscene_Ending.unity` | 11 | 0 | 0 | 0 |
| `…/RYU/Scenes/Dev/FlowTest.unity` | 11 | 0 | 0 | 0 |
| `…/RYU/Scenes/Dev/FreePlayTest.unity` | 19 | 0 | 0 | 0 |
| `…/RYU/Scenes/Dev/TutorialScene.unity` | 25 | 0 | 0 | 0 |
| `…/RYU/Scenes/Main/MainPlayScene.unity` | 224 | **1** | 0 | 0 |
| `Assets/@Scenes/Play.unity` | 1,281 | 0 | 0 | 0 |
| `…/RYU/Scenes/Cutscene_SungnyemunBuild.unity` | 2 | 0 | 0 | 0 |

### 기준선에서 이미 있던 Missing Script 1건

| 씬 | 오브젝트 |
|---|---|
| `Assets/@Developers/RYU/Scenes/Main/MainPlayScene.unity` | `planer` |

**이 Missing Script도 이번 작업 이전부터 존재합니다.**

---

## 기준선 요약 (사후 비교 기준)

| 지표 | 기준선 |
|---|---:|
| 컴파일 에러 | 0 |
| 테스트 실패 | 1 (`TutorialOutlineGuide_CoversEveryObjectInteractionStep`) |
| 테스트 통과 | 9 / 10 |
| Missing Script | 1 (`MainPlayScene` / `planer`) |
| Missing Prefab | 0 |
| 씬 콘솔 에러 | 0 |
