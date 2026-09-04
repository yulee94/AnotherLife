using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public enum OfflineKingdomProductionCatchUpStatus
    {
        Applied = 0,
        Replayed = 1,
        NotApplied = 2,
        CommitUncertain = 3
    }

    public sealed class OfflineKingdomProductionCatchUpResult
    {
        internal OfflineKingdomProductionCatchUpResult(
            OfflineKingdomProductionCatchUpStatus status,
            string diagnosticCode,
            OfflineProductionCatchUpState receipt,
            bool eventPublished)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Receipt = receipt;
            EventPublished = eventPublished;
        }

        public OfflineKingdomProductionCatchUpStatus Status { get; }
        public string DiagnosticCode { get; }
        public OfflineProductionCatchUpState Receipt { get; }
        public bool EventPublished { get; }
        public bool Applied => Status == OfflineKingdomProductionCatchUpStatus.Applied;
    }

    public static class OfflineKingdomProductionCatchUp
    {
        public const string OperationPrefix = "al.offline.catchup.v1";
        public static event Action<OfflineKingdomProductionCatchUpResult> Committed;

        public static OfflineKingdomProductionCatchUpResult TryApplyAfterLoad(
            ISaveGameService saveGameService,
            KingdomProductionProfileSnapshot profile = null,
            Func<long> unixNow = null)
        {
            try
            {
                return Apply(saveGameService, profile, unixNow);
            }
            catch (Exception)
            {
                return NotApplied(EconomyDiagnosticCodes.ProductionDependency);
            }
        }

        private static OfflineKingdomProductionCatchUpResult Apply(
            ISaveGameService saveGameService,
            KingdomProductionProfileSnapshot profile,
            Func<long> unixNow)
        {
            if (saveGameService == null)
            {
                return NotApplied(EconomyDiagnosticCodes.NoCurrentSave);
            }

            if (!(saveGameService is IProfileBoundSaveGameCandidateStore boundStore) ||
                !(saveGameService is IProfileWriteAuthorityProvider))
            {
                return NotApplied(EconomyDiagnosticCodes.ProfileReadOnly);
            }

            if (IsUncertainOrDegraded(saveGameService))
            {
                return NotApplied(EconomyDiagnosticCodes.ProductionProfile);
            }

            ProfileWriteAuthoritySnapshot authority =
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(boundStore);
            if (authority == null ||
                authority.Status != ProfileWriteAuthorityStatus.Writable)
            {
                return NotApplied(EconomyDiagnosticCodes.ProfileReadOnly);
            }

            SaveGameData save;
            try
            {
                save = saveGameService.CurrentSave;
            }
            catch (Exception)
            {
                return NotApplied(EconomyDiagnosticCodes.NoCurrentSave);
            }

            if (save == null)
            {
                return NotApplied(EconomyDiagnosticCodes.NoCurrentSave);
            }

            if (save.SaveSchemaVersion != SaveGameData.CurrentSaveSchemaVersion ||
                string.IsNullOrWhiteSpace(save.ProfileId) ||
                !string.Equals(save.ProfileId, authority.ProfileId, StringComparison.Ordinal))
            {
                return NotApplied(EconomyDiagnosticCodes.ProductionProfile);
            }

            if (profile == null)
            {
                profile = TryLoadLiveLedgerSnapshot();
            }

            if (profile == null ||
                !profile.ProductionEligible ||
                profile.MaxOfflineElapsedSeconds <= 0L ||
                string.IsNullOrWhiteSpace(profile.SourceSha256))
            {
                return NotApplied(EconomyDiagnosticCodes.ProductionCatalog);
            }

            long lastVerified = save.LastSavedTimestamp;
            if (lastVerified <= 0L)
            {
                return NotApplied(EconomyDiagnosticCodes.ProductionElapsed);
            }

            long now = unixNow != null
                ? unixNow()
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now < lastVerified)
            {
                return NotApplied(EconomyDiagnosticCodes.ProductionElapsed);
            }

            long rawElapsed = now - lastVerified;
            if (rawElapsed <= 0L)
            {
                return NotApplied(EconomyDiagnosticCodes.ProductionElapsed);
            }

            long cappedElapsed = rawElapsed > profile.MaxOfflineElapsedSeconds
                ? profile.MaxOfflineElapsedSeconds
                : rawElapsed;
            string operationId = ComputeOperationId(
                save.ProfileId,
                lastVerified,
                profile.SourceSha256);
            if (IsMatchingReceipt(save.OfflineProductionCatchUp, operationId, save.ProfileId))
            {
                return new OfflineKingdomProductionCatchUpResult(
                    OfflineKingdomProductionCatchUpStatus.Replayed,
                    string.Empty,
                    CloneReceipt(save.OfflineProductionCatchUp),
                    false);
            }

            var provider = new KingdomProductionContributionProvider(saveGameService, profile);
            EconomyProductionContributionSnapshot contributions =
                provider.BuildCatchUpContributions(cappedElapsed);
            if (contributions == null ||
                contributions.Status != EconomyProductionSourceStatus.Available ||
                !string.Equals(
                    contributions.ProfileIdentity,
                    save.ProfileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    contributions.SourceRevision,
                    profile.SourceSha256,
                    StringComparison.Ordinal))
            {
                return NotApplied(
                    contributions != null && contributions.Diagnostics.Count > 0
                        ? contributions.Diagnostics[0].Code
                        : EconomyDiagnosticCodes.ProductionCatalog);
            }

            if (!TryPlanDeltas(
                    save,
                    contributions.Contributions,
                    out List<OfflineProductionDeltaRecord> deltas,
                    out string planCode))
            {
                return NotApplied(planCode);
            }

            if (deltas.Count == 0)
            {
                return NotApplied(EconomyDiagnosticCodes.ProductionElapsed);
            }

            var receipt = new OfflineProductionCatchUpState
            {
                Version = OfflineProductionCatchUpState.CurrentVersion,
                OperationId = operationId,
                ReceiptId = ComputeReceiptId(operationId, cappedElapsed, now),
                ProfileId = save.ProfileId,
                VerifiedGenerationFingerprint = authority.VerifiedGenerationFingerprint,
                LastVerifiedTimestamp = lastVerified,
                CatchUpUntilTimestamp = now,
                CappedElapsedSeconds = cappedElapsed,
                CatalogId = profile.CatalogId,
                CatalogSha256 = profile.SourceSha256,
                SourceRevision = profile.SourceRevision,
                Deltas = deltas
            };

            ProfileAuthorityExpectation expectation = ProfileAuthorityExpectation.From(authority);
            ProfileBoundSaveCandidateCommitResult bound = boundStore.TryCommitCandidate(
                expectation,
                operationId,
                receipt.ReceiptId,
                candidate => PrepareCandidate(candidate, save.ProfileId, receipt));
            SaveCandidateCommitResult commit = bound?.CommitResult;
            if (commit == null)
            {
                return new OfflineKingdomProductionCatchUpResult(
                    OfflineKingdomProductionCatchUpStatus.CommitUncertain,
                    EconomyDiagnosticCodes.ProductionDependency,
                    null,
                    false);
            }

            if (commit.Outcome == SaveCandidateCommitOutcome.Duplicate &&
                IsMatchingReceipt(
                    commit.PublishedSave?.OfflineProductionCatchUp,
                    operationId,
                    save.ProfileId))
            {
                return new OfflineKingdomProductionCatchUpResult(
                    OfflineKingdomProductionCatchUpStatus.Replayed,
                    string.Empty,
                    CloneReceipt(commit.PublishedSave.OfflineProductionCatchUp),
                    false);
            }

            if (commit.Outcome == SaveCandidateCommitOutcome.CommitUncertain)
            {
                return new OfflineKingdomProductionCatchUpResult(
                    OfflineKingdomProductionCatchUpStatus.CommitUncertain,
                    EconomyDiagnosticCodes.ProductionProfile,
                    null,
                    false);
            }

            if (commit.Outcome != SaveCandidateCommitOutcome.Committed ||
                commit.PublishedSave == null ||
                !IsMatchingReceipt(
                    commit.PublishedSave.OfflineProductionCatchUp,
                    operationId,
                    save.ProfileId))
            {
                return NotApplied(
                    string.IsNullOrWhiteSpace(commit.Message)
                        ? EconomyDiagnosticCodes.ProfileReadOnly
                        : commit.Message);
            }

            if (saveGameService is LocalSaveGameService localSave)
            {
                localSave.PublishOfflineProgressApplied();
            }

            var applied = new OfflineKingdomProductionCatchUpResult(
                OfflineKingdomProductionCatchUpStatus.Applied,
                string.Empty,
                CloneReceipt(commit.PublishedSave.OfflineProductionCatchUp),
                true);
            Action<OfflineKingdomProductionCatchUpResult> listeners = Committed;
            if (listeners != null)
            {
                listeners(applied);
            }

            return applied;
        }

        private static SaveCandidateMutationPreparation PrepareCandidate(
            SaveGameData candidate,
            string expectedProfileId,
            OfflineProductionCatchUpState receipt)
        {
            if (candidate == null ||
                !string.Equals(
                    candidate.ProfileId ?? string.Empty,
                    expectedProfileId,
                    StringComparison.Ordinal))
            {
                return SaveCandidateMutationPreparation.Rejected(
                    "AL-SAVE-PROFILE-ID-MUTATION-REJECTED");
            }

            if (IsMatchingReceipt(candidate.OfflineProductionCatchUp, receipt.OperationId, expectedProfileId))
            {
                return SaveCandidateMutationPreparation.Duplicate();
            }

            if (!TryApplyDeltas(candidate, receipt.Deltas))
            {
                return SaveCandidateMutationPreparation.Rejected(
                    EconomyDiagnosticCodes.Overflow);
            }

            candidate.LastSavedTimestamp = receipt.CatchUpUntilTimestamp;
            candidate.OfflineProductionCatchUp = CloneReceipt(receipt);
            return SaveCandidateMutationPreparation.Prepared();
        }

        private static bool TryPlanDeltas(
            SaveGameData save,
            IReadOnlyList<EconomyProductionContribution> contributions,
            out List<OfflineProductionDeltaRecord> deltas,
            out string diagnosticCode)
        {
            deltas = new List<OfflineProductionDeltaRecord>();
            diagnosticCode = string.Empty;
            if (contributions == null)
            {
                diagnosticCode = EconomyDiagnosticCodes.ProductionDependency;
                return false;
            }

            var merged = new Dictionary<ResourceType, double>();
            for (int index = 0; index < contributions.Count; index++)
            {
                EconomyProductionContribution contribution = contributions[index];
                if (!ResourceRules.TryGetWalletIndex(contribution.ResourceType, out _) ||
                    double.IsNaN(contribution.Amount) ||
                    double.IsInfinity(contribution.Amount) ||
                    contribution.Amount < 0d)
                {
                    diagnosticCode = EconomyDiagnosticCodes.ProductionInvalidContribution;
                    return false;
                }

                if (!merged.TryGetValue(contribution.ResourceType, out double current))
                {
                    current = 0d;
                }

                double next = current + contribution.Amount;
                if (double.IsNaN(next) || double.IsInfinity(next) || next < 0d)
                {
                    diagnosticCode = EconomyDiagnosticCodes.Overflow;
                    return false;
                }

                merged[contribution.ResourceType] = next;
            }

            foreach (KeyValuePair<ResourceType, double> pair in merged)
            {
                double wholeAsDouble = Math.Floor(pair.Value);
                if (wholeAsDouble <= 0d)
                {
                    continue;
                }

                if (wholeAsDouble >= 9223372036854775808d)
                {
                    diagnosticCode = EconomyDiagnosticCodes.Overflow;
                    return false;
                }

                long whole = (long)wholeAsDouble;
                long currentAmount = ReadAmount(save, pair.Key);
                try
                {
                    checked
                    {
                        _ = currentAmount + whole;
                    }
                }
                catch (OverflowException)
                {
                    diagnosticCode = EconomyDiagnosticCodes.Overflow;
                    return false;
                }

                deltas.Add(new OfflineProductionDeltaRecord
                {
                    ResourceType = pair.Key,
                    Amount = whole
                });
            }

            return true;
        }

        private static bool TryApplyDeltas(
            SaveGameData save,
            List<OfflineProductionDeltaRecord> deltas)
        {
            if (save?.Resources == null || deltas == null)
            {
                return false;
            }

            for (int index = 0; index < deltas.Count; index++)
            {
                OfflineProductionDeltaRecord delta = deltas[index];
                if (delta == null || delta.Amount <= 0L)
                {
                    return false;
                }

                ResourceData entry = FindResource(save, delta.ResourceType);
                if (entry == null)
                {
                    return false;
                }

                try
                {
                    entry.Amount = checked(entry.Amount + delta.Amount);
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            return true;
        }

        private static long ReadAmount(SaveGameData save, ResourceType type)
        {
            ResourceData entry = FindResource(save, type);
            return entry == null ? 0L : entry.Amount;
        }

        private static ResourceData FindResource(SaveGameData save, ResourceType type)
        {
            IList<ResourceData> resources = save?.Resources;
            if (resources == null)
            {
                return null;
            }

            for (int index = 0; index < resources.Count; index++)
            {
                ResourceData entry = resources[index];
                if (entry != null && entry.Type == type)
                {
                    return entry;
                }
            }

            return null;
        }

        private static bool IsMatchingReceipt(
            OfflineProductionCatchUpState receipt,
            string operationId,
            string profileId)
        {
            return receipt != null &&
                   receipt.Version == OfflineProductionCatchUpState.CurrentVersion &&
                   string.Equals(receipt.OperationId, operationId, StringComparison.Ordinal) &&
                   string.Equals(receipt.ProfileId, profileId, StringComparison.Ordinal);
        }

        private static OfflineProductionCatchUpState CloneReceipt(
            OfflineProductionCatchUpState receipt)
        {
            if (receipt == null)
            {
                return null;
            }

            var deltas = new List<OfflineProductionDeltaRecord>(
                receipt.Deltas == null ? 0 : receipt.Deltas.Count);
            if (receipt.Deltas != null)
            {
                for (int index = 0; index < receipt.Deltas.Count; index++)
                {
                    OfflineProductionDeltaRecord delta = receipt.Deltas[index];
                    if (delta == null)
                    {
                        continue;
                    }

                    deltas.Add(new OfflineProductionDeltaRecord
                    {
                        ResourceType = delta.ResourceType,
                        Amount = delta.Amount
                    });
                }
            }

            return new OfflineProductionCatchUpState
            {
                Version = receipt.Version,
                OperationId = receipt.OperationId,
                ReceiptId = receipt.ReceiptId,
                ProfileId = receipt.ProfileId,
                VerifiedGenerationFingerprint = receipt.VerifiedGenerationFingerprint,
                LastVerifiedTimestamp = receipt.LastVerifiedTimestamp,
                CatchUpUntilTimestamp = receipt.CatchUpUntilTimestamp,
                CappedElapsedSeconds = receipt.CappedElapsedSeconds,
                CatalogId = receipt.CatalogId,
                CatalogSha256 = receipt.CatalogSha256,
                SourceRevision = receipt.SourceRevision,
                Deltas = deltas
            };
        }

        private static string ComputeOperationId(
            string profileId,
            long lastVerifiedTimestamp,
            string catalogSha256)
        {
            string material = profileId + "|" +
                lastVerifiedTimestamp.ToString(CultureInfo.InvariantCulture) + "|" +
                catalogSha256;
            return OperationPrefix + "." + Sha256Hex(material);
        }

        private static string ComputeReceiptId(
            string operationId,
            long cappedElapsed,
            long untilTimestamp)
        {
            string material = operationId + "|" +
                cappedElapsed.ToString(CultureInfo.InvariantCulture) + "|" +
                untilTimestamp.ToString(CultureInfo.InvariantCulture);
            return "rcpt." + Sha256Hex(material);
        }

        private static string Sha256Hex(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static KingdomProductionProfileSnapshot TryLoadLiveLedgerSnapshot()
        {
            try
            {
                string path = Path.GetFullPath(
                    Path.Combine(
                        Application.dataPath,
                        "..",
                        KingdomProductionProfileCatalog.LiveLedgerRelativePath));
                if (!File.Exists(path))
                {
                    return null;
                }

                KingdomProductionProfileLoadResult result =
                    KingdomProductionProfileCatalog.TryBindAuthorityLedger(
                        File.ReadAllBytes(path));
                return result.IsReady ? result.Snapshot : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsUncertainOrDegraded(ISaveGameService saveGameService)
        {
            try
            {
                if (saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain ||
                    saveGameService.LastSaveStatus == SaveOperationStatus.SaveFailedPreviousPreserved)
                {
                    return true;
                }

                SaveLoadStatus loadStatus = saveGameService.LastLoadStatus;
                return loadStatus == SaveLoadStatus.LoadedPrimaryDegraded ||
                       loadStatus == SaveLoadStatus.LoadedForwardSchemaReadOnly ||
                       loadStatus == SaveLoadStatus.RecoveryRequired ||
                       loadStatus == SaveLoadStatus.RecoveryFailed;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static OfflineKingdomProductionCatchUpResult NotApplied(string code)
        {
            return new OfflineKingdomProductionCatchUpResult(
                OfflineKingdomProductionCatchUpStatus.NotApplied,
                code,
                null,
                false);
        }
    }
}
