using AL.ChampionMode.Quests;
using AL.Core;
using AL.Core.Interfaces;
using AL.UI.SharedMenu;
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

        public void Refresh()
        {
            if (Overlay == null)
            {
                Overlay = QuestHudOverlay.Mount(transform);
            }

            ProofOfWorthDirector director = Object.FindObjectOfType<ProofOfWorthDirector>();
            if (director != null && director.State != null && !director.State.LordshipGranted)
            {
                Overlay.Bind(
                    QuestHudPlanner.FromProofOfWorth(director.State, QuestHudAutoQuest.Enabled),
                    director.ChoosePrimary);
                return;
            }

            if (CrossModeSceneSwitch.IsKingdomScene(SceneName()) && HasLordship())
            {
                Overlay.Bind(
                    QuestHudPlanner.TeachingStores(QuestHudAutoQuest.Enabled),
                    () => { });
            }
        }

        private static string SceneName()
        {
            return Application.isPlaying ? SceneManager.GetActiveScene().name : SharedMenuIds.KingdomScene;
        }

        private static bool HasLordship()
        {
            if (ServiceLocator.TryGet<IOfflineServiceStackMarker>(out var marker) &&
                marker.TryGetExpected<ISaveGameService>(out var save) &&
                save != null)
            {
                return ProofOfWorthLordship.IsGranted(save.CurrentSave);
            }

            return false;
        }
    }
}
