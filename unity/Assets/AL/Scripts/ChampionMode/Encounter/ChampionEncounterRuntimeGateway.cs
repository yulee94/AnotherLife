using System;
using System.Collections.Generic;

namespace AL.ChampionMode.Encounter
{
    /// <summary>
    /// C3 runtime migration. The C2 load snapshot/receipt is the sole production
    /// load authority for loader/caster/combat/boss start. Invalid or hybrid
    /// input is typed and non-mutating. Persistence and rewards remain C4.
    /// </summary>
    public static class ChampionEncounterRuntimeGateway
    {
        public const string InvalidInputCode =
            "AL-CHAMPION-ENCOUNTER-RUNTIME-INPUT-INVALID";

        public static ChampionEncounterRuntimePlan Apply(
            ChampionEncounterLoadPlan loadPlan,
            IChampionEncounterRuntimeHost host)
        {
            if (loadPlan == null)
            {
                return Plan(ChampionEncounterRuntimeStatus.InvalidInput, InvalidInputCode);
            }

            if (loadPlan.Status == ChampionEncounterLoadStatus.CatalogUnavailable)
            {
                return Plan(
                    ChampionEncounterRuntimeStatus.CatalogUnavailable,
                    string.IsNullOrEmpty(loadPlan.DiagnosticCode)
                        ? ChampionEncounterLoadGateway.CatalogUnavailableCode
                        : loadPlan.DiagnosticCode);
            }

            if (loadPlan.Status == ChampionEncounterLoadStatus.InvalidSource &&
                string.Equals(
                    loadPlan.DiagnosticCode,
                    ChampionEncounterLoadGateway.HybridSourceCode,
                    StringComparison.Ordinal))
            {
                return Plan(
                    ChampionEncounterRuntimeStatus.HybridRejected,
                    ChampionEncounterLoadGateway.HybridSourceCode);
            }

            if (loadPlan.Status != ChampionEncounterLoadStatus.Loaded ||
                !ValidReceipt(loadPlan.Receipt))
            {
                return Plan(ChampionEncounterRuntimeStatus.InvalidInput, InvalidInputCode);
            }

            if (host == null)
            {
                return Plan(
                    ChampionEncounterRuntimeStatus.InvalidDependency,
                    ChampionEncounterLoadGateway.InvalidDependencyCode);
            }

            if (!host.TryBind(loadPlan.Receipt))
            {
                return Plan(
                    ChampionEncounterRuntimeStatus.ApplicationRejected,
                    ChampionEncounterLoadGateway.ApplicationRejectedCode);
            }

            return new ChampionEncounterRuntimePlan(
                ChampionEncounterRuntimeStatus.Applied,
                string.Empty,
                loadPlan.Receipt);
        }

        internal static bool ValidReceipt(ChampionEncounterLoadReceipt receipt)
        {
            return receipt != null &&
                   StableText(receipt.ApplicationId) &&
                   ChampionEncounterLoadGateway.IsCommittedValidRealm(receipt.RealmId) &&
                   StableText(receipt.ActorId) &&
                   StableText(receipt.CasterId) &&
                   StableText(receipt.BossId) &&
                   StableText(receipt.LoadoutId) &&
                   StableText(receipt.SourceFingerprint) &&
                   SlotsEqual(
                       receipt.SlotIds,
                       ChampionEncounterSourceSet.AuthoredWireSlotOrder);
        }

        private static bool SlotsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StableText(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static ChampionEncounterRuntimePlan Plan(
            ChampionEncounterRuntimeStatus status,
            string diagnosticCode)
        {
            return new ChampionEncounterRuntimePlan(status, diagnosticCode, null);
        }
    }
}
