using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Catalogs;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public sealed partial class ProfileBoundOathmarkWalletTests
    {
        private static void WithWallet(Action<LocalSaveGameService, string> test, bool install = true)
        {
            string root = Path.Combine(Path.GetTempPath(), "AL-Wallet-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var save = new LocalSaveGameService(root);
                save.Load();
                Assert.NotNull(save.CurrentSave);
                if (install) Assert.AreEqual(OathmarkWalletStatus.Committed,
                    save.TryCommitOathmarkWallet(Request(save, OathmarkWalletOperation.Install)).Status);
                test(save, root);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void InspectUninstalledWalletDoesNotWriteOrConvert()
        {
            WithWallet((save, root) =>
            {
                byte[] before = File.ReadAllBytes(Path.Combine(root, "save.json"));
                var inspect = save.TryCommitOathmarkWallet(Request(save, OathmarkWalletOperation.Inspect));
                Assert.AreEqual(OathmarkWalletStatus.Uninstalled, inspect.Status);
                CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(root, "save.json")));
            }, false);
        }

        [TestCase("gold")]
        [TestCase("Gold Coin")]
        [TestCase("kingdom_resource")]
        [TestCase("guild_treasury")]
        [TestCase("realm_resource")]
        [TestCase("warzone_credits")]
        [TestCase("premium")]
        [TestCase("real_money")]
        public void CrossWalletAliasesCannotAuthorizeCreditOrDebit(string currency)
        {
            WithWallet((save, root) =>
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(root, "save.json"));
                foreach (var op in new[] { OathmarkWalletOperation.Credit, OathmarkWalletOperation.Debit })
                {
                    var r = Request(save, op, 1);
                    var bad = new OathmarkWalletRequest(r.Authority, r.AccountId, r.WalletId, currency,
                        r.OperationId, r.CorrelationId, r.Operation, r.Amount, r.ExpectedRevision, r.PolicyHash);
                    Assert.AreEqual(OathmarkWalletStatus.ForbiddenWallet, save.TryCommitOathmarkWallet(bad).Status);
                }
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(root, "save.json")));
            });
        }

        [Test]
        public void WrongProfileWalletStaleAndCorrelationConflictsPreserveBytes()
        {
            WithWallet((save, root) =>
            {
                var old = Request(save, OathmarkWalletOperation.Credit, 10);
                var committed = Request(save, OathmarkWalletOperation.Credit, 20);
                Assert.AreEqual(OathmarkWalletStatus.Committed, save.TryCommitOathmarkWallet(committed).Status);
                var fresh = Request(save, OathmarkWalletOperation.Debit, 1);
                byte[] bytes = File.ReadAllBytes(Path.Combine(root, "save.json"));
                Assert.AreEqual(OathmarkWalletStatus.Stale, save.TryCommitOathmarkWallet(old).Status);
                Assert.AreEqual(OathmarkWalletStatus.WrongProfile, save.TryCommitOathmarkWallet(new OathmarkWalletRequest(
                    fresh.Authority, "other-account", fresh.WalletId, fresh.CurrencyId, fresh.OperationId,
                    fresh.CorrelationId, fresh.Operation, fresh.Amount, fresh.ExpectedRevision, fresh.PolicyHash)).Status);
                Assert.AreEqual(OathmarkWalletStatus.ForbiddenWallet, save.TryCommitOathmarkWallet(new OathmarkWalletRequest(
                    fresh.Authority, fresh.AccountId, "other-wallet", fresh.CurrencyId, fresh.OperationId,
                    fresh.CorrelationId, fresh.Operation, fresh.Amount, fresh.ExpectedRevision, fresh.PolicyHash)).Status);
                Assert.AreEqual(OathmarkWalletStatus.Stale, save.TryCommitOathmarkWallet(new OathmarkWalletRequest(
                    fresh.Authority, fresh.AccountId, fresh.WalletId, fresh.CurrencyId, fresh.OperationId,
                    fresh.CorrelationId, fresh.Operation, fresh.Amount, fresh.ExpectedRevision - 1, fresh.PolicyHash)).Status);
                foreach (bool sameOperation in new[] { true, false })
                {
                    var collision = new OathmarkWalletRequest(committed.Authority, committed.AccountId, committed.WalletId,
                        committed.CurrencyId, sameOperation ? committed.OperationId : "new-operation",
                        committed.CorrelationId, committed.Operation, sameOperation ? 21 : 20,
                        committed.ExpectedRevision, committed.PolicyHash);
                    Assert.AreEqual(OathmarkWalletStatus.Conflict, save.TryCommitOathmarkWallet(collision).Status);
                }
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(root, "save.json")));
            });
        }

        [Test]
        public void ExactInt64OverflowInsufficientAndInvalidAmountsFailClosed()
        {
            WithWallet((save, root) =>
            {
                foreach (long amount in new[] { -1L, long.MinValue, 0L })
                    Assert.AreEqual(OathmarkWalletStatus.InvalidRequest,
                        save.TryCommitOathmarkWallet(Request(save, OathmarkWalletOperation.Credit, amount)).Status);
                Assert.AreEqual(OathmarkWalletStatus.InsufficientFunds,
                    save.TryCommitOathmarkWallet(Request(save, OathmarkWalletOperation.Debit, 1)).Status);
                Assert.AreEqual(OathmarkWalletStatus.Committed,
                    save.TryCommitOathmarkWallet(Request(save, OathmarkWalletOperation.Credit, long.MaxValue)).Status);
                byte[] bytes = File.ReadAllBytes(Path.Combine(root, "save.json"));
                Assert.AreEqual(OathmarkWalletStatus.Overflow,
                    save.TryCommitOathmarkWallet(Request(save, OathmarkWalletOperation.Credit, 1)).Status);
                Assert.AreEqual(long.MaxValue, save.CurrentSave.OathmarkWallet.Balance);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(root, "save.json")));
                Assert.AreEqual(OathmarkWalletStatus.Committed,
                    save.TryCommitOathmarkWallet(Request(save, OathmarkWalletOperation.Debit, long.MaxValue)).Status);
                Assert.AreEqual(0L, save.CurrentSave.OathmarkWallet.Balance);
            });
        }

        [Test]
        public void WorkerThreadCannotRaceSaveAuthority()
        {
            WithWallet((save, root) =>
            {
                var request = Request(save, OathmarkWalletOperation.Credit, 1);
                OathmarkWalletResult result = null;
                var thread = new Thread(() => result = save.TryCommitOathmarkWallet(request));
                thread.Start();
                Assert.True(thread.Join(5000));
                Assert.AreEqual(OathmarkWalletStatus.ReadOnly, result.Status);
                Assert.AreEqual(0, save.CurrentSave.OathmarkWallet.Balance);
            });
        }

        [TestCase("future")]
        [TestCase("negative")]
        [TestCase("fractional")]
        [TestCase("missing")]
        [TestCase("duplicate")]
        [TestCase("receipt")]
        [TestCase("profile")]
        public void CorruptOrFutureWalletIsNeverNormalizedToWritable(string damage)
        {
            WithWallet((save, root) =>
            {
                var r = Request(save, OathmarkWalletOperation.Credit, 10);
                var wallet = save.CurrentSave.OathmarkWallet;
                string before = JsonUtility.ToJson(wallet);
                string corrupt;
                switch (damage)
                {
                    case "future": corrupt = before.Replace("\"Version\":1", "\"Version\":2"); break;
                    case "negative": corrupt = before.Replace("\"Balance\":0", "\"Balance\":-1"); break;
                    case "fractional": corrupt = before.Replace("\"Balance\":0", "\"Balance\":0.5"); break;
                    case "missing": corrupt = before.Replace("\"Balance\":0,", ""); break;
                    case "duplicate": corrupt = before.Replace("\"Balance\":0", "\"Balance\":0,\"Balance\":5"); break;
                    case "receipt": corrupt = before.Replace("\"AfterBalance\":0", "\"AfterBalance\":1"); break;
                    default: corrupt = before.Replace(wallet.ProfileId, "alp_ffffffffffffffffffffffffffffffff"); break;
                }
                string saveJson = JsonUtility.ToJson(save.CurrentSave).Replace(before, corrupt);
                Assert.AreNotEqual(JsonUtility.ToJson(save.CurrentSave), saveJson);
                byte[] bytes = Encoding.UTF8.GetBytes(saveJson);
                File.WriteAllBytes(Path.Combine(root, "save.json"), bytes);
                File.WriteAllBytes(Path.Combine(root, "save.backup.json"), bytes);
                var reload = new LocalSaveGameService(root);
                bool ignore = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                try { reload.Load(); }
                finally { LogAssert.ignoreFailingMessages = ignore; }
                Assert.AreNotEqual(ProfileWriteAuthorityStatus.Writable, reload.GetCurrentAuthority().Status);
                Assert.AreEqual(OathmarkWalletStatus.ReadOnly, reload.TryCommitOathmarkWallet(r).Status);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(root, "save.json")));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SaveFailureDoesNotPublishValueOrReplayUntilReconciled(bool uncertain)
        {
            string root = Path.Combine(Path.GetTempPath(), "AL-Wallet-Fault-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var files = new WalletFaultFiles();
            try
            {
                var save = new LocalSaveGameService(root, files);
                save.Load();
                Assert.AreEqual(OathmarkWalletStatus.Committed, save.TryCommitOathmarkWallet(Request(save, OathmarkWalletOperation.Install)).Status);
                var request = Request(save, OathmarkWalletOperation.Credit, 7);
                byte[] before = File.ReadAllBytes(Path.Combine(root, "save.json"));
                files.FailWrite = !uncertain;
                files.FailAfterReplace = uncertain;
                bool ignore = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                OathmarkWalletResult result;
                try { result = save.TryCommitOathmarkWallet(request); }
                finally { LogAssert.ignoreFailingMessages = ignore; }
                Assert.AreEqual(uncertain ? OathmarkWalletStatus.SaveUncertain : OathmarkWalletStatus.PreviousPreserved, result.Status, result.Message);
                Assert.AreEqual(0L, save.CurrentSave.OathmarkWallet.Balance);
                Assert.AreEqual(uncertain ? OathmarkWalletStatus.SaveUncertain : OathmarkWalletStatus.ReadOnly,
                    save.TryCommitOathmarkWallet(request).Status);
                if (!uncertain) CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(root, "save.json")));
                files.FailWrite = files.FailAfterReplace = files.HideReads = false;
                var reload = new LocalSaveGameService(root);
                reload.Load();
                if (uncertain)
                {
                    // This fault also hides rollback verification. A primary alone is
                    // not a reconciled primary+backup ledger and cannot authorize replay.
                    Assert.AreEqual(OathmarkWalletStatus.ReadOnly, reload.TryCommitOathmarkWallet(request).Status);
                    Assert.AreNotEqual(ProfileWriteAuthorityStatus.Writable, reload.GetCurrentAuthority().Status);
                }
            }
            finally { Directory.Delete(root, true); }
        }

        private sealed class WalletFaultFiles : ISaveFileOperations
        {
            private readonly SystemSaveFileOperations inner = new SystemSaveFileOperations();
            internal bool FailWrite, FailAfterReplace, HideReads;
            public bool FileExists(string p) => inner.FileExists(p);
            public void CreateDirectory(string p) => inner.CreateDirectory(p);
            public SaveFileReadResult ReadAllBytesBounded(string p, int n) => HideReads
                ? new SaveFileReadResult(SaveFileReadDisposition.IoFailure, null, 0, "TEST-UNREADABLE")
                : inner.ReadAllBytesBounded(p, n);
            public SaveFileWriteResult WriteAllTextDurable(string p, string s) => FailWrite
                ? new SaveFileWriteResult(false, false, "TEST-WRITE-FAILED") : inner.WriteAllTextDurable(p, s);
            public void Copy(string s, string d, bool o) => inner.Copy(s, d, o);
            public void Move(string s, string d) => inner.Move(s, d);
            public void Replace(string s, string d, string b)
            {
                inner.Replace(s, d, b);
                if (FailAfterReplace) HideReads = true;
            }
            public void Delete(string p) => inner.Delete(p);
            public IEnumerable<string> EnumerateFiles(string p, string s) => inner.EnumerateFiles(p, s);
            public DateTime GetCreationTimeUtc(string p) => inner.GetCreationTimeUtc(p);
            public bool IsReparsePoint(string p) => inner.IsReparsePoint(p);
        }
    }
}
