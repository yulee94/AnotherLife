using AL.ChampionMode.UI;
using AL.Input;
using AL.UI.SharedMenu;
using UnityEngine;

namespace AL.UI.QuestHud
{
    /// <summary>
    /// Player preference for auto-accept / auto-continue / auto-complete.
    /// Default OFF matches OMEN_1 autoAccept:false. Never auto-fires the
    /// Warzone-gate prompt, which remains a mandatory hard stop.
    /// </summary>
    public static class QuestHudAutoQuest
    {
        public const string PreferenceKey = "al.mvp.auto-quest.enabled";

        private static bool _enabled;
        private static bool _loaded;

        public static bool Enabled
        {
            get
            {
                EnsureLoaded();
                return _enabled;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            _loaded = true;
            PlayerPrefs.SetInt(PreferenceKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetForTests()
        {
            PlayerPrefs.DeleteKey(PreferenceKey);
            PlayerPrefs.Save();
            _enabled = false;
            _loaded = true;
        }

        public static void ResetRuntimeCacheForTests()
        {
            _enabled = false;
            _loaded = false;
        }

        public static bool ShouldFire(QuestHudModel model)
        {
            return model != null &&
                   model.CanAutoFire &&
                   Enabled &&
                   CanDriveInCurrentContext();
        }

        public static bool CanDriveInCurrentContext()
        {
            return !GameInput.GameplaySuppressed &&
                   !ChampionHudCameraGate.BlocksGameplay &&
                   !SharedMenuModeSwitchHost.DetectCombat() &&
                   !SharedMenuModeSwitchHost.DetectUnsafe();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _enabled = PlayerPrefs.GetInt(PreferenceKey, 0) == 1;
            _loaded = true;
        }
    }
}
