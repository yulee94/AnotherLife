using System;
using AL.ChampionMode.UI;
using AL.UI.SharedMenu;

namespace AL.UI.WorldMap
{
    public enum WorldMapSurface
    {
        Closed = 0,
        Map = 1
    }

    /// <summary>
    /// Open/close state for the 3D world map and the Shared Menu entry that opens it.
    /// </summary>
    public static class WorldMapSession
    {
        public static event Action Changed;

        public static WorldMapSurface Surface { get; private set; }

        public static bool IsMapOpen => Surface == WorldMapSurface.Map;

        public static bool IsBlockingGameplay => Surface != WorldMapSurface.Closed;

        public static void OpenMap()
        {
            PrepareToOpen();
            Set(WorldMapSurface.Map);
        }

        public static void CloseMap()
        {
            if (IsMapOpen)
            {
                Set(WorldMapSurface.Closed);
            }
        }

        public static void ToggleMap()
        {
            if (!IsMapOpen)
            {
                PrepareToOpen();
            }

            Set(IsMapOpen ? WorldMapSurface.Closed : WorldMapSurface.Map);
        }


        public static void CloseAll()
        {
            Set(WorldMapSurface.Closed);
        }


        public static void ResetStatics()
        {
            PruneDestroyedSubscribers();
            Surface = WorldMapSurface.Closed;
            Changed?.Invoke();
        }

        private static void PruneDestroyedSubscribers()
        {
            if (Changed == null)
            {
                return;
            }

            Delegate[] subscribers = Changed.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                Delegate subscriber = subscribers[i];
                if (subscriber.Target is UnityEngine.Object unityTarget &&
                    unityTarget == null)
                {
                    Changed -= (Action)subscriber;
                }
            }
        }

        private static void Set(WorldMapSurface next)
        {
            if (Surface == next)
            {
                return;
            }

            Surface = next;
            Changed?.Invoke();
        }

        private static void PrepareToOpen()
        {
            ChampionHudSession.CloseActiveMenu();
            SharedMenuModeSwitchHost.CloseActiveMenus();
        }
    }
}
