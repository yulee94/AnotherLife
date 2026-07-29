using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
        public void CanonicalCatalogIdentitiesRejectNoncanonicalGrammar()
        {
            BossRewardComputationRequest[] requests =
            {
                BossRewardTestFixtures.Request(gameId: "AnotherLife"),
                BossRewardTestFixtures.Request(catalogSetId: "catalog_set_한"),
                BossRewardTestFixtures.Request(bossDefinitionId: "boss-unknown"),
                BossRewardTestFixtures.Request(bossDefinitionId: "1_boss"),
                BossRewardTestFixtures.Request(
                    rewardProfileId: "reward_profile_한")
            };

            foreach (BossRewardComputationRequest request in requests)
            {
                BossRewardComputationResult result = BossRewardComputation.Compute(
                    request,
                    BossRewardTestFixtures.Catalog());

                Assert.AreEqual(
                    BossRewardComputationStatus.InvalidRequest,
                    result.Status);
                Assert.IsTrue(result.Diagnostics.Any(item =>
                    item.Code == "AL-BOSS-REWARD-REQUEST-ID-INVALID"));
            }
        }

        [Test]
        public void OpaqueAuthorityIdentitiesAcceptExistingAndMultibyteForms()
        {
            const string profileGuidN =
                "0f47ac10b58cc4372a5670e02b2c3d479";
            const string encounterId = "c1-encounter-01";
            const string completionId = "완료-1";
            const string resultId = completionId + ":boss_reward";

            BossRewardComputationResult result = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(
                    profileId: profileGuidN,
                    encounterId: encounterId,
                    encounterCompletionId: completionId,
                    rewardResultId: resultId),
                BossRewardTestFixtures.Catalog());

            Assert.AreEqual(BossRewardComputationStatus.Computed, result.Status);
            Assert.AreEqual(profileGuidN, result.Value.ProfileId);
            Assert.AreEqual(encounterId, result.Value.EncounterId);
            Assert.AreEqual(completionId, result.Value.EncounterCompletionId);
            Assert.AreEqual(resultId, result.Value.RewardResultId);
        }

        [Test]
        public void OpaqueAuthorityIdentityLengthUsesDedicatedUtf8Ceiling()
        {
            string maximum = new string(
                'a',
                BossRewardTechnicalLimits.MaximumOpaqueIdentifierUtf8Bytes);
            string oversized = maximum + "a";

            BossRewardComputationResult accepted = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(rewardResultId: maximum),
                BossRewardTestFixtures.Catalog());
            BossRewardComputationResult rejected = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(rewardResultId: oversized),
                BossRewardTestFixtures.Catalog());

            Assert.AreEqual(
                BossRewardComputationStatus.Computed,
                accepted.Status);
            Assert.AreEqual(
                BossRewardComputationStatus.InvalidRequest,
                rejected.Status);
            Assert.IsTrue(rejected.Diagnostics.Any(item =>
                item.Code == "AL-BOSS-REWARD-REQUEST-ID-INVALID"));
        }

        [Test]
        public void OpaqueAuthorityIdentityRejectsMalformedUtf16()
        {
            BossRewardComputationResult result = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(encounterId: "\ud800"),
                BossRewardTestFixtures.Catalog());

            Assert.AreEqual(
                BossRewardComputationStatus.InvalidRequest,
                result.Status);
            Assert.IsTrue(result.Diagnostics.Any(item =>
                item.Code == "AL-BOSS-REWARD-REQUEST-ID-INVALID"));
        }

        [Test]
        public void CatalogBossProfileAndEquipmentIdsRejectNoncanonicalRecords()
        {
            BossRewardCatalogSnapshot source = BossRewardTestFixtures.Catalog();
            var invalidCatalogIdentity = new BossRewardCatalogSnapshot(
                source.GameId,
                "catalog_set_한",
                source.SchemaVersion,
                source.Revision,
                source.Bindings,
                source.Profiles,
                source.EquipmentDefinitions,
                source.AnnouncementPolicyIds);
            var invalidBinding = new BossRewardBinding(
                "boss_한",
                BossRewardTestFixtures.BossVersion,
                BossRewardTestFixtures.RewardProfileId,
                BossRewardTestFixtures.RewardProfileVersion);
            BossRewardProfile validProfile = BossRewardTestFixtures.Profile();
            var invalidProfile = new BossRewardProfile(
                BossRewardTestFixtures.GameId,
                BossRewardTestFixtures.CatalogSetId,
                "reward_profile_한",
                BossRewardTestFixtures.SchemaVersion,
                BossRewardTestFixtures.RewardProfileVersion,
                0,
                true,
                Array.Empty<BossRewardEntry>(),
                "source_revision_1",
                BossRewardTestFixtures.ShaA);
            BossEquipmentDefinitionSnapshot validEquipment =
                BossRewardTestFixtures.Equipment();
            var invalidEquipment = new BossEquipmentDefinitionSnapshot(
                "equipment_한",
                validEquipment.SchemaVersion,
                validEquipment.ContentVersion,
                validEquipment.SlotId,
                validEquipment.AttackBonus,
                validEquipment.DefenseBonus,
                validEquipment.HealthBonus,
                validEquipment.StackPolicyId,
                validEquipment.AcquisitionSnapshotPolicyId,
                validEquipment.PresentationContentKey,
                validEquipment.SourceRevision,
                validEquipment.RawSha256);

            BossRewardComputationResult catalogResult =
                BossRewardComputation.Compute(
                    BossRewardTestFixtures.Request(),
                    invalidCatalogIdentity);
            BossRewardComputationResult bindingResult =
                BossRewardComputation.Compute(
                    BossRewardTestFixtures.Request(),
                    BossRewardTestFixtures.Catalog(
                        bindings: new[]
                        {
                            invalidBinding,
                            source.Bindings[0]
                        }));
            BossRewardComputationResult profileResult =
                BossRewardComputation.Compute(
                    BossRewardTestFixtures.Request(),
                    new BossRewardCatalogSnapshot(
                        source.GameId,
                        source.CatalogSetId,
                        source.SchemaVersion,
                        source.Revision,
                        source.Bindings,
                        new[] { invalidProfile, validProfile },
                        source.EquipmentDefinitions,
                        source.AnnouncementPolicyIds));
            BossRewardComputationResult equipmentResult =
                BossRewardComputation.Compute(
                    BossRewardTestFixtures.Request(),
                    BossRewardTestFixtures.Catalog(
                        equipment: new[]
                        {
                            invalidEquipment,
                            validEquipment,
                            BossRewardTestFixtures.Equipment(
                                BossRewardTestFixtures.BetaId,
                                "equipment_v1",
                                5,
                                6,
                                7,
                                BossRewardTestFixtures.ShaA)
                        }));

            Assert.AreEqual(
                BossRewardComputationStatus.CatalogUnavailable,
                catalogResult.Status);
            Assert.IsTrue(bindingResult.Diagnostics.Any(item =>
                item.Code == "AL-BOSS-REWARD-CATALOG-BINDING-ID-INVALID"));
            Assert.IsTrue(profileResult.Diagnostics.Any(item =>
                item.Code == "AL-BOSS-REWARD-CATALOG-PROFILE-ID-INVALID"));
            Assert.IsTrue(equipmentResult.Diagnostics.Any(item =>
                item.Code == "AL-BOSS-REWARD-CATALOG-EQUIPMENT-ID-INVALID"));
            Assert.IsFalse(bindingResult.IsSuccess);
            Assert.IsFalse(profileResult.IsSuccess);
            Assert.IsFalse(equipmentResult.IsSuccess);
        }

        [Test]
        public void CoherentFutureRewardSchemaIsRejectedBeforeInterpretation()
        {
            const string futureSchema = "boss_reward_schema_v9";
            var profile = new BossRewardProfile(
                BossRewardTestFixtures.GameId,
                BossRewardTestFixtures.CatalogSetId,
                BossRewardTestFixtures.RewardProfileId,
                futureSchema,
                BossRewardTestFixtures.RewardProfileVersion,
                25,
                false,
                new[]
                {
                    new BossRewardEntry(
                        BossRewardTestFixtures.AlphaId,
                        BossRewardTechnicalLimits.MicrosPerUnit,
                        1,
                        BossRewardTestFixtures.ItemPolicyId)
                },
                "source_revision_1",
                BossRewardTestFixtures.ShaA);
            BossEquipmentDefinitionSnapshot source =
                BossRewardTestFixtures.Equipment();
            var equipment = new BossEquipmentDefinitionSnapshot(
                source.EquipmentDefinitionId,
                futureSchema,
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
            var catalog = new BossRewardCatalogSnapshot(
                BossRewardTestFixtures.GameId,
                BossRewardTestFixtures.CatalogSetId,
                futureSchema,
                "catalog_revision_1",
                new[]
                {
                    new BossRewardBinding(
                        BossRewardTestFixtures.BossId,
                        BossRewardTestFixtures.BossVersion,
                        BossRewardTestFixtures.RewardProfileId,
                        BossRewardTestFixtures.RewardProfileVersion)
                },
                new[] { profile },
                new[] { equipment },
                new[] { BossRewardTestFixtures.ItemPolicyId });

            BossRewardComputationResult result = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                catalog);

            Assert.AreEqual(
                BossRewardComputationStatus.UnsupportedVersion,
                result.Status);
            Assert.AreEqual(
                "AL-BOSS-REWARD-CATALOG-SCHEMA-UNSUPPORTED",
                result.Diagnostics[0].Code);
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
        public void UnsupportedAcquisitionSnapshotPolicyIsRejected()
        {
            BossEquipmentDefinitionSnapshot source =
                BossRewardTestFixtures.Equipment();
            var unsupported = new BossEquipmentDefinitionSnapshot(
                source.EquipmentDefinitionId,
                source.SchemaVersion,
                source.ContentVersion,
                source.SlotId,
                source.AttackBonus,
                source.DefenseBonus,
                source.HealthBonus,
                source.StackPolicyId,
                "acquisition_snapshot_v2",
                source.PresentationContentKey,
                source.SourceRevision,
                source.RawSha256);

            BossRewardComputationResult result = BossRewardComputation.Compute(
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

            Assert.AreEqual(
                BossRewardComputationStatus.InvalidEquipmentDefinition,
                result.Status);
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
        public void DuplicateBindingDiagnosticsIgnoreOppositeEndPermutation()
        {
            BossRewardCatalogSnapshot source = BossRewardTestFixtures.Catalog();
            BossRewardBinding matching = source.Bindings[0];
            var invalidZ = new BossRewardBinding(
                "boss-bad-z",
                BossRewardTestFixtures.BossVersion,
                BossRewardTestFixtures.RewardProfileId,
                BossRewardTestFixtures.RewardProfileVersion);
            var invalidA = new BossRewardBinding(
                "boss-bad-a",
                BossRewardTestFixtures.BossVersion,
                BossRewardTestFixtures.RewardProfileId,
                BossRewardTestFixtures.RewardProfileVersion);
            var forwardBindings = new BossRewardBinding[]
            {
                matching,
                matching,
                invalidZ,
                null,
                invalidA,
                matching
            };
            BossRewardBinding[] reverseBindings =
                forwardBindings.Reverse().ToArray();

            BossRewardComputationResult forward = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                CatalogWithCollections(source, bindings: forwardBindings));
            BossRewardComputationResult reverse = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                CatalogWithCollections(source, bindings: reverseBindings));

            Assert.AreEqual(
                BossRewardComputationStatus.BossRewardBindingMismatch,
                forward.Status);
            Assert.AreEqual(forward.Status, reverse.Status);
            Assert.AreEqual(5, forward.Diagnostics.Count);
            Assert.AreEqual(
                2,
                forward.Diagnostics.Count(item =>
                    item.Code ==
                    "AL-BOSS-REWARD-CATALOG-BINDING-DUPLICATE"));
            Assert.AreEqual(
                2,
                forward.Diagnostics.Count(item =>
                    item.Code ==
                    "AL-BOSS-REWARD-CATALOG-BINDING-ID-INVALID"));
            Assert.AreEqual(
                1,
                forward.Diagnostics.Count(item =>
                    item.Code == "AL-BOSS-REWARD-CATALOG-BINDING-NULL"));
            CollectionAssert.AreEqual(
                forward.Diagnostics.Select(DiagnosticIdentity).ToArray(),
                reverse.Diagnostics.Select(DiagnosticIdentity).ToArray());
        }

        [Test]
        public void DuplicateProfileDiagnosticsIgnoreOppositeEndPermutation()
        {
            BossRewardCatalogSnapshot source = BossRewardTestFixtures.Catalog();
            BossRewardProfile matching = source.Profiles[0];
            BossRewardProfile invalidZ =
                BossRewardTestFixtures.Profile(id: "reward-profile-z");
            BossRewardProfile invalidA =
                BossRewardTestFixtures.Profile(id: "reward-profile-a");
            var forwardProfiles = new BossRewardProfile[]
            {
                matching,
                matching,
                invalidZ,
                null,
                invalidA,
                matching
            };
            BossRewardProfile[] reverseProfiles =
                forwardProfiles.Reverse().ToArray();

            BossRewardComputationResult forward = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                CatalogWithCollections(source, profiles: forwardProfiles));
            BossRewardComputationResult reverse = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                CatalogWithCollections(source, profiles: reverseProfiles));

            Assert.AreEqual(
                BossRewardComputationStatus.InvalidRewardProfile,
                forward.Status);
            Assert.AreEqual(forward.Status, reverse.Status);
            Assert.AreEqual(5, forward.Diagnostics.Count);
            Assert.AreEqual(
                2,
                forward.Diagnostics.Count(item =>
                    item.Code ==
                    "AL-BOSS-REWARD-CATALOG-PROFILE-DUPLICATE"));
            Assert.AreEqual(
                2,
                forward.Diagnostics.Count(item =>
                    item.Code ==
                    "AL-BOSS-REWARD-CATALOG-PROFILE-ID-INVALID"));
            Assert.AreEqual(
                1,
                forward.Diagnostics.Count(item =>
                    item.Code == "AL-BOSS-REWARD-CATALOG-PROFILE-NULL"));
            CollectionAssert.AreEqual(
                forward.Diagnostics.Select(DiagnosticIdentity).ToArray(),
                reverse.Diagnostics.Select(DiagnosticIdentity).ToArray());
        }

        [Test]
        public void MaximumDefinitionsAndEntriesResolveThroughSingleIndex()
        {
            var definitions =
                new List<BossEquipmentDefinitionSnapshot>(
                    BossRewardTechnicalLimits.MaximumCatalogEntries);
            var entries =
                new List<BossRewardEntry>(
                    BossRewardTechnicalLimits.MaximumRewardEntries);
            for (int index = 0;
                 index < BossRewardTechnicalLimits.MaximumCatalogEntries;
                 index++)
            {
                string id = "equipment_bound_" +
                            index.ToString("D4", CultureInfo.InvariantCulture);
                definitions.Add(BossRewardTestFixtures.Equipment(id));
                if (index < BossRewardTechnicalLimits.MaximumRewardEntries)
                {
                    entries.Add(new BossRewardEntry(
                        id,
                        index == 0
                            ? BossRewardTechnicalLimits.MicrosPerUnit
                            : 0,
                        1,
                        BossRewardTestFixtures.ItemPolicyId));
                }
            }
            BossRewardProfile profile =
                BossRewardTestFixtures.Profile(entries: entries);

            BossRewardComputationResult result = BossRewardComputation.Compute(
                BossRewardTestFixtures.Request(),
                BossRewardTestFixtures.Catalog(
                    profile,
                    equipment: definitions));

            Assert.AreEqual(BossRewardComputationStatus.Computed, result.Status);
            Assert.AreEqual(1, result.Value.Drops.Count);
            Assert.AreEqual(
                "equipment_bound_0000",
                result.Value.Drops[0].EquipmentDefinitionId);
            Assert.IsEmpty(result.Diagnostics);
        }

        [Test]
        public void MalformedMaximumCatalogDiagnosticsAreBoundedAndPermutationInvariant()
        {
            var definitions =
                new List<BossEquipmentDefinitionSnapshot>(
                    BossRewardTechnicalLimits.MaximumCatalogEntries);
            for (int index = 0;
                 index < BossRewardTechnicalLimits.MaximumCatalogEntries;
                 index++)
            {
                definitions.Add(BossRewardTestFixtures.Equipment(
                    "equipment-bad-" +
                    index.ToString("D4", CultureInfo.InvariantCulture)));
            }
            int materialized = 0;
            int forwardMaterializations;
            int reverseMaterializations;
            BossRewardComputationResult forward;
            BossRewardComputationResult reverse;
            Action previousMaterializationObserver =
                BossRewardDiagnostic.MaterializedForTesting;
            BossRewardDiagnostic.MaterializedForTesting = () => materialized++;
            try
            {
                forward = BossRewardComputation.Compute(
                    BossRewardTestFixtures.Request(),
                    BossRewardTestFixtures.Catalog(equipment: definitions));
                forwardMaterializations = materialized;
                materialized = 0;
                definitions.Reverse();
                reverse = BossRewardComputation.Compute(
                    BossRewardTestFixtures.Request(),
                    BossRewardTestFixtures.Catalog(equipment: definitions));
                reverseMaterializations = materialized;
            }
            finally
            {
                BossRewardDiagnostic.MaterializedForTesting =
                    previousMaterializationObserver;
            }

            Assert.AreEqual(
                BossRewardComputationStatus.InvalidEquipmentDefinition,
                forward.Status);
            Assert.AreEqual(
                BossRewardTechnicalLimits.MaximumDiagnostics,
                forwardMaterializations);
            Assert.AreEqual(
                BossRewardTechnicalLimits.MaximumDiagnostics,
                reverseMaterializations);
            Assert.AreEqual(
                BossRewardTechnicalLimits.MaximumDiagnostics - 1,
                forward.Diagnostics.Count(item =>
                    item.Code ==
                    "AL-BOSS-REWARD-CATALOG-EQUIPMENT-ID-INVALID"));
            Assert.AreEqual(
                BossRewardTechnicalLimits.MaximumDiagnostics - 1,
                reverse.Diagnostics.Count(item =>
                    item.Code ==
                    "AL-BOSS-REWARD-CATALOG-EQUIPMENT-ID-INVALID"));
            Assert.AreEqual(
                BossRewardTechnicalLimits.MaximumDiagnostics,
                forward.Diagnostics.Count);
            Assert.AreEqual(
                1,
                forward.Diagnostics.Count(item =>
                    item.Code ==
                    "AL-BOSS-REWARD-TRANSACTION-DIAGNOSTIC-LIMIT"));
            CollectionAssert.AreEqual(
                forward.Diagnostics.Select(DiagnosticIdentity).ToArray(),
                reverse.Diagnostics.Select(DiagnosticIdentity).ToArray());
        }

        [Test]
        public void MaximumBindingDiagnosticsScanPastDuplicateAndRemainCanonical()
        {
            BossRewardCatalogSnapshot source = BossRewardTestFixtures.Catalog();
            BossRewardBinding matching = source.Bindings[0];
            var bindings = new List<BossRewardBinding>(
                BossRewardTechnicalLimits.MaximumCatalogEntries)
            {
                matching,
                matching
            };
            for (int index = 0;
                 index <
                 BossRewardTechnicalLimits.MaximumCatalogEntries - 2;
                 index++)
            {
                bindings.Add(new BossRewardBinding(
                    "boss-bad-" +
                    index.ToString("D4", CultureInfo.InvariantCulture),
                    BossRewardTestFixtures.BossVersion,
                    BossRewardTestFixtures.RewardProfileId,
                    BossRewardTestFixtures.RewardProfileVersion));
            }

            int materialized = 0;
            int forwardMaterializations;
            int reverseMaterializations;
            BossRewardComputationResult forward;
            BossRewardComputationResult reverse;
            Action previousMaterializationObserver =
                BossRewardDiagnostic.MaterializedForTesting;
            BossRewardDiagnostic.MaterializedForTesting = () => materialized++;
            try
            {
                forward = BossRewardComputation.Compute(
                    BossRewardTestFixtures.Request(),
                    CatalogWithCollections(source, bindings: bindings));
                forwardMaterializations = materialized;
                materialized = 0;
                bindings.Reverse();
                reverse = BossRewardComputation.Compute(
                    BossRewardTestFixtures.Request(),
                    CatalogWithCollections(source, bindings: bindings));
                reverseMaterializations = materialized;
            }
            finally
            {
                BossRewardDiagnostic.MaterializedForTesting =
                    previousMaterializationObserver;
            }

            Assert.AreEqual(
                BossRewardComputationStatus.BossRewardBindingMismatch,
                forward.Status);
            Assert.AreEqual(forward.Status, reverse.Status);
            Assert.AreEqual(
                BossRewardTechnicalLimits.MaximumDiagnostics,
                forwardMaterializations);
            Assert.AreEqual(
                BossRewardTechnicalLimits.MaximumDiagnostics,
                reverseMaterializations);
            Assert.AreEqual(
                BossRewardTechnicalLimits.MaximumDiagnostics,
                forward.Diagnostics.Count);
            Assert.AreEqual(
                1,
                forward.Diagnostics.Count(item =>
                    item.Code ==
                    "AL-BOSS-REWARD-TRANSACTION-DIAGNOSTIC-LIMIT"));
            Assert.AreEqual(
                1,
                reverse.Diagnostics.Count(item =>
                    item.Code ==
                    "AL-BOSS-REWARD-TRANSACTION-DIAGNOSTIC-LIMIT"));
            Assert.AreEqual(
                1,
                forward.Diagnostics.Count(item =>
                    item.Code ==
                    "AL-BOSS-REWARD-CATALOG-BINDING-DUPLICATE"));
            string[] expectedRetainedInvalidIds = Enumerable
                .Range(
                    0,
                    BossRewardTechnicalLimits.MaximumDiagnostics - 2)
                .Select(index =>
                    "boss-bad-" +
                    index.ToString("D4", CultureInfo.InvariantCulture))
                .ToArray();
            CollectionAssert.AreEqual(
                expectedRetainedInvalidIds,
                forward.Diagnostics
                    .Where(item =>
                        item.Code ==
                        "AL-BOSS-REWARD-CATALOG-BINDING-ID-INVALID")
                    .Select(item => item.RecordId)
                    .ToArray());
            CollectionAssert.AreEqual(
                forward.Diagnostics.Select(DiagnosticIdentity).ToArray(),
                reverse.Diagnostics.Select(DiagnosticIdentity).ToArray());
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

        private static BossRewardCatalogSnapshot CatalogWithCollections(
            BossRewardCatalogSnapshot source,
            IEnumerable<BossRewardBinding> bindings = null,
            IEnumerable<BossRewardProfile> profiles = null)
        {
            return new BossRewardCatalogSnapshot(
                source.GameId,
                source.CatalogSetId,
                source.SchemaVersion,
                source.Revision,
                bindings ?? source.Bindings,
                profiles ?? source.Profiles,
                source.EquipmentDefinitions,
                source.AnnouncementPolicyIds);
        }

        private static string DiagnosticIdentity(
            BossRewardDiagnostic diagnostic)
        {
            return string.Join(
                "|",
                ((int)diagnostic.Severity).ToString(
                    CultureInfo.InvariantCulture),
                diagnostic.Code,
                diagnostic.RecordId,
                diagnostic.FieldPath,
                ((int)diagnostic.Domain).ToString(
                    CultureInfo.InvariantCulture),
                diagnostic.OperationId,
                diagnostic.BlocksOperation ? "1" : "0",
                diagnostic.SchemaVersion,
                diagnostic.ContentVersion,
                diagnostic.SafeDeveloperMessage);
        }
    }
}
