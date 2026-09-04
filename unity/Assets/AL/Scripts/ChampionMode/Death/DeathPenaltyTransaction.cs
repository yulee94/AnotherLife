using System;
using System.Collections.Generic;
using AL.ChampionMode.DeathPenalty;
using AL.Data.Runtime;
using AL.Services.Local;

namespace AL.ChampionMode.Death
{
    internal static class DeathPenaltyTransaction
    {
        internal static DeathPenaltyCommitResult ReplayOrReject(
            SaveGameData save,
            DeathPenaltyCommitRequest request,
            string profileId)
        {
            if (save?.DeathPenalty == null || save.DeathPenalty.Version <= 0)
            {
                return null;
            }

            DeathPenaltyAuthorityState published = save.DeathPenalty;
            if (string.Equals(published.OperationId, request.OperationId, StringComparison.Ordinal))
            {
                if (!string.Equals(published.DeathEventId, request.DeathEventId, StringComparison.Ordinal) ||
                    !string.Equals(published.CombatSessionId, request.CombatSessionId, StringComparison.Ordinal) ||
                    !string.Equals(published.EncounterAttemptId, request.EncounterAttemptId, StringComparison.Ordinal) ||
                    !string.Equals(published.InstanceId, request.InstanceId, StringComparison.Ordinal))
                {
                    return Reject(
                        DeathPenaltyCommitStatus.RejectedCollision,
                        save,
                        DeathPenaltyCommitCodes.Collision);
                }

                if (published.Outcome == DeathPenaltyAuthorityState.OutcomeBelowMaxCommitted)
                {
                    return ReplayBelowMax(save);
                }

                if (published.Outcome == DeathPenaltyAuthorityState.OutcomeOathmarkPaymentRequired)
                {
                    return ReplayPaymentRequired(save);
                }
            }

            if (!string.IsNullOrEmpty(published.DeathEventId) &&
                string.Equals(published.DeathEventId, request.DeathEventId, StringComparison.Ordinal) &&
                !string.Equals(published.OperationId, request.OperationId, StringComparison.Ordinal))
            {
                return Reject(
                    DeathPenaltyCommitStatus.RejectedCollision,
                    save,
                    DeathPenaltyCommitCodes.Collision);
            }

            return null;
        }

        internal static SaveCandidateMutationPreparation Prepare(
            SaveGameData candidate,
            DeathPenaltyCommitRequest request,
            string profileId,
            string generationFingerprint)
        {
            if (candidate == null ||
                !string.Equals(candidate.ProfileId, profileId, StringComparison.Ordinal))
            {
                return SaveCandidateMutationPreparation.Rejected(
                    DeathPenaltyCommitCodes.ProfileMismatch);
            }

            ChampionProgressionState progression = candidate.ChampionProgression;
            if (progression != null &&
                progression.Version == ChampionProgressionState.CurrentVersion &&
                !string.IsNullOrEmpty(progression.ProfileId) &&
                !string.Equals(progression.ProfileId, profileId, StringComparison.Ordinal))
            {
                return SaveCandidateMutationPreparation.Rejected(
                    DeathPenaltyCommitCodes.ProfileMismatch);
            }

            if (!IsInstalledProgression(progression, profileId))
            {
                progression = CreateDefaultProgression(profileId);
            }

            string characterId = DeathPenaltyIds.CharacterId(profileId);
            long ordinal = request.DeathOrdinal > 0L
                ? request.DeathOrdinal
                : (candidate.DeathPenalty?.DeathOrdinal ?? 0L) + 1L;
            if (ordinal <= 0L)
            {
                ordinal = 1L;
            }

            if (progression.CurrentLevel >= progression.MaximumLevel &&
                progression.MaximumLevel > 0)
            {
                candidate.ChampionProgression = progression;
                candidate.DeathPenalty = CreatePaymentRequiredState(
                    request,
                    profileId,
                    characterId,
                    ordinal,
                    generationFingerprint,
                    progression);
                return SaveCandidateMutationPreparation.Prepared();
            }

            string deathStateRevision = DeathPenaltyIds.NextDeathStateRevision(
                candidate.DeathPenalty?.DeathStateRevision);
            string ledgerRevision = string.IsNullOrEmpty(candidate.DeathPenalty?.LedgerRevision)
                ? "al.ledger.rev.initial"
                : candidate.DeathPenalty.LedgerRevision;
            DeathPenaltyPolicySnapshot policy = new DeathPenaltyPolicySnapshot(
                DeathPenaltyIds.PolicyVersion,
                null);
            DeathPenaltyRequest plannerRequest = new DeathPenaltyRequest(
                request.OperationId,
                profileId,
                profileId,
                characterId,
                request.DeathEventId,
                request.CombatSessionId,
                request.EncounterAttemptId,
                request.InstanceId,
                ordinal,
                progression.ProgressionRevision,
                progression.LevelCapPolicyId,
                progression.LevelCapPolicyRevision,
                deathStateRevision,
                DeathPenaltyIds.LedgerVersion,
                ledgerRevision);
            DeathPenaltyDeathStateSnapshot deathState = new DeathPenaltyDeathStateSnapshot(
                DeathPenaltyAuthoritativeDeathStatus.DeadAwaitingPenalty,
                profileId,
                profileId,
                characterId,
                request.DeathEventId,
                request.CombatSessionId,
                request.EncounterAttemptId,
                request.InstanceId,
                ordinal,
                deathStateRevision);
            DeathPenaltyProgressionSnapshot progressionSnapshot =
                new DeathPenaltyProgressionSnapshot(
                    profileId,
                    profileId,
                    characterId,
                    progression.CurrentLevel,
                    progression.MaximumLevel,
                    progression.InLevelExperienceUnits,
                    progression.ExperienceUnitsPerLevel,
                    progression.ProgressionRevision,
                    progression.LevelCapPolicyId,
                    progression.LevelCapPolicyRevision);
            List<DeathPenaltyReceipt> retained = ReconstructReceipts(candidate.DeathPenalty);
            DeathPenaltyReplayLedgerSnapshot ledger = new DeathPenaltyReplayLedgerSnapshot(
                DeathPenaltyReplayLedgerAvailability.Available,
                true,
                DeathPenaltyIds.LedgerVersion,
                ledgerRevision,
                retained);
            DeathPenaltyPlan plan = DeathPenaltyPlanner.Plan(
                plannerRequest,
                deathState,
                progressionSnapshot,
                null,
                policy,
                ledger);
            if (plan.IsCommittedReplay)
            {
                return SaveCandidateMutationPreparation.Duplicate();
            }

            if (!plan.CanCommit ||
                plan.Proposal == null ||
                plan.Proposal.Branch != DeathPenaltyBranch.InLevelExperiencePenalty)
            {
                string plannerCode = string.IsNullOrEmpty(plan.DiagnosticCode)
                    ? DeathPenaltyCommitCodes.InvalidRequest
                    : plan.DiagnosticCode;
                return SaveCandidateMutationPreparation.Rejected(plannerCode);
            }

            string afterRevision = plan.Proposal.RequiresProgressionWrite
                ? DeathPenaltyIds.NextProgressionRevision(progression.ProgressionRevision)
                : progression.ProgressionRevision;
            progression.Version = ChampionProgressionState.CurrentVersion;
            progression.ProfileId = profileId;
            progression.CharacterId = characterId;
            progression.AccountId = profileId;
            progression.CurrentLevel = plan.Proposal.AfterLevel;
            progression.MaximumLevel = plan.Proposal.MaximumLevel;
            progression.InLevelExperienceUnits = plan.Proposal.AfterInLevelExperienceUnits;
            progression.ExperienceUnitsPerLevel = plan.Proposal.ExperienceUnitsPerLevel;
            progression.ProgressionRevision = afterRevision;
            progression.LevelCapPolicyId = plan.Proposal.LevelCapPolicyId;
            progression.LevelCapPolicyRevision = plan.Proposal.LevelCapPolicyRevision;
            DeathPenaltyProgressionSnapshot afterProgression =
                new DeathPenaltyProgressionSnapshot(
                    profileId,
                    profileId,
                    characterId,
                    progression.CurrentLevel,
                    progression.MaximumLevel,
                    progression.InLevelExperienceUnits,
                    progression.ExperienceUnitsPerLevel,
                    afterRevision,
                    progression.LevelCapPolicyId,
                    progression.LevelCapPolicyRevision);
            if (!DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    afterProgression,
                    null,
                    null,
                    out DeathPenaltyReceipt receipt))
            {
                return SaveCandidateMutationPreparation.Rejected(
                    DeathPenaltyCommitCodes.InvalidRequest);
            }

            candidate.ChampionProgression = progression;
            DeathPenaltyAuthorityState state = candidate.DeathPenalty ?? new DeathPenaltyAuthorityState();
            if (state.Receipts == null)
            {
                state.Receipts = new List<DeathPenaltyReceiptState>();
            }

            state.Version = DeathPenaltyAuthorityState.CurrentVersion;
            state.Status = (int)DeathPenaltyAuthoritativeDeathStatus.Resolved;
            state.Outcome = DeathPenaltyAuthorityState.OutcomeBelowMaxCommitted;
            state.ProfileId = profileId;
            state.CharacterId = characterId;
            state.AccountId = profileId;
            state.DeathEventId = request.DeathEventId;
            state.CombatSessionId = request.CombatSessionId;
            state.EncounterAttemptId = request.EncounterAttemptId;
            state.InstanceId = request.InstanceId;
            state.DeathOrdinal = ordinal;
            state.DeathStateRevision = deathStateRevision;
            state.OperationId = request.OperationId;
            state.RequestFingerprint = receipt.RequestFingerprint;
            state.DeathFingerprint = receipt.Proposal.DeathFingerprint;
            state.ReceiptHash = receipt.ReceiptHash;
            state.Branch = (int)DeathPenaltyBranch.InLevelExperiencePenalty;
            state.AfterProgressionRevision = afterRevision;
            state.LedgerVersion = DeathPenaltyIds.LedgerVersion;
            state.LedgerRevision = DeathPenaltyIds.NextLedgerRevision(ledgerRevision);
            state.ExpectedGenerationFingerprint = generationFingerprint ?? string.Empty;
            state.Revision = state.Revision <= 0L ? 1L : state.Revision + 1L;
            state.Receipts.Add(ToState(receipt));
            candidate.DeathPenalty = state;
            return SaveCandidateMutationPreparation.Prepared();
        }

        internal static DeathPenaltyCommitResult MapPublished(
            SaveGameData save,
            bool mutationOccurred,
            bool persisted)
        {
            if (save?.DeathPenalty == null)
            {
                return Reject(
                    DeathPenaltyCommitStatus.RejectedPlanner,
                    save,
                    DeathPenaltyCommitCodes.InvalidRequest);
            }

            if (save.DeathPenalty.Outcome ==
                DeathPenaltyAuthorityState.OutcomeOathmarkPaymentRequired)
            {
                return new DeathPenaltyCommitResult(
                    mutationOccurred
                        ? DeathPenaltyCommitStatus.OathmarkPaymentRequired
                        : DeathPenaltyCommitStatus.ReplayedOathmarkPaymentRequired,
                    mutationOccurred,
                    persisted,
                    false,
                    save.ChampionProgression?.CurrentLevel ?? 0,
                    save.ChampionProgression?.InLevelExperienceUnits ?? 0L,
                    DeathPenaltyCommitCodes.OathmarkPaymentRequired);
            }

            return new DeathPenaltyCommitResult(
                mutationOccurred
                    ? DeathPenaltyCommitStatus.CommittedBelowMax
                    : DeathPenaltyCommitStatus.ReplayedBelowMax,
                mutationOccurred,
                persisted,
                true,
                save.ChampionProgression?.CurrentLevel ?? 0,
                save.ChampionProgression?.InLevelExperienceUnits ?? 0L,
                mutationOccurred
                    ? DeathPenaltyCommitCodes.Committed
                    : DeathPenaltyCommitCodes.Replayed);
        }

        internal static DeathPenaltyCommitResult Reject(
            DeathPenaltyCommitStatus status,
            SaveGameData save,
            string code)
        {
            return new DeathPenaltyCommitResult(
                status,
                false,
                false,
                false,
                save?.ChampionProgression?.CurrentLevel ?? 0,
                save?.ChampionProgression?.InLevelExperienceUnits ?? 0L,
                code);
        }

        internal static DeathPenaltyCommitResult ReplayBelowMax(SaveGameData save)
        {
            return new DeathPenaltyCommitResult(
                DeathPenaltyCommitStatus.ReplayedBelowMax,
                false,
                false,
                true,
                save.ChampionProgression?.CurrentLevel ?? 0,
                save.ChampionProgression?.InLevelExperienceUnits ?? 0L,
                DeathPenaltyCommitCodes.Replayed);
        }

        internal static DeathPenaltyCommitResult ReplayPaymentRequired(SaveGameData save)
        {
            return new DeathPenaltyCommitResult(
                DeathPenaltyCommitStatus.ReplayedOathmarkPaymentRequired,
                false,
                false,
                false,
                save.ChampionProgression?.CurrentLevel ?? 0,
                save.ChampionProgression?.InLevelExperienceUnits ?? 0L,
                DeathPenaltyCommitCodes.OathmarkPaymentRequired);
        }

        private static bool IsInstalledProgression(
            ChampionProgressionState progression,
            string profileId)
        {
            return progression != null &&
                   progression.Version == ChampionProgressionState.CurrentVersion &&
                   progression.CurrentLevel > 0 &&
                   progression.MaximumLevel >= progression.CurrentLevel &&
                   !string.IsNullOrEmpty(progression.ProgressionRevision) &&
                   string.Equals(progression.ProfileId, profileId, StringComparison.Ordinal);
        }

        private static ChampionProgressionState CreateDefaultProgression(string profileId)
        {
            return new ChampionProgressionState
            {
                Version = ChampionProgressionState.CurrentVersion,
                ProfileId = profileId,
                CharacterId = DeathPenaltyIds.CharacterId(profileId),
                AccountId = profileId,
                CurrentLevel = DeathPenaltyIds.DefaultStartingLevel,
                MaximumLevel = DeathPenaltyIds.DefaultMaximumLevel,
                InLevelExperienceUnits = 0L,
                ExperienceUnitsPerLevel = DeathPenaltyIds.DefaultExperienceUnitsPerLevel,
                ProgressionRevision = "al.prog.initial",
                LevelCapPolicyId = DeathPenaltyIds.LevelCapPolicyId,
                LevelCapPolicyRevision = DeathPenaltyIds.LevelCapPolicyRevision
            };
        }

        private static DeathPenaltyAuthorityState CreatePaymentRequiredState(
            DeathPenaltyCommitRequest request,
            string profileId,
            string characterId,
            long ordinal,
            string generationFingerprint,
            ChampionProgressionState progression)
        {
            return new DeathPenaltyAuthorityState
            {
                Version = DeathPenaltyAuthorityState.CurrentVersion,
                Status = (int)DeathPenaltyAuthoritativeDeathStatus.DeadAwaitingPenalty,
                Outcome = DeathPenaltyAuthorityState.OutcomeOathmarkPaymentRequired,
                ProfileId = profileId,
                CharacterId = characterId,
                AccountId = profileId,
                DeathEventId = request.DeathEventId,
                CombatSessionId = request.CombatSessionId,
                EncounterAttemptId = request.EncounterAttemptId,
                InstanceId = request.InstanceId,
                DeathOrdinal = ordinal,
                DeathStateRevision = DeathPenaltyIds.NextDeathStateRevision(string.Empty),
                OperationId = request.OperationId,
                RequestFingerprint = string.Empty,
                DeathFingerprint = string.Empty,
                ReceiptHash = string.Empty,
                Branch = (int)DeathPenaltyBranch.MaxLevelOathmarkRevive,
                AfterProgressionRevision = progression.ProgressionRevision,
                LedgerVersion = DeathPenaltyIds.LedgerVersion,
                LedgerRevision = "al.ledger.rev.initial",
                ExpectedGenerationFingerprint = generationFingerprint ?? string.Empty,
                Revision = 1L,
                Receipts = new List<DeathPenaltyReceiptState>()
            };
        }

        private static List<DeathPenaltyReceipt> ReconstructReceipts(
            DeathPenaltyAuthorityState state)
        {
            var receipts = new List<DeathPenaltyReceipt>();
            if (state?.Receipts == null)
            {
                return receipts;
            }

            for (int i = 0; i < state.Receipts.Count; i++)
            {
                DeathPenaltyReceipt reconstructed = FromState(state.Receipts[i]);
                if (reconstructed != null)
                {
                    receipts.Add(reconstructed);
                }
            }

            return receipts;
        }

        private static DeathPenaltyReceiptState ToState(DeathPenaltyReceipt receipt)
        {
            DeathPenaltyCommitProposal proposal = receipt.Proposal;
            return new DeathPenaltyReceiptState
            {
                OperationId = receipt.OperationId,
                RequestFingerprint = receipt.RequestFingerprint,
                DeathFingerprint = proposal.DeathFingerprint,
                ReceiptHash = receipt.ReceiptHash,
                AccountId = proposal.AccountId,
                ProfileId = proposal.ProfileId,
                CharacterId = proposal.CharacterId,
                PolicyVersion = proposal.PolicyVersion,
                LevelCapPolicyId = proposal.LevelCapPolicyId,
                LevelCapPolicyRevision = proposal.LevelCapPolicyRevision,
                Branch = (int)proposal.Branch,
                BeforeLevel = proposal.BeforeLevel,
                AfterLevel = proposal.AfterLevel,
                MaximumLevel = proposal.MaximumLevel,
                ExperienceUnitsPerLevel = proposal.ExperienceUnitsPerLevel,
                BeforeInLevelExperienceUnits = proposal.BeforeInLevelExperienceUnits,
                AfterInLevelExperienceUnits = proposal.AfterInLevelExperienceUnits,
                BeforeProgressionRevision = proposal.BeforeProgressionRevision,
                AfterProgressionRevision = receipt.AfterProgressionRevision,
                PlanHash = proposal.PlanHash,
                RequiresProgressionWrite = proposal.RequiresProgressionWrite,
                RevivalCommitted = receipt.RevivalCommitted
            };
        }

        private static DeathPenaltyReceipt FromState(DeathPenaltyReceiptState state)
        {
            if (state == null)
            {
                return null;
            }

            var proposal = new DeathPenaltyCommitProposal(
                state.OperationId,
                state.RequestFingerprint,
                state.DeathFingerprint,
                state.AccountId,
                state.ProfileId,
                state.CharacterId,
                state.PolicyVersion,
                state.LevelCapPolicyId,
                state.LevelCapPolicyRevision,
                (DeathPenaltyBranch)state.Branch,
                state.BeforeLevel,
                state.AfterLevel,
                state.MaximumLevel,
                state.ExperienceUnitsPerLevel,
                state.BeforeInLevelExperienceUnits,
                state.AfterInLevelExperienceUnits,
                state.BeforeProgressionRevision,
                null,
                0L,
                0L,
                0L,
                string.Empty,
                string.Empty,
                state.RequiresProgressionWrite,
                false,
                false,
                state.PlanHash);
            return new DeathPenaltyReceipt(
                proposal,
                state.AfterProgressionRevision,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                state.RevivalCommitted,
                state.ReceiptHash);
        }
    }
}
