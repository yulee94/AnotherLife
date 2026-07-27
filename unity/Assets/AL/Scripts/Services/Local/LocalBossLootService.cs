using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalBossLootService : IBossLootService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IWarzoneCreditService _warzoneCreditService;
        private readonly INotificationService _notificationService;

        public LocalBossLootService(
            ISaveGameService saveGameService,
            IWarzoneCreditService warzoneCreditService,
            INotificationService notificationService)
        {
            _saveGameService = saveGameService;
            _warzoneCreditService = warzoneCreditService;
            _notificationService = notificationService;
        }

        public IEnumerable<OwnedEquipmentState> GetOwnedEquipment()
        {
            var ownedEquipment = _saveGameService.CurrentSave?.OwnedEquipment;
            if (ownedEquipment == null || ownedEquipment.Count == 0)
            {
                return Array.Empty<OwnedEquipmentState>();
            }

            var snapshot = new List<OwnedEquipmentState>(ownedEquipment.Count);
            foreach (OwnedEquipmentState owned in ownedEquipment)
            {
                if (owned != null)
                {
                    snapshot.Add(CloneOwnedEquipment(owned));
                }
            }

            return snapshot.AsReadOnly();
        }

        public BossLootResult RollLoot(BossLootRequest request)
        {
            BossLootApplicationValidationResult identityValidation =
                BossLootApplicationIdentityValidator.Validate(request == null
                    ? null
                    : new BossLootApplicationIdentity
                    {
                        EncounterId = request.EncounterId,
                        RewardResultId = request.RewardResultId,
                        BossId = request.BossId
                    });
            if (!identityValidation.IsValid)
            {
                return Reject(
                    request,
                    BossLootApplicationStatus.RejectedInvalidRequest,
                    identityValidation.DiagnosticCode);
            }

            if (!TryValidateDefinition(request, out string validationCode))
            {
                return Reject(
                    request,
                    BossLootApplicationStatus.RejectedInvalidDefinition,
                    validationCode);
            }

            var result = new BossLootResult
            {
                EncounterId = request.EncounterId,
                RewardResultId = request.RewardResultId,
                BossId = request.BossId,
                BossName = request.BossName,
                WarzoneCreditsAwarded = request.WarzoneCreditReward,
                Drops = RollDrops(request, request.BossId)
            };
            string digest = ComputeRewardDigest(result);
            SaveGameData save = _saveGameService?.CurrentSave;
            if (save == null)
            {
                return Reject(request, BossLootApplicationStatus.RejectedNoCurrentSave, "AL-BOSS-LOOT-NO-CURRENT-SAVE");
            }

            save.OwnedEquipment ??= new List<OwnedEquipmentState>();
            save.AppliedBossLootRewards ??= new List<AppliedBossLootRewardState>();
            foreach (AppliedBossLootRewardState applied in save.AppliedBossLootRewards)
            {
                if (applied == null)
                {
                    return Reject(request, BossLootApplicationStatus.RejectedMalformedState, "AL-BOSS-LOOT-LEDGER-MALFORMED");
                }

                bool sameEncounter = string.Equals(applied.EncounterId, request.EncounterId, StringComparison.Ordinal);
                bool sameResult = string.Equals(applied.RewardResultId, request.RewardResultId, StringComparison.Ordinal);
                if (!sameEncounter && !sameResult)
                {
                    continue;
                }

                if (sameEncounter &&
                    sameResult &&
                    string.Equals(applied.BossId, request.BossId, StringComparison.Ordinal) &&
                    string.Equals(applied.RewardDigest, digest, StringComparison.Ordinal))
                {
                    result.ApplicationStatus = BossLootApplicationStatus.AlreadyCommitted;
                    result.DiagnosticCode = "AL-BOSS-LOOT-ALREADY-COMMITTED";
                    return result;
                }

                return Reject(request, BossLootApplicationStatus.RejectedMalformedState, "AL-BOSS-LOOT-IDENTITY-CONFLICT");
            }

            int previousCredits = save.WarzoneCredits;
            List<OwnedEquipmentState> previousEquipment = CloneOwnedEquipment(save.OwnedEquipment);
            List<AppliedBossLootRewardState> previousLedger = CloneLedger(save.AppliedBossLootRewards);
            try
            {
                if (request.WarzoneCreditReward > 0)
                {
                    if (!(_warzoneCreditService is IWarzoneCreditIntegrityService integrityService))
                    {
                        return Reject(request, BossLootApplicationStatus.RejectedCreditMutation, "AL-BOSS-LOOT-CREDIT-CONTRACT-UNAVAILABLE");
                    }

                    EconomyMutationResult creditMutation =
                        integrityService.TryAddCredits(request.WarzoneCreditReward);
                    if (!creditMutation.Changed)
                    {
                        return Reject(
                            request,
                            BossLootApplicationStatus.RejectedCreditMutation,
                            string.IsNullOrWhiteSpace(creditMutation.DiagnosticCode)
                                ? "AL-BOSS-LOOT-CREDIT-MUTATION-REJECTED"
                                : creditMutation.DiagnosticCode);
                    }
                }

                foreach (BossLootDrop drop in result.Drops)
                {
                    if (!TryAddOwnedEquipment(drop, request.BossId))
                    {
                        Restore(save, previousCredits, previousEquipment, previousLedger);
                        return Reject(request, BossLootApplicationStatus.RejectedMalformedState, "AL-BOSS-LOOT-EQUIPMENT-MUTATION-REJECTED");
                    }
                }

                save.AppliedBossLootRewards.Add(new AppliedBossLootRewardState
                {
                    EncounterId = request.EncounterId,
                    RewardResultId = request.RewardResultId,
                    BossId = request.BossId,
                    RewardDigest = digest,
                    CommittedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
                _saveGameService.Save();
                if (_saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain)
                {
                    return Reject(request, BossLootApplicationStatus.CommitUncertain, "AL-BOSS-LOOT-COMMIT-UNCERTAIN");
                }

                if (_saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary)
                {
                    Restore(save, previousCredits, previousEquipment, previousLedger);
                    return Reject(request, BossLootApplicationStatus.SaveFailedRolledBack, "AL-BOSS-LOOT-SAVE-FAILED");
                }
            }
            catch (Exception)
            {
                return Reject(request, BossLootApplicationStatus.CommitUncertain, "AL-BOSS-LOOT-SAVE-EXCEPTION");
            }

            result.ApplicationStatus = BossLootApplicationStatus.Committed;
            result.DiagnosticCode = string.Empty;
            NotifyResultSafely(request, result);
            return result;
        }

        private static List<BossLootDrop> RollDrops(BossLootRequest request, string bossId)
        {
            var drops = new List<BossLootDrop>();
            List<EquipmentDefinition> lootTable = request.LootTable;

            var rng = new System.Random(ResolveSeed(request, bossId));
            foreach (var item in lootTable)
            {
                if (item == null)
                {
                    continue;
                }

                float dropRate = item.DropRate;
                if (dropRate <= 0f || rng.NextDouble() > dropRate)
                {
                    continue;
                }

                drops.Add(CreateDrop(item));
            }

            return drops;
        }

        private static int ResolveSeed(BossLootRequest request, string bossId)
        {
            if (request.RandomSeed != 0)
            {
                return request.RandomSeed;
            }

            unchecked
            {
                int seed = 17;
                seed = seed * 31 + bossId.GetHashCode();
                seed = seed * 31 + Environment.TickCount;
                seed = seed * 31 + DateTimeOffset.UtcNow.Millisecond;
                return seed;
            }
        }

        private static BossLootDrop CreateDrop(EquipmentDefinition item)
        {
            return new BossLootDrop
            {
                EquipmentId = item.Id,
                DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? item.name : item.DisplayName,
                Slot = item.Slot,
                AttackBonus = item.AttackBonus,
                DefenseBonus = item.DefenseBonus,
                HealthBonus = item.HealthBonus,
                AnnounceWorldDrop = item.AnnounceWorldDrop,
                Quantity = 1
            };
        }

        private static bool TryValidateDefinition(BossLootRequest request, out string diagnosticCode)
        {
            if (request.WarzoneCreditReward < 0 || request.LootTable == null)
            {
                diagnosticCode = "AL-BOSS-LOOT-DEFINITION-INVALID";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (EquipmentDefinition item in request.LootTable)
            {
                if (item == null ||
                    string.IsNullOrWhiteSpace(item.Id) ||
                    float.IsNaN(item.DropRate) ||
                    float.IsInfinity(item.DropRate) ||
                    item.DropRate < 0f ||
                    item.DropRate > 1f ||
                    !ids.Add(item.Id))
                {
                    diagnosticCode = "AL-BOSS-LOOT-DEFINITION-INVALID";
                    return false;
                }
            }

            diagnosticCode = string.Empty;
            return true;
        }

        private bool TryAddOwnedEquipment(BossLootDrop drop, string bossId)
        {
            var save = _saveGameService.CurrentSave;
            if (save == null ||
                drop == null ||
                string.IsNullOrWhiteSpace(drop.EquipmentId) ||
                drop.Quantity <= 0)
            {
                return false;
            }

            save.OwnedEquipment ??= new List<OwnedEquipmentState>();

            var ids = new HashSet<string>(StringComparer.Ordinal);
            OwnedEquipmentState matching = null;
            foreach (OwnedEquipmentState owned in save.OwnedEquipment)
            {
                if (owned == null ||
                    string.IsNullOrWhiteSpace(owned.EquipmentId) ||
                    owned.Quantity <= 0 ||
                    owned.FirstAcquiredTimestamp <= 0 ||
                    owned.LastAcquiredTimestamp < owned.FirstAcquiredTimestamp ||
                    !ids.Add(owned.EquipmentId))
                {
                    Debug.LogError("AL-EQUIPMENT-INVENTORY-MALFORMED: Owned equipment mutation was rejected without changing persisted state.");
                    return false;
                }

                if (string.Equals(owned.EquipmentId, drop.EquipmentId, StringComparison.Ordinal))
                {
                    matching = owned;
                }
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (matching != null)
            {
                if (!MatchesPersistedDefinition(matching, drop))
                {
                    Debug.LogError("AL-EQUIPMENT-DEFINITION-DRIFT: Owned equipment mutation was rejected because the persisted definition does not match the awarded definition.");
                    return false;
                }

                try
                {
                    matching.Quantity = checked(matching.Quantity + drop.Quantity);
                }
                catch (OverflowException)
                {
                    Debug.LogError("AL-EQUIPMENT-QUANTITY-OVERFLOW: Owned equipment mutation was rejected without changing persisted state.");
                    return false;
                }

                matching.LastAcquiredTimestamp = now;
                matching.SourceBossId = bossId;
                return true;
            }

            save.OwnedEquipment.Add(new OwnedEquipmentState
            {
                EquipmentId = drop.EquipmentId,
                DisplayName = drop.DisplayName,
                Slot = drop.Slot,
                AttackBonus = drop.AttackBonus,
                DefenseBonus = drop.DefenseBonus,
                HealthBonus = drop.HealthBonus,
                Quantity = drop.Quantity,
                SourceBossId = bossId,
                AnnounceWorldDrop = drop.AnnounceWorldDrop,
                FirstAcquiredTimestamp = now,
                LastAcquiredTimestamp = now
            });
            return true;
        }

        private static bool MatchesPersistedDefinition(
            OwnedEquipmentState owned,
            BossLootDrop drop) =>
            string.Equals(owned.DisplayName, drop.DisplayName, StringComparison.Ordinal) &&
            owned.Slot == drop.Slot &&
            owned.AttackBonus == drop.AttackBonus &&
            owned.DefenseBonus == drop.DefenseBonus &&
            owned.HealthBonus == drop.HealthBonus &&
            owned.AnnounceWorldDrop == drop.AnnounceWorldDrop;

        private static OwnedEquipmentState CloneOwnedEquipment(OwnedEquipmentState source)
        {
            return new OwnedEquipmentState
            {
                EquipmentId = source.EquipmentId,
                DisplayName = source.DisplayName,
                Slot = source.Slot,
                AttackBonus = source.AttackBonus,
                DefenseBonus = source.DefenseBonus,
                HealthBonus = source.HealthBonus,
                Quantity = source.Quantity,
                SourceBossId = source.SourceBossId,
                AnnounceWorldDrop = source.AnnounceWorldDrop,
                FirstAcquiredTimestamp = source.FirstAcquiredTimestamp,
                LastAcquiredTimestamp = source.LastAcquiredTimestamp
            };
        }

        private static List<OwnedEquipmentState> CloneOwnedEquipment(
            IEnumerable<OwnedEquipmentState> equipment) =>
            equipment?.Select(CloneOwnedEquipment).ToList() ?? new List<OwnedEquipmentState>();

        private static List<AppliedBossLootRewardState> CloneLedger(
            IEnumerable<AppliedBossLootRewardState> ledger) =>
            ledger?.Select(entry => new AppliedBossLootRewardState
            {
                EncounterId = entry.EncounterId,
                RewardResultId = entry.RewardResultId,
                BossId = entry.BossId,
                RewardDigest = entry.RewardDigest,
                CommittedTimestamp = entry.CommittedTimestamp
            }).ToList() ?? new List<AppliedBossLootRewardState>();

        private static void Restore(
            SaveGameData save,
            int previousCredits,
            List<OwnedEquipmentState> previousEquipment,
            List<AppliedBossLootRewardState> previousLedger)
        {
            save.WarzoneCredits = previousCredits;
            save.OwnedEquipment = previousEquipment;
            save.AppliedBossLootRewards = previousLedger;
        }

        private static BossLootResult Reject(
            BossLootRequest request,
            BossLootApplicationStatus status,
            string diagnosticCode) =>
            new BossLootResult
            {
                ApplicationStatus = status,
                DiagnosticCode = diagnosticCode ?? string.Empty,
                EncounterId = request?.EncounterId,
                RewardResultId = request?.RewardResultId,
                BossId = request?.BossId,
                BossName = request?.BossName
            };

        private static string ComputeRewardDigest(BossLootResult result)
        {
            var canonical = new StringBuilder();
            canonical.Append(result.EncounterId).Append('\n')
                .Append(result.RewardResultId).Append('\n')
                .Append(result.BossId).Append('\n')
                .Append(result.WarzoneCreditsAwarded.ToString(CultureInfo.InvariantCulture));
            foreach (BossLootDrop drop in result.Drops)
            {
                canonical.Append('\n').Append(drop.EquipmentId)
                    .Append('|').Append(drop.Quantity.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(((int)drop.Slot).ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(drop.AttackBonus.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(drop.DefenseBonus.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(drop.HealthBonus.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(drop.AnnounceWorldDrop ? '1' : '0');
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                return "sha256:" + BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private void NotifyResultSafely(BossLootRequest request, BossLootResult result)
        {
            if (_notificationService == null)
            {
                return;
            }

            try
            {
                NotifyResult(request, result);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AL-BOSS-LOOT-NOTIFICATION-FAILED] Durable reward remains committed. {ex.Message}");
            }
        }

        private void NotifyResult(BossLootRequest request, BossLootResult result)
        {
            if (result.WarzoneCreditsAwarded > 0)
            {
                _notificationService.ShowMessage($"Defeated {result.BossName}. +{result.WarzoneCreditsAwarded} Warzone Credits.");
            }

            foreach (var drop in result.Drops)
            {
                if (drop.AnnounceWorldDrop)
                {
                    string playerName = string.IsNullOrWhiteSpace(request.PlayerDisplayName) ? "Anonymous player" : request.PlayerDisplayName;
                    _notificationService.ShowMessage($"{playerName} has acquired {drop.DisplayName} from {result.BossName}.");
                }
                else
                {
                    _notificationService.ShowMessage($"Loot acquired: {drop.DisplayName}.");
                }
            }
        }
    }
}
