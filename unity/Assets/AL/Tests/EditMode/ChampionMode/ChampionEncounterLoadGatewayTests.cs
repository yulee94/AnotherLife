using System;
using System.Collections.Generic;
using System.IO;
using AL.ChampionMode.Encounter;
using AL.Core;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class ChampionEncounterLoadGatewayTests
    {
        private const string LedgerRelativePath =
            "Docs/GameDataCatalog/six-family-production-authority.v1.json";

        [Test]
        public void CurrentSixFamilyAuthorityMatchesPinnedChampionAndSkillSourceSet()
        {
            ChampionEncounterSourceSet source =
                ChampionEncounterSourceSet.CurrentSixFamilyAuthority();
            string ledger = File.ReadAllText(ResolveLedgerPath());

            Assert.That(source.Disposition,
                Is.EqualTo(ChampionEncounterSourceDisposition.BlockedRequired));
            Assert.That(source.ProductionEligible, Is.False);
            Assert.That(source.AuthorityId,
                Is.EqualTo("al_six_family_production_authority_v1"));
            Assert.That(source.AuthorityRevision,
                Is.EqualTo("382a98f9a2f3ce6f8ee2283107cd593063243e2b"));
            Assert.That(source.SourceSetVersion, Is.EqualTo("2026-09-04-v1"));
            Assert.That(source.SourceSetSha256,
                Is.EqualTo("10cf4e2dea9320521572316f0ebba6b11831665408ec325445bd26ecbe8c7597"));
            Assert.That(source.ChampionFamilyDisposition, Is.EqualTo("blocked_required"));
            Assert.That(source.SkillFamilyDisposition, Is.EqualTo("blocked_required"));
            Assert.That(ledger, Does.Contain(source.AuthorityId));
            Assert.That(ledger, Does.Contain(source.AuthorityRevision));
            Assert.That(ledger, Does.Contain(source.SourceSetVersion));
            Assert.That(ledger, Does.Contain(source.SourceSetSha256));
            Assert.That(ledger, Does.Contain("\"family\": \"champions\""));
            Assert.That(ledger, Does.Contain("\"family\": \"skills\""));
            Assert.That(CountOccurrences(ledger, "\"disposition\": \"blocked_required\""),
                Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ProductionPathFailsClosedWithoutMutationOnCurrentBlockedAuthority()
        {
            var application = new RecordingApplication();
            var receipts = new List<ChampionEncounterLoadReceipt>();

            ChampionEncounterLoadPlan first =
                ChampionEncounterProductionLoadPath.StartFromCommittedRealm(
                    RealmId.Stonehold,
                    application,
                    receipts);
            ChampionEncounterLoadPlan second =
                ChampionEncounterProductionLoadPath.StartFromCommittedRealm(
                    RealmId.Stonehold,
                    application,
                    receipts);

            Assert.That(first.Status,
                Is.EqualTo(ChampionEncounterLoadStatus.CatalogUnavailable));
            Assert.That(second.Status, Is.EqualTo(first.Status));
            Assert.That(second.DiagnosticCode, Is.EqualTo(first.DiagnosticCode));
            Assert.That(first.DiagnosticCode,
                Is.EqualTo(ChampionEncounterLoadGateway.CatalogUnavailableCode));
            Assert.That(first.Receipt, Is.Null);
            Assert.That(second.Receipt, Is.Null);
            Assert.That(application.CallCount, Is.Zero);
            Assert.That(receipts, Is.Empty);
        }

        [Test]
        public void UncommittedOrInvalidRealmFailsClosedWithoutMutation()
        {
            var application = new RecordingApplication();

            ChampionEncounterLoadPlan none =
                ChampionEncounterProductionLoadPath.StartFromCommittedRealm(
                    RealmId.None,
                    application,
                    new List<ChampionEncounterLoadReceipt>());
            ChampionEncounterLoadPlan blank =
                ChampionEncounterLoadGateway.Start(
                    Request(realmId: " "),
                    ChampionEncounterSourceSet.CurrentSixFamilyAuthority(),
                    application,
                    new List<ChampionEncounterLoadReceipt>());

            Assert.That(none.Status, Is.EqualTo(ChampionEncounterLoadStatus.InvalidSource));
            Assert.That(none.DiagnosticCode,
                Is.EqualTo(ChampionEncounterLoadGateway.RealmInvalidCode));
            Assert.That(blank.Status, Is.EqualTo(ChampionEncounterLoadStatus.InvalidSource));
            Assert.That(application.CallCount, Is.Zero);
        }

        [Test]
        public void AbsentStaleMixedAndInvalidSourceFailBeforeApplication()
        {
            var application = new RecordingApplication();
            ChampionEncounterSourceSet current =
                ChampionEncounterSourceSet.CurrentSixFamilyAuthority();

            ChampionEncounterLoadPlan missing =
                ChampionEncounterLoadGateway.Start(
                    null,
                    current,
                    application,
                    new List<ChampionEncounterLoadReceipt>());
            ChampionEncounterLoadPlan stale =
                ChampionEncounterLoadGateway.Start(
                    Request(sourceSetSha256: new string('b', 64)),
                    current,
                    application,
                    new List<ChampionEncounterLoadReceipt>());
            ChampionEncounterLoadPlan hybrid =
                ChampionEncounterLoadGateway.Start(
                    Request(),
                    HybridPublishedSource(),
                    application,
                    new List<ChampionEncounterLoadReceipt>());
            ChampionEncounterLoadPlan invalidSlots =
                ChampionEncounterLoadGateway.Start(
                    Request(slotIds: new[]
                    {
                        "warzone_burst",
                        "realm_strike",
                        "renewing_guard",
                        "warmaster_breaker"
                    }),
                    ValidPublishedSource(),
                    application,
                    new List<ChampionEncounterLoadReceipt>());

            Assert.That(missing.Status, Is.EqualTo(ChampionEncounterLoadStatus.InvalidSource));
            Assert.That(stale.Status,
                Is.EqualTo(ChampionEncounterLoadStatus.CatalogUnavailable));
            Assert.That(stale.DiagnosticCode,
                Is.EqualTo(ChampionEncounterLoadGateway.StaleSnapshotCode));
            Assert.That(hybrid.Status, Is.EqualTo(ChampionEncounterLoadStatus.InvalidSource));
            Assert.That(hybrid.DiagnosticCode,
                Is.EqualTo(ChampionEncounterLoadGateway.HybridSourceCode));
            Assert.That(invalidSlots.Status,
                Is.EqualTo(ChampionEncounterLoadStatus.InvalidSource));
            Assert.That(invalidSlots.DiagnosticCode,
                Is.EqualTo(ChampionEncounterLoadGateway.SlotOrderInvalidCode));
            Assert.That(application.CallCount, Is.Zero);
        }

        [Test]
        public void PublishedCatalogLoadsOnceAndReplaysWithoutRemutation()
        {
            ChampionEncounterSourceSet source = ValidPublishedSource();
            var application = new RecordingApplication();
            var receipts = new List<ChampionEncounterLoadReceipt>();

            ChampionEncounterLoadPlan loaded =
                ChampionEncounterLoadGateway.Start(
                    Request(),
                    source,
                    application,
                    receipts);
            receipts.Add(loaded.Receipt);
            ChampionEncounterLoadPlan duplicate =
                ChampionEncounterLoadGateway.Start(
                    Request(),
                    source,
                    application,
                    receipts);

            Assert.That(loaded.Status, Is.EqualTo(ChampionEncounterLoadStatus.Loaded));
            Assert.That(loaded.Receipt, Is.Not.Null);
            Assert.That(loaded.Receipt.RealmId, Is.EqualTo("stonehold"));
            Assert.That(loaded.Receipt.ActorId, Is.EqualTo("champion_stonehold_vanguard"));
            Assert.That(loaded.Receipt.CasterId, Is.EqualTo("champion_stonehold_vanguard"));
            Assert.That(loaded.Receipt.BossId, Is.EqualTo("boss_stonehold_guardian"));
            Assert.That(loaded.Receipt.LoadoutId, Is.EqualTo("loadout.wire.v1"));
            Assert.That(loaded.Receipt.SlotIds,
                Is.EqualTo(ChampionEncounterSourceSet.AuthoredWireSlotOrder));
            Assert.That(duplicate.Status, Is.EqualTo(ChampionEncounterLoadStatus.DuplicateExact));
            Assert.That(duplicate.Receipt, Is.SameAs(loaded.Receipt));
            Assert.That(application.CallCount, Is.EqualTo(1));
            Assert.That(application.LastSnapshot.EncounterId,
                Is.EqualTo("encounter.stonehold.guardian"));
        }

        [Test]
        public void ReusedEncounterIdentityWithChangedSourceIsRejected()
        {
            ChampionEncounterSourceSet source = ValidPublishedSource();
            var application = new RecordingApplication();
            var receipts = new List<ChampionEncounterLoadReceipt>();
            ChampionEncounterLoadPlan loaded =
                ChampionEncounterLoadGateway.Start(
                    Request(),
                    source,
                    application,
                    receipts);
            receipts.Add(loaded.Receipt);

            ChampionEncounterLoadPlan conflict =
                ChampionEncounterLoadGateway.Start(
                    Request(),
                    ValidPublishedSource(sourceRevision: "source-r2"),
                    application,
                    receipts);

            Assert.That(conflict.Status,
                Is.EqualTo(ChampionEncounterLoadStatus.CorrelationConflict));
            Assert.That(conflict.DiagnosticCode,
                Is.EqualTo(ChampionEncounterLoadGateway.CorrelationConflictCode));
            Assert.That(application.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void ApplicationFailureReturnsTypedNonLoadedResult()
        {
            var application = new RecordingApplication { ShouldApply = false };

            ChampionEncounterLoadPlan result =
                ChampionEncounterLoadGateway.Start(
                    Request(),
                    ValidPublishedSource(),
                    application,
                    new List<ChampionEncounterLoadReceipt>());

            Assert.That(result.Status,
                Is.EqualTo(ChampionEncounterLoadStatus.ApplicationRejected));
            Assert.That(result.Receipt, Is.Null);
            Assert.That(application.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void ProductionSceneControllerWiresTheLoadPathWithoutRewardOrSaveTokens()
        {
            string controller = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "AL",
                    "Scripts",
                    "ChampionMode",
                    "ChampionArenaSceneController.cs"));

            Assert.That(
                controller,
                Does.Contain("ChampionEncounterProductionLoadPath.StartFromCommittedRealm"));
            Assert.That(
                File.Exists(
                    Path.Combine(
                        Application.dataPath,
                        "AL",
                        "Scripts",
                        "ChampionMode",
                        "C1",
                        "Planning",
                        "CombatSkillLoadSessionPlanner.cs")),
                Is.False);
            string production = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "AL",
                    "Scripts",
                    "ChampionMode",
                    "Encounter",
                    "ChampionEncounterProductionLoadPath.cs"));
            Assert.That(production, Does.Not.Contain("ISaveGameService"));
            Assert.That(production, Does.Not.Contain("LocalBossLootService"));
            Assert.That(production, Does.Not.Contain("BossLoot"));
        }

        private static ChampionEncounterLoadRequest Request(
            string realmId = "stonehold",
            string sourceSetSha256 = null,
            IReadOnlyList<string> slotIds = null)
        {
            return new ChampionEncounterLoadRequest(
                "encounter.stonehold.guardian",
                realmId,
                "champion_stonehold_vanguard",
                "champion_stonehold_vanguard",
                "boss_stonehold_guardian",
                "loadout.wire.v1",
                ChampionEncounterSourceSet.CurrentSourceSetVersion,
                sourceSetSha256 ?? ChampionEncounterSourceSet.CurrentSourceSetSha256,
                slotIds ?? ChampionEncounterSourceSet.AuthoredWireSlotOrder);
        }

        private static ChampionEncounterSourceSet ValidPublishedSource(
            string sourceRevision = "source-r1")
        {
            return ChampionEncounterSourceSet.PublishedForTests(
                ChampionEncounterSourceSet.CurrentAuthorityId,
                ChampionEncounterSourceSet.CurrentAuthorityRevision,
                ChampionEncounterSourceSet.CurrentSourceSetVersion,
                ChampionEncounterSourceSet.CurrentSourceSetSha256,
                sourceRevision,
                "stonehold",
                "champion_stonehold_vanguard",
                "champion_stonehold_vanguard",
                "boss_stonehold_guardian",
                "loadout.wire.v1",
                ChampionEncounterSourceSet.AuthoredWireSlotOrder);
        }

        private static ChampionEncounterSourceSet HybridPublishedSource()
        {
            return ChampionEncounterSourceSet.PublishedForTests(
                ChampionEncounterSourceSet.CurrentAuthorityId,
                ChampionEncounterSourceSet.CurrentAuthorityRevision,
                ChampionEncounterSourceSet.CurrentSourceSetVersion,
                ChampionEncounterSourceSet.CurrentSourceSetSha256,
                "source-hybrid",
                "stonehold",
                "champion_stonehold_vanguard",
                "champion_stonehold_vanguard",
                "boss_stonehold_guardian",
                "loadout.wire.v1",
                new[]
                {
                    "skill_iron_bulwark",
                    "skill_shield_slam",
                    "realm_strike",
                    "renewing_guard"
                });
        }

        private static string ResolveLedgerPath()
        {
            string path = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", LedgerRelativePath));
            Assert.That(File.Exists(path), Is.True, path);
            return path;
        }

        private static int CountOccurrences(string text, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        private sealed class RecordingApplication : IChampionEncounterApplication
        {
            public int CallCount { get; private set; }
            public ChampionEncounterLoadSnapshot LastSnapshot { get; private set; }
            public bool ShouldApply { get; set; } = true;

            public bool TryApply(ChampionEncounterLoadSnapshot snapshot)
            {
                CallCount++;
                LastSnapshot = snapshot;
                return ShouldApply;
            }
        }
    }
}
