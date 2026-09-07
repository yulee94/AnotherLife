using System;
using System.Collections.Generic;

namespace AL.Data.Catalogs
{
    // Optional schema-2 extension. Absence means uninstalled, never legacy conversion.
    [Serializable]
    public sealed class OathmarkWalletState
    {
        public int Version;
        public string ProfileId;
        public string AccountId;
        public string WalletId;
        public string CurrencyId;
        public string PolicyHash;
        public long Balance;
        public long Revision;
        public List<OathmarkWalletReceipt> Receipts = new List<OathmarkWalletReceipt>();
    }

    [Serializable]
    public sealed class OathmarkWalletReceipt
    {
        public string OperationId;
        public string CorrelationId;
        public string RequestHash;
        public int Operation;
        public long Amount;
        public long BeforeBalance;
        public long AfterBalance;
        public long BeforeRevision;
        public long AfterRevision;
    }
}
