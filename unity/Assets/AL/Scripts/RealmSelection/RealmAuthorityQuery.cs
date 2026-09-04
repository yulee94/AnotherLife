using AL.Core;

namespace AL.RealmSelection
{
    public enum RealmAuthorityQueryStatus
    {
        FoundValid = 0,
        UnknownId = 1,
        UnavailableCatalog = 2,
        InvalidCatalog = 3,
        UnsupportedVersion = 4,
        NotPlayable = 5
    }

    public readonly struct RealmAuthorityQueryResult
    {
        public RealmAuthorityQueryResult(
            RealmAuthorityQueryStatus status,
            RealmId realmId,
            RealmCatalogEntry entry,
            string technicalCode)
        {
            Status = status;
            RealmId = realmId;
            Entry = entry;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public RealmAuthorityQueryStatus Status { get; }
        public RealmId RealmId { get; }
        public RealmCatalogEntry Entry { get; }
        public string TechnicalCode { get; }
        public bool IsFoundValid =>
            Status == RealmAuthorityQueryStatus.FoundValid &&
            Entry != null &&
            RealmId != RealmId.None;
    }

    public static class RealmAuthorityQuery
    {
        public static RealmAuthorityQueryResult Evaluate(RealmCatalogSnapshot catalog, RealmId id)
        {
            if (catalog == null)
            {
                return new RealmAuthorityQueryResult(
                    RealmAuthorityQueryStatus.UnavailableCatalog,
                    id,
                    null,
                    "AL-REALM-CATALOG-UNAVAILABLE");
            }

            if (string.IsNullOrEmpty(catalog.Version))
            {
                return new RealmAuthorityQueryResult(
                    RealmAuthorityQueryStatus.InvalidCatalog,
                    id,
                    null,
                    "AL-REALM-CATALOG-INVALID");
            }

            if (!string.Equals(catalog.Version, RealmCatalogRuntime.SupportedVersion, System.StringComparison.Ordinal))
            {
                return new RealmAuthorityQueryResult(
                    RealmAuthorityQueryStatus.UnsupportedVersion,
                    id,
                    null,
                    "AL-REALM-CATALOG-UNSUPPORTED");
            }

            if (id == RealmId.None || !System.Enum.IsDefined(typeof(RealmId), id))
            {
                return new RealmAuthorityQueryResult(
                    RealmAuthorityQueryStatus.NotPlayable,
                    id,
                    null,
                    "AL-REALM-REQUEST-INVALID");
            }

            RealmCatalogEntry entry;
            if (!catalog.TryGet(id, out entry) || entry == null)
            {
                return new RealmAuthorityQueryResult(
                    RealmAuthorityQueryStatus.UnknownId,
                    id,
                    null,
                    "AL-REALM-DEFINITION-UNAVAILABLE");
            }

            return new RealmAuthorityQueryResult(
                RealmAuthorityQueryStatus.FoundValid,
                id,
                entry,
                "AL-REALM-AUTHORITY-FOUND");
        }
    }
}
