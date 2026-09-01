using System;

namespace AL.VerticalSlice
{
    /// <summary>
    /// Greybox combat-relevant snapshot of the selected champion for the vertical-slice run state.
    ///
    /// This is the slice's lightweight "character" contract that combat reads. It is deliberately
    /// separate from the authority-scoped <c>AL.ChampionMode.C1.ChampionCombatProfile</c> (the
    /// fixed-point / micros / hash catalog model) because the vertical slice must not depend on
    /// catalog/save/determinism authority. The character-creation task (t_adbe3cc2) is expected to
    /// populate these fields, and the integration task (t_fae6db36) owns reconciling its character
    /// output onto this shape.
    ///
    /// Field IDs follow the project convention: lowercase snake_case for data IDs, PascalCase for
    /// C# identifiers.
    /// </summary>
    [Serializable]
    public sealed class SliceChampionProfile
    {
        public string Id = string.Empty;          // e.g. "champion_stonehold_vanguard"
        public string DisplayName = string.Empty; // catalog display name
        public string ClassName = string.Empty;   // e.g. "Vanguard"

        public int MaxHealth = 300;
        public int MaxMana = 100;
        public int AttackPower = 40;
        public int SpecialPower = 90;

        /// <summary>Fraction (0..1) of incoming damage this champion mitigates by default.</summary>
        public float DefendMitigation = 0.5f;

        public SliceChampionProfile()
        {
        }

        public SliceChampionProfile(
            string id,
            string displayName,
            string className,
            int maxHealth,
            int maxMana,
            int attackPower,
            int specialPower,
            float defendMitigation)
        {
            Id = id;
            DisplayName = displayName;
            ClassName = className;
            MaxHealth = maxHealth;
            MaxMana = maxMana;
            AttackPower = attackPower;
            SpecialPower = specialPower;
            DefendMitigation = defendMitigation;
        }

        /// <summary>
        /// Resolves the packaged catalog default champion. Missing or invalid
        /// catalogs fail closed — there is no silent greybox stat block.
        /// </summary>
        public static SliceChampionProfile CreateDefault()
        {
            SliceChampionProfile profile;
            string diagnosticCode;
            if (!AL.Services.Local.SixFamilyRuntimeCatalog.TryGetDefaultChampion(
                    out profile,
                    out diagnosticCode))
            {
                throw new InvalidOperationException(
                    "AL-GDC-CHAMPION-MISSING: CreateDefault requires a catalog record (" +
                    diagnosticCode +
                    ").");
            }

            return profile;
        }
    }
}
