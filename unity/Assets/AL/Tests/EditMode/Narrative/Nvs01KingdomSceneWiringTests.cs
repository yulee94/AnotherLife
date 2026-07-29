using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using AL.RealmSelection;
using AL.Services.Local;
using AL.UI.Kingdom;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.Narrative
{
    public sealed class Nvs01KingdomSceneWiringTests
    {
        private GameObject _host;
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            ServicesDictionary().Clear();
            ServiceLocator.Register<IRealmService>(new CommittedRealmService(RealmId.Crownlands));
            _saveRoot = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-Nvs01KingdomTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            var saveService = CreateSaveService(_saveRoot);
            saveService.CreateNewSave(RealmId.Crownlands);
            ServiceLocator.Register<ISaveGameService>(saveService);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                UnityEngine.Object.DestroyImmediate(_host);
            }

            GameObject canvas = GameObject.Find("KingdomCanvas");
            if (canvas != null)
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }

            ServicesDictionary().Clear();
            if (!string.IsNullOrEmpty(_saveRoot) && Directory.Exists(_saveRoot))
            {
                Directory.Delete(_saveRoot, true);
            }
        }

        [Test]
        public void ProductionPanelUsesVerifiedPacketAndDrivesOfferToFailClosedDeploy()
        {
            KingdomSceneController controller = CreateController(profileReady: true);

            Nvs01KingdomView initial = CurrentView(controller);
            Assert.AreEqual("OFFERED", initial.StateId);
            Assert.AreEqual(Localize("quest.omen1.title"), initial.Title);
            Assert.That(QuestText(controller), Does.Contain(initial.Title));
            Assert.That(QuestText(controller), Does.Contain(Localize("objective.omen1.talk")));
            AssertButtonLabels(controller, Localize("npc.valerius.name"));

            Click(controller, Localize("npc.valerius.name"));
            Nvs01KingdomView offer = CurrentView(controller);
            Assert.AreEqual(Localize("dialogue.omen1.offer"), offer.DialogueText);
            AssertButtonLabels(
                controller,
                Localize("choice.omen1.accept"),
                Localize("choice.omen1.decline"));
            Assert.That(MessageText(controller), Does.Contain(offer.DialogueText));

            Click(controller, Localize("choice.omen1.accept"));
            AssertButtonLabels(
                controller,
                Localize("choice.omen1.investigate"),
                Localize("choice.omen1.ask_more"));

            Click(controller, Localize("choice.omen1.investigate"));
            AssertButtonLabels(controller, Localize("choice.omen1.deploy"));

            Click(controller, Localize("choice.omen1.deploy"));
            Nvs01KingdomView arenaStart = CurrentView(controller);
            Assert.AreEqual(Nvs01KingdomActionKind.InvokeSemanticAction, arenaStart.PrimaryAction);
            AssertButtonLabels(controller, Localize("choice.omen1.deploy"));

            Click(controller, Localize("choice.omen1.deploy"));
            Nvs01KingdomView unavailable = CurrentView(controller);
            Assert.AreEqual(Nvs01KingdomViewStatus.Attention, unavailable.Status);
            Assert.AreEqual("AL-NVS01-DEPENDENCY-UNAVAILABLE", unavailable.DiagnosticCode);
            Assert.That(MessageText(controller), Does.Contain(unavailable.DiagnosticCode));

            var presenter = Field<Nvs01KingdomPresenter>(controller, "_nvs01Presenter");
            Assert.AreEqual("TALK_TO_VALERIUS", presenter.Runtime.Snapshot.StateId);
            Assert.IsNull(presenter.Runtime.Snapshot.CurrentEncounter);
        }

        [Test]
        public void MissingReadyProfileKeepsQuestControlsUnavailable()
        {
            KingdomSceneController controller = CreateController(profileReady: false);

            SetField(controller, "_nvs01CatalogLoading", true);
            Invoke(controller, "RefreshNvs01QuestPanel");
            var initialization = (IEnumerator)Invoke(controller, "InitializeNvs01QuestPresentation");

            Assert.False(initialization.MoveNext());
            Assert.IsNull(CurrentView(controller));
            Assert.That(QuestText(controller), Does.Contain("TEMPORARILY UNAVAILABLE"));
            Assert.IsEmpty(ActionButtons(controller));
        }

        [Test]
        public void CatalogFailureIsVisibleAndCreatesNoActions()
        {
            KingdomSceneController controller = CreateController(profileReady: true);

            Invoke(controller, "RenderNvs01CatalogUnavailable", new object[] { null });

            Nvs01KingdomView view = CurrentView(controller);
            Assert.AreEqual(Nvs01KingdomViewStatus.Unavailable, view.Status);
            Assert.AreEqual("AL-NVS01-CATALOG-MISSING", view.DiagnosticCode);
            Assert.That(QuestText(controller), Does.Contain(view.DiagnosticCode));
            Assert.IsEmpty(ActionButtons(controller));
        }

        [Test]
        public void ProductionCapabilitiesDeclareOnlyTheMountedKingdomView()
        {
            Nvs01Catalog catalog = VerifiedCatalog().Catalog;
            var snapshot = (Nvs01CapabilitySnapshot)typeof(KingdomSceneController)
                .GetMethod(
                    "BuildNvs01CapabilitySnapshot",
                    BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { catalog });

            Assert.True(snapshot.IsAvailable(catalog.Placement.CompletionDestination));
            foreach (string capability in new[]
                     {
                         "LOCATION_SKY_CASTLE_MARKER",
                         "ACTION_DEPLOY_CHAMPION",
                         "HOOK_SKY_CASTLE_ARENA",
                         "EVENT_SKY_CASTLE_ARENA_SUCCESS",
                         "EVENT_SKY_CASTLE_ARENA_FAILURE",
                         "EVENT_SKY_CASTLE_ARENA_CANCELLED",
                         "EVENT_SKY_CASTLE_ARENA_UNAVAILABLE"
                     })
            {
                Assert.False(snapshot.IsAvailable(capability), capability);
            }
        }

        [Test]
        public void ProductionPanelReloadsTheLastVerifiedChoiceFromDisk()
        {
            KingdomSceneController controller = CreateController(profileReady: true);
            Click(controller, Localize("npc.valerius.name"));
            Click(controller, Localize("choice.omen1.accept"));

            Assert.AreEqual("TALK_TO_VALERIUS", CurrentView(controller).StateId);

            var reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();
            Assert.AreEqual(
                Nvs01ProgressData.CurrentVersion,
                reloaded.CurrentSave.Nvs01Progress.Version);
            Assert.AreEqual(
                "TALK_TO_VALERIUS",
                reloaded.CurrentSave.Nvs01Progress.StateId);
            ServiceLocator.Register<ISaveGameService>(reloaded);

            DestroyControllerUi();
            controller = CreateController(profileReady: true);

            Assert.AreEqual("TALK_TO_VALERIUS", CurrentView(controller).StateId);
            AssertButtonLabels(
                controller,
                Localize("choice.omen1.investigate"),
                Localize("choice.omen1.ask_more"));
        }

        [Test]
        public void ForwardProgressVersionFailsClosedWithoutActions()
        {
            var saveService = ServiceLocator.Get<ISaveGameService>();
            saveService.CurrentSave.Nvs01Progress.Version =
                Nvs01ProgressData.CurrentVersion + 1;

            KingdomSceneController controller = CreateController(profileReady: true);

            Assert.AreEqual(
                Nvs01KingdomViewStatus.Unavailable,
                CurrentView(controller).Status);
            Assert.AreEqual(
                "AL-NVS01-SAVE-PROGRESS-UNAVAILABLE",
                CurrentView(controller).DiagnosticCode);
            Assert.IsEmpty(ActionButtons(controller));
        }

        [TestCase("blank-state")]
        [TestCase("inactive-encounter")]
        [TestCase("inactive-operation")]
        public void RuntimeInconsistentProgressLoadsOnlyAsReadOnlyEvidence(
            string scenario)
        {
            KingdomSceneController controller = CreateController(profileReady: true);
            Click(controller, Localize("npc.valerius.name"));
            Click(controller, Localize("choice.omen1.accept"));

            var saveService =
                (LocalSaveGameService)ServiceLocator.Get<ISaveGameService>();
            Nvs01ProgressData progress = saveService.CurrentSave.Nvs01Progress;
            Assert.AreEqual(Nvs01ProgressData.CurrentVersion, progress.Version);
            switch (scenario)
            {
                case "blank-state":
                    progress.StateId = string.Empty;
                    break;
                case "inactive-encounter":
                    progress.HasCurrentEncounter = false;
                    progress.CurrentEncounter.RequestId =
                        "00000000-0000-0000-0000-000000000001";
                    break;
                case "inactive-operation":
                    Assert.True(progress.HasLastOperation);
                    progress.HasLastOperation = false;
                    break;
                default:
                    Assert.Fail("Unknown malformed progress scenario.");
                    break;
            }

            WriteCanonicalSave(saveService.CurrentSave);
            var reloaded = CreateSaveService(_saveRoot);
            reloaded.Load();

            Assert.AreEqual(
                SaveLoadStatus.LoadedPrimaryDegraded,
                reloaded.LastLoadStatus);
            Assert.IsNull(reloaded.CurrentSave);
            Assert.IsNotNull(reloaded.ReadOnlyCandidateSnapshot);
            Assert.False(reloaded.LastLoadDisposition.IsRuntimeUsable);
            Assert.False(reloaded.LastLoadDisposition.IsWritable);
        }

        [Test]
        public void ControllerDoesNotDuplicateNarrativeOrUseTheDevelopmentFactory()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                "UI",
                "Kingdom",
                "KingdomSceneController.cs");
            string source = File.ReadAllText(path);

            foreach (string narrativeText in VerifiedCatalog().Catalog.Localization.Values)
            {
                Assert.That(source, Does.Not.Contain(narrativeText));
            }

            Assert.That(source, Does.Not.Contain("Nvs01KingdomPresenter.CreateInMemory"));
            Assert.That(source, Does.Not.Contain("SceneManager"));
            Assert.That(source, Does.Contain("Nvs01CatalogLoader.Shared"));
            Assert.That(source, Does.Contain("Nvs01RealmContextAdapter.FromCommittedIdentity"));
        }

        private KingdomSceneController CreateController(bool profileReady)
        {
            _host = new GameObject("Nvs01KingdomSceneWiringTests");
            var controller = _host.AddComponent<KingdomSceneController>();
            SetField(controller, "_profileReady", profileReady);
            Invoke(controller, "BuildRuntimeUi");
            Invoke(controller, "InitializeNvs01Presenter", VerifiedCatalog());
            return controller;
        }

        private void DestroyControllerUi()
        {
            if (_host != null)
            {
                UnityEngine.Object.DestroyImmediate(_host);
                _host = null;
            }

            GameObject canvas = GameObject.Find("KingdomCanvas");
            if (canvas != null)
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        private static LocalSaveGameService CreateSaveService(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(constructor);
            return (LocalSaveGameService)constructor.Invoke(new object[] { root });
        }

        private void WriteCanonicalSave(SaveGameData save)
        {
            string json = JsonUtility.ToJson(save, true);
            var encoding = new UTF8Encoding(false, true);
            File.WriteAllText(
                Path.Combine(_saveRoot, "save.json"),
                json,
                encoding);
            File.WriteAllText(
                Path.Combine(_saveRoot, "save.backup.json"),
                json,
                encoding);
        }

        private static Nvs01KingdomView CurrentView(KingdomSceneController controller)
        {
            return Field<Nvs01KingdomView>(controller, "_nvs01View");
        }

        private static string QuestText(KingdomSceneController controller)
        {
            return Field<Text>(controller, "_questText").text;
        }

        private static string MessageText(KingdomSceneController controller)
        {
            return Field<Text>(controller, "_messageText").text;
        }

        private static IReadOnlyList<Button> ActionButtons(KingdomSceneController controller)
        {
            return Field<List<Button>>(controller, "_nvs01ActionButtons")
                .Where(button => button != null && button.gameObject.activeSelf)
                .ToArray();
        }

        private static void AssertButtonLabels(
            KingdomSceneController controller,
            params string[] expected)
        {
            CollectionAssert.AreEqual(
                expected,
                ActionButtons(controller).Select(ButtonLabel).ToArray());
        }

        private static void Click(KingdomSceneController controller, string label)
        {
            Button button = ActionButtons(controller).Single(
                candidate => string.Equals(ButtonLabel(candidate), label, StringComparison.Ordinal));
            button.onClick.Invoke();
        }

        private static string ButtonLabel(Button button)
        {
            return button.GetComponentInChildren<Text>().text;
        }

        private static Nvs01VerifiedCatalog VerifiedCatalog()
        {
            string path = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                Nvs01CatalogContract.StreamingAssetsRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            Nvs01CatalogValidationResult validation =
                Nvs01CatalogValidator.ValidateCanonicalArtifact(File.ReadAllBytes(path));
            Assert.True(
                validation.IsAccepted,
                string.Join(
                    Environment.NewLine,
                    validation.Diagnostics.Select(item => item.Code + " " + item.Path)));
            return validation.VerifiedCatalog;
        }

        private static string Localize(string key)
        {
            Assert.True(VerifiedCatalog().Catalog.TryGetLocalization(key, out string value), key);
            return value;
        }

        private static T Field<T>(object target, string name)
        {
            return (T)target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            return target.GetType()
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, args);
        }

        private static IDictionary ServicesDictionary()
        {
            return (IDictionary)typeof(ServiceLocator)
                .GetField("Services", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);
        }

        private sealed class CommittedRealmService : IRealmService
        {
            internal CommittedRealmService(RealmId realmId)
            {
                CurrentRealmId = realmId;
                Identity = new RealmIdentitySnapshot(
                    RealmIdentityStatus.CommittedValid,
                    realmId,
                    RealmCatalogRuntime.SupportedVersion,
                    "AL-REALM-COMMITTED-VALID");
            }

            public RealmId CurrentRealmId { get; }
            public RealmDefinition CurrentRealm => null;
            public RealmIdentitySnapshot Identity { get; }

            public RealmSelectionResult TrySelectRealm(RealmSelectionRequest request)
            {
                return default;
            }

            public void SelectRealm(RealmId id)
            {
            }
        }
    }
}
