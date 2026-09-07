using System;
using System.IO;
using System.Reflection;
using AL.Core.SaveAuthority;
using AL.Data.Catalogs;
using UnityEngine;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public sealed partial class ProfileBoundOathmarkWalletTests
    {
        [Test]
        public void SaveHasOptionalOathmarkWalletExtension()
        {
            Assert.NotNull(typeof(SaveGameData).GetField("OathmarkWallet"),
                "Schema-2 saves must retain an optional independent Oathmark wallet.");
        }

        [Test]
        public void SaveRootOwnsInternalWalletTransaction()
        {
            Assert.NotNull(typeof(LocalSaveGameService).GetMethod("TryCommitOathmarkWallet",
                BindingFlags.NonPublic | BindingFlags.Instance),
                "Wallet writes require a profile-bound transaction inside the save root.");
        }

        [TestCase("pre-schema-v0.json")]
        [TestCase("current-schema-v1.json")]
        public void OldProfileInstallsWalletAtZeroWithoutConversion(string fixture)
        {
            string root = Path.Combine(Path.GetTempPath(), "AL-Wallet-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                byte[] oldBytes = File.ReadAllBytes(Path.Combine(Application.dataPath,
                    "AL/Tests/EditMode/Fixtures/SaveSchema1", fixture));
                File.WriteAllBytes(Path.Combine(root, "save.json"), oldBytes);
                var save = new LocalSaveGameService(root);
                save.Load();
                Assert.NotNull(save.CurrentSave);
                Assert.AreEqual(2, save.CurrentSave.SaveSchemaVersion);
                Assert.True(OathmarkWalletValidation.IsEmpty(save.CurrentSave.OathmarkWallet));
                string resources = JsonUtility.ToJson(new ResourceSnapshot { Rows = save.CurrentSave.Resources });
                int credits = save.CurrentSave.WarzoneCredits;
                var authority = save.GetCurrentAuthority();
                Assert.AreEqual(ProfileWriteAuthorityStatus.Writable, authority.Status);
                var request = new OathmarkWalletRequest(ProfileAuthorityExpectation.From(authority),
                    authority.ProfileId, authority.ProfileId + ":oathmark", "oathmark",
                    "wallet-install", "wallet-install-event", OathmarkWalletOperation.Install,
                    0, 0, LocalSaveGameService.LoadOathmarkWalletPolicy().Hash);
                var result = save.TryCommitOathmarkWallet(request);
                Assert.AreEqual(OathmarkWalletStatus.Committed, result.Status, result.Message);
                Assert.AreEqual(0L, save.CurrentSave.OathmarkWallet.Balance);
                Assert.AreEqual(resources, JsonUtility.ToJson(new ResourceSnapshot { Rows = save.CurrentSave.Resources }));
                Assert.AreEqual(credits, save.CurrentSave.WarzoneCredits);
                var reload = new LocalSaveGameService(root);
                reload.Load();
                Assert.AreEqual(authority.ProfileId, reload.CurrentSave.ProfileId);
                Assert.AreEqual(OathmarkWalletStatus.Replayed, reload.TryCommitOathmarkWallet(request).Status);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void CreditDebitReplayAndInspectAreDurable()
        {
            string root = Path.Combine(Path.GetTempPath(), "AL-Wallet-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var save = new LocalSaveGameService(root);
                save.Load();
                Assert.AreEqual(OathmarkWalletStatus.Committed, save.TryCommitOathmarkWallet(Request(save, OathmarkWalletOperation.Install)).Status);
                var credit = Request(save, OathmarkWalletOperation.Credit, 100);
                Assert.AreEqual(OathmarkWalletStatus.Committed, save.TryCommitOathmarkWallet(credit).Status);
                Assert.AreEqual(100L, save.CurrentSave.OathmarkWallet.Balance);
                var debit = Request(save, OathmarkWalletOperation.Debit, 30);
                var committed = save.TryCommitOathmarkWallet(debit);
                Assert.AreEqual(OathmarkWalletStatus.Committed, committed.Status);
                Assert.AreEqual(70L, committed.Wallet.Balance);
                var bytes = File.ReadAllBytes(Path.Combine(root, "save.json"));
                var reloaded = new LocalSaveGameService(root);
                reloaded.Load();
                var replay = reloaded.TryCommitOathmarkWallet(debit);
                Assert.AreEqual(OathmarkWalletStatus.Replayed, replay.Status);
                Assert.AreEqual(committed.Receipt.RequestHash, replay.Receipt.RequestHash);
                Assert.AreEqual(70L, replay.Receipt.AfterBalance);
                Assert.AreEqual(OathmarkWalletStatus.Replayed, reloaded.TryCommitOathmarkWallet(credit).Status);
                var inspected = reloaded.TryCommitOathmarkWallet(Request(reloaded, OathmarkWalletOperation.Inspect));
                Assert.AreEqual(OathmarkWalletStatus.Inspected, inspected.Status);
                Assert.AreEqual(70L, inspected.Wallet.Balance);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(root, "save.json")));
                inspected.Wallet.Balance = 999;
                Assert.AreEqual(70L, reloaded.CurrentSave.OathmarkWallet.Balance);
            }
            finally { Directory.Delete(root, true); }
        }

        [Serializable]
        private sealed class ResourceSnapshot { public System.Collections.Generic.List<ResourceData> Rows; }

        private static OathmarkWalletRequest Request(LocalSaveGameService save, OathmarkWalletOperation operation, long amount = 0)
        {
            var a = save.GetCurrentAuthority();
            var p = LocalSaveGameService.LoadOathmarkWalletPolicy();
            return new OathmarkWalletRequest(ProfileAuthorityExpectation.From(a), a.ProfileId,
                p.WalletId(a.ProfileId), p.CurrencyId, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"),
                operation, amount, save.CurrentSave.OathmarkWallet?.Revision ?? 0, p.Hash);
        }
    }
}
