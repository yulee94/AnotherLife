namespace AL.Core.Interfaces
{
    public enum ResearchTroopCatalogStatus
    {
        CatalogUnavailable = 0,
        CatalogInvalid = 1
    }

    public sealed class ResearchTroopCatalogQueryResult
    {
        public ResearchTroopCatalogQueryResult(
            ResearchTroopCatalogStatus status,
            string family,
            string requestedId,
            string diagnosticCode)
        {
            Status = status;
            Family = family ?? string.Empty;
            RequestedId = requestedId ?? string.Empty;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public ResearchTroopCatalogStatus Status { get; }
        public string Family { get; }
        public string RequestedId { get; }
        public string DiagnosticCode { get; }
        public bool HasRecord => false;
    }

    public sealed class ResearchTroopMutationResult
    {
        public ResearchTroopMutationResult(
            ResearchTroopCatalogStatus status,
            bool changed,
            bool persisted,
            string diagnosticCode,
            string family,
            string requestedId)
        {
            Status = status;
            Changed = changed;
            Persisted = persisted;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Family = family ?? string.Empty;
            RequestedId = requestedId ?? string.Empty;
        }

        public ResearchTroopCatalogStatus Status { get; }
        public bool Changed { get; }
        public bool Persisted { get; }
        public string DiagnosticCode { get; }
        public string Family { get; }
        public string RequestedId { get; }
    }
}
