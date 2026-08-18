using System;

namespace AL.VerticalSlice
{
    /// <summary>
    /// The single hardcoded greybox opponent for the champion duel. "Hardcoded" means the slice
    /// does not resolve this from any catalog or authority service; the values live here and are
    /// additionally exposed through <see cref="Combat.CombatEncounterConfig"/> for the tuning pass.
    /// </summary>
    [Serializable]
    public sealed class SliceOpponentProfile
    {
        public string Id = string.Empty;          // e.g. "opponent_greybox_wraith"
        public string DisplayName = string.Empty; // e.g. "Greybox Wraith"

        public int MaxHealth = 240;
        public int MaxMana = 60;
        public int AttackPower = 30;
        public int SpecialPower = 60;
        public float DefendMitigation = 0.4f;
        public int SpecialManaCost = 25;

        public SliceOpponentProfile()
        {
        }

        public SliceOpponentProfile(
            string id,
            string displayName,
            int maxHealth,
            int maxMana,
            int attackPower,
            int specialPower,
            float defendMitigation,
            int specialManaCost)
        {
            Id = id;
            DisplayName = displayName;
            MaxHealth = maxHealth;
            MaxMana = maxMana;
            AttackPower = attackPower;
            SpecialPower = specialPower;
            DefendMitigation = defendMitigation;
            SpecialManaCost = specialManaCost;
        }

        public static SliceOpponentProfile CreateDefault()
        {
            return new SliceOpponentProfile(
                "opponent_greybox_wraith",
                "Greybox Wraith",
                240,
                60,
                30,
                60,
                0.4f,
                25);
        }
    }
}
