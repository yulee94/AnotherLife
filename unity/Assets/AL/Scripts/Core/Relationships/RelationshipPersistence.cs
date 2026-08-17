using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Core.Relationships
{
    public enum RelationshipPersistenceStatus
    {
        Committed,
        AlreadyCommitted,
        Rejected
    }

    public enum RelationshipPersistenceFaultPoint
    {
        BeforeValidation,
        DuringPersistence,
        AfterDurableWriteBeforeAcknowledgement,
        DuringReload
    }

    public sealed class RelationshipPersistenceFaultException : Exception
    {
        public RelationshipPersistenceFaultException(RelationshipPersistenceFaultPoint point)
            : base("Injected relationship persistence fault: " + point)
        {
            Point = point;
        }

        public RelationshipPersistenceFaultPoint Point { get; }
    }

    public interface IRelationshipPersistenceFaultInjector
    {
        void Hit(RelationshipPersistenceFaultPoint point);
    }

    public interface IRelationshipDocumentStore
    {
        object SyncRoot { get; }
        byte[] Read();
        void WriteAtomically(byte[] expected, byte[] document, Action beforePublish = null);
    }

    public sealed class FileRelationshipDocumentStore : IRelationshipDocumentStore
    {
        private static readonly ConcurrentDictionary<string, object> Locks =
            new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        private readonly string path;

        public FileRelationshipDocumentStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.", nameof(path));
            this.path = Path.GetFullPath(path);
        }

        public object SyncRoot => Locks.GetOrAdd(path, _ => new object());

        public byte[] Read()
        {
            if (File.Exists(path)) return File.ReadAllBytes(path);
            string backup = path + ".backup";
            return File.Exists(backup) ? File.ReadAllBytes(backup) : Array.Empty<byte>();
        }

        public void WriteAtomically(byte[] expected, byte[] document, Action beforePublish = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            byte[] current = Read();
            if (!(expected ?? Array.Empty<byte>()).SequenceEqual(current))
                throw new InvalidOperationException("Relationship persistence generation changed before publication.");
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(document, 0, document.Length);
                stream.Flush(true);
            }

            beforePublish?.Invoke();

            if (File.Exists(path))
            {
                string backup = path + ".backup";
                try
                {
                    File.Replace(temporary, path, backup);
                }
                catch (Exception exception) when (
                    exception is PlatformNotSupportedException ||
                    exception is NotSupportedException)
                {
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(path, backup);
                    try
                    {
                        File.Move(temporary, path);
                    }
                    catch
                    {
                        if (!File.Exists(path) && File.Exists(backup))
                            File.Move(backup, path);
                        throw;
                    }
                }
                if (File.Exists(backup)) File.Delete(backup);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
    }

    public sealed class MemoryRelationshipDocumentStore : IRelationshipDocumentStore
    {
        private readonly object syncRoot = new object();
        private byte[] document;

        public MemoryRelationshipDocumentStore(byte[] initialDocument)
        {
            document = (initialDocument ?? Array.Empty<byte>()).ToArray();
        }

        public object SyncRoot => syncRoot;

        public byte[] Read() => document.ToArray();

        public void WriteAtomically(byte[] expected, byte[] nextDocument, Action beforePublish = null)
        {
            if (nextDocument == null) throw new ArgumentNullException(nameof(nextDocument));
            if (!(expected ?? Array.Empty<byte>()).SequenceEqual(document))
                throw new InvalidOperationException("Relationship persistence generation changed before publication.");
            beforePublish?.Invoke();
            document = nextDocument.ToArray();
        }
    }

    public sealed class RelationshipPersistentSnapshot
    {
        internal RelationshipPersistentSnapshot(
            SaveGameData save,
            RelationshipTransactionState state,
            RelationshipPersistenceMigrationReport migration)
        {
            Save = save;
            State = state;
            Migration = migration;
        }

        public SaveGameData Save { get; }
        public RelationshipTransactionState State { get; }
        public RelationshipPersistenceMigrationReport Migration { get; }
    }

    public sealed class RelationshipPersistenceResult
    {
        internal RelationshipPersistenceResult(
            RelationshipPersistenceStatus status,
            RelationshipPersistentSnapshot snapshot,
            RelationshipTransactionReceipt receipt)
        {
            Status = status;
            Snapshot = snapshot;
            Receipt = receipt;
        }

        public RelationshipPersistenceStatus Status { get; }
        public RelationshipPersistentSnapshot Snapshot { get; }
        public RelationshipTransactionReceipt Receipt { get; }
    }

    public sealed class RelationshipPersistenceCoordinator
    {
        private readonly RelationshipCatalogResolver resolver;
        private readonly RelationshipLegacyIdentityMigrator migrator;
        private readonly IRelationshipDocumentStore store;
        private readonly IRelationshipPersistenceFaultInjector faults;

        public RelationshipPersistenceCoordinator(
            RelationshipCatalogResolver resolver,
            RelationshipLegacyIdentityMigrator migrator,
            IRelationshipDocumentStore store,
            IRelationshipPersistenceFaultInjector faults = null)
        {
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            this.migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.faults = faults;
        }

        public RelationshipPersistentSnapshot Reload()
        {
            lock (store.SyncRoot)
            {
                return ReloadCore(store.Read());
            }
        }

        public RelationshipPersistenceResult Commit(RelationshipTransaction transaction)
        {
            lock (store.SyncRoot)
            {
            byte[] expected = store.Read();
            RelationshipPersistentSnapshot current = ReloadCore(expected);
            Hit(RelationshipPersistenceFaultPoint.BeforeValidation);
            RelationshipTransactionResult candidate = RelationshipTransactionEngine.Commit(
                resolver, current.State, transaction);
            if (candidate.Status == RelationshipTransactionStatus.AlreadyCommitted)
            {
                return new RelationshipPersistenceResult(
                    RelationshipPersistenceStatus.AlreadyCommitted,
                    current,
                    candidate.Receipt);
            }
            if (candidate.Status != RelationshipTransactionStatus.Committed)
            {
                return new RelationshipPersistenceResult(
                    RelationshipPersistenceStatus.Rejected,
                    current,
                    null);
            }

            SaveGameData candidateSave = RelationshipPersistenceCodec.Clone(current.Save);
            PublishSnapshots(candidateSave, candidate.State);
            byte[] document = RelationshipPersistenceCodec.Serialize(
                candidateSave,
                candidate.State.Receipts.Values);
            store.WriteAtomically(
                expected,
                document,
                () => Hit(RelationshipPersistenceFaultPoint.DuringPersistence));
            Hit(RelationshipPersistenceFaultPoint.AfterDurableWriteBeforeAcknowledgement);
            RelationshipPersistentSnapshot published = ReloadCore(store.Read());
            return new RelationshipPersistenceResult(
                RelationshipPersistenceStatus.Committed,
                published,
                candidate.Receipt);
            }
        }

        private RelationshipPersistentSnapshot ReloadCore(byte[] bytes)
        {
            Hit(RelationshipPersistenceFaultPoint.DuringReload);
            RelationshipPersistenceDocument document = RelationshipPersistenceCodec.Deserialize(bytes);
            if (document.Save.SaveSchemaVersion > SaveGameData.CurrentSaveSchemaVersion)
                throw new InvalidDataException("Forward relationship save schema is read-only.");
            RelationshipPersistenceMigrationReport migration =
                RelationshipPersistenceIdentityMigration.Apply(migrator, document.Save);
            if (!migration.CanPersist)
                throw new InvalidDataException("Relationship identities cannot be safely persisted.");
            RelationshipTransactionState state = BuildState(document.Save, document.Receipts);
            return new RelationshipPersistentSnapshot(document.Save, state, migration);
        }

        private RelationshipTransactionState BuildState(
            SaveGameData save,
            IEnumerable<RelationshipTransactionReceipt> receipts)
        {
            RelationshipNumericSnapshot affinity = RelationshipSnapshotBuilder.BuildNpcAffinity(
                resolver,
                (save.Reputation ?? new List<NpcAffinityData>()).Select(row =>
                    new RelationshipNumericRow(row.NpcId, row.Affinity)),
                true);
            RelationshipNumericSnapshot faction = RelationshipSnapshotBuilder.BuildFactionReputation(
                resolver,
                (save.FactionReputations ?? new List<FactionRepData>()).Select(row =>
                    new RelationshipNumericRow(row.FactionId, row.Reputation)),
                true);
            return RelationshipTransactionState.Restore(affinity, faction, receipts);
        }

        private static void PublishSnapshots(SaveGameData save, RelationshipTransactionState state)
        {
            var preservedNpc = (save.Reputation ?? new List<NpcAffinityData>())
                .Where(row => row != null && state.Affinity.PreservedUnknownIds.Contains(row.NpcId))
                .Select(row => new NpcAffinityData { NpcId = row.NpcId, Affinity = row.Affinity });
            save.Reputation = state.Affinity.Values
                .Select(pair => new NpcAffinityData { NpcId = pair.Key, Affinity = (float)pair.Value })
                .Concat(preservedNpc)
                .ToList();

            var preservedFaction = (save.FactionReputations ?? new List<FactionRepData>())
                .Where(row => row != null && state.Faction.PreservedUnknownIds.Contains(row.FactionId))
                .Select(row => new FactionRepData { FactionId = row.FactionId, Reputation = row.Reputation });
            save.FactionReputations = state.Faction.Values
                .Select(pair => new FactionRepData { FactionId = pair.Key, Reputation = checked((int)pair.Value) })
                .Concat(preservedFaction)
                .ToList();
        }

        private void Hit(RelationshipPersistenceFaultPoint point)
        {
            faults?.Hit(point);
        }
    }

    internal sealed class RelationshipPersistenceDocument
    {
        public SaveGameData Save;
        public IReadOnlyList<RelationshipTransactionReceipt> Receipts;
    }

    public static class RelationshipPersistenceCodec
    {
        private const int Version = 1;

        public static byte[] SerializeLegacy(SaveGameData save)
        {
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(save ?? new SaveGameData()));
        }

        public static SaveGameData Clone(SaveGameData save)
        {
            return JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(save ?? new SaveGameData()));
        }

        internal static byte[] Serialize(
            SaveGameData save,
            IEnumerable<RelationshipTransactionReceipt> receipts)
        {
            var envelope = new Envelope
            {
                Version = Version,
                SaveJson = JsonUtility.ToJson(save),
                Receipts = (receipts ?? Enumerable.Empty<RelationshipTransactionReceipt>())
                    .Select(ToData)
                    .ToList()
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope));
        }

        internal static RelationshipPersistenceDocument Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return new RelationshipPersistenceDocument
                {
                    Save = new SaveGameData(),
                    Receipts = Array.Empty<RelationshipTransactionReceipt>()
                };
            }

            string json = Encoding.UTF8.GetString(bytes);
            Envelope envelope = JsonUtility.FromJson<Envelope>(json);
            bool isEnvelope = json.IndexOf("\"Version\"", StringComparison.Ordinal) >= 0 ||
                json.IndexOf("\"SaveJson\"", StringComparison.Ordinal) >= 0 ||
                json.IndexOf("\"Receipts\"", StringComparison.Ordinal) >= 0;
            if (isEnvelope && (envelope == null || envelope.Version != Version ||
                string.IsNullOrEmpty(envelope.SaveJson)))
                throw new InvalidDataException("Unsupported or malformed relationship persistence envelope.");
            if (isEnvelope)
            {
                return new RelationshipPersistenceDocument
                {
                    Save = JsonUtility.FromJson<SaveGameData>(envelope.SaveJson),
                    Receipts = (envelope.Receipts ?? new List<ReceiptData>())
                        .Select(FromData)
                        .ToArray()
                };
            }

            return new RelationshipPersistenceDocument
            {
                Save = JsonUtility.FromJson<SaveGameData>(json) ?? new SaveGameData(),
                Receipts = Array.Empty<RelationshipTransactionReceipt>()
            };
        }

        private static ReceiptData ToData(RelationshipTransactionReceipt receipt)
        {
            return new ReceiptData
            {
                TransactionId = receipt.TransactionId,
                CorrelationId = receipt.CorrelationId,
                SemanticFingerprint = receipt.SemanticFingerprint,
                CommitRevision = receipt.CommitRevision,
                Changes = receipt.Changes.Select(change => new ChangeData
                {
                    Domain = (int)change.Domain,
                    CanonicalTargetId = change.CanonicalTargetId,
                    PreviousValue = change.PreviousValue,
                    NewValue = change.NewValue,
                    AppliedDelta = change.AppliedDelta,
                    OperationId = change.OperationId,
                    CorrelationId = change.CorrelationId
                }).ToList()
            };
        }

        private static RelationshipTransactionReceipt FromData(ReceiptData data)
        {
            return new RelationshipTransactionReceipt(
                data.TransactionId,
                data.CorrelationId,
                data.SemanticFingerprint,
                (data.Changes ?? new List<ChangeData>()).Select(change =>
                    new RelationshipCommittedChange(
                        (RelationshipDomain)change.Domain,
                        change.CanonicalTargetId,
                        change.PreviousValue,
                        change.NewValue,
                        change.AppliedDelta,
                        change.OperationId,
                        change.CorrelationId)),
                data.CommitRevision);
        }

        [Serializable]
        private sealed class Envelope
        {
            public int Version;
            public string SaveJson;
            public List<ReceiptData> Receipts = new List<ReceiptData>();
        }

        [Serializable]
        private sealed class ReceiptData
        {
            public string TransactionId;
            public string CorrelationId;
            public string SemanticFingerprint;
            public string CommitRevision;
            public List<ChangeData> Changes = new List<ChangeData>();
        }

        [Serializable]
        private sealed class ChangeData
        {
            public int Domain;
            public string CanonicalTargetId;
            public double PreviousValue;
            public double NewValue;
            public double AppliedDelta;
            public string OperationId;
            public string CorrelationId;
        }
    }
}
