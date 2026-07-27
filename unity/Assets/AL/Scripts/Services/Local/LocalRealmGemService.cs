using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalRealmGemService : IRealmGemService
    {
        private readonly ISaveGameService _saveGameService;

        public LocalRealmGemService(ISaveGameService saveGameService)
        {
            _saveGameService = saveGameService;
        }

        public IEnumerable<RealmGemState> GetRealmGems()
        {
            var gems = _saveGameService.CurrentSave?.RealmGems;
            if (gems == null || gems.Count == 0)
            {
                return Enumerable.Empty<RealmGemState>();
            }

            return gems
                .Where(gem => gem != null)
                .Select(CloneGem)
                .ToArray();
        }

        public WishgateState GetWishgateState()
        {
            return CloneWishgate(_saveGameService.CurrentSave?.Wishgate);
        }

        public bool PickUpGem(string gemId, string carrierId)
        {
            var gem = GetGem(gemId);
            if (gem == null || string.IsNullOrWhiteSpace(carrierId))
            {
                return false;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (gem.IsDropped && now - gem.LastDroppedTimestamp < 10)
            {
                return false;
            }

            gem.IsAtHome = false;
            gem.IsDropped = false;
            gem.CarrierId = carrierId;
            _saveGameService.Save();
            Debug.Log($"Realm Gem {gemId} picked up by {carrierId}");
            return true;
        }

        public void DropGem(string gemId)
        {
            var gem = GetGem(gemId);
            if (gem == null)
            {
                return;
            }

            gem.IsAtHome = false;
            gem.IsDropped = true;
            gem.CarrierId = null;
            gem.LastDroppedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _saveGameService.Save();
        }

        public void ReturnGemHome(string gemId)
        {
            var gem = GetGem(gemId);
            if (gem == null)
            {
                return;
            }

            gem.IsAtHome = true;
            gem.IsDropped = false;
            gem.CarrierId = null;
            _saveGameService.Save();
        }

        public void MarkWishgateEarned(string reason)
        {
            EnsureGemState();
            var wishgate = _saveGameService.CurrentSave?.Wishgate;
            if (wishgate == null || string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            wishgate.IsEarned = true;
            wishgate.EarnReason = reason;
            _saveGameService.Save();
        }

        public void ChooseWishReward(string rewardId)
        {
            EnsureGemState();
            var wishgate = _saveGameService.CurrentSave?.Wishgate;
            if (wishgate == null || !wishgate.IsEarned || string.IsNullOrWhiteSpace(rewardId))
            {
                return;
            }

            wishgate.IsEarned = false;
            wishgate.LastRewardId = rewardId;
            wishgate.LastRewardChosenTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _saveGameService.Save();
        }

        private static RealmGemState CloneGem(RealmGemState source)
        {
            return new RealmGemState
            {
                GemId = source.GemId,
                HomeRealm = source.HomeRealm,
                GemIndex = source.GemIndex,
                IsAtHome = source.IsAtHome,
                IsDropped = source.IsDropped,
                CarrierId = source.CarrierId,
                LastDroppedTimestamp = source.LastDroppedTimestamp
            };
        }

        private static WishgateState CloneWishgate(WishgateState source)
        {
            if (source == null)
            {
                return new WishgateState();
            }

            return new WishgateState
            {
                IsEarned = source.IsEarned,
                EarnReason = source.EarnReason,
                LastRewardId = source.LastRewardId,
                LastRewardChosenTimestamp = source.LastRewardChosenTimestamp
            };
        }

        private RealmGemState GetGem(string gemId)
        {
            if (string.IsNullOrWhiteSpace(gemId))
            {
                return null;
            }

            EnsureGemState();
            return _saveGameService.CurrentSave?.RealmGems.FirstOrDefault(g => g != null && g.GemId == gemId);
        }

        private void EnsureGemState()
        {
            if (_saveGameService.CurrentSave == null)
            {
                return;
            }

            _saveGameService.CurrentSave.RealmGems ??= new List<RealmGemState>();
            _saveGameService.CurrentSave.Wishgate ??= new WishgateState();

            if (_saveGameService.CurrentSave.RealmGems.Count > 0)
            {
                return;
            }

            AddRealmGems(RealmId.Stonehold);
            AddRealmGems(RealmId.Eldergrove);
            AddRealmGems(RealmId.Crownlands);
            AddRealmGems(RealmId.Umbral);
        }

        private void AddRealmGems(RealmId realm)
        {
            _saveGameService.CurrentSave.RealmGems.Add(new RealmGemState { GemId = $"{realm}_Gem_1", HomeRealm = realm, GemIndex = 1 });
            _saveGameService.CurrentSave.RealmGems.Add(new RealmGemState { GemId = $"{realm}_Gem_2", HomeRealm = realm, GemIndex = 2 });
        }
    }
}

