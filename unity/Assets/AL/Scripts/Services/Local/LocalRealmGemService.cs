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
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!TryGetMutableGem(gemId, now, out RealmGemState gem) ||
                string.IsNullOrWhiteSpace(carrierId) ||
                IsCarried(gem))
            {
                return false;
            }

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
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!TryGetMutableGem(gemId, now, out RealmGemState gem) ||
                !IsCarried(gem))
            {
                return;
            }

            gem.IsAtHome = false;
            gem.IsDropped = true;
            gem.CarrierId = null;
            gem.LastDroppedTimestamp = now;
            _saveGameService.Save();
        }

        public void ReturnGemHome(string gemId)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!TryGetMutableGem(gemId, now, out RealmGemState gem) ||
                gem.IsAtHome)
            {
                return;
            }

            gem.IsAtHome = true;
            gem.IsDropped = false;
            gem.CarrierId = null;
            gem.LastDroppedTimestamp = 0;
            _saveGameService.Save();
        }

        public void MarkWishgateEarned(string reason)
        {
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

        private bool TryGetMutableGem(
            string gemId,
            long now,
            out RealmGemState gem)
        {
            gem = null;
            if (string.IsNullOrWhiteSpace(gemId))
            {
                return false;
            }

            List<RealmGemState> gems = _saveGameService.CurrentSave?.RealmGems;
            if (gems == null)
            {
                return false;
            }

            RealmGemState[] matches = gems
                .Where(candidate =>
                    candidate != null &&
                    string.Equals(candidate.GemId, gemId, StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            if (matches.Length != 1 || !HasValidCustody(matches[0], now))
            {
                return false;
            }

            gem = matches[0];
            return true;
        }

        private static bool HasValidCustody(RealmGemState gem, long now) =>
            gem != null &&
            gem.HomeRealm != RealmId.None &&
            gem.GemIndex > 0 &&
            gem.LastDroppedTimestamp >= 0 &&
            ((!gem.IsDropped &&
              ((gem.IsAtHome && string.IsNullOrWhiteSpace(gem.CarrierId)) ||
               (!gem.IsAtHome && !string.IsNullOrWhiteSpace(gem.CarrierId)))) ||
             (!gem.IsAtHome &&
              gem.IsDropped &&
              string.IsNullOrWhiteSpace(gem.CarrierId) &&
              gem.LastDroppedTimestamp > 0 &&
              gem.LastDroppedTimestamp <= now));

        private static bool IsCarried(RealmGemState gem) =>
            !gem.IsAtHome &&
            !gem.IsDropped &&
            !string.IsNullOrWhiteSpace(gem.CarrierId);
    }
}

