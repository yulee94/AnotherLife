using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AL.Core;
using AL.Core.SaveAuthority;

namespace AL.ChampionMode.Encounter
{
    /// <summary>
    /// C4 durable consequence orchestration. AuthoritativeQuest victory/defeat
    /// results are duplicate-safe and persist only through typed #168 reward
    /// receipts and #137 profile/write authority. Practice, first-session
    /// labeled practice, fallback, and uncommitted paths never mutate saves.
    /// Presentation remains C5.
    /// </summary>
    public static class ChampionEncounterConsequenceGateway
    {
        public const string InvalidInputCode =
            "AL-CHAMPION-ENCOUNTER-CONSEQUENCE-INPUT-INVALID";
        public const string PracticeSuppressedCode =
            "AL-CHAMPION-ENCOUNTER-CONSEQUENCE-PRACTICE-SUPPRESSED";
        public const string ModeRejectedCode =
            "AL-CHAMPION-ENCOUNTER-CONSEQUENCE-MODE-REJECTED";
        public const string InvalidDependencyCode =
            "AL-CHAMPION-ENCOUNTER-CONSEQUENCE-DEPENDENCY-INVALID";
        public const string ProfileWriteUnavailableCode =
            "AL-CHAMPION-ENCOUNTER-CONSEQUENCE-PROFILE-WRITE-UNAVAILABLE";
        public const string RewardUnavailableCode =
            "AL-CHAMPION-ENCOUNTER-CONSEQUENCE-REWARD-UNAVAILABLE";
        public const string CorrelationConflictCode =
            "AL-CHAMPION-ENCOUNTER-CONSEQUENCE-CORRELATION-CONFLICT";
        public const string ApplicationRejectedCode =
            "AL-CHAMPION-ENCOUNTER-CONSEQUENCE-REJECTED";
        public const string NvsIdentityInvalidCode =
            "AL-CHAMPION-ENCOUNTER-CONSEQUENCE-NVS-IDENTITY-INVALID";

        public static ChampionEncounterConsequencePlan ApplyBossDefeat(
            bool labeledPractice,
            RealmId realmId,
            ChampionEncounterLoadReceipt loadReceipt)
        {
            string realm = CanonicalRealm(realmId);
            if (loadReceipt != null &&
                ChampionEncounterLoadGateway.IsCommittedValidRealm(loadReceipt.RealmId))
            {
                realm = loadReceipt.RealmId;
            }

            string encounterId = loadReceipt != null && StableText(loadReceipt.ApplicationId)
                ? loadReceipt.ApplicationId
                : ChampionEncounterProductionLoadPath.ProductionEncounterId;
            var request = new ChampionEncounterConsequenceRequest(
                encounterId,
                encounterId,
                "attempt.terminal",
                labeledPractice
                    ? ChampionEncounterMode.Practice
                    : ChampionEncounterMode.AuthoritativeQuest,
                ChampionEncounterConsequenceOutcome.ChampionVictory,
                realm,
                string.Empty,
                string.Empty,
                string.Empty,
                loadReceipt != null ? loadReceipt.SourceFingerprint : string.Empty,
                string.Empty,
                labeledPractice);
            return Apply(
                request,
                null,
                null,
                null,
                new List<ChampionEncounterConsequenceReceipt>());
        }

        public static ChampionEncounterConsequencePlan Apply(
            ChampionEncounterConsequenceRequest request,
            IChampionEncounterBossRewardAuthority rewardAuthority,
            IProfileWriteAuthorityProvider profileWrite,
            IChampionEncounterProfileCommit commit,
            IList<ChampionEncounterConsequenceReceipt> receipts)
        {
            if (request == null)
            {
                return Plan(ChampionEncounterConsequenceStatus.InvalidInput, InvalidInputCode);
            }

            if (request.LabeledPractice || request.Mode == ChampionEncounterMode.Practice)
            {
                return Plan(
                    ChampionEncounterConsequenceStatus.PracticeSuppressed,
                    PracticeSuppressedCode);
            }

            if (request.Mode != ChampionEncounterMode.AuthoritativeQuest)
            {
                return Plan(ChampionEncounterConsequenceStatus.ModeRejected, ModeRejectedCode);
            }

            if (!ChampionEncounterLoadGateway.IsCommittedValidRealm(request.RealmId))
            {
                return Plan(ChampionEncounterConsequenceStatus.InvalidInput, InvalidInputCode);
            }

            if (!StableText(request.EncounterResultId) ||
                !StableText(request.EncounterId) ||
                !StableText(request.EncounterAttemptId) ||
                !StableText(request.SourceFingerprint) ||
                !StableText(request.ProfileId) ||
                !Enum.IsDefined(typeof(ChampionEncounterConsequenceOutcome), request.Outcome))
            {
                return Plan(ChampionEncounterConsequenceStatus.InvalidInput, InvalidInputCode);
            }

            if (!StableText(request.NvsCorrelationId) ||
                !StableText(request.NvsQuestId))
            {
                return Plan(
                    ChampionEncounterConsequenceStatus.InvalidInput,
                    NvsIdentityInvalidCode);
            }

            if (request.Outcome == ChampionEncounterConsequenceOutcome.ChampionVictory &&
                !StableText(request.RewardOperationId))
            {
                return Plan(ChampionEncounterConsequenceStatus.InvalidInput, InvalidInputCode);
            }

            if (rewardAuthority == null || profileWrite == null || commit == null || receipts == null)
            {
                return Plan(
                    ChampionEncounterConsequenceStatus.InvalidDependency,
                    InvalidDependencyCode);
            }

            ProfileWriteAuthoritySnapshot authority;
            try
            {
                authority = profileWrite.GetCurrentAuthority();
            }
            catch (Exception)
            {
                return Plan(
                    ChampionEncounterConsequenceStatus.ProfileWriteUnavailable,
                    ProfileWriteUnavailableCode);
            }

            if (authority == null ||
                authority.Status != ProfileWriteAuthorityStatus.Writable ||
                !string.Equals(authority.ProfileId, request.ProfileId, StringComparison.Ordinal))
            {
                return Plan(
                    ChampionEncounterConsequenceStatus.ProfileWriteUnavailable,
                    ProfileWriteUnavailableCode);
            }

            string fingerprint = Fingerprint(request);
            ChampionEncounterConsequenceReceipt existing = null;
            for (int index = 0; index < receipts.Count; index++)
            {
                ChampionEncounterConsequenceReceipt candidate = receipts[index];
                if (candidate == null)
                {
                    return Plan(
                        ChampionEncounterConsequenceStatus.InvalidDependency,
                        InvalidDependencyCode);
                }

                if (!string.Equals(
                        candidate.EncounterResultId,
                        request.EncounterResultId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (existing != null)
                {
                    return Plan(
                        ChampionEncounterConsequenceStatus.InvalidDependency,
                        InvalidDependencyCode);
                }

                existing = candidate;
            }

            if (existing != null)
            {
                return string.Equals(
                        existing.ConsequenceFingerprint,
                        fingerprint,
                        StringComparison.Ordinal) &&
                       string.Equals(
                           existing.NvsCorrelationId,
                           request.NvsCorrelationId,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           existing.RealmId,
                           request.RealmId,
                           StringComparison.Ordinal)
                    ? new ChampionEncounterConsequencePlan(
                        ChampionEncounterConsequenceStatus.DuplicateExact,
                        string.Empty,
                        existing)
                    : Plan(
                        ChampionEncounterConsequenceStatus.CorrelationConflict,
                        CorrelationConflictCode);
            }

            ChampionEncounterBossRewardPlan rewardPlan = null;
            if (request.Outcome == ChampionEncounterConsequenceOutcome.ChampionVictory)
            {
                rewardPlan = rewardAuthority.Plan(request);
                if (rewardPlan == null)
                {
                    return Plan(
                        ChampionEncounterConsequenceStatus.RewardAuthorityUnavailable,
                        RewardUnavailableCode);
                }

                if (rewardPlan.Status == ChampionEncounterBossRewardStatus.DuplicateExact)
                {
                    return Plan(
                        ChampionEncounterConsequenceStatus.CorrelationConflict,
                        CorrelationConflictCode);
                }

                if (rewardPlan.Status == ChampionEncounterBossRewardStatus.Unavailable ||
                    rewardPlan.Status == ChampionEncounterBossRewardStatus.Invalid ||
                    rewardPlan.Status == ChampionEncounterBossRewardStatus.CorrelationConflict)
                {
                    return Plan(
                        ChampionEncounterConsequenceStatus.RewardAuthorityUnavailable,
                        string.IsNullOrEmpty(rewardPlan.DiagnosticCode)
                            ? RewardUnavailableCode
                            : rewardPlan.DiagnosticCode);
                }

                if (rewardPlan.Status != ChampionEncounterBossRewardStatus.Issued &&
                    rewardPlan.Status != ChampionEncounterBossRewardStatus.ExplicitNoReward)
                {
                    return Plan(
                        ChampionEncounterConsequenceStatus.RewardAuthorityUnavailable,
                        RewardUnavailableCode);
                }

                if (rewardPlan.Status == ChampionEncounterBossRewardStatus.Issued &&
                    !StableText(rewardPlan.RewardResultId))
                {
                    return Plan(
                        ChampionEncounterConsequenceStatus.RewardAuthorityUnavailable,
                        RewardUnavailableCode);
                }
            }

            var candidateToCommit = new ChampionEncounterConsequenceCandidate(
                request,
                rewardPlan,
                fingerprint);
            if (!commit.TryCommit(candidateToCommit))
            {
                return Plan(
                    ChampionEncounterConsequenceStatus.ApplicationRejected,
                    ApplicationRejectedCode);
            }

            var receipt = new ChampionEncounterConsequenceReceipt(
                request.EncounterResultId,
                request.EncounterId,
                request.Mode,
                request.Outcome,
                request.RealmId,
                request.NvsCorrelationId,
                request.NvsQuestId,
                rewardPlan != null ? rewardPlan.RewardResultId : string.Empty,
                fingerprint,
                request.ProfileId);
            receipts.Add(receipt);
            return new ChampionEncounterConsequencePlan(
                ChampionEncounterConsequenceStatus.Applied,
                string.Empty,
                receipt);
        }

        private static string CanonicalRealm(RealmId realmId)
        {
            switch (realmId)
            {
                case RealmId.Stonehold:
                    return "stonehold";
                case RealmId.Eldergrove:
                    return "eldergrove";
                case RealmId.Crownlands:
                    return "crownlands";
                case RealmId.Umbral:
                    return "umbral";
                default:
                    return string.Empty;
            }
        }

        private static string Fingerprint(ChampionEncounterConsequenceRequest request)
        {
            string canonical = string.Join(
                "\u001f",
                request.EncounterResultId,
                request.EncounterId,
                request.EncounterAttemptId,
                ((int)request.Mode).ToString(CultureInfo.InvariantCulture),
                ((int)request.Outcome).ToString(CultureInfo.InvariantCulture),
                request.RealmId,
                request.NvsCorrelationId,
                request.NvsQuestId,
                request.RewardOperationId,
                request.SourceFingerprint,
                request.ProfileId);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var result = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    result.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }
        }

        internal static bool StableText(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static ChampionEncounterConsequencePlan Plan(
            ChampionEncounterConsequenceStatus status,
            string diagnosticCode)
        {
            return new ChampionEncounterConsequencePlan(status, diagnosticCode, null);
        }
    }
}
