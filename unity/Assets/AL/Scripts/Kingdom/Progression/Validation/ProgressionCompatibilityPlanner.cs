using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        public const int MaximumIdUtf8Bytes =
            ProgressionText.MaximumIdentifierUtf8Bytes;

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
            bool hasDefinitionSource = definitions != null &&
                                       definitionList.Count > 0 &&
                                       !definitionLimitExceeded;

            ValidateCatalog(
                ProgressionDomain.Research,
                catalogSetId,
                catalogRevision,
                definitions,
                definitionList,
                definitionLimitExceeded,
                diagnostics);
            ValidateTimestampPolicy(
                timestampPolicy,
                ProgressionDomain.Research,
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
                        definitionList,
                        prerequisiteTargetList,
                        stateList,
                        Array.Empty<ResearchProgressionSnapshot>(),
                        diagnostics,
                        rawStates == null,
                        stateLimitExceeded,
                        timestampPolicy),
                    Array.Empty<ResearchProgressionSnapshot>(),
                    stateList,
                    diagnostics,
                    definitionList,
                    timestampPolicy,
                    hasDefinitionSource);
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
                    definitionList,
                    prerequisiteTargetList,
                    stateList,
                    snapshots,
                    diagnostics,
                    false,
                    false,
                    timestampPolicy),
                snapshots,
                stateList,
                diagnostics,
                definitionList,
                timestampPolicy,
                hasDefinitionSource);
        }

        public static ProgressionCompatibilityResult BuildTrainingCompatibility(
            string catalogSetId,
            string catalogRevision,
            IEnumerable<TroopProgressionDefinition> definitions,
            IEnumerable<TroopProgressionStateRecord> rawStates,
            IEnumerable<ProgressionPrerequisiteTargetDefinition>
                prerequisiteTargets = null,
            ProgressionTimestampPolicy timestampPolicy = null)
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
            bool hasDefinitionSource = definitions != null &&
                                       definitionList.Count > 0 &&
                                       !definitionLimitExceeded;

            ValidateCatalog(
                ProgressionDomain.Training,
                catalogSetId,
                catalogRevision,
                definitions,
                definitionList,
                definitionLimitExceeded,
                diagnostics);
            ValidateTimestampPolicy(
                timestampPolicy,
                ProgressionDomain.Training,
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
                        definitionList,
                        prerequisiteTargetList,
                        stateList,
                        Array.Empty<TroopProgressionSnapshot>(),
                        diagnostics,
                        rawStates == null,
                        stateLimitExceeded,
                        timestampPolicy),
                    Array.Empty<TroopProgressionSnapshot>(),
                    stateList,
                    diagnostics,
                    definitionList,
                    timestampPolicy,
                    hasDefinitionSource);
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
                    definitionList,
                    prerequisiteTargetList,
                    stateList,
                    snapshots,
                    diagnostics,
                    false,
                    false,
                    timestampPolicy),
                snapshots,
                stateList,
                diagnostics,
                definitionList,
                timestampPolicy,
                hasDefinitionSource);
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
            bool catalogSetIdValid =
                ProgressionText.IsValidIdentifier(catalogSetId);
            bool catalogRevisionValid =
                ProgressionText.IsValidIdentifier(catalogRevision);
            if (!catalogSetIdValid || !catalogRevisionValid)
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

                string id = SafeDiagnosticId(definition.Identity?.Id);
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

                string id = SafeDiagnosticId(definition.Identity?.Id);
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
            string id = SafeDiagnosticId(identity?.Id);
            ProgressionIdentifierValidation idValidation =
                ProgressionText.ValidateIdentifier(identity?.Id);
            if (idValidation == ProgressionIdentifierValidation.Null ||
                idValidation == ProgressionIdentifierValidation.Empty ||
                idValidation == ProgressionIdentifierValidation.Whitespace)
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.BlankDefinitionId,
                    domain,
                    id,
                    index));
            }
            if (!IsValidAnySourceIdentity(identity))
            {
                diagnostics.Add(Diagnostic(
                    ProgressionDiagnosticCode.InvalidDefinitionIdentity,
                    domain,
                    id,
                    index));
            }

            if (identity == null ||
                !ProgressionText.IsValidIdentifier(identity.SchemaVersion) ||
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
                          .Where(identity =>
                              ProgressionText.IsValidIdentifier(identity.Id))
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
                                 IsValidAnySourceIdentity(target.Identity))
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
                    !ProgressionText.IsValidIdentifier(
                        prerequisite.DefinitionId) ||
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
                                                ProgressionText
                                                    .IsValidIdentifier(
                                                        prerequisite
                                                            .DefinitionId))
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
                                            IsValidProfileIdentity(identity))
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
                                     IsValidDefinitionIdentity(
                                         definition.Identity,
                                         ResearchSchemaVersion))
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

                ProgressionIdentifierValidation stateIdValidation =
                    ProgressionText.ValidateIdentifier(state.DefinitionId);
                ProgressionIdentifierValidation contentVersionValidation =
                    ProgressionText.ValidateIdentifier(
                        state.DefinitionContentVersion);
                if (stateIdValidation == ProgressionIdentifierValidation.Null ||
                    stateIdValidation == ProgressionIdentifierValidation.Empty ||
                    stateIdValidation ==
                    ProgressionIdentifierValidation.Whitespace)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.BlankStateId,
                        ProgressionDomain.Research,
                        state.DefinitionId,
                        index));
                    continue;
                }

                if (stateIdValidation != ProgressionIdentifierValidation.Valid)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidStateId,
                        ProgressionDomain.Research,
                        state.DefinitionId,
                        index));
                    continue;
                }

                if (contentVersionValidation !=
                    ProgressionIdentifierValidation.Valid)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.UnsupportedContentVersion,
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
                                     IsValidDefinitionIdentity(
                                         definition.Identity,
                                         TroopSchemaVersion))
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

                ProgressionIdentifierValidation stateIdValidation =
                    ProgressionText.ValidateIdentifier(state.DefinitionId);
                ProgressionIdentifierValidation contentVersionValidation =
                    ProgressionText.ValidateIdentifier(
                        state.DefinitionContentVersion);
                if (stateIdValidation == ProgressionIdentifierValidation.Null ||
                    stateIdValidation == ProgressionIdentifierValidation.Empty ||
                    stateIdValidation ==
                    ProgressionIdentifierValidation.Whitespace)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.BlankStateId,
                        ProgressionDomain.Training,
                        state.DefinitionId,
                        index));
                    continue;
                }

                if (stateIdValidation != ProgressionIdentifierValidation.Valid)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.InvalidStateId,
                        ProgressionDomain.Training,
                        state.DefinitionId,
                        index));
                    continue;
                }

                if (contentVersionValidation !=
                    ProgressionIdentifierValidation.Valid)
                {
                    diagnostics.Add(Diagnostic(
                        ProgressionDiagnosticCode.UnsupportedContentVersion,
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
                         .Where(identity =>
                             ProgressionText.IsValidIdentifier(identity.Id))
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
                         .Where(identity =>
                             ProgressionText.IsValidIdentifier(identity.Id))
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
            return IsValidDefinitionIdentity(identity, ProfileSchemaVersion);
        }

        private static bool IsValidDefinitionIdentity(
            ProgressionSourceIdentity identity,
            string expectedSchemaVersion)
        {
            return IsValidAnySourceIdentity(identity) &&
                   string.Equals(
                       identity.SchemaVersion,
                       expectedSchemaVersion,
                       StringComparison.Ordinal);
        }

        private static bool IsValidAnySourceIdentity(
            ProgressionSourceIdentity identity)
        {
            if (identity == null)
            {
                return false;
            }

            bool idValid =
                ProgressionText.IsValidIdentifier(identity.Id);
            bool schemaValid =
                ProgressionText.IsValidIdentifier(identity.SchemaVersion);
            bool contentValid =
                ProgressionText.IsValidIdentifier(identity.ContentVersion);
            bool revisionValid =
                ProgressionText.IsValidIdentifier(identity.SourceRevision);
            bool hashValid = IsSha256(identity.RawSha256);
            return idValid &&
                   schemaValid &&
                   contentValid &&
                   revisionValid &&
                   hashValid;
        }

        private static void ValidateTimestampPolicy(
            ProgressionTimestampPolicy policy,
            ProgressionDomain domain,
            ICollection<ProgressionDiagnostic> diagnostics)
        {
            if (IsValidTimestampPolicy(policy))
            {
                return;
            }

            diagnostics.Add(Diagnostic(
                ProgressionDiagnosticCode.InvalidTimestampPolicy,
                domain,
                string.Empty,
                -1));
        }

        private static bool IsValidTimestampPolicy(
            ProgressionTimestampPolicy policy)
        {
            if (policy == null ||
                !ProgressionText.IsValidIdentifier(policy.PolicyVersion) ||
                policy.MinimumUtcTimestamp <= 0 ||
                policy.MaximumUtcTimestamp < policy.MinimumUtcTimestamp ||
                policy.MaximumRetentionAgeSeconds < 0 ||
                policy.MaximumFutureLeadSeconds < 0)
            {
                return false;
            }

            long policySpan =
                policy.MaximumUtcTimestamp - policy.MinimumUtcTimestamp;
            return policy.MaximumRetentionAgeSeconds <= policySpan &&
                   policy.MaximumFutureLeadSeconds <= policySpan;
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if ((character < '0' || character > '9') &&
                    (character < 'a' || character > 'f'))
                {
                    return false;
                }
            }

            return true;
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
                 diagnostic.Code == ProgressionDiagnosticCode.InvalidInventoryPolicy ||
                 diagnostic.Code == ProgressionDiagnosticCode.InvalidTimestampPolicy)
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
            IEnumerable<ProgressionDiagnostic> diagnostics,
            IEnumerable<ResearchProgressionDefinition> definitions,
            ProgressionTimestampPolicy timestampPolicy,
            bool hasDefinitionSource)
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
                SortDiagnostics(diagnostics),
                definitions,
                Array.Empty<TroopProgressionDefinition>(),
                timestampPolicy,
                hasDefinitionSource);
        }

        private static ProgressionCompatibilityResult TrainingResult(
            ProgressionCompatibilityStatus status,
            string catalogSetId,
            string catalogRevision,
            string stateRevision,
            IEnumerable<TroopProgressionSnapshot> snapshots,
            IEnumerable<TroopProgressionStateRecord> states,
            IEnumerable<ProgressionDiagnostic> diagnostics,
            IEnumerable<TroopProgressionDefinition> definitions,
            ProgressionTimestampPolicy timestampPolicy,
            bool hasDefinitionSource)
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
                SortDiagnostics(diagnostics),
                Array.Empty<ResearchProgressionDefinition>(),
                definitions,
                timestampPolicy,
                hasDefinitionSource);
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
            IReadOnlyList<ResearchProgressionDefinition> definitions,
            IReadOnlyList<ProgressionPrerequisiteTargetDefinition>
                prerequisiteTargets,
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
                "catalog-set-id",
                IdentifierRevisionValue(catalogSetId),
                "catalog-revision",
                IdentifierRevisionValue(catalogRevision),
                nullStateCollection ? "null-collection" : "collection",
                stateLimitExceeded ? "truncated" : "complete",
                BuildTimestampPolicyRevision(timestampPolicy),
                BuildSequenceRevision(
                    "research-definition-structure",
                    (definitions ??
                     Array.Empty<ResearchProgressionDefinition>())
                    .Select(BuildResearchDefinitionStructureRevision)),
                BuildSequenceRevision(
                    "prerequisite-target-structure",
                    (prerequisiteTargets ??
                     Array.Empty<ProgressionPrerequisiteTargetDefinition>())
                    .Select(BuildPrerequisiteTargetStructureRevision)),
                BuildSequenceRevision(
                    "research-raw-state",
                    (states ?? Array.Empty<ResearchProgressionStateRecord>())
                    .Select(BuildResearchRawStateRevision)),
                BuildSequenceRevision(
                    "research-snapshot",
                    (snapshots ??
                     Array.Empty<ResearchProgressionSnapshot>())
                    .Where(snapshot =>
                        snapshot?.Definition?.Identity != null)
                    .OrderBy(
                        snapshot => snapshot.Definition.Identity.Id,
                        StringComparer.Ordinal)
                    .Select(BuildResearchSnapshotRevision)),
                BuildDiagnosticRevision(diagnostics)
            };
            return ProgressionContractHash.Compute(segments.ToArray());
        }

        private static string BuildTrainingRevision(
            string catalogSetId,
            string catalogRevision,
            IReadOnlyList<TroopProgressionDefinition> definitions,
            IReadOnlyList<ProgressionPrerequisiteTargetDefinition>
                prerequisiteTargets,
            IReadOnlyList<TroopProgressionStateRecord> states,
            IEnumerable<TroopProgressionSnapshot> snapshots,
            IEnumerable<ProgressionDiagnostic> diagnostics,
            bool nullStateCollection,
            bool stateLimitExceeded,
            ProgressionTimestampPolicy timestampPolicy)
        {
            var segments = new List<string>
            {
                "training-state",
                "catalog-set-id",
                IdentifierRevisionValue(catalogSetId),
                "catalog-revision",
                IdentifierRevisionValue(catalogRevision),
                nullStateCollection ? "null-collection" : "collection",
                stateLimitExceeded ? "truncated" : "complete",
                BuildTimestampPolicyRevision(timestampPolicy),
                BuildSequenceRevision(
                    "training-definition-structure",
                    (definitions ??
                     Array.Empty<TroopProgressionDefinition>())
                    .Select(BuildTroopDefinitionStructureRevision)),
                BuildSequenceRevision(
                    "prerequisite-target-structure",
                    (prerequisiteTargets ??
                     Array.Empty<ProgressionPrerequisiteTargetDefinition>())
                    .Select(BuildPrerequisiteTargetStructureRevision)),
                BuildSequenceRevision(
                    "training-raw-state",
                    (states ?? Array.Empty<TroopProgressionStateRecord>())
                    .Select(BuildTrainingRawStateRevision)),
                BuildSequenceRevision(
                    "training-snapshot",
                    (snapshots ?? Array.Empty<TroopProgressionSnapshot>())
                    .Where(snapshot =>
                        snapshot?.Definition?.Identity != null)
                    .OrderBy(
                        snapshot => snapshot.Definition.Identity.Id,
                        StringComparer.Ordinal)
                    .Select(BuildTrainingSnapshotRevision)),
                BuildDiagnosticRevision(diagnostics)
            };
            return ProgressionContractHash.Compute(segments.ToArray());
        }

        private static string BuildTimestampPolicyRevision(
            ProgressionTimestampPolicy policy)
        {
            return ProgressionContractHash.Compute(
                "timestamp-policy",
                IdentifierRevisionValue(policy?.PolicyVersion),
                Invariant(policy?.MinimumUtcTimestamp ?? 0),
                Invariant(policy?.MaximumUtcTimestamp ?? 0),
                Invariant(policy?.MaximumRetentionAgeSeconds ?? 0),
                Invariant(policy?.MaximumFutureLeadSeconds ?? 0));
        }

        private static string BuildResearchDefinitionStructureRevision(
            ResearchProgressionDefinition definition)
        {
            if (definition == null)
            {
                return ProgressionContractHash.Compute(
                    "research-definition-structure",
                    "null-definition");
            }

            return ProgressionContractHash.Compute(
                "research-definition-structure",
                SourceIdentityStatusToken(definition.Identity),
                SourceIdentityStatusToken(definition.CostProfile?.Identity),
                SourceIdentityStatusToken(definition.DurationProfile?.Identity),
                BuildPrerequisiteIdentityStatus(definition.Prerequisites),
                BuildSourceIdentityCollectionStatus(definition.EffectProfiles));
        }

        private static string BuildTroopDefinitionStructureRevision(
            TroopProgressionDefinition definition)
        {
            if (definition == null)
            {
                return ProgressionContractHash.Compute(
                    "training-definition-structure",
                    "null-definition");
            }

            return ProgressionContractHash.Compute(
                "training-definition-structure",
                SourceIdentityStatusToken(definition.Identity),
                SourceIdentityStatusToken(definition.CostProfile?.Identity),
                SourceIdentityStatusToken(definition.DurationProfile?.Identity),
                BuildPrerequisiteIdentityStatus(definition.Prerequisites),
                SourceIdentityStatusToken(definition.BattleProfile),
                SourceIdentityStatusToken(definition.InventoryPolicy));
        }

        private static string BuildPrerequisiteTargetStructureRevision(
            ProgressionPrerequisiteTargetDefinition target)
        {
            return ProgressionContractHash.Compute(
                "prerequisite-target-structure",
                target == null
                    ? "null-target"
                    : SourceIdentityStatusToken(target.Identity));
        }

        private static string BuildPrerequisiteIdentityStatus(
            IReadOnlyList<ProgressionPrerequisite> prerequisites)
        {
            if (prerequisites == null)
            {
                return "null-prerequisite-list";
            }

            if (prerequisites.Count > MaximumPrerequisitesPerDefinition)
            {
                return "prerequisite-identities|over-limit";
            }

            for (int index = 0; index < prerequisites.Count; index++)
            {
                ProgressionPrerequisite prerequisite = prerequisites[index];
                if (prerequisite == null)
                {
                    return "null-prerequisite|" + Invariant(index);
                }

                ProgressionIdentifierValidation validation =
                    ProgressionText.ValidateIdentifier(
                        prerequisite.DefinitionId);
                if (validation != ProgressionIdentifierValidation.Valid)
                {
                    return "invalid-prerequisite|" +
                           Invariant(index) +
                           "|" +
                           IdentifierStatusToken(validation);
                }
            }

            return "valid-prerequisite-identities";
        }

        private static string BuildSourceIdentityCollectionStatus(
            IReadOnlyList<ProgressionSourceIdentity> identities)
        {
            if (identities == null)
            {
                return "null-source-identity-list";
            }

            if (identities.Count > MaximumEffectsPerResearch)
            {
                return "source-identities|over-limit";
            }

            for (int index = 0; index < identities.Count; index++)
            {
                ProgressionSourceIdentity identity = identities[index];
                if (!IsValidAnySourceIdentity(identity))
                {
                    return ProgressionContractHash.Compute(
                        "invalid-source-identity",
                        Invariant(index),
                        SourceIdentityStatusToken(identity));
                }
            }

            return "valid-source-identities";
        }

        private static string SourceIdentityStatusToken(
            ProgressionSourceIdentity identity)
        {
            if (identity == null)
            {
                return "source-identity|null";
            }

            return ProgressionContractHash.Compute(
                "source-identity-status",
                IdentifierStatusToken(
                    ProgressionText.ValidateIdentifier(identity.Id)),
                IdentifierStatusToken(
                    ProgressionText.ValidateIdentifier(
                        identity.SchemaVersion)),
                IdentifierStatusToken(
                    ProgressionText.ValidateIdentifier(
                        identity.ContentVersion)),
                IdentifierStatusToken(
                    ProgressionText.ValidateIdentifier(
                        identity.SourceRevision)),
                IsSha256(identity.RawSha256)
                    ? "sha256-valid"
                    : identity.RawSha256 == null
                        ? "sha256-null"
                        : identity.RawSha256.Length == 0
                            ? "sha256-empty"
                            : "sha256-invalid");
        }

        private static string BuildResearchRawStateRevision(
            ResearchProgressionStateRecord state,
            int index)
        {
            if (state == null)
            {
                return ProgressionContractHash.Compute(
                    "research-raw-state",
                    Invariant(index),
                    "null");
            }

            return ProgressionContractHash.Compute(
                "research-raw-state",
                Invariant(index),
                "value",
                IdentifierRevisionValue(state.DefinitionId),
                IdentifierRevisionValue(state.DefinitionContentVersion),
                Invariant(state.Level),
                state.HasActiveLegacyOrder ? "1" : "0",
                Invariant(state.CompletionTimestamp));
        }

        private static string BuildTrainingRawStateRevision(
            TroopProgressionStateRecord state,
            int index)
        {
            if (state == null)
            {
                return ProgressionContractHash.Compute(
                    "training-raw-state",
                    Invariant(index),
                    "null");
            }

            return ProgressionContractHash.Compute(
                "training-raw-state",
                Invariant(index),
                "value",
                IdentifierRevisionValue(state.DefinitionId),
                IdentifierRevisionValue(state.DefinitionContentVersion),
                Invariant(state.ActiveCount),
                Invariant(state.WoundedCount),
                Invariant(state.ReservedCount));
        }

        private static string BuildResearchSnapshotRevision(
            ResearchProgressionSnapshot snapshot)
        {
            return ProgressionContractHash.Compute(
                "research-snapshot",
                IdentifierRevisionValue(snapshot.Definition.Identity.Id),
                Invariant(snapshot.Level),
                StateOriginToken(snapshot.Origin));
        }

        private static string BuildTrainingSnapshotRevision(
            TroopProgressionSnapshot snapshot)
        {
            return ProgressionContractHash.Compute(
                "training-snapshot",
                IdentifierRevisionValue(snapshot.Definition.Identity.Id),
                Invariant(snapshot.ActiveCount),
                Invariant(snapshot.WoundedCount),
                Invariant(snapshot.ReservedCount),
                StateOriginToken(snapshot.Origin));
        }

        private static string BuildDiagnosticRevision(
            IEnumerable<ProgressionDiagnostic> diagnostics)
        {
            return BuildSequenceRevision(
                "diagnostics",
                SortDiagnostics(diagnostics).Select(diagnostic =>
                    ProgressionContractHash.Compute(
                        "diagnostic",
                        Invariant((long)diagnostic.Domain),
                        IdentifierRevisionValue(diagnostic.DefinitionId),
                        Invariant((long)diagnostic.Code),
                        Invariant(diagnostic.SourceIndex))));
        }

        private static string BuildSequenceRevision(
            string label,
            IEnumerable<string> values)
        {
            const int batchSize = 128;
            var batchHashes = new List<string>();
            var batch = new List<string>(batchSize + 2);
            long valueCount = 0;
            int batchIndex = 0;
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (batch.Count == 0)
                {
                    batch.Add(label + "-batch");
                    batch.Add(Invariant(batchIndex));
                }

                batch.Add(value ?? string.Empty);
                valueCount++;
                if (batch.Count != batchSize + 2)
                {
                    continue;
                }

                batchHashes.Add(
                    ProgressionContractHash.Compute(batch.ToArray()));
                batch.Clear();
                batchIndex++;
            }

            if (batch.Count > 0)
            {
                batchHashes.Add(
                    ProgressionContractHash.Compute(batch.ToArray()));
            }

            var root = new List<string>(batchHashes.Count + 3)
            {
                label,
                Invariant(valueCount),
                batchHashes.Count == 0 ? "empty" : "batched"
            };
            root.AddRange(batchHashes);
            return ProgressionContractHash.Compute(root.ToArray());
        }

        private static string IdentifierRevisionValue(string value)
        {
            ProgressionIdentifierValidation validation =
                ProgressionText.ValidateIdentifier(value);
            return validation == ProgressionIdentifierValidation.Valid
                ? "valid|" + value
                : "invalid|" + IdentifierStatusToken(validation);
        }

        private static string IdentifierStatusToken(
            ProgressionIdentifierValidation validation)
        {
            switch (validation)
            {
                case ProgressionIdentifierValidation.Valid:
                    return "valid";
                case ProgressionIdentifierValidation.Null:
                    return "null";
                case ProgressionIdentifierValidation.Empty:
                    return "empty";
                case ProgressionIdentifierValidation.TooLong:
                    return "too-long";
                case ProgressionIdentifierValidation.Whitespace:
                    return "whitespace";
                case ProgressionIdentifierValidation.Control:
                    return "control";
                case ProgressionIdentifierValidation.UnpairedHighSurrogate:
                    return "unpaired-high-surrogate";
                case ProgressionIdentifierValidation.UnpairedLowSurrogate:
                    return "unpaired-low-surrogate";
                case ProgressionIdentifierValidation.Utf8TooLong:
                    return "utf8-too-long";
                default:
                    return "invalid-utf8";
            }
        }

        private static string StateOriginToken(ProgressionStateOrigin origin)
        {
            switch (origin)
            {
                case ProgressionStateOrigin.Saved:
                    return "saved";
                case ProgressionStateOrigin.EffectiveInitialUnpersisted:
                    return "effective-initial-unpersisted";
                default:
                    return "invalid-origin";
            }
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
            return new ProgressionDiagnostic(
                code,
                domain,
                SafeDiagnosticId(definitionId),
                sourceIndex);
        }

        private static string SafeDiagnosticId(string value)
        {
            return ProgressionText.IsValidIdentifier(value)
                ? value
                : string.Empty;
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
