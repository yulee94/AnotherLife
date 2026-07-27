using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalWarmasterService : IWarmasterService
    {
        private const int RequiredTrueWarmasterPieces = 10;
        private const string TrueWarmasterSetId = "prototype_true_warmaster";
        private static readonly HashSet<string> KnownSetIds = new HashSet<string>(StringComparer.Ordinal)
        {
            TrueWarmasterSetId
        };

        private static readonly HashSet<string> KnownPieceIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "warmaster_piece_01",
            "warmaster_piece_02",
            "warmaster_piece_03",
            "warmaster_piece_04",
            "warmaster_piece_05",
            "warmaster_piece_06",
            "warmaster_piece_07",
            "warmaster_piece_08",
            "warmaster_piece_09",
            "warmaster_piece_10"
        };

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
            return state == null ? null : CloneState(state);
        }

        public void UnlockSet(string setId)
        {
            var save = _saveGameService.CurrentSave;
            var state = save?.Warmaster;
            if (state == null || !IsKnownSet(setId) || !TryValidateState(state, out _))
            {
                return;
            }

            if (state.UnlockedSetIds.Contains(setId, StringComparer.Ordinal))
            {
                return;
            }

            WarmasterState previous = CloneState(state);
            state.UnlockedSetIds.Add(setId);
            _saveGameService.Save();
            if (_saveGameService.LastSaveStatus == SaveOperationStatus.SavedPrimary)
            {
                Debug.Log($"Warmaster Set Unlocked: {setId}");
                return;
            }

            RestoreState(state, previous);
        }

        public void EquipSet(string setId)
        {
            var save = _saveGameService.CurrentSave;
            var state = save?.Warmaster;
            if (state == null || !IsKnownSet(setId) || !TryValidateState(state, out _))
            {
                return;
            }

            if (!state.UnlockedSetIds.Contains(setId, StringComparer.Ordinal) ||
                string.Equals(state.EquippedSetId, setId, StringComparison.Ordinal))
            {
                return;
            }

            WarmasterState previous = CloneState(state);
            state.EquippedSetId = setId;
            _saveGameService.Save();
            if (_saveGameService.LastSaveStatus == SaveOperationStatus.SavedPrimary)
            {
                Debug.Log($"Warmaster Set Equipped: {setId}");
                return;
            }

            RestoreState(state, previous);
        }

        public bool PurchasePiece(string pieceId, int warzoneCreditCost)
        {
            var save = _saveGameService.CurrentSave;
            var state = save?.Warmaster;
            if (state == null ||
                !IsKnownPiece(pieceId) ||
                warzoneCreditCost <= 0 ||
                !TryValidateState(state, out HashSet<string> validPurchasedPieces) ||
                !(_warzoneCreditService is IWarzoneCreditIntegrityService integrityService))
            {
                return false;
            }

            if (validPurchasedPieces.Contains(pieceId))
            {
                return true;
            }

            int previousCredits = save.WarzoneCredits;
            WarmasterState previousState = CloneState(state);
            EconomyMutationResult spend = integrityService.TrySpendCredits(warzoneCreditCost);
            if (!spend.Changed)
            {
                return false;
            }

            try
            {
                state.PurchasedPieceIds.Add(pieceId);
                validPurchasedPieces.Add(pieceId);
                state.Level = Mathf.Max(state.Level, validPurchasedPieces.Count);
                state.Experience = checked(state.Experience + 25);

                if (validPurchasedPieces.Count >= RequiredTrueWarmasterPieces)
                {
                    state.IsTrueWarmaster = true;
                    if (!state.UnlockedSetIds.Contains(TrueWarmasterSetId, StringComparer.Ordinal))
                    {
                        state.UnlockedSetIds.Add(TrueWarmasterSetId);
                    }

                    state.EquippedSetId = TrueWarmasterSetId;
                }

                _saveGameService.Save();
                if (_saveGameService.LastSaveStatus == SaveOperationStatus.SavedPrimary)
                {
                    Debug.Log($"Warmaster piece purchased: {pieceId}");
                    return true;
                }
            }
            catch (Exception)
            {
            }

            save.WarzoneCredits = previousCredits;
            RestoreState(state, previousState);
            return false;
        }

        public int GetPurchasedPieceCount()
        {
            var state = _saveGameService.CurrentSave?.Warmaster;
            return state != null && TryValidateState(state, out HashSet<string> validPurchasedPieces)
                ? validPurchasedPieces.Count
                : 0;
        }

        public int GetRequiredPieceCount()
        {
            return RequiredTrueWarmasterPieces;
        }

        public bool IsTrueWarmaster()
        {
            var state = _saveGameService.CurrentSave?.Warmaster;
            return state != null &&
                   TryValidateState(state, out HashSet<string> validPurchasedPieces) &&
                   (state.IsTrueWarmaster || validPurchasedPieces.Count >= RequiredTrueWarmasterPieces);
        }

        private static bool TryValidateState(
            WarmasterState state,
            out HashSet<string> validPurchasedPieces)
        {
            validPurchasedPieces = null;
            if (state.UnlockedSetIds == null ||
                state.PurchasedPieceIds == null ||
                state.Level < 0 ||
                state.Experience < 0 ||
                HasNullBlankOrDuplicate(state.UnlockedSetIds) ||
                HasNullBlankOrDuplicate(state.PurchasedPieceIds))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(state.EquippedSetId) &&
                (!KnownSetIds.Contains(state.EquippedSetId) ||
                 !state.UnlockedSetIds.Contains(state.EquippedSetId, StringComparer.Ordinal)))
            {
                return false;
            }

            validPurchasedPieces = new HashSet<string>(
                state.PurchasedPieceIds.Where(IsKnownPiece),
                StringComparer.Ordinal);
            return true;
        }

        private static bool HasNullBlankOrDuplicate(IEnumerable<string> values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsKnownSet(string setId) =>
            !string.IsNullOrWhiteSpace(setId) && KnownSetIds.Contains(setId);

        private static bool IsKnownPiece(string pieceId) =>
            !string.IsNullOrWhiteSpace(pieceId) && KnownPieceIds.Contains(pieceId);

        private static WarmasterState CloneState(WarmasterState state) =>
            new WarmasterState
            {
                EquippedSetId = state.EquippedSetId,
                UnlockedSetIds = state.UnlockedSetIds == null
                    ? null
                    : new List<string>(state.UnlockedSetIds),
                PurchasedPieceIds = state.PurchasedPieceIds == null
                    ? null
                    : new List<string>(state.PurchasedPieceIds),
                IsTrueWarmaster = state.IsTrueWarmaster,
                Level = state.Level,
                Experience = state.Experience
            };

        private static void RestoreState(WarmasterState target, WarmasterState source)
        {
            target.EquippedSetId = source.EquippedSetId;
            target.UnlockedSetIds = source.UnlockedSetIds;
            target.PurchasedPieceIds = source.PurchasedPieceIds;
            target.IsTrueWarmaster = source.IsTrueWarmaster;
            target.Level = source.Level;
            target.Experience = source.Experience;
        }
    }
}
