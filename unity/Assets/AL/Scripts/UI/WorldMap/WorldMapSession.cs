using System;

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


        public static void CloseAll()
        {
            Set(WorldMapSurface.Closed);
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
