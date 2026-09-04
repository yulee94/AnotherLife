using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Relationships;

namespace AL.Services.Relationships
{
    public static class RelationshipSnapshotBuilder
    {
        public static RelationshipSnapshot Build(
            RelationshipRawState raw,
            IRelationshipIdentityResolver identities,
            IRelationshipPolicyResolver policies)
        {
            raw ??= RelationshipRawState.NoSave();
            RelationshipPolicySnapshot policy = policies?.ResolvePolicy();
            bool policyReady = policies != null &&
                               policies.PolicyValidation != null &&
                               policies.PolicyValidation.IsValid &&
                               policies.Availability == RelationshipCatalogAvailability.Available &&
                               policy != null;

            RelationshipNpcAffinityDomainSnapshot npc = BuildNpc(
                raw,
                identities,
                policyReady,
                policy);
            RelationshipFactionDomainSnapshot faction = BuildFaction(
                raw,
                identities,
                policyReady,
                policy);
            RelationshipPersonaDomainSnapshot persona = BuildPersona(
                raw,
                policyReady,
                policy);

            var diagnostics = new List<RelationshipDiagnostic>();
            diagnostics.AddRange(npc.Diagnostics);
            diagnostics.AddRange(faction.Diagnostics);
            diagnostics.AddRange(persona.Diagnostics);

            string snapshotRevision = RelationshipHash.Compute(
                policy?.PolicyRevision,
                npc.Fingerprint,
                faction.Fingerprint,
                persona.Fingerprint,
                raw.ProfileWritable ? "w" : "r");

            return new RelationshipSnapshot(
                snapshotRevision,
                policy?.PolicyRevision ?? string.Empty,
                raw.ProfileWritable,
                npc,
                faction,
                persona,
                diagnostics);
        }

        public static RelationshipQueryResult QueryNpcAffinity(
            RelationshipSnapshot snapshot,
            IRelationshipIdentityResolver identities,
            string npcId)
        {
            return QueryNumeric<float>(
                snapshot,
                identities,
                RelationshipDomain.NpcAffinity,
                npcId,
                snapshot.NpcAffinityDomain.Status,
                snapshot.NpcAffinityDomain.IsMutationReady,
                snapshot.NpcAffinityDomain.SupportedValuesByCanonicalNpcId.TryGetValue,
                snapshot.NpcAffinityDomain.Fingerprint);
        }

        public static RelationshipQueryResult QueryFactionReputation(
            RelationshipSnapshot snapshot,
            IRelationshipIdentityResolver identities,
            string factionId)
        {
            return QueryNumeric<int>(
                snapshot,
                identities,
                RelationshipDomain.FactionReputation,
                factionId,
                snapshot.FactionDomain.Status,
                snapshot.FactionDomain.IsMutationReady,
                (string id, out int value) =>
                    snapshot.FactionDomain.SupportedValuesByCanonicalFactionId.TryGetValue(
                        id,
                        out value),
                snapshot.FactionDomain.Fingerprint);
        }

        public static RelationshipClassificationQueryResult ClassifyNpcAffinity(
            RelationshipSnapshot snapshot,
            IRelationshipPolicyResolver policies,
            IRelationshipIdentityResolver identities,
            string npcId)
        {
            RelationshipQueryResult query = QueryNpcAffinity(snapshot, identities, npcId);
            return Classify(query, policies?.ResolvePolicy()?.AffinityBands);
        }

        public static RelationshipClassificationQueryResult ClassifyFactionReputation(
            RelationshipSnapshot snapshot,
            IRelationshipPolicyResolver policies,
            IRelationshipIdentityResolver identities,
            string factionId)
        {
            RelationshipQueryResult query = QueryFactionReputation(snapshot, identities, factionId);
            return Classify(query, policies?.ResolvePolicy()?.FactionBands);
        }

        public static PersonaClassificationResult ClassifyPersona(RelationshipSnapshot snapshot)
        {
            return snapshot.PersonaDomain.Classification;
        }

        private delegate bool TryGetValue<T>(string key, out T value);

        private static RelationshipQueryResult QueryNumeric<T>(
            RelationshipSnapshot snapshot,
            IRelationshipIdentityResolver identities,
            RelationshipDomain domain,
            string requestedId,
            RelationshipDomainValidationStatus domainStatus,
            bool domainReady,
            TryGetValue<T> tryGet,
            string domainFingerprint)
            where T : struct
        {
            if (snapshot == null ||
                domainStatus == RelationshipDomainValidationStatus.UnavailableNoCurrentSave)
            {
                return new RelationshipQueryResult(
                    RelationshipQueryStatus.UnavailableNoSave,
                    domain,
                    requestedId,
                    string.Empty,
                    0d,
                    snapshot?.SnapshotRevision ?? string.Empty,
                    snapshot?.PolicyRevision ?? string.Empty,
                    Array.Empty<RelationshipDiagnostic>());
            }

            if (domainStatus == RelationshipDomainValidationStatus.MalformedPolicyUnavailable ||
                domainStatus == RelationshipDomainValidationStatus.UnsupportedDefinitionVersion)
            {
                return new RelationshipQueryResult(
                    domainStatus == RelationshipDomainValidationStatus.UnsupportedDefinitionVersion
                        ? RelationshipQueryStatus.UnsupportedVersion
                        : RelationshipQueryStatus.UnavailablePolicy,
                    domain,
                    requestedId,
                    string.Empty,
                    0d,
                    snapshot.SnapshotRevision,
                    snapshot.PolicyRevision,
                    Array.Empty<RelationshipDiagnostic>());
            }

            RelationshipIdentityResolution resolution = domain == RelationshipDomain.NpcAffinity
                ? identities.ResolveNpc(requestedId)
                : identities.ResolveFaction(requestedId);

            if (resolution.Status == RelationshipIdentityStatus.CatalogPending ||
                resolution.Status == RelationshipIdentityStatus.CatalogUnavailable ||
                resolution.Status == RelationshipIdentityStatus.InvalidRecord ||
                resolution.Status == RelationshipIdentityStatus.UnsupportedVersion)
            {
                return new RelationshipQueryResult(
                    MapIdentityToQuery(resolution.Status),
                    domain,
                    requestedId,
                    string.Empty,
                    0d,
                    snapshot.SnapshotRevision,
                    snapshot.PolicyRevision,
                    resolution.Diagnostics);
            }

            if (!resolution.SupportsMutation)
            {
                return new RelationshipQueryResult(
                    RelationshipQueryStatus.UnavailableUnknownId,
                    domain,
                    requestedId,
                    resolution.CanonicalId,
                    0d,
                    snapshot.SnapshotRevision,
                    snapshot.PolicyRevision,
                    resolution.Diagnostics);
            }

            if (!domainReady)
            {
                return new RelationshipQueryResult(
                    RelationshipQueryStatus.UnavailableMalformedDomain,
                    domain,
                    requestedId,
                    resolution.CanonicalId,
                    0d,
                    snapshot.SnapshotRevision,
                    snapshot.PolicyRevision,
                    Array.Empty<RelationshipDiagnostic>());
            }

            if (tryGet(resolution.CanonicalId, out T stored))
            {
                return new RelationshipQueryResult(
                    resolution.Status == RelationshipIdentityStatus.AliasResolved
                        ? RelationshipQueryStatus.AliasResolved
                        : RelationshipQueryStatus.Available,
                    domain,
                    requestedId,
                    resolution.CanonicalId,
                    Convert.ToDouble(stored, CultureInfo.InvariantCulture),
                    domainFingerprint,
                    snapshot.PolicyRevision,
                    Array.Empty<RelationshipDiagnostic>());
            }

            return new RelationshipQueryResult(
                RelationshipQueryStatus.AvailableSparseZero,
                domain,
                requestedId,
                resolution.CanonicalId,
                0d,
                domainFingerprint,
                snapshot.PolicyRevision,
                Array.Empty<RelationshipDiagnostic>());
        }

        private static RelationshipQueryStatus MapIdentityToQuery(
            RelationshipIdentityStatus status)
        {
            switch (status)
            {
                case RelationshipIdentityStatus.CatalogPending:
                case RelationshipIdentityStatus.CatalogUnavailable:
                case RelationshipIdentityStatus.InvalidRecord:
                    return RelationshipQueryStatus.UnavailablePolicy;
                case RelationshipIdentityStatus.UnsupportedVersion:
                    return RelationshipQueryStatus.UnsupportedVersion;
                default:
                    return RelationshipQueryStatus.UnavailableUnknownId;
            }
        }

        private static RelationshipClassificationQueryResult Classify(
            RelationshipQueryResult query,
            IReadOnlyList<RelationshipClassificationBand> bands)
        {
            if (query.Status != RelationshipQueryStatus.Available &&
                query.Status != RelationshipQueryStatus.AvailableSparseZero &&
                query.Status != RelationshipQueryStatus.AliasResolved)
            {
                return new RelationshipClassificationQueryResult(
                    query.Status,
                    string.Empty,
                    query.Value,
                    0d,
                    0d,
                    false,
                    false,
                    string.Empty,
                    query.Diagnostics);
            }

            foreach (RelationshipClassificationBand band in
                     bands ?? Array.Empty<RelationshipClassificationBand>())
            {
                if (band.Contains(query.Value))
                {
                    return new RelationshipClassificationQueryResult(
                        query.Status,
                        band.ClassificationId,
                        query.Value,
                        band.Minimum,
                        band.Maximum,
                        band.MinimumInclusive,
                        band.MaximumInclusive,
                        band.ContentReference,
                        Array.Empty<RelationshipDiagnostic>());
                }
            }

            return new RelationshipClassificationQueryResult(
                RelationshipQueryStatus.UnavailablePolicy,
                string.Empty,
                query.Value,
                0d,
                0d,
                false,
                false,
                string.Empty,
                Array.Empty<RelationshipDiagnostic>());
        }

        private static RelationshipNpcAffinityDomainSnapshot BuildNpc(
            RelationshipRawState raw,
            IRelationshipIdentityResolver identities,
            bool policyReady,
            RelationshipPolicySnapshot policy)
        {
            var diagnostics = new List<RelationshipDiagnostic>();
            if (!raw.HasCurrentSave)
            {
                return Npc(
                    RelationshipDomainValidationStatus.UnavailableNoCurrentSave,
                    "no-save",
                    new Dictionary<string, float>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    0,
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.NoCurrentSave,
                            RelationshipDomain.NpcAffinity,
                            string.Empty,
                            "No current save.")
                    });
            }

            if (!policyReady)
            {
                RelationshipDomainValidationStatus pending =
                    policiesUnavailableStatus(identities, policy);
                return Npc(
                    pending,
                    "policy-unavailable",
                    new Dictionary<string, float>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    raw.NpcAffinityRows.Count,
                    new[]
                    {
                        Error(
                            pending == RelationshipDomainValidationStatus.UnsupportedDefinitionVersion
                                ? RelationshipDiagnosticCodes.UnsupportedVersion
                                : RelationshipDiagnosticCodes.Policy,
                            RelationshipDomain.NpcAffinity,
                            string.Empty,
                            "Policy or identity catalog is not available.")
                    });
            }

            IReadOnlyList<RelationshipNpcAffinityRow> rows = raw.NpcAffinityOmitted
                ? Array.Empty<RelationshipNpcAffinityRow>()
                : raw.NpcAffinityRows;

            var supported = new Dictionary<string, float>(StringComparer.Ordinal);
            var unknowns = new List<string>();
            var duplicates = new List<string>();
            RelationshipDomainValidationStatus status = raw.NpcAffinityOmitted
                ? RelationshipDomainValidationStatus.CompatibleNormalizedTopLevel
                : RelationshipDomainValidationStatus.ValidSparse;

            for (int i = 0; i < rows.Count; i++)
            {
                RelationshipNpcAffinityRow row = rows[i];
                string path = "Reputation[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                if (row == null || row.IsNullEntry)
                {
                    diagnostics.Add(
                        Error(
                            RelationshipDiagnosticCodes.NullEntry,
                            RelationshipDomain.NpcAffinity,
                            string.Empty,
                            "Null affinity row is malformed.",
                            path,
                            "npcId"));
                    status = Worse(status, RelationshipDomainValidationStatus.MalformedNullEntry);
                    continue;
                }

                if (string.IsNullOrEmpty(row.NpcId))
                {
                    diagnostics.Add(
                        Error(
                            RelationshipDiagnosticCodes.BlankId,
                            RelationshipDomain.NpcAffinity,
                            row.NpcId,
                            "Blank NPC identity is malformed.",
                            path,
                            "npcId"));
                    status = Worse(status, RelationshipDomainValidationStatus.MalformedBlankId);
                    continue;
                }

                RelationshipIdentityResolution resolution = identities.ResolveNpc(row.NpcId);
                if (!resolution.SupportsMutation)
                {
                    unknowns.Add(row.NpcId);
                    status = Worse(status, RelationshipDomainValidationStatus.PreservedUnknown);
                    continue;
                }

                if (supported.ContainsKey(resolution.CanonicalId))
                {
                    duplicates.Add(resolution.CanonicalId);
                    diagnostics.Add(
                        Error(
                            RelationshipDiagnosticCodes.DuplicateId,
                            RelationshipDomain.NpcAffinity,
                            resolution.CanonicalId,
                            "Duplicate supported NPC identity is malformed.",
                            path,
                            "npcId"));
                    status = Worse(status, RelationshipDomainValidationStatus.MalformedDuplicateId);
                    continue;
                }

                if (!IsFinite(row.Affinity))
                {
                    diagnostics.Add(
                        Error(
                            RelationshipDiagnosticCodes.NonFinite,
                            RelationshipDomain.NpcAffinity,
                            resolution.CanonicalId,
                            "Non-finite affinity is malformed.",
                            path,
                            "affinity"));
                    status = Worse(status, RelationshipDomainValidationStatus.MalformedNonFinite);
                    continue;
                }

                if (row.Affinity < policy.AffinityMinimum ||
                    row.Affinity > policy.AffinityMaximum)
                {
                    diagnostics.Add(
                        Error(
                            RelationshipDiagnosticCodes.OutOfRange,
                            RelationshipDomain.NpcAffinity,
                            resolution.CanonicalId,
                            "Finite affinity outside [-100,100] is malformed.",
                            path,
                            "affinity"));
                    status = Worse(status, RelationshipDomainValidationStatus.MalformedOutOfRange);
                    continue;
                }

                supported.Add(resolution.CanonicalId, row.Affinity);
            }

            if (IsMalformed(status))
            {
                supported.Clear();
            }
            else if (status == RelationshipDomainValidationStatus.ValidSparse &&
                     supported.Count > 0 &&
                     unknowns.Count == 0)
            {
                status = RelationshipDomainValidationStatus.Valid;
            }

            string fingerprint = FingerprintNpc(supported, unknowns, status);
            return Npc(
                status,
                fingerprint,
                supported,
                unknowns.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal),
                duplicates,
                rows.Count,
                diagnostics);
        }

        private static RelationshipFactionDomainSnapshot BuildFaction(
            RelationshipRawState raw,
            IRelationshipIdentityResolver identities,
            bool policyReady,
            RelationshipPolicySnapshot policy)
        {
            var diagnostics = new List<RelationshipDiagnostic>();
            if (!raw.HasCurrentSave)
            {
                return Faction(
                    RelationshipDomainValidationStatus.UnavailableNoCurrentSave,
                    "no-save",
                    new Dictionary<string, int>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    0,
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.NoCurrentSave,
                            RelationshipDomain.FactionReputation,
                            string.Empty,
                            "No current save.")
                    });
            }

            if (!policyReady)
            {
                RelationshipDomainValidationStatus pending =
                    policiesUnavailableStatus(identities, policy);
                return Faction(
                    pending,
                    "policy-unavailable",
                    new Dictionary<string, int>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    raw.FactionRows.Count,
                    new[]
                    {
                        Error(
                            pending == RelationshipDomainValidationStatus.UnsupportedDefinitionVersion
                                ? RelationshipDiagnosticCodes.UnsupportedVersion
                                : RelationshipDiagnosticCodes.Policy,
                            RelationshipDomain.FactionReputation,
                            string.Empty,
                            "Policy or identity catalog is not available.")
                    });
            }

            IReadOnlyList<RelationshipFactionRow> rows = raw.FactionOmitted
                ? Array.Empty<RelationshipFactionRow>()
                : raw.FactionRows;

            var supported = new Dictionary<string, int>(StringComparer.Ordinal);
            var unknowns = new List<string>();
            var duplicates = new List<string>();
            RelationshipDomainValidationStatus status = raw.FactionOmitted
                ? RelationshipDomainValidationStatus.CompatibleNormalizedTopLevel
                : RelationshipDomainValidationStatus.ValidSparse;

            for (int i = 0; i < rows.Count; i++)
            {
                RelationshipFactionRow row = rows[i];
                string path = "FactionReputations[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                if (row == null || row.IsNullEntry)
                {
                    diagnostics.Add(
                        Error(
                            RelationshipDiagnosticCodes.NullEntry,
                            RelationshipDomain.FactionReputation,
                            string.Empty,
                            "Null faction row is malformed.",
                            path,
                            "factionId"));
                    status = Worse(status, RelationshipDomainValidationStatus.MalformedNullEntry);
                    continue;
                }

                if (string.IsNullOrEmpty(row.FactionId))
                {
                    diagnostics.Add(
                        Error(
                            RelationshipDiagnosticCodes.BlankId,
                            RelationshipDomain.FactionReputation,
                            row.FactionId,
                            "Blank faction identity is malformed.",
                            path,
                            "factionId"));
                    status = Worse(status, RelationshipDomainValidationStatus.MalformedBlankId);
                    continue;
                }

                RelationshipIdentityResolution resolution = identities.ResolveFaction(row.FactionId);
                if (!resolution.SupportsMutation)
                {
                    unknowns.Add(row.FactionId);
                    status = Worse(status, RelationshipDomainValidationStatus.PreservedUnknown);
                    continue;
                }

                if (supported.ContainsKey(resolution.CanonicalId))
                {
                    duplicates.Add(resolution.CanonicalId);
                    diagnostics.Add(
                        Error(
                            RelationshipDiagnosticCodes.DuplicateId,
                            RelationshipDomain.FactionReputation,
                            resolution.CanonicalId,
                            "Duplicate supported faction identity is malformed.",
                            path,
                            "factionId"));
                    status = Worse(status, RelationshipDomainValidationStatus.MalformedDuplicateId);
                    continue;
                }

                supported.Add(resolution.CanonicalId, row.Reputation);
            }

            if (IsMalformed(status))
            {
                supported.Clear();
            }
            else if (status == RelationshipDomainValidationStatus.ValidSparse &&
                     supported.Count > 0 &&
                     unknowns.Count == 0)
            {
                status = RelationshipDomainValidationStatus.Valid;
            }

            string fingerprint = FingerprintFaction(supported, unknowns, status);
            return Faction(
                status,
                fingerprint,
                supported,
                unknowns.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal),
                duplicates,
                rows.Count,
                diagnostics);
        }

        private static RelationshipPersonaDomainSnapshot BuildPersona(
            RelationshipRawState raw,
            bool policyReady,
            RelationshipPolicySnapshot policy)
        {
            if (!raw.HasCurrentSave)
            {
                return new RelationshipPersonaDomainSnapshot(
                    RelationshipDomainValidationStatus.UnavailableNoCurrentSave,
                    "no-save",
                    RelationshipPersonaValues.Missing(),
                    new PersonaClassificationResult(
                        PersonaClassificationStatus.Unavailable,
                        null,
                        Array.Empty<PersonaTrait>(),
                        0,
                        policy?.PersonaUnavailableContentReference ?? string.Empty),
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.NoCurrentSave,
                            RelationshipDomain.PersonaTrait,
                            string.Empty,
                            "No current save.")
                    });
            }

            if (!policyReady)
            {
                RelationshipDomainValidationStatus pending =
                    policy != null &&
                    policy.SchemaVersion != RelationshipTechnicalLimits.CurrentSchemaVersion
                        ? RelationshipDomainValidationStatus.UnsupportedDefinitionVersion
                        : RelationshipDomainValidationStatus.MalformedPolicyUnavailable;
                return new RelationshipPersonaDomainSnapshot(
                    pending,
                    "policy-unavailable",
                    RelationshipPersonaValues.Missing(),
                    new PersonaClassificationResult(
                        PersonaClassificationStatus.Malformed,
                        null,
                        Array.Empty<PersonaTrait>(),
                        0,
                        policy?.PersonaMalformedContentReference ?? string.Empty),
                    new[]
                    {
                        Error(
                            RelationshipDiagnosticCodes.Policy,
                            RelationshipDomain.PersonaTrait,
                            string.Empty,
                            "Persona policy is not available.")
                    });
            }

            if (raw.PersonaOmitted || !raw.Persona.IsPresent)
            {
                return new RelationshipPersonaDomainSnapshot(
                    RelationshipDomainValidationStatus.CompatibleNormalizedTopLevel,
                    RelationshipHash.Compute("persona", "omitted"),
                    RelationshipPersonaValues.Missing(),
                    new PersonaClassificationResult(
                        PersonaClassificationStatus.Unavailable,
                        null,
                        Array.Empty<PersonaTrait>(),
                        0,
                        policy.PersonaUnavailableContentReference),
                    Array.Empty<RelationshipDiagnostic>());
            }

            PersonaClassificationResult classification = ClassifyPersonaValues(
                raw.Persona,
                policy);
            string fingerprint = RelationshipHash.Compute(
                "persona",
                raw.Persona.Warlord.ToString(CultureInfo.InvariantCulture),
                raw.Persona.Diplomat.ToString(CultureInfo.InvariantCulture),
                raw.Persona.Sage.ToString(CultureInfo.InvariantCulture),
                raw.Persona.Rogue.ToString(CultureInfo.InvariantCulture));
            return new RelationshipPersonaDomainSnapshot(
                RelationshipDomainValidationStatus.Valid,
                fingerprint,
                raw.Persona,
                classification,
                Array.Empty<RelationshipDiagnostic>());
        }

        internal static PersonaClassificationResult ClassifyPersonaValues(
            RelationshipPersonaValues values,
            RelationshipPolicySnapshot policy)
        {
            if (values == null || !values.IsPresent)
            {
                return new PersonaClassificationResult(
                    PersonaClassificationStatus.Unavailable,
                    null,
                    Array.Empty<PersonaTrait>(),
                    0,
                    policy?.PersonaUnavailableContentReference ?? string.Empty);
            }

            PersonaTrait[] traits =
            {
                PersonaTrait.Warlord,
                PersonaTrait.Diplomat,
                PersonaTrait.Sage,
                PersonaTrait.Rogue
            };
            int max = int.MinValue;
            foreach (PersonaTrait trait in traits)
            {
                int value = values.Get(trait);
                if (value > max)
                {
                    max = value;
                }
            }

            if (values.Warlord == 0 &&
                values.Diplomat == 0 &&
                values.Sage == 0 &&
                values.Rogue == 0)
            {
                return new PersonaClassificationResult(
                    PersonaClassificationStatus.AllZero,
                    null,
                    Array.Empty<PersonaTrait>(),
                    0,
                    policy.PersonaAllZeroContentReference);
            }

            PersonaTrait[] tied = traits.Where(trait => values.Get(trait) == max).ToArray();
            if (tied.Length == 1)
            {
                return new PersonaClassificationResult(
                    PersonaClassificationStatus.UniqueDominant,
                    tied[0],
                    Array.Empty<PersonaTrait>(),
                    max,
                    policy.PersonaUniqueContentReference);
            }

            return new PersonaClassificationResult(
                PersonaClassificationStatus.Tie,
                null,
                tied,
                max,
                policy.PersonaTieContentReference);
        }

        private static RelationshipDomainValidationStatus policiesUnavailableStatus(
            IRelationshipIdentityResolver identities,
            RelationshipPolicySnapshot policy)
        {
            if (identities != null &&
                identities.Availability == RelationshipCatalogAvailability.Pending)
            {
                return RelationshipDomainValidationStatus.MalformedPolicyUnavailable;
            }

            if (policy != null &&
                policy.SchemaVersion != RelationshipTechnicalLimits.CurrentSchemaVersion)
            {
                return RelationshipDomainValidationStatus.UnsupportedDefinitionVersion;
            }

            return RelationshipDomainValidationStatus.MalformedPolicyUnavailable;
        }

        private static RelationshipNpcAffinityDomainSnapshot Npc(
            RelationshipDomainValidationStatus status,
            string fingerprint,
            Dictionary<string, float> supported,
            IEnumerable<string> unknowns,
            IEnumerable<string> duplicates,
            int sourceCount,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            return new RelationshipNpcAffinityDomainSnapshot(
                status,
                fingerprint,
                FreezeFloats(supported),
                unknowns,
                duplicates,
                sourceCount,
                diagnostics);
        }

        private static RelationshipFactionDomainSnapshot Faction(
            RelationshipDomainValidationStatus status,
            string fingerprint,
            Dictionary<string, int> supported,
            IEnumerable<string> unknowns,
            IEnumerable<string> duplicates,
            int sourceCount,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            return new RelationshipFactionDomainSnapshot(
                status,
                fingerprint,
                FreezeInts(supported),
                unknowns,
                duplicates,
                sourceCount,
                diagnostics);
        }

        private static IReadOnlyDictionary<string, float> FreezeFloats(
            Dictionary<string, float> source)
        {
            return RelationshipCollections.FreezeMap(
                new Dictionary<string, float>(source, StringComparer.Ordinal));
        }

        private static IReadOnlyDictionary<string, int> FreezeInts(
            Dictionary<string, int> source)
        {
            return RelationshipCollections.FreezeMap(
                new Dictionary<string, int>(source, StringComparer.Ordinal));
        }

        private static string FingerprintNpc(
            Dictionary<string, float> supported,
            IEnumerable<string> unknowns,
            RelationshipDomainValidationStatus status)
        {
            var parts = new List<string>
            {
                "npc",
                ((int)status).ToString(CultureInfo.InvariantCulture)
            };
            foreach (KeyValuePair<string, float> pair in supported.OrderBy(
                         item => item.Key,
                         StringComparer.Ordinal))
            {
                parts.Add(pair.Key);
                parts.Add(pair.Value.ToString("R", CultureInfo.InvariantCulture));
            }

            foreach (string unknown in unknowns.Distinct(StringComparer.Ordinal)
                         .OrderBy(id => id, StringComparer.Ordinal))
            {
                parts.Add("u:" + unknown);
            }

            return RelationshipHash.Compute(parts.ToArray());
        }

        private static string FingerprintFaction(
            Dictionary<string, int> supported,
            IEnumerable<string> unknowns,
            RelationshipDomainValidationStatus status)
        {
            var parts = new List<string>
            {
                "faction",
                ((int)status).ToString(CultureInfo.InvariantCulture)
            };
            foreach (KeyValuePair<string, int> pair in supported.OrderBy(
                         item => item.Key,
                         StringComparer.Ordinal))
            {
                parts.Add(pair.Key);
                parts.Add(pair.Value.ToString(CultureInfo.InvariantCulture));
            }

            foreach (string unknown in unknowns.Distinct(StringComparer.Ordinal)
                         .OrderBy(id => id, StringComparer.Ordinal))
            {
                parts.Add("u:" + unknown);
            }

            return RelationshipHash.Compute(parts.ToArray());
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsMalformed(RelationshipDomainValidationStatus status)
        {
            return status == RelationshipDomainValidationStatus.MalformedNullEntry ||
                   status == RelationshipDomainValidationStatus.MalformedBlankId ||
                   status == RelationshipDomainValidationStatus.MalformedDuplicateId ||
                   status == RelationshipDomainValidationStatus.MalformedNonFinite ||
                   status == RelationshipDomainValidationStatus.MalformedOutOfRange;
        }

        private static RelationshipDomainValidationStatus Worse(
            RelationshipDomainValidationStatus current,
            RelationshipDomainValidationStatus candidate)
        {
            return Rank(candidate) > Rank(current) ? candidate : current;
        }

        private static int Rank(RelationshipDomainValidationStatus status)
        {
            switch (status)
            {
                case RelationshipDomainValidationStatus.UnavailableNoCurrentSave:
                    return 100;
                case RelationshipDomainValidationStatus.MalformedPolicyUnavailable:
                    return 90;
                case RelationshipDomainValidationStatus.UnsupportedDefinitionVersion:
                    return 85;
                case RelationshipDomainValidationStatus.MalformedNullEntry:
                    return 80;
                case RelationshipDomainValidationStatus.MalformedBlankId:
                    return 79;
                case RelationshipDomainValidationStatus.MalformedDuplicateId:
                    return 78;
                case RelationshipDomainValidationStatus.MalformedNonFinite:
                    return 77;
                case RelationshipDomainValidationStatus.MalformedOutOfRange:
                    return 76;
                case RelationshipDomainValidationStatus.PreservedUnknown:
                    return 20;
                case RelationshipDomainValidationStatus.CompatibleNormalizedTopLevel:
                    return 15;
                case RelationshipDomainValidationStatus.ValidSparse:
                    return 10;
                default:
                    return 0;
            }
        }

        private static RelationshipDiagnostic Error(
            string code,
            RelationshipDomain domain,
            string targetId,
            string action,
            string recordPath = "",
            string field = "")
        {
            return new RelationshipDiagnostic(
                RelationshipDiagnosticSeverity.Error,
                code,
                domain,
                recordPath,
                targetId ?? string.Empty,
                field,
                string.Empty,
                action,
                true);
        }
    }
}
