using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class KingdomProductionContributionProviderTests
    {
        private const string ProfileId = "alp_0123456789abcdef0123456789abcdef";
        private const string EligibleCatalogJson =
            "{\"schemaVersion\":1,\"catalogId\":\"kingdom_production_profile_v1\",\"productionEligible\":true,\"sourceRevision\":\"test-source-v1\",\"authorityLedgerId\":\"al_six_family_production_authority_v1\",\"maxOfflineElapsedSeconds\":3600,\"contributions\":[{\"id\":\"farm_food\",\"resourceId\":\"food\",\"buildingId\":\"farm\",\"minBuildingLevel\":1,\"ratePerLevelPerSecond\":2.0,\"capPerTick\":1.5,\"realmIds\":[\"stonehold\"]},{\"id\":\"gold_mine_gold\",\"resourceId\":\"gold\",\"buildingId\":\"gold_mine\",\"minBuildingLevel\":1,\"ratePerLevelPerSecond\":1.0,\"capPerTick\":0.0,\"realmIds\":[\"stonehold\"]}]}";

        [Test]
        public void LiveAuthorityLedgerIsBoundAndNotProductionEligible()
        {
            string path = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    KingdomProductionProfileCatalog.LiveLedgerRelativePath));
            Assert.True(File.Exists(path), path);

            KingdomProductionProfileLoadResult result =
                KingdomProductionProfileCatalog.TryBindAuthorityLedger(File.ReadAllBytes(path));

            Assert.True(result.IsReady);
            Assert.NotNull(result.Snapshot);
            Assert.False(result.Snapshot.ProductionEligible);
            Assert.AreEqual(
                KingdomProductionProfileCatalog.AuthorityLedgerId,
                result.Snapshot.AuthorityLedgerId);
            Assert.AreEqual(EconomyDiagnosticCodes.ProductionCatalog, result.DiagnosticCode);
        }

        [Test]
        public void LiveIneligibleLedgerCannotMintContributions()
        {
            string path = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    KingdomProductionProfileCatalog.LiveLedgerRelativePath));
            KingdomProductionProfileLoadResult ledger =
                KingdomProductionProfileCatalog.TryBindAuthorityLedger(File.ReadAllBytes(path));
            FakeSaveGameService save = CreateWritableSave();
            var provider = new KingdomProductionContributionProvider(save, ledger.Snapshot);

            EconomyProductionContributionSnapshot snapshot = provider.BuildContributions(1d);

            Assert.AreEqual(EconomyProductionSourceStatus.Unavailable, snapshot.Status);
            Assert.AreEqual(EconomyDiagnosticCodes.ProductionCatalog, snapshot.Diagnostics[0].Code);
            Assert.AreEqual(0, snapshot.Contributions.Count);
        }

        [Test]
        public void EligibleCatalogProducesBoundedRealmContributions()
        {
            KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
            FakeSaveGameService save = CreateWritableSave();
            var provider = new KingdomProductionContributionProvider(save, catalog);

            EconomyProductionContributionSnapshot snapshot = provider.BuildContributions(1d);

            Assert.AreEqual(EconomyProductionSourceStatus.Available, snapshot.Status);
            Assert.AreEqual(ProfileId, snapshot.ProfileIdentity);
            Assert.AreEqual(catalog.SourceSha256, snapshot.SourceRevision);
            Assert.AreEqual(2, snapshot.Contributions.Count);
            Assert.AreEqual(ResourceType.Food, snapshot.Contributions[0].ResourceType);
            Assert.AreEqual(1.5d, snapshot.Contributions[0].Amount, 1e-12);
            Assert.AreEqual(ResourceType.Gold, snapshot.Contributions[1].ResourceType);
            Assert.AreEqual(1d, snapshot.Contributions[1].Amount, 1e-12);
        }

        [Test]
        public void CapLimitsContributionBelowRawRate()
        {
            KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
            FakeSaveGameService save = CreateWritableSave();
            save.CurrentSave.Buildings[0].Level = 10;
            var provider = new KingdomProductionContributionProvider(save, catalog);

            EconomyProductionContributionSnapshot snapshot = provider.BuildContributions(1d);

            Assert.AreEqual(EconomyProductionSourceStatus.Available, snapshot.Status);
            Assert.AreEqual(1.5d, snapshot.Contributions[0].Amount, 1e-12);
        }

        [Test]
        public void ReplayIsDeterministicForTheSameTick()
        {
            KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
            FakeSaveGameService save = CreateWritableSave();
            var provider = new KingdomProductionContributionProvider(save, catalog);

            EconomyProductionContributionSnapshot first = provider.BuildContributions(0.5d);
            EconomyProductionContributionSnapshot second = provider.BuildContributions(0.5d);

            Assert.AreEqual(first.Status, second.Status);
            Assert.AreEqual(first.ProfileIdentity, second.ProfileIdentity);
            Assert.AreEqual(first.SourceRevision, second.SourceRevision);
            Assert.AreEqual(first.Contributions.Count, second.Contributions.Count);
            Assert.AreEqual(first.Contributions[0].Amount, second.Contributions[0].Amount, 1e-12);
            Assert.AreEqual(first.Contributions[1].Amount, second.Contributions[1].Amount, 1e-12);
        }

        [Test]
        public void CatchUpContributionsScaleAndHonorElapsedCap()
        {
            KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
            FakeSaveGameService save = CreateWritableSave();
            var provider = new KingdomProductionContributionProvider(save, catalog);

            EconomyProductionContributionSnapshot snapshot = provider.BuildCatchUpContributions(10);

            Assert.AreEqual(EconomyProductionSourceStatus.Available, snapshot.Status);
            Assert.AreEqual(2, snapshot.Contributions.Count);
            Assert.AreEqual(15d, snapshot.Contributions[0].Amount, 1e-12);
            Assert.AreEqual(10d, snapshot.Contributions[1].Amount, 1e-12);
        }

        [Test]
        public void CatchUpRejectsElapsedAboveCatalogPolicy()
        {
            KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
            FakeSaveGameService save = CreateWritableSave();
            var provider = new KingdomProductionContributionProvider(save, catalog);

            EconomyProductionContributionSnapshot snapshot =
                provider.BuildCatchUpContributions(3601);

            Assert.AreEqual(EconomyProductionSourceStatus.Unavailable, snapshot.Status);
            Assert.AreEqual(EconomyDiagnosticCodes.ProductionElapsed, snapshot.Diagnostics[0].Code);
        }

        [Test]
        public void ForbiddenOathmarkCatalogIsRejected()
        {
            const string json =
                "{\"schemaVersion\":1,\"catalogId\":\"kingdom_production_profile_v1\",\"productionEligible\":true,\"sourceRevision\":\"test-oathmark\",\"authorityLedgerId\":\"al_six_family_production_authority_v1\",\"contributions\":[{\"id\":\"oathmark_grant\",\"resourceId\":\"oathmark\",\"buildingId\":\"farm\",\"minBuildingLevel\":1,\"ratePerLevelPerSecond\":1.0,\"capPerTick\":0.0,\"realmIds\":[\"stonehold\"]}]}";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            KingdomProductionProfileLoadResult result =
                KingdomProductionProfileCatalog.TryLoadProfile(
                    bytes,
                    KingdomProductionProfileCatalog.ComputeSha256(bytes));

            Assert.False(result.IsReady);
            Assert.AreEqual(EconomyDiagnosticCodes.ProductionOathmark, result.DiagnosticCode);
            Assert.IsNull(result.Snapshot);
        }

        [Test]
        public void CatalogDriftIsRejected()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(EligibleCatalogJson);
            KingdomProductionProfileLoadResult result =
                KingdomProductionProfileCatalog.TryLoadProfile(
                    bytes,
                    new string('a', 64));

            Assert.False(result.IsReady);
            Assert.AreEqual(EconomyDiagnosticCodes.ProductionDrift, result.DiagnosticCode);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void InvalidSchemaOrBlankProfileIsRejected(int schemaVersion)
        {
            KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
            FakeSaveGameService save = CreateWritableSave();
            save.CurrentSave.SaveSchemaVersion = schemaVersion;
            if (schemaVersion == SaveGameData.CurrentSaveSchemaVersion)
            {
                save.CurrentSave.ProfileId = string.Empty;
            }

            var provider = new KingdomProductionContributionProvider(save, catalog);
            EconomyProductionContributionSnapshot snapshot = provider.BuildContributions(1d);

            Assert.AreEqual(EconomyProductionSourceStatus.Unavailable, snapshot.Status);
            Assert.AreEqual(EconomyDiagnosticCodes.ProductionProfile, snapshot.Diagnostics[0].Code);
        }

        [Test]
        public void UndefinedRealmIsRejectedWithoutCrownlandsFallback()
        {
            KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
            FakeSaveGameService save = CreateWritableSave();
            save.CurrentSave.SelectedRealm = RealmId.None;
            var provider = new KingdomProductionContributionProvider(save, catalog);

            EconomyProductionContributionSnapshot snapshot = provider.BuildContributions(1d);

            Assert.AreEqual(EconomyProductionSourceStatus.Unavailable, snapshot.Status);
            Assert.AreEqual(EconomyDiagnosticCodes.ProductionRealm, snapshot.Diagnostics[0].Code);
        }

        [Test]
        public void OtherRealmDoesNotReceiveStoneholdProduction()
        {
            KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
            FakeSaveGameService save = CreateWritableSave();
            save.CurrentSave.SelectedRealm = RealmId.Umbral;
            var provider = new KingdomProductionContributionProvider(save, catalog);

            EconomyProductionContributionSnapshot snapshot = provider.BuildContributions(1d);

            Assert.AreEqual(EconomyProductionSourceStatus.Available, snapshot.Status);
            Assert.AreEqual(0, snapshot.Contributions.Count);
        }

        [Test]
        public void CommitUncertainSaveIsRejected()
        {
            KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
            FakeSaveGameService save = CreateWritableSave();
            save.LastSaveStatus = SaveOperationStatus.CommitUncertain;
            var provider = new KingdomProductionContributionProvider(save, catalog);

            EconomyProductionContributionSnapshot snapshot = provider.BuildContributions(1d);

            Assert.AreEqual(EconomyProductionSourceStatus.Unavailable, snapshot.Status);
            Assert.AreEqual(EconomyDiagnosticCodes.ProductionProfile, snapshot.Diagnostics[0].Code);
        }

        [Test]
        public void OverflowRollsBackWalletAndRemainders()
        {
            KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
            FakeSaveGameService save = CreateWritableSave();
            FindResource(save.CurrentSave, ResourceType.Gold).Amount = long.MaxValue;
            LocalResourceService service = CreateWritableResourceService(
                save,
                new KingdomProductionContributionProvider(save, catalog));
            WalletSnapshot before = SnapshotWallet(save.CurrentSave);
            var events = new List<string>();
            service.OnResourceChanged += (type, balance) => events.Add($"{type}:{balance}");

            EconomyProductionTickResult result = service.TryTickProduction(1d);

            Assert.AreEqual(EconomyMutationStatus.RejectedOverflow, result.Status);
            AssertWalletUnchanged(save.CurrentSave, before);
            Assert.AreEqual(0, save.SaveCount);
            Assert.IsEmpty(events);
        }

        [Test]
        public void PositiveTickAppliesOnceAndReplayAccumulatesRemainders()
        {
            const string json =
                "{\"schemaVersion\":1,\"catalogId\":\"kingdom_production_profile_v1\",\"productionEligible\":true,\"sourceRevision\":\"test-food-only\",\"authorityLedgerId\":\"al_six_family_production_authority_v1\",\"contributions\":[{\"id\":\"farm_food\",\"resourceId\":\"food\",\"buildingId\":\"farm\",\"minBuildingLevel\":1,\"ratePerLevelPerSecond\":0.75,\"capPerTick\":0.0,\"realmIds\":[\"stonehold\"]}]}";
            KingdomProductionProfileSnapshot catalog = LoadCatalog(json);
            FakeSaveGameService save = CreateWritableSave();
            LocalResourceService service = CreateWritableResourceService(
                save,
                new KingdomProductionContributionProvider(save, catalog));

            EconomyProductionTickResult first = service.TryTickProduction(1d);
            Assert.AreEqual(EconomyMutationStatus.Applied, first.Status);
            Assert.AreEqual(100L, service.GetResourceCount(ResourceType.Food));
            Assert.AreEqual(0, save.SaveCount);

            EconomyProductionTickResult second = service.TryTickProduction(1d);
            Assert.AreEqual(EconomyMutationStatus.Applied, second.Status);
            Assert.AreEqual(101L, service.GetResourceCount(ResourceType.Food));
            Assert.AreEqual(0, save.SaveCount);
        }

        [Test]
        public void InvalidElapsedTimeDoesNotCallThroughAsAvailable()
        {
            KingdomProductionProfileSnapshot catalog = LoadEligibleCatalog();
            FakeSaveGameService save = CreateWritableSave();
            var provider = new KingdomProductionContributionProvider(save, catalog);

            EconomyProductionContributionSnapshot snapshot = provider.BuildContributions(double.NaN);

            Assert.AreEqual(EconomyProductionSourceStatus.Unavailable, snapshot.Status);
            Assert.AreEqual(EconomyDiagnosticCodes.ProductionElapsed, snapshot.Diagnostics[0].Code);
        }

        private static KingdomProductionProfileSnapshot LoadEligibleCatalog()
        {
            return LoadCatalog(EligibleCatalogJson);
        }

        private static KingdomProductionProfileSnapshot LoadCatalog(string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            KingdomProductionProfileLoadResult result =
                KingdomProductionProfileCatalog.TryLoadProfile(
                    bytes,
                    KingdomProductionProfileCatalog.ComputeSha256(bytes));
            Assert.True(result.IsReady, result.DiagnosticCode);
            Assert.NotNull(result.Snapshot);
            return result.Snapshot;
        }

        private static FakeSaveGameService CreateWritableSave()
        {
            var save = new FakeSaveGameService
            {
                CurrentSave = new SaveGameData
                {
                    SaveFormatId = SaveGameData.CurrentSaveFormatId,
                    SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion,
                    ProfileInitializationVersion =
                        SaveGameData.CurrentProfileInitializationVersion,
                    ProfileId = ProfileId,
                    SelectedRealm = RealmId.Stonehold,
                    Resources = CreateWallet(),
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState { BuildingId = "farm", Level = 1 },
                        new BuildingState { BuildingId = "gold_mine", Level = 1 }
                    }
                }
            };
            return save;
        }

        private static List<ResourceData> CreateWallet()
        {
            var wallet = new List<ResourceData>();
            for (int index = 0; index < ResourceRules.WalletResources.Count; index++)
            {
                wallet.Add(
                    new ResourceData
                    {
                        Type = ResourceRules.WalletResources[index],
                        Amount = 100L
                    });
            }

            return wallet;
        }

        private static ResourceData FindResource(SaveGameData save, ResourceType type)
        {
            for (int index = 0; index < save.Resources.Count; index++)
            {
                if (save.Resources[index] != null && save.Resources[index].Type == type)
                {
                    return save.Resources[index];
                }
            }

            Assert.Fail($"Missing {type}");
            return null;
        }

        private static WalletSnapshot SnapshotWallet(SaveGameData save)
        {
            var amounts = new Dictionary<ResourceType, long>();
            for (int index = 0; index < save.Resources.Count; index++)
            {
                ResourceData entry = save.Resources[index];
                if (entry != null)
                {
                    amounts[entry.Type] = entry.Amount;
                }
            }

            return new WalletSnapshot(amounts, save.Resources.Count);
        }

        private static void AssertWalletUnchanged(SaveGameData save, WalletSnapshot before)
        {
            WalletSnapshot after = SnapshotWallet(save);
            Assert.AreEqual(before.Count, after.Count);
            foreach (KeyValuePair<ResourceType, long> pair in before.Amounts)
            {
                Assert.AreEqual(pair.Value, after.Amounts[pair.Key]);
            }
        }

        private static LocalResourceService CreateWritableResourceService(
            ISaveGameService save,
            IEconomyProductionContributionProvider provider)
        {
            Type gateType = typeof(LocalResourceService).Assembly.GetType(
                "AL.Services.Local.EconomyWriteAuthorityGate");
            Assert.NotNull(gateType);
            ConstructorInfo gateCtor = gateType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(ISaveGameService), typeof(IProfileWriteAuthorityProvider) },
                null);
            Assert.NotNull(gateCtor);
            object gate = gateCtor.Invoke(new object[] { save, new WritableAuthority() });

            ConstructorInfo serviceCtor = typeof(LocalResourceService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(ISaveGameService),
                    gateType,
                    typeof(IEconomyProductionContributionProvider)
                },
                null);
            Assert.NotNull(serviceCtor);
            return (LocalResourceService)serviceCtor.Invoke(new[] { save, gate, provider });
        }

        private sealed class WalletSnapshot
        {
            public WalletSnapshot(Dictionary<ResourceType, long> amounts, int count)
            {
                Amounts = amounts;
                Count = count;
            }

            public Dictionary<ResourceType, long> Amounts { get; }
            public int Count { get; }
        }

        private sealed class WritableAuthority : IProfileWriteAuthorityProvider
        {
            public ProfileWriteAuthoritySnapshot GetCurrentAuthority()
            {
                return ProfileWriteAuthoritySnapshotFactory.Writable(
                    ProfileId,
                    "0123456789abcdef0000000000000001",
                    new string('a', 64),
                    ProfileAuthoritySourceGeneration.Primary,
                    Array.Empty<string>());
            }
        }

        private sealed class FakeSaveGameService : ISaveGameService
        {
            public SaveGameData CurrentSave { get; set; }
            public SaveLoadStatus LastLoadStatus { get; set; } = SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage { get; set; } = string.Empty;
            public SaveOperationStatus LastSaveStatus { get; set; } = SaveOperationStatus.SavedPrimary;
            public string LastSaveMessage { get; set; } = string.Empty;
            public int SaveCount { get; private set; }

            public void Save()
            {
                SaveCount++;
            }

            public void Load()
            {
            }

            public bool HasSave() => CurrentSave != null;

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
