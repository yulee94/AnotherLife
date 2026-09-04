using System;
using AL.Core;
using AL.Core.Interfaces;

namespace AL.Services.Local
{
    /// <summary>
    /// Production research/troop catalog authority for the #165 containment slice.
    /// No research or troop definition is published here. Well-formed requests
    /// fail closed as CatalogUnavailable; malformed requests are CatalogInvalid.
    /// </summary>
    public static class ResearchTroopCatalogAuthority
    {
        public const string ResearchFamily = "research";
        public const string TroopsFamily = "troops";
        public const string ResearchUnavailableCode = "AL-RSCH-CATALOG-UNAVAILABLE";
        public const string ResearchInvalidCode = "AL-RSCH-CATALOG-INVALID";
        public const string TroopUnavailableCode = "AL-TRP-CATALOG-UNAVAILABLE";
        public const string TroopInvalidCode = "AL-TRP-CATALOG-INVALID";

        public static ResearchTroopCatalogQueryResult QueryResearch(string researchId)
        {
            if (string.IsNullOrWhiteSpace(researchId))
            {
                return new ResearchTroopCatalogQueryResult(
                    ResearchTroopCatalogStatus.CatalogInvalid,
                    ResearchFamily,
                    researchId ?? string.Empty,
                    ResearchInvalidCode);
            }

            return new ResearchTroopCatalogQueryResult(
                ResearchTroopCatalogStatus.CatalogUnavailable,
                ResearchFamily,
                researchId,
                ResearchUnavailableCode);
        }

        public static ResearchTroopCatalogQueryResult QueryTroop(TroopType type)
        {
            if (!Enum.IsDefined(typeof(TroopType), type))
            {
                return new ResearchTroopCatalogQueryResult(
                    ResearchTroopCatalogStatus.CatalogInvalid,
                    TroopsFamily,
                    ((int)type).ToString(),
                    TroopInvalidCode);
            }

            return new ResearchTroopCatalogQueryResult(
                ResearchTroopCatalogStatus.CatalogUnavailable,
                TroopsFamily,
                type.ToString(),
                TroopUnavailableCode);
        }

        public static ResearchTroopMutationResult RejectResearch(string researchId)
        {
            return Reject(QueryResearch(researchId));
        }

        public static ResearchTroopMutationResult RejectTroop(TroopType type)
        {
            return Reject(QueryTroop(type));
        }

        private static ResearchTroopMutationResult Reject(
            ResearchTroopCatalogQueryResult query)
        {
            return new ResearchTroopMutationResult(
                query.Status,
                false,
                false,
                query.DiagnosticCode,
                query.Family,
                query.RequestedId);
        }
    }
}
