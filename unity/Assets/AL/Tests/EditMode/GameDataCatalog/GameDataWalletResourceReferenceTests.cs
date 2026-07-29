using System;
using System.Collections;
using System.Linq;
using AL.Core;
using AL.Data.Catalogs;
using NUnit.Framework;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class GameDataWalletResourceReferenceTests
    {
        private static readonly string[] StableIds =
        {
            "food",
            "wood",
            "stone",
            "gold",
            "mana_stone",
            "ore",
            "deep_ore",
            "world_sap",
            "royal_sigil",
            "dark_crystal"
        };

        private static readonly string[] EnumNames =
        {
            "Food",
            "Wood",
            "Stone",
            "Gold",
            "ManaStone",
            "Ore",
            "DeepOre",
            "WorldSap",
            "RoyalSigil",
            "DarkCrystal"
        };

        [Test]
        public void RegistryAndTypedRulesExposeOneExactReadOnlyAuthority()
        {
            Assert.AreEqual(1, GameDataWalletResourceReferences.Version);
            Assert.AreEqual(6, GameDataWalletResourceReferences.CoreResourceCount);
            Assert.AreEqual(10, GameDataWalletResourceReferences.Entries.Count);
            CollectionAssert.AreEqual(
                StableIds,
                GameDataWalletResourceReferences.StableIds.ToArray());
            CollectionAssert.AreEqual(
                StableIds,
                GameDataWalletResourceReferences.Entries
                    .Select(reference => reference.StableId)
                    .ToArray());

            var entries = (IList)GameDataWalletResourceReferences.Entries;
            var ids = (IList)GameDataWalletResourceReferences.StableIds;
            Assert.True(entries.IsReadOnly);
            Assert.True(ids.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => entries[0] = null);
            Assert.Throws<NotSupportedException>(() => ids[0] = "changed");

            for (var index = 0; index < StableIds.Length; index++)
            {
                var expectedType = (ResourceType)index;
                var expectedClassification =
                    index < GameDataWalletResourceReferences.CoreResourceCount
                        ? GameDataWalletResourceClassification.Core
                        : GameDataWalletResourceClassification.OptionalRare;
                var reference = GameDataWalletResourceReferences.Entries[index];

                Assert.AreEqual(index, reference.WalletIndex, StableIds[index]);
                Assert.AreEqual(StableIds[index], reference.StableId, StableIds[index]);
                Assert.AreEqual(EnumNames[index], reference.LegacyEnumName, StableIds[index]);
                Assert.AreEqual(index, reference.LegacyEnumValue, StableIds[index]);
                Assert.AreEqual(
                    expectedClassification,
                    reference.Classification,
                    StableIds[index]);
                Assert.AreEqual(expectedType, ResourceRules.WalletResources[index]);
                Assert.AreEqual(
                    index < GameDataWalletResourceReferences.CoreResourceCount,
                    ResourceRules.IsCoreResource(expectedType),
                    StableIds[index]);
                Assert.AreEqual(
                    index >= GameDataWalletResourceReferences.CoreResourceCount,
                    ResourceRules.IsRareResource(expectedType),
                    StableIds[index]);

                Assert.True(
                    GameDataWalletResourceReferences.TryGetByStableId(
                        StableIds[index],
                        out var byId),
                    StableIds[index]);
                Assert.AreSame(reference, byId, StableIds[index]);
                Assert.True(
                    GameDataWalletResourceReferences.TryGetByLegacyName(
                        EnumNames[index],
                        out var byName),
                    StableIds[index]);
                Assert.AreSame(reference, byName, StableIds[index]);
                Assert.True(
                    GameDataWalletResourceReferences.TryGetByLegacyValue(
                        index,
                        out var byValue),
                    StableIds[index]);
                Assert.AreSame(reference, byValue, StableIds[index]);

                Assert.True(
                    ResourceRules.TryGetResourceTypeByStableId(
                        StableIds[index],
                        out var resolvedType),
                    StableIds[index]);
                Assert.AreEqual(expectedType, resolvedType, StableIds[index]);
                Assert.True(
                    ResourceRules.TryGetStableId(expectedType, out var resolvedId),
                    StableIds[index]);
                Assert.AreEqual(StableIds[index], resolvedId, StableIds[index]);
            }
        }

        [Test]
        public void StableIdResolutionRejectsEveryUnapprovedIdentityShape()
        {
            foreach (var invalid in new[]
                     {
                         null,
                         string.Empty,
                         " ",
                         "Food",
                         "FOOD",
                         "food ",
                         " food",
                         "mana-stone",
                         "mana stone",
                         "ManaStone",
                         "ResourceType.ManaStone",
                         "unknown_resource"
                     })
            {
                Assert.False(
                    GameDataWalletResourceReferences.TryGetByStableId(
                        invalid,
                        out var catalogReference),
                    invalid ?? "<null>");
                Assert.IsNull(catalogReference, invalid ?? "<null>");
                Assert.False(
                    ResourceRules.TryGetResourceTypeByStableId(
                        invalid,
                        out _),
                    invalid ?? "<null>");
            }

            foreach (var unsupported in new[]
                     {
                         (ResourceType)(-1),
                         (ResourceType)10,
                         (ResourceType)9001
                     })
            {
                Assert.False(ResourceRules.TryGetStableId(unsupported, out var stableId));
                Assert.IsNull(stableId);
                Assert.False(
                    GameDataWalletResourceReferences.TryGetByLegacyValue(
                        (int)unsupported,
                        out var catalogReference));
                Assert.IsNull(catalogReference);
            }
        }

    }
}
