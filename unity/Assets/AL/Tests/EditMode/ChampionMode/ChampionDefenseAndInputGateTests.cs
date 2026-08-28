using System;
using System.IO;
using System.Linq;
using AL.ChampionMode.AI;
using AL.ChampionMode.Control;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Input;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class ChampionDefenseAndInputGateTests
    {
        private GameObject _root;
        private string _catalogRoot;
        private bool _previousGameplaySuppressed;

        [SetUp]
        public void SetUp()
        {
            _previousGameplaySuppressed = GameInput.GameplaySuppressed;
            GameInput.SetGameplaySuppressed(false);
            ChampionHudCameraGate.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            ChampionHudCameraGate.Reset();
            GameInput.SetGameplaySuppressed(_previousGameplaySuppressed);
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }

            if (!string.IsNullOrEmpty(_catalogRoot) &&
                Directory.Exists(_catalogRoot))
            {
                Directory.Delete(_catalogRoot, true);
            }
        }

        [TestCase(RealmId.Stonehold)]
        [TestCase(RealmId.Eldergrove)]
        [TestCase(RealmId.Crownlands)]
        [TestCase(RealmId.Umbral)]
        public void PackagedRealmDefenseResolvesFromItsExactChampionRecord(
            RealmId realmId)
        {
            Assert.That(
                SixFamilyRuntimeCatalog.TryLoad(
                    out SixFamilyRuntimeSnapshot snapshot,
                    out string loadCode),
                Is.True,
                loadCode);

            var champion = snapshot.GetAllChampions()
                .Single(candidate => candidate.Realm == realmId);
            Assert.That(
                snapshot.TryCreateSliceProfile(
                    champion.Id,
                    out var profile,
                    out string profileCode),
                Is.True,
                profileCode);
            Assert.That(
                snapshot.TryResolveDefendMitigation(
                    realmId,
                    out float mitigation,
                    out string mitigationCode),
                Is.True,
                mitigationCode);

            Assert.That(
                mitigationCode,
                Is.EqualTo(SixFamilyRuntimeCatalog.DefendMitigationReadyCode));
            Assert.That(mitigation, Is.EqualTo(profile.DefendMitigation));
            Assert.That(mitigation, Is.InRange(0f, 1f));
        }

        [Test]
        public void AmbiguousRealmDefenseCatalogFailsClosedWithExactDiagnostic()
        {
            _catalogRoot = Path.Combine(
                Path.GetTempPath(),
                "al-defense-authority-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_catalogRoot);
            string packagedRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData"));
            string[] requiredFiles =
            {
                SixFamilyRuntimeCatalog.RealmsFileName,
                SixFamilyRuntimeCatalog.BuildingsFileName,
                SixFamilyRuntimeCatalog.ChampionsFileName,
                SixFamilyRuntimeCatalog.SkillsFileName,
                SixFamilyRuntimeCatalog.ChampionRuntimeFileName
            };
            for (var index = 0; index < requiredFiles.Length; index++)
            {
                File.Copy(
                    Path.Combine(packagedRoot, requiredFiles[index]),
                    Path.Combine(_catalogRoot, requiredFiles[index]));
            }

            string runtimePath = Path.Combine(
                _catalogRoot,
                SixFamilyRuntimeCatalog.ChampionRuntimeFileName);
            string runtimeJson = File.ReadAllText(runtimePath);
            Assert.That(runtimeJson, Does.Contain("\"realm_id\": \"umbral\""));
            File.WriteAllText(
                runtimePath,
                runtimeJson.Replace(
                    "\"realm_id\": \"umbral\"",
                    "\"realm_id\": \"stonehold\""));

            Assert.That(
                SixFamilyRuntimeCatalog.TryLoadFromDirectory(
                    _catalogRoot,
                    out SixFamilyRuntimeSnapshot snapshot,
                    out string loadCode),
                Is.True,
                loadCode);
            Assert.That(
                snapshot.TryResolveDefendMitigation(
                    RealmId.Stonehold,
                    out _,
                    out string mitigationCode),
                Is.False);
            Assert.That(
                mitigationCode,
                Is.EqualTo(SixFamilyRuntimeCatalog.DefendMitigationAmbiguousCode));
        }

        [Test]
        public void GeneralDamageBoundaryUsesBoundRealmMitigationAndPublishesReceipt()
        {
            _root = new GameObject("ChampionDefenseAndInputGateTests_Player");
            ChampionCombat combat = _root.AddComponent<ChampionCombat>();
            ChampionController controller = _root.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Stonehold);
            Assert.That(combat.DefendMitigationReady, Is.True);
            Assert.That(combat.ApplyCatalogStats(200f, 100f, 50f), Is.True);

            ChampionDamageReceipt observed = default;
            var receiptCount = 0;
            combat.DamageResolved += receipt =>
            {
                observed = receipt;
                receiptCount++;
            };

            controller.SetBlocking(true);
            Assert.That(controller.IsBlocking, Is.True);
            ChampionDamageReceipt blocked = combat.TakeDamage(80f);

            Assert.That(blocked.Accepted, Is.True);
            Assert.That(blocked.WasDefending, Is.True);
            Assert.That(blocked.WasMitigated, Is.True);
            Assert.That(blocked.DefendMitigation, Is.EqualTo(combat.DefendMitigation));
            Assert.That(blocked.AppliedDamage, Is.EqualTo(40f).Within(0.001f));
            Assert.That(blocked.MitigatedDamage, Is.EqualTo(40f).Within(0.001f));
            Assert.That(blocked.RemainingHealth, Is.EqualTo(160f).Within(0.001f));
            Assert.That(receiptCount, Is.EqualTo(1));
            Assert.That(observed.Sequence, Is.EqualTo(blocked.Sequence));
            Assert.That(
                BossDummyAI.FormatIncomingDamageFeedback(blocked),
                Is.EqualTo("-40  BLOCK 40"));

            controller.SetBlocking(false);
            ChampionDamageReceipt unblocked = combat.TakeDamage(80f);

            Assert.That(unblocked.Accepted, Is.True);
            Assert.That(unblocked.WasDefending, Is.False);
            Assert.That(unblocked.WasMitigated, Is.False);
            Assert.That(unblocked.AppliedDamage, Is.EqualTo(80f).Within(0.001f));
            Assert.That(unblocked.RemainingHealth, Is.EqualTo(80f).Within(0.001f));
            Assert.That(unblocked.Sequence, Is.GreaterThan(blocked.Sequence));
            Assert.That(receiptCount, Is.EqualTo(2));
            Assert.That(
                BossDummyAI.FormatIncomingDamageFeedback(unblocked),
                Is.EqualTo("-80"));
        }

        [Test]
        public void MissingDefenseAuthorityAppliesFullDamageAndReportsUnavailable()
        {
            _root = new GameObject("ChampionDefenseAndInputGateTests_Unbound");
            ChampionCombat combat = _root.AddComponent<ChampionCombat>();
            Assert.That(combat.ApplyCatalogStats(200f, 100f, 50f), Is.True);

            ChampionDamageReceipt receipt = combat.ApplyIncomingDamage(80f, true);

            Assert.That(receipt.Accepted, Is.True);
            Assert.That(receipt.WasDefending, Is.True);
            Assert.That(receipt.WasMitigated, Is.False);
            Assert.That(receipt.AppliedDamage, Is.EqualTo(80f).Within(0.001f));
            Assert.That(receipt.RemainingHealth, Is.EqualTo(120f).Within(0.001f));
            Assert.That(
                receipt.DiagnosticCode,
                Is.EqualTo(ChampionCombat.DefendMitigationUnavailableCode));
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void PointerOverUiSuppressesOnlyMouseOriginBasicAttack(
            bool pointerOverUi,
            bool attackOriginatesFromMouse,
            bool expectedSuppressed)
        {
            Assert.That(
                ChampionCombatInputPolicy.ShouldSuppressBasicAttack(
                    pointerOverUi,
                    attackOriginatesFromMouse),
                Is.EqualTo(expectedSuppressed));
        }

        [Test]
        public void ChampionInputRoutesMouseAttackThroughLiveEventSystemGate()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                "ChampionMode",
                "Control",
                "ChampionController.cs"));

            Assert.That(
                source,
                Does.Contain("ChampionCombatInputPolicy.ShouldSuppressBasicAttack"));
            Assert.That(
                source,
                Does.Contain("ChampionHudCameraGate.IsPointerOverUi()"));
            Assert.That(
                source,
                Does.Contain("GameInput.Attack.activeControl?.device is Mouse"));
        }
    }
}
