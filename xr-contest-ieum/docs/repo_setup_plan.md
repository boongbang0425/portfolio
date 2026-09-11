# 새 저장소 분리 계획 — `xr-contest-ieum` (실행 금지 — 승인용 초안)

작성: 2026-09-11 · 기준 상태: 1·2차 삭제 완료 (`Assets/` 750.7 MB / 2,829 파일)

**이 문서는 계획서입니다. `git init`·`add`·`commit`·`push`·`gc`를 하나도 실행하지 않았습니다.**

---

## 0. 먼저 처리해야 할 것 — 자격증명 노출

새 저장소를 만들기 **전에** 반드시 끝내야 합니다. 새 저장소로 옮기면 과거 이력은 안 따라오지만,
**현재 작업트리의 파일을 그대로 커밋하면 키가 그대로 새 저장소에 들어갑니다.**

| 파일 | 내용 | 현재 상태 |
|---|---|---|
| `IUM/Assets/@Documents/09_협업_설치_가이드.md` 38·58줄 | Google OAuth refresh token (`1//` 로 시작, 103자) | **작업트리·커밋 2개 모두에 존재** |

추가로 같은 모노레포의 다른 프로젝트에도 있습니다 (이번 분리 대상은 아니지만 같은 공개 저장소):

| 파일 | 내용 |
|---|---|
| `digitaltwin-emolamp/Guide/1_환경설정.md:142` | OpenWeather API key (32자 hex) |
| `digitaltwin-emolamp/Guide/2_Unity가이드.md:99` | 동일 키 |

### 순서

1. **키를 먼저 폐기·재발급합니다.** Google Cloud Console에서 해당 OAuth 클라이언트의 토큰 취소,
   OpenWeather에서 키 재발급. 이미 공개 저장소에 푸시됐으므로 **이력 정리보다 폐기가 우선**입니다
2. 문서에서 값을 지우고 `<발급받은_토큰>` 같은 플레이스홀더로 교체
3. 그 다음 새 저장소 생성

> `cleanup_audit.md` 1-11절에서 "키를 제거하신 것 같습니다"라고 추정했는데, 실제로는 **아직 남아 있습니다.**

---

## 1. `.gitignore`

저장소 루트(`xr-contest-ieum/`)에 둡니다. 기존 129줄짜리 Unity ignore를 살리고 아래를 반영합니다.

```gitignore
# ── Unity 생성물 ────────────────────────────────
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emoryCaptures/
[Rr]ecordings/
/IUM/[Aa]ssets/AssetStoreTools*

# ── 빌드 산출물 ─────────────────────────────────
*.apk
*.aab
*.aab.meta
*.unitypackage
*.unitypackage.meta
*.symbols.zip

# ── Addressables 빌드 산출물 ────────────────────
#   기존 규칙이 저장소 루트에 앵커돼 IUM/ 아래에 안 걸렸습니다. **/ 로 고칩니다
**/[Ss]treamingAssets/aa/
**/[Ss]treamingAssets/aa.meta
**/[Aa]ddressableAssetsData/*/*.bin*
**/[Aa]ddressableAssetsData/link.xml

# ── 외부 에셋 (Asset Store / Unity 템플릿 / ambientCG) ──
#   재배포 문제 + 용량. README에 목록과 받는 곳을 적습니다
/IUM/Assets/ThirdParty/
/IUM/Assets/ThirdParty.meta
/IUM/Assets/Plugins/Demigiant/
/IUM/Assets/Plugins/Demigiant.meta
/IUM/Assets/Samples/
/IUM/Assets/Samples.meta

# ── 대용량 영상 (Releases / YouTube 로 분리) ────
*.mov
*.mov.meta
/IUM/Assets/StreamingAssets/Prologue_*.mp4
/IUM/Assets/StreamingAssets/Prologue_*.mp4.meta

# ── 로컬 STT 모델 (343 MB) ──────────────────────
**/[Ss]treamingAssets/sherpa-onnx*/
**/[Ss]treamingAssets/sherpa-onnx*.meta

# ── 비밀키 ──────────────────────────────────────
**/[Ss]treamingAssets/ai_secrets.json*
**/[Ss]treamingAssets/ai_service_account.json*
**/[Ss]treamingAssets/ai_oauth_client.json*
**/[Ss]treamingAssets/client_secret_*.json*
.env
.env.*
!.env.example
*.pem
*.key
*.p12
*.keystore

# ── IDE / OS ────────────────────────────────────
.vs/
.vscode/
.idea/
*.csproj
*.sln
*.user
*.pidb
*.booproj
.DS_Store
Thumbs.db

# ── Python (도구 스크립트용) ────────────────────
__pycache__/
.venv/
```

### 주의 두 가지

**① `ThirdParty/`를 ignore하면 클론만으로 프로젝트가 안 열립니다.**
`Play.unity`·`GongpoScene.unity`·`MainPlayScene.unity`가 이 폴더의 에셋 4개를 실제로 참조합니다
(`folder_delete_list.md`): `SkySeries/6SidedMegaSun.mat`, `ADG_Textures/ground1/ground1.mat`,
`ParticlePack/WoodImpacts.prefab`, `VRTemplateAssets/Pointer Outline.mat`.
README에 에셋 목록·버전·받는 곳을 반드시 적어야 합니다.

**② `ParticlePack/URP.asset`은 `ProjectSettings/GraphicsSettings.asset`이 GUID로 물고 있습니다.**
ignore하면 렌더 파이프라인 설정이 깨집니다. 3번 항목(이동안)을 먼저 처리하거나,
예외 규칙 `!/IUM/Assets/ThirdParty/UnityTechnologies/ParticlePack/URP.asset` 을 넣어야 합니다.

**③ ambientCG 세트 132.8 MB는 경로로 분리할 수 없습니다.**
`@Developers/RYU/Start/FixedUI/meterials/` 안에 팀 에셋과 섞여 있고 `StartScene.unity`가 참조합니다.
`restructure_plan.md` 3-1절대로 `@Art/Materials/StartRoom/`으로 옮긴 뒤에야 정책을 적용할 수 있으니,
이번에는 **LFS로 올리는 쪽**으로 두었습니다.

---

## 2. `.gitattributes`

저장소 루트에 두고 **첫 커밋 전에** 적용합니다. 지금은 새 저장소이므로 이 조건을 만족할 수 있습니다.

```gitattributes
* text=auto eol=lf

# ── 텍스트로 강제 (Unity YAML) ──────────────────
*.cs        text diff=csharp
*.shader    text
*.cginc     text
*.hlsl      text
*.json      text
*.md        text
*.uxml      text
*.uss       text
*.tss       text
*.unity     text
*.prefab    text
*.mat       text
*.asset     text
*.meta      text
*.anim      text
*.controller text
*.asmdef    text

# ── LFS: 모델 ───────────────────────────────────
*.fbx  filter=lfs diff=lfs merge=lfs -text
*.obj  filter=lfs diff=lfs merge=lfs -text
*.blend filter=lfs diff=lfs merge=lfs -text

# ── LFS: 텍스처 ─────────────────────────────────
*.png  filter=lfs diff=lfs merge=lfs -text
*.jpg  filter=lfs diff=lfs merge=lfs -text
*.jpeg filter=lfs diff=lfs merge=lfs -text
*.tga  filter=lfs diff=lfs merge=lfs -text
*.tif  filter=lfs diff=lfs merge=lfs -text
*.tiff filter=lfs diff=lfs merge=lfs -text
*.psd  filter=lfs diff=lfs merge=lfs -text
*.exr  filter=lfs diff=lfs merge=lfs -text
*.hdr  filter=lfs diff=lfs merge=lfs -text
*.cubemap filter=lfs diff=lfs merge=lfs -text

# ── LFS: 오디오 / 영상 ──────────────────────────
*.wav  filter=lfs diff=lfs merge=lfs -text
*.mp3  filter=lfs diff=lfs merge=lfs -text
*.ogg  filter=lfs diff=lfs merge=lfs -text
*.mp4  filter=lfs diff=lfs merge=lfs -text
*.webm filter=lfs diff=lfs merge=lfs -text

# ── LFS: 폰트 / 문서 ────────────────────────────
*.ttf  filter=lfs diff=lfs merge=lfs -text
*.otf  filter=lfs diff=lfs merge=lfs -text
*.pdf  filter=lfs diff=lfs merge=lfs -text

# ── LFS: 대용량 .asset 개별 지정 ────────────────
#   .asset은 대부분 수 KB YAML이라 확장자 단위로 LFS에 넣으면 안 됩니다.
#   TMP SDF 폰트 아틀라스처럼 수 MB짜리만 경로로 콕 집습니다.
IUM/Assets/**/Giants-Bold\ SDF.asset               filter=lfs diff=lfs merge=lfs -text
IUM/Assets/**/Giants-Bold\ Dynamic\ SDF.asset      filter=lfs diff=lfs merge=lfs -text
IUM/Assets/**/*\ SDF.asset                         filter=lfs diff=lfs merge=lfs -text
```

> 마지막 `*\ SDF.asset` 한 줄이면 TMP 폰트 아틀라스를 전부 잡습니다.
> 다른 프로젝트를 같은 정책으로 옮길 때도 유용합니다 — 예: `digitaltwin-emolamp`의
> `NotoSansKR-VariableFont_wght SDF.asset`(37.5 MB), `LiberationSans SDF.asset`(2.15 MB).
>
> 기존 모노레포의 `.gitattributes`는 `* text=auto` 한 줄뿐이라 바이너리 정규화 위험이 있었습니다.
> 위처럼 바이너리를 `-text`로 명시하면 그 문제도 같이 해결됩니다.

---

## 3. 예상 LFS 용량

삭제 완료 시점 기준 `IUM/Assets/` 750.7 MB를 정책대로 나눈 결과입니다.

| 구분 | 용량 | 파일 | 처리 |
|---|---:|---:|---|
| 외부 에셋 (`ThirdParty`, `Plugins/Demigiant`, `Samples`, `TextMesh Pro`) | 178.6 MB | 563 | `.gitignore` 제외 |
| **LFS 대상** | **545.4 MB** | **137** | Git LFS |
| 일반 Git (텍스트·소형 에셋) | 25.6 MB | 536 | 일반 |
| `.meta` | 1.2 MB | 1,593 | 일반 |

### LFS 대상 내역

| 확장자 | 파일 | 용량 |
|---|---:|---:|
| `.png` | 86 | 346.90 MB |
| `.fbx` | 32 | 100.80 MB |
| `.mov` | 1 | 75.56 MB |
| `.tga` | 4 | 13.35 MB |
| `.webm` | 1 | 4.11 MB |
| `.exr` | 2 | 2.29 MB |
| `.ttf` | 1 | 1.19 MB |
| `.pdf`, `.jpg`, `.mp3`, `.jpeg` | 10 | 1.16 MB |
| `* SDF.asset` (개별 지정) | 2 | 14.73 MB |

### 사전 정리로 줄어드는 양

| 조치 | 감소 | 이후 LFS |
|---|---:|---:|
| 시작점 | — | 545.4 MB |
| `devcontest.mov` 제외 (`.gitignore` `*.mov`) | −75.6 MB | **469.8 MB** |
| 4096 텍스처 22장 → 2048 (`size_report.md` 6-2) | 약 −166 MB | **약 304 MB** |
| `@Documents/` 를 `docs/` 로 이동 (pdf/jpg 0.7 MB) | −0.7 MB | 약 303 MB |

**최종 예상: LFS 약 300 MB, 일반 Git 약 26 MB.**

> GitHub 무료 계정의 LFS 한도는 스토리지 1 GB · 대역폭 월 1 GB입니다.
> 300 MB면 스토리지는 여유가 있지만, **대역폭은 클론 3회면 소진**됩니다.
> 협업자가 여럿이면 데이터 팩 구매나 Unity Version Control 쪽을 고려하십시오.
>
> 4096 텍스처 축소는 **런타임에는 아무 효과가 없습니다.** import Max Size가 이미 2048이라
> APK에는 2048로 들어갑니다 (`size_report.md` 6-2). 순수하게 저장소 용량만을 위한 작업입니다.

---

## 4. 명령 순서 (승인 후 실행)

전제: 0번(키 폐기·문서 정리)이 끝났고, `git-lfs`가 설치되어 있으며, GitHub에 빈 저장소
`xr-contest-ieum`을 만들어 둔 상태.

```bash
# ── 1. 현재 모노레포와 분리된 사본을 만든다 (원본은 손대지 않음) ──
cd ~/Desktop
cp -r portfolio/xr-contest-ieum ./xr-contest-ieum-new
cd xr-contest-ieum-new

# ── 2. 혹시 남아 있을 상위 저장소 흔적 제거 ──
ls -a                    # .git 이 없어야 한다. 있으면 rm -rf .git

# ── 3. .gitignore / .gitattributes 를 먼저 놓는다 (첫 커밋 전!) ──
#      위 1·2절 내용을 각각 저장

# ── 4. 저장소 초기화 + LFS 설치 ──
git init -b main
git lfs install --local

# ── 5. .gitattributes 가 반영됐는지 확인 (아직 add 전) ──
git lfs track                       # 등록된 패턴 목록 확인
git add .gitattributes .gitignore
git commit -m "chore: add gitignore and LFS tracking rules"

# ── 6. 무엇이 LFS로 갈지 먼저 확인한다 (실제 add 전) ──
git add -A --dry-run | head -50
git status --short | wc -l

# ── 7. 본 커밋 ──
git add -A
git lfs status | head -40           # LFS 대상이 맞는지 육안 확인
git commit -m "feat: initial import of IUM XR project"

# ── 8. LFS 실제 적재 확인 ──
git lfs ls-files | wc -l            # 약 139개 기대
du -sh .git                         # LFS 포인터만 있으므로 작아야 한다
du -sh .git/lfs                     # 약 300 MB 기대

# ── 9. 원격 연결 후 푸시 ──
git remote add origin https://github.com/<계정>/xr-contest-ieum.git
git push -u origin main
```

### 검증 체크리스트

| 단계 | 확인할 것 |
|---|---|
| 5 뒤 | `git lfs track` 출력에 `*.png`, `*.fbx`, `* SDF.asset` 이 보이는가 |
| 7 뒤 | `git lfs ls-files`에 `.cs`·`.mat`·`.unity`가 **섞여 있지 않은가** (섞였으면 `.gitattributes` 오류) |
| 7 뒤 | `git ls-files | grep -c ThirdParty` 가 **0**인가 |
| 7 뒤 | `git ls-files | grep -i -E 'secrets|oauth_client|\.mov$'` 가 **비어 있는가** |
| 8 뒤 | 50 MiB 넘는 일반 blob이 없는가: `git cat-file --batch-check` 로 확인 |
| 9 전 | 0번 키 폐기가 끝났는가 |

### 모노레포 쪽 처리 (별도 결정)

현재 `portfolio` 저장소는 커밋 2개 · `.git` 1.7 GB이고, **5,811 객체가 전부 loose**입니다
(`in-pack: 0`). 선택지:

1. **그대로 둔다** — `xr-contest-ieum`만 새 저장소로 나가고, 모노레포는 아카이브
2. **`git gc --aggressive` 로 압축** — 이력은 그대로, 크기만 줄임. 분리와 무관하게 효과 있음
3. **모노레포에서 `xr-contest-ieum` 제거 후 재작성** — 이력 재작성 + force-push 필요.
   커밋이 2개뿐이라 잃을 게 거의 없지만, 이미 푸시됐으므로 협업자가 있으면 재클론 필요

어느 쪽이든 **0번 키 폐기는 독립적으로 먼저** 해야 합니다. 이력을 지워도 GitHub 캐시·포크·
크롤러에 남았을 수 있으므로, 키 무효화 외에는 안전을 보장할 방법이 없습니다.
