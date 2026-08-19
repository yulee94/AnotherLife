using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Production schema registry: the six-family MVP set plus the three
    /// flattened WIRE families. SKIP / unused catalogs are not registered.
    /// </summary>
    public static class GameDataProductionCatalogSchemas
    {
        private static readonly IReadOnlyList<string> orderedFamilies =
            new ReadOnlyCollection<string>(Concat(
                GameDataSixFamilySchemas.FamilyOrder,
                GameDataWireFamilySchemas.FamilyOrder));

        public static IReadOnlyList<string> FamilyOrder => orderedFamilies;

        public static GameDataCatalogSchemaRegistry CreateRegistry()
        {
            var schemas = new List<GameDataCatalogFamilySchema>();
            Append(schemas, GameDataSixFamilySchemas.CreateRegistry());
            Append(schemas, GameDataWireFamilySchemas.CreateRegistry());
            return new GameDataCatalogSchemaRegistry(schemas);
        }

        private static void Append(
            List<GameDataCatalogFamilySchema> target,
            GameDataCatalogSchemaRegistry registry)
        {
            for (var index = 0; index < registry.Schemas.Count; index++)
            {
                target.Add(registry.Schemas[index]);
            }
        }

        private static string[] Concat(
            IReadOnlyList<string> left,
            IReadOnlyList<string> right)
        {
            var combined = new string[left.Count + right.Count];
            for (var index = 0; index < left.Count; index++)
            {
                combined[index] = left[index];
            }

            for (var index = 0; index < right.Count; index++)
            {
                combined[left.Count + index] = right[index];
            }

            return combined;
        }
    }
}
