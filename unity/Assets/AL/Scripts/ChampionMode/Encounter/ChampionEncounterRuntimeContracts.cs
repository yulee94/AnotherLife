using System;

namespace AL.ChampionMode.Encounter
{
    public enum ChampionEncounterRuntimeStatus
    {
        Applied = 0,
        CatalogUnavailable = 1,
        InvalidInput = 2,
        HybridRejected = 3,
        InvalidDependency = 4,
        ApplicationRejected = 5
    }

    public sealed class ChampionEncounterRuntimePlan
    {
        internal ChampionEncounterRuntimePlan(
            ChampionEncounterRuntimeStatus status,
            string diagnosticCode,
            ChampionEncounterLoadReceipt receipt)
        {
            Status = status;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Receipt = receipt;
        }

        public ChampionEncounterRuntimeStatus Status { get; }
        public string DiagnosticCode { get; }
        public ChampionEncounterLoadReceipt Receipt { get; }
    }

    public interface IChampionEncounterRuntimeHost
    {
        bool TryBind(ChampionEncounterLoadReceipt receipt);
    }
}
