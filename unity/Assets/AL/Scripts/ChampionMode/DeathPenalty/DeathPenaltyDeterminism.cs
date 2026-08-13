using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AL.ChampionMode.DeathPenalty
{
    internal static class DeathPenaltyDeterminism
    {
        // These unkeyed digests support deterministic equality and corruption
        // checks only. They are not signatures, MACs, or authority evidence.
        private const int MaximumStableTextUtf8Bytes = 256;
        private const int MaximumVersionUtf8Bytes = 128;
        private const int MaximumCanonicalCharacters = 16384;
        private const long MaximumIntegerUnitScale = 1000000000000L;

        internal static bool IsStableId(string value) =>
            IsBoundedTechnicalText(value, MaximumStableTextUtf8Bytes);

        internal static bool IsVersion(string value) =>
            IsBoundedTechnicalText(value, MaximumVersionUtf8Bytes);

        internal static bool IsIntegerUnitScale(long value) =>
            value > 0L && value <= MaximumIntegerUnitScale;

        internal static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool digit = character >= '0' && character <= '9';
                bool lowerHex = character >= 'a' && character <= 'f';
                if (!digit && !lowerHex)
                {
                    return false;
                }
            }

            return true;
        }

        internal static string DeathFingerprint(DeathPenaltyRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            return HashFields(
                "death-v1",
                request.AccountId,
                request.ProfileId,
                request.CharacterId,
                request.DeathEventId,
                request.CombatSessionId,
                request.EncounterAttemptId,
                request.InstanceId,
                Long(request.DeathOrdinal));
        }

        internal static string RequestFingerprint(
            DeathPenaltyRequest request,
            DeathPenaltyPolicySnapshot policy)
        {
            if (request == null || policy == null)
            {
                return string.Empty;
            }

            // Current death-state and ledger revisions are CAS observations,
            // not semantic operation identity. A reconciler may refresh them
            // while retaining the exact same death operation fingerprint.
            return HashFields(
                "request-v1",
                request.OperationId,
                DeathFingerprint(request),
                request.ExpectedProgressionRevision,
                request.ExpectedLevelCapPolicyId,
                request.ExpectedLevelCapPolicyRevision,
                request.ExpectedReplayLedgerVersion,
                request.ExpectedOathmarkTechnicalCurrencyId,
                request.ExpectedOathmarkProviderId,
                request.ExpectedOathmarkBindingRevision,
                request.ExpectedOathmarkWalletRevision,
                request.ExpectedRevivalRevision,
                policy.PolicyVersion,
                policy.MaxLevelReviveOathmarkCost.HasValue
                    ? Long(policy.MaxLevelReviveOathmarkCost.Value)
                    : "unset");
        }

        internal static string PlanHash(DeathPenaltyCommitProposal proposal)
        {
            if (proposal == null)
            {
                return string.Empty;
            }

            OathmarkWalletBinding binding = proposal.OathmarkBinding;
            return HashFields(
                "plan-v1",
                proposal.OperationId,
                proposal.RequestFingerprint,
                proposal.DeathFingerprint,
                proposal.AccountId,
                proposal.ProfileId,
                proposal.CharacterId,
                proposal.PolicyVersion,
                proposal.LevelCapPolicyId,
                proposal.LevelCapPolicyRevision,
                Integer((int)proposal.Branch),
                Integer(proposal.BeforeLevel),
                Integer(proposal.AfterLevel),
                Integer(proposal.MaximumLevel),
                Long(proposal.ExperienceUnitsPerLevel),
                Long(proposal.BeforeInLevelExperienceUnits),
                Long(proposal.AfterInLevelExperienceUnits),
                proposal.BeforeProgressionRevision,
                binding?.TechnicalCurrencyId ?? string.Empty,
                binding?.ProviderId ?? string.Empty,
                binding?.BindingRevision ?? string.Empty,
                Integer((int)(binding?.Domain ?? PlayerCurrencyDomain.Unknown)),
                Boolean(binding?.IsSoleMainCurrency ?? false),
                Long(binding?.IntegerUnitScale ?? 0L),
                Long(proposal.OathmarkDebitUnits),
                Long(proposal.BeforeOathmarkBalance),
                Long(proposal.AfterOathmarkBalance),
                proposal.BeforeOathmarkWalletRevision,
                proposal.BeforeRevivalRevision,
                Boolean(proposal.RequiresProgressionWrite),
                Boolean(proposal.RequiresOathmarkWalletDebit),
                Boolean(proposal.RequiresAtomicRevival));
        }

        internal static string ReceiptHash(DeathPenaltyReceipt receipt)
        {
            if (receipt?.Proposal == null)
            {
                return string.Empty;
            }

            return HashFields(
                "receipt-v1",
                receipt.Proposal.PlanHash,
                receipt.AfterProgressionRevision,
                receipt.AfterOathmarkWalletRevision,
                receipt.AfterRevivalRevision,
                receipt.AtomicCommitRevision,
                receipt.AtomicRevivalFingerprint,
                Boolean(receipt.RevivalCommitted));
        }

        internal static string AtomicRevivalFingerprint(
            DeathPenaltyAtomicRevivalSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            return HashFields(
                "atomic-revival-v1",
                Integer((int)snapshot.Status),
                snapshot.OperationId,
                snapshot.RequestFingerprint,
                snapshot.DeathFingerprint,
                snapshot.AccountId,
                snapshot.ProfileId,
                snapshot.CharacterId,
                snapshot.TechnicalCurrencyId,
                snapshot.ProviderId,
                snapshot.BindingRevision,
                Long(snapshot.DebitUnits),
                Long(snapshot.BeforeWalletBalance),
                Long(snapshot.AfterWalletBalance),
                snapshot.BeforeWalletRevision,
                snapshot.AfterWalletRevision,
                snapshot.BeforeRevivalRevision,
                snapshot.AfterRevivalRevision,
                snapshot.AtomicCommitRevision,
                Boolean(snapshot.WasDeadBefore),
                Boolean(snapshot.IsAliveAfter));
        }

        private static string HashFields(params string[] fields)
        {
            var builder = new StringBuilder();
            foreach (string raw in fields ?? Array.Empty<string>())
            {
                string value = raw ?? string.Empty;
                builder
                    .Append(value.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(value);
            }

            if (builder.Length > MaximumCanonicalCharacters)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fields),
                    "Death-penalty canonical payload exceeds its bounded contract.");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            var hex = new StringBuilder(digest.Length * 2);
            for (int index = 0; index < digest.Length; index++)
            {
                hex.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }

        private static string Long(long value) =>
            value.ToString(CultureInfo.InvariantCulture);

        private static string Integer(int value) =>
            value.ToString(CultureInfo.InvariantCulture);

        private static string Boolean(bool value) => value ? "1" : "0";

        private static bool IsBoundedTechnicalText(
            string value,
            int maximumUtf8Bytes)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !StringComparer.Ordinal.Equals(value, value.Trim()))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsControl(character) || char.IsWhiteSpace(character))
                {
                    return false;
                }

                if (!char.IsSurrogate(character))
                {
                    continue;
                }

                if (!char.IsHighSurrogate(character) ||
                    index + 1 >= value.Length ||
                    !char.IsLowSurrogate(value[index + 1]))
                {
                    return false;
                }

                index++;
            }

            return Encoding.UTF8.GetByteCount(value) <= maximumUtf8Bytes;
        }
    }
}
