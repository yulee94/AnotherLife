using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.ChampionMode.C1
{
    public enum CombatParticipantRole
    {
        Champion = 0,
        Boss = 1,
        Ally = 2,
        Enemy = 3,
        PracticeDummy = 4,
        Ambient = 5
    }

    public enum CombatTargetIntentKind
    {
        Self = 0,
        ParticipantId = 1,
        Point = 2,
        Direction = 3,
        AreaProfile = 4
    }

    public enum CombatTargetTeamRule
    {
        Self = 0,
        Ally = 1,
        Enemy = 2,
        Any = 3
    }

    public enum ParticipantTargetPlanStatus
    {
        Resolved = 0,
        ResolvedNoTargets = 1,
        ResolvedIntentOnly = 2,
        RejectedInvalidRequest = 3,
        RejectedInvalidRegistry = 4,
        RejectedSourceUnavailable = 5,
        RejectedWrongEncounter = 6,
        RejectedSourceNotAlive = 7,
        RejectedInvalidRange = 8,
        RejectedOutOfRange = 9,
        RejectedTargetingPolicy = 10,
        RejectedInvalidHitLedger = 11,
        RejectedStaleHitLedger = 12,
        RejectedHitLedgerMismatch = 13,
        RejectedHitLedgerCapacity = 14,
        RejectedActionUnavailable = 15,
        RejectedEncounterUnavailable = 16
    }

    public enum ParticipantTargetCandidateStatus
    {
        Accepted = 0,
        DuplicateParticipantSuppressed = 1,
        RejectedNullCandidate = 2,
        RejectedUnknownHandle = 3,
        RejectedStaleHandleGeneration = 4,
        RejectedWrongEncounter = 5,
        RejectedWrongAttempt = 6,
        RejectedIneligibleLifeState = 7,
        RejectedTeamRule = 8,
        RejectedInvalidPosition = 9,
        RejectedOutOfRange = 10,
        RejectedLineOfSight = 11,
        RejectedDifferentParticipant = 12,
        RejectedAlreadyHit = 13,
        RejectedTargetingProfile = 14
    }

    public static class CombatTargetingTechnicalLimits
    {
        public const int MaximumParticipants = 128;
        public const int MaximumCandidates = 512;
        public const int MaximumActionHitLedgerEntries = 512;
    }

    /// <summary>
    /// Pure participant-registry row. RuntimeHandleId is an injected fake handle
    /// identity, never a Unity instance ID, object name, component, or tag.
    /// </summary>
    public sealed class CombatParticipantRegistration
    {
        public CombatParticipantRegistration(
            string participantId,
            string actorProfileId,
            CombatParticipantRole role,
            string teamId,
            string realmContextId,
            CombatantLifeState lifeState,
            CombatantControlState controlState,
            string actionLockOwnerActionId,
            bool isTargetEligible,
            string runtimeHandleId,
            long runtimeHandleGeneration,
            string targetingProfileId,
            string encounterSessionId,
            string encounterAttemptId,
            float positionX,
            float positionY,
            float positionZ,
            string positionUnitProfileId)
        {
            ParticipantId = participantId ?? string.Empty;
            ActorProfileId = actorProfileId ?? string.Empty;
            Role = role;
            TeamId = teamId ?? string.Empty;
            RealmContextId = realmContextId ?? string.Empty;
            LifeState = lifeState;
            ControlState = controlState;
            ActionLockOwnerActionId =
                actionLockOwnerActionId ?? string.Empty;
            IsTargetEligible = isTargetEligible;
            RuntimeHandleId = runtimeHandleId ?? string.Empty;
            RuntimeHandleGeneration = runtimeHandleGeneration;
            TargetingProfileId = targetingProfileId ?? string.Empty;
            EncounterSessionId = encounterSessionId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            PositionX = positionX;
            PositionY = positionY;
            PositionZ = positionZ;
            PositionUnitProfileId = positionUnitProfileId ?? string.Empty;
        }

        public string ParticipantId { get; }
        public string ActorProfileId { get; }
        public CombatParticipantRole Role { get; }
        public string TeamId { get; }
        public string RealmContextId { get; }
        public CombatantLifeState LifeState { get; }
        public CombatantControlState ControlState { get; }
        public string ActionLockOwnerActionId { get; }
        public bool IsTargetEligible { get; }
        public string RuntimeHandleId { get; }
        public long RuntimeHandleGeneration { get; }
        public string TargetingProfileId { get; }
        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public float PositionX { get; }
        public float PositionY { get; }
        public float PositionZ { get; }
        public string PositionUnitProfileId { get; }
    }

    /// <summary>
    /// A physics-adapter observation represented only by stable fake data.
    /// Multiple observations can represent multiple colliders on one participant.
    /// </summary>
    public sealed class FakeTargetHandleObservation
    {
        public FakeTargetHandleObservation(
            string runtimeHandleId,
            long runtimeHandleGeneration,
            float observedX,
            float observedY,
            float observedZ,
            string positionUnitProfileId,
            bool hasLineOfSight)
        {
            RuntimeHandleId = runtimeHandleId ?? string.Empty;
            RuntimeHandleGeneration = runtimeHandleGeneration;
            ObservedX = observedX;
            ObservedY = observedY;
            ObservedZ = observedZ;
            PositionUnitProfileId = positionUnitProfileId ?? string.Empty;
            HasLineOfSight = hasLineOfSight;
        }

        public string RuntimeHandleId { get; }
        public long RuntimeHandleGeneration { get; }
        public float ObservedX { get; }
        public float ObservedY { get; }
        public float ObservedZ { get; }
        public string PositionUnitProfileId { get; }
        public bool HasLineOfSight { get; }
    }

    public sealed class ParticipantTargetRequest
    {
        public ParticipantTargetRequest(
            string actionId,
            string encounterSessionId,
            string encounterAttemptId,
            string sourceParticipantId,
            string skillDefinitionId,
            string skillContentVersion,
            string expectedActionRevision,
            CombatActionSource actionSource,
            CombatTargetIntentKind intentKind,
            CombatTargetTeamRule teamRule,
            string targetParticipantId,
            string areaProfileId,
            float targetX,
            float targetY,
            float targetZ,
            string targetUnitProfileId,
            float maximumRange,
            string rangeUnitProfileId,
            bool requireLineOfSight,
            IList<FakeTargetHandleObservation> candidates,
            IList<string> actionHitLedgerParticipantIds,
            long expectedHitLedgerRevision = 0L)
        {
            ActionId = actionId ?? string.Empty;
            EncounterSessionId = encounterSessionId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            SourceParticipantId = sourceParticipantId ?? string.Empty;
            SkillDefinitionId = skillDefinitionId ?? string.Empty;
            SkillContentVersion = skillContentVersion ?? string.Empty;
            ExpectedActionRevision =
                expectedActionRevision ?? string.Empty;
            ActionSource = actionSource;
            IntentKind = intentKind;
            TeamRule = teamRule;
            TargetParticipantId = targetParticipantId ?? string.Empty;
            AreaProfileId = areaProfileId ?? string.Empty;
            TargetX = targetX;
            TargetY = targetY;
            TargetZ = targetZ;
            TargetUnitProfileId = targetUnitProfileId ?? string.Empty;
            MaximumRange = maximumRange;
            RangeUnitProfileId = rangeUnitProfileId ?? string.Empty;
            RequireLineOfSight = requireLineOfSight;
            CandidateInputCount = candidates?.Count ?? 0;
            ActionHitLedgerInputCount = actionHitLedgerParticipantIds?.Count ?? 0;
            ExpectedHitLedgerRevision = expectedHitLedgerRevision;
            Candidates = FreezeAllowNull(
                candidates,
                CombatTargetingTechnicalLimits.MaximumCandidates + 1);
            ActionHitLedgerParticipantIds = FreezeStrings(
                actionHitLedgerParticipantIds,
                CombatTargetingTechnicalLimits.MaximumActionHitLedgerEntries + 1);
        }

        public string ActionId { get; }
        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string SourceParticipantId { get; }
        public string SkillDefinitionId { get; }
        public string SkillContentVersion { get; }
        public string ExpectedActionRevision { get; }
        public CombatActionSource ActionSource { get; }
        public CombatTargetIntentKind IntentKind { get; }
        public CombatTargetTeamRule TeamRule { get; }
        public string TargetParticipantId { get; }
        public string AreaProfileId { get; }
        public float TargetX { get; }
        public float TargetY { get; }
        public float TargetZ { get; }
        public string TargetUnitProfileId { get; }
        public float MaximumRange { get; }
        public string RangeUnitProfileId { get; }
        public bool RequireLineOfSight { get; }
        public int CandidateInputCount { get; }
        public int ActionHitLedgerInputCount { get; }
        public long ExpectedHitLedgerRevision { get; }
        public IReadOnlyList<FakeTargetHandleObservation> Candidates { get; }
        public IReadOnlyList<string> ActionHitLedgerParticipantIds { get; }

        private static IReadOnlyList<FakeTargetHandleObservation> FreezeAllowNull(
            IList<FakeTargetHandleObservation> source,
            int maximumCopyCount)
        {
            if (source == null)
            {
                return Array.AsReadOnly(new FakeTargetHandleObservation[0]);
            }

            int count = Math.Min(source.Count, maximumCopyCount);
            var copy = new FakeTargetHandleObservation[count];
            for (int index = 0; index < count; index++)
            {
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyList<string> FreezeStrings(
            IList<string> source,
            int maximumCopyCount)
        {
            if (source == null)
            {
                return Array.AsReadOnly(new string[0]);
            }

            int count = Math.Min(source.Count, maximumCopyCount);
            var copy = new string[count];
            for (int index = 0; index < count; index++)
            {
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    /// Immutable, action-bound authority for participants already affected by an
    /// action. The owning adapter retains the latest returned snapshot; request
    /// lists are optimistic assertions and never become ledger authority.
    /// </summary>
    public sealed class CombatActionHitLedgerSnapshot
    {
        internal CombatActionHitLedgerSnapshot(
            string actionId,
            string encounterSessionId,
            string encounterAttemptId,
            string actionRevision,
            long revision,
            IList<string> participantIds,
            string stateHash)
        {
            ActionId = actionId;
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActionRevision = actionRevision;
            Revision = revision;
            ParticipantIds = Array.AsReadOnly(
                (participantIds ?? new string[0]).ToArray());
            StateHash = stateHash;
        }

        public string ActionId { get; }
        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string ActionRevision { get; }
        public long Revision { get; }
        public IReadOnlyList<string> ParticipantIds { get; }
        public string StateHash { get; }
    }

    public sealed class ParticipantTargetHitLedgerReceipt
    {
        internal ParticipantTargetHitLedgerReceipt(
            string actionId,
            string encounterSessionId,
            string encounterAttemptId,
            string actionRevision,
            long beforeRevision,
            long afterRevision,
            string beforeStateHash,
            string afterStateHash,
            IList<string> addedParticipantIds)
        {
            ActionId = actionId;
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActionRevision = actionRevision;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            BeforeStateHash = beforeStateHash;
            AfterStateHash = afterStateHash;
            AddedParticipantIds = Array.AsReadOnly(
                (addedParticipantIds ?? new string[0]).ToArray());
        }

        public string ActionId { get; }
        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string ActionRevision { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public string BeforeStateHash { get; }
        public string AfterStateHash { get; }
        public IReadOnlyList<string> AddedParticipantIds { get; }
    }

    public sealed class ParticipantTargetCandidateReceipt
    {
        public ParticipantTargetCandidateReceipt(
            int candidateIndex,
            string runtimeHandleId,
            string participantId,
            ParticipantTargetCandidateStatus status)
        {
            CandidateIndex = candidateIndex;
            RuntimeHandleId = runtimeHandleId ?? string.Empty;
            ParticipantId = participantId ?? string.Empty;
            Status = status;
        }

        public int CandidateIndex { get; }
        public string RuntimeHandleId { get; }
        public string ParticipantId { get; }
        public ParticipantTargetCandidateStatus Status { get; }
    }

    public sealed class ParticipantTargetPlan
    {
        internal ParticipantTargetPlan(
            ParticipantTargetPlanStatus status,
            IEnumerable<string> resolvedParticipantIds,
            IEnumerable<ParticipantTargetCandidateReceipt> candidateReceipts,
            bool hasResolvedVector,
            FiniteCombatVector3 resolvedVector,
            CombatActionHitLedgerSnapshot beforeHitLedger,
            CombatActionHitLedgerSnapshot afterHitLedger,
            ParticipantTargetHitLedgerReceipt hitLedgerReceipt,
            IEnumerable<CombatDiagnostic> diagnostics)
        {
            Status = status;
            ResolvedParticipantIds = Freeze(
                resolvedParticipantIds ?? Enumerable.Empty<string>());
            CandidateReceipts = Freeze(
                candidateReceipts ?? Enumerable.Empty<ParticipantTargetCandidateReceipt>());
            HasResolvedVector = hasResolvedVector;
            ResolvedVector = resolvedVector;
            BeforeHitLedger = beforeHitLedger;
            AfterHitLedger = afterHitLedger;
            HitLedgerReceipt = hitLedgerReceipt;
            Diagnostics = CombatDiagnosticOrdering.Order(diagnostics);
        }

        public ParticipantTargetPlanStatus Status { get; }
        public IReadOnlyList<string> ResolvedParticipantIds { get; }
        public IReadOnlyList<ParticipantTargetCandidateReceipt> CandidateReceipts { get; }
        public bool HasResolvedVector { get; }
        public FiniteCombatVector3 ResolvedVector { get; }
        public CombatActionHitLedgerSnapshot BeforeHitLedger { get; }
        public CombatActionHitLedgerSnapshot AfterHitLedger { get; }
        public ParticipantTargetHitLedgerReceipt HitLedgerReceipt { get; }
        public IReadOnlyList<CombatDiagnostic> Diagnostics { get; }

        private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values)
        {
            return Array.AsReadOnly(values.ToArray());
        }
    }

    /// <summary>
    /// Immutable action-scoped targeting authority derived from an atomically
    /// validated loadout and its exact reference catalog at the one Requested
    /// admission revision. ActionRevision is that stable root revision, not a
    /// mutable lifecycle cursor. Callers cannot construct, rebind, remint after
    /// transition, or widen this policy directly.
    /// </summary>
    public sealed class CombatActionTargetPolicySnapshot
    {
        internal CombatActionTargetPolicySnapshot(
            string actionId,
            string encounterSessionId,
            string encounterAttemptId,
            string actorParticipantId,
            string skillDefinitionId,
            string skillContentVersion,
            CombatActionSource actionSource,
            string actionRevision,
            string loadoutId,
            string loadoutOwnerProfileId,
            string catalogSetId,
            string targetingProfileId,
            CombatTargetDisposition disposition,
            CombatTargetIntentKind allowedIntentKind,
            string rangeUnitProfileId,
            string requiredAreaProfileId,
            string requiredParticipantTargetingProfileId,
            bool requiresLineOfSight,
            long maximumRangeMicros,
            string trustedSkillRawSha256)
        {
            ActionId = actionId;
            EncounterSessionId = encounterSessionId;
            EncounterAttemptId = encounterAttemptId;
            ActorParticipantId = actorParticipantId;
            SkillDefinitionId = skillDefinitionId;
            SkillContentVersion = skillContentVersion;
            ActionSource = actionSource;
            ActionRevision = actionRevision;
            LoadoutId = loadoutId;
            LoadoutOwnerProfileId = loadoutOwnerProfileId;
            CatalogSetId = catalogSetId;
            TargetingProfileId = targetingProfileId;
            Disposition = disposition;
            AllowedIntentKind = allowedIntentKind;
            RangeUnitProfileId = rangeUnitProfileId;
            RequiredAreaProfileId = requiredAreaProfileId;
            RequiredParticipantTargetingProfileId =
                requiredParticipantTargetingProfileId;
            RequiresLineOfSight = requiresLineOfSight;
            MaximumRangeMicros = maximumRangeMicros;
            TrustedSkillRawSha256 = trustedSkillRawSha256;
        }

        public string ActionId { get; }
        public string EncounterSessionId { get; }
        public string EncounterAttemptId { get; }
        public string ActorParticipantId { get; }
        public string SkillDefinitionId { get; }
        public string SkillContentVersion { get; }
        public CombatActionSource ActionSource { get; }
        public string ActionRevision { get; }
        public string LoadoutId { get; }
        public string LoadoutOwnerProfileId { get; }
        public string CatalogSetId { get; }
        public string TargetingProfileId { get; }
        public CombatTargetDisposition Disposition { get; }
        public CombatTargetIntentKind AllowedIntentKind { get; }
        public string RangeUnitProfileId { get; }
        public string RequiredAreaProfileId { get; }
        public string RequiredParticipantTargetingProfileId { get; }
        public bool RequiresLineOfSight { get; }
        public long MaximumRangeMicros { get; }
        public float MaximumRange =>
            (float)(
                MaximumRangeMicros /
                (double)CombatTechnicalLimits.MicrosPerUnit);
        public string TrustedSkillRawSha256 { get; }
    }

    public static class CombatActionTargetPolicyFactory
    {
        public static bool TryCreate(
            CombatActionSnapshot acceptedAction,
            ValidatedCombatSkillLoadoutSnapshot loadout,
            out CombatActionTargetPolicySnapshot policy)
        {
            policy = null;
            CombatActionRequest actionRequest =
                acceptedAction?.Request;
            CombatContractReferenceCatalog references =
                loadout?.References;
            if (actionRequest == null ||
                acceptedAction.Policy == null ||
                acceptedAction.IsTerminal ||
                acceptedAction.State != CombatActionState.Requested ||
                actionRequest.ActionId.IsDefault ||
                actionRequest.EncounterSessionId.IsDefault ||
                actionRequest.EncounterAttemptId.IsDefault ||
                actionRequest.ActorParticipantId.IsDefault ||
                actionRequest.BehaviorOrSkillId.IsDefault ||
                actionRequest.SkillContentVersion.IsDefault ||
                !CombatPrimitiveValidation.IsStableId(
                    acceptedAction.Revision) ||
                loadout?.Loadout == null ||
                references == null ||
                !StringComparer.Ordinal.Equals(
                    loadout.CatalogSetId,
                    references.CatalogSetId) ||
                loadout.SkillsInSlotOrder.Count !=
                    CombatSkillLoadout.RequiredSlotCount ||
                loadout.TrustedSkillRawSha256InSlotOrder.Count !=
                    CombatSkillLoadout.RequiredSlotCount)
            {
                return false;
            }

            CombatSkillDefinition selectedSkill = null;
            string trustedSkillHash = null;
            int matches = 0;
            for (int index = 0;
                 index < loadout.SkillsInSlotOrder.Count;
                 index++)
            {
                CombatSkillDefinition skill =
                    loadout.SkillsInSlotOrder[index];
                string trustedHash =
                    loadout.TrustedSkillRawSha256InSlotOrder[index];
                if (skill == null ||
                    !CombatPrimitiveValidation.IsSha256(trustedHash) ||
                    !StringComparer.Ordinal.Equals(
                        skill.RawSha256,
                        trustedHash))
                {
                    return false;
                }

                if (StringComparer.Ordinal.Equals(
                        skill.Id,
                        actionRequest.BehaviorOrSkillId.Value) &&
                    StringComparer.Ordinal.Equals(
                        skill.ContentVersion,
                        actionRequest.SkillContentVersion.Value))
                {
                    selectedSkill = skill;
                    trustedSkillHash = trustedHash;
                    matches++;
                }
            }

            if (matches != 1 ||
                selectedSkill == null ||
                !loadout.Loadout.Slots.Any(binding =>
                    binding != null &&
                    StringComparer.Ordinal.Equals(
                        binding.SkillDefinitionId,
                        actionRequest.BehaviorOrSkillId.Value) &&
                    StringComparer.Ordinal.Equals(
                        binding.SkillContentVersion,
                        actionRequest.SkillContentVersion.Value)) ||
                !references.TryGetTargeting(
                    selectedSkill.TargetingProfileId,
                    out CombatTargetingReference targeting) ||
                targeting == null ||
                targeting.Disposition == CombatTargetDisposition.Unknown ||
                !Enum.IsDefined(
                    typeof(CombatTargetDisposition),
                    targeting.Disposition))
            {
                return false;
            }

            policy = new CombatActionTargetPolicySnapshot(
                actionRequest.ActionId.Value,
                actionRequest.EncounterSessionId.Value,
                actionRequest.EncounterAttemptId.Value,
                actionRequest.ActorParticipantId.Value,
                selectedSkill.Id,
                selectedSkill.ContentVersion,
                actionRequest.Source,
                acceptedAction.Revision,
                loadout.Loadout.Id,
                loadout.Loadout.ChampionOrClassProfileId,
                loadout.CatalogSetId,
                targeting.Id,
                targeting.Disposition,
                targeting.AllowedIntentKind,
                targeting.RangeUnitProfileId,
                targeting.RequiredAreaProfileId,
                targeting.RequiredParticipantTargetingProfileId,
                targeting.RequiresLineOfSight,
                selectedSkill.RangeMicros,
                trustedSkillHash);
            return true;
        }
    }

    public static class ParticipantTargetPlanner
    {
        public const string PolicyVersion = "combat.targeting.c1.v1";

        public static bool TryCreateInitialHitLedger(
            CombatActionTargetPolicySnapshot policy,
            out CombatActionHitLedgerSnapshot hitLedger)
        {
            hitLedger = null;
            if (!ValidateTargetPolicy(policy))
            {
                return false;
            }

            string[] participantIds = new string[0];
            hitLedger = new CombatActionHitLedgerSnapshot(
                policy.ActionId,
                policy.EncounterSessionId,
                policy.EncounterAttemptId,
                policy.ActionRevision,
                0L,
                participantIds,
                ComputeHitLedgerHash(
                    policy.ActionId,
                    policy.EncounterSessionId,
                    policy.EncounterAttemptId,
                    policy.ActionRevision,
                    0L,
                    participantIds));
            return true;
        }

        public static ParticipantTargetPlan Resolve(
            IList<CombatParticipantRegistration> participantRegistry,
            ParticipantTargetRequest request,
            CombatActionTargetPolicySnapshot policy,
            CombatActionHitLedgerSnapshot hitLedger,
            CombatActionSnapshot currentAction,
            ChampionEncounterStateSnapshot currentEncounter)
        {
            var diagnostics = new List<CombatDiagnostic>();
            if (request == null)
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-INVALID-REQUEST",
                    "request",
                    "Target request is null."));
                return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRequest, diagnostics);
            }

            if (!ValidateRequestIdentity(request, diagnostics))
            {
                return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRequest, diagnostics);
            }

            if (!ValidateTargetPolicy(policy) ||
                !StringComparer.Ordinal.Equals(
                    policy.ActionId,
                    request.ActionId) ||
                !StringComparer.Ordinal.Equals(
                    policy.EncounterSessionId,
                    request.EncounterSessionId) ||
                !StringComparer.Ordinal.Equals(
                    policy.EncounterAttemptId,
                    request.EncounterAttemptId) ||
                !StringComparer.Ordinal.Equals(
                    policy.ActorParticipantId,
                    request.SourceParticipantId) ||
                !StringComparer.Ordinal.Equals(
                    policy.SkillDefinitionId,
                    request.SkillDefinitionId) ||
                !StringComparer.Ordinal.Equals(
                    policy.SkillContentVersion,
                    request.SkillContentVersion) ||
                policy.ActionSource != request.ActionSource ||
                policy.AllowedIntentKind != request.IntentKind ||
                !StringComparer.Ordinal.Equals(
                    policy.RangeUnitProfileId,
                    request.RangeUnitProfileId) ||
                !StringComparer.Ordinal.Equals(
                    policy.RangeUnitProfileId,
                    request.TargetUnitProfileId) ||
                !StringComparer.Ordinal.Equals(
                    policy.RequiredAreaProfileId,
                    request.AreaProfileId) ||
                policy.RequiresLineOfSight !=
                    request.RequireLineOfSight ||
                policy.MaximumRange != request.MaximumRange ||
                !IsDispositionCompatible(
                    policy.Disposition,
                    request.IntentKind,
                    request.TeamRule))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-POLICY-MISMATCH",
                    "policy",
                    "Target request does not match its immutable action-scoped skill targeting policy.",
                    request,
                    request.SourceParticipantId));
                return Rejected(
                    ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                    diagnostics);
            }

            if (!ValidateCurrentAction(
                    currentAction,
                    policy,
                    request))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-ACTION-UNAVAILABLE",
                    "currentAction",
                    "Target resolution requires the current matching nonterminal action revision.",
                    request));
                return Rejected(
                    ParticipantTargetPlanStatus
                        .RejectedActionUnavailable,
                    diagnostics);
            }

            if (!ValidateCurrentEncounter(
                    currentEncounter,
                    policy,
                    request))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-ENCOUNTER-UNAVAILABLE",
                    "currentEncounter",
                    "Target resolution requires the current active matching encounter attempt.",
                    request));
                return Rejected(
                    ParticipantTargetPlanStatus
                        .RejectedEncounterUnavailable,
                    diagnostics);
            }

            if (request.CandidateInputCount >
                    CombatTargetingTechnicalLimits.MaximumCandidates ||
                request.ActionHitLedgerInputCount >
                    CombatTargetingTechnicalLimits.MaximumActionHitLedgerEntries)
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-REQUEST-LIMIT-EXCEEDED",
                    "request.candidates",
                    "Target request exceeds a bounded collection limit.",
                    request));
                return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRequest, diagnostics);
            }

            if (!ValidateHitLedger(hitLedger, policy))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-INVALID-HIT-LEDGER",
                    "hitLedger",
                    "Action hit ledger is malformed or is not bound to the accepted action.",
                    request));
                return Rejected(
                    ParticipantTargetPlanStatus.RejectedInvalidHitLedger,
                    diagnostics);
            }

            if (request.ExpectedHitLedgerRevision != hitLedger.Revision)
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-STALE-HIT-LEDGER",
                    "request.expectedHitLedgerRevision",
                    "Target request does not match the current hit-ledger revision.",
                    request));
                return Rejected(
                    ParticipantTargetPlanStatus.RejectedStaleHitLedger,
                    diagnostics);
            }

            string[] assertedHitIds = request
                .ActionHitLedgerParticipantIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!assertedHitIds.SequenceEqual(
                    hitLedger.ParticipantIds,
                    StringComparer.Ordinal))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-HIT-LEDGER-MISMATCH",
                    "request.actionHitLedgerParticipantIds",
                    "Caller hit assertions do not match the authoritative action ledger.",
                    request));
                return Rejected(
                    ParticipantTargetPlanStatus.RejectedHitLedgerMismatch,
                    diagnostics);
            }

            if (!Enum.IsDefined(typeof(CombatTargetIntentKind), request.IntentKind) ||
                !Enum.IsDefined(typeof(CombatTargetTeamRule), request.TeamRule) ||
                !Enum.IsDefined(typeof(CombatActionSource), request.ActionSource))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-INVALID-REQUEST",
                    "request.intent",
                    "Target intent or team rule is undefined.",
                    request));
                return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRequest, diagnostics);
            }

            if (participantRegistry != null &&
                participantRegistry.Count >
                    CombatTargetingTechnicalLimits.MaximumParticipants)
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-REGISTRY-LIMIT-EXCEEDED",
                    "participantRegistry",
                    "Participant registry exceeds its technical maximum.",
                    request));
                return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRegistry, diagnostics);
            }

            List<CombatParticipantRegistration> participants =
                (participantRegistry ?? new CombatParticipantRegistration[0]).ToList();
            if (!ValidateRegistry(participants, diagnostics))
            {
                return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRegistry, diagnostics);
            }

            CombatParticipantRegistration source = participants.SingleOrDefault(
                candidate => StringComparer.Ordinal.Equals(
                    candidate.ParticipantId,
                    request.SourceParticipantId));
            if (source == null)
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-SOURCE-UNAVAILABLE",
                    "request.sourceParticipantId",
                    "Source participant is not registered.",
                    request,
                    request.SourceParticipantId));
                return Rejected(ParticipantTargetPlanStatus.RejectedSourceUnavailable, diagnostics);
            }

            if (!StringComparer.Ordinal.Equals(
                    policy.LoadoutOwnerProfileId,
                    source.ActorProfileId))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-LOADOUT-OWNER-MISMATCH",
                    "source.actorProfileId",
                    "Source participant does not own the validated loadout that authorized this action.",
                    request,
                    source.ParticipantId));
                return Rejected(
                    ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                    diagnostics);
            }

            if (!StringComparer.Ordinal.Equals(
                    source.EncounterSessionId,
                    request.EncounterSessionId) ||
                !StringComparer.Ordinal.Equals(
                    source.EncounterAttemptId,
                    request.EncounterAttemptId))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-WRONG-ENCOUNTER",
                    "request.encounterAttemptId",
                    "Source participant belongs to another encounter attempt.",
                    request,
                    source.ParticipantId));
                return Rejected(ParticipantTargetPlanStatus.RejectedWrongEncounter, diagnostics);
            }

            if (source.LifeState != CombatantLifeState.Alive ||
                !IsSourceActionEligible(
                    source.ControlState,
                    source.ActionLockOwnerActionId,
                    request.ActionSource,
                    request.ActionId))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-SOURCE-NOT-ALIVE",
                    "source.lifeState",
                    "Source participant life or control state cannot own this action.",
                    request,
                    source.ParticipantId));
                return Rejected(ParticipantTargetPlanStatus.RejectedSourceNotAlive, diagnostics);
            }

            if (!TryPosition(source, out FiniteCombatVector3 sourcePosition))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-INVALID-POSITION",
                    "source.position",
                    "Source participant position is invalid.",
                    request,
                    source.ParticipantId));
                return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRegistry, diagnostics);
            }

            if (request.IntentKind == CombatTargetIntentKind.Self)
            {
                if (request.TeamRule != CombatTargetTeamRule.Self ||
                    !string.IsNullOrEmpty(request.TargetParticipantId) ||
                    !string.IsNullOrEmpty(request.AreaProfileId) ||
                    request.Candidates.Count != 0 ||
                    request.TargetX != 0f ||
                    request.TargetY != 0f ||
                    request.TargetZ != 0f ||
                    request.MaximumRange != 0f ||
                    request.RequireLineOfSight ||
                    !StringComparer.Ordinal.Equals(
                        request.TargetUnitProfileId,
                        source.PositionUnitProfileId) ||
                    !StringComparer.Ordinal.Equals(
                        request.RangeUnitProfileId,
                        source.PositionUnitProfileId))
                {
                    diagnostics.Add(Error(
                        "AL-TARGETING-INVALID-SELF-INTENT",
                        "request.intent",
                        "Self intent requires the Self team rule and no secondary target identity.",
                        request,
                        source.ParticipantId));
                    return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRequest, diagnostics);
                }

                string[] resolvedSelf =
                    hitLedger.ParticipantIds.Contains(
                        source.ParticipantId,
                        StringComparer.Ordinal)
                        ? new string[0]
                        : new[] { source.ParticipantId };
                return CompletedPlan(
                    resolvedSelf.Length == 0
                        ? ParticipantTargetPlanStatus.ResolvedNoTargets
                        : ParticipantTargetPlanStatus.Resolved,
                    resolvedSelf,
                    new ParticipantTargetCandidateReceipt[0],
                    true,
                    sourcePosition,
                    hitLedger,
                    diagnostics);
            }

            bool requiresRange =
                request.IntentKind == CombatTargetIntentKind.Point ||
                request.IntentKind == CombatTargetIntentKind.ParticipantId ||
                request.IntentKind == CombatTargetIntentKind.AreaProfile;
            FiniteCombatScalar maximumRange = default;
            if (requiresRange &&
                (!FiniteCombatScalar.TryCreate(
                     policy.MaximumRange,
                     CombatScalarKind.WorldDistance,
                     policy.RangeUnitProfileId,
                     false,
                     out maximumRange) ||
                 !StringComparer.Ordinal.Equals(
                     source.PositionUnitProfileId,
                     request.RangeUnitProfileId)))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-INVALID-RANGE",
                    "request.maximumRange",
                    "Target range is invalid or uses a different unit profile.",
                    request));
                return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRange, diagnostics);
            }

            if (request.IntentKind == CombatTargetIntentKind.Point)
            {
                if (!FiniteCombatVector3.TryCreate(
                        request.TargetX,
                        request.TargetY,
                        request.TargetZ,
                        request.TargetUnitProfileId,
                        out FiniteCombatVector3 vector) ||
                    !WithinWorldCoordinateLimits(vector) ||
                    !StringComparer.Ordinal.Equals(
                        request.TargetUnitProfileId,
                        request.RangeUnitProfileId) ||
                    request.TeamRule != CombatTargetTeamRule.Any ||
                    !string.IsNullOrEmpty(request.TargetParticipantId) ||
                    !string.IsNullOrEmpty(request.AreaProfileId) ||
                    request.Candidates.Count != 0 ||
                    request.RequireLineOfSight)
                {
                    diagnostics.Add(Error(
                        "AL-TARGETING-INVALID-VECTOR",
                        "request.targetVector",
                        "Point intent contains an invalid or ambiguous vector request.",
                        request));
                    return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRequest, diagnostics);
                }

                if (!WithinRange(
                        sourcePosition,
                        vector,
                        maximumRange.Value))
                {
                    diagnostics.Add(Error(
                        "AL-TARGETING-POINT-OUT-OF-RANGE",
                        "request.targetVector",
                        "Point intent lies outside the authoritative source range.",
                        request));
                    return Rejected(
                        ParticipantTargetPlanStatus.RejectedOutOfRange,
                        diagnostics);
                }

                return CompletedPlan(
                    ParticipantTargetPlanStatus.ResolvedIntentOnly,
                    new string[0],
                    new ParticipantTargetCandidateReceipt[0],
                    true,
                    vector,
                    hitLedger,
                    diagnostics);
            }

            // Direction is a normalized-intent handoff only. It deliberately has
            // no distance query, candidates, hit ledger, or secondary identity.
            if (request.IntentKind == CombatTargetIntentKind.Direction)
            {
                if (!FiniteCombatVector3.TryCreate(
                        request.TargetX,
                        request.TargetY,
                        request.TargetZ,
                        request.TargetUnitProfileId,
                        out FiniteCombatVector3 vector) ||
                    !WithinWorldCoordinateLimits(vector) ||
                    IsZero(vector) ||
                    request.TeamRule != CombatTargetTeamRule.Any ||
                    !string.IsNullOrEmpty(request.TargetParticipantId) ||
                    !string.IsNullOrEmpty(request.AreaProfileId) ||
                    request.MaximumRange != 0f ||
                    request.Candidates.Count != 0 ||
                    request.RequireLineOfSight ||
                    !StringComparer.Ordinal.Equals(
                        request.TargetUnitProfileId,
                        request.RangeUnitProfileId))
                {
                    diagnostics.Add(Error(
                        "AL-TARGETING-INVALID-VECTOR",
                        "request.targetVector",
                        "Direction intent contains an invalid or ambiguous vector request.",
                        request));
                    return Rejected(
                        ParticipantTargetPlanStatus.RejectedInvalidRequest,
                        diagnostics);
                }

                return CompletedPlan(
                    ParticipantTargetPlanStatus.ResolvedIntentOnly,
                    new string[0],
                    new ParticipantTargetCandidateReceipt[0],
                    true,
                    vector,
                    hitLedger,
                    diagnostics);
            }

            if (request.IntentKind == CombatTargetIntentKind.ParticipantId &&
                !CombatPrimitiveValidation.IsStableId(request.TargetParticipantId))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-INVALID-PARTICIPANT-ID",
                    "request.targetParticipantId",
                    "Participant target intent requires one valid stable participant ID.",
                    request));
                return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRequest, diagnostics);
            }

            if (request.IntentKind == CombatTargetIntentKind.ParticipantId &&
                (!string.IsNullOrEmpty(request.AreaProfileId) ||
                 request.TargetX != 0f ||
                 request.TargetY != 0f ||
                 request.TargetZ != 0f))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-AMBIGUOUS-PARTICIPANT-INTENT",
                    "request.targetVector",
                    "Participant intent cannot carry an area or unused vector payload.",
                    request));
                return Rejected(
                    ParticipantTargetPlanStatus.RejectedInvalidRequest,
                    diagnostics);
            }

            if (request.IntentKind == CombatTargetIntentKind.AreaProfile &&
                !CombatPrimitiveValidation.IsStableId(request.AreaProfileId))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-INVALID-AREA-PROFILE",
                    "request.areaProfileId",
                    "Area target intent requires one valid stable area profile ID.",
                    request));
                return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRequest, diagnostics);
            }

            if (request.IntentKind == CombatTargetIntentKind.AreaProfile &&
                (!string.IsNullOrEmpty(request.TargetParticipantId) ||
                 request.TargetX != 0f ||
                 request.TargetY != 0f ||
                 request.TargetZ != 0f))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-AMBIGUOUS-AREA-INTENT",
                    "request.targetVector",
                    "Source-centered area intent cannot carry a participant or unused vector payload.",
                    request));
                return Rejected(
                    ParticipantTargetPlanStatus.RejectedInvalidRequest,
                    diagnostics);
            }

            var hitParticipants =
                new HashSet<string>(StringComparer.Ordinal);
            foreach (string participantId in request.ActionHitLedgerParticipantIds)
            {
                if (!CombatPrimitiveValidation.IsStableId(participantId) ||
                    !hitParticipants.Add(participantId))
                {
                    diagnostics.Add(Error(
                        "AL-TARGETING-INVALID-HIT-LEDGER",
                        "request.actionHitLedgerParticipantIds",
                        "Action hit ledger contains an invalid or duplicate participant ID.",
                        request,
                        participantId));
                }
            }

            if (diagnostics.Count != 0)
            {
                return Rejected(ParticipantTargetPlanStatus.RejectedInvalidRequest, diagnostics);
            }

            Dictionary<string, CombatParticipantRegistration> byHandle =
                participants.ToDictionary(
                    participant => participant.RuntimeHandleId,
                    StringComparer.Ordinal);
            var accepted = new HashSet<string>(StringComparer.Ordinal);
            var receipts = new List<ParticipantTargetCandidateReceipt>();

            for (int index = 0; index < request.Candidates.Count; index++)
            {
                FakeTargetHandleObservation candidate = request.Candidates[index];
                if (candidate == null)
                {
                    receipts.Add(new ParticipantTargetCandidateReceipt(
                        index,
                        string.Empty,
                        string.Empty,
                        ParticipantTargetCandidateStatus.RejectedNullCandidate));
                    continue;
                }

                if (!byHandle.TryGetValue(
                        candidate.RuntimeHandleId,
                        out CombatParticipantRegistration participant))
                {
                    receipts.Add(new ParticipantTargetCandidateReceipt(
                        index,
                        candidate.RuntimeHandleId,
                        string.Empty,
                        ParticipantTargetCandidateStatus.RejectedUnknownHandle));
                    continue;
                }

                ParticipantTargetCandidateStatus candidateStatus = ValidateCandidate(
                    source,
                    participant,
                    candidate,
                    request,
                    sourcePosition,
                    maximumRange,
                    policy.RequiredParticipantTargetingProfileId,
                    hitParticipants,
                    accepted);
                receipts.Add(new ParticipantTargetCandidateReceipt(
                    index,
                    candidate.RuntimeHandleId,
                    participant.ParticipantId,
                    candidateStatus));
                if (candidateStatus == ParticipantTargetCandidateStatus.Accepted)
                {
                    accepted.Add(participant.ParticipantId);
                }
            }

            string[] resolvedIds = accepted
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return CompletedPlan(
                resolvedIds.Length == 0
                    ? ParticipantTargetPlanStatus.ResolvedNoTargets
                    : ParticipantTargetPlanStatus.Resolved,
                resolvedIds,
                receipts,
                false,
                default,
                hitLedger,
                diagnostics);
        }

        private static bool ValidateRequestIdentity(
            ParticipantTargetRequest request,
            ICollection<CombatDiagnostic> diagnostics)
        {
            bool valid = true;
            valid &= RequireId(request.ActionId, "request.actionId", request, diagnostics);
            valid &= RequireId(
                request.EncounterSessionId,
                "request.encounterSessionId",
                request,
                diagnostics);
            valid &= RequireId(
                request.EncounterAttemptId,
                "request.encounterAttemptId",
                request,
                diagnostics);
            valid &= RequireId(
                request.SourceParticipantId,
                "request.sourceParticipantId",
                request,
                diagnostics);
            valid &= RequireId(
                request.SkillDefinitionId,
                "request.skillDefinitionId",
                request,
                diagnostics);
            if (!CombatPrimitiveValidation.IsVersion(
                    request.SkillContentVersion))
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-INVALID-SKILL-VERSION",
                    "request.skillContentVersion",
                    "Target request contains an invalid skill content version.",
                    request));
                valid = false;
            }

            valid &= RequireId(
                request.ExpectedActionRevision,
                "request.expectedActionRevision",
                request,
                diagnostics);
            return valid;
        }

        private static bool ValidateRegistry(
            IList<CombatParticipantRegistration> participants,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (participants.Count == 0)
            {
                diagnostics.Add(Error(
                    "AL-TARGETING-EMPTY-REGISTRY",
                    "participantRegistry",
                    "Participant registry is empty."));
                return false;
            }

            bool valid = true;
            var participantIds = new HashSet<string>(StringComparer.Ordinal);
            var handleIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < participants.Count; index++)
            {
                CombatParticipantRegistration participant = participants[index];
                if (participant == null)
                {
                    diagnostics.Add(Error(
                        "AL-TARGETING-NULL-PARTICIPANT",
                        "participantRegistry[" + index + "]",
                        "Participant registry contains a null row."));
                    valid = false;
                    continue;
                }

                if (!CombatPrimitiveValidation.IsStableId(participant.ParticipantId) ||
                    !CombatPrimitiveValidation.IsStableId(participant.ActorProfileId) ||
                    !CombatPrimitiveValidation.IsStableId(participant.TeamId) ||
                    !CombatPrimitiveValidation.IsStableId(participant.RealmContextId) ||
                    !CombatPrimitiveValidation.IsStableId(participant.RuntimeHandleId) ||
                    !CombatPrimitiveValidation.IsStableId(participant.TargetingProfileId) ||
                    !CombatPrimitiveValidation.IsStableId(participant.EncounterSessionId) ||
                    !CombatPrimitiveValidation.IsStableId(participant.EncounterAttemptId) ||
                    participant.RuntimeHandleGeneration < 0L ||
                    !Enum.IsDefined(typeof(CombatParticipantRole), participant.Role) ||
                    !Enum.IsDefined(typeof(CombatantLifeState), participant.LifeState) ||
                    !Enum.IsDefined(typeof(CombatantControlState), participant.ControlState) ||
                    !IsCoherentLifeAndControl(
                        participant.LifeState,
                        participant.ControlState) ||
                    !IsCoherentActionLockOwner(
                        participant.ControlState,
                        participant.ActionLockOwnerActionId) ||
                    !TryPosition(participant, out _))
                {
                    diagnostics.Add(Error(
                        "AL-TARGETING-INVALID-PARTICIPANT",
                        "participantRegistry[" + index + "]",
                        "Participant registry row is malformed.",
                        null,
                        participant.ParticipantId));
                    valid = false;
                }

                if (!participantIds.Add(participant.ParticipantId))
                {
                    diagnostics.Add(Error(
                        "AL-TARGETING-DUPLICATE-PARTICIPANT",
                        "participantRegistry[" + index + "].participantId",
                        "Participant stable ID is duplicated.",
                        null,
                        participant.ParticipantId));
                    valid = false;
                }

                if (!handleIds.Add(participant.RuntimeHandleId))
                {
                    diagnostics.Add(Error(
                        "AL-TARGETING-DUPLICATE-HANDLE",
                        "participantRegistry[" + index + "].runtimeHandleId",
                        "Runtime handle identity is duplicated.",
                        null,
                        participant.ParticipantId));
                    valid = false;
                }
            }

            return valid;
        }

        private static ParticipantTargetCandidateStatus ValidateCandidate(
            CombatParticipantRegistration source,
            CombatParticipantRegistration participant,
            FakeTargetHandleObservation candidate,
            ParticipantTargetRequest request,
            FiniteCombatVector3 sourcePosition,
            FiniteCombatScalar maximumRange,
            string requiredParticipantTargetingProfileId,
            ISet<string> hitLedger,
            ISet<string> accepted)
        {
            if (candidate.RuntimeHandleGeneration !=
                participant.RuntimeHandleGeneration)
            {
                return ParticipantTargetCandidateStatus.RejectedStaleHandleGeneration;
            }

            if (!StringComparer.Ordinal.Equals(
                    participant.EncounterSessionId,
                    request.EncounterSessionId))
            {
                return ParticipantTargetCandidateStatus.RejectedWrongEncounter;
            }

            if (!StringComparer.Ordinal.Equals(
                    participant.EncounterAttemptId,
                    request.EncounterAttemptId))
            {
                return ParticipantTargetCandidateStatus.RejectedWrongAttempt;
            }

            if (!StringComparer.Ordinal.Equals(
                    participant.TargetingProfileId,
                    requiredParticipantTargetingProfileId))
            {
                return ParticipantTargetCandidateStatus
                    .RejectedTargetingProfile;
            }

            if (participant.LifeState != CombatantLifeState.Alive ||
                !participant.IsTargetEligible)
            {
                return ParticipantTargetCandidateStatus.RejectedIneligibleLifeState;
            }

            if (request.IntentKind == CombatTargetIntentKind.ParticipantId &&
                !StringComparer.Ordinal.Equals(
                    participant.ParticipantId,
                    request.TargetParticipantId))
            {
                return ParticipantTargetCandidateStatus.RejectedDifferentParticipant;
            }

            if (!AllowsTeam(source, participant, request.TeamRule))
            {
                return ParticipantTargetCandidateStatus.RejectedTeamRule;
            }

            if (!FiniteCombatVector3.TryCreate(
                    candidate.ObservedX,
                    candidate.ObservedY,
                    candidate.ObservedZ,
                    candidate.PositionUnitProfileId,
                    out FiniteCombatVector3 observedPosition) ||
                !WithinWorldCoordinateLimits(observedPosition) ||
                !StringComparer.Ordinal.Equals(
                    candidate.PositionUnitProfileId,
                    request.RangeUnitProfileId))
            {
                return ParticipantTargetCandidateStatus.RejectedInvalidPosition;
            }

            // Collider observations are discovery evidence only. Range is measured
            // to the immutable registered participant position so a spoofed child
            // collider cannot extend range and multi-collider actors remain equal.
            if (!TryPosition(participant, out FiniteCombatVector3 participantPosition) ||
                !StringComparer.Ordinal.Equals(
                    participant.PositionUnitProfileId,
                    request.RangeUnitProfileId))
            {
                return ParticipantTargetCandidateStatus.RejectedInvalidPosition;
            }

            if (!WithinRange(
                    sourcePosition,
                    participantPosition,
                    maximumRange.Value))
            {
                return ParticipantTargetCandidateStatus.RejectedOutOfRange;
            }

            if (request.RequireLineOfSight && !candidate.HasLineOfSight)
            {
                return ParticipantTargetCandidateStatus.RejectedLineOfSight;
            }

            if (hitLedger.Contains(participant.ParticipantId))
            {
                return ParticipantTargetCandidateStatus.RejectedAlreadyHit;
            }

            if (accepted.Contains(participant.ParticipantId))
            {
                return ParticipantTargetCandidateStatus.DuplicateParticipantSuppressed;
            }

            return ParticipantTargetCandidateStatus.Accepted;
        }

        private static bool AllowsTeam(
            CombatParticipantRegistration source,
            CombatParticipantRegistration candidate,
            CombatTargetTeamRule rule)
        {
            bool self = StringComparer.Ordinal.Equals(
                source.ParticipantId,
                candidate.ParticipantId);
            bool sameTeam = StringComparer.Ordinal.Equals(
                source.TeamId,
                candidate.TeamId);
            switch (rule)
            {
                case CombatTargetTeamRule.Self:
                    return self;
                case CombatTargetTeamRule.Ally:
                    return !self && sameTeam;
                case CombatTargetTeamRule.Enemy:
                    return !sameTeam;
                case CombatTargetTeamRule.Any:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsSourceActionEligible(
            CombatantControlState state,
            string actionLockOwnerActionId,
            CombatActionSource source,
            string requestActionId)
        {
            if (state == CombatantControlState.ActionLocked)
            {
                return StringComparer.Ordinal.Equals(
                    actionLockOwnerActionId,
                    requestActionId);
            }

            switch (source)
            {
                case CombatActionSource.ManualInput:
                    return state == CombatantControlState.Manual;
                case CombatActionSource.AssistAI:
                    return state == CombatantControlState.Assist;
                case CombatActionSource.FullAutoAI:
                    return state == CombatantControlState.Auto;
                case CombatActionSource.EncounterScript:
                    return state == CombatantControlState.EncounterLocked;
                default:
                    return false;
            }
        }

        private static bool IsDispositionCompatible(
            CombatTargetDisposition disposition,
            CombatTargetIntentKind intent,
            CombatTargetTeamRule teamRule)
        {
            switch (disposition)
            {
                case CombatTargetDisposition.Self:
                    return intent == CombatTargetIntentKind.Self &&
                           teamRule == CombatTargetTeamRule.Self;
                case CombatTargetDisposition.Friendly:
                    return intent != CombatTargetIntentKind.Self &&
                           teamRule == CombatTargetTeamRule.Ally;
                case CombatTargetDisposition.Hostile:
                    return intent != CombatTargetIntentKind.Self &&
                           teamRule == CombatTargetTeamRule.Enemy;
                case CombatTargetDisposition.Any:
                    return intent != CombatTargetIntentKind.Self &&
                           teamRule == CombatTargetTeamRule.Any;
                default:
                    return false;
            }
        }

        private static bool ValidateTargetPolicy(
            CombatActionTargetPolicySnapshot policy)
        {
            return policy != null &&
                   CombatPrimitiveValidation.IsStableId(
                       policy.ActionId) &&
                   CombatPrimitiveValidation.IsStableId(
                       policy.EncounterSessionId) &&
                   CombatPrimitiveValidation.IsStableId(
                       policy.EncounterAttemptId) &&
                   CombatPrimitiveValidation.IsStableId(
                       policy.ActorParticipantId) &&
                   CombatPrimitiveValidation.IsStableId(
                       policy.SkillDefinitionId) &&
                   CombatPrimitiveValidation.IsVersion(
                       policy.SkillContentVersion) &&
                   CombatPrimitiveValidation.IsStableId(
                       policy.LoadoutId) &&
                   CombatPrimitiveValidation.IsStableId(
                       policy.LoadoutOwnerProfileId) &&
                   CombatPrimitiveValidation.IsStableId(
                       policy.CatalogSetId) &&
                   CombatPrimitiveValidation.IsStableId(
                       policy.TargetingProfileId) &&
                   Enum.IsDefined(
                       typeof(CombatTargetIntentKind),
                       policy.AllowedIntentKind) &&
                   CombatPrimitiveValidation.IsStableId(
                       policy.RangeUnitProfileId) &&
                   CombatTargetIntentCompatibility
                       .IsSupportedReference(
                           policy.Disposition,
                           policy.AllowedIntentKind,
                           policy.RequiredAreaProfileId,
                           policy.RequiresLineOfSight,
                           policy.RequiredParticipantTargetingProfileId) &&
                   CombatPrimitiveValidation.IsMicrosInRange(
                       policy.MaximumRangeMicros,
                       CombatScalarKind.WorldDistance,
                       false) &&
                   (!CombatTargetIntentCompatibility
                        .RequiresZeroRange(
                            policy.AllowedIntentKind) ||
                    policy.MaximumRangeMicros == 0L) &&
                   CombatPrimitiveValidation.IsSha256(
                       policy.TrustedSkillRawSha256) &&
                   CombatPrimitiveValidation.IsStableId(
                       policy.ActionRevision) &&
                   Enum.IsDefined(
                       typeof(CombatActionSource),
                       policy.ActionSource) &&
                   policy.Disposition !=
                       CombatTargetDisposition.Unknown;
        }

        private static bool ValidateCurrentAction(
            CombatActionSnapshot currentAction,
            CombatActionTargetPolicySnapshot policy,
            ParticipantTargetRequest request)
        {
            CombatActionRequest actionRequest =
                currentAction?.Request;
            return actionRequest != null &&
                   !currentAction.IsTerminal &&
                   currentAction.State ==
                       CombatActionState.Resolving &&
                   CombatPrimitiveValidation.IsStableId(
                       currentAction.Revision) &&
                   StringComparer.Ordinal.Equals(
                       currentAction.Revision,
                       request.ExpectedActionRevision) &&
                   StringComparer.Ordinal.Equals(
                       actionRequest.ActionId.Value,
                       policy.ActionId) &&
                   StringComparer.Ordinal.Equals(
                       actionRequest.ActionId.Value,
                       request.ActionId) &&
                   StringComparer.Ordinal.Equals(
                       actionRequest.EncounterSessionId.Value,
                       request.EncounterSessionId) &&
                   StringComparer.Ordinal.Equals(
                       actionRequest.EncounterAttemptId.Value,
                       request.EncounterAttemptId) &&
                   StringComparer.Ordinal.Equals(
                       actionRequest.ActorParticipantId.Value,
                       request.SourceParticipantId) &&
                   StringComparer.Ordinal.Equals(
                       actionRequest.BehaviorOrSkillId.Value,
                       request.SkillDefinitionId) &&
                   StringComparer.Ordinal.Equals(
                       actionRequest.SkillContentVersion.Value,
                       request.SkillContentVersion) &&
                   actionRequest.Source == request.ActionSource;
        }

        private static bool ValidateCurrentEncounter(
            ChampionEncounterStateSnapshot currentEncounter,
            CombatActionTargetPolicySnapshot policy,
            ParticipantTargetRequest request)
        {
            return currentEncounter != null &&
                   currentEncounter.State ==
                       CombatEncounterState.Active &&
                   !currentEncounter.IsTerminal &&
                   currentEncounter.TerminalOutcome ==
                       ChampionEncounterTerminalOutcome.None &&
                   currentEncounter.FrozenOutcome == null &&
                   currentEncounter.Revision >= 0L &&
                   currentEncounter.EncounterElapsedMicros >= 0L &&
                   CombatPrimitiveValidation.IsSha256(
                       currentEncounter.SourceSnapshotHash) &&
                   StringComparer.Ordinal.Equals(
                       currentEncounter.EncounterSessionId,
                       request.EncounterSessionId) &&
                   StringComparer.Ordinal.Equals(
                       currentEncounter.EncounterAttemptId,
                       request.EncounterAttemptId);
        }

        private static bool IsCoherentActionLockOwner(
            CombatantControlState controlState,
            string actionLockOwnerActionId)
        {
            return controlState == CombatantControlState.ActionLocked
                ? CombatPrimitiveValidation.IsStableId(
                    actionLockOwnerActionId)
                : string.IsNullOrEmpty(actionLockOwnerActionId);
        }

        private static bool IsCoherentLifeAndControl(
            CombatantLifeState lifeState,
            CombatantControlState controlState)
        {
            switch (lifeState)
            {
                case CombatantLifeState.Alive:
                    return controlState != CombatantControlState.Defeated &&
                           controlState != CombatantControlState.Disposed;
                case CombatantLifeState.Defeated:
                    return controlState == CombatantControlState.Defeated;
                case CombatantLifeState.Disposed:
                    return controlState == CombatantControlState.Disposed;
                default:
                    return false;
            }
        }

        private static bool WithinRange(
            FiniteCombatVector3 source,
            FiniteCombatVector3 target,
            float maximumRange)
        {
            double deltaX = (double)target.X - source.X;
            double deltaY = (double)target.Y - source.Y;
            double deltaZ = (double)target.Z - source.Z;
            double distanceSquared =
                (deltaX * deltaX) +
                (deltaY * deltaY) +
                (deltaZ * deltaZ);
            double maximumSquared = (double)maximumRange * maximumRange;
            return CombatPrimitiveValidation.IsFinite(distanceSquared) &&
                   distanceSquared <= maximumSquared;
        }

        private static bool TryPosition(
            CombatParticipantRegistration participant,
            out FiniteCombatVector3 position)
        {
            return FiniteCombatVector3.TryCreate(
                       participant.PositionX,
                       participant.PositionY,
                       participant.PositionZ,
                       participant.PositionUnitProfileId,
                       out position) &&
                   WithinWorldCoordinateLimits(position);
        }

        private static bool WithinWorldCoordinateLimits(
            FiniteCombatVector3 value)
        {
            float maximum = CombatPrimitiveValidation.MaximumUnits(
                CombatScalarKind.WorldDistance);
            return Math.Abs(value.X) <= maximum &&
                   Math.Abs(value.Y) <= maximum &&
                   Math.Abs(value.Z) <= maximum;
        }

        private static bool IsZero(FiniteCombatVector3 value)
        {
            return value.X == 0f && value.Y == 0f && value.Z == 0f;
        }

        private static ParticipantTargetPlan CompletedPlan(
            ParticipantTargetPlanStatus status,
            IEnumerable<string> resolvedParticipantIds,
            IEnumerable<ParticipantTargetCandidateReceipt> candidateReceipts,
            bool hasResolvedVector,
            FiniteCombatVector3 resolvedVector,
            CombatActionHitLedgerSnapshot beforeHitLedger,
            IEnumerable<CombatDiagnostic> diagnostics)
        {
            string[] addedParticipantIds = (
                    resolvedParticipantIds ??
                    Enumerable.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (addedParticipantIds.Length != 0 &&
                (beforeHitLedger.Revision == long.MaxValue ||
                 beforeHitLedger.ParticipantIds.Count >
                    CombatTargetingTechnicalLimits
                        .MaximumActionHitLedgerEntries -
                    addedParticipantIds.Length))
            {
                var failureDiagnostics = (
                        diagnostics ??
                        Enumerable.Empty<CombatDiagnostic>())
                    .ToList();
                failureDiagnostics.Add(Error(
                    "AL-TARGETING-HIT-LEDGER-CAPACITY",
                    "hitLedger",
                    "Action hit ledger cannot accept another participant without explicit recovery."));
                return Rejected(
                    ParticipantTargetPlanStatus
                        .RejectedHitLedgerCapacity,
                    failureDiagnostics);
            }

            CombatActionHitLedgerSnapshot afterHitLedger =
                beforeHitLedger;
            if (addedParticipantIds.Length != 0)
            {
                string[] afterParticipantIds = beforeHitLedger
                    .ParticipantIds
                    .Concat(addedParticipantIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                long afterRevision = beforeHitLedger.Revision + 1L;
                afterHitLedger = new CombatActionHitLedgerSnapshot(
                    beforeHitLedger.ActionId,
                    beforeHitLedger.EncounterSessionId,
                    beforeHitLedger.EncounterAttemptId,
                    beforeHitLedger.ActionRevision,
                    afterRevision,
                    afterParticipantIds,
                    ComputeHitLedgerHash(
                        beforeHitLedger.ActionId,
                        beforeHitLedger.EncounterSessionId,
                        beforeHitLedger.EncounterAttemptId,
                        beforeHitLedger.ActionRevision,
                        afterRevision,
                        afterParticipantIds));
            }

            var receipt = new ParticipantTargetHitLedgerReceipt(
                beforeHitLedger.ActionId,
                beforeHitLedger.EncounterSessionId,
                beforeHitLedger.EncounterAttemptId,
                beforeHitLedger.ActionRevision,
                beforeHitLedger.Revision,
                afterHitLedger.Revision,
                beforeHitLedger.StateHash,
                afterHitLedger.StateHash,
                addedParticipantIds);
            return new ParticipantTargetPlan(
                status,
                addedParticipantIds,
                candidateReceipts,
                hasResolvedVector,
                resolvedVector,
                beforeHitLedger,
                afterHitLedger,
                receipt,
                diagnostics);
        }

        private static bool ValidateHitLedger(
            CombatActionHitLedgerSnapshot hitLedger,
            CombatActionTargetPolicySnapshot policy)
        {
            if (hitLedger == null ||
                hitLedger.Revision < 0L ||
                hitLedger.ParticipantIds.Count >
                    CombatTargetingTechnicalLimits
                        .MaximumActionHitLedgerEntries ||
                !CombatPrimitiveValidation.IsStableId(
                    hitLedger.ActionId) ||
                !CombatPrimitiveValidation.IsStableId(
                    hitLedger.EncounterSessionId) ||
                !CombatPrimitiveValidation.IsStableId(
                    hitLedger.EncounterAttemptId) ||
                !CombatPrimitiveValidation.IsStableId(
                    hitLedger.ActionRevision) ||
                !CombatPrimitiveValidation.IsSha256(
                    hitLedger.StateHash) ||
                !StringComparer.Ordinal.Equals(
                    hitLedger.ActionId,
                    policy.ActionId) ||
                !StringComparer.Ordinal.Equals(
                    hitLedger.EncounterSessionId,
                    policy.EncounterSessionId) ||
                !StringComparer.Ordinal.Equals(
                    hitLedger.EncounterAttemptId,
                    policy.EncounterAttemptId) ||
                !StringComparer.Ordinal.Equals(
                    hitLedger.ActionRevision,
                    policy.ActionRevision))
            {
                return false;
            }

            string previous = null;
            foreach (string participantId in hitLedger.ParticipantIds)
            {
                if (!CombatPrimitiveValidation.IsStableId(participantId) ||
                    (previous != null &&
                     StringComparer.Ordinal.Compare(
                         previous,
                         participantId) >= 0))
                {
                    return false;
                }

                previous = participantId;
            }

            string expectedHash = ComputeHitLedgerHash(
                hitLedger.ActionId,
                hitLedger.EncounterSessionId,
                hitLedger.EncounterAttemptId,
                hitLedger.ActionRevision,
                hitLedger.Revision,
                hitLedger.ParticipantIds);
            return StringComparer.Ordinal.Equals(
                expectedHash,
                hitLedger.StateHash);
        }

        private static string ComputeHitLedgerHash(
            string actionId,
            string encounterSessionId,
            string encounterAttemptId,
            string actionRevision,
            long revision,
            IEnumerable<string> participantIds)
        {
            var builder = new StringBuilder();
            AppendHashField(builder, PolicyVersion);
            AppendHashField(builder, actionId);
            AppendHashField(builder, encounterSessionId);
            AppendHashField(builder, encounterAttemptId);
            AppendHashField(builder, actionRevision);
            AppendHashField(
                builder,
                revision.ToString(CultureInfo.InvariantCulture));
            foreach (string participantId in
                     participantIds ?? Enumerable.Empty<string>())
            {
                AppendHashField(builder, participantId);
            }

            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(builder.ToString()));
            }

            var hex = new StringBuilder(
                CombatTechnicalLimits.Sha256HexCharacters);
            foreach (byte value in digest)
            {
                hex.Append(value.ToString(
                    "x2",
                    CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }

        private static void AppendHashField(
            StringBuilder builder,
            string raw)
        {
            string value = raw ?? string.Empty;
            builder
                .Append(value.Length.ToString(
                    CultureInfo.InvariantCulture))
                .Append(':')
                .Append(value);
        }

        private static bool RequireId(
            string value,
            string field,
            ParticipantTargetRequest request,
            ICollection<CombatDiagnostic> diagnostics)
        {
            if (CombatPrimitiveValidation.IsStableId(value))
            {
                return true;
            }

            diagnostics.Add(Error(
                "AL-TARGETING-INVALID-ID",
                field,
                "Target request contains an invalid stable ID.",
                request));
            return false;
        }

        private static ParticipantTargetPlan Rejected(
            ParticipantTargetPlanStatus status,
            IEnumerable<CombatDiagnostic> diagnostics)
        {
            return new ParticipantTargetPlan(
                status,
                new string[0],
                new ParticipantTargetCandidateReceipt[0],
                false,
                default,
                null,
                null,
                null,
                diagnostics);
        }

        private static CombatDiagnostic Error(
            string code,
            string field,
            string message,
            ParticipantTargetRequest request = null,
            string participantId = "")
        {
            return new CombatDiagnostic(
                code,
                CombatDiagnosticSeverity.Error,
                CombatDiagnosticDomain.Targeting,
                field,
                message,
                CombatBlockScope.Action,
                encounterSessionId: request?.EncounterSessionId ?? string.Empty,
                encounterAttemptId: request?.EncounterAttemptId ?? string.Empty,
                actionId: request?.ActionId ?? string.Empty,
                participantId: participantId ?? string.Empty,
                policyVersion: PolicyVersion);
        }
    }
}
