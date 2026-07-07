namespace AL.Core
{
    public enum RealmId
    {
        None = 0,
        Stonehold = 1,    // Dwarves
        Eldergrove = 2,   // Elves
        Crownlands = 3,   // Humans
        Umbral = 4        // Dark Elves
    }

    public enum ResourceType
    {
        Food,
        Wood,
        Stone,
        Gold
    }

    public enum ClassFamily
    {
        Warrior,
        Mage,
        Ranger,
        Assassin
    }

    public enum SubclassId
    {
        None,
        Vanguard,
        Guardian,
        Berserker,
        Pyromancer,
        Cryomancer,
        Archmage,
        Sharpshooter,
        Stalker,
        Beastmaster,
        Shadowblade,
        Infiltrator,
        Nightstalker
    }

    public enum TroopType
    {
        Infantry,
        Cavalry,
        Ranged,
        Siege
    }

    public enum EquipmentSlot
    {
        Head,
        Chest,
        Legs,
        MainHand,
        OffHand,
        Trinket
    }

    public enum BattleType
    {
        PvE,
        PvP,
        Boss,
        Warzone
    }

    public enum AutoMode
    {
        Manual,
        SemiAuto,
        FullAuto
    }

    public enum SkillTargetType
    {
        Single,
        AoE,
        Self,
        Ally,
        Enemy
    }

    public enum ModifierType
    {
        Flat,
        Percent
    }

    public enum StatType
    {
        Health,
        Attack,
        Defense,
        Speed,
        CritRate,
        CritDamage
    }

    public enum QuestType
    {
        BuildBuilding,
        TrainTroops,
        CaptureTerritory,
        ResearchTech,
        WinBattle
    }
}
