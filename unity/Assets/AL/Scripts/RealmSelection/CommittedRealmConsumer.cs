using System;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Runtime;

namespace AL.RealmSelection
{
    public readonly struct CommittedRealmAuthority
    {
        public CommittedRealmAuthority(
            RealmId realmId,
            RealmCatalogEntry entry,
            string transactionId,
            string eventId)
        {
            RealmId = realmId;
            Entry = entry;
            TransactionId = transactionId ?? string.Empty;
            EventId = eventId ?? string.Empty;
        }

        public RealmId RealmId { get; }
        public RealmCatalogEntry Entry { get; }
        public string CatalogId => Entry != null ? Entry.Id : string.Empty;
        public string TransactionId { get; }
        public string EventId { get; }
        public IReadOnlyList<string> RealmGemIds =>
            Entry != null ? Entry.RealmGemIds : Array.Empty<string>();
        public bool IsAvailable =>
            Entry != null &&
            RealmId != RealmId.None &&
            Enum.IsDefined(typeof(RealmId), RealmId);
    }

    public static class CommittedRealmConsumer
    {
        public static bool TryResolve(
            RealmIdentitySnapshot identity,
            RealmSelectionAuthorityState receipt,
            RealmCatalogSnapshot catalog,
            out CommittedRealmAuthority authority)
        {
            authority = default;
            if (!identity.IsCommittedValid)
            {
                return false;
            }

            RealmAuthorityQueryResult query = RealmAuthorityQuery.Evaluate(catalog, identity.RealmId);
            if (!query.IsFoundValid)
            {
                return false;
            }

            if (receipt == null ||
                !receipt.Committed ||
                receipt.SelectedRealm != (int)identity.RealmId ||
                string.IsNullOrEmpty(receipt.TransactionId) ||
                string.IsNullOrEmpty(receipt.ReceiptFingerprint))
            {
                return false;
            }

            if (!string.Equals(
                    receipt.ReceiptFingerprint,
                    RealmSelectionAuthority.ComputeReceiptFingerprint(
                        receipt.ProfileId,
                        identity.RealmId,
                        receipt.TransactionId,
                        receipt.CorrelationId,
                        receipt.OperationId,
                        receipt.EventId,
                        receipt.Provenance,
                        receipt.Revision),
                    StringComparison.Ordinal))
            {
                return false;
            }

            authority = new CommittedRealmAuthority(
                identity.RealmId,
                query.Entry,
                receipt.TransactionId,
                receipt.EventId);
            return true;
        }

        public static bool TryResolveFromSave(
            SaveGameData save,
            RealmCatalogSnapshot catalog,
            out CommittedRealmAuthority authority)
        {
            authority = default;
            if (save == null)
            {
                return false;
            }

            var identity = new RealmIdentitySnapshot(
                RealmIdentityStatus.CommittedValid,
                save.SelectedRealm,
                catalog != null ? catalog.Version : string.Empty,
                "AL-REALM-COMMITTED-VALID");
            return TryResolve(identity, save.RealmSelection, catalog, out authority);
        }
    }
}
