using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AL.Core;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public sealed class RealmDurabilityPlayerAcceptanceTests
    {
        private static readonly string[] SaveArtifacts =
        {
            "save.json",
            "save.backup.json",
            "save.tmp.json",
            "save.previous.json",
            "save.lock"
        };

        [UnityTest]
        [Category("RealmDurabilityAcceptance")]
        public IEnumerator BuiltPlayerConvergesToOneDurableRealmAcrossReloadAndReplay()
        {
            string acceptanceRoot = Path.Combine(
                Application.persistentDataPath,
                "realm-durability-acceptance");
            DeleteAcceptanceArtifacts(acceptanceRoot);
            try
            {
                yield return null;

                LocalSaveGameService bootstrap = CreateService(acceptanceRoot);
                bootstrap.Load();
                Assert.NotNull(bootstrap.CurrentSave);
                Assert.AreEqual(RealmId.None, bootstrap.CurrentSave.SelectedRealm);

                // A newly installed generation becomes authoritative only after the
                // next launch observes it through the same disk validation path.
                LocalSaveGameService firstLaunch = CreateService(acceptanceRoot);
                firstLaunch.Load();
                Assert.NotNull(firstLaunch.CurrentSave);
                Assert.AreEqual(ProfileWriteAuthorityStatus.Writable,
                    firstLaunch.GetCurrentAuthority().Status);
                Assert.AreEqual(RealmId.None, firstLaunch.CurrentSave.SelectedRealm);

                IProfileBoundRealmSelectionStore firstStore = firstLaunch;
                var committedEvents = new List<RealmSelectionCommittedEvent>();
                firstStore.RealmSelectionCommitted += committedEvents.Add;
                var command = new RealmSelectionCommand(
                    "rsel_eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
                    RealmId.Stonehold,
                    "stonehold",
                    "al_realm_catalog",
                    RealmCatalogRuntime.SupportedVersion,
                    ProfileAuthorityExpectation.From(firstLaunch.GetCurrentAuthority()));

                RealmSelectionCommitResult committed =
                    firstStore.TryCommitRealmSelection(command);
                Assert.AreEqual(RealmSelectionCommitStatus.Committed, committed.Status);
                Assert.True(committed.PersistedAndVerified);
                Assert.AreEqual(1, committedEvents.Count);
                Assert.AreEqual(committed.CommittedEventId, committedEvents[0].EventId);
                AssertPrimaryReceipt(acceptanceRoot, committed);

                yield return null;

                LocalSaveGameService secondLaunch = CreateService(acceptanceRoot);
                secondLaunch.Load();
                IProfileBoundRealmSelectionStore secondStore = secondLaunch;
                int replayEvents = 0;
                secondStore.RealmSelectionCommitted += _ => replayEvents++;
                RealmSelectionCommitResult replay = secondStore.TryCommitRealmSelection(
                    new RealmSelectionCommand(
                        committed.CommittedTransactionId,
                        RealmId.Stonehold,
                        "stonehold",
                        "al_realm_catalog",
                        RealmCatalogRuntime.SupportedVersion,
                        ProfileAuthorityExpectation.From(secondLaunch.GetCurrentAuthority())));

                Assert.AreEqual(RealmSelectionCommitStatus.DuplicateTransaction, replay.Status);
                Assert.AreEqual(committed.ProfileId, replay.ProfileId);
                Assert.AreEqual(committed.CommittedEventId, replay.CommittedEventId);
                Assert.AreEqual(0, replayEvents);
                AssertPrimaryReceipt(acceptanceRoot, committed);

                RealmSelectionCommitResult conflicting = secondStore.TryCommitRealmSelection(
                    new RealmSelectionCommand(
                        "rsel_ffffffffffffffffffffffffffffffff",
                        RealmId.Umbral,
                        "umbral",
                        "al_realm_catalog",
                        RealmCatalogRuntime.SupportedVersion,
                        ProfileAuthorityExpectation.From(secondLaunch.GetCurrentAuthority())));
                Assert.AreEqual(RealmSelectionCommitStatus.RejectedDifferentRealm,
                    conflicting.Status);
                Assert.False(conflicting.MutationOccurred);
                AssertPrimaryReceipt(acceptanceRoot, committed);

                Debug.Log("AL-REALM-DURABILITY-PLAYER-ACCEPTANCE-PASSED");
                if (!Application.isEditor)
                {
                    var quitObject = new GameObject("RealmDurabilityAcceptanceQuit");
                    quitObject.AddComponent<QuitAfterTestCompletion>();
                }
            }
            finally
            {
                DeleteAcceptanceArtifacts(acceptanceRoot);
            }
        }

        private sealed class QuitAfterTestCompletion : MonoBehaviour
        {
            private IEnumerator Start()
            {
                yield return null;
                yield return null;
                Application.Quit(0);
            }
        }

        private static LocalSaveGameService CreateService(string acceptanceRoot)
        {
            return (LocalSaveGameService)Activator.CreateInstance(
                typeof(LocalSaveGameService),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { acceptanceRoot },
                null);
        }

        private static void AssertPrimaryReceipt(
            string acceptanceRoot,
            RealmSelectionCommitResult expected)
        {
            string primaryPath = Path.Combine(acceptanceRoot, "save.json");
            Assert.True(File.Exists(primaryPath), primaryPath);
            SaveGameData persisted = JsonUtility.FromJson<SaveGameData>(
                File.ReadAllText(primaryPath));
            Assert.NotNull(persisted);
            Assert.AreEqual(RealmId.Stonehold, persisted.SelectedRealm);
            Assert.AreEqual(expected.ProfileId, persisted.ProfileId);
            Assert.AreEqual(expected.CommittedTransactionId,
                persisted.RealmSelectionCommit.TransactionId);
            Assert.AreEqual(expected.CommittedEventId,
                persisted.RealmSelectionCommit.CommittedEventId);
            Assert.False(File.Exists(Path.Combine(
                acceptanceRoot, "save.tmp.json")));
            Assert.False(File.Exists(Path.Combine(
                acceptanceRoot, "save.previous.json")));
        }

        private static void DeleteAcceptanceArtifacts(string acceptanceRoot)
        {
            Directory.CreateDirectory(acceptanceRoot);
            foreach (string name in SaveArtifacts)
            {
                string path = Path.Combine(acceptanceRoot, name);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            if (Directory.GetFileSystemEntries(acceptanceRoot).Length == 0)
            {
                Directory.Delete(acceptanceRoot);
            }
        }
    }
}
