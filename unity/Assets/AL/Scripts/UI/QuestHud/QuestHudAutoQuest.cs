using AL.UI.SharedMenu;

namespace AL.UI.QuestHud
{
    /// <summary>
    /// Player preference for auto-accept / auto-continue / auto-complete.
    /// Default OFF matches OMEN_1 autoAccept:false. Never auto-fires the
    /// Warzone-gate prompt, which remains a mandatory hard stop.
    /// </summary>
    public static class QuestHudAutoQuest
    {
        private static bool _enabled;

        public static bool Enabled => _enabled;

        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        public static void ResetForTests()
        {
            _enabled = false;
        }

        public static bool ShouldFire(QuestHudModel model)
        {
            return model != null &&
                   model.CanAutoFire &&
                   CanDriveInCurrentContext();
        }

        public static bool CanDriveInCurrentContext()
        {
            return !SharedMenuModeSwitchHost.DetectCombat() &&
                   !SharedMenuModeSwitchHost.DetectUnsafe();
        }
    }
}
