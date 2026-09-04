using System;
using System.Collections.Generic;

namespace AL.ChampionMode.Encounter
{
    public enum ChampionEncounterSourceDisposition
    {
        BlockedRequired = 0,
        Published = 1
    }

    public enum ChampionEncounterLoadStatus
    {
        Loaded = 0,
        DuplicateExact = 1,
        CatalogUnavailable = 2,
        InvalidSource = 3,
        InvalidDependency = 4,
        CorrelationConflict = 5,
        ApplicationRejected = 6
    }

    public sealed class ChampionEncounterLoadRequest
    {
        public ChampionEncounterLoadRequest(
            string encounterId,
            string realmId,
            string actorId,
            string casterId,
            string bossId,
            string loadoutId,
            string expectedSourceSetVersion,
            string expectedSourceSetSha256,
            IReadOnlyList<string> slotIds)
        {
            EncounterId = encounterId ?? string.Empty;
            RealmId = realmId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            CasterId = casterId ?? string.Empty;
            BossId = bossId ?? string.Empty;
            LoadoutId = loadoutId ?? string.Empty;
            ExpectedSourceSetVersion = expectedSourceSetVersion ?? string.Empty;
            ExpectedSourceSetSha256 = expectedSourceSetSha256 ?? string.Empty;
            SlotIds = CopySlots(slotIds);
        }

        public string EncounterId { get; }
        public string RealmId { get; }
        public string ActorId { get; }
        public string CasterId { get; }
        public string BossId { get; }
        public string LoadoutId { get; }
        public string ExpectedSourceSetVersion { get; }
        public string ExpectedSourceSetSha256 { get; }
        public IReadOnlyList<string> SlotIds { get; }

        private static IReadOnlyList<string> CopySlots(IReadOnlyList<string> slotIds)
        {
            if (slotIds == null || slotIds.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[slotIds.Count];
            for (int index = 0; index < slotIds.Count; index++)
            {
                copy[index] = slotIds[index] ?? string.Empty;
            }

            return copy;
        }
    }

    public sealed class ChampionEncounterSourceSet
    {
        public const string CurrentAuthorityId = "al_six_family_production_authority_v1";
        public const string CurrentAuthorityRevision =
            "382a98f9a2f3ce6f8ee2283107cd593063243e2b";
        public const string CurrentSourceSetVersion = "2026-09-04-v1";
        public const string CurrentSourceSetSha256 =
            "10cf4e2dea9320521572316f0ebba6b11831665408ec325445bd26ecbe8c7597";
        public const string BlockedRequiredDisposition = "blocked_required";

        public static readonly string[] AuthoredWireSlotOrder =
        {
            "realm_strike",
            "renewing_guard",
            "warzone_burst",
            "warmaster_breaker"
        };

        private ChampionEncounterSourceSet(
            ChampionEncounterSourceDisposition disposition,
            bool productionEligible,
            string authorityId,
            string authorityRevision,
            string sourceSetVersion,
            string sourceSetSha256,
            string championFamilyDisposition,
            string skillFamilyDisposition,
            string sourceRevision,
            string realmId,
            string actorId,
            string casterId,
            string bossId,
            string loadoutId,
            IReadOnlyList<string> slotIds)
        {
            Disposition = disposition;
            ProductionEligible = productionEligible;
            AuthorityId = authorityId ?? string.Empty;
            AuthorityRevision = authorityRevision ?? string.Empty;
            SourceSetVersion = sourceSetVersion ?? string.Empty;
            SourceSetSha256 = sourceSetSha256 ?? string.Empty;
            ChampionFamilyDisposition = championFamilyDisposition ?? string.Empty;
            SkillFamilyDisposition = skillFamilyDisposition ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            RealmId = realmId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            CasterId = casterId ?? string.Empty;
            BossId = bossId ?? string.Empty;
            LoadoutId = loadoutId ?? string.Empty;
            SlotIds = slotIds ?? Array.Empty<string>();
        }

        public ChampionEncounterSourceDisposition Disposition { get; }
        public bool ProductionEligible { get; }
        public string AuthorityId { get; }
        public string AuthorityRevision { get; }
        public string SourceSetVersion { get; }
        public string SourceSetSha256 { get; }
        public string ChampionFamilyDisposition { get; }
        public string SkillFamilyDisposition { get; }
        public string SourceRevision { get; }
        public string RealmId { get; }
        public string ActorId { get; }
        public string CasterId { get; }
        public string BossId { get; }
        public string LoadoutId { get; }
        public IReadOnlyList<string> SlotIds { get; }

        public static ChampionEncounterSourceSet CurrentSixFamilyAuthority()
        {
            return new ChampionEncounterSourceSet(
                ChampionEncounterSourceDisposition.BlockedRequired,
                false,
                CurrentAuthorityId,
                CurrentAuthorityRevision,
                CurrentSourceSetVersion,
                CurrentSourceSetSha256,
                BlockedRequiredDisposition,
                BlockedRequiredDisposition,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                Array.Empty<string>());
        }

        public static ChampionEncounterSourceSet PublishedForTests(
            string authorityId,
            string authorityRevision,
            string sourceSetVersion,
            string sourceSetSha256,
            string sourceRevision,
            string realmId,
            string actorId,
            string casterId,
            string bossId,
            string loadoutId,
            IReadOnlyList<string> slotIds)
        {
            var copy = new string[slotIds == null ? 0 : slotIds.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = slotIds[index] ?? string.Empty;
            }

            return new ChampionEncounterSourceSet(
                ChampionEncounterSourceDisposition.Published,
                true,
                authorityId,
                authorityRevision,
                sourceSetVersion,
                sourceSetSha256,
                "source_complete",
                "source_complete",
                sourceRevision,
                realmId,
                actorId,
                casterId,
                bossId,
                loadoutId,
                copy);
        }
    }

    public sealed class ChampionEncounterLoadSnapshot
    {
        internal ChampionEncounterLoadSnapshot(
            string encounterId,
            string realmId,
            string actorId,
            string casterId,
            string bossId,
            string loadoutId,
            IReadOnlyList<string> slotIds,
            string sourceFingerprint)
        {
            EncounterId = encounterId;
            RealmId = realmId;
            ActorId = actorId;
            CasterId = casterId;
            BossId = bossId;
            LoadoutId = loadoutId;
            SlotIds = slotIds;
            SourceFingerprint = sourceFingerprint;
        }

        public string EncounterId { get; }
        public string RealmId { get; }
        public string ActorId { get; }
        public string CasterId { get; }
        public string BossId { get; }
        public string LoadoutId { get; }
        public IReadOnlyList<string> SlotIds { get; }
        public string SourceFingerprint { get; }
    }

    public sealed class ChampionEncounterLoadReceipt
    {
        internal ChampionEncounterLoadReceipt(
            string applicationId,
            string sourceFingerprint,
            string realmId,
            string actorId,
            string casterId,
            string bossId,
            string loadoutId,
            IReadOnlyList<string> slotIds)
        {
            ApplicationId = applicationId;
            SourceFingerprint = sourceFingerprint;
            RealmId = realmId;
            ActorId = actorId;
            CasterId = casterId;
            BossId = bossId;
            LoadoutId = loadoutId;
            SlotIds = slotIds;
        }

        public string ApplicationId { get; }
        public string SourceFingerprint { get; }
        public string RealmId { get; }
        public string ActorId { get; }
        public string CasterId { get; }
        public string BossId { get; }
        public string LoadoutId { get; }
        public IReadOnlyList<string> SlotIds { get; }
    }

    public sealed class ChampionEncounterLoadPlan
    {
        internal ChampionEncounterLoadPlan(
            ChampionEncounterLoadStatus status,
            string diagnosticCode,
            ChampionEncounterLoadReceipt receipt)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Receipt = receipt;
        }

        public ChampionEncounterLoadStatus Status { get; }
        public string DiagnosticCode { get; }
        public ChampionEncounterLoadReceipt Receipt { get; }
    }

    public interface IChampionEncounterApplication
    {
        bool TryApply(ChampionEncounterLoadSnapshot snapshot);
    }
}
