using AL.Core.SaveAuthority;
using AL.Data.Catalogs;

namespace AL.Services.Local
{
    internal enum OathmarkWalletOperation { Inspect = 0, Install = 1, Credit = 2, Debit = 3 }
    internal enum OathmarkWalletStatus
    {
        Inspected, Committed, Replayed, InvalidRequest, WrongProfile, ForbiddenWallet,
        Stale, Conflict, InsufficientFunds, Overflow, ReadOnly, SaveUncertain,
        PreviousPreserved, InvalidPolicy, Malformed, Uninstalled, ReceiptCapacity
    }

    // Infrastructure only: no earning source or player-facing grant API is installed.
    internal sealed class OathmarkWalletRequest
    {
        internal OathmarkWalletRequest(ProfileAuthorityExpectation authority, string accountId,
            string walletId, string currencyId, string operationId, string correlationId,
            OathmarkWalletOperation operation, long amount, long expectedRevision, string policyHash)
        {
            Authority = authority;
            AccountId = accountId;
            WalletId = walletId;
            CurrencyId = currencyId;
            OperationId = operationId;
            CorrelationId = correlationId;
            Operation = operation;
            Amount = amount;
            ExpectedRevision = expectedRevision;
            PolicyHash = policyHash;
        }
        internal ProfileAuthorityExpectation Authority { get; }
        internal string AccountId { get; }
        internal string WalletId { get; }
        internal string CurrencyId { get; }
        internal string OperationId { get; }
        internal string CorrelationId { get; }
        internal OathmarkWalletOperation Operation { get; }
        internal long Amount { get; }
        internal long ExpectedRevision { get; }
        internal string PolicyHash { get; }
    }

    internal sealed class OathmarkWalletResult
    {
        internal OathmarkWalletResult(OathmarkWalletStatus status, OathmarkWalletState wallet = null,
            OathmarkWalletReceipt receipt = null, string message = "")
        {
            Status = status;
            Wallet = wallet;
            Receipt = receipt;
            Message = message;
        }
        internal OathmarkWalletStatus Status { get; }
        // Detached copies: callers cannot edit published save state through a result.
        internal OathmarkWalletState Wallet { get; }
        internal OathmarkWalletReceipt Receipt { get; }
        internal string Message { get; }
    }
}
