using System;
using System.Collections.Generic;
using AL.Core;

namespace AL.RealmSelection
{
    public enum RealmGemCatalogStatus
    {
        Ready,
        AuthorityUnavailable,
        InvalidAuthority
    }

    public enum RealmGemQueryStatus
    {
        Found,
        InvalidId,
        UnknownId
    }

    public sealed class RealmGemCatalogEntry
    {
        internal RealmGemCatalogEntry(
            string id,
            string homeRealmId,
            RealmId homeRealm,
            int saveSlotIndex)
        {
            Id = id;
            HomeRealmId = homeRealmId;
            HomeRealm = homeRealm;
            SaveSlotIndex = saveSlotIndex;
        }

        public string Id { get; }
        public string HomeRealmId { get; }
        public RealmId HomeRealm { get; }

        // Current custody mutations accept only positive GemIndex values. The
        // catalog assigns that one-based mapping instead of leaving it to callers.
        public int SaveSlotIndex { get; }
    }

    public sealed class RealmGemQueryResult
    {
        internal RealmGemQueryResult(
            RealmGemQueryStatus status,
            RealmGemCatalogEntry entry,
            string technicalCode)
        {
            Status = status;
            Entry = entry;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public RealmGemQueryStatus Status { get; }
        public RealmGemCatalogEntry Entry { get; }
        public string TechnicalCode { get; }
        public bool IsFound => Status == RealmGemQueryStatus.Found && Entry != null;
    }

    public sealed class RealmGemCatalogSnapshot
    {
        private readonly Dictionary<string, RealmGemCatalogEntry> entriesById;

        internal RealmGemCatalogSnapshot(
            string sourceVersion,
            IList<RealmGemCatalogEntry> entries)
        {
            SourceVersion = sourceVersion ?? string.Empty;
            var copy = new RealmGemCatalogEntry[entries.Count];
            entries.CopyTo(copy, 0);
            Entries = Array.AsReadOnly(copy);
            entriesById = new Dictionary<string, RealmGemCatalogEntry>(
                entries.Count,
                StringComparer.Ordinal);
            for (var index = 0; index < entries.Count; index++)
            {
                entriesById.Add(entries[index].Id, entries[index]);
            }
        }

        public string SourceVersion { get; }
        public IReadOnlyList<RealmGemCatalogEntry> Entries { get; }

        public RealmGemQueryResult Resolve(string gemId)
        {
            if (!RealmGemCatalogResolver.IsStableId(gemId))
            {
                return new RealmGemQueryResult(
                    RealmGemQueryStatus.InvalidId,
                    null,
                    RealmGemCatalogResolver.InvalidIdCode);
            }

            if (!entriesById.TryGetValue(gemId, out RealmGemCatalogEntry entry))
            {
                return new RealmGemQueryResult(
                    RealmGemQueryStatus.UnknownId,
                    null,
                    RealmGemCatalogResolver.UnknownIdCode);
            }

            return new RealmGemQueryResult(
                RealmGemQueryStatus.Found,
                entry,
                RealmGemCatalogResolver.FoundCode);
        }
    }

    public sealed class RealmGemCatalogBuildResult
    {
        internal RealmGemCatalogBuildResult(
            RealmGemCatalogStatus status,
            RealmGemCatalogSnapshot snapshot,
            string technicalCode)
        {
            Status = status;
            Snapshot = snapshot;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public RealmGemCatalogStatus Status { get; }
        public RealmGemCatalogSnapshot Snapshot { get; }
        public string TechnicalCode { get; }
        public bool IsReady => Status == RealmGemCatalogStatus.Ready && Snapshot != null;
    }

    public static class RealmGemCatalogResolver
    {
        public const string ReadyCode = "AL-REALM-GEM-CATALOG-READY";
        public const string AuthorityUnavailableCode = "AL-REALM-GEM-CATALOG-UNAVAILABLE";
        public const string InvalidAuthorityCode = "AL-REALM-GEM-CATALOG-INVALID";
        public const string InvalidIdCode = "AL-REALM-GEM-ID-INVALID";
        public const string UnknownIdCode = "AL-REALM-GEM-ID-UNKNOWN";
        public const string FoundCode = "AL-REALM-GEM-ID-FOUND";

        private static readonly string[] RealmOrder =
        {
            "crownlands",
            "stonehold",
            "eldergrove",
            "umbral"
        };

        private static readonly RealmId[] RuntimeRealmOrder =
        {
            RealmId.Crownlands,
            RealmId.Stonehold,
            RealmId.Eldergrove,
            RealmId.Umbral
        };

        public static RealmGemCatalogBuildResult BuildCurrent()
        {
            return Build(RealmCatalogRuntime.Current);
        }

        public static RealmGemCatalogBuildResult Build(RealmCatalogSnapshot authority)
        {
            if (authority == null)
            {
                return new RealmGemCatalogBuildResult(
                    RealmGemCatalogStatus.AuthorityUnavailable,
                    null,
                    AuthorityUnavailableCode);
            }

            if (!string.Equals(
                    authority.Version,
                    RealmCatalogRuntime.SupportedVersion,
                    StringComparison.Ordinal) ||
                authority.Realms == null ||
                authority.Realms.Count != RealmOrder.Length)
            {
                return Invalid();
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<RealmGemCatalogEntry>(RealmOrder.Length * 2);
            for (var realmIndex = 0; realmIndex < RealmOrder.Length; realmIndex++)
            {
                RealmCatalogEntry realm = authority.Realms[realmIndex];
                if (realm == null ||
                    !string.Equals(realm.Id, RealmOrder[realmIndex], StringComparison.Ordinal) ||
                    realm.RuntimeId != RuntimeRealmOrder[realmIndex] ||
                    realm.RealmGemIds == null ||
                    realm.RealmGemIds.Count != 2)
                {
                    return Invalid();
                }

                for (var gemIndex = 0; gemIndex < realm.RealmGemIds.Count; gemIndex++)
                {
                    string gemId = realm.RealmGemIds[gemIndex];
                    if (!IsStableId(gemId) || !seenIds.Add(gemId))
                    {
                        return Invalid();
                    }

                    entries.Add(new RealmGemCatalogEntry(
                        gemId,
                        realm.Id,
                        realm.RuntimeId,
                        gemIndex + 1));
                }
            }

            return new RealmGemCatalogBuildResult(
                RealmGemCatalogStatus.Ready,
                new RealmGemCatalogSnapshot(authority.Version, entries),
                ReadyCode);
        }

        private static RealmGemCatalogBuildResult Invalid()
        {
            return new RealmGemCatalogBuildResult(
                RealmGemCatalogStatus.InvalidAuthority,
                null,
                InvalidAuthorityCode);
        }

        internal static bool IsStableId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128 ||
                value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            var previousUnderscore = false;
            for (var index = 1; index < value.Length; index++)
            {
                char character = value[index];
                bool valid =
                    (character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9') ||
                    character == '_';
                if (!valid || (character == '_' && previousUnderscore))
                {
                    return false;
                }

                previousUnderscore = character == '_';
            }

            return !previousUnderscore;
        }
    }
}
