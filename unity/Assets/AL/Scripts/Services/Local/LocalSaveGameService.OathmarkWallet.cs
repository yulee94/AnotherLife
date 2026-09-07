using System;
using System.IO;
using System.Text;
using System.Threading;
using AL.Core.SaveAuthority;
using AL.Data.Catalogs;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public sealed partial class LocalSaveGameService
    {
        private readonly int _walletOwnerThread = Thread.CurrentThread.ManagedThreadId;
        private int _walletActive;

        internal static OathmarkWalletPolicy LoadOathmarkWalletPolicy()
        {
            try
            {
                string directory = Application.isEditor
                    ? Path.Combine(Application.dataPath, "AL", "StreamingAssets", "GameData")
                    : Path.Combine(Application.streamingAssetsPath, "GameData");
                return OathmarkWalletPolicy.TryLoad(
                    new SystemSaveFileOperations().ReadAllBytesBounded(
                        Path.Combine(directory, "al_oathmark_marketplace_policy.json"), 65536).Bytes,
                    new SystemSaveFileOperations().ReadAllBytesBounded(
                        Path.Combine(directory, "al_oathmark_wallet_policy.json"), 65536).Bytes, out var policy)
                    ? policy : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                return null;
            }
        }

        internal OathmarkWalletResult TryCommitOathmarkWallet(OathmarkWalletRequest request)
        {
            if (Thread.CurrentThread.ManagedThreadId != _walletOwnerThread ||
                Interlocked.CompareExchange(ref _walletActive, 1, 0) != 0)
                return new OathmarkWalletResult(OathmarkWalletStatus.ReadOnly);
            try { return CommitOathmarkWalletCore(request); }
            finally { Volatile.Write(ref _walletActive, 0); }
        }

        private OathmarkWalletResult CommitOathmarkWalletCore(OathmarkWalletRequest request)
        {
            if (request?.Authority == null || !OathmarkWalletValidation.IsId(request.OperationId) ||
                !OathmarkWalletValidation.IsId(request.CorrelationId) || request.Amount < 0 ||
                request.ExpectedRevision < 0 || !Enum.IsDefined(typeof(OathmarkWalletOperation), request.Operation) ||
                ((request.Operation == OathmarkWalletOperation.Install || request.Operation == OathmarkWalletOperation.Inspect)
                    ? request.Amount != 0 : request.Amount == 0))
                return new OathmarkWalletResult(OathmarkWalletStatus.InvalidRequest);
            var before = GetCurrentAuthority();
            if (before?.Status == ProfileWriteAuthorityStatus.CommitUncertain)
                return new OathmarkWalletResult(OathmarkWalletStatus.SaveUncertain);
            if (before?.Status != ProfileWriteAuthorityStatus.Writable || !HasExactSchemaTwoProfile(_currentSave))
                return new OathmarkWalletResult(OathmarkWalletStatus.ReadOnly);
            var policy = LoadOathmarkWalletPolicy();
            if (policy == null || _currentSave.SaveSchemaVersion != policy.SaveSchemaVersion)
                return new OathmarkWalletResult(OathmarkWalletStatus.InvalidPolicy);
            if (request.Authority.ProfileId != before.ProfileId || request.AccountId != before.ProfileId)
                return new OathmarkWalletResult(OathmarkWalletStatus.WrongProfile);
            if (request.CurrencyId != policy.CurrencyId || request.WalletId != policy.WalletId(before.ProfileId))
                return new OathmarkWalletResult(OathmarkWalletStatus.ForbiddenWallet);
            if (request.PolicyHash != policy.Hash)
                return new OathmarkWalletResult(OathmarkWalletStatus.Stale);

            OathmarkWalletStatus plannedStatus = OathmarkWalletStatus.Malformed;
            OathmarkWalletReceipt receipt = null;
            var bound = ((IProfileBoundSaveGameCandidateStore)this).TryCommitCandidate(
                ProfileAuthorityExpectation.From(before), "al.save.oathmark-wallet.v1", request.OperationId,
                candidate => PrepareOathmarkWallet(candidate, request, before, policy, out plannedStatus, out receipt));
            var commit = bound?.CommitResult;
            if (commit == null) return new OathmarkWalletResult(OathmarkWalletStatus.ReadOnly);
            if (commit.Outcome == SaveCandidateCommitOutcome.CommitUncertain)
                return new OathmarkWalletResult(OathmarkWalletStatus.SaveUncertain, message: commit.Message);
            if (commit.Outcome == SaveCandidateCommitOutcome.PreviousPreserved)
                return new OathmarkWalletResult(OathmarkWalletStatus.PreviousPreserved, message: commit.Message);
            if (commit.Outcome == SaveCandidateCommitOutcome.ReadOnly)
                return new OathmarkWalletResult(OathmarkWalletStatus.ReadOnly, message: commit.Message);
            if (!commit.IsCommitted)
                return new OathmarkWalletResult(plannedStatus == OathmarkWalletStatus.Committed
                    ? OathmarkWalletStatus.Malformed : plannedStatus, message: commit.Message);
            if (GetCurrentAuthority()?.Status != ProfileWriteAuthorityStatus.Writable)
                return new OathmarkWalletResult(OathmarkWalletStatus.SaveUncertain);
            return new OathmarkWalletResult(plannedStatus,
                JsonUtility.FromJson<OathmarkWalletState>(JsonUtility.ToJson(commit.PublishedSave.OathmarkWallet)),
                receipt == null ? null : JsonUtility.FromJson<OathmarkWalletReceipt>(JsonUtility.ToJson(receipt)));
        }

        private static SaveCandidateMutationPreparation PrepareOathmarkWallet(SaveGameData candidate,
            OathmarkWalletRequest request, ProfileWriteAuthoritySnapshot authority, OathmarkWalletPolicy policy,
            out OathmarkWalletStatus status, out OathmarkWalletReceipt receipt)
        {
            receipt = null;
            status = OathmarkWalletStatus.Malformed;
            var wallet = candidate.OathmarkWallet;
            if (!OathmarkWalletValidation.IsValid(wallet, authority.ProfileId))
                return WalletReject(ref status, OathmarkWalletStatus.Malformed);
            bool empty = OathmarkWalletValidation.IsEmpty(wallet);
            string hash = OathmarkRequestHash(request);
            if (!empty)
            {
                if (wallet.CurrencyId != policy.CurrencyId || wallet.WalletId != request.WalletId || wallet.PolicyHash != policy.Hash)
                    return WalletReject(ref status, OathmarkWalletStatus.ForbiddenWallet);
                foreach (var row in wallet.Receipts)
                {
                    if (row.OperationId != request.OperationId && row.CorrelationId != request.CorrelationId) continue;
                    if (row.OperationId != request.OperationId || row.CorrelationId != request.CorrelationId || row.RequestHash != hash)
                        return WalletReject(ref status, OathmarkWalletStatus.Conflict);
                    receipt = row;
                    status = OathmarkWalletStatus.Replayed;
                    return SaveCandidateMutationPreparation.Duplicate();
                }
            }
            if (request.Authority.AuthorityEpoch != authority.AuthorityEpoch ||
                request.Authority.ExpectedGenerationFingerprint != authority.VerifiedGenerationFingerprint ||
                request.ExpectedRevision != (empty ? 0 : wallet.Revision))
                return WalletReject(ref status, OathmarkWalletStatus.Stale);
            if (request.Operation == OathmarkWalletOperation.Inspect)
            {
                status = empty ? OathmarkWalletStatus.Uninstalled : OathmarkWalletStatus.Inspected;
                return SaveCandidateMutationPreparation.Duplicate();
            }
            if (request.Operation == OathmarkWalletOperation.Install)
            {
                if (!empty) return WalletReject(ref status, OathmarkWalletStatus.Conflict);
                wallet = new OathmarkWalletState
                {
                    Version = 1, ProfileId = authority.ProfileId, AccountId = authority.ProfileId,
                    CurrencyId = policy.CurrencyId, WalletId = policy.WalletId(authority.ProfileId),
                    PolicyHash = policy.Hash, Balance = policy.InitialBalance, Revision = 0
                };
            }
            else if (empty) return WalletReject(ref status, OathmarkWalletStatus.Uninstalled);
            if (wallet.Receipts.Count >= policy.MaximumReceipts)
                return WalletReject(ref status, OathmarkWalletStatus.ReceiptCapacity);
            long oldBalance = wallet.Balance;
            long oldRevision = wallet.Revision;
            if (request.Operation == OathmarkWalletOperation.Debit && wallet.Balance < request.Amount)
                return WalletReject(ref status, OathmarkWalletStatus.InsufficientFunds);
            try
            {
                if (request.Operation == OathmarkWalletOperation.Credit) wallet.Balance = checked(wallet.Balance + request.Amount);
                if (request.Operation == OathmarkWalletOperation.Debit) wallet.Balance = checked(wallet.Balance - request.Amount);
                wallet.Revision = checked(wallet.Revision + 1);
            }
            catch (OverflowException) { return WalletReject(ref status, OathmarkWalletStatus.Overflow); }
            receipt = new OathmarkWalletReceipt
            {
                OperationId = request.OperationId, CorrelationId = request.CorrelationId, RequestHash = hash,
                Operation = (int)request.Operation, Amount = request.Amount, BeforeBalance = oldBalance,
                AfterBalance = wallet.Balance, BeforeRevision = oldRevision, AfterRevision = wallet.Revision
            };
            wallet.Receipts.Add(receipt);
            candidate.OathmarkWallet = wallet;
            status = OathmarkWalletStatus.Committed;
            return SaveCandidateMutationPreparation.Prepared();
        }

        private static SaveCandidateMutationPreparation WalletReject(ref OathmarkWalletStatus status, OathmarkWalletStatus reason)
        {
            status = reason;
            return SaveCandidateMutationPreparation.Rejected("AL-OATHMARK-" + reason);
        }

        private static string OathmarkRequestHash(OathmarkWalletRequest r)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                {
                    writer.Write(r.Authority.ProfileId); writer.Write(r.Authority.AuthorityEpoch);
                    writer.Write(r.Authority.ExpectedGenerationFingerprint); writer.Write(r.AccountId);
                    writer.Write(r.WalletId); writer.Write(r.CurrencyId); writer.Write(r.PolicyHash);
                    writer.Write(r.OperationId); writer.Write(r.CorrelationId); writer.Write((int)r.Operation);
                    writer.Write(r.Amount); writer.Write(r.ExpectedRevision);
                }
                return OathmarkWalletPolicy.Digest(bytes.ToArray());
            }
        }
    }
}
