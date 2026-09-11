1. 현재 구조의 문제

코드가 7곳에 흩어져 있음

@Scripts/, Scripts/(Board3DButton 등), Editor/(MeasureBounds 등)
GazeSystem/*.cs, Assets/SimpleFreeCamera.cs
@GameAssets/Sungnyemun/숭례문조립/BuildingStageDirector.cs
@Developers/RYU/ 안의 Audio, ProcessIntegration, Start 스크립트

개인 작업 폴더에 본편 자산이 있음

@Developers/RYU/Scenes/Main/MainPlayScene.unity, CoreSystems.prefab, PlayLoop.prefab, QuestBoard.prefab
효과음 Resources/Sfx, 테스트 코드

같은 이름의 클래스가 두 곳에 있음

@Scripts/Cores/AudioManager.cs(12.7KB)와 @Developers/RYU/Audio/Core/AudioManager.cs(21.3KB)
Singleton.cs도 양쪽에 있습니다.
둘 다 Assembly-CSharp에 속하므로, 지금 컴파일이 된다면 네임스페이스가 다른 것입니다. 합치기 전에 두 파일을 비교해야 합니다.

크기가 같아 중복으로 보이는 파일

Assets/iumi.fbx ↔ iumi/iumi.fbx (7,436,332 bytes)
tools/bellyplane.fbx·자귀.fbx ↔ tools/자귀배대패/ 안의 같은 파일
blackline.fbx ↔ blackline 1.fbx
darkwood.png ↔ darkwood 1.png
hammer/woodtooltexture1.png ↔ tools/woodtooltexture1.png
iumi/iumi_hair_custom_sage*.mat ↔ iumi/iumhair/ 안의 같은 파일
NightSkyHDRI008.png ↔ NightSkyHDRI008_2K (1)/ 폴더

템플릿·샘플 잔재

Readme.asset, TutorialInfo/, TextMesh Pro/Examples & Extras/
SampleScene 3개 (Scenes/, @Scenes/, warehouseFin/Scenes/)
ADG_Textures/Demo, SkySeries Freebie/ExampleScenes, DOTweenPro Examples, QuickOutline/Samples
unity_all_assets.txt(0 byte)

이름 문제

오타: meterials(2곳), legenooldman
한글 자판 오입력: w미ㅣ.mat
의미 없는 이름: bbbb.png, Object_14.png, finalmesh11.fbx, moded.fbx, modedprefab 1.prefab, warehouse_2.fbx
Blender 자동 이름: 재질_2.001~.111
표기 혼용: IUM / Eeum / Ieumi / iumi
한글 경로: 숭례문조립, 자귀배대패, 천.png, iumi프리펩.prefab

Assets 밖에 있어야 할 것

@Documents/ (md, pdf, jpg). Assets 안에 있으면 Unity가 .meta를 만들고 임포트합니다. 저장소 루트의 docs/로 옮깁니다.

본체 없는 .meta

StreamingAssets/Prologue_1.mp4.meta, Prologue_2.mp4.meta: mp4 파일이 목록에 없습니다. 일부러 뺀 것인지 확인이 필요합니다.
2. 옮기기 전에: 경로가 깨지는 조건

Unity 안의 참조(씬 → 프리팹 → 머티리얼 → 텍스처)는 경로가 아니라 .meta에 적힌 GUID로 연결됩니다. 따라서 다음 두 조건만 지키면 이동해도 깨지지 않습니다.

Unity 에디터의 Project 창 안에서 옮긴다.
탐색기로 옮길 때는 Unity를 끄고 .meta를 함께 옮긴다.

깨지는 것은 코드 안의 문자열 경로입니다. 이 프로젝트에는 DevSceneBuilder, PlayWorkshopBuilder, SungnyemunImportBuilder, ReleaseBuilder 같은 에디터 빌더가 있어 "Assets/..." 경로를 직접 쓸 가능성이 높습니다. 먼저 목록을 뽑습니다. IUM 폴더에서 PowerShell로 실행합니다.

powershell
Get-ChildItem Assets -Recurse -Include *.cs,*.json |
  Select-String -Pattern '"Assets/|Resources\.Load|LoadAssetAtPath|OpenScene|FindAssets|Shader\.Find|streamingAssetsPath|\.mp4|\.webm' |
  ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" } |
  Out-File path_refs.txt -Encoding utf8

위치나 이름이 고정된 폴더

StreamingAssets: Assets 바로 아래, 이름 고정. 안의 파일명도 코드가 문자열로 읽습니다.
Resources: 어디로든 옮길 수 있지만 그 아래 하위 경로는 유지해야 합니다. 예: Resources/Sfx/hammer_hit
Editor: 폴더 이름이 Editor여야 에디터 전용으로 컴파일됩니다.
RYU/ProcessIntegration/Tests/Editor: asmdef가 있으므로 폴더째로 옮깁니다.
Samples/: Package Manager가 버전별 경로로 추적하므로 옮기지 않습니다.
Plugins, TextMesh Pro, XR, XRI, Settings, AddressableAssetsData, Resources/DOTweenSettings: 옮길 이유가 없으니 그대로 둡니다.
@AddressableAssets: 옮겨도 로드는 되지만, 주소 문자열이 옛 경로 그대로 남습니다.

중복 파일 확정 (크기가 아니라 내용 해시로 비교)

powershell
Get-ChildItem Assets -Recurse -File -Exclude *.meta | Get-FileHash -Algorithm MD5 |
  Group-Object Hash | Where-Object Count -gt 1 |
  ForEach-Object { $_.Group.Path; '---' } | Out-File dup.txt -Encoding utf8
3. 정리 절차

① 백업: 현재 폴더를 통째로 압축해 둡니다.

② 미사용 에셋 목록 생성: 빌드 씬, Resources, StreamingAssets, @AddressableAssets에서 참조를 따라가고, 어디에도 연결되지 않은 파일을 크기순으로 뽑습니다. 아래 파일을 Assets/Editor/에 넣고 메뉴 Tools > Unused Asset Report를 실행합니다.

csharp
using System.IO; using System.Linq; using UnityEditor;
public static class UnusedAssetReport {
    [MenuItem("Tools/Unused Asset Report")]
    static void Run() {
        var all = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p)).ToArray();
        var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
            .Concat(all.Where(p => p.Contains("/Resources/") || p.Contains("/StreamingAssets/") || p.Contains("/@AddressableAssets/")));
        var used = AssetDatabase.GetDependencies(roots.ToArray(), true).ToHashSet();
        var lines = all.Where(p => !used.Contains(p) && !p.EndsWith(".cs") && !p.EndsWith(".asmdef"))
            .Select(p => (p, mb: new FileInfo(p).Length / 1048576.0)).OrderByDescending(x => x.mb)
            .Select(x => $"{x.mb:F2} MB\t{x.p}");
        File.WriteAllLines("UnusedAssets.txt", lines);
        UnityEngine.Debug.Log("IUM/UnusedAssets.txt 생성");
    }
}

이 목록에서 "미사용"으로 나와도 바로 지우면 안 되는 경우가 셋 있습니다.

Build Settings에 없는 Dev 씬 전용 자산
path_refs.txt에서 빌더가 경로로 부르는 자산
Addressables 그룹 창에 등록된 엔트리

③ 삭제: 템플릿·샘플 잔재, 확정된 중복, ambientCG 부속 파일(아래 4절)부터 지웁니다.

④ 이동: Unity 에디터 안에서, 폴더 단위로 한 번에 하나씩 옮깁니다. 매번 StartScene부터 끝까지 데스크톱 조작으로 플레이하고, Test Runner로 CoreLoopContractTests를 돌립니다. 문자열 경로는 path_refs.txt를 보고 같이 고칩니다.

목표 구조: 새 규칙을 만들기보다 기존 @ 접두사를 그대로 쓰고 흩어진 것만 흡수합니다. 문자열 경로 수정량이 가장 적습니다.

Assets/
├─ @Scripts/     ← Scripts/, Editor/, GazeSystem/*.cs, SimpleFreeCamera.cs, BuildingStageDirector.cs, RYU 코드
├─ @Scenes/      ← wood.unity, RYU/Scenes/Main·Cutscene / Dev 씬은 @Scenes/Dev
├─ @Prefabs/     ← Prefabs/, RYU/Prefabs, RYU/UI
├─ @Art/
│  ├─ Characters/Ieumi, Nojang      (iumi, legenooldman)
│  ├─ Environment/Sungnyemun, Workshop  (warehouseFin)
│  ├─ Props/Tools, WoodParts        (tools, hammer, @GameAssets/*Part.fbx)
│  ├─ Materials/  Textures/  Fonts/
├─ @Audio/Resources/Sfx
├─ @UI/          ← RYU/Quest/UI의 uss/uxml 포함
├─ @Data/        ← @AddressableAssets
├─ @Tests/       ← RYU/ProcessIntegration/Tests (asmdef째로)
├─ ThirdParty/   ← 사용분만: ParticlePack, SkySeries, ADG, QuickOutline
└─ (고정) StreamingAssets, Plugins, TextMesh Pro, Samples, XR, XRI, Settings, AddressableAssetsData, Resources
docs/            ← @Documents (Assets 밖, 저장소 루트)

이름은 Unity Project 창에서 바꾸면 GUID가 유지됩니다. 스크립트는 파일명과 클래스명이 같아야 하므로 우선순위를 뒤로 미룹니다. 에셋 경로의 한글은 ASCII로 바꾸는 것을 권장합니다.

4. 아트 에셋을 GitHub에 올리는가

GitHub 제한

단일 파일은 100MiB를 넘으면 푸시가 차단되고 50MiB를 넘으면 경고가 뜹니다. 
Fastio
저장소 전체는 1GB 이하가 권장이고, 5GB 이하는 강하게 권장됩니다. 
Fastio
@GameAssets/warehouse_2.fbx(59.9MB)는 이미 경고 구간입니다.

보통 쓰는 방식

게임 회사: 아트는 Perforce나 Unity Version Control로 관리합니다.
GitHub를 쓰는 소규모 팀: 바이너리를 Git LFS로 올립니다.
공개 포트폴리오 저장소: 코드·데이터·문서만 올리고 외부 에셋은 빼는 경우가 많습니다. Asset Store 에셋(DOTween Pro는 유료, SkySeries·ADG·QuickOutline·Particle Pack)을 공개 저장소에 원본으로 올리면 재배포에 해당하기 때문입니다.

권장: 포트폴리오용 공개 저장소는 다음처럼 구성합니다.

올릴 것: @Scripts, @Data json, docs/, ProjectSettings, Packages/manifest.json, 팀이 직접 만든 아트(LFS)
README에 적을 것: 외부 에셋 목록과 받는 곳
별도 첨부: APK와 시연 영상은 GitHub Releases 또는 YouTube

LFS 무료 한도가 있으므로 총량을 먼저 줄인 뒤 적용합니다.

용량이 큰 항목 (목록의 Length 합산 대략치, 전체 약 1.8~2GB)

항목	추정	조치
ADG_Textures/ground_vol1 (TGA 14세트)	약 390MB	사용분만 남김
SkySeries Freebie/FreebieHdri	약 390MB	쓰는 HDRI만 남김
RYU/Start/FixedUI/meterials (ambientCG 8세트)	약 235MB	아래 부속 파일 삭제로 약 85MB 감소, 미사용 세트 삭제
Sungnyemun/texture (*.fbx.png 8장, 각 16~25MB)	약 220MB	원본을 4K 이하로 축소
UnityTechnologies/ParticlePack	약 150MB 이상	사용 이펙트만 남김
warehouse_2.fbx 60MB + warehousetextureFin.fbx 39MB	약 100MB	둘 중 하나만 쓰는지 확인
VRTemplateAssets (skybox01_openGL.png 19.8MB 등)	약 50MB	참조 확인
legenooldman (fbx 24.5MB, diffuse 17.5MB)	약 45MB	텍스처 축소

ambientCG 부속 파일 (Unity에서 쓰지 않음)

.mtlx, .tres, .usdc: MaterialX·Godot·USD용 파일입니다.
*_NormalDX.jpg: Unity는 OpenGL 방식 노멀맵(NormalGL)을 씁니다. 단, 머티리얼이 DX 쪽을 참조하고 있다면 GL로 바꾼 뒤 지웁니다.
*_Displacement.jpg: 머티리얼에 연결되지 않았다면 삭제합니다.

같은 파일명으로 해상도만 줄이면 GUID는 유지됩니다. 확장자를 바꿀 때는(예: TGA→PNG) .meta 파일명도 같이 바꿔야 참조가 유지됩니다.

.gitattributes: 저장소 루트에 두고, 첫 커밋 전에 적용해야 합니다.

gitattributes
*.fbx filter=lfs diff=lfs merge=lfs -text
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.tga filter=lfs diff=lfs merge=lfs -text
*.tif filter=lfs diff=lfs merge=lfs -text
*.exr filter=lfs diff=lfs merge=lfs -text
*.hdr filter=lfs diff=lfs merge=lfs -text
*.psd filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.mp3 filter=lfs diff=lfs merge=lfs -text
*.mp4 filter=lfs diff=lfs merge=lfs -text
*.webm filter=lfs diff=lfs merge=lfs -text
*.ttf filter=lfs diff=lfs merge=lfs -text
*.pdf filter=lfs diff=lfs merge=lfs -text

09_협업_설치_가이드.md, AiConfig.cs, ai_config.json이 9/10에 수정된 것으로 보아 키를 제거하신 것 같습니다. 이 폴더로 새 저장소를 만들면 과거 이력은 따라오지 않습니다. 다만 원격 팀 저장소 이력에는 키가 남아 있으므로 키 폐기는 별도로 해야 합니다.

5. .gitignore 수정

문제

bin/: ESW 프로젝트의 실행 스크립트 proj_main/bin/proj까지 무시됩니다. 이 줄을 지우고 **/ros2_ws/build/, **/ros2_ws/install/, **/ros2_ws/log/로 범위를 좁힙니다.
*.obj: 3D 모델 .obj도 무시됩니다. OBJ 모델을 쓴다면 제거합니다.
파일 위치: portfolio 루트 한 곳에만 있습니다. 프로젝트별로 저장소를 나누면(권장) 각 저장소 루트에 따로 둬야 적용됩니다.
누락: Addressables 빌드 산출물, 로컬 STT 모델(343MB), 비밀키 파일, Python 캐시가 빠져 있습니다.

추가할 내용

gitignore
[Rr]ecordings/
*.apk
*.aab
*.unitypackage
**/[Ss]treamingAssets/aa/
**/[Ss]treamingAssets/aa.meta
**/[Ss]treamingAssets/sherpa-onnx*
**/[Ss]treamingAssets/ai_secrets.json*
**/[Ss]treamingAssets/ai_oauth_client.json*
.env.*
!.env.example
*.pem
*.key
__pycache__/
.venv/