namespace AL.Core
{
    public static class ResourceRules
    {
        public static readonly ResourceType[] WalletResources =
        {
            ResourceType.Food,
            ResourceType.Wood,
            ResourceType.Stone,
            ResourceType.Gold,
            ResourceType.ManaStone,
            ResourceType.Ore,
            ResourceType.DeepOre,
            ResourceType.WorldSap,
            ResourceType.RoyalSigil,
            ResourceType.DarkCrystal
        };

        public static ResourceType GetRareResourceForRealm(RealmId realmId)
        {
            return realmId switch
            {
                RealmId.Stonehold => ResourceType.DeepOre,
                RealmId.Eldergrove => ResourceType.WorldSap,
                RealmId.Crownlands => ResourceType.RoyalSigil,
                RealmId.Umbral => ResourceType.DarkCrystal,
                _ => ResourceType.RoyalSigil
            };
        }

        public static bool IsRareResource(ResourceType resourceType)
        {
            return resourceType == ResourceType.DeepOre ||
                   resourceType == ResourceType.WorldSap ||
                   resourceType == ResourceType.RoyalSigil ||
                   resourceType == ResourceType.DarkCrystal;
        }
    }
}
