using System;
using System.IO;
using System.Reflection;
using AL.ChampionMode.Quests;
using AL.ChampionMode.Tutorial;
using AL.Core;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    internal static class ProfileBoundKingdomTestFixture
    {
        internal static LocalSaveGameService CreateLordshipSave(
            string root,
            RealmId realm,
            ClassFamily classFamily)
        {
            LocalSaveGameService save = CreateIdentitySave(
                root,
                realm,
                classFamily);

            Assert.That(
                FirstWorldProgressSaveAuthority.TryRead(
                    save,
                    out FirstWorldProgressSnapshot progress,
                    out string readMessage),
                Is.True,
                readMessage);
            foreach (FirstWorldTutorialProgressCommand command in new[]
                     {
                         FirstWorldTutorialProgressCommand.CameraLookAccepted,
                         FirstWorldTutorialProgressCommand.MovementAccepted,
                         FirstWorldTutorialProgressCommand.GuideInteractionAccepted,
                         FirstWorldTutorialProgressCommand.BasicAttackAccepted
                     })
            {
                FirstWorldProgressCommitResult tutorial =
                    FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                        save,
                        progress,
                        command);
                Assert.That(tutorial.Accepted, Is.True, command + ": " + tutorial.Message);
                Assert.That(tutorial.Persisted, Is.True, command + ": " + tutorial.Message);
                progress = tutorial.Snapshot;
            }

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
                FirstWorldProgressCommitResult proof =
                    FirstWorldProgressSaveAuthority.TryAdvanceProof(
                        save,
                        progress,
                        command);
                Assert.That(proof.Accepted, Is.True, command + ": " + proof.Message);
                Assert.That(proof.Persisted, Is.True, command + ": " + proof.Message);
                progress = proof.Snapshot;
            }

            MvpLoopCommitResult mark = ProofOfWorthLordship.TryPersist(save, realm);
            Assert.That(mark.Accepted, Is.True, mark.Message);
            Assert.That(mark.Persisted, Is.True, mark.Message);

            FirstWorldProgressCommitResult acceptedMark =
                FirstWorldProgressSaveAuthority.TryAdvanceProof(
                    save,
                    progress,
                    ProofOfWorthCommand.AcceptMark);
            Assert.That(acceptedMark.Accepted, Is.True, acceptedMark.Message);
            Assert.That(acceptedMark.Persisted, Is.True, acceptedMark.Message);
            Assert.That(
                acceptedMark.Snapshot.Proof.Phase,
                Is.EqualTo(ProofOfWorthPhase.LordshipGranted));
            Assert.That(ProofOfWorthLordship.IsGranted(save.CurrentSave), Is.True);
            return save;
        }

        internal static LocalSaveGameService CreateIdentitySave(
            string root,
            RealmId realm,
            ClassFamily classFamily)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(constructor, Is.Not.Null);
            var save = (LocalSaveGameService)constructor.Invoke(new object[] { root });
            save.Load();
            Assert.That(
                save.GetCurrentAuthority().Status,
                Is.EqualTo(ProfileWriteAuthorityStatus.Writable));
            save.CreateNewSave(realm);

            MvpLoopCommitResult identity = MvpLoopSaveAuthority.TryCommit(
                save,
                new MvpLoopCommitRequest(
                    Guid.NewGuid().ToString("N"),
                    realm,
                    classFamily,
                    true,
                    string.Empty,
                    string.Empty,
                    0,
                    "KingdomTester"));
            Assert.That(identity.Accepted, Is.True, identity.Message);
            Assert.That(identity.Persisted, Is.True, identity.Message);
            return save;
        }

        internal static string CreateRoot(string name)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                name,
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
