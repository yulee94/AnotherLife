using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AL.Core;

namespace AL.Kingdom.Progression
{
    public static class ProgressionCompatibilityPlanner
    {
        public const string ResearchSchemaVersion = "al.progression.research.v1";
        public const string TroopSchemaVersion = "al.progression.troop.v1";
        public const string ProfileSchemaVersion = "al.progression.profile.v1";
        public const int MaximumDefinitions = 256;
        public const int MaximumStateRows = 512;
        public const int MaximumPrerequisitesPerDefinition = 64;
        public const int MaximumEffectsPerResearch = 64;
        public const int MaximumCostEntriesPerProfile = 16;
        public const int MaximumPrerequisiteTargets = 512;
        public const int MaximumIdUtf8Bytes = 128;

        public static ProgressionCompatibilityResult BuildResearchCompatibility(
            string catalogSetId,
            string catalogRevision,
            IEnumerable<ResearchProgressionDefinition> definitions,
            IEnumerable<ResearchProgressionStateRecord> rawStates,
            IEnumerable<ProgressionPrerequisiteTargetDefinition>
                prerequisiteTargets = null,
            ProgressionTimestampPolicy timestampPolicy = null)
        {
            var diagnostics = new List<ProgressionDiagnostic>();
            List<ResearchProgressionDefinition> definitionList =
                CopyBounded(definitions, MaximumDefinitions, out bool definitionLimitExceeded);
            List<ResearchProgressionStateRecord> stateList =
                CopyBounded(rawStates, MaximumStateRows, out bool stateLimitExceeded);
            List<ProgressionPrerequisiteTargetDefinition> prerequisiteTargetList =
                CopyBounded(
                    prerequisiteTargets,
                    MaximumPrerequisiteTargets,
                    out bool prerequisiteTargetLimitExceeded);

            ValidateCatalog(
                ProgressionDomain.Research,
                catalogSetId,
                catalogRevision,
                definitions,
                definitionList,
                definitionLimitExceeded,
                diagnostics);

            if (rawStates == null)
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.NullStateCollection,
                    ProgressionDomain.Research,
                    string.Empty,
                    -1));
            }

            if (stateLimitExceeded)
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InputLimitExceeded,
                    ProgressionDomain.Research,
                    string.Empty,
                    MaximumStateRows));
            }

            ValidatePrerequisiteTargets(
                prerequisiteTargetList,
                prerequisiteTargetLimitExceeded,
                ProgressionDomain.Research,
                diagnostics);
            ValidateResearchDefinitions(
                definitionList,
                prerequisiteTargetList,
                diagnostics);
            ValidateResearchStates(
                definitionList,
                stateList,
                timestampPolicy,
                diagnostics);

            if (diagnostics.Count > 0)
            {
                return ResearchResult(
                    DetermineUnavailableStatus(diagnostics),
                    catalogSetId,
                    catalogRevision,
                    BuildResearchRevision(
                        catalogSetId,
                        catalogRevision,
                        stateList,
                        Array.Empty<ResearchProgressionSnapshot>(),
                        diagnostics,
                        rawStates == null,
                        stateLimitExceeded,
                        timestampPolicy),
                    Array.Empty<ResearchProgressionSnapshot>(),
                    stateList,
                    diagnostics);
            }

            var statesById = stateList.ToDictionary(
                state => state.DefinitionId,
                StringComparer.Ordinal);
            var snapshots = new List<ResearchProgressionSnapshot>(definitionList.Count);
            foreach (ResearchProgressionDefinition definition in definitionList
                         .OrderBy(candidate => candidate.Identity.Id, StringComparer.Ordinal))
            {
                if (statesById.TryGetValue(
                        definition.Identity.Id,
                        out ResearchProgressionStateRecord state))
                {
                    snapshots.Add(new ResearchProgressionSnapshot(
                        definition,
                        state.Level,
                        ProgressionStateOrigin.Saved,
                        state.HasActiveLegacyOrder,
                        state.CompletionTimestamp));
                }
                else
                {
                    snapshots.Add(new ResearchProgressionSnapshot(
                        definition,
                        definition.InitialLevel,
                        ProgressionStateOrigin.EffectiveInitialUnpersisted,
                        false,
                        0));
                }
            }

            return ResearchResult(
                ProgressionCompatibilityStatus.Available,
                catalogSetId,
                catalogRevision,
                BuildResearchRevision(
                    catalogSetId,
                    catalogRevision,
                    stateList,
                    snapshots,
                    diagnostics,
                    false,
                    false,
                    timestampPolicy),
                snapshots,
                stateList,
                diagnostics);
        }

        public static ProgressionCompatibilityResult BuildTrainingCompatibility(
            string catalogSetId,
            string catalogRevision,
            IEnumerable<TroopProgressionDefinition> definitions,
            IEnumerable<TroopProgressionStateRecord> rawStates,
            IEnumerable<ProgressionPrerequisiteTargetDefinition>
                prerequisiteTargets = null)
        {
            var diagnostics = new List<ProgressionDiagnostic>();
            List<TroopProgressionDefinition> definitionList =
                CopyBounded(definitions, MaximumDefinitions, out bool definitionLimitExceeded);
            List<TroopProgressionStateRecord> stateList =
                CopyBounded(rawStates, MaximumStateRows, out bool stateLimitExceeded);
            List<ProgressionPrerequisiteTargetDefinition> prerequisiteTargetList =
                CopyBounded(
                    prerequisiteTargets,
                    MaximumPrerequisiteTargets,
                    out bool prerequisiteTargetLimitExceeded);

            ValidateCatalog(
                ProgressionDomain.Training,
                catalogSetId,
                catalogRevision,
                definitions,
                definitionList,
                definitionLimitExceeded,
                diagnostics);

            if (rawStates == null)
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.NullStateCollection,
                    ProgressionDomain.Training,
                    string.Empty,
                    -1));
            }

            if (stateLimitExceeded)
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InputLimitExceeded,
                    ProgressionDomain.Training,
                    string.Empty,
                    MaximumStateRows));
            }

            ValidatePrerequisiteTargets(
                prerequisiteTargetList,
                prerequisiteTargetLimitExceeded,
                ProgressionDomain.Training,
                diagnostics);
            ValidateTroopDefinitions(
                definitionList,
                prerequisiteTargetList,
                diagnostics);
            ValidateTroopStates(definitionList, stateList, diagnostics);

            if (diagnostics.Count > 0)
            {
                return TrainingResult(
                    DetermineUnavailableStatus(diagnostics),
                    catalogSetId,
                    catalogRevision,
                    BuildTrainingRevision(
                        catalogSetId,
                        catalogRevision,
                        stateList,
                        Array.Empty<TroopProgressionSnapshot>(),
                        diagnostics,
                        rawStates == null,
                        stateLimitExceeded),
                    Array.Empty<TroopProgressionSnapshot>(),
                    stateList,
                    diagnostics);
            }

            var statesById = stateList.ToDictionary(
                state => state.DefinitionId,
                StringComparer.Ordinal);
            var snapshots = new List<TroopProgressionSnapshot>(definitionList.Count);
            foreach (TroopProgressionDefinition definition in definitionList
                         .OrderBy(candidate => candidate.Identity.Id, StringComparer.Ordinal))
            {
                if (statesById.TryGetValue(
                        definition.Identity.Id,
                        out TroopProgressionStateRecord state))
                {
                    snapshots.Add(new TroopProgressionSnapshot(
                        definition,
                        state.ActiveCount,
                        state.WoundedCount,
                        state.ReservedCount,
                        ProgressionStateOrigin.Saved));
                }
                else
                {
                    snapshots.Add(new TroopProgressionSnapshot(
                        definition,
                        0,
                        0,
                        0,
                        ProgressionStateOrigin.EffectiveInitialUnpersisted));
                }
            }

            return TrainingResult(
                ProgressionCompatibilityStatus.Available,
                catalogSetId,
                catalogRevision,
                BuildTrainingRevision(
                    catalogSetId,
                    catalogRevision,
                    stateList,
                    snapshots,
                    diagnostics,
                    false,
                    false),
                snapshots,
                stateList,
                diagnostics);
        }

        private static void ValidateCatalog<TDefinition>(
            ProgressionDomain domain,
            string catalogSetId,
            string catalogRevision,
            IEnumerable<TDefinition> source,
            IReadOnlyCollection<TDefinition> definitions,
            bool limitExceeded,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            if (!IsValidId(catalogSetId) || !IsValidId(catalogRevision))
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InvalidCatalogIdentity,
                    domain,
                    string.Empty,
                    -1));
            }

            if (source == null || definitions.Count == 0)
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.UnavailableCatalog,
                    domain,
                    string.Empty,
                    -1));
            }

            if (limitExceeded)
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InputLimitExceeded,
                    domain,
                    string.Empty,
                    MaximumDefinitions));
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.UnavailableCatalog,
                    domain,
                    string.Empty,
                    MaximumDefinitions));
            }
        }

        private static void ValidateResearchDefinitions(
            IReadOnlyList<ResearchProgressionDefinition> definitions,
            IReadOnlyList<ProgressionPrerequisiteTargetDefinition>
                prerequisiteTargets,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                ResearchProgressionDefinition definition = definitions[index];
                if (definition == null)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.NullDefinition,
                        ProgressionDomain.Research,
                        string.Empty,
                        index));
                    continue;
                }

                string id = definition.Identity?.Id ?? string.Empty;
                ValidateDefinitionIdentity(
                    definition.Identity,
                    ResearchSchemaVersion,
                    ProgressionDomain.Research,
                    index,
                    diagnostics);

                if (definition.InitialLevel < 0 ||
                    definition.MaximumLevel < definition.InitialLevel)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidDefinitionRange,
                        ProgressionDomain.Research,
                        id,
                        index));
                }

                ValidateCostProfile(
                    definition.CostProfile,
                    ProgressionDomain.Research,
                    id,
                    index,
                    diagnostics);
                ValidateDurationProfile(
                    definition.DurationProfile,
                    false,
                    ProgressionDomain.Research,
                    id,
                    index,
                    diagnostics);
                ValidatePrerequisites(
                    definition.Prerequisites,
                    prerequisiteTargets,
                    ProgressionDomain.Research,
                    id,
                    index,
                    diagnostics);
                ValidateEffectProfiles(definition, index, diagnostics);
            }

            AddDuplicateDefinitionDiagnostics(
                definitions
                    .Select((definition, index) => new
                    {
                        Definition = definition,
                        Index = index
                    })
                    .Where(item => item.Definition?.Identity != null)
                    .Select(item => new IndexedIdentity(
                        item.Definition.Identity.Id,
                        item.Index)),
                ProgressionDomain.Research,
                diagnostics);
        }

        private static void ValidateTroopDefinitions(
            IReadOnlyList<TroopProgressionDefinition> definitions,
            IReadOnlyList<ProgressionPrerequisiteTargetDefinition>
                prerequisiteTargets,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                TroopProgressionDefinition definition = definitions[index];
                if (definition == null)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.NullDefinition,
                        ProgressionDomain.Training,
                        string.Empty,
                        index));
                    continue;
                }

                string id = definition.Identity?.Id ?? string.Empty;
                ValidateDefinitionIdentity(
                    definition.Identity,
                    TroopSchemaVersion,
                    ProgressionDomain.Training,
                    index,
                    diagnostics);

                if (definition.MaximumInventoryCount < 0 ||
                    definition.MaximumBatchCount <= 0 ||
                    definition.MaximumBatchCount > definition.MaximumInventoryCount)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidDefinitionRange,
                        ProgressionDomain.Training,
                        id,
                        index));
                }

                ValidateCostProfile(
                    definition.CostProfile,
                    ProgressionDomain.Training,
                    id,
                    index,
                    diagnostics);
                ValidateDurationProfile(
                    definition.DurationProfile,
                    true,
                    ProgressionDomain.Training,
                    id,
                    index,
                    diagnostics);
                ValidatePrerequisites(
                    definition.Prerequisites,
                    prerequisiteTargets,
                    ProgressionDomain.Training,
                    id,
                    index,
                    diagnostics);

                if (!IsValidProfileIdentity(definition.BattleProfile))
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidDefinitionIdentity,
                        ProgressionDomain.Training,
                        id,
                        index));
                }

                if (!IsValidProfileIdentity(definition.InventoryPolicy))
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidInventoryPolicy,
                        ProgressionDomain.Training,
                        id,
                        index));
                }

                if (definition.InventoryCapacityPolicy !=
                    TroopInventoryCapacityPolicy.SeparatedCountsTotalCapacityV1)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidInventoryPolicy,
                        ProgressionDomain.Training,
                        id,
                        index));
                }
            }

            AddDuplicateDefinitionDiagnostics(
                definitions
                    .Select((definition, index) => new
                    {
                        Definition = definition,
                        Index = index
                    })
                    .Where(item => item.Definition?.Identity != null)
                    .Select(item => new IndexedIdentity(
                        item.Definition.Identity.Id,
                        item.Index)),
                ProgressionDomain.Training,
                diagnostics);
        }

        private static void ValidateDefinitionIdentity(
            ProgressionSourceIdentity identity,
            string expectedSchemaVersion,
            ProgressionDomain domain,
            int index,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            string id = identity?.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.BlankDefinitionId,
                    domain,
                    id,
                    index));
            }
            else if (!IsValidId(id))
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InvalidDefinitionIdentity,
                    domain,
                    id,
                    index));
            }

            if (identity == null ||
                !IsValidId(identity.ContentVersion) ||
                !IsValidId(identity.SourceRevision) ||
                !IsSha256(identity.RawSha256))
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InvalidDefinitionIdentity,
                    domain,
                    id,
                    index));
            }

            if (identity == null ||
                !string.Equals(
                    identity.SchemaVersion,
                    expectedSchemaVersion,
                    StringComparison.Ordinal))
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.UnsupportedSchemaVersion,
                    domain,
                    id,
                    index));
            }
        }

        private static void ValidateCostProfile(
            ProgressionCostProfile profile,
            ProgressionDomain domain,
            string definitionId,
            int index,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            if (profile == null ||
                !IsValidProfileIdentity(profile.Identity) ||
                profile.UnitCosts.Count == 0 ||
                profile.UnitCosts.Count > MaximumCostEntriesPerProfile ||
                profile.MaximumAmountPerResource <= 0 ||
                profile.UnitCosts.Any(cost =>
                    !Enum.IsDefined(typeof(ResourceType), cost.ResourceType) ||
                    cost.Amount <= 0 ||
                    cost.Amount > profile.MaximumAmountPerResource) ||
                profile.UnitCosts
                    .GroupBy(cost => cost.ResourceType)
                    .Any(group => group.Count() > 1))
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InvalidCostProfile,
                    domain,
                    definitionId,
                    index));
            }
        }

        private static void ValidateDurationProfile(
            ProgressionDurationProfile profile,
            bool zeroDurationMayBeExplicit,
            ProgressionDomain domain,
            string definitionId,
            int index,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            bool invalid = profile == null ||
                           !IsValidProfileIdentity(profile.Identity) ||
                           profile.UnitSeconds < 0 ||
                           profile.MaximumSeconds < 0 ||
                           profile.UnitSeconds > profile.MaximumSeconds ||
                           (profile.UnitSeconds == 0 &&
                            (!zeroDurationMayBeExplicit || !profile.AllowsZeroDuration));

            if (invalid)
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InvalidDurationProfile,
                    domain,
                    definitionId,
                    index));
            }
        }

        private static void ValidatePrerequisiteTargets(
            IReadOnlyList<ProgressionPrerequisiteTargetDefinition> targets,
            bool limitExceeded,
            ProgressionDomain domain,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            if (limitExceeded)
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InvalidPrerequisite,
                    domain,
                    string.Empty,
                    MaximumPrerequisiteTargets));
            }

            for (int index = 0; index < targets.Count; index++)
            {
                ProgressionPrerequisiteTargetDefinition target = targets[index];
                if (target == null ||
                    !IsValidAnySourceIdentity(target.Identity) ||
                    target.MaximumLevel < 0)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidPrerequisite,
                        domain,
                        target?.Identity?.Id,
                        index));
                }
            }

            foreach (IGrouping<string, IndexedIdentity> group in targets
                         .Select((target, index) => new
                         {
                             Target = target,
                             Index = index
                         })
                         .Where(item => item.Target?.Identity != null)
                         .Select(item => new IndexedIdentity(
                             item.Target.Identity.Id,
                             item.Index))
                         .Where(identity => !string.IsNullOrWhiteSpace(identity.Id))
                         .GroupBy(identity => identity.Id, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InvalidPrerequisite,
                    domain,
                    group.Key,
                    group.Min(identity => identity.Index)));
            }
        }

        private static void ValidatePrerequisites(
            IReadOnlyList<ProgressionPrerequisite> prerequisites,
            IReadOnlyList<ProgressionPrerequisiteTargetDefinition> targets,
            ProgressionDomain domain,
            string definitionId,
            int definitionIndex,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            if (prerequisites.Count > MaximumPrerequisitesPerDefinition)
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InputLimitExceeded,
                    domain,
                    definitionId,
                    definitionIndex));
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InvalidPrerequisite,
                    domain,
                    definitionId,
                    definitionIndex));
                return;
            }

            var targetLookup = targets
                .Where(target => target?.Identity != null &&
                                 IsValidId(target.Identity.Id))
                .GroupBy(target => target.Identity.Id, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(
                    group => group.Key,
                    group => group.Single(),
                    StringComparer.Ordinal);
            for (int index = 0; index < prerequisites.Count; index++)
            {
                ProgressionPrerequisite prerequisite = prerequisites[index];
                if (prerequisite == null ||
                    !IsValidId(prerequisite.DefinitionId) ||
                    prerequisite.MinimumLevel < 0 ||
                    !targetLookup.TryGetValue(
                        prerequisite?.DefinitionId ?? string.Empty,
                        out ProgressionPrerequisiteTargetDefinition target) ||
                    prerequisite.MinimumLevel > target.MaximumLevel)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidPrerequisite,
                        domain,
                        definitionId,
                        index));
                }
            }

            foreach (IGrouping<string, ProgressionPrerequisite> group in prerequisites
                         .Where(prerequisite => prerequisite != null &&
                                                !string.IsNullOrWhiteSpace(
                                                    prerequisite.DefinitionId))
                         .GroupBy(
                             prerequisite => prerequisite.DefinitionId,
                             StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.DuplicatePrerequisite,
                    domain,
                    definitionId,
                    definitionIndex));
            }
        }

        private static void ValidateEffectProfiles(
            ResearchProgressionDefinition definition,
            int definitionIndex,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            if (definition.EffectProfiles.Count > MaximumEffectsPerResearch)
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InputLimitExceeded,
                    ProgressionDomain.Research,
                    definition.Identity?.Id,
                    definitionIndex));
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InvalidEffectProfile,
                    ProgressionDomain.Research,
                    definition.Identity?.Id,
                    definitionIndex));
                return;
            }

            for (int index = 0; index < definition.EffectProfiles.Count; index++)
            {
                if (!IsValidProfileIdentity(definition.EffectProfiles[index]))
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidEffectProfile,
                        ProgressionDomain.Research,
                        definition.Identity?.Id,
                        index));
                }
            }

            foreach (IGrouping<string, ProgressionSourceIdentity> group in definition
                         .EffectProfiles
                         .Where(identity => identity != null &&
                                            !string.IsNullOrWhiteSpace(identity.Id))
                         .GroupBy(identity => identity.Id, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.DuplicateEffectProfile,
                    ProgressionDomain.Research,
                    definition.Identity?.Id,
                    definitionIndex));
            }
        }

        private static void ValidateResearchStates(
            IReadOnlyList<ResearchProgressionDefinition> definitions,
            IReadOnlyList<ResearchProgressionStateRecord> states,
            ProgressionTimestampPolicy timestampPolicy,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            var definitionsById = definitions
                .Where(definition => definition?.Identity != null &&
                                     IsValidId(definition.Identity.Id))
                .GroupBy(definition => definition.Identity.Id, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(
                    group => group.Key,
                    group => group.Single(),
                    StringComparer.Ordinal);

            for (int index = 0; index < states.Count; index++)
            {
                ResearchProgressionStateRecord state = states[index];
                if (state == null)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.NullState,
                        ProgressionDomain.Research,
                        string.Empty,
                        index));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(state.DefinitionId))
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.BlankStateId,
                        ProgressionDomain.Research,
                        state.DefinitionId,
                        index));
                    continue;
                }

                if (!IsValidId(state.DefinitionId))
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidStateId,
                        ProgressionDomain.Research,
                        state.DefinitionId,
                        index));
                    continue;
                }

                if (!definitionsById.TryGetValue(
                        state.DefinitionId,
                        out ResearchProgressionDefinition definition))
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.PreservedUnknownFutureDefinition,
                        ProgressionDomain.Research,
                        state.DefinitionId,
                        index));
                    continue;
                }

                if (!string.Equals(
                        state.DefinitionContentVersion,
                        definition.Identity.ContentVersion,
                        StringComparison.Ordinal))
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.UnsupportedContentVersion,
                        ProgressionDomain.Research,
                        state.DefinitionId,
                        index));
                }

                if (state.Level < 0)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.NegativeLevel,
                        ProgressionDomain.Research,
                        state.DefinitionId,
                        index));
                }
                else if (state.Level < definition.InitialLevel)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.BelowInitialLevel,
                        ProgressionDomain.Research,
                        state.DefinitionId,
                        index));
                }
                else if (state.Level > definition.MaximumLevel)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.OverMaximumLevel,
                        ProgressionDomain.Research,
                        state.DefinitionId,
                        index));
                }

                bool hasTimerEvidence =
                    state.HasActiveLegacyOrder || state.CompletionTimestamp != 0;
                bool timestampPolicyValid = IsValidTimestampPolicy(timestampPolicy);
                bool timestampOutsidePolicy = hasTimerEvidence &&
                    (!timestampPolicyValid ||
                     state.CompletionTimestamp <
                     timestampPolicy.MinimumUtcTimestamp ||
                     state.CompletionTimestamp >
                     timestampPolicy.MaximumUtcTimestamp);
                if (hasTimerEvidence && !timestampPolicyValid)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidTimestampPolicy,
                        ProgressionDomain.Research,
                        state.DefinitionId,
                        index));
                }

                if ((state.HasActiveLegacyOrder && state.CompletionTimestamp <= 0) ||
                    (!state.HasActiveLegacyOrder && state.CompletionTimestamp < 0) ||
                    timestampOutsidePolicy ||
                    (state.HasActiveLegacyOrder &&
                     state.Level >= definition.MaximumLevel))
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.ImpossibleTimer,
                        ProgressionDomain.Research,
                        state.DefinitionId,
                        index));
                }
                else if (hasTimerEvidence)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.MigrationRequired,
                        ProgressionDomain.Research,
                        state.DefinitionId,
                        index));
                }
            }

            AddDuplicateStateDiagnostics(
                states
                    .Select((state, index) => new
                    {
                        State = state,
                        Index = index
                    })
                    .Where(item => item.State != null)
                    .Select(item => new IndexedIdentity(
                        item.State.DefinitionId,
                        item.Index)),
                ProgressionDomain.Research,
                diagnostics);
        }

        private static void ValidateTroopStates(
            IReadOnlyList<TroopProgressionDefinition> definitions,
            IReadOnlyList<TroopProgressionStateRecord> states,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            var definitionsById = definitions
                .Where(definition => definition?.Identity != null &&
                                     IsValidId(definition.Identity.Id))
                .GroupBy(definition => definition.Identity.Id, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(
                    group => group.Key,
                    group => group.Single(),
                    StringComparer.Ordinal);

            for (int index = 0; index < states.Count; index++)
            {
                TroopProgressionStateRecord state = states[index];
                if (state == null)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.NullState,
                        ProgressionDomain.Training,
                        string.Empty,
                        index));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(state.DefinitionId))
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.BlankStateId,
                        ProgressionDomain.Training,
                        state.DefinitionId,
                        index));
                    continue;
                }

                if (!IsValidId(state.DefinitionId))
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidStateId,
                        ProgressionDomain.Training,
                        state.DefinitionId,
                        index));
                    continue;
                }

                if (!definitionsById.TryGetValue(
                        state.DefinitionId,
                        out TroopProgressionDefinition definition))
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.PreservedUnknownFutureDefinition,
                        ProgressionDomain.Training,
                        state.DefinitionId,
                        index));
                    continue;
                }

                if (!string.Equals(
                        state.DefinitionContentVersion,
                        definition.Identity.ContentVersion,
                        StringComparison.Ordinal))
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.UnsupportedContentVersion,
                        ProgressionDomain.Training,
                        state.DefinitionId,
                        index));
                }

                if (state.ActiveCount < 0 ||
                    state.WoundedCount < 0 ||
                    state.ReservedCount < 0)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.NegativeCount,
                        ProgressionDomain.Training,
                        state.DefinitionId,
                        index));
                    continue;
                }

                if (definition.InventoryCapacityPolicy !=
                    TroopInventoryCapacityPolicy.SeparatedCountsTotalCapacityV1)
                {
                    continue;
                }

                try
                {
                    long total = checked(
                        checked(state.ActiveCount + state.WoundedCount) +
                        state.ReservedCount);
                    if (total > definition.MaximumInventoryCount)
                    {
                        diagnostics.Add(Diagnostic(
                            ProgressionDiagnosticCode.OverMaximumCount,
                            ProgressionDomain.Training,
                            state.DefinitionId,
                            index));
                    }
                }
                catch (OverflowException)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.CountOverflow,
                        ProgressionDomain.Training,
                        state.DefinitionId,
                        index));
                }
            }

            AddDuplicateStateDiagnostics(
                states
                    .Select((state, index) => new
                    {
                        State = state,
                        Index = index
                    })
                    .Where(item => item.State != null)
                    .Select(item => new IndexedIdentity(
                        item.State.DefinitionId,
                        item.Index)),
                ProgressionDomain.Training,
                diagnostics);
        }

        private static void AddDuplicateDefinitionDiagnostics(
            IEnumerable<IndexedIdentity> identities,
            ProgressionDomain domain,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            foreach (IGrouping<string, IndexedIdentity> group in identities
                         .Where(identity => !string.IsNullOrWhiteSpace(identity.Id))
                         .GroupBy(identity => identity.Id, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.DuplicateDefinitionId,
                    domain,
                    group.Key,
                    group.Min(identity => identity.Index)));
            }
        }

        private static void AddDuplicateStateDiagnostics(
            IEnumerable<IndexedIdentity> identities,
            ProgressionDomain domain,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            foreach (IGrouping<string, IndexedIdentity> group in identities
                         .Where(identity => !string.IsNullOrWhiteSpace(identity.Id))
                         .GroupBy(identity => identity.Id, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.DuplicateStateId,
                    domain,
                    group.Key,
                    group.Min(identity => identity.Index)));
            }
        }

        private static bool IsValidProfileIdentity(ProgressionSourceIdentity identity)
        {
            return identity != null &&
                   IsValidId(identity.Id) &&
                   string.Equals(
                       identity.SchemaVersion,
                       ProfileSchemaVersion,
                       StringComparison.Ordinal) &&
                   IsValidId(identity.ContentVersion) &&
                   IsValidId(identity.SourceRevision) &&
                   IsSha256(identity.RawSha256);
        }

        private static bool IsValidAnySourceIdentity(
            ProgressionSourceIdentity identity)
        {
            return identity != null &&
                   IsValidId(identity.Id) &&
                   IsValidId(identity.SchemaVersion) &&
                   IsValidId(identity.ContentVersion) &&
                   IsValidId(identity.SourceRevision) &&
                   IsSha256(identity.RawSha256);
        }

        private static bool IsValidTimestampPolicy(
            ProgressionTimestampPolicy policy)
        {
            return policy != null &&
                   IsValidId(policy.PolicyVersion) &&
                   policy.MinimumUtcTimestamp > 0 &&
                   policy.MaximumUtcTimestamp >= policy.MinimumUtcTimestamp;
        }

        private static bool IsValidId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                Encoding.UTF8.GetByteCount(value) > MaximumIdUtf8Bytes ||
                value.Any(char.IsWhiteSpace))
            {
                return false;
            }

            return !value.Any(char.IsControl);
        }

        private static bool IsSha256(string value)
        {
            return value != null &&
                   value.Length == 64 &&
                   value.All(character =>
                       (character >= '0' && character <= '9') ||
                       (character >= 'a' && character <= 'f'));
        }

        private static ProgressionCompatibilityStatus DetermineUnavailableStatus(
            IEnumerable<ProgressionDiagnostic> diagnostics)
        {
            return diagnostics.Any(diagnostic =>
                diagnostic.Code == ProgressionDiagnosticCode.UnavailableCatalog ||
                diagnostic.Code == ProgressionDiagnosticCode.InvalidCatalogIdentity ||
                diagnostic.Code == ProgressionDiagnosticCode.NullDefinition ||
                diagnostic.Code == ProgressionDiagnosticCode.BlankDefinitionId ||
                diagnostic.Code == ProgressionDiagnosticCode.InvalidDefinitionIdentity ||
                diagnostic.Code == ProgressionDiagnosticCode.DuplicateDefinitionId ||
                diagnostic.Code == ProgressionDiagnosticCode.UnsupportedSchemaVersion ||
                diagnostic.Code == ProgressionDiagnosticCode.InvalidDefinitionRange ||
                diagnostic.Code == ProgressionDiagnosticCode.InvalidCostProfile ||
                diagnostic.Code == ProgressionDiagnosticCode.InvalidDurationProfile ||
                diagnostic.Code == ProgressionDiagnosticCode.InvalidPrerequisite ||
                diagnostic.Code == ProgressionDiagnosticCode.DuplicatePrerequisite ||
                diagnostic.Code == ProgressionDiagnosticCode.InvalidEffectProfile ||
                diagnostic.Code == ProgressionDiagnosticCode.DuplicateEffectProfile ||
                diagnostic.Code == ProgressionDiagnosticCode.InvalidInventoryPolicy)
                ? ProgressionCompatibilityStatus.UnavailableCatalog
                : ProgressionCompatibilityStatus.MalformedState;
        }

        private static ProgressionCompatibilityResult ResearchResult(
            ProgressionCompatibilityStatus status,
            string catalogSetId,
            string catalogRevision,
            string stateRevision,
            IEnumerable<ResearchProgressionSnapshot> snapshots,
            IEnumerable<ResearchProgressionStateRecord> states,
            IEnumerable<ProgressionDiagnostic> diagnostics)
        {
            return new ProgressionCompatibilityResult(
                ProgressionDomain.Research,
                status,
                catalogSetId,
                catalogRevision,
                stateRevision,
                snapshots,
                Array.Empty<TroopProgressionSnapshot>(),
                states,
                Array.Empty<TroopProgressionStateRecord>(),
                SortDiagnostics(diagnostics));
        }

        private static ProgressionCompatibilityResult TrainingResult(
            ProgressionCompatibilityStatus status,
            string catalogSetId,
            string catalogRevision,
            string stateRevision,
            IEnumerable<TroopProgressionSnapshot> snapshots,
            IEnumerable<TroopProgressionStateRecord> states,
            IEnumerable<ProgressionDiagnostic> diagnostics)
        {
            return new ProgressionCompatibilityResult(
                ProgressionDomain.Training,
                status,
                catalogSetId,
                catalogRevision,
                stateRevision,
                Array.Empty<ResearchProgressionSnapshot>(),
                snapshots,
                Array.Empty<ResearchProgressionStateRecord>(),
                states,
                SortDiagnostics(diagnostics));
        }

        private static IEnumerable<ProgressionDiagnostic> SortDiagnostics(
            IEnumerable<ProgressionDiagnostic> diagnostics)
        {
            return (diagnostics ?? Array.Empty<ProgressionDiagnostic>())
                .OrderBy(diagnostic => diagnostic.Domain)
                .ThenBy(diagnostic => diagnostic.DefinitionId, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Code)
                .ThenBy(diagnostic => diagnostic.SourceIndex);
        }

        private static string BuildResearchRevision(
            string catalogSetId,
            string catalogRevision,
            IReadOnlyList<ResearchProgressionStateRecord> states,
            IEnumerable<ResearchProgressionSnapshot> snapshots,
            IEnumerable<ProgressionDiagnostic> diagnostics,
            bool nullStateCollection,
            bool stateLimitExceeded,
            ProgressionTimestampPolicy timestampPolicy)
        {
            var segments = new List<string>
            {
                "research-state",
                catalogSetId,
                catalogRevision,
                nullStateCollection ? "null-collection" : "collection",
                stateLimitExceeded ? "truncated" : "complete",
                timestampPolicy?.PolicyVersion,
                Invariant(timestampPolicy?.MinimumUtcTimestamp ?? 0),
                Invariant(timestampPolicy?.MaximumUtcTimestamp ?? 0)
            };
            for (int index = 0; index < states.Count; index++)
            {
                ResearchProgressionStateRecord state = states[index];
                segments.Add("raw-state");
                segments.Add(Invariant(index));
                if (state == null)
                {
                    segments.Add("null");
                    continue;
                }

                segments.Add("value");
                AddNullableRawSegment(
                    segments,
                    "definition-id",
                    state.DefinitionId);
                AddNullableRawSegment(
                    segments,
                    "content-version",
                    state.DefinitionContentVersion);
                segments.Add(Invariant(state.Level));
                segments.Add(state.HasActiveLegacyOrder ? "1" : "0");
                segments.Add(Invariant(state.CompletionTimestamp));
            }

            foreach (ResearchProgressionSnapshot snapshot in snapshots
                         .Where(snapshot => snapshot?.Definition?.Identity != null)
                         .OrderBy(
                             snapshot => snapshot.Definition.Identity.Id,
                             StringComparer.Ordinal))
            {
                segments.Add("snapshot");
                segments.Add(snapshot.Definition.Identity.Id);
                segments.Add(Invariant(snapshot.Level));
                segments.Add(snapshot.Origin.ToString());
            }

            AddDiagnosticRevisionSegments(segments, diagnostics);
            return ProgressionContractHash.Compute(segments.ToArray());
        }

        private static string BuildTrainingRevision(
            string catalogSetId,
            string catalogRevision,
            IReadOnlyList<TroopProgressionStateRecord> states,
            IEnumerable<TroopProgressionSnapshot> snapshots,
            IEnumerable<ProgressionDiagnostic> diagnostics,
            bool nullStateCollection,
            bool stateLimitExceeded)
        {
            var segments = new List<string>
            {
                "training-state",
                catalogSetId,
                catalogRevision,
                nullStateCollection ? "null-collection" : "collection",
                stateLimitExceeded ? "truncated" : "complete"
            };
            for (int index = 0; index < states.Count; index++)
            {
                TroopProgressionStateRecord state = states[index];
                segments.Add("raw-state");
                segments.Add(Invariant(index));
                if (state == null)
                {
                    segments.Add("null");
                    continue;
                }

                segments.Add("value");
                AddNullableRawSegment(
                    segments,
                    "definition-id",
                    state.DefinitionId);
                AddNullableRawSegment(
                    segments,
                    "content-version",
                    state.DefinitionContentVersion);
                segments.Add(Invariant(state.ActiveCount));
                segments.Add(Invariant(state.WoundedCount));
                segments.Add(Invariant(state.ReservedCount));
            }

            foreach (TroopProgressionSnapshot snapshot in snapshots
                         .Where(snapshot => snapshot?.Definition?.Identity != null)
                         .OrderBy(
                             snapshot => snapshot.Definition.Identity.Id,
                             StringComparer.Ordinal))
            {
                segments.Add("snapshot");
                segments.Add(snapshot.Definition.Identity.Id);
                segments.Add(Invariant(snapshot.ActiveCount));
                segments.Add(Invariant(snapshot.WoundedCount));
                segments.Add(Invariant(snapshot.ReservedCount));
                segments.Add(snapshot.Origin.ToString());
            }

            AddDiagnosticRevisionSegments(segments, diagnostics);
            return ProgressionContractHash.Compute(segments.ToArray());
        }

        private static void AddDiagnosticRevisionSegments(
            ICollection<string> segments,
            IEnumerable<ProgressionDiagnostic> diagnostics)
        {
            foreach (ProgressionDiagnostic diagnostic in
                     SortDiagnostics(diagnostics))
            {
                segments.Add("diagnostic");
                segments.Add(diagnostic.Domain.ToString());
                segments.Add(diagnostic.DefinitionId);
                segments.Add(diagnostic.Code.ToString());
                segments.Add(Invariant(diagnostic.SourceIndex));
            }
        }

        private static void AddNullableRawSegment(
            ICollection<string> segments,
            string label,
            string value)
        {
            segments.Add(label);
            segments.Add(value == null ? "null" : "value");
            segments.Add(value);
        }

        private static string Invariant(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static List<T> CopyBounded<T>(
            IEnumerable<T> source,
            int maximumCount,
            out bool limitExceeded)
        {
            var result = new List<T>();
            limitExceeded = false;
            if (source == null)
            {
                return result;
            }

            foreach (T value in source)
            {
                if (result.Count == maximumCount)
                {
                    limitExceeded = true;
                    break;
                }

                result.Add(value);
            }

            return result;
        }

        private static ProgressionDiagnostic Diagnostic(
            ProgressionDiagnosticCode code,
            ProgressionDomain domain,
            string definitionId,
            int sourceIndex)
        {
            return new ProgressionDiagnostic(code, domain, definitionId, sourceIndex);
        }

        private readonly struct IndexedIdentity
        {
            public IndexedIdentity(string id, int index)
            {
                Id = id ?? string.Empty;
                Index = index;
            }

            public string Id { get; }
            public int Index { get; }
        }
    }
}
