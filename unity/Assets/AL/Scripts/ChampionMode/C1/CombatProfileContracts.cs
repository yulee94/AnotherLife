using System;
using System.Collections.Generic;

namespace AL.ChampionMode.C1
{
    public sealed class ChampionCombatProfile
    {
        public ChampionCombatProfile(
            string id,
            string schemaVersion,
            string contentVersion,
            string catalogSetId,
            long maxHealthMicros,
            long maxManaMicros,
            long manaRegenPerSecondMicros,
            long basicAttackPowerMicros,
            string basicAttackBehaviorProfileId,
            string movementProfileId,
            string dodgeProfileId,
            string targetingProfileId,
            string sourceRevision,
            string rawSha256)
        {
            Id = id;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            CatalogSetId = catalogSetId;
            MaxHealthMicros = maxHealthMicros;
            MaxManaMicros = maxManaMicros;
            ManaRegenPerSecondMicros = manaRegenPerSecondMicros;
            BasicAttackPowerMicros = basicAttackPowerMicros;
            BasicAttackBehaviorProfileId = basicAttackBehaviorProfileId;
            MovementProfileId = movementProfileId;
            DodgeProfileId = dodgeProfileId;
            TargetingProfileId = targetingProfileId;
            SourceRevision = sourceRevision;
            RawSha256 = rawSha256;
        }

        public string Id { get; }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public string CatalogSetId { get; }
        public long MaxHealthMicros { get; }
        public long MaxManaMicros { get; }
        public long ManaRegenPerSecondMicros { get; }
        public long BasicAttackPowerMicros { get; }
        public string BasicAttackBehaviorProfileId { get; }
        public string MovementProfileId { get; }
        public string DodgeProfileId { get; }
        public string TargetingProfileId { get; }
        public string SourceRevision { get; }
        public string RawSha256 { get; }
    }

    public enum CombatSkillBehaviorKind
    {
        Unknown = 0,
        Damage = 1,
        Healing = 2,
        BreakDamage = 3,
        Utility = 4
    }

    public enum CombatTargetDisposition
    {
        Unknown = 0,
        Self = 1,
        Friendly = 2,
        Hostile = 3,
        Any = 4
    }

    public sealed class CombatSkillDefinition
    {
        public CombatSkillDefinition(
            string id,
            string schemaVersion,
            string contentVersion,
            string behaviorProfileId,
            string targetingProfileId,
            string resourcePolicyId,
            string cooldownPolicyId,
            long manaCostMicros,
            long castDurationMicros,
            long cooldownDurationMicros,
            long rangeMicros,
            long powerMicros,
            long botPowerMultiplierMicros,
            string presentationProfileId,
            string sourceRevision,
            string rawSha256)
        {
            Id = id;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            BehaviorProfileId = behaviorProfileId;
            TargetingProfileId = targetingProfileId;
            ResourcePolicyId = resourcePolicyId;
            CooldownPolicyId = cooldownPolicyId;
            ManaCostMicros = manaCostMicros;
            CastDurationMicros = castDurationMicros;
            CooldownDurationMicros = cooldownDurationMicros;
            RangeMicros = rangeMicros;
            PowerMicros = powerMicros;
            BotPowerMultiplierMicros = botPowerMultiplierMicros;
            PresentationProfileId = presentationProfileId;
            SourceRevision = sourceRevision;
            RawSha256 = rawSha256;
        }

        public string Id { get; }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public string BehaviorProfileId { get; }
        public string TargetingProfileId { get; }
        public string ResourcePolicyId { get; }
        public string CooldownPolicyId { get; }
        public long ManaCostMicros { get; }
        public long CastDurationMicros { get; }
        public long CooldownDurationMicros { get; }
        public long RangeMicros { get; }
        public long PowerMicros { get; }
        public long BotPowerMultiplierMicros { get; }
        public string PresentationProfileId { get; }
        public string SourceRevision { get; }
        public string RawSha256 { get; }
    }

    public sealed class CombatSkillSlotBinding
    {
        public CombatSkillSlotBinding(
            int slotIndex,
            string skillDefinitionId,
            string skillContentVersion,
            string inputBindingId = "",
            string availabilityProfileId = "")
        {
            SlotIndex = slotIndex;
            SkillDefinitionId = skillDefinitionId;
            SkillContentVersion = skillContentVersion;
            InputBindingId = inputBindingId;
            AvailabilityProfileId = availabilityProfileId;
        }

        public int SlotIndex { get; }
        public string SkillDefinitionId { get; }
        public string SkillContentVersion { get; }
        public string InputBindingId { get; }
        public string AvailabilityProfileId { get; }
    }

    /// <summary>
    /// Immutable initial Champion loadout. The validator enforces exactly four
    /// unique bindings for slots 0..3; the constructor only snapshots source data.
    /// </summary>
    public sealed class CombatSkillLoadout
    {
        public const int RequiredSlotCount = 4;

        public CombatSkillLoadout(
            string id,
            string schemaVersion,
            string contentVersion,
            string championOrClassProfileId,
            IList<CombatSkillSlotBinding> slots,
            string sourceRevision,
            string rawSha256)
        {
            Id = id;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            ChampionOrClassProfileId = championOrClassProfileId;
            Slots = CombatImmutable.Freeze(
                slots,
                nameof(slots),
                CombatTechnicalLimits.MaximumLoadoutBindings);
            SourceRevision = sourceRevision;
            RawSha256 = rawSha256;
        }

        public string Id { get; }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public string ChampionOrClassProfileId { get; }
        public IReadOnlyList<CombatSkillSlotBinding> Slots { get; }
        public string SourceRevision { get; }
        public string RawSha256 { get; }
    }

    /// <summary>
    /// Atomic publication unit produced only after the loadout and every
    /// referenced skill have passed identity, version, reference, and trusted
    /// source-hash validation together.
    /// </summary>
    public sealed class ValidatedCombatSkillLoadoutSnapshot
    {
        internal ValidatedCombatSkillLoadoutSnapshot(
            CombatSkillLoadout loadout,
            IList<CombatSkillDefinition> skillsInSlotOrder,
            CombatContractReferenceCatalog references,
            string trustedLoadoutRawSha256,
            IList<string> trustedSkillRawSha256InSlotOrder)
        {
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            References =
                references ?? throw new ArgumentNullException(nameof(references));
            SkillsInSlotOrder = CombatImmutable.FreezeNonNull(
                skillsInSlotOrder,
                nameof(skillsInSlotOrder),
                CombatSkillLoadout.RequiredSlotCount);
            if (!CombatPrimitiveValidation.IsStableId(
                    References.CatalogSetId))
            {
                throw new ArgumentException(
                    "Catalog-set identity is invalid.",
                    nameof(references));
            }
            if (!CombatPrimitiveValidation.IsSha256(
                    trustedLoadoutRawSha256))
            {
                throw new ArgumentException(
                    "Trusted loadout hash is invalid.",
                    nameof(trustedLoadoutRawSha256));
            }

            TrustedLoadoutRawSha256 = trustedLoadoutRawSha256;
            TrustedSkillRawSha256InSlotOrder = CombatImmutable.Freeze(
                trustedSkillRawSha256InSlotOrder,
                nameof(trustedSkillRawSha256InSlotOrder),
                CombatSkillLoadout.RequiredSlotCount);
        }

        public CombatSkillLoadout Loadout { get; }
        public CombatContractReferenceCatalog References { get; }
        public IReadOnlyList<CombatSkillDefinition> SkillsInSlotOrder { get; }
        public string CatalogSetId => References.CatalogSetId;
        public string TrustedLoadoutRawSha256 { get; }
        public IReadOnlyList<string> TrustedSkillRawSha256InSlotOrder { get; }
    }

    public sealed class CombatSkillLoadoutValidationResult
    {
        internal CombatSkillLoadoutValidationResult(
            CombatValidationResult validation,
            ValidatedCombatSkillLoadoutSnapshot snapshot)
        {
            Validation =
                validation ?? throw new ArgumentNullException(nameof(validation));
            Snapshot = snapshot;
            if (Validation.IsValid != (Snapshot != null))
            {
                throw new ArgumentException(
                    "A validated snapshot exists if and only if validation succeeds.",
                    nameof(snapshot));
            }
        }

        public CombatValidationResult Validation { get; }
        public ValidatedCombatSkillLoadoutSnapshot Snapshot { get; }
        public IReadOnlyList<CombatDiagnostic> Diagnostics =>
            Validation.Diagnostics;
        public bool IsValid => Validation.IsValid;
        public bool IsBlocked => Validation.IsBlocked;
    }

    public sealed class CombatSkillBehaviorReference
    {
        public CombatSkillBehaviorReference(string id, CombatSkillBehaviorKind kind)
        {
            Id = id;
            Kind = kind;
        }

        public string Id { get; }
        public CombatSkillBehaviorKind Kind { get; }
    }

    public sealed class CombatTargetingReference
    {
        public CombatTargetingReference(
            string id,
            CombatTargetDisposition disposition,
            CombatTargetIntentKind allowedIntentKind,
            string rangeUnitProfileId,
            string requiredAreaProfileId,
            bool requiresLineOfSight,
            string requiredParticipantTargetingProfileId = "")
        {
            Id = id;
            Disposition = disposition;
            AllowedIntentKind = allowedIntentKind;
            RangeUnitProfileId = rangeUnitProfileId;
            RequiredAreaProfileId = requiredAreaProfileId;
            RequiresLineOfSight = requiresLineOfSight;
            RequiredParticipantTargetingProfileId =
                requiredParticipantTargetingProfileId;
        }

        public string Id { get; }
        public CombatTargetDisposition Disposition { get; }
        public CombatTargetIntentKind AllowedIntentKind { get; }
        public string RangeUnitProfileId { get; }
        public string RequiredAreaProfileId { get; }
        public bool RequiresLineOfSight { get; }
        public string RequiredParticipantTargetingProfileId { get; }
    }

    internal static class CombatTargetIntentCompatibility
    {
        internal static bool IsSupportedReference(
            CombatTargetDisposition disposition,
            CombatTargetIntentKind intent,
            string requiredAreaProfileId,
            bool requiresLineOfSight,
            string requiredParticipantTargetingProfileId)
        {
            switch (intent)
            {
                case CombatTargetIntentKind.Self:
                    return disposition == CombatTargetDisposition.Self &&
                           string.IsNullOrEmpty(requiredAreaProfileId) &&
                           string.IsNullOrEmpty(
                               requiredParticipantTargetingProfileId) &&
                           !requiresLineOfSight;
                case CombatTargetIntentKind.Point:
                case CombatTargetIntentKind.Direction:
                    return disposition == CombatTargetDisposition.Any &&
                           string.IsNullOrEmpty(requiredAreaProfileId) &&
                           string.IsNullOrEmpty(
                               requiredParticipantTargetingProfileId) &&
                           !requiresLineOfSight;
                case CombatTargetIntentKind.ParticipantId:
                    return disposition != CombatTargetDisposition.Unknown &&
                           disposition != CombatTargetDisposition.Self &&
                           Enum.IsDefined(
                               typeof(CombatTargetDisposition),
                               disposition) &&
                           string.IsNullOrEmpty(requiredAreaProfileId) &&
                           CombatPrimitiveValidation.IsStableId(
                               requiredParticipantTargetingProfileId);
                case CombatTargetIntentKind.AreaProfile:
                    return disposition != CombatTargetDisposition.Unknown &&
                           disposition != CombatTargetDisposition.Self &&
                           Enum.IsDefined(
                               typeof(CombatTargetDisposition),
                               disposition) &&
                           CombatPrimitiveValidation.IsStableId(
                               requiredAreaProfileId) &&
                           CombatPrimitiveValidation.IsStableId(
                               requiredParticipantTargetingProfileId);
                default:
                    return false;
            }
        }

        internal static bool RequiresZeroRange(
            CombatTargetIntentKind intent)
        {
            return intent == CombatTargetIntentKind.Self ||
                   intent == CombatTargetIntentKind.Direction;
        }
    }

    /// <summary>
    /// Immutable registry of the exact external references that a contract snapshot
    /// may resolve. It is a validation input, not a fallback catalog.
    /// </summary>
    public sealed class CombatContractReferenceCatalog
    {
        private readonly HashSet<string> _contentVersions;
        private readonly HashSet<string> _championOrClassProfileIds;
        private readonly Dictionary<string, CombatSkillBehaviorReference> _behaviors;
        private readonly Dictionary<string, CombatTargetingReference> _targeting;
        private readonly HashSet<string> _resourcePolicyIds;
        private readonly HashSet<string> _cooldownPolicyIds;
        private readonly HashSet<string> _presentationProfileIds;
        private readonly HashSet<string> _movementProfileIds;
        private readonly HashSet<string> _dodgeProfileIds;
        private readonly HashSet<string> _availabilityProfileIds;

        public CombatContractReferenceCatalog(
            string catalogSetId,
            string schemaVersion,
            IList<string> supportedContentVersions,
            IList<string> championOrClassProfileIds,
            IList<CombatSkillBehaviorReference> behaviors,
            IList<CombatTargetingReference> targeting,
            IList<string> resourcePolicyIds,
            IList<string> cooldownPolicyIds,
            IList<string> presentationProfileIds,
            IList<string> movementProfileIds,
            IList<string> dodgeProfileIds,
            IList<string> availabilityProfileIds = null)
        {
            if (!CombatPrimitiveValidation.IsStableId(catalogSetId))
                throw new ArgumentException("Catalog-set ID is invalid.", nameof(catalogSetId));
            if (!CombatPrimitiveValidation.IsSupportedSchemaVersion(schemaVersion))
                throw new ArgumentException("Schema version is unsupported.", nameof(schemaVersion));

            CatalogSetId = catalogSetId;
            SchemaVersion = schemaVersion;
            SupportedContentVersions = FreezeVersions(
                supportedContentVersions,
                nameof(supportedContentVersions),
                out _contentVersions);
            ChampionOrClassProfileIds = FreezeIdentifiers(
                championOrClassProfileIds,
                nameof(championOrClassProfileIds),
                out _championOrClassProfileIds);
            Behaviors = FreezeBehaviors(behaviors, out _behaviors);
            Targeting = FreezeTargeting(targeting, out _targeting);
            ResourcePolicyIds = FreezeIdentifiers(
                resourcePolicyIds,
                nameof(resourcePolicyIds),
                out _resourcePolicyIds);
            CooldownPolicyIds = FreezeIdentifiers(
                cooldownPolicyIds,
                nameof(cooldownPolicyIds),
                out _cooldownPolicyIds);
            PresentationProfileIds = FreezeIdentifiers(
                presentationProfileIds,
                nameof(presentationProfileIds),
                out _presentationProfileIds);
            MovementProfileIds = FreezeIdentifiers(
                movementProfileIds,
                nameof(movementProfileIds),
                out _movementProfileIds);
            DodgeProfileIds = FreezeIdentifiers(
                dodgeProfileIds,
                nameof(dodgeProfileIds),
                out _dodgeProfileIds);
            AvailabilityProfileIds = FreezeIdentifiers(
                availabilityProfileIds ?? new string[0],
                nameof(availabilityProfileIds),
                out _availabilityProfileIds);
        }

        public string CatalogSetId { get; }
        public string SchemaVersion { get; }
        public IReadOnlyList<string> SupportedContentVersions { get; }
        public IReadOnlyList<string> ChampionOrClassProfileIds { get; }
        public IReadOnlyList<CombatSkillBehaviorReference> Behaviors { get; }
        public IReadOnlyList<CombatTargetingReference> Targeting { get; }
        public IReadOnlyList<string> ResourcePolicyIds { get; }
        public IReadOnlyList<string> CooldownPolicyIds { get; }
        public IReadOnlyList<string> PresentationProfileIds { get; }
        public IReadOnlyList<string> MovementProfileIds { get; }
        public IReadOnlyList<string> DodgeProfileIds { get; }
        public IReadOnlyList<string> AvailabilityProfileIds { get; }

        public bool SupportsContentVersion(string value) =>
            value != null && _contentVersions.Contains(value);

        public bool ContainsChampionOrClassProfile(string value) =>
            value != null && _championOrClassProfileIds.Contains(value);

        public bool TryGetBehavior(
            string value,
            out CombatSkillBehaviorReference reference)
        {
            if (value == null)
            {
                reference = null;
                return false;
            }

            return _behaviors.TryGetValue(value, out reference);
        }

        public bool TryGetTargeting(
            string value,
            out CombatTargetingReference reference)
        {
            if (value == null)
            {
                reference = null;
                return false;
            }

            return _targeting.TryGetValue(value, out reference);
        }

        public bool ContainsResourcePolicy(string value) =>
            value != null && _resourcePolicyIds.Contains(value);

        public bool ContainsCooldownPolicy(string value) =>
            value != null && _cooldownPolicyIds.Contains(value);

        public bool ContainsPresentationProfile(string value) =>
            value != null && _presentationProfileIds.Contains(value);

        public bool ContainsMovementProfile(string value) =>
            value != null && _movementProfileIds.Contains(value);

        public bool ContainsDodgeProfile(string value) =>
            value != null && _dodgeProfileIds.Contains(value);

        public bool ContainsAvailabilityProfile(string value) =>
            value != null && _availabilityProfileIds.Contains(value);

        private static IReadOnlyList<string> FreezeIdentifiers(
            IList<string> source,
            string parameterName,
            out HashSet<string> index)
        {
            IReadOnlyList<string> frozen = CombatImmutable.Freeze(source, parameterName);
            index = new HashSet<string>(StringComparer.Ordinal);
            for (int itemIndex = 0; itemIndex < frozen.Count; itemIndex++)
            {
                string value = frozen[itemIndex];
                if (!CombatPrimitiveValidation.IsStableId(value))
                    throw new ArgumentException("Reference ID is invalid.", parameterName);
                if (!index.Add(value))
                    throw new ArgumentException("Reference ID is duplicated: " + value, parameterName);
            }

            return frozen;
        }

        private static IReadOnlyList<string> FreezeVersions(
            IList<string> source,
            string parameterName,
            out HashSet<string> index)
        {
            IReadOnlyList<string> frozen = CombatImmutable.Freeze(source, parameterName);
            index = new HashSet<string>(StringComparer.Ordinal);
            for (int itemIndex = 0; itemIndex < frozen.Count; itemIndex++)
            {
                string value = frozen[itemIndex];
                if (!CombatPrimitiveValidation.IsVersion(value))
                    throw new ArgumentException("Content version is invalid.", parameterName);
                if (!index.Add(value))
                    throw new ArgumentException("Content version is duplicated: " + value, parameterName);
            }

            return frozen;
        }

        private static IReadOnlyList<CombatSkillBehaviorReference> FreezeBehaviors(
            IList<CombatSkillBehaviorReference> source,
            out Dictionary<string, CombatSkillBehaviorReference> index)
        {
            IReadOnlyList<CombatSkillBehaviorReference> frozen =
                CombatImmutable.FreezeNonNull(source, nameof(source));
            index = new Dictionary<string, CombatSkillBehaviorReference>(StringComparer.Ordinal);
            for (int itemIndex = 0; itemIndex < frozen.Count; itemIndex++)
            {
                CombatSkillBehaviorReference reference = frozen[itemIndex];
                if (!CombatPrimitiveValidation.IsStableId(reference.Id) ||
                    reference.Kind == CombatSkillBehaviorKind.Unknown ||
                    !Enum.IsDefined(typeof(CombatSkillBehaviorKind), reference.Kind))
                    throw new ArgumentException("Behavior reference is invalid.", nameof(source));
                if (index.ContainsKey(reference.Id))
                    throw new ArgumentException("Behavior reference is duplicated: " + reference.Id, nameof(source));
                index.Add(reference.Id, reference);
            }

            return frozen;
        }

        private static IReadOnlyList<CombatTargetingReference> FreezeTargeting(
            IList<CombatTargetingReference> source,
            out Dictionary<string, CombatTargetingReference> index)
        {
            IReadOnlyList<CombatTargetingReference> frozen =
                CombatImmutable.FreezeNonNull(source, nameof(source));
            index = new Dictionary<string, CombatTargetingReference>(StringComparer.Ordinal);
            for (int itemIndex = 0; itemIndex < frozen.Count; itemIndex++)
            {
                CombatTargetingReference reference = frozen[itemIndex];
                if (!CombatPrimitiveValidation.IsStableId(reference.Id) ||
                    reference.Disposition == CombatTargetDisposition.Unknown ||
                    !Enum.IsDefined(typeof(CombatTargetDisposition), reference.Disposition) ||
                    !Enum.IsDefined(
                        typeof(CombatTargetIntentKind),
                        reference.AllowedIntentKind) ||
                    !CombatPrimitiveValidation.IsStableId(
                        reference.RangeUnitProfileId) ||
                    !CombatTargetIntentCompatibility
                        .IsSupportedReference(
                            reference.Disposition,
                             reference.AllowedIntentKind,
                             reference.RequiredAreaProfileId,
                             reference.RequiresLineOfSight,
                             reference.RequiredParticipantTargetingProfileId))
                    throw new ArgumentException("Targeting reference is invalid.", nameof(source));
                if (index.ContainsKey(reference.Id))
                    throw new ArgumentException("Targeting reference is duplicated: " + reference.Id, nameof(source));
                index.Add(reference.Id, reference);
            }

            return frozen;
        }
    }
}
