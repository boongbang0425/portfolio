using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IUM.CoreLoopVerification.Tests
{
    /// <summary>
    /// 물리 공정 구현을 대신 실행하지 않고, 전체 게임 루프를 잇는 계약을 한 번에 검증한다.
    /// 검증 대상은 흐름·퀘스트·대사·컷씬 데이터, Build Settings, 메인 씬 배선, 진행 상태 전이,
    /// MainPlayProcessBridge의 공정별 신호/접근 정책이다.
    ///
    /// 조작 튜토리얼과 제작 공정 네 개(짧은·중간·긴 부재와 조립)가 모두 quest.json의 퀘스트
    /// 그래프로 이관되어 실행기는 QuestManager 하나다. process.json은 빈 배열만 남았으므로 공정
    /// 정의·신호 검증은 전부 퀘스트 그래프를 본다. 공정 하나가 작업 종류마다 다른 신호 키를 쓰므로
    /// 프로필은 키를 배열로 적고, 같은 키를 나눠 쓰는 분할 목표(공포 조립 1 → 18 → 37)의 amount가
    /// 버스 누계라는 규약도 여기서 함께 검증한다.
    ///
    /// 예상 루프는 CoreLoopVerificationProfile.json에 있으므로 다른 루프나 공정 구성을 검증할
    /// 때 테스트 코드를 복사하지 않고 프로필만 교체할 수 있다.
    /// </summary>
    [Category("IUM.CoreLoop")]
    public sealed class CoreLoopContractTests
    {
        const string ProfilePath =
            "Assets/@Developers/RYU/ProcessIntegration/Tests/CoreLoopVerificationProfile.json";
        const string FlowPath = "Assets/@AddressableAssets/Data/Static/flow.json";
        const string ProcessPath = "Assets/@AddressableAssets/Data/Static/process.json";
        // 튜토리얼 목표는 일반 공정 배열이 아니라 재사용 가능한 퀘스트 그래프 계약으로 검증한다.
        const string QuestPath = "Assets/@AddressableAssets/Data/Static/quest.json";
        const string DialoguePath = "Assets/@AddressableAssets/Data/Static/dialogue.json";
        const string CutscenePath = "Assets/@AddressableAssets/Data/Static/cutscene.json";
        const string TutorialScenePath = "Assets/@Developers/RYU/Scenes/Dev/TutorialScene.unity";
        const string PauseUxmlPath = "Assets/@UI/Pause/PauseMenu.uxml";
        const string PauseUssPath = "Assets/@UI/Pause/PauseMenu.uss";
        const string FixedUiIconPath = "Assets/@UI/Pause/FixedUI/Icons";

        static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;

        [Test]
        public void FullRoute_HasEveryDestinationAndBuildScene()
        {
            var profile = ReadJson<VerificationProfile>(ProfilePath);
            var flow = ReadJson<FlowFile>(FlowPath);
            var cutscenes = ReadJson<CutsceneFile>(CutscenePath);

            Assert.That(profile.route, Is.Not.Null.And.Not.Empty, "검증 프로필에 루프가 없습니다.");
            Assert.That(flow.entries, Is.Not.Null.And.Not.Empty, "flow.json에 목적지가 없습니다.");

            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => Path.GetFileNameWithoutExtension(scene.path))
                .ToHashSet(Names);
            var flowByProcess = UniqueBy(flow.entries, entry => entry.process, FlowPath);
            var cutsceneById = UniqueBy(cutscenes.cutscenes, cutscene => cutscene.id, CutscenePath);

            foreach (var stage in profile.route)
            {
                Assert.That(flowByProcess.TryGetValue(stage.process, out var destination), Is.True,
                    $"flow.json에 '{stage.process}' 목적지가 없습니다.");

                if (Names.Equals(stage.destinationKind, "scene"))
                {
                    Assert.That(destination.scene, Is.EqualTo(stage.destination).IgnoreCase,
                        $"'{stage.process}' 씬 목적지가 프로필과 다릅니다.");
                    Assert.That(enabledScenes, Does.Contain(stage.destination),
                        $"씬 '{stage.destination}'이 Build Settings에서 활성화되지 않았습니다.");
                    continue;
                }

                Assert.That(stage.destinationKind, Is.EqualTo("cutscene").IgnoreCase,
                    $"'{stage.process}'의 destinationKind가 scene/cutscene이 아닙니다.");
                Assert.That(destination.cutscene, Is.EqualTo(stage.destination).IgnoreCase,
                    $"'{stage.process}' 컷씬 목적지가 프로필과 다릅니다.");
                Assert.That(cutsceneById.TryGetValue(stage.destination, out var cutscene), Is.True,
                    $"cutscene.json에 '{stage.destination}'가 없습니다.");

                if (!string.IsNullOrWhiteSpace(cutscene.scene))
                    Assert.That(enabledScenes, Does.Contain(cutscene.scene),
                        $"컷씬 씬 '{cutscene.scene}'이 Build Settings에서 활성화되지 않았습니다.");

                if (!string.IsNullOrWhiteSpace(cutscene.video) &&
                    !Uri.TryCreate(cutscene.video, UriKind.Absolute, out _))
                {
                    var videoPath = Path.Combine(Application.streamingAssetsPath, cutscene.video);
                    Assert.That(File.Exists(videoPath), Is.True,
                        $"컷씬 영상 '{cutscene.video}'를 StreamingAssets에서 찾지 못했습니다.");
                }

                if (!string.IsNullOrWhiteSpace(cutscene.nextScene))
                    Assert.That(enabledScenes, Does.Contain(cutscene.nextScene),
                        $"컷씬 다음 씬 '{cutscene.nextScene}'이 Build Settings에서 활성화되지 않았습니다.");
            }
        }

        /// <summary>
        /// 제작 공정 네 개가 quest.json에 실행 가능한 그래프로 있고, 목표가 이 공정의 신호 키만
        /// 쓰며(그리고 그 키를 하나도 빠뜨리지 않으며), 참조하는 대사가 전부 존재하는지 본다.
        /// </summary>
        [Test]
        public void ProductionQuests_HaveSignalObjectivesAndResolvableDialogue()
        {
            var profile = ReadJson<VerificationProfile>(ProfilePath);
            var questFile = ReadJson<QuestFile>(QuestPath);
            var dialogueFile = ReadJson<DialogueFile>(DialoguePath);
            var questsByProcess = UniqueBy(questFile.quests, quest => quest.process, QuestPath);
            var dialogueIds = UniqueBy(dialogueFile.sequences, sequence => sequence.id, DialoguePath);

            foreach (var stage in profile.route.Where(stage =>
                         stage.requiresQuestDefinition && !Names.Equals(stage.process, "tutorial")))
            {
                Assert.That(questsByProcess.TryGetValue(stage.process, out var quest), Is.True,
                    $"quest.json에 '{stage.process}' 퀘스트가 없습니다.");
                Assert.That(quest.title, Is.Not.Null.And.Not.Empty,
                    $"'{stage.process}' 퀘스트에 인게임 표시 제목이 없습니다.");

                var route = FollowQuestRoute(quest);
                var objectives = route.Where(node => Names.Equals(node.kind, "objective")).ToArray();
                Assert.That(objectives, Is.Not.Empty, $"'{stage.process}'에 실행할 목표가 없습니다.");

                AssertDialogueExists(quest.introDialogue, dialogueIds, stage.process, "introDialogue");
                AssertDialogueExists(quest.completeDialogue, dialogueIds, stage.process, "completeDialogue");

                foreach (var node in objectives)
                {
                    Assert.That(node.objective, Is.Not.Null,
                        $"'{stage.process}'의 목표 노드 '{node.id}'에 objective가 없습니다.");
                    Assert.That(node.objective.id, Is.EqualTo(node.id),
                        $"'{stage.process}'의 노드 '{node.id}'와 목표 ID가 다릅니다.");
                    Assert.That(node.objective.goal, Is.Not.Null.And.Not.Empty,
                        $"'{stage.process}/{node.id}'의 인게임 목표 문구가 없습니다.");
                    Assert.That(node.controlHint, Is.Not.Null.And.Not.Empty,
                        $"'{stage.process}/{node.id}'의 인게임 조작 힌트가 없습니다.");

                    AssertDialogueExists(node.objective.introDialogue, dialogueIds, stage.process,
                        $"{node.id}.introDialogue");
                    AssertDialogueExists(node.objective.retryDialogue, dialogueIds, stage.process,
                        $"{node.id}.retryDialogue");
                    AssertDialogueExists(node.objective.successDialogue, dialogueIds, stage.process,
                        $"{node.id}.successDialogue");
                }

                if (stage.signals == null || stage.signals.Length == 0) continue;

                var signalObjectives = SignalObjectives(route);
                Assert.That(signalObjectives, Has.Length.GreaterThan(0),
                    $"'{stage.process}'에 Signal 목표가 없습니다.");
                Assert.That(signalObjectives.All(step => stage.signals.Contains(step.target, Names)), Is.True,
                    $"'{stage.process}'의 Signal 목표가 이 공정에 없는 신호 키를 씁니다. " +
                    $"허용: {string.Join(", ", stage.signals)}");
                Assert.That(signalObjectives.All(step => step.amount > 0f), Is.True,
                    $"'{stage.process}' Signal 목표의 amount는 0보다 커야 합니다.");

                // 프로필에 적은 키가 전부 실제로 쓰이는지도 본다. 존이 늘었는데 목표를 안 만든 경우를
                // 잡는다 — 그러면 그 작업은 아무도 요구하지 않는 작업이 된다.
                foreach (var signal in stage.signals)
                    Assert.That(signalObjectives.Any(step => Names.Equals(step.target, signal)), Is.True,
                        $"'{stage.process}'가 신호 '{signal}'을 쓰는 목표를 갖고 있지 않습니다.");

                var bridgeSignals = InvokeBridgeSignals(stage.process);
                Assert.That(bridgeSignals.OrderBy(value => value, Names),
                    Is.EqualTo(stage.signals.OrderBy(value => value, Names)),
                    $"MainPlayProcessBridge와 검증 프로필의 '{stage.process}' 신호 목록이 다릅니다.");
            }
        }

        /// <summary>
        /// 플레이어에게 보이는 문구의 장치 중립성. 목표 문구(goal)와 대사 text에는 조작키·장치 이름을
        /// 넣지 않고, 장치 차이는 controlHint / controlHintVr 두 필드에서만 갈린다. VR에서 도달할 수
        /// 없는 안내("마우스를 돌려…")가 목표나 대사에 박히는 것을 막는 계약이다.
        /// </summary>
        [Test]
        public void PlayerFacingText_KeepsDeviceNamesInControlHintsOnly()
        {
            var questFile = ReadJson<QuestFile>(QuestPath);
            var dialogueFile = ReadJson<DialogueFile>(DialoguePath);

            foreach (var quest in questFile.quests)
            foreach (var node in quest.nodes.Where(node => Names.Equals(node.kind, "objective")))
            {
                AssertDeviceNeutral(node.objective?.goal, $"{quest.id}/{node.id}.goal");

                if (node.controlHintVr != null)
                    Assert.That(node.controlHintVr.Trim(), Is.Not.Empty,
                        $"'{quest.id}/{node.id}'의 controlHintVr이 공백뿐입니다. 없애거나 채우십시오.");
            }

            foreach (var sequence in dialogueFile.sequences)
            {
                if (sequence.lines == null) continue;
                foreach (var line in sequence.lines)
                    AssertDeviceNeutral(line?.text, $"대사 '{line?.id ?? sequence.id}'");
            }
        }

        /// <summary>
        /// 장치 이름으로 취급하는 낱말. 조작 힌트 밖에서 이 말이 나오면 한쪽 장치에서 거짓말이 된다.
        /// '버튼'은 대사에서 쓰지 않기로 한 말이라 함께 막는다.
        /// </summary>
        static readonly string[] DeviceWords =
        {
            "마우스", "키보드", "컨트롤러", "스틱", "WASD", "W A S D", "그립", "트리거", "버튼",
            "키를", "키로", "키 중", "키나"
        };

        static void AssertDeviceNeutral(string value, string where)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            foreach (var word in DeviceWords)
                Assert.That(value.IndexOf(word, StringComparison.OrdinalIgnoreCase), Is.LessThan(0),
                    $"{where}에 장치 이름 '{word}'이 있습니다. 조작 안내는 controlHint / controlHintVr에만 씁니다.");
        }

        [Test]
        public void ProgressModel_CompletesWholeRouteAndEndingCanReset()
        {
            var profile = ReadJson<VerificationProfile>(ProfilePath);
            var processId = RuntimeType("ProcessId");
            var gradeType = RuntimeType("ProcessGrade");
            var progressType = RuntimeType("UserProgressData");
            var progress = Activator.CreateInstance(progressType);
            var nextProcess = progressType.GetProperty("NextProcess");
            var complete = progressType.GetMethod("Complete", new[] { processId, gradeType });
            var reset = progressType.GetMethod("Reset", Type.EmptyTypes);

            Assert.That(nextProcess, Is.Not.Null);
            Assert.That(complete, Is.Not.Null);
            Assert.That(reset, Is.Not.Null);

            var stagesToComplete = profile.route.Take(profile.route.Length - 1).ToArray();
            var none = Enum.Parse(gradeType, "None", true);
            for (var i = 0; i < stagesToComplete.Length; i++)
            {
                var stage = stagesToComplete[i];
                Assert.That(nextProcess.GetValue(progress).ToString(), Is.EqualTo(stage.process).IgnoreCase,
                    $"'{stage.process}' 시작 전 진행 상태가 끊겼습니다.");

                var current = Enum.Parse(processId, stage.process, true);
                complete.Invoke(progress, new[] { current, none });
            }

            var ending = profile.route[^1].process;
            Assert.That(nextProcess.GetValue(progress).ToString(), Is.EqualTo(ending).IgnoreCase,
                "마지막 제작 공정을 완료해도 엔딩 상태에 도달하지 못했습니다.");

            reset.Invoke(progress, null);
            Assert.That(nextProcess.GetValue(progress).ToString(),
                Is.EqualTo(profile.route[0].process).IgnoreCase,
                "엔딩의 진행 초기화 뒤 첫 공정으로 돌아가지 못했습니다.");
        }

        /// <summary>
        /// 각 공정의 신호 목표를 순서대로 실제 <c>ProcessSignalBus</c>에 통과시켜 본다.
        ///
        /// 버스는 키별 누적이고 <c>ProcessStep.Arm()</c>은 버스를 초기화하지 않으므로, 같은 키를
        /// 나눠 쓰는 분할 목표의 amount는 개별 요구량이 아니라 누계여야 한다. 여기서는 그 규약을
        /// 두 갈래로 확인한다 — amount가 목표 순서대로 증가하는지(그렇지 않으면 뒤 목표가 시작
        /// 즉시 충족되어 마일스톤이 사라진다), 그리고 앞 목표가 남긴 누계 위에서 각 목표를 차례로
        /// 채울 수 있는지.
        /// </summary>
        [Test]
        public void MainPlaySignals_CanSatisfyEveryConfiguredProductionObjective()
        {
            var profile = ReadJson<VerificationProfile>(ProfilePath);
            var questFile = ReadJson<QuestFile>(QuestPath);
            var questsByProcess = UniqueBy(questFile.quests, quest => quest.process, QuestPath);
            var signalBus = RuntimeType("ProcessSignalBus");
            var reset = signalBus.GetMethod("Reset", BindingFlags.Public | BindingFlags.Static);
            var add = signalBus.GetMethod("Add", BindingFlags.Public | BindingFlags.Static);
            var read = signalBus.GetMethod("Read", BindingFlags.Public | BindingFlags.Static);

            Assert.That(reset, Is.Not.Null);
            Assert.That(add, Is.Not.Null);
            Assert.That(read, Is.Not.Null);

            foreach (var stage in profile.route.Where(stage => stage.signals is { Length: > 0 }))
            {
                Assert.That(questsByProcess.TryGetValue(stage.process, out var quest), Is.True,
                    $"quest.json에 '{stage.process}' 퀘스트가 없습니다.");

                var route = SignalObjectives(FollowQuestRoute(quest));

                foreach (var signal in stage.signals)
                {
                    var signalObjectives = route.Where(step => Names.Equals(step.target, signal)).ToArray();
                    Assert.That(signalObjectives, Is.Not.Empty,
                        $"'{stage.process}'가 신호 '{signal}'을 쓰는 목표를 갖고 있지 않습니다.");

                    for (var i = 1; i < signalObjectives.Length; i++)
                        Assert.That(signalObjectives[i].amount, Is.GreaterThan(signalObjectives[i - 1].amount),
                            $"'{stage.process}'의 분할 목표 amount가 누계로 증가하지 않습니다. " +
                            $"'{signalObjectives[i].id}'는 앞 목표보다 큰 값이어야 합니다.");

                    reset.Invoke(null, new object[] { signal });
                    Assert.That((float)read.Invoke(null, new object[] { signal }), Is.EqualTo(0f));

                    foreach (var step in signalObjectives)
                    {
                        // 같은 키를 나눠 쓰는 목표는 앞 목표가 쌓아 둔 만큼을 그대로 물려받는다.
                        // 모자란 분량만 더한다.
                        var current = (float)read.Invoke(null, new object[] { signal });
                        if (step.amount > current)
                            add.Invoke(null, new object[] { signal, step.amount - current });

                        var actual = (float)read.Invoke(null, new object[] { signal });
                        Assert.That(actual, Is.GreaterThanOrEqualTo(step.amount),
                            $"'{stage.process}/{step.id}' 요구량을 ProcessSignalBus에서 충족하지 못했습니다.");
                    }

                    reset.Invoke(null, new object[] { signal });
                }
            }
        }

        [Test]
        public void MainPlayPolicy_RejectsPrematureAssemblyAccess()
        {
            var profile = ReadJson<VerificationProfile>(ProfilePath);
            var bridge = RuntimeType("MainPlayProcessBridge");
            var policy = bridge.GetMethod("IsAssemblyPartAvailable", BindingFlags.Public | BindingFlags.Static);
            var purlinId = (string)bridge.GetField("PurlinPartId", BindingFlags.Public | BindingFlags.Static)
                ?.GetRawConstantValue();

            Assert.That(policy, Is.Not.Null);
            Assert.That(purlinId, Is.Not.Null.And.Not.Empty);

            // 도리 설치와 공포 조립이 한 공정으로 합쳐졌으므로 판정 기준은 공정이 아니라 목표의
            // 신호 키다. 프로필의 모든 키를 넣어 보고, 조립 목표의 키에서만 열리는지 확인한다.
            var assemblyStage = profile.route.Single(stage =>
                !string.IsNullOrWhiteSpace(stage.purlinSignal) && !string.IsNullOrWhiteSpace(stage.gongpoSignal));

            var allSignals = profile.route
                .Where(stage => stage.signals != null)
                .SelectMany(stage => stage.signals)
                .Distinct(Names)
                .ToArray();

            foreach (var signal in allSignals)
            {
                var allowsPurlin = (bool)policy.Invoke(null, new object[] { signal, purlinId, false });
                var allowsGongpo = (bool)policy.Invoke(null, new object[] { signal, "gongpo-test-part", false });

                Assert.That(allowsPurlin, Is.EqualTo(Names.Equals(signal, assemblyStage.purlinSignal)),
                    $"신호 '{signal}'의 도리 접근 정책이 잘못됐습니다.");
                Assert.That(allowsGongpo, Is.EqualTo(Names.Equals(signal, assemblyStage.gongpoSignal)),
                    $"신호 '{signal}'의 공포 부재 접근 정책이 잘못됐습니다.");
            }

            Assert.That((bool)policy.Invoke(null, new object[] { null, purlinId, false }), Is.False,
                "목표가 없는 동안에는 어떤 조립 부재도 열려서는 안 됩니다.");

            var assembledPart =
                (bool)policy.Invoke(null, new object[] { assemblyStage.purlinSignal, purlinId, true });
            Assert.That(assembledPart, Is.False, "이미 조립된 부재를 다시 잡을 수 있게 열면 안 됩니다.");
        }

        [Test]
        public void MainPlayScene_ContainsIntegrationComponentsAndRestartTarget()
        {
            var profile = ReadJson<VerificationProfile>(ProfilePath);
            Assert.That(File.Exists(Absolute(profile.mainPlaySceneAsset)), Is.True,
                $"메인 플레이 씬 '{profile.mainPlaySceneAsset}'이 없습니다.");

            var sceneText = ReadSceneTextWithPrefabs(profile.mainPlaySceneAsset);
            // 공정 실행기는 QuestManager 하나다. ProcessRunner는 더 이상 Play 씬에 없다.
            AssertSceneContainsScript(sceneText, "Assets/@Scripts/Quest/QuestManager.cs");
            AssertSceneContainsScript(sceneText,
                "Assets/@Developers/RYU/ProcessIntegration/MainPlayProcessBridge.cs");
            AssertSceneContainsScript(sceneText, "Assets/@Scripts/Dialogue/InGameDialogue.cs");
            AssertSceneContainsScript(sceneText, "Assets/@Scripts/UI/PauseController.cs");
            AssertSceneContainsScript(sceneText, "Assets/@Scripts/UI/PauseMenuView.cs");

            var pauseTemplate = File.ReadAllText(Absolute(PauseUxmlPath));
            foreach (var elementName in new[]
                     {
                         "pause-root", "pause-menu-panel", "pause-options-panel", "pause-confirm-panel",
                         "resume-button", "pause-options-button", "restart-process-button", "main-menu-button",
                         "close-options-button", "confirm-yes-button", "confirm-no-button"
                     })
                StringAssert.Contains($"name=\"{elementName}\"", pauseTemplate,
                    $"FixedUI 일시정지 메뉴의 필수 요소 '{elementName}'이 없습니다.");

            var pauseStyle = File.ReadAllText(Absolute(PauseUssPath));
            foreach (var icon in new[]
                     {
                         "play_arrow_48dp_000000_FILL0_wght400_GRAD0_opsz48.png",
                         "settings_48dp_000000_FILL0_wght400_GRAD0_opsz48.png",
                         "replay_48dp_000000_FILL0_wght400_GRAD0_opsz48.png",
                         "home_48dp_000000_FILL0_wght400_GRAD0_opsz48.png"
                     })
            {
                Assert.That(File.Exists(Absolute($"{FixedUiIconPath}/{icon}")), Is.True,
                    $"FixedUI 원본 아이콘 '{icon}'이 없습니다.");
                Assert.That(File.Exists(Absolute($"{FixedUiIconPath}/{icon}.meta")), Is.True,
                    $"FixedUI 원본 아이콘 메타 '{icon}.meta'가 없습니다.");
                StringAssert.Contains($"FixedUI/Icons/{icon}", pauseStyle,
                    $"일시정지 메뉴가 FixedUI 원본 아이콘 '{icon}'을 참조하지 않습니다.");
            }

            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => Path.GetFileNameWithoutExtension(scene.path))
                .ToHashSet(Names);
            Assert.That(enabledScenes, Does.Contain(profile.mainPlayScene),
                "공정 재시작 대상인 메인 플레이 씬이 Build Settings에서 활성화되지 않았습니다.");

            var runner = RuntimeType("QuestManager");
            Assert.That(runner.GetEvent("QuestChanged"), Is.Not.Null,
                "연결 계층이 공정 진입을 동기화할 QuestChanged 이벤트가 없습니다.");
            Assert.That(runner.GetEvent("Completed"), Is.Not.Null,
                "연결 계층이 공정 종료를 알 수 있는 Completed 이벤트가 없습니다.");
            Assert.That(runner.GetProperty("Process"), Is.Not.Null,
                "연결 계층이 현재 공정을 읽을 수 없습니다.");
        }

        [Test]
        public void TutorialOutlineGuide_CoversEveryObjectInteractionStep()
        {
            var sceneText = ReadSceneTextWithPrefabs(TutorialScenePath);
            AssertSceneContainsScript(sceneText, "Assets/@Scripts/Quest/QuestManager.cs");
            AssertSceneContainsScript(sceneText, "Assets/@Scripts/Quest/UI/QuestHud.cs");
            AssertSceneContainsScript(sceneText, "Assets/@Scripts/Process/TutorialOutlineGuide.cs");
            AssertSceneContainsScript(sceneText, "Assets/QuickOutline/Scripts/Outline.cs");

            Assert.That(File.Exists(Absolute("Assets/QuickOutline/Resources/Materials/OutlineMask.mat")), Is.True);
            Assert.That(File.Exists(Absolute("Assets/QuickOutline/Resources/Materials/OutlineFill.mat")), Is.True);
            Assert.That(File.Exists(Absolute("Assets/@Developers/RYU/Quest/UI/QuestHud.uxml")), Is.True);
            const string questHudStylePath = "Assets/@Developers/RYU/Quest/UI/QuestHud.uss";
            const string questHudFontPath = "Assets/@Developers/RYU/Quest/UI/Fonts/Giants-Bold.ttf";
            Assert.That(File.Exists(Absolute(questHudStylePath)), Is.True);
            Assert.That(File.Exists(Absolute(questHudFontPath)), Is.True);
            var hudStyle = File.ReadAllText(Absolute(questHudStylePath));
            StringAssert.Contains("Fonts/Giants-Bold.ttf", hudStyle,
                "Quest HUD가 선별 임포트한 Giants 폰트를 참조하지 않습니다.");
            StringAssert.Contains("quest-card--blocked", hudStyle, "HUD에 진행 대기 상태 스타일이 없습니다.");
            StringAssert.Contains("quest-card--complete", hudStyle, "HUD에 완료 상태 스타일이 없습니다.");
            StringAssert.Contains("quest-card--error", hudStyle, "HUD에 오류 상태 스타일이 없습니다.");

            var hudTemplate = File.ReadAllText(Absolute("Assets/@Developers/RYU/Quest/UI/QuestHud.uxml"));
            foreach (var elementName in new[]
                     {
                         "quest-card", "quest-progress-fill", "quest-category", "quest-title", "quest-count",
                         "quest-goal", "quest-hint", "quest-state", "quest-skip"
                     })
                StringAssert.Contains($"name=\"{elementName}\"", hudTemplate,
                    $"Quest HUD 필수 요소 '{elementName}'이 없습니다.");

            var questManager = RuntimeType("QuestManager");
            Assert.That(questManager.GetProperty("State"), Is.Not.Null,
                "HUD가 읽을 퀘스트 런타임 상태가 공개되지 않았습니다.");
            Assert.That(questManager.GetProperty("ObjectiveBlockReason"), Is.Not.Null,
                "HUD가 일시정지·대화·컷씬 등의 진행 대기 사유를 읽을 수 없습니다.");
            Assert.That(questManager.GetProperty("FailureReason"), Is.Not.Null,
                "HUD가 퀘스트 로딩/그래프 오류 상태를 설명할 수 없습니다.");
            Assert.That(questManager.GetEvent("StateChanged"), Is.Not.Null,
                "HUD 갱신에 필요한 StateChanged 이벤트가 없습니다.");

            var processFile = ReadJson<ProcessFile>(ProcessPath);
            Assert.That(processFile.processes.Any(process => Names.Equals(process.process, "tutorial")), Is.False,
                "튜토리얼이 process.json에 중복 정의되어 있습니다.");

            var questFile = ReadJson<QuestFile>(QuestPath);
            var tutorial = questFile.quests.Single(quest => Names.Equals(quest.id, "tutorial"));
            var route = FollowQuestRoute(tutorial);
            var objectives = route
                .Where(node => Names.Equals(node.kind, "objective"))
                .Select(node => node.objective)
                .ToArray();
            var point = objectives.Single(step => Names.Equals(step.condition, "point"));
            var grab = objectives.Single(step => Names.Equals(step.condition, "grab"));
            var place = objectives.Single(step => Names.Equals(step.condition, "place"));

            Assert.That(point.target, Is.EqualTo("tool_saw"));
            Assert.That(grab.target, Is.EqualTo("tool_saw"));
            Assert.That(place.target, Is.EqualTo("socket_bench"));
            Assert.That(place.unlock, Does.Contain("tool_saw"));
        }

        [Test]
        public void TutorialQuestGraph_IsLinearReachableAndHasResolvableDialogue()
        {
            var questFile = ReadJson<QuestFile>(QuestPath);
            var dialogueFile = ReadJson<DialogueFile>(DialoguePath);
            var dialogueIds = UniqueBy(dialogueFile.sequences, sequence => sequence.id, DialoguePath);

            Assert.That(questFile.schemaVersion, Is.EqualTo(1));
            var quests = UniqueBy(questFile.quests, quest => quest.id, QuestPath);
            Assert.That(quests.TryGetValue("tutorial", out var tutorial), Is.True);
            Assert.That(tutorial.process, Is.EqualTo("tutorial").IgnoreCase);
            Assert.That(tutorial.title, Is.Not.Null.And.Not.Empty);

            var route = FollowQuestRoute(tutorial);
            Assert.That(route[0].kind, Is.EqualTo("entry").IgnoreCase);
            Assert.That(route[^1].kind, Is.EqualTo("complete").IgnoreCase);
            Assert.That(route.Count, Is.EqualTo(tutorial.nodes.Length),
                "튜토리얼 그래프에 진입점에서 도달할 수 없는 노드가 있습니다.");

            AssertDialogueExists(tutorial.introDialogue, dialogueIds, tutorial.id, "introDialogue");
            AssertDialogueExists(tutorial.completeDialogue, dialogueIds, tutorial.id, "completeDialogue");
            foreach (var node in route.Where(node => Names.Equals(node.kind, "objective")))
            {
                Assert.That(node.objective, Is.Not.Null, $"'{node.id}' 목표 데이터가 없습니다.");
                Assert.That(node.objective.id, Is.EqualTo(node.id),
                    $"'{node.id}' 노드와 목표 ID가 다릅니다.");
                Assert.That(node.objective.goal, Is.Not.Null.And.Not.Empty,
                    $"'{node.id}'의 인게임 목표 문구가 없습니다.");
                Assert.That(node.controlHint, Is.Not.Null.And.Not.Empty,
                    $"'{node.id}'의 인게임 조작 힌트가 없습니다.");
                AssertDialogueExists(node.objective.introDialogue, dialogueIds, tutorial.id,
                    $"{node.id}.introDialogue");
                AssertDialogueExists(node.objective.retryDialogue, dialogueIds, tutorial.id,
                    $"{node.id}.retryDialogue");
                AssertDialogueExists(node.objective.successDialogue, dialogueIds, tutorial.id,
                    $"{node.id}.successDialogue");
            }
        }

        /// <summary>진입 순서를 지킨 채 Signal 조건 목표만 추린다. 누계 amount 검증이 순서에 의존한다.</summary>
        static ProcessStep[] SignalObjectives(IReadOnlyList<QuestNode> route) =>
            route
                .Where(node => Names.Equals(node.kind, "objective") && node.objective != null &&
                               Names.Equals(node.objective.condition, "signal"))
                .Select(node => node.objective)
                .ToArray();

        static IReadOnlyList<QuestNode> FollowQuestRoute(QuestDefinition quest)
        {
            Assert.That(quest.nodes, Is.Not.Null.And.Not.Empty, $"'{quest.id}' 노드가 없습니다.");
            Assert.That(quest.edges, Is.Not.Null, $"'{quest.id}' 연결선이 없습니다.");

            var nodes = UniqueBy(quest.nodes, node => node.id, $"{QuestPath}:{quest.id}.nodes");
            Assert.That(nodes.ContainsKey(quest.entryNode), Is.True,
                $"'{quest.id}' 진입 노드 '{quest.entryNode}'를 찾지 못했습니다.");

            var outgoing = new Dictionary<string, QuestEdge>(Names);
            foreach (var edge in quest.edges)
            {
                Assert.That(nodes.ContainsKey(edge.from), Is.True, $"존재하지 않는 시작 노드 '{edge.from}'.");
                Assert.That(nodes.ContainsKey(edge.to), Is.True, $"존재하지 않는 도착 노드 '{edge.to}'.");
                Assert.That(outgoing.TryAdd(edge.from, edge), Is.True,
                    $"'{edge.from}'에 둘 이상의 다음 노드가 있습니다. 현재 런타임은 단일 경로만 지원합니다.");
            }

            var route = new List<QuestNode>();
            var visited = new HashSet<string>(Names);
            var current = quest.entryNode;
            while (true)
            {
                Assert.That(visited.Add(current), Is.True, $"'{quest.id}' 그래프에 순환이 있습니다.");
                var node = nodes[current];
                route.Add(node);

                if (Names.Equals(node.kind, "complete"))
                {
                    Assert.That(outgoing.ContainsKey(current), Is.False,
                        $"완료 노드 '{current}'에 다음 연결이 있습니다.");
                    break;
                }

                Assert.That(outgoing.TryGetValue(current, out var edge), Is.True,
                    $"'{current}'에 다음 연결이 없습니다.");
                current = edge.to;
            }

            return route;
        }

        /// <summary>
        /// 신호 키 표(<c>PartProcessSignals</c>)가 이 공정에 대해 내놓는 키 목록. 데이터와 코드가
        /// 같은 키를 쓰는지 확인하는 유일한 접점이다.
        /// </summary>
        static string[] InvokeBridgeSignals(string processName)
        {
            var signals = RuntimeType("PartProcessSignals");
            var processId = RuntimeType("ProcessId");
            var method = signals.GetMethod("SignalsForProcess", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            var result = method.Invoke(null, new[] { Enum.Parse(processId, processName, true) });
            Assert.That(result, Is.Not.Null, $"'{processName}'의 신호 키 목록이 null입니다.");
            return ((IEnumerable<string>)result).ToArray();
        }

        /// <summary>
        /// 씬 텍스트에 씬이 참조하는 프리팹 텍스트를 재귀적으로 이어 붙인다. Play 씬처럼 통합
        /// 컴포넌트가 프리팹(CoreSystems·PlayLoop) 안에 있으면 씬 YAML에는 스크립트 GUID가
        /// 직접 나타나지 않으므로, 프리팹까지 포함해야 "씬에 컴포넌트가 있다"를 판정할 수 있다.
        /// </summary>
        static string ReadSceneTextWithPrefabs(string scenePath)
        {
            var visited = new HashSet<string>(Names);
            var builder = new StringBuilder();
            Append(scenePath);
            return builder.ToString();

            void Append(string assetPath)
            {
                if (!visited.Add(assetPath)) return;

                var absolute = Absolute(assetPath);
                if (!File.Exists(absolute)) return;

                var text = File.ReadAllText(absolute);
                builder.Append(text).Append('\n');

                foreach (Match match in Regex.Matches(text, "guid: ([0-9a-f]{32})"))
                {
                    var referenced = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
                    if (!string.IsNullOrEmpty(referenced) &&
                        referenced.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                        Append(referenced);
                }
            }
        }

        static void AssertSceneContainsScript(string sceneText, string scriptPath)
        {
            var metaPath = Absolute(scriptPath + ".meta");
            Assert.That(File.Exists(metaPath), Is.True, $"'{scriptPath}.meta'가 없습니다.");

            var guidLine = File.ReadLines(metaPath)
                .FirstOrDefault(line => line.StartsWith("guid: ", StringComparison.Ordinal));
            Assert.That(guidLine, Is.Not.Null, $"'{scriptPath}.meta'에서 GUID를 찾지 못했습니다.");

            var guid = guidLine.Substring("guid: ".Length).Trim();
            Assert.That(sceneText, Does.Contain($"guid: {guid}"),
                $"메인 플레이 씬에 '{scriptPath}' 컴포넌트가 없습니다.");
        }

        static void AssertDialogueExists(
            string dialogueId,
            IReadOnlyDictionary<string, DialogueSequence> dialogueIds,
            string process,
            string field)
        {
            if (string.IsNullOrWhiteSpace(dialogueId)) return;
            Assert.That(dialogueIds.ContainsKey(dialogueId), Is.True,
                $"'{process}.{field}'가 존재하지 않는 대사 '{dialogueId}'를 참조합니다.");
        }

        static Dictionary<string, T> UniqueBy<T>(IEnumerable<T> items, Func<T, string> key, string source)
        {
            Assert.That(items, Is.Not.Null, $"'{source}'의 배열이 null입니다.");
            var result = new Dictionary<string, T>(Names);
            foreach (var item in items)
            {
                Assert.That(item, Is.Not.Null, $"'{source}' 배열에 null 항목이 있습니다.");
                var value = key(item);
                Assert.That(value, Is.Not.Null.And.Not.Empty, $"'{source}'에 키가 없는 항목이 있습니다.");
                Assert.That(result.TryAdd(value, item), Is.True,
                    $"'{source}'에 중복 키 '{value}'가 있습니다.");
            }

            return result;
        }

        static Type RuntimeType(string name)
        {
            var type = Type.GetType($"{name}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"런타임 타입 '{name}'을 찾지 못했습니다.");
            return type;
        }

        static T ReadJson<T>(string assetPath)
        {
            var path = Absolute(assetPath);
            Assert.That(File.Exists(path), Is.True, $"검증 입력 '{assetPath}'이 없습니다.");
            var result = JsonUtility.FromJson<T>(File.ReadAllText(path));
            Assert.That(result, Is.Not.Null, $"검증 입력 '{assetPath}'을 읽지 못했습니다.");
            return result;
        }

        static string Absolute(string assetPath) =>
            Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty, assetPath);

        [Serializable]
        sealed class VerificationProfile
        {
            public string name;
            public string mainPlayScene;
            public string mainPlaySceneAsset;
            public VerificationStage[] route;
        }

        [Serializable]
        sealed class VerificationStage
        {
            public string process;
            public string destinationKind;
            public string destination;

            /// <summary>quest.json에 실행 가능한 퀘스트 그래프가 있어야 하는 구간.</summary>
            public bool requiresQuestDefinition;

            /// <summary>
            /// 이 공정이 쓰는 신호 키 전부. 공정이 부재 단위가 되면서 한 공정이 작업 종류마다
            /// 다른 키를 쓰므로 배열이다. 순서는 목표 진행 순서와 같아야 한다.
            /// </summary>
            public string[] signals;

            /// <summary>조립 구간에서 도리 부재만 열려야 하는 목표의 키. 없으면 검사하지 않는다.</summary>
            public string purlinSignal;

            /// <summary>조립 구간에서 공포 부재만 열려야 하는 목표의 키. 없으면 검사하지 않는다.</summary>
            public string gongpoSignal;
        }

        [Serializable]
        sealed class FlowFile { public FlowEntry[] entries; }

        [Serializable]
        sealed class FlowEntry
        {
            public string process;
            public string scene;
            public string cutscene;
        }

        [Serializable]
        sealed class ProcessFile { public ProcessDefinition[] processes; }

        [Serializable]
        sealed class ProcessDefinition
        {
            public string process;
            public string introDialogue;
            public string completeDialogue;
            public ProcessStep[] steps;
        }

        [Serializable]
        sealed class ProcessStep
        {
            public string id;
            public string condition;
            public string target;
            public string[] unlock;
            public float amount = 1f;
            public string goal;
            public string introDialogue;
            public string retryDialogue;
            public string successDialogue;
        }

        [Serializable]
        sealed class QuestFile
        {
            public int schemaVersion;
            public QuestDefinition[] quests;
        }

        [Serializable]
        sealed class QuestDefinition
        {
            public string id;
            public string title;
            public string process;
            public string entryNode;
            public string introDialogue;
            public string completeDialogue;
            public QuestNode[] nodes;
            public QuestEdge[] edges;
        }

        [Serializable]
        sealed class QuestNode
        {
            public string id;
            public string kind;
            public string controlHint;
            public string controlHintVr;
            public ProcessStep objective;
        }

        [Serializable]
        sealed class QuestEdge
        {
            public string from;
            public string to;
        }

        [Serializable]
        sealed class DialogueFile { public DialogueSequence[] sequences; }

        [Serializable]
        sealed class DialogueSequence
        {
            public string id;
            public DialogueLine[] lines;
        }

        [Serializable]
        sealed class DialogueLine
        {
            public string id;
            public string text;
        }

        [Serializable]
        sealed class CutsceneFile { public CutsceneDefinition[] cutscenes; }

        [Serializable]
        sealed class CutsceneDefinition
        {
            public string id;
            public string scene;
            public string video;
            public string nextScene;
        }
    }
}
