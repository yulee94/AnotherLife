using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Data.Catalogs;

namespace AL.Core.Relationships
{
    public enum RelationshipResolutionStatus
    {
        Found,
        AliasResolved,
        UnknownId,
        CatalogPending,
        CatalogUnavailable,
        InvalidRecord,
        UnsupportedVersion,
        InvalidId
    }

    public enum RelationshipDomain
    {
        NpcAffinity,
        FactionReputation,
        PersonaTrait
    }

    public enum RelationshipSnapshotStatus
    {
        Valid,
        ValidSparse,
        ValidWithPreservedUnknown,
        MalformedNullRow,
        MalformedBlankId,
        MalformedDuplicateId,
        MalformedNonFinite,
        MalformedOutOfRange,
        CatalogUnavailable
    }

    public enum RelationshipPersonaClassification
    {
        AllZero,
        UniqueDominant,
        Tie
    }

    public enum RelationshipPlanStatus
    {
        Prepared,
        PreparedClamped,
        NoChange,
        RejectedUnknownId,
        RejectedDisabledIdentity,
        RejectedInvalidDelta,
        RejectedMalformedSnapshot,
        RejectedReadOnly,
        RejectedOverflow,
        RejectedCorrelationRequired,
        RejectedStaleSnapshot
    }

    public enum RelationshipRowOperation
    {
        None,
        Create,
        Update
    }

    public interface IRelationshipIdentityResolver
    {
        RelationshipIdentityResolution ResolveNpc(string id);
        RelationshipIdentityResolution ResolveFaction(string id);
        string PolicyRevision { get; }
    }

    public sealed class RelationshipIdentity
    {
        internal RelationshipIdentity(
            RelationshipDomain domain,
            string canonicalId,
            IEnumerable<string> aliases,
            bool relationshipEnabled,
            string classificationProfileId)
        {
            Domain = domain;
            CanonicalId = canonicalId;
            LegacyAliases = Array.AsReadOnly((aliases ?? Enumerable.Empty<string>()).ToArray());
            RelationshipEnabled = relationshipEnabled;
            ClassificationProfileId = classificationProfileId;
        }

        public RelationshipDomain Domain { get; }
        public string CanonicalId { get; }
        public IReadOnlyList<string> LegacyAliases { get; }
        public bool RelationshipEnabled { get; }
        public string ClassificationProfileId { get; }
    }

    public sealed class RelationshipIdentityResolution
    {
        internal RelationshipIdentityResolution(
            RelationshipResolutionStatus status,
            string requestedId,
            RelationshipIdentity identity)
        {
            Status = status;
            RequestedId = requestedId;
            Identity = identity;
        }

        public RelationshipResolutionStatus Status { get; }
        public string RequestedId { get; }
        public RelationshipIdentity Identity { get; }
    }

    /// <summary>
    /// Immutable resolver over the byte-exact approved relationship source packet.
    /// Construction validates the bounded artifact and publishes either the complete
    /// catalog or no records; source bytes and parsed mutable structures are never retained.
    /// </summary>
    public sealed class RelationshipCatalogResolver : IRelationshipIdentityResolver
    {
        public const int ExpectedSourceByteLength = 20313;
        public const int MaximumSourceBytes = 24576;
        public const string ExpectedSourceSha256 =
            "ed85e4a2796c277a8223aaf0ae75332fd6437e1f81a4520cf1ae867ab2992b8d";
        public const string ExpectedCatalogId = "al_relationship_authority_content_catalog";
        public const string ExpectedCatalogVersion = "0.1.0";
        public const string ExpectedSourcePacketId = "al_narrative_relationship_authority_source_v001";

        private readonly IReadOnlyDictionary<string, RelationshipIdentity> npcs;
        private readonly IReadOnlyDictionary<string, RelationshipIdentity> factions;
        private readonly IReadOnlyDictionary<string, RelationshipIdentity> npcAliases;
        private readonly IReadOnlyDictionary<string, RelationshipIdentity> factionAliases;
        private readonly RelationshipResolutionStatus catalogStatus;

        public RelationshipCatalogResolver()
        {
            npcs = EmptyIdentityMap();
            factions = EmptyIdentityMap();
            npcAliases = EmptyIdentityMap();
            factionAliases = EmptyIdentityMap();
            NpcIds = EmptyStrings();
            FactionIds = EmptyStrings();
            PolicyRevision = string.Empty;
            catalogStatus = RelationshipResolutionStatus.CatalogPending;
        }

        public RelationshipCatalogResolver(byte[] sourceBytes)
        {
            if (sourceBytes == null)
            {
                npcs = EmptyIdentityMap();
                factions = EmptyIdentityMap();
                npcAliases = EmptyIdentityMap();
                factionAliases = EmptyIdentityMap();
                NpcIds = EmptyStrings();
                FactionIds = EmptyStrings();
                PolicyRevision = string.Empty;
                catalogStatus = RelationshipResolutionStatus.CatalogUnavailable;
                return;
            }

            BuildResult result = TryBuild(sourceBytes);
            npcs = result.Npcs;
            factions = result.Factions;
            npcAliases = result.NpcAliases;
            factionAliases = result.FactionAliases;
            NpcIds = result.NpcIds;
            FactionIds = result.FactionIds;
            PolicyRevision = result.PolicyRevision;
            catalogStatus = result.Status;
        }

        public IReadOnlyList<string> NpcIds { get; }
        public IReadOnlyList<string> FactionIds { get; }
        public string PolicyRevision { get; }

        public RelationshipIdentityResolution ResolveNpc(string id)
        {
            return Resolve(id, npcs, npcAliases);
        }

        public RelationshipIdentityResolution ResolveFaction(string id)
        {
            return Resolve(id, factions, factionAliases);
        }

        private RelationshipIdentityResolution Resolve(
            string id,
            IReadOnlyDictionary<string, RelationshipIdentity> canonical,
            IReadOnlyDictionary<string, RelationshipIdentity> aliases)
        {
            if (catalogStatus != RelationshipResolutionStatus.Found)
            {
                return new RelationshipIdentityResolution(catalogStatus, id, null);
            }

            if (string.IsNullOrEmpty(id))
            {
                return new RelationshipIdentityResolution(RelationshipResolutionStatus.InvalidId, id, null);
            }

            RelationshipIdentity identity;
            if (canonical.TryGetValue(id, out identity))
            {
                return new RelationshipIdentityResolution(RelationshipResolutionStatus.Found, id, identity);
            }

            if (aliases.TryGetValue(id, out identity))
            {
                return new RelationshipIdentityResolution(RelationshipResolutionStatus.AliasResolved, id, identity);
            }

            return new RelationshipIdentityResolution(RelationshipResolutionStatus.UnknownId, id, null);
        }

        private static BuildResult TryBuild(byte[] sourceBytes)
        {
            if (sourceBytes.Length != ExpectedSourceByteLength ||
                sourceBytes.Length > MaximumSourceBytes ||
                !string.Equals(ComputeSha256(sourceBytes), ExpectedSourceSha256, StringComparison.Ordinal))
            {
                return BuildResult.Invalid();
            }

            StrictJsonObject root;
            try
            {
                root = StrictJsonDocument.Parse(sourceBytes, MaximumSourceBytes) as StrictJsonObject;
            }
            catch (StrictJsonException)
            {
                return BuildResult.Invalid();
            }

            string version;
            string catalogId;
            string packetId;
            if (root == null ||
                !TryString(root, "version", out version) ||
                !TryString(root, "catalogId", out catalogId) ||
                !TryString(root, "sourcePacketId", out packetId))
            {
                return BuildResult.Invalid();
            }

            if (!string.Equals(version, ExpectedCatalogVersion, StringComparison.Ordinal) ||
                !string.Equals(catalogId, ExpectedCatalogId, StringComparison.Ordinal) ||
                !string.Equals(packetId, ExpectedSourcePacketId, StringComparison.Ordinal))
            {
                return BuildResult.Unsupported();
            }

            StrictJsonArray npcRows;
            StrictJsonArray factionRows;
            StrictJsonArray affinityProfiles;
            StrictJsonArray factionProfiles;
            StrictJsonObject personaPolicy;
            if (!TryArray(root, "npcRecords", out npcRows) || npcRows.Items.Count != 6 ||
                !TryArray(root, "factionRecords", out factionRows) || factionRows.Items.Count != 5 ||
                !TryArray(root, "affinityClassificationProfiles", out affinityProfiles) || affinityProfiles.Items.Count != 1 ||
                !TryArray(root, "factionClassificationProfiles", out factionProfiles) || factionProfiles.Items.Count != 1 ||
                !TryObject(root, "personaPolicy", out personaPolicy) ||
                !ValidatePolicy(affinityProfiles, factionProfiles, personaPolicy))
            {
                return BuildResult.Invalid();
            }

            Dictionary<string, RelationshipIdentity> npcs;
            Dictionary<string, RelationshipIdentity> npcAliases;
            Dictionary<string, RelationshipIdentity> factions;
            Dictionary<string, RelationshipIdentity> factionAliases;
            if (!TryIdentities(npcRows, RelationshipDomain.NpcAffinity, out npcs, out npcAliases) ||
                !TryIdentities(factionRows, RelationshipDomain.FactionReputation, out factions, out factionAliases))
            {
                return BuildResult.Invalid();
            }

            return BuildResult.Found(npcs, npcAliases, factions, factionAliases);
        }

        private static bool ValidatePolicy(
            StrictJsonArray affinityProfiles,
            StrictJsonArray factionProfiles,
            StrictJsonObject personaPolicy)
        {
            StrictJsonObject affinity = affinityProfiles.Items[0] as StrictJsonObject;
            StrictJsonObject faction = factionProfiles.Items[0] as StrictJsonObject;
            StrictJsonArray affinityBands;
            StrictJsonArray factionBands;
            StrictJsonArray traits;
            string affinityId;
            string factionId;
            string personaId;
            return affinity != null && faction != null &&
                TryString(affinity, "id", out affinityId) && affinityId == "affinity_legacy_five_band" &&
                TryArray(affinity, "bands", out affinityBands) && affinityBands.Items.Count == 5 &&
                TryString(faction, "id", out factionId) && factionId == "faction_legacy_five_band" &&
                TryArray(faction, "bands", out factionBands) && factionBands.Items.Count == 5 &&
                TryString(personaPolicy, "id", out personaId) && personaId == "persona_legacy_four_trait_policy" &&
                TryArray(personaPolicy, "supportedTraits", out traits) && traits.Items.Count == 4;
        }

        private static bool TryIdentities(
            StrictJsonArray rows,
            RelationshipDomain domain,
            out Dictionary<string, RelationshipIdentity> canonical,
            out Dictionary<string, RelationshipIdentity> aliases)
        {
            canonical = new Dictionary<string, RelationshipIdentity>(StringComparer.Ordinal);
            aliases = new Dictionary<string, RelationshipIdentity>(StringComparer.Ordinal);
            foreach (StrictJsonValue value in rows.Items)
            {
                StrictJsonObject row = value as StrictJsonObject;
                string id;
                string profile;
                bool enabled;
                StrictJsonArray aliasRows;
                if (row == null || !TryString(row, "id", out id) || !IsCanonicalId(id) ||
                    !TryString(row, "classificationProfileId", out profile) ||
                    !TryBoolean(row, "relationshipEnabled", out enabled) ||
                    !TryArray(row, "legacyAliases", out aliasRows) ||
                    canonical.ContainsKey(id) || aliases.ContainsKey(id))
                {
                    return false;
                }

                var aliasValues = new string[aliasRows.Items.Count];
                var aliasesInRow = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < aliasRows.Items.Count; index++)
                {
                    var alias = aliasRows.Items[index] as StrictJsonString;
                    if (alias == null || string.IsNullOrEmpty(alias.Value) ||
                        canonical.ContainsKey(alias.Value) || aliases.ContainsKey(alias.Value) ||
                        !aliasesInRow.Add(alias.Value))
                    {
                        return false;
                    }

                    aliasValues[index] = alias.Value;
                }

                var identity = new RelationshipIdentity(domain, id, aliasValues, enabled, profile);
                canonical.Add(id, identity);
                foreach (string alias in aliasValues)
                {
                    aliases.Add(alias, identity);
                }
            }

            return true;
        }

        private static bool IsCanonicalId(string id)
        {
            if (string.IsNullOrEmpty(id) || id[0] < 'a' || id[0] > 'z') return false;
            for (var index = 1; index < id.Length; index++)
            {
                char value = id[index];
                if ((value < 'a' || value > 'z') && (value < '0' || value > '9') && value != '_')
                {
                    return false;
                }
            }
            return !id.EndsWith("_", StringComparison.Ordinal) && !id.Contains("__");
        }

        private static bool TryString(StrictJsonObject parent, string name, out string result)
        {
            result = null;
            StrictJsonValue value;
            var text = parent.TryGet(name, out value) ? value as StrictJsonString : null;
            if (text == null || string.IsNullOrEmpty(text.Value)) return false;
            result = text.Value;
            return true;
        }

        private static bool TryBoolean(StrictJsonObject parent, string name, out bool result)
        {
            result = false;
            StrictJsonValue value;
            var boolean = parent.TryGet(name, out value) ? value as StrictJsonBoolean : null;
            if (boolean == null) return false;
            result = boolean.Value;
            return true;
        }

        private static bool TryArray(StrictJsonObject parent, string name, out StrictJsonArray result)
        {
            result = null;
            StrictJsonValue value;
            result = parent.TryGet(name, out value) ? value as StrictJsonArray : null;
            return result != null;
        }

        private static bool TryObject(StrictJsonObject parent, string name, out StrictJsonObject result)
        {
            result = null;
            StrictJsonValue value;
            result = parent.TryGet(name, out value) ? value as StrictJsonObject : null;
            return result != null;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static IReadOnlyDictionary<string, RelationshipIdentity> EmptyIdentityMap()
        {
            return new ReadOnlyDictionary<string, RelationshipIdentity>(
                new Dictionary<string, RelationshipIdentity>(StringComparer.Ordinal));
        }

        private static IReadOnlyList<string> EmptyStrings()
        {
            return Array.AsReadOnly(new string[0]);
        }

        private sealed class BuildResult
        {
            private BuildResult(RelationshipResolutionStatus status)
            {
                Status = status;
                Npcs = EmptyIdentityMap();
                Factions = EmptyIdentityMap();
                NpcAliases = EmptyIdentityMap();
                FactionAliases = EmptyIdentityMap();
                NpcIds = EmptyStrings();
                FactionIds = EmptyStrings();
                PolicyRevision = string.Empty;
            }

            internal RelationshipResolutionStatus Status { get; private set; }
            internal IReadOnlyDictionary<string, RelationshipIdentity> Npcs { get; private set; }
            internal IReadOnlyDictionary<string, RelationshipIdentity> Factions { get; private set; }
            internal IReadOnlyDictionary<string, RelationshipIdentity> NpcAliases { get; private set; }
            internal IReadOnlyDictionary<string, RelationshipIdentity> FactionAliases { get; private set; }
            internal IReadOnlyList<string> NpcIds { get; private set; }
            internal IReadOnlyList<string> FactionIds { get; private set; }
            internal string PolicyRevision { get; private set; }

            internal static BuildResult Invalid() { return new BuildResult(RelationshipResolutionStatus.InvalidRecord); }
            internal static BuildResult Unsupported() { return new BuildResult(RelationshipResolutionStatus.UnsupportedVersion); }

            internal static BuildResult Found(
                Dictionary<string, RelationshipIdentity> npcs,
                Dictionary<string, RelationshipIdentity> npcAliases,
                Dictionary<string, RelationshipIdentity> factions,
                Dictionary<string, RelationshipIdentity> factionAliases)
            {
                return new BuildResult(RelationshipResolutionStatus.Found)
                {
                    Npcs = new ReadOnlyDictionary<string, RelationshipIdentity>(npcs),
                    NpcAliases = new ReadOnlyDictionary<string, RelationshipIdentity>(npcAliases),
                    Factions = new ReadOnlyDictionary<string, RelationshipIdentity>(factions),
                    FactionAliases = new ReadOnlyDictionary<string, RelationshipIdentity>(factionAliases),
                    NpcIds = Array.AsReadOnly(npcs.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray()),
                    FactionIds = Array.AsReadOnly(factions.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray()),
                    PolicyRevision = ExpectedSourceSha256
                };
            }
        }
    }

    public sealed class RelationshipNumericRow
    {
        public RelationshipNumericRow(string id, double value)
        {
            Id = id;
            Value = value;
        }

        public string Id { get; }
        public double Value { get; }
    }

    public sealed class RelationshipNumericSnapshot
    {
        internal RelationshipNumericSnapshot(
            RelationshipDomain domain,
            RelationshipSnapshotStatus status,
            bool profileWritable,
            IReadOnlyDictionary<string, double> values,
            IReadOnlyList<string> unknownIds,
            IReadOnlyList<string> diagnostics,
            string policyRevision,
            string snapshotRevision)
        {
            Domain = domain;
            Status = status;
            ProfileWritable = profileWritable;
            Values = values;
            PreservedUnknownIds = unknownIds;
            Diagnostics = diagnostics;
            PolicyRevision = policyRevision;
            SnapshotRevision = snapshotRevision;
        }

        public RelationshipDomain Domain { get; }
        public RelationshipSnapshotStatus Status { get; }
        public bool ProfileWritable { get; }
        public IReadOnlyDictionary<string, double> Values { get; }
        public IReadOnlyList<string> PreservedUnknownIds { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public string PolicyRevision { get; }
        public string SnapshotRevision { get; }
        public bool CanPlan => ProfileWritable &&
            (Status == RelationshipSnapshotStatus.Valid ||
             Status == RelationshipSnapshotStatus.ValidSparse ||
             Status == RelationshipSnapshotStatus.ValidWithPreservedUnknown);
    }

    public sealed class RelationshipPersonaSnapshot
    {
        internal RelationshipPersonaSnapshot(
            IReadOnlyDictionary<string, int> values,
            RelationshipPersonaClassification classification,
            IReadOnlyList<string> dominantTraits,
            bool profileWritable,
            string revision)
        {
            Values = values;
            Classification = classification;
            DominantTraitIds = dominantTraits;
            ProfileWritable = profileWritable;
            SnapshotRevision = revision;
        }

        public IReadOnlyDictionary<string, int> Values { get; }
        public RelationshipPersonaClassification Classification { get; }
        public IReadOnlyList<string> DominantTraitIds { get; }
        public bool ProfileWritable { get; }
        public string SnapshotRevision { get; }
    }

    public static class RelationshipSnapshotBuilder
    {
        public static RelationshipNumericSnapshot BuildNpcAffinity(
            IRelationshipIdentityResolver resolver,
            IEnumerable<RelationshipNumericRow> rows,
            bool profileWritable)
        {
            return BuildNumeric(RelationshipDomain.NpcAffinity, resolver, rows, profileWritable);
        }

        public static RelationshipNumericSnapshot BuildFactionReputation(
            IRelationshipIdentityResolver resolver,
            IEnumerable<RelationshipNumericRow> rows,
            bool profileWritable)
        {
            return BuildNumeric(RelationshipDomain.FactionReputation, resolver, rows, profileWritable);
        }

        public static RelationshipPersonaSnapshot BuildPersona(
            int warlord,
            int diplomat,
            int sage,
            int rogue,
            bool profileWritable)
        {
            var values = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "warlord", warlord },
                { "diplomat", diplomat },
                { "sage", sage },
                { "rogue", rogue }
            };
            string[] sourceOrder = { "warlord", "diplomat", "sage", "rogue" };
            int maximum = values.Values.Max();
            string[] dominant = sourceOrder.Where(id => values[id] == maximum).ToArray();
            RelationshipPersonaClassification classification = values.Values.All(value => value == 0)
                ? RelationshipPersonaClassification.AllZero
                : dominant.Length == 1
                    ? RelationshipPersonaClassification.UniqueDominant
                    : RelationshipPersonaClassification.Tie;
            string revision = Fingerprint(
                "persona",
                sourceOrder.Select(id => id + "=" + values[id].ToString(CultureInfo.InvariantCulture)));
            return new RelationshipPersonaSnapshot(
                new ReadOnlyDictionary<string, int>(values),
                classification,
                Array.AsReadOnly(classification == RelationshipPersonaClassification.AllZero ? new string[0] : dominant),
                profileWritable,
                revision);
        }

        private static RelationshipNumericSnapshot BuildNumeric(
            RelationshipDomain domain,
            IRelationshipIdentityResolver resolver,
            IEnumerable<RelationshipNumericRow> rows,
            bool profileWritable)
        {
            if (resolver == null || string.IsNullOrEmpty(resolver.PolicyRevision))
            {
                return Empty(domain, RelationshipSnapshotStatus.CatalogUnavailable, profileWritable, string.Empty, "AL-REL-POLICY");
            }

            var values = new SortedDictionary<string, double>(StringComparer.Ordinal);
            var unknown = new SortedSet<string>(StringComparer.Ordinal);
            RelationshipSnapshotStatus? malformed = null;
            foreach (RelationshipNumericRow row in rows ?? Enumerable.Empty<RelationshipNumericRow>())
            {
                if (row == null)
                {
                    malformed = malformed ?? RelationshipSnapshotStatus.MalformedNullRow;
                    continue;
                }
                if (string.IsNullOrEmpty(row.Id))
                {
                    malformed = malformed ?? RelationshipSnapshotStatus.MalformedBlankId;
                    continue;
                }

                RelationshipIdentityResolution resolution = domain == RelationshipDomain.NpcAffinity
                    ? resolver.ResolveNpc(row.Id)
                    : resolver.ResolveFaction(row.Id);
                if (resolution.Status == RelationshipResolutionStatus.UnknownId)
                {
                    unknown.Add(row.Id);
                    continue;
                }
                if (resolution.Status != RelationshipResolutionStatus.Found &&
                    resolution.Status != RelationshipResolutionStatus.AliasResolved)
                {
                    malformed = malformed ?? RelationshipSnapshotStatus.CatalogUnavailable;
                    continue;
                }
                if (values.ContainsKey(resolution.Identity.CanonicalId))
                {
                    malformed = malformed ?? RelationshipSnapshotStatus.MalformedDuplicateId;
                    continue;
                }
                if (double.IsNaN(row.Value) || double.IsInfinity(row.Value))
                {
                    malformed = malformed ?? RelationshipSnapshotStatus.MalformedNonFinite;
                    continue;
                }
                if (domain == RelationshipDomain.NpcAffinity && (row.Value < -100d || row.Value > 100d))
                {
                    malformed = malformed ?? RelationshipSnapshotStatus.MalformedOutOfRange;
                    continue;
                }
                if (domain == RelationshipDomain.FactionReputation &&
                    (row.Value < int.MinValue || row.Value > int.MaxValue || row.Value != Math.Truncate(row.Value)))
                {
                    malformed = malformed ?? RelationshipSnapshotStatus.MalformedOutOfRange;
                    continue;
                }
                values.Add(resolution.Identity.CanonicalId, row.Value);
            }

            if (malformed.HasValue)
            {
                return Empty(domain, malformed.Value, profileWritable, resolver.PolicyRevision, Diagnostic(malformed.Value));
            }

            RelationshipSnapshotStatus status = unknown.Count > 0
                ? RelationshipSnapshotStatus.ValidWithPreservedUnknown
                : values.Count == 0 ? RelationshipSnapshotStatus.ValidSparse : RelationshipSnapshotStatus.Valid;
            var frozenValues = new ReadOnlyDictionary<string, double>(values);
            var frozenUnknown = Array.AsReadOnly(unknown.ToArray());
            string revision = Fingerprint(
                domain.ToString() + "|" + resolver.PolicyRevision,
                values.Select(pair => pair.Key + "=" + pair.Value.ToString("R", CultureInfo.InvariantCulture))
                    .Concat(unknown.Select(value => "?" + value)));
            return new RelationshipNumericSnapshot(
                domain,
                status,
                profileWritable,
                frozenValues,
                frozenUnknown,
                Array.AsReadOnly(new string[0]),
                resolver.PolicyRevision,
                revision);
        }

        private static RelationshipNumericSnapshot Empty(
            RelationshipDomain domain,
            RelationshipSnapshotStatus status,
            bool writable,
            string policyRevision,
            string diagnostic)
        {
            return new RelationshipNumericSnapshot(
                domain,
                status,
                writable,
                new ReadOnlyDictionary<string, double>(new Dictionary<string, double>(StringComparer.Ordinal)),
                Array.AsReadOnly(new string[0]),
                Array.AsReadOnly(new[] { diagnostic }),
                policyRevision,
                Fingerprint(domain.ToString() + "|" + policyRevision, new[] { diagnostic }));
        }

        private static string Diagnostic(RelationshipSnapshotStatus status)
        {
            switch (status)
            {
                case RelationshipSnapshotStatus.MalformedNullRow: return "AL-REL-NULL-ENTRY";
                case RelationshipSnapshotStatus.MalformedBlankId: return "AL-REL-BLANK-ID";
                case RelationshipSnapshotStatus.MalformedDuplicateId: return "AL-REL-DUPLICATE-ID";
                case RelationshipSnapshotStatus.MalformedNonFinite: return "AL-REL-NONFINITE";
                case RelationshipSnapshotStatus.MalformedOutOfRange: return "AL-REL-OUT-OF-RANGE";
                default: return "AL-REL-POLICY";
            }
        }

        internal static string Fingerprint(string prefix, IEnumerable<string> fields)
        {
            string canonical = prefix + "\n" + string.Join("\n", fields ?? Enumerable.Empty<string>());
            using (var sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))
                    .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
    }

    public sealed class RelationshipMutationPlan
    {
        internal RelationshipMutationPlan(
            RelationshipPlanStatus status,
            RelationshipDomain domain,
            string targetId,
            double requestedDelta,
            double previousValue,
            double newValue,
            double appliedDelta,
            RelationshipRowOperation rowOperation,
            string expectedSnapshotRevision,
            string policyRevision,
            string operationId,
            string correlationId)
        {
            Status = status;
            Domain = domain;
            CanonicalTargetId = targetId;
            RequestedDelta = requestedDelta;
            PreviousValue = previousValue;
            NewValue = newValue;
            AppliedDelta = appliedDelta;
            RowOperation = rowOperation;
            ExpectedSnapshotRevision = expectedSnapshotRevision;
            PolicyRevision = policyRevision;
            OperationId = operationId;
            CorrelationId = correlationId;
        }

        public RelationshipPlanStatus Status { get; }
        public RelationshipDomain Domain { get; }
        public string CanonicalTargetId { get; }
        public double RequestedDelta { get; }
        public double PreviousValue { get; }
        public double NewValue { get; }
        public double AppliedDelta { get; }
        public RelationshipRowOperation RowOperation { get; }
        public string ExpectedSnapshotRevision { get; }
        public string PolicyRevision { get; }
        public string OperationId { get; }
        public string CorrelationId { get; }
    }

    public static class RelationshipPlanner
    {
        public static RelationshipMutationPlan PlanAffinity(
            IRelationshipIdentityResolver resolver,
            RelationshipNumericSnapshot snapshot,
            string targetId,
            double delta,
            string operationId,
            string correlationId)
        {
            return Plan(resolver, snapshot, targetId, delta, operationId, correlationId, true);
        }

        public static RelationshipMutationPlan PlanFaction(
            IRelationshipIdentityResolver resolver,
            RelationshipNumericSnapshot snapshot,
            string targetId,
            int delta,
            string operationId,
            string correlationId)
        {
            return Plan(resolver, snapshot, targetId, delta, operationId, correlationId, false);
        }

        private static RelationshipMutationPlan Plan(
            IRelationshipIdentityResolver resolver,
            RelationshipNumericSnapshot snapshot,
            string targetId,
            double delta,
            string operationId,
            string correlationId,
            bool affinity)
        {
            RelationshipDomain domain = affinity ? RelationshipDomain.NpcAffinity : RelationshipDomain.FactionReputation;
            if (snapshot == null || snapshot.Domain != domain || !snapshot.CanPlan)
            {
                RelationshipPlanStatus status = snapshot != null && !snapshot.ProfileWritable
                    ? RelationshipPlanStatus.RejectedReadOnly
                    : RelationshipPlanStatus.RejectedMalformedSnapshot;
                return Rejected(status, domain, targetId, delta, snapshot, operationId, correlationId);
            }
            if (string.IsNullOrEmpty(operationId) || string.IsNullOrEmpty(correlationId))
            {
                return Rejected(RelationshipPlanStatus.RejectedCorrelationRequired, domain, targetId, delta, snapshot, operationId, correlationId);
            }
            if (resolver == null || !string.Equals(resolver.PolicyRevision, snapshot.PolicyRevision, StringComparison.Ordinal))
            {
                return Rejected(RelationshipPlanStatus.RejectedStaleSnapshot, domain, targetId, delta, snapshot, operationId, correlationId);
            }

            RelationshipIdentityResolution resolution = affinity ? resolver.ResolveNpc(targetId) : resolver.ResolveFaction(targetId);
            if (resolution.Status != RelationshipResolutionStatus.Found &&
                resolution.Status != RelationshipResolutionStatus.AliasResolved)
            {
                return Rejected(RelationshipPlanStatus.RejectedUnknownId, domain, targetId, delta, snapshot, operationId, correlationId);
            }
            if (!resolution.Identity.RelationshipEnabled)
            {
                return Rejected(RelationshipPlanStatus.RejectedDisabledIdentity, domain, resolution.Identity.CanonicalId, delta, snapshot, operationId, correlationId);
            }
            if (double.IsNaN(delta) || double.IsInfinity(delta))
            {
                return Rejected(RelationshipPlanStatus.RejectedInvalidDelta, domain, resolution.Identity.CanonicalId, delta, snapshot, operationId, correlationId);
            }

            double previous;
            bool exists = snapshot.Values.TryGetValue(resolution.Identity.CanonicalId, out previous);
            if (delta == 0d)
            {
                return NewPlan(RelationshipPlanStatus.NoChange, domain, resolution.Identity.CanonicalId, 0d,
                    previous, previous, 0d, RelationshipRowOperation.None, snapshot, operationId, correlationId);
            }

            double next;
            RelationshipPlanStatus preparedStatus = RelationshipPlanStatus.Prepared;
            if (affinity)
            {
                double raw = previous + delta;
                if (double.IsNaN(raw))
                {
                    return Rejected(RelationshipPlanStatus.RejectedInvalidDelta, domain, resolution.Identity.CanonicalId, delta, snapshot, operationId, correlationId);
                }
                next = Math.Max(-100d, Math.Min(100d, raw));
                if (next - previous != delta) preparedStatus = RelationshipPlanStatus.PreparedClamped;
            }
            else
            {
                if (previous < int.MinValue || previous > int.MaxValue || previous != Math.Truncate(previous) ||
                    delta < int.MinValue || delta > int.MaxValue || delta != Math.Truncate(delta))
                {
                    return Rejected(RelationshipPlanStatus.RejectedInvalidDelta, domain, resolution.Identity.CanonicalId, delta, snapshot, operationId, correlationId);
                }
                long candidate = (long)previous + (long)delta;
                if (candidate < int.MinValue || candidate > int.MaxValue)
                {
                    return Rejected(RelationshipPlanStatus.RejectedOverflow, domain, resolution.Identity.CanonicalId, delta, snapshot, operationId, correlationId);
                }
                next = candidate;
            }

            return NewPlan(preparedStatus, domain, resolution.Identity.CanonicalId, delta, previous, next,
                next - previous, exists ? RelationshipRowOperation.Update : RelationshipRowOperation.Create,
                snapshot, operationId, correlationId);
        }

        private static RelationshipMutationPlan Rejected(
            RelationshipPlanStatus status,
            RelationshipDomain domain,
            string target,
            double delta,
            RelationshipNumericSnapshot snapshot,
            string operationId,
            string correlationId)
        {
            return NewPlan(status, domain, target, delta, 0d, 0d, 0d, RelationshipRowOperation.None,
                snapshot, operationId, correlationId);
        }

        private static RelationshipMutationPlan NewPlan(
            RelationshipPlanStatus status,
            RelationshipDomain domain,
            string target,
            double delta,
            double previous,
            double next,
            double applied,
            RelationshipRowOperation operation,
            RelationshipNumericSnapshot snapshot,
            string operationId,
            string correlationId)
        {
            return new RelationshipMutationPlan(status, domain, target, delta, previous, next, applied,
                operation, snapshot == null ? string.Empty : snapshot.SnapshotRevision,
                snapshot == null ? string.Empty : snapshot.PolicyRevision, operationId, correlationId);
        }
    }
}
