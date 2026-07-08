using UnityEngine;
using UnityEngine.SceneManagement;
using AL.Core;
using AL.Core.Interfaces;
using System.Collections;

namespace AL.UI
{
    public class BootController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _minSplashScreenTime = 2f;
        [SerializeField] private string _realmSelectionScene = "RealmSelection";
        [SerializeField] private string _kingdomScene = "Kingdom";

        private IEnumerator Start()
        {
            Debug.Log("AL Boot Sequence Started...");

            // Wait for a minimum splash screen duration
            yield return new WaitForSeconds(_minSplashScreenTime);

            var realmService = ServiceLocator.Get<IRealmService>();

            if (realmService.CurrentRealmId == RealmId.None)
            {
                Debug.Log("No Realm Selected. Transitioning to Realm Selection...");
                SceneManager.LoadScene(_realmSelectionScene);
            }
            else
            {
                Debug.Log($"Realm {realmService.CurrentRealmId} detected. Loading Kingdom...");
                SceneManager.LoadScene(_kingdomScene);
            }
        }
    }
}
