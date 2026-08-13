using System;
using System.Collections.Generic;

namespace AL.ChampionMode.DeathPenalty
{
    /// <summary>
    /// Player-facing semantics only. These labels never select a technical
    /// currency identity, provider, wallet, conversion, or compatibility row.
    /// </summary>
    public static class OathmarkPlayerCurrencySemantics
    {
        public const string SingularDisplayName = "Oathmark";
        public const string PluralDisplayName = "Oathmarks";
        public const bool CoinPresentationHasWalletAuthority = false;
    }

    public enum PlayerCurrencyDomain
    {
        Unknown = 0,
        ThreeDimensionalPlayerMain = 1,
        TwoPointFiveDimensionalKingdom = 2,
        LegacyCompatibility = 3,
        GuildOrRealm = 4
    }

    public enum OathmarkWalletAvailability
    {
        Unknown = 0,
        AvailableWritable = 1,
        AvailableReadOnly = 2,
        Unavailable = 3,
        Malformed = 4
    }

    public enum DeathPenaltyBranch
    {
        Unknown = 0,
        InLevelExperiencePenalty = 1,
        MaxLevelOathmarkRevive = 2
    }

    public enum DeathPenaltyPlanStatus
    {
        Unknown = 0,
        ReadyToCommit = 1,
        ReplayedCommitted = 2,
        RejectedInvalidRequest = 3,
        RejectedInvalidPolicy = 4,
        RejectedReplayLedgerInvalid = 5,
        RejectedOperationCollision = 6,
        RejectedInvalidProgression = 7,
        RejectedIdentityMismatch = 8,
        RejectedStaleProgression = 9,
        RejectedLevelCapPolicyMismatch = 10,
        RejectedOathmarkConfigurationUnavailable = 11,
        RejectedInvalidOathmarkBinding = 12,
        RejectedOathmarkWalletUnavailable = 13,
        RejectedStaleOathmarkWallet = 14,
        RejectedInsufficientOathmarks = 15,
        RejectedArithmeticFailure = 16,
        RejectedReplayLedgerUnavailable = 17,
        RejectedReplayLedgerIncomplete = 18,
        RejectedReplayLedgerStale = 19,
        RejectedDeathStateUnavailable = 20,
        RejectedInvalidDeathState = 21,
        RejectedDeathStateMismatch = 22,
        RejectedStaleDeathState = 23,
        RejectedDeathAlreadyResolved = 24,
        RejectedDeathEventCollision = 25,
        RejectedDeathStateReceiptInconsistent = 26
    }

    public enum DeathPenaltyReplayLedgerAvailability
    {
        Unknown = 0,
        Available = 1,
        Unavailable = 2,
        Malformed = 3
    }

    public enum DeathPenaltyAuthoritativeDeathStatus
    {
        Unknown = 0,
        DeadAwaitingPenalty = 1,
        Resolved = 2
    }

    public enum DeathPenaltyAtomicRevivalStatus
    {
        Unknown = 0,
        CommittedAtomically = 1,
        WalletDebitedWithoutRevival = 2,
        RevivalCommittedWithoutDebit = 3,
        Rejected = 4
    }

    public static class DeathPenaltyDiagnosticCodes
    {
        public const string InvalidRequest = "AL-DEATH-PENALTY-REQUEST-INVALID";
        public const string InvalidPolicy = "AL-DEATH-PENALTY-POLICY-INVALID";
        public const string ReplayLedgerInvalid = "AL-DEATH-PENALTY-REPLAY-LEDGER-INVALID";
        public const string OperationCollision = "AL-DEATH-PENALTY-OPERATION-COLLISION";
        public const string InvalidProgression = "AL-DEATH-PENALTY-PROGRESSION-INVALID";
        public const string IdentityMismatch = "AL-DEATH-PENALTY-IDENTITY-MISMATCH";
        public const string StaleProgression = "AL-DEATH-PENALTY-PROGRESSION-STALE";
        public const string LevelCapPolicyMismatch = "AL-DEATH-PENALTY-LEVEL-CAP-POLICY-MISMATCH";
        public const string OathmarkConfigurationUnavailable = "AL-DEATH-PENALTY-OATHMARK-COST-UNAVAILABLE";
        public const string InvalidOathmarkBinding = "AL-DEATH-PENALTY-OATHMARK-BINDING-INVALID";
        public const string OathmarkWalletUnavailable = "AL-DEATH-PENALTY-OATHMARK-WALLET-UNAVAILABLE";
        public const string StaleOathmarkWallet = "AL-DEATH-PENALTY-OATHMARK-WALLET-STALE";
        public const string InsufficientOathmarks = "AL-DEATH-PENALTY-OATHMARKS-INSUFFICIENT";
        public const string ArithmeticFailure = "AL-DEATH-PENALTY-ARITHMETIC-FAILURE";
        public const string ReplayLedgerUnavailable = "AL-DEATH-PENALTY-REPLAY-LEDGER-UNAVAILABLE";
        public const string ReplayLedgerIncomplete = "AL-DEATH-PENALTY-REPLAY-LEDGER-INCOMPLETE";
        public const string ReplayLedgerStale = "AL-DEATH-PENALTY-REPLAY-LEDGER-STALE";
        public const string DeathStateUnavailable = "AL-DEATH-PENALTY-DEATH-STATE-UNAVAILABLE";
        public const string InvalidDeathState = "AL-DEATH-PENALTY-DEATH-STATE-INVALID";
        public const string DeathStateMismatch = "AL-DEATH-PENALTY-DEATH-STATE-MISMATCH";
        public const string StaleDeathState = "AL-DEATH-PENALTY-DEATH-STATE-STALE";
        public const string DeathAlreadyResolved = "AL-DEATH-PENALTY-DEATH-ALREADY-RESOLVED";
        public const string DeathEventCollision = "AL-DEATH-PENALTY-DEATH-EVENT-COLLISION";
        public const string DeathStateReceiptInconsistent = "AL-DEATH-PENALTY-DEATH-STATE-RECEIPT-INCONSISTENT";
    }

    public sealed class DeathPenaltyRequest
    {
        public DeathPenaltyRequest(
            string operationId,
            string accountId,
            string profileId,
            string characterId,
            string deathEventId,
            string combatSessionId,
            string encounterAttemptId,
            string instanceId,
            long deathOrdinal,
            string expectedProgressionRevision,
            string expectedLevelCapPolicyId,
            string expectedLevelCapPolicyRevision,
            string expectedDeathStateRevision,
            string expectedReplayLedgerVersion,
            string expectedReplayLedgerRevision,
            string expectedOathmarkTechnicalCurrencyId = null,
            string expectedOathmarkProviderId = null,
            string expectedOathmarkBindingRevision = null,
            string expectedOathmarkWalletRevision = null,
            string expectedRevivalRevision = null)
        {
            OperationId = operationId ?? string.Empty;
            AccountId = accountId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            CharacterId = characterId ?? string.Empty;
            DeathEventId = deathEventId ?? string.Empty;
            CombatSessionId = combatSessionId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            DeathOrdinal = deathOrdinal;
            ExpectedProgressionRevision = expectedProgressionRevision ?? string.Empty;
            ExpectedLevelCapPolicyId = expectedLevelCapPolicyId ?? string.Empty;
            ExpectedLevelCapPolicyRevision = expectedLevelCapPolicyRevision ?? string.Empty;
            ExpectedDeathStateRevision = expectedDeathStateRevision ?? string.Empty;
            ExpectedReplayLedgerVersion = expectedReplayLedgerVersion ?? string.Empty;
            ExpectedReplayLedgerRevision = expectedReplayLedgerRevision ?? string.Empty;
            ExpectedOathmarkTechnicalCurrencyId = expectedOathmarkTechnicalCurrencyId ?? string.Empty;
            ExpectedOathmarkProviderId = expectedOathmarkProviderId ?? string.Empty;
            ExpectedOathmarkBindingRevision = expectedOathmarkBindingRevision ?? string.Empty;
            ExpectedOathmarkWalletRevision = expectedOathmarkWalletRevision ?? string.Empty;
            ExpectedRevivalRevision = expectedRevivalRevision ?? string.Empty;
        }

        public string OperationId { get; }
        public string AccountId { get; }
        public string ProfileId { get; }
        public string CharacterId { get; }
        public string DeathEventId { get; }
        public string CombatSessionId { get; }
        public string EncounterAttemptId { get; }
        public string InstanceId { get; }
        public long DeathOrdinal { get; }
        public string ExpectedProgressionRevision { get; }
        public string ExpectedLevelCapPolicyId { get; }
        public string ExpectedLevelCapPolicyRevision { get; }
        public string ExpectedDeathStateRevision { get; }
        public string ExpectedReplayLedgerVersion { get; }
        public string ExpectedReplayLedgerRevision { get; }
        public string ExpectedOathmarkTechnicalCurrencyId { get; }
        public string ExpectedOathmarkProviderId { get; }
        public string ExpectedOathmarkBindingRevision { get; }
        public string ExpectedOathmarkWalletRevision { get; }
        public string ExpectedRevivalRevision { get; }

        internal bool HasNoOathmarkExpectation =>
            string.IsNullOrEmpty(ExpectedOathmarkTechnicalCurrencyId) &&
            string.IsNullOrEmpty(ExpectedOathmarkProviderId) &&
            string.IsNullOrEmpty(ExpectedOathmarkBindingRevision) &&
            string.IsNullOrEmpty(ExpectedOathmarkWalletRevision) &&
            string.IsNullOrEmpty(ExpectedRevivalRevision);

        internal bool HasCompleteOathmarkExpectation =>
            !string.IsNullOrEmpty(ExpectedOathmarkTechnicalCurrencyId) &&
            !string.IsNullOrEmpty(ExpectedOathmarkProviderId) &&
            !string.IsNullOrEmpty(ExpectedOathmarkBindingRevision) &&
            !string.IsNullOrEmpty(ExpectedOathmarkWalletRevision) &&
            !string.IsNullOrEmpty(ExpectedRevivalRevision);
    }

    public sealed class DeathPenaltyProgressionSnapshot
    {
        public DeathPenaltyProgressionSnapshot(
            string accountId,
            string profileId,
            string characterId,
            int currentLevel,
            int maximumLevel,
            long inLevelExperienceUnits,
            long experienceUnitsPerLevel,
            string progressionRevision,
            string levelCapPolicyId,
            string levelCapPolicyRevision)
        {
            AccountId = accountId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            CharacterId = characterId ?? string.Empty;
            CurrentLevel = currentLevel;
            MaximumLevel = maximumLevel;
            InLevelExperienceUnits = inLevelExperienceUnits;
            ExperienceUnitsPerLevel = experienceUnitsPerLevel;
            ProgressionRevision = progressionRevision ?? string.Empty;
            LevelCapPolicyId = levelCapPolicyId ?? string.Empty;
            LevelCapPolicyRevision = levelCapPolicyRevision ?? string.Empty;
        }

        public string AccountId { get; }
        public string ProfileId { get; }
        public string CharacterId { get; }
        public int CurrentLevel { get; }
        public int MaximumLevel { get; }
        public long InLevelExperienceUnits { get; }
        public long ExperienceUnitsPerLevel { get; }
        public string ProgressionRevision { get; }
        public string LevelCapPolicyId { get; }
        public string LevelCapPolicyRevision { get; }
    }

    /// <summary>
    /// A caller-injected technical binding. This type deliberately defines no
    /// default technical currency ID and has no relationship to legacy wallet
    /// enums or any Kingdom resource family.
    /// </summary>
    public sealed class OathmarkWalletBinding
    {
        public OathmarkWalletBinding(
            string technicalCurrencyId,
            string providerId,
            string bindingRevision,
            PlayerCurrencyDomain domain,
            bool isSoleMainCurrency,
            long integerUnitScale)
        {
            TechnicalCurrencyId = technicalCurrencyId ?? string.Empty;
            ProviderId = providerId ?? string.Empty;
            BindingRevision = bindingRevision ?? string.Empty;
            Domain = domain;
            IsSoleMainCurrency = isSoleMainCurrency;
            IntegerUnitScale = integerUnitScale;
        }

        public string TechnicalCurrencyId { get; }
        public string ProviderId { get; }
        public string BindingRevision { get; }
        public PlayerCurrencyDomain Domain { get; }
        public bool IsSoleMainCurrency { get; }
        public long IntegerUnitScale { get; }
    }

    public sealed class OathmarkWalletSnapshot
    {
        public OathmarkWalletSnapshot(
            string accountId,
            string profileId,
            string characterId,
            OathmarkWalletBinding binding,
            OathmarkWalletAvailability availability,
            long balance,
            string walletRevision)
        {
            AccountId = accountId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            CharacterId = characterId ?? string.Empty;
            Binding = binding;
            Availability = availability;
            Balance = balance;
            WalletRevision = walletRevision ?? string.Empty;
        }

        public string AccountId { get; }
        public string ProfileId { get; }
        public string CharacterId { get; }
        public OathmarkWalletBinding Binding { get; }
        public OathmarkWalletAvailability Availability { get; }
        public long Balance { get; }
        public string WalletRevision { get; }
    }

    public sealed class DeathPenaltyPolicySnapshot
    {
        public DeathPenaltyPolicySnapshot(
            string policyVersion,
            long? maxLevelReviveOathmarkCost)
        {
            PolicyVersion = policyVersion ?? string.Empty;
            MaxLevelReviveOathmarkCost = maxLevelReviveOathmarkCost;
        }

        public string PolicyVersion { get; }
        public long? MaxLevelReviveOathmarkCost { get; }
    }

    /// <summary>
    /// Current authoritative death-state evidence. This contract does not
    /// define revival, respawn, checkpoint, or scene-transition policy.
    /// </summary>
    public sealed class DeathPenaltyDeathStateSnapshot
    {
        public DeathPenaltyDeathStateSnapshot(
            DeathPenaltyAuthoritativeDeathStatus status,
            string accountId,
            string profileId,
            string characterId,
            string deathEventId,
            string combatSessionId,
            string encounterAttemptId,
            string instanceId,
            long deathOrdinal,
            string deathStateRevision)
        {
            Status = status;
            AccountId = accountId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            CharacterId = characterId ?? string.Empty;
            DeathEventId = deathEventId ?? string.Empty;
            CombatSessionId = combatSessionId ?? string.Empty;
            EncounterAttemptId = encounterAttemptId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            DeathOrdinal = deathOrdinal;
            DeathStateRevision = deathStateRevision ?? string.Empty;
        }

        public DeathPenaltyAuthoritativeDeathStatus Status { get; }
        public string AccountId { get; }
        public string ProfileId { get; }
        public string CharacterId { get; }
        public string DeathEventId { get; }
        public string CombatSessionId { get; }
        public string EncounterAttemptId { get; }
        public string InstanceId { get; }
        public long DeathOrdinal { get; }
        public string DeathStateRevision { get; }
    }

    /// <summary>
    /// Explicit, immutable, bounded replay-ledger view. Null, unavailable,
    /// incomplete, stale, malformed, or truncated views are never equivalent
    /// to an empty authoritative ledger.
    /// </summary>
    public sealed class DeathPenaltyReplayLedgerSnapshot
    {
        private const int MaximumCapturedReceipts = 257;
        private readonly IReadOnlyList<DeathPenaltyReceipt> _receipts;

        public DeathPenaltyReplayLedgerSnapshot(
            DeathPenaltyReplayLedgerAvailability availability,
            bool isComplete,
            string ledgerVersion,
            string ledgerRevision,
            IEnumerable<DeathPenaltyReceipt> receipts)
        {
            Availability = availability;
            IsComplete = isComplete;
            LedgerVersion = ledgerVersion ?? string.Empty;
            LedgerRevision = ledgerRevision ?? string.Empty;
            HasReceiptCollection = receipts != null;

            var captured = new List<DeathPenaltyReceipt>();
            if (receipts != null)
            {
                foreach (DeathPenaltyReceipt receipt in receipts)
                {
                    captured.Add(receipt);
                    if (captured.Count >= MaximumCapturedReceipts)
                    {
                        WasTruncated = true;
                        break;
                    }
                }
            }

            _receipts = captured.AsReadOnly();
        }

        public DeathPenaltyReplayLedgerAvailability Availability { get; }
        public bool IsComplete { get; }
        public string LedgerVersion { get; }
        public string LedgerRevision { get; }
        public bool HasReceiptCollection { get; }
        public bool WasTruncated { get; }
        public int ReceiptCount => _receipts.Count;
        public IReadOnlyList<DeathPenaltyReceipt> Receipts => _receipts;
    }

    /// <summary>
    /// Authoritative after-application evidence for the maximum-level branch.
    /// Only CommittedAtomically can support a receipt; the two partial statuses
    /// are explicit fail-closed evidence and never revival authority.
    /// </summary>
    public sealed class DeathPenaltyAtomicRevivalSnapshot
    {
        public DeathPenaltyAtomicRevivalSnapshot(
            DeathPenaltyAtomicRevivalStatus status,
            string operationId,
            string requestFingerprint,
            string deathFingerprint,
            string accountId,
            string profileId,
            string characterId,
            string technicalCurrencyId,
            string providerId,
            string bindingRevision,
            long debitUnits,
            long beforeWalletBalance,
            long afterWalletBalance,
            string beforeWalletRevision,
            string afterWalletRevision,
            string beforeRevivalRevision,
            string afterRevivalRevision,
            string atomicCommitRevision,
            bool wasDeadBefore,
            bool isAliveAfter)
        {
            Status = status;
            OperationId = operationId ?? string.Empty;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            DeathFingerprint = deathFingerprint ?? string.Empty;
            AccountId = accountId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            CharacterId = characterId ?? string.Empty;
            TechnicalCurrencyId = technicalCurrencyId ?? string.Empty;
            ProviderId = providerId ?? string.Empty;
            BindingRevision = bindingRevision ?? string.Empty;
            DebitUnits = debitUnits;
            BeforeWalletBalance = beforeWalletBalance;
            AfterWalletBalance = afterWalletBalance;
            BeforeWalletRevision = beforeWalletRevision ?? string.Empty;
            AfterWalletRevision = afterWalletRevision ?? string.Empty;
            BeforeRevivalRevision = beforeRevivalRevision ?? string.Empty;
            AfterRevivalRevision = afterRevivalRevision ?? string.Empty;
            AtomicCommitRevision = atomicCommitRevision ?? string.Empty;
            WasDeadBefore = wasDeadBefore;
            IsAliveAfter = isAliveAfter;
        }

        public DeathPenaltyAtomicRevivalStatus Status { get; }
        public string OperationId { get; }
        public string RequestFingerprint { get; }
        public string DeathFingerprint { get; }
        public string AccountId { get; }
        public string ProfileId { get; }
        public string CharacterId { get; }
        public string TechnicalCurrencyId { get; }
        public string ProviderId { get; }
        public string BindingRevision { get; }
        public long DebitUnits { get; }
        public long BeforeWalletBalance { get; }
        public long AfterWalletBalance { get; }
        public string BeforeWalletRevision { get; }
        public string AfterWalletRevision { get; }
        public string BeforeRevivalRevision { get; }
        public string AfterRevivalRevision { get; }
        public string AtomicCommitRevision { get; }
        public bool WasDeadBefore { get; }
        public bool IsAliveAfter { get; }
    }

    public sealed class DeathPenaltyCommitProposal
    {
        internal DeathPenaltyCommitProposal(
            string operationId,
            string requestFingerprint,
            string deathFingerprint,
            string accountId,
            string profileId,
            string characterId,
            string policyVersion,
            string levelCapPolicyId,
            string levelCapPolicyRevision,
            DeathPenaltyBranch branch,
            int beforeLevel,
            int afterLevel,
            int maximumLevel,
            long experienceUnitsPerLevel,
            long beforeInLevelExperienceUnits,
            long afterInLevelExperienceUnits,
            string beforeProgressionRevision,
            OathmarkWalletBinding oathmarkBinding,
            long oathmarkDebitUnits,
            long beforeOathmarkBalance,
            long afterOathmarkBalance,
            string beforeOathmarkWalletRevision,
            string beforeRevivalRevision,
            bool requiresProgressionWrite,
            bool requiresOathmarkWalletDebit,
            bool requiresAtomicRevival,
            string planHash)
        {
            OperationId = operationId;
            RequestFingerprint = requestFingerprint;
            DeathFingerprint = deathFingerprint;
            AccountId = accountId;
            ProfileId = profileId;
            CharacterId = characterId;
            PolicyVersion = policyVersion;
            LevelCapPolicyId = levelCapPolicyId;
            LevelCapPolicyRevision = levelCapPolicyRevision;
            Branch = branch;
            BeforeLevel = beforeLevel;
            AfterLevel = afterLevel;
            MaximumLevel = maximumLevel;
            ExperienceUnitsPerLevel = experienceUnitsPerLevel;
            BeforeInLevelExperienceUnits = beforeInLevelExperienceUnits;
            AfterInLevelExperienceUnits = afterInLevelExperienceUnits;
            BeforeProgressionRevision = beforeProgressionRevision;
            OathmarkBinding = oathmarkBinding;
            OathmarkDebitUnits = oathmarkDebitUnits;
            BeforeOathmarkBalance = beforeOathmarkBalance;
            AfterOathmarkBalance = afterOathmarkBalance;
            BeforeOathmarkWalletRevision = beforeOathmarkWalletRevision;
            BeforeRevivalRevision = beforeRevivalRevision;
            RequiresProgressionWrite = requiresProgressionWrite;
            RequiresOathmarkWalletDebit = requiresOathmarkWalletDebit;
            RequiresAtomicRevival = requiresAtomicRevival;
            PlanHash = planHash;
        }

        public string OperationId { get; }
        public string RequestFingerprint { get; }
        public string DeathFingerprint { get; }
        public string AccountId { get; }
        public string ProfileId { get; }
        public string CharacterId { get; }
        public string PolicyVersion { get; }
        public string LevelCapPolicyId { get; }
        public string LevelCapPolicyRevision { get; }
        public DeathPenaltyBranch Branch { get; }
        public int BeforeLevel { get; }
        public int AfterLevel { get; }
        public int MaximumLevel { get; }
        public long ExperienceUnitsPerLevel { get; }
        public long BeforeInLevelExperienceUnits { get; }
        public long AfterInLevelExperienceUnits { get; }
        public string BeforeProgressionRevision { get; }
        public OathmarkWalletBinding OathmarkBinding { get; }
        public long OathmarkDebitUnits { get; }
        public long BeforeOathmarkBalance { get; }
        public long AfterOathmarkBalance { get; }
        public string BeforeOathmarkWalletRevision { get; }
        public string BeforeRevivalRevision { get; }
        public bool RequiresProgressionWrite { get; }
        public bool RequiresOathmarkWalletDebit { get; }
        public bool RequiresAtomicRevival { get; }
        public string PlanHash { get; }
    }

    public sealed class DeathPenaltyReceipt
    {
        internal DeathPenaltyReceipt(
            DeathPenaltyCommitProposal proposal,
            string afterProgressionRevision,
            string afterOathmarkWalletRevision,
            string afterRevivalRevision,
            string atomicCommitRevision,
            string atomicRevivalFingerprint,
            bool revivalCommitted,
            string receiptHash)
        {
            Proposal = proposal;
            AfterProgressionRevision = afterProgressionRevision ?? string.Empty;
            AfterOathmarkWalletRevision = afterOathmarkWalletRevision ?? string.Empty;
            AfterRevivalRevision = afterRevivalRevision ?? string.Empty;
            AtomicCommitRevision = atomicCommitRevision ?? string.Empty;
            AtomicRevivalFingerprint = atomicRevivalFingerprint ?? string.Empty;
            RevivalCommitted = revivalCommitted;
            ReceiptHash = receiptHash ?? string.Empty;
        }

        public DeathPenaltyCommitProposal Proposal { get; }
        public string OperationId => Proposal?.OperationId ?? string.Empty;
        public string RequestFingerprint => Proposal?.RequestFingerprint ?? string.Empty;
        public DeathPenaltyBranch Branch => Proposal?.Branch ?? DeathPenaltyBranch.Unknown;
        public string AfterProgressionRevision { get; }
        public string AfterOathmarkWalletRevision { get; }
        public string AfterRevivalRevision { get; }
        public string AtomicCommitRevision { get; }
        public string AtomicRevivalFingerprint { get; }
        public bool RevivalCommitted { get; }
        public string ReceiptHash { get; }
    }

    public sealed class DeathPenaltyPlan
    {
        internal DeathPenaltyPlan(
            DeathPenaltyPlanStatus status,
            DeathPenaltyCommitProposal proposal,
            DeathPenaltyReceipt replayReceipt,
            string diagnosticCode)
        {
            Status = status;
            Proposal = proposal;
            ReplayReceipt = replayReceipt;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public DeathPenaltyPlanStatus Status { get; }
        public DeathPenaltyCommitProposal Proposal { get; }
        public DeathPenaltyReceipt ReplayReceipt { get; }
        public string DiagnosticCode { get; }
        public bool CanCommit =>
            Status == DeathPenaltyPlanStatus.ReadyToCommit &&
            Proposal != null;
        public bool IsCommittedReplay =>
            Status == DeathPenaltyPlanStatus.ReplayedCommitted &&
            ReplayReceipt != null;
        public bool HasMutationProposal => CanCommit;
    }
}
