using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AL.Battle.Contracts;

namespace AL.Battle.Application
{
    public enum BattleTransactionState
    {
        Pending = 0,
        Applying = 1,
        Applied = 2,
        Acknowledged = 3,
        Rejected = 4,
        RecoveryRequired = 5
    }

    public enum BattleTransactionRecoveryStatus
    {
        Recovered = 0,
        NothingToRecover = 1,
        CorruptedPersistence = 2,
        UnsupportedVersion = 3
    }

    public enum BattleTransactionInterruptionPoint
    {
        AfterApplyingPersisted = 0,
        AfterAdapterCommit = 1
    }

    public sealed class BattleTransactionRecord
    {
        internal BattleTransactionRecord(
            BattleComputedResult result,
            BattleTransactionState state,
            string diagnosticCode,
            long commitSequence)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            State = state;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            CommitSequence = commitSequence;
        }

        public BattleComputedResult Result { get; }
        public string BattleResultId => Result.BattleResultId;
        public string ComputationSha256 => Result.ComputationSha256;
        public BattleTransactionState State { get; }
        public string DiagnosticCode { get; }
        public long CommitSequence { get; }

        internal BattleTransactionRecord With(
            BattleTransactionState state,
            string diagnosticCode = null,
            long? commitSequence = null)
        {
            return new BattleTransactionRecord(
                Result,
                state,
                diagnosticCode ?? DiagnosticCode,
                commitSequence ?? CommitSequence);
        }
    }

    public sealed class BattleTransactionRecoveryReport
    {
        internal BattleTransactionRecoveryReport(
            BattleTransactionRecoveryStatus status,
            int attemptedCount,
            int appliedCount)
        {
            Status = status;
            AttemptedCount = attemptedCount;
            AppliedCount = appliedCount;
        }

        public BattleTransactionRecoveryStatus Status { get; }
        public int AttemptedCount { get; }
        public int AppliedCount { get; }
    }

    /// <summary>
    /// Durable byte boundary owned by the shared save transaction architecture. Implementations
    /// must replace the complete snapshot atomically; a partial write must preserve the prior bytes.
    /// </summary>
    public interface IBattleTransactionPersistence
    {
        object SyncRoot { get; }
        byte[] ReadSnapshot();
        void WriteSnapshot(byte[] snapshot);
    }

    /// <summary>
    /// File-backed host adapter. Writes are flushed to a sibling temporary file and then renamed,
    /// so interruption preserves either the previous complete snapshot or the next complete one.
    /// The owning save service supplies a path inside its shared transaction storage.
    /// </summary>
    public sealed class AtomicFileBattleTransactionPersistence : IBattleTransactionPersistence
    {
        private static readonly object GateMapLock = new object();
        private static readonly Dictionary<string, object> GateMap =
            new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly object _gate;
        private readonly string _path;

        public AtomicFileBattleTransactionPersistence(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A snapshot path is required.", nameof(path));
            _path = Path.GetFullPath(path);
            lock (GateMapLock)
            {
                if (!GateMap.TryGetValue(_path, out _gate))
                {
                    _gate = new object();
                    GateMap.Add(_path, _gate);
                }
            }
        }

        public object SyncRoot => _gate;

        public byte[] ReadSnapshot()
        {
            lock (_gate)
            {
                if (!File.Exists(_path)) return null;
                long length = new FileInfo(_path).Length;
                if (length > BattleTransactionSnapshotCodec.MaximumStoredSnapshotBytes)
                    throw new InvalidDataException("Battle transaction snapshot exceeds the supported size.");
                return File.ReadAllBytes(_path);
            }
        }

        public void WriteSnapshot(byte[] snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            lock (_gate)
            {
                string directory = Path.GetDirectoryName(_path);
                if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Snapshot path has no directory.");
                Directory.CreateDirectory(directory);
                string temporary = _path + ".tmp";
                try
                {
                    using (var stream = new FileStream(
                        temporary,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        4096,
                        FileOptions.WriteThrough))
                    {
                        stream.Write(snapshot, 0, snapshot.Length);
                        stream.Flush(true);
                    }
                    if (File.Exists(_path))
                    {
                        string backup = _path + ".bak";
                        File.Replace(temporary, _path, backup);
                        if (File.Exists(backup)) File.Delete(backup);
                    }
                    else
                    {
                        File.Move(temporary, _path);
                    }
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
        }
    }

    /// <summary>Byte-backed persistence used by deterministic recovery tests and host adapters.</summary>
    public sealed class InMemoryBattleTransactionPersistence : IBattleTransactionPersistence
    {
        private readonly object _gate = new object();
        private byte[] _snapshot;

        public InMemoryBattleTransactionPersistence(byte[] snapshot = null)
        {
            _snapshot = snapshot == null ? null : (byte[])snapshot.Clone();
        }

        public object SyncRoot => _gate;

        public byte[] ReadSnapshot()
        {
            lock (_gate) return _snapshot == null ? null : (byte[])_snapshot.Clone();
        }

        public void WriteSnapshot(byte[] snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            lock (_gate) _snapshot = (byte[])snapshot.Clone();
        }
    }

    public sealed class BattleTransactionCoordinator
    {
        private readonly IBattleTransactionPersistence _persistence;
        private readonly AtomicBattleResultAdapter _adapter;
        private readonly Action<BattleTransactionInterruptionPoint> _interruption;
        private Dictionary<string, BattleTransactionRecord> _records;
        private BattleTransactionRecoveryStatus? _loadFailure;

        public BattleTransactionCoordinator(
            IBattleTransactionPersistence persistence,
            AtomicBattleResultAdapter adapter,
            Action<BattleTransactionInterruptionPoint> interruption = null)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _interruption = interruption;
        }

        public BattleTransactionRecord Begin(BattleComputedResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (string.IsNullOrWhiteSpace(result.BattleResultId))
                throw new ArgumentException("A stable battle result identity is required.", nameof(result));
            lock (_persistence.SyncRoot)
            {
                Reload();
                EnsureWritable();
                if (_records.TryGetValue(result.BattleResultId, out BattleTransactionRecord existing))
                {
                    if (SameIdentity(existing.Result, result)) return existing;
                    throw new InvalidOperationException("Battle result identity is already bound to another computation.");
                }

                var created = new BattleTransactionRecord(
                    result,
                    BattleTransactionState.Pending,
                    string.Empty,
                    0L);
                Dictionary<string, BattleTransactionRecord> previous = CloneRecords();
                _records.Add(created.BattleResultId, created);
                PersistOrRestore(previous);
                return created;
            }
        }

        public BattleTransactionRecord Apply(string battleResultId)
        {
            lock (_persistence.SyncRoot)
            {
                Reload();
                EnsureWritable();
                BattleTransactionRecord record = Require(battleResultId);
                if (record.State == BattleTransactionState.Applied ||
                    record.State == BattleTransactionState.Acknowledged ||
                    record.State == BattleTransactionState.Rejected ||
                    record.State == BattleTransactionState.RecoveryRequired)
                    return record;

                Dictionary<string, BattleTransactionRecord> previous = CloneRecords();
                record = record.With(BattleTransactionState.Applying, string.Empty);
                Replace(record);
                PersistOrRestore(previous);
                _interruption?.Invoke(BattleTransactionInterruptionPoint.AfterApplyingPersisted);

                BattleResultApplicationResult applied = _adapter.Apply(record.Result);
                _interruption?.Invoke(BattleTransactionInterruptionPoint.AfterAdapterCommit);

                if (applied.IsCommitted)
                {
                    record = record.With(
                        BattleTransactionState.Applied,
                        string.Empty,
                        applied.Receipt?.CommitSequence ?? 0L);
                }
                else if (IsRecoverable(applied.Status))
                {
                    record = record.With(BattleTransactionState.RecoveryRequired, applied.DiagnosticCode);
                }
                else
                {
                    record = record.With(BattleTransactionState.Rejected, applied.DiagnosticCode);
                }
                previous = CloneRecords();
                Replace(record);
                PersistOrRestore(previous);
                return record;
            }
        }

        public BattleTransactionRecord Acknowledge(string battleResultId)
        {
            lock (_persistence.SyncRoot)
            {
                Reload();
                EnsureWritable();
                BattleTransactionRecord record = Require(battleResultId);
                if (record.State == BattleTransactionState.Acknowledged) return record;
                if (record.State != BattleTransactionState.Applied)
                    throw new InvalidOperationException("Only an applied battle transaction can be acknowledged.");
                Dictionary<string, BattleTransactionRecord> previous = CloneRecords();
                record = record.With(BattleTransactionState.Acknowledged);
                Replace(record);
                PersistOrRestore(previous);
                return record;
            }
        }

        public BattleTransactionRecord Get(string battleResultId)
        {
            lock (_persistence.SyncRoot)
            {
                Reload();
                return _loadFailure.HasValue ? null : Require(battleResultId);
            }
        }

        public BattleTransactionRecoveryReport RecoverAll()
        {
            lock (_persistence.SyncRoot)
            {
                Reload();
                if (_loadFailure.HasValue)
                    return new BattleTransactionRecoveryReport(_loadFailure.Value, 0, 0);

                var recoverable = new List<string>();
                foreach (KeyValuePair<string, BattleTransactionRecord> pair in _records)
                {
                    if (pair.Value.State == BattleTransactionState.Pending ||
                        pair.Value.State == BattleTransactionState.Applying)
                        recoverable.Add(pair.Key);
                }
                recoverable.Sort(StringComparer.Ordinal);
                if (recoverable.Count == 0)
                    return new BattleTransactionRecoveryReport(
                        BattleTransactionRecoveryStatus.NothingToRecover, 0, 0);

                int applied = 0;
                for (int index = 0; index < recoverable.Count; index++)
                {
                    BattleTransactionRecord result = Apply(recoverable[index]);
                    if (result.State == BattleTransactionState.Applied ||
                        result.State == BattleTransactionState.Acknowledged)
                        applied++;
                }
                return new BattleTransactionRecoveryReport(
                    BattleTransactionRecoveryStatus.Recovered,
                    recoverable.Count,
                    applied);
            }
        }

        private void EnsureWritable()
        {
            EnsureLoaded();
            if (_loadFailure.HasValue)
                throw new InvalidOperationException("Battle transaction persistence is not recoverable.");
        }

        private void EnsureLoaded()
        {
            if (_records != null || _loadFailure.HasValue) return;
            BattleTransactionDecodeResult decoded =
                BattleTransactionSnapshotCodec.Decode(_persistence.ReadSnapshot());
            if (decoded.Status == BattleTransactionDecodeStatus.Success)
                _records = decoded.Records;
            else if (decoded.Status == BattleTransactionDecodeStatus.UnsupportedVersion)
                _loadFailure = BattleTransactionRecoveryStatus.UnsupportedVersion;
            else
                _loadFailure = BattleTransactionRecoveryStatus.CorruptedPersistence;
        }

        private void Reload()
        {
            _records = null;
            _loadFailure = null;
            EnsureLoaded();
        }

        private Dictionary<string, BattleTransactionRecord> CloneRecords()
        {
            return new Dictionary<string, BattleTransactionRecord>(_records, StringComparer.Ordinal);
        }

        private void PersistOrRestore(Dictionary<string, BattleTransactionRecord> previous)
        {
            try
            {
                Persist();
            }
            catch
            {
                _records = previous;
                throw;
            }
        }

        private void Persist()
        {
            _persistence.WriteSnapshot(BattleTransactionSnapshotCodec.Encode(_records.Values));
        }

        private BattleTransactionRecord Require(string battleResultId)
        {
            if (string.IsNullOrWhiteSpace(battleResultId) ||
                !_records.TryGetValue(battleResultId, out BattleTransactionRecord record))
                throw new KeyNotFoundException("Unknown battle result transaction.");
            return record;
        }

        private void Replace(BattleTransactionRecord record)
        {
            _records[record.BattleResultId] = record;
        }

        private static bool SameIdentity(BattleComputedResult left, BattleComputedResult right)
        {
            return string.Equals(left.BattleResultId, right.BattleResultId, StringComparison.Ordinal) &&
                string.Equals(left.BattleId, right.BattleId, StringComparison.Ordinal) &&
                string.Equals(left.BattleRequestId, right.BattleRequestId, StringComparison.Ordinal) &&
                string.Equals(left.ProfileId, right.ProfileId, StringComparison.Ordinal) &&
                string.Equals(left.ComputationSha256, right.ComputationSha256, StringComparison.Ordinal);
        }

        private static bool IsRecoverable(BattleResultApplicationStatus status)
        {
            return status == BattleResultApplicationStatus.PersistenceFailed;
        }
    }

    internal enum BattleTransactionDecodeStatus
    {
        Success,
        Corrupted,
        UnsupportedVersion
    }

    internal sealed class BattleTransactionDecodeResult
    {
        internal BattleTransactionDecodeResult(
            BattleTransactionDecodeStatus status,
            Dictionary<string, BattleTransactionRecord> records)
        {
            Status = status;
            Records = records;
        }

        internal BattleTransactionDecodeStatus Status { get; }
        internal Dictionary<string, BattleTransactionRecord> Records { get; }
    }

    public static class BattleTransactionSnapshotCodec
    {
        private const int Magic = 0x4254584E; // BTXN
        private const int Version = 1;
        private const int MaximumSnapshotBytes = 4 * 1024 * 1024;
        internal const int MaximumStoredSnapshotBytes = MaximumSnapshotBytes + 44;

        internal static byte[] Encode(IEnumerable<BattleTransactionRecord> source)
        {
            var records = new List<BattleTransactionRecord>(source);
            if (records.Count > BattleTechnicalLimits.MaximumCollectionCount)
                throw new InvalidDataException("Too many battle transaction records.");
            records.Sort((left, right) => string.CompareOrdinal(left.BattleResultId, right.BattleResultId));
            byte[] payload;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(records.Count);
                for (int index = 0; index < records.Count; index++) WriteRecord(writer, records[index]);
                writer.Flush();
                payload = stream.ToArray();
            }
            if (payload.Length > MaximumSnapshotBytes)
                throw new InvalidDataException("Battle transaction snapshot exceeds the supported size.");
            return Wrap(Version, payload);
        }

        internal static BattleTransactionDecodeResult Decode(byte[] snapshot)
        {
            if (snapshot == null)
                return Success(new Dictionary<string, BattleTransactionRecord>(StringComparer.Ordinal));
            if (snapshot.Length == 0)
                return Failure(BattleTransactionDecodeStatus.Corrupted);
            if (snapshot.Length > MaximumSnapshotBytes + 44)
                return Failure(BattleTransactionDecodeStatus.Corrupted);
            try
            {
                using (var stream = new MemoryStream(snapshot, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    if (reader.ReadInt32() != Magic) return Failure(BattleTransactionDecodeStatus.Corrupted);
                    int version = reader.ReadInt32();
                    if (version != Version) return Failure(BattleTransactionDecodeStatus.UnsupportedVersion);
                    int payloadLength = reader.ReadInt32();
                    if (payloadLength < 0 || payloadLength > MaximumSnapshotBytes ||
                        payloadLength != stream.Length - stream.Position - 32)
                        return Failure(BattleTransactionDecodeStatus.Corrupted);
                    byte[] expectedHash = reader.ReadBytes(32);
                    byte[] payload = reader.ReadBytes(payloadLength);
                    if (!FixedEquals(expectedHash, Sha256(payload)))
                        return Failure(BattleTransactionDecodeStatus.Corrupted);
                    return DecodePayload(payload);
                }
            }
            catch (Exception)
            {
                return Failure(BattleTransactionDecodeStatus.Corrupted);
            }
        }

        public static byte[] CreateIncompatibleSnapshotForTest()
        {
            return Wrap(Version + 1, Array.Empty<byte>());
        }

        private static BattleTransactionDecodeResult DecodePayload(byte[] payload)
        {
            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                int count = reader.ReadInt32();
                if (count < 0 || count > BattleTechnicalLimits.MaximumCollectionCount)
                    return Failure(BattleTransactionDecodeStatus.Corrupted);
                var records = new Dictionary<string, BattleTransactionRecord>(StringComparer.Ordinal);
                for (int index = 0; index < count; index++)
                {
                    BattleTransactionRecord record = ReadRecord(reader);
                    if (!records.TryAdd(record.BattleResultId, record))
                        return Failure(BattleTransactionDecodeStatus.Corrupted);
                }
                if (stream.Position != stream.Length)
                    return Failure(BattleTransactionDecodeStatus.Corrupted);
                return Success(records);
            }
        }

        private static byte[] Wrap(int version, byte[] payload)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(version);
                writer.Write(payload.Length);
                writer.Write(Sha256(payload));
                writer.Write(payload);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void WriteRecord(BinaryWriter writer, BattleTransactionRecord record)
        {
            writer.Write((int)record.State);
            WriteString(writer, record.DiagnosticCode);
            writer.Write(record.CommitSequence);
            WriteResult(writer, record.Result);
        }

        private static BattleTransactionRecord ReadRecord(BinaryReader reader)
        {
            var state = (BattleTransactionState)reader.ReadInt32();
            if (state < BattleTransactionState.Pending || state > BattleTransactionState.RecoveryRequired)
                throw new InvalidDataException();
            string diagnostic = ReadString(reader);
            long sequence = reader.ReadInt64();
            if (sequence < 0L) throw new InvalidDataException();
            return new BattleTransactionRecord(ReadResult(reader), state, diagnostic, sequence);
        }

        private static void WriteResult(BinaryWriter writer, BattleComputedResult result)
        {
            WriteString(writer, result.GameId);
            WriteString(writer, result.CatalogSetId);
            WriteString(writer, result.ProfileId);
            WriteString(writer, result.BattleRequestId);
            WriteString(writer, result.BattleId);
            WriteString(writer, result.BattleResultId);
            WriteString(writer, result.ExpectedResultConsumerId);
            writer.Write((int)result.ExecutionMode);
            writer.Write((int)result.BattleKind);
            WriteString(writer, result.BattleTypeId);
            writer.Write((int)result.Outcome);
            WriteString(writer, result.OutcomeTechnicalId);
            writer.Write(result.AttackerPower);
            writer.Write(result.OpponentPower);
            WriteRounds(writer, result.Rounds);
            WriteLosses(writer, result.AttackerLosses);
            WriteLosses(writer, result.OpponentLosses);
            writer.Write(result.RewardProposal.Credits);
            writer.Write(result.RewardProposal.Food);
            writer.Write(result.RewardProposal.Gold);
            writer.Write(result.RewardProposal.Experience);
            WriteContributions(writer, result.Contributions);
            WriteString(writer, result.CatalogSha256);
            WriteString(writer, result.AttackerArmySha256);
            WriteString(writer, result.OpponentSha256);
            WriteString(writer, result.ContextSha256);
            WriteString(writer, result.RulesSha256);
            WriteString(writer, result.RewardProfileSha256);
            WriteString(writer, result.DeterminismVersion);
            WriteString(writer, result.SeedHex);
            WriteString(writer, result.ComputationSha256);
        }

        private static BattleComputedResult ReadResult(BinaryReader reader)
        {
            string gameId = ReadString(reader);
            string catalogSetId = ReadString(reader);
            string profileId = ReadString(reader);
            string requestId = ReadString(reader);
            string battleId = ReadString(reader);
            string resultId = ReadString(reader);
            string consumerId = ReadString(reader);
            var mode = (BattleExecutionMode)reader.ReadInt32();
            var kind = (BattleKind)reader.ReadInt32();
            string typeId = ReadString(reader);
            var outcome = (BattleOutcome)reader.ReadInt32();
            string outcomeId = ReadString(reader);
            long attackerPower = reader.ReadInt64();
            long opponentPower = reader.ReadInt64();
            BattleRoundResult[] rounds = ReadRounds(reader);
            BattleTroopLoss[] attackerLosses = ReadLosses(reader);
            BattleTroopLoss[] opponentLosses = ReadLosses(reader);
            var reward = new BattleRewardProposal(
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            BattleContribution[] contributions = ReadContributions(reader);
            return new BattleComputedResult(
                gameId, catalogSetId, profileId, requestId, battleId, resultId, consumerId,
                mode, kind, typeId, outcome, outcomeId, attackerPower, opponentPower,
                rounds, attackerLosses, opponentLosses, reward, contributions,
                ReadString(reader), ReadString(reader), ReadString(reader), ReadString(reader),
                ReadString(reader), ReadString(reader), ReadString(reader), ReadString(reader),
                ReadString(reader));
        }

        private static void WriteRounds(BinaryWriter writer, IReadOnlyList<BattleRoundResult> values)
        {
            writer.Write(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                BattleRoundResult value = values[index];
                writer.Write(value.RoundIndex);
                writer.Write(value.AttackerRateMicros);
                writer.Write(value.OpponentRateMicros);
                writer.Write(value.DamageToOpponentMicros);
                writer.Write(value.DamageToAttackerMicros);
                writer.Write(value.AttackerRemainingPowerMicros);
                writer.Write(value.OpponentRemainingPowerMicros);
            }
        }

        private static BattleRoundResult[] ReadRounds(BinaryReader reader)
        {
            int count = ReadCount(reader);
            var values = new BattleRoundResult[count];
            for (int index = 0; index < count; index++)
                values[index] = new BattleRoundResult(
                    reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64(),
                    reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64());
            return values;
        }

        private static void WriteLosses(BinaryWriter writer, IReadOnlyList<BattleTroopLoss> values)
        {
            writer.Write(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                WriteString(writer, values[index].TroopDefinitionId);
                writer.Write(values[index].Killed);
                writer.Write(values[index].Wounded);
                writer.Write(values[index].Survived);
            }
        }

        private static BattleTroopLoss[] ReadLosses(BinaryReader reader)
        {
            int count = ReadCount(reader);
            var values = new BattleTroopLoss[count];
            for (int index = 0; index < count; index++)
                values[index] = new BattleTroopLoss(
                    ReadString(reader), reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64());
            return values;
        }

        private static void WriteContributions(BinaryWriter writer, IReadOnlyList<BattleContribution> values)
        {
            writer.Write(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                WriteString(writer, values[index].TechnicalId);
                writer.Write(values[index].ValueMicros);
            }
        }

        private static BattleContribution[] ReadContributions(BinaryReader reader)
        {
            int count = ReadCount(reader);
            var values = new BattleContribution[count];
            for (int index = 0; index < count; index++)
                values[index] = new BattleContribution(ReadString(reader), reader.ReadInt64());
            return values;
        }

        private static int ReadCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > BattleTechnicalLimits.MaximumCollectionCount)
                throw new InvalidDataException();
            return count;
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > 4096) throw new InvalidDataException();
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > 4096) throw new InvalidDataException();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return new UTF8Encoding(false, true).GetString(bytes);
        }

        private static byte[] Sha256(byte[] value)
        {
            using (SHA256 hash = SHA256.Create()) return hash.ComputeHash(value);
        }

        private static bool FixedEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int difference = 0;
            for (int index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }

        private static BattleTransactionDecodeResult Success(
            Dictionary<string, BattleTransactionRecord> records)
        {
            return new BattleTransactionDecodeResult(BattleTransactionDecodeStatus.Success, records);
        }

        private static BattleTransactionDecodeResult Failure(BattleTransactionDecodeStatus status)
        {
            return new BattleTransactionDecodeResult(status, null);
        }
    }
}
