using System;
using System.IO;
using System.Reflection;
using AL.ChampionMode.Control;
using AL.ChampionMode.Quests;
using AL.ChampionMode.Skills;
using AL.ChampionMode.Tutorial;
using AL.Core;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class FirstWorldProgressPersistenceTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-FirstWorldProgressTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void MissingLegacySlotDefaultsWithoutRewritingOldSave()
        {
            LocalSaveGameService service = CreateSaveService(_root);
            service.CreateNewSave(RealmId.Crownlands);

            Assert.That(service.CurrentSave, Is.Not.Null);
            Assert.That(
                service.CurrentSave.FirstWorldProgress == null ||
                service.CurrentSave.FirstWorldProgress.Version == 0,
                Is.True,
                "Unity JsonUtility may materialize an absent inline class as a neutral object; " +
                "that representation must remain legacy rather than durable progress.");
            string primaryPath = Path.Combine(_root, "save.json");
            byte[] beforeRead = File.ReadAllBytes(primaryPath);
            Assert.That(
                FirstWorldProgressSaveAuthority.TryRead(
                    service,
                    out FirstWorldProgressSnapshot snapshot,
                    out string message),
                Is.True,
                message);
            Assert.That(
                snapshot.ReadDisposition,
                Is.EqualTo(FirstWorldProgressReadDisposition.LegacyDefault));
            Assert.That(snapshot.Revision, Is.Zero);
            Assert.That(
                snapshot.Tutorial.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.CameraLook));
            Assert.That(snapshot.IsTutorialComplete, Is.False);
            Assert.That(snapshot.HandoffCommitted, Is.False);
            Assert.That(snapshot.Proof, Is.Null);
            CollectionAssert.AreEqual(
                beforeRead,
                File.ReadAllBytes(primaryPath),
                "Reading an old save must not rewrite or silently migrate its bytes.");

            LocalSaveGameService reloaded = CreateSaveService(_root);
            reloaded.Load();
            Assert.That(reloaded.CurrentSave, Is.Not.Null, reloaded.LastLoadMessage);
            Assert.That(
                reloaded.CurrentSave.FirstWorldProgress == null ||
                reloaded.CurrentSave.FirstWorldProgress.Version == 0,
                Is.True);
            Assert.That(
                FirstWorldProgressSaveAuthority.TryRead(
                    reloaded,
                    out FirstWorldProgressSnapshot resumed,
                    out message),
                Is.True,
                message);
            Assert.That(
                resumed.ReadDisposition,
                Is.EqualTo(FirstWorldProgressReadDisposition.LegacyDefault));
        }

        [Test]
        public void LegacySaveWithoutMapDisclosureLoadsWithoutRewritingBytes()
        {
            LocalSaveGameService service = CreateSaveService(_root);
            service.CreateNewSave(RealmId.Crownlands);
            string primaryPath = Path.Combine(_root, "save.json");
            byte[] legacyBytes = File.ReadAllBytes(primaryPath);

            Assert.That(
                service.CurrentSave.MapDisclosure == null ||
                service.CurrentSave.MapDisclosure.Version == 0,
                Is.True);

            LocalSaveGameService reloaded = CreateSaveService(_root);
            reloaded.Load();

            Assert.That(reloaded.CurrentSave, Is.Not.Null, reloaded.LastLoadMessage);
            Assert.That(
                reloaded.CurrentSave.MapDisclosure == null ||
                reloaded.CurrentSave.MapDisclosure.Version == 0,
                Is.True);
            CollectionAssert.AreEqual(
                legacyBytes,
                File.ReadAllBytes(primaryPath),
                "Loading a save without map disclosure state must not rewrite its bytes.");
        }

        [Test]
        public void TutorialResumesEachDurableBeatAndHandsOffOmenExactlyOnce()
        {
            LocalSaveGameService service = CreateSaveService(_root);
            service.CreateNewSave(RealmId.Eldergrove);
            FirstWorldProgressSnapshot initial = Read(service);

            FirstWorldProgressCommitResult look =
                FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                    service,
                    initial,
                    FirstWorldTutorialProgressCommand.CameraLookAccepted);
            AssertCommitted(look, FirstWorldEntryTeachingBeat.Move, revision: 1);

            service = Reload();
            FirstWorldProgressSnapshot moveReady = Read(service);
            Assert.That(
                moveReady.Tutorial.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.Move));

            FirstWorldProgressCommitResult movement =
                FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                    service,
                    moveReady,
                    FirstWorldTutorialProgressCommand.MovementAccepted,
                    blockTaught: true);
            AssertCommitted(
                movement,
                FirstWorldEntryTeachingBeat.Interact,
                revision: 2);
            Assert.That(movement.Snapshot.Tutorial.BlockTaught, Is.True);

            service = Reload();
            FirstWorldProgressSnapshot interactReady = Read(service);
            Assert.That(
                interactReady.Tutorial.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.Interact));
            Assert.That(interactReady.Tutorial.BlockTaught, Is.True);

            FirstWorldProgressCommitResult interaction =
                FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                    service,
                    interactReady,
                    FirstWorldTutorialProgressCommand.GuideInteractionAccepted);
            AssertCommitted(
                interaction,
                FirstWorldEntryTeachingBeat.BasicAttack,
                revision: 3);

            service = Reload();
            FirstWorldProgressSnapshot attackReady = Read(service);
            FirstWorldProgressCommitResult attack =
                FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                    service,
                    attackReady,
                    FirstWorldTutorialProgressCommand.BasicAttackAccepted);
            Assert.That(attack.Accepted, Is.True, attack.Message);
            Assert.That(attack.Persisted, Is.True, attack.Message);
            AssertCompletedHandoff(attack.Snapshot, revision: 4);

            FirstWorldProgressCommitResult replay =
                FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                    service,
                    attackReady,
                    FirstWorldTutorialProgressCommand.BasicAttackAccepted);
            Assert.That(replay.Accepted, Is.True, replay.Message);
            Assert.That(replay.Persisted, Is.False, replay.Message);
            AssertCompletedHandoff(replay.Snapshot, revision: 4);

            service = Reload();
            AssertCompletedHandoff(Read(service), revision: 4);
        }

        [Test]
        public void ProofStateReloadsAndDuplicateCommandDoesNotAdvanceRevision()
        {
            LocalSaveGameService service = CreateCompletedTutorialService();
            FirstWorldProgressSnapshot offered = Read(service);

            FirstWorldProgressCommitResult accepted =
                FirstWorldProgressSaveAuthority.TryAdvanceProof(
                    service,
                    offered,
                    ProofOfWorthCommand.AcceptOffer);
            Assert.That(accepted.Accepted, Is.True, accepted.Message);
            Assert.That(accepted.Persisted, Is.True, accepted.Message);
            Assert.That(
                accepted.Snapshot.Proof.Phase,
                Is.EqualTo(ProofOfWorthPhase.OmenTalk));
            Assert.That(accepted.Snapshot.Revision, Is.EqualTo(5));

            FirstWorldProgressCommitResult replay =
                FirstWorldProgressSaveAuthority.TryAdvanceProof(
                    service,
                    offered,
                    ProofOfWorthCommand.AcceptOffer);
            Assert.That(replay.Accepted, Is.True, replay.Message);
            Assert.That(replay.Persisted, Is.False, replay.Message);
            Assert.That(replay.Snapshot.Revision, Is.EqualTo(5));

            LocalSaveGameService reloaded = Reload();
            FirstWorldProgressSnapshot resumed = Read(reloaded);
            Assert.That(resumed.Revision, Is.EqualTo(5));
            Assert.That(
                resumed.Proof.Phase,
                Is.EqualTo(ProofOfWorthPhase.OmenTalk));
            Assert.That(resumed.Proof.OmenAccepted, Is.True);
        }

        [Test]
        public void DirectorRestoresReloadedProofAndPersistsBeforePublishing()
        {
            LocalSaveGameService service = CreateCompletedTutorialService();
            FirstWorldProgressCommitResult accepted =
                FirstWorldProgressSaveAuthority.TryAdvanceProof(
                    service,
                    Read(service),
                    ProofOfWorthCommand.AcceptOffer);
            Assert.That(accepted.Accepted, Is.True, accepted.Message);
            Assert.That(accepted.Persisted, Is.True, accepted.Message);

            LocalSaveGameService reloaded = Reload();
            FirstWorldProgressSnapshot resumed = Read(reloaded);
            var host = new GameObject("DurableProofResumeHost");
            try
            {
                host.AddComponent<CharacterController>();
                host.AddComponent<ChampionCombat>();
                host.AddComponent<SkillCaster>();
                ChampionController controller = host.AddComponent<ChampionController>();
                controller.ConfigureRealmContext(RealmId.Crownlands);
                ProofOfWorthDirector director =
                    host.AddComponent<ProofOfWorthDirector>();
                MethodInfo initialize = typeof(ProofOfWorthDirector).GetMethod(
                    "EnsureReadyDurable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(initialize, Is.Not.Null);
                initialize.Invoke(
                    director,
                    new object[]
                    {
                        null,
                        host.transform,
                        RealmId.Crownlands,
                        reloaded,
                        resumed
                    });

                Assert.That(
                    director.State.Phase,
                    Is.EqualTo(ProofOfWorthPhase.OmenTalk));
                Assert.That(
                    director.State.DialogueId,
                    Is.EqualTo(ProofOfWorthIds.StartDialogueId));

                NpcConversationView conversation =
                    Object.FindObjectOfType<NpcConversationView>();
                Assert.That(conversation, Is.Not.Null);
                conversation.Collapse();

                ProofOfWorthTransition investigate =
                    director.ApplyForTests(ProofOfWorthCommand.Investigate);
                Assert.That(investigate.Changed, Is.True);
                Assert.That(
                    director.State.DialogueId,
                    Is.EqualTo(ProofOfWorthIds.GoDialogueId));
                Assert.That(
                    director.ApplyForTests(ProofOfWorthCommand.Investigate).Changed,
                    Is.False,
                    "A replayed command must not publish a second transition.");

                LocalSaveGameService afterDirector = Reload();
                FirstWorldProgressSnapshot persisted = Read(afterDirector);
                Assert.That(persisted.Revision, Is.EqualTo(6));
                Assert.That(
                    persisted.Proof.DialogueId,
                    Is.EqualTo(ProofOfWorthIds.GoDialogueId));
            }
            finally
            {
                Object.DestroyImmediate(host);
                ProofOfWorthDirector.ResetForTests();
            }
        }

        [Test]
        public void LordshipReconciliationFinishesTypedProofCommitExactlyOnce()
        {
            LocalSaveGameService service = CreateCompletedTutorialService();
            MvpLoopCommitResult identity = MvpLoopSaveAuthority.TryCommit(
                service,
                new MvpLoopCommitRequest(
                    Guid.NewGuid().ToString("N"),
                    RealmId.Crownlands,
                    ClassFamily.Warrior,
                    confirmIdentity: true,
                    lastResultId: string.Empty,
                    buildingId: string.Empty,
                    buildingLevel: 0,
                    username: "ProofTester"));
            Assert.That(identity.Accepted, Is.True, identity.Message);
            Assert.That(identity.Persisted, Is.True, identity.Message);

            FirstWorldProgressSnapshot snapshot = Read(service);
            foreach (ProofOfWorthCommand command in new[]
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
                     })
            {
                FirstWorldProgressCommitResult step =
                    FirstWorldProgressSaveAuthority.TryAdvanceProof(
                        service,
                        snapshot,
                        command);
                Assert.That(step.Accepted, Is.True, command + ": " + step.Message);
                Assert.That(step.Persisted, Is.True, command + ": " + step.Message);
                snapshot = step.Snapshot;
            }

            Assert.That(
                snapshot.Proof.Phase,
                Is.EqualTo(ProofOfWorthPhase.C1AcceptMark));
            FirstWorldProgressSnapshot beforeMark = snapshot;

            MvpLoopCommitResult mark = ProofOfWorthLordship.TryPersist(
                service,
                RealmId.Crownlands);
            Assert.That(mark.Accepted, Is.True, mark.Message);
            Assert.That(mark.Persisted, Is.True, mark.Message);
            Assert.That(ProofOfWorthLordship.IsGranted(service.CurrentSave), Is.True);

            FirstWorldProgressCommitResult final =
                FirstWorldProgressSaveAuthority.TryAdvanceProof(
                    service,
                    beforeMark,
                    ProofOfWorthCommand.AcceptMark);
            Assert.That(final.Accepted, Is.True, final.Message);
            Assert.That(final.Persisted, Is.True, final.Message);
            Assert.That(final.Snapshot.Revision, Is.EqualTo(15));
            Assert.That(
                final.Snapshot.Proof.Phase,
                Is.EqualTo(ProofOfWorthPhase.LordshipGranted));

            FirstWorldProgressCommitResult replay =
                FirstWorldProgressSaveAuthority.TryAdvanceProof(
                    service,
                    beforeMark,
                    ProofOfWorthCommand.AcceptMark);
            Assert.That(replay.Accepted, Is.True, replay.Message);
            Assert.That(replay.Persisted, Is.False, replay.Message);
            Assert.That(replay.Snapshot.Revision, Is.EqualTo(15));

            LocalSaveGameService reloaded = Reload();
            FirstWorldProgressSnapshot resumed = Read(reloaded);
            Assert.That(resumed.Revision, Is.EqualTo(15));
            Assert.That(
                resumed.Proof.Phase,
                Is.EqualTo(ProofOfWorthPhase.LordshipGranted));
            Assert.That(ProofOfWorthLordship.IsGranted(reloaded.CurrentSave), Is.True);
        }

        [Test]
        public void OldLordshipSaveReconcilesPastTutorialWithoutReplayingOmen()
        {
            var legacy = new SaveGameData
            {
                SelectedRealm = RealmId.Stonehold
            };
            Assert.That(
                ProofOfWorthLordship.TryWriteMark(
                    legacy,
                    ProofOfWorthLordship.ResolveMarkId(RealmId.Stonehold)),
                Is.True);
            Assert.That(legacy.FirstWorldProgress, Is.Null);

            Assert.That(
                FirstWorldProgressSaveCodec.TryRead(
                    legacy,
                    out FirstWorldProgressSnapshot snapshot,
                    out string message),
                Is.True,
                message);
            Assert.That(
                snapshot.ReadDisposition,
                Is.EqualTo(
                    FirstWorldProgressReadDisposition.ReconciledFromLordship));
            AssertCompletedHandoff(snapshot, revision: 0);
            Assert.That(
                snapshot.Proof.Phase,
                Is.EqualTo(ProofOfWorthPhase.LordshipGranted));
        }

        [Test]
        public void ContradictoryStoredProgressFailsClosed()
        {
            LocalSaveGameService service = CreateCompletedTutorialService();
            FirstWorldProgressData data = service.CurrentSave.FirstWorldProgress;
            data.HandoffCommitted = false;

            Assert.That(
                FirstWorldProgressSaveCodec.TryRead(
                    service.CurrentSave,
                    out FirstWorldProgressSnapshot snapshot,
                    out string message),
                Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(message, Is.EqualTo("AL-FIRST-WORLD-HANDOFF-MISSING"));
        }

        private LocalSaveGameService CreateCompletedTutorialService()
        {
            LocalSaveGameService service = CreateSaveService(_root);
            service.CreateNewSave(RealmId.Crownlands);
            FirstWorldProgressSnapshot snapshot = Read(service);
            snapshot = Commit(
                service,
                snapshot,
                FirstWorldTutorialProgressCommand.CameraLookAccepted).Snapshot;
            snapshot = Commit(
                service,
                snapshot,
                FirstWorldTutorialProgressCommand.MovementAccepted).Snapshot;
            snapshot = Commit(
                service,
                snapshot,
                FirstWorldTutorialProgressCommand.GuideInteractionAccepted).Snapshot;
            snapshot = Commit(
                service,
                snapshot,
                FirstWorldTutorialProgressCommand.BasicAttackAccepted).Snapshot;
            AssertCompletedHandoff(snapshot, revision: 4);
            return service;
        }

        private static FirstWorldProgressCommitResult Commit(
            LocalSaveGameService service,
            FirstWorldProgressSnapshot expected,
            FirstWorldTutorialProgressCommand command)
        {
            FirstWorldProgressCommitResult result =
                FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                    service,
                    expected,
                    command);
            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.Persisted, Is.True, result.Message);
            return result;
        }

        private static void AssertCommitted(
            FirstWorldProgressCommitResult result,
            FirstWorldEntryTeachingBeat expectedBeat,
            long revision)
        {
            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.Persisted, Is.True, result.Message);
            Assert.That(result.Snapshot.Revision, Is.EqualTo(revision));
            Assert.That(
                result.Snapshot.Tutorial.TeachingBeat,
                Is.EqualTo(expectedBeat));
        }

        private static void AssertCompletedHandoff(
            FirstWorldProgressSnapshot snapshot,
            long revision)
        {
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot.Revision, Is.EqualTo(revision));
            Assert.That(snapshot.IsTutorialComplete, Is.True);
            Assert.That(snapshot.HandoffCommitted, Is.True);
            Assert.That(snapshot.Tutorial.CompletionEventCount, Is.EqualTo(1));
            Assert.That(snapshot.Tutorial.OmenOfferCount, Is.EqualTo(1));
            Assert.That(snapshot.CanRunProof, Is.True);
        }

        private static FirstWorldProgressSnapshot Read(
            LocalSaveGameService service)
        {
            Assert.That(
                FirstWorldProgressSaveAuthority.TryRead(
                    service,
                    out FirstWorldProgressSnapshot snapshot,
                    out string message),
                Is.True,
                message);
            return snapshot;
        }

        private LocalSaveGameService Reload()
        {
            LocalSaveGameService service = CreateSaveService(_root);
            service.Load();
            Assert.That(service.CurrentSave, Is.Not.Null, service.LastLoadMessage);
            return service;
        }

        private static LocalSaveGameService CreateSaveService(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(string) },
                    modifiers: null);
            Assert.That(constructor, Is.Not.Null);
            return (LocalSaveGameService)constructor.Invoke(
                new object[] { root });
        }
    }
}
