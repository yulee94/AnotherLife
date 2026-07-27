using System;
using System.Collections.Generic;
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
            request ??= new BossLootRequest();

            var identity = new BossLootApplicationIdentity
            {
                EncounterId = request.EncounterId,
                RewardResultId = request.RewardResultId,
                BossId = request.BossId
            };

            BossLootApplicationValidationResult identityValidation =
                BossLootApplicationIdentityValidator.Validate(identity);

            string bossId = request.BossId ?? string.Empty;
            string bossName = string.IsNullOrWhiteSpace(request.BossName) ? bossId : request.BossName;

            var result = new BossLootResult
            {
                BossId = bossId,
                BossName = bossName,
                EncounterId = request.EncounterId ?? string.Empty,
                RewardResultId = request.RewardResultId ?? string.Empty,
                DiagnosticCode = identityValidation.DiagnosticCode
            };

            if (!identityValidation.IsValid)
            {
                result.CommitStatus = BossLootCommitStatus.RejectedInvalidIdentity;
                return result;
            }

            if (!TryPrepareResult(request, bossId, bossName, result))
            {
                return result;
            }

            result.RewardDigest = ComputeRewardDigest(result);

            SaveGameData save = _saveGameService.CurrentSave;
            if (save == null)
            {
                result.CommitStatus = BossLootCommitStatus.SaveFailedRolledBack;
                result.DiagnosticCode = "AL-BOSS-LOOT-NO-CURRENT-SAVE";
                return result;
            }

            save.AppliedBossLootRewards ??= new List<AppliedBossLootRewardState>();
            if (TryFindAppliedReward(save, result, out AppliedBossLootRewardState applied, out string conflictDiagnostic))
            {
                if (IsExactReplay(applied, result))
                {
                    result.CommitStatus = BossLootCommitStatus.Duplicate;
                    return result;
                }

                result.CommitStatus = BossLootCommitStatus.RejectedInvalidIdentity;
                result.DiagnosticCode = conflictDiagnostic;
                return result;
            }

            int previousCredits = save.WarzoneCredits;
            List<OwnedEquipmentState> previousEquipment = CloneOwnedEquipmentList(save.OwnedEquipment);
            List<AppliedBossLootRewardState> previousLedger = CloneAppliedRewardList(save.AppliedBossLootRewards);

            if (!TryApplyPreparedResult(save, result))
            {
                RestoreSaveState(save, previousCredits, previousEquipment, previousLedger);
                return result;
            }

            try
            {
                _saveGameService.Save();
            }
            catch (Exception)
            {
                RestoreSaveState(save, previousCredits, previousEquipment, previousLedger);
                result.CommitStatus = BossLootCommitStatus.SaveFailedRolledBack;
                result.DiagnosticCode = "AL-BOSS-LOOT-SAVE-THREW";
                return result;
            }

            if (_saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary)
            {
                RestoreSaveState(save, previousCredits, previousEquipment, previousLedger);
                result.CommitStatus = BossLootCommitStatus.SaveFailedRolledBack;
                result.DiagnosticCode = "AL-BOSS-LOOT-SAVE-FAILED";
                return result;
            }

            result.CommitStatus = result.WarzoneCreditsAwarded == 0 && result.Drops.Count == 0
                ? BossLootCommitStatus.NoReward
                : BossLootCommitStatus.Committed;
            TryNotifyResult(request, result);
            return result;
        }

        private static bool TryPrepareResult(
            BossLootRequest request,
            string bossId,
            string bossName,
            BossLootResult result)
        {
            if (request.WarzoneCreditReward < 0)
            {
                result.CommitStatus = BossLootCommitStatus.RejectedInvalidDefinition;
                result.DiagnosticCode = "AL-BOSS-LOOT-CREDITS-INVALID";
                return false;
            }

            if (!TryRollDrops(request, bossId, out List<BossLootDrop> drops, out string diagnosticCode))
            {
                result.CommitStatus = BossLootCommitStatus.RejectedInvalidDefinition;
                result.DiagnosticCode = diagnosticCode;
                return false;
            }

            result.BossId = bossId;
            result.BossName = bossName;
            result.WarzoneCreditsAwarded = request.WarzoneCreditReward;
            result.Drops.AddRange(drops);
            return true;
        }

        private static bool TryRollDrops(
            BossLootRequest request,
            string bossId,
            out List<BossLootDrop> drops,
            out string diagnosticCode)
        {
            drops = new List<BossLootDrop>();
            diagnosticCode = string.Empty;
            var lootTable = request.LootTable ?? new List<EquipmentDefinition>();

            if (lootTable.Count == 0)
            {
                return true;
            }

            var rng = new System.Random(ResolveSeed(request, bossId));
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in lootTable)
            {
                if (item == null)
                {
                    diagnosticCode = "AL-BOSS-LOOT-TABLE-NULL-ITEM";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    diagnosticCode = "AL-BOSS-LOOT-ITEM-ID-INVALID";
                    return false;
                }

                if (!itemIds.Add(item.Id))
                {
                    diagnosticCode = "AL-BOSS-LOOT-ITEM-ID-DUPLICATE";
                    return false;
                }

                if (item.DropRate < 0f || item.DropRate > 1f)
                {
                    diagnosticCode = "AL-BOSS-LOOT-DROP-RATE-INVALID";
                    return false;
                }

                if (item.DropRate <= 0f || rng.NextDouble() > item.DropRate)
                {
                    continue;
                }

                drops.Add(CreateDrop(item));
            }

            return true;
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
                seed = seed * 31 + DeterministicHash(bossId);
                seed = seed * 31 + DeterministicHash(request.EncounterId);
                seed = seed * 31 + DeterministicHash(request.RewardResultId);
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

        private bool TryApplyPreparedResult(SaveGameData save, BossLootResult result)
        {
            if (result.WarzoneCreditsAwarded > 0)
            {
                if (_warzoneCreditService is IWarzoneCreditIntegrityService integrityService)
                {
                    EconomyMutationResult creditResult =
                        integrityService.TryAddCredits(result.WarzoneCreditsAwarded);
                    if (creditResult.Status != EconomyMutationStatus.Applied)
                    {
                        result.CommitStatus = BossLootCommitStatus.RejectedEconomy;
                        result.DiagnosticCode = string.IsNullOrWhiteSpace(creditResult.DiagnosticCode)
                            ? "AL-BOSS-LOOT-CREDITS-REJECTED"
                            : creditResult.DiagnosticCode;
                        return false;
                    }
                }
                else
                {
                    try
                    {
                        save.WarzoneCredits = checked(save.WarzoneCredits + result.WarzoneCreditsAwarded);
                    }
                    catch (OverflowException)
                    {
                        result.CommitStatus = BossLootCommitStatus.RejectedEconomy;
                        result.DiagnosticCode = "AL-BOSS-LOOT-CREDITS-OVERFLOW";
                        return false;
                    }
                }
            }

            foreach (var drop in result.Drops)
            {
                if (!TryAddOwnedEquipment(drop, result.BossId))
                {
                    result.CommitStatus = BossLootCommitStatus.RejectedMalformedInventory;
                    result.DiagnosticCode = "AL-BOSS-LOOT-EQUIPMENT-REJECTED";
                    return false;
                }
            }

            save.AppliedBossLootRewards.Add(new AppliedBossLootRewardState
            {
                EncounterId = result.EncounterId,
                RewardResultId = result.RewardResultId,
                BossId = result.BossId,
                RewardDigest = result.RewardDigest,
                CommittedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
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
                matching.AnnounceWorldDrop |= drop.AnnounceWorldDrop;
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

        private void TryNotifyResult(BossLootRequest request, BossLootResult result)
        {
            if (_notificationService == null)
            {
                return;
            }

            if (result.CommitStatus != BossLootCommitStatus.Committed &&
                result.CommitStatus != BossLootCommitStatus.NoReward)
            {
                return;
            }

            try
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
            catch (Exception ex)
            {
                Debug.LogWarning($"AL-BOSS-LOOT-NOTIFICATION-FAILED: Reward remained committed but notification failed: {ex.Message}");
            }
        }

        private static bool TryFindAppliedReward(
            SaveGameData save,
            BossLootResult result,
            out AppliedBossLootRewardState applied,
            out string conflictDiagnostic)
        {
            applied = null;
            conflictDiagnostic = string.Empty;
            if (save.AppliedBossLootRewards == null)
            {
                return false;
            }

            foreach (AppliedBossLootRewardState reward in save.AppliedBossLootRewards)
            {
                if (reward == null)
                {
                    continue;
                }

                if (string.Equals(reward.RewardResultId, result.RewardResultId, StringComparison.Ordinal))
                {
                    applied = reward;
                    conflictDiagnostic = "AL-BOSS-LOOT-RESULT-ID-CONFLICT";
                    return true;
                }

                if (string.Equals(reward.EncounterId, result.EncounterId, StringComparison.Ordinal))
                {
                    applied = reward;
                    conflictDiagnostic = "AL-BOSS-LOOT-ENCOUNTER-ID-CONFLICT";
                    return true;
                }
            }

            return false;
        }

        private static bool IsExactReplay(
            AppliedBossLootRewardState applied,
            BossLootResult result) =>
            string.Equals(applied.EncounterId, result.EncounterId, StringComparison.Ordinal) &&
            string.Equals(applied.BossId, result.BossId, StringComparison.Ordinal) &&
            string.Equals(applied.RewardDigest, result.RewardDigest, StringComparison.Ordinal);

        private static string ComputeRewardDigest(BossLootResult result)
        {
            var builder = new StringBuilder();
            builder.Append(result.BossId).Append('|')
                .Append(result.EncounterId).Append('|')
                .Append(result.RewardResultId).Append('|')
                .Append(result.WarzoneCreditsAwarded);

            foreach (BossLootDrop drop in result.Drops.OrderBy(drop => drop.EquipmentId, StringComparer.Ordinal))
            {
                builder.Append('|')
                    .Append(drop.EquipmentId).Append(':')
                    .Append(drop.Quantity).Append(':')
                    .Append((int)drop.Slot).Append(':')
                    .Append(drop.AttackBonus).Append(':')
                    .Append(drop.DefenseBonus).Append(':')
                    .Append(drop.HealthBonus).Append(':')
                    .Append(drop.AnnounceWorldDrop ? "1" : "0");
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static int DeterministicHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                int hash = 23;
                foreach (char c in value)
                {
                    hash = hash * 31 + c;
                }

                return hash;
            }
        }

        private static List<OwnedEquipmentState> CloneOwnedEquipmentList(
            List<OwnedEquipmentState> source) =>
            source?.Select(item => item == null ? null : CloneOwnedEquipment(item)).ToList();

        private static List<AppliedBossLootRewardState> CloneAppliedRewardList(
            List<AppliedBossLootRewardState> source) =>
            source?.Select(CloneAppliedReward).ToList();

        private static AppliedBossLootRewardState CloneAppliedReward(
            AppliedBossLootRewardState source) =>
            source == null
                ? null
                : new AppliedBossLootRewardState
                {
                    EncounterId = source.EncounterId,
                    RewardResultId = source.RewardResultId,
                    BossId = source.BossId,
                    RewardDigest = source.RewardDigest,
                    CommittedTimestamp = source.CommittedTimestamp
                };

        private static void RestoreSaveState(
            SaveGameData save,
            int previousCredits,
            List<OwnedEquipmentState> previousEquipment,
            List<AppliedBossLootRewardState> previousLedger)
        {
            save.WarzoneCredits = previousCredits;
            save.OwnedEquipment = previousEquipment;
            save.AppliedBossLootRewards = previousLedger;
        }
    }
}
