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

        public int GetCredits() => _saveGameService.CurrentSave?.WarzoneCredits ?? 0;

        public void AddCredits(int amount)
        {
            if (_saveGameService.CurrentSave == null)
            {
                return;
            }

            _saveGameService.CurrentSave.WarzoneCredits += amount;
            _saveGameService.Save();
            Debug.Log($"Added {amount} Warzone Credits. Total: {_saveGameService.CurrentSave.WarzoneCredits}");
        }

        public bool SpendCredits(int amount)
        {
            if (_saveGameService.CurrentSave != null && _saveGameService.CurrentSave.WarzoneCredits >= amount)
            {
                _saveGameService.CurrentSave.WarzoneCredits -= amount;
                _saveGameService.Save();
                return true;
            }
            return false;
        }
    }
}
