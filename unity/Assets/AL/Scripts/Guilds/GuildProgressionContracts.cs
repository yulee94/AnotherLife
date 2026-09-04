using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Guilds
{
    public enum GuildProgressionOperation
    {
        AdvanceLevel,
        CompleteResearch
    }

    public enum GuildPerkScope
    {
        MemberCharacterStats
    }

    public sealed class GuildProgressionLevelDefinition
    {
        public GuildProgressionLevelDefinition(string levelId, int ordinal, bool productionEligible)
        {
            LevelId = levelId ?? string.Empty;
            Ordinal = ordinal;
            ProductionEligible = productionEligible;
        }

        public string LevelId { get; }
        public int Ordinal { get; }
        public bool ProductionEligible { get; }
    }

    public sealed class GuildResearchDefinition
    {
        public GuildResearchDefinition(
            string researchId,
            string requiredLevelId,
            IEnumerable<string> requiredResearchIds,
            bool productionEligible)
        {
            ResearchId = researchId ?? string.Empty;
            RequiredLevelId = requiredLevelId ?? string.Empty;
            RequiredResearchIds = requiredResearchIds == null
                ? null
                : Array.AsReadOnly(requiredResearchIds.ToArray());
            ProductionEligible = productionEligible;
        }

        public string ResearchId { get; }
        public string RequiredLevelId { get; }
        public IReadOnlyList<string> RequiredResearchIds { get; }
        public bool ProductionEligible { get; }
    }

    public sealed class GuildPerkCap
    {
        public GuildPerkCap(string kind, bool productionEligible)
        {
            Kind = kind ?? string.Empty;
            ProductionEligible = productionEligible;
        }

        public string Kind { get; }
        public bool ProductionEligible { get; }
    }

    public sealed class GuildPerkStacking
    {
        public GuildPerkStacking(string group, int order, string rule)
        {
            Group = group ?? string.Empty;
            Order = order;
            Rule = rule ?? string.Empty;
        }

        public string Group { get; }
        public int Order { get; }
        public string Rule { get; }
    }

    public sealed class GuildPerkDefinition
    {
        public GuildPerkDefinition(
            string sourceId,
            string profileId,
            string ruleId,
            string requiredLevelId,
            string requiredResearchId,
            GuildPerkScope scope,
            GuildPerkCap cap,
            GuildPerkStacking stacking,
            string sourceVersion,
            string sourceHash,
            string statBreakdownToken,
            bool hiddenGlobalMultiplier,
            bool productionEligible,
            bool appliesCombatMutation)
        {
            SourceId = sourceId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            RuleId = ruleId ?? string.Empty;
            RequiredLevelId = requiredLevelId ?? string.Empty;
            RequiredResearchId = requiredResearchId ?? string.Empty;
            Scope = scope;
            Cap = cap;
            Stacking = stacking;
            SourceVersion = sourceVersion ?? string.Empty;
            SourceHash = sourceHash ?? string.Empty;
            StatBreakdownToken = statBreakdownToken ?? string.Empty;
            HiddenGlobalMultiplier = hiddenGlobalMultiplier;
            ProductionEligible = productionEligible;
            AppliesCombatMutation = appliesCombatMutation;
        }

        public string SourceId { get; }
        public string ProfileId { get; }
        public string RuleId { get; }
        public string RequiredLevelId { get; }
        public string RequiredResearchId { get; }
        public GuildPerkScope Scope { get; }
        public GuildPerkCap Cap { get; }
        public GuildPerkStacking Stacking { get; }
        public string SourceVersion { get; }
        public string SourceHash { get; }
        public string StatBreakdownToken { get; }
        public bool HiddenGlobalMultiplier { get; }
        public bool ProductionEligible { get; }
        public bool AppliesCombatMutation { get; }
    }

    public sealed class GuildProgressionPolicySnapshot
    {
        public GuildProgressionPolicySnapshot(
            GuildCatalogStatus status,
            GuildCatalogBinding binding,
            bool levelCapSelected,
            bool researchTreeSelected,
            bool costsSelected,
            bool numericPerkTuningProductionEligible,
            bool masterOnlyAuthority,
            IEnumerable<GuildProgressionLevelDefinition> levels,
            IEnumerable<GuildResearchDefinition> research,
            IEnumerable<GuildPerkDefinition> perks,
            bool hiddenGlobalMultipliersForbidden,
            bool isComplete)
        {
            Status = status;
            Binding = binding;
            LevelCapSelected = levelCapSelected;
            ResearchTreeSelected = researchTreeSelected;
            CostsSelected = costsSelected;
            NumericPerkTuningProductionEligible = numericPerkTuningProductionEligible;
            MasterOnlyAuthority = masterOnlyAuthority;
            Levels = levels == null ? null : Array.AsReadOnly(levels.ToArray());
            Research = research == null ? null : Array.AsReadOnly(research.ToArray());
            Perks = perks == null ? null : Array.AsReadOnly(perks.ToArray());
            HiddenGlobalMultipliersForbidden = hiddenGlobalMultipliersForbidden;
            IsComplete = isComplete;
        }

        public GuildCatalogStatus Status { get; }
        public GuildCatalogBinding Binding { get; }
        public bool LevelCapSelected { get; }
        public bool ResearchTreeSelected { get; }
        public bool CostsSelected { get; }
        public bool NumericPerkTuningProductionEligible { get; }
        public bool MasterOnlyAuthority { get; }
        public IReadOnlyList<GuildProgressionLevelDefinition> Levels { get; }
        public IReadOnlyList<GuildResearchDefinition> Research { get; }
        public IReadOnlyList<GuildPerkDefinition> Perks { get; }
        public bool HiddenGlobalMultipliersForbidden { get; }
        public bool IsComplete { get; }
    }

    public sealed class GuildProgressionReceipt
    {
        public GuildProgressionReceipt(
            string operationId,
            GuildProgressionOperation operation,
            string requestFingerprint,
            string guildId,
            string actorAccountId,
            string targetLevelId,
            string targetResearchId,
            long resultingRevision,
            string planHash,
            bool isSupported)
        {
            OperationId = operationId ?? string.Empty;
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            GuildId = guildId ?? string.Empty;
            ActorAccountId = actorAccountId ?? string.Empty;
            TargetLevelId = targetLevelId ?? string.Empty;
            TargetResearchId = targetResearchId ?? string.Empty;
            ResultingRevision = resultingRevision;
            PlanHash = planHash ?? string.Empty;
            IsSupported = isSupported;
        }

        public string OperationId { get; }
        public GuildProgressionOperation Operation { get; }
        public string RequestFingerprint { get; }
        public string GuildId { get; }
        public string ActorAccountId { get; }
        public string TargetLevelId { get; }
        public string TargetResearchId { get; }
        public long ResultingRevision { get; }
        public string PlanHash { get; }
        public bool IsSupported { get; }
    }

    public sealed class GuildProgressionStateSnapshot
    {
        public GuildProgressionStateSnapshot(
            GuildAuthorityStatus status,
            string guildId,
            long revision,
            string currentLevelId,
            IEnumerable<string> completedResearchIds,
            IEnumerable<GuildProgressionReceipt> receipts,
            bool isComplete)
        {
            Status = status;
            GuildId = guildId ?? string.Empty;
            Revision = revision;
            CurrentLevelId = currentLevelId ?? string.Empty;
            CompletedResearchIds = completedResearchIds == null
                ? null
                : Array.AsReadOnly(completedResearchIds.ToArray());
            Receipts = receipts == null ? null : Array.AsReadOnly(receipts.ToArray());
            IsComplete = isComplete;
        }

        public GuildAuthorityStatus Status { get; }
        public string GuildId { get; }
        public long Revision { get; }
        public string CurrentLevelId { get; }
        public IReadOnlyList<string> CompletedResearchIds { get; }
        public IReadOnlyList<GuildProgressionReceipt> Receipts { get; }
        public bool IsComplete { get; }
    }

    public sealed class GuildMemberPerkProvenance
    {
        public GuildMemberPerkProvenance(
            string accountId,
            string sourceId,
            string profileId,
            string ruleId,
            string requiredLevelId,
            string requiredResearchId,
            GuildPerkScope scope,
            string capKind,
            string stackingGroup,
            int stackingOrder,
            string stackingRule,
            string sourceVersion,
            string sourceHash,
            string statBreakdownToken,
            bool productionEligible)
        {
            AccountId = accountId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            RuleId = ruleId ?? string.Empty;
            RequiredLevelId = requiredLevelId ?? string.Empty;
            RequiredResearchId = requiredResearchId ?? string.Empty;
            Scope = scope;
            CapKind = capKind ?? string.Empty;
            StackingGroup = stackingGroup ?? string.Empty;
            StackingOrder = stackingOrder;
            StackingRule = stackingRule ?? string.Empty;
            SourceVersion = sourceVersion ?? string.Empty;
            SourceHash = sourceHash ?? string.Empty;
            StatBreakdownToken = statBreakdownToken ?? string.Empty;
            ProductionEligible = productionEligible;
        }

        public string AccountId { get; }
        public string SourceId { get; }
        public string ProfileId { get; }
        public string RuleId { get; }
        public string RequiredLevelId { get; }
        public string RequiredResearchId { get; }
        public GuildPerkScope Scope { get; }
        public string CapKind { get; }
        public string StackingGroup { get; }
        public int StackingOrder { get; }
        public string StackingRule { get; }
        public string SourceVersion { get; }
        public string SourceHash { get; }
        public string StatBreakdownToken { get; }
        public bool ProductionEligible { get; }
    }

    public sealed class GuildProgressionTransitionRequest
    {
        public GuildProgressionTransitionRequest(
            GuildProgressionOperation operation,
            string operationId,
            string actorAccountId,
            string guildId,
            string targetLevelId,
            string targetResearchId,
            long expectedProgressionRevision,
            long expectedGuildRevision,
            GuildCatalogBinding expectedCatalogBinding)
        {
            Operation = operation;
            OperationId = operationId ?? string.Empty;
            ActorAccountId = actorAccountId ?? string.Empty;
            GuildId = guildId ?? string.Empty;
            TargetLevelId = targetLevelId ?? string.Empty;
            TargetResearchId = targetResearchId ?? string.Empty;
            ExpectedProgressionRevision = expectedProgressionRevision;
            ExpectedGuildRevision = expectedGuildRevision;
            ExpectedCatalogBinding = expectedCatalogBinding;
        }

        public GuildProgressionOperation Operation { get; }
        public string OperationId { get; }
        public string ActorAccountId { get; }
        public string GuildId { get; }
        public string TargetLevelId { get; }
        public string TargetResearchId { get; }
        public long ExpectedProgressionRevision { get; }
        public long ExpectedGuildRevision { get; }
        public GuildCatalogBinding ExpectedCatalogBinding { get; }
    }

    public sealed class GuildProgressionTransitionPlan
    {
        internal GuildProgressionTransitionPlan(
            GuildProgressionOperation operation,
            string requestFingerprint,
            GuildProgressionStateSnapshot expectedSnapshot,
            GuildProgressionStateSnapshot candidateSnapshot,
            GuildProgressionReceipt receipt,
            string planHash,
            IEnumerable<GuildMemberPerkProvenance> memberPerkProvenance)
        {
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            ExpectedSnapshot = expectedSnapshot;
            CandidateSnapshot = candidateSnapshot;
            Receipt = receipt;
            PlanHash = planHash ?? string.Empty;
            EffectDomains = Array.AsReadOnly(Array.Empty<GuildEffectDomain>());
            MemberPerkProvenance = memberPerkProvenance == null
                ? Array.AsReadOnly(Array.Empty<GuildMemberPerkProvenance>())
                : Array.AsReadOnly(memberPerkProvenance.ToArray());
        }

        public GuildProgressionOperation Operation { get; }
        public string RequestFingerprint { get; }
        public GuildProgressionStateSnapshot ExpectedSnapshot { get; }
        public GuildProgressionStateSnapshot CandidateSnapshot { get; }
        public GuildProgressionReceipt Receipt { get; }
        public string PlanHash { get; }
        public IReadOnlyList<GuildEffectDomain> EffectDomains { get; }
        public IReadOnlyList<GuildMemberPerkProvenance> MemberPerkProvenance { get; }
    }

    public sealed class GuildProgressionPlanningResult
    {
        internal GuildProgressionPlanningResult(
            GuildPlanningStatus status,
            GuildProgressionTransitionPlan plan,
            GuildProgressionReceipt existingReceipt,
            IEnumerable<GuildDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            ExistingReceipt = existingReceipt;
            Diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<GuildDiagnostic>())
                .OrderBy(value => value.Code, StringComparer.Ordinal)
                .ThenBy(value => value.SubjectId, StringComparer.Ordinal)
                .ToArray());
        }

        public GuildPlanningStatus Status { get; }
        public GuildProgressionTransitionPlan Plan { get; }
        public GuildProgressionReceipt ExistingReceipt { get; }
        public IReadOnlyList<GuildDiagnostic> Diagnostics { get; }
        public bool IsPrepared => Status == GuildPlanningStatus.Prepared && Plan != null;
    }
}
