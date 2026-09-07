using System;
using System.Collections.Generic;

namespace AL.Data.Catalogs
{
    public static class OathmarkWalletValidation
    {
        public static bool IsId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 160) return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' ||
                    c == '_' || c == '-' || c == '.' || c == ':')) return false;
            return true;
        }
        public static bool IsHash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
            return true;
        }
        public static bool IsEmpty(OathmarkWalletState wallet) => wallet == null ||
            wallet.Version == 0 && string.IsNullOrEmpty(wallet.ProfileId) && string.IsNullOrEmpty(wallet.AccountId) &&
            string.IsNullOrEmpty(wallet.WalletId) && string.IsNullOrEmpty(wallet.CurrencyId) &&
            string.IsNullOrEmpty(wallet.PolicyHash) && wallet.Balance == 0 && wallet.Revision == 0 &&
            (wallet.Receipts == null || wallet.Receipts.Count == 0);

        public static bool IsValid(OathmarkWalletState wallet, string profileId)
        {
            if (IsEmpty(wallet)) return true;
            if (wallet.Version != 1 || wallet.ProfileId != profileId || wallet.AccountId != profileId ||
                !IsId(profileId) || !IsId(wallet.CurrencyId) || wallet.WalletId != profileId + ":" + wallet.CurrencyId ||
                !IsHash(wallet.PolicyHash) || wallet.Balance < 0 || wallet.Revision < 1 ||
                wallet.Receipts == null || wallet.Receipts.Count < 1 || wallet.Receipts.Count > 2048 ||
                wallet.Revision != wallet.Receipts.Count) return false;
            var operations = new HashSet<string>(StringComparer.Ordinal);
            var correlations = new HashSet<string>(StringComparer.Ordinal);
            long balance = 0, revision = 0;
            foreach (var r in wallet.Receipts)
            {
                if (r == null || !IsId(r.OperationId) || !operations.Add(r.OperationId) ||
                    !IsId(r.CorrelationId) || !correlations.Add(r.CorrelationId) || !IsHash(r.RequestHash) ||
                    r.Amount < 0 || r.BeforeBalance != balance || r.BeforeRevision != revision ||
                    r.AfterRevision != revision + 1 || r.AfterBalance < 0) return false;
                try
                {
                    switch (r.Operation)
                    {
                        case 1: if (revision != 0 || r.Amount != 0) return false; break;
                        case 2: if (revision == 0 || r.Amount <= 0) return false; balance = checked(balance + r.Amount); break;
                        case 3: if (revision == 0 || r.Amount <= 0) return false; balance = checked(balance - r.Amount); break;
                        default: return false;
                    }
                }
                catch (OverflowException) { return false; }
                if (balance != r.AfterBalance || balance < 0) return false;
                revision = r.AfterRevision;
            }
            return wallet.Balance == balance && wallet.Revision == revision;
        }

        internal static OathmarkWalletState Read(StrictJsonObject obj)
        {
            WalletJson.Exact(obj, "Version", "ProfileId", "AccountId", "WalletId", "CurrencyId", "PolicyHash", "Balance", "Revision", "Receipts");
            var wallet = new OathmarkWalletState
            {
                Version = checked((int)WalletJson.Long(obj, "Version")),
                ProfileId = WalletJson.Text(obj, "ProfileId"), AccountId = WalletJson.Text(obj, "AccountId"),
                WalletId = WalletJson.Text(obj, "WalletId"), CurrencyId = WalletJson.Text(obj, "CurrencyId"),
                PolicyHash = WalletJson.Text(obj, "PolicyHash"), Balance = WalletJson.Long(obj, "Balance"),
                Revision = WalletJson.Long(obj, "Revision")
            };
            var rows = WalletJson.Get(obj, "Receipts") as StrictJsonArray;
            if (rows == null || rows.Items.Count > 2048) throw new FormatException();
            foreach (var item in rows.Items)
            {
                var r = WalletJson.Object(item);
                WalletJson.Exact(r, "OperationId", "CorrelationId", "RequestHash", "Operation", "Amount", "BeforeBalance", "AfterBalance", "BeforeRevision", "AfterRevision");
                wallet.Receipts.Add(new OathmarkWalletReceipt
                {
                    OperationId = WalletJson.Text(r, "OperationId"), CorrelationId = WalletJson.Text(r, "CorrelationId"),
                    RequestHash = WalletJson.Text(r, "RequestHash"), Operation = checked((int)WalletJson.Long(r, "Operation")),
                    Amount = WalletJson.Long(r, "Amount"), BeforeBalance = WalletJson.Long(r, "BeforeBalance"),
                    AfterBalance = WalletJson.Long(r, "AfterBalance"), BeforeRevision = WalletJson.Long(r, "BeforeRevision"),
                    AfterRevision = WalletJson.Long(r, "AfterRevision")
                });
            }
            return wallet;
        }
    }
}
