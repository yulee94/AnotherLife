namespace AL.Slice
{
    /// <summary>
    /// In-memory "local run state" shared by the greybox slice stages. Realm selection, character
    /// creation, combat, and kingdom build each read/write this single live state object; the save
    /// point persists a snapshot of it and reload restores it. Deliberately independent of the
    /// catalog/save/determinism authority and of the ServiceLocator.
    /// </summary>
    public static class RunStateSession
    {
        private static RunState _current;

        public static RunState Current => _current;

        public static bool IsStarted => _current != null;

        public static RunState GetOrCreate()
        {
            if (_current == null)
            {
                _current = RunState.CreateDefault();
            }

            return _current;
        }

        /// <summary>Stores a defensive clone so callers cannot mutate the live state through the snapshot.</summary>
        public static void Set(RunState state)
        {
            _current = state != null ? state.Clone() : null;
        }

        public static void Reset()
        {
            _current = null;
        }
    }
}
