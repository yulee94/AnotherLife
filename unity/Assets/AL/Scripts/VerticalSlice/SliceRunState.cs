namespace AL.VerticalSlice
{
    /// <summary>
    /// Local, in-memory, session-only run state for the greybox vertical slice.
    ///
    /// This is the shared seam the parallel slice tasks read from and write to. It intentionally
    /// does NOT touch catalog/save/determinism authority. This type owns only the combat-relevant
    /// fields this task is responsible for; realm selection, character creation, kingdom build, and
    /// the persistent RunState snapshot contract are owned by their sibling tasks (t_f93cd02b,
    /// t_adbe3cc2, t_6ef5205e, t_59bca09b) and reconciled by the integration task (t_fae6db36).
    ///
    /// Combat reads <see cref="SelectedChampion"/> and writes <see cref="LastCombatResult"/>.
    /// </summary>
    public static class SliceRunState
    {
        /// <summary>
        /// The champion chosen/confirmed during character creation. Combat reads this; if null the
        /// encounter falls back to <see cref="SliceChampionProfile.CreateDefault"/>.
        /// </summary>
        public static SliceChampionProfile SelectedChampion;

        /// <summary>
        /// Result of the most recent champion duel. Combat writes this; kingdom build (reward
        /// consumption) and save/reload read it.
        /// </summary>
        public static SliceCombatResult LastCombatResult;

        public static void Reset()
        {
            SelectedChampion = null;
            LastCombatResult = null;
        }
    }
}
