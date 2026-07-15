using AL.Core.Interfaces;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalWarzoneCreditService : IWarzoneCreditService
    {
        private readonly ISaveGameService _saveGameService;

        public LocalWarzoneCreditService(ISaveGameService saveGameService)
        {
            _saveGameService = saveGameService;
        }

        public int GetCredits()
        {
            NormalizeCredits();
            return _saveGameService.CurrentSave?.WarzoneCredits ?? 0;
        }

        public void AddCredits(int amount)
        {
            if (_saveGameService.CurrentSave == null)
            {
                return;
            }

            NormalizeCredits();
            if (amount == 0)
            {
                return;
            }

            if (amount < 0)
            {
                Debug.LogWarning($"[AL-ECO-INVALID-CREDITS] AddCredits rejected negative amount {amount}.");
                return;
            }

            int finalCredits;
            try
            {
                finalCredits = checked(_saveGameService.CurrentSave.WarzoneCredits + amount);
            }
            catch (System.OverflowException)
            {
                Debug.LogWarning($"[AL-ECO-CREDIT-OVERFLOW] AddCredits rejected overflow: {_saveGameService.CurrentSave.WarzoneCredits} + {amount}.");
                return;
            }

            _saveGameService.CurrentSave.WarzoneCredits = finalCredits;
            _saveGameService.Save();
            Debug.Log($"Added {amount} Warzone Credits. Total: {_saveGameService.CurrentSave.WarzoneCredits}");
        }

        public bool SpendCredits(int amount)
        {
            if (_saveGameService.CurrentSave == null)
            {
                return false;
            }

            NormalizeCredits();
            if (amount <= 0)
            {
                Debug.LogWarning($"[AL-ECO-INVALID-CREDITS] SpendCredits rejected non-positive amount {amount}.");
                return false;
            }

            if (_saveGameService.CurrentSave.WarzoneCredits >= amount)
            {
                _saveGameService.CurrentSave.WarzoneCredits -= amount;
                _saveGameService.Save();
                return true;
            }
            return false;
        }

        private void NormalizeCredits()
        {
            if (_saveGameService.CurrentSave == null)
            {
                return;
            }

            if (_saveGameService.CurrentSave.WarzoneCredits < 0)
            {
                Debug.LogWarning($"[AL-ECO-NEGATIVE-CREDITS] Repaired negative Warzone Credits balance {_saveGameService.CurrentSave.WarzoneCredits} to 0.");
                _saveGameService.CurrentSave.WarzoneCredits = 0;
            }
        }
    }
}
