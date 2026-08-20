using UnityEngine;
using UnityEngine.EventSystems;

namespace AL.ChampionMode.UI
{
    /// <summary>
    /// HUD clicks and Shared Menu must not swing the follow camera.
    /// </summary>
    public static class ChampionHudCameraGate
    {
        public static bool MenuOpen { get; set; }

        public static bool RecapOpen { get; set; }

        public static bool BlocksLook => MenuOpen || RecapOpen;

        public static bool ShouldIgnoreLook()
        {
            if (BlocksLook)
            {
                return true;
            }

            if (IsPointerOverUi())
            {
                return true;
            }

            return Cursor.lockState != CursorLockMode.Locked;
        }

        public static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        public static void Reset()
        {
            MenuOpen = false;
            RecapOpen = false;
        }
    }
}
