using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AL.ChampionMode;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Catalogs.WorldAtlas;
using AL.Data.Runtime;
using AL.UI.Kingdom;
using AL.UI.QuestHud;
using AL.UI.SharedMenu;
using AL.UI.WorldMap;
using AL.World;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.QuestHud
{
    public sealed class MainQuestAutoAssistContractTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            RemoveSaveService();
            ProofOfWorthDirector.ResetForTests();
            QuestHudAutoQuest.ResetForTests();
            MainQuestMapSession.ResetForTests();
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            _root = new GameObject("MainQuestAutoAssistContractTests.Root");
        }

        [TearDown]
        public void TearDown()
        {
            RemoveSaveService();
            QuestHudAutoQuest.ResetForTests();
            ProofOfWorthDirector.ResetForTests();
            MainQuestMapSession.ResetForTests();
            FirstSessionChampionStart.ResetToFirstSessionLanding();

            foreach (QuestHudOverlay overlay in Object.FindObjectsOfType<QuestHudOverlay>())
            {
                Object.DestroyImmediate(overlay.gameObject);
            }

            foreach (ProofOfWorthDirector director in Object.FindObjectsOfType<ProofOfWorthDirector>())
            {
                Object.DestroyImmediate(director.gameObject);
            }

            GameObject markerRoot = GameObject.Find(ProofOfWorthDirector.MarkerRootName);
            if (markerRoot != null)
            {
                Object.DestroyImmediate(markerRoot);
            }

            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void AutoQuestOnCompletesNextThreeDMainQuestStepWithoutMutatingSideQuest()
        {
            var unrelatedQuest = new QuestState
            {
                QuestId = "SIDE_SENTINEL",
                CurrentValue = 0,
                IsCompleted = false,
                IsClaimed = false
            };
            var save = new SaveGameData
            {
                Quests = new List<QuestState> { unrelatedQuest }
            };
            ServiceLocator.Register<ISaveGameService>(new TestSaveGameService(save));
            QuestHudAutoQuest.SetEnabled(true);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();

            director.EnsureReady(null, _root.transform, RealmId.Stonehold);

            Assert.That(director.State.QuestId, Is.EqualTo(ProofOfWorthIds.OmenQuestId));
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenArena));
            Assert.That(director.State.OmenAccepted, Is.True);

            GameObject markerRoot = GameObject.Find(ProofOfWorthDirector.MarkerRootName);
            Assert.That(markerRoot, Is.Not.Null);
            Assert.That(markerRoot.transform.childCount, Is.EqualTo(1));
            _root.transform.position = markerRoot.transform.GetChild(0).position;
            InvokeUpdate(director);

            Assert.That(director.State.QuestId, Is.EqualTo(ProofOfWorthIds.MainQuestId));
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.C1MeetGuide));
            Assert.That(save.Quests, Has.Count.EqualTo(1));
            Assert.That(
                ServiceLocator.Get<ISaveGameService>().CurrentSave.Quests[0],
                Is.SameAs(unrelatedQuest));
            Assert.That(unrelatedQuest.CurrentValue, Is.Zero);
            Assert.That(unrelatedQuest.IsCompleted, Is.False);
            Assert.That(unrelatedQuest.IsClaimed, Is.False);
        }

        [Test]
        public void AutoQuestOffNeverAcceptsTheOfferedMainQuest()
        {
            QuestHudAutoQuest.SetEnabled(false);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();

            director.EnsureReady(null, _root.transform, RealmId.Crownlands);
            InvokeUpdate(director);

            Assert.That(director.State.QuestId, Is.EqualTo(ProofOfWorthIds.OmenQuestId));
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenOffered));
            Assert.That(director.State.IsOmenOffered, Is.True);
            Assert.That(director.State.OmenAccepted, Is.False);
        }

        [Test]
        public void AutoQuestCannotCompleteAnArrivalWithoutAPlayer()
        {
            QuestHudAutoQuest.SetEnabled(true);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();

            director.EnsureReady(null, null, RealmId.Stonehold);
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenArena));

            InvokeUpdate(director);

            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenArena));
        }

        [Test]
        public void AutoQuestCannotCompleteAnArrivalWithoutItsMarker()
        {
            QuestHudAutoQuest.SetEnabled(true);
            ProofOfWorthDirector director = _root.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, _root.transform, RealmId.Eldergrove);
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenArena));
            Object.DestroyImmediate(GameObject.Find(ProofOfWorthDirector.MarkerRootName));

            InvokeUpdate(director);

            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenArena));
        }

        [Test]
        public void WarzoneGatePromptCannotBeAutoAcceptedOrCompleted()
        {
            QuestHudAutoQuest.SetEnabled(true);
            int primaryInvocations = 0;
            QuestHudOverlay hud = QuestHudOverlay.Mount(_root.transform);

            hud.Bind(
                QuestHudPlanner.WarzoneGate(autoQuestOn: true),
                () => primaryInvocations++);
            hud.ConsiderAutoQuest();

            Assert.That(hud.Model.IsWarzoneGate, Is.True);
            Assert.That(hud.Model.Action, Is.EqualTo(QuestHudAction.None));
            Assert.That(hud.Model.CanAutoFire, Is.False);
            Assert.That(QuestHudAutoQuest.ShouldFire(hud.Model), Is.False);
            Assert.That(primaryInvocations, Is.Zero);
        }

        [Test]
        public void TeachingChainIsLockedNarrativeBeforeLordshipAndAvailableAfter()
        {
            KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
            var save = new SaveGameData
            {
                SelectedRealm = RealmId.Eldergrove,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "ranger",
                    IdentityConfirmed = true
                }
            };

            SharedMenuModuleState locked = KingdomManagementUnlock.EvaluateKingdomManagement(save);
            KingdomTeachingState unavailable = KingdomTeachingQuestline.Evaluate(save, catalog);
            Assert.That(locked.Availability, Is.EqualTo(SharedMenuAvailability.LockedNarrative));
            Assert.That(unavailable.IsAvailable, Is.False);
            Assert.That(unavailable.CurrentStep, Is.Null);

            Assert.That(
                ProofOfWorthLordship.TryWriteMark(
                    save,
                    ProofOfWorthLordship.ResolveMarkId(save.SelectedRealm)),
                Is.True);
            SharedMenuModuleState available = KingdomManagementUnlock.EvaluateKingdomManagement(save);
            KingdomTeachingState teaching = KingdomTeachingQuestline.Evaluate(save, catalog);

            Assert.That(available.Availability, Is.EqualTo(SharedMenuAvailability.Available));
            Assert.That(teaching.IsAvailable, Is.True);
            Assert.That(teaching.IsComplete, Is.False);
            Assert.That(teaching.CurrentStep, Is.SameAs(catalog.Steps[0]));
        }

        [Test]
        public void MapAndMinimapEnumerableMarkersContainNoOuterRealmIds()
        {
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            MainQuestMapMarkerCatalog markerCatalog = MainQuestMapMarkerCatalog.LoadCanonical();
            var realms = new[]
            {
                RealmId.Stonehold,
                RealmId.Eldergrove,
                RealmId.Crownlands,
                RealmId.Umbral
            };
            Assert.That(markerCatalog.ObjectiveIds.Count, Is.EqualTo(7));

            foreach (RealmId realm in realms)
            {
                KingdomWorldMapQueryResult kingdom = KingdomWorldMapQuery.Enumerate(snapshot, realm);
                Assert.That(kingdom.RegionIds.Count, Is.GreaterThan(0), realm.ToString());
                Assert.That(kingdom.MarkerIds.Count, Is.GreaterThan(0), realm.ToString());
                Assert.That(KingdomWorldMapQuery.ContainsOuterRealmId(kingdom.RegionIds), Is.False, realm.ToString());
                Assert.That(KingdomWorldMapQuery.ContainsOuterRealmId(kingdom.MarkerIds), Is.False, realm.ToString());

                foreach (string objectiveId in markerCatalog.ObjectiveIds)
                {
                    IReadOnlyList<MainQuestMapMarker> current =
                        MainQuestMapMarkerResolver.ResolveCurrent(
                            snapshot,
                            markerCatalog,
                            objectiveId,
                            realm,
                            "Continue the current main quest.");
                    Assert.That(current.Count, Is.EqualTo(1), objectiveId + " / " + realm);
                    Assert.That(current[0].IsInnerRealm, Is.True, current[0].MarkerId);
                    Assert.That(KingdomWorldMapQuery.IsForbiddenId(current[0].MarkerId), Is.False);
                    Assert.That(KingdomWorldMapQuery.IsForbiddenId(current[0].ZoneId), Is.False);
                }
            }

            Assert.That(
                MainQuestMapMarkerResolver.ResolveCurrent(
                    snapshot,
                    markerCatalog,
                    "OBJ_ENTER_WARZONE",
                    RealmId.Stonehold,
                    "Do not enter automatically."),
                Is.Empty);
        }

        private static void InvokeUpdate(ProofOfWorthDirector director)
        {
            MethodInfo update = typeof(ProofOfWorthDirector).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            update.Invoke(director, null);
        }

        private static void RemoveSaveService()
        {
            FieldInfo servicesField = typeof(ServiceLocator).GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(servicesField, Is.Not.Null);
            var services = (IDictionary)servicesField.GetValue(null);
            services.Remove(typeof(ISaveGameService));
        }

        private sealed class TestSaveGameService : ISaveGameService
        {
            internal TestSaveGameService(SaveGameData save)
            {
                CurrentSave = save;
            }

            public SaveGameData CurrentSave { get; private set; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.None;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus => SaveOperationStatus.None;
            public string LastSaveMessage => string.Empty;

            public void Save()
            {
            }

            public void Load()
            {
            }

            public bool HasSave()
            {
                return CurrentSave != null;
            }

            public void CreateNewSave(RealmId realmId)
            {
                CurrentSave = new SaveGameData { SelectedRealm = realmId };
            }

            public void DeleteSave()
            {
                CurrentSave = null;
            }
        }
    }
}
