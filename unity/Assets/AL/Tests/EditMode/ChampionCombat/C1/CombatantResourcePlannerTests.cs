using System;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.C1;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionCombat.C1
{
    public sealed class CombatantResourcePlannerTests
    {
        [Test]
        public void DamageHealAndDefeatAreImmutableIdempotentAndExactlyOnce()
        {
            CombatantResourceSnapshot initial = CreateSnapshot(
                currentHealth: 100L,
                currentMana: 50L);
            CombatantResourceOperationRequest damage =
                CombatantResourceOperationRequest.Damage(
                    Id("damage-1"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("source-actor-1"),
                    Id("source-behavior-1"),
                    Id("source-1"),
                    40L,
                    initial.Revision);

            CombatantResourcePlanResult damaged =
                CombatantResourcePlanner.Plan(initial, damage);

            Assert.AreEqual(CombatantResourcePlanStatus.Applied, damaged.Status);
            Assert.AreEqual(100L, initial.CurrentHealthMicros);
            Assert.AreEqual(60L, damaged.Snapshot.CurrentHealthMicros);
            Assert.AreNotEqual(initial.Revision, damaged.Snapshot.Revision);
            Assert.AreEqual(1, damaged.Events.Count);
            Assert.AreEqual(
                CombatantResourceEventKind.ResourcesChanged,
                damaged.Events[0].Kind);
            Assert.AreEqual(
                initial.Revision,
                damaged.Events[0].BeforeResourceRevision);
            Assert.AreEqual(
                damaged.Snapshot.Revision,
                damaged.Events[0].AfterResourceRevision);
            Assert.AreEqual(Id("source-1"), damaged.Events[0].CorrelationId);
            Assert.AreEqual(
                Id("source-actor-1"),
                damaged.Events[0].SourceParticipantId);
            Assert.AreEqual(
                Id("source-behavior-1"),
                damaged.Events[0].SourceBehaviorId);

            CombatantResourcePlanResult duplicate =
                CombatantResourcePlanner.Plan(damaged.Snapshot, damage);
            Assert.AreEqual(
                CombatantResourcePlanStatus.DuplicateExact,
                duplicate.Status);
            Assert.AreSame(damaged.OperationReceipt, duplicate.ExistingReceipt);
            Assert.AreEqual(0, duplicate.Events.Count);
            Assert.AreSame(damaged.Snapshot, duplicate.Snapshot);

            CombatantResourceOperationRequest changedReuse =
                CombatantResourceOperationRequest.Damage(
                    Id("damage-1"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("source-actor-1"),
                    Id("source-behavior-1"),
                    Id("source-1"),
                    41L,
                    initial.Revision);
            Assert.AreEqual(
                CombatantResourcePlanStatus.CorrelationConflict,
                CombatantResourcePlanner.Plan(
                    damaged.Snapshot,
                    changedReuse).Status);
            CombatantResourceOperationRequest changedSource =
                CombatantResourceOperationRequest.Damage(
                    Id("damage-1"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("source-actor-other"),
                    Id("source-behavior-1"),
                    Id("source-1"),
                    40L,
                    initial.Revision);
            Assert.AreEqual(
                CombatantResourcePlanStatus.CorrelationConflict,
                CombatantResourcePlanner.Plan(
                    damaged.Snapshot,
                    changedSource).Status);

            CombatantResourcePlanResult healed = CombatantResourcePlanner.Plan(
                damaged.Snapshot,
                CombatantResourceOperationRequest.Healing(
                    Id("heal-1"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("source-actor-1"),
                    Id("source-behavior-1"),
                    Id("source-2"),
                    10L,
                    damaged.Snapshot.Revision));
            Assert.AreEqual(70L, healed.Snapshot.CurrentHealthMicros);

            CombatantResourcePlanResult defeated = CombatantResourcePlanner.Plan(
                healed.Snapshot,
                CombatantResourceOperationRequest.Damage(
                    Id("damage-lethal"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("source-actor-1"),
                    Id("source-behavior-1"),
                    Id("source-3"),
                    1_000L,
                    healed.Snapshot.Revision));
            Assert.AreEqual(
                CombatantResourcePlanStatus.AppliedAndDefeated,
                defeated.Status);
            Assert.AreEqual(0L, defeated.Snapshot.CurrentHealthMicros);
            Assert.AreEqual(
                CombatantLifeState.Defeated,
                defeated.Snapshot.LifeState);
            Assert.AreEqual(
                1,
                defeated.Events.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatantResourceEventKind.CombatantDefeated));

            CombatantResourcePlanResult afterDefeat =
                CombatantResourcePlanner.Plan(
                    defeated.Snapshot,
                    CombatantResourceOperationRequest.Damage(
                        Id("damage-after-defeat"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("source-actor-1"),
                        Id("source-behavior-1"),
                        Id("source-4"),
                        1L,
                        defeated.Snapshot.Revision));
            Assert.AreEqual(
                CombatantResourcePlanStatus.NotAlive,
                afterDefeat.Status);
            Assert.AreEqual(0, afterDefeat.Events.Count);

            CombatantResourcePlanResult healAfterDefeat =
                CombatantResourcePlanner.Plan(
                    defeated.Snapshot,
                    CombatantResourceOperationRequest.Healing(
                        Id("heal-after-defeat"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("source-actor-1"),
                        Id("source-behavior-1"),
                        Id("source-5"),
                        50L,
                        defeated.Snapshot.Revision));
            Assert.AreEqual(
                CombatantResourcePlanStatus.NotAlive,
                healAfterDefeat.Status);
            Assert.AreEqual(0L, healAfterDefeat.Snapshot.CurrentHealthMicros);
        }

        [Test]
        public void ContextRevisionAndAmountChecksFailClosedWithoutMutation()
        {
            CombatantResourceSnapshot snapshot = CreateSnapshot();
            CombatantResourceOperationRequest[] requests =
            {
                CombatantResourceOperationRequest.Damage(
                    Id("wrong-session"),
                    Id("session-other"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("source-actor-1"),
                    Id("source-behavior-1"),
                    Id("source-1"),
                    1L,
                    snapshot.Revision),
                CombatantResourceOperationRequest.Damage(
                    Id("wrong-attempt"),
                    Id("session-1"),
                    Id("attempt-other"),
                    Id("actor-1"),
                    Id("source-actor-1"),
                    Id("source-behavior-1"),
                    Id("source-2"),
                    1L,
                    snapshot.Revision),
                CombatantResourceOperationRequest.Damage(
                    Id("wrong-actor"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-other"),
                    Id("source-actor-1"),
                    Id("source-behavior-1"),
                    Id("source-3"),
                    1L,
                    snapshot.Revision),
                CombatantResourceOperationRequest.Damage(
                    Id("stale"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("source-actor-1"),
                    Id("source-behavior-1"),
                    Id("source-4"),
                    1L,
                    "resource-r9999999999999999"),
                CombatantResourceOperationRequest.Damage(
                    Id("negative"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("source-actor-1"),
                    Id("source-behavior-1"),
                    Id("source-5"),
                    -1L,
                    snapshot.Revision),
                CombatantResourceOperationRequest.Damage(
                    Id("above-maximum"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("source-actor-1"),
                    Id("source-behavior-1"),
                    Id("source-8"),
                    CombatTechnicalLimits
                        .HealthManaDamageHealingAttackPowerMaximumMicros + 1L,
                    snapshot.Revision)
            };
            CombatantResourcePlanStatus[] expected =
            {
                CombatantResourcePlanStatus.WrongEncounter,
                CombatantResourcePlanStatus.WrongEncounter,
                CombatantResourcePlanStatus.WrongActor,
                CombatantResourcePlanStatus.StaleRevision,
                CombatantResourcePlanStatus.NegativeAmount,
                CombatantResourcePlanStatus.AmountAboveMaximum
            };

            for (int index = 0; index < requests.Length; index++)
            {
                CombatantResourcePlanResult result =
                    CombatantResourcePlanner.Plan(snapshot, requests[index]);
                Assert.AreEqual(expected[index], result.Status, "case " + index);
                Assert.AreSame(snapshot, result.Snapshot, "case " + index);
                Assert.AreEqual(0, result.Events.Count, "case " + index);
                Assert.IsNull(result.OperationReceipt, "case " + index);
            }

            CombatantResourceOperationRequest invalidIdentity =
                CombatantResourceOperationRequest.Damage(
                    default,
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("source-actor-1"),
                    Id("source-behavior-1"),
                    Id("source-6"),
                    1L,
                    snapshot.Revision);
            Assert.AreEqual(
                CombatantResourcePlanStatus.InvalidRequest,
                CombatantResourcePlanner.Plan(
                    snapshot,
                    invalidIdentity).Status);

            CombatantResourcePlanResult zero = CombatantResourcePlanner.Plan(
                snapshot,
                CombatantResourceOperationRequest.Damage(
                    Id("zero-damage"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("source-actor-1"),
                    Id("source-behavior-1"),
                    Id("source-7"),
                    0L,
                    snapshot.Revision));
            Assert.AreEqual(
                CombatantResourcePlanStatus.NoChangeZero,
                zero.Status);
            Assert.AreEqual(snapshot.Revision, zero.Snapshot.Revision);
            Assert.AreEqual(0, zero.Events.Count);
        }

        [Test]
        public void ManaReserveCommitReleaseAndCorrelationAreExact()
        {
            CombatantResourceSnapshot initial = CreateSnapshot(
                currentMana: 50L,
                maxMana: 100L);
            CombatantResourceOperationRequest reserve =
                CombatantResourceOperationRequest.ReserveMana(
                    Id("reserve-1"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("action-1"),
                    25L,
                    initial.Revision);
            CombatantResourcePlanResult reserved =
                CombatantResourcePlanner.Plan(initial, reserve);

            Assert.AreEqual(CombatantResourcePlanStatus.Applied, reserved.Status);
            Assert.AreEqual(50L, reserved.Snapshot.CurrentManaMicros);
            Assert.AreEqual(25L, reserved.Snapshot.ReservedManaMicros);
            Assert.AreEqual(25L, reserved.Snapshot.AvailableManaMicros);
            Assert.AreEqual(1, reserved.Snapshot.Reservations.Count);

            CombatantResourcePlanResult committed =
                CombatantResourcePlanner.Plan(
                    reserved.Snapshot,
                    CombatantResourceOperationRequest.CommitManaReservation(
                        Id("commit-1"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("action-1"),
                        reserved.Snapshot.Revision));
            Assert.AreEqual(CombatantResourcePlanStatus.Applied, committed.Status);
            Assert.AreEqual(25L, committed.Snapshot.CurrentManaMicros);
            Assert.AreEqual(0L, committed.Snapshot.ReservedManaMicros);
            Assert.AreEqual(0, committed.Snapshot.Reservations.Count);

            CombatantResourcePlanResult doubleCommit =
                CombatantResourcePlanner.Plan(
                    committed.Snapshot,
                    CombatantResourceOperationRequest.CommitManaReservation(
                        Id("commit-2"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("action-1"),
                        committed.Snapshot.Revision));
            Assert.AreEqual(
                CombatantResourcePlanStatus.ReservationAlreadyFinalized,
                doubleCommit.Status);

            CombatantResourcePlanResult actionReuse =
                CombatantResourcePlanner.Plan(
                    committed.Snapshot,
                    CombatantResourceOperationRequest.ReserveMana(
                        Id("reserve-reuse"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("action-1"),
                        25L,
                        committed.Snapshot.Revision));
            Assert.AreEqual(
                CombatantResourcePlanStatus.CorrelationConflict,
                actionReuse.Status);

            CombatantResourcePlanResult reserveSecond =
                CombatantResourcePlanner.Plan(
                    committed.Snapshot,
                    CombatantResourceOperationRequest.ReserveMana(
                        Id("reserve-2"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("action-2"),
                        10L,
                        committed.Snapshot.Revision));
            CombatantResourceOperationRequest release =
                CombatantResourceOperationRequest.ReleaseManaReservation(
                    Id("release-2"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("action-2"),
                    "manual-cancel",
                    reserveSecond.Snapshot.Revision);
            CombatantResourcePlanResult released =
                CombatantResourcePlanner.Plan(reserveSecond.Snapshot, release);
            Assert.AreEqual(CombatantResourcePlanStatus.Applied, released.Status);
            Assert.AreEqual(25L, released.Snapshot.CurrentManaMicros);
            Assert.AreEqual(0L, released.Snapshot.ReservedManaMicros);
            Assert.AreEqual(0, released.Snapshot.Reservations.Count);

            CombatantResourcePlanResult releaseDuplicate =
                CombatantResourcePlanner.Plan(released.Snapshot, release);
            Assert.AreEqual(
                CombatantResourcePlanStatus.DuplicateExact,
                releaseDuplicate.Status);
            Assert.AreSame(released.OperationReceipt, releaseDuplicate.ExistingReceipt);
            Assert.AreEqual(0, releaseDuplicate.Events.Count);
        }

        [Test]
        public void SequentialFinalizedReservationsDoNotCreateLifetimeSixtyFourCastCap()
        {
            CombatantResourceSnapshot snapshot = CreateSnapshot(
                currentMana: 100L,
                maxMana: 100L);

            for (int index = 0; index < 65; index++)
            {
                CombatantResourcePlanResult reserved =
                    CombatantResourcePlanner.Plan(
                        snapshot,
                        CombatantResourceOperationRequest.ReserveMana(
                            Id("reserve-" + index),
                            Id("session-1"),
                            Id("attempt-1"),
                            Id("actor-1"),
                            Id("action-" + index),
                            1L,
                            snapshot.Revision));
                Assert.AreEqual(
                    CombatantResourcePlanStatus.Applied,
                    reserved.Status,
                    "reserve " + index);
                Assert.AreEqual(1, reserved.Snapshot.Reservations.Count);

                CombatantResourcePlanResult released =
                    CombatantResourcePlanner.Plan(
                        reserved.Snapshot,
                        CombatantResourceOperationRequest.ReleaseManaReservation(
                            Id("release-" + index),
                            Id("session-1"),
                            Id("attempt-1"),
                            Id("actor-1"),
                            Id("action-" + index),
                            "bounded-cycle",
                            reserved.Snapshot.Revision));
                Assert.AreEqual(
                    CombatantResourcePlanStatus.Applied,
                    released.Status,
                    "release " + index);
                Assert.AreEqual(0, released.Snapshot.Reservations.Count);
                snapshot = released.Snapshot;
            }

            Assert.AreEqual(100L, snapshot.CurrentManaMicros);
            Assert.AreEqual(0L, snapshot.ReservedManaMicros);
            Assert.AreEqual(130, snapshot.OperationReceipts.Count);
        }

        [Test]
        public void ReservationCapacityPreservesOneFinalizationSlotAndTerminalNoOps()
        {
            CombatantResourceSnapshot snapshot = CreateSnapshot(
                currentMana: 100L,
                maxMana: 100L);
            for (int index = 0;
                 index <
                 CombatantResourceSnapshot.MaximumOperationReceipts - 2;
                 index++)
            {
                CombatantResourcePlanResult recorded =
                    CombatantResourcePlanner.Plan(
                        snapshot,
                        CombatantResourceOperationRequest.RestoreMana(
                            Id("capacity-fill-" + index),
                            Id("session-1"),
                            Id("attempt-1"),
                            Id("actor-1"),
                            Id("capacity-source-" + index),
                            0L,
                            snapshot.Revision));
                Assert.AreEqual(
                    CombatantResourcePlanStatus.NoChangeZero,
                    recorded.Status,
                    "fill " + index);
                snapshot = recorded.Snapshot;
            }

            CombatantResourcePlanResult reserved =
                CombatantResourcePlanner.Plan(
                    snapshot,
                    CombatantResourceOperationRequest.ReserveMana(
                        Id("capacity-reserve"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("capacity-action"),
                        1L,
                        snapshot.Revision));
            Assert.AreEqual(CombatantResourcePlanStatus.Applied, reserved.Status);
            Assert.AreEqual(
                CombatantResourceSnapshot.MaximumOperationReceipts,
                reserved.Snapshot.OperationReceipts.Count +
                reserved.Snapshot.Reservations.Count);
            Assert.AreEqual(
                CombatantResourcePlanStatus.CapacityReached,
                CombatantResourcePlanner.Plan(
                    reserved.Snapshot,
                    CombatantResourceOperationRequest.RestoreMana(
                        Id("capacity-blocked"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("capacity-blocked-source"),
                        0L,
                        reserved.Snapshot.Revision)).Status);

            CombatantResourceOperationRequest release =
                CombatantResourceOperationRequest.ReleaseManaReservation(
                    Id("capacity-release"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("capacity-action"),
                    "capacity-terminal",
                    reserved.Snapshot.Revision);
            CombatantResourcePlanResult released =
                CombatantResourcePlanner.Plan(reserved.Snapshot, release);
            Assert.AreEqual(CombatantResourcePlanStatus.Applied, released.Status);
            Assert.AreEqual(0, released.Snapshot.Reservations.Count);
            Assert.AreEqual(
                CombatantResourceSnapshot.MaximumOperationReceipts,
                released.Snapshot.OperationReceipts.Count);
            Assert.AreEqual(
                CombatantResourcePlanStatus.DuplicateExact,
                CombatantResourcePlanner.Plan(
                    released.Snapshot,
                    release).Status);
            Assert.AreEqual(
                CombatantResourcePlanStatus.ReservationAlreadyFinalized,
                CombatantResourcePlanner.Plan(
                    released.Snapshot,
                    CombatantResourceOperationRequest.ReleaseManaReservation(
                        Id("capacity-release-again"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("capacity-action"),
                        "capacity-terminal",
                        released.Snapshot.Revision)).Status);
        }

        [Test]
        public void DefeatCanReleaseActiveReservationWithoutSecondLifeEvent()
        {
            CombatantResourceSnapshot initial = CreateSnapshot(
                currentHealth: 10L,
                currentMana: 100L,
                maxMana: 100L);
            CombatantResourcePlanResult reserved =
                CombatantResourcePlanner.Plan(
                    initial,
                    CombatantResourceOperationRequest.ReserveMana(
                        Id("reserve-before-defeat"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("action-before-defeat"),
                        40L,
                        initial.Revision));
            CombatantResourcePlanResult defeated =
                CombatantResourcePlanner.Plan(
                    reserved.Snapshot,
                    CombatantResourceOperationRequest.Damage(
                        Id("defeat-with-reservation"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("source-actor-1"),
                        Id("source-behavior-1"),
                        Id("source-defeat"),
                        10L,
                        reserved.Snapshot.Revision));
            Assert.AreEqual(
                CombatantLifeState.Defeated,
                defeated.Snapshot.LifeState);
            Assert.AreEqual(40L, defeated.Snapshot.ReservedManaMicros);

            CombatantResourcePlanResult released =
                CombatantResourcePlanner.Plan(
                    defeated.Snapshot,
                    CombatantResourceOperationRequest.ReleaseManaReservation(
                        Id("release-after-defeat"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("action-before-defeat"),
                        "actor-defeated",
                        defeated.Snapshot.Revision));
            Assert.AreEqual(CombatantResourcePlanStatus.Applied, released.Status);
            Assert.AreEqual(
                CombatantLifeState.Defeated,
                released.Snapshot.LifeState);
            Assert.AreEqual(100L, released.Snapshot.CurrentManaMicros);
            Assert.AreEqual(0L, released.Snapshot.ReservedManaMicros);
            Assert.AreEqual(1, released.Events.Count);
            Assert.AreEqual(
                CombatantResourceEventKind.ResourcesChanged,
                released.Events[0].Kind);
            Assert.AreEqual(
                0,
                released.Events.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatantResourceEventKind.CombatantDefeated));

            CombatantResourcePlanResult duplicate =
                CombatantResourcePlanner.Plan(released.Snapshot, released.OperationReceipt.Request);
            Assert.AreEqual(
                CombatantResourcePlanStatus.DuplicateExact,
                duplicate.Status);
            Assert.AreEqual(0, duplicate.Events.Count);
        }

        [Test]
        public void RestoreAndRegenerationAreClampedAndPartitionDeterministic()
        {
            CombatantResourceSnapshot half = CreateSnapshot(
                currentMana: 50L,
                maxMana: 100L);
            CombatantResourcePlanResult restored =
                CombatantResourcePlanner.Plan(
                    half,
                    CombatantResourceOperationRequest.RestoreMana(
                        Id("restore-1"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("source-restore"),
                        1_000L,
                        half.Revision));
            Assert.AreEqual(100L, restored.Snapshot.CurrentManaMicros);

            CombatantResourcePlanResult atMaximum =
                CombatantResourcePlanner.Plan(
                    restored.Snapshot,
                    CombatantResourceOperationRequest.RestoreMana(
                        Id("restore-max"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("source-restore-2"),
                        1L,
                        restored.Snapshot.Revision));
            Assert.AreEqual(
                CombatantResourcePlanStatus.NoChangeAtMaximum,
                atMaximum.Status);
            Assert.AreEqual(restored.Snapshot.Revision, atMaximum.Snapshot.Revision);

            CombatantResourceSnapshot partitioned = CreateSnapshot(
                currentMana: 0L,
                maxMana: 100L);
            CombatantResourcePlanResult firstHalf =
                CombatantResourcePlanner.Plan(
                    partitioned,
                    CombatantResourceOperationRequest.RegenerateMana(
                        RegenerationId("tick-half-1", 0L),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        1L,
                        500_000L,
                        Id("clock-1"),
                        true,
                        0L,
                        partitioned.Revision));
            Assert.AreEqual(CombatantResourcePlanStatus.Applied, firstHalf.Status);
            Assert.AreEqual(0L, firstHalf.Snapshot.CurrentManaMicros);
            Assert.AreEqual(
                500_000L,
                firstHalf.Snapshot.RegenerationAccumulatorRemainder);

            CombatantResourcePlanResult secondHalf =
                CombatantResourcePlanner.Plan(
                    firstHalf.Snapshot,
                    CombatantResourceOperationRequest.RegenerateMana(
                        RegenerationId("tick-half-2", 1L),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        1L,
                        500_000L,
                        Id("clock-2"),
                        true,
                        1L,
                        firstHalf.Snapshot.Revision));
            CombatantResourceSnapshot single = CreateSnapshot(
                currentMana: 0L,
                maxMana: 100L);
            CombatantResourcePlanResult whole =
                CombatantResourcePlanner.Plan(
                    single,
                    CombatantResourceOperationRequest.RegenerateMana(
                        RegenerationId("tick-whole", 0L),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        1L,
                        1_000_000L,
                        Id("clock-whole"),
                        true,
                        0L,
                        single.Revision));
            Assert.AreEqual(
                whole.Snapshot.CurrentManaMicros,
                secondHalf.Snapshot.CurrentManaMicros);
            Assert.AreEqual(
                whole.Snapshot.RegenerationAccumulatorRemainder,
                secondHalf.Snapshot.RegenerationAccumulatorRemainder);
            Assert.AreEqual(1L, whole.Snapshot.CurrentManaMicros);

            CombatantResourcePlanResult prohibited =
                CombatantResourcePlanner.Plan(
                    whole.Snapshot,
                    CombatantResourceOperationRequest.RegenerateMana(
                        RegenerationId("tick-prohibited", 1L),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        1L,
                        1L,
                        Id("clock-prohibited"),
                        false,
                        1L,
                        whole.Snapshot.Revision));
            Assert.AreEqual(
                CombatantResourcePlanStatus.RegenerationProhibited,
                prohibited.Status);
        }

        [Test]
        public void RegenerationTickHistoryCompactsWithoutFramePartitionCapacityCap()
        {
            CombatantResourceSnapshot snapshot = CreateSnapshot(
                currentMana: 0L,
                maxMana: 100L);
            CombatantResourceOperationRequest latestRequest = null;

            for (int ordinal = 0; ordinal < 600; ordinal++)
            {
                latestRequest =
                    CombatantResourceOperationRequest.RegenerateMana(
                        RegenerationId("compact-tick", ordinal),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        1L,
                        1L,
                        Id("compact-clock-" + ordinal),
                        true,
                        ordinal,
                        snapshot.Revision);
                CombatantResourcePlanResult tick =
                    CombatantResourcePlanner.Plan(snapshot, latestRequest);
                Assert.AreEqual(
                    CombatantResourcePlanStatus.Applied,
                    tick.Status,
                    "tick " + ordinal);
                snapshot = tick.Snapshot;
            }

            Assert.AreEqual(600L, snapshot.NextRegenerationTickOrdinal);
            Assert.AreSame(
                latestRequest,
                snapshot.LatestRegenerationReceipt.Request);
            Assert.AreEqual(0, snapshot.OperationReceipts.Count);
            Assert.AreEqual(
                CombatantResourceSnapshot.MaximumRegenerationReplayReceipts,
                snapshot.RegenerationReplayReceipts.Count);

            CombatantResourcePlanResult duplicate =
                CombatantResourcePlanner.Plan(snapshot, latestRequest);
            Assert.AreEqual(
                CombatantResourcePlanStatus.DuplicateExact,
                duplicate.Status);
            Assert.AreEqual(0, duplicate.Events.Count);

            CombatantResourceOperationRequest changedLatest =
                CombatantResourceOperationRequest.RegenerateMana(
                    RegenerationId("compact-tick", 599L),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    1L,
                    2L,
                    Id("compact-clock-599"),
                    true,
                    599L,
                    latestRequest.ExpectedResourceRevision);
            Assert.AreEqual(
                CombatantResourcePlanStatus.CorrelationConflict,
                CombatantResourcePlanner.Plan(
                    snapshot,
                    changedLatest).Status);

            CombatantResourceOperationReceipt retainedOlder =
                snapshot.RegenerationReplayReceipts.Single(
                    receipt =>
                        receipt.Request.RegenerationTickOrdinal == 598L);
            Assert.AreEqual(
                CombatantResourcePlanStatus.DuplicateExact,
                CombatantResourcePlanner.Plan(
                    snapshot,
                    retainedOlder.Request).Status);
            CombatantResourceOperationRequest changedOlder =
                CombatantResourceOperationRequest.RegenerateMana(
                    RegenerationId("compact-tick", 598L),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    1L,
                    2L,
                    Id("compact-clock-598"),
                    true,
                    598L,
                    retainedOlder.Request.ExpectedResourceRevision);
            Assert.AreEqual(
                CombatantResourcePlanStatus.CorrelationConflict,
                CombatantResourcePlanner.Plan(
                    snapshot,
                    changedOlder).Status);
            CombatantResourceOperationRequest expired =
                CombatantResourceOperationRequest.RegenerateMana(
                    RegenerationId("compact-tick", 0L),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    1L,
                    1L,
                    Id("compact-clock-0"),
                    true,
                    0L,
                    snapshot.Revision);
            CombatantResourcePlanResult expiredResult =
                CombatantResourcePlanner.Plan(snapshot, expired);
            Assert.AreEqual(
                CombatantResourcePlanStatus.ReplayWindowExpired,
                expiredResult.Status);
            Assert.AreSame(snapshot, expiredResult.Snapshot);
            Assert.AreEqual(0, expiredResult.Events.Count);
            CombatantResourceOperationRequest reusedOlderIdAtNextOrdinal =
                CombatantResourceOperationRequest.RegenerateMana(
                    RegenerationId("compact-tick", 598L),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    1L,
                    1L,
                    Id("compact-clock-next"),
                    true,
                    600L,
                    snapshot.Revision);
            Assert.AreEqual(
                CombatantResourcePlanStatus.CorrelationConflict,
                CombatantResourcePlanner.Plan(
                    snapshot,
                    reusedOlderIdAtNextOrdinal).Status);
            Assert.AreEqual(
                CombatantResourcePlanStatus.CorrelationConflict,
                CombatantResourcePlanner.Plan(
                    snapshot,
                    CombatantResourceOperationRequest.Damage(
                        latestRequest.OperationId,
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("source-actor-1"),
                        Id("source-behavior-1"),
                        Id("cross-kind-source"),
                        1L,
                        snapshot.Revision)).Status);
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatantResourceOperationReceipt>)
                    snapshot.RegenerationReplayReceipts).Add(null));
        }

        [Test]
        public void ReachingManaCapDiscardsFractionalRegenerationCredit()
        {
            CombatantResourceSnapshot snapshot = CreateSnapshot(
                currentMana: 0L,
                maxMana: 100L);
            CombatantResourcePlanResult fractional =
                CombatantResourcePlanner.Plan(
                    snapshot,
                    CombatantResourceOperationRequest.RegenerateMana(
                        RegenerationId("fraction-before-cap", 0L),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        1L,
                        500_000L,
                        Id("clock-before-cap"),
                        true,
                        0L,
                        snapshot.Revision));
            Assert.AreEqual(
                500_000L,
                fractional.Snapshot.RegenerationAccumulatorRemainder);

            CombatantResourcePlanResult capped =
                CombatantResourcePlanner.Plan(
                    fractional.Snapshot,
                    CombatantResourceOperationRequest.RestoreMana(
                        Id("restore-to-cap"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("restore-source"),
                        100L,
                        fractional.Snapshot.Revision));
            Assert.AreEqual(100L, capped.Snapshot.CurrentManaMicros);
            Assert.AreEqual(
                0L,
                capped.Snapshot.RegenerationAccumulatorRemainder);

            CombatantResourcePlanResult reserved =
                CombatantResourcePlanner.Plan(
                    capped.Snapshot,
                    CombatantResourceOperationRequest.ReserveMana(
                        Id("reserve-after-cap"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("action-after-cap"),
                        1L,
                        capped.Snapshot.Revision));
            CombatantResourcePlanResult spent =
                CombatantResourcePlanner.Plan(
                    reserved.Snapshot,
                    CombatantResourceOperationRequest.CommitManaReservation(
                        Id("commit-after-cap"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("action-after-cap"),
                        reserved.Snapshot.Revision));
            Assert.AreEqual(99L, spent.Snapshot.CurrentManaMicros);

            CombatantResourcePlanResult nextHalf =
                CombatantResourcePlanner.Plan(
                    spent.Snapshot,
                    CombatantResourceOperationRequest.RegenerateMana(
                        RegenerationId("fraction-after-cap", 1L),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        1L,
                        500_000L,
                        Id("clock-after-cap"),
                        true,
                        1L,
                        spent.Snapshot.Revision));
            Assert.AreEqual(99L, nextHalf.Snapshot.CurrentManaMicros);
            Assert.AreEqual(
                500_000L,
                nextHalf.Snapshot.RegenerationAccumulatorRemainder);
        }

        [Test]
        public void SnapshotCollectionsAreCopiedReadOnlyAndConstructionIsStrict()
        {
            Assert.False(CombatantResourceSnapshot.TryCreate(
                Id("session-1"),
                Id("attempt-1"),
                Id("actor-1"),
                1L,
                0L,
                0L,
                1L,
                out _));
            Assert.False(CombatantResourceSnapshot.TryCreate(
                default,
                Id("attempt-1"),
                Id("actor-1"),
                1L,
                1L,
                0L,
                1L,
                out _));

            CombatantResourceSnapshot initial = CreateSnapshot();
            CombatantResourcePlanResult reserve =
                CombatantResourcePlanner.Plan(
                    initial,
                    CombatantResourceOperationRequest.ReserveMana(
                        Id("reserve-readonly"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        Id("action-readonly"),
                        1L,
                        initial.Revision));

            Assert.Throws<NotSupportedException>(
                () => ((IList<ManaReservationSnapshot>)
                    reserve.Snapshot.Reservations).Add(null));
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatantResourceOperationReceipt>)
                    reserve.Snapshot.OperationReceipts).Add(null));
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatantResourceEventReceipt>)
                    reserve.Events).Add(null));
        }

        [Test]
        public void CombatantStateMatricesAreTotalAndTerminalControlsAreCoherent()
        {
            CombatantLifeState[] lifeStates =
                (CombatantLifeState[])Enum.GetValues(typeof(CombatantLifeState));
            Assert.AreEqual(
                lifeStates.Length * lifeStates.Length,
                CombatantStatePlanner.LifeTransitionMatrix.Count);
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatantLifeTransitionRule>)
                    CombatantStatePlanner.LifeTransitionMatrix).Add(null));
            foreach (CombatantLifeState from in lifeStates)
            {
                foreach (CombatantLifeState to in lifeStates)
                {
                    CombatantLifeTransitionRule rule =
                        CombatantStatePlanner.LifeTransitionMatrix.Single(
                            candidate =>
                                candidate.From == from &&
                                candidate.To == to);
                    Assert.AreEqual(
                        CombatantStatePlanner.IsLifeTransitionAllowed(from, to),
                        rule.Allowed,
                        from + " -> " + to);
                }
            }

            Assert.True(CombatantStatePlanner.IsLifeTransitionAllowed(
                CombatantLifeState.Uninitialized,
                CombatantLifeState.Alive));
            Assert.True(CombatantStatePlanner.IsLifeTransitionAllowed(
                CombatantLifeState.Alive,
                CombatantLifeState.Defeated));
            Assert.True(CombatantStatePlanner.IsLifeTransitionAllowed(
                CombatantLifeState.Alive,
                CombatantLifeState.Disposed));
            Assert.True(CombatantStatePlanner.IsLifeTransitionAllowed(
                CombatantLifeState.Defeated,
                CombatantLifeState.Disposed));
            Assert.False(CombatantStatePlanner.IsLifeTransitionAllowed(
                CombatantLifeState.Defeated,
                CombatantLifeState.Alive));
            Assert.False(CombatantStatePlanner.IsLifeTransitionAllowed(
                CombatantLifeState.Disposed,
                CombatantLifeState.Disposed));

            Assert.True(CombatantStatePlanner.IsControlTransitionAllowed(
                CombatantLifeState.Uninitialized,
                CombatantControlState.Disabled,
                CombatantLifeState.Alive,
                CombatantControlState.Manual));
            Assert.True(CombatantStatePlanner.IsControlTransitionAllowed(
                CombatantLifeState.Alive,
                CombatantControlState.Manual,
                CombatantLifeState.Alive,
                CombatantControlState.Assist));
            Assert.True(CombatantStatePlanner.IsControlTransitionAllowed(
                CombatantLifeState.Alive,
                CombatantControlState.Auto,
                CombatantLifeState.Defeated,
                CombatantControlState.Defeated));
            Assert.False(CombatantStatePlanner.IsControlTransitionAllowed(
                CombatantLifeState.Alive,
                CombatantControlState.Manual,
                CombatantLifeState.Defeated,
                CombatantControlState.Manual));
        }

        [Test]
        public void CombatantStateLifecycleOwnsControlAndDisposesExactlyOnce()
        {
            Assert.True(CombatantStateSnapshot.TryCreate(
                Id("session-1"),
                Id("attempt-1"),
                Id("actor-1"),
                Id("owner-1"),
                out CombatantStateSnapshot state));

            CombatantStatePlanResult constructed = CombatantStatePlanner.Plan(
                state,
                StateRequest(
                    state,
                    "state-construct",
                    0L,
                    CombatantLifeState.Alive,
                    CombatantControlState.Manual,
                    "owner-1",
                    "owner-1"));
            Assert.AreEqual(CombatantStatePlanStatus.Applied, constructed.Status);
            Assert.AreEqual(CombatantLifeState.Alive, constructed.Snapshot.LifeState);
            Assert.AreEqual(
                CombatantControlState.Manual,
                constructed.Snapshot.ControlState);
            Assert.AreEqual(2, constructed.Events.Count);
            Assert.AreEqual(
                Id("session-1"),
                constructed.Events[0].EncounterSessionId);
            Assert.AreEqual(
                Id("attempt-1"),
                constructed.Events[0].EncounterAttemptId);
            Assert.AreEqual(state.Revision, constructed.Events[0].BeforeRevision);
            Assert.AreEqual(
                constructed.Snapshot.Revision,
                constructed.Events[0].AfterRevision);

            CombatantStatePlanResult transferred = CombatantStatePlanner.Plan(
                constructed.Snapshot,
                StateRequest(
                    constructed.Snapshot,
                    "state-transfer",
                    1L,
                    CombatantLifeState.Alive,
                    CombatantControlState.Assist,
                    "owner-1",
                    "owner-2"));
            Assert.AreEqual(CombatantStatePlanStatus.Applied, transferred.Status);
            Assert.AreEqual(Id("owner-2"), transferred.Snapshot.ControlOwnerId);
            CollectionAssert.AreEqual(
                new[]
                {
                    CombatantStateEventKind.ControlStateChanged,
                    CombatantStateEventKind.ControlOwnerChanged
                },
                transferred.Events.Select(receipt => receipt.Kind).ToArray());

            CombatantStatePlanResult staleOwner = CombatantStatePlanner.Plan(
                transferred.Snapshot,
                StateRequest(
                    transferred.Snapshot,
                    "state-stale-owner",
                    2L,
                    CombatantLifeState.Alive,
                    CombatantControlState.Manual,
                    "owner-1",
                    "owner-1"));
            Assert.AreEqual(
                CombatantStatePlanStatus.ControlOwnerConflict,
                staleOwner.Status);
            Assert.AreSame(transferred.Snapshot, staleOwner.Snapshot);

            CombatantStatePlanResult defeated = CombatantStatePlanner.Plan(
                transferred.Snapshot,
                StateRequest(
                    transferred.Snapshot,
                    "state-defeat",
                    2L,
                    CombatantLifeState.Defeated,
                    CombatantControlState.Defeated,
                    "owner-2",
                    "owner-2"));
            Assert.AreEqual(CombatantStatePlanStatus.Applied, defeated.Status);
            Assert.AreEqual(
                CombatantLifeState.Defeated,
                defeated.Snapshot.LifeState);
            Assert.AreEqual(
                CombatantControlState.Defeated,
                defeated.Snapshot.ControlState);

            CombatantStatePlanResult disposed = CombatantStatePlanner.Plan(
                defeated.Snapshot,
                StateRequest(
                    defeated.Snapshot,
                    "state-dispose",
                    3L,
                    CombatantLifeState.Disposed,
                    CombatantControlState.Disposed,
                    "owner-2",
                    "owner-2"));
            Assert.AreEqual(CombatantStatePlanStatus.Applied, disposed.Status);
            Assert.AreEqual(
                1,
                disposed.Events.Count(
                    receipt =>
                        receipt.Kind == CombatantStateEventKind.Disposed));

            CombatantStatePlanResult afterDisposed = CombatantStatePlanner.Plan(
                disposed.Snapshot,
                StateRequest(
                    disposed.Snapshot,
                    "state-after-dispose",
                    4L,
                    CombatantLifeState.Disposed,
                    CombatantControlState.Disposed,
                    "owner-2",
                    "owner-3"));
            Assert.AreEqual(
                CombatantStatePlanStatus.TerminalState,
                afterDisposed.Status);
            Assert.AreEqual(0, afterDisposed.Events.Count);
        }

        [Test]
        public void CombatantStateRevisionAndOrdinalDetectExactConflictAndStale()
        {
            Assert.True(CombatantStateSnapshot.TryCreate(
                Id("session-1"),
                Id("attempt-1"),
                Id("actor-1"),
                Id("owner-1"),
                out CombatantStateSnapshot initial));
            CombatantStateTransitionRequest request = StateRequest(
                initial,
                "state-first",
                0L,
                CombatantLifeState.Alive,
                CombatantControlState.Manual,
                "owner-1",
                "owner-1");
            CombatantStatePlanResult applied =
                CombatantStatePlanner.Plan(initial, request);

            CombatantStatePlanResult duplicate =
                CombatantStatePlanner.Plan(applied.Snapshot, request);
            Assert.AreEqual(
                CombatantStatePlanStatus.DuplicateExact,
                duplicate.Status);
            Assert.AreSame(applied.TransitionReceipt, duplicate.ExistingReceipt);
            Assert.AreEqual(0, duplicate.Events.Count);

            CombatantStateTransitionRequest changed = StateRequest(
                initial,
                "state-first",
                0L,
                CombatantLifeState.Alive,
                CombatantControlState.Assist,
                "owner-1",
                "owner-1");
            Assert.AreEqual(
                CombatantStatePlanStatus.CorrelationConflict,
                CombatantStatePlanner.Plan(
                    applied.Snapshot,
                    changed).Status);

            CombatantStatePlanResult second = CombatantStatePlanner.Plan(
                applied.Snapshot,
                StateRequest(
                    applied.Snapshot,
                    "state-second",
                    1L,
                    CombatantLifeState.Alive,
                    CombatantControlState.Assist,
                    "owner-1",
                    "owner-1"));
            Assert.AreEqual(
                CombatantStatePlanStatus.DuplicateExact,
                CombatantStatePlanner.Plan(
                    second.Snapshot,
                    request).Status);
            Assert.AreEqual(
                CombatantStatePlanStatus.OutOfOrderTransition,
                CombatantStatePlanner.Plan(
                    second.Snapshot,
                    StateRequest(
                        second.Snapshot,
                        "state-gap",
                        3L,
                        CombatantLifeState.Alive,
                        CombatantControlState.Auto,
                        "owner-1",
                        "owner-1")).Status);
            CombatantStateTransitionRequest reusedOlderIdAtCurrentOrdinal =
                new CombatantStateTransitionRequest(
                    OrdinalId("state-first", 0L),
                    2L,
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    CombatantLifeState.Alive,
                    CombatantControlState.Auto,
                    Id("owner-1"),
                    Id("owner-1"),
                    second.Snapshot.Revision);
            Assert.AreEqual(
                CombatantStatePlanStatus.CorrelationConflict,
                CombatantStatePlanner.Plan(
                    second.Snapshot,
                    reusedOlderIdAtCurrentOrdinal).Status);
            Assert.AreEqual(2, second.Snapshot.TransitionReplayReceipts.Count);
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatantStateTransitionReceipt>)
                    second.Snapshot.TransitionReplayReceipts).Add(null));
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatantStateEventReceipt>)
                    second.Events).Add(null));
        }

        [Test]
        public void CombatantStateReplayWindowEvictsWithoutLifetimeTransitionCap()
        {
            Assert.True(CombatantStateSnapshot.TryCreate(
                Id("session-1"),
                Id("attempt-1"),
                Id("actor-1"),
                Id("owner-1"),
                out CombatantStateSnapshot snapshot));
            CombatantStateTransitionRequest first = StateRequest(
                snapshot,
                "state-window",
                0L,
                CombatantLifeState.Alive,
                CombatantControlState.Manual,
                "owner-1",
                "owner-1");
            CombatantStatePlanResult firstResult =
                CombatantStatePlanner.Plan(snapshot, first);
            Assert.AreEqual(CombatantStatePlanStatus.Applied, firstResult.Status);
            snapshot = firstResult.Snapshot;

            for (long ordinal = 1L; ordinal <= 70L; ordinal++)
            {
                CombatantStatePlanResult result = CombatantStatePlanner.Plan(
                    snapshot,
                    StateRequest(
                        snapshot,
                        "state-window",
                        ordinal,
                        CombatantLifeState.Alive,
                        ordinal % 2L == 0L
                            ? CombatantControlState.Manual
                            : CombatantControlState.Assist,
                        "owner-1",
                        "owner-1"));
                Assert.AreEqual(
                    CombatantStatePlanStatus.Applied,
                    result.Status,
                    "transition " + ordinal);
                snapshot = result.Snapshot;
            }

            Assert.AreEqual(71L, snapshot.NextTransitionOrdinal);
            Assert.AreEqual(
                CombatantStateSnapshot.MaximumTransitionReplayReceipts,
                snapshot.TransitionReplayReceipts.Count);
            CombatantStatePlanResult expired =
                CombatantStatePlanner.Plan(snapshot, first);
            Assert.AreEqual(
                CombatantStatePlanStatus.ReplayWindowExpired,
                expired.Status);
            Assert.AreSame(snapshot, expired.Snapshot);
            Assert.AreEqual(0, expired.Events.Count);
        }

        private static CombatantResourceSnapshot CreateSnapshot(
            long currentHealth = 100L,
            long maxHealth = 100L,
            long currentMana = 50L,
            long maxMana = 100L)
        {
            Assert.True(CombatantResourceSnapshot.TryCreate(
                Id("session-1"),
                Id("attempt-1"),
                Id("actor-1"),
                currentHealth,
                maxHealth,
                currentMana,
                maxMana,
                out CombatantResourceSnapshot snapshot));
            return snapshot;
        }

        private static CombatantStateTransitionRequest StateRequest(
            CombatantStateSnapshot snapshot,
            string transitionId,
            long ordinal,
            CombatantLifeState lifeState,
            CombatantControlState controlState,
            string expectedOwner,
            string nextOwner)
        {
            return new CombatantStateTransitionRequest(
                OrdinalId(transitionId, ordinal),
                ordinal,
                Id("session-1"),
                Id("attempt-1"),
                Id("actor-1"),
                lifeState,
                controlState,
                Id(expectedOwner),
                Id(nextOwner),
                snapshot.Revision);
        }

        private static CombatStableId OrdinalId(string prefix, long ordinal)
        {
            return Id(
                prefix +
                "-o" +
                ordinal.ToString("D19", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static CombatStableId RegenerationId(
            string prefix,
            long ordinal)
        {
            return OrdinalId("regen-" + prefix, ordinal);
        }

        private static CombatStableId Id(string value)
        {
            Assert.True(CombatStableId.TryCreate(value, out CombatStableId id));
            return id;
        }
    }
}
