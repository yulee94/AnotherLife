using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalRealmService : IRealmService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IGameDataService _gameDataService;

        public RealmId CurrentRealmId => _saveGameService.CurrentSave?.SelectedRealm ?? RealmId.None;

        public RealmDefinition CurrentRealm => _gameDataService.GetRealm(CurrentRealmId);

        public LocalRealmService(ISaveGameService saveGameService, IGameDataService gameDataService)
        {
            _saveGameService = saveGameService;
            _gameDataService = gameDataService;
        }

        public void SelectRealm(RealmId id)
        {
            if (_saveGameService.CurrentSave == null)
            {
                _saveGameService.CreateNewSave(id);
            }
            else
            {
                _saveGameService.CurrentSave.SelectedRealm = id;
                _saveGameService.Save();
            }

            Debug.Log($"Realm Selected: {id}");
        }
    }
}
