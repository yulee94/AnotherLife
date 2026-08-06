using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using AL.Core.SaveAuthority;
using AL.Narrative.Nvs01.Contracts;

[assembly: InternalsVisibleTo("AL.EditMode.Tests")]

namespace AL.Narrative.Nvs01
{
    internal static class Nvs01ConsequenceContract
    {
        internal const int ContractVersion = 1;
        internal const string PacketVersion =
            Nvs01CatalogContract.PacketVersion;
        internal const string PacketSha256 =
            Nvs01CatalogContract.CanonicalSha256;
        internal const int PacketByteLength =
            Nvs01CatalogContract.CanonicalByteLength;
        internal const string QuestId = Nvs01CatalogContract.QuestId;

        internal const string InvestigateStateId =
            "INVESTIGATE_SKY_CASTLE";
        internal const string ReportStateId = "REPORT_TO_VALERIUS";
        internal const string CompletedStateId = "COMPLETED";
        internal const string ArenaObjectiveId = "OBJ_OMEN_1_ARENA";
        internal const string ReportObjectiveId = "OBJ_OMEN_1_REPORT";
        internal const string TalkObjectiveId = "OBJ_OMEN_1_TALK";
        internal const string ArenaHookId = "HOOK_SKY_CASTLE_ARENA";
        internal const string ArenaLocationId =
            "LOCATION_SKY_CASTLE_MARKER";
        internal const string ArenaStartDialogueId =
            "DLG_OMEN_1_ARENA_START";
        internal const string ArenaSuccessEventId =
            "EVENT_SKY_CASTLE_ARENA_SUCCESS";
        internal const string ArenaFailureEventId =
            "EVENT_SKY_CASTLE_ARENA_FAILURE";
        internal const string ArenaCancelledEventId =
            "EVENT_SKY_CASTLE_ARENA_CANCELLED";
        internal const string ArenaUnavailableEventId =
            "EVENT_SKY_CASTLE_ARENA_UNAVAILABLE";
        internal const string ReturnSceneId = "Kingdom";
        internal const string ReportConclusionEventId =
            "DLG_OMEN_1_REPORT_CONCLUSION";
        internal const string ReportDialogueId = "DLG_OMEN_1_REPORT";
        internal const string ReportConclusionDialogueId =
            "DLG_OMEN_1_REPORT_CONCLUSION";
        internal const string ReportConclusionChoiceId =
            "choice.omen1.present_tear";

        internal const string TearConsequenceId =
            "ACQUIRE_CELESTIAL_TEAR";
        internal const string GoldConsequenceId = "GRANT_GOLD_500";
        internal const string AffinityConsequenceId =
            "GRANT_VALERIUS_AFFINITY_5";
        internal const string CompletionConsequenceId = "COMPLETE_OMEN_1";
        internal const string ChapterConsequenceId =
            "UNLOCK_REALM_CHAPTER_1";

        internal const string TearArtifactId = "ARTIFACT_CELESTIAL_TEAR";
        internal const string GoldResourceId = "RESOURCE_GOLD";
        internal const string ValeriusNpcId = "NPC_VALERIUS";
        internal const string AbstractChapterId = "CH1_REALM_INTRO";

        internal const string ArenaOperationPrefix =
            "OMEN_1:ARENA_SUCCESS:";
        internal const string ReportOperationId =
            "OMEN_1:REPORT_COMPLETE:v1";
        internal const long GoldAmount = 500;
        internal const float AffinityAmount = 5f;
        internal const float MinimumAffinity = -100f;
        internal const float MaximumAffinity = 100f;

        internal const int ExpectedConsequenceCount = 5;
        internal const int MaximumChapterDefinitionCount = 16;
        internal const int MaximumArtifactCount = 16;
        internal const int MaximumAppliedOperationCount = 2;
        internal const int MaximumAppliedEffectCount = 5;
        internal const int MaximumApplicationReceiptCount = 2;
        internal const int MaximumChapterOrder = 4096;
        internal const int DependencyAuthorityContractVersion = 1;
        internal const int MaximumAuthorityTokenLength = 128;

        private static readonly IReadOnlyList<string> ConsequenceOrder =
            Array.AsReadOnly(
                new[]
                {
                    TearConsequenceId,
                    GoldConsequenceId,
                    AffinityConsequenceId,
                    CompletionConsequenceId,
                    ChapterConsequenceId
                });

        internal static IReadOnlyList<string> ExpectedConsequenceOrder =>
            ConsequenceOrder;

        internal static string ChapterForRealm(string realmId)
        {
            switch (realmId)
            {
                case "crownlands":
                    return "C1_CL";
                case "stonehold":
                    return "C1_SH";
                case "eldergrove":
                    return "C1_EG";
                case "umbral":
                    return "C1_UM";
                default:
                    return string.Empty;
            }
        }
    }

    internal static class Nvs01ConsequenceDiagnosticCodes
    {
        internal const string MissingInput =
            "AL-NVS01-C4-MISSING-INPUT";
        internal const string ContractMismatch =
            "AL-NVS01-C4-CONTRACT-MISMATCH";
        internal const string AuthorityUnavailable =
            "AL-NVS01-C4-AUTHORITY-UNAVAILABLE";
        internal const string StaleAuthority =
            "AL-NVS01-C4-STALE-AUTHORITY";
        internal const string StaleQuestRevision =
            "AL-NVS01-C4-STALE-QUEST-REVISION";
        internal const string QuestTransitionMismatch =
            "AL-NVS01-C4-QUEST-TRANSITION-MISMATCH";
        internal const string DependencyUnavailable =
            "AL-NVS01-C4-DEPENDENCY-UNAVAILABLE";
        internal const string DependencyMalformed =
            "AL-NVS01-C4-DEPENDENCY-MALFORMED";
        internal const string PartialApplication =
            "AL-NVS01-C4-PARTIAL-APPLICATION";
        internal const string Overflow = "AL-NVS01-C4-OVERFLOW";
        internal const string ChapterIncompatible =
            "AL-NVS01-C4-CHAPTER-INCOMPATIBLE";
    }

    internal enum Nvs01ConsequencePlanningStatus
    {
        Ready = 0,
        AlreadyApplied = 1,
        RejectedMissingInput = 2,
        RejectedContractMismatch = 3,
        RejectedAuthorityUnavailable = 4,
        RejectedStaleAuthority = 5,
        RejectedStaleQuestRevision = 6,
        RejectedQuestTransitionMismatch = 7,
        RejectedDependencyUnavailable = 8,
        RejectedDependencyMalformed = 9,
        RejectedPartialApplication = 10,
        RejectedOverflow = 11,
        RejectedChapterIncompatible = 12
    }

    internal enum Nvs01ConsequencePlanKind
    {
        ArenaSuccess = 0,
        ReportCompletion = 1
    }

    internal enum Nvs01ConsequenceMutationKind
    {
        AcquireArtifact = 0,
        CreditResource = 1,
        AdjustAffinity = 2,
        CompleteQuest = 3,
        UnlockChapter = 4
    }

    internal enum Nvs01ConsequenceDependencyStatus
    {
        Available = 0,
        Unavailable = 1,
        Missing = 2,
        Duplicate = 3,
        Malformed = 4
    }

    internal sealed class Nvs01ChapterReference
    {
        internal Nvs01ChapterReference(
            string chapterId,
            string realmId,
            int progressionOrder,
            bool isForwardOnly)
        {
            ChapterId = chapterId ?? string.Empty;
            RealmId = realmId ?? string.Empty;
            ProgressionOrder = progressionOrder;
            IsForwardOnly = isForwardOnly;
        }

        internal string ChapterId { get; }
        internal string RealmId { get; }
        internal int ProgressionOrder { get; }
        internal bool IsForwardOnly { get; }
    }

    internal sealed class Nvs01ChapterAuthoritySnapshot
    {
        internal Nvs01ChapterAuthoritySnapshot(
            Nvs01ConsequenceDependencyStatus status,
            bool isComplete,
            IList<Nvs01ChapterReference> chapters)
        {
            Status = status;
            IsComplete = isComplete;
            InputCount = chapters?.Count ?? -1;
            Chapters = Nvs01ConsequenceImmutable.Freeze(
                chapters,
                Nvs01ConsequenceContract.MaximumChapterDefinitionCount + 1);
        }

        internal Nvs01ConsequenceDependencyStatus Status { get; }
        internal bool IsComplete { get; }
        internal int InputCount { get; }
        internal IReadOnlyList<Nvs01ChapterReference> Chapters { get; }
    }

    internal sealed class Nvs01ConsequenceDomainSnapshot
    {
        internal Nvs01ConsequenceDomainSnapshot(
            Nvs01ConsequenceDependencyStatus artifactDefinitionStatus,
            Nvs01ConsequenceDependencyStatus goldDefinitionStatus,
            Nvs01ConsequenceDependencyStatus affinityDefinitionStatus,
            long goldBalance,
            float valeriusAffinity,
            string currentChapterId,
            IList<string> acquiredArtifactIds,
            IList<string> appliedOperationIds,
            IList<string> appliedEffectKeys,
            IList<Nvs01ConsequenceApplicationReceipt>
                applicationReceipts)
        {
            ArtifactDefinitionStatus = artifactDefinitionStatus;
            GoldDefinitionStatus = goldDefinitionStatus;
            AffinityDefinitionStatus = affinityDefinitionStatus;
            GoldBalance = goldBalance;
            ValeriusAffinity = valeriusAffinity;
            CurrentChapterId = currentChapterId ?? string.Empty;
            AcquiredArtifactInputCount = acquiredArtifactIds?.Count ?? -1;
            AcquiredArtifactIds = Nvs01ConsequenceImmutable.FreezeStrings(
                acquiredArtifactIds,
                Nvs01ConsequenceContract.MaximumArtifactCount + 1);
            AppliedOperationInputCount = appliedOperationIds?.Count ?? -1;
            AppliedOperationIds = Nvs01ConsequenceImmutable.FreezeStrings(
                appliedOperationIds,
                Nvs01ConsequenceContract.MaximumAppliedOperationCount + 1);
            AppliedEffectInputCount = appliedEffectKeys?.Count ?? -1;
            AppliedEffectKeys = Nvs01ConsequenceImmutable.FreezeStrings(
                appliedEffectKeys,
                Nvs01ConsequenceContract.MaximumAppliedEffectCount + 1);
            ApplicationReceiptInputCount =
                applicationReceipts?.Count ?? -1;
            ApplicationReceipts = Nvs01ConsequenceImmutable.Freeze(
                applicationReceipts,
                Nvs01ConsequenceContract.MaximumApplicationReceiptCount + 1);
        }

        internal Nvs01ConsequenceDependencyStatus ArtifactDefinitionStatus
        {
            get;
        }
        internal Nvs01ConsequenceDependencyStatus GoldDefinitionStatus
        {
            get;
        }
        internal Nvs01ConsequenceDependencyStatus AffinityDefinitionStatus
        {
            get;
        }
        internal long GoldBalance { get; }
        internal float ValeriusAffinity { get; }
        internal string CurrentChapterId { get; }
        internal int AcquiredArtifactInputCount { get; }
        internal IReadOnlyList<string> AcquiredArtifactIds { get; }
        internal int AppliedOperationInputCount { get; }
        internal IReadOnlyList<string> AppliedOperationIds { get; }
        internal int AppliedEffectInputCount { get; }
        internal IReadOnlyList<string> AppliedEffectKeys { get; }
        internal int ApplicationReceiptInputCount { get; }
        internal IReadOnlyList<Nvs01ConsequenceApplicationReceipt>
            ApplicationReceipts { get; }
    }

    internal sealed class Nvs01ConsequenceDependencyProviderIdentity
    {
        internal Nvs01ConsequenceDependencyProviderIdentity(
            int contractVersion,
            string providerId,
            string catalogSetId,
            string contentVersion,
            string sourceRevision,
            string sourceFingerprint,
            long providerRevision)
        {
            ContractVersion = contractVersion;
            ProviderId = providerId ?? string.Empty;
            CatalogSetId = catalogSetId ?? string.Empty;
            ContentVersion = contentVersion ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            SourceFingerprint = sourceFingerprint ?? string.Empty;
            ProviderRevision = providerRevision;
        }

        internal int ContractVersion { get; }
        internal string ProviderId { get; }
        internal string CatalogSetId { get; }
        internal string ContentVersion { get; }
        internal string SourceRevision { get; }
        internal string SourceFingerprint { get; }
        internal long ProviderRevision { get; }
    }

    internal sealed class Nvs01ConsequenceReceiptAuthorityEntry
    {
        internal Nvs01ConsequenceReceiptAuthorityEntry(
            string operationId,
            string planFingerprint)
        {
            OperationId = operationId ?? string.Empty;
            PlanFingerprint = planFingerprint ?? string.Empty;
        }

        internal string OperationId { get; }
        internal string PlanFingerprint { get; }
    }

    internal sealed class Nvs01ConsequenceReceiptAuthoritySnapshot
    {
        internal Nvs01ConsequenceReceiptAuthoritySnapshot(
            IList<Nvs01ConsequenceReceiptAuthorityEntry> entries)
        {
            InputCount = entries?.Count ?? -1;
            Entries = Nvs01ConsequenceImmutable.Freeze(
                entries,
                Nvs01ConsequenceContract.MaximumApplicationReceiptCount + 1);
        }

        internal int InputCount { get; }
        internal IReadOnlyList<Nvs01ConsequenceReceiptAuthorityEntry>
            Entries { get; }

        internal bool TryGetExpectedFingerprint(
            string operationId,
            out string planFingerprint)
        {
            for (int index = 0; index < Entries.Count; index++)
            {
                Nvs01ConsequenceReceiptAuthorityEntry entry = Entries[index];
                if (entry != null && string.Equals(
                        entry.OperationId,
                        operationId,
                        StringComparison.Ordinal))
                {
                    planFingerprint = entry.PlanFingerprint;
                    return true;
                }
            }

            planFingerprint = string.Empty;
            return false;
        }
    }

    internal sealed class Nvs01ConsequenceDependencyProviderCapture
    {
        internal Nvs01ConsequenceDependencyProviderCapture(
            Nvs01ConsequenceDependencyProviderIdentity identity,
            string profileId,
            string expectedGenerationFingerprint,
            Nvs01CapabilitySnapshot capabilities,
            Nvs01ConsequenceDomainSnapshot domain,
            Nvs01ChapterAuthoritySnapshot chapters,
            Nvs01ConsequenceReceiptAuthoritySnapshot receiptAuthorities)
        {
            Identity = identity;
            ProfileId = profileId ?? string.Empty;
            ExpectedGenerationFingerprint =
                expectedGenerationFingerprint ?? string.Empty;
            Capabilities = capabilities;
            Domain = domain;
            Chapters = chapters;
            ReceiptAuthorities = receiptAuthorities;
        }

        internal Nvs01ConsequenceDependencyProviderIdentity Identity
        {
            get;
        }
        internal string ProfileId { get; }
        internal string ExpectedGenerationFingerprint { get; }
        internal Nvs01CapabilitySnapshot Capabilities { get; }
        internal Nvs01ConsequenceDomainSnapshot Domain { get; }
        internal Nvs01ChapterAuthoritySnapshot Chapters { get; }
        internal Nvs01ConsequenceReceiptAuthoritySnapshot ReceiptAuthorities
        {
            get;
        }
    }

    internal interface INvs01ConsequenceDependencyProvider
    {
        bool TryGetIdentity(
            out Nvs01ConsequenceDependencyProviderIdentity identity);

        bool TryCapture(
            string profileId,
            string expectedGenerationFingerprint,
            out Nvs01ConsequenceDependencyProviderCapture capture);
    }

    internal sealed class Nvs01VerifiedConsequenceDependencies
    {
        private readonly object _issuerProvenance;

        internal Nvs01VerifiedConsequenceDependencies(
            Nvs01ConsequenceDependencyProviderIdentity providerIdentity,
            string catalogId,
            string packetVersion,
            string packetSha256,
            int packetByteLength,
            string profileId,
            string expectedGenerationFingerprint,
            Nvs01CapabilitySnapshot capabilities,
            Nvs01ConsequenceDomainSnapshot domain,
            Nvs01ChapterAuthoritySnapshot chapters,
            Nvs01ConsequenceReceiptAuthoritySnapshot receiptAuthorities,
            string authorityFingerprint)
            : this(
                providerIdentity,
                catalogId,
                packetVersion,
                packetSha256,
                packetByteLength,
                profileId,
                expectedGenerationFingerprint,
                capabilities,
                domain,
                chapters,
                receiptAuthorities,
                authorityFingerprint,
                null)
        {
        }

        private Nvs01VerifiedConsequenceDependencies(
            Nvs01ConsequenceDependencyProviderIdentity providerIdentity,
            string catalogId,
            string packetVersion,
            string packetSha256,
            int packetByteLength,
            string profileId,
            string expectedGenerationFingerprint,
            Nvs01CapabilitySnapshot capabilities,
            Nvs01ConsequenceDomainSnapshot domain,
            Nvs01ChapterAuthoritySnapshot chapters,
            Nvs01ConsequenceReceiptAuthoritySnapshot receiptAuthorities,
            string authorityFingerprint,
            object issuerProvenance)
        {
            ProviderIdentity = providerIdentity;
            CatalogId = catalogId ?? string.Empty;
            PacketVersion = packetVersion ?? string.Empty;
            PacketSha256 = packetSha256 ?? string.Empty;
            PacketByteLength = packetByteLength;
            ProfileId = profileId ?? string.Empty;
            ExpectedGenerationFingerprint =
                expectedGenerationFingerprint ?? string.Empty;
            Capabilities = capabilities;
            Domain = domain;
            Chapters = chapters;
            ReceiptAuthorities = receiptAuthorities;
            AuthorityFingerprint = authorityFingerprint ?? string.Empty;
            _issuerProvenance = issuerProvenance;
        }

        internal Nvs01ConsequenceDependencyProviderIdentity ProviderIdentity
        {
            get;
        }
        internal string CatalogId { get; }
        internal string PacketVersion { get; }
        internal string PacketSha256 { get; }
        internal int PacketByteLength { get; }
        internal string ProfileId { get; }
        internal string ExpectedGenerationFingerprint { get; }
        internal Nvs01CapabilitySnapshot Capabilities { get; }
        internal Nvs01ConsequenceDomainSnapshot Domain { get; }
        internal Nvs01ChapterAuthoritySnapshot Chapters { get; }
        internal Nvs01ConsequenceReceiptAuthoritySnapshot ReceiptAuthorities
        {
            get;
        }
        internal string AuthorityFingerprint { get; }

        internal static Nvs01VerifiedConsequenceDependencies Issue(
            Nvs01ConsequenceDependencyProviderCapture capture,
            Nvs01VerifiedCatalog catalog,
            object issuerProvenance)
        {
            var provisional = new Nvs01VerifiedConsequenceDependencies(
                capture.Identity,
                catalog.CatalogId,
                catalog.Catalog.PacketVersion,
                catalog.CanonicalSha256,
                catalog.CanonicalByteLength,
                capture.ProfileId,
                capture.ExpectedGenerationFingerprint,
                capture.Capabilities,
                capture.Domain,
                capture.Chapters,
                capture.ReceiptAuthorities,
                string.Empty,
                issuerProvenance);
            return new Nvs01VerifiedConsequenceDependencies(
                provisional.ProviderIdentity,
                provisional.CatalogId,
                provisional.PacketVersion,
                provisional.PacketSha256,
                provisional.PacketByteLength,
                provisional.ProfileId,
                provisional.ExpectedGenerationFingerprint,
                provisional.Capabilities,
                provisional.Domain,
                provisional.Chapters,
                provisional.ReceiptAuthorities,
                ComputeFingerprint(provisional),
                issuerProvenance);
        }

        internal bool IsIssuedBy(object issuerProvenance) =>
            issuerProvenance != null &&
            ReferenceEquals(_issuerProvenance, issuerProvenance);

        internal bool HasCanonicalFingerprint() =>
            string.Equals(
                AuthorityFingerprint,
                ComputeFingerprint(this),
                StringComparison.Ordinal);

        private static string ComputeFingerprint(
            Nvs01VerifiedConsequenceDependencies value)
        {
            var builder = new StringBuilder(4096);
            AppendIdentity(builder, value.ProviderIdentity);
            Append(builder, value.CatalogId);
            Append(builder, value.PacketVersion);
            Append(builder, value.PacketSha256);
            Append(
                builder,
                value.PacketByteLength.ToString(CultureInfo.InvariantCulture));
            Append(builder, value.ProfileId);
            Append(builder, value.ExpectedGenerationFingerprint);

            if (value.Capabilities == null)
            {
                Append(builder, "capabilities:null");
            }
            else
            {
                var capabilities = new List<KeyValuePair<string, bool>>(
                    value.Capabilities.Availability);
                capabilities.Sort(
                    (left, right) => string.CompareOrdinal(
                        left.Key,
                        right.Key));
                Append(
                    builder,
                    capabilities.Count.ToString(CultureInfo.InvariantCulture));
                for (int index = 0; index < capabilities.Count; index++)
                {
                    Append(builder, capabilities[index].Key);
                    Append(builder, capabilities[index].Value ? "1" : "0");
                }
            }

            AppendDomain(builder, value.Domain);
            AppendChapters(builder, value.Chapters);
            AppendReceiptAuthorities(builder, value.ReceiptAuthorities);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(builder.ToString()));
                var hex = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    hex.Append(hash[index].ToString(
                        "x2", CultureInfo.InvariantCulture));
                }
                return hex.ToString();
            }
        }

        private static void AppendIdentity(
            StringBuilder builder,
            Nvs01ConsequenceDependencyProviderIdentity identity)
        {
            if (identity == null)
            {
                Append(builder, "identity:null");
                return;
            }

            Append(
                builder,
                identity.ContractVersion.ToString(
                    CultureInfo.InvariantCulture));
            Append(builder, identity.ProviderId);
            Append(builder, identity.CatalogSetId);
            Append(builder, identity.ContentVersion);
            Append(builder, identity.SourceRevision);
            Append(builder, identity.SourceFingerprint);
            Append(
                builder,
                identity.ProviderRevision.ToString(
                    CultureInfo.InvariantCulture));
        }

        private static void AppendDomain(
            StringBuilder builder,
            Nvs01ConsequenceDomainSnapshot domain)
        {
            if (domain == null)
            {
                Append(builder, "domain:null");
                return;
            }

            Append(builder, ((int)domain.ArtifactDefinitionStatus).ToString(
                CultureInfo.InvariantCulture));
            Append(builder, ((int)domain.GoldDefinitionStatus).ToString(
                CultureInfo.InvariantCulture));
            Append(builder, ((int)domain.AffinityDefinitionStatus).ToString(
                CultureInfo.InvariantCulture));
            Append(builder, domain.GoldBalance.ToString(
                CultureInfo.InvariantCulture));
            Append(builder, domain.ValeriusAffinity.ToString(
                "R", CultureInfo.InvariantCulture));
            Append(builder, domain.CurrentChapterId);
            AppendStrings(
                builder,
                domain.AcquiredArtifactInputCount,
                domain.AcquiredArtifactIds);
            AppendStrings(
                builder,
                domain.AppliedOperationInputCount,
                domain.AppliedOperationIds);
            AppendStrings(
                builder,
                domain.AppliedEffectInputCount,
                domain.AppliedEffectKeys);
            Append(
                builder,
                domain.ApplicationReceiptInputCount.ToString(
                    CultureInfo.InvariantCulture));
            for (int index = 0;
                 index < domain.ApplicationReceipts.Count;
                 index++)
            {
                Nvs01ConsequenceApplicationReceipt receipt =
                    domain.ApplicationReceipts[index];
                if (receipt == null)
                {
                    Append(builder, "receipt:null");
                    continue;
                }

                Append(builder, receipt.PlanFingerprint);
                Append(builder, receipt.OperationId);
                Append(builder, receipt.ProfileId);
                Append(builder, receipt.ExpectedGenerationFingerprint);
                Append(builder, receipt.CausalOperationId);
                Append(builder, receipt.CausalPayloadFingerprint);
                Append(builder, receipt.PredecessorReceiptFingerprint);
                Append(
                    builder,
                    receipt.PredecessorExpectedGenerationFingerprint);
                Append(builder, receipt.RealmId);
                Append(builder, receipt.CorrelationId);
                Append(builder, receipt.ExpectedQuestRevision.ToString(
                    CultureInfo.InvariantCulture));
                Append(builder, receipt.CandidateQuestRevision.ToString(
                    CultureInfo.InvariantCulture));
            }
        }

        private static void AppendChapters(
            StringBuilder builder,
            Nvs01ChapterAuthoritySnapshot chapters)
        {
            if (chapters == null)
            {
                Append(builder, "chapters:null");
                return;
            }

            Append(builder, ((int)chapters.Status).ToString(
                CultureInfo.InvariantCulture));
            Append(builder, chapters.IsComplete ? "1" : "0");
            Append(builder, chapters.InputCount.ToString(
                CultureInfo.InvariantCulture));
            for (int index = 0; index < chapters.Chapters.Count; index++)
            {
                Nvs01ChapterReference chapter = chapters.Chapters[index];
                if (chapter == null)
                {
                    Append(builder, "chapter:null");
                    continue;
                }

                Append(builder, chapter.ChapterId);
                Append(builder, chapter.RealmId);
                Append(builder, chapter.ProgressionOrder.ToString(
                    CultureInfo.InvariantCulture));
                Append(builder, chapter.IsForwardOnly ? "1" : "0");
            }
        }

        private static void AppendReceiptAuthorities(
            StringBuilder builder,
            Nvs01ConsequenceReceiptAuthoritySnapshot authorities)
        {
            if (authorities == null)
            {
                Append(builder, "receipt-authorities:null");
                return;
            }

            Append(builder, authorities.InputCount.ToString(
                CultureInfo.InvariantCulture));
            var entries = new List<Nvs01ConsequenceReceiptAuthorityEntry>(
                authorities.Entries);
            entries.Sort(
                (left, right) => string.CompareOrdinal(
                    left?.OperationId,
                    right?.OperationId));
            for (int index = 0; index < entries.Count; index++)
            {
                Nvs01ConsequenceReceiptAuthorityEntry entry = entries[index];
                if (entry == null)
                {
                    Append(builder, "receipt-authority:null");
                    continue;
                }

                Append(builder, entry.OperationId);
                Append(builder, entry.PlanFingerprint);
            }
        }

        private static void AppendStrings(
            StringBuilder builder,
            int inputCount,
            IReadOnlyList<string> values)
        {
            Append(builder, inputCount.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                Append(builder, values[index]);
            }
        }

        private static void Append(StringBuilder builder, string value)
        {
            string safe = value ?? string.Empty;
            builder.Append(safe.Length.ToString(
                    CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safe)
                .Append('|');
        }
    }

    internal sealed class Nvs01ConsequenceDependencyAuthority
    {
        private readonly INvs01ConsequenceDependencyProvider _provider;
        private readonly string _expectedProviderId;
        private readonly string _expectedCatalogSetId;
        private readonly object _issuerProvenance = new object();

        internal Nvs01ConsequenceDependencyAuthority(
            INvs01ConsequenceDependencyProvider provider,
            string expectedProviderId,
            string expectedCatalogSetId)
        {
            _provider = provider ??
                throw new ArgumentNullException(nameof(provider));
            _expectedProviderId = RequireAuthorityToken(
                expectedProviderId,
                nameof(expectedProviderId));
            _expectedCatalogSetId = RequireAuthorityToken(
                expectedCatalogSetId,
                nameof(expectedCatalogSetId));
        }

        internal bool TryCapture(
            Nvs01VerifiedCatalog catalog,
            string profileId,
            string expectedGenerationFingerprint,
            out Nvs01VerifiedConsequenceDependencies dependencies)
        {
            dependencies = null;
            try
            {
                if (!CatalogIsCanonical(catalog) ||
                    !Nvs01AuthorityGuard.IsCanonicalProfileId(profileId) ||
                    !Nvs01AuthorityGuard.IsCanonicalSha256(
                        expectedGenerationFingerprint) ||
                    !_provider.TryGetIdentity(
                        out Nvs01ConsequenceDependencyProviderIdentity current) ||
                    !IdentityIsCanonical(current) ||
                    !_provider.TryCapture(
                        profileId,
                        expectedGenerationFingerprint,
                        out Nvs01ConsequenceDependencyProviderCapture capture) ||
                    capture == null ||
                    !IdentitiesMatch(current, capture.Identity) ||
                    !string.Equals(
                        capture.ProfileId,
                        profileId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        capture.ExpectedGenerationFingerprint,
                        expectedGenerationFingerprint,
                        StringComparison.Ordinal) ||
                    capture.Capabilities == null ||
                    capture.Domain == null ||
                    capture.Chapters == null ||
                    capture.ReceiptAuthorities == null ||
                    !CaptureIsBounded(capture) ||
                    !CapabilitiesMatchCatalog(
                        catalog,
                        capture.Capabilities))
                {
                    return false;
                }

                dependencies =
                    Nvs01VerifiedConsequenceDependencies.Issue(
                        capture,
                        catalog,
                        _issuerProvenance);
                return dependencies.HasCanonicalFingerprint();
            }
            catch
            {
                dependencies = null;
                return false;
            }
        }

        internal bool IsCurrent(
            Nvs01VerifiedConsequenceDependencies dependencies,
            Nvs01VerifiedCatalog catalog,
            string profileId,
            string expectedGenerationFingerprint)
        {
            try
            {
                return dependencies != null &&
                       dependencies.IsIssuedBy(_issuerProvenance) &&
                       dependencies.HasCanonicalFingerprint() &&
                       CatalogIsCanonical(catalog) &&
                       string.Equals(
                           dependencies.CatalogId,
                           catalog.CatalogId,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           dependencies.PacketVersion,
                           catalog.Catalog.PacketVersion,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           dependencies.PacketSha256,
                           catalog.CanonicalSha256,
                           StringComparison.Ordinal) &&
                       dependencies.PacketByteLength ==
                           catalog.CanonicalByteLength &&
                       string.Equals(
                           dependencies.ProfileId,
                           profileId,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           dependencies.ExpectedGenerationFingerprint,
                           expectedGenerationFingerprint,
                           StringComparison.Ordinal) &&
                       _provider.TryGetIdentity(
                           out Nvs01ConsequenceDependencyProviderIdentity current) &&
                       IdentityIsCanonical(current) &&
                       IdentitiesMatch(
                           current,
                           dependencies.ProviderIdentity) &&
                       CapabilitiesMatchCatalog(
                           catalog,
                           dependencies.Capabilities);
            }
            catch
            {
                return false;
            }
        }

        private bool IdentityIsCanonical(
            Nvs01ConsequenceDependencyProviderIdentity identity) =>
            identity != null &&
            identity.ContractVersion ==
                Nvs01ConsequenceContract
                    .DependencyAuthorityContractVersion &&
            string.Equals(
                identity.ProviderId,
                _expectedProviderId,
                StringComparison.Ordinal) &&
            string.Equals(
                identity.CatalogSetId,
                _expectedCatalogSetId,
                StringComparison.Ordinal) &&
            IsAuthorityToken(identity.ContentVersion) &&
            IsAuthorityToken(identity.SourceRevision) &&
            Nvs01AuthorityGuard.IsCanonicalSha256(
                identity.SourceFingerprint) &&
            identity.ProviderRevision > 0;

        private static bool IdentitiesMatch(
            Nvs01ConsequenceDependencyProviderIdentity left,
            Nvs01ConsequenceDependencyProviderIdentity right) =>
            left != null && right != null &&
            left.ContractVersion == right.ContractVersion &&
            left.ProviderRevision == right.ProviderRevision &&
            string.Equals(
                left.ProviderId,
                right.ProviderId,
                StringComparison.Ordinal) &&
            string.Equals(
                left.CatalogSetId,
                right.CatalogSetId,
                StringComparison.Ordinal) &&
            string.Equals(
                left.ContentVersion,
                right.ContentVersion,
                StringComparison.Ordinal) &&
            string.Equals(
                left.SourceRevision,
                right.SourceRevision,
                StringComparison.Ordinal) &&
            string.Equals(
                left.SourceFingerprint,
                right.SourceFingerprint,
                StringComparison.Ordinal);

        private static bool CatalogIsCanonical(
            Nvs01VerifiedCatalog catalog) =>
            catalog != null &&
            catalog.Catalog != null &&
            string.Equals(
                catalog.CatalogId,
                Nvs01CatalogContract.CatalogId,
                StringComparison.Ordinal) &&
            string.Equals(
                catalog.Catalog.PacketVersion,
                Nvs01ConsequenceContract.PacketVersion,
                StringComparison.Ordinal) &&
            string.Equals(
                catalog.CanonicalSha256,
                Nvs01ConsequenceContract.PacketSha256,
                StringComparison.Ordinal) &&
            catalog.CanonicalByteLength ==
                Nvs01ConsequenceContract.PacketByteLength;

        private static bool CapabilitiesMatchCatalog(
            Nvs01VerifiedCatalog catalog,
            Nvs01CapabilitySnapshot capabilities)
        {
            if (catalog?.Catalog == null || capabilities == null ||
                capabilities.Availability.Count !=
                    catalog.Catalog.ExternalCapabilities.Count)
            {
                return false;
            }

            for (int index = 0;
                 index < catalog.Catalog.ExternalCapabilities.Count;
                 index++)
            {
                if (!capabilities.Availability.ContainsKey(
                        catalog.Catalog.ExternalCapabilities[index].Id))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CaptureIsBounded(
            Nvs01ConsequenceDependencyProviderCapture capture)
        {
            Nvs01ConsequenceDomainSnapshot domain = capture.Domain;
            Nvs01ChapterAuthoritySnapshot chapters = capture.Chapters;
            Nvs01ConsequenceReceiptAuthoritySnapshot receiptAuthorities =
                capture.ReceiptAuthorities;
            if (domain.AcquiredArtifactInputCount >
                    Nvs01ConsequenceContract.MaximumArtifactCount ||
                domain.AppliedOperationInputCount >
                    Nvs01ConsequenceContract.MaximumAppliedOperationCount ||
                domain.AppliedEffectInputCount >
                    Nvs01ConsequenceContract.MaximumAppliedEffectCount ||
                domain.ApplicationReceiptInputCount >
                    Nvs01ConsequenceContract.MaximumApplicationReceiptCount ||
                chapters.InputCount >
                    Nvs01ConsequenceContract.MaximumChapterDefinitionCount ||
                receiptAuthorities.InputCount >
                    Nvs01ConsequenceContract.MaximumApplicationReceiptCount ||
                !IsBoundedValue(domain.CurrentChapterId) ||
                !ValuesAreBounded(domain.AcquiredArtifactIds) ||
                !ValuesAreBounded(domain.AppliedOperationIds) ||
                !ValuesAreBounded(domain.AppliedEffectKeys))
            {
                return false;
            }

            for (int index = 0; index < chapters.Chapters.Count; index++)
            {
                Nvs01ChapterReference chapter = chapters.Chapters[index];
                if (chapter != null &&
                    (!IsBoundedValue(chapter.ChapterId) ||
                    !IsBoundedValue(chapter.RealmId))
                   )
                {
                    return false;
                }
            }

            for (int index = 0;
                 index < receiptAuthorities.Entries.Count;
                 index++)
            {
                Nvs01ConsequenceReceiptAuthorityEntry entry =
                    receiptAuthorities.Entries[index];
                if (entry != null &&
                    (!IsBoundedValue(entry.OperationId) ||
                    !IsBoundedValue(entry.PlanFingerprint))
                   )
                {
                    return false;
                }
            }

            for (int index = 0;
                 index < domain.ApplicationReceipts.Count;
                 index++)
            {
                Nvs01ConsequenceApplicationReceipt receipt =
                    domain.ApplicationReceipts[index];
                if (receipt != null &&
                    (receipt.EffectKeyInputCount >
                        Nvs01ConsequenceContract.MaximumAppliedEffectCount ||
                    !IsBoundedValue(receipt.OperationId) ||
                    !IsBoundedValue(receipt.ProfileId) ||
                    !IsBoundedValue(
                        receipt.ExpectedGenerationFingerprint) ||
                    !IsBoundedValue(receipt.CausalOperationId) ||
                    !IsBoundedValue(receipt.CausalPayloadFingerprint) ||
                    !IsBoundedValue(
                        receipt.PredecessorReceiptFingerprint) ||
                    !IsBoundedValue(
                        receipt.PredecessorExpectedGenerationFingerprint) ||
                    !IsBoundedValue(receipt.RealmId) ||
                    !IsBoundedValue(receipt.CorrelationId) ||
                    !ValuesAreBounded(receipt.EffectKeys) ||
                    !IsBoundedValue(receipt.TargetChapterId) ||
                    !IsBoundedValue(receipt.PreviousChapterId) ||
                    !IsBoundedValue(receipt.ResultingChapterId) ||
                    !IsBoundedValue(receipt.PlanFingerprint)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValuesAreBounded(
            IReadOnlyList<string> values)
        {
            if (values == null) return true;
            for (int index = 0; index < values.Count; index++)
            {
                if (!IsBoundedValue(values[index])) return false;
            }

            return true;
        }

        private static bool IsBoundedValue(string value) =>
            value == null ||
            value.Length <= Nvs01RuntimeContract.MaximumIdentifierLength;

        private static string RequireAuthorityToken(
            string value,
            string parameterName)
        {
            if (!IsAuthorityToken(value))
            {
                throw new ArgumentException(
                    "A bounded authority identity is required.",
                    parameterName);
            }

            return value;
        }

        private static bool IsAuthorityToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length >
                    Nvs01ConsequenceContract.MaximumAuthorityTokenLength ||
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
    }

    internal sealed class Nvs01ConsequencePlanningContext
    {
        internal Nvs01ConsequencePlanningContext(
            Nvs01VerifiedCatalog catalog,
            Nvs01MutationPlan questMutation,
            ProfileWriteAuthoritySnapshot authority,
            ProfileAuthorityExpectation expectedAuthority,
            long expectedQuestRevision,
            Nvs01ConsequenceReceiptExpectation receiptExpectation,
            Nvs01VerifiedConsequenceDependencies dependencies)
        {
            Catalog = catalog;
            QuestMutation = questMutation;
            Authority = authority;
            ExpectedAuthority = expectedAuthority;
            ExpectedQuestRevision = expectedQuestRevision;
            ReceiptExpectation = receiptExpectation;
            Dependencies = dependencies;
        }

        internal Nvs01VerifiedCatalog Catalog { get; }
        internal Nvs01MutationPlan QuestMutation { get; }
        internal ProfileWriteAuthoritySnapshot Authority { get; }
        internal ProfileAuthorityExpectation ExpectedAuthority { get; }
        internal long ExpectedQuestRevision { get; }
        internal Nvs01ConsequenceReceiptExpectation ReceiptExpectation
        {
            get;
        }
        internal Nvs01VerifiedConsequenceDependencies Dependencies { get; }
        internal Nvs01CapabilitySnapshot Capabilities =>
            Dependencies?.Capabilities;
        internal Nvs01ConsequenceDomainSnapshot Domain =>
            Dependencies?.Domain;
        internal Nvs01ChapterAuthoritySnapshot Chapters =>
            Dependencies?.Chapters;
        internal Nvs01ConsequenceReceiptAuthoritySnapshot ReceiptAuthorities =>
            Dependencies?.ReceiptAuthorities;
    }

    internal sealed class Nvs01ConsequenceReceiptExpectation
    {
        internal Nvs01ConsequenceReceiptExpectation(
            string operationId,
            string payloadFingerprint)
        {
            OperationId = operationId ?? string.Empty;
            PayloadFingerprint = payloadFingerprint ?? string.Empty;
        }

        internal string OperationId { get; }
        internal string PayloadFingerprint { get; }
    }

    internal sealed class Nvs01ConsequenceOperation
    {
        internal Nvs01ConsequenceOperation(
            string consequenceId,
            Nvs01ConsequenceMutationKind kind,
            string targetId,
            long amount,
            string value)
        {
            ConsequenceId = consequenceId;
            Kind = kind;
            TargetId = targetId;
            Amount = amount;
            Value = value ?? string.Empty;
        }

        internal string ConsequenceId { get; }
        internal Nvs01ConsequenceMutationKind Kind { get; }
        internal string TargetId { get; }
        internal long Amount { get; }
        internal string Value { get; }
    }

    internal sealed class Nvs01ConsequenceApplicationReceipt
    {
        internal Nvs01ConsequenceApplicationReceipt(
            int contractVersion,
            Nvs01ConsequencePlanKind kind,
            string operationId,
            string profileId,
            string expectedGenerationFingerprint,
            string causalOperationId,
            string causalPayloadFingerprint,
            string predecessorReceiptFingerprint,
            string predecessorExpectedGenerationFingerprint,
            string realmId,
            string correlationId,
            long expectedQuestRevision,
            long candidateQuestRevision,
            IList<string> effectKeys,
            string targetChapterId,
            long previousGoldBalance,
            long resultingGoldBalance,
            float previousValeriusAffinity,
            float resultingValeriusAffinity,
            string previousChapterId,
            string resultingChapterId,
            string planFingerprint)
        {
            ContractVersion = contractVersion;
            Kind = kind;
            OperationId = operationId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            ExpectedGenerationFingerprint =
                expectedGenerationFingerprint ?? string.Empty;
            CausalOperationId = causalOperationId ?? string.Empty;
            CausalPayloadFingerprint =
                causalPayloadFingerprint ?? string.Empty;
            PredecessorReceiptFingerprint =
                predecessorReceiptFingerprint ?? string.Empty;
            PredecessorExpectedGenerationFingerprint =
                predecessorExpectedGenerationFingerprint ?? string.Empty;
            RealmId = realmId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            ExpectedQuestRevision = expectedQuestRevision;
            CandidateQuestRevision = candidateQuestRevision;
            EffectKeyInputCount = effectKeys?.Count ?? -1;
            EffectKeys = Nvs01ConsequenceImmutable.FreezeStrings(
                effectKeys,
                Nvs01ConsequenceContract.MaximumAppliedEffectCount + 1);
            TargetChapterId = targetChapterId ?? string.Empty;
            PreviousGoldBalance = previousGoldBalance;
            ResultingGoldBalance = resultingGoldBalance;
            PreviousValeriusAffinity = previousValeriusAffinity;
            ResultingValeriusAffinity = resultingValeriusAffinity;
            PreviousChapterId = previousChapterId ?? string.Empty;
            ResultingChapterId = resultingChapterId ?? string.Empty;
            PlanFingerprint = planFingerprint ?? string.Empty;
        }

        internal int ContractVersion { get; }
        internal Nvs01ConsequencePlanKind Kind { get; }
        internal string OperationId { get; }
        internal string ProfileId { get; }
        internal string ExpectedGenerationFingerprint { get; }
        internal string CausalOperationId { get; }
        internal string CausalPayloadFingerprint { get; }
        internal string PredecessorReceiptFingerprint { get; }
        internal string PredecessorExpectedGenerationFingerprint { get; }
        internal string RealmId { get; }
        internal string CorrelationId { get; }
        internal long ExpectedQuestRevision { get; }
        internal long CandidateQuestRevision { get; }
        internal int EffectKeyInputCount { get; }
        internal IReadOnlyList<string> EffectKeys { get; }
        internal string TargetChapterId { get; }
        internal long PreviousGoldBalance { get; }
        internal long ResultingGoldBalance { get; }
        internal float PreviousValeriusAffinity { get; }
        internal float ResultingValeriusAffinity { get; }
        internal string PreviousChapterId { get; }
        internal string ResultingChapterId { get; }
        internal string PlanFingerprint { get; }

        internal static Nvs01ConsequenceApplicationReceipt Create(
            Nvs01ConsequencePlanKind kind,
            string operationId,
            string profileId,
            string expectedGenerationFingerprint,
            string causalOperationId,
            string causalPayloadFingerprint,
            string predecessorReceiptFingerprint,
            string predecessorExpectedGenerationFingerprint,
            string realmId,
            string correlationId,
            long expectedQuestRevision,
            long candidateQuestRevision,
            IList<string> effectKeys,
            string targetChapterId,
            long previousGoldBalance,
            long resultingGoldBalance,
            float previousValeriusAffinity,
            float resultingValeriusAffinity,
            string previousChapterId,
            string resultingChapterId)
        {
            var provisional = new Nvs01ConsequenceApplicationReceipt(
                Nvs01ConsequenceContract.ContractVersion,
                kind,
                operationId,
                profileId,
                expectedGenerationFingerprint,
                causalOperationId,
                causalPayloadFingerprint,
                predecessorReceiptFingerprint,
                predecessorExpectedGenerationFingerprint,
                realmId,
                correlationId,
                expectedQuestRevision,
                candidateQuestRevision,
                effectKeys,
                targetChapterId,
                previousGoldBalance,
                resultingGoldBalance,
                previousValeriusAffinity,
                resultingValeriusAffinity,
                previousChapterId,
                resultingChapterId,
                string.Empty);
            return new Nvs01ConsequenceApplicationReceipt(
                provisional.ContractVersion,
                provisional.Kind,
                provisional.OperationId,
                provisional.ProfileId,
                provisional.ExpectedGenerationFingerprint,
                provisional.CausalOperationId,
                provisional.CausalPayloadFingerprint,
                provisional.PredecessorReceiptFingerprint,
                provisional.PredecessorExpectedGenerationFingerprint,
                provisional.RealmId,
                provisional.CorrelationId,
                provisional.ExpectedQuestRevision,
                provisional.CandidateQuestRevision,
                new List<string>(provisional.EffectKeys),
                provisional.TargetChapterId,
                provisional.PreviousGoldBalance,
                provisional.ResultingGoldBalance,
                provisional.PreviousValeriusAffinity,
                provisional.ResultingValeriusAffinity,
                provisional.PreviousChapterId,
                provisional.ResultingChapterId,
                ComputeFingerprint(provisional));
        }

        internal bool HasCanonicalFingerprint() =>
            string.Equals(
                PlanFingerprint,
                ComputeFingerprint(this),
                StringComparison.Ordinal);

        private static string ComputeFingerprint(
            Nvs01ConsequenceApplicationReceipt receipt)
        {
            var builder = new StringBuilder(512);
            Append(builder, receipt.ContractVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(builder, ((int)receipt.Kind).ToString(
                CultureInfo.InvariantCulture));
            Append(builder, receipt.OperationId);
            Append(builder, receipt.ProfileId);
            Append(builder, receipt.ExpectedGenerationFingerprint);
            Append(builder, receipt.CausalOperationId);
            Append(builder, receipt.CausalPayloadFingerprint);
            Append(builder, receipt.PredecessorReceiptFingerprint);
            Append(
                builder,
                receipt.PredecessorExpectedGenerationFingerprint);
            Append(builder, receipt.RealmId);
            Append(builder, receipt.CorrelationId);
            Append(builder, receipt.ExpectedQuestRevision.ToString(
                CultureInfo.InvariantCulture));
            Append(builder, receipt.CandidateQuestRevision.ToString(
                CultureInfo.InvariantCulture));
            Append(builder, receipt.EffectKeyInputCount.ToString(
                CultureInfo.InvariantCulture));
            for (int index = 0; index < receipt.EffectKeys.Count; index++)
            {
                Append(builder, receipt.EffectKeys[index]);
            }
            Append(builder, receipt.TargetChapterId);
            Append(builder, receipt.PreviousGoldBalance.ToString(
                CultureInfo.InvariantCulture));
            Append(builder, receipt.ResultingGoldBalance.ToString(
                CultureInfo.InvariantCulture));
            Append(builder, receipt.PreviousValeriusAffinity.ToString(
                "R", CultureInfo.InvariantCulture));
            Append(builder, receipt.ResultingValeriusAffinity.ToString(
                "R", CultureInfo.InvariantCulture));
            Append(builder, receipt.PreviousChapterId);
            Append(builder, receipt.ResultingChapterId);

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(builder.ToString()));
                var hex = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    hex.Append(hash[index].ToString(
                        "x2", CultureInfo.InvariantCulture));
                }
                return hex.ToString();
            }
        }

        private static void Append(StringBuilder builder, string value)
        {
            string safe = value ?? string.Empty;
            builder.Append(safe.Length.ToString(
                    CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safe)
                .Append('|');
        }
    }

    internal sealed class Nvs01ConsequencePlan
    {
        internal Nvs01ConsequencePlan(
            Nvs01ConsequencePlanKind kind,
            string operationId,
            string profileId,
            string authorityEpoch,
            string expectedGenerationFingerprint,
            long expectedQuestRevision,
            long candidateQuestRevision,
            string realmId,
            string correlationId,
            string nextStateId,
            long resultingGoldBalance,
            float resultingValeriusAffinity,
            string resultingChapterId,
            IList<Nvs01ConsequenceOperation> operations,
            Nvs01ConsequenceApplicationReceipt applicationReceipt)
        {
            Kind = kind;
            OperationId = operationId;
            ProfileId = profileId;
            AuthorityEpoch = authorityEpoch;
            ExpectedGenerationFingerprint =
                expectedGenerationFingerprint;
            ExpectedQuestRevision = expectedQuestRevision;
            CandidateQuestRevision = candidateQuestRevision;
            RealmId = realmId;
            CorrelationId = correlationId ?? string.Empty;
            NextStateId = nextStateId;
            ResultingGoldBalance = resultingGoldBalance;
            ResultingValeriusAffinity = resultingValeriusAffinity;
            ResultingChapterId = resultingChapterId ?? string.Empty;
            Operations = Nvs01ConsequenceImmutable.Freeze(
                operations,
                Nvs01ConsequenceContract.ExpectedConsequenceCount);
            ApplicationReceipt = applicationReceipt;
        }

        internal Nvs01ConsequencePlanKind Kind { get; }
        internal string OperationId { get; }
        internal string ProfileId { get; }
        internal string AuthorityEpoch { get; }
        internal string ExpectedGenerationFingerprint { get; }
        internal long ExpectedQuestRevision { get; }
        internal long CandidateQuestRevision { get; }
        internal string RealmId { get; }
        internal string CorrelationId { get; }
        internal string NextStateId { get; }
        internal long ResultingGoldBalance { get; }
        internal float ResultingValeriusAffinity { get; }
        internal string ResultingChapterId { get; }
        internal IReadOnlyList<Nvs01ConsequenceOperation> Operations { get; }
        internal Nvs01ConsequenceApplicationReceipt ApplicationReceipt
        {
            get;
        }
    }

    internal sealed class Nvs01ConsequencePlanningResult
    {
        internal Nvs01ConsequencePlanningResult(
            Nvs01ConsequencePlanningStatus status,
            string diagnosticCode,
            Nvs01ConsequencePlan plan,
            Nvs01ConsequenceApplicationReceipt recoveryReceipt)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Plan = plan;
            RecoveryReceipt = recoveryReceipt;
        }

        internal Nvs01ConsequencePlanningStatus Status { get; }
        internal string DiagnosticCode { get; }
        internal Nvs01ConsequencePlan Plan { get; }
        internal Nvs01ConsequenceApplicationReceipt RecoveryReceipt
        {
            get;
        }
        internal bool IsReady =>
            Status == Nvs01ConsequencePlanningStatus.Ready && Plan != null;
    }

    internal static class Nvs01ConsequenceImmutable
    {
        internal static IReadOnlyList<string> FreezeStrings(
            IList<string> values,
            int maximumCopyCount)
        {
            if (values == null)
            {
                return Array.AsReadOnly(new string[0]);
            }

            int count = Math.Min(values.Count, maximumCopyCount);
            var copy = new string[count];
            for (int index = 0; index < count; index++)
            {
                copy[index] = values[index];
            }

            return Array.AsReadOnly(copy);
        }

        internal static IReadOnlyList<T> Freeze<T>(
            IList<T> values,
            int maximumCopyCount)
        {
            if (values == null)
            {
                return Array.AsReadOnly(new T[0]);
            }

            int count = Math.Min(values.Count, maximumCopyCount);
            var copy = new T[count];
            for (int index = 0; index < count; index++)
            {
                copy[index] = values[index];
            }

            return Array.AsReadOnly(copy);
        }
    }
}
