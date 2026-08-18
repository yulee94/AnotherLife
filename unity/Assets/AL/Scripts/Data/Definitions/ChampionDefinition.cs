using System;
using UnityEngine;
using AL.Core;

namespace AL.Data.Definitions
{
    [CreateAssetMenu(fileName = "New Champion", menuName = "AL/Data/Champion")]
    public class ChampionDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public RealmId Realm;
        public ClassFamily Family;
        public SubclassId Subclass;
        public Sprite Portrait;
        public SkillDefinition[] BaseSkills;
        public ChampionBaseStats BaseStats = new ChampionBaseStats();
        public string WeaponStyleId = "sword";
        public string OffhandStyleId = "shield";
    }

    /// <summary>
    /// Greybox champion base stats carried by a hardcoded LocalGameDataService archetype.
    /// Plain serializable values (not authority micros) so the character creation and legacy
    /// combat surfaces can share one source without catalog/save authority.
    /// </summary>
    [Serializable]
    public class ChampionBaseStats
    {
        public int MaxHealth = 1000;
        public int MaxMana = 100;
        public int Attack = 50;
        public int Defense = 30;
        public int Speed = 10;
        public int CritRate = 5;
    }
}
