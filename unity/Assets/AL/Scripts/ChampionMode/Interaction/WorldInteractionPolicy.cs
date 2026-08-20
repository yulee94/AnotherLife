namespace AL.ChampionMode.Interaction
{
    /// <summary>
    /// Confirm gate. No existing combat/unsafe interact policy lives in ChampionMode,
    /// so this only rejects an unavailable actor (dead). Combat lock is not invented here.
    /// </summary>
    public static class WorldInteractionPolicy
    {
        public static bool CanConfirm(bool actorAvailable)
        {
            return actorAvailable;
        }
    }
}
