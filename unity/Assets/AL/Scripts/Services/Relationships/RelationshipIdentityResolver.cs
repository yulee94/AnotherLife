using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core.Interfaces.Relationships;

namespace AL.Services.Relationships
{
    public sealed class InjectedRelationshipIdentityResolver : IRelationshipIdentityResolver
    {
        private readonly Dictionary<string, RelationshipIdentityRecord> _npcs;
        private readonly Dictionary<string, string> _npcAliases;
        private readonly Dictionary<string, RelationshipIdentityRecord> _factions;
        private readonly Dictionary<string, string> _factionAliases;

        public InjectedRelationshipIdentityResolver(
            RelationshipCatalogAvailability availability,
            string identityCatalogRevision,
            int schemaVersion,
            IEnumerable<RelationshipIdentityRecord> npcRecords,
            IEnumerable<RelationshipIdentityRecord> factionRecords)
        {
            Availability = availability;
            IdentityCatalogRevision = identityCatalogRevision ?? string.Empty;
            SchemaVersion = schemaVersion;
            _npcs = new Dictionary<string, RelationshipIdentityRecord>(StringComparer.Ordinal);
            _npcAliases = new Dictionary<string, string>(StringComparer.Ordinal);
            _factions = new Dictionary<string, RelationshipIdentityRecord>(StringComparer.Ordinal);
            _factionAliases = new Dictionary<string, string>(StringComparer.Ordinal);
            CatalogValidation = ValidateAndIndex(
                npcRecords,
                factionRecords,
                _npcs,
                _npcAliases,
                _factions,
                _factionAliases,
                availability,
                schemaVersion);
        }

        public RelationshipCatalogAvailability Availability { get; }

        public string IdentityCatalogRevision { get; }

        public int SchemaVersion { get; }

        public RelationshipIdentityCatalogValidationResult CatalogValidation { get; }

        public RelationshipIdentityResolution ResolveNpc(string npcId)
        {
            return Resolve(
                npcId,
                RelationshipDomain.NpcAffinity,
                _npcs,
                _npcAliases);
        }

        public RelationshipIdentityResolution ResolveFaction(string factionId)
        {
            return Resolve(
                factionId,
                RelationshipDomain.FactionReputation,
                _factions,
                _factionAliases);
        }

        private RelationshipIdentityResolution Resolve(
            string requestedId,
            RelationshipDomain domain,
            Dictionary<string, RelationshipIdentityRecord> canonicals,
            Dictionary<string, string> aliases)
        {
            if (Availability == RelationshipCatalogAvailability.Pending)
            {
                return new RelationshipIdentityResolution(
                    RelationshipIdentityStatus.CatalogPending,
                    requestedId,
                    string.Empty,
                    false,
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.CatalogPending,
                            domain,
                            requestedId,
                            "Identity catalog is pending #183 production authority.")
                    });
            }

            if (Availability == RelationshipCatalogAvailability.Unavailable)
            {
                return new RelationshipIdentityResolution(
                    RelationshipIdentityStatus.CatalogUnavailable,
                    requestedId,
                    string.Empty,
                    false,
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.CatalogUnavailable,
                            domain,
                            requestedId,
                            "Identity catalog is unavailable.")
                    });
            }

            if (SchemaVersion != RelationshipTechnicalLimits.CurrentSchemaVersion)
            {
                return new RelationshipIdentityResolution(
                    RelationshipIdentityStatus.UnsupportedVersion,
                    requestedId,
                    string.Empty,
                    false,
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.UnsupportedVersion,
                            domain,
                            requestedId,
                            "Identity catalog schema version is unsupported.")
                    });
            }

            if (!CatalogValidation.IsValid)
            {
                return new RelationshipIdentityResolution(
                    RelationshipIdentityStatus.InvalidRecord,
                    requestedId,
                    string.Empty,
                    false,
                    CatalogValidation.Diagnostics);
            }

            if (string.IsNullOrEmpty(requestedId))
            {
                return new RelationshipIdentityResolution(
                    RelationshipIdentityStatus.UnknownId,
                    requestedId,
                    string.Empty,
                    false,
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.BlankId,
                            domain,
                            requestedId,
                            "Blank identity is invalid.")
                    });
            }

            if (canonicals.TryGetValue(requestedId, out RelationshipIdentityRecord canonical))
            {
                return new RelationshipIdentityResolution(
                    RelationshipIdentityStatus.Found,
                    requestedId,
                    canonical.CanonicalId,
                    canonical.RelationshipEnabled,
                    Array.Empty<RelationshipDiagnostic>());
            }

            if (aliases.TryGetValue(requestedId, out string aliasedCanonical) &&
                canonicals.TryGetValue(aliasedCanonical, out RelationshipIdentityRecord aliased))
            {
                return new RelationshipIdentityResolution(
                    RelationshipIdentityStatus.AliasResolved,
                    requestedId,
                    aliased.CanonicalId,
                    aliased.RelationshipEnabled,
                    Array.Empty<RelationshipDiagnostic>());
            }

            return new RelationshipIdentityResolution(
                RelationshipIdentityStatus.UnknownId,
                requestedId,
                string.Empty,
                false,
                new[]
                {
                    Error(
                        RelationshipDiagnosticCodes.UnknownId,
                        domain,
                        requestedId,
                        "Identity is unknown to the injected catalog.")
                });
        }

        private static RelationshipIdentityCatalogValidationResult ValidateAndIndex(
            IEnumerable<RelationshipIdentityRecord> npcRecords,
            IEnumerable<RelationshipIdentityRecord> factionRecords,
            Dictionary<string, RelationshipIdentityRecord> npcs,
            Dictionary<string, string> npcAliases,
            Dictionary<string, RelationshipIdentityRecord> factions,
            Dictionary<string, string> factionAliases,
            RelationshipCatalogAvailability availability,
            int schemaVersion)
        {
            if (availability == RelationshipCatalogAvailability.Pending)
            {
                return new RelationshipIdentityCatalogValidationResult(
                    RelationshipIdentityCatalogValidationStatus.CatalogPending,
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.CatalogPending,
                            null,
                            string.Empty,
                            "Injected identity records are not production #183 authority.")
                    });
            }

            if (availability == RelationshipCatalogAvailability.Unavailable)
            {
                return new RelationshipIdentityCatalogValidationResult(
                    RelationshipIdentityCatalogValidationStatus.CatalogUnavailable,
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.CatalogUnavailable,
                            null,
                            string.Empty,
                            "Identity catalog is unavailable.")
                    });
            }

            if (schemaVersion != RelationshipTechnicalLimits.CurrentSchemaVersion)
            {
                return new RelationshipIdentityCatalogValidationResult(
                    RelationshipIdentityCatalogValidationStatus.UnsupportedVersion,
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.UnsupportedVersion,
                            null,
                            string.Empty,
                            "Identity catalog schema version is unsupported.")
                    });
            }

            var diagnostics = new List<RelationshipDiagnostic>();
            RelationshipIdentityCatalogValidationStatus status =
                IndexDomain(
                    npcRecords,
                    RelationshipDomain.NpcAffinity,
                    npcs,
                    npcAliases,
                    diagnostics);
            RelationshipIdentityCatalogValidationStatus factionStatus =
                IndexDomain(
                    factionRecords,
                    RelationshipDomain.FactionReputation,
                    factions,
                    factionAliases,
                    diagnostics);
            if (status == RelationshipIdentityCatalogValidationStatus.Valid)
            {
                status = factionStatus;
            }

            return new RelationshipIdentityCatalogValidationResult(status, diagnostics);
        }

        private static RelationshipIdentityCatalogValidationStatus IndexDomain(
            IEnumerable<RelationshipIdentityRecord> records,
            RelationshipDomain domain,
            Dictionary<string, RelationshipIdentityRecord> canonicals,
            Dictionary<string, string> aliases,
            List<RelationshipDiagnostic> diagnostics)
        {
            RelationshipIdentityCatalogValidationStatus status =
                RelationshipIdentityCatalogValidationStatus.Valid;
            foreach (RelationshipIdentityRecord record in
                     records ?? Array.Empty<RelationshipIdentityRecord>())
            {
                if (record == null ||
                    string.IsNullOrEmpty(record.CanonicalId) ||
                    !IsCanonicalId(record.CanonicalId))
                {
                    diagnostics.Add(
                        Error(
                            RelationshipDiagnosticCodes.BlankId,
                            domain,
                            record?.CanonicalId,
                            "Canonical identity is blank or not lowercase snake-case."));
                    status = RelationshipIdentityCatalogValidationStatus.InvalidId;
                    continue;
                }

                if (canonicals.ContainsKey(record.CanonicalId))
                {
                    diagnostics.Add(
                        Error(
                            RelationshipDiagnosticCodes.DuplicateId,
                            domain,
                            record.CanonicalId,
                            "Duplicate canonical identity."));
                    status = RelationshipIdentityCatalogValidationStatus.DuplicateId;
                    continue;
                }

                canonicals.Add(record.CanonicalId, record);
            }

            foreach (RelationshipIdentityRecord record in canonicals.Values)
            {
                foreach (string alias in record.LegacyAliases)
                {
                    if (string.IsNullOrEmpty(alias))
                    {
                        diagnostics.Add(
                            Error(
                                RelationshipDiagnosticCodes.Alias,
                                domain,
                                record.CanonicalId,
                                "Blank alias is invalid."));
                        status = RelationshipIdentityCatalogValidationStatus.InvalidId;
                        continue;
                    }

                    if (string.Equals(alias, record.CanonicalId, StringComparison.Ordinal))
                    {
                        diagnostics.Add(
                            Error(
                                RelationshipDiagnosticCodes.Alias,
                                domain,
                                record.CanonicalId,
                                "Alias cycles onto its own canonical identity."));
                        status = RelationshipIdentityCatalogValidationStatus.AliasCycle;
                        continue;
                    }

                    if (canonicals.ContainsKey(alias))
                    {
                        diagnostics.Add(
                            Error(
                                RelationshipDiagnosticCodes.Alias,
                                domain,
                                alias,
                                "Alias shadows another canonical identity."));
                        status = RelationshipIdentityCatalogValidationStatus.AliasShadow;
                        continue;
                    }

                    if (aliases.ContainsKey(alias))
                    {
                        diagnostics.Add(
                            Error(
                                RelationshipDiagnosticCodes.Alias,
                                domain,
                                alias,
                                "Alias collides with another alias."));
                        status = RelationshipIdentityCatalogValidationStatus.AliasCollision;
                        continue;
                    }

                    aliases.Add(alias, record.CanonicalId);
                }
            }

            return status;
        }

        internal static bool IsCanonicalId(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            for (int i = 1; i < value.Length; i++)
            {
                char c = value[i];
                bool ok = (c >= 'a' && c <= 'z') ||
                          (c >= '0' && c <= '9') ||
                          c == '_';
                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }

        private static RelationshipDiagnostic Error(
            string code,
            RelationshipDomain? domain,
            string targetId,
            string action)
        {
            return new RelationshipDiagnostic(
                RelationshipDiagnosticSeverity.Error,
                code,
                domain,
                string.Empty,
                targetId ?? string.Empty,
                string.Empty,
                string.Empty,
                action,
                true);
        }
    }

    public sealed class InjectedRelationshipPolicyResolver : IRelationshipPolicyResolver
    {
        public InjectedRelationshipPolicyResolver(
            RelationshipCatalogAvailability availability,
            RelationshipPolicySnapshot policy)
        {
            Availability = availability;
            Policy = policy;
            PolicyValidation = Validate(availability, policy);
        }

        public RelationshipCatalogAvailability Availability { get; }

        public RelationshipPolicySnapshot Policy { get; }

        public RelationshipPolicyValidationResult PolicyValidation { get; }

        public RelationshipPolicySnapshot ResolvePolicy()
        {
            return Policy;
        }

        public static RelationshipPolicySnapshot CreateLegacyFixturePolicy(
            string identityCatalogRevision)
        {
            RelationshipClassificationBand[] affinity =
            {
                new RelationshipClassificationBand(
                    "affinity_exalted", 80d, 100d, true, true,
                    "relationship.affinity.exalted.name"),
                new RelationshipClassificationBand(
                    "affinity_friendly", 50d, 80d, true, false,
                    "relationship.affinity.friendly.name"),
                new RelationshipClassificationBand(
                    "affinity_neutral", 0d, 50d, true, false,
                    "relationship.affinity.neutral.name"),
                new RelationshipClassificationBand(
                    "affinity_hostile", -50d, 0d, true, false,
                    "relationship.affinity.hostile.name"),
                new RelationshipClassificationBand(
                    "affinity_nemesis", -100d, -50d, true, false,
                    "relationship.affinity.nemesis.name")
            };
            RelationshipClassificationBand[] faction =
            {
                new RelationshipClassificationBand(
                    "faction_ally", 500d, int.MaxValue, true, true,
                    "relationship.faction_band.ally.name"),
                new RelationshipClassificationBand(
                    "faction_supporter", 100d, 500d, true, false,
                    "relationship.faction_band.supporter.name"),
                new RelationshipClassificationBand(
                    "faction_neutral", -100d, 100d, false, false,
                    "relationship.faction_band.neutral.name"),
                new RelationshipClassificationBand(
                    "faction_opponent", -500d, -100d, false, true,
                    "relationship.faction_band.opponent.name"),
                new RelationshipClassificationBand(
                    "faction_enemy", int.MinValue, -500d, true, true,
                    "relationship.faction_band.enemy.name")
            };
            return new RelationshipPolicySnapshot(
                RelationshipTechnicalLimits.CurrentSchemaVersion,
                RelationshipTechnicalLimits.FixtureContentVersion,
                RelationshipTechnicalLimits.FixtureSourceRevision,
                identityCatalogRevision,
                RelationshipTechnicalLimits.FixturePolicyRevision,
                RelationshipTechnicalLimits.AffinityMinimum,
                RelationshipTechnicalLimits.AffinityMaximum,
                affinity,
                faction,
                new[]
                {
                    AL.Core.Interfaces.PersonaTrait.Warlord,
                    AL.Core.Interfaces.PersonaTrait.Diplomat,
                    AL.Core.Interfaces.PersonaTrait.Sage,
                    AL.Core.Interfaces.PersonaTrait.Rogue
                },
                "relationship.persona_state.unique_dominant.name",
                "relationship.persona_state.tie.name",
                "relationship.persona_state.all_zero.name",
                "relationship.persona_state.unavailable.name",
                "relationship.persona_state.malformed.name");
        }

        private static RelationshipPolicyValidationResult Validate(
            RelationshipCatalogAvailability availability,
            RelationshipPolicySnapshot policy)
        {
            if (availability == RelationshipCatalogAvailability.Pending)
            {
                return new RelationshipPolicyValidationResult(
                    RelationshipPolicyValidationStatus.CatalogPending,
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.CatalogPending,
                            "Policy catalog is pending #183 production authority.")
                    });
            }

            if (availability == RelationshipCatalogAvailability.Unavailable || policy == null)
            {
                return new RelationshipPolicyValidationResult(
                    RelationshipPolicyValidationStatus.CatalogUnavailable,
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.CatalogUnavailable,
                            "Policy catalog is unavailable.")
                    });
            }

            if (policy.SchemaVersion != RelationshipTechnicalLimits.CurrentSchemaVersion)
            {
                return new RelationshipPolicyValidationResult(
                    RelationshipPolicyValidationStatus.UnsupportedVersion,
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.UnsupportedVersion,
                            "Policy schema version is unsupported.")
                    });
            }

            var diagnostics = new List<RelationshipDiagnostic>();
            if (policy.AffinityMinimum != RelationshipTechnicalLimits.AffinityMinimum ||
                policy.AffinityMaximum != RelationshipTechnicalLimits.AffinityMaximum ||
                !IsFinite(policy.AffinityMinimum) ||
                !IsFinite(policy.AffinityMaximum))
            {
                diagnostics.Add(
                    Error(
                        RelationshipDiagnosticCodes.OutOfRange,
                        "Affinity range must remain exactly [-100,100]."));
                return new RelationshipPolicyValidationResult(
                    RelationshipPolicyValidationStatus.InvalidRange,
                    diagnostics);
            }

            if (!BandsCoverExactly(policy.AffinityBands, -100d, 100d, true, true) ||
                !BandsCoverExactly(
                    policy.FactionBands,
                    int.MinValue,
                    int.MaxValue,
                    true,
                    true))
            {
                diagnostics.Add(
                    Error(
                        RelationshipDiagnosticCodes.Policy,
                        "Classification bands overlap, gap, or drift from the legacy profile."));
                return new RelationshipPolicyValidationResult(
                    RelationshipPolicyValidationStatus.InvalidBandOverlapOrGap,
                    diagnostics);
            }

            if (policy.SupportedPersonaTraits.Count != 4 ||
                !policy.SupportedPersonaTraits.Contains(AL.Core.Interfaces.PersonaTrait.Warlord) ||
                !policy.SupportedPersonaTraits.Contains(AL.Core.Interfaces.PersonaTrait.Diplomat) ||
                !policy.SupportedPersonaTraits.Contains(AL.Core.Interfaces.PersonaTrait.Sage) ||
                !policy.SupportedPersonaTraits.Contains(AL.Core.Interfaces.PersonaTrait.Rogue))
            {
                diagnostics.Add(
                    Error(
                        RelationshipDiagnosticCodes.Policy,
                        "Persona policy must include the four current traits."));
                return new RelationshipPolicyValidationResult(
                    RelationshipPolicyValidationStatus.InvalidPersonaTraits,
                    diagnostics);
            }

            return new RelationshipPolicyValidationResult(
                RelationshipPolicyValidationStatus.Valid,
                diagnostics);
        }

        private static bool BandsCoverExactly(
            IReadOnlyList<RelationshipClassificationBand> bands,
            double minimum,
            double maximum,
            bool minimumInclusive,
            bool maximumInclusive)
        {
            if (bands == null || bands.Count == 0)
            {
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            RelationshipClassificationBand[] ordered = bands
                .OrderBy(band => band.Minimum)
                .ThenBy(band => band.ClassificationId, StringComparer.Ordinal)
                .ToArray();
            if (ordered[0].Minimum != minimum ||
                ordered[0].MinimumInclusive != minimumInclusive)
            {
                return false;
            }

            double cursor = ordered[0].Maximum;
            bool cursorInclusive = ordered[0].MaximumInclusive;
            if (!ids.Add(ordered[0].ClassificationId) ||
                string.IsNullOrEmpty(ordered[0].ClassificationId) ||
                string.IsNullOrEmpty(ordered[0].ContentReference))
            {
                return false;
            }

            for (int i = 1; i < ordered.Length; i++)
            {
                RelationshipClassificationBand band = ordered[i];
                if (!ids.Add(band.ClassificationId) ||
                    string.IsNullOrEmpty(band.ClassificationId) ||
                    string.IsNullOrEmpty(band.ContentReference))
                {
                    return false;
                }

                bool contiguous = cursorInclusive
                    ? !band.MinimumInclusive && band.Minimum == cursor
                    : band.MinimumInclusive && band.Minimum == cursor;
                if (!contiguous)
                {
                    return false;
                }

                cursor = band.Maximum;
                cursorInclusive = band.MaximumInclusive;
            }

            return cursor == maximum && cursorInclusive == maximumInclusive;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static RelationshipDiagnostic Error(string code, string action)
        {
            return new RelationshipDiagnostic(
                RelationshipDiagnosticSeverity.Error,
                code,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                action,
                true);
        }
    }
}
