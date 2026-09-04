using System;
using System.IO;
using System.Text;
using AL.Core.BossRewards;
using NUnit.Framework;

namespace AL.Tests.EditMode.BossRewards
{
    public sealed class BossRewardSourceCatalogTests
    {
        [Test]
        public void PinnedCatalogResolvesApprovedBossWithoutMutation()
        {
            byte[] bytes = ReadCatalogBytes();
            BossRewardSourceCatalogLoadResult loaded =
                BossRewardSourceCatalog.LoadPinned(bytes);

            Assert.AreEqual(BossRewardSourceCatalogStatus.Ready, loaded.Status);
            Assert.IsFalse(loaded.AllowsMutation);
            Assert.AreEqual("blocked", loaded.MutationActivation);
            Assert.AreEqual(BossRewardSourceCatalog.ExpectedSourceByteLength, bytes.Length);
            Assert.AreEqual(BossRewardSourceCatalog.ExpectedSourceSha256, loaded.SourceSha256);

            BossRewardSourceResolution resolved = BossRewardSourceCatalog.Resolve(
                loaded,
                BossRewardSourceCatalog.RepresentativeBossId);
            Assert.IsTrue(resolved.IsFound);
            Assert.AreEqual(
                BossRewardSourceCatalog.RepresentativeProfileId,
                resolved.Profile.Id);
            Assert.AreEqual(
                BossRewardSourceCatalog.RepresentativeEquipmentId,
                resolved.Equipment[0].EquipmentDefinitionId);
            Assert.AreEqual(250, resolved.Profile.WarzoneCredits);
            Assert.AreEqual(1, resolved.Profile.Entries[0].Quantity);
            Assert.AreEqual(
                BossRewardTechnicalLimits.MicrosPerUnit,
                resolved.Profile.Entries[0].DropChanceMicros);
            Assert.AreNotEqual(
                resolved.Equipment[0].EquipmentDefinitionId,
                resolved.Equipment[0].PresentationContentKey);

            BossRewardComputationResult computed = BossRewardComputation.Compute(
                new BossRewardComputationRequest(
                    BossRewardSourceCatalog.GameId,
                    BossRewardSourceCatalog.CatalogSetId,
                    "profile_source_test",
                    "encounter_source_test",
                    "completion_source_test",
                    "result_source_test",
                    resolved.Binding.BossDefinitionId,
                    resolved.Binding.BossDefinitionContentVersion,
                    resolved.Binding.RewardProfileId,
                    resolved.Binding.RewardProfileContentVersion,
                    BossRewardTechnicalLimits.SupportedDeterminismVersion),
                loaded.Snapshot);
            Assert.AreEqual(BossRewardComputationStatus.Computed, computed.Status);
            Assert.AreEqual(250, computed.Value.WarzoneCredits);
            Assert.AreEqual(1, computed.Value.Drops.Count);
        }

        [Test]
        public void UnknownBossIsExplicit()
        {
            BossRewardSourceCatalogLoadResult loaded =
                BossRewardSourceCatalog.Load(ReadCatalogBytes());
            BossRewardSourceResolution resolved = BossRewardSourceCatalog.Resolve(
                loaded,
                "boss_unknown_placeholder");
            Assert.AreEqual(BossRewardSourceCatalogStatus.UnknownBoss, resolved.Status);
            Assert.IsFalse(resolved.IsFound);
        }

        [Test]
        public void MissingBytesAreUnavailable()
        {
            BossRewardSourceCatalogLoadResult loaded = BossRewardSourceCatalog.Load(null);
            Assert.AreEqual(BossRewardSourceCatalogStatus.SourceUnavailable, loaded.Status);
            Assert.IsFalse(loaded.AllowsMutation);
        }

        [Test]
        public void DuplicateBindingFailsClosed()
        {
            string json = CatalogText();
            int start = json.IndexOf("\"bindings\": [", StringComparison.Ordinal);
            int rowStart = json.IndexOf('{', start);
            int rowEnd = json.IndexOf('}', rowStart);
            string row = json.Substring(rowStart, rowEnd - rowStart + 1);
            json = json.Insert(rowEnd + 1, "," + row);
            BossRewardSourceCatalogLoadResult loaded =
                BossRewardSourceCatalog.Load(Encoding.UTF8.GetBytes(json));
            Assert.AreEqual(BossRewardSourceCatalogStatus.DuplicateBinding, loaded.Status);
        }

        [Test]
        public void UnsupportedVersionFailsClosed()
        {
            string json = CatalogText().Replace(
                "boss_reward_schema_v1",
                "boss_reward_schema_v2");
            BossRewardSourceCatalogLoadResult loaded =
                BossRewardSourceCatalog.Load(Encoding.UTF8.GetBytes(json));
            Assert.AreEqual(BossRewardSourceCatalogStatus.UnsupportedVersion, loaded.Status);
        }

        [Test]
        public void ProfileHashMismatchFailsClosed()
        {
            string json = CatalogText().Replace(
                "14d182912ca9c8b62e7eadfb14900cfcdbe2e0708a1124baa9588ad1f1b527dc",
                "0000000000000000000000000000000000000000000000000000000000000000");
            BossRewardSourceCatalogLoadResult loaded =
                BossRewardSourceCatalog.Load(Encoding.UTF8.GetBytes(json));
            Assert.AreEqual(BossRewardSourceCatalogStatus.HashMismatch, loaded.Status);
        }

        [Test]
        public void MissingEquipmentReferenceFailsClosed()
        {
            string json = CatalogText().Replace(
                "\"equipmentDefinitionId\": \"equipment_stonehold_fault_crowned_colossus_core\",\n          \"dropChanceMicros\": 1000000",
                "\"equipmentDefinitionId\": \"equipment_missing_reference\",\n          \"dropChanceMicros\": 1000000");
            json = RepairProfileHash(json);
            BossRewardSourceCatalogLoadResult loaded =
                BossRewardSourceCatalog.Load(Encoding.UTF8.GetBytes(json));
            Assert.AreEqual(BossRewardSourceCatalogStatus.MissingReference, loaded.Status);
        }

        [Test]
        public void PinnedSabotageFailsHashThenRestore()
        {
            byte[] original = ReadCatalogBytes();
            byte[] sabotaged = Encoding.UTF8.GetBytes(
                CatalogText().Replace(
                    "boss_stonehold_fault_crowned_colossus",
                    "boss_stonehold_fault_crowned_colossuX"));
            BossRewardSourceCatalogLoadResult failed =
                BossRewardSourceCatalog.LoadPinned(sabotaged);
            Assert.AreEqual(BossRewardSourceCatalogStatus.HashMismatch, failed.Status);
            Assert.AreNotEqual(BossRewardSourceCatalog.ExpectedSourceSha256, failed.SourceSha256);
            BossRewardSourceCatalogLoadResult restored =
                BossRewardSourceCatalog.LoadPinned(original);
            Assert.AreEqual(BossRewardSourceCatalogStatus.Ready, restored.Status);
            Assert.IsFalse(restored.AllowsMutation);
        }

        [Test]
        public void MutationActivationCannotBeForged()
        {
            string json = CatalogText().Replace(
                "\"mutationActivation\": \"blocked\"",
                "\"mutationActivation\": \"enabled\"");
            BossRewardSourceCatalogLoadResult loaded =
                BossRewardSourceCatalog.Load(Encoding.UTF8.GetBytes(json));
            Assert.AreEqual(BossRewardSourceCatalogStatus.InvalidCatalog, loaded.Status);
            Assert.IsFalse(loaded.AllowsMutation);
        }

        private static string RepairProfileHash(string json)
        {
            var profile = new BossRewardProfile(
                BossRewardSourceCatalog.GameId,
                BossRewardSourceCatalog.CatalogSetId,
                BossRewardSourceCatalog.RepresentativeProfileId,
                BossRewardTechnicalLimits.SupportedRewardSchemaVersion,
                "v001",
                250,
                false,
                new[]
                {
                    new BossRewardEntry(
                        "equipment_missing_reference",
                        BossRewardTechnicalLimits.MicrosPerUnit,
                        1,
                        "boss_reward.item_acquired")
                },
                "boss_reward_source_v001",
                "0000000000000000000000000000000000000000000000000000000000000000");
            string hash = BossRewardSourceCatalog.ComputeProfileSha256(profile);
            return json.Replace(
                "14d182912ca9c8b62e7eadfb14900cfcdbe2e0708a1124baa9588ad1f1b527dc",
                hash);
        }

        private static string CatalogText()
        {
            return Encoding.UTF8.GetString(ReadCatalogBytes());
        }

        private static byte[] ReadCatalogBytes()
        {
            string[] candidates =
            {
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Assets",
                    "AL",
                    "StreamingAssets",
                    "GameData",
                    "al_boss_reward_source_catalog.json"),
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "unity",
                    "Assets",
                    "AL",
                    "StreamingAssets",
                    "GameData",
                    "al_boss_reward_source_catalog.json")
            };
            for (int index = 0; index < candidates.Length; index++)
            {
                if (File.Exists(candidates[index]))
                    return File.ReadAllBytes(candidates[index]);
            }

            Assert.Fail("boss reward source catalog was not found");
            return Array.Empty<byte>();
        }
    }
}
