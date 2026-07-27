using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.Core.BossRewards
{
    public static class BossRewardDeterministicRoll
    {
        public const ulong Scale = 4_294_967_296UL;

        public static byte[] BuildCanonicalInput(
            string determinismVersion,
            string catalogSetId,
            string rewardResultId,
            string encounterCompletionId,
            string bossDefinitionId,
            string rewardProfileId,
            string rewardProfileContentVersion,
            string equipmentDefinitionId)
        {
            using (var writer = new BossRewardCanonicalWriter())
            {
                writer.WriteString(determinismVersion);
                writer.WriteString(catalogSetId);
                writer.WriteString(rewardResultId);
                writer.WriteString(encounterCompletionId);
                writer.WriteString(bossDefinitionId);
                writer.WriteString(rewardProfileId);
                writer.WriteString(rewardProfileContentVersion);
                writer.WriteString(equipmentDefinitionId);
                return writer.ToArray();
            }
        }

        public static byte[] ComputeDigest(byte[] canonicalInput)
        {
            if (canonicalInput == null) throw new ArgumentNullException(nameof(canonicalInput));
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(canonicalInput);
            }
        }

        public static uint ReadBigEndianDraw(byte[] digest)
        {
            if (digest == null || digest.Length < 4)
                throw new ArgumentException("At least four digest bytes are required.", nameof(digest));
            return ((uint)digest[0] << 24) |
                   ((uint)digest[1] << 16) |
                   ((uint)digest[2] << 8) |
                   digest[3];
        }

        public static ulong ComputeThresholdExclusive(int dropChanceMicros)
        {
            if (dropChanceMicros < 0 ||
                dropChanceMicros > BossRewardTechnicalLimits.MicrosPerUnit)
                throw new ArgumentOutOfRangeException(nameof(dropChanceMicros));
            return checked((ulong)dropChanceMicros * Scale) /
                   BossRewardTechnicalLimits.MicrosPerUnit;
        }

        public static bool IsHit(uint draw, int dropChanceMicros)
        {
            if (dropChanceMicros == 0) return false;
            if (dropChanceMicros == BossRewardTechnicalLimits.MicrosPerUnit) return true;
            return draw < ComputeThresholdExclusive(dropChanceMicros);
        }

        public static uint ComputeDraw(
            BossRewardComputationRequest request,
            string equipmentDefinitionId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            byte[] canonical = BuildCanonicalInput(
                request.DeterminismVersion,
                request.CatalogSetId,
                request.RewardResultId,
                request.EncounterCompletionId,
                request.BossDefinitionId,
                request.RewardProfileId,
                request.RewardProfileContentVersion,
                equipmentDefinitionId);
            return ReadBigEndianDraw(ComputeDigest(canonical));
        }

        public static string ToLowerHex(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
                builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    public static class BossRewardComputation
    {
        public static BossRewardComputationResult Compute(
            BossRewardComputationRequest request,
            BossRewardCatalogSnapshot catalog)
        {
            var diagnostics = new List<BossRewardDiagnostic>();
            ValidateRequest(request, diagnostics);
            if (diagnostics.Count > 0)
                return Failure(BossRewardComputationStatus.InvalidRequest, diagnostics);

            if (catalog == null)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-REWARD-CATALOG-UNAVAILABLE",
                    BossRewardDiagnosticDomain.Catalog,
                    "catalog",
                    request.RewardResultId,
                    string.Empty,
                    "The immutable boss-reward catalog snapshot is unavailable."));
                return Failure(BossRewardComputationStatus.CatalogUnavailable, diagnostics);
            }

            if (!BossRewardText.IsBoundedTechnicalId(catalog.GameId) ||
                !BossRewardText.IsBoundedTechnicalId(catalog.CatalogSetId) ||
                !BossRewardText.IsBoundedVersion(catalog.SchemaVersion) ||
                !BossRewardText.IsBoundedTechnicalId(catalog.Revision) ||
                !string.Equals(request.GameId, catalog.GameId, StringComparison.Ordinal) ||
                !string.Equals(request.CatalogSetId, catalog.CatalogSetId, StringComparison.Ordinal))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-REWARD-CATALOG-IDENTITY-MISMATCH",
                    BossRewardDiagnosticDomain.Catalog,
                    "catalog",
                    request.RewardResultId,
                    catalog.CatalogSetId,
                    "The catalog identity does not match the computation request."));
                return Failure(BossRewardComputationStatus.CatalogUnavailable, diagnostics);
            }

            if (!string.Equals(
                    request.DeterminismVersion,
                    BossRewardTechnicalLimits.SupportedDeterminismVersion,
                    StringComparison.Ordinal))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-REWARD-DETERMINISM-VERSION-UNSUPPORTED",
                    BossRewardDiagnosticDomain.Determinism,
                    "request.determinismVersion",
                    request.RewardResultId,
                    request.DeterminismVersion,
                    "The requested deterministic roll version is unsupported."));
                return Failure(BossRewardComputationStatus.UnsupportedVersion, diagnostics);
            }

            BossRewardBinding binding = ResolveUniqueBinding(
                catalog.Bindings,
                request.BossDefinitionId,
                diagnostics,
                request.RewardResultId);
            if (binding == null)
            {
                BossRewardComputationStatus status = diagnostics.Count == 0
                    ? BossRewardComputationStatus.UnknownBoss
                    : BossRewardComputationStatus.BossRewardBindingMismatch;
                if (diagnostics.Count == 0)
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-BOSS-UNKNOWN",
                        BossRewardDiagnosticDomain.Catalog,
                        "request.bossDefinitionId",
                        request.RewardResultId,
                        request.BossDefinitionId,
                        "The boss definition is not bound to a reward profile."));
                return Failure(status, diagnostics);
            }

            if (!string.Equals(
                    binding.BossDefinitionContentVersion,
                    request.BossDefinitionContentVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(binding.RewardProfileId, request.RewardProfileId, StringComparison.Ordinal) ||
                !string.Equals(
                    binding.RewardProfileContentVersion,
                    request.RewardProfileContentVersion,
                    StringComparison.Ordinal))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-REWARD-CATALOG-BINDING-MISMATCH",
                    BossRewardDiagnosticDomain.Catalog,
                    "request.rewardProfileId",
                    request.RewardResultId,
                    request.RewardProfileId,
                    "The request does not match the immutable boss reward binding."));
                return Failure(BossRewardComputationStatus.BossRewardBindingMismatch, diagnostics);
            }

            BossRewardProfile profile = ResolveUniqueProfile(
                catalog.Profiles,
                request.RewardProfileId,
                request.RewardProfileContentVersion,
                diagnostics,
                request.RewardResultId);
            if (profile == null)
            {
                if (diagnostics.Count == 0)
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-PROFILE-UNKNOWN",
                        BossRewardDiagnosticDomain.Catalog,
                        "request.rewardProfileId",
                        request.RewardResultId,
                        request.RewardProfileId,
                        "The requested reward profile is unavailable."));
                return Failure(
                    diagnostics.Any(item => item.Code.EndsWith("DUPLICATE", StringComparison.Ordinal))
                        ? BossRewardComputationStatus.InvalidRewardProfile
                        : BossRewardComputationStatus.UnknownRewardProfile,
                    diagnostics);
            }

            Dictionary<string, BossEquipmentDefinitionSnapshot> equipmentById =
                ValidateProfileAndDefinitions(request, catalog, profile, diagnostics);
            if (diagnostics.Count > 0)
            {
                BossRewardComputationStatus status = diagnostics.Any(item =>
                    item.Code.StartsWith(
                        "AL-BOSS-REWARD-CATALOG-EQUIPMENT",
                        StringComparison.Ordinal))
                    ? BossRewardComputationStatus.InvalidEquipmentDefinition
                    : BossRewardComputationStatus.InvalidRewardProfile;
                return Failure(status, diagnostics);
            }

            try
            {
                BossRewardEntry[] orderedEntries = profile.Entries
                    .OrderBy(entry => entry.EquipmentDefinitionId, StringComparer.Ordinal)
                    .ToArray();
                var drops = new List<BossRewardComputedDrop>(orderedEntries.Length);
                for (int index = 0; index < orderedEntries.Length; index++)
                {
                    BossRewardEntry entry = orderedEntries[index];
                    uint draw = BossRewardDeterministicRoll.ComputeDraw(
                        request,
                        entry.EquipmentDefinitionId);
                    if (!BossRewardDeterministicRoll.IsHit(draw, entry.DropChanceMicros))
                        continue;

                    BossEquipmentDefinitionSnapshot definition =
                        equipmentById[entry.EquipmentDefinitionId];
                    drops.Add(new BossRewardComputedDrop(
                        definition.EquipmentDefinitionId,
                        definition.ContentVersion,
                        ComputeAcquisitionSnapshotFingerprint(definition),
                        definition.SlotId,
                        definition.AttackBonus,
                        definition.DefenseBonus,
                        definition.HealthBonus,
                        entry.Quantity,
                        definition.StackPolicyId,
                        entry.AcquisitionAnnouncementPolicyId));
                }

                string computationHash = ComputeComputationHash(
                    request,
                    profile,
                    drops);
                var value = new BossRewardComputedValue(
                    request.GameId,
                    request.CatalogSetId,
                    request.ProfileId,
                    request.RewardResultId,
                    request.EncounterId,
                    request.EncounterCompletionId,
                    request.BossDefinitionId,
                    request.BossDefinitionContentVersion,
                    profile.Id,
                    profile.ContentVersion,
                    profile.RawSha256,
                    profile.WarzoneCredits,
                    profile.IsExplicitNoReward,
                    drops,
                    request.DeterminismVersion,
                    computationHash);
                return new BossRewardComputationResult(
                    profile.IsExplicitNoReward
                        ? BossRewardComputationStatus.ExplicitNoReward
                        : BossRewardComputationStatus.Computed,
                    value,
                    Array.Empty<BossRewardDiagnostic>());
            }
            catch (Exception exception) when (
                exception is CryptographicException ||
                exception is IOException ||
                exception is OverflowException ||
                exception is EncoderFallbackException)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-REWARD-DETERMINISM-FAILED",
                    BossRewardDiagnosticDomain.Determinism,
                    "computation",
                    request.RewardResultId,
                    request.BossDefinitionId,
                    "The deterministic reward computation could not be completed."));
                return Failure(BossRewardComputationStatus.DeterminismFailure, diagnostics);
            }
        }

        public static string ComputeAcquisitionSnapshotFingerprint(
            BossEquipmentDefinitionSnapshot definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            using (var writer = new BossRewardCanonicalWriter())
            {
                writer.WriteString("boss_equipment_snapshot_v1");
                writer.WriteString(definition.EquipmentDefinitionId);
                writer.WriteString(definition.SchemaVersion);
                writer.WriteString(definition.ContentVersion);
                writer.WriteString(definition.SlotId);
                writer.WriteInt32(definition.AttackBonus);
                writer.WriteInt32(definition.DefenseBonus);
                writer.WriteInt32(definition.HealthBonus);
                writer.WriteString(definition.StackPolicyId);
                writer.WriteString(definition.AcquisitionSnapshotPolicyId);
                return Hash(writer.ToArray());
            }
        }

        public static string ComputeComputationHash(
            BossRewardComputationRequest request,
            BossRewardProfile profile,
            IEnumerable<BossRewardComputedDrop> drops)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            BossRewardComputedDrop[] orderedDrops = (drops ?? Array.Empty<BossRewardComputedDrop>())
                .OrderBy(drop => drop.EquipmentDefinitionId, StringComparer.Ordinal)
                .ToArray();
            using (var writer = new BossRewardCanonicalWriter())
            {
                writer.WriteString("boss_reward_computed_value_v1");
                writer.WriteString(request.GameId);
                writer.WriteString(request.CatalogSetId);
                writer.WriteString(request.ProfileId);
                writer.WriteString(request.RewardResultId);
                writer.WriteString(request.EncounterId);
                writer.WriteString(request.EncounterCompletionId);
                writer.WriteString(request.BossDefinitionId);
                writer.WriteString(request.BossDefinitionContentVersion);
                writer.WriteString(profile.Id);
                writer.WriteString(profile.ContentVersion);
                writer.WriteString(profile.RawSha256);
                writer.WriteInt32(profile.WarzoneCredits);
                writer.WriteBoolean(profile.IsExplicitNoReward);
                writer.WriteString(request.DeterminismVersion);
                writer.WriteUInt32((uint)orderedDrops.Length);
                for (int index = 0; index < orderedDrops.Length; index++)
                {
                    BossRewardComputedDrop drop = orderedDrops[index];
                    writer.WriteString(drop.EquipmentDefinitionId);
                    writer.WriteString(drop.EquipmentDefinitionContentVersion);
                    writer.WriteString(drop.AcquisitionSnapshotFingerprint);
                    writer.WriteString(drop.SlotId);
                    writer.WriteInt32(drop.AttackBonus);
                    writer.WriteInt32(drop.DefenseBonus);
                    writer.WriteInt32(drop.HealthBonus);
                    writer.WriteInt32(drop.Quantity);
                    writer.WriteString(drop.StackPolicyId);
                    writer.WriteString(drop.AcquisitionAnnouncementPolicyId);
                }
                writer.WriteUInt32(0);
                return Hash(writer.ToArray());
            }
        }

        public static string RecomputeComputationHash(BossRewardComputedValue value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            BossRewardComputedDrop[] orderedDrops = value.Drops
                .OrderBy(drop => drop.EquipmentDefinitionId, StringComparer.Ordinal)
                .ToArray();
            using (var writer = new BossRewardCanonicalWriter())
            {
                writer.WriteString("boss_reward_computed_value_v1");
                writer.WriteString(value.GameId);
                writer.WriteString(value.CatalogSetId);
                writer.WriteString(value.ProfileId);
                writer.WriteString(value.RewardResultId);
                writer.WriteString(value.EncounterId);
                writer.WriteString(value.EncounterCompletionId);
                writer.WriteString(value.BossDefinitionId);
                writer.WriteString(value.BossDefinitionContentVersion);
                writer.WriteString(value.RewardProfileId);
                writer.WriteString(value.RewardProfileContentVersion);
                writer.WriteString(value.RewardProfileSha256);
                writer.WriteInt32(value.WarzoneCredits);
                writer.WriteBoolean(value.IsExplicitNoReward);
                writer.WriteString(value.DeterminismVersion);
                writer.WriteUInt32((uint)orderedDrops.Length);
                for (int index = 0; index < orderedDrops.Length; index++)
                {
                    BossRewardComputedDrop drop = orderedDrops[index];
                    writer.WriteString(drop.EquipmentDefinitionId);
                    writer.WriteString(drop.EquipmentDefinitionContentVersion);
                    writer.WriteString(drop.AcquisitionSnapshotFingerprint);
                    writer.WriteString(drop.SlotId);
                    writer.WriteInt32(drop.AttackBonus);
                    writer.WriteInt32(drop.DefenseBonus);
                    writer.WriteInt32(drop.HealthBonus);
                    writer.WriteInt32(drop.Quantity);
                    writer.WriteString(drop.StackPolicyId);
                    writer.WriteString(drop.AcquisitionAnnouncementPolicyId);
                }
                writer.WriteUInt32(0);
                return Hash(writer.ToArray());
            }
        }

        private static void ValidateRequest(
            BossRewardComputationRequest request,
            ICollection<BossRewardDiagnostic> diagnostics)
        {
            if (request == null)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-REWARD-REQUEST-NULL",
                    BossRewardDiagnosticDomain.Request,
                    "request",
                    string.Empty,
                    string.Empty,
                    "A boss reward computation request is required."));
                return;
            }

            RequireId(request.GameId, "request.gameId", request.RewardResultId, diagnostics);
            RequireId(request.CatalogSetId, "request.catalogSetId", request.RewardResultId, diagnostics);
            RequireId(request.ProfileId, "request.profileId", request.RewardResultId, diagnostics);
            RequireId(request.EncounterId, "request.encounterId", request.RewardResultId, diagnostics);
            RequireId(
                request.EncounterCompletionId,
                "request.encounterCompletionId",
                request.RewardResultId,
                diagnostics);
            RequireId(request.RewardResultId, "request.rewardResultId", request.RewardResultId, diagnostics);
            RequireId(
                request.BossDefinitionId,
                "request.bossDefinitionId",
                request.RewardResultId,
                diagnostics);
            RequireVersion(
                request.BossDefinitionContentVersion,
                "request.bossDefinitionContentVersion",
                request.RewardResultId,
                diagnostics);
            RequireId(
                request.RewardProfileId,
                "request.rewardProfileId",
                request.RewardResultId,
                diagnostics);
            RequireVersion(
                request.RewardProfileContentVersion,
                "request.rewardProfileContentVersion",
                request.RewardResultId,
                diagnostics);
            RequireVersion(
                request.DeterminismVersion,
                "request.determinismVersion",
                request.RewardResultId,
                diagnostics);
        }

        private static BossRewardBinding ResolveUniqueBinding(
            IReadOnlyList<BossRewardBinding> bindings,
            string bossDefinitionId,
            ICollection<BossRewardDiagnostic> diagnostics,
            string operationId)
        {
            BossRewardBinding found = null;
            for (int index = 0; index < bindings.Count; index++)
            {
                BossRewardBinding candidate = bindings[index];
                if (candidate == null)
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-BINDING-NULL",
                        BossRewardDiagnosticDomain.Catalog,
                        "catalog.bindings[" + index + "]",
                        operationId,
                        string.Empty,
                        "The catalog contains a null boss reward binding."));
                    continue;
                }
                if (!string.Equals(
                        candidate.BossDefinitionId,
                        bossDefinitionId,
                        StringComparison.Ordinal))
                    continue;
                if (found != null)
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-BINDING-DUPLICATE",
                        BossRewardDiagnosticDomain.Catalog,
                        "catalog.bindings",
                        operationId,
                        bossDefinitionId,
                        "The catalog contains duplicate boss reward bindings."));
                    return null;
                }
                found = candidate;
            }
            return found;
        }

        private static BossRewardProfile ResolveUniqueProfile(
            IReadOnlyList<BossRewardProfile> profiles,
            string profileId,
            string contentVersion,
            ICollection<BossRewardDiagnostic> diagnostics,
            string operationId)
        {
            BossRewardProfile found = null;
            for (int index = 0; index < profiles.Count; index++)
            {
                BossRewardProfile candidate = profiles[index];
                if (candidate == null)
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-PROFILE-NULL",
                        BossRewardDiagnosticDomain.Catalog,
                        "catalog.profiles[" + index + "]",
                        operationId,
                        string.Empty,
                        "The catalog contains a null reward profile."));
                    continue;
                }
                if (!string.Equals(candidate.Id, profileId, StringComparison.Ordinal) ||
                    !string.Equals(candidate.ContentVersion, contentVersion, StringComparison.Ordinal))
                    continue;
                if (found != null)
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-PROFILE-DUPLICATE",
                        BossRewardDiagnosticDomain.Catalog,
                        "catalog.profiles",
                        operationId,
                        profileId,
                        "The catalog contains duplicate reward profile versions."));
                    return null;
                }
                found = candidate;
            }
            return found;
        }

        private static Dictionary<string, BossEquipmentDefinitionSnapshot>
            ValidateProfileAndDefinitions(
                BossRewardComputationRequest request,
                BossRewardCatalogSnapshot catalog,
                BossRewardProfile profile,
                ICollection<BossRewardDiagnostic> diagnostics)
        {
            if (!BossRewardText.IsBoundedTechnicalId(profile.GameId) ||
                !BossRewardText.IsBoundedTechnicalId(profile.CatalogSetId) ||
                !BossRewardText.IsBoundedTechnicalId(profile.Id) ||
                !BossRewardText.IsBoundedVersion(profile.SchemaVersion) ||
                !BossRewardText.IsBoundedVersion(profile.ContentVersion) ||
                !BossRewardText.IsBoundedTechnicalId(profile.SourceRevision) ||
                !BossRewardText.IsLowerSha256(profile.RawSha256) ||
                !string.Equals(profile.GameId, catalog.GameId, StringComparison.Ordinal) ||
                !string.Equals(profile.CatalogSetId, catalog.CatalogSetId, StringComparison.Ordinal) ||
                !string.Equals(profile.SchemaVersion, catalog.SchemaVersion, StringComparison.Ordinal))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-REWARD-CATALOG-PROFILE-IDENTITY-INVALID",
                    BossRewardDiagnosticDomain.Catalog,
                    "profile",
                    request.RewardResultId,
                    profile.Id,
                    "The reward profile identity, version, source revision, or hash is invalid."));
            }
            if (profile.WarzoneCredits < 0 ||
                profile.WarzoneCredits > BossRewardTechnicalLimits.MaximumWarzoneCredits)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-REWARD-CATALOG-CREDITS-INVALID",
                    BossRewardDiagnosticDomain.Catalog,
                    "profile.warzoneCredits",
                    request.RewardResultId,
                    profile.Id,
                    "The reward profile credit amount is outside the technical range."));
            }
            if (profile.IsExplicitNoReward &&
                (profile.WarzoneCredits != 0 || profile.Entries.Count != 0))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-REWARD-CATALOG-NO-REWARD-CONTRADICTORY",
                    BossRewardDiagnosticDomain.Catalog,
                    "profile.isExplicitNoReward",
                    request.RewardResultId,
                    profile.Id,
                    "An explicit no-reward profile cannot grant credits or items."));
            }
            if (!profile.IsExplicitNoReward &&
                profile.WarzoneCredits == 0 &&
                profile.Entries.Count == 0)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-REWARD-CATALOG-EMPTY-PROFILE-INVALID",
                    BossRewardDiagnosticDomain.Catalog,
                    "profile.entries",
                    request.RewardResultId,
                    profile.Id,
                    "An empty zero-credit profile must explicitly declare no reward."));
            }

            var allowedPolicies = new HashSet<string>(
                catalog.AnnouncementPolicyIds.Where(item => item != null),
                StringComparer.Ordinal);
            var entryIds = new HashSet<string>(StringComparer.Ordinal);
            var equipmentById = new Dictionary<string, BossEquipmentDefinitionSnapshot>(
                StringComparer.Ordinal);
            for (int index = 0; index < profile.Entries.Count; index++)
            {
                BossRewardEntry entry = profile.Entries[index];
                string path = "profile.entries[" + index + "]";
                if (entry == null)
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-ENTRY-NULL",
                        BossRewardDiagnosticDomain.Catalog,
                        path,
                        request.RewardResultId,
                        profile.Id,
                        "The reward profile contains a null entry."));
                    continue;
                }
                if (!BossRewardText.IsBoundedTechnicalId(entry.EquipmentDefinitionId))
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-ENTRY-ID-INVALID",
                        BossRewardDiagnosticDomain.Catalog,
                        path + ".equipmentDefinitionId",
                        request.RewardResultId,
                        entry.EquipmentDefinitionId,
                        "The reward entry equipment identity is invalid."));
                    continue;
                }
                if (!entryIds.Add(entry.EquipmentDefinitionId))
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-ENTRY-DUPLICATE",
                        BossRewardDiagnosticDomain.Catalog,
                        path + ".equipmentDefinitionId",
                        request.RewardResultId,
                        entry.EquipmentDefinitionId,
                        "The reward profile contains a duplicate equipment identity."));
                }
                if (entry.DropChanceMicros < 0 ||
                    entry.DropChanceMicros > BossRewardTechnicalLimits.MicrosPerUnit)
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-CHANCE-INVALID",
                        BossRewardDiagnosticDomain.Catalog,
                        path + ".dropChanceMicros",
                        request.RewardResultId,
                        entry.EquipmentDefinitionId,
                        "The fixed-point drop chance is outside 0..1,000,000."));
                }
                if (entry.Quantity != 1)
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-QUANTITY-UNAPPROVED",
                        BossRewardDiagnosticDomain.Catalog,
                        path + ".quantity",
                        request.RewardResultId,
                        entry.EquipmentDefinitionId,
                        "The first production migration permits quantity one only."));
                }
                if (!BossRewardText.IsBoundedTechnicalId(
                        entry.AcquisitionAnnouncementPolicyId) ||
                    !allowedPolicies.Contains(entry.AcquisitionAnnouncementPolicyId))
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-ANNOUNCEMENT-POLICY-UNKNOWN",
                        BossRewardDiagnosticDomain.Notification,
                        path + ".acquisitionAnnouncementPolicyId",
                        request.RewardResultId,
                        entry.EquipmentDefinitionId,
                        "The acquisition announcement policy is unavailable."));
                }

                BossEquipmentDefinitionSnapshot definition = ResolveUniqueEquipment(
                    catalog.EquipmentDefinitions,
                    entry.EquipmentDefinitionId,
                    diagnostics,
                    request.RewardResultId);
                if (definition != null)
                {
                    ValidateEquipmentDefinition(
                        definition,
                        catalog.SchemaVersion,
                        diagnostics,
                        request.RewardResultId);
                    equipmentById[entry.EquipmentDefinitionId] = definition;
                }
            }
            return equipmentById;
        }

        private static BossEquipmentDefinitionSnapshot ResolveUniqueEquipment(
            IReadOnlyList<BossEquipmentDefinitionSnapshot> definitions,
            string equipmentId,
            ICollection<BossRewardDiagnostic> diagnostics,
            string operationId)
        {
            BossEquipmentDefinitionSnapshot found = null;
            for (int index = 0; index < definitions.Count; index++)
            {
                BossEquipmentDefinitionSnapshot candidate = definitions[index];
                if (candidate == null ||
                    !string.Equals(
                        candidate.EquipmentDefinitionId,
                        equipmentId,
                        StringComparison.Ordinal))
                    continue;
                if (found != null)
                {
                    diagnostics.Add(Error(
                        "AL-BOSS-REWARD-CATALOG-EQUIPMENT-DUPLICATE",
                        BossRewardDiagnosticDomain.Catalog,
                        "catalog.equipmentDefinitions",
                        operationId,
                        equipmentId,
                        "The catalog contains duplicate equipment definition versions."));
                    return null;
                }
                found = candidate;
            }
            if (found == null)
            {
                diagnostics.Add(Error(
                    "AL-BOSS-REWARD-CATALOG-EQUIPMENT-UNKNOWN",
                    BossRewardDiagnosticDomain.Catalog,
                    "profile.entries",
                    operationId,
                    equipmentId,
                    "The reward entry references an unknown equipment definition."));
            }
            return found;
        }

        private static void ValidateEquipmentDefinition(
            BossEquipmentDefinitionSnapshot definition,
            string supportedSchemaVersion,
            ICollection<BossRewardDiagnostic> diagnostics,
            string operationId)
        {
            if (!BossRewardText.IsBoundedTechnicalId(definition.EquipmentDefinitionId) ||
                !BossRewardText.IsBoundedVersion(definition.SchemaVersion) ||
                !BossRewardText.IsBoundedVersion(definition.ContentVersion) ||
                !BossRewardText.IsBoundedTechnicalId(definition.SlotId) ||
                !BossRewardStackPolicies.IsSupported(definition.StackPolicyId) ||
                !BossRewardText.IsBoundedTechnicalId(definition.AcquisitionSnapshotPolicyId) ||
                !BossRewardText.IsBoundedTechnicalId(definition.PresentationContentKey) ||
                !BossRewardText.IsBoundedTechnicalId(definition.SourceRevision) ||
                !BossRewardText.IsLowerSha256(definition.RawSha256) ||
                !string.Equals(
                    definition.SchemaVersion,
                    supportedSchemaVersion,
                    StringComparison.Ordinal))
            {
                diagnostics.Add(Error(
                    "AL-BOSS-REWARD-CATALOG-EQUIPMENT-INVALID",
                    BossRewardDiagnosticDomain.Catalog,
                    "catalog.equipmentDefinitions",
                    operationId,
                    definition.EquipmentDefinitionId,
                    "The equipment technical definition is invalid or unsupported."));
            }
        }

        private static void RequireId(
            string value,
            string fieldPath,
            string operationId,
            ICollection<BossRewardDiagnostic> diagnostics)
        {
            if (BossRewardText.IsBoundedTechnicalId(value)) return;
            diagnostics.Add(Error(
                "AL-BOSS-REWARD-REQUEST-ID-INVALID",
                BossRewardDiagnosticDomain.Request,
                fieldPath,
                operationId,
                value,
                "A required bounded technical identity is invalid."));
        }

        private static void RequireVersion(
            string value,
            string fieldPath,
            string operationId,
            ICollection<BossRewardDiagnostic> diagnostics)
        {
            if (BossRewardText.IsBoundedVersion(value)) return;
            diagnostics.Add(Error(
                "AL-BOSS-REWARD-REQUEST-VERSION-INVALID",
                BossRewardDiagnosticDomain.Request,
                fieldPath,
                operationId,
                value,
                "A required bounded technical version is invalid."));
        }

        private static BossRewardComputationResult Failure(
            BossRewardComputationStatus status,
            IEnumerable<BossRewardDiagnostic> diagnostics)
        {
            return new BossRewardComputationResult(status, null, diagnostics);
        }

        private static BossRewardDiagnostic Error(
            string code,
            BossRewardDiagnosticDomain domain,
            string fieldPath,
            string operationId,
            string recordId,
            string message)
        {
            return new BossRewardDiagnostic(
                code,
                BossRewardDiagnosticSeverity.Error,
                domain,
                fieldPath,
                true,
                message,
                operationId,
                recordId);
        }

        private static string Hash(byte[] bytes)
        {
            return BossRewardDeterministicRoll.ToLowerHex(
                BossRewardDeterministicRoll.ComputeDigest(bytes));
        }
    }

    internal sealed class BossRewardCanonicalWriter : IDisposable
    {
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly MemoryStream stream = new MemoryStream();

        public void WriteString(string value)
        {
            byte[] bytes = StrictUtf8.GetBytes(value ?? string.Empty);
            WriteUInt32((uint)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        public void WriteBoolean(bool value)
        {
            stream.WriteByte(value ? (byte)1 : (byte)0);
        }

        public void WriteInt32(int value)
        {
            WriteUInt32(unchecked((uint)value));
        }

        public void WriteInt64(long value)
        {
            ulong bits = unchecked((ulong)value);
            stream.WriteByte((byte)(bits >> 56));
            stream.WriteByte((byte)(bits >> 48));
            stream.WriteByte((byte)(bits >> 40));
            stream.WriteByte((byte)(bits >> 32));
            stream.WriteByte((byte)(bits >> 24));
            stream.WriteByte((byte)(bits >> 16));
            stream.WriteByte((byte)(bits >> 8));
            stream.WriteByte((byte)bits);
        }

        public void WriteUInt32(uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        public byte[] ToArray()
        {
            return stream.ToArray();
        }

        public void Dispose()
        {
            stream.Dispose();
        }
    }
}
