using System;
using System.Collections.Generic;

namespace AL.Data.Catalogs
{
    public sealed class GameDataBuildingAssetReference
    {
        internal GameDataBuildingAssetReference(
            int order,
            string stableId,
            string legacyBuildingId,
            string atlasFragment,
            int column,
            int row,
            int pixelX,
            int pixelYFromTop,
            int pixelWidth,
            int pixelHeight)
        {
            Order = order;
            StableId = stableId;
            LegacyBuildingId = legacyBuildingId;
            AtlasFragment = atlasFragment;
            AssetReference =
                GameDataBuildingAssetReferences.AtlasAssetPath +
                "#" +
                atlasFragment;
            Column = column;
            Row = row;
            PixelX = pixelX;
            PixelYFromTop = pixelYFromTop;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
        }

        public int Order { get; }
        public string StableId { get; }
        public string LegacyBuildingId { get; }
        public string AtlasFragment { get; }
        public string AssetReference { get; }
        public int Column { get; }
        public int Row { get; }
        public int PixelX { get; }
        public int PixelYFromTop { get; }
        public int PixelWidth { get; }
        public int PixelHeight { get; }
    }

    /// <summary>
    /// Exact non-published building icon-atlas authority accepted from Phase C4D.
    /// Fragment references identify reviewed atlas cells without changing the
    /// realm-specific Town Hall/Workshop production-model catalog.
    /// </summary>
    public static class GameDataBuildingAssetReferences
    {
        public const int Version = 1;
        public const int BuildingCount = 15;
        public const int AtlasColumnCount = 5;
        public const int AtlasRowCount = 3;
        public const int AtlasPixelWidth = 1536;
        public const int AtlasPixelHeight = 1024;
        public const string AtlasAssetPath =
            "Assets/AL/Art/Buildings/RuntimeExports/" +
            "S_Building_Icon_Atlas_1536x1024_v001.png";
        public const string AtlasGuid =
            "8cfa4b19fc1e4475873c4ea7560dc9ad";
        public const string AtlasSha256 =
            "874bba1c9fa9ba8435dcf61b29eca2786c049e0abf7d899680011a22e481b3a8";

        private static readonly IReadOnlyList<GameDataBuildingAssetReference>
            entries;
        private static readonly IReadOnlyList<string> assetReferences;
        private static readonly Dictionary<string, GameDataBuildingAssetReference>
            entriesByStableId;

        static GameDataBuildingAssetReferences()
        {
            var mutableEntries = new[]
            {
                Cell(0, "town_hall", "TownHall", 0, 0, 0, 0, 307, 341),
                Cell(1, "farm", "Farm", 1, 0, 307, 0, 307, 341),
                Cell(2, "lumber_mill", "LumberMill", 2, 0, 614, 0, 308, 341),
                Cell(3, "quarry", "Quarry", 3, 0, 922, 0, 307, 341),
                Cell(4, "gold_mine", "GoldMine", 4, 0, 1229, 0, 307, 341),
                Cell(5, "barracks", "Barracks", 0, 1, 0, 341, 307, 342),
                Cell(6, "academy", "Academy", 1, 1, 307, 341, 307, 342),
                Cell(7, "market", "Market", 2, 1, 614, 341, 308, 342),
                Cell(8, "storehouse", "Storehouse", 3, 1, 922, 341, 307, 342),
                Cell(9, "forge", "Forge", 4, 1, 1229, 341, 307, 342),
                Cell(10, "stable", "Stable", 0, 2, 0, 683, 307, 341),
                Cell(11, "workshop", "Workshop", 1, 2, 307, 683, 307, 341),
                Cell(12, "embassy", "Embassy", 2, 2, 614, 683, 308, 341),
                Cell(13, "wall", "Wall", 3, 2, 922, 683, 307, 341),
                Cell(14, "watchtower", "Watchtower", 4, 2, 1229, 683, 307, 341)
            };

            if (mutableEntries.Length != BuildingCount ||
                mutableEntries.Length !=
                GameDataBuildingProgressionRegistry.BuildingCount)
            {
                throw new InvalidOperationException(
                    "The building asset authority must cover all reviewed buildings.");
            }

            entriesByStableId =
                new Dictionary<string, GameDataBuildingAssetReference>(
                    StringComparer.Ordinal);
            var uniqueReferences =
                new HashSet<string>(StringComparer.Ordinal);
            var mutableAssetReferences =
                new string[mutableEntries.Length];

            for (var index = 0; index < mutableEntries.Length; index++)
            {
                var entry = mutableEntries[index];
                var progression =
                    GameDataBuildingProgressionRegistry.Entries[index];
                if (entry.Order != index ||
                    entry.Column != index % AtlasColumnCount ||
                    entry.Row != index / AtlasColumnCount ||
                    !string.Equals(
                        entry.StableId,
                        progression.StableId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        entry.LegacyBuildingId,
                        progression.LegacyBuildingId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        entry.AtlasFragment,
                        entry.StableId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        entry.AssetReference,
                        AtlasAssetPath + "#" + entry.StableId,
                        StringComparison.Ordinal) ||
                    entry.PixelX < 0 ||
                    entry.PixelYFromTop < 0 ||
                    entry.PixelWidth <= 0 ||
                    entry.PixelHeight <= 0 ||
                    entry.PixelX + entry.PixelWidth > AtlasPixelWidth ||
                    entry.PixelYFromTop + entry.PixelHeight > AtlasPixelHeight ||
                    !entriesByStableId.TryAdd(entry.StableId, entry) ||
                    !uniqueReferences.Add(entry.AssetReference))
                {
                    throw new InvalidOperationException(
                        "The building asset authority contains an invalid relation.");
                }

                mutableAssetReferences[index] = entry.AssetReference;
            }

            ValidateCompleteAtlasCoverage(mutableEntries);
            entries = ImmutableCollections.Freeze(mutableEntries);
            assetReferences =
                ImmutableCollections.Freeze(mutableAssetReferences);
        }

        public static IReadOnlyList<GameDataBuildingAssetReference> Entries =>
            entries;
        public static IReadOnlyList<string> AssetReferences =>
            assetReferences;

        public static bool TryGetByStableId(
            string stableId,
            out GameDataBuildingAssetReference reference)
        {
            return entriesByStableId.TryGetValue(
                stableId ?? string.Empty,
                out reference);
        }

        public static bool IsApprovedBuildingAssetRelation(
            string stableId,
            string legacyBuildingId,
            string assetReference)
        {
            GameDataBuildingAssetReference reference;
            return TryGetByStableId(stableId, out reference) &&
                   string.Equals(
                       reference.LegacyBuildingId,
                       legacyBuildingId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       reference.AssetReference,
                       assetReference,
                       StringComparison.Ordinal);
        }

        private static GameDataBuildingAssetReference Cell(
            int order,
            string stableId,
            string legacyBuildingId,
            int column,
            int row,
            int pixelX,
            int pixelYFromTop,
            int pixelWidth,
            int pixelHeight)
        {
            return new GameDataBuildingAssetReference(
                order,
                stableId,
                legacyBuildingId,
                stableId,
                column,
                row,
                pixelX,
                pixelYFromTop,
                pixelWidth,
                pixelHeight);
        }

        private static void ValidateCompleteAtlasCoverage(
            IReadOnlyList<GameDataBuildingAssetReference> references)
        {
            for (var row = 0; row < AtlasRowCount; row++)
            {
                var expectedX = 0;
                var expectedY = row == 0 ? 0 : row == 1 ? 341 : 683;
                var expectedHeight = row == 1 ? 342 : 341;
                for (var column = 0; column < AtlasColumnCount; column++)
                {
                    var reference =
                        references[row * AtlasColumnCount + column];
                    if (reference.PixelX != expectedX ||
                        reference.PixelYFromTop != expectedY ||
                        reference.PixelHeight != expectedHeight)
                    {
                        throw new InvalidOperationException(
                            "The building icon atlas cells must be contiguous.");
                    }

                    expectedX += reference.PixelWidth;
                }

                if (expectedX != AtlasPixelWidth)
                {
                    throw new InvalidOperationException(
                        "The building icon atlas row must span the full width.");
                }
            }
        }
    }
}
