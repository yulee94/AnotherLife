using System;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Owns the single published snapshot reference. Candidates are validated off-side and
    /// exchanged atomically; a failed reload never replaces the previous accepted snapshot.
    /// </summary>
    public sealed class GameDataCatalogStore : IDisposable
    {
        private readonly object gate = new object();
        private GameDataCatalogSetSnapshot snapshot;
        private GameDataCatalogServiceState state;
        private GameDataCatalogLoadResult lastLoadResult;
        private IGameDataCatalogLoadOperation activeOperation;
        private long generation;
        private long revision;
        private bool disposed;

        public GameDataCatalogStore()
        {
            state = NewState(
                GameDataCatalogLifecycleStatus.Uninitialized,
                null,
                false,
                new GameDataCatalogDiagnostic[0],
                default(DateTimeOffset),
                default(DateTimeOffset));
        }

        public GameDataCatalogSetSnapshot Snapshot
        {
            get
            {
                lock (gate) return snapshot;
            }
        }

        public GameDataCatalogServiceState State
        {
            get
            {
                lock (gate) return state;
            }
        }

        public GameDataCatalogLoadResult LastLoadResult
        {
            get
            {
                lock (gate) return lastLoadResult;
            }
        }

        public IGameDataCatalogLoadOperation BeginLoad(
            GameDataCatalogLoader loader,
            IGameDataCatalogSource source,
            string manifestRelativePath,
            GameDataCatalogSourceKind sourceKind)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            if (source == null) throw new ArgumentNullException(nameof(source));

            IGameDataCatalogLoadOperation previous;
            long loadGeneration;
            var startedAtUtc = DateTimeOffset.UtcNow;
            lock (gate)
            {
                ThrowIfDisposed();
                if (!Enum.IsDefined(typeof(GameDataCatalogSourceKind), sourceKind))
                {
                    throw new ArgumentOutOfRangeException(nameof(sourceKind));
                }

                loadGeneration = ++generation;
                previous = activeOperation;
                activeOperation = null;
                state = snapshot == null
                    ? NewState(
                        GameDataCatalogLifecycleStatus.Loading,
                        null,
                        true,
                        new GameDataCatalogDiagnostic[0],
                        startedAtUtc,
                        default(DateTimeOffset))
                    : NewState(
                        ReadyStatus(snapshot),
                        snapshot,
                        true,
                        state.Diagnostics,
                        startedAtUtc,
                        default(DateTimeOffset));
            }

            SafeDispose(previous);

            IGameDataCatalogLoadOperation operation;
            try
            {
                operation = loader.BeginLoad(
                    source,
                    manifestRelativePath,
                    sourceKind,
                    result => CompleteLoad(loadGeneration, result));
                if (operation == null)
                {
                    throw new InvalidOperationException("The catalog loader returned no operation.");
                }
            }
            catch (Exception exception)
            {
                var diagnostic = new GameDataCatalogDiagnostic(
                    "LOAD-START-THREW",
                    GameDataDiagnosticSeverity.Error,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "$",
                    "catalog.load.start_threw",
                    "The catalog loader failed while starting a load (" + exception.GetType().Name + ").",
                    "Inspect the loader boundary and return a typed load operation.",
                    true,
                    true,
                    -1,
                    -1);
                CompleteLoad(
                    loadGeneration,
                    new GameDataCatalogLoadResult(
                        GameDataCatalogLoadStatus.ReadFailed,
                        null,
                        new[] { diagnostic },
                        startedAtUtc,
                        DateTimeOffset.UtcNow));
                return CompletedLoadOperation.Instance;
            }

            var keepOperation = false;
            lock (gate)
            {
                if (!disposed && generation == loadGeneration && state.IsLoading)
                {
                    activeOperation = operation;
                    keepOperation = true;
                }
            }

            if (!keepOperation) SafeDispose(operation);
            return operation;
        }

        public void Tick()
        {
            IGameDataCatalogLoadOperation operation;
            lock (gate) operation = activeOperation;
            if (operation != null)
            {
                try
                {
                    operation.Tick();
                }
                catch (Exception)
                {
                    try
                    {
                        operation.Cancel();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        public void CancelActiveLoad()
        {
            IGameDataCatalogLoadOperation operation;
            lock (gate) operation = activeOperation;
            if (operation != null)
            {
                try
                {
                    operation.Cancel();
                }
                catch (Exception)
                {
                }
            }
        }

        public GameDataCatalogQueryResult QueryRecord(string family, string id)
        {
            GameDataCatalogSetSnapshot currentSnapshot;
            GameDataCatalogServiceState currentState;
            lock (gate)
            {
                currentSnapshot = snapshot;
                currentState = state;
            }

            if (currentSnapshot != null && currentState.Status != GameDataCatalogLifecycleStatus.Disposed)
            {
                return currentSnapshot.QueryRecord(family, id);
            }

            var status = MapQueryStatus(currentState.Status);
            return GameDataCatalogQueryResult.Empty(status, family, id, currentState.Diagnostics);
        }

        public void Dispose()
        {
            IGameDataCatalogLoadOperation operation;
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                generation++;
                operation = activeOperation;
                activeOperation = null;
                snapshot = null;
                var diagnostic = new GameDataCatalogDiagnostic(
                    "STORE-DISPOSED",
                    GameDataDiagnosticSeverity.Information,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "$",
                    "catalog.store.disposed",
                    "The catalog store has been disposed and exposes no definitions.",
                    "Create a new store through the owning runtime lifecycle.",
                    false,
                    false,
                    -1,
                    -1);
                state = NewState(
                    GameDataCatalogLifecycleStatus.Disposed,
                    null,
                    false,
                    new[] { diagnostic },
                    default(DateTimeOffset),
                    default(DateTimeOffset));
            }

            SafeDispose(operation);
        }

        private void CompleteLoad(long loadGeneration, GameDataCatalogLoadResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            lock (gate)
            {
                if (disposed || loadGeneration != generation) return;
                activeOperation = null;

                if (result.IsSuccess)
                {
                    var published = result.Snapshot.WithRevision(++revision);
                    snapshot = published;
                    lastLoadResult = result.WithSnapshot(published);
                    state = NewState(
                        ReadyStatus(published),
                        published,
                        false,
                        result.Diagnostics,
                        result.StartedAtUtc,
                        result.CompletedAtUtc);
                    return;
                }

                lastLoadResult = result;
                if (snapshot != null)
                {
                    state = NewState(
                        ReadyStatus(snapshot),
                        snapshot,
                        false,
                        result.Diagnostics,
                        result.StartedAtUtc,
                        result.CompletedAtUtc);
                    return;
                }

                state = NewState(
                    MapLifecycleStatus(result.Status),
                    null,
                    false,
                    result.Diagnostics,
                    result.StartedAtUtc,
                    result.CompletedAtUtc);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(GameDataCatalogStore));
        }

        private static GameDataCatalogLifecycleStatus ReadyStatus(GameDataCatalogSetSnapshot value)
        {
            if (value.SourceKind == GameDataCatalogSourceKind.DevelopmentFallback)
            {
                return GameDataCatalogLifecycleStatus.DevelopmentFallback;
            }

            return value.MissingOptionalFamilies.Count == 0
                ? GameDataCatalogLifecycleStatus.Ready
                : GameDataCatalogLifecycleStatus.ReadyWithOptionalGaps;
        }

        private static GameDataCatalogLifecycleStatus MapLifecycleStatus(GameDataCatalogLoadStatus status)
        {
            switch (status)
            {
                case GameDataCatalogLoadStatus.UnsupportedVersion:
                    return GameDataCatalogLifecycleStatus.UnsupportedVersion;
                case GameDataCatalogLoadStatus.MalformedJson:
                case GameDataCatalogLoadStatus.InvalidEnvelope:
                case GameDataCatalogLoadStatus.HashMismatch:
                case GameDataCatalogLoadStatus.InvalidRecord:
                case GameDataCatalogLoadStatus.CrossReferenceFailure:
                    return GameDataCatalogLifecycleStatus.Invalid;
                case GameDataCatalogLoadStatus.Disposed:
                    return GameDataCatalogLifecycleStatus.Disposed;
                default:
                    return GameDataCatalogLifecycleStatus.Unavailable;
            }
        }

        private static GameDataQueryStatus MapQueryStatus(GameDataCatalogLifecycleStatus status)
        {
            switch (status)
            {
                case GameDataCatalogLifecycleStatus.Uninitialized:
                case GameDataCatalogLifecycleStatus.Loading:
                    return GameDataQueryStatus.CatalogPending;
                case GameDataCatalogLifecycleStatus.Invalid:
                    return GameDataQueryStatus.CatalogInvalid;
                case GameDataCatalogLifecycleStatus.UnsupportedVersion:
                    return GameDataQueryStatus.UnsupportedVersion;
                default:
                    return GameDataQueryStatus.CatalogUnavailable;
            }
        }

        private static GameDataCatalogServiceState NewState(
            GameDataCatalogLifecycleStatus status,
            GameDataCatalogSetSnapshot currentSnapshot,
            bool isLoading,
            System.Collections.Generic.IEnumerable<GameDataCatalogDiagnostic> diagnostics,
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc)
        {
            return new GameDataCatalogServiceState(
                status,
                currentSnapshot,
                isLoading,
                diagnostics,
                startedAtUtc,
                completedAtUtc);
        }

        private static void SafeDispose(IDisposable value)
        {
            if (value == null) return;
            try
            {
                value.Dispose();
            }
            catch (Exception)
            {
            }
        }

        private sealed class CompletedLoadOperation : IGameDataCatalogLoadOperation
        {
            public static readonly CompletedLoadOperation Instance = new CompletedLoadOperation();

            private CompletedLoadOperation()
            {
            }

            public bool IsCompleted => true;
            public bool IsCancelled => false;

            public void Tick()
            {
            }

            public void Cancel()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
