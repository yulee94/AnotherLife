using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AL.Runtime")]

namespace AL.Data.Catalogs
{
    public enum SaveCandidateSourceGeneration
    {
        Unknown = 0,
        Primary = 1,
        Backup = 2,
        Previous = 3,
        Temp = 4
    }

    public enum SaveSemanticCandidateOutcome
    {
        Invalid = 0,
        RepairableWithDataChange = 1,
        DegradedMalformed = 2,
        CompatiblePreservedUnknown = 3,
        CompatibleNormalized = 4,
        Valid = 5,
        ForwardSchemaReadOnly = 6,
        OversizePreservedReadOnly = 7
    }

    [Flags]
    public enum SaveSemanticDomain
    {
        None = 0,
        Envelope = 1 << 0,
        Metadata = 1 << 1,
        Resources = 1 << 2,
        Quests = 1 << 3,
        Relationships = 1 << 4,
        Buildings = 1 << 5,
        Troops = 1 << 6,
        Research = 1 << 7,
        Territories = 1 << 8,
        RealmGems = 1 << 9,
        Equipment = 1 << 10,
        Customization = 1 << 11,
        Warmaster = 1 << 12,
        Chapter = 1 << 13,
        Narrative = 1 << 14,
        All = (1 << 15) - 1
    }

    public enum SaveSemanticDiagnosticSeverity
    {
        Information = 0,
        Warning = 1,
        Error = 2
    }

    public sealed class SaveSemanticDiagnostic
    {
        internal SaveSemanticDiagnostic(
            string code,
            string path,
            SaveSemanticDomain domain,
            SaveSemanticDiagnosticSeverity severity)
        {
            Code = code ?? string.Empty;
            Path = path ?? "$";
            Domain = domain;
            Severity = severity;
        }

        public string Code { get; }
        public string Path { get; }
        public SaveSemanticDomain Domain { get; }
        public SaveSemanticDiagnosticSeverity Severity { get; }
    }

    public sealed class SaveSemanticQuestRule
    {
        public SaveSemanticQuestRule(string questId, int targetValue)
        {
            if (string.IsNullOrWhiteSpace(questId) || questId.Length > 256)
            {
                throw new ArgumentException(
                    "Quest IDs must be nonblank and at most 256 characters.",
                    nameof(questId));
            }

            if (targetValue <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetValue));
            }

            QuestId = questId;
            TargetValue = targetValue;
        }

        public string QuestId { get; }
        public int TargetValue { get; }
    }

    public enum SaveSemanticStableIdKind
    {
        Chapter = 0,
        Npc = 1,
        Faction = 2,
        Building = 3,
        Research = 4,
        Territory = 5,
        RealmGem = 6,
        WishgateReward = 7,
        WarmasterSet = 8,
        WarmasterPiece = 9,
        BodyPreset = 10,
        HairStyle = 11,
        ArmorStyle = 12,
        FaceMark = 13,
        WeaponStyle = 14,
        OffhandStyle = 15,
        Equipment = 16,
        Boss = 17
    }

    public sealed class SaveSemanticStableIdRule
    {
        public SaveSemanticStableIdRule(SaveSemanticStableIdKind kind, string stableId)
        {
            if ((int)kind < (int)SaveSemanticStableIdKind.Chapter ||
                (int)kind > (int)SaveSemanticStableIdKind.Boss)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (string.IsNullOrWhiteSpace(stableId) || stableId.Length > 256)
            {
                throw new ArgumentException(
                    "Stable IDs must be nonblank and at most 256 characters.",
                    nameof(stableId));
            }

            Kind = kind;
            StableId = stableId;
        }

        public SaveSemanticStableIdKind Kind { get; }
        public string StableId { get; }
    }

    /// <summary>
    /// Immutable runtime authority supplied by the assembly that owns the actual enums,
    /// wallet rules, and quest definitions. The JSON classifier deliberately does not
    /// duplicate those contracts in its engine-independent assembly.
    /// </summary>
    public sealed class SaveSemanticValidationAuthority
    {
        public const int MaximumAuthorityValues = 256;
        public const int MaximumQuestRules = 4096;
        public const int MaximumStableIdRules = 16384;

        private readonly HashSet<int> supportedRealmValues;
        private readonly HashSet<int> supportedResourceValues;
        private readonly HashSet<int> requiredLegacyResourceValues;
        private readonly HashSet<int> requiredCurrentResourceValues;
        private readonly HashSet<int> supportedTroopValues;
        private readonly HashSet<int> supportedEquipmentSlotValues;
        private readonly Dictionary<string, int> questTargets;
        private readonly Dictionary<SaveSemanticStableIdKind, HashSet<string>> stableIds;

        public SaveSemanticValidationAuthority(
            IEnumerable<int> supportedRealmValues,
            IEnumerable<int> supportedResourceValues,
            IEnumerable<int> requiredLegacyResourceValues,
            IEnumerable<int> requiredCurrentResourceValues,
            IEnumerable<int> supportedTroopValues,
            IEnumerable<int> supportedEquipmentSlotValues,
            IEnumerable<SaveSemanticQuestRule> questRules,
            IEnumerable<SaveSemanticStableIdRule> stableIdRules = null)
        {
            this.supportedRealmValues = CopyValues(
                supportedRealmValues,
                nameof(supportedRealmValues),
                allowEmpty: false);
            this.supportedResourceValues = CopyValues(
                supportedResourceValues,
                nameof(supportedResourceValues),
                allowEmpty: false);
            this.requiredLegacyResourceValues = CopyValues(
                requiredLegacyResourceValues,
                nameof(requiredLegacyResourceValues),
                allowEmpty: false);
            this.requiredCurrentResourceValues = CopyValues(
                requiredCurrentResourceValues,
                nameof(requiredCurrentResourceValues),
                allowEmpty: false);
            this.supportedTroopValues = CopyValues(
                supportedTroopValues,
                nameof(supportedTroopValues),
                allowEmpty: false);
            this.supportedEquipmentSlotValues = CopyValues(
                supportedEquipmentSlotValues,
                nameof(supportedEquipmentSlotValues),
                allowEmpty: false);

            if (!this.requiredLegacyResourceValues.IsSubsetOf(this.supportedResourceValues) ||
                !this.requiredCurrentResourceValues.IsSubsetOf(this.supportedResourceValues))
            {
                throw new ArgumentException(
                    "Required resource values must be members of the supported wallet authority.");
            }

            questTargets = new Dictionary<string, int>(StringComparer.Ordinal);
            if (questRules == null)
            {
                throw new ArgumentNullException(nameof(questRules));
            }

            foreach (var rule in questRules)
            {
                if (rule == null)
                {
                    throw new ArgumentException("Quest rules cannot contain null.", nameof(questRules));
                }

                if (!questTargets.TryAdd(rule.QuestId, rule.TargetValue))
                {
                    throw new ArgumentException("Quest rule IDs must be unique.", nameof(questRules));
                }

                if (questTargets.Count > MaximumQuestRules)
                {
                    throw new ArgumentException(
                        "The quest rule set exceeds the supported bound.",
                        nameof(questRules));
                }
            }

            stableIds = new Dictionary<SaveSemanticStableIdKind, HashSet<string>>();
            var stableIdRuleCount = 0;
            if (stableIdRules != null)
            {
                foreach (var rule in stableIdRules)
                {
                    if (rule == null)
                    {
                        throw new ArgumentException(
                            "Stable ID rules cannot contain null.",
                            nameof(stableIdRules));
                    }

                    HashSet<string> values;
                    if (!stableIds.TryGetValue(rule.Kind, out values))
                    {
                        values = new HashSet<string>(StringComparer.Ordinal);
                        stableIds.Add(rule.Kind, values);
                    }

                    if (!values.Add(rule.StableId))
                    {
                        throw new ArgumentException(
                            "Stable ID rules must be unique within each kind.",
                            nameof(stableIdRules));
                    }

                    stableIdRuleCount++;
                    if (stableIdRuleCount > MaximumStableIdRules)
                    {
                        throw new ArgumentException(
                            "The stable ID rule set exceeds the supported bound.",
                            nameof(stableIdRules));
                    }
                }
            }
        }

        public int QuestRuleCount => questTargets.Count;

        internal bool IsSupportedRealm(int value) => supportedRealmValues.Contains(value);
        internal bool IsSupportedResource(int value) => supportedResourceValues.Contains(value);
        internal bool IsRequiredLegacyResource(int value) =>
            requiredLegacyResourceValues.Contains(value);
        internal bool IsRequiredCurrentResource(int value) =>
            requiredCurrentResourceValues.Contains(value);
        internal bool IsSupportedTroop(int value) => supportedTroopValues.Contains(value);
        internal bool IsSupportedEquipmentSlot(int value) =>
            supportedEquipmentSlotValues.Contains(value);

        internal bool IsPlayableRealm(int value) =>
            value != 0 && supportedRealmValues.Contains(value);

        internal bool IsSupportedStableId(SaveSemanticStableIdKind kind, string stableId)
        {
            HashSet<string> values;
            return stableIds.TryGetValue(kind, out values) &&
                   values.Contains(stableId ?? string.Empty);
        }

        internal IEnumerable<int> RequiredResources(bool legacy) =>
            legacy ? requiredLegacyResourceValues : requiredCurrentResourceValues;

        internal bool TryGetQuestTarget(string questId, out int targetValue) =>
            questTargets.TryGetValue(questId ?? string.Empty, out targetValue);

        private static HashSet<int> CopyValues(
            IEnumerable<int> values,
            string parameterName,
            bool allowEmpty)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var result = new HashSet<int>();
            foreach (var value in values)
            {
                if (!result.Add(value))
                {
                    throw new ArgumentException("Authority values must be unique.", parameterName);
                }

                if (result.Count > MaximumAuthorityValues)
                {
                    throw new ArgumentException(
                        "The authority value set exceeds the supported bound.",
                        parameterName);
                }
            }

            if (!allowEmpty && result.Count == 0)
            {
                throw new ArgumentException("Authority values cannot be empty.", parameterName);
            }

            return result;
        }
    }

    /// <summary>
    /// Immutable limits and compatibility knowledge used while inspecting one raw save.
    /// The validator does not read catalogs, Unity types, or files.
    /// </summary>
    public sealed class SaveSemanticNvs01Rule
    {
        public SaveSemanticNvs01Rule(
            int currentVersion,
            string packetVersion,
            string packetSha256,
            string questId)
            : this(
                currentVersion,
                packetVersion,
                packetSha256,
                questId,
                string.Empty,
                string.Empty)
        {
        }

        public SaveSemanticNvs01Rule(
            int currentVersion,
            string packetVersion,
            string packetSha256,
            string questId,
            string migratablePacketVersion,
            string migratablePacketSha256)
        {
            if (currentVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(currentVersion));
            if (string.IsNullOrWhiteSpace(packetVersion) ||
                packetVersion.Length > 256)
                throw new ArgumentException("Packet version is invalid.", nameof(packetVersion));
            if (string.IsNullOrWhiteSpace(packetSha256) ||
                packetSha256.Length > 256)
                throw new ArgumentException("Packet hash is invalid.", nameof(packetSha256));
            if (string.IsNullOrWhiteSpace(questId) || questId.Length > 256)
                throw new ArgumentException("Quest ID is invalid.", nameof(questId));
            bool hasMigratableVersion =
                !string.IsNullOrWhiteSpace(migratablePacketVersion);
            bool hasMigratableHash =
                !string.IsNullOrWhiteSpace(migratablePacketSha256);
            if (hasMigratableVersion != hasMigratableHash ||
                hasMigratableVersion &&
                (migratablePacketVersion.Length > 256 ||
                 migratablePacketSha256.Length > 256 ||
                 string.Equals(
                     packetVersion,
                     migratablePacketVersion,
                     StringComparison.Ordinal) ||
                 string.Equals(
                     packetSha256,
                     migratablePacketSha256,
                     StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Migratable packet identity is invalid.",
                    nameof(migratablePacketVersion));
            }

            CurrentVersion = currentVersion;
            PacketVersion = packetVersion;
            PacketSha256 = packetSha256;
            QuestId = questId;
            MigratablePacketVersion = migratablePacketVersion ?? string.Empty;
            MigratablePacketSha256 = migratablePacketSha256 ?? string.Empty;
        }

        public int CurrentVersion { get; }
        public string PacketVersion { get; }
        public string PacketSha256 { get; }
        public string QuestId { get; }
        internal string MigratablePacketVersion { get; }
        internal string MigratablePacketSha256 { get; }
        internal bool HasMigratablePacketIdentity =>
            MigratablePacketVersion.Length > 0 &&
            MigratablePacketSha256.Length > 0;
    }

    public sealed class SaveSemanticValidationPolicy
    {
        public const int DefaultMaximumInputBytes = 1024 * 1024;
        public const int AbsoluteMaximumInputBytes = DefaultMaximumInputBytes;
        public const int DefaultMaximumDiagnostics = 64;
        public const int AbsoluteMaximumDiagnostics = 256;

        public SaveSemanticValidationPolicy(
            string currentSaveFormatId,
            int currentSaveSchemaVersion,
            int currentProfileInitializationVersion,
            SaveSemanticValidationAuthority authority,
            int maximumInputBytes = DefaultMaximumInputBytes,
            int maximumDiagnostics = DefaultMaximumDiagnostics,
            SaveSemanticNvs01Rule nvs01Rule = null)
        {
            if (string.IsNullOrWhiteSpace(currentSaveFormatId) ||
                currentSaveFormatId.Length > 128)
            {
                throw new ArgumentException(
                    "The save format ID must be nonblank and at most 128 characters.",
                    nameof(currentSaveFormatId));
            }

            if (currentSaveSchemaVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentSaveSchemaVersion));
            }

            if (currentProfileInitializationVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentProfileInitializationVersion));
            }

            if (authority == null)
            {
                throw new ArgumentNullException(nameof(authority));
            }

            if (maximumInputBytes <= 0 || maximumInputBytes > AbsoluteMaximumInputBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumInputBytes));
            }

            if (maximumDiagnostics < 2 || maximumDiagnostics > AbsoluteMaximumDiagnostics)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDiagnostics));
            }

            CurrentSaveFormatId = currentSaveFormatId;
            CurrentSaveSchemaVersion = currentSaveSchemaVersion;
            CurrentProfileInitializationVersion = currentProfileInitializationVersion;
            Authority = authority;
            MaximumInputBytes = maximumInputBytes;
            MaximumDiagnostics = maximumDiagnostics;
            Nvs01Rule = nvs01Rule;
        }

        public string CurrentSaveFormatId { get; }
        public int CurrentSaveSchemaVersion { get; }
        public int CurrentProfileInitializationVersion { get; }
        public SaveSemanticValidationAuthority Authority { get; }
        public int MaximumInputBytes { get; }
        public int MaximumDiagnostics { get; }
        public SaveSemanticNvs01Rule Nvs01Rule { get; }
    }

    public sealed class SaveSemanticCandidate
    {
        private readonly byte[] rawBytes;

        internal SaveSemanticCandidate(
            SaveCandidateSourceGeneration sourceGeneration,
            SaveSemanticCandidateOutcome outcome,
            SaveSemanticDomain disabledDomains,
            SaveSemanticDomain normalizedDomains,
            SaveSemanticDomain preservedUnknownDomains,
            int saveSchemaVersion,
            int profileInitializationVersion,
            bool hasExplicitSaveSchemaVersion,
            bool hasExplicitProfileInitializationVersion,
            bool writable,
            byte[] rawBytes,
            int originalRawByteCount,
            IReadOnlyList<SaveSemanticDiagnostic> diagnostics)
        {
            SourceGeneration = sourceGeneration;
            Outcome = outcome;
            DisabledDomains = disabledDomains;
            NormalizedDomains = normalizedDomains;
            PreservedUnknownDomains = preservedUnknownDomains;
            SaveSchemaVersion = saveSchemaVersion;
            ProfileInitializationVersion = profileInitializationVersion;
            HasExplicitSaveSchemaVersion = hasExplicitSaveSchemaVersion;
            HasExplicitProfileInitializationVersion = hasExplicitProfileInitializationVersion;
            IsWritable = writable;
            this.rawBytes = Copy(rawBytes);
            OriginalRawByteCount = originalRawByteCount < 0 ? 0 : originalRawByteCount;
            Diagnostics = diagnostics ?? Array.AsReadOnly(new SaveSemanticDiagnostic[0]);
        }

        public SaveCandidateSourceGeneration SourceGeneration { get; }
        public SaveSemanticCandidateOutcome Outcome { get; }
        public SaveSemanticDomain DisabledDomains { get; }
        public SaveSemanticDomain NormalizedDomains { get; }
        public SaveSemanticDomain PreservedUnknownDomains { get; }
        public int SaveSchemaVersion { get; }
        public int ProfileInitializationVersion { get; }
        public bool HasExplicitSaveSchemaVersion { get; }
        public bool HasExplicitProfileInitializationVersion { get; }
        public bool IsWritable { get; }
        public int OriginalRawByteCount { get; }
        public bool HasRetainedRawBytes => rawBytes != null;
        public IReadOnlyList<SaveSemanticDiagnostic> Diagnostics { get; }

        /// <summary>
        /// Returns a new copy on every call. Accepted-size inputs are retained exactly;
        /// rejected oversize inputs deliberately are not duplicated in memory.
        /// </summary>
        public byte[] CopyRawBytes()
        {
            return Copy(rawBytes);
        }

        public int SelectionRank
        {
            get
            {
                switch (Outcome)
                {
                    case SaveSemanticCandidateOutcome.Valid:
                        return 6;
                    case SaveSemanticCandidateOutcome.CompatibleNormalized:
                        return 5;
                    case SaveSemanticCandidateOutcome.CompatiblePreservedUnknown:
                        return 4;
                    case SaveSemanticCandidateOutcome.DegradedMalformed:
                        return 3;
                    case SaveSemanticCandidateOutcome.RepairableWithDataChange:
                        return 2;
                    case SaveSemanticCandidateOutcome.Invalid:
                        return 1;
                    case SaveSemanticCandidateOutcome.ForwardSchemaReadOnly:
                        return 7;
                    case SaveSemanticCandidateOutcome.OversizePreservedReadOnly:
                        return 0;
                    default:
                        return 0;
                }
            }
        }

        internal static bool IsCleanSupported(SaveSemanticCandidate candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            return candidate.Outcome == SaveSemanticCandidateOutcome.Valid ||
                   candidate.Outcome == SaveSemanticCandidateOutcome.CompatibleNormalized ||
                   candidate.Outcome == SaveSemanticCandidateOutcome.CompatiblePreservedUnknown;
        }

        private static byte[] Copy(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            var copy = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
            return copy;
        }
    }

    public sealed class SaveSemanticCandidateSelection
    {
        internal SaveSemanticCandidateSelection(
            SaveSemanticCandidate selectedCandidate,
            string reasonCode)
        {
            SelectedCandidate = selectedCandidate;
            ReasonCode = reasonCode ?? string.Empty;
        }

        public SaveSemanticCandidate SelectedCandidate { get; }
        public SaveCandidateSourceGeneration SelectedSource =>
            SelectedCandidate == null
                ? SaveCandidateSourceGeneration.Unknown
                : SelectedCandidate.SourceGeneration;
        public string ReasonCode { get; }
        public bool HasSelection => SelectedCandidate != null;
        public bool IsWritable => SelectedCandidate != null && SelectedCandidate.IsWritable;
    }

    public static class SaveSemanticCandidateSelector
    {
        public static SaveSemanticCandidateSelection Select(
            SaveSemanticCandidate primary,
            SaveSemanticCandidate backup,
            SaveSemanticCandidate previous = null)
        {
            RequireSource(primary, SaveCandidateSourceGeneration.Primary, nameof(primary));
            RequireSource(backup, SaveCandidateSourceGeneration.Backup, nameof(backup));
            RequireSource(previous, SaveCandidateSourceGeneration.Previous, nameof(previous));

            if (primary != null &&
                primary.Outcome == SaveSemanticCandidateOutcome.ForwardSchemaReadOnly)
            {
                return new SaveSemanticCandidateSelection(
                    primary,
                    "SAVE_SELECT_FORWARD_PRIMARY_READ_ONLY");
            }

            // An oversize primary has not been parsed and deliberately has no retained
            // in-memory byte copy. Preserve it in place and require an explicit recovery
            // decision instead of presenting it as an active load candidate.
            if (primary != null &&
                primary.Outcome == SaveSemanticCandidateOutcome.OversizePreservedReadOnly)
            {
                return new SaveSemanticCandidateSelection(
                    null,
                    "SAVE_SELECT_OVERSIZE_PRIMARY_RECOVERY_REQUIRED");
            }

            // A bounded, exact migration candidate is the newest retained authority.
            // It must reach the reviewed atomic migration boundary instead of losing
            // to an older backup solely because that backup is already on the current
            // packet identity.
            if (primary != null &&
                primary.Outcome ==
                    SaveSemanticCandidateOutcome.RepairableWithDataChange &&
                primary.HasRetainedRawBytes &&
                primary.DisabledDomains == SaveSemanticDomain.None &&
                primary.NormalizedDomains == SaveSemanticDomain.None &&
                primary.PreservedUnknownDomains == SaveSemanticDomain.None &&
                primary.Diagnostics.Count == 1 &&
                primary.Diagnostics[0] != null &&
                primary.Diagnostics[0].Code ==
                    "SAVE_NVS01_PACKET_IDENTITY_MIGRATION_REQUIRED" &&
                primary.Diagnostics[0].Path == "$.Nvs01Progress" &&
                primary.Diagnostics[0].Domain ==
                    SaveSemanticDomain.Narrative &&
                primary.Diagnostics[0].Severity ==
                    SaveSemanticDiagnosticSeverity.Information)
            {
                return new SaveSemanticCandidateSelection(
                    primary,
                    "SAVE_SELECT_REPAIRABLE_PRIMARY");
            }

            // A supported primary is authoritative even when a backup has a nominally
            // cleaner rank. Preserved unknown records may be legitimate newer content.
            if (SaveSemanticCandidate.IsCleanSupported(primary))
            {
                return new SaveSemanticCandidateSelection(primary, "SAVE_SELECT_SUPPORTED_PRIMARY");
            }

            var active = PreferHigherRank(primary, backup);
            if (active != null &&
                (SaveSemanticCandidate.IsCleanSupported(active) ||
                 active.Outcome == SaveSemanticCandidateOutcome.ForwardSchemaReadOnly))
            {
                return new SaveSemanticCandidateSelection(
                    active,
                    active.SourceGeneration == SaveCandidateSourceGeneration.Backup
                        ? "SAVE_SELECT_CLEANER_BACKUP"
                        : "SAVE_SELECT_ACTIVE_CANDIDATE");
            }

            // Previous is a fallback generation. It is considered only after both
            // active generations have been evaluated, and only when it is cleaner.
            if (IsSelectable(previous) &&
                (active == null || previous.SelectionRank > active.SelectionRank))
            {
                return new SaveSemanticCandidateSelection(previous, "SAVE_SELECT_CLEANER_PREVIOUS");
            }

            if (IsSelectable(active))
            {
                return new SaveSemanticCandidateSelection(
                    active,
                    active.SourceGeneration == SaveCandidateSourceGeneration.Backup
                        ? "SAVE_SELECT_BETTER_BACKUP"
                        : "SAVE_SELECT_PRIMARY_TIE_OR_BETTER");
            }

            if (IsSelectable(previous))
            {
                return new SaveSemanticCandidateSelection(previous, "SAVE_SELECT_ONLY_PREVIOUS");
            }

            return new SaveSemanticCandidateSelection(null, "SAVE_SELECT_NONE");
        }

        private static bool IsSelectable(SaveSemanticCandidate candidate) =>
            candidate != null &&
            candidate.Outcome != SaveSemanticCandidateOutcome.Invalid &&
            candidate.Outcome != SaveSemanticCandidateOutcome.OversizePreservedReadOnly &&
            candidate.HasRetainedRawBytes;

        private static SaveSemanticCandidate PreferHigherRank(
            SaveSemanticCandidate primary,
            SaveSemanticCandidate backup)
        {
            if (primary == null)
            {
                return backup;
            }

            if (backup == null)
            {
                return primary;
            }

            // Strictly higher is required: a tie remains with primary.
            return backup.SelectionRank > primary.SelectionRank ? backup : primary;
        }

        private static void RequireSource(
            SaveSemanticCandidate candidate,
            SaveCandidateSourceGeneration expected,
            string parameterName)
        {
            if (candidate != null && candidate.SourceGeneration != expected)
            {
                throw new ArgumentException(
                    "Candidate provenance does not match the selection slot.",
                    parameterName);
            }
        }
    }

    public static class SaveSemanticCandidateValidator
    {
        public const int MaximumDomainRows = 4096;
        public const int MaximumResourceRows = 256;
        public const int MaximumStableIdCharacters = 256;
        private const int IdentityAwareSaveSchemaVersion = 2;

        private static readonly HashSet<string> RecognizedTopLevelFields =
            new HashSet<string>(
                new[]
                {
                    "SaveFormatId",
                    "SaveSchemaVersion",
                    "ProfileInitializationVersion",
                    "ProfileId",
                    "SelectedRealm",
                    "Resources",
                    "Buildings",
                    "Troops",
                    "Researches",
                    "Quests",
                    "Reputation",
                    "FactionReputations",
                    "LordPersona",
                    "Territories",
                    "RealmGems",
                    "Wishgate",
                    "CurrentChapterId",
                    "Warmaster",
                    "ChampionCustomization",
                    "OwnedEquipment",
                    "AppliedBossLootRewards",
                    "Nvs01Progress",
                    "WarzoneCredits",
                    "LastSavedTimestamp"
                },
                StringComparer.Ordinal);

        internal static SaveSemanticCandidate RejectNvs01MigrationTopology(
            SaveSemanticCandidate candidate)
        {
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            if (candidate.Outcome !=
                    SaveSemanticCandidateOutcome.RepairableWithDataChange ||
                !candidate.HasRetainedRawBytes)
            {
                throw new ArgumentException(
                    "Only a retained repairable candidate can be topology-rejected.",
                    nameof(candidate));
            }

            return new SaveSemanticCandidate(
                candidate.SourceGeneration,
                SaveSemanticCandidateOutcome.Invalid,
                SaveSemanticDomain.All,
                candidate.NormalizedDomains,
                candidate.PreservedUnknownDomains,
                candidate.SaveSchemaVersion,
                candidate.ProfileInitializationVersion,
                candidate.HasExplicitSaveSchemaVersion,
                candidate.HasExplicitProfileInitializationVersion,
                false,
                candidate.CopyRawBytes(),
                candidate.OriginalRawByteCount,
                Array.AsReadOnly(
                    new[]
                    {
                        new SaveSemanticDiagnostic(
                            "SAVE_NVS01_PACKET_TOPOLOGY_UNSUPPORTED",
                            "$.Nvs01Progress",
                            SaveSemanticDomain.Narrative,
                            SaveSemanticDiagnosticSeverity.Error)
                    }));
        }

        private static readonly HashSet<string> ResourceRowFields =
            new HashSet<string>(new[] { "Type", "Amount" }, StringComparer.Ordinal);

        private static readonly HashSet<string> QuestRowFields =
            new HashSet<string>(
                new[] { "QuestId", "CurrentValue", "IsCompleted", "IsClaimed" },
                StringComparer.Ordinal);

        private static readonly HashSet<string> ReputationRowFields =
            new HashSet<string>(new[] { "NpcId", "Affinity" }, StringComparer.Ordinal);

        private static readonly HashSet<string> FactionReputationRowFields =
            new HashSet<string>(new[] { "FactionId", "Reputation" }, StringComparer.Ordinal);

        private static readonly HashSet<string> PersonaFields =
            new HashSet<string>(
                new[] { "Warlord", "Diplomat", "Sage", "Rogue" },
                StringComparer.Ordinal);

        private static readonly HashSet<string> BuildingRowFields =
            Fields("BuildingId", "Level", "IsUpgrading", "UpgradeCompleteTimestamp");

        private static readonly HashSet<string> TroopRowFields =
            Fields("Type", "Count", "WoundedCount");

        private static readonly HashSet<string> ResearchRowFields =
            Fields("ResearchId", "Level", "IsResearching", "CompleteTimestamp");

        private static readonly HashSet<string> TerritoryRowFields =
            Fields("Id", "Name", "OwnerRealm", "BonusType", "BonusAmount", "IsFortress");

        private static readonly HashSet<string> RealmGemRowFields =
            Fields(
                "GemId",
                "HomeRealm",
                "GemIndex",
                "IsAtHome",
                "IsDropped",
                "CarrierId",
                "LastDroppedTimestamp");

        private static readonly HashSet<string> WishgateFields =
            Fields("IsEarned", "EarnReason", "LastRewardId", "LastRewardChosenTimestamp");

        private static readonly HashSet<string> WarmasterFields =
            Fields(
                "EquippedSetId",
                "UnlockedSetIds",
                "PurchasedPieceIds",
                "IsTrueWarmaster",
                "Level",
                "Experience");

        private static readonly HashSet<string> CurrentCustomizationFields =
            Fields(
                "BodyPresetId",
                "HairStyleId",
                "ArmorStyleId",
                "FaceMarkId",
                "WeaponStyleId",
                "OffhandStyleId",
                "PrimaryR",
                "PrimaryG",
                "PrimaryB",
                "HairR",
                "HairG",
                "HairB",
                "SkinR",
                "SkinG",
                "SkinB",
                "EyeR",
                "EyeG",
                "EyeB",
                "AccentR",
                "AccentG",
                "AccentB",
                "CapeEnabled",
                "HelmetEnabled");

        private static readonly HashSet<string> EquipmentRowFields =
            Fields(
                "EquipmentId",
                "DisplayName",
                "Slot",
                "AttackBonus",
                "DefenseBonus",
                "HealthBonus",
                "Quantity",
                "SourceBossId",
                "AnnounceWorldDrop",
                "FirstAcquiredTimestamp",
                "LastAcquiredTimestamp");

        private static readonly HashSet<string> AppliedBossLootRewardRowFields =
            Fields(
                "EncounterId",
                "RewardResultId",
                "BossId",
                "RewardDigest",
                "CommittedTimestamp");

        private static readonly HashSet<string> Nvs01ProgressFields =
            Fields(
                "Version",
                "PacketVersion",
                "PacketSha256",
                "QuestId",
                "Revision",
                "StateId",
                "Objectives",
                "CurrentDialogueNodeId",
                "PendingChoice",
                "PendingSemanticActionId",
                "CommittedRealmId",
                "EncounterStatus",
                "HasCurrentEncounter",
                "CurrentEncounter",
                "LastEncounterCorrelationId",
                "HasLastEncounterOutcome",
                "LastEncounterOutcome",
                "LastEncounterEventId",
                "LastEncounterSnapshotVersion",
                "LastEncounterSnapshotReference",
                "HasLastOperation",
                "LastOperation",
                "ConsequenceIntentIds",
                "AcquiredArtifactIds",
                "AppliedEffectKeys",
                "UnlockedChapterId");

        private static readonly HashSet<string> Nvs01ObjectiveFields =
            Fields("ObjectiveId", "Status");

        private static readonly HashSet<string> Nvs01EncounterFields =
            Fields(
                "ContractVersion",
                "RequestId",
                "CorrelationId",
                "QuestId",
                "StateId",
                "ObjectiveId",
                "HookId",
                "LocationId",
                "RealmId",
                "SuccessEventId",
                "FailureEventId",
                "CancelledEventId",
                "UnavailableEventId",
                "ReturnScene");

        private static readonly HashSet<string> Nvs01OperationFields =
            Fields(
                "OperationId",
                "PayloadFingerprint",
                "Status",
                "Revision",
                "StateId",
                "EventId",
                "CorrelationId",
                "ExpectedGenerationFingerprint");

        public static SaveSemanticCandidate Validate(
            byte[] rawBytes,
            SaveCandidateSourceGeneration sourceGeneration,
            SaveSemanticValidationPolicy policy)
        {
            if (sourceGeneration != SaveCandidateSourceGeneration.Primary &&
                sourceGeneration != SaveCandidateSourceGeneration.Backup &&
                sourceGeneration != SaveCandidateSourceGeneration.Previous &&
                sourceGeneration != SaveCandidateSourceGeneration.Temp)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceGeneration));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            var collector = new DiagnosticCollector(policy.MaximumDiagnostics);
            var originalByteCount = rawBytes == null ? 0 : rawBytes.Length;
            if (rawBytes == null)
            {
                collector.Add(
                    "SAVE_INPUT_NULL",
                    "$",
                    SaveSemanticDomain.Envelope,
                    SaveSemanticDiagnosticSeverity.Error);
                return CreateInvalid(sourceGeneration, null, originalByteCount, collector);
            }

            if (rawBytes.Length == 0)
            {
                collector.Add(
                    "SAVE_INPUT_EMPTY",
                    "$",
                    SaveSemanticDomain.Envelope,
                    SaveSemanticDiagnosticSeverity.Error);
                return CreateInvalid(sourceGeneration, rawBytes, originalByteCount, collector);
            }

            if (rawBytes.Length > policy.MaximumInputBytes)
            {
                collector.Add(
                    "SAVE_INPUT_TOO_LARGE",
                    "$",
                    SaveSemanticDomain.Envelope,
                    SaveSemanticDiagnosticSeverity.Warning);
                return Create(
                    sourceGeneration,
                    SaveSemanticCandidateOutcome.OversizePreservedReadOnly,
                    SaveSemanticDomain.All,
                    SaveSemanticDomain.None,
                    SaveSemanticDomain.Envelope,
                    0,
                    0,
                    false,
                    false,
                    false,
                    null,
                    originalByteCount,
                    collector);
            }

            StrictJsonValue parsed;
            try
            {
                parsed = StrictJsonDocument.Parse(rawBytes, policy.MaximumInputBytes);
            }
            catch (StrictJsonException exception)
            {
                collector.Add(
                    "SAVE_JSON_" + exception.Code,
                    exception.Path,
                    SaveSemanticDomain.Envelope,
                    SaveSemanticDiagnosticSeverity.Error);
                return CreateInvalid(sourceGeneration, rawBytes, originalByteCount, collector);
            }

            var root = parsed as StrictJsonObject;
            if (root == null)
            {
                collector.Add(
                    "SAVE_ROOT_NOT_OBJECT",
                    "$",
                    SaveSemanticDomain.Envelope,
                    SaveSemanticDiagnosticSeverity.Error);
                return CreateInvalid(sourceGeneration, rawBytes, originalByteCount, collector);
            }

            if (root.Properties.Count == 0)
            {
                collector.Add(
                    "SAVE_ROOT_EMPTY_OBJECT",
                    "$",
                    SaveSemanticDomain.Envelope,
                    SaveSemanticDiagnosticSeverity.Error);
                return CreateInvalid(sourceGeneration, rawBytes, originalByteCount, collector);
            }

            int schemaVersion;
            bool hasExplicitSchemaVersion;
            if (!TryReadOptionalInt32(
                    root,
                    "SaveSchemaVersion",
                    out schemaVersion,
                    out hasExplicitSchemaVersion))
            {
                collector.Add(
                    "SAVE_SCHEMA_VERSION_INVALID",
                    "$.SaveSchemaVersion",
                    SaveSemanticDomain.Metadata,
                    SaveSemanticDiagnosticSeverity.Error);
                return CreateInvalid(sourceGeneration, rawBytes, originalByteCount, collector);
            }

            if (schemaVersion < 0)
            {
                collector.Add(
                    "SAVE_SCHEMA_VERSION_NEGATIVE",
                    "$.SaveSchemaVersion",
                    SaveSemanticDomain.Metadata,
                    SaveSemanticDiagnosticSeverity.Error);
                return CreateInvalid(sourceGeneration, rawBytes, originalByteCount, collector);
            }

            int initializationVersion;
            bool hasExplicitInitializationVersion;
            if (!TryReadOptionalInt32(
                    root,
                    "ProfileInitializationVersion",
                    out initializationVersion,
                    out hasExplicitInitializationVersion))
            {
                collector.Add(
                    "SAVE_INITIALIZATION_VERSION_INVALID",
                    "$.ProfileInitializationVersion",
                    SaveSemanticDomain.Metadata,
                    SaveSemanticDiagnosticSeverity.Error);
                return CreateInvalid(sourceGeneration, rawBytes, originalByteCount, collector);
            }

            if (initializationVersion < 0)
            {
                collector.Add(
                    "SAVE_INITIALIZATION_VERSION_NEGATIVE",
                    "$.ProfileInitializationVersion",
                    SaveSemanticDomain.Metadata,
                    SaveSemanticDiagnosticSeverity.Error);
                return CreateInvalid(sourceGeneration, rawBytes, originalByteCount, collector);
            }

            var state = new ValidationState();

            if (schemaVersion > policy.CurrentSaveSchemaVersion ||
                (schemaVersion == policy.CurrentSaveSchemaVersion &&
                 initializationVersion > policy.CurrentProfileInitializationVersion))
            {
                if (!hasExplicitSchemaVersion ||
                    !hasExplicitInitializationVersion ||
                    !HasExactFormatId(root, policy.CurrentSaveFormatId))
                {
                    collector.Add(
                        "SAVE_FORWARD_FINGERPRINT_INCOMPLETE",
                        "$",
                        SaveSemanticDomain.Envelope,
                        SaveSemanticDiagnosticSeverity.Error);
                    return CreateInvalid(sourceGeneration, rawBytes, originalByteCount, collector);
                }

                InspectUnknownTopLevelFields(root, collector, state);
                collector.Add(
                    schemaVersion > policy.CurrentSaveSchemaVersion
                        ? "SAVE_FORWARD_SCHEMA_VERSION"
                        : "SAVE_FORWARD_INITIALIZATION_VERSION",
                    schemaVersion > policy.CurrentSaveSchemaVersion
                        ? "$.SaveSchemaVersion"
                        : "$.ProfileInitializationVersion",
                    SaveSemanticDomain.Metadata,
                    SaveSemanticDiagnosticSeverity.Warning);
                return Create(
                    sourceGeneration,
                    SaveSemanticCandidateOutcome.ForwardSchemaReadOnly,
                    SaveSemanticDomain.All,
                    SaveSemanticDomain.None,
                    state.PreservedUnknownDomains,
                    schemaVersion,
                    initializationVersion,
                    hasExplicitSchemaVersion,
                    hasExplicitInitializationVersion,
                    false,
                    rawBytes,
                    originalByteCount,
                    collector);
            }

            var isLegacySchema = !hasExplicitSchemaVersion || schemaVersion == 0;
            if (isLegacySchema)
            {
                if (!HasLegacyFingerprint(root))
                {
                    collector.Add(
                        "SAVE_LEGACY_FINGERPRINT_INCOMPLETE",
                        "$",
                        SaveSemanticDomain.Envelope,
                        SaveSemanticDiagnosticSeverity.Error);
                    return CreateInvalid(sourceGeneration, rawBytes, originalByteCount, collector);
                }

                state.NeedsNormalization = true;
                state.NormalizedDomains |= SaveSemanticDomain.Metadata;
                collector.Add(
                    "SAVE_LEGACY_SCHEMA_VERSION",
                    "$.SaveSchemaVersion",
                    SaveSemanticDomain.Metadata,
                    SaveSemanticDiagnosticSeverity.Information);

                StrictJsonValue formatValue;
                if (hasExplicitSchemaVersion || root.TryGet("SaveFormatId", out formatValue))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_LEGACY_METADATA_EXPLICIT",
                        hasExplicitSchemaVersion ? "$.SaveSchemaVersion" : "$.SaveFormatId",
                        SaveSemanticDomain.Metadata);
                }
            }
            else
            {
                if (!HasExactFormatId(root, policy.CurrentSaveFormatId))
                {
                    collector.Add(
                        "SAVE_FORMAT_ID_INVALID",
                        "$.SaveFormatId",
                        SaveSemanticDomain.Metadata,
                        SaveSemanticDiagnosticSeverity.Error);
                    return CreateInvalid(sourceGeneration, rawBytes, originalByteCount, collector);
                }

                if (!HasCurrentMaterialFootprint(root))
                {
                    collector.Add(
                        "SAVE_MATERIAL_FOOTPRINT_INCOMPLETE",
                        "$",
                        SaveSemanticDomain.Envelope,
                        SaveSemanticDiagnosticSeverity.Error);
                    return CreateInvalid(sourceGeneration, rawBytes, originalByteCount, collector);
                }

                if (schemaVersion < policy.CurrentSaveSchemaVersion)
                {
                    state.HasMalformedData = true;
                    state.DisabledDomains |= SaveSemanticDomain.All;
                    collector.Add(
                        "SAVE_LOWER_SCHEMA_UNSUPPORTED",
                        "$.SaveSchemaVersion",
                        SaveSemanticDomain.Metadata,
                        SaveSemanticDiagnosticSeverity.Error);
                }
            }

            InspectUnknownTopLevelFields(root, collector, state);

            if (!hasExplicitInitializationVersion || initializationVersion == 0)
            {
                if (isLegacySchema)
                {
                    state.NeedsNormalization = true;
                    state.NormalizedDomains |= SaveSemanticDomain.Metadata;
                    collector.Add(
                        "SAVE_LEGACY_INITIALIZATION_VERSION",
                        "$.ProfileInitializationVersion",
                        SaveSemanticDomain.Metadata,
                        SaveSemanticDiagnosticSeverity.Information);
                    if (hasExplicitInitializationVersion)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_LEGACY_METADATA_EXPLICIT",
                            "$.ProfileInitializationVersion",
                            SaveSemanticDomain.Metadata);
                    }
                }
                else
                {
                    state.HasMalformedData = true;
                    state.DisabledDomains |= SaveSemanticDomain.All;
                    collector.Add(
                        "SAVE_INITIALIZATION_VERSION_MISSING",
                        "$.ProfileInitializationVersion",
                        SaveSemanticDomain.Metadata,
                        SaveSemanticDiagnosticSeverity.Error);
                }
            }
            else if (initializationVersion < policy.CurrentProfileInitializationVersion)
            {
                state.HasMalformedData = true;
                state.DisabledDomains |= SaveSemanticDomain.All;
                collector.Add(
                    "SAVE_LOWER_INITIALIZATION_UNSUPPORTED",
                    "$.ProfileInitializationVersion",
                    SaveSemanticDomain.Metadata,
                    SaveSemanticDiagnosticSeverity.Error);
            }
            else if (isLegacySchema)
            {
                state.HasMalformedData = true;
                state.DisabledDomains |= SaveSemanticDomain.Metadata;
                collector.Add(
                    "SAVE_LEGACY_METADATA_CONTRADICTORY",
                    "$.ProfileInitializationVersion",
                    SaveSemanticDomain.Metadata,
                    SaveSemanticDiagnosticSeverity.Error);
            }

            ValidateTopLevelShape(
                root,
                isLegacySchema,
                schemaVersion,
                policy.Authority,
                collector,
                state);
            ValidateResourceRows(root, isLegacySchema, policy.Authority, collector, state);
            ValidateQuestRows(root, policy, collector, state);
            ValidateRelationshipRows(root, policy.Authority, collector, state);
            ValidateBuildingRows(root, policy.Authority, collector, state);
            ValidateTroopRows(root, policy.Authority, collector, state);
            ValidateResearchRows(root, policy.Authority, collector, state);
            ValidateTerritoryRows(root, policy.Authority, collector, state);
            ValidateRealmGemRows(root, policy.Authority, collector, state);
            ValidateWishgate(root, policy.Authority, collector, state);
            ValidateWarmaster(root, isLegacySchema, policy.Authority, collector, state);
            ValidateChampionCustomization(
                root,
                isLegacySchema,
                policy.Authority,
                collector,
                state);
            ValidateEquipmentRows(root, policy.Authority, collector, state);
            ValidateAppliedBossLootRewardRows(root, policy.Authority, collector, state);
            ValidateNvs01Progress(root, policy.Nvs01Rule, collector, state);

            SaveSemanticCandidateOutcome outcome;
            bool writable;
            if (state.HasMalformedData)
            {
                outcome = SaveSemanticCandidateOutcome.DegradedMalformed;
                writable = false;
            }
            else if (state.HasPreservedUnknown)
            {
                outcome = SaveSemanticCandidateOutcome.CompatiblePreservedUnknown;
                writable = !state.HasRawOnlyUnknown;
            }
            else if (state.NeedsDataChange)
            {
                outcome = SaveSemanticCandidateOutcome.RepairableWithDataChange;
                writable = false;
            }
            else if (state.NeedsNormalization)
            {
                outcome = SaveSemanticCandidateOutcome.CompatibleNormalized;
                writable = true;
            }
            else
            {
                outcome = SaveSemanticCandidateOutcome.Valid;
                writable = true;
            }

            return Create(
                sourceGeneration,
                outcome,
                state.DisabledDomains,
                state.NormalizedDomains,
                state.PreservedUnknownDomains,
                schemaVersion,
                initializationVersion,
                hasExplicitSchemaVersion,
                hasExplicitInitializationVersion,
                writable,
                rawBytes,
                originalByteCount,
                collector);
        }

        private static void InspectUnknownTopLevelFields(
            StrictJsonObject root,
            DiagnosticCollector collector,
            ValidationState state)
        {
            for (var index = 0; index < root.Properties.Count; index++)
            {
                var property = root.Properties[index];
                if (RecognizedTopLevelFields.Contains(property.Name))
                {
                    continue;
                }

                state.HasPreservedUnknown = true;
                state.HasRawOnlyUnknown = true;
                state.PreservedUnknownDomains |= SaveSemanticDomain.Envelope;
                collector.Add(
                    "SAVE_UNKNOWN_TOP_LEVEL_FIELD",
                    AppendPropertyPath("$", property.Name),
                    SaveSemanticDomain.Envelope,
                    SaveSemanticDiagnosticSeverity.Warning);
            }
        }

        private static bool HasLegacyFingerprint(StrictJsonObject root)
        {
            return HasInt32(root, "SelectedRealm") &&
                   HasArray(root, "Resources") &&
                   HasArray(root, "Buildings") &&
                   HasArray(root, "Troops") &&
                   HasArray(root, "Researches") &&
                   IsMissingNullOrArray(root, "Quests") &&
                   HasArray(root, "Territories") &&
                   HasArray(root, "RealmGems") &&
                   HasObject(root, "Wishgate") &&
                   HasString(root, "CurrentChapterId") &&
                   HasObject(root, "Warmaster") &&
                   HasObject(root, "ChampionCustomization") &&
                   HasInt64(root, "LastSavedTimestamp");
        }

        private static bool HasExactFormatId(StrictJsonObject root, string expected)
        {
            StrictJsonValue value;
            var text = root.TryGet("SaveFormatId", out value)
                ? value as StrictJsonString
                : null;
            return text != null && string.Equals(text.Value, expected, StringComparison.Ordinal);
        }

        private static bool HasCurrentMaterialFootprint(StrictJsonObject root)
        {
            StrictJsonValue ignored;
            return root.TryGet("SelectedRealm", out ignored) &&
                   root.TryGet("Resources", out ignored) &&
                   root.TryGet("LastSavedTimestamp", out ignored);
        }

        private static bool IsCanonicalProfileIdValue(string value)
        {
            if (value == null ||
                value.Length != 36 ||
                !value.StartsWith("alp_", StringComparison.Ordinal))
            {
                return false;
            }

            bool anyNonZero = false;
            for (int index = 4; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= '0' && character <= '9' ||
                      character >= 'a' && character <= 'f'))
                {
                    return false;
                }

                anyNonZero |= character != '0';
            }

            return anyNonZero;
        }

        private static void ValidateTopLevelShape(
            StrictJsonObject root,
            bool isLegacySchema,
            int schemaVersion,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            RequireInt32(root, "SelectedRealm", SaveSemanticDomain.Envelope, false, collector, state);
            RequireArray(root, "Resources", SaveSemanticDomain.Resources, false, collector, state);
            RequireArray(root, "Buildings", SaveSemanticDomain.Buildings, false, collector, state);
            RequireArray(root, "Troops", SaveSemanticDomain.Troops, false, collector, state);
            RequireArray(root, "Researches", SaveSemanticDomain.Research, false, collector, state);
            RequireArray(
                root,
                "Quests",
                SaveSemanticDomain.Quests,
                isLegacySchema,
                collector,
                state);
            RequireArray(
                root,
                "Reputation",
                SaveSemanticDomain.Relationships,
                isLegacySchema,
                collector,
                state);
            RequireArray(
                root,
                "FactionReputations",
                SaveSemanticDomain.Relationships,
                isLegacySchema,
                collector,
                state);
            RequireObject(
                root,
                "LordPersona",
                SaveSemanticDomain.Relationships,
                isLegacySchema,
                collector,
                state);
            RequireArray(root, "Territories", SaveSemanticDomain.Territories, false, collector, state);
            RequireArray(root, "RealmGems", SaveSemanticDomain.RealmGems, false, collector, state);
            RequireObject(root, "Wishgate", SaveSemanticDomain.Envelope, false, collector, state);
            RequireObject(root, "Warmaster", SaveSemanticDomain.Warmaster, false, collector, state);
            RequireObject(
                root,
                "ChampionCustomization",
                SaveSemanticDomain.Customization,
                false,
                collector,
                state);
            RequireArray(
                root,
                "OwnedEquipment",
                SaveSemanticDomain.Equipment,
                isLegacySchema,
                collector,
                state);
            RequireArray(
                root,
                "AppliedBossLootRewards",
                SaveSemanticDomain.Envelope,
                isLegacySchema,
                collector,
                state);
            RequireInt32(
                root,
                "WarzoneCredits",
                SaveSemanticDomain.Resources,
                false,
                collector,
                state);
            RequireInt64(root, "LastSavedTimestamp", SaveSemanticDomain.Envelope, false, collector, state);

            StrictJsonValue profileIdValue;
            bool hasProfileId = root.TryGet("ProfileId", out profileIdValue);
            if (schemaVersion >= IdentityAwareSaveSchemaVersion)
            {
                // Post-migration identity-aware schema: one canonical ProfileId
                // is required. Absence, blank, or malformed identity is invalid
                // and is never neutral-normalized into a writable profile.
                var profileId = hasProfileId
                    ? profileIdValue as StrictJsonString
                    : null;
                if (profileId == null ||
                    !IsCanonicalProfileIdValue(profileId.Value))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_SCHEMA_V2_PROFILE_ID_INVALID",
                        "$.ProfileId",
                        SaveSemanticDomain.Metadata);
                }
            }
            else if (hasProfileId)
            {
                // Schema-v1 and legacy profiles must serialize ProfileId as blank;
                // a nonblank value is malformed and remains MigrationRequired.
                var profileId = profileIdValue as StrictJsonString;
                if (profileId == null || profileId.Value.Length != 0)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_SCHEMA_V1_PROFILE_ID_INVALID",
                        "$.ProfileId",
                        SaveSemanticDomain.Metadata);
                }
            }

            StrictJsonValue realmValue;
            int realm;
            if (root.TryGet("SelectedRealm", out realmValue) &&
                TryReadInt32(realmValue, out realm) &&
                !authority.IsSupportedRealm(realm))
            {
                MarkPreservedUnknown(
                    state,
                    collector,
                    "SAVE_REALM_ENUM_PRESERVED_UNKNOWN",
                    "$.SelectedRealm",
                    SaveSemanticDomain.Envelope,
                    rawOnly: true);
            }

            StrictJsonValue chapterValue;
            if (!root.TryGet("CurrentChapterId", out chapterValue) ||
                chapterValue is StrictJsonNull)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_CHAPTER_ID_MISSING",
                    "$.CurrentChapterId",
                    SaveSemanticDomain.Chapter);
            }
            else
            {
                var chapter = chapterValue as StrictJsonString;
                if (chapter == null || string.IsNullOrWhiteSpace(chapter.Value))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_CHAPTER_ID_BLANK",
                        "$.CurrentChapterId",
                        SaveSemanticDomain.Chapter);
                }
                else if (chapter.Value.Length > MaximumStableIdCharacters)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_CHAPTER_ID_TOO_LONG",
                        "$.CurrentChapterId",
                        SaveSemanticDomain.Chapter);
                }
                else
                {
                    MarkUnsupportedStableId(
                        authority,
                        SaveSemanticStableIdKind.Chapter,
                        chapter.Value,
                        "$.CurrentChapterId",
                        SaveSemanticDomain.Chapter,
                        collector,
                        state);
                }
            }

            StrictJsonValue creditsValue;
            long credits;
            if (root.TryGet("WarzoneCredits", out creditsValue) &&
                TryReadInt64(creditsValue, out credits) &&
                credits < 0)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_WARZONE_CREDITS_NEGATIVE",
                    "$.WarzoneCredits",
                    SaveSemanticDomain.Resources);
            }

            StrictJsonValue timestampValue;
            long timestamp;
            if (root.TryGet("LastSavedTimestamp", out timestampValue) &&
                TryReadInt64(timestampValue, out timestamp) &&
                timestamp <= 0)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_TIMESTAMP_NOT_POSITIVE",
                    "$.LastSavedTimestamp",
                    SaveSemanticDomain.Envelope);
            }
        }

        private static void ValidateResourceRows(
            StrictJsonObject root,
            bool isLegacySchema,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue value;
            if (!root.TryGet("Resources", out value))
            {
                return;
            }

            var resources = value as StrictJsonArray;
            if (resources == null)
            {
                return;
            }

            var rowCount = resources.Items.Count;
            if (rowCount > MaximumResourceRows)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_RESOURCE_ROW_LIMIT",
                    "$.Resources",
                    SaveSemanticDomain.Resources);
                rowCount = MaximumResourceRows;
            }

            var firstPathByType = new Dictionary<int, string>();
            var duplicateTypes = new HashSet<int>();
            for (var index = 0; index < rowCount; index++)
            {
                var path = "$.Resources[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                var item = resources.Items[index];
                if (item is StrictJsonNull)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_RESOURCE_ROW_NULL",
                        path,
                        SaveSemanticDomain.Resources);
                    continue;
                }

                var row = item as StrictJsonObject;
                if (row == null)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_RESOURCE_ROW_NOT_OBJECT",
                        path,
                        SaveSemanticDomain.Resources);
                    continue;
                }

                InspectUnexpectedProperties(
                    row,
                    ResourceRowFields,
                    path,
                    SaveSemanticDomain.Resources,
                    collector,
                    state);

                StrictJsonValue typeValue;
                int type;
                var typeValid = false;
                var typeSupported = false;
                if (!row.TryGet("Type", out typeValue) || !TryReadInt32(typeValue, out type))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_RESOURCE_ID_BLANK_OR_INVALID",
                        path + ".Type",
                        SaveSemanticDomain.Resources);
                }
                else
                {
                    typeValid = true;
                    typeSupported = authority.IsSupportedResource(type);
                    if (!typeSupported)
                    {
                        MarkPreservedUnknown(
                            state,
                            collector,
                            "SAVE_RESOURCE_ENUM_PRESERVED_UNKNOWN",
                            path + ".Type",
                            SaveSemanticDomain.Resources,
                            rawOnly: true);
                    }

                    string firstPath;
                    if (firstPathByType.TryGetValue(type, out firstPath))
                    {
                        if (duplicateTypes.Add(type))
                        {
                            MarkMalformed(
                                state,
                                collector,
                                "SAVE_RESOURCE_ID_DUPLICATE",
                                firstPath + ".Type",
                                SaveSemanticDomain.Resources);
                        }

                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_RESOURCE_ID_DUPLICATE",
                            path + ".Type",
                            SaveSemanticDomain.Resources);
                    }
                    else
                    {
                        firstPathByType.Add(type, path);
                    }
                }

                StrictJsonValue amountValue;
                long amount;
                if (!row.TryGet("Amount", out amountValue) || !TryReadInt64(amountValue, out amount))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_RESOURCE_AMOUNT_INVALID",
                        path + ".Amount",
                        SaveSemanticDomain.Resources);
                }
                else if (typeValid && typeSupported && amount < 0)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_RESOURCE_AMOUNT_NEGATIVE",
                        path + ".Amount",
                        SaveSemanticDomain.Resources);
                }
            }

            foreach (var requiredType in authority.RequiredResources(isLegacySchema))
            {
                if (firstPathByType.ContainsKey(requiredType))
                {
                    continue;
                }

                MarkMalformed(
                    state,
                    collector,
                    "SAVE_RESOURCE_REQUIRED_TYPE_MISSING",
                    "$.Resources",
                    SaveSemanticDomain.Resources);
            }

            if (isLegacySchema)
            {
                foreach (var currentRequiredType in authority.RequiredResources(false))
                {
                    if (firstPathByType.ContainsKey(currentRequiredType) ||
                        authority.IsRequiredLegacyResource(currentRequiredType))
                    {
                        continue;
                    }

                    // These wallet types did not exist in the original profile shape.
                    // Their only approved migration value is neutral zero; starting
                    // balances must never be inferred during generic normalization.
                    MarkNormalized(
                        state,
                        collector,
                        "SAVE_LEGACY_RESOURCE_ZERO_MIGRATION",
                        "$.Resources",
                        SaveSemanticDomain.Resources);
                }
            }
        }

        private static void ValidateQuestRows(
            StrictJsonObject root,
            SaveSemanticValidationPolicy policy,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue value;
            if (!root.TryGet("Quests", out value))
            {
                return;
            }

            var quests = value as StrictJsonArray;
            if (quests == null)
            {
                return;
            }

            var rowCount = quests.Items.Count;
            if (rowCount > MaximumDomainRows)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_QUEST_ROW_LIMIT",
                    "$.Quests",
                    SaveSemanticDomain.Quests);
                rowCount = MaximumDomainRows;
            }

            var firstPathById = new Dictionary<string, string>(StringComparer.Ordinal);
            var duplicateIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < rowCount; index++)
            {
                var path = "$.Quests[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                var item = quests.Items[index];
                if (item is StrictJsonNull)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_QUEST_ROW_NULL",
                        path,
                        SaveSemanticDomain.Quests);
                    continue;
                }

                var row = item as StrictJsonObject;
                if (row == null)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_QUEST_ROW_NOT_OBJECT",
                        path,
                        SaveSemanticDomain.Quests);
                    continue;
                }

                InspectUnexpectedProperties(
                    row,
                    QuestRowFields,
                    path,
                    SaveSemanticDomain.Quests,
                    collector,
                    state);

                StrictJsonValue idValue;
                var idString = default(StrictJsonString);
                if (row.TryGet("QuestId", out idValue))
                {
                    idString = idValue as StrictJsonString;
                }

                var questId = idString == null ? null : idString.Value;
                if (string.IsNullOrWhiteSpace(questId) ||
                    questId.Length > MaximumStableIdCharacters)
                {
                    MarkMalformed(
                        state,
                        collector,
                        string.IsNullOrWhiteSpace(questId)
                            ? "SAVE_QUEST_ID_BLANK"
                            : "SAVE_QUEST_ID_TOO_LONG",
                        path + ".QuestId",
                        SaveSemanticDomain.Quests);
                }
                else
                {
                    string firstPath;
                    if (firstPathById.TryGetValue(questId, out firstPath))
                    {
                        if (duplicateIds.Add(questId))
                        {
                            MarkMalformed(
                                state,
                                collector,
                                "SAVE_QUEST_ID_DUPLICATE",
                                firstPath + ".QuestId",
                                SaveSemanticDomain.Quests);
                        }

                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_QUEST_ID_DUPLICATE",
                            path + ".QuestId",
                            SaveSemanticDomain.Quests);
                    }
                    else
                    {
                        firstPathById.Add(questId, path);
                    }
                }

                StrictJsonValue progressValue;
                var progress = 0;
                var progressValid = false;
                if (!row.TryGet("CurrentValue", out progressValue) ||
                    !TryReadInt32(progressValue, out progress))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_QUEST_PROGRESS_INVALID",
                        path + ".CurrentValue",
                        SaveSemanticDomain.Quests);
                }
                else
                {
                    progressValid = true;
                }

                bool completed;
                bool claimed;
                var completedValid = TryReadRequiredBoolean(row, "IsCompleted", out completed);
                var claimedValid = TryReadRequiredBoolean(row, "IsClaimed", out claimed);
                if (!completedValid)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_QUEST_COMPLETED_INVALID",
                        path + ".IsCompleted",
                        SaveSemanticDomain.Quests);
                }

                if (!claimedValid)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_QUEST_CLAIMED_INVALID",
                        path + ".IsClaimed",
                        SaveSemanticDomain.Quests);
                }

                var targetValue = 0;
                var knownQuest = !string.IsNullOrWhiteSpace(questId) &&
                    policy.Authority.TryGetQuestTarget(questId, out targetValue);
                if (knownQuest && progressValid && progress < 0)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_QUEST_PROGRESS_NEGATIVE",
                        path + ".CurrentValue",
                        SaveSemanticDomain.Quests);
                    progressValid = false;
                }

                if (knownQuest && completedValid && claimedValid && claimed && !completed)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_QUEST_STATE_CONTRADICTORY",
                        path,
                        SaveSemanticDomain.Quests);
                }

                if (knownQuest && progressValid && completedValid)
                {
                    var consistent = completed
                        ? progress == targetValue
                        : progress < targetValue;
                    if (!consistent)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_QUEST_STATE_CONTRADICTORY",
                            path,
                            SaveSemanticDomain.Quests);
                    }
                }

                if (!string.IsNullOrWhiteSpace(questId) &&
                    !knownQuest)
                {
                    MarkPreservedUnknown(
                        state,
                        collector,
                        "SAVE_QUEST_ID_PRESERVED_UNKNOWN",
                        path + ".QuestId",
                        SaveSemanticDomain.Quests,
                        rawOnly: false);
                }
            }
        }

        private static void ValidateRelationshipRows(
            StrictJsonObject root,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            ValidateRelationshipArray(
                root,
                "Reputation",
                "NpcId",
                "Affinity",
                true,
                authority,
                SaveSemanticStableIdKind.Npc,
                collector,
                state);
            ValidateRelationshipArray(
                root,
                "FactionReputations",
                "FactionId",
                "Reputation",
                false,
                authority,
                SaveSemanticStableIdKind.Faction,
                collector,
                state);

            StrictJsonValue personaValue;
            if (!root.TryGet("LordPersona", out personaValue) ||
                personaValue is StrictJsonNull)
            {
                return;
            }

            var persona = personaValue as StrictJsonObject;
            if (persona == null)
            {
                return;
            }

            InspectUnexpectedProperties(
                persona,
                PersonaFields,
                "$.LordPersona",
                SaveSemanticDomain.Relationships,
                collector,
                state);

            var fields = new[] { "Warlord", "Diplomat", "Sage", "Rogue" };
            for (var index = 0; index < fields.Length; index++)
            {
                StrictJsonValue fieldValue;
                int ignored;
                if (!persona.TryGet(fields[index], out fieldValue))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_PERSONA_FIELD_MISSING",
                        "$.LordPersona." + fields[index],
                        SaveSemanticDomain.Relationships);
                }
                else if (!TryReadInt32(fieldValue, out ignored))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_PERSONA_FIELD_INVALID",
                        "$.LordPersona." + fields[index],
                        SaveSemanticDomain.Relationships);
                }
            }
        }

        private static void ValidateRelationshipArray(
            StrictJsonObject root,
            string collectionName,
            string idField,
            string valueField,
            bool floatingValue,
            SaveSemanticValidationAuthority authority,
            SaveSemanticStableIdKind stableIdKind,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue collectionValue;
            if (!root.TryGet(collectionName, out collectionValue))
            {
                return;
            }

            var collection = collectionValue as StrictJsonArray;
            if (collection == null)
            {
                return;
            }

            var rowCount = collection.Items.Count;
            if (rowCount > MaximumDomainRows)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_RELATIONSHIP_ROW_LIMIT",
                    "$." + collectionName,
                    SaveSemanticDomain.Relationships);
                rowCount = MaximumDomainRows;
            }

            var firstPathById = new Dictionary<string, string>(StringComparer.Ordinal);
            var duplicateIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < rowCount; index++)
            {
                var path = "$." + collectionName + "[" +
                           index.ToString(CultureInfo.InvariantCulture) + "]";
                var item = collection.Items[index];
                if (item is StrictJsonNull)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_RELATIONSHIP_ROW_NULL",
                        path,
                        SaveSemanticDomain.Relationships);
                    continue;
                }

                var row = item as StrictJsonObject;
                if (row == null)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_RELATIONSHIP_ROW_NOT_OBJECT",
                        path,
                        SaveSemanticDomain.Relationships);
                    continue;
                }

                InspectUnexpectedProperties(
                    row,
                    floatingValue ? ReputationRowFields : FactionReputationRowFields,
                    path,
                    SaveSemanticDomain.Relationships,
                    collector,
                    state);

                StrictJsonValue idValue;
                var idString = default(StrictJsonString);
                if (row.TryGet(idField, out idValue))
                {
                    idString = idValue as StrictJsonString;
                }

                var stableId = idString == null ? null : idString.Value;
                if (string.IsNullOrWhiteSpace(stableId) ||
                    stableId.Length > MaximumStableIdCharacters)
                {
                    MarkMalformed(
                        state,
                        collector,
                        string.IsNullOrWhiteSpace(stableId)
                            ? "SAVE_RELATIONSHIP_ID_BLANK"
                            : "SAVE_RELATIONSHIP_ID_TOO_LONG",
                        path + "." + idField,
                        SaveSemanticDomain.Relationships);
                }
                else
                {
                    string firstPath;
                    if (firstPathById.TryGetValue(stableId, out firstPath))
                    {
                        if (duplicateIds.Add(stableId))
                        {
                            MarkMalformed(
                                state,
                                collector,
                                "SAVE_RELATIONSHIP_ID_DUPLICATE",
                                firstPath + "." + idField,
                                SaveSemanticDomain.Relationships);
                        }

                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_RELATIONSHIP_ID_DUPLICATE",
                            path + "." + idField,
                            SaveSemanticDomain.Relationships);
                    }
                    else
                    {
                        firstPathById.Add(stableId, path);
                    }

                    MarkUnsupportedStableId(
                        authority,
                        stableIdKind,
                        stableId,
                        path + "." + idField,
                        SaveSemanticDomain.Relationships,
                        collector,
                        state);
                }

                StrictJsonValue relationshipValue;
                if (!row.TryGet(valueField, out relationshipValue))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_RELATIONSHIP_VALUE_INVALID",
                        path + "." + valueField,
                        SaveSemanticDomain.Relationships);
                    continue;
                }

                if (floatingValue)
                {
                    float affinity;
                    if (!TryReadFiniteSingle(relationshipValue, out affinity) ||
                        affinity < -100f ||
                        affinity > 100f)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_RELATIONSHIP_VALUE_INVALID",
                            path + "." + valueField,
                            SaveSemanticDomain.Relationships);
                    }
                }
                else
                {
                    int ignored;
                    if (!TryReadInt32(relationshipValue, out ignored))
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_RELATIONSHIP_VALUE_INVALID",
                            path + "." + valueField,
                            SaveSemanticDomain.Relationships);
                    }
                }
            }
        }

        private static void ValidateBuildingRows(
            StrictJsonObject root,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            var firstPathById = new Dictionary<string, string>(StringComparer.Ordinal);
            ValidateObjectRows(
                root,
                "Buildings",
                SaveSemanticDomain.Buildings,
                BuildingRowFields,
                collector,
                state,
                (row, path) =>
                {
                    var buildingId = ValidateUniqueStableId(
                        row,
                        "BuildingId",
                        path,
                        SaveSemanticDomain.Buildings,
                        firstPathById,
                        collector,
                        state);
                    MarkUnsupportedStableId(
                        authority,
                        SaveSemanticStableIdKind.Building,
                        buildingId,
                        path + ".BuildingId",
                        SaveSemanticDomain.Buildings,
                        collector,
                        state);

                    int level;
                    bool levelValid = TryReadRequiredInt32(
                            row,
                            "Level",
                            path,
                            SaveSemanticDomain.Buildings,
                            collector,
                            state,
                            out level);
                    if (levelValid && (level < 0 || level > 10))
                    {
                        MarkMalformed(
                            state,
                            collector,
                            level < 0
                                ? "SAVE_BUILDING_LEVEL_NEGATIVE"
                                : "SAVE_BUILDING_LEVEL_ABOVE_CAP",
                            path + ".Level",
                            SaveSemanticDomain.Buildings);
                    }

                    bool upgrading;
                    var upgradingValid = TryReadRequiredBoolean(
                        row,
                        "IsUpgrading",
                        path,
                        SaveSemanticDomain.Buildings,
                        collector,
                        state,
                        out upgrading);
                    long timestamp;
                    var timestampValid = TryReadRequiredInt64(
                        row,
                        "UpgradeCompleteTimestamp",
                        path,
                        SaveSemanticDomain.Buildings,
                        collector,
                        state,
                        out timestamp);
                    if (timestampValid && timestamp < 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_BUILDING_TIMESTAMP_NEGATIVE",
                            path + ".UpgradeCompleteTimestamp",
                            SaveSemanticDomain.Buildings);
                    }
                    else if (upgradingValid && timestampValid && upgrading && timestamp <= 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_BUILDING_TIMER_CONTRADICTORY",
                            path,
                            SaveSemanticDomain.Buildings);
                    }
                    else if (levelValid &&
                             upgradingValid &&
                             upgrading &&
                             level >= 10)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_BUILDING_UPGRADE_ABOVE_CAP",
                            path,
                            SaveSemanticDomain.Buildings);
                    }
                });
        }

        private static void ValidateTroopRows(
            StrictJsonObject root,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            var firstPathByType = new Dictionary<int, string>();
            ValidateObjectRows(
                root,
                "Troops",
                SaveSemanticDomain.Troops,
                TroopRowFields,
                collector,
                state,
                (row, path) =>
                {
                    int type;
                    if (TryReadRequiredInt32(
                            row,
                            "Type",
                            path,
                            SaveSemanticDomain.Troops,
                            collector,
                            state,
                            out type))
                    {
                        ValidateUniqueIntegerIdentity(
                            type,
                            "Type",
                            path,
                            SaveSemanticDomain.Troops,
                            firstPathByType,
                            collector,
                            state);
                        if (!authority.IsSupportedTroop(type))
                        {
                            MarkPreservedUnknown(
                                state,
                                collector,
                                "SAVE_TROOP_ENUM_PRESERVED_UNKNOWN",
                                path + ".Type",
                                SaveSemanticDomain.Troops,
                                rawOnly: true);
                        }
                    }

                    int count;
                    var countValid = TryReadRequiredInt32(
                        row,
                        "Count",
                        path,
                        SaveSemanticDomain.Troops,
                        collector,
                        state,
                        out count);
                    int wounded;
                    var woundedValid = TryReadRequiredInt32(
                        row,
                        "WoundedCount",
                        path,
                        SaveSemanticDomain.Troops,
                        collector,
                        state,
                        out wounded);
                    if (countValid && count < 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_TROOP_COUNT_NEGATIVE",
                            path + ".Count",
                            SaveSemanticDomain.Troops);
                    }

                    if (woundedValid && wounded < 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_TROOP_WOUNDED_NEGATIVE",
                            path + ".WoundedCount",
                            SaveSemanticDomain.Troops);
                    }
                    else if (countValid && woundedValid && wounded > count)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_TROOP_COUNTS_CONTRADICTORY",
                            path,
                            SaveSemanticDomain.Troops);
                    }
                });
        }

        private static void ValidateResearchRows(
            StrictJsonObject root,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            var firstPathById = new Dictionary<string, string>(StringComparer.Ordinal);
            ValidateObjectRows(
                root,
                "Researches",
                SaveSemanticDomain.Research,
                ResearchRowFields,
                collector,
                state,
                (row, path) =>
                {
                    var researchId = ValidateUniqueStableId(
                        row,
                        "ResearchId",
                        path,
                        SaveSemanticDomain.Research,
                        firstPathById,
                        collector,
                        state);
                    MarkUnsupportedStableId(
                        authority,
                        SaveSemanticStableIdKind.Research,
                        researchId,
                        path + ".ResearchId",
                        SaveSemanticDomain.Research,
                        collector,
                        state);

                    int level;
                    if (TryReadRequiredInt32(
                            row,
                            "Level",
                            path,
                            SaveSemanticDomain.Research,
                            collector,
                            state,
                            out level) &&
                        level < 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_RESEARCH_LEVEL_NEGATIVE",
                            path + ".Level",
                            SaveSemanticDomain.Research);
                    }

                    bool researching;
                    var researchingValid = TryReadRequiredBoolean(
                        row,
                        "IsResearching",
                        path,
                        SaveSemanticDomain.Research,
                        collector,
                        state,
                        out researching);
                    long timestamp;
                    var timestampValid = TryReadRequiredInt64(
                        row,
                        "CompleteTimestamp",
                        path,
                        SaveSemanticDomain.Research,
                        collector,
                        state,
                        out timestamp);
                    if (timestampValid && timestamp < 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_RESEARCH_TIMESTAMP_NEGATIVE",
                            path + ".CompleteTimestamp",
                            SaveSemanticDomain.Research);
                    }
                    else if (researchingValid && timestampValid && researching && timestamp <= 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_RESEARCH_TIMER_CONTRADICTORY",
                            path,
                            SaveSemanticDomain.Research);
                    }
                });
        }

        private static void ValidateTerritoryRows(
            StrictJsonObject root,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            var firstPathById = new Dictionary<string, string>(StringComparer.Ordinal);
            ValidateObjectRows(
                root,
                "Territories",
                SaveSemanticDomain.Territories,
                TerritoryRowFields,
                collector,
                state,
                (row, path) =>
                {
                    var territoryId = ValidateUniqueStableId(
                        row,
                        "Id",
                        path,
                        SaveSemanticDomain.Territories,
                        firstPathById,
                        collector,
                        state);
                    MarkUnsupportedStableId(
                        authority,
                        SaveSemanticStableIdKind.Territory,
                        territoryId,
                        path + ".Id",
                        SaveSemanticDomain.Territories,
                        collector,
                        state);
                    string ignoredText;
                    TryReadRequiredOpaqueString(
                        row,
                        "Name",
                        path,
                        SaveSemanticDomain.Territories,
                        allowBlank: true,
                        collector,
                        state,
                        out ignoredText);

                    int ownerRealm;
                    if (TryReadRequiredInt32(
                            row,
                            "OwnerRealm",
                            path,
                            SaveSemanticDomain.Territories,
                            collector,
                            state,
                            out ownerRealm) &&
                        !authority.IsSupportedRealm(ownerRealm))
                    {
                        MarkPreservedUnknown(
                            state,
                            collector,
                            "SAVE_TERRITORY_REALM_PRESERVED_UNKNOWN",
                            path + ".OwnerRealm",
                            SaveSemanticDomain.Territories,
                            rawOnly: true);
                    }

                    int bonusType;
                    if (TryReadRequiredInt32(
                            row,
                            "BonusType",
                            path,
                            SaveSemanticDomain.Territories,
                            collector,
                            state,
                            out bonusType) &&
                        !authority.IsSupportedResource(bonusType))
                    {
                        MarkPreservedUnknown(
                            state,
                            collector,
                            "SAVE_TERRITORY_RESOURCE_PRESERVED_UNKNOWN",
                            path + ".BonusType",
                            SaveSemanticDomain.Territories,
                            rawOnly: true);
                    }

                    long bonusAmount;
                    if (TryReadRequiredInt64(
                            row,
                            "BonusAmount",
                            path,
                            SaveSemanticDomain.Territories,
                            collector,
                            state,
                            out bonusAmount) &&
                        bonusAmount < 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_TERRITORY_BONUS_NEGATIVE",
                            path + ".BonusAmount",
                            SaveSemanticDomain.Territories);
                    }

                    bool ignoredBoolean;
                    TryReadRequiredBoolean(
                        row,
                        "IsFortress",
                        path,
                        SaveSemanticDomain.Territories,
                        collector,
                        state,
                        out ignoredBoolean);
                });
        }

        private static void ValidateRealmGemRows(
            StrictJsonObject root,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            var firstPathById = new Dictionary<string, string>(StringComparer.Ordinal);
            var firstPathBySlot = new Dictionary<string, string>(StringComparer.Ordinal);
            ValidateObjectRows(
                root,
                "RealmGems",
                SaveSemanticDomain.RealmGems,
                RealmGemRowFields,
                collector,
                state,
                (row, path) =>
                {
                    var gemId = ValidateUniqueStableId(
                        row,
                        "GemId",
                        path,
                        SaveSemanticDomain.RealmGems,
                        firstPathById,
                        collector,
                        state);
                    MarkUnsupportedStableId(
                        authority,
                        SaveSemanticStableIdKind.RealmGem,
                        gemId,
                        path + ".GemId",
                        SaveSemanticDomain.RealmGems,
                        collector,
                        state);

                    int homeRealm;
                    var realmValid = TryReadRequiredInt32(
                        row,
                        "HomeRealm",
                        path,
                        SaveSemanticDomain.RealmGems,
                        collector,
                        state,
                        out homeRealm);
                    if (realmValid && homeRealm == 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_GEM_HOME_REALM_NONE",
                            path + ".HomeRealm",
                            SaveSemanticDomain.RealmGems);
                    }
                    else if (realmValid && !authority.IsPlayableRealm(homeRealm))
                    {
                        MarkPreservedUnknown(
                            state,
                            collector,
                            "SAVE_GEM_REALM_PRESERVED_UNKNOWN",
                            path + ".HomeRealm",
                            SaveSemanticDomain.RealmGems,
                            rawOnly: true);
                    }

                    int gemIndex;
                    var indexValid = TryReadRequiredInt32(
                        row,
                        "GemIndex",
                        path,
                        SaveSemanticDomain.RealmGems,
                        collector,
                        state,
                        out gemIndex);
                    if (indexValid && gemIndex < 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_GEM_INDEX_NEGATIVE",
                            path + ".GemIndex",
                            SaveSemanticDomain.RealmGems);
                    }
                    else if (realmValid && indexValid)
                    {
                        var slot = homeRealm.ToString(CultureInfo.InvariantCulture) + ":" +
                                   gemIndex.ToString(CultureInfo.InvariantCulture);
                        string firstPath;
                        if (!firstPathBySlot.TryGetValue(slot, out firstPath))
                        {
                            firstPathBySlot.Add(slot, path);
                        }
                        else
                        {
                            MarkMalformed(
                                state,
                                collector,
                                "SAVE_GEM_SLOT_DUPLICATE",
                                firstPath,
                                SaveSemanticDomain.RealmGems);
                            MarkMalformed(
                                state,
                                collector,
                                "SAVE_GEM_SLOT_DUPLICATE",
                                path,
                                SaveSemanticDomain.RealmGems);
                        }
                    }

                    bool isAtHome;
                    var atHomeValid = TryReadRequiredBoolean(
                        row,
                        "IsAtHome",
                        path,
                        SaveSemanticDomain.RealmGems,
                        collector,
                        state,
                        out isAtHome);
                    bool isDropped;
                    var droppedValid = TryReadRequiredBoolean(
                        row,
                        "IsDropped",
                        path,
                        SaveSemanticDomain.RealmGems,
                        collector,
                        state,
                        out isDropped);
                    string carrierId;
                    var carrierValid = TryReadRequiredString(
                        row,
                        "CarrierId",
                        path,
                        SaveSemanticDomain.RealmGems,
                        allowBlank: true,
                        collector,
                        state,
                        out carrierId);
                    long droppedTimestamp;
                    var timestampValid = TryReadRequiredInt64(
                        row,
                        "LastDroppedTimestamp",
                        path,
                        SaveSemanticDomain.RealmGems,
                        collector,
                        state,
                        out droppedTimestamp);
                    if (timestampValid && droppedTimestamp < 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_GEM_TIMESTAMP_NEGATIVE",
                            path + ".LastDroppedTimestamp",
                            SaveSemanticDomain.RealmGems);
                    }

                    if (atHomeValid && droppedValid && carrierValid && timestampValid)
                    {
                        var carrierBlank = string.IsNullOrWhiteSpace(carrierId);
                        var home = isAtHome && !isDropped && carrierBlank;
                        var dropped = !isAtHome && isDropped && carrierBlank && droppedTimestamp > 0;
                        var carried = !isAtHome && !isDropped && !carrierBlank;
                        if (!home && !dropped && !carried)
                        {
                            MarkMalformed(
                                state,
                                collector,
                                "SAVE_GEM_CUSTODY_CONTRADICTORY",
                                path,
                                SaveSemanticDomain.RealmGems);
                        }
                    }
                });
        }

        private static void ValidateWishgate(
            StrictJsonObject root,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue value;
            var wishgate = root.TryGet("Wishgate", out value)
                ? value as StrictJsonObject
                : null;
            if (wishgate == null)
            {
                return;
            }

            const string path = "$.Wishgate";
            InspectUnexpectedProperties(
                wishgate,
                WishgateFields,
                path,
                SaveSemanticDomain.Envelope,
                collector,
                state);

            bool ignoredEarned;
            TryReadRequiredBoolean(
                wishgate,
                "IsEarned",
                path,
                SaveSemanticDomain.Envelope,
                collector,
                state,
                out ignoredEarned);
            string ignoredReason;
            TryReadRequiredOpaqueString(
                wishgate,
                "EarnReason",
                path,
                SaveSemanticDomain.Envelope,
                allowBlank: true,
                collector,
                state,
                out ignoredReason);
            string rewardId;
            var rewardValid = TryReadRequiredString(
                wishgate,
                "LastRewardId",
                path,
                SaveSemanticDomain.Envelope,
                allowBlank: true,
                collector,
                state,
                out rewardId);
            if (rewardValid && !string.IsNullOrWhiteSpace(rewardId))
            {
                MarkUnsupportedStableId(
                    authority,
                    SaveSemanticStableIdKind.WishgateReward,
                    rewardId,
                    path + ".LastRewardId",
                    SaveSemanticDomain.Envelope,
                    collector,
                    state);
            }
            long timestamp;
            var timestampValid = TryReadRequiredInt64(
                wishgate,
                "LastRewardChosenTimestamp",
                path,
                SaveSemanticDomain.Envelope,
                collector,
                state,
                out timestamp);
            if (timestampValid && timestamp < 0)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_WISHGATE_TIMESTAMP_NEGATIVE",
                    path + ".LastRewardChosenTimestamp",
                    SaveSemanticDomain.Envelope);
            }
            else if (rewardValid && timestampValid &&
                     (string.IsNullOrWhiteSpace(rewardId) != (timestamp == 0)))
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_WISHGATE_REWARD_CONTRADICTORY",
                    path,
                    SaveSemanticDomain.Envelope);
            }
        }

        private static void ValidateWarmaster(
            StrictJsonObject root,
            bool isLegacySchema,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue value;
            var warmaster = root.TryGet("Warmaster", out value)
                ? value as StrictJsonObject
                : null;
            if (warmaster == null)
            {
                return;
            }

            const string path = "$.Warmaster";
            InspectUnexpectedProperties(
                warmaster,
                WarmasterFields,
                path,
                SaveSemanticDomain.Warmaster,
                collector,
                state);

            string equippedSetId;
            var equippedValid = TryReadRequiredString(
                warmaster,
                "EquippedSetId",
                path,
                SaveSemanticDomain.Warmaster,
                allowBlank: true,
                collector,
                state,
                out equippedSetId);

            var unlocked = ValidateStringList(
                warmaster,
                "UnlockedSetIds",
                path,
                SaveSemanticDomain.Warmaster,
                isLegacySchema,
                collector,
                state);
            var purchased = ValidateStringList(
                warmaster,
                "PurchasedPieceIds",
                path,
                SaveSemanticDomain.Warmaster,
                isLegacySchema,
                collector,
                state);

            if (equippedValid && !string.IsNullOrWhiteSpace(equippedSetId))
            {
                MarkUnsupportedStableId(
                    authority,
                    SaveSemanticStableIdKind.WarmasterSet,
                    equippedSetId,
                    path + ".EquippedSetId",
                    SaveSemanticDomain.Warmaster,
                    collector,
                    state);
            }

            MarkUnsupportedStableIds(
                authority,
                SaveSemanticStableIdKind.WarmasterSet,
                unlocked,
                path + ".UnlockedSetIds",
                SaveSemanticDomain.Warmaster,
                collector,
                state);
            MarkUnsupportedStableIds(
                authority,
                SaveSemanticStableIdKind.WarmasterPiece,
                purchased,
                path + ".PurchasedPieceIds",
                SaveSemanticDomain.Warmaster,
                collector,
                state);

            bool ignoredTrueWarmaster;
            TryReadRequiredBoolean(
                warmaster,
                "IsTrueWarmaster",
                path,
                SaveSemanticDomain.Warmaster,
                collector,
                state,
                out ignoredTrueWarmaster);
            int level;
            if (TryReadRequiredInt32(
                    warmaster,
                    "Level",
                    path,
                    SaveSemanticDomain.Warmaster,
                    collector,
                    state,
                    out level) &&
                level < 0)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_WARMASTER_LEVEL_NEGATIVE",
                    path + ".Level",
                    SaveSemanticDomain.Warmaster);
            }

            int experience;
            if (TryReadRequiredInt32(
                    warmaster,
                    "Experience",
                    path,
                    SaveSemanticDomain.Warmaster,
                    collector,
                    state,
                    out experience) &&
                experience < 0)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_WARMASTER_EXPERIENCE_NEGATIVE",
                    path + ".Experience",
                    SaveSemanticDomain.Warmaster);
            }

            if (equippedValid && !string.IsNullOrWhiteSpace(equippedSetId) &&
                unlocked != null && !unlocked.Contains(equippedSetId))
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_WARMASTER_EQUIPPED_SET_LOCKED",
                    path + ".EquippedSetId",
                    SaveSemanticDomain.Warmaster);
            }
        }

        private static void ValidateChampionCustomization(
            StrictJsonObject root,
            bool isLegacySchema,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue value;
            var customization = root.TryGet("ChampionCustomization", out value)
                ? value as StrictJsonObject
                : null;
            if (customization == null)
            {
                return;
            }

            const string path = "$.ChampionCustomization";
            InspectUnexpectedProperties(
                customization,
                CurrentCustomizationFields,
                path,
                SaveSemanticDomain.Customization,
                collector,
                state);

            var originalStringFields = new[] { "BodyPresetId", "HairStyleId", "ArmorStyleId" };
            for (var index = 0; index < originalStringFields.Length; index++)
            {
                string stableId;
                if (TryReadRequiredString(
                    customization,
                    originalStringFields[index],
                    path,
                    SaveSemanticDomain.Customization,
                    allowBlank: false,
                    collector,
                    state,
                    out stableId))
                {
                    MarkUnsupportedStableId(
                        authority,
                        GetCustomizationStableIdKind(originalStringFields[index]),
                        stableId,
                        path + "." + originalStringFields[index],
                        SaveSemanticDomain.Customization,
                        collector,
                        state);
                }
            }

            var laterStringFields = new[] { "FaceMarkId", "WeaponStyleId", "OffhandStyleId" };
            for (var index = 0; index < laterStringFields.Length; index++)
            {
                ValidateVersionedCustomizationString(
                    customization,
                    laterStringFields[index],
                    isLegacySchema,
                    authority,
                    collector,
                    state);
            }

            var originalColorFields = new[]
            {
                "PrimaryR", "PrimaryG", "PrimaryB", "HairR", "HairG", "HairB"
            };
            for (var index = 0; index < originalColorFields.Length; index++)
            {
                ValidateCustomizationColor(
                    customization,
                    originalColorFields[index],
                    allowLegacyOmission: false,
                    isLegacySchema,
                    collector,
                    state);
            }

            var laterColorFields = new[]
            {
                "SkinR", "SkinG", "SkinB",
                "EyeR", "EyeG", "EyeB",
                "AccentR", "AccentG", "AccentB"
            };
            for (var index = 0; index < laterColorFields.Length; index++)
            {
                ValidateCustomizationColor(
                    customization,
                    laterColorFields[index],
                    allowLegacyOmission: true,
                    isLegacySchema,
                    collector,
                    state);
            }

            bool ignoredBoolean;
            TryReadRequiredBoolean(
                customization,
                "CapeEnabled",
                path,
                SaveSemanticDomain.Customization,
                collector,
                state,
                out ignoredBoolean);
            TryReadRequiredBoolean(
                customization,
                "HelmetEnabled",
                path,
                SaveSemanticDomain.Customization,
                collector,
                state,
                out ignoredBoolean);
        }

        private static void ValidateEquipmentRows(
            StrictJsonObject root,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            var firstPathById = new Dictionary<string, string>(StringComparer.Ordinal);
            ValidateObjectRows(
                root,
                "OwnedEquipment",
                SaveSemanticDomain.Equipment,
                EquipmentRowFields,
                collector,
                state,
                (row, path) =>
                {
                    var equipmentId = ValidateUniqueStableId(
                        row,
                        "EquipmentId",
                        path,
                        SaveSemanticDomain.Equipment,
                        firstPathById,
                        collector,
                        state);
                    MarkUnsupportedStableId(
                        authority,
                        SaveSemanticStableIdKind.Equipment,
                        equipmentId,
                        path + ".EquipmentId",
                        SaveSemanticDomain.Equipment,
                        collector,
                        state);
                    string ignoredText;
                    TryReadRequiredOpaqueString(
                        row,
                        "DisplayName",
                        path,
                        SaveSemanticDomain.Equipment,
                        allowBlank: true,
                        collector,
                        state,
                        out ignoredText);
                    int slot;
                    if (TryReadRequiredInt32(
                            row,
                            "Slot",
                            path,
                            SaveSemanticDomain.Equipment,
                            collector,
                            state,
                            out slot) &&
                        !authority.IsSupportedEquipmentSlot(slot))
                    {
                        MarkPreservedUnknown(
                            state,
                            collector,
                            "SAVE_EQUIPMENT_SLOT_PRESERVED_UNKNOWN",
                            path + ".Slot",
                            SaveSemanticDomain.Equipment,
                            rawOnly: true);
                    }

                    var bonusFields = new[] { "AttackBonus", "DefenseBonus", "HealthBonus" };
                    for (var index = 0; index < bonusFields.Length; index++)
                    {
                        int ignoredBonus;
                        TryReadRequiredInt32(
                            row,
                            bonusFields[index],
                            path,
                            SaveSemanticDomain.Equipment,
                            collector,
                            state,
                            out ignoredBonus);
                    }

                    int quantity;
                    if (TryReadRequiredInt32(
                            row,
                            "Quantity",
                            path,
                            SaveSemanticDomain.Equipment,
                            collector,
                            state,
                            out quantity) &&
                        quantity <= 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_EQUIPMENT_QUANTITY_NOT_POSITIVE",
                            path + ".Quantity",
                            SaveSemanticDomain.Equipment);
                    }

                    string sourceBossId;
                    if (TryReadRequiredString(
                        row,
                        "SourceBossId",
                        path,
                        SaveSemanticDomain.Equipment,
                        allowBlank: true,
                        collector,
                        state,
                        out sourceBossId) &&
                        !string.IsNullOrWhiteSpace(sourceBossId))
                    {
                        MarkUnsupportedStableId(
                            authority,
                            SaveSemanticStableIdKind.Boss,
                            sourceBossId,
                            path + ".SourceBossId",
                            SaveSemanticDomain.Equipment,
                            collector,
                            state);
                    }
                    bool ignoredAnnouncement;
                    TryReadRequiredBoolean(
                        row,
                        "AnnounceWorldDrop",
                        path,
                        SaveSemanticDomain.Equipment,
                        collector,
                        state,
                        out ignoredAnnouncement);
                    long firstTimestamp;
                    var firstValid = TryReadRequiredInt64(
                        row,
                        "FirstAcquiredTimestamp",
                        path,
                        SaveSemanticDomain.Equipment,
                        collector,
                        state,
                        out firstTimestamp);
                    long lastTimestamp;
                    var lastValid = TryReadRequiredInt64(
                        row,
                        "LastAcquiredTimestamp",
                        path,
                        SaveSemanticDomain.Equipment,
                        collector,
                        state,
                        out lastTimestamp);
                    if (firstValid && lastValid &&
                        (firstTimestamp <= 0 ||
                         lastTimestamp <= 0 ||
                         firstTimestamp > lastTimestamp))
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_EQUIPMENT_TIMESTAMPS_INVALID",
                            path,
                            SaveSemanticDomain.Equipment);
                    }
                });
        }

        private static void ValidateAppliedBossLootRewardRows(
            StrictJsonObject root,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            var firstPathByEncounterId = new Dictionary<string, string>(StringComparer.Ordinal);
            var firstPathByResultId = new Dictionary<string, string>(StringComparer.Ordinal);
            ValidateObjectRows(
                root,
                "AppliedBossLootRewards",
                SaveSemanticDomain.Envelope,
                AppliedBossLootRewardRowFields,
                collector,
                state,
                (row, path) =>
                {
                    ValidateUniqueStableId(
                        row,
                        "EncounterId",
                        path,
                        SaveSemanticDomain.Envelope,
                        firstPathByEncounterId,
                        collector,
                        state);
                    ValidateUniqueStableId(
                        row,
                        "RewardResultId",
                        path,
                        SaveSemanticDomain.Envelope,
                        firstPathByResultId,
                        collector,
                        state);
                    string bossId;
                    if (TryReadRequiredString(
                        row,
                        "BossId",
                        path,
                        SaveSemanticDomain.Envelope,
                        allowBlank: false,
                        collector,
                        state,
                        out bossId))
                    {
                        MarkUnsupportedStableId(
                            authority,
                            SaveSemanticStableIdKind.Boss,
                            bossId,
                            path + ".BossId",
                            SaveSemanticDomain.Envelope,
                            collector,
                            state);
                    }

                    string ignoredDigest;
                    TryReadRequiredOpaqueString(
                        row,
                        "RewardDigest",
                        path,
                        SaveSemanticDomain.Envelope,
                        allowBlank: false,
                        collector,
                        state,
                        out ignoredDigest);
                    long committedTimestamp;
                    if (TryReadRequiredInt64(
                            row,
                            "CommittedTimestamp",
                            path,
                            SaveSemanticDomain.Envelope,
                            collector,
                            state,
                            out committedTimestamp) &&
                        committedTimestamp <= 0)
                    {
                        MarkMalformed(
                            state,
                            collector,
                            "SAVE_BOSS_LOOT_COMMIT_TIMESTAMP_INVALID",
                            path + ".CommittedTimestamp",
                            SaveSemanticDomain.Envelope);
                    }
                });
        }

        private static void ValidateNvs01Progress(
            StrictJsonObject root,
            SaveSemanticNvs01Rule rule,
            DiagnosticCollector collector,
            ValidationState state)
        {
            const string path = "$.Nvs01Progress";
            StrictJsonValue value;
            if (!root.TryGet("Nvs01Progress", out value))
            {
                MarkNormalized(
                    state,
                    collector,
                    "SAVE_NVS01_PROGRESS_DEFAULTED",
                    path,
                    SaveSemanticDomain.Narrative);
                return;
            }

            var progress = value as StrictJsonObject;
            if (progress == null)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_PROGRESS_INVALID",
                    path,
                    SaveSemanticDomain.Narrative);
                return;
            }

            InspectUnexpectedProperties(
                progress,
                Nvs01ProgressFields,
                path,
                SaveSemanticDomain.Narrative,
                collector,
                state);

            int version;
            if (!TryReadRequiredInt32(
                    progress,
                    "Version",
                    path,
                    SaveSemanticDomain.Narrative,
                    collector,
                    state,
                    out version))
            {
                return;
            }

            if (version < 0)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_VERSION_NEGATIVE",
                    path + ".Version",
                    SaveSemanticDomain.Narrative);
                return;
            }

            int currentVersion = rule == null ? 1 : rule.CurrentVersion;
            if (version > currentVersion)
            {
                MarkPreservedUnknown(
                    state,
                    collector,
                    "SAVE_NVS01_VERSION_FORWARD",
                    path + ".Version",
                    SaveSemanticDomain.Narrative,
                    rawOnly: true);
                return;
            }

            if (version > 0 && rule == null)
            {
                MarkPreservedUnknown(
                    state,
                    collector,
                    "SAVE_NVS01_AUTHORITY_UNAVAILABLE",
                    path,
                    SaveSemanticDomain.Narrative,
                    rawOnly: true);
                return;
            }

            string ignoredString;
            foreach (string field in new[]
                     {
                         "PacketVersion",
                         "PacketSha256",
                         "QuestId",
                         "StateId",
                         "CurrentDialogueNodeId",
                         "PendingSemanticActionId",
                         "CommittedRealmId",
                         "LastEncounterCorrelationId",
                         "LastEncounterEventId",
                         "LastEncounterSnapshotVersion",
                         "LastEncounterSnapshotReference",
                         "UnlockedChapterId"
                     })
            {
                TryReadRequiredString(
                    progress,
                    field,
                    path,
                    SaveSemanticDomain.Narrative,
                    allowBlank: true,
                    collector,
                    state,
                    out ignoredString);
            }

            if (version == currentVersion)
            {
                StrictJsonValue packetVersionValue;
                StrictJsonValue packetShaValue;
                StrictJsonValue questValue;
                var packetVersion = progress.TryGet(
                        "PacketVersion",
                        out packetVersionValue)
                    ? packetVersionValue as StrictJsonString
                    : null;
                var packetSha = progress.TryGet(
                        "PacketSha256",
                        out packetShaValue)
                    ? packetShaValue as StrictJsonString
                    : null;
                var questId = progress.TryGet("QuestId", out questValue)
                    ? questValue as StrictJsonString
                    : null;
                bool currentIdentity =
                    packetVersion != null &&
                    packetSha != null &&
                    questId != null &&
                    string.Equals(
                        packetVersion.Value,
                        rule.PacketVersion,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        packetSha.Value,
                        rule.PacketSha256,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        questId.Value,
                        rule.QuestId,
                        StringComparison.Ordinal);
                bool migratableIdentity =
                    !currentIdentity &&
                    rule.HasMigratablePacketIdentity &&
                    packetVersion != null &&
                    packetSha != null &&
                    questId != null &&
                    string.Equals(
                        packetVersion.Value,
                        rule.MigratablePacketVersion,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        packetSha.Value,
                        rule.MigratablePacketSha256,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        questId.Value,
                        rule.QuestId,
                        StringComparison.Ordinal);
                if (migratableIdentity)
                {
                    MarkDataChange(
                        state,
                        collector,
                        "SAVE_NVS01_PACKET_IDENTITY_MIGRATION_REQUIRED",
                        path,
                        SaveSemanticDomain.Narrative);
                }
                else if (!currentIdentity)
                {
                    MarkPreservedUnknown(
                        state,
                        collector,
                        "SAVE_NVS01_PACKET_IDENTITY_UNSUPPORTED",
                        path,
                        SaveSemanticDomain.Narrative,
                        rawOnly: true);
                }
            }

            long revision;
            if (TryReadRequiredInt64(
                    progress,
                    "Revision",
                    path,
                    SaveSemanticDomain.Narrative,
                    collector,
                    state,
                    out revision) &&
                revision < 0)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_REVISION_NEGATIVE",
                    path + ".Revision",
                    SaveSemanticDomain.Narrative);
            }

            int encounterStatus;
            if (TryReadRequiredInt32(
                    progress,
                    "EncounterStatus",
                    path,
                    SaveSemanticDomain.Narrative,
                    collector,
                    state,
                    out encounterStatus) &&
                (encounterStatus < 0 || encounterStatus > 3))
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_ENCOUNTER_STATUS_INVALID",
                    path + ".EncounterStatus",
                    SaveSemanticDomain.Narrative);
            }

            int encounterOutcome;
            if (TryReadRequiredInt32(
                    progress,
                    "LastEncounterOutcome",
                    path,
                    SaveSemanticDomain.Narrative,
                    collector,
                    state,
                    out encounterOutcome) &&
                (encounterOutcome < 0 || encounterOutcome > 3))
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_ENCOUNTER_OUTCOME_INVALID",
                    path + ".LastEncounterOutcome",
                    SaveSemanticDomain.Narrative);
            }

            bool ignoredBoolean;
            foreach (string field in new[]
                     {
                         "PendingChoice",
                         "HasCurrentEncounter",
                         "HasLastEncounterOutcome",
                         "HasLastOperation"
                     })
            {
                TryReadRequiredBoolean(
                    progress,
                    field,
                    path,
                    SaveSemanticDomain.Narrative,
                    collector,
                    state,
                    out ignoredBoolean);
            }

            ValidateNvs01ObjectiveRows(progress, collector, state);
            ValidateNvs01StringArray(
                progress,
                "ConsequenceIntentIds",
                16,
                collector,
                state);
            ValidateNvs01StringArray(
                progress,
                "AcquiredArtifactIds",
                16,
                collector,
                state);
            ValidateNvs01StringArray(
                progress,
                "AppliedEffectKeys",
                16,
                collector,
                state);
            ValidateNvs01Encounter(progress, collector, state);
            ValidateNvs01Operation(progress, collector, state);

            if (version == 0 && !IsNeutralNvs01Progress(progress))
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_NEUTRAL_STATE_INVALID",
                    path,
                    SaveSemanticDomain.Narrative);
            }
        }

        private static bool IsNeutralNvs01Progress(StrictJsonObject progress)
        {
            if (!HasNvs01IntegerValue(progress, "Revision", 0) ||
                !HasNvs01IntegerValue(progress, "EncounterStatus", 0) ||
                !HasNvs01IntegerValue(progress, "LastEncounterOutcome", 0))
            {
                return false;
            }

            foreach (string field in new[]
                     {
                         "PacketVersion",
                         "PacketSha256",
                         "QuestId",
                         "StateId",
                         "CurrentDialogueNodeId",
                         "PendingSemanticActionId",
                         "CommittedRealmId",
                         "LastEncounterCorrelationId",
                         "LastEncounterEventId",
                         "LastEncounterSnapshotVersion",
                         "LastEncounterSnapshotReference",
                         "UnlockedChapterId"
                     })
            {
                if (!HasNvs01StringValue(progress, field, string.Empty))
                {
                    return false;
                }
            }

            foreach (string field in new[]
                     {
                         "PendingChoice",
                         "HasCurrentEncounter",
                         "HasLastEncounterOutcome",
                         "HasLastOperation"
                     })
            {
                if (!HasNvs01BooleanValue(progress, field, false))
                {
                    return false;
                }
            }

            foreach (string field in new[]
                     {
                         "Objectives",
                         "ConsequenceIntentIds",
                         "AcquiredArtifactIds",
                         "AppliedEffectKeys"
                     })
            {
                StrictJsonValue value;
                var rows = progress.TryGet(field, out value)
                    ? value as StrictJsonArray
                    : null;
                if (rows == null || rows.Items.Count != 0)
                {
                    return false;
                }
            }

            StrictJsonValue encounterValue;
            var encounter = progress.TryGet("CurrentEncounter", out encounterValue)
                ? encounterValue as StrictJsonObject
                : null;
            if (encounter == null ||
                !HasNvs01IntegerValue(encounter, "ContractVersion", 0))
            {
                return false;
            }

            foreach (string field in Nvs01EncounterFields.Where(
                         name => !string.Equals(
                             name,
                             "ContractVersion",
                             StringComparison.Ordinal)))
            {
                if (!HasNvs01StringValue(encounter, field, string.Empty))
                {
                    return false;
                }
            }

            StrictJsonValue operationValue;
            var operation = progress.TryGet("LastOperation", out operationValue)
                ? operationValue as StrictJsonObject
                : null;
            if (operation == null ||
                !HasNvs01IntegerValue(operation, "Status", 0) ||
                !HasNvs01IntegerValue(operation, "Revision", 0))
            {
                return false;
            }

            foreach (string field in new[]
                     {
                         "OperationId",
                         "PayloadFingerprint",
                         "StateId",
                         "EventId",
                         "CorrelationId"
                     })
            {
                if (!HasNvs01StringValue(operation, field, string.Empty))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasNvs01StringValue(
            StrictJsonObject row,
            string field,
            string expected)
        {
            StrictJsonValue value;
            var text = row.TryGet(field, out value)
                ? value as StrictJsonString
                : null;
            return text != null &&
                   string.Equals(text.Value, expected, StringComparison.Ordinal);
        }

        private static bool HasNvs01IntegerValue(
            StrictJsonObject row,
            string field,
            long expected)
        {
            StrictJsonValue value;
            long actual;
            return row.TryGet(field, out value) &&
                   TryReadInt64(value, out actual) &&
                   actual == expected;
        }

        private static bool HasNvs01BooleanValue(
            StrictJsonObject row,
            string field,
            bool expected)
        {
            StrictJsonValue value;
            var boolean = row.TryGet(field, out value)
                ? value as StrictJsonBoolean
                : null;
            return boolean != null && boolean.Value == expected;
        }

        private static void ValidateNvs01ObjectiveRows(
            StrictJsonObject progress,
            DiagnosticCollector collector,
            ValidationState state)
        {
            const string path = "$.Nvs01Progress.Objectives";
            StrictJsonValue value;
            var rows = progress.TryGet("Objectives", out value)
                ? value as StrictJsonArray
                : null;
            if (rows == null)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_OBJECTIVES_INVALID",
                    path,
                    SaveSemanticDomain.Narrative);
                return;
            }

            if (rows.Items.Count > 16)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_OBJECTIVE_LIMIT",
                    path,
                    SaveSemanticDomain.Narrative);
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var count = Math.Min(rows.Items.Count, 16);
            for (var index = 0; index < count; index++)
            {
                string rowPath = path + "[" +
                                 index.ToString(CultureInfo.InvariantCulture) + "]";
                var row = rows.Items[index] as StrictJsonObject;
                if (row == null)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_NVS01_OBJECTIVE_INVALID",
                        rowPath,
                        SaveSemanticDomain.Narrative);
                    continue;
                }

                InspectUnexpectedProperties(
                    row,
                    Nvs01ObjectiveFields,
                    rowPath,
                    SaveSemanticDomain.Narrative,
                    collector,
                    state);
                string objectiveId;
                if (TryReadRequiredString(
                        row,
                        "ObjectiveId",
                        rowPath,
                        SaveSemanticDomain.Narrative,
                        allowBlank: false,
                        collector,
                        state,
                        out objectiveId) &&
                    !ids.Add(objectiveId))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_NVS01_OBJECTIVE_DUPLICATE",
                        rowPath + ".ObjectiveId",
                        SaveSemanticDomain.Narrative);
                }

                int status;
                if (TryReadRequiredInt32(
                        row,
                        "Status",
                        rowPath,
                        SaveSemanticDomain.Narrative,
                        collector,
                        state,
                        out status) &&
                    (status < 0 || status > 2))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_NVS01_OBJECTIVE_STATUS_INVALID",
                        rowPath + ".Status",
                        SaveSemanticDomain.Narrative);
                }
            }
        }

        private static void ValidateNvs01StringArray(
            StrictJsonObject progress,
            string field,
            int maximum,
            DiagnosticCollector collector,
            ValidationState state)
        {
            string path = "$.Nvs01Progress." + field;
            StrictJsonValue value;
            var rows = progress.TryGet(field, out value)
                ? value as StrictJsonArray
                : null;
            if (rows == null)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_COLLECTION_INVALID",
                    path,
                    SaveSemanticDomain.Narrative);
                return;
            }

            if (rows.Items.Count > maximum)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_COLLECTION_LIMIT",
                    path,
                    SaveSemanticDomain.Narrative);
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var count = Math.Min(rows.Items.Count, maximum);
            for (var index = 0; index < count; index++)
            {
                string itemPath = path + "[" +
                                  index.ToString(CultureInfo.InvariantCulture) + "]";
                var text = rows.Items[index] as StrictJsonString;
                if (text == null ||
                    string.IsNullOrWhiteSpace(text.Value) ||
                    text.Value.Length > MaximumStableIdCharacters ||
                    !ids.Add(text.Value))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_NVS01_COLLECTION_ITEM_INVALID",
                        itemPath,
                        SaveSemanticDomain.Narrative);
                }
            }
        }

        private static void ValidateNvs01Encounter(
            StrictJsonObject progress,
            DiagnosticCollector collector,
            ValidationState state)
        {
            const string path = "$.Nvs01Progress.CurrentEncounter";
            StrictJsonValue value;
            var encounter = progress.TryGet("CurrentEncounter", out value)
                ? value as StrictJsonObject
                : null;
            if (encounter == null)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_ENCOUNTER_INVALID",
                    path,
                    SaveSemanticDomain.Narrative);
                return;
            }

            InspectUnexpectedProperties(
                encounter,
                Nvs01EncounterFields,
                path,
                SaveSemanticDomain.Narrative,
                collector,
                state);
            int ignoredInteger;
            TryReadRequiredInt32(
                encounter,
                "ContractVersion",
                path,
                SaveSemanticDomain.Narrative,
                collector,
                state,
                out ignoredInteger);
            string ignoredString;
            foreach (string field in Nvs01EncounterFields.Where(
                         name => !string.Equals(
                             name,
                             "ContractVersion",
                             StringComparison.Ordinal)))
            {
                TryReadRequiredString(
                    encounter,
                    field,
                    path,
                    SaveSemanticDomain.Narrative,
                    allowBlank: true,
                    collector,
                    state,
                    out ignoredString);
            }
        }

        private static void ValidateNvs01Operation(
            StrictJsonObject progress,
            DiagnosticCollector collector,
            ValidationState state)
        {
            const string path = "$.Nvs01Progress.LastOperation";
            StrictJsonValue value;
            var operation = progress.TryGet("LastOperation", out value)
                ? value as StrictJsonObject
                : null;
            if (operation == null)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_OPERATION_INVALID",
                    path,
                    SaveSemanticDomain.Narrative);
                return;
            }

            InspectUnexpectedProperties(
                operation,
                Nvs01OperationFields,
                path,
                SaveSemanticDomain.Narrative,
                collector,
                state);
            string ignoredString;
            foreach (string field in new[]
                     {
                         "OperationId",
                         "PayloadFingerprint",
                         "StateId",
                         "EventId",
                         "CorrelationId"
                     })
            {
                TryReadRequiredString(
                    operation,
                    field,
                    path,
                    SaveSemanticDomain.Narrative,
                    allowBlank: true,
                    collector,
                    state,
                    out ignoredString);
            }

            StrictJsonValue expectedFingerprintValue;
            if (operation.TryGet(
                    "ExpectedGenerationFingerprint",
                    out expectedFingerprintValue))
            {
                var expectedFingerprint =
                    expectedFingerprintValue as StrictJsonString;
                if (expectedFingerprint == null ||
                    expectedFingerprint.Value.Length != 0)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_SCHEMA_V1_NVS01_EXPECTED_GENERATION_INVALID",
                        path + ".ExpectedGenerationFingerprint",
                        SaveSemanticDomain.Narrative);
                }
            }

            int status;
            if (TryReadRequiredInt32(
                    operation,
                    "Status",
                    path,
                    SaveSemanticDomain.Narrative,
                    collector,
                    state,
                    out status) &&
                (status < 0 || status > 4))
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_OPERATION_STATUS_INVALID",
                    path + ".Status",
                    SaveSemanticDomain.Narrative);
            }

            long revision;
            if (TryReadRequiredInt64(
                    operation,
                    "Revision",
                    path,
                    SaveSemanticDomain.Narrative,
                    collector,
                    state,
                    out revision) &&
                revision < 0)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NVS01_OPERATION_REVISION_NEGATIVE",
                    path + ".Revision",
                    SaveSemanticDomain.Narrative);
            }
        }

        private static void ValidateObjectRows(
            StrictJsonObject root,
            string collectionName,
            SaveSemanticDomain domain,
            HashSet<string> knownFields,
            DiagnosticCollector collector,
            ValidationState state,
            Action<StrictJsonObject, string> validateRow)
        {
            StrictJsonValue value;
            var rows = root.TryGet(collectionName, out value)
                ? value as StrictJsonArray
                : null;
            if (rows == null)
            {
                return;
            }

            var rowCount = rows.Items.Count;
            if (rowCount > MaximumDomainRows)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_DOMAIN_ROW_LIMIT",
                    "$." + collectionName,
                    domain);
                rowCount = MaximumDomainRows;
            }

            for (var index = 0; index < rowCount; index++)
            {
                var path = "$." + collectionName + "[" +
                           index.ToString(CultureInfo.InvariantCulture) + "]";
                var item = rows.Items[index];
                if (item is StrictJsonNull)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_DOMAIN_ROW_NULL",
                        path,
                        domain);
                    continue;
                }

                var row = item as StrictJsonObject;
                if (row == null)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_DOMAIN_ROW_NOT_OBJECT",
                        path,
                        domain);
                    continue;
                }

                InspectUnexpectedProperties(
                    row,
                    knownFields,
                    path,
                    domain,
                    collector,
                    state);
                validateRow(row, path);
            }
        }

        private static string ValidateUniqueStableId(
            StrictJsonObject row,
            string fieldName,
            string path,
            SaveSemanticDomain domain,
            Dictionary<string, string> firstPathById,
            DiagnosticCollector collector,
            ValidationState state)
        {
            string stableId;
            if (!TryReadRequiredString(
                    row,
                    fieldName,
                    path,
                    domain,
                    allowBlank: false,
                    collector,
                    state,
                    out stableId))
            {
                return null;
            }

            string firstPath;
            if (!firstPathById.TryGetValue(stableId, out firstPath))
            {
                firstPathById.Add(stableId, path);
                return stableId;
            }

            MarkMalformed(
                state,
                collector,
                "SAVE_STABLE_ID_DUPLICATE",
                firstPath + "." + fieldName,
                domain);
            MarkMalformed(
                state,
                collector,
                "SAVE_STABLE_ID_DUPLICATE",
                path + "." + fieldName,
                domain);
            return stableId;
        }

        private static void ValidateUniqueIntegerIdentity(
            int value,
            string fieldName,
            string path,
            SaveSemanticDomain domain,
            Dictionary<int, string> firstPathByValue,
            DiagnosticCollector collector,
            ValidationState state)
        {
            string firstPath;
            if (!firstPathByValue.TryGetValue(value, out firstPath))
            {
                firstPathByValue.Add(value, path);
                return;
            }

            MarkMalformed(
                state,
                collector,
                "SAVE_ENUM_ID_DUPLICATE",
                firstPath + "." + fieldName,
                domain);
            MarkMalformed(
                state,
                collector,
                "SAVE_ENUM_ID_DUPLICATE",
                path + "." + fieldName,
                domain);
        }

        private static bool TryReadRequiredString(
            StrictJsonObject row,
            string name,
            string path,
            SaveSemanticDomain domain,
            bool allowBlank,
            DiagnosticCollector collector,
            ValidationState state,
            out string result)
        {
            result = null;
            StrictJsonValue value;
            var text = row.TryGet(name, out value) ? value as StrictJsonString : null;
            if (text == null)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_REQUIRED_STRING_INVALID",
                    path + "." + name,
                    domain);
                return false;
            }

            result = text.Value;
            if ((!allowBlank && string.IsNullOrWhiteSpace(result)) ||
                result.Length > MaximumStableIdCharacters)
            {
                MarkMalformed(
                    state,
                    collector,
                    string.IsNullOrWhiteSpace(result)
                        ? "SAVE_REQUIRED_STRING_BLANK"
                        : "SAVE_REQUIRED_STRING_TOO_LONG",
                    path + "." + name,
                    domain);
                return false;
            }

            return true;
        }

        private static bool TryReadRequiredOpaqueString(
            StrictJsonObject row,
            string name,
            string path,
            SaveSemanticDomain domain,
            bool allowBlank,
            DiagnosticCollector collector,
            ValidationState state,
            out string result)
        {
            result = null;
            StrictJsonValue value;
            var text = row.TryGet(name, out value) ? value as StrictJsonString : null;
            if (text == null)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_REQUIRED_TEXT_INVALID",
                    path + "." + name,
                    domain);
                return false;
            }

            result = text.Value;
            if (!allowBlank && string.IsNullOrWhiteSpace(result))
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_REQUIRED_TEXT_BLANK",
                    path + "." + name,
                    domain);
                return false;
            }

            return true;
        }

        private static bool TryReadRequiredInt32(
            StrictJsonObject row,
            string name,
            string path,
            SaveSemanticDomain domain,
            DiagnosticCollector collector,
            ValidationState state,
            out int result)
        {
            StrictJsonValue value;
            if (row.TryGet(name, out value) && TryReadInt32(value, out result))
            {
                return true;
            }

            result = 0;
            MarkMalformed(
                state,
                collector,
                "SAVE_REQUIRED_INTEGER_INVALID",
                path + "." + name,
                domain);
            return false;
        }

        private static bool TryReadRequiredInt64(
            StrictJsonObject row,
            string name,
            string path,
            SaveSemanticDomain domain,
            DiagnosticCollector collector,
            ValidationState state,
            out long result)
        {
            StrictJsonValue value;
            if (row.TryGet(name, out value) && TryReadInt64(value, out result))
            {
                return true;
            }

            result = 0;
            MarkMalformed(
                state,
                collector,
                "SAVE_REQUIRED_INTEGER_INVALID",
                path + "." + name,
                domain);
            return false;
        }

        private static bool TryReadRequiredBoolean(
            StrictJsonObject row,
            string name,
            string path,
            SaveSemanticDomain domain,
            DiagnosticCollector collector,
            ValidationState state,
            out bool result)
        {
            StrictJsonValue value;
            var boolean = row.TryGet(name, out value) ? value as StrictJsonBoolean : null;
            if (boolean != null)
            {
                result = boolean.Value;
                return true;
            }

            result = false;
            MarkMalformed(
                state,
                collector,
                "SAVE_REQUIRED_BOOLEAN_INVALID",
                path + "." + name,
                domain);
            return false;
        }

        private static HashSet<string> ValidateStringList(
            StrictJsonObject row,
            string name,
            string path,
            SaveSemanticDomain domain,
            bool allowLegacyOmission,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue value;
            if (!row.TryGet(name, out value) || value is StrictJsonNull)
            {
                if (allowLegacyOmission)
                {
                    MarkNormalized(
                        state,
                        collector,
                        "SAVE_COMPATIBLE_NESTED_COLLECTION_DEFAULTED",
                        path + "." + name,
                        domain);
                    return new HashSet<string>(StringComparer.Ordinal);
                }

                MarkMalformed(
                    state,
                    collector,
                    "SAVE_REQUIRED_ARRAY_INVALID",
                    path + "." + name,
                    domain);
                return null;
            }

            var array = value as StrictJsonArray;
            if (array == null)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_REQUIRED_ARRAY_INVALID",
                    path + "." + name,
                    domain);
                return null;
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            var count = Math.Min(array.Items.Count, MaximumDomainRows);
            if (array.Items.Count > MaximumDomainRows)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_NESTED_ROW_LIMIT",
                    path + "." + name,
                    domain);
            }

            for (var index = 0; index < count; index++)
            {
                var itemPath = path + "." + name + "[" +
                               index.ToString(CultureInfo.InvariantCulture) + "]";
                var text = array.Items[index] as StrictJsonString;
                if (text == null ||
                    string.IsNullOrWhiteSpace(text.Value) ||
                    text.Value.Length > MaximumStableIdCharacters)
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_NESTED_ID_INVALID",
                        itemPath,
                        domain);
                    continue;
                }

                if (!result.Add(text.Value))
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_NESTED_ID_DUPLICATE",
                        itemPath,
                        domain);
                }
            }

            return result;
        }

        private static void ValidateVersionedCustomizationString(
            StrictJsonObject customization,
            string name,
            bool isLegacySchema,
            SaveSemanticValidationAuthority authority,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue value;
            if (!customization.TryGet(name, out value) || value is StrictJsonNull)
            {
                if (isLegacySchema)
                {
                    MarkNormalized(
                        state,
                        collector,
                        "SAVE_CUSTOMIZATION_FIELD_DEFAULTED",
                        "$.ChampionCustomization." + name,
                        SaveSemanticDomain.Customization);
                }
                else
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_CUSTOMIZATION_FIELD_MISSING",
                        "$.ChampionCustomization." + name,
                        SaveSemanticDomain.Customization);
                }

                return;
            }

            string stableId;
            if (TryReadRequiredString(
                customization,
                name,
                "$.ChampionCustomization",
                SaveSemanticDomain.Customization,
                allowBlank: false,
                collector,
                state,
                out stableId))
            {
                MarkUnsupportedStableId(
                    authority,
                    GetCustomizationStableIdKind(name),
                    stableId,
                    "$.ChampionCustomization." + name,
                    SaveSemanticDomain.Customization,
                    collector,
                    state);
            }
        }

        private static SaveSemanticStableIdKind GetCustomizationStableIdKind(string name)
        {
            switch (name)
            {
                case "BodyPresetId":
                    return SaveSemanticStableIdKind.BodyPreset;
                case "HairStyleId":
                    return SaveSemanticStableIdKind.HairStyle;
                case "ArmorStyleId":
                    return SaveSemanticStableIdKind.ArmorStyle;
                case "FaceMarkId":
                    return SaveSemanticStableIdKind.FaceMark;
                case "WeaponStyleId":
                    return SaveSemanticStableIdKind.WeaponStyle;
                case "OffhandStyleId":
                    return SaveSemanticStableIdKind.OffhandStyle;
                default:
                    throw new ArgumentOutOfRangeException(nameof(name));
            }
        }

        private static void ValidateCustomizationColor(
            StrictJsonObject customization,
            string name,
            bool allowLegacyOmission,
            bool isLegacySchema,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue value;
            if (!customization.TryGet(name, out value) || value is StrictJsonNull)
            {
                if (allowLegacyOmission && isLegacySchema)
                {
                    MarkNormalized(
                        state,
                        collector,
                        "SAVE_CUSTOMIZATION_FIELD_DEFAULTED",
                        "$.ChampionCustomization." + name,
                        SaveSemanticDomain.Customization);
                }
                else
                {
                    MarkMalformed(
                        state,
                        collector,
                        "SAVE_CUSTOMIZATION_COLOR_MISSING",
                        "$.ChampionCustomization." + name,
                        SaveSemanticDomain.Customization);
                }

                return;
            }

            float channel;
            if (!TryReadFiniteSingle(value, out channel) || channel < 0f || channel > 1f)
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_CUSTOMIZATION_COLOR_INVALID",
                    "$.ChampionCustomization." + name,
                    SaveSemanticDomain.Customization);
            }
        }

        private static void RequireArray(
            StrictJsonObject root,
            string name,
            SaveSemanticDomain domain,
            bool compatibleNeutralDefault,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue value;
            if (!root.TryGet(name, out value) || value is StrictJsonNull)
            {
                HandleMissingOrNull(name, domain, compatibleNeutralDefault, collector, state);
                return;
            }

            if (!(value is StrictJsonArray))
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_TOP_LEVEL_ARRAY_INVALID",
                    "$." + name,
                    domain);
            }
        }

        private static void RequireObject(
            StrictJsonObject root,
            string name,
            SaveSemanticDomain domain,
            bool compatibleNeutralDefault,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue value;
            if (!root.TryGet(name, out value) || value is StrictJsonNull)
            {
                HandleMissingOrNull(name, domain, compatibleNeutralDefault, collector, state);
                return;
            }

            if (!(value is StrictJsonObject))
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_TOP_LEVEL_OBJECT_INVALID",
                    "$." + name,
                    domain);
            }
        }

        private static void RequireInt32(
            StrictJsonObject root,
            string name,
            SaveSemanticDomain domain,
            bool compatibleNeutralDefault,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue value;
            int ignored;
            if (!root.TryGet(name, out value) || value is StrictJsonNull)
            {
                HandleMissingOrNull(name, domain, compatibleNeutralDefault, collector, state);
            }
            else if (!TryReadInt32(value, out ignored))
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_TOP_LEVEL_INTEGER_INVALID",
                    "$." + name,
                    domain);
            }
        }

        private static void RequireInt64(
            StrictJsonObject root,
            string name,
            SaveSemanticDomain domain,
            bool compatibleNeutralDefault,
            DiagnosticCollector collector,
            ValidationState state)
        {
            StrictJsonValue value;
            long ignored;
            if (!root.TryGet(name, out value) || value is StrictJsonNull)
            {
                HandleMissingOrNull(name, domain, compatibleNeutralDefault, collector, state);
            }
            else if (!TryReadInt64(value, out ignored))
            {
                MarkMalformed(
                    state,
                    collector,
                    "SAVE_TOP_LEVEL_INTEGER_INVALID",
                    "$." + name,
                    domain);
            }
        }

        private static void HandleMissingOrNull(
            string name,
            SaveSemanticDomain domain,
            bool compatibleNeutralDefault,
            DiagnosticCollector collector,
            ValidationState state)
        {
            if (compatibleNeutralDefault)
            {
                state.NeedsNormalization = true;
                state.NormalizedDomains |= domain;
                collector.Add(
                    "SAVE_COMPATIBLE_FIELD_DEFAULTED",
                    "$." + name,
                    domain,
                    SaveSemanticDiagnosticSeverity.Information);
                return;
            }

            MarkMalformed(
                state,
                collector,
                "SAVE_REQUIRED_FIELD_MISSING",
                "$." + name,
                domain);
        }

        private static void InspectUnexpectedProperties(
            StrictJsonObject row,
            HashSet<string> knownFields,
            string path,
            SaveSemanticDomain domain,
            DiagnosticCollector collector,
            ValidationState state)
        {
            for (var index = 0; index < row.Properties.Count; index++)
            {
                var property = row.Properties[index];
                if (knownFields.Contains(property.Name))
                {
                    continue;
                }

                state.HasPreservedUnknown = true;
                state.HasRawOnlyUnknown = true;
                state.PreservedUnknownDomains |= domain;
                collector.Add(
                    "SAVE_UNKNOWN_NESTED_FIELD",
                    AppendPropertyPath(path, property.Name),
                    domain,
                    SaveSemanticDiagnosticSeverity.Warning);
            }
        }

        private static bool HasArray(StrictJsonObject root, string name)
        {
            StrictJsonValue value;
            return root.TryGet(name, out value) && value is StrictJsonArray;
        }

        private static bool IsMissingNullOrArray(StrictJsonObject root, string name)
        {
            StrictJsonValue value;
            return !root.TryGet(name, out value) ||
                   value is StrictJsonNull ||
                   value is StrictJsonArray;
        }

        private static bool HasObject(StrictJsonObject root, string name)
        {
            StrictJsonValue value;
            return root.TryGet(name, out value) && value is StrictJsonObject;
        }

        private static bool HasString(StrictJsonObject root, string name)
        {
            StrictJsonValue value;
            return root.TryGet(name, out value) && value is StrictJsonString;
        }

        private static bool HasNumber(StrictJsonObject root, string name)
        {
            StrictJsonValue value;
            return root.TryGet(name, out value) && value is StrictJsonNumber;
        }

        private static bool HasBoolean(StrictJsonObject root, string name)
        {
            StrictJsonValue value;
            return root.TryGet(name, out value) && value is StrictJsonBoolean;
        }

        private static bool HasInt32(StrictJsonObject root, string name)
        {
            StrictJsonValue value;
            int ignored;
            return root.TryGet(name, out value) && TryReadInt32(value, out ignored);
        }

        private static bool HasInt64(StrictJsonObject root, string name)
        {
            StrictJsonValue value;
            long ignored;
            return root.TryGet(name, out value) && TryReadInt64(value, out ignored);
        }

        private static bool TryReadOptionalInt32(
            StrictJsonObject root,
            string name,
            out int value,
            out bool isExplicit)
        {
            StrictJsonValue jsonValue;
            if (!root.TryGet(name, out jsonValue))
            {
                value = 0;
                isExplicit = false;
                return true;
            }

            isExplicit = true;
            if (jsonValue is StrictJsonNull)
            {
                value = 0;
                return false;
            }

            return TryReadInt32(jsonValue, out value);
        }

        private static bool TryReadInt32(StrictJsonValue value, out int result)
        {
            result = 0;
            var number = value as StrictJsonNumber;
            return number != null &&
                   int.TryParse(
                       number.RawValue,
                       NumberStyles.AllowLeadingSign,
                       CultureInfo.InvariantCulture,
                       out result);
        }

        private static bool TryReadInt64(StrictJsonValue value, out long result)
        {
            result = 0;
            var number = value as StrictJsonNumber;
            return number != null &&
                   long.TryParse(
                       number.RawValue,
                       NumberStyles.AllowLeadingSign,
                       CultureInfo.InvariantCulture,
                       out result);
        }

        private static bool TryReadFiniteSingle(StrictJsonValue value, out float result)
        {
            result = 0f;
            var number = value as StrictJsonNumber;
            if (number == null ||
                !number.HasFiniteDoubleValue ||
                !float.TryParse(
                    number.RawValue,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result) ||
                float.IsNaN(result) ||
                float.IsInfinity(result))
            {
                result = 0f;
                return false;
            }

            // A nonzero JSON significand that underflows to zero would be changed by
            // JsonUtility. Check the token itself because double parsing may already
            // have underflowed before this narrower float conversion.
            if (number.HasNonZeroSignificand && result == 0f)
            {
                return false;
            }

            return true;
        }

        private static bool TryReadRequiredBoolean(
            StrictJsonObject row,
            string name,
            out bool result)
        {
            StrictJsonValue value;
            if (row.TryGet(name, out value))
            {
                var boolean = value as StrictJsonBoolean;
                if (boolean != null)
                {
                    result = boolean.Value;
                    return true;
                }
            }

            result = false;
            return false;
        }

        private static void MarkMalformed(
            ValidationState state,
            DiagnosticCollector collector,
            string code,
            string path,
            SaveSemanticDomain domain)
        {
            state.HasMalformedData = true;
            state.DisabledDomains |= domain;
            collector.Add(code, path, domain, SaveSemanticDiagnosticSeverity.Error);
        }

        private static void MarkNormalized(
            ValidationState state,
            DiagnosticCollector collector,
            string code,
            string path,
            SaveSemanticDomain domain)
        {
            state.NeedsNormalization = true;
            state.NormalizedDomains |= domain;
            collector.Add(code, path, domain, SaveSemanticDiagnosticSeverity.Information);
        }

        private static void MarkDataChange(
            ValidationState state,
            DiagnosticCollector collector,
            string code,
            string path,
            SaveSemanticDomain domain)
        {
            state.NeedsDataChange = true;
            collector.Add(
                code,
                path,
                domain,
                SaveSemanticDiagnosticSeverity.Information);
        }

        private static void MarkPreservedUnknown(
            ValidationState state,
            DiagnosticCollector collector,
            string code,
            string path,
            SaveSemanticDomain domain,
            bool rawOnly)
        {
            state.HasPreservedUnknown = true;
            state.HasRawOnlyUnknown |= rawOnly;
            state.PreservedUnknownDomains |= domain;
            collector.Add(code, path, domain, SaveSemanticDiagnosticSeverity.Warning);
        }

        private static void MarkUnsupportedStableId(
            SaveSemanticValidationAuthority authority,
            SaveSemanticStableIdKind kind,
            string stableId,
            string path,
            SaveSemanticDomain domain,
            DiagnosticCollector collector,
            ValidationState state)
        {
            if (string.IsNullOrWhiteSpace(stableId) ||
                authority.IsSupportedStableId(kind, stableId))
            {
                return;
            }

            MarkPreservedUnknown(
                state,
                collector,
                "SAVE_STABLE_ID_PRESERVED_UNKNOWN",
                path,
                domain,
                rawOnly: false);
        }

        private static void MarkUnsupportedStableIds(
            SaveSemanticValidationAuthority authority,
            SaveSemanticStableIdKind kind,
            IEnumerable<string> stableIds,
            string path,
            SaveSemanticDomain domain,
            DiagnosticCollector collector,
            ValidationState state)
        {
            if (stableIds == null)
            {
                return;
            }

            foreach (var stableId in stableIds)
            {
                MarkUnsupportedStableId(
                    authority,
                    kind,
                    stableId,
                    path,
                    domain,
                    collector,
                    state);
            }
        }

        private static SaveSemanticCandidate CreateInvalid(
            SaveCandidateSourceGeneration sourceGeneration,
            byte[] rawBytes,
            int originalByteCount,
            DiagnosticCollector collector)
        {
            return Create(
                sourceGeneration,
                SaveSemanticCandidateOutcome.Invalid,
                SaveSemanticDomain.All,
                SaveSemanticDomain.None,
                SaveSemanticDomain.None,
                0,
                0,
                false,
                false,
                false,
                rawBytes,
                originalByteCount,
                collector);
        }

        private static SaveSemanticCandidate Create(
            SaveCandidateSourceGeneration sourceGeneration,
            SaveSemanticCandidateOutcome outcome,
            SaveSemanticDomain disabledDomains,
            SaveSemanticDomain normalizedDomains,
            SaveSemanticDomain preservedUnknownDomains,
            int schemaVersion,
            int initializationVersion,
            bool hasExplicitSchemaVersion,
            bool hasExplicitInitializationVersion,
            bool writable,
            byte[] rawBytes,
            int originalByteCount,
            DiagnosticCollector collector)
        {
            return new SaveSemanticCandidate(
                sourceGeneration,
                outcome,
                disabledDomains,
                normalizedDomains,
                preservedUnknownDomains,
                schemaVersion,
                initializationVersion,
                hasExplicitSchemaVersion,
                hasExplicitInitializationVersion,
                writable,
                rawBytes,
                originalByteCount,
                collector.Freeze());
        }

        private static string AppendPropertyPath(string path, string name)
        {
            if (!string.IsNullOrEmpty(name) &&
                name.Length <= 64 &&
                IsIdentifierStart(name[0]))
            {
                var isIdentifier = true;
                for (var index = 1; index < name.Length; index++)
                {
                    if (!IsIdentifierPart(name[index]))
                    {
                        isIdentifier = false;
                        break;
                    }
                }

                if (isIdentifier)
                {
                    return path + "." + name;
                }
            }

            return path + ".<property>";
        }

        private static HashSet<string> Fields(params string[] names) =>
            new HashSet<string>(names, StringComparer.Ordinal);

        private static bool IsIdentifierStart(char value)
        {
            return (value >= 'A' && value <= 'Z') ||
                   (value >= 'a' && value <= 'z') ||
                   value == '_';
        }

        private static bool IsIdentifierPart(char value)
        {
            return IsIdentifierStart(value) || (value >= '0' && value <= '9');
        }

        private sealed class ValidationState
        {
            internal bool HasMalformedData;
            internal bool NeedsNormalization;
            internal bool NeedsDataChange;
            internal bool HasPreservedUnknown;
            internal bool HasRawOnlyUnknown;
            internal SaveSemanticDomain DisabledDomains;
            internal SaveSemanticDomain NormalizedDomains;
            internal SaveSemanticDomain PreservedUnknownDomains;
        }

        private sealed class DiagnosticCollector
        {
            private readonly int maximumDiagnostics;
            private readonly List<SaveSemanticDiagnostic> diagnostics;
            private bool truncated;

            internal DiagnosticCollector(int maximumDiagnostics)
            {
                this.maximumDiagnostics = maximumDiagnostics;
                diagnostics = new List<SaveSemanticDiagnostic>(maximumDiagnostics);
            }

            internal void Add(
                string code,
                string path,
                SaveSemanticDomain domain,
                SaveSemanticDiagnosticSeverity severity)
            {
                if (truncated)
                {
                    return;
                }

                if (diagnostics.Count < maximumDiagnostics)
                {
                    diagnostics.Add(new SaveSemanticDiagnostic(code, path, domain, severity));
                    return;
                }

                truncated = true;
                diagnostics[maximumDiagnostics - 1] = new SaveSemanticDiagnostic(
                    "SAVE_DIAGNOSTICS_TRUNCATED",
                    "$",
                    SaveSemanticDomain.Envelope,
                    SaveSemanticDiagnosticSeverity.Warning);
            }

            internal IReadOnlyList<SaveSemanticDiagnostic> Freeze()
            {
                return new ReadOnlyCollection<SaveSemanticDiagnostic>(diagnostics.ToArray());
            }
        }
    }
}
