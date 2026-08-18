using AL.Core;

namespace AL.Slice
{
    /// <summary>
    /// Process-local, in-memory run state for the greybox vertical slice.
    ///
    /// This is the SHARED hand-off contract consumed by the slice workstreams:
    ///   boot/realm-selection -> character-creation -> combat -> kingdom-build -> save/reload.
    ///
    /// It deliberately has NO dependency on the catalog/save/determinism authority
    /// (no ServiceLocator, no ISaveGameService, no RealmCatalogRuntime). Realm selection is the first
    /// field committed by the boot workstream; the later workstreams extend this holder with their own
    /// stable fields (champion, combat result, kingdom build state) before the integration pass.
    /// </summary>
    public static class GreyboxRunState
    {
        private static RealmId _selectedRealmId = RealmId.None;

        /// <summary>The realm the player committed during realm selection, or <see cref="RealmId.None"/>.</summary>
        public static RealmId SelectedRealmId => _selectedRealmId;

        /// <summary>True once a playable realm has been committed for this run.</summary>
        public static bool HasRealm => _selectedRealmId != RealmId.None;

        /// <summary>Stores the committed realm in local run state.</summary>
        public static void CommitRealm(RealmId realmId)
        {
            _selectedRealmId = realmId;
        }

        /// <summary>Clears the slice run state for a fresh boot.</summary>
        public static void Reset()
        {
            _selectedRealmId = RealmId.None;
        }
    }
}
