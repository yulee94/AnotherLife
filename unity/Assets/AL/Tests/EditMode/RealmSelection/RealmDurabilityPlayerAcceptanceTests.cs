using System.IO;
using AL.Core;
using AL.RealmSelection;
using NUnit.Framework;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class RealmDurabilityPlayerAcceptanceTests
    {
        [Test]
        public void CommandLineRequiresEnableFlagAndDefaultsLifecycle()
        {
            string root;
            string phase;
            string output;
            Assert.False(
                RealmDurabilityPlayerAcceptance.TryParseCommandLine(
                    new[] { "-batchmode", "-nographics" },
                    out root,
                    out phase,
                    out output));
            Assert.True(
                RealmDurabilityPlayerAcceptance.TryParseCommandLine(
                    new[]
                    {
                        RealmDurabilityPlayerAcceptance.EnableArgument,
                        RealmDurabilityPlayerAcceptance.RootArgument,
                        "C:/tmp/realm-durability",
                        RealmDurabilityPlayerAcceptance.PhaseArgument,
                        RealmDurabilityPlayerAcceptance.ReloadPhase,
                        RealmDurabilityPlayerAcceptance.OutputArgument,
                        "C:/tmp/out.txt"
                    },
                    out root,
                    out phase,
                    out output));
            Assert.AreEqual("C:/tmp/realm-durability", root);
            Assert.AreEqual(RealmDurabilityPlayerAcceptance.ReloadPhase, phase);
            Assert.AreEqual("C:/tmp/out.txt", output);
        }

        [Test]
        public void MissingRootAndUnknownPhaseFailClosed()
        {
            RealmDurabilityAcceptanceResult missing = RealmDurabilityPlayerAcceptance.Run(
                string.Empty,
                RealmDurabilityPlayerAcceptance.LifecyclePhase);
            Assert.False(missing.Passed);
            Assert.AreEqual("AL-REALM-DURABILITY-ROOT-MISSING", missing.TechnicalCode);

            string root = CreateRoot();
            try
            {
                RealmDurabilityAcceptanceResult unknown = RealmDurabilityPlayerAcceptance.Run(root, "explode");
                Assert.False(unknown.Passed);
                Assert.AreEqual("AL-REALM-DURABILITY-PHASE-INVALID", unknown.TechnicalCode);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ReloadWithoutCommitFailsClosed()
        {
            string root = CreateRoot();
            try
            {
                RealmDurabilityAcceptanceResult reload = RealmDurabilityPlayerAcceptance.Run(
                    root,
                    RealmDurabilityPlayerAcceptance.ReloadPhase);
                Assert.False(reload.Passed);
                Assert.AreNotEqual(RealmDurabilityPlayerAcceptance.PassedMarker, reload.Marker);
                Assert.AreNotEqual(RealmId.Crownlands, reload.CommittedRealmId);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LifecycleCommitThenReloadKeepsStoneholdReceiptAndNvsEligibility()
        {
            string root = CreateRoot();
            try
            {
                RealmDurabilityAcceptanceResult lifecycle = RealmDurabilityPlayerAcceptance.Run(
                    root,
                    RealmDurabilityPlayerAcceptance.LifecyclePhase);
                Assert.True(lifecycle.Passed, lifecycle.TechnicalCode);
                Assert.AreEqual(RealmDurabilityPlayerAcceptance.PassedMarker, lifecycle.Marker);
                Assert.AreEqual(RealmId.Stonehold, lifecycle.CommittedRealmId);
                Assert.AreNotEqual(RealmId.Crownlands, lifecycle.CommittedRealmId);
                Assert.True(lifecycle.NvsEligible);

                RealmDurabilityAcceptanceResult reloaded = RealmDurabilityPlayerAcceptance.Run(
                    root,
                    RealmDurabilityPlayerAcceptance.ReloadPhase);
                Assert.True(reloaded.Passed, reloaded.TechnicalCode);
                Assert.AreEqual(RealmId.Stonehold, reloaded.CommittedRealmId);
                Assert.True(File.Exists(Path.Combine(root, "save.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static string CreateRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-RealmDurabilityAcceptance",
                System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
