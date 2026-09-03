using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Core;

namespace AL.RealmSelection
{
    public enum RealmGemCustodyState
    {
        AtHome,
        Carried,
        Dropped
    }

    public enum RealmGemCustodyOperation
    {
        PickUp,
        Drop,
        ReturnHome
    }

    public enum RealmGemCustodySnapshotStatus
    {
        Available,
        Unavailable,
        Malformed
    }

    public enum RealmGemCustodyAuthorizationStatus
    {
        Allowed,
        Denied,
        Unavailable
    }

    public enum RealmGemCustodyPlanStatus
    {
        Prepared,
        Duplicate,
        NoChange,
        InvalidRequest,
        Unauthorized,
        Stale,
        CooldownActive,
        Unsupported,
        Unavailable,
        Corrupt,
        DuplicateConflict,
        Overflow
    }

    public sealed class RealmGemCustodyDiagnostic
    {
        public RealmGemCustodyDiagnostic(string code, string subjectId, string message)
        {
            Code = code ?? string.Empty;
            SubjectId = subjectId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string SubjectId { get; }
        public string Message { get; }
    }

    public sealed class RealmGemCustodyRecord
    {
        public RealmGemCustodyRecord(
            string gemId,
            string homeRealmId,
            RealmId homeRealm,
            int saveSlotIndex,
            RealmGemCustodyState state,
            string carrierId,
            long lastDroppedUtcSeconds,
            long revision,
            bool isSupported)
        {
            GemId = gemId ?? string.Empty;
            HomeRealmId = homeRealmId ?? string.Empty;
            HomeRealm = homeRealm;
            SaveSlotIndex = saveSlotIndex;
            State = state;
            CarrierId = carrierId ?? string.Empty;
            LastDroppedUtcSeconds = lastDroppedUtcSeconds;
            Revision = revision;
            IsSupported = isSupported;
        }

        public string GemId { get; }
        public string HomeRealmId { get; }
        public RealmId HomeRealm { get; }
        public int SaveSlotIndex { get; }
        public RealmGemCustodyState State { get; }
        public string CarrierId { get; }
        public long LastDroppedUtcSeconds { get; }
        public long Revision { get; }
        public bool IsSupported { get; }
    }

    public sealed class RealmGemCustodyReceipt
    {
        public RealmGemCustodyReceipt(
            string operationId,
            string correlationId,
            RealmGemCustodyOperation operation,
            string gemId,
            string requestFingerprint,
            long committedSnapshotRevision,
            RealmGemCustodyRecord committedRecord)
        {
            OperationId = operationId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            Operation = operation;
            GemId = gemId ?? string.Empty;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            CommittedSnapshotRevision = committedSnapshotRevision;
            CommittedRecord = committedRecord;
        }

        public string OperationId { get; }
        public string CorrelationId { get; }
        public RealmGemCustodyOperation Operation { get; }
        public string GemId { get; }
        public string RequestFingerprint { get; }
        public long CommittedSnapshotRevision { get; }
        public RealmGemCustodyRecord CommittedRecord { get; }
    }

    public sealed class RealmGemCustodySnapshot
    {
        public RealmGemCustodySnapshot(
            RealmGemCustodySnapshotStatus status,
            long revision,
            IEnumerable<RealmGemCustodyRecord> records)
        {
            Status = status;
            Revision = revision;
            Records = Array.AsReadOnly((records ?? Array.Empty<RealmGemCustodyRecord>()).ToArray());
        }

        public RealmGemCustodySnapshotStatus Status { get; }
        public long Revision { get; }
        public IReadOnlyList<RealmGemCustodyRecord> Records { get; }
    }

    public sealed class RealmGemCustodyRequest
    {
        public RealmGemCustodyRequest(
            RealmGemCustodyOperation operation,
            string operationId,
            string correlationId,
            string gemId,
            string actorId,
            long observedUtcSeconds,
            long expectedSnapshotRevision,
            long expectedRecordRevision,
            RealmGemCustodyReceipt priorReceipt = null)
        {
            Operation = operation;
            OperationId = operationId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            GemId = gemId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            ObservedUtcSeconds = observedUtcSeconds;
            ExpectedSnapshotRevision = expectedSnapshotRevision;
            ExpectedRecordRevision = expectedRecordRevision;
            PriorReceipt = priorReceipt;
        }

        public RealmGemCustodyOperation Operation { get; }
        public string OperationId { get; }
        public string CorrelationId { get; }
        public string GemId { get; }
        public string ActorId { get; }
        public long ObservedUtcSeconds { get; }
        public long ExpectedSnapshotRevision { get; }
        public long ExpectedRecordRevision { get; }
        public RealmGemCustodyReceipt PriorReceipt { get; }
    }

    public sealed class RealmGemCustodyPlan
    {
        internal RealmGemCustodyPlan(
            RealmGemCustodyOperation operation,
            long expectedSnapshotRevision,
            long candidateSnapshotRevision,
            RealmGemCustodyRecord expectedRecord,
            RealmGemCustodyRecord candidateRecord,
            IEnumerable<RealmGemCustodyRecord> candidateRecords,
            RealmGemCustodyReceipt receipt,
            long plannedUtcSeconds)
        {
            Operation = operation;
            ExpectedSnapshotRevision = expectedSnapshotRevision;
            CandidateSnapshotRevision = candidateSnapshotRevision;
            ExpectedRecord = expectedRecord;
            CandidateRecord = candidateRecord;
            CandidateRecords = Array.AsReadOnly(candidateRecords.ToArray());
            Receipt = receipt;
            PlannedUtcSeconds = plannedUtcSeconds;
        }

        public RealmGemCustodyOperation Operation { get; }
        public long ExpectedSnapshotRevision { get; }
        public long CandidateSnapshotRevision { get; }
        public RealmGemCustodyRecord ExpectedRecord { get; }
        public RealmGemCustodyRecord CandidateRecord { get; }
        public IReadOnlyList<RealmGemCustodyRecord> CandidateRecords { get; }
        public RealmGemCustodyReceipt Receipt { get; }
        public long PlannedUtcSeconds { get; }
    }

    public sealed class RealmGemCustodyPlanningResult
    {
        internal RealmGemCustodyPlanningResult(
            RealmGemCustodyPlanStatus status,
            RealmGemCustodyPlan plan,
            RealmGemCustodyReceipt existingReceipt,
            IEnumerable<RealmGemCustodyDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            ExistingReceipt = existingReceipt;
            Diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<RealmGemCustodyDiagnostic>())
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.SubjectId, StringComparer.Ordinal)
                .ToArray());
        }

        public RealmGemCustodyPlanStatus Status { get; }
        public RealmGemCustodyPlan Plan { get; }
        public RealmGemCustodyReceipt ExistingReceipt { get; }
        public IReadOnlyList<RealmGemCustodyDiagnostic> Diagnostics { get; }
        public bool IsPrepared => Status == RealmGemCustodyPlanStatus.Prepared && Plan != null;
    }

    public interface IRealmGemCustodyClock
    {
        bool TryGetUtcSeconds(out long utcSeconds);
    }

    public interface IRealmGemCustodyAuthority
    {
        RealmGemCustodyAuthorizationStatus Authorize(
            RealmGemCustodyRequest request,
            RealmGemCatalogEntry catalogEntry,
            RealmGemCustodyRecord currentRecord);
    }

    public sealed class RealmGemCustodyPolicy
    {
        public RealmGemCustodyPolicy(long pickupCooldownSeconds)
        {
            PickupCooldownSeconds = pickupCooldownSeconds;
        }

        public long PickupCooldownSeconds { get; }
    }

    public sealed class RealmGemCustodyPlanner
    {
        private const int MaximumRecords = 64;
        private const int MaximumIdentityLength = 128;

        private readonly RealmGemCatalogSnapshot catalog;
        private readonly IRealmGemCustodyClock clock;
        private readonly IRealmGemCustodyAuthority authority;
        private readonly RealmGemCustodyPolicy policy;

        public RealmGemCustodyPlanner(
            RealmGemCatalogSnapshot catalog,
            IRealmGemCustodyClock clock,
            IRealmGemCustodyAuthority authority,
            RealmGemCustodyPolicy policy)
        {
            this.catalog = catalog;
            this.clock = clock;
            this.authority = authority;
            this.policy = policy;
        }

        public RealmGemCustodyPlanningResult Plan(
            RealmGemCustodyRequest request,
            RealmGemCustodySnapshot snapshot)
        {
            if (!IsValidRequest(request))
            {
                return Reject(
                    RealmGemCustodyPlanStatus.InvalidRequest,
                    "AL-REALM-GEM-REQUEST-INVALID",
                    request?.GemId,
                    "Custody request identity, time, revision, or operation is invalid.");
            }

            string fingerprint = Fingerprint(request);
            if (catalog == null || clock == null || authority == null || policy == null)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.Unavailable,
                    "AL-REALM-GEM-DEPENDENCY-UNAVAILABLE",
                    request.GemId,
                    "Catalog, clock, authority, or policy is unavailable.");
            }

            if (policy.PickupCooldownSeconds < 0)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.Corrupt,
                    "AL-REALM-GEM-POLICY-INVALID",
                    request.GemId,
                    "Pickup cooldown policy is invalid.");
            }

            RealmGemQueryResult catalogResult = catalog.Resolve(request.GemId);
            if (catalogResult.Status == RealmGemQueryStatus.InvalidId)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.InvalidRequest,
                    RealmGemCatalogResolver.InvalidIdCode,
                    request.GemId,
                    "Realm Gem ID is invalid.");
            }

            if (catalogResult.Status == RealmGemQueryStatus.UnknownId)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.Unsupported,
                    RealmGemCatalogResolver.UnknownIdCode,
                    request.GemId,
                    "Unknown-future Realm Gem rows are preserved but cannot be mutated.");
            }

            RealmGemCustodyPlanningResult replay = ClassifyReplay(
                request,
                fingerprint,
                catalogResult.Entry);
            if (replay != null)
            {
                return replay;
            }

            if (snapshot == null || snapshot.Status == RealmGemCustodySnapshotStatus.Unavailable)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.Unavailable,
                    "AL-REALM-GEM-SNAPSHOT-UNAVAILABLE",
                    request.GemId,
                    "Custody snapshot is unavailable.");
            }

            if (snapshot.Status != RealmGemCustodySnapshotStatus.Available)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.Corrupt,
                    "AL-REALM-GEM-SNAPSHOT-MALFORMED",
                    request.GemId,
                    "Custody snapshot is malformed.");
            }

            if (!clock.TryGetUtcSeconds(out long nowUtcSeconds) ||
                nowUtcSeconds <= 0 ||
                nowUtcSeconds != request.ObservedUtcSeconds)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.Unavailable,
                    "AL-REALM-GEM-CLOCK-INVALID",
                    request.GemId,
                    "Authoritative time is unavailable or does not match the observation.");
            }

            List<RealmGemCustodyDiagnostic> corruption = ValidateSnapshot(snapshot, nowUtcSeconds);
            if (corruption.Count != 0)
            {
                return new RealmGemCustodyPlanningResult(
                    RealmGemCustodyPlanStatus.Corrupt,
                    null,
                    null,
                    corruption);
            }

            if (snapshot.Revision != request.ExpectedSnapshotRevision)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.Stale,
                    "AL-REALM-GEM-SNAPSHOT-STALE",
                    request.GemId,
                    "Expected snapshot revision does not match current authority.");
            }

            RealmGemCustodyRecord current = snapshot.Records.Single(record =>
                string.Equals(record.GemId, request.GemId, StringComparison.Ordinal));
            if (current.Revision != request.ExpectedRecordRevision)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.Stale,
                    "AL-REALM-GEM-RECORD-STALE",
                    request.GemId,
                    "Expected Realm Gem revision does not match current authority.");
            }

            if (current.Revision == long.MaxValue)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.Overflow,
                    "AL-REALM-GEM-RECORD-REVISION-OVERFLOW",
                    request.GemId,
                    "Realm Gem record revision cannot advance.");
            }

            RealmGemCustodyAuthorizationStatus authorization = authority.Authorize(
                request,
                catalogResult.Entry,
                current);
            if (authorization == RealmGemCustodyAuthorizationStatus.Unavailable)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.Unavailable,
                    "AL-REALM-GEM-AUTHORITY-UNAVAILABLE",
                    request.GemId,
                    "Actor authority is unavailable.");
            }

            if (authorization != RealmGemCustodyAuthorizationStatus.Allowed)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.Unauthorized,
                    "AL-REALM-GEM-ACTOR-UNAUTHORIZED",
                    request.GemId,
                    "Actor is not authorized for this custody operation.");
            }

            RealmGemCustodyPlanningResult transitionGate = TryPlanTransition(
                request,
                current,
                nowUtcSeconds,
                out RealmGemCustodyRecord candidate);
            if (transitionGate != null)
            {
                return transitionGate;
            }

            long candidateSnapshotRevision;
            try
            {
                candidateSnapshotRevision = checked(snapshot.Revision + 1);
            }
            catch (OverflowException)
            {
                return Reject(
                    RealmGemCustodyPlanStatus.Overflow,
                    "AL-REALM-GEM-REVISION-OVERFLOW",
                    request.GemId,
                    "Snapshot revision cannot advance.");
            }

            IReadOnlyList<RealmGemCustodyRecord> candidateRecords = BuildCandidateRecords(
                snapshot.Records,
                candidate);
            var receipt = new RealmGemCustodyReceipt(
                request.OperationId,
                request.CorrelationId,
                request.Operation,
                request.GemId,
                fingerprint,
                candidateSnapshotRevision,
                candidate);
            var plan = new RealmGemCustodyPlan(
                request.Operation,
                snapshot.Revision,
                candidateSnapshotRevision,
                current,
                candidate,
                candidateRecords,
                receipt,
                nowUtcSeconds);
            return new RealmGemCustodyPlanningResult(
                RealmGemCustodyPlanStatus.Prepared,
                plan,
                null,
                Array.Empty<RealmGemCustodyDiagnostic>());
        }

        private RealmGemCustodyPlanningResult TryPlanTransition(
            RealmGemCustodyRequest request,
            RealmGemCustodyRecord current,
            long nowUtcSeconds,
            out RealmGemCustodyRecord candidate)
        {
            candidate = null;
            switch (request.Operation)
            {
                case RealmGemCustodyOperation.PickUp:
                    if (current.State == RealmGemCustodyState.Carried)
                    {
                        return string.Equals(current.CarrierId, request.ActorId, StringComparison.Ordinal)
                            ? Reject(
                                RealmGemCustodyPlanStatus.NoChange,
                                "AL-REALM-GEM-ALREADY-CARRIED",
                                request.GemId,
                                "Actor already carries this Realm Gem.")
                            : Reject(
                                RealmGemCustodyPlanStatus.Unauthorized,
                                "AL-REALM-GEM-CARRIER-REPLACEMENT",
                                request.GemId,
                                "An active carrier cannot be replaced.");
                    }

                    if (current.State == RealmGemCustodyState.Dropped)
                    {
                        long elapsed;
                        try
                        {
                            elapsed = checked(nowUtcSeconds - current.LastDroppedUtcSeconds);
                        }
                        catch (OverflowException)
                        {
                            return Reject(
                                RealmGemCustodyPlanStatus.Overflow,
                                "AL-REALM-GEM-COOLDOWN-OVERFLOW",
                                request.GemId,
                                "Pickup cooldown calculation overflowed.");
                        }

                        if (elapsed < policy.PickupCooldownSeconds)
                        {
                            return Reject(
                                RealmGemCustodyPlanStatus.CooldownActive,
                                "AL-REALM-GEM-COOLDOWN-ACTIVE",
                                request.GemId,
                                "Dropped Realm Gem has not reached the pickup boundary.");
                        }
                    }

                    candidate = Advance(
                        current,
                        RealmGemCustodyState.Carried,
                        request.ActorId,
                        0);
                    return null;

                case RealmGemCustodyOperation.Drop:
                    if (current.State == RealmGemCustodyState.Dropped)
                    {
                        return Reject(
                            RealmGemCustodyPlanStatus.NoChange,
                            "AL-REALM-GEM-ALREADY-DROPPED",
                            request.GemId,
                            "Realm Gem is already dropped.");
                    }

                    if (current.State != RealmGemCustodyState.Carried ||
                        !string.Equals(current.CarrierId, request.ActorId, StringComparison.Ordinal))
                    {
                        return Reject(
                            RealmGemCustodyPlanStatus.Unauthorized,
                            "AL-REALM-GEM-DROP-NOT-CARRIER",
                            request.GemId,
                            "Only the current carrier may drop this Realm Gem.");
                    }

                    candidate = Advance(
                        current,
                        RealmGemCustodyState.Dropped,
                        string.Empty,
                        nowUtcSeconds);
                    return null;

                case RealmGemCustodyOperation.ReturnHome:
                    if (current.State == RealmGemCustodyState.AtHome)
                    {
                        return Reject(
                            RealmGemCustodyPlanStatus.NoChange,
                            "AL-REALM-GEM-ALREADY-HOME",
                            request.GemId,
                            "Realm Gem is already home.");
                    }

                    candidate = Advance(
                        current,
                        RealmGemCustodyState.AtHome,
                        string.Empty,
                        0);
                    return null;

                default:
                    return Reject(
                        RealmGemCustodyPlanStatus.InvalidRequest,
                        "AL-REALM-GEM-OPERATION-INVALID",
                        request.GemId,
                        "Custody operation is invalid.");
            }
        }

        private static RealmGemCustodyRecord Advance(
            RealmGemCustodyRecord current,
            RealmGemCustodyState state,
            string carrierId,
            long droppedUtcSeconds)
        {
            return new RealmGemCustodyRecord(
                current.GemId,
                current.HomeRealmId,
                current.HomeRealm,
                current.SaveSlotIndex,
                state,
                carrierId,
                droppedUtcSeconds,
                checked(current.Revision + 1),
                true);
        }

        private List<RealmGemCustodyDiagnostic> ValidateSnapshot(
            RealmGemCustodySnapshot snapshot,
            long nowUtcSeconds)
        {
            var diagnostics = new List<RealmGemCustodyDiagnostic>();
            if (snapshot.Revision < 0 ||
                snapshot.Records == null ||
                snapshot.Records.Count > MaximumRecords)
            {
                diagnostics.Add(Diagnostic(
                    "AL-REALM-GEM-SNAPSHOT-INVALID",
                    string.Empty,
                    "Snapshot revision or record count is invalid."));
                return diagnostics;
            }

            var recordsById = new Dictionary<string, RealmGemCustodyRecord>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Records.Count; index++)
            {
                RealmGemCustodyRecord record = snapshot.Records[index];
                if (record == null ||
                    !RealmGemCatalogResolver.IsStableId(record.GemId) ||
                    recordsById.ContainsKey(record.GemId))
                {
                    diagnostics.Add(Diagnostic(
                        "AL-REALM-GEM-RECORD-DUPLICATE-OR-INVALID",
                        record?.GemId,
                        "Realm Gem record is null, invalid, or duplicated."));
                    continue;
                }

                recordsById.Add(record.GemId, record);
                RealmGemQueryResult resolved = catalog.Resolve(record.GemId);
                if (resolved.Status == RealmGemQueryStatus.Found)
                {
                    if (!record.IsSupported ||
                        !string.Equals(record.HomeRealmId, resolved.Entry.HomeRealmId, StringComparison.Ordinal) ||
                        record.HomeRealm != resolved.Entry.HomeRealm ||
                        record.SaveSlotIndex != resolved.Entry.SaveSlotIndex)
                    {
                        diagnostics.Add(Diagnostic(
                            "AL-REALM-GEM-RECORD-CATALOG-MISMATCH",
                            record.GemId,
                            "Supported Realm Gem record does not match catalog authority."));
                    }

                    if (!IsValidRecordState(record, nowUtcSeconds))
                    {
                        diagnostics.Add(Diagnostic(
                            "AL-REALM-GEM-CUSTODY-CONTRADICTORY",
                            record.GemId,
                            "Realm Gem custody fields are contradictory or out of range."));
                    }
                }
                else if (record.IsSupported)
                {
                    diagnostics.Add(Diagnostic(
                        "AL-REALM-GEM-FUTURE-ROW-MUTABLE",
                        record.GemId,
                        "Unknown-future Realm Gem record must remain unsupported."));
                }
            }

            for (var index = 0; index < catalog.Entries.Count; index++)
            {
                string requiredId = catalog.Entries[index].Id;
                if (!recordsById.ContainsKey(requiredId))
                {
                    diagnostics.Add(Diagnostic(
                        "AL-REALM-GEM-REQUIRED-ROW-MISSING",
                        requiredId,
                        "Complete catalog-shaped custody state is required before mutation."));
                }
            }

            return diagnostics;
        }

        private static bool IsValidRecordState(
            RealmGemCustodyRecord record,
            long nowUtcSeconds)
        {
            if (record.Revision < 0 ||
                record.SaveSlotIndex <= 0 ||
                record.LastDroppedUtcSeconds < 0 ||
                !Enum.IsDefined(typeof(RealmGemCustodyState), record.State))
            {
                return false;
            }

            switch (record.State)
            {
                case RealmGemCustodyState.AtHome:
                    return string.IsNullOrEmpty(record.CarrierId) &&
                           record.LastDroppedUtcSeconds == 0;
                case RealmGemCustodyState.Carried:
                    return IsOpaqueId(record.CarrierId) &&
                           record.LastDroppedUtcSeconds == 0;
                case RealmGemCustodyState.Dropped:
                    return string.IsNullOrEmpty(record.CarrierId) &&
                           record.LastDroppedUtcSeconds > 0 &&
                           record.LastDroppedUtcSeconds <= nowUtcSeconds;
                default:
                    return false;
            }
        }

        private IReadOnlyList<RealmGemCustodyRecord> BuildCandidateRecords(
            IReadOnlyList<RealmGemCustodyRecord> current,
            RealmGemCustodyRecord candidate)
        {
            var byId = current.ToDictionary(record => record.GemId, StringComparer.Ordinal);
            byId[candidate.GemId] = candidate;
            var ordered = new List<RealmGemCustodyRecord>(current.Count);
            for (var index = 0; index < catalog.Entries.Count; index++)
            {
                ordered.Add(byId[catalog.Entries[index].Id]);
            }

            ordered.AddRange(byId.Values
                .Where(record => catalog.Resolve(record.GemId).Status == RealmGemQueryStatus.UnknownId)
                .OrderBy(record => record.GemId, StringComparer.Ordinal));
            return ordered.AsReadOnly();
        }

        private static RealmGemCustodyPlanningResult ClassifyReplay(
            RealmGemCustodyRequest request,
            string fingerprint,
            RealmGemCatalogEntry catalogEntry)
        {
            RealmGemCustodyReceipt receipt = request.PriorReceipt;
            if (receipt == null)
            {
                return null;
            }

            bool exact =
                string.Equals(receipt.OperationId, request.OperationId, StringComparison.Ordinal) &&
                string.Equals(receipt.CorrelationId, request.CorrelationId, StringComparison.Ordinal) &&
                receipt.Operation == request.Operation &&
                string.Equals(receipt.GemId, request.GemId, StringComparison.Ordinal) &&
                string.Equals(receipt.RequestFingerprint, fingerprint, StringComparison.Ordinal) &&
                IsValidReceiptCandidate(request, receipt, catalogEntry);
            return exact
                ? new RealmGemCustodyPlanningResult(
                    RealmGemCustodyPlanStatus.Duplicate,
                    null,
                    receipt,
                    new[]
                    {
                        Diagnostic(
                            "AL-REALM-GEM-DUPLICATE",
                            request.GemId,
                            "Committed custody receipt already satisfies this request.")
                    })
                : Reject(
                    RealmGemCustodyPlanStatus.DuplicateConflict,
                    "AL-REALM-GEM-DUPLICATE-CONFLICT",
                    request.GemId,
                    "Prior receipt does not match the request payload.");
        }

        private static bool IsValidReceiptCandidate(
            RealmGemCustodyRequest request,
            RealmGemCustodyReceipt receipt,
            RealmGemCatalogEntry catalogEntry)
        {
            RealmGemCustodyRecord committed = receipt.CommittedRecord;
            if (committed == null ||
                catalogEntry == null ||
                request.ExpectedSnapshotRevision == long.MaxValue ||
                request.ExpectedRecordRevision == long.MaxValue ||
                receipt.CommittedSnapshotRevision != request.ExpectedSnapshotRevision + 1 ||
                committed.Revision != request.ExpectedRecordRevision + 1 ||
                !committed.IsSupported ||
                !string.Equals(committed.GemId, catalogEntry.Id, StringComparison.Ordinal) ||
                !string.Equals(committed.HomeRealmId, catalogEntry.HomeRealmId, StringComparison.Ordinal) ||
                committed.HomeRealm != catalogEntry.HomeRealm ||
                committed.SaveSlotIndex != catalogEntry.SaveSlotIndex)
            {
                return false;
            }

            switch (request.Operation)
            {
                case RealmGemCustodyOperation.PickUp:
                    return committed.State == RealmGemCustodyState.Carried &&
                           string.Equals(committed.CarrierId, request.ActorId, StringComparison.Ordinal) &&
                           committed.LastDroppedUtcSeconds == 0;
                case RealmGemCustodyOperation.Drop:
                    return committed.State == RealmGemCustodyState.Dropped &&
                           string.IsNullOrEmpty(committed.CarrierId) &&
                           committed.LastDroppedUtcSeconds == request.ObservedUtcSeconds;
                case RealmGemCustodyOperation.ReturnHome:
                    return committed.State == RealmGemCustodyState.AtHome &&
                           string.IsNullOrEmpty(committed.CarrierId) &&
                           committed.LastDroppedUtcSeconds == 0;
                default:
                    return false;
            }
        }

        private static bool IsValidRequest(RealmGemCustodyRequest request)
        {
            return request != null &&
                   Enum.IsDefined(typeof(RealmGemCustodyOperation), request.Operation) &&
                   IsOpaqueId(request.OperationId) &&
                   IsOpaqueId(request.CorrelationId) &&
                   RealmGemCatalogResolver.IsStableId(request.GemId) &&
                   IsOpaqueId(request.ActorId) &&
                   request.ObservedUtcSeconds > 0 &&
                   request.ExpectedSnapshotRevision >= 0 &&
                   request.ExpectedRecordRevision >= 0;
        }

        private static bool IsOpaqueId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentityLength)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character < '!' || character > '~')
                {
                    return false;
                }
            }

            return true;
        }

        private static string Fingerprint(RealmGemCustodyRequest request)
        {
            var payload = new StringBuilder();
            AppendFingerprintPart(payload, request.Operation.ToString());
            AppendFingerprintPart(payload, request.OperationId);
            AppendFingerprintPart(payload, request.CorrelationId);
            AppendFingerprintPart(payload, request.GemId);
            AppendFingerprintPart(payload, request.ActorId);
            AppendFingerprintPart(
                payload,
                request.ObservedUtcSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendFingerprintPart(
                payload,
                request.ExpectedSnapshotRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendFingerprintPart(
                payload,
                request.ExpectedRecordRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString()))
                    .Select(value => value.ToString("x2")));
            }
        }

        private static void AppendFingerprintPart(StringBuilder payload, string value)
        {
            payload.Append(value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            payload.Append(':');
            payload.Append(value);
        }

        private static RealmGemCustodyPlanningResult Reject(
            RealmGemCustodyPlanStatus status,
            string code,
            string subjectId,
            string message)
        {
            return new RealmGemCustodyPlanningResult(
                status,
                null,
                null,
                new[] { Diagnostic(code, subjectId, message) });
        }

        private static RealmGemCustodyDiagnostic Diagnostic(
            string code,
            string subjectId,
            string message)
        {
            return new RealmGemCustodyDiagnostic(code, subjectId, message);
        }
    }
}
