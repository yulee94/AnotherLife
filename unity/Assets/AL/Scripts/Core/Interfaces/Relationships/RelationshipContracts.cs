using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Core.Interfaces;

namespace AL.Core.Interfaces.Relationships
{
    public enum RelationshipDomain
    {
        NpcAffinity = 0,
        FactionReputation = 1,
        PersonaTrait = 2
    }

    public enum RelationshipCatalogAvailability
    {
        Available = 0,
        Pending = 1,
        Unavailable = 2
    }

    public enum RelationshipIdentityStatus
    {
        Found = 0,
        AliasResolved = 1,
        UnknownId = 2,
        CatalogPending = 3,
        CatalogUnavailable = 4,
        InvalidRecord = 5,
        UnsupportedVersion = 6
    }

    public enum RelationshipDomainValidationStatus
    {
        Valid = 0,
        ValidSparse = 1,
        CompatibleNormalizedTopLevel = 2,
        PreservedUnknown = 3,
        MalformedNullEntry = 4,
        MalformedBlankId = 5,
        MalformedDuplicateId = 6,
        MalformedNonFinite = 7,
        MalformedOutOfRange = 8,
        MalformedPolicyUnavailable = 9,
        UnavailableNoCurrentSave = 10,
        UnavailableReadOnlyProfile = 11,
        UnsupportedDefinitionVersion = 12
    }

    public enum RelationshipQueryStatus
    {
        Available = 0,
        AvailableSparseZero = 1,
        AliasResolved = 2,
        UnavailableNoSave = 3,
        UnavailableReadOnly = 4,
        UnavailableUnknownId = 5,
        UnavailableMalformedDomain = 6,
        UnavailablePolicy = 7,
        UnsupportedVersion = 8
    }

    public enum PersonaClassificationStatus
    {
        UniqueDominant = 0,
        Tie = 1,
        AllZero = 2,
        Unavailable = 3,
        Malformed = 4
    }

    public enum RelationshipPreparationStatus
    {
        Prepared = 0,
        PreparedClamped = 1,
        NoChange = 2,
        RejectedNoCurrentSave = 3,
        RejectedReadOnlyProfile = 4,
        RejectedUnknownId = 5,
        RejectedInvalidTrait = 6,
        RejectedMalformedDomain = 7,
        RejectedInvalidDelta = 8,
        RejectedOverflow = 9,
        RejectedPolicyUnavailable = 10,
        RejectedCorrelationRequired = 11,
        RejectedStaleSnapshot = 12,
        UnsupportedVersion = 13
    }

    public enum RelationshipRowOperation
    {
        None = 0,
        Create = 1,
        Update = 2
    }

    public enum RelationshipApplyStatus
    {
        Applied = 0,
        NoChange = 1,
        RejectedStalePlan = 2,
        RejectedTargetInvalid = 3,
        RejectedTargetReadOnly = 4,
        RejectedCorrelationConflict = 5,
        RejectedAlreadyApplied = 6,
        RejectedApplyFailure = 7
    }

    public enum RelationshipDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum RelationshipIdentityCatalogValidationStatus
    {
        Valid = 0,
        InvalidId = 1,
        DuplicateId = 2,
        AliasCollision = 3,
        AliasShadow = 4,
        AliasCycle = 5,
        CatalogPending = 6,
        CatalogUnavailable = 7,
        UnsupportedVersion = 8
    }

    public enum RelationshipPolicyValidationStatus
    {
        Valid = 0,
        InvalidRange = 1,
        InvalidBandOverlapOrGap = 2,
        InvalidPersonaTraits = 3,
        CatalogPending = 4,
        CatalogUnavailable = 5,
        UnsupportedVersion = 6
    }

    public static class RelationshipDiagnosticCodes
    {
        public const string NoCurrentSave = "AL-REL-NO-CURRENT-SAVE";
        public const string ProfileReadOnly = "AL-REL-PROFILE-READ-ONLY";
        public const string Policy = "AL-REL-POLICY";
        public const string UnknownId = "AL-REL-UNKNOWN-ID";
        public const string BlankId = "AL-REL-BLANK-ID";
        public const string NullEntry = "AL-REL-NULL-ENTRY";
        public const string DuplicateId = "AL-REL-DUPLICATE-ID";
        public const string NonFinite = "AL-REL-NONFINITE";
        public const string OutOfRange = "AL-REL-OUT-OF-RANGE";
        public const string Overflow = "AL-REL-OVERFLOW";
        public const string StalePlan = "AL-REL-STALE-PLAN";
        public const string Correlation = "AL-REL-CORRELATION";
        public const string Apply = "AL-REL-APPLY";
        public const string Persistence = "AL-REL-PERSISTENCE";
        public const string EventHandler = "AL-REL-EVENT-HANDLER";
        public const string LegacyClassification = "AL-REL-LEGACY-CLASSIFICATION";
        public const string CatalogPending = "AL-REL-CATALOG-PENDING";
        public const string CatalogUnavailable = "AL-REL-CATALOG-UNAVAILABLE";
        public const string UnsupportedVersion = "AL-REL-UNSUPPORTED-VERSION";
        public const string Alias = "AL-REL-ALIAS";
    }

    public static class RelationshipTechnicalLimits
    {
        public const int CurrentSchemaVersion = 1;
        public const int MaximumDiagnostics = 256;
        public const int MaximumRecords = 256;
        public const int MaximumAliasesPerRecord = 32;
        public const int MaximumBands = 16;
        public const float AffinityMinimum = -100f;
        public const float AffinityMaximum = 100f;
        public const string FixtureCatalogRevision = "al_relationship_authority_content_catalog.fixture";
        public const string FixturePolicyRevision = "al_relationship_legacy_policy_v1";
        public const string FixtureContentVersion = "0.1.0";
        public const string FixtureSourceRevision = "pr-347-18c93a50-fixture-only";
    }

    public sealed class RelationshipDiagnostic
    {
        public RelationshipDiagnostic(
            RelationshipDiagnosticSeverity severity,
            string code,
            RelationshipDomain? domain,
            string recordPath,
            string targetId,
            string field,
            string sourceRevision,
            string action,
            bool mutationDisabled)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Domain = domain;
            RecordPath = recordPath ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Field = field ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            Action = action ?? string.Empty;
            MutationDisabled = mutationDisabled;
        }

        public RelationshipDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public RelationshipDomain? Domain { get; }
        public string RecordPath { get; }
        public string TargetId { get; }
        public string Field { get; }
        public string SourceRevision { get; }
        public string Action { get; }
        public bool MutationDisabled { get; }
    }

    public sealed class RelationshipIdentityRecord
    {
        public RelationshipIdentityRecord(
            string canonicalId,
            IEnumerable<string> legacyAliases,
            bool relationshipEnabled,
            string contentReference)
        {
            CanonicalId = canonicalId ?? string.Empty;
            LegacyAliases = RelationshipCollections.Freeze(
                legacyAliases,
                RelationshipTechnicalLimits.MaximumAliasesPerRecord);
            RelationshipEnabled = relationshipEnabled;
            ContentReference = contentReference ?? string.Empty;
        }

        public string CanonicalId { get; }
        public IReadOnlyList<string> LegacyAliases { get; }
        public bool RelationshipEnabled { get; }
        public string ContentReference { get; }
    }

    public sealed class RelationshipIdentityResolution
    {
        public RelationshipIdentityResolution(
            RelationshipIdentityStatus status,
            string requestedId,
            string canonicalId,
            bool relationshipEnabled,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            Status = status;
            RequestedId = requestedId ?? string.Empty;
            CanonicalId = canonicalId ?? string.Empty;
            RelationshipEnabled = relationshipEnabled;
            Diagnostics = RelationshipCollections.Freeze(
                diagnostics,
                RelationshipTechnicalLimits.MaximumDiagnostics);
        }

        public RelationshipIdentityStatus Status { get; }
        public string RequestedId { get; }
        public string CanonicalId { get; }
        public bool RelationshipEnabled { get; }
        public IReadOnlyList<RelationshipDiagnostic> Diagnostics { get; }

        public bool SupportsMutation =>
            (Status == RelationshipIdentityStatus.Found ||
             Status == RelationshipIdentityStatus.AliasResolved) &&
            RelationshipEnabled;
    }

    public sealed class RelationshipIdentityCatalogValidationResult
    {
        public RelationshipIdentityCatalogValidationResult(
            RelationshipIdentityCatalogValidationStatus status,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            Status = status;
            Diagnostics = RelationshipCollections.Freeze(
                diagnostics,
                RelationshipTechnicalLimits.MaximumDiagnostics);
        }

        public RelationshipIdentityCatalogValidationStatus Status { get; }
        public IReadOnlyList<RelationshipDiagnostic> Diagnostics { get; }
        public bool IsValid => Status == RelationshipIdentityCatalogValidationStatus.Valid;
    }

    public sealed class RelationshipClassificationBand
    {
        public RelationshipClassificationBand(
            string classificationId,
            double minimum,
            double maximum,
            bool minimumInclusive,
            bool maximumInclusive,
            string contentReference)
        {
            ClassificationId = classificationId ?? string.Empty;
            Minimum = minimum;
            Maximum = maximum;
            MinimumInclusive = minimumInclusive;
            MaximumInclusive = maximumInclusive;
            ContentReference = contentReference ?? string.Empty;
        }

        public string ClassificationId { get; }
        public double Minimum { get; }
        public double Maximum { get; }
        public bool MinimumInclusive { get; }
        public bool MaximumInclusive { get; }
        public string ContentReference { get; }

        public bool Contains(double value)
        {
            bool aboveMinimum = MinimumInclusive ? value >= Minimum : value > Minimum;
            bool belowMaximum = MaximumInclusive ? value <= Maximum : value < Maximum;
            return aboveMinimum && belowMaximum;
        }
    }

    public sealed class RelationshipPolicySnapshot
    {
        public RelationshipPolicySnapshot(
            int schemaVersion,
            string contentVersion,
            string sourceRevision,
            string identityCatalogRevision,
            string policyRevision,
            float affinityMinimum,
            float affinityMaximum,
            IEnumerable<RelationshipClassificationBand> affinityBands,
            IEnumerable<RelationshipClassificationBand> factionBands,
            IEnumerable<PersonaTrait> supportedPersonaTraits,
            string personaUniqueContentReference,
            string personaTieContentReference,
            string personaAllZeroContentReference,
            string personaUnavailableContentReference,
            string personaMalformedContentReference)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            IdentityCatalogRevision = identityCatalogRevision ?? string.Empty;
            PolicyRevision = policyRevision ?? string.Empty;
            AffinityMinimum = affinityMinimum;
            AffinityMaximum = affinityMaximum;
            AffinityBands = RelationshipCollections.Freeze(
                affinityBands,
                RelationshipTechnicalLimits.MaximumBands);
            FactionBands = RelationshipCollections.Freeze(
                factionBands,
                RelationshipTechnicalLimits.MaximumBands);
            SupportedPersonaTraits = RelationshipCollections.Freeze(
                supportedPersonaTraits,
                8);
            PersonaUniqueContentReference = personaUniqueContentReference ?? string.Empty;
            PersonaTieContentReference = personaTieContentReference ?? string.Empty;
            PersonaAllZeroContentReference = personaAllZeroContentReference ?? string.Empty;
            PersonaUnavailableContentReference = personaUnavailableContentReference ?? string.Empty;
            PersonaMalformedContentReference = personaMalformedContentReference ?? string.Empty;
        }

        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public string IdentityCatalogRevision { get; }
        public string PolicyRevision { get; }
        public float AffinityMinimum { get; }
        public float AffinityMaximum { get; }
        public IReadOnlyList<RelationshipClassificationBand> AffinityBands { get; }
        public IReadOnlyList<RelationshipClassificationBand> FactionBands { get; }
        public IReadOnlyList<PersonaTrait> SupportedPersonaTraits { get; }
        public string PersonaUniqueContentReference { get; }
        public string PersonaTieContentReference { get; }
        public string PersonaAllZeroContentReference { get; }
        public string PersonaUnavailableContentReference { get; }
        public string PersonaMalformedContentReference { get; }
    }

    public sealed class RelationshipPolicyValidationResult
    {
        public RelationshipPolicyValidationResult(
            RelationshipPolicyValidationStatus status,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            Status = status;
            Diagnostics = RelationshipCollections.Freeze(
                diagnostics,
                RelationshipTechnicalLimits.MaximumDiagnostics);
        }

        public RelationshipPolicyValidationStatus Status { get; }
        public IReadOnlyList<RelationshipDiagnostic> Diagnostics { get; }
        public bool IsValid => Status == RelationshipPolicyValidationStatus.Valid;
    }

    public sealed class RelationshipNpcAffinityRow
    {
        public RelationshipNpcAffinityRow(bool isNullEntry, string npcId, float affinity)
        {
            IsNullEntry = isNullEntry;
            NpcId = npcId;
            Affinity = affinity;
        }

        public bool IsNullEntry { get; }
        public string NpcId { get; }
        public float Affinity { get; }

        public static RelationshipNpcAffinityRow NullEntry()
        {
            return new RelationshipNpcAffinityRow(true, null, 0f);
        }

        public static RelationshipNpcAffinityRow Value(string npcId, float affinity)
        {
            return new RelationshipNpcAffinityRow(false, npcId, affinity);
        }
    }

    public sealed class RelationshipFactionRow
    {
        public RelationshipFactionRow(bool isNullEntry, string factionId, int reputation)
        {
            IsNullEntry = isNullEntry;
            FactionId = factionId;
            Reputation = reputation;
        }

        public bool IsNullEntry { get; }
        public string FactionId { get; }
        public int Reputation { get; }

        public static RelationshipFactionRow NullEntry()
        {
            return new RelationshipFactionRow(true, null, 0);
        }

        public static RelationshipFactionRow Value(string factionId, int reputation)
        {
            return new RelationshipFactionRow(false, factionId, reputation);
        }
    }

    public sealed class RelationshipPersonaValues
    {
        public RelationshipPersonaValues(
            bool isPresent,
            int warlord,
            int diplomat,
            int sage,
            int rogue)
        {
            IsPresent = isPresent;
            Warlord = warlord;
            Diplomat = diplomat;
            Sage = sage;
            Rogue = rogue;
        }

        public bool IsPresent { get; }
        public int Warlord { get; }
        public int Diplomat { get; }
        public int Sage { get; }
        public int Rogue { get; }

        public static RelationshipPersonaValues Missing()
        {
            return new RelationshipPersonaValues(false, 0, 0, 0, 0);
        }

        public static RelationshipPersonaValues From(
            int warlord,
            int diplomat,
            int sage,
            int rogue)
        {
            return new RelationshipPersonaValues(true, warlord, diplomat, sage, rogue);
        }

        public int Get(PersonaTrait trait)
        {
            switch (trait)
            {
                case PersonaTrait.Warlord:
                    return Warlord;
                case PersonaTrait.Diplomat:
                    return Diplomat;
                case PersonaTrait.Sage:
                    return Sage;
                case PersonaTrait.Rogue:
                    return Rogue;
                default:
                    throw new ArgumentOutOfRangeException(nameof(trait));
            }
        }

        public RelationshipPersonaValues With(PersonaTrait trait, int value)
        {
            return trait switch
            {
                PersonaTrait.Warlord => From(value, Diplomat, Sage, Rogue),
                PersonaTrait.Diplomat => From(Warlord, value, Sage, Rogue),
                PersonaTrait.Sage => From(Warlord, Diplomat, value, Rogue),
                PersonaTrait.Rogue => From(Warlord, Diplomat, Sage, value),
                _ => this
            };
        }
    }

    public sealed class RelationshipRawState
    {
        public RelationshipRawState(
            bool hasCurrentSave,
            bool profileWritable,
            bool npcAffinityOmitted,
            bool factionOmitted,
            bool personaOmitted,
            IEnumerable<RelationshipNpcAffinityRow> npcAffinityRows,
            IEnumerable<RelationshipFactionRow> factionRows,
            RelationshipPersonaValues persona)
        {
            HasCurrentSave = hasCurrentSave;
            ProfileWritable = profileWritable;
            NpcAffinityOmitted = npcAffinityOmitted;
            FactionOmitted = factionOmitted;
            PersonaOmitted = personaOmitted;
            NpcAffinityRows = RelationshipCollections.Freeze(
                npcAffinityRows,
                RelationshipTechnicalLimits.MaximumRecords);
            FactionRows = RelationshipCollections.Freeze(
                factionRows,
                RelationshipTechnicalLimits.MaximumRecords);
            Persona = persona ?? RelationshipPersonaValues.Missing();
        }

        public bool HasCurrentSave { get; }
        public bool ProfileWritable { get; }
        public bool NpcAffinityOmitted { get; }
        public bool FactionOmitted { get; }
        public bool PersonaOmitted { get; }
        public IReadOnlyList<RelationshipNpcAffinityRow> NpcAffinityRows { get; }
        public IReadOnlyList<RelationshipFactionRow> FactionRows { get; }
        public RelationshipPersonaValues Persona { get; }

        public static RelationshipRawState NoSave()
        {
            return new RelationshipRawState(
                false,
                false,
                true,
                true,
                true,
                Array.Empty<RelationshipNpcAffinityRow>(),
                Array.Empty<RelationshipFactionRow>(),
                RelationshipPersonaValues.Missing());
        }

        public static RelationshipRawState EmptyWritable()
        {
            return new RelationshipRawState(
                true,
                true,
                false,
                false,
                false,
                Array.Empty<RelationshipNpcAffinityRow>(),
                Array.Empty<RelationshipFactionRow>(),
                RelationshipPersonaValues.From(0, 0, 0, 0));
        }

        public RelationshipRawState WithNpcRows(
            IEnumerable<RelationshipNpcAffinityRow> rows,
            bool omitted = false)
        {
            return new RelationshipRawState(
                HasCurrentSave,
                ProfileWritable,
                omitted,
                FactionOmitted,
                PersonaOmitted,
                rows,
                FactionRows,
                Persona);
        }

        public RelationshipRawState WithFactionRows(
            IEnumerable<RelationshipFactionRow> rows,
            bool omitted = false)
        {
            return new RelationshipRawState(
                HasCurrentSave,
                ProfileWritable,
                NpcAffinityOmitted,
                omitted,
                PersonaOmitted,
                NpcAffinityRows,
                rows,
                Persona);
        }

        public RelationshipRawState WithPersona(
            RelationshipPersonaValues persona,
            bool omitted = false)
        {
            return new RelationshipRawState(
                HasCurrentSave,
                ProfileWritable,
                NpcAffinityOmitted,
                FactionOmitted,
                omitted,
                NpcAffinityRows,
                FactionRows,
                persona);
        }

        public RelationshipRawState WithWritable(bool writable)
        {
            return new RelationshipRawState(
                HasCurrentSave,
                writable,
                NpcAffinityOmitted,
                FactionOmitted,
                PersonaOmitted,
                NpcAffinityRows,
                FactionRows,
                Persona);
        }
    }

    public sealed class RelationshipNpcAffinityDomainSnapshot
    {
        public RelationshipNpcAffinityDomainSnapshot(
            RelationshipDomainValidationStatus status,
            string fingerprint,
            IReadOnlyDictionary<string, float> supportedValuesByCanonicalNpcId,
            IEnumerable<string> preservedUnknownIds,
            IEnumerable<string> duplicateIds,
            int sourceRecordCount,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            Status = status;
            Fingerprint = fingerprint ?? string.Empty;
            SupportedValuesByCanonicalNpcId = supportedValuesByCanonicalNpcId ??
                new ReadOnlyDictionary<string, float>(new Dictionary<string, float>());
            PreservedUnknownIds = RelationshipCollections.Freeze(
                preservedUnknownIds,
                RelationshipTechnicalLimits.MaximumRecords);
            DuplicateIds = RelationshipCollections.Freeze(
                duplicateIds,
                RelationshipTechnicalLimits.MaximumRecords);
            SourceRecordCount = sourceRecordCount;
            Diagnostics = RelationshipCollections.Freeze(
                diagnostics,
                RelationshipTechnicalLimits.MaximumDiagnostics);
        }

        public RelationshipDomainValidationStatus Status { get; }
        public string Fingerprint { get; }
        public IReadOnlyDictionary<string, float> SupportedValuesByCanonicalNpcId { get; }
        public IReadOnlyList<string> PreservedUnknownIds { get; }
        public IReadOnlyList<string> DuplicateIds { get; }
        public int SourceRecordCount { get; }
        public IReadOnlyList<RelationshipDiagnostic> Diagnostics { get; }

        public bool IsMutationReady =>
            Status == RelationshipDomainValidationStatus.Valid ||
            Status == RelationshipDomainValidationStatus.ValidSparse ||
            Status == RelationshipDomainValidationStatus.CompatibleNormalizedTopLevel ||
            Status == RelationshipDomainValidationStatus.PreservedUnknown;
    }

    public sealed class RelationshipFactionDomainSnapshot
    {
        public RelationshipFactionDomainSnapshot(
            RelationshipDomainValidationStatus status,
            string fingerprint,
            IReadOnlyDictionary<string, int> supportedValuesByCanonicalFactionId,
            IEnumerable<string> preservedUnknownIds,
            IEnumerable<string> duplicateIds,
            int sourceRecordCount,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            Status = status;
            Fingerprint = fingerprint ?? string.Empty;
            SupportedValuesByCanonicalFactionId = supportedValuesByCanonicalFactionId ??
                new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());
            PreservedUnknownIds = RelationshipCollections.Freeze(
                preservedUnknownIds,
                RelationshipTechnicalLimits.MaximumRecords);
            DuplicateIds = RelationshipCollections.Freeze(
                duplicateIds,
                RelationshipTechnicalLimits.MaximumRecords);
            SourceRecordCount = sourceRecordCount;
            Diagnostics = RelationshipCollections.Freeze(
                diagnostics,
                RelationshipTechnicalLimits.MaximumDiagnostics);
        }

        public RelationshipDomainValidationStatus Status { get; }
        public string Fingerprint { get; }
        public IReadOnlyDictionary<string, int> SupportedValuesByCanonicalFactionId { get; }
        public IReadOnlyList<string> PreservedUnknownIds { get; }
        public IReadOnlyList<string> DuplicateIds { get; }
        public int SourceRecordCount { get; }
        public IReadOnlyList<RelationshipDiagnostic> Diagnostics { get; }

        public bool IsMutationReady =>
            Status == RelationshipDomainValidationStatus.Valid ||
            Status == RelationshipDomainValidationStatus.ValidSparse ||
            Status == RelationshipDomainValidationStatus.CompatibleNormalizedTopLevel ||
            Status == RelationshipDomainValidationStatus.PreservedUnknown;
    }

    public sealed class PersonaClassificationResult
    {
        public PersonaClassificationResult(
            PersonaClassificationStatus status,
            PersonaTrait? dominantTrait,
            IEnumerable<PersonaTrait> tiedTraits,
            int maximumValue,
            string contentReference)
        {
            Status = status;
            DominantTrait = dominantTrait;
            TiedTraits = RelationshipCollections.Freeze(tiedTraits, 4);
            MaximumValue = maximumValue;
            ContentReference = contentReference ?? string.Empty;
        }

        public PersonaClassificationStatus Status { get; }
        public PersonaTrait? DominantTrait { get; }
        public IReadOnlyList<PersonaTrait> TiedTraits { get; }
        public int MaximumValue { get; }
        public string ContentReference { get; }
    }

    public sealed class RelationshipPersonaDomainSnapshot
    {
        public RelationshipPersonaDomainSnapshot(
            RelationshipDomainValidationStatus status,
            string fingerprint,
            RelationshipPersonaValues values,
            PersonaClassificationResult classification,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            Status = status;
            Fingerprint = fingerprint ?? string.Empty;
            Values = values ?? RelationshipPersonaValues.Missing();
            Classification = classification;
            Diagnostics = RelationshipCollections.Freeze(
                diagnostics,
                RelationshipTechnicalLimits.MaximumDiagnostics);
        }

        public RelationshipDomainValidationStatus Status { get; }
        public string Fingerprint { get; }
        public RelationshipPersonaValues Values { get; }
        public PersonaClassificationResult Classification { get; }
        public IReadOnlyList<RelationshipDiagnostic> Diagnostics { get; }

        public bool IsMutationReady =>
            Status == RelationshipDomainValidationStatus.Valid ||
            Status == RelationshipDomainValidationStatus.ValidSparse ||
            Status == RelationshipDomainValidationStatus.CompatibleNormalizedTopLevel;
    }

    public sealed class RelationshipSnapshot
    {
        public RelationshipSnapshot(
            string snapshotRevision,
            string policyRevision,
            bool profileWritable,
            RelationshipNpcAffinityDomainSnapshot npcAffinityDomain,
            RelationshipFactionDomainSnapshot factionDomain,
            RelationshipPersonaDomainSnapshot personaDomain,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            SnapshotRevision = snapshotRevision ?? string.Empty;
            PolicyRevision = policyRevision ?? string.Empty;
            ProfileWritable = profileWritable;
            NpcAffinityDomain = npcAffinityDomain;
            FactionDomain = factionDomain;
            PersonaDomain = personaDomain;
            Diagnostics = RelationshipCollections.Freeze(
                diagnostics,
                RelationshipTechnicalLimits.MaximumDiagnostics);
        }

        public string SnapshotRevision { get; }
        public string PolicyRevision { get; }
        public bool ProfileWritable { get; }
        public RelationshipNpcAffinityDomainSnapshot NpcAffinityDomain { get; }
        public RelationshipFactionDomainSnapshot FactionDomain { get; }
        public RelationshipPersonaDomainSnapshot PersonaDomain { get; }
        public IReadOnlyList<RelationshipDiagnostic> Diagnostics { get; }
    }

    public sealed class RelationshipQueryResult
    {
        public RelationshipQueryResult(
            RelationshipQueryStatus status,
            RelationshipDomain domain,
            string requestedId,
            string canonicalId,
            double value,
            string snapshotRevision,
            string policyRevision,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            Status = status;
            Domain = domain;
            RequestedId = requestedId ?? string.Empty;
            CanonicalId = canonicalId ?? string.Empty;
            Value = value;
            SnapshotRevision = snapshotRevision ?? string.Empty;
            PolicyRevision = policyRevision ?? string.Empty;
            Diagnostics = RelationshipCollections.Freeze(
                diagnostics,
                RelationshipTechnicalLimits.MaximumDiagnostics);
        }

        public RelationshipQueryStatus Status { get; }
        public RelationshipDomain Domain { get; }
        public string RequestedId { get; }
        public string CanonicalId { get; }
        public double Value { get; }
        public string SnapshotRevision { get; }
        public string PolicyRevision { get; }
        public IReadOnlyList<RelationshipDiagnostic> Diagnostics { get; }
    }

    public sealed class RelationshipClassificationQueryResult
    {
        public RelationshipClassificationQueryResult(
            RelationshipQueryStatus status,
            string classificationId,
            double value,
            double rangeMinimum,
            double rangeMaximum,
            bool minimumInclusive,
            bool maximumInclusive,
            string contentReference,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            Status = status;
            ClassificationId = classificationId ?? string.Empty;
            Value = value;
            RangeMinimum = rangeMinimum;
            RangeMaximum = rangeMaximum;
            MinimumInclusive = minimumInclusive;
            MaximumInclusive = maximumInclusive;
            ContentReference = contentReference ?? string.Empty;
            Diagnostics = RelationshipCollections.Freeze(
                diagnostics,
                RelationshipTechnicalLimits.MaximumDiagnostics);
        }

        public RelationshipQueryStatus Status { get; }
        public string ClassificationId { get; }
        public double Value { get; }
        public double RangeMinimum { get; }
        public double RangeMaximum { get; }
        public bool MinimumInclusive { get; }
        public bool MaximumInclusive { get; }
        public string ContentReference { get; }
        public IReadOnlyList<RelationshipDiagnostic> Diagnostics { get; }
    }

    public sealed class RelationshipMutationRequest
    {
        public RelationshipMutationRequest(
            RelationshipDomain domain,
            string targetId,
            PersonaTrait? personaTrait,
            double delta,
            string correlationId,
            string operationId,
            string sourceSystemId,
            DateTime occurredAtUtc,
            string expectedSnapshotRevision)
        {
            Domain = domain;
            TargetId = targetId ?? string.Empty;
            PersonaTrait = personaTrait;
            Delta = delta;
            CorrelationId = correlationId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            SourceSystemId = sourceSystemId ?? string.Empty;
            OccurredAtUtc = occurredAtUtc;
            ExpectedSnapshotRevision = expectedSnapshotRevision ?? string.Empty;
        }

        public RelationshipDomain Domain { get; }
        public string TargetId { get; }
        public PersonaTrait? PersonaTrait { get; }
        public double Delta { get; }
        public string CorrelationId { get; }
        public string OperationId { get; }
        public string SourceSystemId { get; }
        public DateTime OccurredAtUtc { get; }
        public string ExpectedSnapshotRevision { get; }

        public static RelationshipMutationRequest Affinity(
            string targetId,
            float delta,
            string correlationId,
            string operationId,
            string sourceSystemId,
            string expectedSnapshotRevision = "")
        {
            return new RelationshipMutationRequest(
                RelationshipDomain.NpcAffinity,
                targetId,
                null,
                delta,
                correlationId,
                operationId,
                sourceSystemId,
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                expectedSnapshotRevision);
        }

        public static RelationshipMutationRequest Faction(
            string targetId,
            int delta,
            string correlationId,
            string operationId,
            string sourceSystemId,
            string expectedSnapshotRevision = "")
        {
            return new RelationshipMutationRequest(
                RelationshipDomain.FactionReputation,
                targetId,
                null,
                delta,
                correlationId,
                operationId,
                sourceSystemId,
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                expectedSnapshotRevision);
        }

        public static RelationshipMutationRequest Persona(
            PersonaTrait trait,
            int delta,
            string correlationId,
            string operationId,
            string sourceSystemId,
            string expectedSnapshotRevision = "")
        {
            return new RelationshipMutationRequest(
                RelationshipDomain.PersonaTrait,
                string.Empty,
                trait,
                delta,
                correlationId,
                operationId,
                sourceSystemId,
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                expectedSnapshotRevision);
        }
    }

    public sealed class RelationshipPreparedPlan
    {
        public RelationshipPreparedPlan(
            string planId,
            RelationshipPreparationStatus status,
            RelationshipDomain domain,
            string canonicalTargetId,
            PersonaTrait? personaTrait,
            double requestedDelta,
            double previousValue,
            double newValue,
            double appliedDelta,
            bool wasClamped,
            RelationshipRowOperation rowOperation,
            string expectedSnapshotRevision,
            string policyRevision,
            string correlationId,
            string operationId,
            string sourceSystemId,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            PlanId = planId ?? string.Empty;
            Status = status;
            Domain = domain;
            CanonicalTargetId = canonicalTargetId ?? string.Empty;
            PersonaTrait = personaTrait;
            RequestedDelta = requestedDelta;
            PreviousValue = previousValue;
            NewValue = newValue;
            AppliedDelta = appliedDelta;
            WasClamped = wasClamped;
            RowOperation = rowOperation;
            ExpectedSnapshotRevision = expectedSnapshotRevision ?? string.Empty;
            PolicyRevision = policyRevision ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            SourceSystemId = sourceSystemId ?? string.Empty;
            Diagnostics = RelationshipCollections.Freeze(
                diagnostics,
                RelationshipTechnicalLimits.MaximumDiagnostics);
        }

        public string PlanId { get; }
        public RelationshipPreparationStatus Status { get; }
        public RelationshipDomain Domain { get; }
        public string CanonicalTargetId { get; }
        public PersonaTrait? PersonaTrait { get; }
        public double RequestedDelta { get; }
        public double PreviousValue { get; }
        public double NewValue { get; }
        public double AppliedDelta { get; }
        public bool WasClamped { get; }
        public RelationshipRowOperation RowOperation { get; }
        public string ExpectedSnapshotRevision { get; }
        public string PolicyRevision { get; }
        public string CorrelationId { get; }
        public string OperationId { get; }
        public string SourceSystemId { get; }
        public IReadOnlyList<RelationshipDiagnostic> Diagnostics { get; }

        public bool CanApply =>
            Status == RelationshipPreparationStatus.Prepared ||
            Status == RelationshipPreparationStatus.PreparedClamped ||
            Status == RelationshipPreparationStatus.NoChange;
    }

    public sealed class RelationshipPlanningResult
    {
        public RelationshipPlanningResult(
            RelationshipPreparationStatus status,
            RelationshipPreparedPlan plan,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            Diagnostics = RelationshipCollections.Freeze(
                diagnostics,
                RelationshipTechnicalLimits.MaximumDiagnostics);
        }

        public RelationshipPreparationStatus Status { get; }
        public RelationshipPreparedPlan Plan { get; }
        public IReadOnlyList<RelationshipDiagnostic> Diagnostics { get; }
    }

    public sealed class RelationshipApplyResult
    {
        public RelationshipApplyResult(
            RelationshipApplyStatus status,
            RelationshipPreparedPlan plan,
            RelationshipSnapshot snapshotAfter,
            IEnumerable<RelationshipDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            SnapshotAfter = snapshotAfter;
            Diagnostics = RelationshipCollections.Freeze(
                diagnostics,
                RelationshipTechnicalLimits.MaximumDiagnostics);
        }

        public RelationshipApplyStatus Status { get; }
        public RelationshipPreparedPlan Plan { get; }
        public RelationshipSnapshot SnapshotAfter { get; }
        public IReadOnlyList<RelationshipDiagnostic> Diagnostics { get; }
    }

    public sealed class RelationshipCommitEventDescription
    {
        public RelationshipCommitEventDescription(
            RelationshipDomain domain,
            string canonicalTargetId,
            double previousValue,
            double newValue,
            string operationId,
            string correlationId,
            string commitRevision)
        {
            Domain = domain;
            CanonicalTargetId = canonicalTargetId ?? string.Empty;
            PreviousValue = previousValue;
            NewValue = newValue;
            OperationId = operationId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            CommitRevision = commitRevision ?? string.Empty;
        }

        public RelationshipDomain Domain { get; }
        public string CanonicalTargetId { get; }
        public double PreviousValue { get; }
        public double NewValue { get; }
        public string OperationId { get; }
        public string CorrelationId { get; }
        public string CommitRevision { get; }
    }

    internal static class RelationshipCollections
    {
        public static IReadOnlyList<T> Freeze<T>(IEnumerable<T> source, int maximumCount)
        {
            if (source == null)
            {
                return Array.Empty<T>();
            }

            var copy = new List<T>(Math.Min(maximumCount + 1, 64));
            using (IEnumerator<T> enumerator = source.GetEnumerator())
            {
                while (copy.Count <= maximumCount && enumerator.MoveNext())
                {
                    copy.Add(enumerator.Current);
                }
            }

            return new ReadOnlyCollection<T>(copy);
        }

        public static IReadOnlyDictionary<TKey, TValue> FreezeMap<TKey, TValue>(
            IDictionary<TKey, TValue> source)
        {
            if (source == null)
            {
                return new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>());
            }

            return new ReadOnlyDictionary<TKey, TValue>(
                new Dictionary<TKey, TValue>(source));
        }
    }

    internal static class RelationshipHash
    {
        public static string Compute(params string[] values)
        {
            string canonical = string.Join(
                "\u001f",
                (values ?? Array.Empty<string>()).Select(value => value ?? string.Empty));
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes)
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }
}
