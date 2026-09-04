using System.Collections.Generic;
using AL.Core;

namespace AL.ChampionMode.Encounter
{
    /// <summary>
    /// Production Champion encounter start/load entry. Binds the live #183
    /// Champion/skill source set, requires a committed valid realm, and applies
    /// no save, reward, or combat-result mutation. C3 consumes the resulting
    /// snapshot as the sole production load authority for runtime start.
    /// </summary>
    public static class ChampionEncounterProductionLoadPath
    {
        public const string ProductionEncounterId = "champion.encounter.production";

        public static ChampionEncounterLoadPlan StartFromCommittedRealm(RealmId realmId)
        {
            return StartFromCommittedRealm(
                realmId,
                NoMutationApplication.Instance,
                new List<ChampionEncounterLoadReceipt>());
        }

        public static ChampionEncounterLoadPlan StartFromCommittedRealm(
            RealmId realmId,
            IChampionEncounterApplication application,
            IList<ChampionEncounterLoadReceipt> receipts)
        {
            string canonicalRealm = CanonicalRealm(realmId);
            var request = new ChampionEncounterLoadRequest(
                ProductionEncounterId,
                canonicalRealm,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                ChampionEncounterSourceSet.CurrentSourceSetVersion,
                ChampionEncounterSourceSet.CurrentSourceSetSha256,
                ChampionEncounterSourceSet.AuthoredWireSlotOrder);
            return ChampionEncounterLoadGateway.Start(
                request,
                ChampionEncounterSourceSet.CurrentSixFamilyAuthority(),
                application,
                receipts);
        }

        private static string CanonicalRealm(RealmId realmId)
        {
            switch (realmId)
            {
                case RealmId.Stonehold:
                    return "stonehold";
                case RealmId.Eldergrove:
                    return "eldergrove";
                case RealmId.Crownlands:
                    return "crownlands";
                case RealmId.Umbral:
                    return "umbral";
                default:
                    return string.Empty;
            }
        }

        private sealed class NoMutationApplication : IChampionEncounterApplication
        {
            internal static readonly NoMutationApplication Instance =
                new NoMutationApplication();

            public bool TryApply(ChampionEncounterLoadSnapshot snapshot)
            {
                return false;
            }
        }
    }
}
