using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using AL.ChampionMode.C1;

namespace AL.ChampionMode.Integration
{
    public enum ChampionEncounterSourceDisposition
    {
        BlockedRequired = 0,
        Published = 1
    }

    public enum ChampionEncounterApplicationStatus
    {
        Applied = 0,
        DuplicateExact = 1,
        CatalogUnavailable = 2,
        InvalidSource = 3,
        InvalidDependency = 4,
        CorrelationConflict = 5,
        ApplicationRejected = 6
    }

    /// <summary>
    /// The complete source handoff consumed by the application boundary. The
    /// production factory intentionally exposes only the current C7A
    /// blocked-required disposition. PublishedForTests is structural evidence
    /// for the future #183 whole-catalog publisher; it is not production data.
    /// </summary>
    public sealed class ChampionEncounterSourceEnvelope
    {
        private ChampionEncounterSourceEnvelope(
            ChampionEncounterSourceDisposition disposition,
            string authorityId,
            string authorityRevision,
            ChampionEncounterRequestPlan encounterPlan,
            CombatSkillLoadSessionSnapshot skillLoadSession,
            string sourceRevision)
        {
            Disposition = disposition;
            AuthorityId = authorityId ?? string.Empty;
            AuthorityRevision = authorityRevision ?? string.Empty;
            EncounterPlan = encounterPlan;
            SkillLoadSession = skillLoadSession;
            SourceRevision = sourceRevision ?? string.Empty;
        }

        public ChampionEncounterSourceDisposition Disposition { get; }
        public string AuthorityId { get; }
        public string AuthorityRevision { get; }
        public ChampionEncounterRequestPlan EncounterPlan { get; }
        public CombatSkillLoadSessionSnapshot SkillLoadSession { get; }
        public string SourceRevision { get; }

        public static ChampionEncounterSourceEnvelope BlockedRequired(
            string authorityId,
            string authorityRevision)
        {
            return new ChampionEncounterSourceEnvelope(
                ChampionEncounterSourceDisposition.BlockedRequired,
                authorityId,
                authorityRevision,
                null,
                null,
                string.Empty);
        }

        internal static ChampionEncounterSourceEnvelope PublishedForTests(
            string authorityId,
            string authorityRevision,
            ChampionEncounterRequestPlan encounterPlan,
            CombatSkillLoadSessionSnapshot skillLoadSession,
            string sourceRevision = "source-r1")
        {
            return new ChampionEncounterSourceEnvelope(
                ChampionEncounterSourceDisposition.Published,
                authorityId,
                authorityRevision,
                encounterPlan,
                skillLoadSession,
                sourceRevision);
        }
    }

    public interface IChampionEncounterApplication
    {
        bool TryApply(ChampionEncounterStateSnapshot initialState);
    }

    public sealed class ChampionEncounterApplicationReceipt
    {
        internal ChampionEncounterApplicationReceipt(
            string applicationId,
            string sourceFingerprint,
            ChampionEncounterStateSnapshot state)
        {
            ApplicationId = applicationId;
            SourceFingerprint = sourceFingerprint;
            State = state;
        }

        public string ApplicationId { get; }
        public string SourceFingerprint { get; }
        public ChampionEncounterStateSnapshot State { get; }
    }

    public sealed class ChampionEncounterApplicationPlan
    {
        internal ChampionEncounterApplicationPlan(
            ChampionEncounterApplicationStatus status,
            string diagnosticCode,
            ChampionEncounterApplicationReceipt receipt)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Receipt = receipt;
        }

        public ChampionEncounterApplicationStatus Status { get; }
        public string DiagnosticCode { get; }
        public ChampionEncounterApplicationReceipt Receipt { get; }
    }

    /// <summary>
    /// Application-owned orchestration only. C1 owns request resolution and
    /// initial encounter state; C1b owns atomic skill publication. #183 owns
    /// source publication, #168 reward receipts, #174 battle application,
    /// #137 durable commit/recovery, and #184 appearance mutation. This gateway
    /// neither selects content nor applies those domains.
    /// </summary>
    public static class ChampionEncounterApplicationGateway
    {
        private const string CatalogUnavailable =
            "AL-CHAMPION-APPLICATION-CATALOG-UNAVAILABLE";
        private const string InvalidSource =
            "AL-CHAMPION-APPLICATION-SOURCE-INVALID";
        private const string InvalidDependency =
            "AL-CHAMPION-APPLICATION-DEPENDENCY-INVALID";
        private const string CorrelationConflict =
            "AL-CHAMPION-APPLICATION-CORRELATION-CONFLICT";
        private const string ApplicationRejected =
            "AL-CHAMPION-APPLICATION-REJECTED";

        public static ChampionEncounterApplicationPlan Apply(
            ChampionEncounterSourceEnvelope source,
            IChampionEncounterApplication application,
            IList<ChampionEncounterApplicationReceipt> receipts)
        {
            if (source == null || !ValidAuthority(source))
            {
                return Plan(
                    ChampionEncounterApplicationStatus.InvalidSource,
                    InvalidSource);
            }

            if (source.Disposition ==
                ChampionEncounterSourceDisposition.BlockedRequired)
            {
                return Plan(
                    ChampionEncounterApplicationStatus.CatalogUnavailable,
                    CatalogUnavailable);
            }

            if (source.Disposition != ChampionEncounterSourceDisposition.Published ||
                !ValidPublishedSource(source))
            {
                return Plan(
                    ChampionEncounterApplicationStatus.InvalidSource,
                    InvalidSource);
            }

            if (application == null || receipts == null)
            {
                return Plan(
                    ChampionEncounterApplicationStatus.InvalidDependency,
                    InvalidDependency);
            }

            ChampionEncounterStateSnapshot initialState =
                ChampionEncounterPlanner.CreateInitialState(
                    source.EncounterPlan.Resolved);
            if (initialState == null)
            {
                return Plan(
                    ChampionEncounterApplicationStatus.InvalidSource,
                    InvalidSource);
            }

            string applicationId = initialState.EncounterResultId;
            string fingerprint = Fingerprint(source, initialState);
            ChampionEncounterApplicationReceipt existing = null;
            for (int index = 0; index < receipts.Count; index++)
            {
                ChampionEncounterApplicationReceipt candidate = receipts[index];
                if (candidate == null)
                {
                    return Plan(
                        ChampionEncounterApplicationStatus.InvalidDependency,
                        InvalidDependency);
                }

                if (!string.Equals(
                        candidate.ApplicationId,
                        applicationId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (existing != null)
                {
                    return Plan(
                        ChampionEncounterApplicationStatus.InvalidDependency,
                        InvalidDependency);
                }

                existing = candidate;
            }

            if (existing != null)
            {
                return string.Equals(
                        existing.SourceFingerprint,
                        fingerprint,
                        StringComparison.Ordinal)
                    ? new ChampionEncounterApplicationPlan(
                        ChampionEncounterApplicationStatus.DuplicateExact,
                        string.Empty,
                        existing)
                    : Plan(
                        ChampionEncounterApplicationStatus.CorrelationConflict,
                        CorrelationConflict);
            }

            if (!application.TryApply(initialState))
            {
                return Plan(
                    ChampionEncounterApplicationStatus.ApplicationRejected,
                    ApplicationRejected);
            }

            return new ChampionEncounterApplicationPlan(
                ChampionEncounterApplicationStatus.Applied,
                string.Empty,
                new ChampionEncounterApplicationReceipt(
                    applicationId,
                    fingerprint,
                    initialState));
        }

        private static bool ValidAuthority(
            ChampionEncounterSourceEnvelope source)
        {
            return StableText(source.AuthorityId) &&
                   IsLowerHex(source.AuthorityRevision, 40);
        }

        private static bool ValidPublishedSource(
            ChampionEncounterSourceEnvelope source)
        {
            return StableText(source.SourceRevision) &&
                   source.EncounterPlan != null &&
                   source.EncounterPlan.Status ==
                       ChampionEncounterRequestStatus.Resolved &&
                   source.EncounterPlan.Resolved != null &&
                   source.SkillLoadSession != null &&
                   source.SkillLoadSession.Status ==
                       CombatSkillLoadStatus.Loaded &&
                   source.SkillLoadSession.HasPublishedSnapshot &&
                   source.SkillLoadSession.IsAuthoritative;
        }

        private static string Fingerprint(
            ChampionEncounterSourceEnvelope source,
            ChampionEncounterStateSnapshot state)
        {
            string canonical = string.Join(
                "\u001f",
                source.AuthorityId,
                source.AuthorityRevision,
                source.SourceRevision,
                source.EncounterPlan.Resolved.SemanticFingerprint,
                source.EncounterPlan.Resolved.SourceSnapshotHash,
                source.SkillLoadSession.OwnerId.Value,
                source.SkillLoadSession.Generation.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                ((int)source.SkillLoadSession.Status).ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                state.EncounterSessionId,
                state.EncounterAttemptId,
                state.EncounterResultId);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var result = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    result.Append(hash[index].ToString("x2"));
                }

                return result.ToString();
            }
        }

        private static bool StableText(string value)
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

        private static bool IsLowerHex(string value, int length)
        {
            if (value == null || value.Length != length)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        private static ChampionEncounterApplicationPlan Plan(
            ChampionEncounterApplicationStatus status,
            string diagnosticCode)
        {
            return new ChampionEncounterApplicationPlan(
                status,
                diagnosticCode,
                null);
        }
    }
}
