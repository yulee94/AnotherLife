using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using AL.Core.SaveAuthority;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public class QuestSaveCompatibilityTests
    {
        [Test]
        public void EmptyQuestCollectionIsNotSeededByConstructionOrQueries()
        {
            object save = CreateSaveData();
            IList quests = CreateQuestList();
            SetField(save, "Quests", quests);
            MainFixture fixture = CreateMainFixture(save);

            object[] first = Enumerate(Invoke(fixture.Service, "GetActiveQuests"));
            object[] second = Enumerate(Invoke(fixture.Service, "GetActiveQuests"));

            Assert.AreSame(quests, GetField(save, "Quests"));
            Assert.AreEqual(0, quests.Count);
            Assert.AreEqual(0, first.Length);
            Assert.AreEqual(0, second.Length);
            Assert.AreEqual(0, fixture.Save.State.SaveCount);
            Assert.AreEqual(0, fixture.Resource.State.AddCalls);
            Assert.AreEqual(0, fixture.Credit.State.AddCalls);
        }

        [Test]
        public void NullQuestCollectionQueriesAndRejectedCommandsDoNotAssignOrSave()
        {
            object save = CreateSaveData();
            SetField(save, "Quests", null);
            MainFixture fixture = CreateMainFixture(save);
            object sideService = CreateSideQuestService(fixture.Save.Proxy, fixture.Resource.Proxy);

            Assert.AreEqual(0, Enumerate(Invoke(fixture.Service, "GetActiveQuests")).Length);
            Assert.AreEqual(0, Enumerate(Invoke(sideService, "GetActiveSideQuests")).Length);
            Invoke(fixture.Service, "UpdateProgress", EnumValue("AL.Core.QuestType", "BuildBuilding"), 1);
            Invoke(fixture.Service, "ClaimReward", "Q_UNKNOWN");
            Invoke(fixture.Service, "ClaimReward", " ");
            Invoke(sideService, "AcceptQuest", "SQ_UNKNOWN");

            Assert.IsNull(GetField(save, "Quests"));
            Assert.AreEqual(0, fixture.Save.State.SaveCount);
            Assert.AreEqual(0, fixture.Resource.State.AddCalls);
            Assert.AreEqual(0, fixture.Credit.State.AddCalls);
        }

        [Test]
        public void QueriesFilterWithoutRewritingRawQuestCollection()
        {
            object blank = CreateQuestState("   ", 5, true, true);
            object unknown = CreateQuestState("Q_FUTURE", 7, true, true);
            object duplicateFirst = CreateQuestState("Q1", 0, false, false);
            object duplicateSecond = CreateQuestState("Q1", 1, true, false);
            object supported = CreateQuestState("Q2", 0, false, false);
            object claimedNotCompleted = CreateQuestState("Q3", 0, false, true);
            object negative = CreateQuestState("Q4", -1, false, false);
            object overTarget = CreateQuestState("Q5", 2, true, false);
            IList quests = CreateQuestList(
                null,
                blank,
                unknown,
                duplicateFirst,
                duplicateSecond,
                supported,
                claimedNotCompleted,
                negative,
                overTarget);
            object save = CreateSaveData(quests);
            MainFixture fixture = CreateMainFixture(save);
            object sideService = CreateSideQuestService(fixture.Save.Proxy, fixture.Resource.Proxy);
            QuestCollectionSnapshot before = CaptureQuestCollection(save);

            object[] firstMain = Enumerate(Invoke(fixture.Service, "GetActiveQuests"));
            object[] secondMain = Enumerate(Invoke(fixture.Service, "GetActiveQuests"));
            object[] firstSide = Enumerate(Invoke(sideService, "GetActiveSideQuests"));
            object[] secondSide = Enumerate(Invoke(sideService, "GetActiveSideQuests"));

            CollectionAssert.AreEqual(new[] { supported }, firstMain);
            CollectionAssert.AreEqual(new[] { supported }, secondMain);
            Assert.AreEqual(0, firstSide.Length);
            Assert.AreEqual(0, secondSide.Length);
            AssertQuestCollectionUnchanged(before, save);
            Assert.AreEqual(0, fixture.Save.State.SaveCount);
        }

        [Test]
        public void ActiveQueryMembershipIsCapturedAtCallTime()
        {
            object q1 = CreateQuestState("Q1", 0, false, false);
            IList quests = CreateQuestList(q1);
            object save = CreateSaveData(quests);
            MainFixture fixture = CreateMainFixture(save);

            object activeSnapshot = Invoke(fixture.Service, "GetActiveQuests");
            SetField(q1, "IsClaimed", true);
            quests.Add(CreateQuestState("Q2", 0, false, false));

            object[] captured = Enumerate(activeSnapshot);
            Assert.AreEqual(1, captured.Length);
            Assert.AreSame(q1, captured[0]);
            Assert.AreEqual(0, fixture.Save.State.SaveCount);
        }

        [Test]
        public void MalformedQueryDiagnosticsUseStableCodesAndReportOnce()
        {
            object save = CreateSaveData(CreateQuestList(
                null,
                CreateQuestState(" ", 0, false, false),
                CreateQuestState("Q1", 0, false, false),
                CreateQuestState("Q1", 1, true, false),
                CreateQuestState("Q_FUTURE", 7, true, false),
                CreateQuestState("Q2", 0, false, true)));
            MainFixture fixture = CreateMainFixture(save);
            QuestCollectionSnapshot before = CaptureQuestCollection(save);

            LogAssert.Expect(LogType.Warning, new Regex(@"\[AL-QST-NULL-STATE\]"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[AL-QST-INVALID-ID\]"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[AL-QST-DUPLICATE-ID\]"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[AL-QST-UNKNOWN-ID\]"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[AL-QST-CONTRADICTORY-STATE\]"));

            Assert.AreEqual(0, Enumerate(Invoke(fixture.Service, "GetActiveQuests")).Length);
            Assert.AreEqual(0, Enumerate(Invoke(fixture.Service, "GetActiveQuests")).Length);
            LogAssert.NoUnexpectedReceived();

            AssertQuestCollectionUnchanged(before, save);
            Assert.AreEqual(0, fixture.Save.State.SaveCount);
        }

        [Test]
        public void HiddenTriggerIsNotificationOnlyAndUnsafeRowsRemainUntouched()
        {
            object claimed = CreateQuestState("Q_HIDDEN_CLAIMED", 1, true, true);
            object duplicateFirst = CreateQuestState("Q_HIDDEN_DUP", 0, false, false);
            object duplicateSecond = CreateQuestState("Q_HIDDEN_DUP", 1, true, false);
            object contradictory = CreateQuestState("Q_HIDDEN_BAD", 0, true, false);
            object save = CreateSaveData(CreateQuestList(claimed, duplicateFirst, duplicateSecond, contradictory));
            MainFixture fixture = CreateMainFixture(save);
            QuestCollectionSnapshot before = CaptureQuestCollection(save);
            var definitions = new List<object>();
            object trigger = EnumValue("AL.Core.TriggerCondition", "Event");

            try
            {
                definitions.Add(InjectHiddenQuestDefinition(fixture.Service, "Q_HIDDEN_ABSENT", trigger));
                definitions.Add(InjectHiddenQuestDefinition(fixture.Service, "Q_HIDDEN_CLAIMED", trigger));
                definitions.Add(InjectHiddenQuestDefinition(fixture.Service, "Q_HIDDEN_DUP", trigger));
                definitions.Add(InjectHiddenQuestDefinition(fixture.Service, "Q_HIDDEN_BAD", trigger));

                LogAssert.Expect(LogType.Warning, new Regex(@"\[AL-QST-DUPLICATE-ID\]"));
                LogAssert.Expect(LogType.Warning, new Regex(@"\[AL-QST-CONTRADICTORY-STATE\]"));
                LogAssert.Expect(LogType.Log, new Regex("Hidden Quest Revealed"));
                Invoke(fixture.Service, "TriggerHiddenQuest", "hidden-event", trigger);
                LogAssert.NoUnexpectedReceived();

                AssertQuestCollectionUnchanged(before, save);
                Assert.AreEqual(0, fixture.Save.State.SaveCount);
                Assert.AreEqual(4, ((IList)GetField(save, "Quests")).Count,
                    "Notification-only reveal must not create or seed a state.");
            }
            finally
            {
                foreach (object definition in definitions)
                {
                    DestroyDefinition(definition);
                }
            }
        }

        [Test]
        public void DuplicateKnownRowsDisableTheEntireGroupWithoutEffects()
        {
            object first = CreateQuestState("Q1", 0, false, false);
            object second = CreateQuestState("Q1", 1, true, false);
            object save = CreateSaveData(CreateQuestList(first, second));
            MainFixture fixture = CreateMainFixture(save);
            QuestCollectionSnapshot before = CaptureQuestCollection(save);
            int updated = 0;
            int completed = 0;
            AddQuestEventHandler(fixture.Service, "OnQuestUpdated", _ => updated++);
            AddQuestEventHandler(fixture.Service, "OnQuestCompleted", _ => completed++);
            var story = new RecordingStoryService();

            using (RegisterStoryService(story))
            {
                Assert.AreEqual(0, Enumerate(Invoke(fixture.Service, "GetActiveQuests")).Length);
                Invoke(fixture.Service, "UpdateProgress", EnumValue("AL.Core.QuestType", "BuildBuilding"), 1);
                Invoke(fixture.Service, "ClaimReward", "Q1");
            }

            AssertQuestCollectionUnchanged(before, save);
            AssertZeroEffects(fixture, story, updated, completed);
        }

        [TestCase(-1, false, false, TestName = "ContradictoryState_NegativeProgress")]
        [TestCase(0, false, true, TestName = "ContradictoryState_ClaimedBeforeCompletion")]
        [TestCase(2, false, false, TestName = "ContradictoryState_OverTarget")]
        [TestCase(0, true, false, TestName = "ContradictoryState_CompletedBelowTarget")]
        [TestCase(1, false, false, TestName = "ContradictoryState_IncompleteAtTarget")]
        [TestCase(2, true, false, TestName = "ContradictoryState_CompletedOverTarget")]
        public void ContradictoryKnownStateRejectsProgressClaimAndAllEffects(
            int currentValue,
            bool isCompleted,
            bool isClaimed)
        {
            object state = CreateQuestState("Q1", currentValue, isCompleted, isClaimed);
            object save = CreateSaveData(CreateQuestList(state));
            MainFixture fixture = CreateMainFixture(save);
            QuestCollectionSnapshot before = CaptureQuestCollection(save);
            int updated = 0;
            int completed = 0;
            AddQuestEventHandler(fixture.Service, "OnQuestUpdated", _ => updated++);
            AddQuestEventHandler(fixture.Service, "OnQuestCompleted", _ => completed++);
            var story = new RecordingStoryService();

            using (RegisterStoryService(story))
            {
                Invoke(fixture.Service, "UpdateProgress", EnumValue("AL.Core.QuestType", "BuildBuilding"), 1);
                Invoke(fixture.Service, "ClaimReward", "Q1");
            }

            AssertQuestCollectionUnchanged(before, save);
            AssertZeroEffects(fixture, story, updated, completed);
        }

        [Test]
        public void BlankUnknownAndNonPositiveMutationsHaveZeroEffects()
        {
            object unknown = CreateQuestState("Q_FUTURE", 1, true, false);
            object save = CreateSaveData(CreateQuestList(null, CreateQuestState(" ", 0, false, false), unknown));
            MainFixture fixture = CreateMainFixture(save);
            QuestCollectionSnapshot before = CaptureQuestCollection(save);
            int updated = 0;
            int completed = 0;
            AddQuestEventHandler(fixture.Service, "OnQuestUpdated", _ => updated++);
            AddQuestEventHandler(fixture.Service, "OnQuestCompleted", _ => completed++);
            var story = new RecordingStoryService();

            using (RegisterStoryService(story))
            {
                Invoke(fixture.Service, "UpdateProgress", EnumValue("AL.Core.QuestType", "BuildBuilding"), 0);
                Invoke(fixture.Service, "UpdateProgress", EnumValue("AL.Core.QuestType", "BuildBuilding"), -1);
                Invoke(fixture.Service, "UpdateProgress", EnumValue("AL.Core.QuestType", "Side"), 1);
                Invoke(fixture.Service, "ClaimReward", new object[] { null });
                Invoke(fixture.Service, "ClaimReward", " ");
                Invoke(fixture.Service, "ClaimReward", "Q_FUTURE");
            }

            AssertQuestCollectionUnchanged(before, save);
            AssertZeroEffects(fixture, story, updated, completed);
        }

        [Test]
        public void ClaimedUnknownStateCannotRewardWhenDefinitionReturns()
        {
            object future = CreateQuestState("Q_FUTURE", 3, true, true);
            object save = CreateSaveData(CreateQuestList(future));
            MainFixture fixture = CreateMainFixture(save);
            QuestCollectionSnapshot before = CaptureQuestCollection(save);
            object definition = null;

            try
            {
                Assert.AreEqual(0, Enumerate(Invoke(fixture.Service, "GetActiveQuests")).Length);
                definition = InjectQuestDefinition(
                    fixture.Service,
                    "Q_FUTURE",
                    3,
                    EnumValue("AL.Core.QuestType", "BuildBuilding"));

                Assert.AreEqual(0, Enumerate(Invoke(fixture.Service, "GetActiveQuests")).Length);
                Invoke(fixture.Service, "ClaimReward", "Q_FUTURE");
                Invoke(fixture.Service, "UpdateProgress", EnumValue("AL.Core.QuestType", "BuildBuilding"), 1);

                AssertQuestCollectionUnchanged(before, save);
                Assert.True((bool)GetField(future, "IsClaimed"));
                Assert.AreEqual(0, fixture.Save.State.SaveCount);
                Assert.AreEqual(0, fixture.Resource.State.AddCalls);
                Assert.AreEqual(0, fixture.Credit.State.AddCalls);
            }
            finally
            {
                DestroyDefinition(definition);
            }
        }

        [Test]
        public void SideQuestPrefixIsNotAuthorityAndInjectedDefinitionAcceptsExactlyOnce()
        {
            object unknown = CreateQuestState("SQ_UNKNOWN", 4, false, false);
            IList quests = CreateQuestList(unknown);
            object save = CreateSaveData(quests);
            MainFixture main = CreateMainFixture(save);
            object sideService = CreateSideQuestService(main.Save.Proxy, main.Resource.Proxy);
            QuestCollectionSnapshot before = CaptureQuestCollection(save);

            Invoke(sideService, "AcceptQuest", "SQ_NEW");
            Invoke(sideService, "AcceptQuest", "SQ_UNKNOWN");
            Assert.AreEqual(0, Enumerate(Invoke(sideService, "GetActiveSideQuests")).Length);
            AssertQuestCollectionUnchanged(before, save);
            Assert.AreEqual(0, main.Save.State.SaveCount);

            object definition = null;
            try
            {
                definition = InjectSideQuestDefinition(
                    sideService,
                    "SQ_KNOWN",
                    2,
                    EnumValue("AL.Core.QuestType", "Side"));
                Invoke(sideService, "AcceptQuest", "sq_known");
                Invoke(sideService, "AcceptQuest", "SQ_KNOWN");
                Invoke(sideService, "AcceptQuest", "SQ_KNOWN");

                Assert.AreSame(quests, GetField(save, "Quests"));
                Assert.AreEqual(2, quests.Count);
                Assert.AreSame(unknown, quests[0]);
                object known = quests[1];
                Assert.AreEqual("SQ_KNOWN", GetField(known, "QuestId"));
                CollectionAssert.AreEqual(new[] { known }, Enumerate(Invoke(sideService, "GetActiveSideQuests")));
                Assert.AreEqual(1, main.Save.State.SaveCount);
            }
            finally
            {
                DestroyDefinition(definition);
            }
        }

        [Test]
        public void DuplicateAndContradictorySideQuestRowsRejectQueryAndAcceptanceWithoutSaving()
        {
            object duplicateFirst = CreateQuestState("SQ_DUP", 0, false, false);
            object duplicateSecond = CreateQuestState("SQ_DUP", 1, true, false);
            object contradictory = CreateQuestState("SQ_BAD", 1, false, false);
            object save = CreateSaveData(CreateQuestList(duplicateFirst, duplicateSecond, contradictory));
            MainFixture main = CreateMainFixture(save);
            object sideService = CreateSideQuestService(main.Save.Proxy, main.Resource.Proxy);
            QuestCollectionSnapshot before = CaptureQuestCollection(save);
            var definitions = new List<object>();

            try
            {
                definitions.Add(InjectSideQuestDefinition(
                    sideService,
                    "SQ_DUP",
                    1,
                    EnumValue("AL.Core.QuestType", "Side")));
                definitions.Add(InjectSideQuestDefinition(
                    sideService,
                    "SQ_BAD",
                    1,
                    EnumValue("AL.Core.QuestType", "Side")));

                Assert.AreEqual(0, Enumerate(Invoke(sideService, "GetActiveSideQuests")).Length);
                Invoke(sideService, "AcceptQuest", "SQ_DUP");
                Invoke(sideService, "AcceptQuest", "SQ_BAD");

                AssertQuestCollectionUnchanged(before, save);
                Assert.AreEqual(0, main.Save.State.SaveCount);
                Assert.AreEqual(0, main.Resource.State.AddCalls);
            }
            finally
            {
                foreach (object definition in definitions)
                {
                    DestroyDefinition(definition);
                }
            }
        }

        [Test]
        public void ExplicitKnownQuestProgressesAndClaimsExactlyOnceWithoutSeeding()
        {
            object q1 = CreateQuestState("Q1", 0, false, false);
            IList quests = CreateQuestList(q1);
            object save = CreateSaveData(quests);
            MainFixture fixture = CreateMainFixture(save);
            int updated = 0;
            int completed = 0;
            AddQuestEventHandler(fixture.Service, "OnQuestUpdated", _ => updated++);
            AddQuestEventHandler(fixture.Service, "OnQuestCompleted", _ => completed++);
            var story = new RecordingStoryService();

            using (RegisterStoryService(story))
            {
                Invoke(fixture.Service, "UpdateProgress", EnumValue("AL.Core.QuestType", "BuildBuilding"), int.MaxValue);
                Invoke(fixture.Service, "UpdateProgress", EnumValue("AL.Core.QuestType", "BuildBuilding"), 1);
                Invoke(fixture.Service, "ClaimReward", "Q1");
                Invoke(fixture.Service, "ClaimReward", "Q1");
            }

            Assert.AreSame(quests, GetField(save, "Quests"));
            Assert.AreEqual(1, quests.Count, "Quest definitions must not seed Q2-Q5 into the save.");
            Assert.AreSame(q1, quests[0]);
            Assert.AreEqual(1, GetField(q1, "CurrentValue"));
            Assert.True((bool)GetField(q1, "IsCompleted"));
            Assert.True((bool)GetField(q1, "IsClaimed"));
            Assert.AreEqual(1, updated);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(1, story.AdvanceCalls);
            Assert.AreEqual(1, fixture.Resource.State.AddCalls);
            Assert.AreEqual(1000L, fixture.Resource.State.LastAmount);
            Assert.AreEqual("Gold", fixture.Resource.State.LastType.ToString());
            Assert.AreEqual(1, fixture.Credit.State.AddCalls);
            Assert.AreEqual(0, fixture.Credit.State.LastAmount);
            Assert.AreEqual(2, fixture.Save.State.SaveCount);
        }

        [Test]
        public void NullTopLevelQuestListIsNotNormalizedByContainedManualSave()
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateActualSaveService(root);
                CreateNewSave(saveService);
                object save = GetProperty(saveService, "CurrentSave");
                SetField(save, "Quests", null);
                byte[] primaryBefore = File.ReadAllBytes(
                    Path.Combine(root, "save.json"));

                Invoke(saveService, "Save");
                Assert.IsNull(GetField(GetProperty(saveService, "CurrentSave"), "Quests"));
                Assert.That(
                    (string)GetProperty(saveService, "LastSaveMessage"),
                    Does.StartWith("AL-SAVE-MANUAL-WRITE-CONTAINED:"));
                CollectionAssert.AreEqual(
                    primaryBefore,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));

                object reloaded = CreateActualSaveService(root);
                Invoke(reloaded, "Load");
                IList reloadedQuests = (IList)GetField(GetProperty(reloaded, "CurrentSave"), "Quests");
                Assert.NotNull(reloadedQuests);
                Assert.AreEqual(0, reloadedQuests.Count);

                ResourceFixture resource = CreateResourceProxy();
                CreditFixture credit = CreateCreditProxy();
                object questService = CreateQuestService(reloaded, resource.Proxy, credit.Proxy);
                Assert.AreEqual(0, Enumerate(Invoke(questService, "GetActiveQuests")).Length);
                Assert.AreEqual(0, reloadedQuests.Count);
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Test]
        public void MixedMalformedUnknownDuplicateAndKnownRowsStayReadOnlyAndKeepPositions()
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateActualSaveService(root);
                CreateNewSave(saveService);
                object save = GetProperty(saveService, "CurrentSave");
                IList quests = CreateQuestList(
                    null,
                    CreateQuestState(" ", 4, false, false),
                    CreateQuestState("Q_FUTURE", 7, true, true),
                    CreateQuestState("Q1", 0, false, false),
                    CreateQuestState("Q1", 1, true, false),
                    CreateQuestState("Q2", 0, false, false));
                SetField(save, "Quests", quests);

                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                byte[] primaryBefore = File.ReadAllBytes(primaryPath);
                byte[] backupBefore = File.ReadAllBytes(backupPath);
                Invoke(saveService, "Save");
                Assert.AreEqual(
                    "SaveFailedPreviousPreserved",
                    GetProperty(saveService, "LastSaveStatus").ToString());
                CollectionAssert.AreEqual(primaryBefore, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(backupPath));
                Assert.That(
                    (string)GetProperty(saveService, "LastSaveMessage"),
                    Does.StartWith("AL-SAVE-MANUAL-WRITE-CONTAINED:"));
                Assert.AreSame(quests, GetField(save, "Quests"));

                string diagnosticJson = JsonUtility.ToJson(save, true);
                File.WriteAllText(primaryPath, diagnosticJson);
                File.WriteAllText(backupPath, diagnosticJson);
                byte[] diagnosticPrimary = File.ReadAllBytes(primaryPath);
                byte[] diagnosticBackup = File.ReadAllBytes(backupPath);

                object reloaded = CreateActualSaveService(root);
                Invoke(reloaded, "Load");
                Assert.AreEqual(
                    "RecoveryRequired",
                    GetProperty(reloaded, "LastLoadStatus").ToString());
                Assert.Null(GetProperty(reloaded, "CurrentSave"));
                object reloadedSave = GetProperty(reloaded, "ReadOnlyCandidateSnapshot");
                IList reloadedQuests = AssertPersistedMixedRows(reloadedSave);
                Assert.AreEqual(6, reloadedQuests.Count);
                CollectionAssert.AreEqual(diagnosticPrimary, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(diagnosticBackup, File.ReadAllBytes(backupPath));
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        private static IList AssertPersistedMixedRows(object save)
        {
            IList rows = (IList)GetField(save, "Quests");
            Assert.NotNull(rows);
            Assert.AreEqual(6, rows.Count, "The serializer may canonicalize a null inline row, but it must not remove its position.");
            if (rows[0] != null)
            {
                Assert.True(string.IsNullOrEmpty((string)GetField(rows[0], "QuestId")));
                Assert.AreEqual(0, GetField(rows[0], "CurrentValue"));
                Assert.False((bool)GetField(rows[0], "IsCompleted"));
                Assert.False((bool)GetField(rows[0], "IsClaimed"));
            }

            AssertQuestValues(rows[1], " ", 4, false, false);
            AssertQuestValues(rows[2], "Q_FUTURE", 7, true, true);
            AssertQuestValues(rows[3], "Q1", 0, false, false);
            AssertQuestValues(rows[4], "Q1", 1, true, false);
            AssertQuestValues(rows[5], "Q2", 0, false, false);
            return rows;
        }

        private static void AssertQuestValues(
            object state,
            string questId,
            int currentValue,
            bool isCompleted,
            bool isClaimed)
        {
            Assert.NotNull(state);
            Assert.AreEqual(questId, GetField(state, "QuestId"));
            Assert.AreEqual(currentValue, GetField(state, "CurrentValue"));
            Assert.AreEqual(isCompleted, GetField(state, "IsCompleted"));
            Assert.AreEqual(isClaimed, GetField(state, "IsClaimed"));
        }

        private static void AssertZeroEffects(
            MainFixture fixture,
            RecordingStoryService story,
            int updated,
            int completed)
        {
            Assert.AreEqual(0, fixture.Save.State.SaveCount);
            Assert.AreEqual(0, fixture.Resource.State.AddCalls);
            Assert.AreEqual(0, fixture.Credit.State.AddCalls);
            Assert.AreEqual(0, story.AdvanceCalls);
            Assert.AreEqual(0, updated);
            Assert.AreEqual(0, completed);
        }

        private static MainFixture CreateMainFixture(object save)
        {
            SaveFixture saveFixture = CreateSaveFixture(save);
            ResourceFixture resource = CreateResourceProxy();
            CreditFixture credit = CreateCreditProxy();
            object service = CreateQuestService(saveFixture.Proxy, resource.Proxy, credit.Proxy);
            return new MainFixture(service, saveFixture, resource, credit);
        }

        private static object CreateQuestService(object saveService, object resourceService, object creditService)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalQuestService");
            Type gateType = GetRuntimeType(
                "AL.Services.Local.EconomyWriteAuthorityGate");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    GetRuntimeType("AL.Core.Interfaces.ISaveGameService"),
                    GetRuntimeType("AL.Core.Interfaces.IResourceService"),
                    GetRuntimeType("AL.Core.Interfaces.IWarzoneCreditService"),
                    gateType
                },
                null);
            Assert.NotNull(constructor);
            return constructor.Invoke(
                new[]
                {
                    saveService,
                    resourceService,
                    creditService,
                    CreateWritableGate(saveService)
                });
        }

        private static object CreateWritableGate(object saveService)
        {
            Type gateType = GetRuntimeType(
                "AL.Services.Local.EconomyWriteAuthorityGate");
            ConstructorInfo constructor = gateType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    GetRuntimeType("AL.Core.Interfaces.ISaveGameService"),
                    typeof(IProfileWriteAuthorityProvider)
                },
                null);
            Assert.NotNull(constructor);
            return constructor.Invoke(
                new object[]
                {
                    saveService,
                    new WritableAuthorityProvider()
                });
        }

        private static object CreateSideQuestService(object saveService, object resourceService)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.SideQuestService");
            ConstructorInfo constructor = serviceType.GetConstructor(new[]
            {
                GetRuntimeType("AL.Core.Interfaces.ISaveGameService"),
                GetRuntimeType("AL.Core.Interfaces.IResourceService")
            });
            Assert.NotNull(constructor);
            return constructor.Invoke(new[] { saveService, resourceService });
        }

        private sealed class WritableAuthorityProvider :
            IProfileWriteAuthorityProvider
        {
            private static readonly ProfileWriteAuthoritySnapshot Snapshot =
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    "alp_0123456789abcdef0123456789abcdef",
                    "0123456789abcdef0000000000000001",
                    new string(
                        'a',
                        SaveAuthorityTechnicalLimits.Sha256Characters),
                    ProfileAuthoritySourceGeneration.Primary,
                    Array.Empty<string>());

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority() =>
                Snapshot;
        }

        private static object InjectQuestDefinition(object service, string id, int targetValue, object questType)
        {
            object definition = ScriptableObject.CreateInstance(GetRuntimeType("AL.Data.Definitions.Narrative.QuestDefinition"));
            SetField(definition, "Id", id);
            SetField(definition, "TargetValue", targetValue);
            SetField(definition, "Type", questType);
            ((IDictionary)GetField(service, "_definitions"))[id] = definition;
            return definition;
        }

        private static object InjectHiddenQuestDefinition(object service, string id, object trigger)
        {
            object definition = InjectQuestDefinition(
                service,
                id,
                1,
                EnumValue("AL.Core.QuestType", "Side"));
            SetField(definition, "IsHidden", true);
            SetField(definition, "RequiredItemId", "hidden-event");
            SetField(definition, "Trigger", trigger);
            SetField(definition, "Title", id);
            return definition;
        }

        private static object InjectSideQuestDefinition(object service, string id, int targetValue, object questType)
        {
            object definition = ScriptableObject.CreateInstance(GetRuntimeType("AL.Data.Definitions.Narrative.SideQuestDefinition"));
            SetField(definition, "Id", id);
            SetField(definition, "TargetValue", targetValue);
            SetField(definition, "Type", questType);
            ((IDictionary)GetField(service, "_definitions"))[id] = definition;
            return definition;
        }

        private static void DestroyDefinition(object definition)
        {
            if (definition is UnityEngine.Object unityObject)
            {
                UnityEngine.Object.DestroyImmediate(unityObject);
            }
        }

        private static IDisposable RegisterStoryService(RecordingStoryService state)
        {
            Type storyType = GetRuntimeType("AL.Core.Interfaces.IStoryService");
            object proxy = CreateDispatchProxy(storyType, typeof(RecordingStoryServiceProxy));
            ((RecordingStoryServiceProxy)proxy).State = state;

            Type locatorType = GetRuntimeType("AL.Core.ServiceLocator");
            FieldInfo servicesField = locatorType.GetField("Services", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(servicesField);
            IDictionary services = (IDictionary)servicesField.GetValue(null);
            bool hadPrevious = services.Contains(storyType);
            object previous = hadPrevious ? services[storyType] : null;
            services[storyType] = proxy;

            return new ActionScope(() =>
            {
                if (hadPrevious)
                {
                    services[storyType] = previous;
                }
                else
                {
                    services.Remove(storyType);
                }
            });
        }

        private static void AddQuestEventHandler(object service, string eventName, Action<object> callback)
        {
            EventInfo eventInfo = service.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(eventInfo);
            Type questStateType = GetRuntimeType("AL.Core.Interfaces.QuestState");
            Type delegateType = typeof(Action<>).MakeGenericType(questStateType);
            ParameterExpression state = Expression.Parameter(questStateType, "state");
            InvocationExpression invoke = Expression.Invoke(
                Expression.Constant(callback),
                Expression.Convert(state, typeof(object)));
            Delegate handler = Expression.Lambda(delegateType, invoke, state).Compile();
            eventInfo.AddEventHandler(service, handler);
        }

        private static SaveFixture CreateSaveFixture(object currentSave)
        {
            Type interfaceType = GetRuntimeType("AL.Core.Interfaces.ISaveGameService");
            object proxy = CreateDispatchProxy(interfaceType, typeof(RecordingSaveServiceProxy));
            var state = new RecordingSaveService { CurrentSave = currentSave };
            ((RecordingSaveServiceProxy)proxy).State = state;
            return new SaveFixture(proxy, state);
        }

        private static ResourceFixture CreateResourceProxy()
        {
            Type interfaceType = GetRuntimeType("AL.Core.Interfaces.IResourceService");
            object proxy = CreateDispatchProxy(interfaceType, typeof(RecordingResourceServiceProxy));
            var state = new RecordingResourceService();
            ((RecordingResourceServiceProxy)proxy).State = state;
            return new ResourceFixture(proxy, state);
        }

        private static CreditFixture CreateCreditProxy()
        {
            Type interfaceType = GetRuntimeType("AL.Core.Interfaces.IWarzoneCreditService");
            object proxy = CreateDispatchProxy(interfaceType, typeof(RecordingCreditServiceProxy));
            var state = new RecordingCreditService();
            ((RecordingCreditServiceProxy)proxy).State = state;
            return new CreditFixture(proxy, state);
        }

        private static object CreateDispatchProxy(Type interfaceType, Type proxyType)
        {
            MethodInfo create = typeof(DispatchProxy).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "Create" && method.GetGenericArguments().Length == 2);
            return create.MakeGenericMethod(interfaceType, proxyType).Invoke(null, null);
        }

        private static object CreateSaveData(IList quests = null)
        {
            object save = Activator.CreateInstance(GetRuntimeType("AL.Data.Runtime.SaveGameData"));
            if (quests != null)
            {
                SetField(save, "Quests", quests);
            }

            return save;
        }

        private static IList CreateQuestList(params object[] states)
        {
            IList list = (IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(GetRuntimeType("AL.Core.Interfaces.QuestState")));
            foreach (object state in states ?? Array.Empty<object>())
            {
                list.Add(state);
            }

            return list;
        }

        private static object CreateQuestState(string questId, int currentValue, bool isCompleted, bool isClaimed)
        {
            object state = Activator.CreateInstance(GetRuntimeType("AL.Core.Interfaces.QuestState"));
            SetField(state, "QuestId", questId);
            SetField(state, "CurrentValue", currentValue);
            SetField(state, "IsCompleted", isCompleted);
            SetField(state, "IsClaimed", isClaimed);
            return state;
        }

        private static QuestCollectionSnapshot CaptureQuestCollection(object save)
        {
            IList quests = (IList)GetField(save, "Quests");
            var rows = new QuestRow[quests.Count];
            for (int index = 0; index < quests.Count; index++)
            {
                object state = quests[index];
                rows[index] = state == null
                    ? new QuestRow(null, null, 0, false, false)
                    : new QuestRow(
                        state,
                        (string)GetField(state, "QuestId"),
                        (int)GetField(state, "CurrentValue"),
                        (bool)GetField(state, "IsCompleted"),
                        (bool)GetField(state, "IsClaimed"));
            }

            return new QuestCollectionSnapshot(quests, rows);
        }

        private static void AssertQuestCollectionUnchanged(QuestCollectionSnapshot expected, object save)
        {
            IList actual = (IList)GetField(save, "Quests");
            Assert.AreSame(expected.List, actual);
            Assert.AreEqual(expected.Rows.Length, actual.Count);
            for (int index = 0; index < expected.Rows.Length; index++)
            {
                QuestRow row = expected.Rows[index];
                Assert.AreSame(row.Reference, actual[index], $"Quest row reference drift at index {index}.");
                if (row.Reference == null)
                {
                    continue;
                }

                Assert.AreEqual(row.QuestId, GetField(actual[index], "QuestId"), $"Quest id drift at index {index}.");
                Assert.AreEqual(row.CurrentValue, GetField(actual[index], "CurrentValue"), $"Progress drift at index {index}.");
                Assert.AreEqual(row.IsCompleted, GetField(actual[index], "IsCompleted"), $"Completion drift at index {index}.");
                Assert.AreEqual(row.IsClaimed, GetField(actual[index], "IsClaimed"), $"Claim drift at index {index}.");
            }
        }

        private static object CreateActualSaveService(string root)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(constructor, "Expected the testable persistence-path constructor.");
            return constructor.Invoke(new object[] { root });
        }

        private static void CreateNewSave(object saveService)
        {
            Invoke(saveService, "CreateNewSave", EnumValue("AL.Core.RealmId", "None"));
        }

        private static string CreateTempRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-QuestTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteTempRoot(string root)
        {
            string fullRoot = Path.GetFullPath(root);
            string expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "AnotherLife-QuestTests"));
            Assert.True(fullRoot.StartsWith(expectedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            if (Directory.Exists(fullRoot))
            {
                Directory.Delete(fullRoot, true);
            }
        }

        private static object[] Enumerate(object result)
        {
            return ((IEnumerable)result).Cast<object>().ToArray();
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
            Assert.NotNull(method, $"Expected method {methodName}.");
            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static object EnumValue(string enumTypeName, string value)
        {
            return Enum.Parse(GetRuntimeType(enumTypeName), value);
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(type, $"Expected loaded runtime type {typeName}.");
            return type;
        }

        private static object GetProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, $"Expected property {name}.");
            return property.GetValue(target);
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Expected field {name}.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Expected field {name}.");
            field.SetValue(target, value);
        }

        private static object DefaultReturn(Type type)
        {
            return type == typeof(void)
                ? null
                : type.IsValueType
                    ? Activator.CreateInstance(type)
                    : null;
        }

        private sealed class MainFixture
        {
            public MainFixture(object service, SaveFixture save, ResourceFixture resource, CreditFixture credit)
            {
                Service = service;
                Save = save;
                Resource = resource;
                Credit = credit;
            }

            public object Service { get; }
            public SaveFixture Save { get; }
            public ResourceFixture Resource { get; }
            public CreditFixture Credit { get; }
        }

        private sealed class SaveFixture
        {
            public SaveFixture(object proxy, RecordingSaveService state)
            {
                Proxy = proxy;
                State = state;
            }

            public object Proxy { get; }
            public RecordingSaveService State { get; }
        }

        private sealed class ResourceFixture
        {
            public ResourceFixture(object proxy, RecordingResourceService state)
            {
                Proxy = proxy;
                State = state;
            }

            public object Proxy { get; }
            public RecordingResourceService State { get; }
        }

        private sealed class CreditFixture
        {
            public CreditFixture(object proxy, RecordingCreditService state)
            {
                Proxy = proxy;
                State = state;
            }

            public object Proxy { get; }
            public RecordingCreditService State { get; }
        }

        private sealed class QuestCollectionSnapshot
        {
            public QuestCollectionSnapshot(IList list, QuestRow[] rows)
            {
                List = list;
                Rows = rows;
            }

            public IList List { get; }
            public QuestRow[] Rows { get; }
        }

        private sealed class QuestRow
        {
            public QuestRow(object reference, string questId, int currentValue, bool isCompleted, bool isClaimed)
            {
                Reference = reference;
                QuestId = questId;
                CurrentValue = currentValue;
                IsCompleted = isCompleted;
                IsClaimed = isClaimed;
            }

            public object Reference { get; }
            public string QuestId { get; }
            public int CurrentValue { get; }
            public bool IsCompleted { get; }
            public bool IsClaimed { get; }
        }

        private sealed class ActionScope : IDisposable
        {
            private Action _dispose;

            public ActionScope(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                Action dispose = _dispose;
                _dispose = null;
                dispose?.Invoke();
            }
        }

        public class RecordingSaveServiceProxy : DispatchProxy
        {
            public RecordingSaveService State { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args) =>
                State.Invoke(targetMethod, args);
        }

        public sealed class RecordingSaveService
        {
            public object CurrentSave;
            public int SaveCount;

            public object Invoke(MethodInfo method, object[] args)
            {
                switch (method.Name)
                {
                    case "get_CurrentSave":
                        return CurrentSave;
                    case "Save":
                        SaveCount++;
                        return null;
                    case "HasSave":
                        return CurrentSave != null;
                    case "get_LastLoadMessage":
                    case "get_LastSaveMessage":
                        return string.Empty;
                    default:
                        return DefaultReturn(method.ReturnType);
                }
            }
        }

        public class RecordingResourceServiceProxy : DispatchProxy
        {
            public RecordingResourceService State { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args) =>
                State.Invoke(targetMethod, args);
        }

        public sealed class RecordingResourceService
        {
            public int AddCalls;
            public object LastType;
            public long LastAmount;

            public object Invoke(MethodInfo method, object[] args)
            {
                if (method.Name == "AddResource")
                {
                    AddCalls++;
                    LastType = args[0];
                    LastAmount = Convert.ToInt64(args[1]);
                    return null;
                }

                if (method.Name == "GetResourceCount")
                {
                    return 0L;
                }

                if (method.Name == "HasEnough")
                {
                    return true;
                }

                if (method.Name == "ConsumeResource")
                {
                    return false;
                }

                return DefaultReturn(method.ReturnType);
            }
        }

        public class RecordingCreditServiceProxy : DispatchProxy
        {
            public RecordingCreditService State { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args) =>
                State.Invoke(targetMethod, args);
        }

        public sealed class RecordingCreditService
        {
            public int AddCalls;
            public int LastAmount;

            public object Invoke(MethodInfo method, object[] args)
            {
                if (method.Name == "AddCredits")
                {
                    AddCalls++;
                    LastAmount = Convert.ToInt32(args[0]);
                    return null;
                }

                if (method.Name == "GetCredits")
                {
                    return 0;
                }

                if (method.Name == "SpendCredits")
                {
                    return false;
                }

                return DefaultReturn(method.ReturnType);
            }
        }

        public class RecordingStoryServiceProxy : DispatchProxy
        {
            public RecordingStoryService State { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args) =>
                State.Invoke(targetMethod, args);
        }

        public sealed class RecordingStoryService
        {
            public int AdvanceCalls;

            public object Invoke(MethodInfo method, object[] args)
            {
                if (method.Name == "AdvanceStory")
                {
                    AdvanceCalls++;
                    return null;
                }

                if (method.Name == "get_CurrentChapterId")
                {
                    return "C1";
                }

                return DefaultReturn(method.ReturnType);
            }
        }
    }
}
