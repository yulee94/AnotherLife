using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AL.Core.BossRewards
{
    public enum BossRewardSourceCatalogStatus
    {
        Ready = 0,
        SourceUnavailable = 1,
        InvalidJson = 2,
        DuplicateMember = 3,
        UnsupportedVersion = 4,
        DuplicateBinding = 5,
        DuplicateProfile = 6,
        DuplicateEquipment = 7,
        HashMismatch = 8,
        MissingReference = 9,
        UnknownBoss = 10,
        InvalidId = 11,
        InvalidCatalog = 12
    }

    public sealed class BossRewardSourceResolution
    {
        internal BossRewardSourceResolution(
            BossRewardSourceCatalogStatus status,
            BossRewardBinding binding,
            BossRewardProfile profile,
            IReadOnlyList<BossEquipmentDefinitionSnapshot> equipment)
        {
            Status = status;
            Binding = binding;
            Profile = profile;
            Equipment = equipment ?? Array.Empty<BossEquipmentDefinitionSnapshot>();
        }

        public BossRewardSourceCatalogStatus Status { get; }
        public BossRewardBinding Binding { get; }
        public BossRewardProfile Profile { get; }
        public IReadOnlyList<BossEquipmentDefinitionSnapshot> Equipment { get; }
        public bool IsFound =>
            Status == BossRewardSourceCatalogStatus.Ready &&
            Binding != null &&
            Profile != null;
    }

    public sealed class BossRewardSourceCatalogLoadResult
    {
        internal BossRewardSourceCatalogLoadResult(
            BossRewardSourceCatalogStatus status,
            string diagnosticCode,
            BossRewardCatalogSnapshot snapshot,
            string sourceSha256,
            int sourceByteLength)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Snapshot = snapshot;
            SourceSha256 = sourceSha256 ?? string.Empty;
            SourceByteLength = sourceByteLength;
        }

        public BossRewardSourceCatalogStatus Status { get; }
        public string DiagnosticCode { get; }
        public BossRewardCatalogSnapshot Snapshot { get; }
        public string SourceSha256 { get; }
        public int SourceByteLength { get; }
        public bool AllowsMutation => false;
        public string MutationActivation => BossRewardSourceCatalog.MutationActivation;
        public bool IsReady =>
            Status == BossRewardSourceCatalogStatus.Ready && Snapshot != null;
    }

    public static class BossRewardSourceCatalog
    {
        public const string MutationActivation = "blocked";
        public const string CatalogId = "al_boss_reward_source_catalog";
        public const string GameId = "another_life";
        public const string CatalogSetId = "catalog_set_boss_reward_source_v001";
        public const string Revision = "boss_reward_source_v001";
        public const string RepresentativeBossId =
            "boss_stonehold_fault_crowned_colossus";
        public const string RepresentativeProfileId =
            "reward_profile_stonehold_fault_crowned_colossus";
        public const string RepresentativeEquipmentId =
            "equipment_stonehold_fault_crowned_colossus_core";
        public const int ExpectedSourceByteLength = 2282;
        public const string ExpectedSourceSha256 =
            "5d6d8cfaf7a2253ec3885c3398024572aed1bac1c109eedbe0ac279e9a50633e";
        public const int MaximumSourceBytes = 65536;
        public const int BoundedCredits = 250;

        private static readonly string[] RootKeys =
        {
            "schemaVersion",
            "catalogId",
            "gameId",
            "catalogSetId",
            "revision",
            "authority",
            "mutationActivation",
            "approval",
            "announcementPolicyIds",
            "bindings",
            "profiles",
            "equipmentDefinitions"
        };

        private static readonly string[] AnnouncementPolicyIds =
        {
            "boss_reward.item_acquired",
            "boss_reward.credits_committed",
            "boss_reward.explicit_no_reward"
        };

        public static BossRewardSourceCatalogLoadResult Load(byte[] sourceBytes)
        {
            return Load(sourceBytes, false);
        }

        public static BossRewardSourceCatalogLoadResult LoadPinned(byte[] sourceBytes)
        {
            return Load(sourceBytes, true);
        }

        public static BossRewardSourceResolution Resolve(
            BossRewardSourceCatalogLoadResult catalog,
            string bossDefinitionId)
        {
            if (catalog == null || !catalog.IsReady)
            {
                return new BossRewardSourceResolution(
                    catalog == null
                        ? BossRewardSourceCatalogStatus.SourceUnavailable
                        : catalog.Status,
                    null,
                    null,
                    Array.Empty<BossEquipmentDefinitionSnapshot>());
            }

            if (!BossRewardText.IsCanonicalTechnicalId(bossDefinitionId))
            {
                return new BossRewardSourceResolution(
                    BossRewardSourceCatalogStatus.InvalidId,
                    null,
                    null,
                    Array.Empty<BossEquipmentDefinitionSnapshot>());
            }

            BossRewardBinding match = null;
            int matches = 0;
            IReadOnlyList<BossRewardBinding> bindings = catalog.Snapshot.Bindings;
            for (int index = 0; index < bindings.Count; index++)
            {
                BossRewardBinding candidate = bindings[index];
                if (candidate == null) continue;
                if (!string.Equals(
                        candidate.BossDefinitionId,
                        bossDefinitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                matches++;
                match = candidate;
            }

            if (matches == 0)
            {
                return new BossRewardSourceResolution(
                    BossRewardSourceCatalogStatus.UnknownBoss,
                    null,
                    null,
                    Array.Empty<BossEquipmentDefinitionSnapshot>());
            }

            if (matches != 1)
            {
                return new BossRewardSourceResolution(
                    BossRewardSourceCatalogStatus.DuplicateBinding,
                    null,
                    null,
                    Array.Empty<BossEquipmentDefinitionSnapshot>());
            }

            BossRewardProfile profile = null;
            int profileMatches = 0;
            IReadOnlyList<BossRewardProfile> profiles = catalog.Snapshot.Profiles;
            for (int index = 0; index < profiles.Count; index++)
            {
                BossRewardProfile candidate = profiles[index];
                if (candidate == null) continue;
                if (!string.Equals(candidate.Id, match.RewardProfileId, StringComparison.Ordinal) ||
                    !string.Equals(
                        candidate.ContentVersion,
                        match.RewardProfileContentVersion,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                profileMatches++;
                profile = candidate;
            }

            if (profileMatches == 0)
            {
                return new BossRewardSourceResolution(
                    BossRewardSourceCatalogStatus.MissingReference,
                    match,
                    null,
                    Array.Empty<BossEquipmentDefinitionSnapshot>());
            }

            if (profileMatches != 1)
            {
                return new BossRewardSourceResolution(
                    BossRewardSourceCatalogStatus.DuplicateProfile,
                    match,
                    null,
                    Array.Empty<BossEquipmentDefinitionSnapshot>());
            }

            var equipment = new List<BossEquipmentDefinitionSnapshot>(profile.Entries.Count);
            for (int entryIndex = 0; entryIndex < profile.Entries.Count; entryIndex++)
            {
                BossRewardEntry entry = profile.Entries[entryIndex];
                BossEquipmentDefinitionSnapshot definition = null;
                int definitionMatches = 0;
                IReadOnlyList<BossEquipmentDefinitionSnapshot> definitions =
                    catalog.Snapshot.EquipmentDefinitions;
                for (int index = 0; index < definitions.Count; index++)
                {
                    BossEquipmentDefinitionSnapshot candidate = definitions[index];
                    if (candidate == null) continue;
                    if (!string.Equals(
                            candidate.EquipmentDefinitionId,
                            entry.EquipmentDefinitionId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    definitionMatches++;
                    definition = candidate;
                }

                if (definitionMatches == 0)
                {
                    return new BossRewardSourceResolution(
                        BossRewardSourceCatalogStatus.MissingReference,
                        match,
                        profile,
                        Array.Empty<BossEquipmentDefinitionSnapshot>());
                }

                if (definitionMatches != 1)
                {
                    return new BossRewardSourceResolution(
                        BossRewardSourceCatalogStatus.DuplicateEquipment,
                        match,
                        profile,
                        Array.Empty<BossEquipmentDefinitionSnapshot>());
                }

                equipment.Add(definition);
            }

            return new BossRewardSourceResolution(
                BossRewardSourceCatalogStatus.Ready,
                match,
                profile,
                equipment);
        }

        internal static string ComputeProfileSha256(BossRewardProfile profile)
        {
            using (var writer = new BossRewardCanonicalWriter())
            {
                writer.WriteString("boss_reward_profile_v1");
                writer.WriteString(profile.GameId);
                writer.WriteString(profile.CatalogSetId);
                writer.WriteString(profile.Id);
                writer.WriteString(profile.SchemaVersion);
                writer.WriteString(profile.ContentVersion);
                writer.WriteInt32(profile.WarzoneCredits);
                writer.WriteBoolean(profile.IsExplicitNoReward);
                writer.WriteString(profile.SourceRevision);
                writer.WriteUInt32((uint)profile.Entries.Count);
                for (int index = 0; index < profile.Entries.Count; index++)
                {
                    BossRewardEntry entry = profile.Entries[index];
                    writer.WriteString(entry.EquipmentDefinitionId);
                    writer.WriteInt32(entry.DropChanceMicros);
                    writer.WriteInt32(entry.Quantity);
                    writer.WriteString(entry.AcquisitionAnnouncementPolicyId);
                }

                return Hash(writer.ToArray());
            }
        }

        internal static string ComputeEquipmentSha256(
            BossEquipmentDefinitionSnapshot definition)
        {
            using (var writer = new BossRewardCanonicalWriter())
            {
                writer.WriteString("boss_equipment_definition_v1");
                writer.WriteString(definition.EquipmentDefinitionId);
                writer.WriteString(definition.SchemaVersion);
                writer.WriteString(definition.ContentVersion);
                writer.WriteString(definition.SlotId);
                writer.WriteInt32(definition.AttackBonus);
                writer.WriteInt32(definition.DefenseBonus);
                writer.WriteInt32(definition.HealthBonus);
                writer.WriteString(definition.StackPolicyId);
                writer.WriteString(definition.AcquisitionSnapshotPolicyId);
                writer.WriteString(definition.PresentationContentKey);
                writer.WriteString(definition.SourceRevision);
                return Hash(writer.ToArray());
            }
        }

        private static BossRewardSourceCatalogLoadResult Load(
            byte[] sourceBytes,
            bool requirePinnedSource)
        {
            if (sourceBytes == null)
            {
                return Fail(BossRewardSourceCatalogStatus.SourceUnavailable, 0, string.Empty);
            }

            string digest = Hash(sourceBytes);
            if (requirePinnedSource &&
                (sourceBytes.Length != ExpectedSourceByteLength ||
                 !string.Equals(digest, ExpectedSourceSha256, StringComparison.Ordinal)))
            {
                return Fail(
                    BossRewardSourceCatalogStatus.HashMismatch,
                    sourceBytes.Length,
                    digest);
            }

            StrictJsonValue root;
            try
            {
                root = StrictJsonDocument.Parse(sourceBytes, MaximumSourceBytes);
            }
            catch (StrictJsonException exception)
            {
                BossRewardSourceCatalogStatus status =
                    string.Equals(exception.Code, "DUPLICATE_MEMBER", StringComparison.Ordinal)
                        ? BossRewardSourceCatalogStatus.DuplicateMember
                        : BossRewardSourceCatalogStatus.InvalidJson;
                return Fail(status, sourceBytes.Length, digest);
            }

            var objectRoot = root as StrictJsonObject;
            if (objectRoot == null)
                return Fail(BossRewardSourceCatalogStatus.InvalidJson, sourceBytes.Length, digest);

            try
            {
                BossRewardCatalogSnapshot snapshot = BuildSnapshot(objectRoot);
                return new BossRewardSourceCatalogLoadResult(
                    BossRewardSourceCatalogStatus.Ready,
                    "AL-BOSS-REWARD-SOURCE-READY",
                    snapshot,
                    digest,
                    sourceBytes.Length);
            }
            catch (SourceCatalogException exception)
            {
                return Fail(exception.Status, sourceBytes.Length, digest);
            }
        }

        private static BossRewardCatalogSnapshot BuildSnapshot(StrictJsonObject root)
        {
            RequireExactKeys(root, RootKeys, "$");
            string schemaVersion = RequireString(root, "schemaVersion");
            if (!string.Equals(
                    schemaVersion,
                    BossRewardTechnicalLimits.SupportedRewardSchemaVersion,
                    StringComparison.Ordinal))
            {
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.UnsupportedVersion);
            }

            if (!string.Equals(RequireString(root, "catalogId"), CatalogId, StringComparison.Ordinal) ||
                !string.Equals(RequireString(root, "gameId"), GameId, StringComparison.Ordinal) ||
                !string.Equals(RequireString(root, "catalogSetId"), CatalogSetId, StringComparison.Ordinal) ||
                !string.Equals(RequireString(root, "revision"), Revision, StringComparison.Ordinal) ||
                !string.Equals(
                    RequireString(root, "authority"),
                    "technical_boss_reward_source",
                    StringComparison.Ordinal))
            {
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            }

            if (!string.Equals(
                    RequireString(root, "mutationActivation"),
                    MutationActivation,
                    StringComparison.Ordinal))
            {
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            }

            StrictJsonObject approval = RequireObject(root, "approval");
            RequireExactKeys(
                approval,
                new[]
                {
                    "mode",
                    "issue",
                    "representativeBossId",
                    "sourceQualificationIssue"
                },
                "$.approval");
            if (!string.Equals(
                    RequireString(approval, "mode"),
                    "autonomous_bounded_recommendation",
                    StringComparison.Ordinal) ||
                !string.Equals(RequireString(approval, "issue"), "#168", StringComparison.Ordinal) ||
                !string.Equals(
                    RequireString(approval, "representativeBossId"),
                    RepresentativeBossId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    RequireString(approval, "sourceQualificationIssue"),
                    "#259",
                    StringComparison.Ordinal))
            {
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            }

            StrictJsonArray policies = RequireArray(root, "announcementPolicyIds");
            if (policies.Items.Count != AnnouncementPolicyIds.Length)
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            var announcement = new string[policies.Items.Count];
            for (int index = 0; index < policies.Items.Count; index++)
            {
                var value = policies.Items[index] as StrictJsonString;
                if (value == null ||
                    !string.Equals(value.Value, AnnouncementPolicyIds[index], StringComparison.Ordinal))
                {
                    throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
                }

                announcement[index] = value.Value;
            }

            var bindings = new List<BossRewardBinding>();
            var bindingIds = new HashSet<string>(StringComparer.Ordinal);
            StrictJsonArray bindingArray = RequireArray(root, "bindings");
            if (bindingArray.Items.Count == 0)
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            for (int index = 0; index < bindingArray.Items.Count; index++)
            {
                var row = bindingArray.Items[index] as StrictJsonObject;
                if (row == null)
                    throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
                RequireExactKeys(
                    row,
                    new[]
                    {
                        "bossDefinitionId",
                        "bossDefinitionContentVersion",
                        "rewardProfileId",
                        "rewardProfileContentVersion"
                    },
                    "$.bindings");
                string bossId = RequireTechnicalId(row, "bossDefinitionId");
                if (!bindingIds.Add(bossId))
                    throw new SourceCatalogException(BossRewardSourceCatalogStatus.DuplicateBinding);
                bindings.Add(
                    new BossRewardBinding(
                        bossId,
                        RequireTechnicalId(row, "bossDefinitionContentVersion"),
                        RequireTechnicalId(row, "rewardProfileId"),
                        RequireTechnicalId(row, "rewardProfileContentVersion")));
            }
            if (bindings.Count != 1)
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);

            var profiles = new List<BossRewardProfile>();
            var profileKeys = new HashSet<string>(StringComparer.Ordinal);
            StrictJsonArray profileArray = RequireArray(root, "profiles");
            if (profileArray.Items.Count == 0)
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            for (int index = 0; index < profileArray.Items.Count; index++)
            {
                var row = profileArray.Items[index] as StrictJsonObject;
                if (row == null)
                    throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
                profiles.Add(ReadProfile(row, profileKeys));
            }

            var equipment = new List<BossEquipmentDefinitionSnapshot>();
            var equipmentIds = new HashSet<string>(StringComparer.Ordinal);
            StrictJsonArray equipmentArray = RequireArray(root, "equipmentDefinitions");
            if (equipmentArray.Items.Count == 0)
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            for (int index = 0; index < equipmentArray.Items.Count; index++)
            {
                var row = equipmentArray.Items[index] as StrictJsonObject;
                if (row == null)
                    throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
                equipment.Add(ReadEquipment(row, equipmentIds));
            }

            var profileIndex = new Dictionary<string, BossRewardProfile>(StringComparer.Ordinal);
            for (int index = 0; index < profiles.Count; index++)
            {
                BossRewardProfile profile = profiles[index];
                profileIndex[profile.Id + "\n" + profile.ContentVersion] = profile;
            }

            var equipmentIndex = new Dictionary<string, BossEquipmentDefinitionSnapshot>(
                StringComparer.Ordinal);
            for (int index = 0; index < equipment.Count; index++)
            {
                equipmentIndex[equipment[index].EquipmentDefinitionId] = equipment[index];
            }

            for (int index = 0; index < bindings.Count; index++)
            {
                BossRewardBinding binding = bindings[index];
                string key = binding.RewardProfileId + "\n" + binding.RewardProfileContentVersion;
                BossRewardProfile profile;
                if (!profileIndex.TryGetValue(key, out profile))
                    throw new SourceCatalogException(BossRewardSourceCatalogStatus.MissingReference);
                for (int entryIndex = 0; entryIndex < profile.Entries.Count; entryIndex++)
                {
                    if (!equipmentIndex.ContainsKey(profile.Entries[entryIndex].EquipmentDefinitionId))
                    {
                        throw new SourceCatalogException(
                            BossRewardSourceCatalogStatus.MissingReference);
                    }
                }
            }

            return new BossRewardCatalogSnapshot(
                GameId,
                CatalogSetId,
                BossRewardTechnicalLimits.SupportedRewardSchemaVersion,
                Revision,
                bindings,
                profiles,
                equipment,
                announcement);
        }

        private static BossRewardProfile ReadProfile(
            StrictJsonObject row,
            HashSet<string> profileKeys)
        {
            RequireExactKeys(
                row,
                new[]
                {
                    "gameId",
                    "catalogSetId",
                    "id",
                    "schemaVersion",
                    "contentVersion",
                    "warzoneCredits",
                    "isExplicitNoReward",
                    "entries",
                    "sourceRevision",
                    "rawSha256"
                },
                "$.profiles");
            string id = RequireTechnicalId(row, "id");
            string contentVersion = RequireTechnicalId(row, "contentVersion");
            if (!profileKeys.Add(id + "\n" + contentVersion))
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.DuplicateProfile);
            if (!string.Equals(RequireString(row, "gameId"), GameId, StringComparison.Ordinal) ||
                !string.Equals(
                    RequireString(row, "catalogSetId"),
                    CatalogSetId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    RequireString(row, "schemaVersion"),
                    BossRewardTechnicalLimits.SupportedRewardSchemaVersion,
                    StringComparison.Ordinal))
            {
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            }

            int credits = RequireInt(row, "warzoneCredits", 0, BoundedCredits);
            bool explicitNoReward = RequireBool(row, "isExplicitNoReward");
            StrictJsonArray entriesArray = RequireArray(row, "entries");
            var entries = new List<BossRewardEntry>(entriesArray.Items.Count);
            var entryIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < entriesArray.Items.Count; index++)
            {
                var entryRow = entriesArray.Items[index] as StrictJsonObject;
                if (entryRow == null)
                    throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
                RequireExactKeys(
                    entryRow,
                    new[]
                    {
                        "equipmentDefinitionId",
                        "dropChanceMicros",
                        "quantity",
                        "acquisitionAnnouncementPolicyId"
                    },
                    "$.profiles.entries");
                string equipmentId = RequireTechnicalId(entryRow, "equipmentDefinitionId");
                if (!entryIds.Add(equipmentId))
                    throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
                string policy = RequireString(entryRow, "acquisitionAnnouncementPolicyId");
                if (!BossRewardText.IsBoundedContentKey(policy))
                    throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
                entries.Add(
                    new BossRewardEntry(
                        equipmentId,
                        RequireInt(
                            entryRow,
                            "dropChanceMicros",
                            0,
                            BossRewardTechnicalLimits.MicrosPerUnit),
                        RequireInt(entryRow, "quantity", 1, 1),
                        policy));
            }

            var profile = new BossRewardProfile(
                GameId,
                CatalogSetId,
                id,
                BossRewardTechnicalLimits.SupportedRewardSchemaVersion,
                contentVersion,
                credits,
                explicitNoReward,
                entries,
                RequireTechnicalId(row, "sourceRevision"),
                RequireSha256(row, "rawSha256"));
            if (!string.Equals(profile.RawSha256, ComputeProfileSha256(profile), StringComparison.Ordinal))
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.HashMismatch);
            return profile;
        }

        private static BossEquipmentDefinitionSnapshot ReadEquipment(
            StrictJsonObject row,
            HashSet<string> equipmentIds)
        {
            RequireExactKeys(
                row,
                new[]
                {
                    "equipmentDefinitionId",
                    "schemaVersion",
                    "contentVersion",
                    "slotId",
                    "attackBonus",
                    "defenseBonus",
                    "healthBonus",
                    "stackPolicyId",
                    "acquisitionSnapshotPolicyId",
                    "presentationContentKey",
                    "sourceRevision",
                    "rawSha256"
                },
                "$.equipmentDefinitions");
            string id = RequireTechnicalId(row, "equipmentDefinitionId");
            if (!equipmentIds.Add(id))
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.DuplicateEquipment);
            string presentation = RequireString(row, "presentationContentKey");
            if (!BossRewardText.IsBoundedContentKey(presentation) ||
                string.Equals(presentation, id, StringComparison.Ordinal))
            {
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            }

            var definition = new BossEquipmentDefinitionSnapshot(
                id,
                RequireString(row, "schemaVersion"),
                RequireTechnicalId(row, "contentVersion"),
                RequireTechnicalId(row, "slotId"),
                RequireInt(row, "attackBonus", 0, 0),
                RequireInt(row, "defenseBonus", 0, 0),
                RequireInt(row, "healthBonus", 0, 0),
                RequireString(row, "stackPolicyId"),
                RequireString(row, "acquisitionSnapshotPolicyId"),
                presentation,
                RequireTechnicalId(row, "sourceRevision"),
                RequireSha256(row, "rawSha256"));
            if (!string.Equals(
                    definition.StackPolicyId,
                    BossRewardStackPolicies.StackQuantity,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    definition.AcquisitionSnapshotPolicyId,
                    BossRewardAcquisitionSnapshotPolicies.SnapshotV1,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    definition.SchemaVersion,
                    BossRewardTechnicalLimits.SupportedRewardSchemaVersion,
                    StringComparison.Ordinal))
            {
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            }

            if (!string.Equals(
                    definition.RawSha256,
                    ComputeEquipmentSha256(definition),
                    StringComparison.Ordinal))
            {
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.HashMismatch);
            }

            return definition;
        }

        private static void RequireExactKeys(
            StrictJsonObject value,
            IReadOnlyList<string> expected,
            string location)
        {
            if (value.Properties.Count != expected.Count)
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            for (int index = 0; index < expected.Count; index++)
            {
                if (!string.Equals(
                        value.Properties[index].Name,
                        expected[index],
                        StringComparison.Ordinal))
                {
                    throw new SourceCatalogException(
                        BossRewardSourceCatalogStatus.InvalidCatalog);
                }
            }
        }

        private static string RequireString(StrictJsonObject parent, string name)
        {
            StrictJsonValue value;
            if (!parent.TryGet(name, out value))
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            var text = value as StrictJsonString;
            if (text == null || string.IsNullOrEmpty(text.Value) ||
                !string.Equals(text.Value, text.Value.Trim(), StringComparison.Ordinal))
            {
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            }

            return text.Value;
        }

        private static string RequireTechnicalId(StrictJsonObject parent, string name)
        {
            string value = RequireString(parent, name);
            if (!BossRewardText.IsCanonicalTechnicalId(value))
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidId);
            return value;
        }

        private static string RequireSha256(StrictJsonObject parent, string name)
        {
            string value = RequireString(parent, name);
            if (!BossRewardText.IsLowerSha256(value))
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.HashMismatch);
            return value;
        }

        private static int RequireInt(
            StrictJsonObject parent,
            string name,
            int minimum,
            int maximum)
        {
            StrictJsonValue value;
            if (!parent.TryGet(name, out value))
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            var number = value as StrictJsonNumber;
            if (number == null || !number.IsInt32)
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            if (number.Int32Value < minimum || number.Int32Value > maximum)
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            return number.Int32Value;
        }

        private static bool RequireBool(StrictJsonObject parent, string name)
        {
            StrictJsonValue value;
            if (!parent.TryGet(name, out value))
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            var flag = value as StrictJsonBoolean;
            if (flag == null)
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            return flag.Value;
        }

        private static StrictJsonObject RequireObject(StrictJsonObject parent, string name)
        {
            StrictJsonValue value;
            if (!parent.TryGet(name, out value))
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            var obj = value as StrictJsonObject;
            if (obj == null)
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            return obj;
        }

        private static StrictJsonArray RequireArray(StrictJsonObject parent, string name)
        {
            StrictJsonValue value;
            if (!parent.TryGet(name, out value))
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            var array = value as StrictJsonArray;
            if (array == null)
                throw new SourceCatalogException(BossRewardSourceCatalogStatus.InvalidCatalog);
            return array;
        }

        private static BossRewardSourceCatalogLoadResult Fail(
            BossRewardSourceCatalogStatus status,
            int byteLength,
            string digest)
        {
            return new BossRewardSourceCatalogLoadResult(
                status,
                "AL-BOSS-REWARD-SOURCE-" + status.ToString().ToUpperInvariant(),
                null,
                digest,
                byteLength);
        }

        private static string Hash(byte[] bytes)
        {
            return BossRewardDeterministicRoll.ToLowerHex(
                BossRewardDeterministicRoll.ComputeDigest(bytes));
        }

        private sealed class SourceCatalogException : Exception
        {
            internal SourceCatalogException(BossRewardSourceCatalogStatus status)
            {
                Status = status;
            }

            internal BossRewardSourceCatalogStatus Status { get; }
        }
    }

    internal abstract class StrictJsonValue
    {
    }

    internal sealed class StrictJsonProperty
    {
        internal StrictJsonProperty(string name, StrictJsonValue value)
        {
            Name = name;
            Value = value;
        }

        internal string Name { get; }
        internal StrictJsonValue Value { get; }
    }

    internal sealed class StrictJsonObject : StrictJsonValue
    {
        private readonly Dictionary<string, StrictJsonValue> index;

        internal StrictJsonObject(IList<StrictJsonProperty> properties)
        {
            var ordered = new StrictJsonProperty[properties.Count];
            index = new Dictionary<string, StrictJsonValue>(properties.Count, StringComparer.Ordinal);
            for (int i = 0; i < properties.Count; i++)
            {
                ordered[i] = properties[i];
                index.Add(properties[i].Name, properties[i].Value);
            }

            Properties = Array.AsReadOnly(ordered);
        }

        internal IReadOnlyList<StrictJsonProperty> Properties { get; }

        internal bool TryGet(string name, out StrictJsonValue value)
        {
            return index.TryGetValue(name ?? string.Empty, out value);
        }
    }

    internal sealed class StrictJsonArray : StrictJsonValue
    {
        internal StrictJsonArray(IList<StrictJsonValue> items)
        {
            var copy = new StrictJsonValue[items.Count];
            for (int i = 0; i < items.Count; i++)
                copy[i] = items[i];
            Items = Array.AsReadOnly(copy);
        }

        internal IReadOnlyList<StrictJsonValue> Items { get; }
    }

    internal sealed class StrictJsonString : StrictJsonValue
    {
        internal StrictJsonString(string value)
        {
            Value = value;
        }

        internal string Value { get; }
    }

    internal sealed class StrictJsonNumber : StrictJsonValue
    {
        internal StrictJsonNumber(int value)
        {
            Int32Value = value;
            IsInt32 = true;
        }

        internal int Int32Value { get; }
        internal bool IsInt32 { get; }
    }

    internal sealed class StrictJsonBoolean : StrictJsonValue
    {
        internal StrictJsonBoolean(bool value)
        {
            Value = value;
        }

        internal bool Value { get; }
    }

    internal sealed class StrictJsonNull : StrictJsonValue
    {
        internal static readonly StrictJsonNull Instance = new StrictJsonNull();
    }

    internal sealed class StrictJsonException : Exception
    {
        internal StrictJsonException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        internal string Code { get; }
    }

    internal static class StrictJsonDocument
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static StrictJsonValue Parse(byte[] bytes, int maximumBytes)
        {
            if (bytes == null)
                throw new StrictJsonException("INPUT_NULL", "JSON input bytes are required.");
            if (bytes.Length == 0)
                throw new StrictJsonException("INPUT_EMPTY", "JSON input cannot be empty.");
            if (bytes.Length > maximumBytes)
                throw new StrictJsonException("INPUT_TOO_LARGE", "JSON input exceeds the byte limit.");
            if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
                throw new StrictJsonException("UTF8_BOM", "UTF-8 byte-order marks are not supported.");
            string source;
            try
            {
                source = StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                throw new StrictJsonException("UTF8_INVALID", "JSON input is not well-formed UTF-8.");
            }

            return new Parser(source).Parse();
        }

        private sealed class Parser
        {
            private readonly string source;
            private int index;

            internal Parser(string source)
            {
                this.source = source;
            }

            internal StrictJsonValue Parse()
            {
                SkipWhitespace();
                StrictJsonValue value = ParseValue();
                SkipWhitespace();
                if (index != source.Length)
                    throw new StrictJsonException("TRAILING_CONTENT", "Unexpected trailing JSON content.");
                return value;
            }

            private StrictJsonValue ParseValue()
            {
                SkipWhitespace();
                if (index >= source.Length)
                    throw new StrictJsonException("UNEXPECTED_END", "Unexpected end of JSON input.");
                char current = source[index];
                if (current == '{') return ParseObject();
                if (current == '[') return ParseArray();
                if (current == '"') return new StrictJsonString(ParseString());
                if (current == 't') return ParseLiteral("true", new StrictJsonBoolean(true));
                if (current == 'f') return ParseLiteral("false", new StrictJsonBoolean(false));
                if (current == 'n') return ParseLiteral("null", StrictJsonNull.Instance);
                if (current == '-' || (current >= '0' && current <= '9')) return ParseNumber();
                throw new StrictJsonException("INVALID_TOKEN", "Unexpected JSON token.");
            }

            private StrictJsonValue ParseObject()
            {
                index++;
                var properties = new List<StrictJsonProperty>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                SkipWhitespace();
                if (index < source.Length && source[index] == '}')
                {
                    index++;
                    return new StrictJsonObject(properties);
                }

                while (true)
                {
                    SkipWhitespace();
                    if (index >= source.Length || source[index] != '"')
                        throw new StrictJsonException("INVALID_MEMBER", "JSON object members require names.");
                    string name = ParseString();
                    if (!seen.Add(name))
                        throw new StrictJsonException("DUPLICATE_MEMBER", "Duplicate JSON member: " + name);
                    SkipWhitespace();
                    if (index >= source.Length || source[index] != ':')
                        throw new StrictJsonException("INVALID_MEMBER", "JSON members require a colon.");
                    index++;
                    properties.Add(new StrictJsonProperty(name, ParseValue()));
                    SkipWhitespace();
                    if (index >= source.Length)
                        throw new StrictJsonException("UNEXPECTED_END", "Unexpected end of JSON object.");
                    if (source[index] == ',')
                    {
                        index++;
                        continue;
                    }

                    if (source[index] == '}')
                    {
                        index++;
                        return new StrictJsonObject(properties);
                    }

                    throw new StrictJsonException("INVALID_OBJECT", "Invalid JSON object.");
                }
            }

            private StrictJsonValue ParseArray()
            {
                index++;
                var items = new List<StrictJsonValue>();
                SkipWhitespace();
                if (index < source.Length && source[index] == ']')
                {
                    index++;
                    return new StrictJsonArray(items);
                }

                while (true)
                {
                    items.Add(ParseValue());
                    SkipWhitespace();
                    if (index >= source.Length)
                        throw new StrictJsonException("UNEXPECTED_END", "Unexpected end of JSON array.");
                    if (source[index] == ',')
                    {
                        index++;
                        continue;
                    }

                    if (source[index] == ']')
                    {
                        index++;
                        return new StrictJsonArray(items);
                    }

                    throw new StrictJsonException("INVALID_ARRAY", "Invalid JSON array.");
                }
            }

            private string ParseString()
            {
                index++;
                var builder = new StringBuilder();
                while (index < source.Length)
                {
                    char current = source[index++];
                    if (current == '"') return builder.ToString();
                    if (current == '\\')
                    {
                        if (index >= source.Length)
                            throw new StrictJsonException("INVALID_STRING", "Unterminated escape.");
                        char escape = source[index++];
                        switch (escape)
                        {
                            case '"':
                            case '\\':
                            case '/':
                                builder.Append(escape);
                                break;
                            case 'b':
                                builder.Append('\b');
                                break;
                            case 'f':
                                builder.Append('\f');
                                break;
                            case 'n':
                                builder.Append('\n');
                                break;
                            case 'r':
                                builder.Append('\r');
                                break;
                            case 't':
                                builder.Append('\t');
                                break;
                            default:
                                throw new StrictJsonException("INVALID_STRING", "Unsupported escape.");
                        }

                        continue;
                    }

                    if (char.IsControl(current))
                        throw new StrictJsonException("INVALID_STRING", "Control characters are not allowed.");
                    builder.Append(current);
                }

                throw new StrictJsonException("INVALID_STRING", "Unterminated JSON string.");
            }

            private StrictJsonValue ParseNumber()
            {
                int start = index;
                if (source[index] == '-') index++;
                if (index >= source.Length || source[index] < '0' || source[index] > '9')
                    throw new StrictJsonException("INVALID_NUMBER", "Invalid JSON number.");
                if (source[index] == '0')
                {
                    index++;
                }
                else
                {
                    while (index < source.Length && source[index] >= '0' && source[index] <= '9')
                        index++;
                }

                if (index < source.Length && (source[index] == '.' || source[index] == 'e' || source[index] == 'E'))
                    throw new StrictJsonException("INVALID_NUMBER", "Non-integer JSON numbers are not supported.");
                int value;
                if (!int.TryParse(
                        source.Substring(start, index - start),
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out value))
                {
                    throw new StrictJsonException("INVALID_NUMBER", "JSON integer is out of range.");
                }

                return new StrictJsonNumber(value);
            }

            private StrictJsonValue ParseLiteral(string literal, StrictJsonValue value)
            {
                if (index + literal.Length > source.Length ||
                    string.CompareOrdinal(source, index, literal, 0, literal.Length) != 0)
                {
                    throw new StrictJsonException("INVALID_TOKEN", "Unexpected JSON literal.");
                }

                index += literal.Length;
                return value;
            }

            private void SkipWhitespace()
            {
                while (index < source.Length)
                {
                    char current = source[index];
                    if (current != ' ' && current != '\n' && current != '\r' && current != '\t')
                        return;
                    index++;
                }
            }
        }
    }
}
