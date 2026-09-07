using System;
using System.IO;
using System.Linq;
using System.Text;
using AL.Strongholds;
using NUnit.Framework;

namespace AL.Tests.EditMode.Strongholds
{
    public sealed class StrongholdPlannerTests
    {
        [Test]
        public void LegacyMappingCannotMoveFortressFlagToAnotherTerritory()
        {
            string json = File.ReadAllText(CatalogPath());
            string moved = json.Replace("\"territoryId\": \"T1\"", "\"territoryId\": \"swap\"")
                .Replace("\"territoryId\": \"T2\"", "\"territoryId\": \"T1\"")
                .Replace("\"territoryId\": \"swap\"", "\"territoryId\": \"T2\"");
            Assert.That(StrongholdCatalog.TryLoad(Encoding.UTF8.GetBytes(moved), out _), Is.False);
        }

        [Test]
        public void CatalogRejectsSwappedProfileTypesEvenWhenAllReferencesAreUnique()
        {
            string json = File.ReadAllText(CatalogPath()).Replace("stronghold_gate_l01_v1", "swap_l01_v1")
                .Replace("stronghold_visual_l01_v1", "stronghold_gate_l01_v1")
                .Replace("swap_l01_v1", "stronghold_visual_l01_v1");
            Assert.That(StrongholdCatalog.TryLoad(Encoding.UTF8.GetBytes(json), out _), Is.False);
        }

        [Test]
        public void LowTierDoesNotManufactureACommandDefeatRequirement()
        {
            var planner = Planner();
            var state = Step(planner, planner.Fresh("T1", "instance_1", "stonehold"), StrongholdOperation.BreachGate,
                "breach", target: "stronghold_t1_gate");
            var defeat = Request(state, StrongholdOperation.DefeatCommandNpc, "defeat", target: "stronghold_t1_command");
            Assert.That(Plan(planner, state, defeat, Observe(defeat)).Candidate, Is.Null);
        }

        [Test]
        public void CatalogRejectsMalformedInputsAndAllNegativeFixtures()
        {
            var root = Directory.GetParent(Path.GetDirectoryName(CatalogPath())).Parent.Parent.Parent.FullName;
            var fixtures = Directory.GetFiles(Path.Combine(root, "SharedContracts/Tests/fixtures/invalid"), "al-stronghold.invalid.*.json");
            Assert.That(fixtures.Length, Is.EqualTo(12));
            foreach (var path in fixtures)
                Assert.That(StrongholdCatalog.TryLoad(File.ReadAllBytes(path), out _), Is.False, path);
            foreach (var bytes in new[] { (byte[])null, Array.Empty<byte>(), new byte[] { 255 }, Encoding.UTF8.GetBytes("{}"),
                Encoding.UTF8.GetBytes("{\"catalogId\":\"x\",\"catalogId\":\"x\"}"), new byte[65537] })
                Assert.That(StrongholdCatalog.TryLoad(bytes, out var rejected), Is.False);
        }

        [Test]
        public void FreshRejectsWhitespaceIdentifiersWithoutNormalizingThem()
        {
            var planner = Planner();
            Assert.That(planner.Fresh("T1", "instance_1\n", "stonehold"), Is.Null);
            Assert.That(planner.Fresh("T1", "instance_1", "Stonehold"), Is.Null);
            Assert.That(planner.Fresh("t1", "instance_1", "stonehold"), Is.Null);
        }

        [Test]
        public void SerializedCancelExpiryRaceHasOnlyOneWinnerAndReplayNeverRestoresCandidate()
        {
            var planner = Planner();
            var state = Step(planner, planner.Fresh("T1", "instance_1", "stonehold"), StrongholdOperation.BreachGate,
                "breach", target: "stronghold_t1_gate");
            var start = Request(state, StrongholdOperation.InteractStatue, "start");
            state = Plan(planner, state, start, Observe(start)).Candidate;
            // A fresh planner recovers the same immutable snapshot, not a new deadline.
            planner = Planner();
            var expiry = Request(state, StrongholdOperation.CompleteTakeover, "expiry", attempt: state.Attempt.Id);
            var cancel = Request(state, StrongholdOperation.InteractStatue, "cancel", "umbral");
            var captured = Plan(planner, state, expiry, Observe(expiry, 180100)).Candidate;
            var cancelled = Plan(planner, state, cancel, Observe(cancel, 180100)).Candidate;
            Assert.That(Plan(planner, captured, cancel, Observe(cancel, 180100)).Reason, Is.EqualTo("StaleState"));
            Assert.That(Plan(planner, cancelled, expiry, Observe(expiry, 180100)).Reason, Is.EqualTo("StaleState"));
            foreach (var pair in new[] { Tuple.Create(captured, expiry), Tuple.Create(cancelled, cancel), Tuple.Create(captured, start) })
            {
                var replay = Plan(planner, pair.Item1, pair.Item2, Observe(pair.Item2, 200000));
                Assert.That(replay.Status, Is.EqualTo(StrongholdPlanStatus.Replayed));
                Assert.That(replay.Candidate, Is.Null);
                Assert.That(replay.Receipt.OperationId, Is.EqualTo(pair.Item2.OperationId));
            }
            var changed = Request(cancelled, StrongholdOperation.InteractStatue, "cancel", "eldergrove");
            Assert.That(Plan(planner, cancelled, changed, Observe(changed, 200000)).Status, Is.EqualTo(StrongholdPlanStatus.Conflict));
        }

        [Test]
        public void WrongBindingsTargetsAndClockFailurePreservePriorState()
        {
            var planner = Planner();
            var state = Step(planner, planner.Fresh("T1", "instance_1", "stonehold"), StrongholdOperation.BreachGate,
                "breach", target: "stronghold_t1_gate");
            var good = Request(state, StrongholdOperation.InteractStatue, "start");
            var requests = new[] {
                new StrongholdRequest("wrong_territory", good.Operation, "T4", good.InstanceId, good.ExpectedCatalogHash, state.Hash, "crownlands", good.TargetId),
                new StrongholdRequest("wrong_instance", good.Operation, "T1", "instance_2", good.ExpectedCatalogHash, state.Hash, "crownlands", good.TargetId),
                new StrongholdRequest("wrong_catalog", good.Operation, "T1", good.InstanceId, new string('f',64), state.Hash, "crownlands", good.TargetId),
                new StrongholdRequest("wrong_state", good.Operation, "T1", good.InstanceId, good.ExpectedCatalogHash, new string('f',64), "crownlands", good.TargetId),
                Request(state, good.Operation, "wrong_target", target: "stronghold_t4_statue"),
                Request(state, good.Operation, "unknown_realm", realm: "accordant"),
                Request(state, good.Operation, "owner_start", realm: "stonehold"),
                Request(state, (StrongholdOperation)99, "unknown_operation") };
            foreach (var request in requests)
                Assert.That(Plan(planner, state, request, Observe(request)).Candidate, Is.Null, request.OperationId);
            foreach (var observation in new[] { Observe(good, -1), Observe(good, 99), Observe(good, long.MaxValue),
                new StrongholdObservation("wrong_fingerprint", 100, StrongholdObservationSource.FixtureOnly, true, true),
                new StrongholdObservation(good.Fingerprint, 100, StrongholdObservationSource.FixtureOnly, false, true),
                new StrongholdObservation(good.Fingerprint, 100, StrongholdObservationSource.Untrusted, true, true) })
                Assert.That(Plan(planner, state, good, observation).Candidate, Is.Null);
            Assert.That(state.Owner, Is.EqualTo("stonehold"));
            Assert.That(state.Attempt, Is.Null);
        }

        [Test]
        public void ResealFencesOldBreachAndNpcWorkWithoutSilentlyChangingTimer()
        {
            var planner = Planner();
            var state = UpgradeTo(planner, planner.Fresh("T1", "instance_1", "stonehold"), 5);
            state = Step(planner, state, StrongholdOperation.BreachGate, "breach", target: "stronghold_t1_gate");
            var oldDefeat = Request(state, StrongholdOperation.DefeatCommandNpc, "delayed_defeat", target: "stronghold_t1_command");
            state = Step(planner, state, StrongholdOperation.DefeatCommandNpc, "defeat", target: "stronghold_t1_command");
            state = Step(planner, state, StrongholdOperation.InteractStatue, "start");
            var attempt = state.Attempt;
            state = Step(planner, state, StrongholdOperation.ResealGate, "reseal", "stonehold", "stronghold_t1_gate", now: 200);
            Assert.That(state.GateBreached, Is.False);
            Assert.That(state.CommandNpcDefeated, Is.False);
            Assert.That(state.Attempt, Is.SameAs(attempt));
            Assert.That(Plan(planner, state, oldDefeat, Observe(oldDefeat, 200)).Candidate, Is.Null);
            state = Step(planner, state, StrongholdOperation.BreachGate, "rebreach", target: "stronghold_t1_gate", now: 300);
            state = Step(planner, state, StrongholdOperation.DefeatCommandNpc, "new_defeat", target: "stronghold_t1_command", now: 300);
            var staleAttempt = Request(state, StrongholdOperation.CompleteTakeover, "finish", attempt: attempt.Id);
            Assert.That(Plan(planner, state, staleAttempt, Observe(staleAttempt, attempt.Deadline)).Candidate, Is.Null);
            Assert.That(state.Attempt.Deadline, Is.EqualTo(attempt.Deadline));
            state = Step(planner, state, StrongholdOperation.InteractStatue, "cancel", "umbral", now: 400);
            state = Step(planner, state, StrongholdOperation.InteractStatue, "restart", "crownlands", now: 500);
            Assert.That(state.Attempt.Deadline, Is.EqualTo(180500));
        }

        [Test]
        public void CommandDefeatIsRequiredOnlyFromLevelFiveAndCaptureInvalidatesOldQuotes()
        {
            for (int level = 1; level <= 10; level++)
            {
                var planner = Planner();
                var state = UpgradeTo(planner, planner.Fresh("T1", "instance_1", "stonehold"), level);
                var oldQuote = Quote(planner, state);
                state = Step(planner, state, StrongholdOperation.BreachGate, "breach", target: "stronghold_t1_gate");
                Assert.That(Quote(planner, state), Is.Null, "No upgrade quotes during breached siege");
                var start = Request(state, StrongholdOperation.InteractStatue, "start");
                if (level >= 5)
                {
                    Assert.That(Plan(planner, state, start, Observe(start)).Candidate, Is.Null);
                    var defeat = Request(state, StrongholdOperation.DefeatCommandNpc, "defeat", target: "stronghold_t1_command");
                    Assert.That(Plan(planner, state, defeat, Observe(defeat, combat: false)).Candidate, Is.Null);
                    state = Step(planner, state, StrongholdOperation.DefeatCommandNpc, "defeat", target: "stronghold_t1_command");
                }
                state = Step(planner, state, StrongholdOperation.InteractStatue, "start");
                state = Step(planner, state, StrongholdOperation.CompleteTakeover, "finish", now: 180100, attempt: state.Attempt.Id);
                Assert.That(state.Level, Is.EqualTo(1));
                Assert.That(state.CommandNpcDefeated, Is.False);
                if (oldQuote != null)
                {
                    var stale = Request(state, StrongholdOperation.Upgrade, "stale_quote", "crownlands", "stronghold_t1_upgrade", quote: oldQuote);
                    Assert.That(Plan(planner, state, stale, Observe(stale, 180100)).Candidate, Is.Null);
                }
                state = UpgradeTo(planner, state, 9, "new_owner");
                Assert.That(Quote(planner, state).RareResource, Is.EqualTo("RoyalSigil"));
            }
        }

        private static StrongholdState UpgradeTo(StrongholdPlanner planner, StrongholdState state, int level, string prefix = "upgrade")
        {
            while (state.Level < level)
                state = Step(planner, state, StrongholdOperation.Upgrade, prefix + "_" + state.Level, state.Owner,
                    "stronghold_t1_upgrade", now: Math.Max(100, state.LastTrustedTime), quote: Quote(planner, state));
            return state;
        }

        [Test]
        public void FixtureUpgradesTraverseExactTenLevelsWithCurrentOwnerRareProfile()
        {
            var resources = new[] { "DeepOre", "WorldSap", "RoyalSigil", "DarkCrystal" };
            var owners = new[] { "stonehold", "eldergrove", "crownlands", "umbral" };
            for (int owner = 0; owner < owners.Length; owner++)
            {
                var planner = Planner();
                var state = planner.Fresh("T1", "instance_1", owners[owner]);
                for (int level = 2; level <= 10; level++)
                {
                    var quote = Quote(planner, state);
                    Assert.That(quote, Is.Not.Null);
                    Assert.That(quote.TargetLevel, Is.EqualTo(level));
                    Assert.That(quote.Owner, Is.EqualTo(owners[owner]));
                    Assert.That(quote.CostProfileId, Is.EqualTo($"stronghold_cost_l{level:00}_v1"));
                    Assert.That(quote.RareResource, Is.EqualTo(level == 10 ? resources[owner] : ""));
                    Assert.That(quote.OwnerRareCostProfileId, Is.EqualTo(level == 10 ? "stronghold_owner_rare_9_to_10_v1" : ""));
                    Assert.That(quote.NumericCostResolved, Is.False);
                    Assert.That(quote.CanDebit, Is.False);
                    var request = Request(state, StrongholdOperation.Upgrade, "upgrade_" + level,
                        owners[owner], "stronghold_t1_upgrade", quote: quote);
                    Assert.That(Plan(planner, state, request, Observe(request, funding: false)).Candidate, Is.Null);
                    Assert.That(Plan(planner, state, request, Observe(request, permission: false)).Candidate, Is.Null);
                    Assert.That(Plan(planner, state, request, Observe(request, interaction: false)).Candidate, Is.Null);
                    state = Step(planner, state, StrongholdOperation.Upgrade, "upgrade_" + level,
                        owners[owner], "stronghold_t1_upgrade", quote: quote);
                    Assert.That(state.Level, Is.EqualTo(level));
                }
                Assert.That(Quote(planner, state), Is.Null);
            }
        }

        private static StrongholdUpgradeQuote Quote(StrongholdPlanner planner, StrongholdState state)
        {
            var method = typeof(StrongholdPlanner).GetMethod("QuoteUpgrade");
            Assert.That(method, Is.Not.Null, "Missing versioned owner-bound upgrade planner");
            return (StrongholdUpgradeQuote)method.Invoke(planner, new object[] { state });
        }

        [Test]
        public void RealmScopedAttemptKeepsDeadlineAndOtherRealmCancelsWithoutReplacement()
        {
            foreach (string cancellingRealm in new[] { "stonehold", "eldergrove", "umbral" })
            {
                var planner = Planner();
                var state = Step(planner, planner.Fresh("T1", "instance_1", "stonehold"), StrongholdOperation.BreachGate,
                    "breach", target: "stronghold_t1_gate");
                state = Step(planner, state, StrongholdOperation.InteractStatue, "start");
                long deadline = state.Attempt.Deadline;
                state = Step(planner, state, StrongholdOperation.InteractStatue, "same_realm", now: 50100);
                Assert.That(state.Attempt.Deadline, Is.EqualTo(deadline));
                state = Step(planner, state, StrongholdOperation.InteractStatue, "cancel", realm: cancellingRealm, now: 90100);
                Assert.That(state.Attempt, Is.Null);
                Assert.That(state.Owner, Is.EqualTo("stonehold"));
                // A later distinct interaction starts from zero; cancellation itself never starts anything.
                state = Step(planner, state, StrongholdOperation.InteractStatue, "restart", realm: "umbral", now: 100100);
                Assert.That(state.Attempt.StartedAt, Is.EqualTo(100100));
                Assert.That(state.Attempt.Deadline, Is.EqualTo(280100));
            }
        }

        [Test]
        public void StatueWaitsFull180SecondsAndFinalizationResetsCaptureAtomically()
        {
            var planner = Planner();
            var fresh = planner.Fresh("T1", "instance_1", "stonehold");
            var denied = Request(fresh, StrongholdOperation.InteractStatue, "intact");
            Assert.That(Plan(planner, fresh, denied, Observe(denied)).Candidate, Is.Null);
            var breached = Step(planner, fresh, StrongholdOperation.BreachGate, "breach", target: "stronghold_t1_gate");
            var begin = Request(breached, StrongholdOperation.InteractStatue, "start");
            Assert.That(Plan(planner, breached, begin, Observe(begin, interaction: false)).Candidate, Is.Null);
            var active = Plan(planner, breached, begin, Observe(begin)).Candidate;
            Assert.That(active, Is.Not.Null, "Valid statue interaction must start, not instantly capture");
            Assert.That(active.Owner, Is.EqualTo("stonehold"));
            Assert.That(active.Attempt.Deadline - active.Attempt.StartedAt, Is.EqualTo(180000));
            var finish = Request(active, StrongholdOperation.CompleteTakeover, "finish", attempt: active.Attempt.Id);
            Assert.That(Plan(planner, active, finish, Observe(finish, 180099)).Candidate, Is.Null);
            var captured = Plan(planner, active, finish, Observe(finish, 180100));
            Assert.That(captured.Status, Is.EqualTo(StrongholdPlanStatus.Prepared));
            Assert.That(captured.Candidate.Owner, Is.EqualTo("crownlands"));
            Assert.That(captured.Candidate.Level, Is.EqualTo(1));
            Assert.That(captured.Candidate.OwnershipEpoch, Is.EqualTo(active.OwnershipEpoch + 1));
            Assert.That(captured.Candidate.Generation, Is.EqualTo(active.Generation + 1));
            Assert.That(captured.Candidate.GateBreached, Is.False);
            Assert.That(captured.Candidate.CommandNpcDefeated, Is.False);
            Assert.That(captured.Candidate.Attempt, Is.Null);
        }

        private static StrongholdState Step(StrongholdPlanner planner, StrongholdState state, StrongholdOperation operation,
            string id, string realm = "crownlands", string target = "stronghold_t1_statue", long now = 100,
            string attempt = "", StrongholdUpgradeQuote quote = null)
        {
            var request = Request(state, operation, id, realm, target, attempt, quote);
            var result = Plan(planner, state, request, Observe(request, now));
            Assert.That(result.Status, Is.EqualTo(StrongholdPlanStatus.Prepared), result.Reason);
            return result.Candidate;
        }

        [Test]
        public void BreachRequiresExactFixtureCombatObservationAndNeverCaptures()
        {
            var planner = Planner();
            var state = planner.Fresh("T1", "instance_1", "stonehold");
            var request = Request(state, StrongholdOperation.BreachGate, "breach", "crownlands", "stronghold_t1_gate");
            Assert.That(Plan(planner, state, request, new StrongholdObservation(request.Fingerprint, 100)).Candidate, Is.Null);
            Assert.That(Plan(planner, state, request, Observe(request, combat: false)).Candidate, Is.Null);
            var result = Plan(planner, state, request, Observe(request));
            Assert.That(result.Status, Is.EqualTo(StrongholdPlanStatus.Prepared));
            Assert.That(result.Candidate.GateBreached, Is.True);
            Assert.That(result.Candidate.Owner, Is.EqualTo("stonehold"));
            Assert.That(result.Candidate.Attempt, Is.Null);
            Assert.That(result.Candidate.Revision, Is.EqualTo(state.Revision + 1));
            Assert.That(result.CanApplyProduction, Is.False);
            Assert.That(state.GateBreached, Is.False);
        }

        private static StrongholdPlanner Planner()
        {
            Assert.That(StrongholdCatalog.TryLoad(File.ReadAllBytes(CatalogPath()), out var catalog), Is.True);
            return new StrongholdPlanner(catalog);
        }
        private static StrongholdRequest Request(StrongholdState state, StrongholdOperation operation, string id,
            string realm = "crownlands", string target = "stronghold_t1_statue", string attempt = "", StrongholdUpgradeQuote quote = null)
            => new StrongholdRequest(id, operation, state.TerritoryId, state.InstanceId, state.CatalogHash,
                state.Hash, realm, target, attempt, quote);
        private static StrongholdObservation Observe(StrongholdRequest request, long now = 100,
            bool interaction = true, bool combat = true, bool permission = true, bool funding = true)
            => new StrongholdObservation(request.Fingerprint, now, StrongholdObservationSource.FixtureOnly,
                true, interaction, combat, permission, funding);
        private static StrongholdPlan Plan(StrongholdPlanner planner, StrongholdState state,
            StrongholdRequest request, StrongholdObservation observation)
        {
            var method = typeof(StrongholdPlanner).GetMethod("Plan");
            Assert.That(method, Is.Not.Null, "Missing siege transition planner");
            return (StrongholdPlan)method.Invoke(planner, new object[] { state, request, observation });
        }

        [Test]
        public void FreshStrongholdStartsAtLevelOneAndNeverGrantsProductionWrite()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("AL.Strongholds.StrongholdPlanner")).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Missing stronghold planner");
            Assert.That(StrongholdCatalog.TryLoad(File.ReadAllBytes(CatalogPath()), out var catalog), Is.True);
            var planner = Activator.CreateInstance(type, catalog);
            var fresh = type.GetMethod("Fresh").Invoke(planner, new object[] { "T1", "instance_1", "stonehold" });
            Assert.That(fresh, Is.Not.Null);
            Assert.That(fresh.GetType().GetProperty("Level").GetValue(fresh), Is.EqualTo(1));
            Assert.That(type.GetProperty("CanApplyProduction").GetValue(planner), Is.EqualTo(false));
            Assert.That(type.GetMethod("Fresh").Invoke(planner, new object[] { "T2", "instance_1", "stonehold" }), Is.Null);
        }

        [Test]
        public void CatalogLoadsCanonicalBytesWithoutEngineOrProductionAuthority()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("AL.Strongholds.StrongholdCatalog")).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Missing engine-free stronghold catalog");
            object[] args = { File.ReadAllBytes(CatalogPath()), null };
            Assert.That(type.GetMethod("TryLoad").Invoke(null, args), Is.EqualTo(true));
            Assert.That(type.GetProperty("ProductionEligible").GetValue(args[1]), Is.EqualTo(false));
        }

        private static string CatalogPath()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                foreach (string prefix in new[] { "unity/", "" })
                {
                    string path = Path.Combine(directory.FullName, prefix + "Assets/AL/StreamingAssets/GameData/al_stronghold_catalog.json");
                    if (File.Exists(path)) return path;
                }
                directory = directory.Parent;
            }
            throw new FileNotFoundException("Stronghold test requires the repository catalog");
        }
    }
}
