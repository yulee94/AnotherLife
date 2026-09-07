using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AL.ChampionMode.Quests;
using AL.ChampionMode.Tutorial;
using AL.Core;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public sealed class ProfileBoundFirstSessionTests
    {
        private const string PrimarySaveName = "save.json";
        private const string Username = "ProfileBound";
        private const string OtherUsername = "OtherBound";

        [Test]
        public void SchemaTwoIdentityDraftAndConfirmReloadWithSameProfileId()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateBoundRealm(root, RealmId.Eldergrove);
                string profileId = save.CurrentSave.ProfileId;

                MvpLoopCommitResult draft = MvpLoopSaveAuthority.TryCommit(
                    save,
                    IdentityRequest(
                        RealmId.Eldergrove,
                        ClassFamily.Ranger,
                        confirmIdentity: false,
                        username: string.Empty));
                Assert.That(draft.Accepted, Is.True, draft.Message);
                Assert.That(draft.Persisted, Is.True, draft.Message);
                MvpLoopSnapshot draftSnapshot = MvpLoopSaveCodec.Read(save.CurrentSave);
                Assert.That(draftSnapshot.ClassFamily, Is.EqualTo(ClassFamily.Ranger));
                Assert.That(draftSnapshot.IdentityConfirmed, Is.False);

                MvpLoopCommitResult confirm = MvpLoopSaveAuthority.TryCommit(
                    save,
                    IdentityRequest(
                        RealmId.Eldergrove,
                        ClassFamily.Ranger,
                        confirmIdentity: true,
                        username: Username));
                Assert.That(confirm.Accepted, Is.True, confirm.Message);
                Assert.That(confirm.Persisted, Is.True, confirm.Message);

                LocalSaveGameService reloaded = Reload(root);
                Assert.That(reloaded.CurrentSave.ProfileId, Is.EqualTo(profileId));
                MvpLoopSnapshot resumed = MvpLoopSaveCodec.Read(reloaded.CurrentSave);
                Assert.That(resumed.Realm, Is.EqualTo(RealmId.Eldergrove));
                Assert.That(resumed.ClassFamily, Is.EqualTo(ClassFamily.Ranger));
                Assert.That(resumed.IdentityConfirmed, Is.True);
                Assert.That(resumed.Username, Is.EqualTo(Username));
                Assert.That(resumed.ShouldSkipCreate, Is.True);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SchemaTwoIdentityExactReplayDoesNotRewriteDisk()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateBoundRealm(root, RealmId.Crownlands);
                MvpLoopCommitRequest request = IdentityRequest(
                    RealmId.Crownlands,
                    ClassFamily.Warrior,
                    confirmIdentity: true,
                    username: Username);
                MvpLoopCommitResult first = MvpLoopSaveAuthority.TryCommit(save, request);
                Assert.That(first.Accepted, Is.True, first.Message);
                Assert.That(first.Persisted, Is.True, first.Message);
                byte[] afterFirst = ReadPrimary(root);

                MvpLoopCommitResult replay = MvpLoopSaveAuthority.TryCommit(save, request);
                Assert.That(replay.Accepted, Is.True, replay.Message);
                Assert.That(replay.Persisted, Is.False, replay.Message);
                CollectionAssert.AreEqual(afterFirst, ReadPrimary(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SchemaTwoNewProfilesRejectLegacyOnlyMvpAndFirstWorldWriters()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateBoundRealm(root, RealmId.Crownlands);
                FirstWorldProgressSnapshot snapshot = ReadFirstWorld(save);
                byte[] before = ReadPrimary(root);

                SaveCandidateCommitResult mvp =
                    ((ILegacyMvpLoopCandidateStore)save).TryCommitLegacyMvpLoop(
                        IdentityRequest(
                            RealmId.Crownlands,
                            ClassFamily.Warrior,
                            confirmIdentity: true,
                            username: Username));
                Assert.That(mvp.Outcome, Is.EqualTo(SaveCandidateCommitOutcome.ReadOnly));
                Assert.That(mvp.Message, Is.EqualTo("AL-MVP-LOOP-PROFILE-READ-ONLY"));

                string operationId = FirstWorldProgressSaveCodec.BuildOperationId(
                    snapshot,
                    FirstWorldTutorialProgressCommand.CameraLookAccepted,
                    blockTaught: false,
                    proofCommand: ProofOfWorthCommand.Invalid);
                SaveCandidateCommitResult firstWorld =
                    ((ILegacyFirstWorldProgressCandidateStore)save)
                        .TryCommitLegacyFirstWorldProgress(
                            new FirstWorldProgressCommitRequest(
                                operationId,
                                operationId,
                                snapshot,
                                FirstWorldTutorialProgressCommand.CameraLookAccepted,
                                blockTaught: false,
                                proofCommand: ProofOfWorthCommand.Invalid));
                Assert.That(
                    firstWorld.Outcome,
                    Is.EqualTo(SaveCandidateCommitOutcome.ReadOnly));
                Assert.That(
                    firstWorld.Message,
                    Is.EqualTo("AL-FIRST-WORLD-PROFILE-READ-ONLY"));

                CollectionAssert.AreEqual(before, ReadPrimary(root));
                Assert.That(ReadFirstWorld(save).Revision, Is.Zero);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SchemaTwoIdentityRejectsBuildingCreationAndUpgrade()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateBoundRealm(root, RealmId.Stonehold);
                MvpLoopCommitResult identity = MvpLoopSaveAuthority.TryCommit(
                    save,
                    IdentityRequest(
                        RealmId.Stonehold,
                        ClassFamily.Warrior,
                        confirmIdentity: true,
                        username: Username));
                Assert.That(identity.Accepted, Is.True, identity.Message);
                byte[] before = ReadPrimary(root);

                MvpLoopCommitResult createBuilding = MvpLoopSaveAuthority.TryCommit(
                    save,
                    new MvpLoopCommitRequest(
                        NewTransactionId(),
                        RealmId.Stonehold,
                        ClassFamily.Warrior,
                        true,
                        string.Empty,
                        MvpLoopSaveCodec.DefaultOneBuildId,
                        1,
                        Username));
                AssertRejectedWithoutDiskChange(createBuilding, root, before);

                MvpLoopCommitResult upgradeBuilding = MvpLoopSaveAuthority.TryCommit(
                    save,
                    new MvpLoopCommitRequest(
                        NewTransactionId(),
                        RealmId.Stonehold,
                        ClassFamily.Warrior,
                        true,
                        string.Empty,
                        MvpLoopSaveCodec.DefaultOneBuildId,
                        2,
                        Username));
                AssertRejectedWithoutDiskChange(upgradeBuilding, root, before);

                MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(save.CurrentSave);
                Assert.That(snapshot.LastBuildId, Is.EqualTo(string.Empty));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SchemaTwoIdentityRejectsArbitraryResultGrant()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateBoundRealm(root, RealmId.Stonehold);
                MvpLoopCommitResult identity = MvpLoopSaveAuthority.TryCommit(
                    save,
                    IdentityRequest(
                        RealmId.Stonehold,
                        ClassFamily.Warrior,
                        confirmIdentity: true,
                        username: Username));
                Assert.That(identity.Accepted, Is.True, identity.Message);
                byte[] before = ReadPrimary(root);

                MvpLoopCommitResult arbitraryResult = MvpLoopSaveAuthority.TryCommit(
                    save,
                    new MvpLoopCommitRequest(
                        NewTransactionId(),
                        RealmId.Stonehold,
                        ClassFamily.Warrior,
                        true,
                        "ch01_proof_of_worth:victory",
                        string.Empty,
                        0,
                        Username));
                AssertRejectedWithoutDiskChange(arbitraryResult, root, before);
                Assert.That(
                    MvpLoopSaveCodec.Read(save.CurrentSave).LastResultId,
                    Is.EqualTo(string.Empty));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SchemaTwoConfirmedIdentityRejectsClassRewrite()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateBoundRealm(root, RealmId.Stonehold);
                MvpLoopCommitResult identity = MvpLoopSaveAuthority.TryCommit(
                    save,
                    IdentityRequest(
                        RealmId.Stonehold,
                        ClassFamily.Warrior,
                        confirmIdentity: true,
                        username: Username));
                Assert.That(identity.Accepted, Is.True, identity.Message);
                byte[] before = ReadPrimary(root);

                MvpLoopCommitResult rewriteClass = MvpLoopSaveAuthority.TryCommit(
                    save,
                    IdentityRequest(
                        RealmId.Stonehold,
                        ClassFamily.Mage,
                        confirmIdentity: true,
                        username: Username));
                AssertRejectedWithoutDiskChange(rewriteClass, root, before);
                MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(save.CurrentSave);
                Assert.That(snapshot.ClassFamily, Is.EqualTo(ClassFamily.Warrior));
                Assert.That(snapshot.Username, Is.EqualTo(Username));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SchemaTwoConfirmedIdentityRejectsUsernameRewrite()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateBoundRealm(root, RealmId.Stonehold);
                MvpLoopCommitResult identity = MvpLoopSaveAuthority.TryCommit(
                    save,
                    IdentityRequest(
                        RealmId.Stonehold,
                        ClassFamily.Warrior,
                        confirmIdentity: true,
                        username: Username));
                Assert.That(identity.Accepted, Is.True, identity.Message);
                byte[] before = ReadPrimary(root);

                MvpLoopCommitResult rewriteUsername = MvpLoopSaveAuthority.TryCommit(
                    save,
                    IdentityRequest(
                        RealmId.Stonehold,
                        ClassFamily.Warrior,
                        confirmIdentity: true,
                        username: OtherUsername));
                AssertRejectedWithoutDiskChange(rewriteUsername, root, before);
                MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(save.CurrentSave);
                Assert.That(snapshot.ClassFamily, Is.EqualTo(ClassFamily.Warrior));
                Assert.That(snapshot.Username, Is.EqualTo(Username));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SchemaTwoFirstWorldRejectsSnapshotFromAnotherSameRealmProfile()
        {
            string rootA = CreateRoot();
            string rootB = CreateRoot();
            try
            {
                LocalSaveGameService saveA = CreateBoundRealm(rootA, RealmId.Crownlands);
                LocalSaveGameService saveB = CreateBoundRealm(rootB, RealmId.Crownlands);
                FirstWorldProgressSnapshot otherSnapshot = ReadFirstWorld(saveB);
                Assert.That(
                    otherSnapshot.ProfileId,
                    Is.Not.EqualTo(saveA.CurrentSave.ProfileId));
                byte[] before = ReadPrimary(rootA);

                FirstWorldProgressCommitResult rejected =
                    FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                        saveA,
                        otherSnapshot,
                        FirstWorldTutorialProgressCommand.CameraLookAccepted);

                Assert.That(rejected.Accepted, Is.False, rejected.Message);
                Assert.That(rejected.Persisted, Is.False, rejected.Message);
                Assert.That(
                    rejected.Message,
                    Is.EqualTo("AL-FIRST-SESSION-PROFILE-READ-ONLY"));
                CollectionAssert.AreEqual(before, ReadPrimary(rootA));
                Assert.That(ReadFirstWorld(saveA).Revision, Is.Zero);
            }
            finally
            {
                DeleteRoot(rootA);
                DeleteRoot(rootB);
            }
        }

        [Test]
        public void SchemaTwoFirstWorldRejectsInvalidOrderAndStaleProgressWithoutDiskChange()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateBoundRealm(root, RealmId.Eldergrove);
                FirstWorldProgressSnapshot initial = ReadFirstWorld(save);
                byte[] before = ReadPrimary(root);

                FirstWorldProgressCommitResult invalidOrder =
                    FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                        save,
                        initial,
                        FirstWorldTutorialProgressCommand.MovementAccepted);
                Assert.That(invalidOrder.Accepted, Is.False, invalidOrder.Message);
                Assert.That(invalidOrder.Persisted, Is.False, invalidOrder.Message);
                Assert.That(
                    invalidOrder.Message,
                    Is.EqualTo("AL-FIRST-WORLD-TUTORIAL-ORDER-CONFLICT"));
                CollectionAssert.AreEqual(before, ReadPrimary(root));

                FirstWorldProgressCommitResult first =
                    FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                        save,
                        initial,
                        FirstWorldTutorialProgressCommand.CameraLookAccepted);
                Assert.That(first.Accepted, Is.True, first.Message);
                Assert.That(first.Persisted, Is.True, first.Message);
                byte[] afterFirst = ReadPrimary(root);

                FirstWorldProgressCommitResult stale =
                    FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                        save,
                        initial,
                        FirstWorldTutorialProgressCommand.MovementAccepted);
                Assert.That(stale.Accepted, Is.False, stale.Message);
                Assert.That(stale.Persisted, Is.False, stale.Message);
                Assert.That(
                    stale.Message,
                    Is.EqualTo("AL-FIRST-WORLD-TUTORIAL-ORDER-CONFLICT"));
                CollectionAssert.AreEqual(afterFirst, ReadPrimary(root));
                Assert.That(ReadFirstWorld(save).Revision, Is.EqualTo(1));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SchemaTwoFirstWorldWriteFailureDoesNotPublishProgress()
        {
            string root = CreateRoot();
            try
            {
                var gated = new GatedSaveFileOperations();
                LocalSaveGameService save = CreateBoundRealm(root, RealmId.Crownlands, gated);
                FirstWorldProgressSnapshot initial = ReadFirstWorld(save);
                byte[] before = ReadPrimary(root);

                gated.FailDurableWrites = true;
                LogAssert.Expect(
                    LogType.Error,
                    "AL-SAVE-TEMP-WRITE-FAILED: The temporary save could not be written durably. AL-TEST-WRITE-FAILED");
                FirstWorldProgressCommitResult failed =
                    FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                        save,
                        initial,
                        FirstWorldTutorialProgressCommand.CameraLookAccepted);

                Assert.That(failed.Accepted, Is.False, failed.Message);
                Assert.That(failed.Persisted, Is.False, failed.Message);
                CollectionAssert.AreEqual(before, ReadPrimary(root));
                FirstWorldProgressSnapshot current = ReadFirstWorld(save);
                Assert.That(current.Revision, Is.Zero);
                Assert.That(
                    current.Tutorial.TeachingBeat,
                    Is.EqualTo(FirstWorldEntryTeachingBeat.CameraLook));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SchemaTwoLordshipPersistsOnlyAtAcceptMarkState()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateBoundRealm(root, RealmId.Crownlands);
                MvpLoopCommitResult identity = MvpLoopSaveAuthority.TryCommit(
                    save,
                    IdentityRequest(
                        RealmId.Crownlands,
                        ClassFamily.Warrior,
                        confirmIdentity: true,
                        username: Username));
                Assert.That(identity.Accepted, Is.True, identity.Message);

                FirstWorldProgressSnapshot snapshot = CompleteTutorial(save);
                byte[] beforeProof = ReadPrimary(root);
                MvpLoopCommitResult tooEarly =
                    ProofOfWorthLordship.TryPersist(save, RealmId.Crownlands);
                AssertRejectedWithoutDiskChange(tooEarly, root, beforeProof);
                Assert.That(ProofOfWorthLordship.IsGranted(save.CurrentSave), Is.False);

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
                            save,
                            snapshot,
                            command);
                    Assert.That(step.Accepted, Is.True, command + ": " + step.Message);
                    Assert.That(step.Persisted, Is.True, command + ": " + step.Message);
                    snapshot = step.Snapshot;
                }

                Assert.That(snapshot.Proof.Phase, Is.EqualTo(ProofOfWorthPhase.C1AcceptMark));
                MvpLoopCommitResult mark =
                    ProofOfWorthLordship.TryPersist(save, RealmId.Crownlands);
                Assert.That(mark.Accepted, Is.True, mark.Message);
                Assert.That(mark.Persisted, Is.True, mark.Message);
                Assert.That(ProofOfWorthLordship.IsGranted(save.CurrentSave), Is.True);
                byte[] afterMark = ReadPrimary(root);

                MvpLoopCommitResult replayAfterMark =
                    MvpLoopSaveAuthority.TryCommit(
                        save,
                        IdentityRequest(
                            RealmId.Crownlands,
                            ClassFamily.Warrior,
                            confirmIdentity: true,
                            username: Username));
                Assert.That(replayAfterMark.Accepted, Is.True, replayAfterMark.Message);
                Assert.That(replayAfterMark.Persisted, Is.False, replayAfterMark.Message);
                CollectionAssert.AreEqual(afterMark, ReadPrimary(root));

                FirstWorldProgressCommitResult final =
                    FirstWorldProgressSaveAuthority.TryAdvanceProof(
                        save,
                        snapshot,
                        ProofOfWorthCommand.AcceptMark);
                Assert.That(final.Accepted, Is.True, final.Message);
                Assert.That(final.Persisted, Is.True, final.Message);
                Assert.That(final.Snapshot.Proof.Phase, Is.EqualTo(ProofOfWorthPhase.LordshipGranted));

                LocalSaveGameService reloaded = Reload(root);
                Assert.That(ProofOfWorthLordship.IsGranted(reloaded.CurrentSave), Is.True);
                Assert.That(
                    ReadFirstWorld(reloaded).Proof.Phase,
                    Is.EqualTo(ProofOfWorthPhase.LordshipGranted));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static FirstWorldProgressSnapshot CompleteTutorial(
            LocalSaveGameService save)
        {
            FirstWorldProgressSnapshot snapshot = ReadFirstWorld(save);
            snapshot = CommitTutorial(
                save,
                snapshot,
                FirstWorldTutorialProgressCommand.CameraLookAccepted).Snapshot;
            snapshot = CommitTutorial(
                save,
                snapshot,
                FirstWorldTutorialProgressCommand.MovementAccepted).Snapshot;
            snapshot = CommitTutorial(
                save,
                snapshot,
                FirstWorldTutorialProgressCommand.GuideInteractionAccepted).Snapshot;
            snapshot = CommitTutorial(
                save,
                snapshot,
                FirstWorldTutorialProgressCommand.BasicAttackAccepted).Snapshot;
            Assert.That(snapshot.IsTutorialComplete, Is.True);
            Assert.That(snapshot.HandoffCommitted, Is.True);
            Assert.That(snapshot.Proof.Phase, Is.EqualTo(ProofOfWorthPhase.OmenOffered));
            return snapshot;
        }

        private static FirstWorldProgressCommitResult CommitTutorial(
            LocalSaveGameService save,
            FirstWorldProgressSnapshot snapshot,
            FirstWorldTutorialProgressCommand command)
        {
            FirstWorldProgressCommitResult result =
                FirstWorldProgressSaveAuthority.TryAdvanceTutorial(save, snapshot, command);
            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.Persisted, Is.True, result.Message);
            return result;
        }

        private static FirstWorldProgressSnapshot ReadFirstWorld(
            LocalSaveGameService save)
        {
            Assert.That(
                FirstWorldProgressSaveAuthority.TryRead(
                    save,
                    out FirstWorldProgressSnapshot snapshot,
                    out string message),
                Is.True,
                message);
            return snapshot;
        }

        private static MvpLoopCommitRequest IdentityRequest(
            RealmId realm,
            ClassFamily classFamily,
            bool confirmIdentity,
            string username) =>
            new MvpLoopCommitRequest(
                NewTransactionId(),
                realm,
                classFamily,
                confirmIdentity,
                string.Empty,
                string.Empty,
                0,
                username);

        private static void AssertRejectedWithoutDiskChange(
            MvpLoopCommitResult result,
            string root,
            byte[] before)
        {
            Assert.That(result.Accepted, Is.False, result.Message);
            Assert.That(result.Persisted, Is.False, result.Message);
            CollectionAssert.AreEqual(before, ReadPrimary(root));
        }

        private static LocalSaveGameService CreateBoundRealm(
            string root,
            RealmId realm,
            ISaveFileOperations fileOperations = null)
        {
            LocalSaveGameService save = CreateService(root, fileOperations);
            save.Load();
            Assert.That(save.CurrentSave, Is.Not.Null, save.LastLoadMessage);
            Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.None));
            Assert.That(
                save.GetCurrentAuthority().Status,
                Is.EqualTo(ProfileWriteAuthorityStatus.Writable));

            save.CreateNewSave(realm);
            Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(realm));
            Assert.That(save.CurrentSave.SaveSchemaVersion, Is.EqualTo(2));
            Assert.That(save.CurrentSave.ProfileInitializationVersion, Is.EqualTo(1));
            Assert.That(save.CurrentSave.ProfileId, Does.StartWith("alp_"));
            Assert.That(save.GetCurrentAuthority().Status, Is.EqualTo(ProfileWriteAuthorityStatus.Writable));
            return save;
        }

        private static LocalSaveGameService Reload(string root)
        {
            LocalSaveGameService save = CreateService(root);
            save.Load();
            Assert.That(save.CurrentSave, Is.Not.Null, save.LastLoadMessage);
            return save;
        }

        private static LocalSaveGameService CreateService(
            string root,
            ISaveFileOperations fileOperations = null)
        {
            Type[] signature = fileOperations == null
                ? new[] { typeof(string) }
                : new[] { typeof(string), typeof(ISaveFileOperations) };
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                signature,
                null);
            Assert.That(constructor, Is.Not.Null);
            object[] args = fileOperations == null
                ? new object[] { root }
                : new object[] { root, fileOperations };
            return (LocalSaveGameService)constructor.Invoke(args);
        }

        private static byte[] ReadPrimary(string root) =>
            File.ReadAllBytes(Path.Combine(root, PrimarySaveName));

        private static string NewTransactionId() => Guid.NewGuid().ToString("N");

        private static string CreateRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-ProfileBoundFirstSession",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private sealed class GatedSaveFileOperations : ISaveFileOperations
        {
            private readonly SystemSaveFileOperations _inner = new SystemSaveFileOperations();

            public bool FailDurableWrites { get; set; }

            public bool FileExists(string path) => _inner.FileExists(path);
            public void CreateDirectory(string path) => _inner.CreateDirectory(path);

            public SaveFileReadResult ReadAllBytesBounded(string path, int maximumBytes) =>
                _inner.ReadAllBytesBounded(path, maximumBytes);

            public SaveFileWriteResult WriteAllTextDurable(string path, string contents)
            {
                return FailDurableWrites
                    ? new SaveFileWriteResult(false, false, "AL-TEST-WRITE-FAILED")
                    : _inner.WriteAllTextDurable(path, contents);
            }

            public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
                _inner.Copy(sourcePath, destinationPath, overwrite);

            public void Move(string sourcePath, string destinationPath) =>
                _inner.Move(sourcePath, destinationPath);

            public void Replace(string sourcePath, string destinationPath, string backupPath) =>
                _inner.Replace(sourcePath, destinationPath, backupPath);

            public void Delete(string path) => _inner.Delete(path);

            public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern) =>
                _inner.EnumerateFiles(directoryPath, searchPattern);

            public DateTime GetCreationTimeUtc(string path) =>
                _inner.GetCreationTimeUtc(path);

            public bool IsReparsePoint(string path) => _inner.IsReparsePoint(path);
        }
    }
}
