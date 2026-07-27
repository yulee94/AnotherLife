using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.Core.BossRewards;
using NUnit.Framework;

namespace AL.Tests.EditMode.BossRewards
{
    public class BossRewardComputationTests
    {
        [Test]
        public void ValidProfileComputesImmutableTechnicalReward()
        {
            BossRewardComputationResult result = BossRewardTestFixtures.Computation();

            Assert.AreEqual(BossRewardComputationStatus.Computed, result.Status);
            Assert.IsTrue(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.AreEqual(25, result.Value.WarzoneCredits);
            Assert.AreEqual(BossRewardTestFixtures.ResultId, result.Value.RewardResultId);
            Assert.AreEqual(1, result.Value.Drops.Count);
            Assert.AreEqual(
                BossRewardTestFixtures.AlphaId,
                result.Value.Drops[0].EquipmentDefinitionId);
            Assert.AreEqual(64, result.Value.ComputationHash.Length);
            Assert.Throws<NotSupportedException>(() =>
                ((IList)result.Value.Drops).Add(result.Value.Drops[0]));
        }

        [Test]
        public void NullAndMalformedRequestsFailClosedWithoutValue()
        {
            BossRewardComputationResult nullResult =
                BossRewardComputation.Compute(null, BossRewardTestFixtures.Catalog());
            BossRewardComputationResult blankResult = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(rewardResultId: " "),
                BossRewardTestFixtures.Catalog());
            BossRewardComputationResult controlResult = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(encounterId: "bad\nencounter"),
                BossRewardTestFixtures.Catalog());

            Assert.AreEqual(BossRewardComputationStatus.InvalidRequest, nullResult.Status);
            Assert.AreEqual(BossRewardComputationStatus.InvalidRequest, blankResult.Status);
            Assert.AreEqual(BossRewardComputationStatus.InvalidRequest, controlResult.Status);
            Assert.IsNull(nullResult.Value);
            Assert.IsNull(blankResult.Value);
            Assert.IsNull(controlResult.Value);
            Assert.IsTrue(blankResult.Diagnostics.All(item => item.BlocksOperation));
        }

        [Test]
        public void OversizedUtf8IdentityIsRejected()
        {
            string oversized = new string('x', 257);
            BossRewardComputationResult result = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(encounterId: oversized),
                BossRewardTestFixtures.Catalog());

            Assert.AreEqual(BossRewardComputationStatus.InvalidRequest, result.Status);
            StringAssert.Contains("ID-INVALID", result.Diagnostics[0].Code);
        }

        [Test]
        public void UnsupportedDeterminismVersionIsTyped()
        {
            BossRewardComputationResult result = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(determinismVersion: "future_v2"),
                BossRewardTestFixtures.Catalog());

            Assert.AreEqual(BossRewardComputationStatus.UnsupportedVersion, result.Status);
            Assert.IsNull(result.Value);
        }

        [Test]
        public void UnknownBossAndBindingMismatchRemainDistinct()
        {
            BossRewardComputationResult unknown = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(bossDefinitionId: "boss_unknown"),
                BossRewardTestFixtures.Catalog());
            var mismatchedBinding = new BossRewardBinding(
                BossRewardTestFixtures.BossId,
                "boss_v2",
                BossRewardTestFixtures.RewardProfileId,
                BossRewardTestFixtures.RewardProfileVersion);
            BossRewardComputationResult mismatch = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                BossRewardTestFixtures.Catalog(bindings: new[] { mismatchedBinding }));

            Assert.AreEqual(BossRewardComputationStatus.UnknownBoss, unknown.Status);
            Assert.AreEqual(
                BossRewardComputationStatus.BossRewardBindingMismatch,
                mismatch.Status);
        }

        [Test]
        public void UnknownRewardProfileIsTyped()
        {
            const string missingProfile = "reward_profile_missing";
            var binding = new BossRewardBinding(
                BossRewardTestFixtures.BossId,
                BossRewardTestFixtures.BossVersion,
                missingProfile,
                BossRewardTestFixtures.RewardProfileVersion);
            BossRewardComputationResult result = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(rewardProfileId: missingProfile),
                BossRewardTestFixtures.Catalog(bindings: new[] { binding }));

            Assert.AreEqual(
                BossRewardComputationStatus.UnknownRewardProfile,
                result.Status);
        }

        [Test]
        public void ProfileRejectsDuplicateItemsInvalidChanceAndUnapprovedQuantity()
        {
            var profile = BossRewardTestFixtures.Profile(entries: new[]
            {
                new BossRewardEntry(
                    BossRewardTestFixtures.AlphaId,
                    -1,
                    2,
                    BossRewardTestFixtures.ItemPolicyId),
                new BossRewardEntry(
                    BossRewardTestFixtures.AlphaId,
                    500_000,
                    1,
                    BossRewardTestFixtures.ItemPolicyId)
            });

            BossRewardComputationResult result = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                BossRewardTestFixtures.Catalog(profile));

            Assert.AreEqual(
                BossRewardComputationStatus.InvalidRewardProfile,
                result.Status);
            CollectionAssert.IsSupersetOf(
                result.Diagnostics.Select(item => item.Code).ToArray(),
                new[]
                {
                    "AL-BOSS-REWARD-CATALOG-ENTRY-DUPLICATE",
                    "AL-BOSS-REWARD-CATALOG-CHANCE-INVALID",
                    "AL-BOSS-REWARD-CATALOG-QUANTITY-UNAPPROVED"
                });
        }

        [Test]
        public void MissingEquipmentAndUnknownAnnouncementPolicyAreRejected()
        {
            var profile = BossRewardTestFixtures.Profile(entries: new[]
            {
                new BossRewardEntry("equipment_missing", 500_000, 1, "policy_missing")
            });

            BossRewardComputationResult result = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                BossRewardTestFixtures.Catalog(
                    profile,
                    equipment: Array.Empty<BossEquipmentDefinitionSnapshot>()));

            Assert.AreEqual(
                BossRewardComputationStatus.InvalidEquipmentDefinition,
                result.Status);
            Assert.IsTrue(result.Diagnostics.Any(item =>
                item.Code == "AL-BOSS-REWARD-CATALOG-EQUIPMENT-UNKNOWN"));
            Assert.IsTrue(result.Diagnostics.Any(item =>
                item.Code ==
                "AL-BOSS-REWARD-CATALOG-ANNOUNCEMENT-POLICY-UNKNOWN"));
        }

        [Test]
        public void UnsupportedEquipmentSchemaAndMalformedSourceHashAreRejected()
        {
            BossEquipmentDefinitionSnapshot source =
                BossRewardTestFixtures.Equipment();
            var unsupported = new BossEquipmentDefinitionSnapshot(
                source.EquipmentDefinitionId,
                "future_schema",
                source.ContentVersion,
                source.SlotId,
                source.AttackBonus,
                source.DefenseBonus,
                source.HealthBonus,
                source.StackPolicyId,
                source.AcquisitionSnapshotPolicyId,
                source.PresentationContentKey,
                source.SourceRevision,
                source.RawSha256);
            BossRewardComputationResult equipmentResult =
                BossRewardComputation.Compute(
                    BossRewardTestFixtures.Request(),
                    BossRewardTestFixtures.Catalog(
                        equipment: new[]
                        {
                            unsupported,
                            BossRewardTestFixtures.Equipment(
                                BossRewardTestFixtures.BetaId,
                                "equipment_v1",
                                5,
                                6,
                                7,
                                BossRewardTestFixtures.ShaA)
                        }));
            BossRewardComputationResult profileResult =
                BossRewardComputation.Compute(
                    BossRewardTestFixtures.Request(),
                    BossRewardTestFixtures.Catalog(
                        BossRewardTestFixtures.Profile(rawSha256: "bad_hash")));

            Assert.AreEqual(
                BossRewardComputationStatus.InvalidEquipmentDefinition,
                equipmentResult.Status);
            Assert.AreEqual(
                BossRewardComputationStatus.InvalidRewardProfile,
                profileResult.Status);
        }

        [Test]
        public void ExplicitNoRewardRequiresZeroCreditsAndNoEntries()
        {
            BossRewardProfile invalid = BossRewardTestFixtures.Profile(
                entries: Array.Empty<BossRewardEntry>(),
                credits: 1,
                explicitNoReward: true);
            BossRewardComputationResult invalidResult = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                BossRewardTestFixtures.Catalog(invalid));

            BossRewardProfile valid = BossRewardTestFixtures.Profile(
                entries: Array.Empty<BossRewardEntry>(),
                credits: 0,
                explicitNoReward: true);
            BossRewardComputationResult validResult = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                BossRewardTestFixtures.Catalog(valid));

            Assert.AreEqual(
                BossRewardComputationStatus.InvalidRewardProfile,
                invalidResult.Status);
            Assert.AreEqual(
                BossRewardComputationStatus.ExplicitNoReward,
                validResult.Status);
            Assert.IsTrue(validResult.Value.IsExplicitNoReward);
            Assert.IsEmpty(validResult.Value.Drops);
        }

        [Test]
        public void CreditOnlyProfileIsValid()
        {
            BossRewardProfile profile = BossRewardTestFixtures.Profile(
                entries: Array.Empty<BossRewardEntry>(),
                credits: 10);
            BossRewardComputationResult result = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                BossRewardTestFixtures.Catalog(profile));

            Assert.AreEqual(BossRewardComputationStatus.Computed, result.Status);
            Assert.AreEqual(10, result.Value.WarzoneCredits);
            Assert.IsEmpty(result.Value.Drops);
        }

        [Test]
        public void DefinitionEnumerationOrderCannotChangeResultOrHash()
        {
            var alpha = new BossRewardEntry(
                BossRewardTestFixtures.AlphaId,
                800_000,
                1,
                BossRewardTestFixtures.ItemPolicyId);
            var beta = new BossRewardEntry(
                BossRewardTestFixtures.BetaId,
                800_000,
                1,
                BossRewardTestFixtures.ItemPolicyId);
            BossRewardComputationResult forward = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                BossRewardTestFixtures.Catalog(
                    BossRewardTestFixtures.Profile(entries: new[] { alpha, beta })));
            BossRewardComputationResult reverse = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                BossRewardTestFixtures.Catalog(
                    BossRewardTestFixtures.Profile(entries: new[] { beta, alpha })));

            CollectionAssert.AreEqual(
                forward.Value.Drops.Select(item => item.EquipmentDefinitionId).ToArray(),
                reverse.Value.Drops.Select(item => item.EquipmentDefinitionId).ToArray());
            Assert.AreEqual(
                forward.Value.ComputationHash,
                reverse.Value.ComputationHash);
        }

        [Test]
        public void ContentVersionChangesHashAndUnrelatedMissDoesNotChangeDraw()
        {
            BossRewardComputationResult baseline =
                BossRewardTestFixtures.Computation();
            const string nextVersion = "reward_v2";
            BossRewardProfile versionedProfile = BossRewardTestFixtures.Profile(
                contentVersion: nextVersion);
            var versionedBinding = new BossRewardBinding(
                BossRewardTestFixtures.BossId,
                BossRewardTestFixtures.BossVersion,
                BossRewardTestFixtures.RewardProfileId,
                nextVersion);
            BossRewardComputationResult versioned = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(
                    rewardProfileContentVersion: nextVersion),
                BossRewardTestFixtures.Catalog(
                    versionedProfile,
                    bindings: new[] { versionedBinding }));

            BossRewardProfile alphaOnly = BossRewardTestFixtures.Profile(
                entries: new[]
                {
                    new BossRewardEntry(
                        BossRewardTestFixtures.AlphaId,
                        BossRewardTechnicalLimits.MicrosPerUnit,
                        1,
                        BossRewardTestFixtures.ItemPolicyId)
                });
            BossRewardComputationResult withoutUnrelated =
                BossRewardTestFixtures.Computation(
                    catalog: BossRewardTestFixtures.Catalog(alphaOnly));

            Assert.AreNotEqual(
                baseline.Value.ComputationHash,
                versioned.Value.ComputationHash);
            Assert.AreEqual(
                baseline.Value.Drops[0].EquipmentDefinitionId,
                withoutUnrelated.Value.Drops[0].EquipmentDefinitionId);
            Assert.AreEqual(
                baseline.Value.ComputationHash,
                withoutUnrelated.Value.ComputationHash);
        }

        [Test]
        public void EntrySpecificDrawChangesWithResultIdentity()
        {
            uint first = BossRewardDeterministicRoll.ComputeDraw(
                BossRewardTestFixtures.Request(rewardResultId: "result_one"),
                BossRewardTestFixtures.AlphaId);
            uint second = BossRewardDeterministicRoll.ComputeDraw(
                BossRewardTestFixtures.Request(rewardResultId: "result_two"),
                BossRewardTestFixtures.AlphaId);

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void ComputationHashBindsSaveProfileIdentity()
        {
            BossRewardComputationResult first =
                BossRewardTestFixtures.Computation();
            BossRewardComputationResult second =
                BossRewardTestFixtures.Computation(
                    request: BossRewardTestFixtures.Request(
                        profileId: "profile_other"));

            CollectionAssert.AreEqual(
                first.Value.Drops.Select(item => item.EquipmentDefinitionId).ToArray(),
                second.Value.Drops.Select(item => item.EquipmentDefinitionId).ToArray());
            Assert.AreNotEqual(
                first.Value.ComputationHash,
                second.Value.ComputationHash);
        }

        [TestCase(0, 0UL)]
        [TestCase(1, 4294UL)]
        [TestCase(500000, 2147483648UL)]
        [TestCase(999999, 4294963001UL)]
        [TestCase(1000000, 4294967296UL)]
        public void FixedPointThresholdUsesExactUInt64Floor(
            int chanceMicros,
            ulong expected)
        {
            Assert.AreEqual(
                expected,
                BossRewardDeterministicRoll.ComputeThresholdExclusive(chanceMicros));
        }

        [Test]
        public void ChanceBoundaryRulesAreExact()
        {
            Assert.IsFalse(BossRewardDeterministicRoll.IsHit(0, 0));
            Assert.IsFalse(BossRewardDeterministicRoll.IsHit(uint.MaxValue, 999_999));
            Assert.IsTrue(BossRewardDeterministicRoll.IsHit(uint.MaxValue, 1_000_000));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BossRewardDeterministicRoll.ComputeThresholdExclusive(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BossRewardDeterministicRoll.ComputeThresholdExclusive(1_000_001));
        }

        [Test]
        public void ContractCollectionsAreDefensiveAndReadOnly()
        {
            var entries = new List<BossRewardEntry>
            {
                new BossRewardEntry(
                    BossRewardTestFixtures.AlphaId,
                    1,
                    1,
                    BossRewardTestFixtures.ItemPolicyId)
            };
            BossRewardProfile profile = BossRewardTestFixtures.Profile(entries);
            entries.Clear();

            Assert.AreEqual(1, profile.Entries.Count);
            Assert.Throws<NotSupportedException>(() =>
                ((IList)profile.Entries).Clear());
        }

        [Test]
        public void RequiredContractCollectionCannotNormalizeNullToEmpty()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BossRewardProfile(
                    BossRewardTestFixtures.GameId,
                    BossRewardTestFixtures.CatalogSetId,
                    BossRewardTestFixtures.RewardProfileId,
                    BossRewardTestFixtures.SchemaVersion,
                    BossRewardTestFixtures.RewardProfileVersion,
                    0,
                    true,
                    null,
                    "source_revision_1",
                    BossRewardTestFixtures.ShaA));
        }
    }
}
