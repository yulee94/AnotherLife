using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.UI;
using AL.UI.Kingdom;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AL.Tests.EditMode
{
    public sealed class ProfileMutationPresentationPolicyTests
    {
        private const string ProfileId =
            "alp_11111111111111111111111111111111";
        private const string AuthorityEpoch =
            "11111111111111112222222222222222";
        private const string GenerationFingerprint =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            ServicesDictionary().Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ServicesDictionary().Clear();
            DestroyImmediateIfPresent(_host);
            _host = null;
            DestroyImmediateIfPresent(GameObject.Find("KingdomCanvas"));
        }

        [TestCase(ProfileWriteAuthorityStatus.Unavailable, "PROFILE AUTHORITY UNAVAILABLE")]
        [TestCase(ProfileWriteAuthorityStatus.MissingProfile, "PROFILE MISSING")]
        [TestCase(ProfileWriteAuthorityStatus.MigrationRequired, "PROFILE MIGRATION REQUIRED")]
        [TestCase(ProfileWriteAuthorityStatus.ForwardSchemaReadOnly, "NEWER PROFILE VERSION")]
        [TestCase(ProfileWriteAuthorityStatus.DegradedReadOnly, "PROFILE DATA DEGRADED")]
        [TestCase(ProfileWriteAuthorityStatus.RecoveryRequired, "PROFILE RECOVERY REQUIRED")]
        [TestCase(ProfileWriteAuthorityStatus.CommitUncertain, "SAVE COMMIT UNRESOLVED")]
        [TestCase(ProfileWriteAuthorityStatus.Deleted, "PROFILE DELETED")]
        [TestCase(ProfileWriteAuthorityStatus.Writable, "PROFILE WRITES NOT ACTIVATED")]
        public void EveryAuthorityStatusFailsClosedWithoutProductionActivation(
            ProfileWriteAuthorityStatus status,
            string expectedReason)
        {
            var provider = new CountingAuthorityProvider(
                CreateAuthority(status));

            ProfileMutationPresentationState presentation =
                ProfileMutationPresentationPolicy.Capture(
                    provider,
                    productionWriteActivationEnabled: false);

            Assert.False(presentation.OrdinaryMutationCommandsEnabled);
            Assert.True(presentation.IsReadOnly);
            Assert.AreEqual(expectedReason, presentation.ReasonText);
            Assert.AreEqual(
                "COMMAND DECK READ-ONLY — " + expectedReason,
                presentation.DisplayText,
                "Kingdom copy must remain byte-stable while the reason becomes context-neutral.");
            Assert.AreEqual(1, provider.ReadCount);
        }

        [Test]
        public void CompleteWritableAuthorityAndActivationAreBothRequired()
        {
            var writable = new CountingAuthorityProvider(
                CreateAuthority(ProfileWriteAuthorityStatus.Writable));
            var migration = new CountingAuthorityProvider(
                CreateAuthority(ProfileWriteAuthorityStatus.MigrationRequired));

            ProfileMutationPresentationState active =
                ProfileMutationPresentationPolicy.Capture(
                    writable,
                    productionWriteActivationEnabled: true);
            ProfileMutationPresentationState nonWritable =
                ProfileMutationPresentationPolicy.Capture(
                    migration,
                    productionWriteActivationEnabled: true);

            Assert.True(active.OrdinaryMutationCommandsEnabled);
            Assert.False(active.IsReadOnly);
            Assert.AreEqual(
                "COMMAND DECK WRITABLE — PROFILE AUTHORITY VERIFIED",
                active.DisplayText);
            Assert.AreEqual("PROFILE AUTHORITY VERIFIED", active.ReasonText);
            Assert.False(nonWritable.OrdinaryMutationCommandsEnabled);
            Assert.AreEqual(
                "COMMAND DECK READ-ONLY — PROFILE MIGRATION REQUIRED",
                nonWritable.DisplayText);
            Assert.AreEqual(
                "PROFILE MIGRATION REQUIRED",
                nonWritable.ReasonText);
        }

        [Test]
        public void MissingThrowingAndNullProvidersReadAtMostOnceAndFailClosed()
        {
            ProfileMutationPresentationState missing =
                ProfileMutationPresentationPolicy.Capture(
                    null,
                    productionWriteActivationEnabled: true);
            var throwing = new ThrowingAuthorityProvider();
            ProfileMutationPresentationState failed =
                ProfileMutationPresentationPolicy.Capture(
                    throwing,
                    productionWriteActivationEnabled: true);
            var nullProvider = new CountingAuthorityProvider(null);
            ProfileMutationPresentationState nullSnapshot =
                ProfileMutationPresentationPolicy.Capture(
                    nullProvider,
                    productionWriteActivationEnabled: true);

            Assert.False(missing.OrdinaryMutationCommandsEnabled);
            Assert.False(failed.OrdinaryMutationCommandsEnabled);
            Assert.False(nullSnapshot.OrdinaryMutationCommandsEnabled);
            Assert.AreEqual(1, throwing.ReadCount);
            Assert.AreEqual(1, nullProvider.ReadCount);
        }

        [Test]
        public void KingdomPresentationCapturesOnceKeepsTownHallAndDisablesOtherOrders()
        {
            var save = new AuthoritySaveService(
                CreateAuthority(ProfileWriteAuthorityStatus.MigrationRequired));
            var realm = new FakeRealmService();
            var buildings = new FakeBuildingService();
            ServiceLocator.Register<ISaveGameService>(save);
            ServiceLocator.Register<IRealmService>(realm);
            ServiceLocator.Register<IBuildingService>(buildings);

            _host = new GameObject("ProfileMutationPresentationControllerTest");
            var controller = _host.AddComponent<KingdomSceneController>();
            SetField(controller, "_profileReady", true);
            Invoke(controller, "BuildRuntimeUi");

            GameObject canvas = GameObject.Find("KingdomCanvas");
            Assert.NotNull(canvas);

            string[] ordinaryBuildingCommands =
            {
                KingdomCommandPolicy.FarmUpgrade,
                KingdomCommandPolicy.LumberMillUpgrade,
                KingdomCommandPolicy.QuarryUpgrade,
                KingdomCommandPolicy.GoldMineUpgrade,
                KingdomCommandPolicy.BarracksUpgrade
            };

            foreach (string commandId in ordinaryBuildingCommands)
            {
                Button button = FindButton(canvas, commandId);
                Assert.False(button.interactable, commandId);
                Text[] buttonTexts = button.GetComponentsInChildren<Text>(true);
                Text status = buttonTexts.Single(
                    text => text.name == "UnavailableStatusText");
                Assert.AreEqual("LOCKED", status.text, commandId);

                Text commandLabel = buttonTexts.Single(
                    text => text.name != "UnavailableStatusText");
                Color mutedLabelColor = commandLabel.color;
                var feedback =
                    button.GetComponent<KingdomCommandButtonFeedback>();
                Assert.NotNull(feedback, commandId);
                var pointer = new PointerEventData(null);
                feedback.OnPointerEnter(pointer);
                feedback.OnPointerDown(pointer);
                feedback.OnPointerUp(pointer);
                Invoke(feedback, "Update");

                Assert.False((bool)GetField(feedback, "_hovered"), commandId);
                Assert.False((bool)GetField(feedback, "_pressed"), commandId);
                Assert.AreEqual(0f, (float)GetField(feedback, "_impactAmount"), commandId);
                Assert.AreEqual(Vector3.one, button.transform.localScale, commandId);
                AssertColor(mutedLabelColor, commandLabel.color, commandId);

                button.onClick.Invoke();
            }

            Button townHall = FindButton(canvas, KingdomCommandPolicy.TownHallUpgrade);
            Assert.True(townHall.interactable, KingdomCommandPolicy.TownHallUpgrade);
            townHall.onClick.Invoke();

            Text authorityText = canvas.GetComponentsInChildren<Text>(true)
                .Single(text => text.name == "CommandDeckAuthorityStatus");
            Assert.That(authorityText.text, Does.Contain("COMMAND DECK READ-ONLY"));
            Assert.That(authorityText.text, Does.Contain("PROFILE MIGRATION REQUIRED"));

            Button boardView = FindButton(canvas, "Board View");
            Assert.True(boardView.interactable);

            int nvsCallbacks = 0;
            Invoke(
                controller,
                "CreateNvs01ActionButton",
                "Continue",
                new UnityAction(() => nvsCallbacks++));
            var nvsButtons = (IReadOnlyList<Button>)GetField(
                controller,
                "_nvs01ActionButtons");
            Assert.AreEqual(1, nvsButtons.Count);
            Assert.True(nvsButtons[0].interactable);
            nvsButtons[0].onClick.Invoke();

            SetField(controller, "_profileReady", false);
            Invoke(controller, "Update");
            Invoke(controller, "Update");

            Assert.AreEqual(1, save.AuthorityReadCount,
                "Authority must be captured once while presentation is constructed.");
            Assert.AreEqual(0, buildings.StartConstructionCount,
                "Read-only building commands must not reach gameplay callbacks.");
            Assert.AreEqual(0, save.SaveCount,
                "Read-only command selection must not persist.");
            Assert.AreEqual(1, nvsCallbacks,
                "Typed NVS actions remain independently available.");
        }

        private static ProfileWriteAuthoritySnapshot CreateAuthority(
            ProfileWriteAuthorityStatus status)
        {
            switch (status)
            {
                case ProfileWriteAuthorityStatus.Writable:
                    return ProfileWriteAuthoritySnapshotFactory.Writable(
                        ProfileId,
                        AuthorityEpoch,
                        GenerationFingerprint,
                        ProfileAuthoritySourceGeneration.Primary,
                        Array.Empty<string>());
                case ProfileWriteAuthorityStatus.MigrationRequired:
                    return ProfileWriteAuthoritySnapshotFactory.MigrationRequired(
                        ProfileAuthoritySourceGeneration.Primary,
                        new[] { "AL-TEST-MIGRATION" });
                case ProfileWriteAuthorityStatus.ForwardSchemaReadOnly:
                    return NonWritable(status, 3, 1, true);
                case ProfileWriteAuthorityStatus.DegradedReadOnly:
                    return NonWritable(status, 1, 1, true);
                case ProfileWriteAuthorityStatus.RecoveryRequired:
                case ProfileWriteAuthorityStatus.CommitUncertain:
                case ProfileWriteAuthorityStatus.MissingProfile:
                case ProfileWriteAuthorityStatus.Deleted:
                    return NonWritable(status, 0, 0, false);
                case ProfileWriteAuthorityStatus.Unavailable:
                default:
                    return ProfileWriteAuthoritySnapshotFactory.Unavailable(
                        "AL-TEST-AUTHORITY-UNAVAILABLE");
            }
        }

        private static ProfileWriteAuthoritySnapshot NonWritable(
            ProfileWriteAuthorityStatus status,
            int saveSchemaVersion,
            int initializationVersion,
            bool hasSource)
        {
            return ProfileWriteAuthoritySnapshotFactory.NonWritable(
                status,
                saveSchemaVersion,
                initializationVersion,
                hasSource,
                hasSource
                    ? ProfileAuthoritySourceGeneration.Primary
                    : ProfileAuthoritySourceGeneration.None,
                new[] { "AL-TEST-" + status.ToString().ToUpperInvariant() });
        }

        private static Button FindButton(GameObject root, string name)
        {
            Button[] matches = root.GetComponentsInChildren<Button>(true)
                .Where(button => string.Equals(
                    button.name,
                    name,
                    StringComparison.Ordinal))
                .ToArray();
            Assert.AreEqual(1, matches.Length, name);
            return matches[0];
        }

        private static void AssertColor(
            Color expected,
            Color actual,
            string message)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f), message);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f), message);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f), message);
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f), message);
        }

        private static object Invoke(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, methodName);
            return method.Invoke(instance, arguments);
        }

        private static object GetField(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            return field.GetValue(instance);
        }

        private static void SetField(
            object instance,
            string fieldName,
            object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            field.SetValue(instance, value);
        }

        private static IDictionary ServicesDictionary() =>
            (IDictionary)typeof(ServiceLocator)
                .GetField(
                    "Services",
                    BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);

        private static void DestroyImmediateIfPresent(UnityEngine.Object value)
        {
            if (value != null)
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }

        private sealed class CountingAuthorityProvider :
            IProfileWriteAuthorityProvider
        {
            private readonly ProfileWriteAuthoritySnapshot _snapshot;

            public CountingAuthorityProvider(
                ProfileWriteAuthoritySnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public int ReadCount { get; private set; }

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority()
            {
                ReadCount++;
                return _snapshot;
            }
        }

        private sealed class ThrowingAuthorityProvider :
            IProfileWriteAuthorityProvider
        {
            public int ReadCount { get; private set; }

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority()
            {
                ReadCount++;
                throw new InvalidOperationException("injected authority fault");
            }
        }

        private sealed class AuthoritySaveService :
            ISaveGameService,
            IProfileWriteAuthorityProvider
        {
            private readonly ProfileWriteAuthoritySnapshot _authority;

            public AuthoritySaveService(
                ProfileWriteAuthoritySnapshot authority)
            {
                _authority = authority;
                CurrentSave = new SaveGameData
                {
                    SaveFormatId = SaveGameData.CurrentSaveFormatId,
                    SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion,
                    ProfileInitializationVersion =
                        SaveGameData.CurrentProfileInitializationVersion,
                    SelectedRealm = RealmId.Stonehold
                };
            }

            public int AuthorityReadCount { get; private set; }
            public int SaveCount { get; private set; }
            public int LoadCount { get; private set; }
            public int DeleteCount { get; private set; }
            public SaveGameData CurrentSave { get; private set; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus => SaveOperationStatus.None;
            public string LastSaveMessage => string.Empty;

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority()
            {
                AuthorityReadCount++;
                return _authority;
            }

            public void Save() => SaveCount++;
            public void Load() => LoadCount++;
            public bool HasSave() => CurrentSave != null;

            public void CreateNewSave(RealmId realmId)
            {
                CurrentSave = new SaveGameData { SelectedRealm = realmId };
            }

            public void DeleteSave()
            {
                DeleteCount++;
                CurrentSave = null;
            }
        }

        private sealed class FakeRealmService : IRealmService
        {
            public RealmId CurrentRealmId => RealmId.Stonehold;
            public RealmDefinition CurrentRealm => null;
            public RealmIdentitySnapshot Identity => new RealmIdentitySnapshot(
                RealmIdentityStatus.CommittedValid,
                RealmId.Stonehold,
                "test",
                "AL-TEST-REALM");

            public RealmSelectionResult TrySelectRealm(
                RealmSelectionRequest request) =>
                new RealmSelectionResult(
                    RealmSelectionStatus.AlreadyCommittedSameRealm,
                    request.RequestedRealmId,
                    RealmId.Stonehold,
                    false,
                    false,
                    "AL-TEST-REALM");

            public void SelectRealm(RealmId id)
            {
            }
        }

        private sealed class FakeBuildingService : IBuildingService
        {
            public int StartConstructionCount { get; private set; }

            public BuildingState GetBuildingState(string buildingId) => null;
            public IEnumerable<BuildingState> GetAllBuildingStates() =>
                Array.Empty<BuildingState>();
            public BuildingConstructionQuote GetConstructionQuote(
                string buildingId) => null;

            public BuildingConstructionResult TryStartConstruction(
                string buildingId,
                long requestedAtTimestamp)
            {
                StartConstructionCount++;
                return new BuildingConstructionResult(
                    BuildingConstructionStatus.RejectedNoCurrentSave,
                    null,
                    false,
                    false,
                    "AL-TEST-BUILDING");
            }

            public BuildingConstructionResult TryCompleteConstruction(
                string buildingId,
                long observedAtTimestamp) => null;

            public BuildingConstructionReconcileResult
                ReconcileCompletedConstructions(long observedAtTimestamp) =>
                new BuildingConstructionReconcileResult(
                    BuildingConstructionStatus.Available,
                    Array.Empty<string>(),
                    false,
                    "AL-TEST-BUILDING");

            public void StartUpgrade(string buildingId)
            {
            }

            public void CompleteUpgrade(string buildingId)
            {
            }
        }
    }
}
