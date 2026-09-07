using System;
using System.IO;
using System.Text;
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
        [Test]
        public void WalletPolicyValuesAreLoadedAndInvalidOrMissingPolicyFailsClosed()
        {
            string directory = Path.Combine(Application.dataPath, "AL/StreamingAssets/GameData");
            byte[] currency = File.ReadAllBytes(Path.Combine(directory, "al_oathmark_marketplace_policy.json"));
            string text = File.ReadAllText(Path.Combine(directory, "al_oathmark_wallet_policy.json"));
            Assert.True(OathmarkWalletPolicy.TryLoad(currency, Encoding.UTF8.GetBytes(
                text.Replace("\"maximumReceipts\": 2048", "\"maximumReceipts\": 2")), out var small));
            Assert.AreEqual(2, small.MaximumReceipts);
            foreach (string invalid in new[] {
                text.Replace("\"initialBalance\": 0", "\"initialBalance\": 500"),
                text.Replace("\"earningSourcesEnabled\": false", "\"earningSourcesEnabled\": true"),
                text.Replace("\"maximumReceipts\": 2048", "\"maximumReceipts\": 0"),
                text.Replace("\"saveSchemaVersion\": 2", "\"saveSchemaVersion\": 1"),
                text.Replace("local_profile_identity", "caller_selected") })
                Assert.False(OathmarkWalletPolicy.TryLoad(currency, Encoding.UTF8.GetBytes(invalid), out _));
            Assert.False(OathmarkWalletPolicy.TryLoad(null, Encoding.UTF8.GetBytes(text), out _));
            Assert.False(OathmarkWalletPolicy.TryLoad(currency, null, out _));
        }

        [TestCase("gold")]
        [TestCase("fraction")]
        [TestCase("conversion")]
        [TestCase("missing")]
        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("overflow")]
        public void CurrencyPolicyParserRejectsMalformedOrContradictorySource(string damage)
        {
            string directory = Path.Combine(Application.dataPath, "AL/StreamingAssets/GameData");
            string currency = File.ReadAllText(Path.Combine(directory, "al_oathmark_marketplace_policy.json"));
            byte[] policy = File.ReadAllBytes(Path.Combine(directory, "al_oathmark_wallet_policy.json"));
            Assert.True(OathmarkWalletPolicy.TryLoad(Encoding.UTF8.GetBytes(currency), policy, out _));
            switch (damage)
            {
                case "gold": currency = currency.Replace("\"technicalId\": \"oathmark\"", "\"technicalId\": \"gold\""); break;
                case "fraction": currency = currency.Replace("\"fractionalUnits\": false", "\"fractionalUnits\": true"); break;
                case "conversion": currency = currency.Replace("\"conversion\": \"forbidden\"", "\"conversion\": \"allowed\""); break;
                case "missing": currency = currency.Replace("\"integerUnitScale\": 1,", ""); break;
                case "duplicate": currency = currency.Replace("\"integerUnitScale\": 1,", "\"integerUnitScale\": 1, \"integerUnitScale\": 2,"); break;
                case "unknown": currency = currency.Replace("\"integerUnitScale\": 1,", "\"integerUnitScale\": 1, \"fallback\": \"gold\","); break;
                default: currency = currency.Replace("\"integerUnitScale\": 1,", "\"integerUnitScale\": 9223372036854775808,"); break;
            }
            Assert.False(OathmarkWalletPolicy.TryLoad(Encoding.UTF8.GetBytes(currency), policy, out _));
        }

        [Test]
        public void ChangedDiskCannotReplayAnInMemoryReceipt()
        {
            WithWallet((save, root) =>
            {
                var r = Request(save, OathmarkWalletOperation.Credit, 5);
                Assert.AreEqual(OathmarkWalletStatus.Committed, save.TryCommitOathmarkWallet(r).Status);
                // Distinct valid generation, not a malformed-file-only test.
                var altered = JsonUtility.FromJson<AL.Data.Runtime.SaveGameData>(JsonUtility.ToJson(save.CurrentSave));
                altered.LastSavedTimestamp += 1;
                string json = JsonUtility.ToJson(altered, true);
                File.WriteAllText(Path.Combine(root, "save.json"), json, new UTF8Encoding(false));
                bool ignore = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                OathmarkWalletResult result;
                try { result = save.TryCommitOathmarkWallet(r); }
                finally { LogAssert.ignoreFailingMessages = ignore; }
                Assert.AreNotEqual(OathmarkWalletStatus.Replayed, result.Status);
                Assert.AreNotEqual(OathmarkWalletStatus.Committed, result.Status);
                Assert.AreEqual(json, File.ReadAllText(Path.Combine(root, "save.json")));
            });
        }

        [Test]
        public void ReceiptCapacityNeverEvictsOldOperationIdentity()
        {
            WithWallet((save, root) =>
            {
                int cap = LocalSaveGameService.LoadOathmarkWalletPolicy().MaximumReceipts;
                var result = ((IProfileBoundSaveGameCandidateStore)save).TryCommitCandidate(
                    ProfileAuthorityExpectation.From(save.GetCurrentAuthority()), "test-cap-seed", "test-cap-result", candidate =>
                    {
                        var w = candidate.OathmarkWallet;
                        for (int i = 1; i < cap; i++)
                            w.Receipts.Add(new OathmarkWalletReceipt
                            {
                                OperationId = "cap-op-" + i, CorrelationId = "cap-event-" + i,
                                RequestHash = new string('a', 64), Operation = 2, Amount = 1,
                                BeforeRevision = i, AfterRevision = i + 1, BeforeBalance = i - 1, AfterBalance = i
                            });
                        w.Balance = cap - 1; w.Revision = cap;
                        return SaveCandidateMutationPreparation.Prepared();
                    });
                Assert.True(result.CommitResult.IsCommitted, result.CommitResult.Message);
                byte[] before = File.ReadAllBytes(Path.Combine(root, "save.json"));
                var denied = save.TryCommitOathmarkWallet(Request(save, OathmarkWalletOperation.Credit, 1));
                Assert.AreEqual(OathmarkWalletStatus.ReceiptCapacity, denied.Status, denied.Message);
                Assert.AreEqual(cap, save.CurrentSave.OathmarkWallet.Receipts.Count);
                CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(root, "save.json")));
            });
        }
    }
}
