using System;

namespace AL.VerticalSlice.Combat
{
    public enum CombatAction
    {
        Attack = 0,
        Defend = 1,
        Special = 2
    }

    /// <summary>
    /// Deterministic, turn-based champion-vs-opponent simulation for the greybox duel.
    ///
    /// Pure logic (no Unity types) so it can be driven end-to-end from EditMode tests. Each player
    /// action resolves a full turn: the player acts, then the (seeded) opponent acts. Rules:
    ///   - Attack deals AttackPower; Special deals SpecialPower (mana-gated + cooldown-gated);
    ///     Defend raises the guard for the next incoming hit.
    ///   - Defending reduces the next incoming hit by <c>DefendReduction</c>.
    ///   - A Special that is on cooldown or lacks mana does NOT consume the turn (player may
    ///     re-choose); this is surfaced via <see cref="LastActionConsumedTurn"/>.
    ///   - Win when the opponent reaches 0 HP; lose when the champion reaches 0 HP.
    /// </summary>
    public sealed class CombatEncounterSim
    {
        private readonly SliceChampionProfile _champion;
        private readonly SliceOpponentProfile _opponent;
        private readonly CombatEncounterConfig _config;
        private readonly Random _rng;

        private int _championHealth;
        private int _championMana;
        private int _opponentHealth;
        private int _opponentMana;
        private bool _championDefending;
        private bool _opponentDefending;
        private int _specialCooldownRemaining;
        private int _turnNumber;
        private int _damageDealt;
        private int _damageTaken;
        private int _specialsUsed;
        private CombatEncounterOutcome _outcome;
        private string _lastLog = string.Empty;
        private bool _lastActionConsumedTurn;

        public CombatEncounterSim(
            SliceChampionProfile champion,
            SliceOpponentProfile opponent,
            CombatEncounterConfig config,
            int seed)
        {
            _champion = champion ?? throw new ArgumentNullException(nameof(champion));
            _opponent = opponent ?? throw new ArgumentNullException(nameof(opponent));
            _config = config ?? new CombatEncounterConfig();
            _rng = new Random(seed);

            _championHealth = Math.Max(1, _champion.MaxHealth);
            _championMana = Math.Max(0, _champion.MaxMana);
            _opponentHealth = Math.Max(1, _opponent.MaxHealth);
            _opponentMana = Math.Max(0, _opponent.MaxMana);
        }

        public bool IsFinished => _outcome != CombatEncounterOutcome.None;
        public CombatEncounterOutcome Outcome => _outcome;
        public int TurnNumber => _turnNumber;
        public int ChampionHealth => _championHealth;
        public int ChampionMaxHealth => _champion.MaxHealth;
        public int ChampionMana => _championMana;
        public int ChampionMaxMana => _champion.MaxMana;
        public int OpponentHealth => _opponentHealth;
        public int OpponentMaxHealth => _opponent.MaxHealth;
        public bool IsChampionDefending => _championDefending;
        public int SpecialCooldownRemaining => _specialCooldownRemaining;
        public string LastLog => _lastLog;
        public bool LastActionConsumedTurn => _lastActionConsumedTurn;

        public bool CanUseSpecial =>
            !IsFinished &&
            _specialCooldownRemaining <= 0 &&
            _championMana >= _config.SpecialManaCost;

        /// <summary>
        /// Resolves one player action (and, if it consumes a turn, the opponent's response).
        /// Returns a human-readable log line.
        /// </summary>
        public string PerformPlayerAction(CombatAction action)
        {
            _lastActionConsumedTurn = false;
            if (IsFinished)
            {
                _lastLog = "The encounter has already concluded.";
                return _lastLog;
            }

            switch (action)
            {
                case CombatAction.Attack:
                    return ResolvePlayerAttack();
                case CombatAction.Defend:
                    return ResolvePlayerDefend();
                case CombatAction.Special:
                    return ResolvePlayerSpecial();
                default:
                    _lastLog = "Unknown action.";
                    return _lastLog;
            }
        }

        private string ResolvePlayerAttack()
        {
            int damage = ComputeDamage(_champion.AttackPower, _opponentDefending);
            _opponentHealth = Math.Max(0, _opponentHealth - damage);
            _damageDealt += damage;
            _championDefending = false;
            _lastLog = $"You strike {_opponent.DisplayName} for {damage} damage.";
            return ResolveTurn();
        }

        private string ResolvePlayerDefend()
        {
            _championDefending = true;
            _lastLog = "You brace yourself, reducing the next incoming blow.";
            return ResolveTurn();
        }

        private string ResolvePlayerSpecial()
        {
            if (_specialCooldownRemaining > 0)
            {
                _lastLog = $"Your special is recharging ({_specialCooldownRemaining} turn(s)).";
                return _lastLog; // no turn consumed
            }

            if (_championMana < _config.SpecialManaCost)
            {
                _lastLog = "Not enough mana for your special.";
                return _lastLog; // no turn consumed
            }

            _championMana -= _config.SpecialManaCost;
            _specialCooldownRemaining = Math.Max(0, _config.SpecialCooldownTurns);
            _specialsUsed++;
            int damage = ComputeDamage(_champion.SpecialPower, _opponentDefending);
            _opponentHealth = Math.Max(0, _opponentHealth - damage);
            _damageDealt += damage;
            _championDefending = false;
            _lastLog = $"You unleash your special for {damage} damage.";
            return ResolveTurn();
        }

        private string ResolveTurn()
        {
            _lastActionConsumedTurn = true;
            _turnNumber++;

            if (_opponentHealth <= 0)
            {
                _outcome = CombatEncounterOutcome.Win;
                _lastLog += $" The {_opponent.DisplayName} is defeated!";
                return _lastLog;
            }

            ResolveOpponentAction();

            if (_championHealth <= 0)
            {
                _outcome = CombatEncounterOutcome.Lose;
                _lastLog += " You have fallen!";
                return _lastLog;
            }

            EndOfTurnBookkeeping();
            return _lastLog;
        }

        private void ResolveOpponentAction()
        {
            _opponentMana = Math.Min(_opponent.MaxMana, _opponentMana + _config.OpponentManaRegenPerTurn);

            // Clear a previously-raised guard before deciding the opponent's new action.
            _opponentDefending = false;

            bool useSpecial =
                _opponentMana >= _opponent.SpecialManaCost &&
                _rng.NextDouble() < _config.OpponentSpecialChance;
            bool useDefend = _rng.NextDouble() < _config.OpponentDefendChance;

            if (useSpecial)
            {
                _opponentMana -= _opponent.SpecialManaCost;
                int damage = ComputeDamage(_opponent.SpecialPower, _championDefending);
                _championHealth = Math.Max(0, _championHealth - damage);
                _damageTaken += damage;
                _lastLog += $" {_opponent.DisplayName} lashes out with its special for {damage} damage.";
            }
            else if (useDefend)
            {
                _opponentDefending = true;
                _lastLog += $" {_opponent.DisplayName} raises its guard.";
            }
            else
            {
                int damage = ComputeDamage(_opponent.AttackPower, _championDefending);
                _championHealth = Math.Max(0, _championHealth - damage);
                _damageTaken += damage;
                _lastLog += $" {_opponent.DisplayName} attacks for {damage} damage.";
            }

            // The champion's guard lasts exactly one incoming hit.
            _championDefending = false;
        }

        private void EndOfTurnBookkeeping()
        {
            _championMana = Math.Min(_champion.MaxMana, _championMana + _config.ManaRegenPerTurn);
            if (_specialCooldownRemaining > 0)
            {
                _specialCooldownRemaining--;
            }
        }

        private int ComputeDamage(int power, bool targetDefending)
        {
            float reduction = Math.Max(0f, Math.Min(1f, _config.DefendReduction));
            float multiplier = targetDefending ? (1f - reduction) : 1f;
            return Math.Max(0, (int)Math.Round(power * multiplier, MidpointRounding.AwayFromZero));
        }

        /// <summary>Snapshots the terminal state into a <see cref="SliceCombatResult"/>.</summary>
        public SliceCombatResult BuildResult(string attemptId)
        {
            return new SliceCombatResult
            {
                Outcome = _outcome,
                AttemptId = attemptId ?? string.Empty,
                ChampionId = _champion.Id,
                ChampionDisplayName = _champion.DisplayName,
                OpponentId = _opponent.Id,
                OpponentDisplayName = _opponent.DisplayName,
                TurnsTaken = _turnNumber,
                ChampionHealthRemaining = _championHealth,
                ChampionMaxHealth = _champion.MaxHealth,
                OpponentHealthRemaining = _opponentHealth,
                OpponentMaxHealth = _opponent.MaxHealth,
                DamageDealt = _damageDealt,
                DamageTaken = _damageTaken,
                SpecialsUsed = _specialsUsed
            };
        }
    }
}
