using System;
using System.Collections.Generic;
using System.Linq;
using AL.RealmDungeon;
using NUnit.Framework;

namespace AL.Tests.EditMode.RealmDungeon
{
    public sealed class RealmDungeonLifecyclePlannerTests
    {
        private const string CrownlandsDungeon = "realm_dungeon_crownlands_deep";
        private const string StoneholdDungeon = "realm_dungeon_stonehold_deep";
        private const string EldergroveDungeon = "realm_dungeon_eldergrove_deep";
        private const string UmbralDungeon = "realm_dungeon_umbral_deep";
        private const string CrownlandsRaid = "raid_dragon_crownlands_dawn_regent";
        private const string StoneholdRaid = "raid_dragon_stonehold_iron_wyrm";
        private const string EldergroveRaid = "raid_dragon_eldergrove_moonbough";
        private const string UmbralRaid = "raid_dragon_umbral_void_seraph";
        private const string CrownlandsGuardian = "dragon_crownlands_dawn_regent";
        private const string ClosedRaidId = "closed_raid_veil_vault";
        private const string GuildBossId = "raid_boss_iron_colossus";
        private const string InstanceId = "realm_dungeon_crownlands_deep_dragon_instance";
        private const string DefeatId = "defeat_crownlands_001";
        private const string LeaseId = "lease_crownlands_001";
        private const string SpawnCycleId = "spawn_cycle_crownlands_001";
        private const long ClockZero = 1_700_000_000;
        private const long CooldownSeconds = 604800;
        private static readonly string CatalogHash = new string('c', 64);

        [Test]
        public void ExactFourPublicDungeonsRejectGuardianAndGuildAliases()
        {
            RealmDungeonLifecyclePlanner planner = Planner();
            RealmDungeonAuthoritySnapshot live = Alive(RealmDungeonLifeState.AliveIdle);

            Assert.That(planner.Catalog.Dungeons.Count, Is.EqualTo(4));
            Assert.That(
                planner.Catalog.Dungeons.Select(value => value.DungeonId).ToArray(),
                Is.EqualTo(new[]
                {
                    CrownlandsDungeon,
                    StoneholdDungeon,
                    EldergroveDungeon,
                    UmbralDungeon
                }));
            Assert.That(
                planner.Catalog.Dungeons.Select(value => value.RaidDragonId).ToArray(),
                Is.EqualTo(new[]
                {
                    CrownlandsRaid,
                    StoneholdRaid,
                    EldergroveRaid,
                    UmbralRaid
                }));
            foreach (RealmDungeonDefinition dungeon in planner.Catalog.Dungeons)
            {
                Assert.That(dungeon.EntranceIds.Count, Is.EqualTo(2));
                Assert.That(dungeon.EntranceIds[0], Is.EqualTo(dungeon.DungeonId + "_entrance_01"));
                Assert.That(dungeon.EntranceIds[1], Is.EqualTo(dungeon.DungeonId + "_entrance_02"));
                Assert.That(dungeon.RaidDragonId, Is.Not.EqualTo(dungeon.GuardianPresentationRef));
                Assert.That(dungeon.ProductionEligible, Is.False);
                Assert.That(dungeon.PresentationApproved, Is.False);
            }

            RealmDungeonPlanningResult guardianAlias = planner.Plan(
                Request(
                    RealmDungeonOperation.Observe,
                    "operation_guardian_alias",
                    raidDragonId: CrownlandsGuardian),
                live);
            Assert.That(guardianAlias.Status, Is.EqualTo(RealmDungeonPlanningStatus.Rejected));
            Assert.That(guardianAlias.Reason, Is.EqualTo(RealmDungeonRejectReason.GuardianIdentityAlias));

            RealmDungeonPlanningResult closedAlias = planner.Plan(
                Request(
                    RealmDungeonOperation.Observe,
                    "operation_closed_alias",
                    dungeonId: ClosedRaidId,
                    raidDragonId: GuildBossId),
                live);
            Assert.That(closedAlias.Status, Is.EqualTo(RealmDungeonPlanningStatus.Rejected));
            Assert.That(closedAlias.Reason, Is.EqualTo(RealmDungeonRejectReason.GuildClosedInstanceAlias));
        }

        [Test]
        public void CertifiedKillStaysOnCooldownAt604799AndBecomesEligibleAt604800()
        {
            RealmDungeonLifecyclePlanner planner = Planner();
            RealmDungeonPlanningResult defeat = planner.Plan(
                Request(RealmDungeonOperation.CommitDefeat, "operation_kill_commit", clock: ClockZero),
                Alive(RealmDungeonLifeState.AliveEngaged));
            Assert.That(defeat.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared));
            Assert.That(defeat.Plan.CandidateSnapshot.LifeState, Is.EqualTo(RealmDungeonLifeState.Cooldown));
            Assert.That(defeat.Plan.CandidateSnapshot.DefeatCommittedAtUnixSeconds, Is.EqualTo(ClockZero));
            Assert.That(
                defeat.Plan.CandidateSnapshot.NextEligibleAtUnixSeconds,
                Is.EqualTo(ClockZero + CooldownSeconds));
            Assert.That(defeat.Plan.RewardReceipt, Is.Not.Null);
            Assert.That(defeat.Plan.RewardReceipt.DefeatIdentity, Is.EqualTo(DefeatId));

            RealmDungeonPlanningResult stillCooling = planner.Plan(
                Request(
                    RealmDungeonOperation.Observe,
                    "operation_observe_604799",
                    clock: ClockZero + CooldownSeconds - 1,
                    expectedRevision: defeat.Plan.CandidateSnapshot.Revision),
                defeat.Plan.CandidateSnapshot);
            Assert.That(stillCooling.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared));
            Assert.That(stillCooling.Plan.CandidateSnapshot.LifeState, Is.EqualTo(RealmDungeonLifeState.Cooldown));
            Assert.That(stillCooling.Plan.CandidateSnapshot.RespawnEligible, Is.False);

            RealmDungeonPlanningResult eligible = planner.Plan(
                Request(
                    RealmDungeonOperation.Observe,
                    "operation_observe_604800",
                    clock: ClockZero + CooldownSeconds,
                    expectedRevision: defeat.Plan.CandidateSnapshot.Revision),
                defeat.Plan.CandidateSnapshot);
            Assert.That(eligible.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared));
            Assert.That(
                eligible.Plan.CandidateSnapshot.LifeState,
                Is.EqualTo(RealmDungeonLifeState.RespawnEligible));
            Assert.That(eligible.Plan.CandidateSnapshot.RespawnEligible, Is.True);
        }

        [Test]
        public void WipeLeashTimeoutDamageAndDisconnectNeverStartCooldown()
        {
            RealmDungeonLifecyclePlanner planner = Planner();
            RealmDungeonAuthoritySnapshot engaged = Alive(RealmDungeonLifeState.AliveEngaged);
            RealmDungeonNonKillKind[] kinds =
            {
                RealmDungeonNonKillKind.Wipe,
                RealmDungeonNonKillKind.Leash,
                RealmDungeonNonKillKind.Timeout,
                RealmDungeonNonKillKind.AbandonedAttempt,
                RealmDungeonNonKillKind.OrdinaryMobGrind,
                RealmDungeonNonKillKind.DamageOnly,
                RealmDungeonNonKillKind.Disconnect,
                RealmDungeonNonKillKind.ClientClockManipulation
            };

            foreach (RealmDungeonNonKillKind kind in kinds)
            {
                RealmDungeonPlanningResult result = planner.Plan(
                    Request(
                        RealmDungeonOperation.RecordNonKill,
                        "operation_nonkill_" + kind.ToString().ToLowerInvariant(),
                        nonKillKind: kind),
                    engaged);
                Assert.That(result.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared), kind.ToString());
                Assert.That(
                    result.Plan.CandidateSnapshot.LifeState,
                    Is.EqualTo(RealmDungeonLifeState.AliveEngaged),
                    kind.ToString());
                Assert.That(result.Plan.CandidateSnapshot.NextEligibleAtUnixSeconds, Is.EqualTo(0), kind.ToString());
                Assert.That(result.Plan.RewardReceipt, Is.Null, kind.ToString());
                Assert.That(engaged.LifeState, Is.EqualTo(RealmDungeonLifeState.AliveEngaged));
            }
        }

        [Test]
        public void NonKillEventsPreserveCommittedCooldownAndReceipt()
        {
            RealmDungeonLifecyclePlanner planner = Planner();
            RealmDungeonAuthoritySnapshot cooling = planner.Plan(
                Request(RealmDungeonOperation.CommitDefeat, "operation_kill"),
                Alive(RealmDungeonLifeState.AliveEngaged)).Plan.CandidateSnapshot;

            foreach (RealmDungeonNonKillKind kind in Enum.GetValues(typeof(RealmDungeonNonKillKind)))
            {
                if (kind == RealmDungeonNonKillKind.None)
                {
                    continue;
                }

                RealmDungeonPlanningResult result = planner.Plan(
                    Request(
                        RealmDungeonOperation.RecordNonKill,
                        "operation_nonkill",
                        clock: ClockZero + 1,
                        expectedRevision: cooling.Revision,
                        nonKillKind: kind),
                    cooling);
                Assert.That(result.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared), kind.ToString());
                Assert.That(result.Plan.CandidateSnapshot.NextEligibleAtUnixSeconds,
                    Is.EqualTo(ClockZero + CooldownSeconds), kind.ToString());
                Assert.That(result.Plan.CandidateSnapshot.DefeatCommittedAtUnixSeconds, Is.EqualTo(ClockZero));
                Assert.That(result.Plan.CandidateSnapshot.Receipts, Is.EqualTo(cooling.Receipts));
                Assert.That(result.Plan.RewardReceipt, Is.Null);

                RealmDungeonPlanningResult eligible = planner.Plan(
                    Request(RealmDungeonOperation.Observe, "operation_eligible",
                        clock: ClockZero + CooldownSeconds,
                        expectedRevision: result.Plan.CandidateSnapshot.Revision),
                    result.Plan.CandidateSnapshot);
                Assert.That(eligible.Plan.CandidateSnapshot.LifeState,
                    Is.EqualTo(RealmDungeonLifeState.RespawnEligible));
            }
        }

        [Test]
        public void ReplayRestartAndConflictingKillKeepOneDefeatReceipt()
        {
            RealmDungeonLifecyclePlanner planner = Planner();
            RealmDungeonPlanningResult first = planner.Plan(
                Request(RealmDungeonOperation.CommitDefeat, "operation_kill_once", clock: ClockZero),
                Alive(RealmDungeonLifeState.AliveEngaged));
            Assert.That(first.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared));

            RealmDungeonPlanningResult replay = planner.Plan(
                Request(
                    RealmDungeonOperation.CommitDefeat,
                    "operation_kill_once",
                    clock: ClockZero,
                    expectedRevision: first.Plan.CandidateSnapshot.Revision),
                first.Plan.CandidateSnapshot);
            Assert.That(replay.Status, Is.EqualTo(RealmDungeonPlanningStatus.AlreadyCommitted));
            Assert.That(replay.ExistingReceipt.DefeatIdentity, Is.EqualTo(DefeatId));
            Assert.That(replay.Plan, Is.Null);
            Assert.That(first.Plan.CandidateSnapshot.Receipts.Count, Is.EqualTo(1));

            RealmDungeonPlanningResult conflict = planner.Plan(
                Request(
                    RealmDungeonOperation.CommitDefeat,
                    "operation_kill_conflict",
                    clock: ClockZero + 12,
                    expectedRevision: first.Plan.CandidateSnapshot.Revision),
                first.Plan.CandidateSnapshot);
            Assert.That(conflict.Status, Is.EqualTo(RealmDungeonPlanningStatus.Conflict));

            RealmDungeonLifecyclePlanner restarted = Planner();
            RealmDungeonPlanningResult afterRestart = restarted.Plan(
                Request(
                    RealmDungeonOperation.Observe,
                    "operation_observe_restart",
                    clock: ClockZero + 30,
                    expectedRevision: first.Plan.CandidateSnapshot.Revision),
                first.Plan.CandidateSnapshot);
            Assert.That(afterRestart.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared));
            Assert.That(afterRestart.Plan.CandidateSnapshot.InstanceId, Is.EqualTo(InstanceId));
            Assert.That(afterRestart.Plan.CandidateSnapshot.LifeState, Is.EqualTo(RealmDungeonLifeState.Cooldown));
            Assert.That(afterRestart.Plan.CandidateSnapshot.Receipts.Count, Is.EqualTo(1));
        }

        [Test]
        public void TwoEntrancesShareOneDragonAndSecondLeaseIsRejected()
        {
            RealmDungeonLifecyclePlanner planner = Planner();
            RealmDungeonAuthoritySnapshot idle = Alive(RealmDungeonLifeState.AliveIdle);

            RealmDungeonPlanningResult entranceOne = planner.Plan(
                Request(
                    RealmDungeonOperation.Observe,
                    "operation_observe_e1",
                    entranceId: CrownlandsDungeon + "_entrance_01"),
                idle);
            RealmDungeonPlanningResult entranceTwo = planner.Plan(
                Request(
                    RealmDungeonOperation.Observe,
                    "operation_observe_e2",
                    entranceId: CrownlandsDungeon + "_entrance_02"),
                idle);
            Assert.That(entranceOne.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared));
            Assert.That(entranceTwo.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared));
            Assert.That(entranceOne.Plan.CandidateSnapshot.InstanceId, Is.EqualTo(InstanceId));
            Assert.That(
                entranceTwo.Plan.CandidateSnapshot.InstanceId,
                Is.EqualTo(entranceOne.Plan.CandidateSnapshot.InstanceId));

            RealmDungeonAuthoritySnapshot eligible = Alive(
                RealmDungeonLifeState.RespawnEligible,
                nextEligibleAt: ClockZero,
                presentationApproved: true);
            RealmDungeonPlanningResult firstLease = planner.Plan(
                Request(
                    RealmDungeonOperation.BeginManifestation,
                    "operation_lease_a",
                    leaseId: LeaseId,
                    spawnCycleId: SpawnCycleId,
                    presentationApproved: true),
                eligible);
            Assert.That(firstLease.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared));
            Assert.That(firstLease.Plan.CandidateSnapshot.LifeState, Is.EqualTo(RealmDungeonLifeState.Manifesting));
            Assert.That(firstLease.Plan.CandidateSnapshot.Targetable, Is.False);
            Assert.That(firstLease.Plan.CandidateSnapshot.Invulnerable, Is.True);

            RealmDungeonPlanningResult secondLease = planner.Plan(
                Request(
                    RealmDungeonOperation.BeginManifestation,
                    "operation_lease_b",
                    leaseId: "lease_crownlands_002",
                    spawnCycleId: "spawn_cycle_crownlands_002",
                    presentationApproved: true,
                    expectedRevision: firstLease.Plan.CandidateSnapshot.Revision),
                firstLease.Plan.CandidateSnapshot);
            Assert.That(secondLease.Status, Is.EqualTo(RealmDungeonPlanningStatus.Rejected));
            Assert.That(secondLease.Reason, Is.EqualTo(RealmDungeonRejectReason.DuplicateLease));
            Assert.That(firstLease.Plan.CandidateSnapshot.InstanceId, Is.EqualTo(InstanceId));
        }

        [Test]
        public void ActiveLeaseCannotReplaceSpawnCycleButExactRetryPreservesIt()
        {
            RealmDungeonLifecyclePlanner planner = Planner();
            RealmDungeonAuthoritySnapshot manifesting = planner.Plan(
                Request(RealmDungeonOperation.BeginManifestation, "operation_begin",
                    leaseId: LeaseId, spawnCycleId: SpawnCycleId, presentationApproved: true),
                Alive(RealmDungeonLifeState.RespawnEligible, presentationApproved: true)).Plan.CandidateSnapshot;

            RealmDungeonPlanningResult conflicting = planner.Plan(
                Request(RealmDungeonOperation.BeginManifestation, "operation_replace_cycle",
                    expectedRevision: manifesting.Revision, leaseId: LeaseId,
                    spawnCycleId: "spawn_cycle_other", presentationApproved: true),
                manifesting);
            Assert.That(conflicting.Status, Is.EqualTo(RealmDungeonPlanningStatus.Rejected));
            Assert.That(conflicting.Reason, Is.EqualTo(RealmDungeonRejectReason.DuplicateLease));
            Assert.That(conflicting.Plan, Is.Null);
            Assert.That(manifesting.SpawnCycleId, Is.EqualTo(SpawnCycleId));

            RealmDungeonPlanningResult retry = planner.Plan(
                Request(RealmDungeonOperation.BeginManifestation, "operation_begin",
                    expectedRevision: manifesting.Revision, leaseId: LeaseId,
                    spawnCycleId: SpawnCycleId, presentationApproved: true),
                manifesting);
            Assert.That(retry.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared));
            Assert.That(retry.Plan.CandidateSnapshot.LeaseId, Is.EqualTo(LeaseId));
            Assert.That(retry.Plan.CandidateSnapshot.SpawnCycleId, Is.EqualTo(SpawnCycleId));
            Assert.That(retry.Plan.CandidateSnapshot.Targetable, Is.False);
        }

        [Test]
        public void InwardPortalAndMissingBundleFailClosedWithoutGenericFallback()
        {
            RealmDungeonLifecyclePlanner planner = Planner();
            RealmDungeonAuthoritySnapshot eligible = Alive(RealmDungeonLifeState.RespawnEligible);

            RealmDungeonPlanningResult inward = planner.Plan(
                Request(
                    RealmDungeonOperation.TraversePortal,
                    "operation_inward",
                    traversal: RealmDungeonPortalTraversal.Inward,
                    presentationApproved: true),
                eligible);
            Assert.That(inward.Status, Is.EqualTo(RealmDungeonPlanningStatus.Rejected));
            Assert.That(inward.Reason, Is.EqualTo(RealmDungeonRejectReason.InwardPortalTraversal));

            RealmDungeonPlanningResult missingBundle = planner.Plan(
                Request(
                    RealmDungeonOperation.BeginManifestation,
                    "operation_missing_bundle",
                    leaseId: LeaseId,
                    spawnCycleId: SpawnCycleId,
                    presentationApproved: false),
                eligible);
            Assert.That(missingBundle.Status, Is.EqualTo(RealmDungeonPlanningStatus.Unavailable));
            Assert.That(missingBundle.Reason, Is.EqualTo(RealmDungeonRejectReason.MissingPresentationBundle));
            Assert.That(missingBundle.UsedGenericFallback, Is.False);
            Assert.That(eligible.LifeState, Is.EqualTo(RealmDungeonLifeState.RespawnEligible));
            Assert.That(planner.Catalog.ProductionEligible, Is.False);
        }

        [Test]
        public void PortalTraversalRequiresExactActiveManifestationBinding()
        {
            RealmDungeonLifecyclePlanner planner = Planner();
            foreach (RealmDungeonLifeState state in Enum.GetValues(typeof(RealmDungeonLifeState)))
            {
                RealmDungeonPlanningResult unfenced = planner.Plan(
                    Request(RealmDungeonOperation.TraversePortal, "operation_unfenced",
                        traversal: RealmDungeonPortalTraversal.Outward),
                    Alive(state));
                Assert.That(unfenced.Status, Is.Not.EqualTo(RealmDungeonPlanningStatus.Prepared), state.ToString());
            }

            RealmDungeonAuthoritySnapshot manifesting = planner.Plan(
                Request(RealmDungeonOperation.BeginManifestation, "operation_begin",
                    leaseId: LeaseId, spawnCycleId: SpawnCycleId, presentationApproved: true),
                Alive(RealmDungeonLifeState.RespawnEligible, presentationApproved: true)).Plan.CandidateSnapshot;
            RealmDungeonPlanningResult matching = planner.Plan(
                Request(RealmDungeonOperation.TraversePortal, "operation_outward",
                    expectedRevision: manifesting.Revision, traversal: RealmDungeonPortalTraversal.Outward,
                    leaseId: LeaseId, spawnCycleId: SpawnCycleId, presentationApproved: true),
                manifesting);
            Assert.That(matching.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared));
            Assert.That(matching.Plan.CandidateSnapshot.Targetable, Is.False);

            foreach (RealmDungeonPortalTraversal traversal in new[]
                { RealmDungeonPortalTraversal.None, RealmDungeonPortalTraversal.Inward, RealmDungeonPortalTraversal.Ambient })
            {
                Assert.That(planner.Plan(
                    Request(RealmDungeonOperation.TraversePortal, "operation_invalid_direction",
                        expectedRevision: manifesting.Revision, traversal: traversal,
                        leaseId: LeaseId, spawnCycleId: SpawnCycleId, presentationApproved: true),
                    manifesting).Status, Is.EqualTo(RealmDungeonPlanningStatus.Rejected));
            }

            Assert.That(planner.Plan(
                Request(RealmDungeonOperation.TraversePortal, "operation_wrong_lease",
                    expectedRevision: manifesting.Revision, traversal: RealmDungeonPortalTraversal.Outward,
                    leaseId: "lease_other", spawnCycleId: SpawnCycleId, presentationApproved: true),
                manifesting).Status, Is.EqualTo(RealmDungeonPlanningStatus.Rejected));
            Assert.That(planner.Plan(
                Request(RealmDungeonOperation.TraversePortal, "operation_wrong_cycle",
                    expectedRevision: manifesting.Revision, traversal: RealmDungeonPortalTraversal.Outward,
                    leaseId: LeaseId, spawnCycleId: "cycle_other", presentationApproved: true),
                manifesting).Status, Is.EqualTo(RealmDungeonPlanningStatus.Rejected));
            Assert.That(planner.Plan(
                Request(RealmDungeonOperation.TraversePortal, "operation_wrong_portal",
                    expectedRevision: manifesting.Revision, traversal: RealmDungeonPortalTraversal.Outward,
                    leaseId: LeaseId, spawnCycleId: SpawnCycleId, presentationApproved: true,
                    portalId: "portal_other"),
                manifesting).Status, Is.EqualTo(RealmDungeonPlanningStatus.Rejected));
            Assert.That(planner.Plan(
                Request(RealmDungeonOperation.TraversePortal, "operation_missing_presentation",
                    expectedRevision: manifesting.Revision, traversal: RealmDungeonPortalTraversal.Outward,
                    leaseId: LeaseId, spawnCycleId: SpawnCycleId),
                manifesting).Status, Is.EqualTo(RealmDungeonPlanningStatus.Unavailable));
        }

        [Test]
        public void ManifestationBoundariesRejectWrongPortalAndForbiddenDirection()
        {
            RealmDungeonLifecyclePlanner planner = Planner();
            RealmDungeonAuthoritySnapshot eligible = Alive(RealmDungeonLifeState.RespawnEligible, presentationApproved: true);
            RealmDungeonAuthoritySnapshot manifesting = planner.Plan(
                Request(RealmDungeonOperation.BeginManifestation, "operation_begin",
                    leaseId: LeaseId, spawnCycleId: SpawnCycleId, presentationApproved: true),
                eligible).Plan.CandidateSnapshot;

            foreach (RealmDungeonOperation operation in new[]
                { RealmDungeonOperation.BeginManifestation, RealmDungeonOperation.CompleteManifestation })
            {
                RealmDungeonAuthoritySnapshot snapshot = operation == RealmDungeonOperation.BeginManifestation
                    ? eligible : manifesting;
                foreach (RealmDungeonPortalTraversal traversal in new[]
                    { RealmDungeonPortalTraversal.Inward, RealmDungeonPortalTraversal.Ambient })
                {
                    Assert.That(planner.Plan(
                        Request(operation, "operation_forbidden_direction",
                            expectedRevision: snapshot.Revision, leaseId: LeaseId,
                            spawnCycleId: SpawnCycleId, presentationApproved: true, traversal: traversal),
                        snapshot).Status, Is.EqualTo(RealmDungeonPlanningStatus.Rejected), operation + ":" + traversal);
                }

                foreach (string portalId in new[] { "portal_other", string.Empty })
                {
                    Assert.That(planner.Plan(
                        Request(operation, "operation_wrong_portal",
                            expectedRevision: snapshot.Revision, leaseId: LeaseId,
                            spawnCycleId: SpawnCycleId, presentationApproved: true, portalId: portalId),
                        snapshot).Status, Is.EqualTo(RealmDungeonPlanningStatus.Rejected), operation.ToString());
                }
            }
        }

        [Test]
        public void CompleteManifestationRejectsMissingSnapshotLease()
        {
            RealmDungeonPlanningResult result = Planner().Plan(
                Request(RealmDungeonOperation.CompleteManifestation, "operation_empty_lease", presentationApproved: true),
                Alive(RealmDungeonLifeState.Manifesting, presentationApproved: true));
            Assert.That(result.Status, Is.EqualTo(RealmDungeonPlanningStatus.Rejected));
            Assert.That(result.Plan, Is.Null);
        }

        [Test]
        public void TimeRollbackUnavailableClockAndSplitBrainFreezeRecovery()
        {
            RealmDungeonLifecyclePlanner planner = Planner();
            RealmDungeonAuthoritySnapshot cooling = Alive(
                RealmDungeonLifeState.Cooldown,
                defeatCommittedAt: ClockZero,
                nextEligibleAt: ClockZero + CooldownSeconds,
                lastObservedClock: ClockZero + 50);

            RealmDungeonPlanningResult rollback = planner.Plan(
                Request(RealmDungeonOperation.Observe, "operation_rollback", clock: ClockZero + 10),
                cooling);
            Assert.That(rollback.Status, Is.EqualTo(RealmDungeonPlanningStatus.RecoveryRequired));
            Assert.That(rollback.Plan.CandidateSnapshot.LifeState, Is.EqualTo(RealmDungeonLifeState.RecoveryRequired));

            RealmDungeonPlanningResult missingTime = planner.Plan(
                Request(
                    RealmDungeonOperation.ReportFault,
                    "operation_missing_time",
                    fault: RealmDungeonFaultKind.TrustedTimeUnavailable),
                cooling);
            Assert.That(missingTime.Status, Is.EqualTo(RealmDungeonPlanningStatus.RecoveryRequired));

            RealmDungeonPlanningResult splitBrain = planner.Plan(
                Request(
                    RealmDungeonOperation.ReportFault,
                    "operation_split_brain",
                    fault: RealmDungeonFaultKind.SplitBrainOwnership),
                cooling);
            Assert.That(splitBrain.Status, Is.EqualTo(RealmDungeonPlanningStatus.RecoveryRequired));
            Assert.That(cooling.LifeState, Is.EqualTo(RealmDungeonLifeState.Cooldown));
        }

        [Test]
        public void ManifestationStaysUntargetableUntilEmergenceCompletes()
        {
            RealmDungeonLifecyclePlanner planner = Planner();
            RealmDungeonAuthoritySnapshot eligible = Alive(
                RealmDungeonLifeState.RespawnEligible,
                presentationApproved: true);
            RealmDungeonPlanningResult begin = planner.Plan(
                Request(
                    RealmDungeonOperation.BeginManifestation,
                    "operation_manifest_begin",
                    leaseId: LeaseId,
                    spawnCycleId: SpawnCycleId,
                    traversal: RealmDungeonPortalTraversal.Outward,
                    presentationApproved: true),
                eligible);
            Assert.That(begin.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared));
            Assert.That(begin.Plan.CandidateSnapshot.LifeState, Is.EqualTo(RealmDungeonLifeState.Manifesting));
            Assert.That(begin.Plan.CandidateSnapshot.Targetable, Is.False);
            Assert.That(begin.Plan.CandidateSnapshot.Invulnerable, Is.True);
            Assert.That(begin.Plan.CandidateSnapshot.LeaseId, Is.EqualTo(LeaseId));

            RealmDungeonPlanningResult complete = planner.Plan(
                Request(
                    RealmDungeonOperation.CompleteManifestation,
                    "operation_manifest_complete",
                    leaseId: LeaseId,
                    spawnCycleId: SpawnCycleId,
                    presentationApproved: true,
                    expectedRevision: begin.Plan.CandidateSnapshot.Revision),
                begin.Plan.CandidateSnapshot);
            Assert.That(complete.Status, Is.EqualTo(RealmDungeonPlanningStatus.Prepared));
            Assert.That(complete.Plan.CandidateSnapshot.LifeState, Is.EqualTo(RealmDungeonLifeState.AliveIdle));
            Assert.That(complete.Plan.CandidateSnapshot.Targetable, Is.True);
            Assert.That(complete.Plan.CandidateSnapshot.Invulnerable, Is.False);
            Assert.That(complete.Plan.CandidateSnapshot.InstanceId, Is.EqualTo(InstanceId));
        }

        private static RealmDungeonLifecyclePlanner Planner()
        {
            return new RealmDungeonLifecyclePlanner(Catalog());
        }

        private static RealmDungeonCatalogSnapshot Catalog()
        {
            return new RealmDungeonCatalogSnapshot(
                RealmDungeonCatalogStatus.Ready,
                new RealmDungeonCatalogBinding(
                    "al_realm_dungeon_catalog",
                    "realm_dungeon_catalog_v1",
                    CatalogHash),
                CooldownSeconds,
                true,
                false,
                false,
                "closed_raid_",
                new[]
                {
                    "raid_boss_iron_colossus",
                    "raid_boss_ash_seraph",
                    "raid_boss_thorn_wraith",
                    "raid_boss_veil_regent"
                },
                new[]
                {
                    "dragon_crownlands_dawn_regent",
                    "dragon_stonehold_iron_wyrm",
                    "dragon_eldergrove_moonbough",
                    "dragon_umbral_void_seraph"
                },
                new[]
                {
                    Dungeon(CrownlandsDungeon, "crownlands", CrownlandsRaid, CrownlandsGuardian),
                    Dungeon(StoneholdDungeon, "stonehold", StoneholdRaid, "dragon_stonehold_iron_wyrm"),
                    Dungeon(EldergroveDungeon, "eldergrove", EldergroveRaid, "dragon_eldergrove_moonbough"),
                    Dungeon(UmbralDungeon, "umbral", UmbralRaid, "dragon_umbral_void_seraph")
                },
                true);
        }

        private static RealmDungeonDefinition Dungeon(
            string dungeonId,
            string realmId,
            string raidId,
            string presentationRef)
        {
            return new RealmDungeonDefinition(
                dungeonId,
                realmId,
                new[] { dungeonId + "_entrance_01", dungeonId + "_entrance_02" },
                dungeonId + "_portal",
                raidId,
                presentationRef,
                string.Empty,
                false,
                false);
        }

        private static RealmDungeonAuthoritySnapshot Alive(
            RealmDungeonLifeState lifeState,
            long defeatCommittedAt = 0,
            long nextEligibleAt = 0,
            long lastObservedClock = ClockZero,
            bool presentationApproved = false,
            IEnumerable<RealmDungeonReceipt> receipts = null)
        {
            return new RealmDungeonAuthoritySnapshot(
                CrownlandsDungeon,
                CrownlandsRaid,
                InstanceId,
                lifeState,
                defeatCommittedAt,
                nextEligibleAt,
                lastObservedClock,
                string.Empty,
                string.Empty,
                lifeState != RealmDungeonLifeState.Manifesting &&
                lifeState != RealmDungeonLifeState.RecoveryRequired &&
                lifeState != RealmDungeonLifeState.Cooldown &&
                lifeState != RealmDungeonLifeState.RespawnEligible,
                lifeState == RealmDungeonLifeState.Manifesting ||
                lifeState == RealmDungeonLifeState.RecoveryRequired ||
                lifeState == RealmDungeonLifeState.Cooldown ||
                lifeState == RealmDungeonLifeState.RespawnEligible,
                presentationApproved,
                false,
                1,
                receipts ?? Array.Empty<RealmDungeonReceipt>());
        }

        private static RealmDungeonTransitionRequest Request(
            RealmDungeonOperation operation,
            string operationId,
            string dungeonId = CrownlandsDungeon,
            string raidDragonId = CrownlandsRaid,
            string entranceId = CrownlandsDungeon + "_entrance_01",
            long clock = ClockZero,
            long expectedRevision = 1,
            RealmDungeonNonKillKind nonKillKind = RealmDungeonNonKillKind.None,
            RealmDungeonFaultKind fault = RealmDungeonFaultKind.None,
            RealmDungeonPortalTraversal traversal = RealmDungeonPortalTraversal.None,
            string leaseId = "",
            string spawnCycleId = "",
            bool presentationApproved = false,
            string portalId = null)
        {
            return new RealmDungeonTransitionRequest(
                operation,
                operationId,
                dungeonId,
                raidDragonId,
                entranceId,
                portalId ?? dungeonId + "_portal",
                DefeatId,
                leaseId,
                spawnCycleId,
                clock,
                expectedRevision,
                nonKillKind,
                fault,
                traversal,
                presentationApproved,
                new RealmDungeonCatalogBinding(
                    "al_realm_dungeon_catalog",
                    "realm_dungeon_catalog_v1",
                    CatalogHash));
        }
    }
}
