using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AL.Core.SaveAuthority;
using AL.Narrative.Nvs01.Contracts;

namespace AL.Narrative.Nvs01
{
    internal sealed class Nvs01ConsequencePlanner
    {
        private readonly Nvs01ConsequenceDependencyAuthority
            _dependencyAuthority;

        internal Nvs01ConsequencePlanner(
            Nvs01ConsequenceDependencyAuthority dependencyAuthority)
        {
            _dependencyAuthority = dependencyAuthority ??
                throw new ArgumentNullException(nameof(dependencyAuthority));
        }

        internal Nvs01ConsequencePlanningResult Plan(
            Nvs01ConsequencePlanningContext context)
        {
            Nvs01ConsequencePlanningResult rejected = ValidateCommon(context);
            if (rejected != null) return rejected;

            Nvs01MutationPlan mutation = context.QuestMutation;
            if (mutation.IsReplayVerification)
            {
                return PlanReplay(context);
            }

            if (string.Equals(
                    mutation.TriggerEventId,
                    Nvs01ConsequenceContract.ArenaSuccessEventId,
                    StringComparison.Ordinal))
            {
                return PlanArenaSuccess(context);
            }

            if (string.Equals(
                    mutation.TriggerEventId,
                    Nvs01ConsequenceContract.ReportConclusionEventId,
                    StringComparison.Ordinal))
            {
                return PlanReportCompletion(context);
            }

            return Reject(
                Nvs01ConsequencePlanningStatus
                    .RejectedQuestTransitionMismatch);
        }

        private static Nvs01ConsequencePlanningResult PlanArenaSuccess(
            Nvs01ConsequencePlanningContext context)
        {
            Nvs01MutationPlan mutation = context.QuestMutation;
            Nvs01QuestSnapshot expected = mutation.Expected;
            Nvs01QuestSnapshot candidate = mutation.Candidate;
            NvsEncounterRequest request = expected.CurrentEncounter;

            if (!Matches(
                    mutation.ConsequenceIntentIds,
                    Nvs01ConsequenceContract.TearConsequenceId) ||
                expected.ConsequenceIntentIds.Count != 0 ||
                !Matches(
                    candidate.ConsequenceIntentIds,
                    Nvs01ConsequenceContract.TearConsequenceId) ||
                !string.Equals(
                    expected.StateId,
                    Nvs01ConsequenceContract.InvestigateStateId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    candidate.StateId,
                    Nvs01ConsequenceContract.ReportStateId,
                    StringComparison.Ordinal) ||
                request == null ||
                (expected.EncounterStatus != Nvs01EncounterStatus.Requested &&
                 expected.EncounterStatus != Nvs01EncounterStatus.Active) ||
                candidate.EncounterStatus != Nvs01EncounterStatus.Resolved ||
                candidate.CurrentEncounter != null ||
                !SameRealm(expected, candidate) ||
                !ValidateRequest(request, expected.CommittedRealmId) ||
                !string.Equals(
                    candidate.LastEncounterCorrelationId,
                    request.CorrelationId,
                    StringComparison.Ordinal) ||
                candidate.LastEncounterOutcome != NvsEncounterOutcome.Success ||
                !string.Equals(
                    candidate.LastEncounterEventId,
                    Nvs01ConsequenceContract.ArenaSuccessEventId,
                    StringComparison.Ordinal) ||
                !ValidateCommittedReceipt(
                    candidate,
                    Nvs01ConsequenceContract.ArenaSuccessEventId,
                    request.CorrelationId,
                    request.CorrelationId,
                    ComputeArenaResultFingerprint(request, candidate)) ||
                !ValidateArenaObjectives(expected, candidate) ||
                !HasArenaStartDialogue(expected) ||
                !HasClearDialogue(candidate))
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedQuestTransitionMismatch);
            }

            Nvs01ConsequencePlanningResult dependency =
                RequireDependency(
                    context.Domain.ArtifactDefinitionStatus);
            if (dependency != null) return dependency;
            if (!context.Capabilities.IsAvailable(
                    Nvs01ConsequenceContract.ArenaHookId) ||
                !context.Capabilities.IsAvailable(
                    Nvs01ConsequenceContract.ArenaSuccessEventId) ||
                !context.Capabilities.IsAvailable(
                    Nvs01ConsequenceContract.TearArtifactId))
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedDependencyUnavailable);
            }

            string operationId =
                Nvs01ConsequenceContract.ArenaOperationPrefix +
                request.CorrelationId;
            if (context.Domain.AcquiredArtifactIds.Count != 0 ||
                context.Domain.AppliedOperationIds.Count != 0 ||
                context.Domain.AppliedEffectKeys.Count != 0 ||
                context.Domain.ApplicationReceipts.Count != 0)
            {
                Nvs01ConsequenceApplicationReceipt receipt =
                    FindReceipt(context.Domain, operationId);
                return Partial(
                    ValidateArenaReceipt(
                        receipt,
                        context.Authority.ProfileId,
                        candidate,
                        candidate.Revision,
                        context.Authority.VerifiedGenerationFingerprint)
                        ? receipt
                        : null);
            }

            var operations = new List<Nvs01ConsequenceOperation>(1)
            {
                new Nvs01ConsequenceOperation(
                    Nvs01ConsequenceContract.TearConsequenceId,
                    Nvs01ConsequenceMutationKind.AcquireArtifact,
                    Nvs01ConsequenceContract.TearArtifactId,
                    0,
                    Nvs01ConsequenceContract.TearArtifactId)
            };

            return Ready(
                context,
                Nvs01ConsequencePlanKind.ArenaSuccess,
                operationId,
                request.CorrelationId,
                candidate.StateId,
                context.Domain.GoldBalance,
                context.Domain.ValeriusAffinity,
                context.Domain.CurrentChapterId,
                string.Empty,
                operations);
        }

        private static Nvs01ConsequencePlanningResult PlanReportCompletion(
            Nvs01ConsequencePlanningContext context)
        {
            Nvs01MutationPlan mutation = context.QuestMutation;
            Nvs01QuestSnapshot expected = mutation.Expected;
            Nvs01QuestSnapshot candidate = mutation.Candidate;

            if (!Matches(
                    mutation.ConsequenceIntentIds,
                    Nvs01ConsequenceContract.GoldConsequenceId,
                    Nvs01ConsequenceContract.AffinityConsequenceId,
                    Nvs01ConsequenceContract.CompletionConsequenceId,
                    Nvs01ConsequenceContract.ChapterConsequenceId) ||
                !Matches(
                    expected.ConsequenceIntentIds,
                    Nvs01ConsequenceContract.TearConsequenceId) ||
                !MatchesExpectedConsequences(
                    candidate.ConsequenceIntentIds) ||
                !string.Equals(
                    expected.StateId,
                    Nvs01ConsequenceContract.ReportStateId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    candidate.StateId,
                    Nvs01ConsequenceContract.CompletedStateId,
                    StringComparison.Ordinal) ||
                !SameRealm(expected, candidate) ||
                !ValidateResolvedSuccess(expected) ||
                !SameLastEncounter(expected, candidate) ||
                !ValidateReportCommittedReceipt(
                    context,
                    expected,
                    candidate) ||
                !ValidateReportObjectives(expected, candidate) ||
                !string.Equals(
                    expected.CurrentDialogueNodeId,
                    Nvs01ConsequenceContract.ReportDialogueId,
                    StringComparison.Ordinal) ||
                !expected.PendingChoice ||
                expected.PendingSemanticActionId.Length != 0 ||
                !string.Equals(
                    candidate.CurrentDialogueNodeId,
                    Nvs01ConsequenceContract.ReportConclusionDialogueId,
                    StringComparison.Ordinal) ||
                !candidate.PendingChoice ||
                candidate.PendingSemanticActionId.Length != 0)
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedQuestTransitionMismatch);
            }

            Nvs01ConsequencePlanningResult dependency =
                RequireDependency(
                    context.Domain.ArtifactDefinitionStatus);
            if (dependency != null) return dependency;
            dependency = RequireDependency(
                context.Domain.GoldDefinitionStatus);
            if (dependency != null) return dependency;
            dependency = RequireDependency(
                context.Domain.AffinityDefinitionStatus);
            if (dependency != null) return dependency;
            dependency = RequireDependency(context.Chapters.Status);
            if (dependency != null || !context.Chapters.IsComplete)
            {
                return dependency ?? Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedDependencyUnavailable);
            }

            if (!context.Capabilities.IsAvailable(
                    Nvs01ConsequenceContract.TearArtifactId) ||
                !context.Capabilities.IsAvailable(
                    Nvs01ConsequenceContract.AbstractChapterId))
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedDependencyUnavailable);
            }

            string arenaOperationId =
                Nvs01ConsequenceContract.ArenaOperationPrefix +
                expected.LastEncounterCorrelationId;
            bool ownsTear = Contains(
                context.Domain.AcquiredArtifactIds,
                Nvs01ConsequenceContract.TearArtifactId);
            bool hasArenaOperation = Contains(
                context.Domain.AppliedOperationIds,
                arenaOperationId);
            bool hasReportOperation = Contains(
                context.Domain.AppliedOperationIds,
                Nvs01ConsequenceContract.ReportOperationId);
            Nvs01ConsequenceApplicationReceipt arenaReceipt =
                FindReceipt(context.Domain, arenaOperationId);
            bool hasArenaEffect = Contains(
                context.Domain.AppliedEffectKeys,
                Nvs01ConsequenceContract.TearConsequenceId);
            bool hasAnyReportEffect = HasAnyReportEffect(
                context.Domain.AppliedEffectKeys);
            Nvs01ConsequenceApplicationReceipt reportReceipt =
                FindReceipt(
                    context.Domain,
                    Nvs01ConsequenceContract.ReportOperationId);
            bool exactArenaState =
                context.Domain.AcquiredArtifactIds.Count == 1 &&
                ownsTear &&
                context.Domain.AppliedOperationIds.Count == 1 &&
                hasArenaOperation &&
                context.Domain.AppliedEffectKeys.Count == 1 &&
                hasArenaEffect &&
                context.Domain.ApplicationReceipts.Count == 1 &&
                ValidateArenaReceipt(
                    arenaReceipt,
                    context.Authority.ProfileId,
                    expected,
                    expected.Revision - 1,
                    expected.LastOperation?
                        .ExpectedGenerationFingerprint);
            if (!exactArenaState || hasReportOperation ||
                hasAnyReportEffect || reportReceipt != null)
            {
                Nvs01ConsequenceApplicationReceipt recovery =
                    reportReceipt != null
                        ? ValidateReportRecoveryChain(
                            arenaReceipt,
                            reportReceipt,
                            context,
                            expected,
                            expected.Revision - 1,
                            expected.LastOperation?
                                .ExpectedGenerationFingerprint,
                            expected.Revision)
                            ? reportReceipt
                            : null
                        : ValidateArenaReceipt(
                            arenaReceipt,
                            context.Authority.ProfileId,
                            expected,
                            expected.Revision - 1,
                            expected.LastOperation?
                                .ExpectedGenerationFingerprint)
                            ? arenaReceipt
                            : null;
                return Partial(recovery);
            }

            string targetChapter = Nvs01ConsequenceContract.ChapterForRealm(
                expected.CommittedRealmId);
            if (targetChapter.Length == 0 ||
                !TryResolveChapter(
                    context.Chapters,
                    expected.CommittedRealmId,
                    targetChapter,
                    context.Domain.CurrentChapterId,
                    out string resultingChapter))
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedChapterIncompatible);
            }

            long resultingGold;
            try
            {
                resultingGold = checked(
                    context.Domain.GoldBalance +
                    Nvs01ConsequenceContract.GoldAmount);
            }
            catch (OverflowException)
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus.RejectedOverflow);
            }

            float affinity = context.Domain.ValeriusAffinity;
            if (affinity >
                Nvs01ConsequenceContract.MaximumAffinity -
                Nvs01ConsequenceContract.AffinityAmount)
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus.RejectedOverflow);
            }

            float resultingAffinity =
                affinity + Nvs01ConsequenceContract.AffinityAmount;
            if (float.IsNaN(resultingAffinity) ||
                float.IsInfinity(resultingAffinity) ||
                resultingAffinity < Nvs01ConsequenceContract.MinimumAffinity ||
                resultingAffinity > Nvs01ConsequenceContract.MaximumAffinity)
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus.RejectedOverflow);
            }

            var operations = new List<Nvs01ConsequenceOperation>(4)
            {
                new Nvs01ConsequenceOperation(
                    Nvs01ConsequenceContract.GoldConsequenceId,
                    Nvs01ConsequenceMutationKind.CreditResource,
                    Nvs01ConsequenceContract.GoldResourceId,
                    Nvs01ConsequenceContract.GoldAmount,
                    string.Empty),
                new Nvs01ConsequenceOperation(
                    Nvs01ConsequenceContract.AffinityConsequenceId,
                    Nvs01ConsequenceMutationKind.AdjustAffinity,
                    Nvs01ConsequenceContract.ValeriusNpcId,
                    (long)Nvs01ConsequenceContract.AffinityAmount,
                    string.Empty),
                new Nvs01ConsequenceOperation(
                    Nvs01ConsequenceContract.CompletionConsequenceId,
                    Nvs01ConsequenceMutationKind.CompleteQuest,
                    Nvs01ConsequenceContract.QuestId,
                    0,
                    Nvs01ConsequenceContract.CompletedStateId),
                new Nvs01ConsequenceOperation(
                    Nvs01ConsequenceContract.ChapterConsequenceId,
                    Nvs01ConsequenceMutationKind.UnlockChapter,
                    Nvs01ConsequenceContract.AbstractChapterId,
                    0,
                    targetChapter)
            };

            return Ready(
                context,
                Nvs01ConsequencePlanKind.ReportCompletion,
                Nvs01ConsequenceContract.ReportOperationId,
                expected.LastEncounterCorrelationId,
                candidate.StateId,
                resultingGold,
                resultingAffinity,
                resultingChapter,
                targetChapter,
                operations);
        }

        private static Nvs01ConsequencePlanningResult PlanReplay(
            Nvs01ConsequencePlanningContext context)
        {
            Nvs01MutationPlan mutation = context.QuestMutation;
            Nvs01QuestSnapshot snapshot = mutation.Candidate;
            if (!Nvs01ProgressCodec.Equivalent(
                    mutation.Expected,
                    mutation.Candidate) ||
                mutation.ConsequenceIntentIds.Count != 0 ||
                snapshot.LastOperation == null)
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedQuestTransitionMismatch);
            }

            if (string.Equals(
                    snapshot.LastOperation.EventId,
                    Nvs01ConsequenceContract.ArenaSuccessEventId,
                    StringComparison.Ordinal))
            {
                if (!string.Equals(
                        snapshot.StateId,
                        Nvs01ConsequenceContract.ReportStateId,
                        StringComparison.Ordinal) ||
                    !Matches(
                        snapshot.ConsequenceIntentIds,
                        Nvs01ConsequenceContract.TearConsequenceId) ||
                    !ValidateResolvedSuccess(snapshot) ||
                    !ValidateCommittedReceipt(
                        snapshot,
                        Nvs01ConsequenceContract.ArenaSuccessEventId,
                        snapshot.LastEncounterCorrelationId,
                        snapshot.LastEncounterCorrelationId,
                        ComputeArenaResultFingerprint(snapshot)))
                {
                    return Reject(
                        Nvs01ConsequencePlanningStatus
                            .RejectedQuestTransitionMismatch);
                }

                string operationId =
                    Nvs01ConsequenceContract.ArenaOperationPrefix +
                    snapshot.LastEncounterCorrelationId;
                Nvs01ConsequenceApplicationReceipt receipt =
                    FindReceipt(context.Domain, operationId);
                bool exact =
                    Contains(
                        context.Domain.AcquiredArtifactIds,
                        Nvs01ConsequenceContract.TearArtifactId) &&
                    Contains(
                        context.Domain.AppliedOperationIds,
                        operationId) &&
                    context.Domain.AcquiredArtifactIds.Count == 1 &&
                    context.Domain.AppliedOperationIds.Count == 1 &&
                    Matches(
                        context.Domain.AppliedEffectKeys,
                        Nvs01ConsequenceContract.TearConsequenceId) &&
                    context.Domain.ApplicationReceipts.Count == 1 &&
                    ValidateArenaReceipt(
                        receipt,
                        context.Authority.ProfileId,
                        mutation.Expected,
                        snapshot.Revision,
                        mutation.Expected.LastOperation?
                            .ExpectedGenerationFingerprint);
                return exact
                    ? AlreadyApplied()
                    : Partial(
                        ValidateArenaReceipt(
                            receipt,
                            context.Authority.ProfileId,
                            mutation.Expected,
                            snapshot.Revision,
                            mutation.Expected.LastOperation?
                                .ExpectedGenerationFingerprint)
                            ? receipt
                            : null);
            }

            if (string.Equals(
                    snapshot.LastOperation.EventId,
                    Nvs01ConsequenceContract.ReportConclusionEventId,
                    StringComparison.Ordinal))
            {
                if (!string.Equals(
                        snapshot.StateId,
                        Nvs01ConsequenceContract.CompletedStateId,
                        StringComparison.Ordinal) ||
                    !MatchesExpectedConsequences(
                        snapshot.ConsequenceIntentIds) ||
                    !ValidateResolvedSuccess(snapshot) ||
                    !ValidateReportReplayCommittedReceipt(
                        context,
                        snapshot))
                {
                    return Reject(
                        Nvs01ConsequencePlanningStatus
                            .RejectedQuestTransitionMismatch);
                }

                string arenaOperationId =
                    Nvs01ConsequenceContract.ArenaOperationPrefix +
                    snapshot.LastEncounterCorrelationId;
                Nvs01ConsequenceApplicationReceipt arenaReceipt =
                    FindReceipt(context.Domain, arenaOperationId);
                Nvs01ConsequenceApplicationReceipt reportReceipt =
                    FindReceipt(
                        context.Domain,
                        Nvs01ConsequenceContract.ReportOperationId);
                bool exact =
                    Contains(
                        context.Domain.AcquiredArtifactIds,
                        Nvs01ConsequenceContract.TearArtifactId) &&
                    Contains(
                        context.Domain.AppliedOperationIds,
                        arenaOperationId) &&
                    Contains(
                        context.Domain.AppliedOperationIds,
                        Nvs01ConsequenceContract.ReportOperationId) &&
                    context.Domain.AcquiredArtifactIds.Count == 1 &&
                    Matches(
                        context.Domain.AppliedOperationIds,
                        arenaOperationId,
                        Nvs01ConsequenceContract.ReportOperationId) &&
                    MatchesExpectedConsequences(
                        context.Domain.AppliedEffectKeys) &&
                    context.Domain.ApplicationReceipts.Count == 2 &&
                    ValidateArenaReceipt(
                        arenaReceipt,
                        context.Authority.ProfileId,
                        mutation.Expected,
                        snapshot.Revision - 2,
                        reportReceipt?
                            .PredecessorExpectedGenerationFingerprint) &&
                    ValidateReportReceipt(
                        reportReceipt,
                        context,
                        snapshot.Revision - 1);
                return exact
                    ? AlreadyApplied()
                    : Partial(
                        reportReceipt != null
                            ? ValidateReportRecoveryChain(
                                arenaReceipt,
                                reportReceipt,
                                context,
                                mutation.Expected,
                                snapshot.Revision - 2,
                                reportReceipt
                                    .PredecessorExpectedGenerationFingerprint,
                                snapshot.Revision - 1)
                                ? reportReceipt
                                : null
                            : ValidateArenaReceipt(
                                arenaReceipt,
                                context.Authority.ProfileId,
                                mutation.Expected,
                                snapshot.Revision - 2,
                                null)
                                ? arenaReceipt
                                : null);
            }

            return Reject(
                Nvs01ConsequencePlanningStatus
                    .RejectedQuestTransitionMismatch);
        }

        private Nvs01ConsequencePlanningResult ValidateCommon(
            Nvs01ConsequencePlanningContext context)
        {
            if (context == null ||
                context.Catalog == null ||
                context.QuestMutation == null ||
                context.Authority == null ||
                context.ExpectedAuthority == null ||
                context.Dependencies == null ||
                context.QuestMutation.Expected == null ||
                context.QuestMutation.Candidate == null)
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus.RejectedMissingInput);
            }

            if (!ValidateCatalog(context.Catalog) ||
                !ValidateQuestIdentity(context.QuestMutation.Expected) ||
                !ValidateQuestIdentity(context.QuestMutation.Candidate))
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedContractMismatch);
            }

            Nvs01MutationPlan mutation = context.QuestMutation;
            if (context.ExpectedQuestRevision < 0 ||
                mutation.Expected.Revision != context.ExpectedQuestRevision)
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedStaleQuestRevision);
            }

            if (mutation.IsReplayVerification)
            {
                if (mutation.Candidate.Revision != mutation.Expected.Revision)
                {
                    return Reject(
                        Nvs01ConsequencePlanningStatus
                            .RejectedQuestTransitionMismatch);
                }
            }
            else if (mutation.Expected.Revision == long.MaxValue ||
                     mutation.Candidate.Revision !=
                     mutation.Expected.Revision + 1)
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedQuestTransitionMismatch);
            }

            ProfileWriteAuthoritySnapshot authority = context.Authority;
            if (authority.Status != ProfileWriteAuthorityStatus.Writable ||
                !string.Equals(
                    authority.ContractVersion,
                    SaveAuthorityTechnicalLimits.ContractVersion,
                    StringComparison.Ordinal) ||
                authority.SaveSchemaVersion !=
                    SaveAuthorityTechnicalLimits
                        .IdentityAwareSaveSchemaVersion ||
                authority.ProfileInitializationVersion !=
                    SaveAuthorityTechnicalLimits
                        .IdentityAwareProfileInitializationVersion ||
                !authority.HasSelectedSourceGeneration ||
                authority.SelectedSourceGeneration ==
                    ProfileAuthoritySourceGeneration.None ||
                !Nvs01AuthorityGuard.IsCanonicalProfileId(
                    authority.ProfileId) ||
                !AuthorityEpochAllocator.IsCanonical(
                    authority.AuthorityEpoch) ||
                !Nvs01AuthorityGuard.IsCanonicalSha256(
                    authority.VerifiedGenerationFingerprint))
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedAuthorityUnavailable);
            }

            ProfileAuthorityExpectation expectedAuthority =
                context.ExpectedAuthority;
            if (!mutation.IsAuthorityBound ||
                !string.Equals(
                    expectedAuthority.ProfileId,
                    authority.ProfileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    expectedAuthority.AuthorityEpoch,
                    authority.AuthorityEpoch,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    expectedAuthority.ExpectedGenerationFingerprint,
                    authority.VerifiedGenerationFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                     mutation.ProfileId,
                     authority.ProfileId,
                     StringComparison.Ordinal) ||
                 !string.Equals(
                     mutation.AuthorityEpoch,
                     authority.AuthorityEpoch,
                     StringComparison.Ordinal) ||
                 !string.Equals(
                     mutation.ExpectedGenerationFingerprint,
                     authority.VerifiedGenerationFingerprint,
                     StringComparison.Ordinal) ||
                mutation.IsReplayVerification &&
                (mutation.Expected.LastOperation == null ||
                 !Nvs01AuthorityGuard.IsCanonicalSha256(
                     mutation.Expected.LastOperation
                         .ExpectedGenerationFingerprint)) ||
                mutation.Candidate.LastOperation == null ||
                !string.Equals(
                    mutation.Candidate.LastOperation
                        .ExpectedGenerationFingerprint,
                    authority.VerifiedGenerationFingerprint,
                    StringComparison.Ordinal))
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedStaleAuthority);
            }

            if (!_dependencyAuthority.IsCurrent(
                    context.Dependencies,
                    context.Catalog,
                    authority.ProfileId,
                    authority.VerifiedGenerationFingerprint))
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedDependencyMalformed);
            }

            Nvs01ConsequenceDomainSnapshot domain = context.Domain;
            if (!Enum.IsDefined(
                    typeof(Nvs01ConsequenceDependencyStatus),
                    domain.ArtifactDefinitionStatus) ||
                !Enum.IsDefined(
                    typeof(Nvs01ConsequenceDependencyStatus),
                    domain.GoldDefinitionStatus) ||
                !Enum.IsDefined(
                    typeof(Nvs01ConsequenceDependencyStatus),
                    domain.AffinityDefinitionStatus) ||
                !Enum.IsDefined(
                    typeof(Nvs01ConsequenceDependencyStatus),
                    context.Chapters.Status) ||
                domain.AcquiredArtifactInputCount < 0 ||
                domain.AcquiredArtifactInputCount >
                    Nvs01ConsequenceContract.MaximumArtifactCount ||
                domain.AppliedOperationInputCount < 0 ||
                domain.AppliedOperationInputCount >
                    Nvs01ConsequenceContract.MaximumAppliedOperationCount ||
                domain.AppliedEffectInputCount < 0 ||
                domain.AppliedEffectInputCount >
                    Nvs01ConsequenceContract.MaximumAppliedEffectCount ||
                domain.ApplicationReceiptInputCount < 0 ||
                domain.ApplicationReceiptInputCount >
                    Nvs01ConsequenceContract
                        .MaximumApplicationReceiptCount ||
                context.Chapters.InputCount < 0 ||
                context.Chapters.InputCount >
                    Nvs01ConsequenceContract.MaximumChapterDefinitionCount ||
                domain.GoldBalance < 0 ||
                float.IsNaN(domain.ValeriusAffinity) ||
                float.IsInfinity(domain.ValeriusAffinity) ||
                domain.ValeriusAffinity <
                    Nvs01ConsequenceContract.MinimumAffinity ||
                domain.ValeriusAffinity >
                    Nvs01ConsequenceContract.MaximumAffinity ||
                !IsOptionalIdentifier(domain.CurrentChapterId) ||
                !IdentifiersAreUniqueAndValid(
                    domain.AcquiredArtifactIds) ||
                !IdentifiersAreUniqueAndValid(
                    domain.AppliedOperationIds) ||
                !IdentifiersAreUniqueAndValid(
                    domain.AppliedEffectKeys) ||
                !ArtifactsAreKnown(domain.AcquiredArtifactIds) ||
                !OperationsAreKnown(domain.AppliedOperationIds) ||
                !EffectKeysAreKnown(domain.AppliedEffectKeys) ||
                !ReceiptsAreCanonical(domain.ApplicationReceipts) ||
                !ReceiptAuthoritiesMatchDomain(
                    context.ReceiptAuthorities,
                    domain.ApplicationReceipts))
            {
                return Reject(
                    Nvs01ConsequencePlanningStatus
                        .RejectedDependencyMalformed);
            }

            return null;
        }

        private static bool ValidateCatalog(Nvs01VerifiedCatalog verified)
        {
            if (!string.Equals(
                    verified.CatalogId,
                    Nvs01CatalogContract.CatalogId,
                    StringComparison.Ordinal) ||
                verified.CanonicalByteLength !=
                    Nvs01ConsequenceContract.PacketByteLength ||
                !string.Equals(
                    verified.CanonicalSha256,
                    Nvs01ConsequenceContract.PacketSha256,
                    StringComparison.Ordinal) ||
                verified.Catalog == null ||
                !string.Equals(
                    verified.Catalog.PacketVersion,
                    Nvs01ConsequenceContract.PacketVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    verified.Catalog.QuestId,
                    Nvs01ConsequenceContract.QuestId,
                    StringComparison.Ordinal) ||
                verified.Catalog.Consequences.Count !=
                    Nvs01ConsequenceContract.ExpectedConsequenceCount)
            {
                return false;
            }

            string[] ids =
            {
                Nvs01ConsequenceContract.TearConsequenceId,
                Nvs01ConsequenceContract.GoldConsequenceId,
                Nvs01ConsequenceContract.AffinityConsequenceId,
                Nvs01ConsequenceContract.CompletionConsequenceId,
                Nvs01ConsequenceContract.ChapterConsequenceId
            };
            string[] targets =
            {
                Nvs01ConsequenceContract.TearArtifactId,
                Nvs01ConsequenceContract.GoldResourceId,
                Nvs01ConsequenceContract.ValeriusNpcId,
                Nvs01ConsequenceContract.QuestId,
                Nvs01ConsequenceContract.AbstractChapterId
            };
            string[] triggers =
            {
                Nvs01ConsequenceContract.ArenaSuccessEventId,
                Nvs01ConsequenceContract.ReportConclusionEventId,
                Nvs01ConsequenceContract.ReportConclusionEventId,
                Nvs01ConsequenceContract.ReportConclusionEventId,
                Nvs01ConsequenceContract.ReportConclusionEventId
            };
            long?[] amounts = { null, 500, 5, null, null };
            bool?[] retained = { true, null, null, null, null };
            for (int index = 0; index < ids.Length; index++)
            {
                Nvs01Consequence consequence =
                    verified.Catalog.Consequences[index];
                if (!string.Equals(
                        consequence.Id,
                        ids[index],
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        consequence.Target,
                        targets[index],
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        consequence.Trigger,
                        triggers[index],
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        consequence.Repeatability,
                        "once",
                        StringComparison.Ordinal) ||
                    consequence.Amount != amounts[index] ||
                    consequence.Retained != retained[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateQuestIdentity(Nvs01QuestSnapshot quest) =>
            string.Equals(
                quest.PacketVersion,
                Nvs01ConsequenceContract.PacketVersion,
                StringComparison.Ordinal) &&
            string.Equals(
                quest.PacketSha256,
                Nvs01ConsequenceContract.PacketSha256,
                StringComparison.Ordinal) &&
            string.Equals(
                quest.QuestId,
                Nvs01ConsequenceContract.QuestId,
                StringComparison.Ordinal) &&
            Nvs01ConsequenceContract.ChapterForRealm(
                quest.CommittedRealmId).Length > 0;

        private static bool ValidateRequest(
            NvsEncounterRequest request,
            string realmId) =>
            request.ContractVersion == Nvs01RuntimeContract.ContractVersion &&
            string.Equals(
                request.QuestId,
                Nvs01ConsequenceContract.QuestId,
                StringComparison.Ordinal) &&
            string.Equals(
                request.StateId,
                Nvs01ConsequenceContract.InvestigateStateId,
                StringComparison.Ordinal) &&
            string.Equals(
                request.ObjectiveId,
                Nvs01ConsequenceContract.ArenaObjectiveId,
                StringComparison.Ordinal) &&
            string.Equals(
                request.HookId,
                Nvs01ConsequenceContract.ArenaHookId,
                StringComparison.Ordinal) &&
            string.Equals(
                request.LocationId,
                Nvs01ConsequenceContract.ArenaLocationId,
                StringComparison.Ordinal) &&
            string.Equals(request.RealmId, realmId, StringComparison.Ordinal) &&
            string.Equals(
                request.SuccessEventId,
                Nvs01ConsequenceContract.ArenaSuccessEventId,
                StringComparison.Ordinal) &&
            string.Equals(
                request.FailureEventId,
                Nvs01ConsequenceContract.ArenaFailureEventId,
                StringComparison.Ordinal) &&
            string.Equals(
                request.CancelledEventId,
                Nvs01ConsequenceContract.ArenaCancelledEventId,
                StringComparison.Ordinal) &&
            string.Equals(
                request.UnavailableEventId,
                Nvs01ConsequenceContract.ArenaUnavailableEventId,
                StringComparison.Ordinal) &&
            string.Equals(
                request.ReturnScene,
                Nvs01ConsequenceContract.ReturnSceneId,
                StringComparison.Ordinal);

        private static bool ValidateCommittedReceipt(
            Nvs01QuestSnapshot candidate,
            string eventId,
            string correlationId,
            string operationId,
            string payloadFingerprint) =>
            candidate.LastOperation != null &&
            candidate.LastOperation.Status == Nvs01CommandStatus.Committed &&
            candidate.LastOperation.Revision == candidate.Revision &&
            string.Equals(
                candidate.LastOperation.OperationId,
                operationId,
                StringComparison.Ordinal) &&
            Nvs01AuthorityGuard.IsCanonicalSha256(
                candidate.LastOperation.PayloadFingerprint) &&
            string.Equals(
                candidate.LastOperation.PayloadFingerprint,
                payloadFingerprint,
                StringComparison.Ordinal) &&
            string.Equals(
                candidate.LastOperation.StateId,
                candidate.StateId,
                StringComparison.Ordinal) &&
            string.Equals(
                candidate.LastOperation.EventId,
                eventId,
                StringComparison.Ordinal) &&
            string.Equals(
                candidate.LastOperation.CorrelationId,
                correlationId,
                StringComparison.Ordinal);

        private static bool ValidateReportCommittedReceipt(
            Nvs01ConsequencePlanningContext context,
            Nvs01QuestSnapshot expected,
            Nvs01QuestSnapshot candidate)
        {
            Nvs01ConsequenceReceiptExpectation expectation =
                context.ReceiptExpectation;
            if (expectation == null ||
                !IsCanonicalGuid(expectation.OperationId))
            {
                return false;
            }

            string computed = ComputeFingerprint(
                expected.QuestId,
                expected.StateId,
                expected.Revision.ToString(CultureInfo.InvariantCulture),
                "PLAYER",
                Nvs01ConsequenceContract.ReportDialogueId,
                "SelectDialogueChoice",
                Nvs01ConsequenceContract.ReportConclusionChoiceId);
            if (!string.Equals(
                    expectation.PayloadFingerprint,
                    computed,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return ValidateCommittedReceipt(
                candidate,
                Nvs01ConsequenceContract.ReportConclusionEventId,
                expected.LastEncounterCorrelationId,
                expectation.OperationId,
                computed);
        }

        private static bool ValidateReportReplayCommittedReceipt(
            Nvs01ConsequencePlanningContext context,
            Nvs01QuestSnapshot snapshot)
        {
            Nvs01ConsequenceReceiptExpectation expectation =
                context.ReceiptExpectation;
            if (expectation == null || snapshot.Revision < 1 ||
                !IsCanonicalGuid(expectation.OperationId))
            {
                return false;
            }

            string computed = ComputeFingerprint(
                snapshot.QuestId,
                Nvs01ConsequenceContract.ReportStateId,
                (snapshot.Revision - 1).ToString(
                    CultureInfo.InvariantCulture),
                "PLAYER",
                Nvs01ConsequenceContract.ReportDialogueId,
                "SelectDialogueChoice",
                Nvs01ConsequenceContract.ReportConclusionChoiceId);
            return string.Equals(
                       expectation.PayloadFingerprint,
                       computed,
                       StringComparison.Ordinal) &&
                   ValidateCommittedReceipt(
                       snapshot,
                       Nvs01ConsequenceContract.ReportConclusionEventId,
                       snapshot.LastEncounterCorrelationId,
                       expectation.OperationId,
                       computed);
        }

        private static string ComputeArenaResultFingerprint(
            NvsEncounterRequest request,
            Nvs01QuestSnapshot candidate) =>
            ComputeFingerprint(
                "ApplyEncounterResult",
                request.CorrelationId,
                request.QuestId,
                request.HookId,
                request.RealmId,
                NvsEncounterOutcome.Success.ToString(),
                Nvs01ConsequenceContract.ArenaSuccessEventId,
                candidate.LastEncounterSnapshotVersion,
                candidate.LastEncounterSnapshotReference);

        private static string ComputeArenaResultFingerprint(
            Nvs01QuestSnapshot snapshot) =>
            ComputeFingerprint(
                "ApplyEncounterResult",
                snapshot.LastEncounterCorrelationId,
                snapshot.QuestId,
                Nvs01ConsequenceContract.ArenaHookId,
                snapshot.CommittedRealmId,
                NvsEncounterOutcome.Success.ToString(),
                Nvs01ConsequenceContract.ArenaSuccessEventId,
                snapshot.LastEncounterSnapshotVersion,
                snapshot.LastEncounterSnapshotReference);

        private static string ComputeFingerprint(params string[] parts)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(
                    string.Join("\u001f", parts ?? Array.Empty<string>()));
                byte[] hash = sha256.ComputeHash(bytes);
                var result = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    result.Append(hash[index].ToString(
                        "x2", CultureInfo.InvariantCulture));
                }
                return result.ToString();
            }
        }

        private static bool ValidateArenaObjectives(
            Nvs01QuestSnapshot expected,
            Nvs01QuestSnapshot candidate) =>
            ValidateObjectives(
                expected,
                Nvs01ObjectiveStatus.Completed,
                Nvs01ObjectiveStatus.Active,
                Nvs01ObjectiveStatus.Inactive) &&
            ValidateObjectives(
                candidate,
                Nvs01ObjectiveStatus.Completed,
                Nvs01ObjectiveStatus.Completed,
                Nvs01ObjectiveStatus.Active);

        private static bool ValidateReportObjectives(
            Nvs01QuestSnapshot expected,
            Nvs01QuestSnapshot candidate) =>
            ValidateObjectives(
                expected,
                Nvs01ObjectiveStatus.Completed,
                Nvs01ObjectiveStatus.Completed,
                Nvs01ObjectiveStatus.Active) &&
            ValidateObjectives(
                candidate,
                Nvs01ObjectiveStatus.Completed,
                Nvs01ObjectiveStatus.Completed,
                Nvs01ObjectiveStatus.Completed);

        private static bool ValidateObjectives(
            Nvs01QuestSnapshot snapshot,
            Nvs01ObjectiveStatus talk,
            Nvs01ObjectiveStatus arena,
            Nvs01ObjectiveStatus report) =>
            snapshot.Objectives.Count == 3 &&
            string.Equals(
                snapshot.Objectives[0].ObjectiveId,
                Nvs01ConsequenceContract.TalkObjectiveId,
                StringComparison.Ordinal) &&
            snapshot.Objectives[0].Status == talk &&
            string.Equals(
                snapshot.Objectives[1].ObjectiveId,
                Nvs01ConsequenceContract.ArenaObjectiveId,
                StringComparison.Ordinal) &&
            snapshot.Objectives[1].Status == arena &&
            string.Equals(
                snapshot.Objectives[2].ObjectiveId,
                Nvs01ConsequenceContract.ReportObjectiveId,
                StringComparison.Ordinal) &&
            snapshot.Objectives[2].Status == report;

        private static bool ValidateResolvedSuccess(
            Nvs01QuestSnapshot snapshot) =>
            snapshot.EncounterStatus == Nvs01EncounterStatus.Resolved &&
            snapshot.CurrentEncounter == null &&
            IsCanonicalGuid(snapshot.LastEncounterCorrelationId) &&
            snapshot.LastEncounterOutcome == NvsEncounterOutcome.Success &&
            string.Equals(
                snapshot.LastEncounterEventId,
                Nvs01ConsequenceContract.ArenaSuccessEventId,
                StringComparison.Ordinal);

        private static bool SameLastEncounter(
            Nvs01QuestSnapshot left,
            Nvs01QuestSnapshot right) =>
            left.EncounterStatus == right.EncounterStatus &&
            left.CurrentEncounter == null &&
            right.CurrentEncounter == null &&
            string.Equals(
                left.LastEncounterCorrelationId,
                right.LastEncounterCorrelationId,
                StringComparison.Ordinal) &&
            left.LastEncounterOutcome == right.LastEncounterOutcome &&
            string.Equals(
                left.LastEncounterEventId,
                right.LastEncounterEventId,
                StringComparison.Ordinal) &&
            string.Equals(
                left.LastEncounterSnapshotVersion,
                right.LastEncounterSnapshotVersion,
                StringComparison.Ordinal) &&
            string.Equals(
                left.LastEncounterSnapshotReference,
                right.LastEncounterSnapshotReference,
                StringComparison.Ordinal);

        private static bool SameRealm(
            Nvs01QuestSnapshot left,
            Nvs01QuestSnapshot right) =>
            string.Equals(
                left.CommittedRealmId,
                right.CommittedRealmId,
                StringComparison.Ordinal) &&
            Nvs01ConsequenceContract.ChapterForRealm(
                left.CommittedRealmId).Length > 0;

        private static bool HasClearDialogue(Nvs01QuestSnapshot snapshot) =>
            snapshot.CurrentDialogueNodeId.Length == 0 &&
            !snapshot.PendingChoice &&
            snapshot.PendingSemanticActionId.Length == 0;

        private static bool HasArenaStartDialogue(
            Nvs01QuestSnapshot snapshot) =>
            string.Equals(
                snapshot.CurrentDialogueNodeId,
                Nvs01ConsequenceContract.ArenaStartDialogueId,
                StringComparison.Ordinal) &&
            !snapshot.PendingChoice &&
            snapshot.PendingSemanticActionId.Length == 0;

        private static Nvs01ConsequencePlanningResult RequireDependency(
            Nvs01ConsequenceDependencyStatus status)
        {
            switch (status)
            {
                case Nvs01ConsequenceDependencyStatus.Available:
                    return null;
                case Nvs01ConsequenceDependencyStatus.Unavailable:
                case Nvs01ConsequenceDependencyStatus.Missing:
                    return Reject(
                        Nvs01ConsequencePlanningStatus
                            .RejectedDependencyUnavailable);
                case Nvs01ConsequenceDependencyStatus.Duplicate:
                case Nvs01ConsequenceDependencyStatus.Malformed:
                default:
                    return Reject(
                        Nvs01ConsequencePlanningStatus
                            .RejectedDependencyMalformed);
            }
        }

        private static bool TryResolveChapter(
            Nvs01ChapterAuthoritySnapshot authority,
            string realmId,
            string targetChapterId,
            string currentChapterId,
            out string resultingChapterId)
        {
            resultingChapterId = string.Empty;
            var byId = new Dictionary<string, Nvs01ChapterReference>(
                authority.Chapters.Count,
                StringComparer.Ordinal);
            var realmOrders = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < authority.Chapters.Count; index++)
            {
                Nvs01ChapterReference chapter = authority.Chapters[index];
                if (chapter == null ||
                    !IsIdentifier(chapter.ChapterId) ||
                    Nvs01ConsequenceContract.ChapterForRealm(
                        chapter.RealmId).Length == 0 ||
                    chapter.ProgressionOrder < 1 ||
                    chapter.ProgressionOrder >
                        Nvs01ConsequenceContract.MaximumChapterOrder ||
                    byId.ContainsKey(chapter.ChapterId) ||
                    !realmOrders.Add(
                        chapter.RealmId + "\n" +
                        chapter.ProgressionOrder))
                {
                    return false;
                }

                byId.Add(chapter.ChapterId, chapter);
            }

            if (!byId.TryGetValue(
                    targetChapterId,
                    out Nvs01ChapterReference target) ||
                target.IsForwardOnly ||
                target.ProgressionOrder != 1 ||
                !string.Equals(
                    target.RealmId,
                    realmId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (currentChapterId.Length == 0 ||
                string.Equals(currentChapterId, "C1", StringComparison.Ordinal) ||
                string.Equals(
                    currentChapterId,
                    "CH0_PROLOGUE",
                    StringComparison.Ordinal) ||
                string.Equals(
                    currentChapterId,
                    "C_OMEN",
                    StringComparison.Ordinal) ||
                string.Equals(
                    currentChapterId,
                    targetChapterId,
                    StringComparison.Ordinal))
            {
                resultingChapterId = targetChapterId;
                return true;
            }

            if (!byId.TryGetValue(
                    currentChapterId,
                    out Nvs01ChapterReference current) ||
                current.IsForwardOnly ||
                !string.Equals(
                    current.RealmId,
                    realmId,
                    StringComparison.Ordinal) ||
                current.ProgressionOrder <= target.ProgressionOrder)
            {
                return false;
            }

            resultingChapterId = currentChapterId;
            return true;
        }

        private static Nvs01ConsequenceApplicationReceipt FindReceipt(
            Nvs01ConsequenceDomainSnapshot domain,
            string operationId)
        {
            for (int index = 0;
                 index < domain.ApplicationReceipts.Count;
                 index++)
            {
                Nvs01ConsequenceApplicationReceipt receipt =
                    domain.ApplicationReceipts[index];
                if (receipt != null && string.Equals(
                        receipt.OperationId,
                        operationId,
                        StringComparison.Ordinal))
                {
                    return receipt;
                }
            }

            return null;
        }

        private static bool ValidateReportRecoveryChain(
            Nvs01ConsequenceApplicationReceipt arenaReceipt,
            Nvs01ConsequenceApplicationReceipt reportReceipt,
            Nvs01ConsequencePlanningContext context,
            Nvs01QuestSnapshot arenaCausalSnapshot,
            long arenaCandidateQuestRevision,
            string arenaExpectedGenerationFingerprint,
            long reportExpectedQuestRevision) =>
            ValidateArenaReceipt(
                arenaReceipt,
                context?.Authority?.ProfileId,
                arenaCausalSnapshot,
                arenaCandidateQuestRevision,
                arenaExpectedGenerationFingerprint) &&
            ValidateReportReceipt(
                reportReceipt,
                context,
                reportExpectedQuestRevision);

        private static bool ValidateArenaReceipt(
            Nvs01ConsequenceApplicationReceipt receipt,
            string profileId,
            Nvs01QuestSnapshot causalSnapshot,
            long candidateQuestRevision,
            string expectedGenerationFingerprint)
        {
            if (receipt == null || causalSnapshot == null)
            {
                return false;
            }

            string correlationId =
                causalSnapshot.LastEncounterCorrelationId;
            string causalPayloadFingerprint =
                ComputeArenaResultFingerprint(causalSnapshot);
            return
            receipt != null &&
            receipt.Kind == Nvs01ConsequencePlanKind.ArenaSuccess &&
            string.Equals(
                receipt.ProfileId,
                profileId,
                StringComparison.Ordinal) &&
            (expectedGenerationFingerprint == null ||
             string.Equals(
                 receipt.ExpectedGenerationFingerprint,
                 expectedGenerationFingerprint,
                 StringComparison.Ordinal)) &&
            string.Equals(
                receipt.CausalOperationId,
                correlationId,
                StringComparison.Ordinal) &&
            string.Equals(
                receipt.CausalPayloadFingerprint,
                causalPayloadFingerprint,
                StringComparison.Ordinal) &&
            string.Equals(
                receipt.RealmId,
                causalSnapshot.CommittedRealmId,
                StringComparison.Ordinal) &&
            string.Equals(
                receipt.CorrelationId,
                correlationId,
                StringComparison.Ordinal) &&
            receipt.CandidateQuestRevision == candidateQuestRevision &&
            ReceiptIsCanonical(receipt);
        }

        private static bool ValidateReportReceipt(
            Nvs01ConsequenceApplicationReceipt receipt,
            Nvs01ConsequencePlanningContext context,
            long expectedQuestRevision)
        {
            Nvs01OperationReceipt causalOperation =
                context?.QuestMutation?.IsReplayVerification == true
                    ? context.QuestMutation.Expected.LastOperation
                    : context?.QuestMutation?.Candidate.LastOperation;
            if (receipt == null ||
                causalOperation == null ||
                context.Chapters.Status !=
                    Nvs01ConsequenceDependencyStatus.Available ||
                !context.Chapters.IsComplete ||
                receipt.Kind !=
                    Nvs01ConsequencePlanKind.ReportCompletion ||
                !string.Equals(
                    receipt.ProfileId,
                    context.Authority.ProfileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    receipt.ExpectedGenerationFingerprint,
                    causalOperation.ExpectedGenerationFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    receipt.CausalOperationId,
                    causalOperation.OperationId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    receipt.CausalPayloadFingerprint,
                    causalOperation.PayloadFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    receipt.RealmId,
                    context.QuestMutation.Candidate.CommittedRealmId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    receipt.CorrelationId,
                    context.QuestMutation.Candidate
                        .LastEncounterCorrelationId,
                    StringComparison.Ordinal) ||
                receipt.ExpectedQuestRevision != expectedQuestRevision ||
                !ReceiptIsCanonical(receipt))
            {
                return false;
            }

            Nvs01ConsequenceApplicationReceipt predecessor = FindReceipt(
                context.Domain,
                Nvs01ConsequenceContract.ArenaOperationPrefix +
                receipt.CorrelationId);
            if (predecessor == null ||
                !string.Equals(
                    receipt.PredecessorReceiptFingerprint,
                    predecessor.PlanFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    receipt.PredecessorExpectedGenerationFingerprint,
                    predecessor.ExpectedGenerationFingerprint,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string target = Nvs01ConsequenceContract.ChapterForRealm(
                receipt.RealmId);
            if (!string.Equals(
                    receipt.TargetChapterId,
                    target,
                    StringComparison.Ordinal) ||
                !TryResolveChapter(
                    context.Chapters,
                    receipt.RealmId,
                    target,
                    receipt.PreviousChapterId,
                    out string expectedResultingChapter))
            {
                return false;
            }

            return string.Equals(
                receipt.ResultingChapterId,
                expectedResultingChapter,
                StringComparison.Ordinal);
        }

        private static bool ReceiptsAreCanonical(
            IReadOnlyList<Nvs01ConsequenceApplicationReceipt> receipts)
        {
            var operations = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < receipts.Count; index++)
            {
                Nvs01ConsequenceApplicationReceipt receipt =
                    receipts[index];
                if (!ReceiptIsCanonical(receipt) ||
                    !operations.Add(receipt.OperationId))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ReceiptAuthoritiesMatchDomain(
            Nvs01ConsequenceReceiptAuthoritySnapshot authorities,
            IReadOnlyList<Nvs01ConsequenceApplicationReceipt> receipts)
        {
            if (authorities == null || receipts == null ||
                authorities.InputCount != receipts.Count ||
                authorities.Entries.Count != receipts.Count)
            {
                return false;
            }

            var operations = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0;
                 index < authorities.Entries.Count;
                 index++)
            {
                Nvs01ConsequenceReceiptAuthorityEntry entry =
                    authorities.Entries[index];
                if (entry == null ||
                    !IsIdentifier(entry.OperationId) ||
                    !Nvs01AuthorityGuard.IsCanonicalSha256(
                        entry.PlanFingerprint) ||
                    !operations.Add(entry.OperationId))
                {
                    return false;
                }
            }

            for (int index = 0; index < receipts.Count; index++)
            {
                Nvs01ConsequenceApplicationReceipt receipt = receipts[index];
                if (receipt == null ||
                    !authorities.TryGetExpectedFingerprint(
                        receipt.OperationId,
                        out string expectedFingerprint) ||
                    !string.Equals(
                        expectedFingerprint,
                        receipt.PlanFingerprint,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ReceiptIsCanonical(
            Nvs01ConsequenceApplicationReceipt receipt)
        {
            if (receipt == null ||
                receipt.ContractVersion !=
                    Nvs01ConsequenceContract.ContractVersion ||
                !Enum.IsDefined(
                    typeof(Nvs01ConsequencePlanKind), receipt.Kind) ||
                !Nvs01AuthorityGuard.IsCanonicalProfileId(
                    receipt.ProfileId) ||
                !Nvs01AuthorityGuard.IsCanonicalSha256(
                    receipt.ExpectedGenerationFingerprint) ||
                !IsCanonicalGuid(receipt.CausalOperationId) ||
                !Nvs01AuthorityGuard.IsCanonicalSha256(
                    receipt.CausalPayloadFingerprint) ||
                Nvs01ConsequenceContract.ChapterForRealm(
                    receipt.RealmId).Length == 0 ||
                !IsCanonicalGuid(receipt.CorrelationId) ||
                receipt.ExpectedQuestRevision < 0 ||
                receipt.ExpectedQuestRevision == long.MaxValue ||
                receipt.CandidateQuestRevision < 0 ||
                receipt.CandidateQuestRevision !=
                    receipt.ExpectedQuestRevision + 1 ||
                receipt.EffectKeyInputCount < 0 ||
                receipt.EffectKeyInputCount >
                    Nvs01ConsequenceContract.MaximumAppliedEffectCount ||
                !IdentifiersAreUniqueAndValid(receipt.EffectKeys) ||
                !EffectKeysAreKnown(receipt.EffectKeys) ||
                receipt.PreviousGoldBalance < 0 ||
                receipt.ResultingGoldBalance < 0 ||
                !IsBoundedAffinity(receipt.PreviousValeriusAffinity) ||
                !IsBoundedAffinity(receipt.ResultingValeriusAffinity) ||
                !IsOptionalIdentifier(receipt.PreviousChapterId) ||
                !IsOptionalIdentifier(receipt.ResultingChapterId) ||
                !IsOptionalIdentifier(receipt.TargetChapterId) ||
                !Nvs01AuthorityGuard.IsCanonicalSha256(
                    receipt.PlanFingerprint) ||
                !receipt.HasCanonicalFingerprint())
            {
                return false;
            }

            if (receipt.Kind == Nvs01ConsequencePlanKind.ArenaSuccess)
            {
                return string.Equals(
                           receipt.OperationId,
                           Nvs01ConsequenceContract.ArenaOperationPrefix +
                           receipt.CorrelationId,
                           StringComparison.Ordinal) &&
                       Matches(
                           receipt.EffectKeys,
                           Nvs01ConsequenceContract.TearConsequenceId) &&
                       receipt.PredecessorReceiptFingerprint.Length == 0 &&
                       receipt.PredecessorExpectedGenerationFingerprint
                           .Length == 0 &&
                       receipt.TargetChapterId.Length == 0 &&
                       receipt.PreviousGoldBalance ==
                           receipt.ResultingGoldBalance &&
                       receipt.PreviousValeriusAffinity.Equals(
                           receipt.ResultingValeriusAffinity) &&
                       string.Equals(
                           receipt.PreviousChapterId,
                           receipt.ResultingChapterId,
                           StringComparison.Ordinal);
            }

            long expectedGold;
            try
            {
                expectedGold = checked(
                    receipt.PreviousGoldBalance +
                    Nvs01ConsequenceContract.GoldAmount);
            }
            catch (OverflowException)
            {
                return false;
            }

            float expectedAffinity = receipt.PreviousValeriusAffinity +
                                     Nvs01ConsequenceContract.AffinityAmount;
            return string.Equals(
                       receipt.OperationId,
                       Nvs01ConsequenceContract.ReportOperationId,
                       StringComparison.Ordinal) &&
                   Nvs01AuthorityGuard.IsCanonicalSha256(
                       receipt.PredecessorReceiptFingerprint) &&
                   Nvs01AuthorityGuard.IsCanonicalSha256(
                       receipt.PredecessorExpectedGenerationFingerprint) &&
                   MatchesReportEffects(receipt.EffectKeys) &&
                   string.Equals(
                       receipt.TargetChapterId,
                       Nvs01ConsequenceContract.ChapterForRealm(
                           receipt.RealmId),
                       StringComparison.Ordinal) &&
                   receipt.ResultingGoldBalance == expectedGold &&
                   IsBoundedAffinity(expectedAffinity) &&
                   receipt.ResultingValeriusAffinity.Equals(
                       expectedAffinity);
        }

        private static bool ArtifactsAreKnown(
            IReadOnlyList<string> values)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (!string.Equals(
                        values[index],
                        Nvs01ConsequenceContract.TearArtifactId,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool OperationsAreKnown(
            IReadOnlyList<string> values)
        {
            for (int index = 0; index < values.Count; index++)
            {
                string value = values[index];
                if (string.Equals(
                        value,
                        Nvs01ConsequenceContract.ReportOperationId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (value == null || !value.StartsWith(
                        Nvs01ConsequenceContract.ArenaOperationPrefix,
                        StringComparison.Ordinal) ||
                    !IsCanonicalGuid(value.Substring(
                        Nvs01ConsequenceContract.ArenaOperationPrefix
                            .Length)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EffectKeysAreKnown(
            IReadOnlyList<string> values)
        {
            IReadOnlyList<string> expected =
                Nvs01ConsequenceContract.ExpectedConsequenceOrder;
            for (int index = 0; index < values.Count; index++)
            {
                if (!Contains(expected, values[index])) return false;
            }

            return true;
        }

        private static bool HasAnyReportEffect(
            IReadOnlyList<string> values)
        {
            for (int index = 1;
                 index < Nvs01ConsequenceContract
                     .ExpectedConsequenceOrder.Count;
                 index++)
            {
                if (Contains(
                        values,
                        Nvs01ConsequenceContract
                            .ExpectedConsequenceOrder[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesReportEffects(
            IReadOnlyList<string> values)
        {
            IReadOnlyList<string> expected =
                Nvs01ConsequenceContract.ExpectedConsequenceOrder;
            if (values == null || values.Count != expected.Count - 1)
            {
                return false;
            }

            for (int index = 0; index < values.Count; index++)
            {
                if (!string.Equals(
                        values[index],
                        expected[index + 1],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBoundedAffinity(float value) =>
            !float.IsNaN(value) &&
            !float.IsInfinity(value) &&
            value >= Nvs01ConsequenceContract.MinimumAffinity &&
            value <= Nvs01ConsequenceContract.MaximumAffinity;

        private static bool MatchesExpectedConsequences(
            IReadOnlyList<string> values)
        {
            IReadOnlyList<string> expected =
                Nvs01ConsequenceContract.ExpectedConsequenceOrder;
            if (values == null || values.Count != expected.Count) return false;
            for (int index = 0; index < expected.Count; index++)
            {
                if (!string.Equals(
                        values[index],
                        expected[index],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Matches(
            IReadOnlyList<string> values,
            params string[] expected)
        {
            if (values == null || values.Count != expected.Length) return false;
            for (int index = 0; index < expected.Length; index++)
            {
                if (!string.Equals(
                        values[index],
                        expected[index],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            string expected)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(
                        values[index],
                        expected,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IdentifiersAreUniqueAndValid(
            IReadOnlyList<string> values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Count; index++)
            {
                if (!IsIdentifier(values[index]) || !seen.Add(values[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsOptionalIdentifier(string value) =>
            value != null && (value.Length == 0 || IsIdentifier(value));

        private static bool IsIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > Nvs01RuntimeContract.MaximumIdentifierLength ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index])) return false;
            }

            return true;
        }

        private static bool IsCanonicalGuid(string value)
        {
            if (!Guid.TryParseExact(value, "D", out Guid parsed) ||
                parsed == Guid.Empty)
            {
                return false;
            }

            return string.Equals(
                parsed.ToString("D"),
                value,
                StringComparison.Ordinal);
        }

        private static Nvs01ConsequencePlanningResult Ready(
            Nvs01ConsequencePlanningContext context,
            Nvs01ConsequencePlanKind kind,
            string operationId,
            string correlationId,
            string nextStateId,
            long resultingGold,
            float resultingAffinity,
            string resultingChapter,
            string targetChapter,
            IList<Nvs01ConsequenceOperation> operations)
        {
            var effectKeys = new List<string>(operations.Count);
            for (int index = 0; index < operations.Count; index++)
            {
                effectKeys.Add(operations[index].ConsequenceId);
            }

            string predecessorReceiptFingerprint = string.Empty;
            string predecessorExpectedGenerationFingerprint = string.Empty;
            if (kind == Nvs01ConsequencePlanKind.ReportCompletion)
            {
                Nvs01ConsequenceApplicationReceipt predecessor =
                    FindReceipt(
                        context.Domain,
                        Nvs01ConsequenceContract.ArenaOperationPrefix +
                        correlationId);
                predecessorReceiptFingerprint =
                    predecessor?.PlanFingerprint ?? string.Empty;
                predecessorExpectedGenerationFingerprint =
                    predecessor?.ExpectedGenerationFingerprint ??
                    string.Empty;
            }

            Nvs01ConsequenceApplicationReceipt receipt =
                Nvs01ConsequenceApplicationReceipt.Create(
                    kind,
                    operationId,
                    context.Authority.ProfileId,
                    context.Authority.VerifiedGenerationFingerprint,
                    context.QuestMutation.Candidate.LastOperation
                        .OperationId,
                    context.QuestMutation.Candidate.LastOperation
                        .PayloadFingerprint,
                    predecessorReceiptFingerprint,
                    predecessorExpectedGenerationFingerprint,
                    context.QuestMutation.Expected.CommittedRealmId,
                    correlationId,
                    context.QuestMutation.Expected.Revision,
                    context.QuestMutation.Candidate.Revision,
                    effectKeys,
                    targetChapter,
                    context.Domain.GoldBalance,
                    resultingGold,
                    context.Domain.ValeriusAffinity,
                    resultingAffinity,
                    context.Domain.CurrentChapterId,
                    resultingChapter);

            var plan = new Nvs01ConsequencePlan(
                kind,
                operationId,
                context.Authority.ProfileId,
                context.Authority.AuthorityEpoch,
                context.Authority.VerifiedGenerationFingerprint,
                context.QuestMutation.Expected.Revision,
                context.QuestMutation.Candidate.Revision,
                context.QuestMutation.Expected.CommittedRealmId,
                correlationId,
                nextStateId,
                resultingGold,
                resultingAffinity,
                resultingChapter,
                operations,
                receipt);
            return new Nvs01ConsequencePlanningResult(
                Nvs01ConsequencePlanningStatus.Ready,
                string.Empty,
                plan,
                null);
        }

        private static Nvs01ConsequencePlanningResult AlreadyApplied() =>
            new Nvs01ConsequencePlanningResult(
                Nvs01ConsequencePlanningStatus.AlreadyApplied,
                string.Empty,
                null,
                null);

        private static Nvs01ConsequencePlanningResult Partial(
            Nvs01ConsequenceApplicationReceipt receipt) =>
            new Nvs01ConsequencePlanningResult(
                Nvs01ConsequencePlanningStatus.RejectedPartialApplication,
                Nvs01ConsequenceDiagnosticCodes.PartialApplication,
                null,
                receipt);

        private static Nvs01ConsequencePlanningResult Reject(
            Nvs01ConsequencePlanningStatus status) =>
            new Nvs01ConsequencePlanningResult(
                status,
                DiagnosticFor(status),
                null,
                null);

        private static string DiagnosticFor(
            Nvs01ConsequencePlanningStatus status)
        {
            switch (status)
            {
                case Nvs01ConsequencePlanningStatus.RejectedMissingInput:
                    return Nvs01ConsequenceDiagnosticCodes.MissingInput;
                case Nvs01ConsequencePlanningStatus.RejectedContractMismatch:
                    return Nvs01ConsequenceDiagnosticCodes.ContractMismatch;
                case Nvs01ConsequencePlanningStatus
                    .RejectedAuthorityUnavailable:
                    return Nvs01ConsequenceDiagnosticCodes
                        .AuthorityUnavailable;
                case Nvs01ConsequencePlanningStatus.RejectedStaleAuthority:
                    return Nvs01ConsequenceDiagnosticCodes.StaleAuthority;
                case Nvs01ConsequencePlanningStatus
                    .RejectedStaleQuestRevision:
                    return Nvs01ConsequenceDiagnosticCodes
                        .StaleQuestRevision;
                case Nvs01ConsequencePlanningStatus
                    .RejectedQuestTransitionMismatch:
                    return Nvs01ConsequenceDiagnosticCodes
                        .QuestTransitionMismatch;
                case Nvs01ConsequencePlanningStatus
                    .RejectedDependencyUnavailable:
                    return Nvs01ConsequenceDiagnosticCodes
                        .DependencyUnavailable;
                case Nvs01ConsequencePlanningStatus
                    .RejectedDependencyMalformed:
                    return Nvs01ConsequenceDiagnosticCodes
                        .DependencyMalformed;
                case Nvs01ConsequencePlanningStatus
                    .RejectedPartialApplication:
                    return Nvs01ConsequenceDiagnosticCodes
                        .PartialApplication;
                case Nvs01ConsequencePlanningStatus.RejectedOverflow:
                    return Nvs01ConsequenceDiagnosticCodes.Overflow;
                case Nvs01ConsequencePlanningStatus
                    .RejectedChapterIncompatible:
                    return Nvs01ConsequenceDiagnosticCodes
                        .ChapterIncompatible;
                default:
                    return Nvs01ConsequenceDiagnosticCodes
                        .DependencyMalformed;
            }
        }
    }
}
