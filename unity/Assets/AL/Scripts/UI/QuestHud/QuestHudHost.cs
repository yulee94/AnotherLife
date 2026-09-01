using AL.ChampionMode.Quests;
using AL.Core;
using AL.Core.Interfaces;
using AL.UI.Kingdom;
using AL.UI.SharedMenu;
using AL.UI.WorldMap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.UI.QuestHud
{
    /// <summary>
    /// Mounts the Quest HUD on ChampionArena (3D) and Kingdom (2.5D teaching).
    /// Does not edit ChampionArenaSceneController or SharedMenuOverlay.
    /// </summary>
    [DefaultExecutionOrder(220)]
    public sealed class QuestHudHost : MonoBehaviour
    {
        private bool _awaitingSave = true;

        public QuestHudOverlay Overlay { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        public static QuestHudHost EnsureForScene(Scene scene)
        {
            return EnsureForSceneName(scene.name);
        }

        public static QuestHudHost EnsureForSceneName(string sceneName)
        {
            if (!CrossModeSceneSwitch.IsSupportedHostScene(sceneName))
            {
                return null;
            }

            QuestHudHost existing = Object.FindObjectOfType<QuestHudHost>();
            if (existing != null)
            {
                existing.Refresh();
                return existing;
            }

            var go = new GameObject(QuestHudCopy.HostName);
            return go.AddComponent<QuestHudHost>();
        }

        private void Awake()
        {
            Overlay = QuestHudOverlay.Mount(transform);
            Refresh();
        }

        private void OnEnable()
        {
            ProofOfWorthDirector.LordshipGrantedObserved += HandleLordshipGranted;
        }

        private void OnDisable()
        {
            ProofOfWorthDirector.LordshipGrantedObserved -= HandleLordshipGranted;
        }

        private void Update()
        {
            if (_awaitingSave)
            {
                Refresh();
            }

            Overlay?.ConsiderAutoQuest();
        }

        public void Refresh()
        {
            if (Overlay == null)
            {
                Overlay = QuestHudOverlay.Mount(transform);
            }

            ProofOfWorthDirector director = Object.FindObjectOfType<ProofOfWorthDirector>();
            if (director != null && director.State != null && !director.State.LordshipGranted)
            {
                _awaitingSave = false;
                Overlay.Bind(
                    QuestHudPlanner.FromProofOfWorth(director.State, QuestHudAutoQuest.Enabled),
                    director.ChoosePrimary);
                return;
            }

            if (!TryGetSave(out ISaveGameService save))
            {
                _awaitingSave = true;
                return;
            }

            _awaitingSave = false;

            if (CrossModeSceneSwitch.IsAdventureScene(SceneName()))
            {
                KingdomTeachingReturnDirector returnDirector =
                    GetComponent<KingdomTeachingReturnDirector>();
                if (returnDirector == null)
                {
                    returnDirector = gameObject.AddComponent<KingdomTeachingReturnDirector>();
                }

                if (returnDirector.EnsureReady(save.CurrentSave, Overlay))
                {
                    Overlay.Bind(
                        QuestHudPlanner.WarzoneGate(QuestHudAutoQuest.Enabled),
                        null,
                        Refresh);
                    return;
                }

                KingdomTeachingCatalog catalog = KingdomTeachingCatalog.LoadCanonical();
                KingdomTeachingState teaching =
                    KingdomTeachingQuestline.Evaluate(save.CurrentSave, catalog);
                if (teaching.IsAvailable && !teaching.IsComplete)
                {
                    MainQuestMapSession.Clear();
                    Overlay.Bind(
                        QuestHudPlanner.FromKingdomTeachingEntry(
                            catalog.Entry,
                            QuestHudAutoQuest.Enabled),
                        EnterKingdomTeaching,
                        Refresh);
                    return;
                }
            }

            if (CrossModeSceneSwitch.IsKingdomScene(SceneName()) &&
                ProofOfWorthLordship.IsGranted(save.CurrentSave))
            {
                KingdomTeachingDirector teaching =
                    GetComponent<KingdomTeachingDirector>();
                if (teaching == null)
                {
                    teaching = gameObject.AddComponent<KingdomTeachingDirector>();
                }

                teaching.EnsureReady(save, Overlay);
            }
        }

        private void EnterKingdomTeaching()
        {
            string currentScene = SceneName();
            if (!CrossModeSceneSwitch.IsAdventureScene(currentScene))
            {
                return;
            }

            SharedMenuModeSwitchHost host =
                SharedMenuModeSwitchHost.EnsureForSceneName(currentScene);
            host?.Commit(currentScene, SharedMenuIds.Kingdom2_5D);
        }

        private void HandleLordshipGranted()
        {
            Refresh();
        }

        private static string SceneName()
        {
            return Application.isPlaying ? SceneManager.GetActiveScene().name : SharedMenuIds.KingdomScene;
        }

        private static bool TryGetSave(out ISaveGameService save)
        {
            if (ServiceLocator.TryGet<IOfflineServiceStackMarker>(out var marker) &&
                marker.TryGetExpected(out save) &&
                save != null)
            {
                return true;
            }

            save = null;
            return false;
        }
    }
}
