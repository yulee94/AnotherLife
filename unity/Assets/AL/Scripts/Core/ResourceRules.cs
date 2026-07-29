using System;
using System.Collections.Generic;
using AL.Data.Catalogs;

namespace AL.Core
{
    public static class ResourceRules
    {
        private const int CoreResourceCount = 6;

        private static readonly ResourceType[] WalletResourceValues =
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

        static ResourceRules()
        {
            if (GameDataWalletResourceReferences.Entries.Count != WalletResourceValues.Length ||
                GameDataWalletResourceReferences.CoreResourceCount != CoreResourceCount)
            {
                throw new InvalidOperationException(
                    "ResourceRules and the catalog resource-reference authority have different bounds.");
            }

            var unique = new HashSet<ResourceType>();
            for (int index = 0; index < WalletResourceValues.Length; index++)
            {
                ResourceType resourceType = WalletResourceValues[index];
                if (!Enum.IsDefined(typeof(ResourceType), resourceType) || !unique.Add(resourceType))
                {
                    throw new InvalidOperationException("ResourceRules wallet authority contains an undefined or duplicate resource.");
                }

                if (!TryGetWalletIndex(resourceType, out int mappedIndex) || mappedIndex != index)
                {
                    throw new InvalidOperationException("ResourceRules wallet index mapping drifted from its canonical order.");
                }

                GameDataWalletResourceReference reference =
                    GameDataWalletResourceReferences.Entries[index];
                bool expectedCore = index < CoreResourceCount;
                bool referenceCore =
                    reference.Classification == GameDataWalletResourceClassification.Core;
                bool referenceOptionalRare =
                    reference.Classification ==
                    GameDataWalletResourceClassification.OptionalRare;
                if (reference.WalletIndex != index ||
                    reference.LegacyEnumValue != (int)resourceType ||
                    !string.Equals(
                        reference.LegacyEnumName,
                        resourceType.ToString(),
                        StringComparison.Ordinal) ||
                    referenceCore != expectedCore ||
                    referenceCore != IsCoreResource(resourceType) ||
                    referenceOptionalRare != IsRareResource(resourceType))
                {
                    throw new InvalidOperationException(
                        "ResourceRules drifted from the catalog resource-reference authority.");
                }
            }

            WalletResources = Array.AsReadOnly(WalletResourceValues);
        }

        public static IReadOnlyList<ResourceType> WalletResources { get; }

        public static bool TryGetResourceTypeByStableId(
            string stableId,
            out ResourceType resourceType)
        {
            GameDataWalletResourceReference reference;
            if (!GameDataWalletResourceReferences.TryGetByStableId(stableId, out reference))
            {
                resourceType = default;
                return false;
            }

            resourceType = (ResourceType)reference.LegacyEnumValue;
            return TryGetWalletIndex(resourceType, out int walletIndex) &&
                   walletIndex == reference.WalletIndex &&
                   string.Equals(
                       resourceType.ToString(),
                       reference.LegacyEnumName,
                       StringComparison.Ordinal);
        }

        public static bool TryGetStableId(
            ResourceType resourceType,
            out string stableId)
        {
            if (!TryGetWalletIndex(resourceType, out int walletIndex))
            {
                stableId = null;
                return false;
            }

            GameDataWalletResourceReference reference =
                GameDataWalletResourceReferences.Entries[walletIndex];
            if (reference.LegacyEnumValue != (int)resourceType ||
                !string.Equals(
                    reference.LegacyEnumName,
                    resourceType.ToString(),
                    StringComparison.Ordinal))
            {
                stableId = null;
                return false;
            }

            stableId = reference.StableId;
            return true;
        }

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

        public static bool TryGetRareResourceForRealm(RealmId realmId, out ResourceType resourceType)
        {
            switch (realmId)
            {
                case RealmId.Stonehold:
                    resourceType = ResourceType.DeepOre;
                    return true;
                case RealmId.Eldergrove:
                    resourceType = ResourceType.WorldSap;
                    return true;
                case RealmId.Crownlands:
                    resourceType = ResourceType.RoyalSigil;
                    return true;
                case RealmId.Umbral:
                    resourceType = ResourceType.DarkCrystal;
                    return true;
                default:
                    resourceType = default;
                    return false;
            }
        }

        public static bool IsSupportedWalletResource(ResourceType resourceType) =>
            TryGetWalletIndex(resourceType, out _);

        public static bool IsCoreResource(ResourceType resourceType) =>
            TryGetWalletIndex(resourceType, out int index) && index < CoreResourceCount;

        public static bool IsRareResource(ResourceType resourceType)
        {
            return resourceType == ResourceType.DeepOre ||
                   resourceType == ResourceType.WorldSap ||
                   resourceType == ResourceType.RoyalSigil ||
                   resourceType == ResourceType.DarkCrystal;
        }

        internal static bool TryGetWalletIndex(ResourceType resourceType, out int index)
        {
            switch (resourceType)
            {
                case ResourceType.Food: index = 0; return true;
                case ResourceType.Wood: index = 1; return true;
                case ResourceType.Stone: index = 2; return true;
                case ResourceType.Gold: index = 3; return true;
                case ResourceType.ManaStone: index = 4; return true;
                case ResourceType.Ore: index = 5; return true;
                case ResourceType.DeepOre: index = 6; return true;
                case ResourceType.WorldSap: index = 7; return true;
                case ResourceType.RoyalSigil: index = 8; return true;
                case ResourceType.DarkCrystal: index = 9; return true;
                default:
                    index = -1;
                    return false;
            }
        }
    }
}
