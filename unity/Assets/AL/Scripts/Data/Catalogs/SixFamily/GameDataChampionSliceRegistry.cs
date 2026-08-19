using System;
using System.Collections.Generic;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Observed greybox champion overlay (subclass, stats, weapon styles) keyed by
    /// catalog champion id. Record identity lives in champions.v1.json; these fields
    /// are not yet in the six-family champion schema. Values are primitives so this
    /// assembly stays engine-free; LocalGameDataService maps them onto definitions.
    /// </summary>
    public static class GameDataChampionSliceRegistry
    {
        public readonly struct Overlay
        {
            public Overlay(
                string displayName,
                string subclassId,
                int maxHealth,
                int maxMana,
                int attack,
                int defense,
                int speed,
                int critRate,
                string weaponStyleId,
                string offhandStyleId)
            {
                DisplayName = displayName;
                SubclassId = subclassId;
                MaxHealth = maxHealth;
                MaxMana = maxMana;
                Attack = attack;
                Defense = defense;
                Speed = speed;
                CritRate = critRate;
                WeaponStyleId = weaponStyleId;
                OffhandStyleId = offhandStyleId;
            }

            public string DisplayName { get; }
            public string SubclassId { get; }
            public int MaxHealth { get; }
            public int MaxMana { get; }
            public int Attack { get; }
            public int Defense { get; }
            public int Speed { get; }
            public int CritRate { get; }
            public string WeaponStyleId { get; }
            public string OffhandStyleId { get; }
        }

        private static readonly Dictionary<string, Overlay> Overlays =
            new Dictionary<string, Overlay>(StringComparer.Ordinal)
            {
                ["champion_stonehold_vanguard"] = new Overlay(
                    "Bronn Ironhide", "Vanguard", 1250, 80, 55, 45, 8, 5, "greataxe", "towershield"),
                ["champion_eldergrove_archmage"] = new Overlay(
                    "Lyra Moonshadow", "Archmage", 820, 150, 78, 18, 10, 8, "staff", "tome"),
                ["champion_crownlands_sharpshooter"] = new Overlay(
                    "Aurelia Dawnblade", "Sharpshooter", 900, 110, 62, 26, 15, 20, "longbow", "quiver"),
                ["champion_umbral_shadowblade"] = new Overlay(
                    "Vex Nocturne", "Shadowblade", 850, 100, 72, 16, 22, 30, "twinblades", "shroud")
            };

        public static bool TryGet(string championId, out Overlay overlay)
        {
            return Overlays.TryGetValue(championId, out overlay);
        }
    }
}
