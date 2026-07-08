using System.Collections.Generic;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalWarmasterService : IWarmasterService
    {
        private const int RequiredTrueWarmasterPieces = 10;
        private const string TrueWarmasterSetId = "prototype_true_warmaster";
        private readonly ISaveGameService _saveGameService;
        private readonly IWarzoneCreditService _warzoneCreditService;

        public LocalWarmasterService(ISaveGameService saveGameService, IWarzoneCreditService warzoneCreditService)
        {
            _saveGameService = saveGameService;
            _warzoneCreditService = warzoneCreditService;
        }

        public WarmasterState GetState()
        {
            var state = _saveGameService.CurrentSave?.Warmaster;
            if (state == null)
            {
                return null;
            }

            state.UnlockedSetIds ??= new List<string>();
            state.PurchasedPieceIds ??= new List<string>();
            return state;
        }

        public void UnlockSet(string setId)
        {
            var state = GetState();
            if (state != null && !state.UnlockedSetIds.Contains(setId))
            {
                state.UnlockedSetIds.Add(setId);
                _saveGameService.Save();
                Debug.Log($"Warmaster Set Unlocked: {setId}");
            }
        }

        public void EquipSet(string setId)
        {
            var state = GetState();
            if (state != null && state.UnlockedSetIds.Contains(setId))
            {
                state.EquippedSetId = setId;
                _saveGameService.Save();
                Debug.Log($"Warmaster Set Equipped: {setId}");
            }
        }

        public bool PurchasePiece(string pieceId, int warzoneCreditCost)
        {
            var state = GetState();
            if (state == null || string.IsNullOrWhiteSpace(pieceId))
            {
                return false;
            }

            state.PurchasedPieceIds ??= new List<string>();
            if (state.PurchasedPieceIds.Contains(pieceId))
            {
                return true;
            }

            if (warzoneCreditCost > 0 && (_warzoneCreditService == null || !_warzoneCreditService.SpendCredits(warzoneCreditCost)))
            {
                return false;
            }

            state.PurchasedPieceIds.Add(pieceId);
            state.Level = Mathf.Max(state.Level, state.PurchasedPieceIds.Count);
            state.Experience += 25;

            if (state.PurchasedPieceIds.Count >= RequiredTrueWarmasterPieces)
            {
                state.IsTrueWarmaster = true;
                if (!state.UnlockedSetIds.Contains(TrueWarmasterSetId))
                {
                    state.UnlockedSetIds.Add(TrueWarmasterSetId);
                }

                state.EquippedSetId = TrueWarmasterSetId;
            }

            _saveGameService.Save();
            Debug.Log($"Warmaster piece purchased: {pieceId}");
            return true;
        }

        public int GetPurchasedPieceCount()
        {
            return GetState()?.PurchasedPieceIds?.Count ?? 0;
        }

        public int GetRequiredPieceCount()
        {
            return RequiredTrueWarmasterPieces;
        }

        public bool IsTrueWarmaster()
        {
            var state = GetState();
            return state != null && (state.IsTrueWarmaster || state.PurchasedPieceIds.Count >= RequiredTrueWarmasterPieces);
        }
    }
}
