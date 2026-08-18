using System;
using System.Collections.Generic;
using AL.Core;

namespace AL.Data.Runtime
{
    /// <summary>
    /// Greybox champion run state. Captures the single champion the player creates or confirms at
    /// the character creation entry point, so combat and save/reload can read stable identity,
    /// class, stats, and loadout without depending on authority services.
    ///
    /// It is a single value (not a list) held on <see cref="SliceRunState"/>, so "exactly one
    /// champion" is enforced structurally. Persistence is deferred to the save/reload slice task;
    /// the character creation controller writes this in-memory run state only.
    /// </summary>
    [Serializable]
    public class ChampionState
    {
        // Stable identity.
        public string Id = string.Empty;                 // archetype id (lowercase snake_case)
        public string DisplayName = string.Empty;        // champion name

        // Class / realm identity.
        public ClassFamily Family = ClassFamily.Warrior;
        public SubclassId Subclass = SubclassId.None;
        public RealmId Realm = RealmId.None;             // home realm (matches SelectedRealm)

        // Stats (greybox integers, compatible with the legacy ChampionCombat surface).
        public int MaxHealth = 1000;
        public int MaxMana = 100;
        public int Attack = 50;
        public int Defense = 30;
        public int Speed = 10;
        public int CritRate = 5;

        // Loadout.
        public List<string> SkillIds = new List<string>();
        public string WeaponStyleId = "sword";
        public string OffhandStyleId = "shield";

        // Confirmation.
        public bool IsConfirmed;
        public long CreatedTimestamp;                    // unix seconds

        public bool HasIdentity =>
            !string.IsNullOrWhiteSpace(Id) &&
            !string.IsNullOrWhiteSpace(DisplayName);
    }
}
