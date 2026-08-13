using System;
using System.Collections.Generic;

namespace AL.ChampionMode.DeathPenalty
{
    /// <summary>
    /// Pure planning boundary. It never reads or writes a save, progression
    /// provider, wallet, scene, respawn service, or presentation surface.
    /// A future trusted adapter must commit by compare-and-swap; only that
    /// adapter may later receive friend access to structural receipt issuance.
    /// </summary>
    public static class DeathPenaltyPlanner
    {
        public const int InLevelPenaltyPercentagePoints = 5;
        public const int MaximumReplayReceipts = 256;

        public static DeathPenaltyPlan Plan(
            DeathPenaltyRequest request,
            DeathPenaltyDeathStateSnapshot deathState,
            DeathPenaltyProgressionSnapshot progression,
            OathmarkWalletSnapshot oathmarkWallet,
            DeathPenaltyPolicySnapshot policy,
            DeathPenaltyReplayLedgerSnapshot replayLedger)
        {
            if (!ValidateRequest(request))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedInvalidRequest,
                    DeathPenaltyDiagnosticCodes.InvalidRequest);
            }

            if (policy == null ||
                !DeathPenaltyDeterminism.IsVersion(policy.PolicyVersion))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedInvalidPolicy,
                    DeathPenaltyDiagnosticCodes.InvalidPolicy);
            }

            if (deathState == null ||
                deathState.Status ==
                    DeathPenaltyAuthoritativeDeathStatus.Unknown)
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedDeathStateUnavailable,
                    DeathPenaltyDiagnosticCodes.DeathStateUnavailable);
            }

            if (!ValidateDeathState(deathState))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedInvalidDeathState,
                    DeathPenaltyDiagnosticCodes.InvalidDeathState);
            }

            if (!DeathStateMatches(request, deathState))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedDeathStateMismatch,
                    DeathPenaltyDiagnosticCodes.DeathStateMismatch);
            }

            if (!StringComparer.Ordinal.Equals(
                    request.ExpectedDeathStateRevision,
                    deathState.DeathStateRevision))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedStaleDeathState,
                    DeathPenaltyDiagnosticCodes.StaleDeathState);
            }

            DeathPenaltyPlan ledgerRejection =
                ValidateReplayLedgerEnvelope(request, replayLedger);
            if (ledgerRejection != null)
            {
                return ledgerRejection;
            }

            string requestFingerprint =
                DeathPenaltyDeterminism.RequestFingerprint(request, policy);
            string deathFingerprint =
                DeathPenaltyDeterminism.DeathFingerprint(request);

            DeathPenaltyPlan replay = ResolveReplay(
                request.OperationId,
                requestFingerprint,
                deathFingerprint,
                replayLedger.Receipts);
            if (replay != null)
            {
                if (replay.IsCommittedReplay &&
                    deathState.Status ==
                        DeathPenaltyAuthoritativeDeathStatus
                            .DeadAwaitingPenalty)
                {
                    return Reject(
                        DeathPenaltyPlanStatus
                            .RejectedDeathStateReceiptInconsistent,
                        DeathPenaltyDiagnosticCodes
                            .DeathStateReceiptInconsistent);
                }

                return replay;
            }

            if (deathState.Status !=
                DeathPenaltyAuthoritativeDeathStatus.DeadAwaitingPenalty)
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedDeathAlreadyResolved,
                    DeathPenaltyDiagnosticCodes.DeathAlreadyResolved);
            }

            if (!ValidateProgression(progression))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedInvalidProgression,
                    DeathPenaltyDiagnosticCodes.InvalidProgression);
            }

            if (!IdentityMatches(request, progression))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedIdentityMismatch,
                    DeathPenaltyDiagnosticCodes.IdentityMismatch);
            }

            if (!StringComparer.Ordinal.Equals(
                    request.ExpectedProgressionRevision,
                    progression.ProgressionRevision))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedStaleProgression,
                    DeathPenaltyDiagnosticCodes.StaleProgression);
            }

            if (!StringComparer.Ordinal.Equals(
                    request.ExpectedLevelCapPolicyId,
                    progression.LevelCapPolicyId) ||
                !StringComparer.Ordinal.Equals(
                    request.ExpectedLevelCapPolicyRevision,
                    progression.LevelCapPolicyRevision))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedLevelCapPolicyMismatch,
                    DeathPenaltyDiagnosticCodes.LevelCapPolicyMismatch);
            }

            return progression.CurrentLevel < progression.MaximumLevel
                ? PlanInLevelPenalty(
                    request,
                    progression,
                    policy,
                    requestFingerprint,
                    deathFingerprint)
                : PlanMaxLevelRevival(
                    request,
                    progression,
                    oathmarkWallet,
                    policy,
                    requestFingerprint,
                    deathFingerprint);
        }

        /// <summary>
        /// Adapter-only structural verification for future trusted providers.
        /// This internal helper neither authenticates public snapshots nor
        /// grants runtime authority; no public API can mint a receipt.
        /// </summary>
        internal static bool TryVerifyAdapterCommitAndCreateReceipt(
            DeathPenaltyPlan plan,
            DeathPenaltyProgressionSnapshot afterProgression,
            OathmarkWalletSnapshot afterOathmarkWallet,
            DeathPenaltyAtomicRevivalSnapshot atomicRevival,
            out DeathPenaltyReceipt receipt)
        {
            receipt = null;
            DeathPenaltyCommitProposal proposal = plan?.Proposal;
            if (plan == null ||
                !plan.CanCommit ||
                !ValidateProposal(proposal) ||
                !AfterProgressionMatches(proposal, afterProgression))
            {
                return false;
            }

            if (proposal.RequiresProgressionWrite)
            {
                if (StringComparer.Ordinal.Equals(
                        afterProgression.ProgressionRevision,
                        proposal.BeforeProgressionRevision))
                {
                    return false;
                }
            }
            else if (!StringComparer.Ordinal.Equals(
                         afterProgression.ProgressionRevision,
                         proposal.BeforeProgressionRevision))
            {
                return false;
            }

            if (proposal.RequiresOathmarkWalletDebit)
            {
                if (!AfterWalletMatches(
                        proposal,
                        afterOathmarkWallet) ||
                    !AtomicRevivalMatches(
                        proposal,
                        afterOathmarkWallet,
                        atomicRevival) ||
                    StringComparer.Ordinal.Equals(
                        afterOathmarkWallet.WalletRevision,
                        proposal.BeforeOathmarkWalletRevision))
                {
                    return false;
                }
            }
            else if (afterOathmarkWallet != null || atomicRevival != null)
            {
                return false;
            }

            bool revivalCommitted = proposal.RequiresAtomicRevival;
            string atomicRevivalFingerprint = atomicRevival == null
                ? string.Empty
                : DeathPenaltyDeterminism.AtomicRevivalFingerprint(
                    atomicRevival);
            var unsigned = new DeathPenaltyReceipt(
                proposal,
                afterProgression.ProgressionRevision,
                afterOathmarkWallet?.WalletRevision ?? string.Empty,
                atomicRevival?.AfterRevivalRevision ?? string.Empty,
                atomicRevival?.AtomicCommitRevision ?? string.Empty,
                atomicRevivalFingerprint,
                revivalCommitted,
                string.Empty);
            receipt = new DeathPenaltyReceipt(
                proposal,
                afterProgression.ProgressionRevision,
                afterOathmarkWallet?.WalletRevision ?? string.Empty,
                atomicRevival?.AfterRevivalRevision ?? string.Empty,
                atomicRevival?.AtomicCommitRevision ?? string.Empty,
                atomicRevivalFingerprint,
                revivalCommitted,
                DeathPenaltyDeterminism.ReceiptHash(unsigned));
            return ValidateReceipt(receipt);
        }

        internal static bool ValidateReceipt(DeathPenaltyReceipt receipt)
        {
            if (receipt == null ||
                !ValidateProposal(receipt.Proposal) ||
                !DeathPenaltyDeterminism.IsStableId(
                    receipt.AfterProgressionRevision) ||
                !DeathPenaltyDeterminism.IsSha256(receipt.ReceiptHash) ||
                !StringComparer.Ordinal.Equals(
                    receipt.ReceiptHash,
                    DeathPenaltyDeterminism.ReceiptHash(receipt)))
            {
                return false;
            }

            DeathPenaltyCommitProposal proposal = receipt.Proposal;
            if (proposal.RequiresProgressionWrite)
            {
                if (StringComparer.Ordinal.Equals(
                        receipt.AfterProgressionRevision,
                        proposal.BeforeProgressionRevision))
                {
                    return false;
                }
            }
            else if (!StringComparer.Ordinal.Equals(
                         receipt.AfterProgressionRevision,
                         proposal.BeforeProgressionRevision))
            {
                return false;
            }

            if (proposal.RequiresOathmarkWalletDebit)
            {
                return receipt.RevivalCommitted &&
                       DeathPenaltyDeterminism.IsStableId(
                           receipt.AfterOathmarkWalletRevision) &&
                       !StringComparer.Ordinal.Equals(
                           receipt.AfterOathmarkWalletRevision,
                           proposal.BeforeOathmarkWalletRevision) &&
                       DeathPenaltyDeterminism.IsStableId(
                           receipt.AfterRevivalRevision) &&
                       !StringComparer.Ordinal.Equals(
                           receipt.AfterRevivalRevision,
                           proposal.BeforeRevivalRevision) &&
                       DeathPenaltyDeterminism.IsStableId(
                           receipt.AtomicCommitRevision) &&
                       DeathPenaltyDeterminism.IsSha256(
                           receipt.AtomicRevivalFingerprint);
            }

            return !receipt.RevivalCommitted &&
                   string.IsNullOrEmpty(receipt.AfterOathmarkWalletRevision) &&
                   string.IsNullOrEmpty(receipt.AfterRevivalRevision) &&
                   string.IsNullOrEmpty(receipt.AtomicCommitRevision) &&
                   string.IsNullOrEmpty(receipt.AtomicRevivalFingerprint);
        }

        private static DeathPenaltyPlan PlanInLevelPenalty(
            DeathPenaltyRequest request,
            DeathPenaltyProgressionSnapshot progression,
            DeathPenaltyPolicySnapshot policy,
            string requestFingerprint,
            string deathFingerprint)
        {
            long penaltyUnits;
            long afterExperience;
            try
            {
                penaltyUnits = checked(
                    progression.ExperienceUnitsPerLevel /
                    (100L / InLevelPenaltyPercentagePoints));
                afterExperience = Math.Max(
                    0L,
                    checked(progression.InLevelExperienceUnits - penaltyUnits));
            }
            catch (OverflowException)
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedArithmeticFailure,
                    DeathPenaltyDiagnosticCodes.ArithmeticFailure);
            }

            DeathPenaltyCommitProposal proposal = CreateProposal(
                request,
                progression,
                policy,
                requestFingerprint,
                deathFingerprint,
                DeathPenaltyBranch.InLevelExperiencePenalty,
                afterExperience,
                null,
                0L,
                0L,
                0L,
                string.Empty,
                string.Empty,
                afterExperience != progression.InLevelExperienceUnits,
                false,
                false);
            return Ready(proposal);
        }

        private static DeathPenaltyPlan PlanMaxLevelRevival(
            DeathPenaltyRequest request,
            DeathPenaltyProgressionSnapshot progression,
            OathmarkWalletSnapshot wallet,
            DeathPenaltyPolicySnapshot policy,
            string requestFingerprint,
            string deathFingerprint)
        {
            if (!policy.MaxLevelReviveOathmarkCost.HasValue)
            {
                return Reject(
                    DeathPenaltyPlanStatus
                        .RejectedOathmarkConfigurationUnavailable,
                    DeathPenaltyDiagnosticCodes
                        .OathmarkConfigurationUnavailable);
            }

            long cost = policy.MaxLevelReviveOathmarkCost.Value;
            if (cost <= 0L)
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedInvalidPolicy,
                    DeathPenaltyDiagnosticCodes.InvalidPolicy);
            }

            if (!request.HasCompleteOathmarkExpectation ||
                !ValidateBinding(wallet?.Binding))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedInvalidOathmarkBinding,
                    DeathPenaltyDiagnosticCodes.InvalidOathmarkBinding);
            }

            if (wallet.Availability !=
                    OathmarkWalletAvailability.AvailableWritable ||
                wallet.Balance < 0L ||
                !DeathPenaltyDeterminism.IsStableId(wallet.WalletRevision))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedOathmarkWalletUnavailable,
                    DeathPenaltyDiagnosticCodes.OathmarkWalletUnavailable);
            }

            if (!IdentityMatches(request, wallet))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedIdentityMismatch,
                    DeathPenaltyDiagnosticCodes.IdentityMismatch);
            }

            if (!StringComparer.Ordinal.Equals(
                    request.ExpectedOathmarkTechnicalCurrencyId,
                    wallet.Binding.TechnicalCurrencyId) ||
                !StringComparer.Ordinal.Equals(
                    request.ExpectedOathmarkProviderId,
                    wallet.Binding.ProviderId) ||
                !StringComparer.Ordinal.Equals(
                    request.ExpectedOathmarkBindingRevision,
                    wallet.Binding.BindingRevision) ||
                !StringComparer.Ordinal.Equals(
                    request.ExpectedOathmarkWalletRevision,
                    wallet.WalletRevision))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedStaleOathmarkWallet,
                    DeathPenaltyDiagnosticCodes.StaleOathmarkWallet);
            }

            if (wallet.Balance < cost)
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedInsufficientOathmarks,
                    DeathPenaltyDiagnosticCodes.InsufficientOathmarks);
            }

            long afterBalance;
            try
            {
                afterBalance = checked(wallet.Balance - cost);
            }
            catch (OverflowException)
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedArithmeticFailure,
                    DeathPenaltyDiagnosticCodes.ArithmeticFailure);
            }

            DeathPenaltyCommitProposal proposal = CreateProposal(
                request,
                progression,
                policy,
                requestFingerprint,
                deathFingerprint,
                DeathPenaltyBranch.MaxLevelOathmarkRevive,
                progression.InLevelExperienceUnits,
                wallet.Binding,
                cost,
                wallet.Balance,
                afterBalance,
                wallet.WalletRevision,
                request.ExpectedRevivalRevision,
                false,
                true,
                true);
            return Ready(proposal);
        }

        private static DeathPenaltyCommitProposal CreateProposal(
            DeathPenaltyRequest request,
            DeathPenaltyProgressionSnapshot progression,
            DeathPenaltyPolicySnapshot policy,
            string requestFingerprint,
            string deathFingerprint,
            DeathPenaltyBranch branch,
            long afterExperience,
            OathmarkWalletBinding binding,
            long debit,
            long beforeBalance,
            long afterBalance,
            string beforeWalletRevision,
            string beforeRevivalRevision,
            bool requiresProgressionWrite,
            bool requiresWalletDebit,
            bool requiresAtomicRevival)
        {
            var unsigned = new DeathPenaltyCommitProposal(
                request.OperationId,
                requestFingerprint,
                deathFingerprint,
                request.AccountId,
                request.ProfileId,
                request.CharacterId,
                policy.PolicyVersion,
                progression.LevelCapPolicyId,
                progression.LevelCapPolicyRevision,
                branch,
                progression.CurrentLevel,
                progression.CurrentLevel,
                progression.MaximumLevel,
                progression.ExperienceUnitsPerLevel,
                progression.InLevelExperienceUnits,
                afterExperience,
                progression.ProgressionRevision,
                binding,
                debit,
                beforeBalance,
                afterBalance,
                beforeWalletRevision,
                beforeRevivalRevision,
                requiresProgressionWrite,
                requiresWalletDebit,
                requiresAtomicRevival,
                string.Empty);
            return new DeathPenaltyCommitProposal(
                unsigned.OperationId,
                unsigned.RequestFingerprint,
                unsigned.DeathFingerprint,
                unsigned.AccountId,
                unsigned.ProfileId,
                unsigned.CharacterId,
                unsigned.PolicyVersion,
                unsigned.LevelCapPolicyId,
                unsigned.LevelCapPolicyRevision,
                unsigned.Branch,
                unsigned.BeforeLevel,
                unsigned.AfterLevel,
                unsigned.MaximumLevel,
                unsigned.ExperienceUnitsPerLevel,
                unsigned.BeforeInLevelExperienceUnits,
                unsigned.AfterInLevelExperienceUnits,
                unsigned.BeforeProgressionRevision,
                unsigned.OathmarkBinding,
                unsigned.OathmarkDebitUnits,
                unsigned.BeforeOathmarkBalance,
                unsigned.AfterOathmarkBalance,
                unsigned.BeforeOathmarkWalletRevision,
                unsigned.BeforeRevivalRevision,
                unsigned.RequiresProgressionWrite,
                unsigned.RequiresOathmarkWalletDebit,
                unsigned.RequiresAtomicRevival,
                DeathPenaltyDeterminism.PlanHash(unsigned));
        }

        private static DeathPenaltyPlan ResolveReplay(
            string operationId,
            string requestFingerprint,
            string deathFingerprint,
            IReadOnlyList<DeathPenaltyReceipt> retainedReceipts)
        {
            if (retainedReceipts == null)
            {
                return null;
            }

            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            var deathFingerprints = new HashSet<string>(StringComparer.Ordinal);
            DeathPenaltyReceipt operationMatch = null;
            DeathPenaltyReceipt deathMatch = null;
            for (int index = 0; index < retainedReceipts.Count; index++)
            {
                DeathPenaltyReceipt receipt = retainedReceipts[index];
                if (index >= MaximumReplayReceipts ||
                    !ValidateReceipt(receipt) ||
                    !operationIds.Add(receipt.OperationId) ||
                    !deathFingerprints.Add(
                        receipt.Proposal.DeathFingerprint))
                {
                    return Reject(
                        DeathPenaltyPlanStatus.RejectedReplayLedgerInvalid,
                        DeathPenaltyDiagnosticCodes.ReplayLedgerInvalid);
                }

                if (StringComparer.Ordinal.Equals(
                        receipt.OperationId,
                        operationId))
                {
                    operationMatch = receipt;
                }

                if (StringComparer.Ordinal.Equals(
                        receipt.Proposal.DeathFingerprint,
                        deathFingerprint))
                {
                    deathMatch = receipt;
                }
            }

            if (operationMatch != null)
            {
                return StringComparer.Ordinal.Equals(
                        operationMatch.RequestFingerprint,
                        requestFingerprint)
                    ? new DeathPenaltyPlan(
                        DeathPenaltyPlanStatus.ReplayedCommitted,
                        null,
                        operationMatch,
                        string.Empty)
                    : Reject(
                        DeathPenaltyPlanStatus.RejectedOperationCollision,
                        DeathPenaltyDiagnosticCodes.OperationCollision);
            }

            if (deathMatch != null)
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedDeathEventCollision,
                    DeathPenaltyDiagnosticCodes.DeathEventCollision);
            }

            return null;
        }

        private static DeathPenaltyPlan ValidateReplayLedgerEnvelope(
            DeathPenaltyRequest request,
            DeathPenaltyReplayLedgerSnapshot ledger)
        {
            if (ledger == null ||
                ledger.Availability ==
                    DeathPenaltyReplayLedgerAvailability.Unknown ||
                ledger.Availability ==
                    DeathPenaltyReplayLedgerAvailability.Unavailable)
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedReplayLedgerUnavailable,
                    DeathPenaltyDiagnosticCodes.ReplayLedgerUnavailable);
            }

            if (ledger.Availability ==
                    DeathPenaltyReplayLedgerAvailability.Malformed ||
                ledger.Availability !=
                    DeathPenaltyReplayLedgerAvailability.Available ||
                !ledger.HasReceiptCollection ||
                ledger.WasTruncated ||
                ledger.ReceiptCount > MaximumReplayReceipts ||
                !DeathPenaltyDeterminism.IsVersion(
                    ledger.LedgerVersion) ||
                !DeathPenaltyDeterminism.IsStableId(
                    ledger.LedgerRevision))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedReplayLedgerInvalid,
                    DeathPenaltyDiagnosticCodes.ReplayLedgerInvalid);
            }

            if (!ledger.IsComplete)
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedReplayLedgerIncomplete,
                    DeathPenaltyDiagnosticCodes.ReplayLedgerIncomplete);
            }

            if (!StringComparer.Ordinal.Equals(
                    request.ExpectedReplayLedgerVersion,
                    ledger.LedgerVersion) ||
                !StringComparer.Ordinal.Equals(
                    request.ExpectedReplayLedgerRevision,
                    ledger.LedgerRevision))
            {
                return Reject(
                    DeathPenaltyPlanStatus.RejectedReplayLedgerStale,
                    DeathPenaltyDiagnosticCodes.ReplayLedgerStale);
            }

            return null;
        }

        private static bool ValidateDeathState(
            DeathPenaltyDeathStateSnapshot deathState)
        {
            return deathState != null &&
                   (deathState.Status ==
                        DeathPenaltyAuthoritativeDeathStatus
                            .DeadAwaitingPenalty ||
                    deathState.Status ==
                        DeathPenaltyAuthoritativeDeathStatus.Resolved) &&
                   DeathPenaltyDeterminism.IsStableId(
                       deathState.AccountId) &&
                   DeathPenaltyDeterminism.IsStableId(
                       deathState.ProfileId) &&
                   DeathPenaltyDeterminism.IsStableId(
                       deathState.CharacterId) &&
                   DeathPenaltyDeterminism.IsStableId(
                       deathState.DeathEventId) &&
                   DeathPenaltyDeterminism.IsStableId(
                       deathState.CombatSessionId) &&
                   DeathPenaltyDeterminism.IsStableId(
                       deathState.EncounterAttemptId) &&
                   DeathPenaltyDeterminism.IsStableId(
                       deathState.InstanceId) &&
                   deathState.DeathOrdinal >= 0L &&
                   DeathPenaltyDeterminism.IsStableId(
                       deathState.DeathStateRevision);
        }

        private static bool DeathStateMatches(
            DeathPenaltyRequest request,
            DeathPenaltyDeathStateSnapshot deathState)
        {
            return StringComparer.Ordinal.Equals(
                       request.AccountId,
                       deathState.AccountId) &&
                   StringComparer.Ordinal.Equals(
                       request.ProfileId,
                       deathState.ProfileId) &&
                   StringComparer.Ordinal.Equals(
                       request.CharacterId,
                       deathState.CharacterId) &&
                   StringComparer.Ordinal.Equals(
                       request.DeathEventId,
                       deathState.DeathEventId) &&
                   StringComparer.Ordinal.Equals(
                       request.CombatSessionId,
                       deathState.CombatSessionId) &&
                   StringComparer.Ordinal.Equals(
                       request.EncounterAttemptId,
                       deathState.EncounterAttemptId) &&
                   StringComparer.Ordinal.Equals(
                       request.InstanceId,
                       deathState.InstanceId) &&
                   request.DeathOrdinal == deathState.DeathOrdinal;
        }

        private static bool ValidateRequest(DeathPenaltyRequest request)
        {
            if (request == null ||
                !DeathPenaltyDeterminism.IsStableId(request.OperationId) ||
                !DeathPenaltyDeterminism.IsStableId(request.AccountId) ||
                !DeathPenaltyDeterminism.IsStableId(request.ProfileId) ||
                !DeathPenaltyDeterminism.IsStableId(request.CharacterId) ||
                !DeathPenaltyDeterminism.IsStableId(request.DeathEventId) ||
                !DeathPenaltyDeterminism.IsStableId(request.CombatSessionId) ||
                !DeathPenaltyDeterminism.IsStableId(request.EncounterAttemptId) ||
                !DeathPenaltyDeterminism.IsStableId(request.InstanceId) ||
                request.DeathOrdinal < 0L ||
                !DeathPenaltyDeterminism.IsStableId(
                    request.ExpectedProgressionRevision) ||
                !DeathPenaltyDeterminism.IsStableId(
                    request.ExpectedLevelCapPolicyId) ||
                !DeathPenaltyDeterminism.IsStableId(
                    request.ExpectedLevelCapPolicyRevision) ||
                !DeathPenaltyDeterminism.IsStableId(
                    request.ExpectedDeathStateRevision) ||
                !DeathPenaltyDeterminism.IsVersion(
                    request.ExpectedReplayLedgerVersion) ||
                !DeathPenaltyDeterminism.IsStableId(
                    request.ExpectedReplayLedgerRevision))
            {
                return false;
            }

            if (!request.HasNoOathmarkExpectation &&
                !request.HasCompleteOathmarkExpectation)
            {
                return false;
            }

            return request.HasNoOathmarkExpectation ||
                   (DeathPenaltyDeterminism.IsStableId(
                        request.ExpectedOathmarkTechnicalCurrencyId) &&
                    DeathPenaltyDeterminism.IsStableId(
                        request.ExpectedOathmarkProviderId) &&
                    DeathPenaltyDeterminism.IsStableId(
                        request.ExpectedOathmarkBindingRevision) &&
                    DeathPenaltyDeterminism.IsStableId(
                        request.ExpectedOathmarkWalletRevision) &&
                    DeathPenaltyDeterminism.IsStableId(
                        request.ExpectedRevivalRevision));
        }

        private static bool ValidateProgression(
            DeathPenaltyProgressionSnapshot progression)
        {
            return progression != null &&
                   DeathPenaltyDeterminism.IsStableId(progression.AccountId) &&
                   DeathPenaltyDeterminism.IsStableId(progression.ProfileId) &&
                   DeathPenaltyDeterminism.IsStableId(progression.CharacterId) &&
                   progression.CurrentLevel > 0 &&
                   progression.MaximumLevel > 0 &&
                   progression.CurrentLevel <= progression.MaximumLevel &&
                   progression.ExperienceUnitsPerLevel > 0L &&
                   progression.ExperienceUnitsPerLevel %
                       (100L / InLevelPenaltyPercentagePoints) == 0L &&
                   progression.InLevelExperienceUnits >= 0L &&
                   progression.InLevelExperienceUnits <=
                       progression.ExperienceUnitsPerLevel &&
                   (progression.CurrentLevel == progression.MaximumLevel ||
                    progression.InLevelExperienceUnits <
                        progression.ExperienceUnitsPerLevel) &&
                   DeathPenaltyDeterminism.IsStableId(
                       progression.ProgressionRevision) &&
                   DeathPenaltyDeterminism.IsStableId(
                       progression.LevelCapPolicyId) &&
                   DeathPenaltyDeterminism.IsStableId(
                       progression.LevelCapPolicyRevision);
        }

        private static bool ValidateBinding(OathmarkWalletBinding binding)
        {
            return binding != null &&
                   DeathPenaltyDeterminism.IsStableId(
                       binding.TechnicalCurrencyId) &&
                   DeathPenaltyDeterminism.IsStableId(binding.ProviderId) &&
                   DeathPenaltyDeterminism.IsStableId(
                       binding.BindingRevision) &&
                   binding.Domain ==
                       PlayerCurrencyDomain.ThreeDimensionalPlayerMain &&
                   binding.IsSoleMainCurrency &&
                   DeathPenaltyDeterminism.IsIntegerUnitScale(
                       binding.IntegerUnitScale);
        }

        private static bool ValidateProposal(
            DeathPenaltyCommitProposal proposal)
        {
            if (proposal == null ||
                !DeathPenaltyDeterminism.IsStableId(proposal.OperationId) ||
                !DeathPenaltyDeterminism.IsSha256(
                    proposal.RequestFingerprint) ||
                !DeathPenaltyDeterminism.IsSha256(proposal.DeathFingerprint) ||
                !DeathPenaltyDeterminism.IsStableId(proposal.AccountId) ||
                !DeathPenaltyDeterminism.IsStableId(proposal.ProfileId) ||
                !DeathPenaltyDeterminism.IsStableId(proposal.CharacterId) ||
                !DeathPenaltyDeterminism.IsVersion(proposal.PolicyVersion) ||
                !DeathPenaltyDeterminism.IsStableId(
                    proposal.LevelCapPolicyId) ||
                !DeathPenaltyDeterminism.IsStableId(
                    proposal.LevelCapPolicyRevision) ||
                proposal.BeforeLevel <= 0 ||
                proposal.AfterLevel != proposal.BeforeLevel ||
                proposal.MaximumLevel < proposal.BeforeLevel ||
                proposal.ExperienceUnitsPerLevel <= 0L ||
                proposal.ExperienceUnitsPerLevel %
                    (100L / InLevelPenaltyPercentagePoints) != 0L ||
                proposal.BeforeInLevelExperienceUnits < 0L ||
                proposal.BeforeInLevelExperienceUnits >
                    proposal.ExperienceUnitsPerLevel ||
                proposal.AfterInLevelExperienceUnits < 0L ||
                proposal.AfterInLevelExperienceUnits >
                    proposal.ExperienceUnitsPerLevel ||
                !DeathPenaltyDeterminism.IsStableId(
                    proposal.BeforeProgressionRevision) ||
                !DeathPenaltyDeterminism.IsSha256(proposal.PlanHash) ||
                !StringComparer.Ordinal.Equals(
                    proposal.PlanHash,
                    DeathPenaltyDeterminism.PlanHash(proposal)))
            {
                return false;
            }

            if (proposal.Branch ==
                DeathPenaltyBranch.InLevelExperiencePenalty)
            {
                long exactPenalty =
                    proposal.ExperienceUnitsPerLevel /
                    (100L / InLevelPenaltyPercentagePoints);
                long expected = Math.Max(
                    0L,
                    proposal.BeforeInLevelExperienceUnits - exactPenalty);
                return proposal.BeforeLevel < proposal.MaximumLevel &&
                       proposal.AfterInLevelExperienceUnits == expected &&
                       proposal.RequiresProgressionWrite ==
                           (expected !=
                            proposal.BeforeInLevelExperienceUnits) &&
                       proposal.OathmarkBinding == null &&
                       proposal.OathmarkDebitUnits == 0L &&
                       proposal.BeforeOathmarkBalance == 0L &&
                       proposal.AfterOathmarkBalance == 0L &&
                       string.IsNullOrEmpty(
                           proposal.BeforeOathmarkWalletRevision) &&
                       string.IsNullOrEmpty(
                           proposal.BeforeRevivalRevision) &&
                       !proposal.RequiresOathmarkWalletDebit &&
                       !proposal.RequiresAtomicRevival;
            }

            if (proposal.Branch !=
                    DeathPenaltyBranch.MaxLevelOathmarkRevive ||
                proposal.BeforeLevel != proposal.MaximumLevel ||
                proposal.AfterInLevelExperienceUnits !=
                    proposal.BeforeInLevelExperienceUnits ||
                proposal.RequiresProgressionWrite ||
                !ValidateBinding(proposal.OathmarkBinding) ||
                proposal.OathmarkDebitUnits <= 0L ||
                proposal.BeforeOathmarkBalance <
                    proposal.OathmarkDebitUnits ||
                proposal.AfterOathmarkBalance !=
                    proposal.BeforeOathmarkBalance -
                    proposal.OathmarkDebitUnits ||
                !DeathPenaltyDeterminism.IsStableId(
                    proposal.BeforeOathmarkWalletRevision) ||
                !DeathPenaltyDeterminism.IsStableId(
                    proposal.BeforeRevivalRevision) ||
                !proposal.RequiresOathmarkWalletDebit ||
                !proposal.RequiresAtomicRevival)
            {
                return false;
            }

            return true;
        }

        private static bool IdentityMatches(
            DeathPenaltyRequest request,
            DeathPenaltyProgressionSnapshot progression)
        {
            return StringComparer.Ordinal.Equals(
                       request.AccountId,
                       progression.AccountId) &&
                   StringComparer.Ordinal.Equals(
                       request.ProfileId,
                       progression.ProfileId) &&
                   StringComparer.Ordinal.Equals(
                       request.CharacterId,
                       progression.CharacterId);
        }

        private static bool AfterProgressionMatches(
            DeathPenaltyCommitProposal proposal,
            DeathPenaltyProgressionSnapshot after)
        {
            return after != null &&
                   DeathPenaltyDeterminism.IsStableId(
                       after.ProgressionRevision) &&
                   StringComparer.Ordinal.Equals(
                       proposal.AccountId,
                       after.AccountId) &&
                   StringComparer.Ordinal.Equals(
                       proposal.ProfileId,
                       after.ProfileId) &&
                   StringComparer.Ordinal.Equals(
                       proposal.CharacterId,
                       after.CharacterId) &&
                   after.CurrentLevel == proposal.AfterLevel &&
                   after.MaximumLevel == proposal.MaximumLevel &&
                   after.InLevelExperienceUnits ==
                       proposal.AfterInLevelExperienceUnits &&
                   after.ExperienceUnitsPerLevel ==
                       proposal.ExperienceUnitsPerLevel &&
                   StringComparer.Ordinal.Equals(
                       after.LevelCapPolicyId,
                       proposal.LevelCapPolicyId) &&
                   StringComparer.Ordinal.Equals(
                       after.LevelCapPolicyRevision,
                       proposal.LevelCapPolicyRevision);
        }

        private static bool AfterWalletMatches(
            DeathPenaltyCommitProposal proposal,
            OathmarkWalletSnapshot after)
        {
            OathmarkWalletBinding expected = proposal.OathmarkBinding;
            OathmarkWalletBinding actual = after?.Binding;
            return after != null &&
                   actual != null &&
                   after.Availability ==
                       OathmarkWalletAvailability.AvailableWritable &&
                   after.Balance == proposal.AfterOathmarkBalance &&
                   DeathPenaltyDeterminism.IsStableId(after.WalletRevision) &&
                   StringComparer.Ordinal.Equals(
                       proposal.AccountId,
                       after.AccountId) &&
                   StringComparer.Ordinal.Equals(
                       proposal.ProfileId,
                       after.ProfileId) &&
                   StringComparer.Ordinal.Equals(
                       proposal.CharacterId,
                       after.CharacterId) &&
                   StringComparer.Ordinal.Equals(
                       expected.TechnicalCurrencyId,
                       actual.TechnicalCurrencyId) &&
                   StringComparer.Ordinal.Equals(
                       expected.ProviderId,
                       actual.ProviderId) &&
                   StringComparer.Ordinal.Equals(
                       expected.BindingRevision,
                       actual.BindingRevision) &&
                   expected.Domain == actual.Domain &&
                   expected.IsSoleMainCurrency ==
                       actual.IsSoleMainCurrency &&
                   expected.IntegerUnitScale == actual.IntegerUnitScale;
        }

        private static bool AtomicRevivalMatches(
            DeathPenaltyCommitProposal proposal,
            OathmarkWalletSnapshot afterWallet,
            DeathPenaltyAtomicRevivalSnapshot atomic)
        {
            OathmarkWalletBinding binding = proposal.OathmarkBinding;
            return atomic != null &&
                   atomic.Status ==
                       DeathPenaltyAtomicRevivalStatus.CommittedAtomically &&
                   atomic.WasDeadBefore &&
                   atomic.IsAliveAfter &&
                   StringComparer.Ordinal.Equals(
                       atomic.OperationId,
                       proposal.OperationId) &&
                   StringComparer.Ordinal.Equals(
                       atomic.RequestFingerprint,
                       proposal.RequestFingerprint) &&
                   StringComparer.Ordinal.Equals(
                       atomic.DeathFingerprint,
                       proposal.DeathFingerprint) &&
                   StringComparer.Ordinal.Equals(
                       atomic.AccountId,
                       proposal.AccountId) &&
                   StringComparer.Ordinal.Equals(
                       atomic.ProfileId,
                       proposal.ProfileId) &&
                   StringComparer.Ordinal.Equals(
                       atomic.CharacterId,
                       proposal.CharacterId) &&
                   StringComparer.Ordinal.Equals(
                       atomic.TechnicalCurrencyId,
                       binding.TechnicalCurrencyId) &&
                   StringComparer.Ordinal.Equals(
                       atomic.ProviderId,
                       binding.ProviderId) &&
                   StringComparer.Ordinal.Equals(
                       atomic.BindingRevision,
                       binding.BindingRevision) &&
                   atomic.DebitUnits == proposal.OathmarkDebitUnits &&
                   atomic.BeforeWalletBalance ==
                       proposal.BeforeOathmarkBalance &&
                   atomic.AfterWalletBalance ==
                       proposal.AfterOathmarkBalance &&
                   StringComparer.Ordinal.Equals(
                       atomic.BeforeWalletRevision,
                       proposal.BeforeOathmarkWalletRevision) &&
                   StringComparer.Ordinal.Equals(
                       atomic.AfterWalletRevision,
                       afterWallet.WalletRevision) &&
                   StringComparer.Ordinal.Equals(
                       atomic.BeforeRevivalRevision,
                       proposal.BeforeRevivalRevision) &&
                   DeathPenaltyDeterminism.IsStableId(
                       atomic.AfterRevivalRevision) &&
                   !StringComparer.Ordinal.Equals(
                       atomic.AfterRevivalRevision,
                       proposal.BeforeRevivalRevision) &&
                   DeathPenaltyDeterminism.IsStableId(
                       atomic.AtomicCommitRevision);
        }

        private static bool IdentityMatches(
            DeathPenaltyRequest request,
            OathmarkWalletSnapshot wallet)
        {
            return wallet != null &&
                   StringComparer.Ordinal.Equals(
                       request.AccountId,
                       wallet.AccountId) &&
                   StringComparer.Ordinal.Equals(
                       request.ProfileId,
                       wallet.ProfileId) &&
                   StringComparer.Ordinal.Equals(
                       request.CharacterId,
                       wallet.CharacterId);
        }

        private static DeathPenaltyPlan Ready(
            DeathPenaltyCommitProposal proposal)
        {
            return ValidateProposal(proposal)
                ? new DeathPenaltyPlan(
                    DeathPenaltyPlanStatus.ReadyToCommit,
                    proposal,
                    null,
                    string.Empty)
                : Reject(
                    DeathPenaltyPlanStatus.RejectedArithmeticFailure,
                    DeathPenaltyDiagnosticCodes.ArithmeticFailure);
        }

        private static DeathPenaltyPlan Reject(
            DeathPenaltyPlanStatus status,
            string diagnosticCode)
        {
            return new DeathPenaltyPlan(
                status,
                null,
                null,
                diagnosticCode);
        }
    }
}
