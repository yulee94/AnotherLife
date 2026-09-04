namespace AL.RealmSelection
{
    public static class RealmSelectionResetPolicy
    {
        public const string ExplicitDeleteSavePath = "explicit-delete-save";

        public static string PathId => ExplicitDeleteSavePath;

        public static bool AllowsAutomaticProfileReplacement => false;

        public static bool RequiresVerifiedDeletion => true;
    }
}
