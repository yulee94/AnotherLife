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
        public const string UncommittedRealmCode = "AL-FIRST-FIGHT-REALM-UNCOMMITTED";

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

            if (!SkillLoadoutCatalog.TryCreateSnapshot(skills, out SkillLoadoutSnapshot snapshot))
            {
                diagnosticCode = SpecialMissingCode;
                return false;
            }

            return TryResolveSnapshot(
                data,
                selected,
                fallbackRealm,
                snapshot,
                out loadout,
                out diagnosticCode);
        }

        /// <summary>
        /// Authoritative first-fight resolver. Callers must supply the same complete,
        /// immutable snapshot that is already published by the live SkillCaster.
        /// This keeps URI-backed StreamingAssets platforms off the synchronous file path.
        /// </summary>
        public static bool TryResolveSnapshot(
            IGameDataService data,
            ChampionState selected,
            RealmId fallbackRealm,
            SkillLoadoutSnapshot skills,
            out FirstFightLoadout loadout,
            out string diagnosticCode)
        {
            loadout = null;
            diagnosticCode = MissingCode;
            if (data == null)
            {
                return false;
            }

            if (!TryResolveSpecial(skills, out SkillLoadoutSlot special))
            {
                diagnosticCode = SpecialMissingCode;
                return false;
            }

            bool hasSelectedIdentity = selected != null && selected.HasIdentity;
            if (!hasSelectedIdentity && !IsCommittedRealm(fallbackRealm))
            {
                diagnosticCode = UncommittedRealmCode;
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
                SpecialSlot = special.Slot,
                SpecialSkillId = special.Id,
                SpecialSkillName = special.DisplayName,
                SpecialPower = special.Power,
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

            if (!SkillLoadoutCatalog.TryLoadSnapshot(out SkillLoadoutSnapshot skills))
            {
                diagnosticCode = SpecialMissingCode;
                return false;
            }

            return TryResolveSnapshot(
                data,
                SliceRunState.Champion,
                fallbackRealm,
                skills,
                out loadout,
                out diagnosticCode);
        }

        /// <summary>
        /// Runtime-safe registered-data path. The snapshot comes from SkillCaster,
        /// so Android/WebGL do not synchronously reopen packaged StreamingAssets.
        /// </summary>
        public static bool TryResolveFromRegistered(
            RealmId fallbackRealm,
            SkillLoadoutSnapshot skills,
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

            return TryResolveSnapshot(
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

            return FindFirstForRealm(data, fallbackRealm);
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

        private static bool IsCommittedRealm(RealmId realm)
        {
            return realm == RealmId.Stonehold ||
                   realm == RealmId.Eldergrove ||
                   realm == RealmId.Crownlands ||
                   realm == RealmId.Umbral;
        }

        private static bool TryResolveSpecial(
            SkillLoadoutSnapshot skills,
            out SkillLoadoutSlot special)
        {
            special = null;
            if (skills == null ||
                skills.Count != SkillLoadoutCatalog.RequiredSlotCount ||
                !skills.TryGetSlot(FirstSessionChampionStart.SpecialSkillSlot, out special))
            {
                return false;
            }

            return special.Identity == MvpSkillIdentity.RealmStrike;
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
