using AL.ChampionMode;
using AL.ChampionMode.Quests;
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
        public void DirectorOffersOmenWithoutAccepting()
        {
            var host = new GameObject("ProofOfWorthHost");
            try
            {
                ProofOfWorthDirector director = host.AddComponent<ProofOfWorthDirector>();
                director.EnsureReady(null, host.transform, RealmId.Crownlands);
                Assert.IsTrue(director.State.IsOmenOffered);
                Assert.IsFalse(director.State.OmenAccepted);
                Assert.AreEqual(ProofOfWorthIds.OmenQuestId, director.State.QuestId);
                Assert.AreEqual(ProofOfWorthIds.OfferDialogueId, director.State.DialogueId);

                ProofOfWorthTransition decline = director.ApplyForTests(ProofOfWorthCommand.DeclineOffer);
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

        private static void PlayThroughDirector(ProofOfWorthDirector director)
        {
            Assert.IsTrue(director.ApplyForTests(ProofOfWorthCommand.AcceptOffer).Changed);
            Assert.IsTrue(director.ApplyForTests(ProofOfWorthCommand.Investigate).Changed);
            Assert.IsTrue(director.ApplyForTests(ProofOfWorthCommand.DeployChampion).Changed);
            Assert.IsTrue(director.ApplyForTests(ProofOfWorthCommand.ArenaSuccess).Changed);
            Assert.IsTrue(director.ApplyForTests(ProofOfWorthCommand.SelectValerius).Changed);
            Assert.IsTrue(director.ApplyForTests(ProofOfWorthCommand.PresentTear).Changed);
            Assert.IsTrue(director.ApplyForTests(ProofOfWorthCommand.ConcludeReport).Changed);
            Assert.IsFalse(director.State.LordshipGranted);
            Assert.IsTrue(director.ApplyForTests(ProofOfWorthCommand.MeetRealmGuide).Changed);
            Assert.IsTrue(director.ApplyForTests(ProofOfWorthCommand.RestoreCovenant).Changed);
            Assert.IsTrue(director.ApplyForTests(ProofOfWorthCommand.GuardianDefeated).Changed);
            Assert.IsFalse(director.State.LordshipGranted);
            Assert.IsTrue(director.ApplyForTests(ProofOfWorthCommand.AcceptMark).Changed);
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
