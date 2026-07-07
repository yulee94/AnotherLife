using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalWarmasterService : IWarmasterService
    {
        private readonly ISaveGameService _saveGameService;

        public LocalWarmasterService(ISaveGameService saveGameService)
        {
            _saveGameService = saveGameService;
        }

        public WarmasterState GetState()
        {
            return _saveGameService.CurrentSave?.Warmaster;
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
    }
}
