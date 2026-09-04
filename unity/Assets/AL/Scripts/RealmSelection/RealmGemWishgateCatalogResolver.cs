using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Data.Catalogs;

namespace AL.RealmSelection
{
    public enum RealmGemWishgateCatalogLoadStatus
    {
        Ready,
        SourceUnavailable,
        InvalidJson,
        DuplicateMember,
        UnsupportedVersion,
        FutureVersion,
        IdentityMismatch,
        InvalidSource,
        SourceHashMismatch,
        RealmAuthorityUnavailable,
        DuplicateId,
        UnknownRealmGem,
        RealmAuthorityMismatch
    }

    public enum RealmGemWishgateQueryStatus
    {
        Found,
        InvalidId,
        UnknownId
    }

    public sealed class RealmGemWishgateCatalogEntry
    {
        internal RealmGemWishgateCatalogEntry(
            string id,
            string realmId,
            string displayNameKey,
            string summaryKey,
            string custodyMeaningKey,
            string signatureKey)
        {
            Id = id;
            RealmId = realmId;
            DisplayNameKey = displayNameKey;
            SummaryKey = summaryKey;
            CustodyMeaningKey = custodyMeaningKey;
            SignatureKey = signatureKey;
        }

        public string Id { get; }
        public string RealmId { get; }
        public string DisplayNameKey { get; }
        public string SummaryKey { get; }
        public string CustodyMeaningKey { get; }
        public string SignatureKey { get; }
    }

    public sealed class WishEmphasisCatalogEntry
    {
        internal WishEmphasisCatalogEntry(
            string id,
            string displayNameKey,
            string summaryKey,
            string effectBoundary)
        {
            Id = id;
            DisplayNameKey = displayNameKey;
            SummaryKey = summaryKey;
            EffectBoundary = effectBoundary;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string SummaryKey { get; }
        public string EffectBoundary { get; }
    }

    public sealed class WishgateCatalogEntry
    {
        internal WishgateCatalogEntry(
            string id,
            string displayNameKey,
            string summaryKey,
            string entryZoneId,
            string guardianDragonNameKey)
        {
            Id = id;
            DisplayNameKey = displayNameKey;
            SummaryKey = summaryKey;
            EntryZoneId = entryZoneId;
            GuardianDragonNameKey = guardianDragonNameKey;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string SummaryKey { get; }
        public string EntryZoneId { get; }
        public string GuardianDragonNameKey { get; }
    }

    public sealed class RealmGemWishgateCatalogQueryResult<TEntry>
        where TEntry : class
    {
        internal RealmGemWishgateCatalogQueryResult(
            RealmGemWishgateQueryStatus status,
            TEntry entry,
            string technicalCode)
        {
            Status = status;
            Entry = entry;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public RealmGemWishgateQueryStatus Status { get; }
        public TEntry Entry { get; }
        public string TechnicalCode { get; }
        public bool IsFound => Status == RealmGemWishgateQueryStatus.Found && Entry != null;
    }

    public sealed class RealmGemWishgateCatalogSnapshot
    {
        private readonly IReadOnlyDictionary<string, RealmGemWishgateCatalogEntry> realmGemsById;
        private readonly IReadOnlyDictionary<string, WishEmphasisCatalogEntry> wishEmphasesById;

        internal RealmGemWishgateCatalogSnapshot(
            string sourceVersion,
            string sourcePacketId,
            string sourceSha256,
            IList<RealmGemWishgateCatalogEntry> realmGems,
            WishgateCatalogEntry wishgate,
            IList<WishEmphasisCatalogEntry> wishEmphases)
        {
            SourceVersion = sourceVersion;
            SourcePacketId = sourcePacketId;
            SourceSha256 = sourceSha256;
            RealmGems = Array.AsReadOnly(realmGems.ToArray());
            Wishgate = wishgate;
            WishEmphases = Array.AsReadOnly(wishEmphases.ToArray());
            realmGemsById = new ReadOnlyDictionary<string, RealmGemWishgateCatalogEntry>(
                realmGems.ToDictionary(entry => entry.Id, StringComparer.Ordinal));
            wishEmphasesById = new ReadOnlyDictionary<string, WishEmphasisCatalogEntry>(
                wishEmphases.ToDictionary(entry => entry.Id, StringComparer.Ordinal));
        }

        public string SourceVersion { get; }
        public string SourcePacketId { get; }
        public string SourceSha256 { get; }
        public IReadOnlyList<RealmGemWishgateCatalogEntry> RealmGems { get; }
        public WishgateCatalogEntry Wishgate { get; }
        public IReadOnlyList<WishEmphasisCatalogEntry> WishEmphases { get; }

        public RealmGemWishgateCatalogQueryResult<RealmGemWishgateCatalogEntry> ResolveRealmGem(
            string id)
        {
            return Resolve(id, realmGemsById);
        }

        public RealmGemWishgateCatalogQueryResult<WishEmphasisCatalogEntry> ResolveWishEmphasis(
            string id)
        {
            return Resolve(id, wishEmphasesById);
        }

        private static RealmGemWishgateCatalogQueryResult<TEntry> Resolve<TEntry>(
            string id,
            IReadOnlyDictionary<string, TEntry> entries)
            where TEntry : class
        {
            if (!RealmGemCatalogResolver.IsStableId(id))
            {
                return new RealmGemWishgateCatalogQueryResult<TEntry>(
                    RealmGemWishgateQueryStatus.InvalidId,
                    null,
                    RealmGemWishgateCatalogResolver.InvalidQueryIdCode);
            }

            if (!entries.TryGetValue(id, out TEntry entry))
            {
                return new RealmGemWishgateCatalogQueryResult<TEntry>(
                    RealmGemWishgateQueryStatus.UnknownId,
                    null,
                    RealmGemWishgateCatalogResolver.UnknownQueryIdCode);
            }

            return new RealmGemWishgateCatalogQueryResult<TEntry>(
                RealmGemWishgateQueryStatus.Found,
                entry,
                RealmGemWishgateCatalogResolver.FoundQueryIdCode);
        }
    }

    public sealed class RealmGemWishgateCatalogLoadResult
    {
        internal RealmGemWishgateCatalogLoadResult(
            RealmGemWishgateCatalogLoadStatus status,
            RealmGemWishgateCatalogSnapshot snapshot,
            string technicalCode)
        {
            Status = status;
            Snapshot = snapshot;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public RealmGemWishgateCatalogLoadStatus Status { get; }
        public RealmGemWishgateCatalogSnapshot Snapshot { get; }
        public string TechnicalCode { get; }
        public bool IsReady => Status == RealmGemWishgateCatalogLoadStatus.Ready && Snapshot != null;
    }

    public static class RealmGemWishgateCatalogResolver
    {
        public const string ExpectedCatalogId = "al_realm_gem_wishgate_content_catalog";
        public const string ExpectedSourcePacketId = "al_narrative_realm_gem_wishgate_source_v001";
        public const string SupportedVersion = "0.1.0";
        public const int ExpectedSourceByteLength = 16513;
        public const int MaximumSourceBytes = 32768;
        public const string ExpectedSourceSha256 =
            "942699cb3c39ebea243c381bd5cadf78ab85aef177902ed09aad2b60897a086b";

        public const string ReadyCode = "AL-REALM-GEM-WISHGATE-CATALOG-READY";
        public const string InvalidQueryIdCode = "AL-REALM-GEM-WISHGATE-ID-INVALID";
        public const string UnknownQueryIdCode = "AL-REALM-GEM-WISHGATE-ID-UNKNOWN";
        public const string FoundQueryIdCode = "AL-REALM-GEM-WISHGATE-ID-FOUND";

        private const string ExpectedEffectBoundary =
            "epilogue_and_cosmetic_emphasis_only_until_future_reward_contract";

        public static RealmGemWishgateCatalogLoadResult Load(
            byte[] sourceBytes,
            RealmGemCatalogSnapshot realmGemAuthority)
        {
            if (sourceBytes == null)
            {
                return Reject(RealmGemWishgateCatalogLoadStatus.SourceUnavailable);
            }

            StrictJsonObject root;
            try
            {
                root = StrictJsonDocument.Parse(sourceBytes, MaximumSourceBytes) as StrictJsonObject;
            }
            catch (StrictJsonException exception)
            {
                return Reject(
                    string.Equals(exception.Code, "PROPERTY_DUPLICATE", StringComparison.Ordinal)
                        ? RealmGemWishgateCatalogLoadStatus.DuplicateMember
                        : RealmGemWishgateCatalogLoadStatus.InvalidJson);
            }

            if (root == null)
            {
                return Reject(RealmGemWishgateCatalogLoadStatus.InvalidJson);
            }

            string version;
            if (!TryReadRequiredString(root, "version", out version))
            {
                return Reject(RealmGemWishgateCatalogLoadStatus.InvalidSource);
            }

            if (!string.Equals(version, SupportedVersion, StringComparison.Ordinal))
            {
                Version parsed;
                Version supported = new Version(SupportedVersion);
                return Reject(
                    Version.TryParse(version, out parsed) && parsed > supported
                        ? RealmGemWishgateCatalogLoadStatus.FutureVersion
                        : RealmGemWishgateCatalogLoadStatus.UnsupportedVersion);
            }

            string catalogId;
            string game;
            string sourcePacketId;
            string idFormat;
            if (!TryReadRequiredString(root, "catalogId", out catalogId) ||
                !TryReadRequiredString(root, "game", out game) ||
                !TryReadRequiredString(root, "sourcePacketId", out sourcePacketId) ||
                !TryReadRequiredString(root, "idFormat", out idFormat) ||
                !string.Equals(catalogId, ExpectedCatalogId, StringComparison.Ordinal) ||
                !string.Equals(game, "Another Life", StringComparison.Ordinal) ||
                !string.Equals(sourcePacketId, ExpectedSourcePacketId, StringComparison.Ordinal) ||
                !string.Equals(idFormat, "lowercase_snake_case", StringComparison.Ordinal))
            {
                return Reject(RealmGemWishgateCatalogLoadStatus.IdentityMismatch);
            }

            if (!HasExactProperties(
                    root,
                    "version",
                    "catalogId",
                    "game",
                    "sourcePacketId",
                    "idFormat",
                    "sourceAuthorities",
                    "contentPolicy",
                    "realmGems",
                    "custodyStates",
                    "wishgate",
                    "statusCopy",
                    "draftLocalization",
                    "engineeringHandoff"))
            {
                return Reject(RealmGemWishgateCatalogLoadStatus.InvalidSource);
            }

            StrictJsonObject sourceAuthorities;
            if (!TryReadRequiredObject(root, "sourceAuthorities", out sourceAuthorities) ||
                !ValidateSourceAuthorities(sourceAuthorities))
            {
                return Reject(RealmGemWishgateCatalogLoadStatus.InvalidSource);
            }

            if (realmGemAuthority == null)
            {
                return Reject(RealmGemWishgateCatalogLoadStatus.RealmAuthorityUnavailable);
            }

            StrictJsonArray localizationArray;
            Dictionary<string, string> localization;
            if (!TryReadRequiredArray(root, "draftLocalization", out localizationArray) ||
                !TryReadLocalization(localizationArray, out localization))
            {
                return Reject(RealmGemWishgateCatalogLoadStatus.InvalidSource);
            }

            var referencedLocalization = new HashSet<string>(StringComparer.Ordinal);
            StrictJsonArray gemArray;
            List<RealmGemWishgateCatalogEntry> realmGems;
            RealmGemWishgateCatalogLoadStatus failure =
                RealmGemWishgateCatalogLoadStatus.InvalidSource;
            if (!TryReadRequiredArray(root, "realmGems", out gemArray) ||
                !TryReadRealmGems(
                    gemArray,
                    realmGemAuthority,
                    localization,
                    referencedLocalization,
                    out realmGems,
                    out failure))
            {
                return Reject(failure);
            }

            StrictJsonArray custodyStates;
            if (!TryReadRequiredArray(root, "custodyStates", out custodyStates) ||
                !ValidateCustodyStates(
                    custodyStates,
                    localization,
                    referencedLocalization,
                    out failure))
            {
                return Reject(failure);
            }

            StrictJsonObject wishgateObject;
            WishgateCatalogEntry wishgate;
            List<WishEmphasisCatalogEntry> wishEmphases;
            if (!TryReadRequiredObject(root, "wishgate", out wishgateObject) ||
                !TryReadWishgate(
                    wishgateObject,
                    localization,
                    referencedLocalization,
                    out wishgate,
                    out wishEmphases,
                    out failure))
            {
                return Reject(failure);
            }

            StrictJsonArray statusCopy;
            if (!TryReadRequiredArray(root, "statusCopy", out statusCopy) ||
                !ValidateStatusCopy(
                    statusCopy,
                    localization,
                    referencedLocalization,
                    out failure))
            {
                return Reject(failure);
            }

            if (!referencedLocalization.SetEquals(localization.Keys))
            {
                return Reject(RealmGemWishgateCatalogLoadStatus.InvalidSource);
            }

            string sha256 = ComputeSha256(sourceBytes);
            if (sourceBytes.Length != ExpectedSourceByteLength ||
                !string.Equals(sha256, ExpectedSourceSha256, StringComparison.Ordinal))
            {
                return Reject(RealmGemWishgateCatalogLoadStatus.SourceHashMismatch);
            }

            return new RealmGemWishgateCatalogLoadResult(
                RealmGemWishgateCatalogLoadStatus.Ready,
                new RealmGemWishgateCatalogSnapshot(
                    version,
                    sourcePacketId,
                    sha256,
                    realmGems,
                    wishgate,
                    wishEmphases),
                ReadyCode);
        }

        private static bool ValidateSourceAuthorities(StrictJsonObject value)
        {
            string primaryMode;
            string realmCatalog;
            string worldAtlasCatalog;
            string mainQuestPacket;
            string notificationCatalog;
            StrictJsonValue consumerIssueValue;
            var consumerIssue = value != null && value.TryGet("consumerIssue", out consumerIssueValue)
                ? consumerIssueValue as StrictJsonNumber
                : null;

            return HasExactProperties(
                       value,
                       "primaryMode",
                       "consumerIssue",
                       "realmCatalog",
                       "worldAtlasCatalog",
                       "mainQuestPacket",
                       "notificationCatalog") &&
                   TryReadRequiredString(value, "primaryMode", out primaryMode) &&
                   consumerIssue != null &&
                   string.Equals(consumerIssue.RawValue, "169", StringComparison.Ordinal) &&
                   TryReadRequiredString(value, "realmCatalog", out realmCatalog) &&
                   TryReadRequiredString(value, "worldAtlasCatalog", out worldAtlasCatalog) &&
                   TryReadRequiredString(value, "mainQuestPacket", out mainQuestPacket) &&
                   TryReadRequiredString(value, "notificationCatalog", out notificationCatalog) &&
                   string.Equals(primaryMode, "codex_narrative_content", StringComparison.Ordinal) &&
                   string.Equals(realmCatalog, "al_realm_catalog", StringComparison.Ordinal) &&
                   string.Equals(
                       worldAtlasCatalog,
                       "al_world_atlas_narrative_catalog",
                       StringComparison.Ordinal) &&
                   string.Equals(mainQuestPacket, "ANOTHERLIFE_MAIN_QUEST_LINE", StringComparison.Ordinal) &&
                   string.Equals(
                       notificationCatalog,
                       "al_notification_content_catalog",
                       StringComparison.Ordinal);
        }

        private static bool TryReadRealmGems(
            StrictJsonArray array,
            RealmGemCatalogSnapshot authority,
            IReadOnlyDictionary<string, string> localization,
            ISet<string> referencedLocalization,
            out List<RealmGemWishgateCatalogEntry> entries,
            out RealmGemWishgateCatalogLoadStatus failure)
        {
            entries = null;
            failure = RealmGemWishgateCatalogLoadStatus.InvalidSource;
            if (array == null || array.Items.Count != 8 ||
                authority.Entries == null || authority.Entries.Count != 8)
            {
                return false;
            }

            var built = new List<RealmGemWishgateCatalogEntry>(8);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < array.Items.Count; index++)
            {
                var row = array.Items[index] as StrictJsonObject;
                string id;
                string realmId;
                string displayNameKey;
                string summaryKey;
                string custodyMeaningKey;
                string signatureKey;
                string status;
                if (row == null ||
                    !HasExactProperties(
                        row,
                        "id",
                        "realmId",
                        "displayNameKey",
                        "summaryKey",
                        "custodyMeaningKey",
                        "signatureKey",
                        "status") ||
                    !TryReadRequiredString(row, "id", out id) ||
                    !TryReadRequiredString(row, "realmId", out realmId) ||
                    !TryReadRequiredString(row, "displayNameKey", out displayNameKey) ||
                    !TryReadRequiredString(row, "summaryKey", out summaryKey) ||
                    !TryReadRequiredString(row, "custodyMeaningKey", out custodyMeaningKey) ||
                    !TryReadRequiredString(row, "signatureKey", out signatureKey) ||
                    !TryReadRequiredString(row, "status", out status) ||
                    !RealmGemCatalogResolver.IsStableId(id) ||
                    !RealmGemCatalogResolver.IsStableId(realmId) ||
                    !string.Equals(
                        status,
                        "source_ready_runtime_custody_unimplemented",
                        StringComparison.Ordinal) ||
                    !ReferencesExist(
                        localization,
                        referencedLocalization,
                        displayNameKey,
                        summaryKey,
                        custodyMeaningKey,
                        signatureKey))
                {
                    return false;
                }

                if (!ids.Add(id))
                {
                    failure = RealmGemWishgateCatalogLoadStatus.DuplicateId;
                    return false;
                }

                RealmGemQueryResult resolved = authority.Resolve(id);
                if (resolved.Status == RealmGemQueryStatus.UnknownId)
                {
                    failure = RealmGemWishgateCatalogLoadStatus.UnknownRealmGem;
                    return false;
                }

                if (!resolved.IsFound ||
                    !string.Equals(resolved.Entry.HomeRealmId, realmId, StringComparison.Ordinal) ||
                    !string.Equals(authority.Entries[index].Id, id, StringComparison.Ordinal))
                {
                    failure = RealmGemWishgateCatalogLoadStatus.RealmAuthorityMismatch;
                    return false;
                }

                built.Add(new RealmGemWishgateCatalogEntry(
                    id,
                    realmId,
                    displayNameKey,
                    summaryKey,
                    custodyMeaningKey,
                    signatureKey));
            }

            entries = built;
            return true;
        }

        private static bool ValidateCustodyStates(
            StrictJsonArray array,
            IReadOnlyDictionary<string, string> localization,
            ISet<string> referencedLocalization,
            out RealmGemWishgateCatalogLoadStatus failure)
        {
            failure = RealmGemWishgateCatalogLoadStatus.InvalidSource;
            if (array == null || array.Items.Count != 4)
            {
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (StrictJsonValue value in array.Items)
            {
                var row = value as StrictJsonObject;
                string id;
                string displayNameKey;
                string summaryKey;
                string runtimeMeaning;
                if (row == null ||
                    !HasExactProperties(
                        row,
                        "id",
                        "displayNameKey",
                        "summaryKey",
                        "runtimeMeaning") ||
                    !TryReadRequiredString(row, "id", out id) ||
                    !TryReadRequiredString(row, "displayNameKey", out displayNameKey) ||
                    !TryReadRequiredString(row, "summaryKey", out summaryKey) ||
                    !TryReadRequiredString(row, "runtimeMeaning", out runtimeMeaning) ||
                    !RealmGemCatalogResolver.IsStableId(id) ||
                    !ReferencesExist(
                        localization,
                        referencedLocalization,
                        displayNameKey,
                        summaryKey))
                {
                    return false;
                }

                if (!ids.Add(id))
                {
                    failure = RealmGemWishgateCatalogLoadStatus.DuplicateId;
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadWishgate(
            StrictJsonObject row,
            IReadOnlyDictionary<string, string> localization,
            ISet<string> referencedLocalization,
            out WishgateCatalogEntry wishgate,
            out List<WishEmphasisCatalogEntry> wishEmphases,
            out RealmGemWishgateCatalogLoadStatus failure)
        {
            wishgate = null;
            wishEmphases = null;
            failure = RealmGemWishgateCatalogLoadStatus.InvalidSource;
            if (!HasExactProperties(
                    row,
                    "id",
                    "displayNameKey",
                    "summaryKey",
                    "entryZoneId",
                    "guardianDragonNameKey",
                    "eligibilitySource",
                    "defaultStatus",
                    "rewardPolicy",
                    "approvedWishEmphases",
                    "blockedRuntimeClaims"))
            {
                return false;
            }

            string id;
            string displayNameKey;
            string summaryKey;
            string entryZoneId;
            string guardianDragonNameKey;
            string eligibilitySource;
            string defaultStatus;
            string rewardPolicy;
            StrictJsonArray emphasisArray;
            StrictJsonArray blockedClaims;
            if (!TryReadRequiredString(row, "id", out id) ||
                !TryReadRequiredString(row, "displayNameKey", out displayNameKey) ||
                !TryReadRequiredString(row, "summaryKey", out summaryKey) ||
                !TryReadRequiredString(row, "entryZoneId", out entryZoneId) ||
                !TryReadRequiredString(row, "guardianDragonNameKey", out guardianDragonNameKey) ||
                !TryReadRequiredString(row, "eligibilitySource", out eligibilitySource) ||
                !TryReadRequiredString(row, "defaultStatus", out defaultStatus) ||
                !TryReadRequiredString(row, "rewardPolicy", out rewardPolicy) ||
                !TryReadRequiredArray(row, "approvedWishEmphases", out emphasisArray) ||
                !TryReadRequiredArray(row, "blockedRuntimeClaims", out blockedClaims) ||
                !string.Equals(id, "wishgate_eightfold_concordance", StringComparison.Ordinal) ||
                !string.Equals(entryZoneId, "zone_accordant_isle", StringComparison.Ordinal) ||
                !string.Equals(
                    eligibilitySource,
                    "all_eight_realm_gem_signatures_future_engineering_contract",
                    StringComparison.Ordinal) ||
                !string.Equals(defaultStatus, "requested_unavailable", StringComparison.Ordinal) ||
                !string.Equals(
                    rewardPolicy,
                    "future_engineering_entitlement_contract",
                    StringComparison.Ordinal) ||
                !ReferencesExist(
                    localization,
                    referencedLocalization,
                    displayNameKey,
                    summaryKey,
                    guardianDragonNameKey) ||
                !ValidateBlockedClaims(blockedClaims))
            {
                return false;
            }

            if (!TryReadWishEmphases(
                    emphasisArray,
                    localization,
                    referencedLocalization,
                    out wishEmphases,
                    out failure))
            {
                return false;
            }

            wishgate = new WishgateCatalogEntry(
                id,
                displayNameKey,
                summaryKey,
                entryZoneId,
                guardianDragonNameKey);
            return true;
        }

        private static bool TryReadWishEmphases(
            StrictJsonArray array,
            IReadOnlyDictionary<string, string> localization,
            ISet<string> referencedLocalization,
            out List<WishEmphasisCatalogEntry> entries,
            out RealmGemWishgateCatalogLoadStatus failure)
        {
            entries = null;
            failure = RealmGemWishgateCatalogLoadStatus.InvalidSource;
            if (array == null || array.Items.Count != 3)
            {
                return false;
            }

            var built = new List<WishEmphasisCatalogEntry>(3);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (StrictJsonValue value in array.Items)
            {
                var row = value as StrictJsonObject;
                string id;
                string displayNameKey;
                string summaryKey;
                string effectBoundary;
                if (row == null ||
                    !HasExactProperties(
                        row,
                        "id",
                        "displayNameKey",
                        "summaryKey",
                        "effectBoundary") ||
                    !TryReadRequiredString(row, "id", out id) ||
                    !TryReadRequiredString(row, "displayNameKey", out displayNameKey) ||
                    !TryReadRequiredString(row, "summaryKey", out summaryKey) ||
                    !TryReadRequiredString(row, "effectBoundary", out effectBoundary) ||
                    !id.StartsWith("wish_emphasis_", StringComparison.Ordinal) ||
                    !RealmGemCatalogResolver.IsStableId(id) ||
                    !string.Equals(effectBoundary, ExpectedEffectBoundary, StringComparison.Ordinal) ||
                    !ReferencesExist(
                        localization,
                        referencedLocalization,
                        displayNameKey,
                        summaryKey))
                {
                    return false;
                }

                if (!ids.Add(id))
                {
                    failure = RealmGemWishgateCatalogLoadStatus.DuplicateId;
                    return false;
                }

                built.Add(new WishEmphasisCatalogEntry(
                    id,
                    displayNameKey,
                    summaryKey,
                    effectBoundary));
            }

            entries = built;
            return true;
        }

        private static bool ValidateStatusCopy(
            StrictJsonArray array,
            IReadOnlyDictionary<string, string> localization,
            ISet<string> referencedLocalization,
            out RealmGemWishgateCatalogLoadStatus failure)
        {
            failure = RealmGemWishgateCatalogLoadStatus.InvalidSource;
            if (array == null || array.Items.Count != 3)
            {
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (StrictJsonValue value in array.Items)
            {
                var row = value as StrictJsonObject;
                string id;
                string displayNameKey;
                string bodyKey;
                if (row == null ||
                    !HasExactProperties(row, "id", "displayNameKey", "bodyKey") ||
                    !TryReadRequiredString(row, "id", out id) ||
                    !TryReadRequiredString(row, "displayNameKey", out displayNameKey) ||
                    !TryReadRequiredString(row, "bodyKey", out bodyKey) ||
                    !RealmGemCatalogResolver.IsStableId(id) ||
                    !ReferencesExist(
                        localization,
                        referencedLocalization,
                        displayNameKey,
                        bodyKey))
                {
                    return false;
                }

                if (!ids.Add(id))
                {
                    failure = RealmGemWishgateCatalogLoadStatus.DuplicateId;
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateBlockedClaims(StrictJsonArray array)
        {
            if (array == null || array.Items.Count != 5)
            {
                return false;
            }

            var claims = new HashSet<string>(StringComparer.Ordinal);
            foreach (StrictJsonValue value in array.Items)
            {
                var text = value as StrictJsonString;
                if (text == null || string.IsNullOrWhiteSpace(text.Value) || !claims.Add(text.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadLocalization(
            StrictJsonArray array,
            out Dictionary<string, string> entries)
        {
            entries = new Dictionary<string, string>(55, StringComparer.Ordinal);
            if (array == null || array.Items.Count != 55)
            {
                return false;
            }

            foreach (StrictJsonValue value in array.Items)
            {
                var row = value as StrictJsonObject;
                string key;
                string text;
                if (row == null ||
                    !HasExactProperties(row, "key", "text") ||
                    !TryReadRequiredString(row, "key", out key) ||
                    !TryReadRequiredString(row, "text", out text) ||
                    entries.ContainsKey(key))
                {
                    return false;
                }

                entries.Add(key, text);
            }

            return true;
        }

        private static bool ReferencesExist(
            IReadOnlyDictionary<string, string> localization,
            ISet<string> referencedLocalization,
            params string[] keys)
        {
            foreach (string key in keys)
            {
                if (string.IsNullOrEmpty(key) || !localization.ContainsKey(key))
                {
                    return false;
                }

                referencedLocalization.Add(key);
            }

            return true;
        }

        private static bool HasExactProperties(StrictJsonObject value, params string[] expected)
        {
            if (value == null || value.Properties.Count != expected.Length)
            {
                return false;
            }

            for (var index = 0; index < expected.Length; index++)
            {
                if (!string.Equals(value.Properties[index].Name, expected[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadRequiredString(
            StrictJsonObject parent,
            string name,
            out string result)
        {
            result = null;
            StrictJsonValue value;
            var text = parent != null && parent.TryGet(name, out value)
                ? value as StrictJsonString
                : null;
            if (text == null || string.IsNullOrEmpty(text.Value))
            {
                return false;
            }

            result = text.Value;
            return true;
        }

        private static bool TryReadRequiredArray(
            StrictJsonObject parent,
            string name,
            out StrictJsonArray result)
        {
            result = null;
            StrictJsonValue value;
            result = parent != null && parent.TryGet(name, out value)
                ? value as StrictJsonArray
                : null;
            return result != null;
        }

        private static bool TryReadRequiredObject(
            StrictJsonObject parent,
            string name,
            out StrictJsonObject result)
        {
            result = null;
            StrictJsonValue value;
            result = parent != null && parent.TryGet(name, out value)
                ? value as StrictJsonObject
                : null;
            return result != null;
        }

        private static string ComputeSha256(byte[] sourceBytes)
        {
            using (var sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(sourceBytes)
                    .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static RealmGemWishgateCatalogLoadResult Reject(
            RealmGemWishgateCatalogLoadStatus status)
        {
            return new RealmGemWishgateCatalogLoadResult(
                status,
                null,
                "AL-REALM-GEM-WISHGATE-CATALOG-" + FormatStatusCode(status));
        }

        private static string FormatStatusCode(RealmGemWishgateCatalogLoadStatus status)
        {
            string value = status.ToString();
            var result = new StringBuilder(value.Length + 8);
            for (var index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (index > 0 &&
                    char.IsUpper(character) &&
                    (char.IsLower(value[index - 1]) || char.IsDigit(value[index - 1])))
                {
                    result.Append('-');
                }

                result.Append(char.ToUpperInvariant(character));
            }

            return result.ToString();
        }
    }
}
