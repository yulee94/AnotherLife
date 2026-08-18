using System;

namespace AL.VerticalSlice
{
    public enum CombatEncounterOutcome
    {
        None = 0,
        Win = 1,
        Lose = 2,
        Aborted = 3
    }

    /// <summary>
    /// Session-durable result of one champion duel. Written to <see cref="SliceRunState"/> when the
    /// encounter finishes, and consumed by the kingdom-build and save/reload tasks. Plain data only —
    /// no authority, no reward mutation, no persistence.
    /// </summary>
    [Serializable]
    public sealed class SliceCombatResult
    {
        public CombatEncounterOutcome Outcome = CombatEncounterOutcome.None;

        /// <summary>Stable per-attempt id (a fresh GUID per encounter run in the greybox slice).</summary>
        public string AttemptId = string.Empty;

        public string ChampionId = string.Empty;
        public string ChampionDisplayName = string.Empty;
        public string OpponentId = string.Empty;
        public string OpponentDisplayName = string.Empty;

        public int TurnsTaken = 0;
        public int ChampionHealthRemaining = 0;
        public int ChampionMaxHealth = 0;
        public int OpponentHealthRemaining = 0;
        public int OpponentMaxHealth = 0;
        public int DamageDealt = 0;
        public int DamageTaken = 0;
        public int SpecialsUsed = 0;

        public bool Won => Outcome == CombatEncounterOutcome.Win;
        public bool Lost => Outcome == CombatEncounterOutcome.Lose;
        public bool Aborted => Outcome == CombatEncounterOutcome.Aborted;
    }
}
