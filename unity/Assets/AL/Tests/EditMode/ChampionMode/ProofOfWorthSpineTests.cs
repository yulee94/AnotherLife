using AL.ChampionMode;
using AL.ChampionMode.Control;
using AL.ChampionMode.Interaction;
using AL.ChampionMode.Quests;
using AL.ChampionMode.Skills;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Data.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class ProofOfWorthSpineTests
    {
        [SetUp]
        public void SetUp()
        {
            ChampionHudCameraGate.Reset();
            FirstSessionChampionStart.ResetToFirstSessionLanding();
        }

        [TearDown]
        public void TearDown()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            ProofOfWorthDirector[] leftovers = Object.FindObjectsOfType<ProofOfWorthDirector>();
            for (int i = 0; i < leftovers.Length; i++)
            {
                Object.DestroyImmediate(leftovers[i].gameObject);
            }

            ProofOfWorthDirector.ResetForTests();
            ChampionHudCameraGate.Reset();
        }

        [Test]
        public void PinsAuthoredIdsWithoutInventingLore()
        {
            Assert.AreEqual("OMEN_1", ProofOfWorthIds.OmenQuestId);
            Assert.AreEqual("SELECT_VALERIUS", ProofOfWorthIds.OfferAction);
            Assert.IsFalse(ProofOfWorthIds.AutoAccept);
            Assert.AreEqual("MQ_C1_PROOF_OF_WORTH", ProofOfWorthIds.MainQuestId);
            Assert.AreEqual("ch01_proof_of_worth", ProofOfWorthIds.ChapterId);
            Assert.AreEqual("ch01_stonehold", ProofOfWorthIds.StoneholdVariantId);
            Assert.AreEqual("ch01_eldergrove", ProofOfWorthIds.EldergroveVariantId);
            Assert.AreEqual("ch01_crownlands", ProofOfWorthIds.CrownlandsVariantId);
            Assert.AreEqual("ch01_umbral", ProofOfWorthIds.UmbralVariantId);
            Assert.AreEqual("OBJ_C1_MEET_REALM_GUIDE", ProofOfWorthIds.MeetGuideObjectiveId);
            Assert.AreEqual("OBJ_C1_RESTORE_COVENANT", ProofOfWorthIds.RestoreCovenantObjectiveId);
            Assert.AreEqual("OBJ_C1_FACE_GUARDIAN", ProofOfWorthIds.FaceGuardianObjectiveId);
            Assert.AreEqual("OBJ_C1_ACCEPT_MARK", ProofOfWorthIds.AcceptMarkObjectiveId);
            Assert.AreEqual("HOOK_REALM_GUARDIAN_TRIAL", ProofOfWorthIds.GuardianTrialHook);
            Assert.AreEqual("NPC_VALERIUS", ProofOfWorthIds.SpeakerId);
        }

        [Test]
        public void OfferStartsUnacceptedAndDeclineKeepsOffer()
        {
            ProofOfWorthState offered = ProofOfWorthPlanner.CreateOffered(RealmId.Stonehold);
            Assert.IsTrue(offered.IsOmenOffered);
            Assert.IsFalse(offered.OmenAccepted);
            Assert.IsFalse(offered.AutoAccept);
            Assert.IsFalse(offered.LordshipGranted);
            Assert.AreEqual(ProofOfWorthIds.OmenTalkObjectiveId, offered.ObjectiveId);

            ProofOfWorthTransition decline = ProofOfWorthPlanner.Apply(
                offered,
                ProofOfWorthCommand.DeclineOffer);
            Assert.AreEqual(ProofOfWorthStatus.DuplicateIgnored, decline.Status);
            Assert.IsTrue(decline.State.IsOmenOffered);
            Assert.IsFalse(ProofOfWorthLordship.IsGranted(string.Empty));
        }

        [Test]
        public void OutOfOrderC1AndGuardianAreRejected()
        {
            ProofOfWorthState offered = ProofOfWorthPlanner.CreateOffered(RealmId.Crownlands);
            Assert.AreEqual(
                ProofOfWorthStatus.Rejected,
                ProofOfWorthPlanner.Apply(offered, ProofOfWorthCommand.MeetRealmGuide).Status);
            Assert.AreEqual(
                ProofOfWorthStatus.Rejected,
                ProofOfWorthPlanner.Apply(offered, ProofOfWorthCommand.GuardianDefeated).Status);
            Assert.AreEqual(
                ProofOfWorthStatus.Rejected,
                ProofOfWorthPlanner.Apply(offered, ProofOfWorthCommand.AcceptMark).Status);
            Assert.IsFalse(offered.LordshipGranted);
        }

        [Test]
        public void FreshWalkCompletesOmenThenC1AndGrantsLordshipOnlyAtMark()
        {
            ProofOfWorthState state = WalkTo(ProofOfWorthPhase.OmenReport, RealmId.Eldergrove);
            Assert.IsFalse(state.LordshipGranted);
            Assert.AreEqual(ProofOfWorthIds.OmenQuestId, state.QuestId);

            state = WalkFrom(state, ProofOfWorthCommand.SelectValerius);
            state = WalkFrom(state, ProofOfWorthCommand.PresentTear);
            Assert.IsFalse(state.LordshipGranted);
            state = WalkFrom(state, ProofOfWorthCommand.ConcludeReport);
            Assert.AreEqual(ProofOfWorthIds.MainQuestId, state.QuestId);
            Assert.AreEqual(ProofOfWorthIds.MeetGuideObjectiveId, state.ObjectiveId);
            Assert.IsFalse(state.LordshipGranted);

            state = WalkFrom(state, ProofOfWorthCommand.MeetRealmGuide);
            state = WalkFrom(state, ProofOfWorthCommand.RestoreCovenant);
            Assert.AreEqual(ProofOfWorthPhase.C1FaceGuardian, state.Phase);
            Assert.AreEqual(ProofOfWorthIds.FaceGuardianObjectiveId, state.ObjectiveId);
            Assert.IsFalse(state.LordshipGranted);

            state = WalkFrom(state, ProofOfWorthCommand.GuardianDefeated);
            Assert.AreEqual(ProofOfWorthPhase.C1AcceptMark, state.Phase);
            Assert.IsFalse(state.LordshipGranted);

            ProofOfWorthTransition mark = ProofOfWorthPlanner.Apply(state, ProofOfWorthCommand.AcceptMark);
            Assert.AreEqual(ProofOfWorthStatus.Applied, mark.Status);
            Assert.IsTrue(mark.State.LordshipGranted);
            Assert.AreEqual(ProofOfWorthIds.EldergroveVariantId, mark.State.ChapterVariantId);
            Assert.IsTrue(ProofOfWorthLordship.IsGranted(mark.State.ChapterVariantId));
        }

        [Test]
        public void AcceptMarkWithoutRealmIsRejected()
        {
            ProofOfWorthState state = WalkTo(ProofOfWorthPhase.C1AcceptMark, RealmId.None);
            ProofOfWorthTransition mark = ProofOfWorthPlanner.Apply(state, ProofOfWorthCommand.AcceptMark);
            Assert.AreEqual(ProofOfWorthStatus.Rejected, mark.Status);
            Assert.IsFalse(mark.State.LordshipGranted);
        }

        [Test]
        public void OldSaveWithoutMarkStaysLocked()
        {
            var empty = new SaveGameData();
            Assert.IsFalse(ProofOfWorthLordship.IsGranted(empty));

            var identityOnly = new SaveGameData
            {
                SelectedRealm = RealmId.Stonehold,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "warrior",
                    IdentityConfirmed = true,
                    LastResultId = string.Empty
                }
            };
            Assert.IsFalse(ProofOfWorthLordship.IsGranted(identityOnly));

            var leftoverVictory = new SaveGameData
            {
                SelectedRealm = RealmId.Stonehold,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "warrior",
                    IdentityConfirmed = true,
                    LastResultId = "ch01_proof_of_worth:victory"
                }
            };
            Assert.IsFalse(ProofOfWorthLordship.IsGranted(leftoverVictory));
        }

        [Test]
        public void WritingCh01RealmMarkGrantsLordshipAndKeepsOtherSavesLocked()
        {
            var save = new SaveGameData
            {
                SelectedRealm = RealmId.Umbral,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "assassin",
                    IdentityConfirmed = true
                }
            };
            Assert.IsTrue(ProofOfWorthLordship.TryWriteMark(save, ProofOfWorthIds.UmbralVariantId));
            Assert.AreEqual("ch01_umbral", save.ChampionCustomization.LastResultId);
            Assert.IsTrue(ProofOfWorthLordship.IsGranted(save));
            Assert.IsFalse(ProofOfWorthLordship.TryWriteMark(save, "C1_UM"));
            Assert.AreEqual("ch01_umbral", save.ChampionCustomization.LastResultId);
        }

        [Test]
        public void NoDialogueTransitionRetiresPreviousConversationSession()
        {
            var host = new GameObject("ProofConversationRetirementHost");
            ChampionController controller = CreateChampion(
                host,
                RealmId.Crownlands);
            ProofOfWorthDirector director = host.AddComponent<ProofOfWorthDirector>();
            director.EnsureReady(null, host.transform, RealmId.Crownlands);
            NpcConversationView conversation =
                Object.FindObjectOfType<NpcConversationView>();
            Assert.That(conversation, Is.Not.Null);

            conversation.Collapse();
            Assert.That(director.ApplyForTests(ProofOfWorthCommand.AcceptOffer).Changed,
                Is.True);
            conversation.Collapse();
            Assert.That(director.ApplyForTests(ProofOfWorthCommand.Investigate).Changed,
                Is.True);
            Assert.That(conversation.Session, Is.Not.Null);

            conversation.Collapse();
            Assert.That(director.ApplyForTests(ProofOfWorthCommand.DeployChampion).Changed,
                Is.True);
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenArena));

            conversation.Collapse();
            Assert.That(director.ApplyForTests(ProofOfWorthCommand.ArenaSuccess).Changed,
                Is.True);
            Assert.That(director.State.Phase, Is.EqualTo(ProofOfWorthPhase.OmenReport));
            Assert.That(conversation.IsVisible, Is.False);
            Assert.That(conversation.Session, Is.Null);
        }

        [Test]
        public void DirectorOffersOmenWithoutAccepting()
        {
            var host = new GameObject("ProofOfWorthHost");
            try
            {
                ChampionController controller = CreateChampion(
                    host,
                    RealmId.Crownlands);
                ProofOfWorthDirector director = host.AddComponent<ProofOfWorthDirector>();
                director.EnsureReady(null, host.transform, RealmId.Crownlands);
                Assert.IsTrue(director.State.IsOmenOffered);
                Assert.IsFalse(director.State.OmenAccepted);
                Assert.AreEqual(ProofOfWorthIds.OmenQuestId, director.State.QuestId);
                Assert.AreEqual(ProofOfWorthIds.OfferDialogueId, director.State.DialogueId);

                ProofOfWorthTransition decline = ApplyUnblocked(
                    director,
                    ProofOfWorthCommand.DeclineOffer);
                Assert.AreEqual(ProofOfWorthStatus.DuplicateIgnored, decline.Status);
                Assert.IsTrue(director.State.IsOmenOffered);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DirectorWalkGrantsLordshipOnlyAfterAcceptMark()
        {
            var host = new GameObject("ProofOfWorthHost");
            try
            {
                ChampionController controller = CreateChampion(
                    host,
                    RealmId.Stonehold);
                ProofOfWorthDirector director = host.AddComponent<ProofOfWorthDirector>();
                director.EnsureReady(null, host.transform, RealmId.Stonehold);
                PlayThroughDirector(director);
                Assert.IsTrue(director.State.LordshipGranted);
                Assert.AreEqual(ProofOfWorthIds.StoneholdVariantId, director.State.ChapterVariantId);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ProductionProofApiExposesNoDirectWorldInteractionProgression()
        {
            System.Reflection.MethodInfo[] publicMethods =
                typeof(ProofOfWorthDirector).GetMethods(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public);

            Assert.IsFalse(System.Array.Exists(
                publicMethods,
                method =>
                    method.Name == "TryApplyWorldInteraction" &&
                    method.GetParameters().Length == 1 &&
                    (method.GetParameters()[0].ParameterType == typeof(string) ||
                     method.GetParameters()[0].ParameterType ==
                     typeof(WorldInteractionResult))));
        }

        [Test]
        public void MatchingWorldInteractionsAdvanceC1WhileWrongTargetIsRejected()
        {
            var host = new GameObject("ProofOfWorthWorldInteractionHost");
            try
            {
                ChampionController controller = CreateChampion(
                    host,
                    RealmId.Crownlands);
                ProofOfWorthDirector director = host.AddComponent<ProofOfWorthDirector>();
                director.EnsureReady(null, host.transform, RealmId.Crownlands);
                foreach (ProofOfWorthCommand command in new[]
                {
                    ProofOfWorthCommand.AcceptOffer,
                    ProofOfWorthCommand.Investigate,
                    ProofOfWorthCommand.DeployChampion,
                    ProofOfWorthCommand.ArenaSuccess,
                    ProofOfWorthCommand.SelectValerius,
                    ProofOfWorthCommand.PresentTear,
                    ProofOfWorthCommand.ConcludeReport
                })
                {
                    Assert.IsTrue(ApplyUnblocked(director, command).Changed);
                }

                Assert.AreEqual(ProofOfWorthPhase.C1MeetGuide, director.State.Phase);
                Assert.IsFalse(director.ApplyWorldInteractionForTests(
                    FirstSessionWorldInteractables.CovenantSiteCatalogId));
                Assert.IsTrue(director.ApplyWorldInteractionForTests(
                    FirstSessionWorldInteractables.GuideCatalogId));
                Assert.AreEqual(ProofOfWorthPhase.C1RestoreCovenant, director.State.Phase);
                Assert.IsTrue(director.ApplyWorldInteractionForTests(
                    FirstSessionWorldInteractables.CovenantSiteCatalogId));
                Assert.AreEqual(ProofOfWorthPhase.C1FaceGuardian, director.State.Phase);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void GuideWorldInteractionStartsTheValeriusOmenDialogue()
        {
            var host = new GameObject("ProofOfWorthOmenInteractionHost");
            try
            {
                ChampionController controller = CreateChampion(
                    host,
                    RealmId.Crownlands);
                ProofOfWorthDirector director = host.AddComponent<ProofOfWorthDirector>();
                director.EnsureReady(null, host.transform, RealmId.Crownlands);

                Assert.IsTrue(director.State.IsOmenOffered);
                Object.FindObjectOfType<NpcConversationView>().Collapse();
                Assert.IsFalse(director.ApplyWorldInteractionForTests(
                    FirstSessionWorldInteractables.CovenantSiteCatalogId));
                Assert.IsTrue(director.ApplyWorldInteractionForTests(
                    FirstSessionWorldInteractables.GuideCatalogId));
                Assert.IsTrue(
                    Object.FindObjectOfType<NpcConversationView>().SkipCurrentLine());
                Assert.AreEqual(ProofOfWorthPhase.OmenTalk, director.State.Phase);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RepeatedGuideWorldInteractionsAdvanceTheOmenDialogueToArena()
        {
            var host = new GameObject("ProofOfWorthRepeatedGuideInteractionHost");
            try
            {
                ChampionController controller = CreateChampion(
                    host,
                    RealmId.Crownlands);
                ProofOfWorthDirector director = host.AddComponent<ProofOfWorthDirector>();
                director.EnsureReady(null, host.transform, RealmId.Crownlands);

                Object.FindObjectOfType<NpcConversationView>().Collapse();
                Assert.IsTrue(director.ApplyWorldInteractionForTests(
                    FirstSessionWorldInteractables.GuideCatalogId));
                Assert.IsTrue(
                    Object.FindObjectOfType<NpcConversationView>().SkipCurrentLine());
                Assert.AreEqual(ProofOfWorthPhase.OmenTalk, director.State.Phase);
                Assert.AreEqual(ProofOfWorthIds.StartDialogueId, director.State.DialogueId);

                Object.FindObjectOfType<NpcConversationView>().Collapse();
                Assert.IsTrue(director.ApplyWorldInteractionForTests(
                    FirstSessionWorldInteractables.GuideCatalogId));
                Assert.IsTrue(
                    Object.FindObjectOfType<NpcConversationView>().SkipCurrentLine());
                Assert.AreEqual(ProofOfWorthPhase.OmenTalk, director.State.Phase);
                Assert.AreEqual(ProofOfWorthIds.GoDialogueId, director.State.DialogueId);

                Object.FindObjectOfType<NpcConversationView>().Collapse();
                Assert.IsTrue(director.ApplyWorldInteractionForTests(
                    FirstSessionWorldInteractables.GuideCatalogId));
                Assert.IsTrue(
                    Object.FindObjectOfType<NpcConversationView>().SkipCurrentLine());
                Assert.AreEqual(ProofOfWorthPhase.OmenArena, director.State.Phase);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static ChampionController CreateChampion(
            GameObject host,
            RealmId realm)
        {
            host.AddComponent<CharacterController>();
            host.AddComponent<ChampionCombat>();
            host.AddComponent<SkillCaster>();
            ChampionController controller = host.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(realm);
            return controller;
        }

        private static void PlayThroughDirector(ProofOfWorthDirector director)
        {
            Assert.IsTrue(ApplyUnblocked(director, ProofOfWorthCommand.AcceptOffer).Changed);
            Assert.IsTrue(ApplyUnblocked(director, ProofOfWorthCommand.Investigate).Changed);
            Assert.IsTrue(ApplyUnblocked(director, ProofOfWorthCommand.DeployChampion).Changed);
            Assert.IsTrue(ApplyUnblocked(director, ProofOfWorthCommand.ArenaSuccess).Changed);
            Assert.IsTrue(ApplyUnblocked(director, ProofOfWorthCommand.SelectValerius).Changed);
            Assert.IsTrue(ApplyUnblocked(director, ProofOfWorthCommand.PresentTear).Changed);
            Assert.IsTrue(ApplyUnblocked(director, ProofOfWorthCommand.ConcludeReport).Changed);
            Assert.IsFalse(director.State.LordshipGranted);
            Assert.IsTrue(ApplyUnblocked(director, ProofOfWorthCommand.MeetRealmGuide).Changed);
            Assert.IsTrue(ApplyUnblocked(director, ProofOfWorthCommand.RestoreCovenant).Changed);
            Assert.IsTrue(ApplyUnblocked(director, ProofOfWorthCommand.GuardianDefeated).Changed);
            Assert.IsFalse(director.State.LordshipGranted);
            Assert.IsTrue(ApplyUnblocked(director, ProofOfWorthCommand.AcceptMark).Changed);
        }

        private static ProofOfWorthTransition ApplyUnblocked(
            ProofOfWorthDirector director,
            ProofOfWorthCommand command)
        {
            NpcConversationView conversation =
                Object.FindObjectOfType<NpcConversationView>();
            if (conversation != null && conversation.IsVisible)
            {
                conversation.Collapse();
            }

            return director.ApplyForTests(command);
        }

        private static ProofOfWorthState WalkTo(ProofOfWorthPhase phase, RealmId realm)
        {
            ProofOfWorthState state = ProofOfWorthPlanner.CreateOffered(realm);
            ProofOfWorthCommand[] commands =
            {
                ProofOfWorthCommand.AcceptOffer,
                ProofOfWorthCommand.Investigate,
                ProofOfWorthCommand.DeployChampion,
                ProofOfWorthCommand.ArenaSuccess,
                ProofOfWorthCommand.SelectValerius,
                ProofOfWorthCommand.PresentTear,
                ProofOfWorthCommand.ConcludeReport,
                ProofOfWorthCommand.MeetRealmGuide,
                ProofOfWorthCommand.RestoreCovenant,
                ProofOfWorthCommand.GuardianDefeated
            };

            for (int i = 0; i < commands.Length && state.Phase != phase; i++)
            {
                state = WalkFrom(state, commands[i]);
            }

            Assert.AreEqual(phase, state.Phase);
            return state;
        }

        private static ProofOfWorthState WalkFrom(ProofOfWorthState state, ProofOfWorthCommand command)
        {
            ProofOfWorthTransition transition = ProofOfWorthPlanner.Apply(state, command);
            Assert.AreEqual(ProofOfWorthStatus.Applied, transition.Status, command.ToString());
            return transition.State;
        }
    }
}
