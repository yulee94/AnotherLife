using UnityEngine;
using UnityEngine.SceneManagement;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using System.Collections.Generic;

namespace AL.UI.RealmSelection
{
    public class RealmSelectionController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private RealmSelectionCard _cardPrefab;
        [SerializeField] private Transform _container;
        [SerializeField] private string _nextScene = "Kingdom";

        private void Start()
        {
            PopulateRealms();
        }

        private void PopulateRealms()
        {
            var dataService = ServiceLocator.Get<IGameDataService>();
            var realms = dataService.GetAllRealms();

            foreach (var realm in realms)
            {
                var card = Instantiate(_cardPrefab, _container);
                card.Setup(realm, OnRealmSelected);
            }
        }

        private void OnRealmSelected(RealmId id)
        {
            Debug.Log($"Realm Selected in UI: {id}");
            var realmService = ServiceLocator.Get<IRealmService>();
            realmService.SelectRealm(id);

            SceneManager.LoadScene(_nextScene);
        }
    }
}
