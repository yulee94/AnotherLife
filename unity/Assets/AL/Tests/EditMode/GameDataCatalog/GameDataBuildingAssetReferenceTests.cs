using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AL.Data.Catalogs;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class GameDataBuildingAssetReferenceTests
    {
        private static readonly string[] StableIds =
        {
            "town_hall", "farm", "lumber_mill", "quarry", "gold_mine",
            "barracks", "academy", "market", "storehouse", "forge",
            "stable", "workshop", "embassy", "wall", "watchtower"
        };

        private static readonly string[] LegacyBuildingIds =
        {
            "TownHall", "Farm", "LumberMill", "Quarry", "GoldMine",
            "Barracks", "Academy", "Market", "Storehouse", "Forge",
            "Stable", "Workshop", "Embassy", "Wall", "Watchtower"
        };

        [Test]
        public void RegistryPublishesExactImmutableAtlasRelations()
        {
            Assert.AreEqual(1, GameDataBuildingAssetReferences.Version);
            Assert.AreEqual(15, GameDataBuildingAssetReferences.BuildingCount);
            Assert.AreEqual(5, GameDataBuildingAssetReferences.AtlasColumnCount);
            Assert.AreEqual(3, GameDataBuildingAssetReferences.AtlasRowCount);
            Assert.AreEqual(1536, GameDataBuildingAssetReferences.AtlasPixelWidth);
            Assert.AreEqual(1024, GameDataBuildingAssetReferences.AtlasPixelHeight);
            Assert.AreEqual(
                StableIds.Length,
                GameDataBuildingAssetReferences.Entries.Count);
            Assert.AreEqual(
                StableIds.Length,
                GameDataBuildingAssetReferences.AssetReferences.Count);

            for (var index = 0; index < StableIds.Length; index++)
            {
                var reference =
                    GameDataBuildingAssetReferences.Entries[index];
                Assert.AreEqual(index, reference.Order, StableIds[index]);
                Assert.AreEqual(StableIds[index], reference.StableId);
                Assert.AreEqual(
                    LegacyBuildingIds[index],
                    reference.LegacyBuildingId);
                Assert.AreEqual(StableIds[index], reference.AtlasFragment);
                Assert.AreEqual(index % 5, reference.Column);
                Assert.AreEqual(index / 5, reference.Row);
                Assert.AreEqual(
                    GameDataBuildingAssetReferences.AtlasAssetPath +
                    "#" +
                    StableIds[index],
                    reference.AssetReference);
                Assert.True(
                    GameDataBuildingAssetReferences.TryGetByStableId(
                        StableIds[index],
                        out var resolved));
                Assert.AreSame(reference, resolved);
                Assert.True(
                    GameDataBuildingAssetReferences
                        .IsApprovedBuildingAssetRelation(
                            StableIds[index],
                            LegacyBuildingIds[index],
                            reference.AssetReference));
            }

            foreach (var list in new IList[]
                     {
                         (IList)GameDataBuildingAssetReferences.Entries,
                         (IList)GameDataBuildingAssetReferences.AssetReferences
                     })
            {
                Assert.True(list.IsReadOnly);
                Assert.Throws<NotSupportedException>(() => list[0] = null);
            }

            Assert.AreEqual(
                StableIds.Length,
                GameDataBuildingAssetReferences.AssetReferences
                    .Distinct(StringComparer.Ordinal)
                    .Count());
        }

        [Test]
        public void ExactResolversRejectNormalizationAndSwappedRelations()
        {
            foreach (var invalid in new[]
                     {
                         null,
                         string.Empty,
                         " ",
                         "TownHall",
                         "townhall",
                         "TOWN_HALL",
                         "town-hall",
                         " town_hall",
                         "town_hall ",
                         "ManaShrine",
                         "Mine",
                         "unknown_building"
                     })
            {
                Assert.False(
                    GameDataBuildingAssetReferences.TryGetByStableId(
                        invalid,
                        out var reference),
                    invalid ?? "<null>");
                Assert.IsNull(reference, invalid ?? "<null>");
            }

            Assert.False(
                GameDataBuildingAssetReferences
                    .IsApprovedBuildingAssetRelation(
                        StableIds[0],
                        LegacyBuildingIds[1],
                        GameDataBuildingAssetReferences
                            .AssetReferences[0]));
            Assert.False(
                GameDataBuildingAssetReferences
                    .IsApprovedBuildingAssetRelation(
                        StableIds[0],
                        LegacyBuildingIds[0],
                        GameDataBuildingAssetReferences
                            .AssetReferences[1]));
            Assert.False(
                GameDataBuildingAssetReferences
                    .IsApprovedBuildingAssetRelation(
                        StableIds[0],
                        LegacyBuildingIds[0],
                        GameDataBuildingAssetReferences
                            .AssetReferences[0]
                            .Replace("#town_hall", "#TownHall")));
        }

        [Test]
        public void AtlasCellsCoverTheExactImageWithoutGapsOrOverflow()
        {
            for (var row = 0; row < 3; row++)
            {
                var cells = GameDataBuildingAssetReferences.Entries
                    .Where(reference => reference.Row == row)
                    .OrderBy(reference => reference.Column)
                    .ToArray();
                Assert.AreEqual(5, cells.Length);
                Assert.AreEqual(0, cells[0].PixelX);
                Assert.AreEqual(
                    GameDataBuildingAssetReferences.AtlasPixelWidth,
                    cells.Sum(reference => reference.PixelWidth));
                for (var column = 1; column < cells.Length; column++)
                {
                    Assert.AreEqual(
                        cells[column - 1].PixelX +
                        cells[column - 1].PixelWidth,
                        cells[column].PixelX);
                }

                foreach (var cell in cells)
                {
                    Assert.Greater(cell.PixelWidth, 0);
                    Assert.Greater(cell.PixelHeight, 0);
                    Assert.LessOrEqual(
                        cell.PixelX + cell.PixelWidth,
                        GameDataBuildingAssetReferences.AtlasPixelWidth);
                    Assert.LessOrEqual(
                        cell.PixelYFromTop + cell.PixelHeight,
                        GameDataBuildingAssetReferences.AtlasPixelHeight);
                }
            }

            Assert.AreEqual(0, GameDataBuildingAssetReferences.Entries[0].PixelYFromTop);
            Assert.AreEqual(341, GameDataBuildingAssetReferences.Entries[5].PixelYFromTop);
            Assert.AreEqual(683, GameDataBuildingAssetReferences.Entries[10].PixelYFromTop);
            Assert.AreEqual(
                GameDataBuildingAssetReferences.AtlasPixelHeight,
                GameDataBuildingAssetReferences.Entries[10].PixelYFromTop +
                GameDataBuildingAssetReferences.Entries[10].PixelHeight);
        }

        [Test]
        public void PinnedAtlasMatchesCommittedGuidDimensionsImportAndRawBytes()
        {
            Assert.AreEqual(
                GameDataBuildingAssetReferences.AtlasGuid,
                AssetDatabase.AssetPathToGUID(
                    GameDataBuildingAssetReferences.AtlasAssetPath));

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                GameDataBuildingAssetReferences.AtlasAssetPath);
            Assert.NotNull(texture);
            Assert.AreEqual(
                GameDataBuildingAssetReferences.AtlasPixelWidth,
                texture.width);
            Assert.AreEqual(
                GameDataBuildingAssetReferences.AtlasPixelHeight,
                texture.height);

            var importer = AssetImporter.GetAtPath(
                GameDataBuildingAssetReferences.AtlasAssetPath) as
                TextureImporter;
            Assert.NotNull(importer);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
            Assert.False(importer.mipmapEnabled);
            Assert.AreEqual(2048, importer.maxTextureSize);

            var projectRoot = Directory.GetParent(Application.dataPath);
            Assert.NotNull(projectRoot);
            var absolutePath = Path.GetFullPath(
                Path.Combine(
                    projectRoot.FullName,
                    GameDataBuildingAssetReferences.AtlasAssetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            Assert.True(File.Exists(absolutePath));
            Assert.AreEqual(
                GameDataBuildingAssetReferences.AtlasSha256,
                Sha256(File.ReadAllBytes(absolutePath)));
        }

        private static string Sha256(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
            {
                return string.Concat(
                    algorithm.ComputeHash(bytes)
                        .Select(value => value.ToString("x2")));
            }
        }
    }
}
