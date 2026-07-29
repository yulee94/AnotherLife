using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using AL.Core.BossRewards;
using NUnit.Framework;

namespace AL.Tests.EditMode.BossRewards
{
    public class BossRewardDeterminismVectorTests
    {
        [Test]
        public void EveryRetainedCrossRuntimeVectorIsSemanticallyExact()
        {
            VectorArtifact artifact = ReadArtifact();
            var names = new HashSet<string>(StringComparer.Ordinal);

            Assert.NotNull(artifact.Vectors);
            Assert.AreEqual(5, artifact.Vectors.Length);
            foreach (DeterminismVector vector in artifact.Vectors)
            {
                Assert.NotNull(vector);
                Assert.IsTrue(names.Add(vector.Name), vector.Name);
                Assert.That(
                    vector.ChanceMicros,
                    Is.InRange(0, BossRewardTechnicalLimits.MicrosPerUnit),
                    vector.Name);

                BossRewardComputationRequest request = Request(artifact);
                byte[] canonical = BossRewardDeterministicRoll.BuildCanonicalInput(
                    request.DeterminismVersion,
                    request.CatalogSetId,
                    request.RewardResultId,
                    request.EncounterCompletionId,
                    request.BossDefinitionId,
                    request.RewardProfileId,
                    request.RewardProfileContentVersion,
                    vector.EquipmentDefinitionId);
                byte[] digest = BossRewardDeterministicRoll.ComputeDigest(canonical);
                uint draw = BossRewardDeterministicRoll.ReadBigEndianDraw(digest);
                ulong threshold =
                    BossRewardDeterministicRoll.ComputeThresholdExclusive(
                        vector.ChanceMicros);
                bool hit = BossRewardDeterministicRoll.IsHit(
                    draw,
                    vector.ChanceMicros);

                Assert.AreEqual(
                    vector.CanonicalHex,
                    BossRewardDeterministicRoll.ToLowerHex(canonical),
                    vector.Name);
                Assert.AreEqual(
                    vector.Sha256,
                    BossRewardDeterministicRoll.ToLowerHex(digest),
                    vector.Name);
                Assert.AreEqual((ulong)vector.Draw, draw, vector.Name);
                Assert.AreEqual(
                    (ulong)vector.ThresholdExclusive,
                    threshold,
                    vector.Name);
                Assert.AreEqual(vector.ExpectedHit, hit, vector.Name);

                BossRewardProfile profile = Profile(artifact, vector);
                Assert.AreEqual(
                    vector.ChanceMicros,
                    profile.Entries[0].DropChanceMicros,
                    vector.Name);
                BossRewardComputationResult result = BossRewardComputation.Compute(
                    request,
                    Catalog(
                        artifact,
                        profile,
                        vector.EquipmentDefinitionId));

                Assert.AreEqual(
                    BossRewardComputationStatus.Computed,
                    result.Status,
                    vector.Name);
                CollectionAssert.AreEqual(
                    vector.ExpectedOrderedResult,
                    result.Value.Drops
                        .Select(item => item.EquipmentDefinitionId)
                        .ToArray(),
                    vector.Name);
                Assert.AreEqual(
                    vector.ExpectedHit,
                    result.Value.Drops.Count == 1,
                    vector.Name);
                Assert.AreEqual(
                    vector.ExpectedComputationHash,
                    result.Value.ComputationHash,
                    vector.Name);
            }
        }

        [Test]
        public void EveryRetainedNegativeVectorIsRejectedSemantically()
        {
            VectorArtifact artifact = ReadArtifact();
            var names = new HashSet<string>(StringComparer.Ordinal);

            Assert.NotNull(artifact.NegativeVectors);
            Assert.AreEqual(1, artifact.NegativeVectors.Length);
            foreach (NegativeDeterminismVector vector in artifact.NegativeVectors)
            {
                Assert.NotNull(vector);
                Assert.IsTrue(names.Add(vector.Name), vector.Name);
                Assert.That(
                    vector.ChanceMicros,
                    Is.InRange(0, BossRewardTechnicalLimits.MicrosPerUnit),
                    vector.Name);
                var profile = new BossRewardProfile(
                    artifact.ComputedResultDefaults.GameId,
                    artifact.CanonicalInputDefaults.CatalogSetId,
                    artifact.CanonicalInputDefaults.RewardProfileId,
                    artifact.ComputedResultDefaults
                        .EquipmentDefinitionSchemaVersion,
                    artifact.CanonicalInputDefaults
                        .RewardProfileContentVersion,
                    artifact.ComputedResultDefaults.WarzoneCredits,
                    artifact.ComputedResultDefaults.IsExplicitNoReward,
                    new[]
                    {
                        new BossRewardEntry(
                            vector.EquipmentDefinitionId,
                            vector.ChanceMicros,
                            artifact.ComputedResultDefaults.Quantity,
                            artifact.ComputedResultDefaults
                                .AcquisitionAnnouncementPolicyId)
                    },
                    "source_revision_1",
                    artifact.ComputedResultDefaults.RewardProfileSha256);
                BossRewardCatalogSnapshot catalog = Catalog(
                    artifact,
                    profile,
                    BossRewardTestFixtures.AlphaId);

                BossRewardComputationResult result = BossRewardComputation.Compute(
                    Request(artifact),
                    catalog);

                Assert.AreEqual(
                    vector.ExpectedStatus,
                    result.Status.ToString(),
                    vector.Name);
                Assert.IsTrue(
                    result.Diagnostics.Any(item =>
                        string.Equals(
                            item.Code,
                            vector.ExpectedDiagnosticCode,
                            StringComparison.Ordinal)),
                    vector.Name);
                Assert.IsNull(result.Value, vector.Name);
            }
        }

        [Test]
        public void VectorArtifactDeclaresExactSupportedSchemaAndDefaults()
        {
            VectorArtifact artifact = ReadArtifact();

            Assert.AreEqual("boss_reward_vector_v1", artifact.VectorSchemaVersion);
            Assert.AreEqual(
                BossRewardTechnicalLimits.SupportedDeterminismVersion,
                artifact.DeterminismVersion);
            Assert.AreEqual(
                BossRewardTestFixtures.CatalogSetId,
                artifact.CanonicalInputDefaults.CatalogSetId);
            Assert.AreEqual(
                BossRewardTestFixtures.ResultId,
                artifact.CanonicalInputDefaults.RewardResultId);
            Assert.AreEqual(
                BossRewardTestFixtures.CompletionId,
                artifact.CanonicalInputDefaults.EncounterCompletionId);
            Assert.AreEqual(
                BossRewardTestFixtures.BossId,
                artifact.CanonicalInputDefaults.BossDefinitionId);
            Assert.AreEqual(
                BossRewardTestFixtures.RewardProfileId,
                artifact.CanonicalInputDefaults.RewardProfileId);
            Assert.AreEqual(
                BossRewardTestFixtures.RewardProfileVersion,
                artifact.CanonicalInputDefaults.RewardProfileContentVersion);
            Assert.AreEqual(
                BossRewardTestFixtures.GameId,
                artifact.ComputedResultDefaults.GameId);
            Assert.AreEqual(
                BossRewardTestFixtures.ProfileId,
                artifact.ComputedResultDefaults.ProfileId);
            Assert.AreEqual(
                BossRewardTechnicalLimits.SupportedRewardSchemaVersion,
                artifact.ComputedResultDefaults.EquipmentDefinitionSchemaVersion);
            Assert.AreEqual(
                BossRewardAcquisitionSnapshotPolicies.SnapshotV1,
                artifact.ComputedResultDefaults.AcquisitionSnapshotPolicyId);
            Assert.AreEqual(
                BossRewardStackPolicies.StackQuantity,
                artifact.ComputedResultDefaults.StackPolicyId);
        }

        private static BossRewardComputationRequest Request(VectorArtifact artifact)
        {
            return BossRewardTestFixtures.Request(
                gameId: artifact.ComputedResultDefaults.GameId,
                catalogSetId: artifact.CanonicalInputDefaults.CatalogSetId,
                profileId: artifact.ComputedResultDefaults.ProfileId,
                encounterId: artifact.ComputedResultDefaults.EncounterId,
                encounterCompletionId:
                artifact.CanonicalInputDefaults.EncounterCompletionId,
                rewardResultId: artifact.CanonicalInputDefaults.RewardResultId,
                bossDefinitionId:
                artifact.CanonicalInputDefaults.BossDefinitionId,
                bossDefinitionContentVersion:
                artifact.ComputedResultDefaults.BossDefinitionContentVersion,
                rewardProfileId:
                artifact.CanonicalInputDefaults.RewardProfileId,
                rewardProfileContentVersion:
                artifact.CanonicalInputDefaults.RewardProfileContentVersion,
                determinismVersion: artifact.DeterminismVersion);
        }

        private static BossRewardProfile Profile(
            VectorArtifact artifact,
            DeterminismVector vector)
        {
            ComputedResultDefaults defaults = artifact.ComputedResultDefaults;
            return new BossRewardProfile(
                defaults.GameId,
                artifact.CanonicalInputDefaults.CatalogSetId,
                artifact.CanonicalInputDefaults.RewardProfileId,
                defaults.EquipmentDefinitionSchemaVersion,
                artifact.CanonicalInputDefaults.RewardProfileContentVersion,
                defaults.WarzoneCredits,
                defaults.IsExplicitNoReward,
                new[]
                {
                    new BossRewardEntry(
                        vector.EquipmentDefinitionId,
                        vector.ChanceMicros,
                        defaults.Quantity,
                        defaults.AcquisitionAnnouncementPolicyId)
                },
                "source_revision_1",
                defaults.RewardProfileSha256);
        }

        private static BossRewardCatalogSnapshot Catalog(
            VectorArtifact artifact,
            BossRewardProfile profile,
            string equipmentDefinitionId)
        {
            ComputedResultDefaults defaults = artifact.ComputedResultDefaults;
            var definition = new BossEquipmentDefinitionSnapshot(
                equipmentDefinitionId,
                defaults.EquipmentDefinitionSchemaVersion,
                defaults.EquipmentDefinitionContentVersion,
                defaults.SlotId,
                defaults.AttackBonus,
                defaults.DefenseBonus,
                defaults.HealthBonus,
                defaults.StackPolicyId,
                defaults.AcquisitionSnapshotPolicyId,
                "equipment_content_" + equipmentDefinitionId,
                "source_revision_1",
                BossRewardTestFixtures.ShaB);
            return new BossRewardCatalogSnapshot(
                defaults.GameId,
                artifact.CanonicalInputDefaults.CatalogSetId,
                defaults.EquipmentDefinitionSchemaVersion,
                "catalog_revision_1",
                new[]
                {
                    new BossRewardBinding(
                        artifact.CanonicalInputDefaults.BossDefinitionId,
                        defaults.BossDefinitionContentVersion,
                        artifact.CanonicalInputDefaults.RewardProfileId,
                        artifact.CanonicalInputDefaults.RewardProfileContentVersion)
                },
                new[] { profile },
                new[] { definition },
                new[] { defaults.AcquisitionAnnouncementPolicyId });
        }

        private static VectorArtifact ReadArtifact()
        {
            using (FileStream stream = File.OpenRead(VectorPath()))
            {
                var serializer =
                    new DataContractJsonSerializer(typeof(VectorArtifact));
                return (VectorArtifact)serializer.ReadObject(stream);
            }
        }

        private static string VectorPath()
        {
            return Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "AL",
                "Tests",
                "EditMode",
                "BossRewards",
                "TestVectors",
                "boss_reward_sha256_v1.json");
        }

        [DataContract]
        private sealed class VectorArtifact
        {
            [DataMember(Name = "vectorSchemaVersion", IsRequired = true)]
            public string VectorSchemaVersion { get; set; }

            [DataMember(Name = "determinismVersion", IsRequired = true)]
            public string DeterminismVersion { get; set; }

            [DataMember(Name = "canonicalInputDefaults", IsRequired = true)]
            public CanonicalInputDefaults CanonicalInputDefaults { get; set; }

            [DataMember(Name = "computedResultDefaults", IsRequired = true)]
            public ComputedResultDefaults ComputedResultDefaults { get; set; }

            [DataMember(Name = "vectors", IsRequired = true)]
            public DeterminismVector[] Vectors { get; set; }

            [DataMember(Name = "negativeVectors", IsRequired = true)]
            public NegativeDeterminismVector[] NegativeVectors { get; set; }
        }

        [DataContract]
        private sealed class CanonicalInputDefaults
        {
            [DataMember(Name = "catalogSetId", IsRequired = true)]
            public string CatalogSetId { get; set; }

            [DataMember(Name = "rewardResultId", IsRequired = true)]
            public string RewardResultId { get; set; }

            [DataMember(Name = "encounterCompletionId", IsRequired = true)]
            public string EncounterCompletionId { get; set; }

            [DataMember(Name = "bossDefinitionId", IsRequired = true)]
            public string BossDefinitionId { get; set; }

            [DataMember(Name = "rewardProfileId", IsRequired = true)]
            public string RewardProfileId { get; set; }

            [DataMember(Name = "rewardProfileContentVersion", IsRequired = true)]
            public string RewardProfileContentVersion { get; set; }
        }

        [DataContract]
        private sealed class ComputedResultDefaults
        {
            [DataMember(Name = "gameId", IsRequired = true)]
            public string GameId { get; set; }

            [DataMember(Name = "profileId", IsRequired = true)]
            public string ProfileId { get; set; }

            [DataMember(Name = "encounterId", IsRequired = true)]
            public string EncounterId { get; set; }

            [DataMember(Name = "bossDefinitionContentVersion", IsRequired = true)]
            public string BossDefinitionContentVersion { get; set; }

            [DataMember(Name = "rewardProfileSha256", IsRequired = true)]
            public string RewardProfileSha256 { get; set; }

            [DataMember(Name = "warzoneCredits", IsRequired = true)]
            public int WarzoneCredits { get; set; }

            [DataMember(Name = "isExplicitNoReward", IsRequired = true)]
            public bool IsExplicitNoReward { get; set; }

            [DataMember(Name = "equipmentDefinitionSchemaVersion", IsRequired = true)]
            public string EquipmentDefinitionSchemaVersion { get; set; }

            [DataMember(Name = "equipmentDefinitionContentVersion", IsRequired = true)]
            public string EquipmentDefinitionContentVersion { get; set; }

            [DataMember(Name = "slotId", IsRequired = true)]
            public string SlotId { get; set; }

            [DataMember(Name = "attackBonus", IsRequired = true)]
            public int AttackBonus { get; set; }

            [DataMember(Name = "defenseBonus", IsRequired = true)]
            public int DefenseBonus { get; set; }

            [DataMember(Name = "healthBonus", IsRequired = true)]
            public int HealthBonus { get; set; }

            [DataMember(Name = "quantity", IsRequired = true)]
            public int Quantity { get; set; }

            [DataMember(Name = "stackPolicyId", IsRequired = true)]
            public string StackPolicyId { get; set; }

            [DataMember(Name = "acquisitionSnapshotPolicyId", IsRequired = true)]
            public string AcquisitionSnapshotPolicyId { get; set; }

            [DataMember(Name = "acquisitionAnnouncementPolicyId", IsRequired = true)]
            public string AcquisitionAnnouncementPolicyId { get; set; }
        }

        [DataContract]
        private sealed class DeterminismVector
        {
            [DataMember(Name = "name", IsRequired = true)]
            public string Name { get; set; }

            [DataMember(Name = "equipmentDefinitionId", IsRequired = true)]
            public string EquipmentDefinitionId { get; set; }

            [DataMember(Name = "chanceMicros", IsRequired = true)]
            public int ChanceMicros { get; set; }

            [DataMember(Name = "canonicalHex", IsRequired = true)]
            public string CanonicalHex { get; set; }

            [DataMember(Name = "sha256", IsRequired = true)]
            public string Sha256 { get; set; }

            [DataMember(Name = "draw", IsRequired = true)]
            public long Draw { get; set; }

            [DataMember(Name = "thresholdExclusive", IsRequired = true)]
            public long ThresholdExclusive { get; set; }

            [DataMember(Name = "expectedHit", IsRequired = true)]
            public bool ExpectedHit { get; set; }

            [DataMember(Name = "expectedOrderedResult", IsRequired = true)]
            public string[] ExpectedOrderedResult { get; set; }

            [DataMember(Name = "expectedComputationHash", IsRequired = true)]
            public string ExpectedComputationHash { get; set; }
        }

        [DataContract]
        private sealed class NegativeDeterminismVector
        {
            [DataMember(Name = "name", IsRequired = true)]
            public string Name { get; set; }

            [DataMember(Name = "equipmentDefinitionId", IsRequired = true)]
            public string EquipmentDefinitionId { get; set; }

            [DataMember(Name = "chanceMicros", IsRequired = true)]
            public int ChanceMicros { get; set; }

            [DataMember(Name = "expectedStatus", IsRequired = true)]
            public string ExpectedStatus { get; set; }

            [DataMember(Name = "expectedDiagnosticCode", IsRequired = true)]
            public string ExpectedDiagnosticCode { get; set; }
        }
    }
}
