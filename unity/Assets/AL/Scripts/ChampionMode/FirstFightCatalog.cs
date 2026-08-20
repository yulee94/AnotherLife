using System.Collections.Generic;
using AL.ChampionMode.Skills;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;

namespace AL.ChampionMode
{
    /// <summary>
    /// Resolves the first-session direct-control fight from catalog sources only.
    /// Missing player, opponent, or special stats fail closed — no fabricated numbers.
    /// </summary>
    public sealed class FirstFightLoadout
    {
        public string PlayerId;
        public string PlayerDisplayName;
        public int PlayerMaxHealth;
        public int PlayerMaxMana;
        public int PlayerAttack;
        public string OpponentId;
        public string OpponentDisplayName;
        public int OpponentMaxHealth;
        public int OpponentMaxMana;
        public int OpponentAttack;
        public int SpecialSlot;
        public string SpecialSkillId;
        public string SpecialSkillName;
        public float SpecialPower;
        public string DiagnosticCode;
    }

    public static class FirstFightCatalog
    {
        public const string ReadyCode = "AL-FIRST-FIGHT-READY";
        public const string MissingCode = "AL-FIRST-FIGHT-CATALOG-MISSING";
        public const string PlayerMissingCode = "AL-FIRST-FIGHT-PLAYER-MISSING";
        public const string OpponentMissingCode = "AL-FIRST-FIGHT-OPPONENT-MISSING";
        public const string SpecialMissingCode = "AL-FIRST-FIGHT-SPECIAL-MISSING";
        public const string AttackMissingCode = "AL-FIRST-FIGHT-ATTACK-MISSING";

        public static bool TryResolve(
            IGameDataService data,
            ChampionState selected,
            RealmId fallbackRealm,
            SkillLoadoutData[] skills,
            out FirstFightLoadout loadout,
            out string diagnosticCode)
        {
            loadout = null;
            diagnosticCode = MissingCode;
            if (data == null)
            {
                return false;
            }

            ChampionDefinition player = ResolvePlayer(data, selected, fallbackRealm);
            if (!HasCombatStats(player))
            {
                diagnosticCode = selected != null && selected.HasIdentity
                    ? PlayerMissingCode + ":" + selected.Id
                    : PlayerMissingCode;
                return false;
            }

            ChampionDefinition opponent = ResolveOpponent(data, player);
            if (!HasCombatStats(opponent))
            {
                diagnosticCode = OpponentMissingCode;
                return false;
            }

            if (player.BaseStats.Attack <= 0 || opponent.BaseStats.Attack <= 0)
            {
                diagnosticCode = AttackMissingCode;
                return false;
            }

            SkillLoadoutData special = ResolveSpecial(skills);
            if (special == null || special.power <= 0f || string.IsNullOrWhiteSpace(special.id))
            {
                diagnosticCode = SpecialMissingCode;
                return false;
            }

            loadout = new FirstFightLoadout
            {
                PlayerId = player.Id,
                PlayerDisplayName = player.DisplayName,
                PlayerMaxHealth = player.BaseStats.MaxHealth,
                PlayerMaxMana = player.BaseStats.MaxMana,
                PlayerAttack = player.BaseStats.Attack,
                OpponentId = opponent.Id,
                OpponentDisplayName = opponent.DisplayName,
                OpponentMaxHealth = opponent.BaseStats.MaxHealth,
                OpponentMaxMana = opponent.BaseStats.MaxMana,
                OpponentAttack = opponent.BaseStats.Attack,
                SpecialSlot = special.slot,
                SpecialSkillId = special.id,
                SpecialSkillName = string.IsNullOrWhiteSpace(special.displayName)
                    ? special.id
                    : special.displayName,
                SpecialPower = special.power,
                DiagnosticCode = ReadyCode
            };
            diagnosticCode = ReadyCode;
            return true;
        }

        public static bool TryResolveFromRegistered(
            RealmId fallbackRealm,
            out FirstFightLoadout loadout,
            out string diagnosticCode)
        {
            loadout = null;
            diagnosticCode = MissingCode;
            IGameDataService data;
            if (!ServiceLocator.TryGet(out data) || data == null)
            {
                return false;
            }

            SkillLoadoutData[] skills = null;
            SkillLoadoutCatalog.TryLoad(out skills);
            return TryResolve(
                data,
                SliceRunState.Champion,
                fallbackRealm,
                skills,
                out loadout,
                out diagnosticCode);
        }

        private static ChampionDefinition ResolvePlayer(
            IGameDataService data,
            ChampionState selected,
            RealmId fallbackRealm)
        {
            if (selected != null && selected.HasIdentity)
            {
                return data.GetChampion(selected.Id);
            }

            ChampionDefinition byRealm = FindFirstForRealm(data, fallbackRealm);
            return byRealm != null ? byRealm : FindFirstValid(data);
        }

        private static ChampionDefinition ResolveOpponent(IGameDataService data, ChampionDefinition player)
        {
            ChampionDefinition differentRealm = null;
            foreach (ChampionDefinition champion in Enumerate(data))
            {
                if (!HasCombatStats(champion) || champion.Id == player.Id)
                {
                    continue;
                }

                if (champion.Realm != player.Realm)
                {
                    return champion;
                }

                if (differentRealm == null)
                {
                    differentRealm = champion;
                }
            }

            return differentRealm;
        }

        private static ChampionDefinition FindFirstForRealm(IGameDataService data, RealmId realm)
        {
            if (realm == RealmId.None)
            {
                return null;
            }

            foreach (ChampionDefinition champion in Enumerate(data))
            {
                if (HasCombatStats(champion) && champion.Realm == realm)
                {
                    return champion;
                }
            }

            return null;
        }

        private static ChampionDefinition FindFirstValid(IGameDataService data)
        {
            foreach (ChampionDefinition champion in Enumerate(data))
            {
                if (HasCombatStats(champion))
                {
                    return champion;
                }
            }

            return null;
        }

        private static SkillLoadoutData ResolveSpecial(SkillLoadoutData[] skills)
        {
            if (skills == null)
            {
                return null;
            }

            SkillLoadoutData fallback = null;
            for (int i = 0; i < skills.Length; i++)
            {
                SkillLoadoutData skill = skills[i];
                if (skill == null || skill.power <= 0f || string.IsNullOrWhiteSpace(skill.id))
                {
                    continue;
                }

                if (skill.slot == FirstSessionChampionStart.SpecialSkillSlot ||
                    string.Equals(skill.id, FirstSessionChampionStart.SpecialSkillId))
                {
                    return skill;
                }

                if (fallback == null)
                {
                    fallback = skill;
                }
            }

            return fallback;
        }

        private static IEnumerable<ChampionDefinition> Enumerate(IGameDataService data)
        {
            IEnumerable<ChampionDefinition> champions = data.GetAllChampions();
            return champions ?? System.Array.Empty<ChampionDefinition>();
        }

        private static bool HasCombatStats(ChampionDefinition champion)
        {
            return champion != null &&
                   !string.IsNullOrWhiteSpace(champion.Id) &&
                   champion.BaseStats != null &&
                   champion.BaseStats.MaxHealth > 0;
        }
    }
}
