using System;
using UnityEngine;

namespace AL.VerticalSlice.Combat
{
    /// <summary>
    /// Greybox combat tuning surface. Every balance/feel knob the integration/tuning pass
    /// (t_fae6db36) may need is a public field here, so tuning happens in the Inspector without code
    /// changes. The opponent is "hardcoded" by default but exposed here so its stats and AI can be
    /// tuned in place.
    /// </summary>
    [Serializable]
    public sealed class CombatEncounterConfig
    {
        [Header("Champion action economy")]
        [Tooltip("Mana cost of the champion's Special action.")]
        public int SpecialManaCost = 30;

        [Tooltip("Turns the Special stays on cooldown after use.")]
        public int SpecialCooldownTurns = 2;

        [Tooltip("Mana restored to the champion at the end of each of its turns.")]
        public int ManaRegenPerTurn = 10;

        [Tooltip("Fraction (0..1) of incoming damage negated while defending. 0.6 = take 40%.")]
        [Range(0f, 1f)]
        public float DefendReduction = 0.6f;

        [Header("Opponent (hardcoded, tunable)")]
        public SliceOpponentProfile Opponent = SliceOpponentProfile.CreateDefault();

        [Header("Opponent AI (deterministic given a seed)")]
        [Tooltip("Chance (0..1) the opponent uses its special when it has mana.")]
        [Range(0f, 1f)]
        public float OpponentSpecialChance = 0.35f;

        [Tooltip("Chance (0..1) the opponent defends instead of attacking.")]
        [Range(0f, 1f)]
        public float OpponentDefendChance = 0.25f;

        [Tooltip("Mana restored to the opponent at the start of each of its actions.")]
        public int OpponentManaRegenPerTurn = 8;

        public CombatEncounterConfig()
        {
        }
    }
}
