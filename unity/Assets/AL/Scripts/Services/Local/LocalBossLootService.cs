using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalBossLootService : IBossLootService
    {
        private const string FallbackBossId = "boss_dummy";
        private const string FallbackBossName = "Boss Dummy";
        private const string FallbackItemId = "ember_crown_shard";
        private const string FallbackItemName = "Ember Crown Shard";

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

            string bossId = string.IsNullOrWhiteSpace(request.BossId) ? FallbackBossId : request.BossId;
            string bossName = string.IsNullOrWhiteSpace(request.BossName) ? FallbackBossName : request.BossName;
            int creditReward = Mathf.Max(0, request.WarzoneCreditReward);

            var result = new BossLootResult
            {
                BossId = bossId,
                BossName = bossName,
                WarzoneCreditsAwarded = creditReward
            };

            if (creditReward > 0)
            {
                _warzoneCreditService.AddCredits(creditReward);
            }

            List<BossLootDrop> drops = RollDrops(request, bossId);
            foreach (var drop in drops)
            {
                if (TryAddOwnedEquipment(drop, bossId))
                {
                    result.Drops.Add(drop);
                }
            }

            if (result.Drops.Count > 0)
            {
                _saveGameService.Save();
            }

            NotifyResult(request, result);
            return result;
        }

        private static List<BossLootDrop> RollDrops(BossLootRequest request, string bossId)
        {
            var drops = new List<BossLootDrop>();
            var lootTable = request.LootTable ?? new List<EquipmentDefinition>();

            if (lootTable.Count == 0)
            {
                drops.Add(CreateFallbackDrop());
                return drops;
            }

            var rng = new System.Random(ResolveSeed(request, bossId));
            foreach (var item in lootTable)
            {
                if (item == null)
                {
                    continue;
                }

                float dropRate = Mathf.Clamp01(item.DropRate);
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
                EquipmentId = string.IsNullOrWhiteSpace(item.Id) ? item.name : item.Id,
                DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? item.name : item.DisplayName,
                Slot = item.Slot,
                AttackBonus = item.AttackBonus,
                DefenseBonus = item.DefenseBonus,
                HealthBonus = item.HealthBonus,
                AnnounceWorldDrop = item.AnnounceWorldDrop,
                Quantity = 1
            };
        }

        private static BossLootDrop CreateFallbackDrop()
        {
            return new BossLootDrop
            {
                EquipmentId = FallbackItemId,
                DisplayName = FallbackItemName,
                Slot = EquipmentSlot.Trinket,
                AnnounceWorldDrop = true,
                Quantity = 1
            };
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
