#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.ChampionMode;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.ChampionMode.Presentation;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Editor.Development.FirstUserGameTest;
using AL.Services.Local;
using AL.UI.CharacterCreation;
using AL.UI.FirstUserIdentity;
using AL.UI.RealmSelection;
using AL.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AL.Tests.PlayMode.FirstUserGameTest
{
    public sealed class IntegratedWritableFirstUserJourneyTests
    {
        private const string BootPath = "Assets/AL/Scenes/Boot.unity";
        private const string RealmSelectionPath = "Assets/AL/Scenes/RealmSelection.unity";
        private const string CharacterCreationPath = "Assets/AL/Scenes/CharacterCreation.unity";
        private const string KingdomPath = "Assets/AL/Scenes/Kingdom.unity";
        private const string ChampionArenaPath = "Assets/AL/Scenes/ChampionArena.unity";
        private const float TimeoutSeconds = 30f;

        private readonly List<string> _visitedScenePaths = new List<string>();
        private bool _originalIgnoreFailingMessages;
        private JourneyLogTap _logs;
        private string _isolatedSaveRoot = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _visitedScenePaths.Clear();
            _originalIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            ProofOfWorthDirector.ResetForTests();
            FirstWorldEntryTutorialDirector.ResetForTests();
            CharacterCreationIdentity.ResetClaims();
            SliceRunState.Reset();
            ClearServiceLocator();
            ResetStackOverrides();
            object countingResource = NewInternal("AL.Core.CountingResourceService");
            Func<object, object> resourceFactory = _ => countingResource;
            SetStackOverride("ResourceFactoryOverride", resourceFactory);
            _logs = new JourneyLogTap();
            _logs.Start();
            SceneManager.sceneLoaded += RecordScene;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= RecordScene;
            yield return UnloadIntoEmptyScene();
            _logs?.Stop();
            var unexpected = _logs == null
                ? new List<string>()
                : _logs.Errors
                    .Where(message => !message.Contains("BOOT_STACK_RUNTIME_OWNER_REJECTED"))
                    .ToList();
            ResetStackOverrides();
            ClearServiceLocator();
            ProofOfWorthDirector.ResetForTests();
            FirstWorldEntryTutorialDirector.ResetForTests();
            CharacterCreationIdentity.ResetClaims();
            SliceRunState.Reset();
            if (!string.IsNullOrEmpty(_isolatedSaveRoot) && Directory.Exists(_isolatedSaveRoot))
            {
                Directory.Delete(_isolatedSaveRoot, true);
            }

            _isolatedSaveRoot = string.Empty;
            LogAssert.ignoreFailingMessages = _originalIgnoreFailingMessages;
            Assert.That(unexpected, Is.Empty,
                "Integrated first-user journey emitted unexpected severe logs, including during cleanup:\n" +
                string.Join("\n", unexpected));
        }

        [UnityTest]
        public IEnumerator FreshIsolatedWritableProfileCompletesOnboardingProofOfWorthAndGuardianWithoutKingdom()
        {
            Assert.That(
                FirstUserOnboardingEnvironmentRegistry.TryResolve(out _, out _),
                Is.True,
                "The final launch trial requires the admitted authored MVP packet.");

            _isolatedSaveRoot = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-FullFirstUserJourney",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_isolatedSaveRoot);
            ISaveGameService isolatedSave = CreateIsolatedLocalSaveService(_isolatedSaveRoot);
            Func<object> saveFactory = () => isolatedSave;
            SetStackOverride("SaveGameFactoryOverride", saveFactory);

            yield return LoadAndSettle(BootPath);
            Button continueButton = null;
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (continueButton == null || !continueButton.interactable)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    "Boot did not expose the explicit Finished Loading action.");
                GameObject readiness = GameObject.Find("LaunchReadinessCanvas");
                continueButton = readiness == null
                    ? null
                    : readiness.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(button => button.name == "FinishedLoadingAction");
                yield return null;
            }

            continueButton.onClick.Invoke();
            yield return WaitForActiveScene(RealmSelectionPath);
            GameObject realmCanvas = GameObject.Find("RealmSelectionCanvas");
            Assert.That(realmCanvas, Is.Not.Null);
            string[] realmNames = { "Crownlands", "Stonehold", "Eldergrove", "Umbral" };
            Button[] realmButtons = realmCanvas.GetComponentsInChildren<Button>(true)
                .Where(button => realmNames.Contains(button.name, StringComparer.Ordinal))
                .ToArray();
            Assert.That(realmButtons.Select(button => button.name),
                Is.EquivalentTo(realmNames));
            Button eldergrove = realmButtons.Single(button => button.name == "Eldergrove");
            Assert.That(
                eldergrove.GetComponentsInChildren<Text>(true)
                    .Any(text => text.text == "Eldergrove Elves"),
                Is.True,
                "The selected realm must visibly lock its people before commitment.");
            eldergrove.onClick.Invoke();
            RealmSelectionController realmController = Object.FindObjectOfType<RealmSelectionController>();
            Assert.That(realmController, Is.Not.Null);
            Assert.That(realmController.PendingRealmId, Is.EqualTo(RealmId.Eldergrove));
            Assert.That(realmController.IsCommitOverlayVisible, Is.True,
                "Considering a realm must open the binding ritual without persisting it.");
            Button bindRealm = realmCanvas.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == RealmSelectionCommitOverlay.ConfirmButtonName);
            bindRealm.onClick.Invoke();

            yield return WaitForActiveScene(CharacterCreationPath);
            Assert.That(isolatedSave.CurrentSave, Is.Not.Null);
            Assert.That(isolatedSave.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.Eldergrove));
            Assert.That(
                ImplementsInterface(
                    isolatedSave,
                    "AL.Services.Local.ILegacyRealmSelectionCandidateStore"),
                Is.True);
            Assert.That(
                isolatedSave.LastSaveStatus,
                Is.EqualTo(SaveOperationStatus.SavedPrimary),
                "The realm commit must exercise the production typed candidate authority and persist.");
            Assert.That(File.Exists(Path.Combine(_isolatedSaveRoot, "save.json")), Is.True);

            GameObject creatorCanvas = null;
            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (creatorCanvas == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    "Character Creation did not build its production surface.");
                creatorCanvas = GameObject.Find(CharacterCreationProductionLayout.CanvasName);
                yield return null;
            }

            Text people = creatorCanvas.GetComponentsInChildren<Text>(true)
                .Single(text => text.name == "People");
            Assert.That(people.text, Does.Contain("Elven people"));
            Assert.That(people.text, Does.Contain("locked to this realm"));
            Button ranger = creatorCanvas.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Class_Ranger");
            InputField username = creatorCanvas.GetComponentsInChildren<InputField>(true)
                .Single(field => field.name == CharacterCreationProductionLayout.UsernameName);
            Button confirm = creatorCanvas.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == CharacterCreationProductionLayout.ConfirmName);
            ranger.onClick.Invoke();
            username.text = "EldergroveScout";
            Assert.That(confirm.interactable, Is.True);
            confirm.onClick.Invoke();

            yield return WaitForActiveScene(ChampionArenaPath);
            Assert.That(_visitedScenePaths, Does.Not.Contain(KingdomPath));
            Assert.That(SceneManager.GetSceneByPath(KingdomPath).isLoaded, Is.False);
            Assert.That(GameObject.Find(FirstSessionChampionStart.EnvironmentRootName), Is.Not.Null,
                "The first user must land in the 3D inner-realm capital, never Kingdom.");
            GameObject authoredEnvironment =
                GameObject.Find(FirstSessionChampionStart.EnvironmentRootName);
            FirstSessionAuthoredWorldMarker authoredMarker =
                authoredEnvironment.GetComponent<FirstSessionAuthoredWorldMarker>();
            Assert.That(authoredMarker, Is.Not.Null);
            Assert.That(authoredMarker.Realm, Is.EqualTo(RealmId.Eldergrove));
            Assert.That(authoredEnvironment.transform.Find(
                    FirstSessionAuthoredWorldBuilder.HallName),
                Is.Not.Null);
            Assert.That(authoredEnvironment.transform.Find(
                    FirstSessionAuthoredWorldBuilder.StructuralIdentityPrefix +
                    RealmId.Eldergrove),
                Is.Not.Null);
            Assert.That(GameObject.Find(FirstSessionChampionStart.TemporaryPlaqueName),
                Is.Null,
                "The authored launch may not render a greybox plaque.");

            MvpLoopSnapshot identity = MvpLoopSaveCodec.Read(isolatedSave.CurrentSave);
            Assert.That(identity.Realm, Is.EqualTo(RealmId.Eldergrove));
            Assert.That(identity.People, Is.EqualTo(FirstUserRace.Elves));
            Assert.That(identity.ClassFamily, Is.EqualTo(ClassFamily.Ranger));
            Assert.That(identity.Username, Is.EqualTo("EldergroveScout"));
            Assert.That(identity.IdentityConfirmed, Is.True);

            ChampionArenaSceneController arena =
                Object.FindObjectOfType<ChampionArenaSceneController>();
            ChampionController player = Object.FindObjectOfType<ChampionController>();
            ProofOfWorthDirector proof = Object.FindObjectOfType<ProofOfWorthDirector>();
            Assert.That(arena, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(player.isActiveAndEnabled, Is.True,
                "Direct 3D champion control must be live in the inner realm.");
            Transform authoredChampion = player.transform.Find(
                FirstSessionAuthoredVisualBinder.ChampionVisualName);
            Assert.That(authoredChampion, Is.Not.Null);
            Assert.That(authoredChampion.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,
                Is.GreaterThan(0));
            Assert.That(player.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => !renderer.transform.IsChildOf(authoredChampion))
                    .All(renderer =>
                        !renderer.enabled || !renderer.gameObject.activeInHierarchy),
                Is.True,
                "Procedural mannequin renderers must be hidden on the authored launch.");
            Assert.That(proof, Is.Not.Null);
            Assert.That(proof.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenOffered));
            Assert.That(proof.State.QuestId, Is.EqualTo(ProofOfWorthIds.OmenQuestId));
            Assert.That(proof.State.OmenAccepted, Is.False,
                "OMEN_1 must be offered and require an explicit player choice.");

            Vector3 movementStart = player.transform.position;
            player.SetExternalMoveInput(Vector2.up);
            yield return new WaitForSeconds(0.2f);
            player.SetExternalMoveInput(Vector2.zero);
            Vector3 moved = player.transform.position - movementStart;
            moved.y = 0f;
            Assert.That(moved.magnitude, Is.GreaterThan(0.05f),
                "The integrated journey must exercise live direct movement.");

            proof.ChoosePrimary();
            Assert.That(proof.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenTalk));
            Assert.That(proof.State.OmenAccepted, Is.True);
            proof.ChoosePrimary();
            proof.ChoosePrimary();
            Assert.That(proof.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenArena));
            Assert.That(
                GameObject.Find(ProofOfWorthIds.SkyCastleMarkerId + "_TEMPORARY"),
                Is.Not.Null);
            Assert.That(
                proof.ApplyForTests(ProofOfWorthCommand.ArenaSuccess).Changed,
                Is.True);
            Assert.That(proof.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenReport));
            proof.ChoosePrimary();
            proof.ChoosePrimary();
            proof.ChoosePrimary();
            Assert.That(proof.State.Phase, Is.EqualTo(ProofOfWorthPhase.C1MeetGuide));
            Assert.That(proof.State.QuestId, Is.EqualTo(ProofOfWorthIds.MainQuestId));
            Assert.That(
                GameObject.Find(ProofOfWorthIds.MeetGuideObjectiveId + "_TEMPORARY"),
                Is.Not.Null);
            Assert.That(
                proof.ApplyForTests(ProofOfWorthCommand.MeetRealmGuide).Changed,
                Is.True);
            Assert.That(proof.State.Phase, Is.EqualTo(ProofOfWorthPhase.C1RestoreCovenant));
            Assert.That(
                GameObject.Find(ProofOfWorthIds.RestoreCovenantObjectiveId + "_TEMPORARY"),
                Is.Not.Null);
            Assert.That(
                proof.ApplyForTests(ProofOfWorthCommand.RestoreCovenant).Changed,
                Is.True);
            Assert.That(proof.State.Phase, Is.EqualTo(ProofOfWorthPhase.C1FaceGuardian));

            yield return null;
            Assert.That(arena.TryStartGuardianTrial(), Is.True);
            BossDummyAI guardian = Object.FindObjectOfType<BossDummyAI>();
            Assert.That(guardian, Is.Not.Null);
            Assert.That(guardian.gameObject.activeInHierarchy, Is.True);
            Transform authoredGuardian = guardian.transform.Find(
                FirstSessionAuthoredVisualBinder.GuardianVisualName);
            Assert.That(authoredGuardian, Is.Not.Null);
            Assert.That(authoredGuardian.GetComponentInChildren<SkinnedMeshRenderer>(true),
                Is.Not.Null);
            Assert.That(authoredGuardian.GetComponentInChildren<Animator>(true),
                Is.Not.Null);
            AuthoredGuardianMotion guardianMotion =
                authoredGuardian.GetComponent<AuthoredGuardianMotion>();
            Assert.That(guardianMotion, Is.Not.Null);
            Assert.That(guardianMotion.Clip, Is.Not.Null);
            Assert.That(guardianMotion.IsPlaying, Is.True);
            Assert.That(authoredGuardian.GetComponentsInChildren<Renderer>(true)
                    .All(renderer =>
                        renderer.sharedMaterial != null &&
                        renderer.sharedMaterial.GetTexture("_MainTex") != null),
                Is.True,
                "The live guardian must use the admitted textured PBR sentinel.");
            yield return new WaitForSecondsRealtime(2.8f);

            MovePlayerToPosition(
                player,
                guardian.transform.position - Vector3.forward * 1.25f);
            player.transform.rotation = Quaternion.LookRotation(
                guardian.transform.position - player.transform.position,
                Vector3.up);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            int attacks = 0;
            bool requestedAttackReducedGuardianHealth = false;
            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!guardian.IsDead)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    "The direct-control guardian fight did not resolve within the bounded trial.");
                MovePlayerToPosition(
                    player,
                    guardian.transform.position - guardian.transform.forward * 1.25f);
                player.transform.rotation = Quaternion.LookRotation(
                    guardian.transform.position - player.transform.position,
                    Vector3.up);
                Physics.SyncTransforms();
                int beforeAttack = player.EditorBasicAttackSequence;
                float healthBeforeAttack = guardian.CurrentHealth;
                player.RequestBasicAttack();
                float attackDeadline = Time.realtimeSinceStartup + 1f;
                while (player.EditorBasicAttackSequence == beforeAttack &&
                       Time.realtimeSinceStartup < attackDeadline)
                {
                    yield return null;
                }

                Assert.That(player.EditorBasicAttackSequence, Is.GreaterThan(beforeAttack),
                    "RequestBasicAttack must advance the live editor attack sequence.");
                attacks++;
                // PerformAttack spends up to 0.1s lunging before its 0.5s cooldown.
                yield return new WaitForSeconds(0.7f);
                requestedAttackReducedGuardianHealth |=
                    guardian.CurrentHealth < healthBeforeAttack;
            }

            Assert.That(attacks, Is.GreaterThan(0));
            Assert.That(requestedAttackReducedGuardianHealth, Is.True,
                "At least one requested live basic attack must visibly reduce guardian health.");
            Assert.That(guardian.IsDead, Is.True,
                "The catalog guardian must be defeated by the live champion attack path.");
            ProofOfWorthTransition guardianDefeated =
                proof.ApplyForTests(ProofOfWorthCommand.GuardianDefeated);
            Assert.That(guardianDefeated.Changed, Is.True);
            Assert.That(proof.State.Phase, Is.EqualTo(ProofOfWorthPhase.C1AcceptMark));
            Assert.That(
                GameObject.Find(ProofOfWorthIds.AcceptMarkObjectiveId + "_TEMPORARY"),
                Is.Not.Null);
            Assert.That(
                proof.ApplyForTests(ProofOfWorthCommand.AcceptMark).Changed,
                Is.True);

            Assert.That(proof.State.Phase, Is.EqualTo(ProofOfWorthPhase.LordshipGranted));
            Assert.That(proof.PersistAttempted, Is.True);
            Assert.That(proof.State.ChapterVariantId,
                Is.EqualTo(ProofOfWorthIds.EldergroveVariantId));
            Assert.That(ProofOfWorthLordship.IsGranted(isolatedSave.CurrentSave), Is.True);
            Assert.That(
                ImplementsInterface(
                    isolatedSave,
                    "AL.Services.Local.ILegacyMvpLoopCandidateStore"),
                Is.True);
            Assert.That(isolatedSave.LastSaveStatus, Is.EqualTo(SaveOperationStatus.SavedPrimary));
            Assert.That(ProfileMutationContainment.ProductionWriteActivationEnabled, Is.False,
                "The integrated harness must not weaken the general production mutation latch.");
            Assert.That(
                ProfileWriteAuthorityProviderGuard.IsCurrentWritable(
                    (IProfileWriteAuthorityProvider)isolatedSave),
                Is.False,
                "Only the reviewed typed first-user candidate seams may write during this trial.");

            ISaveGameService reloaded = CreateIsolatedLocalSaveService(_isolatedSaveRoot);
            reloaded.Load();
            MvpLoopSnapshot persisted = MvpLoopSaveCodec.Read(reloaded.CurrentSave);
            Assert.That(persisted.Realm, Is.EqualTo(RealmId.Eldergrove));
            Assert.That(persisted.ClassFamily, Is.EqualTo(ClassFamily.Ranger));
            Assert.That(persisted.Username, Is.EqualTo("EldergroveScout"));
            Assert.That(persisted.LastResultId,
                Is.EqualTo(ProofOfWorthIds.EldergroveVariantId));
            Assert.That(_visitedScenePaths, Does.Not.Contain(KingdomPath));
        }

        private void RecordScene(Scene scene, LoadSceneMode mode)
        {
            if (!string.IsNullOrEmpty(scene.path))
            {
                _visitedScenePaths.Add(scene.path);
            }
        }

        private static IEnumerator LoadAndSettle(string path)
        {
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                path,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(load, Is.Not.Null, "Expected a scene load operation for " + path + ".");
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!load.isDone)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    "Timed out loading " + path + ".");
                yield return null;
            }

            yield return null;
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitForActiveScene(string path)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!string.Equals(SceneManager.GetActiveScene().path, path, StringComparison.Ordinal))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    "Timed out waiting for active scene " + path + ".");
                yield return null;
            }

            yield return null;
            yield return null;
            yield return null;
        }

        private static void MovePlayerToPosition(ChampionController player, Vector3 position)
        {
            player.SetExternalMoveInput(Vector2.zero);
            player.TeleportTo(position);
            Physics.SyncTransforms();
        }

        private static ISaveGameService CreateIsolatedLocalSaveService(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(constructor, Is.Not.Null,
                "Expected the production save service's isolated-root constructor.");
            return (ISaveGameService)constructor.Invoke(new object[] { root });
        }

        private static bool ImplementsInterface(object instance, string interfaceName)
        {
            return instance != null && instance.GetType().GetInterfaces()
                .Any(candidate => string.Equals(
                    candidate.FullName,
                    interfaceName,
                    StringComparison.Ordinal));
        }

        private static IEnumerator UnloadIntoEmptyScene()
        {
            Scene empty = SceneManager.CreateScene(
                "IntegratedWritableFirstUserCleanup_" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene == empty || !scene.isLoaded)
                {
                    continue;
                }

                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }
        }

        private static object NewInternal(string typeName)
        {
            return Activator.CreateInstance(RuntimeType(typeName), true);
        }

        private static Type RuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Expected loaded runtime type " + typeName + ".");
            return type;
        }

        private static void SetStackOverride(string fieldName, object value)
        {
            RuntimeType("AL.Core.OfflineServiceStack")
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, value);
        }

        private static void ResetStackOverrides()
        {
            foreach (string field in new[]
            {
                "GameDataFactoryOverride", "SaveGameFactoryOverride", "ResourceFactoryOverride",
                "NotificationFactoryOverride", "BossLootFactoryOverride"
            })
            {
                SetStackOverride(field, null);
            }
        }

        private static void ClearServiceLocator()
        {
            Type serviceLocator = RuntimeType("AL.Core.ServiceLocator");
            FieldInfo services = serviceLocator.GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);
            (services.GetValue(null) as System.Collections.IDictionary)?.Clear();
        }

        private sealed class JourneyLogTap
        {
            private readonly List<string> _errors = new List<string>();
            private bool _started;

            internal IReadOnlyList<string> Errors => _errors;

            internal void Start()
            {
                if (_started)
                {
                    return;
                }

                Application.logMessageReceived += Handle;
                _started = true;
            }

            internal void Stop()
            {
                if (!_started)
                {
                    return;
                }

                Application.logMessageReceived -= Handle;
                _started = false;
            }

            private void Handle(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                {
                    _errors.Add(condition ?? string.Empty);
                }
            }
        }
    }
}
#endif
