using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.ChampionMode;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Catalogs.WorldAtlas;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.UI.Kingdom;
using AL.UI.QuestHud;
using AL.UI.SharedMenu;
using AL.World;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.KingdomTeaching
{
    public sealed class KingdomTeachingQuestlineTests
    {
        [TearDown]
        public void TearDown()
        {
            QuestHudAutoQuest.ResetForTests();
            KingdomTeachingInteraction.ResetForTests();
            CrossModeSession.Reset();
            foreach (KingdomTeachingDirector director in
                     UnityEngine.Object.FindObjectsOfType<KingdomTeachingDirector>())
            {
                UnityEngine.Object.DestroyImmediate(director.gameObject);
            }

            foreach (KingdomTeachingReturnDirector director in
                     UnityEngine.Object.FindObjectsOfType<KingdomTeachingReturnDirector>())
            {
                UnityEngine.Object.DestroyImmediate(director.gameObject);
            }

            foreach (QuestHudOverlay overlay in
                     UnityEngine.Object.FindObjectsOfType<QuestHudOverlay>())
            {
                UnityEngine.Object.DestroyImmediate(overlay.gameObject);
            }

            foreach (SharedMenuModeSwitchHost host in
                     UnityEngine.Object.FindObjectsOfType<SharedMenuModeSwitchHost>())
            {
                UnityEngine.Object.DestroyImmediate(host.gameObject);
            }
        }

        [Test]
        public void LordshipGatesTheFirstCatalogTeachingStep()
        {
            KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
            var save = new SaveGameData
            {
                SelectedRealm = RealmId.Stonehold,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "warrior",
                    IdentityConfirmed = true
                }
            };

            KingdomTeachingState locked = KingdomTeachingQuestline.Evaluate(save, catalog);
            Assert.That(locked.IsAvailable, Is.False);
            Assert.That(locked.CurrentStep, Is.Null);

            Assert.That(
                ProofOfWorthLordship.TryWriteMark(
                    save,
                    ProofOfWorthLordship.ResolveMarkId(save.SelectedRealm)),
                Is.True);
            KingdomTeachingState unlocked = KingdomTeachingQuestline.Evaluate(save, catalog);

            Assert.That(unlocked.IsAvailable, Is.True);
            Assert.That(unlocked.IsComplete, Is.False);
            Assert.That(unlocked.CurrentStep, Is.SameAs(catalog.Steps[0]));
        }

        [Test]
        public void CatalogLocksTeachingOrderAndRejectsOuterRealmIds()
        {
            KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
            CollectionAssert.AreEqual(
                new[]
                {
                    "teach_resources_timers",
                    "teach_construct_town_hall",
                    "teach_research_troops",
                    "teach_construction_dock",
                    "teach_inner_map",
                    "teach_return_3d"
                },
                catalog.Steps.Select(step => step.Id).ToArray());

            MethodInfo parse = typeof(KingdomTeachingCatalog).GetMethod(
                "Parse",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(parse, Is.Not.Null);
            string canonical = File.ReadAllText(
                Path.Combine(
                    UnityEngine.Application.dataPath,
                    "AL",
                    "StreamingAssets",
                    "GameData",
                    KingdomTeachingCatalog.FileName));
            string outerRealmDrift = canonical.Replace(
                "Private Kingdom Map",
                "zone_outer_stonehold");

            Assert.That(
                () => InvokeParse(parse, outerRealmDrift),
                Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void PostLordshipSaveAdvancesOnlyInOrderPersistsAndCanReturnToThreeD()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-KingdomTeachingTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                ISaveGameService writer = CreateSaveService(root);
                writer.CreateNewSave(RealmId.Stonehold);
                Assert.That(
                    MvpLoopSaveAuthority.TryCommit(
                        writer,
                        new MvpLoopCommitRequest(
                            Guid.NewGuid().ToString("N"),
                            RealmId.Stonehold,
                            ClassFamily.Warrior,
                            true,
                            ProofOfWorthIds.StoneholdVariantId,
                            string.Empty,
                            0)).Persisted,
                    Is.True);

                KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
                KingdomTeachingCommitResult outOfOrder =
                    KingdomTeachingSaveAuthority.TryAdvance(
                        writer,
                        catalog,
                        catalog.Steps[1].CompletionEvent);
                Assert.That(outOfOrder.Accepted, Is.False);
                Assert.That(
                    KingdomTeachingQuestline.Evaluate(writer.CurrentSave, catalog).ProgressValue,
                    Is.Zero);

                Assert.That(
                    KingdomTeachingSaveAuthority.TryAdvance(
                        writer,
                        catalog,
                        catalog.Steps[0].CompletionEvent).Persisted,
                    Is.True);
                KingdomTeachingCommitResult beforeBuild =
                    KingdomTeachingSaveAuthority.TryAdvance(
                        writer,
                        catalog,
                        catalog.Steps[1].CompletionEvent);
                Assert.That(beforeBuild.Accepted, Is.False);

                KingdomOneBuildResult build = KingdomOneBuildCommand.TryExecute(
                    writer,
                    new LocalGameDataService());
                Assert.That(build.Accepted, Is.True, build.Message);
                Assert.That(
                    KingdomTeachingSaveAuthority.TryAdvance(
                        writer,
                        catalog,
                        catalog.Steps[1].CompletionEvent).Persisted,
                    Is.True);

                for (int index = 2; index < catalog.Steps.Count; index++)
                {
                    KingdomTeachingCommitResult advanced =
                        KingdomTeachingSaveAuthority.TryAdvance(
                            writer,
                            catalog,
                            catalog.Steps[index].CompletionEvent);
                    Assert.That(advanced.Accepted, Is.True, advanced.Message);
                }

                ISaveGameService reader = CreateSaveService(root);
                reader.Load();
                KingdomTeachingState completed =
                    KingdomTeachingQuestline.Evaluate(reader.CurrentSave, catalog);
                Assert.That(completed.IsAvailable, Is.True);
                Assert.That(completed.IsComplete, Is.True);
                Assert.That(completed.ProgressValue, Is.EqualTo(catalog.Steps.Count));

                CrossModeSwitchPlan returnPlan = CrossModeSceneSwitch.Plan(
                    SharedMenuIds.KingdomScene,
                    SharedMenuIds.Adventure3D,
                    reader.CurrentSave,
                    inCombat: false,
                    unsafeContext: false,
                    SharedMenuIds.InputSharedMenu);
                Assert.That(returnPlan.Succeeded, Is.True);
                Assert.That(returnPlan.DestinationScene, Is.EqualTo(SharedMenuIds.AdventureScene));
                Assert.That(returnPlan.DestinationScene.ToLowerInvariant(), Does.Not.Contain("warzone"));
                Assert.That(returnPlan.DestinationScene.ToLowerInvariant(), Does.Not.Contain("outer"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void CompletedTeachingReturnsEveryRealmToTheInnerSideOfItsMainGate()
        {
            KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(snapshot);

            foreach (RealmId realm in new[]
                     {
                         RealmId.Stonehold,
                         RealmId.Eldergrove,
                         RealmId.Crownlands,
                         RealmId.Umbral
                     })
            {
                SaveGameData save = CompletedTeachingSave(realm, catalog);

                Assert.That(
                    KingdomTeachingReturnPlanner.TryPlan(
                        save,
                        catalog,
                        snapshot,
                        out KingdomTeachingReturnPlan plan),
                    Is.True,
                    realm.ToString());
                Assert.That(plan.DestinationScene, Is.EqualTo(SharedMenuIds.AdventureScene));
                Assert.That(plan.ShouldEnterWarzone, Is.False);
                Assert.That(plan.InnerAtlasZoneId, Does.StartWith("zone_inner_"));
                Assert.That(
                    layout.TryGetInner(
                        InnerRealmWorldLayout.RealmCatalogId(realm),
                        out InnerRealmSlotLayout inner),
                    Is.True);
                Assert.That(plan.MainGateId, Is.EqualTo(inner.MainGateId));
                Assert.That(plan.TransitionZoneId, Is.EqualTo(inner.TransitionZoneId));
                Assert.That(inner.InnerSafe.Contains(plan.Position), Is.True);
                Assert.That(
                    Vector3.Dot(
                        plan.Position - inner.GatePosition,
                        inner.InnerSafe.Center - inner.GatePosition),
                    Is.GreaterThan(0f),
                    "The landing must remain on the protected inner side of the gate.");
                Assert.That(
                    (plan.Position - inner.GatePosition).sqrMagnitude,
                    Is.LessThan((inner.CapitalPosition - inner.GatePosition).sqrMagnitude));
            }

            SaveGameData incomplete = CompletedTeachingSave(RealmId.Stonehold, catalog);
            incomplete.Quests[0].CurrentValue--;
            incomplete.Quests[0].IsCompleted = false;
            Assert.That(
                KingdomTeachingReturnPlanner.TryPlan(
                    incomplete,
                    catalog,
                    snapshot,
                    out _),
                Is.False);

            SaveGameData staleIdentity =
                CompletedTeachingSave(RealmId.Stonehold, catalog);
            staleIdentity.ChampionCustomization.IdentityConfirmed = false;
            Assert.That(
                KingdomTeachingReturnPlanner.TryPlan(
                    staleIdentity,
                    catalog,
                    snapshot,
                    out _),
                Is.False,
                "A stale lordship mark cannot bypass the committed identity gate.");

            SaveGameData mismatchedLordship =
                CompletedTeachingSave(RealmId.Stonehold, catalog);
            mismatchedLordship.ChampionCustomization.LastResultId =
                ProofOfWorthIds.EldergroveVariantId;
            Assert.That(
                KingdomTeachingReturnPlanner.TryPlan(
                    mismatchedLordship,
                    catalog,
                    snapshot,
                    out _),
                Is.False,
                "A lordship mark from another realm cannot authorize this realm's return.");
        }

        [Test]
        public void CompletedTeachingReturnPlacesChampionAndBindsTheNonAutoGatePrompt()
        {
            KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            SaveGameData save = CompletedTeachingSave(RealmId.Eldergrove, catalog);
            var host = new GameObject("KingdomTeachingReturnTests.Host");
            var player = new GameObject(FirstSessionChampionStart.PlayerObjectName);
            try
            {
                QuestHudAutoQuest.SetEnabled(true);
                QuestHudOverlay hud = QuestHudOverlay.Mount(host.transform);
                KingdomTeachingReturnDirector director =
                    host.AddComponent<KingdomTeachingReturnDirector>();
                CrossModeSession.ArmTeachingReturn();

                Assert.That(
                    director.EnsureReady(save, hud, catalog, snapshot),
                    Is.True);
                Assert.That(
                    director.IsApplied,
                    Is.True,
                    "EnsureReady applies immediately when the champion already exists.");
                Assert.That(CrossModeSession.HasPendingTeachingReturn, Is.False);
                Assert.That(player.transform.position, Is.EqualTo(director.Plan.Position));
                Assert.That(
                    Vector3.Dot(player.transform.forward, director.Plan.Forward),
                    Is.GreaterThan(0.999f));
                Assert.That(hud.Model.Surface, Is.EqualTo(QuestHudSurface.WarzoneGate));
                Assert.That(hud.Model.Action, Is.EqualTo(QuestHudAction.None));
                Assert.That(hud.Model.CanAutoFire, Is.False);
                Assert.That(director.Plan.ShouldEnterWarzone, Is.False);

                player.transform.position = Vector3.zero;
                Assert.That(director.TryApply(player.transform), Is.False);
                Assert.That(player.transform.position, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CompletedTeachingWithoutPendingReturnCannotRepositionFutureChampionLoads()
        {
            KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            SaveGameData save = CompletedTeachingSave(RealmId.Eldergrove, catalog);
            var host = new GameObject("KingdomTeachingReturnTests.FutureLoad");
            try
            {
                QuestHudOverlay hud = QuestHudOverlay.Mount(host.transform);
                KingdomTeachingReturnDirector director =
                    host.AddComponent<KingdomTeachingReturnDirector>();

                Assert.That(CrossModeSession.HasPendingTeachingReturn, Is.False);
                Assert.That(
                    director.EnsureReady(save, hud, catalog, snapshot),
                    Is.False,
                    "Persisted teaching completion is not itself a one-shot return intent.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PersistedLordshipCannotRestartProofOfWorthAfterKingdomReturn()
        {
            KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
            SaveGameData completed = CompletedTeachingSave(RealmId.Umbral, catalog);
            var fresh = new SaveGameData
            {
                SelectedRealm = RealmId.Umbral,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "ranger",
                    IdentityConfirmed = true
                }
            };

            Assert.That(ProofOfWorthDirector.ShouldAttachForSave(fresh), Is.True);
            Assert.That(ProofOfWorthDirector.ShouldAttachForSave(completed), Is.False);
            completed.ChampionCustomization.IdentityConfirmed = false;
            Assert.That(
                ProofOfWorthDirector.ShouldAttachForSave(completed),
                Is.True,
                "A stale lordship mark without committed identity cannot suppress the quest owner.");
            completed.ChampionCustomization.IdentityConfirmed = true;
            completed.ChampionCustomization.LastResultId =
                ProofOfWorthIds.EldergroveVariantId;
            Assert.That(
                ProofOfWorthDirector.ShouldAttachForSave(completed),
                Is.True,
                "A lordship mark from another realm cannot suppress this realm's quest owner.");

            string questHost = File.ReadAllText(
                Path.Combine(
                    UnityEngine.Application.dataPath,
                    "AL",
                    "Scripts",
                    "UI",
                    "QuestHud",
                    "QuestHudHost.cs"));
            Assert.That(questHost, Does.Contain("KingdomTeachingReturnDirector"));
            Assert.That(questHost, Does.Contain("QuestHudPlanner.WarzoneGate"));

            string proofDirector = File.ReadAllText(
                Path.Combine(
                    UnityEngine.Application.dataPath,
                    "AL",
                    "Scripts",
                    "ChampionMode",
                    "Quests",
                    "ProofOfWorthDirector.cs"));
            Assert.That(proofDirector, Does.Contain("SharedMenuModeSwitchHost.ReadSave()"));
            Assert.That(
                proofDirector,
                Does.Not.Contain("ServiceLocator.TryGet<ISaveGameService>"));

            string modeSwitchHost = File.ReadAllText(
                Path.Combine(
                    UnityEngine.Application.dataPath,
                    "AL",
                    "Scripts",
                    "UI",
                    "SharedMenu",
                    "SharedMenuModeSwitchHost.cs"));
            Assert.That(modeSwitchHost, Does.Contain("CrossModeSession.ArmTeachingReturn()"));
        }

        [Test]
        public void EveryCatalogStepProjectsPlayerFacingHudWithoutInternalIds()
        {
            KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
            for (int index = 0; index < catalog.Steps.Count; index++)
            {
                KingdomTeachingStep step = catalog.Steps[index];
                QuestHudModel model = QuestHudPlanner.FromKingdomTeaching(
                    step,
                    autoQuestOn: false);

                Assert.That(model.Title, Is.EqualTo(step.Title));
                Assert.That(model.WhatToDo, Is.EqualTo(step.WhatToDo));
                Assert.That(model.LocationName, Is.EqualTo(step.Location));
                Assert.That(model.StepId, Is.EqualTo(step.Id));
                Assert.That(model.Surface, Is.EqualTo(QuestHudSurface.Kingdom25D));
                Assert.That(
                    model.Action,
                    Is.EqualTo(index == catalog.Steps.Count - 1
                        ? QuestHudAction.Complete
                        : QuestHudAction.Continue));
                Assert.That(QuestHudPlanner.CopyLooksLikeId(model.Title), Is.False, step.Id);
                Assert.That(QuestHudPlanner.CopyLooksLikeId(model.WhatToDo), Is.False, step.Id);
                Assert.That(QuestHudPlanner.CopyLooksLikeId(model.LocationName), Is.False, step.Id);
                Assert.That(model.Title, Does.Not.Contain(step.Id));
                Assert.That(model.WhatToDo, Does.Not.Contain(step.Id));
                Assert.That(model.LocationName, Does.Not.Contain(step.Id));
            }
        }

        [Test]
        public void ManualTapAndAutoQuestDriveInteractionsWithoutSkippingTheBuild()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-KingdomTeachingDirectorTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                ISaveGameService save = CreateSaveService(root);
                save.CreateNewSave(RealmId.Crownlands);
                Assert.That(
                    MvpLoopSaveAuthority.TryCommit(
                        save,
                        new MvpLoopCommitRequest(
                            Guid.NewGuid().ToString("N"),
                            RealmId.Crownlands,
                            ClassFamily.Mage,
                            true,
                            ProofOfWorthIds.CrownlandsVariantId,
                            string.Empty,
                            0)).Persisted,
                    Is.True);

                KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
                var host = new GameObject("KingdomTeachingDirectorTests.Host");
                QuestHudOverlay hud = QuestHudOverlay.Mount(host.transform);
                KingdomTeachingDirector director =
                    host.AddComponent<KingdomTeachingDirector>();
                string requested = string.Empty;
                KingdomTeachingInteraction.InteractionRequested +=
                    interaction => requested = interaction;

                director.EnsureReady(save, hud, catalog);
                Assert.That(director.State.ProgressValue, Is.Zero);
                Assert.That(director.Hud.Model.StepId, Is.EqualTo(catalog.Steps[0].Id));

                director.Hud.FirePrimary();
                Assert.That(director.State.ProgressValue, Is.EqualTo(1));
                Assert.That(director.Hud.Model.StepId, Is.EqualTo(catalog.Steps[1].Id));

                director.Hud.FirePrimary();
                Assert.That(requested, Is.EqualTo("construct_town_hall"));
                Assert.That(director.State.ProgressValue, Is.EqualTo(1));

                KingdomOneBuildResult build = KingdomOneBuildCommand.TryExecute(
                    save,
                    new LocalGameDataService());
                Assert.That(build.Accepted, Is.True, build.Message);
                KingdomTeachingInteraction.Observe(
                    catalog.Steps[1].Interaction);
                Assert.That(director.State.ProgressValue, Is.EqualTo(2));

                requested = string.Empty;
                QuestHudAutoQuest.SetEnabled(true);
                director.Refresh();
                Assert.That(director.State.ProgressValue, Is.EqualTo(3));
                Assert.That(requested, Is.EqualTo("open_construction_dock"));

                KingdomTeachingInteraction.Observe(
                    catalog.Steps[3].Interaction);
                Assert.That(director.State.ProgressValue, Is.EqualTo(4));
                Assert.That(requested, Is.EqualTo("open_inner_map"));

                KingdomTeachingInteraction.Observe(
                    catalog.Steps[4].Interaction);
                Assert.That(director.State.ProgressValue, Is.EqualTo(5));
                Assert.That(requested, Is.EqualTo("return_shared_menu"));

                KingdomTeachingInteraction.Observe(
                    catalog.Steps[5].Interaction);
                Assert.That(director.State.IsComplete, Is.True);
                Assert.That(director.State.ProgressValue, Is.EqualTo(catalog.Steps.Count));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void RuntimeHostsWireCatalogInteractionsToTheExistingKingdomSurfaces()
        {
            string scripts = Path.Combine(
                UnityEngine.Application.dataPath,
                "AL",
                "Scripts",
                "UI");
            string questHost = File.ReadAllText(
                Path.Combine(scripts, "QuestHud", "QuestHudHost.cs"));
            string kingdomController = File.ReadAllText(
                Path.Combine(scripts, "Kingdom", "KingdomSceneController.cs"));

            Assert.That(questHost, Does.Contain("KingdomTeachingDirector"));
            Assert.That(questHost, Does.Contain("EnsureReady(save, Overlay)"));
            Assert.That(
                kingdomController,
                Does.Contain(
                    "KingdomTeachingInteraction.InteractionRequested += HandleKingdomTeachingInteraction"));
            Assert.That(
                kingdomController,
                Does.Contain("KingdomTeachingInteraction.Observe(interaction)"));
            Assert.That(kingdomController, Does.Contain("ConstructTownHall"));
            Assert.That(kingdomController, Does.Contain("ToggleConstructionDock"));
            Assert.That(kingdomController, Does.Contain("TogglePrivateMap"));
            Assert.That(kingdomController, Does.Contain("OpenSharedMenu"));
        }

        [Test]
        public void PackagedCatalogHasAnExplicitSharedContractSchema()
        {
            string schemaPath = Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "SharedContracts",
                "Schemas",
                "al-kingdom-teaching.schema.json");

            Assert.That(File.Exists(schemaPath), Is.True, schemaPath);
            string schema = File.ReadAllText(schemaPath);
            Assert.That(schema, Does.Contain("\"catalog_id\""));
            Assert.That(schema, Does.Contain("\"quest_id\""));
            Assert.That(schema, Does.Contain("\"entry\""));
            Assert.That(schema, Does.Contain("\"steps\""));
            Assert.That(schema, Does.Contain("\"completion_event\""));
            Assert.That(schema, Does.Contain("\"additionalProperties\": false"));
        }

        [Test]
        public void CanonicalLoaderUsesTheSharedGameDataDirectoryResolver()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    UnityEngine.Application.dataPath,
                    "AL",
                    "Scripts",
                    "UI",
                    "Kingdom",
                    "KingdomTeachingQuestline.cs"));

            Assert.That(
                source,
                Does.Contain("SixFamilyRuntimeCatalog.TryResolveGameDataDirectory"));
        }

        [Test]
        public void TypedRootRejectsRequestThatDoesNotMatchCurrentCatalogStep()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-KingdomTeachingTypedRootTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                ISaveGameService save = CreateSaveService(root);
                save.CreateNewSave(RealmId.Crownlands);
                Assert.That(
                    MvpLoopSaveAuthority.TryCommit(
                        save,
                        new MvpLoopCommitRequest(
                            Guid.NewGuid().ToString("N"),
                            RealmId.Crownlands,
                            ClassFamily.Mage,
                            true,
                            ProofOfWorthIds.CrownlandsVariantId,
                            string.Empty,
                            0)).Persisted,
                    Is.True);
                KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();

                object result = InvokeKingdomTeachingStore(
                    save,
                    new KingdomTeachingCommitRequest(
                        Guid.NewGuid().ToString("N"),
                        RealmId.Crownlands,
                        catalog.QuestId,
                        "teach_tampered_step",
                        catalog.Steps[0].CompletionEvent,
                        0,
                        1,
                        catalog.Steps.Count));

                Assert.That(
                    ReadProperty(result, "Outcome").ToString(),
                    Is.Not.EqualTo("Committed").And.Not.EqualTo("Duplicate"));
                Assert.That(
                    KingdomTeachingQuestline.Evaluate(save.CurrentSave, catalog)
                        .ProgressValue,
                    Is.Zero);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void TypedRootDerivesTownHallPrerequisiteFromCanonicalStep()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-KingdomTeachingBuildGateTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                ISaveGameService save = CreateSaveService(root);
                save.CreateNewSave(RealmId.Crownlands);
                Assert.That(
                    MvpLoopSaveAuthority.TryCommit(
                        save,
                        new MvpLoopCommitRequest(
                            Guid.NewGuid().ToString("N"),
                            RealmId.Crownlands,
                            ClassFamily.Mage,
                            true,
                            ProofOfWorthIds.CrownlandsVariantId,
                            string.Empty,
                            0)).Persisted,
                    Is.True);
                KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
                Assert.That(
                    KingdomTeachingSaveAuthority.TryAdvance(
                        save,
                        catalog,
                        catalog.Steps[0].CompletionEvent).Accepted,
                    Is.True);

                KingdomTeachingStep build = catalog.Steps[1];
                object result = InvokeKingdomTeachingStore(
                    save,
                    new KingdomTeachingCommitRequest(
                        Guid.NewGuid().ToString("N"),
                        RealmId.Crownlands,
                        catalog.QuestId,
                        build.Id,
                        build.CompletionEvent,
                        1,
                        2,
                        catalog.Steps.Count));

                Assert.That(
                    ReadProperty(result, "Outcome").ToString(),
                    Is.Not.EqualTo("Committed").And.Not.EqualTo("Duplicate"));
                Assert.That(
                    KingdomTeachingQuestline.Evaluate(save.CurrentSave, catalog)
                        .ProgressValue,
                    Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [TestCase("open_construction_dock")]
        [TestCase("open_inner_map")]
        public void MissingKingdomSurfaceCannotCompleteInteraction(string interaction)
        {
            var host = new GameObject("KingdomTeachingMissingSurfaceTests.Host");
            try
            {
                KingdomSceneController controller =
                    host.AddComponent<KingdomSceneController>();
                string observed = string.Empty;
                KingdomTeachingInteraction.InteractionObserved +=
                    value => observed = value;
                MethodInfo handle = typeof(KingdomSceneController).GetMethod(
                    "HandleKingdomTeachingInteraction",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(handle, Is.Not.Null);

                handle.Invoke(controller, new object[] { interaction });

                Assert.That(observed, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RequestingSharedMenuDoesNotCompleteReturnBeforeCommit()
        {
            var host = new GameObject("KingdomTeachingSharedMenuTests.Host");
            try
            {
                KingdomSceneController controller =
                    host.AddComponent<KingdomSceneController>();
                string observed = string.Empty;
                KingdomTeachingInteraction.InteractionObserved +=
                    value => observed = value;
                MethodInfo handle = typeof(KingdomSceneController).GetMethod(
                    "HandleKingdomTeachingInteraction",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(handle, Is.Not.Null);

                handle.Invoke(controller, new object[] { "return_shared_menu" });

                Assert.That(observed, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static SaveGameData CompletedTeachingSave(
            RealmId realm,
            KingdomTeachingCatalog catalog)
        {
            var save = new SaveGameData
            {
                SelectedRealm = realm,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "warrior",
                    IdentityConfirmed = true
                },
                Quests = new List<QuestState>
                {
                    new QuestState
                    {
                        QuestId = catalog.QuestId,
                        CurrentValue = catalog.Steps.Count,
                        IsCompleted = true,
                        IsClaimed = false
                    }
                }
            };
            Assert.That(
                ProofOfWorthLordship.TryWriteMark(
                    save,
                    ProofOfWorthLordship.ResolveMarkId(realm)),
                Is.True);
            return save;
        }

        private static ISaveGameService CreateSaveService(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(constructor, Is.Not.Null);
            return (ISaveGameService)constructor.Invoke(new object[] { root });
        }

        private static object InvokeKingdomTeachingStore(
            ISaveGameService save,
            KingdomTeachingCommitRequest request)
        {
            Type storeType = typeof(LocalSaveGameService).Assembly.GetType(
                "AL.Services.Local.ILegacyKingdomTeachingCandidateStore",
                throwOnError: true);
            MethodInfo commit = storeType.GetMethod(
                "TryCommitLegacyKingdomTeaching",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(commit, Is.Not.Null);
            return commit.Invoke(save, new object[] { request });
        }

        private static object ReadProperty(object instance, string propertyName)
        {
            Assert.That(instance, Is.Not.Null);
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return property.GetValue(instance);
        }

        private static KingdomTeachingCatalog InvokeParse(MethodInfo parse, string json)
        {
            try
            {
                return (KingdomTeachingCatalog)parse.Invoke(
                    null,
                    new object[] { json, "test" });
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException;
            }
        }
    }
}
