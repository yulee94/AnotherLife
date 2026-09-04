using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public sealed class OfflineKingdomProductionCatchUpTests
    {
        private const string EligibleCatalogJson =
            "{\"schemaVersion\":1,\"catalogId\":\"kingdom_production_profile_v1\",\"productionEligible\":true,\"sourceRevision\":\"test-source-v1\",\"authorityLedgerId\":\"al_six_family_production_authority_v1\",\"maxOfflineElapsedSeconds\":3600,\"contributions\":[{\"id\":\"hall_food\",\"resourceId\":\"food\",\"buildingId\":\"town_hall\",\"minBuildingLevel\":1,\"ratePerLevelPerSecond\":2.0,\"capPerTick\":1.5,\"realmIds\":[\"stonehold\"]},{\"id\":\"hall_gold\",\"resourceId\":\"gold\",\"buildingId\":\"TownHall\",\"minBuildingLevel\":1,\"ratePerLevelPerSecond\":1.0,\"capPerTick\":0.0,\"realmIds\":[\"stonehold\"]}]}";

        [Test]
        public void EligibleWritableCatchUpAppliesOnceAndSetsOfflineProgressApplied()
        {
            string root = CreateRoot();
            int events = 0;
            Action<OfflineKingdomProductionCatchUpResult> handler = _ => events++;
            OfflineKingdomProductionCatchUp.Committed += handler;
            try
            {
                LocalSaveGameService service = CreateService(root);
                SeedWritableCatchUpSave(service, out long lastVerified, out long foodBefore, out long goldBefore);
                KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();

                OfflineKingdomProductionCatchUpResult result =
                    OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                        service,
                        catalog,
                        () => lastVerified + 10);

                Assert.AreEqual(
                    OfflineKingdomProductionCatchUpStatus.Applied,
                    result.Status,
                    result.DiagnosticCode);
                Assert.True(result.Applied);
                Assert.True(result.EventPublished);
                Assert.AreEqual(1, events);
                Assert.AreEqual(foodBefore + 15, Amount(service, ResourceType.Food));
                Assert.AreEqual(goldBefore + 10, Amount(service, ResourceType.Gold));
                Assert.True(service.LastLoadDisposition.OfflineProgressApplied);
                Assert.NotNull(service.CurrentSave.OfflineProductionCatchUp);
                Assert.AreEqual(10, service.CurrentSave.OfflineProductionCatchUp.CappedElapsedSeconds);
                Assert.GreaterOrEqual(service.CurrentSave.LastSavedTimestamp, lastVerified);
            }
            finally
            {
                OfflineKingdomProductionCatchUp.Committed -= handler;
                DeleteRoot(root);
            }
        }

        [Test]
        public void ExactReplayReturnsStoredReceiptWithoutSecondDeltaOrEvent()
        {
            string root = CreateRoot();
            int events = 0;
            Action<OfflineKingdomProductionCatchUpResult> handler = _ => events++;
            OfflineKingdomProductionCatchUp.Committed += handler;
            try
            {
                LocalSaveGameService service = CreateService(root);
                SeedWritableCatchUpSave(service, out long lastVerified, out long foodBefore, out _);
                KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
                OfflineKingdomProductionCatchUpResult first =
                    OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                        service,
                        catalog,
                        () => lastVerified + 10);
                long foodAfter = Amount(service, ResourceType.Food);
                service.CurrentSave.LastSavedTimestamp = lastVerified;

                OfflineKingdomProductionCatchUpResult replay =
                    OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                        service,
                        catalog,
                        () => lastVerified + 10);

                Assert.AreEqual(OfflineKingdomProductionCatchUpStatus.Applied, first.Status);
                Assert.AreEqual(OfflineKingdomProductionCatchUpStatus.Replayed, replay.Status);
                Assert.False(replay.EventPublished);
                Assert.AreEqual(1, events);
                Assert.AreEqual(foodAfter, Amount(service, ResourceType.Food));
                Assert.AreEqual(foodBefore + 15, foodAfter);
                Assert.AreEqual(first.Receipt.ReceiptId, replay.Receipt.ReceiptId);
            }
            finally
            {
                OfflineKingdomProductionCatchUp.Committed -= handler;
                DeleteRoot(root);
            }
        }

        [Test]
        public void RestartReloadDoesNotReapplyWhenElapsedIsZero()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService service = CreateService(root);
                SeedWritableCatchUpSave(service, out long lastVerified, out _, out _);
                KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
                OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                    service,
                    catalog,
                    () => lastVerified + 10);
                long food = Amount(service, ResourceType.Food);
                string receiptId = service.CurrentSave.OfflineProductionCatchUp.ReceiptId;
                long committedTimestamp = service.CurrentSave.LastSavedTimestamp;

                LocalSaveGameService restarted = CreateService(root);
                restarted.Load();
                OfflineKingdomProductionCatchUpResult second =
                    OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                        restarted,
                        catalog,
                        () => committedTimestamp);

                Assert.AreEqual(OfflineKingdomProductionCatchUpStatus.NotApplied, second.Status);
                Assert.False(restarted.LastLoadDisposition.OfflineProgressApplied);
                Assert.AreEqual(food, Amount(restarted, ResourceType.Food));
                Assert.AreEqual(receiptId, restarted.CurrentSave.OfflineProductionCatchUp.ReceiptId);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ClockRollbackDoesNotMutate()
        {
            AssertNotApplied(
                (service, catalog, lastVerified) =>
                    OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                        service,
                        catalog,
                        () => lastVerified - 5),
                EconomyDiagnosticCodes.ProductionElapsed);
        }

        [Test]
        public void FutureLastVerifiedTimestampDoesNotMutate()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService service = CreateService(root);
                SeedWritableCatchUpSave(service, out long lastVerified, out long foodBefore, out _);
                KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();

                OfflineKingdomProductionCatchUpResult result =
                    OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                        service,
                        catalog,
                        () => lastVerified);

                Assert.AreEqual(OfflineKingdomProductionCatchUpStatus.NotApplied, result.Status);
                Assert.AreEqual(EconomyDiagnosticCodes.ProductionElapsed, result.DiagnosticCode);
                Assert.AreEqual(foodBefore, Amount(service, ResourceType.Food));
                Assert.False(service.LastLoadDisposition.OfflineProgressApplied);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void CatalogCapLimitsElapsedProduction()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService service = CreateService(root);
                SeedWritableCatchUpSave(service, out long lastVerified, out long foodBefore, out long goldBefore);
                KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();

                OfflineKingdomProductionCatchUpResult result =
                    OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                        service,
                        catalog,
                        () => lastVerified + 10000);

                Assert.AreEqual(
                    OfflineKingdomProductionCatchUpStatus.Applied,
                    result.Status,
                    result.DiagnosticCode);
                Assert.AreEqual(3600, result.Receipt.CappedElapsedSeconds);
                Assert.AreEqual(foodBefore + 5400, Amount(service, ResourceType.Food));
                Assert.AreEqual(goldBefore + 3600, Amount(service, ResourceType.Gold));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void OverflowDoesNotMutateOrApply()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService service = CreateService(root);
                SeedWritableCatchUpSave(service, out long lastVerified, out _, out _);
                PersistCandidate(service, candidate =>
                {
                    FindResource(candidate, ResourceType.Food).Amount = long.MaxValue;
                    return SaveCandidateMutationPreparation.Prepared();
                });
                service.Load();
                lastVerified = service.CurrentSave.LastSavedTimestamp;
                long goldBefore = Amount(service, ResourceType.Gold);
                KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();

                OfflineKingdomProductionCatchUpResult result =
                    OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                        service,
                        catalog,
                        () => lastVerified + 10);

                Assert.AreEqual(OfflineKingdomProductionCatchUpStatus.NotApplied, result.Status);
                Assert.AreEqual(EconomyDiagnosticCodes.Overflow, result.DiagnosticCode);
                Assert.AreEqual(long.MaxValue, Amount(service, ResourceType.Food));
                Assert.AreEqual(goldBefore, Amount(service, ResourceType.Gold));
                Assert.False(service.LastLoadDisposition.OfflineProgressApplied);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void IneligibleLiveLedgerDoesNotApply()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService service = CreateService(root);
                SeedWritableCatchUpSave(service, out long lastVerified, out long foodBefore, out _);

                OfflineKingdomProductionCatchUpResult result =
                    OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                        service,
                        null,
                        () => lastVerified + 10);

                Assert.AreEqual(OfflineKingdomProductionCatchUpStatus.NotApplied, result.Status);
                Assert.AreEqual(foodBefore, Amount(service, ResourceType.Food));
                Assert.False(service.LastLoadDisposition.OfflineProgressApplied);
                Assert.IsTrue(
                    service.CurrentSave.OfflineProductionCatchUp == null ||
                    service.CurrentSave.OfflineProductionCatchUp.Version == 0 ||
                    string.IsNullOrEmpty(service.CurrentSave.OfflineProductionCatchUp.OperationId));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ForwardReadOnlySaveDoesNotApply()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService writer = CreateService(root);
                SeedWritableCatchUpSave(writer, out _, out long foodBefore, out _);
                writer.CurrentSave.SaveSchemaVersion = 3;
                File.WriteAllText(
                    Path.Combine(root, "save.json"),
                    UnityEngine.JsonUtility.ToJson(writer.CurrentSave, true));
                File.Copy(
                    Path.Combine(root, "save.json"),
                    Path.Combine(root, "save.backup.json"),
                    true);

                LocalSaveGameService service = CreateService(root);
                service.Load();
                KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
                OfflineKingdomProductionCatchUpResult result =
                    OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                        service,
                        catalog,
                        () => DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                Assert.AreEqual(OfflineKingdomProductionCatchUpStatus.NotApplied, result.Status);
                Assert.AreNotEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    service.GetCurrentAuthority().Status);
                if (service.CurrentSave != null)
                {
                    Assert.AreEqual(foodBefore, Amount(service, ResourceType.Food));
                }
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void WriteFailurePreservesWalletAndDoesNotApply()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService writer = CreateService(root);
                SeedWritableCatchUpSave(writer, out long lastVerified, out long foodBefore, out _);
                var failing = new FailingWriteFileOperations();
                LocalSaveGameService service = CreateService(root, failing);
                service.Load();
                KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
                LogAssert.Expect(
                    UnityEngine.LogType.Error,
                    new System.Text.RegularExpressions.Regex("AL-SAVE-TEMP-WRITE-FAILED"));

                OfflineKingdomProductionCatchUpResult result =
                    OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                        service,
                        catalog,
                        () => lastVerified + 10);

                Assert.AreNotEqual(OfflineKingdomProductionCatchUpStatus.Applied, result.Status);
                Assert.False(result.EventPublished);
                Assert.AreEqual(foodBefore, Amount(service, ResourceType.Food));
                Assert.False(service.LastLoadDisposition.OfflineProgressApplied);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void PlayerPersistenceRoundTripKeepsReceiptAndWallet()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService service = CreateService(root);
                SeedWritableCatchUpSave(service, out long lastVerified, out long foodBefore, out _);
                KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
                OfflineKingdomProductionCatchUp.TryApplyAfterLoad(
                    service,
                    catalog,
                    () => lastVerified + 10);

                LocalSaveGameService reloaded = CreateService(root);
                reloaded.Load();
                Assert.AreEqual(foodBefore + 15, Amount(reloaded, ResourceType.Food));
                Assert.NotNull(reloaded.CurrentSave.OfflineProductionCatchUp);
                Assert.AreEqual(
                    service.CurrentSave.OfflineProductionCatchUp.OperationId,
                    reloaded.CurrentSave.OfflineProductionCatchUp.OperationId);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    reloaded.GetCurrentAuthority().Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static void AssertNotApplied(
            Func<LocalSaveGameService, KingdomProductionProfileSnapshot, long, OfflineKingdomProductionCatchUpResult> apply,
            string expectedCode)
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService service = CreateService(root);
                SeedWritableCatchUpSave(service, out long lastVerified, out long foodBefore, out _);
                KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
                OfflineKingdomProductionCatchUpResult result = apply(service, catalog, lastVerified);
                Assert.AreEqual(OfflineKingdomProductionCatchUpStatus.NotApplied, result.Status);
                Assert.AreEqual(expectedCode, result.DiagnosticCode);
                Assert.AreEqual(foodBefore, Amount(service, ResourceType.Food));
                Assert.False(service.LastLoadDisposition.OfflineProgressApplied);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static void SeedWritableCatchUpSave(
            LocalSaveGameService service,
            out long lastVerified,
            out long foodBefore,
            out long goldBefore)
        {
            service.CreateNewSave(RealmId.Stonehold);
            Assert.NotNull(
                service.CurrentSave,
                service.LastSaveStatus + " " + service.LastSaveMessage + " / " +
                service.LastLoadStatus + " " + service.LastLoadMessage);
            lastVerified = 1_700_000_000L;
            long seededTimestamp = lastVerified;
            ProfileWriteAuthoritySnapshot authority = service.GetCurrentAuthority();
            Assert.AreEqual(ProfileWriteAuthorityStatus.Writable, authority.Status);
            ProfileBoundSaveCandidateCommitResult seeded =
                ((IProfileBoundSaveGameCandidateStore)service).TryCommitCandidate(
                    ProfileAuthorityExpectation.From(authority),
                    "al.offline.catchup.test-seed",
                    "seed",
                    candidate =>
                    {
                        PrepareCatchUpBuildings(candidate, seededTimestamp);
                        return SaveCandidateMutationPreparation.Prepared();
                    });
            Assert.True(
                seeded.CommitResult.IsCommitted,
                seeded.CommitResult.Message);
            service.Load();
            Assert.NotNull(service.CurrentSave);
            Assert.NotNull(service.LastLoadDisposition);
            Assert.AreEqual(RealmId.Stonehold, service.CurrentSave.SelectedRealm);
            Assert.AreEqual(1, service.CurrentSave.Buildings.Count);
            Assert.AreEqual("TownHall", service.CurrentSave.Buildings[0].BuildingId);
            lastVerified = service.CurrentSave.LastSavedTimestamp;
            Assert.Greater(lastVerified, 0L);
            foodBefore = Amount(service, ResourceType.Food);
            goldBefore = Amount(service, ResourceType.Gold);
        }

        private static void PersistCandidate(
            LocalSaveGameService service,
            Func<SaveGameData, SaveCandidateMutationPreparation> prepare)
        {
            ProfileWriteAuthoritySnapshot authority = service.GetCurrentAuthority();
            ProfileBoundSaveCandidateCommitResult result =
                ((IProfileBoundSaveGameCandidateStore)service).TryCommitCandidate(
                    ProfileAuthorityExpectation.From(authority),
                    "al.offline.catchup.test-mutate",
                    "mutate",
                    prepare);
            Assert.True(result.CommitResult.IsCommitted, result.CommitResult.Message);
        }

        private static void PrepareCatchUpBuildings(SaveGameData save, long lastVerified)
        {
            save.LastSavedTimestamp = lastVerified;
            save.SelectedRealm = RealmId.Stonehold;
            save.Buildings = new List<BuildingState>
            {
                new BuildingState { BuildingId = "TownHall", Level = 1 }
            };
        }

        private static KingdomProductionProfileSnapshot LoadEligibleCatalog()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(EligibleCatalogJson);
            KingdomProductionProfileLoadResult result =
                KingdomProductionProfileCatalog.TryLoadProfile(
                    bytes,
                    KingdomProductionProfileCatalog.ComputeSha256(bytes));
            Assert.True(result.IsReady, result.DiagnosticCode);
            return result.Snapshot;
        }

        private static long Amount(LocalSaveGameService service, ResourceType type)
        {
            return FindResource(service.CurrentSave, type).Amount;
        }

        private static ResourceData FindResource(SaveGameData save, ResourceType type)
        {
            return save.Resources.Single(item => item != null && item.Type == type);
        }

        private static LocalSaveGameService CreateService(
            string root,
            ISaveFileOperations fileOperations = null)
        {
            if (fileOperations == null)
            {
                ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);
                Assert.NotNull(constructor);
                return (LocalSaveGameService)constructor.Invoke(new object[] { root });
            }

            ConstructorInfo withOps = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(ISaveFileOperations) },
                null);
            Assert.NotNull(withOps);
            return (LocalSaveGameService)withOps.Invoke(new object[] { root, fileOperations });
        }

        private static string CreateRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-OfflineCatchUp",
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

        private sealed class FailingWriteFileOperations : ISaveFileOperations
        {
            private readonly ISaveFileOperations _inner = new SystemSaveFileOperations();

            public bool FileExists(string path) => _inner.FileExists(path);
            public void CreateDirectory(string path) => _inner.CreateDirectory(path);
            public SaveFileReadResult ReadAllBytesBounded(string path, int maximumBytes) =>
                _inner.ReadAllBytesBounded(path, maximumBytes);
            public SaveFileWriteResult WriteAllTextDurable(string path, string contents) =>
                new SaveFileWriteResult(false, false, "TEST_CATCHUP_WRITE_FAILED");
            public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
                _inner.Copy(sourcePath, destinationPath, overwrite);
            public void Move(string sourcePath, string destinationPath) =>
                _inner.Move(sourcePath, destinationPath);
            public void Replace(string sourcePath, string destinationPath, string backupPath) =>
                _inner.Replace(sourcePath, destinationPath, backupPath);
            public void Delete(string path) => _inner.Delete(path);
            public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern) =>
                _inner.EnumerateFiles(directoryPath, searchPattern);
            public DateTime GetCreationTimeUtc(string path) => _inner.GetCreationTimeUtc(path);
            public bool IsReparsePoint(string path) => _inner.IsReparsePoint(path);
        }
    }
}
