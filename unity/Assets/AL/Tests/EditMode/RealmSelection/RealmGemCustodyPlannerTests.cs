using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core;
using AL.RealmSelection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class RealmGemCustodyPlannerTests
    {
        private const long Now = 1000;
        private const long SnapshotRevision = 20;
        private const long RecordRevision = 4;

        private RealmGemCatalogSnapshot catalog;
        private FakeClock clock;
        private FakeAuthority authority;
        private RealmGemCustodyPlanner planner;

        [SetUp]
        public void SetUp()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "realm_specialized.v1.json");
            RealmCatalogLoadResult source = RealmCatalogRuntime.Parse(File.ReadAllText(path));
            Assert.That(source.IsSuccess, Is.True, source.TechnicalCode);
            catalog = RealmGemCatalogResolver.Build(source.Snapshot).Snapshot;
            clock = new FakeClock(Now);
            authority = new FakeAuthority(RealmGemCustodyAuthorizationStatus.Allowed);
            planner = CreatePlanner();
        }

        [Test]
        public void PickupBuildsImmutableCandidateAndReceiptWithoutMutatingSnapshot()
        {
            RealmGemCustodySnapshot snapshot = Snapshot();
            RealmGemCustodyRecord original = Find(snapshot, "gem_crownlands_sun");

            RealmGemCustodyPlanningResult result = planner.Plan(
                Request(RealmGemCustodyOperation.PickUp, original),
                snapshot);

            Assert.That(result.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Prepared));
            Assert.That(result.IsPrepared, Is.True);
            Assert.That(result.Plan.ExpectedSnapshotRevision, Is.EqualTo(SnapshotRevision));
            Assert.That(result.Plan.CandidateSnapshotRevision, Is.EqualTo(SnapshotRevision + 1));
            Assert.That(result.Plan.CandidateRecord.State, Is.EqualTo(RealmGemCustodyState.Carried));
            Assert.That(result.Plan.CandidateRecord.CarrierId, Is.EqualTo("actor_one"));
            Assert.That(result.Plan.CandidateRecord.Revision, Is.EqualTo(RecordRevision + 1));
            Assert.That(result.Plan.Receipt.RequestFingerprint, Has.Length.EqualTo(64));
            Assert.That(result.Plan.Receipt.CommittedRecord, Is.SameAs(result.Plan.CandidateRecord));
            Assert.That(original.State, Is.EqualTo(RealmGemCustodyState.AtHome));
            Assert.That(original.Revision, Is.EqualTo(RecordRevision));
            Assert.That(((IList<RealmGemCustodyRecord>)result.Plan.CandidateRecords).IsReadOnly, Is.True);
        }

        [Test]
        public void CarrierCanDropAndAuthorizedActorCanReturnHome()
        {
            RealmGemCustodySnapshot carried = Snapshot(Replace(
                Home("gem_stonehold_forge"),
                RealmGemCustodyState.Carried,
                "actor_one",
                0));

            RealmGemCustodyPlanningResult drop = planner.Plan(
                Request(RealmGemCustodyOperation.Drop, Find(carried, "gem_stonehold_forge")),
                carried);

            Assert.That(drop.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Prepared));
            Assert.That(drop.Plan.CandidateRecord.State, Is.EqualTo(RealmGemCustodyState.Dropped));
            Assert.That(drop.Plan.CandidateRecord.CarrierId, Is.Empty);
            Assert.That(drop.Plan.CandidateRecord.LastDroppedUtcSeconds, Is.EqualTo(Now));

            RealmGemCustodySnapshot dropped = Snapshot(drop.Plan.CandidateRecord, SnapshotRevision + 1);
            RealmGemCustodyPlanningResult returned = planner.Plan(
                Request(
                    RealmGemCustodyOperation.ReturnHome,
                    drop.Plan.CandidateRecord,
                    expectedSnapshotRevision: SnapshotRevision + 1),
                dropped);

            Assert.That(returned.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Prepared));
            Assert.That(returned.Plan.CandidateRecord.State, Is.EqualTo(RealmGemCustodyState.AtHome));
            Assert.That(returned.Plan.CandidateRecord.LastDroppedUtcSeconds, Is.Zero);
        }

        [Test]
        public void WrongCarrierAndCarrierReplacementAreRejected()
        {
            RealmGemCustodyRecord carriedRecord = Replace(
                Home("gem_eldergrove_root"),
                RealmGemCustodyState.Carried,
                "current_carrier",
                0);
            RealmGemCustodySnapshot carried = Snapshot(carriedRecord);

            RealmGemCustodyPlanningResult drop = planner.Plan(
                Request(
                    RealmGemCustodyOperation.Drop,
                    carriedRecord,
                    actorId: "other_actor"),
                carried);
            RealmGemCustodyPlanningResult pickup = planner.Plan(
                Request(
                    RealmGemCustodyOperation.PickUp,
                    carriedRecord,
                    actorId: "other_actor"),
                carried);

            Assert.That(drop.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Unauthorized));
            Assert.That(drop.Diagnostics.Single().Code, Is.EqualTo("AL-REALM-GEM-DROP-NOT-CARRIER"));
            Assert.That(pickup.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Unauthorized));
            Assert.That(pickup.Diagnostics.Single().Code, Is.EqualTo("AL-REALM-GEM-CARRIER-REPLACEMENT"));
        }

        [Test]
        public void SameStateOperationsDoNotAdvanceAndCarriedGemCanReturnHome()
        {
            RealmGemCustodyRecord carriedRecord = Replace(
                Home("gem_eldergrove_root"),
                RealmGemCustodyState.Carried,
                "actor_one",
                0);
            RealmGemCustodyRecord droppedRecord = Replace(
                Home("gem_umbral_ember"),
                RealmGemCustodyState.Dropped,
                string.Empty,
                Now - 10);
            RealmGemCustodyRecord homeRecord = Home("gem_crownlands_oath");

            RealmGemCustodyPlanningResult alreadyCarried = planner.Plan(
                Request(RealmGemCustodyOperation.PickUp, carriedRecord),
                Snapshot(carriedRecord));
            RealmGemCustodyPlanningResult alreadyDropped = planner.Plan(
                Request(RealmGemCustodyOperation.Drop, droppedRecord),
                Snapshot(droppedRecord));
            RealmGemCustodyPlanningResult alreadyHome = planner.Plan(
                Request(RealmGemCustodyOperation.ReturnHome, homeRecord),
                Snapshot(homeRecord));
            RealmGemCustodyPlanningResult returned = planner.Plan(
                Request(RealmGemCustodyOperation.ReturnHome, carriedRecord),
                Snapshot(carriedRecord));

            Assert.That(alreadyCarried.Status, Is.EqualTo(RealmGemCustodyPlanStatus.NoChange));
            Assert.That(alreadyDropped.Status, Is.EqualTo(RealmGemCustodyPlanStatus.NoChange));
            Assert.That(alreadyHome.Status, Is.EqualTo(RealmGemCustodyPlanStatus.NoChange));
            Assert.That(returned.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Prepared));
            Assert.That(returned.Plan.CandidateRecord.State, Is.EqualTo(RealmGemCustodyState.AtHome));
            Assert.That(returned.Plan.CandidateRecord.Revision, Is.EqualTo(RecordRevision + 1));
        }

        [Test]
        public void PickupCooldownAcceptsExactBoundaryAndRejectsOneSecondEarly()
        {
            RealmGemCustodyRecord exact = Replace(
                Home("gem_umbral_veil"),
                RealmGemCustodyState.Dropped,
                string.Empty,
                Now - 10);
            RealmGemCustodyRecord early = Replace(
                exact,
                RealmGemCustodyState.Dropped,
                string.Empty,
                Now - 9);

            RealmGemCustodyPlanningResult accepted = planner.Plan(
                Request(RealmGemCustodyOperation.PickUp, exact),
                Snapshot(exact));
            RealmGemCustodyPlanningResult rejected = planner.Plan(
                Request(RealmGemCustodyOperation.PickUp, early),
                Snapshot(early));

            Assert.That(accepted.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Prepared));
            Assert.That(rejected.Status, Is.EqualTo(RealmGemCustodyPlanStatus.CooldownActive));
        }

        [Test]
        public void PriorCommittedReceiptIsDuplicateButChangedPayloadConflicts()
        {
            RealmGemCustodySnapshot snapshot = Snapshot();
            RealmGemCustodyRecord record = Find(snapshot, "gem_crownlands_oath");
            RealmGemCustodyRequest firstRequest = Request(RealmGemCustodyOperation.PickUp, record);
            RealmGemCustodyPlanningResult first = planner.Plan(firstRequest, snapshot);

            RealmGemCustodyRequest retry = Request(
                RealmGemCustodyOperation.PickUp,
                record,
                priorReceipt: first.Plan.Receipt);
            RealmGemCustodyRequest conflict = Request(
                RealmGemCustodyOperation.PickUp,
                record,
                actorId: "different_actor",
                priorReceipt: first.Plan.Receipt);

            RealmGemCustodyPlanningResult duplicate = planner.Plan(retry, null);
            RealmGemCustodyPlanningResult mismatch = planner.Plan(conflict, snapshot);

            Assert.That(duplicate.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Duplicate));
            Assert.That(duplicate.ExistingReceipt, Is.SameAs(first.Plan.Receipt));
            Assert.That(duplicate.Plan, Is.Null);
            Assert.That(mismatch.Status, Is.EqualTo(RealmGemCustodyPlanStatus.DuplicateConflict));
        }

        [Test]
        public void RehydratedReceiptReplaysAndFingerprintEncodingIsUnambiguous()
        {
            RealmGemCustodySnapshot snapshot = Snapshot();
            RealmGemCustodyRecord record = Find(snapshot, "gem_crownlands_oath");
            var firstRequest = new RealmGemCustodyRequest(
                RealmGemCustodyOperation.PickUp,
                "alpha|beta",
                "gamma",
                record.GemId,
                "actor_one",
                Now,
                SnapshotRevision,
                RecordRevision);
            var secondRequest = new RealmGemCustodyRequest(
                RealmGemCustodyOperation.PickUp,
                "alpha",
                "beta|gamma",
                record.GemId,
                "actor_one",
                Now,
                SnapshotRevision,
                RecordRevision);

            RealmGemCustodyPlanningResult first = planner.Plan(firstRequest, snapshot);
            RealmGemCustodyPlanningResult second = planner.Plan(secondRequest, snapshot);
            var rehydrated = new RealmGemCustodyReceipt(
                first.Plan.Receipt.OperationId,
                first.Plan.Receipt.CorrelationId,
                first.Plan.Receipt.Operation,
                first.Plan.Receipt.GemId,
                first.Plan.Receipt.RequestFingerprint,
                first.Plan.Receipt.CommittedSnapshotRevision,
                first.Plan.Receipt.CommittedRecord);
            var replayRequest = new RealmGemCustodyRequest(
                firstRequest.Operation,
                firstRequest.OperationId,
                firstRequest.CorrelationId,
                firstRequest.GemId,
                firstRequest.ActorId,
                firstRequest.ObservedUtcSeconds,
                firstRequest.ExpectedSnapshotRevision,
                firstRequest.ExpectedRecordRevision,
                rehydrated);

            RealmGemCustodyPlanningResult replay = planner.Plan(replayRequest, null);

            Assert.That(first.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Prepared));
            Assert.That(second.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Prepared));
            Assert.That(
                second.Plan.Receipt.RequestFingerprint,
                Is.Not.EqualTo(first.Plan.Receipt.RequestFingerprint));
            Assert.That(replay.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Duplicate));
            Assert.That(replay.ExistingReceipt, Is.SameAs(rehydrated));
        }

        [Test]
        public void StaleSnapshotAndRecordRevisionsCannotPlan()
        {
            RealmGemCustodySnapshot snapshot = Snapshot();
            RealmGemCustodyRecord record = Find(snapshot, "gem_stonehold_depth");

            RealmGemCustodyPlanningResult staleSnapshot = planner.Plan(
                Request(
                    RealmGemCustodyOperation.PickUp,
                    record,
                    expectedSnapshotRevision: SnapshotRevision - 1),
                snapshot);
            RealmGemCustodyPlanningResult staleRecord = planner.Plan(
                Request(
                    RealmGemCustodyOperation.PickUp,
                    record,
                    expectedRecordRevision: RecordRevision - 1),
                snapshot);

            Assert.That(staleSnapshot.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Stale));
            Assert.That(staleSnapshot.Diagnostics.Single().Code, Is.EqualTo("AL-REALM-GEM-SNAPSHOT-STALE"));
            Assert.That(staleRecord.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Stale));
            Assert.That(staleRecord.Diagnostics.Single().Code, Is.EqualTo("AL-REALM-GEM-RECORD-STALE"));
        }

        [Test]
        public void MissingClockAuthorityCatalogAndSnapshotFailClosed()
        {
            RealmGemCustodySnapshot snapshot = Snapshot();
            RealmGemCustodyRecord record = snapshot.Records[0];
            RealmGemCustodyRequest request = Request(RealmGemCustodyOperation.PickUp, record);

            RealmGemCustodyPlanningResult noCatalog = new RealmGemCustodyPlanner(
                null,
                clock,
                authority,
                new RealmGemCustodyPolicy(10)).Plan(request, snapshot);
            clock.IsAvailable = false;
            RealmGemCustodyPlanningResult noClock = planner.Plan(request, snapshot);
            clock.IsAvailable = true;
            authority.Status = RealmGemCustodyAuthorizationStatus.Unavailable;
            RealmGemCustodyPlanningResult noAuthority = planner.Plan(request, snapshot);
            authority.Status = RealmGemCustodyAuthorizationStatus.Denied;
            RealmGemCustodyPlanningResult denied = planner.Plan(request, snapshot);
            RealmGemCustodyPlanningResult noSnapshot = planner.Plan(request, null);

            Assert.That(noCatalog.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Unavailable));
            Assert.That(noClock.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Unavailable));
            Assert.That(noAuthority.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Unavailable));
            Assert.That(denied.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Unauthorized));
            Assert.That(noSnapshot.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Unavailable));
        }

        [Test]
        public void IncompleteContradictoryAndFutureTimestampStateIsCorrupt()
        {
            List<RealmGemCustodyRecord> records = Snapshot().Records.ToList();
            records.RemoveAll(record => record.GemId == "gem_crownlands_sun");
            RealmGemCustodyRecord bad = Replace(
                Home("gem_stonehold_depth"),
                RealmGemCustodyState.Dropped,
                "impossible_carrier",
                Now + 1);
            ReplaceIn(records, bad);
            RealmGemCustodySnapshot malformed = new RealmGemCustodySnapshot(
                RealmGemCustodySnapshotStatus.Available,
                SnapshotRevision,
                records);

            RealmGemCustodyPlanningResult result = planner.Plan(
                Request(RealmGemCustodyOperation.PickUp, bad),
                malformed);

            Assert.That(result.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Corrupt));
            CollectionAssert.AreEqual(
                result.Diagnostics
                    .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.SubjectId, StringComparer.Ordinal)
                    .Select(diagnostic => diagnostic.Code)
                    .ToArray(),
                result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
            CollectionAssert.Contains(
                result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
                "AL-REALM-GEM-CUSTODY-CONTRADICTORY");
            CollectionAssert.Contains(
                result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
                "AL-REALM-GEM-REQUIRED-ROW-MISSING");
        }

        [Test]
        public void DuplicateRowsAndNegativeSnapshotRevisionAreCorrupt()
        {
            RealmGemCustodySnapshot valid = Snapshot();
            List<RealmGemCustodyRecord> duplicateRows = valid.Records.ToList();
            duplicateRows.Add(valid.Records[0]);
            RealmGemCustodySnapshot duplicate = new RealmGemCustodySnapshot(
                RealmGemCustodySnapshotStatus.Available,
                SnapshotRevision,
                duplicateRows);
            RealmGemCustodySnapshot negativeRevision = new RealmGemCustodySnapshot(
                RealmGemCustodySnapshotStatus.Available,
                -1,
                valid.Records);

            RealmGemCustodyPlanningResult duplicateResult = planner.Plan(
                Request(RealmGemCustodyOperation.PickUp, valid.Records[0]),
                duplicate);
            RealmGemCustodyPlanningResult negativeResult = planner.Plan(
                Request(
                    RealmGemCustodyOperation.PickUp,
                    valid.Records[0],
                    expectedSnapshotRevision: 0),
                negativeRevision);

            Assert.That(duplicateResult.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Corrupt));
            Assert.That(
                duplicateResult.Diagnostics.Single().Code,
                Is.EqualTo("AL-REALM-GEM-RECORD-DUPLICATE-OR-INVALID"));
            Assert.That(negativeResult.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Corrupt));
            Assert.That(
                negativeResult.Diagnostics.Single().Code,
                Is.EqualTo("AL-REALM-GEM-SNAPSHOT-INVALID"));
        }

        [Test]
        public void NullRowsCatalogSlotCollisionsAndExtremeTimestampsAreCorrupt()
        {
            RealmGemCustodySnapshot valid = Snapshot();
            List<RealmGemCustodyRecord> nullRows = valid.Records.ToList();
            nullRows.Add(null);
            RealmGemCustodyRecord source = Find(valid, "gem_crownlands_oath");
            var duplicateSlot = new RealmGemCustodyRecord(
                source.GemId,
                source.HomeRealmId,
                source.HomeRealm,
                1,
                source.State,
                source.CarrierId,
                source.LastDroppedUtcSeconds,
                source.Revision,
                true);
            RealmGemCustodyRecord extremeTimestamp = Replace(
                Home("gem_stonehold_forge"),
                RealmGemCustodyState.Dropped,
                string.Empty,
                long.MaxValue);

            RealmGemCustodyPlanningResult nullResult = planner.Plan(
                Request(RealmGemCustodyOperation.PickUp, valid.Records[0]),
                new RealmGemCustodySnapshot(
                    RealmGemCustodySnapshotStatus.Available,
                    SnapshotRevision,
                    nullRows));
            RealmGemCustodyPlanningResult slotResult = planner.Plan(
                Request(RealmGemCustodyOperation.PickUp, duplicateSlot),
                Snapshot(duplicateSlot));
            RealmGemCustodyPlanningResult timestampResult = planner.Plan(
                Request(RealmGemCustodyOperation.PickUp, extremeTimestamp),
                Snapshot(extremeTimestamp));

            Assert.That(nullResult.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Corrupt));
            Assert.That(
                nullResult.Diagnostics.Single().Code,
                Is.EqualTo("AL-REALM-GEM-RECORD-DUPLICATE-OR-INVALID"));
            Assert.That(slotResult.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Corrupt));
            Assert.That(
                slotResult.Diagnostics.Single().Code,
                Is.EqualTo("AL-REALM-GEM-RECORD-CATALOG-MISMATCH"));
            Assert.That(timestampResult.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Corrupt));
            Assert.That(
                timestampResult.Diagnostics.Single().Code,
                Is.EqualTo("AL-REALM-GEM-CUSTODY-CONTRADICTORY"));
        }

        [Test]
        public void UnknownFutureRowsArePreservedButCannotBeMutated()
        {
            var futureZeta = new RealmGemCustodyRecord(
                "gem_future_zeta",
                string.Empty,
                RealmId.None,
                0,
                RealmGemCustodyState.AtHome,
                string.Empty,
                0,
                99,
                false);
            var futureAlpha = new RealmGemCustodyRecord(
                "gem_future_alpha",
                string.Empty,
                RealmId.None,
                0,
                RealmGemCustodyState.AtHome,
                string.Empty,
                0,
                100,
                false);
            List<RealmGemCustodyRecord> records = Snapshot().Records.ToList();
            records.Add(futureZeta);
            records.Add(futureAlpha);
            RealmGemCustodySnapshot snapshot = new RealmGemCustodySnapshot(
                RealmGemCustodySnapshotStatus.Available,
                SnapshotRevision,
                records);
            RealmGemCustodyRecord known = Find(snapshot, "gem_eldergrove_moon");

            RealmGemCustodyPlanningResult knownResult = planner.Plan(
                Request(RealmGemCustodyOperation.PickUp, known),
                snapshot);
            RealmGemCustodyPlanningResult futureResult = planner.Plan(
                new RealmGemCustodyRequest(
                    RealmGemCustodyOperation.PickUp,
                    "operation_001",
                    "correlation_001",
                    futureZeta.GemId,
                    "actor_one",
                    Now,
                    SnapshotRevision,
                    futureZeta.Revision),
                snapshot);

            Assert.That(knownResult.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Prepared));
            CollectionAssert.AreEqual(
                new[] { futureAlpha.GemId, futureZeta.GemId },
                knownResult.Plan.CandidateRecords.Skip(8).Select(record => record.GemId).ToArray());
            Assert.That(knownResult.Plan.CandidateRecords[8], Is.SameAs(futureAlpha));
            Assert.That(knownResult.Plan.CandidateRecords[9], Is.SameAs(futureZeta));
            Assert.That(futureResult.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Unsupported));
            Assert.That(futureResult.Plan, Is.Null);
        }

        [Test]
        public void InvalidInputsAndRevisionOverflowReturnTypedFailures()
        {
            RealmGemCustodySnapshot snapshot = Snapshot();
            RealmGemCustodyRecord record = snapshot.Records[0];
            RealmGemCustodyPlanningResult invalid = planner.Plan(
                new RealmGemCustodyRequest(
                    (RealmGemCustodyOperation)99,
                    string.Empty,
                    "correlation_001",
                    record.GemId,
                    "actor_one",
                    Now,
                    SnapshotRevision,
                    RecordRevision),
                snapshot);
            RealmGemCustodyPlanningResult blankGem = planner.Plan(
                new RealmGemCustodyRequest(
                    RealmGemCustodyOperation.PickUp,
                    "operation_001",
                    "correlation_001",
                    string.Empty,
                    "actor_one",
                    Now,
                    SnapshotRevision,
                    RecordRevision),
                snapshot);
            var maxRecord = new RealmGemCustodyRecord(
                record.GemId,
                record.HomeRealmId,
                record.HomeRealm,
                record.SaveSlotIndex,
                record.State,
                record.CarrierId,
                record.LastDroppedUtcSeconds,
                long.MaxValue,
                true);
            RealmGemCustodyPlanningResult recordOverflow = planner.Plan(
                Request(
                    RealmGemCustodyOperation.PickUp,
                    maxRecord,
                    expectedRecordRevision: long.MaxValue),
                Snapshot(maxRecord));
            RealmGemCustodySnapshot maxSnapshot = Snapshot(record, long.MaxValue);
            RealmGemCustodyPlanningResult snapshotOverflow = planner.Plan(
                Request(
                    RealmGemCustodyOperation.PickUp,
                    record,
                    expectedSnapshotRevision: long.MaxValue),
                maxSnapshot);

            Assert.That(invalid.Status, Is.EqualTo(RealmGemCustodyPlanStatus.InvalidRequest));
            Assert.That(blankGem.Status, Is.EqualTo(RealmGemCustodyPlanStatus.InvalidRequest));
            Assert.That(recordOverflow.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Overflow));
            Assert.That(snapshotOverflow.Status, Is.EqualTo(RealmGemCustodyPlanStatus.Overflow));
        }

        private RealmGemCustodyPlanner CreatePlanner()
        {
            return new RealmGemCustodyPlanner(
                catalog,
                clock,
                authority,
                new RealmGemCustodyPolicy(10));
        }

        private RealmGemCustodySnapshot Snapshot(
            RealmGemCustodyRecord replacement = null,
            long revision = SnapshotRevision)
        {
            var records = catalog.Entries.Select(entry => new RealmGemCustodyRecord(
                entry.Id,
                entry.HomeRealmId,
                entry.HomeRealm,
                entry.SaveSlotIndex,
                RealmGemCustodyState.AtHome,
                string.Empty,
                0,
                RecordRevision,
                true)).ToList();
            if (replacement != null)
            {
                ReplaceIn(records, replacement);
            }

            return new RealmGemCustodySnapshot(
                RealmGemCustodySnapshotStatus.Available,
                revision,
                records);
        }

        private RealmGemCustodyRequest Request(
            RealmGemCustodyOperation operation,
            RealmGemCustodyRecord record,
            string actorId = "actor_one",
            long expectedSnapshotRevision = SnapshotRevision,
            long? expectedRecordRevision = null,
            RealmGemCustodyReceipt priorReceipt = null)
        {
            return new RealmGemCustodyRequest(
                operation,
                "operation_001",
                "correlation_001",
                record.GemId,
                actorId,
                Now,
                expectedSnapshotRevision,
                expectedRecordRevision ?? record.Revision,
                priorReceipt);
        }

        private RealmGemCustodyRecord Home(string gemId)
        {
            RealmGemCatalogEntry entry = catalog.Resolve(gemId).Entry;
            return new RealmGemCustodyRecord(
                entry.Id,
                entry.HomeRealmId,
                entry.HomeRealm,
                entry.SaveSlotIndex,
                RealmGemCustodyState.AtHome,
                string.Empty,
                0,
                RecordRevision,
                true);
        }

        private static RealmGemCustodyRecord Find(
            RealmGemCustodySnapshot snapshot,
            string gemId)
        {
            return snapshot.Records.Single(record => record.GemId == gemId);
        }

        private static RealmGemCustodyRecord Replace(
            RealmGemCustodyRecord record,
            RealmGemCustodyState state,
            string carrierId,
            long droppedUtcSeconds)
        {
            return new RealmGemCustodyRecord(
                record.GemId,
                record.HomeRealmId,
                record.HomeRealm,
                record.SaveSlotIndex,
                state,
                carrierId,
                droppedUtcSeconds,
                record.Revision,
                record.IsSupported);
        }

        private static void ReplaceIn(
            IList<RealmGemCustodyRecord> records,
            RealmGemCustodyRecord replacement)
        {
            for (var index = 0; index < records.Count; index++)
            {
                if (records[index].GemId == replacement.GemId)
                {
                    records[index] = replacement;
                    return;
                }
            }
        }

        private sealed class FakeClock : IRealmGemCustodyClock
        {
            public FakeClock(long now)
            {
                Now = now;
            }

            public long Now { get; set; }
            public bool IsAvailable { get; set; } = true;

            public bool TryGetUtcSeconds(out long utcSeconds)
            {
                utcSeconds = Now;
                return IsAvailable;
            }
        }

        private sealed class FakeAuthority : IRealmGemCustodyAuthority
        {
            public FakeAuthority(RealmGemCustodyAuthorizationStatus status)
            {
                Status = status;
            }

            public RealmGemCustodyAuthorizationStatus Status { get; set; }

            public RealmGemCustodyAuthorizationStatus Authorize(
                RealmGemCustodyRequest request,
                RealmGemCatalogEntry catalogEntry,
                RealmGemCustodyRecord currentRecord)
            {
                return Status;
            }
        }
    }
}
