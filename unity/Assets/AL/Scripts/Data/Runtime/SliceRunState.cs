using System;

namespace AL.Data.Runtime
{
    /// <summary>
    /// Greybox in-memory run state for the vertical slice.
    ///
    /// SaveGameData is schema-v1 authority-locked: any new top-level field fails semantic validation
    /// with SAVE_UNKNOWN_TOP_LEVEL_FIELD and forces the save authority into a read-only/Unavailable
    /// state. The slice therefore keeps its cross-scene run state here instead of on SaveGameData.
    /// Static state survives scene loads (Boot -> RealmSelection -> CharacterCreation -> ChampionArena
    /// -> Kingdom) for the duration of the session.
    ///
    /// Realm selection already persists through the legacy realm-selection candidate store, so it
    /// remains on SaveGameData.SelectedRealm. The champion lives here; the combat and save/reload
    /// slice tasks read it from <see cref="Champion"/>. The save/reload slice task owns a simple
    /// file/in-memory snapshot that persists this holder; this type only stores it.
    /// </summary>
    public static class SliceRunState
    {
        public static ChampionState Champion { get; private set; } = new ChampionState();

        public static bool HasConfirmedChampion =>
            Champion != null && Champion.IsConfirmed;

        public static void ConfirmChampion(ChampionState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            state.IsConfirmed = true;
            if (state.CreatedTimestamp <= 0)
            {
                state.CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            Champion = state;
        }

        public static void Reset()
        {
            Champion = new ChampionState();
        }
    }
}
