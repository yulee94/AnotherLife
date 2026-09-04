using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AL.ChampionMode.Encounter
{
    /// <summary>
    /// C2 encounter load/application boundary. Consumes the #183 six-family
    /// Champion/skill source set and never synthesizes records, stats, or
    /// skills. Persistence, rewards, and combat presentation remain C3-C5.
    /// </summary>
    public static class ChampionEncounterLoadGateway
    {
        public const string CatalogUnavailableCode =
            "AL-CHAMPION-ENCOUNTER-CATALOG-UNAVAILABLE";
        public const string InvalidSourceCode =
            "AL-CHAMPION-ENCOUNTER-SOURCE-INVALID";
        public const string InvalidDependencyCode =
            "AL-CHAMPION-ENCOUNTER-DEPENDENCY-INVALID";
        public const string CorrelationConflictCode =
            "AL-CHAMPION-ENCOUNTER-CORRELATION-CONFLICT";
        public const string ApplicationRejectedCode =
            "AL-CHAMPION-ENCOUNTER-REJECTED";
        public const string RealmInvalidCode =
            "AL-CHAMPION-ENCOUNTER-REALM-INVALID";
        public const string StaleSnapshotCode =
            "AL-CHAMPION-ENCOUNTER-STALE-SNAPSHOT";
        public const string HybridSourceCode =
            "AL-CHAMPION-ENCOUNTER-HYBRID-SOURCE";
        public const string SlotOrderInvalidCode =
            "AL-CHAMPION-ENCOUNTER-SLOT-ORDER-INVALID";

        public static ChampionEncounterLoadPlan Start(
            ChampionEncounterLoadRequest request,
            ChampionEncounterSourceSet source,
            IChampionEncounterApplication application,
            IList<ChampionEncounterLoadReceipt> receipts)
        {
            if (request == null || source == null || !ValidAuthority(source))
            {
                return Plan(ChampionEncounterLoadStatus.InvalidSource, InvalidSourceCode);
            }

            if (!IsCommittedValidRealm(request.RealmId))
            {
                return Plan(ChampionEncounterLoadStatus.InvalidSource, RealmInvalidCode);
            }

            if (!string.Equals(
                    request.ExpectedSourceSetVersion,
                    source.SourceSetVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    request.ExpectedSourceSetSha256,
                    source.SourceSetSha256,
                    StringComparison.Ordinal))
            {
                return Plan(
                    ChampionEncounterLoadStatus.CatalogUnavailable,
                    StaleSnapshotCode);
            }

            if (source.Disposition == ChampionEncounterSourceDisposition.BlockedRequired ||
                !source.ProductionEligible ||
                string.Equals(
                    source.ChampionFamilyDisposition,
                    ChampionEncounterSourceSet.BlockedRequiredDisposition,
                    StringComparison.Ordinal) ||
                string.Equals(
                    source.SkillFamilyDisposition,
                    ChampionEncounterSourceSet.BlockedRequiredDisposition,
                    StringComparison.Ordinal))
            {
                return Plan(
                    ChampionEncounterLoadStatus.CatalogUnavailable,
                    CatalogUnavailableCode);
            }

            if (source.Disposition != ChampionEncounterSourceDisposition.Published ||
                !ValidPublishedIdentities(source))
            {
                return Plan(ChampionEncounterLoadStatus.InvalidSource, InvalidSourceCode);
            }

            if (IsHybridSlotSet(source.SlotIds))
            {
                return Plan(ChampionEncounterLoadStatus.InvalidSource, HybridSourceCode);
            }

            if (!IdentitiesMatch(request, source))
            {
                return Plan(ChampionEncounterLoadStatus.InvalidSource, InvalidSourceCode);
            }

            if (!SlotsEqual(request.SlotIds, source.SlotIds) ||
                !SlotsEqual(source.SlotIds, ChampionEncounterSourceSet.AuthoredWireSlotOrder))
            {
                return Plan(
                    ChampionEncounterLoadStatus.InvalidSource,
                    SlotOrderInvalidCode);
            }

            if (application == null || receipts == null)
            {
                return Plan(
                    ChampionEncounterLoadStatus.InvalidDependency,
                    InvalidDependencyCode);
            }

            string fingerprint = Fingerprint(request, source);
            ChampionEncounterLoadReceipt existing = null;
            for (int index = 0; index < receipts.Count; index++)
            {
                ChampionEncounterLoadReceipt candidate = receipts[index];
                if (candidate == null)
                {
                    return Plan(
                        ChampionEncounterLoadStatus.InvalidDependency,
                        InvalidDependencyCode);
                }

                if (!string.Equals(
                        candidate.ApplicationId,
                        request.EncounterId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (existing != null)
                {
                    return Plan(
                        ChampionEncounterLoadStatus.InvalidDependency,
                        InvalidDependencyCode);
                }

                existing = candidate;
            }

            if (existing != null)
            {
                return string.Equals(
                        existing.SourceFingerprint,
                        fingerprint,
                        StringComparison.Ordinal)
                    ? new ChampionEncounterLoadPlan(
                        ChampionEncounterLoadStatus.DuplicateExact,
                        string.Empty,
                        existing)
                    : Plan(
                        ChampionEncounterLoadStatus.CorrelationConflict,
                        CorrelationConflictCode);
            }

            var snapshot = new ChampionEncounterLoadSnapshot(
                request.EncounterId,
                request.RealmId,
                request.ActorId,
                request.CasterId,
                request.BossId,
                request.LoadoutId,
                source.SlotIds,
                fingerprint);
            if (!application.TryApply(snapshot))
            {
                return Plan(
                    ChampionEncounterLoadStatus.ApplicationRejected,
                    ApplicationRejectedCode);
            }

            return new ChampionEncounterLoadPlan(
                ChampionEncounterLoadStatus.Loaded,
                string.Empty,
                new ChampionEncounterLoadReceipt(
                    request.EncounterId,
                    fingerprint,
                    request.RealmId,
                    request.ActorId,
                    request.CasterId,
                    request.BossId,
                    request.LoadoutId,
                    source.SlotIds));
        }

        internal static bool IsCommittedValidRealm(string realmId)
        {
            return string.Equals(realmId, "stonehold", StringComparison.Ordinal) ||
                   string.Equals(realmId, "eldergrove", StringComparison.Ordinal) ||
                   string.Equals(realmId, "crownlands", StringComparison.Ordinal) ||
                   string.Equals(realmId, "umbral", StringComparison.Ordinal);
        }

        private static bool ValidAuthority(ChampionEncounterSourceSet source)
        {
            return StableText(source.AuthorityId) &&
                   IsLowerHex(source.AuthorityRevision, 40) &&
                   StableText(source.SourceSetVersion) &&
                   IsLowerHex(source.SourceSetSha256, 64);
        }

        private static bool ValidPublishedIdentities(ChampionEncounterSourceSet source)
        {
            return StableText(source.SourceRevision) &&
                   IsCommittedValidRealm(source.RealmId) &&
                   StableText(source.ActorId) &&
                   StableText(source.CasterId) &&
                   StableText(source.BossId) &&
                   StableText(source.LoadoutId) &&
                   source.SlotIds != null &&
                   source.SlotIds.Count ==
                       ChampionEncounterSourceSet.AuthoredWireSlotOrder.Length;
        }

        private static bool IdentitiesMatch(
            ChampionEncounterLoadRequest request,
            ChampionEncounterSourceSet source)
        {
            return string.Equals(request.RealmId, source.RealmId, StringComparison.Ordinal) &&
                   string.Equals(request.ActorId, source.ActorId, StringComparison.Ordinal) &&
                   string.Equals(request.CasterId, source.CasterId, StringComparison.Ordinal) &&
                   string.Equals(request.BossId, source.BossId, StringComparison.Ordinal) &&
                   string.Equals(request.LoadoutId, source.LoadoutId, StringComparison.Ordinal);
        }

        private static bool IsHybridSlotSet(IReadOnlyList<string> slotIds)
        {
            if (slotIds == null)
            {
                return true;
            }

            bool sawWire = false;
            bool sawForeign = false;
            for (int index = 0; index < slotIds.Count; index++)
            {
                if (IsAuthoredWireSlot(slotIds[index]))
                {
                    sawWire = true;
                }
                else
                {
                    sawForeign = true;
                }
            }

            return sawWire && sawForeign;
        }

        private static bool IsAuthoredWireSlot(string slotId)
        {
            string[] authored = ChampionEncounterSourceSet.AuthoredWireSlotOrder;
            for (int index = 0; index < authored.Length; index++)
            {
                if (string.Equals(authored[index], slotId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SlotsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string Fingerprint(
            ChampionEncounterLoadRequest request,
            ChampionEncounterSourceSet source)
        {
            string canonical = string.Join(
                "\u001f",
                source.AuthorityId,
                source.AuthorityRevision,
                source.SourceSetVersion,
                source.SourceSetSha256,
                source.SourceRevision,
                request.EncounterId,
                request.RealmId,
                request.ActorId,
                request.CasterId,
                request.BossId,
                request.LoadoutId);
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

        private static ChampionEncounterLoadPlan Plan(
            ChampionEncounterLoadStatus status,
            string diagnosticCode)
        {
            return new ChampionEncounterLoadPlan(status, diagnosticCode, null);
        }
    }
}
