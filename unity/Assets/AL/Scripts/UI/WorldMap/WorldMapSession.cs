using System;

namespace AL.UI.WorldMap
{
    public enum WorldMapSurface
    {
        Closed = 0,
        Map = 1,
        SharedMenu = 2
    }

    /// <summary>
    /// Open/close state for the 3D world map and the Shared Menu entry that opens it.
    /// </summary>
    public static class WorldMapSession
    {
        public static event Action Changed;

        public static WorldMapSurface Surface { get; private set; }

        public static bool IsMapOpen => Surface == WorldMapSurface.Map;

        public static bool IsMenuOpen => Surface == WorldMapSurface.SharedMenu;

        public static bool IsBlockingGameplay => Surface != WorldMapSurface.Closed;

        public static void OpenMap()
        {
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
            Set(IsMapOpen ? WorldMapSurface.Closed : WorldMapSurface.Map);
        }

        public static void OpenSharedMenu()
        {
            Set(WorldMapSurface.SharedMenu);
        }

        public static void CloseSharedMenu()
        {
            if (IsMenuOpen)
            {
                Set(WorldMapSurface.Closed);
            }
        }

        public static void ToggleSharedMenu()
        {
            Set(IsMenuOpen ? WorldMapSurface.Closed : WorldMapSurface.SharedMenu);
        }

        public static void CloseAll()
        {
            Set(WorldMapSurface.Closed);
        }

        public static void OpenMapFromSharedMenu()
        {
            Set(WorldMapSurface.Map);
        }

        public static void ResetStatics()
        {
            Surface = WorldMapSurface.Closed;
            Changed = null;
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
    }
}
