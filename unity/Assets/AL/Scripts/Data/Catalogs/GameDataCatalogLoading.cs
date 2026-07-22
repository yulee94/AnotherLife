using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AL.Data.Catalogs
{
    public interface IGameDataCatalogClock
    {
        long Timestamp { get; }
        DateTimeOffset UtcNow { get; }
        TimeSpan ElapsedSince(long timestamp);
    }

    public sealed class SystemGameDataCatalogClock : IGameDataCatalogClock
    {
        public long Timestamp => Stopwatch.GetTimestamp();
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public TimeSpan ElapsedSince(long timestamp)
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - timestamp;
            return TimeSpan.FromSeconds((double)elapsedTicks / Stopwatch.Frequency);
        }
    }

    public interface IGameDataCatalogLoadOperation : IDisposable
    {
        bool IsCompleted { get; }
        bool IsCancelled { get; }
        void Tick();
        void Cancel();
    }

    public sealed class GameDataCatalogLoader
    {
        private readonly GameDataCatalogValidationPolicy policy;
        private readonly GameDataCatalogSchemaRegistry schemas;
        private readonly IGameDataCatalogClock clock;
        private readonly TimeSpan requestTimeout;

        public GameDataCatalogLoader(
            GameDataCatalogValidationPolicy policy,
            GameDataCatalogSchemaRegistry schemas,
            IGameDataCatalogClock clock = null,
            TimeSpan? requestTimeout = null)
        {
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
            this.schemas = schemas ?? throw new ArgumentNullException(nameof(schemas));
            this.clock = clock ?? new SystemGameDataCatalogClock();
            this.requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(15);
            if (this.requestTimeout <= TimeSpan.Zero || this.requestTimeout > TimeSpan.FromMinutes(5))
            {
                throw new ArgumentOutOfRangeException(nameof(requestTimeout), "The request timeout must be positive and bounded.");
            }
        }

        public IGameDataCatalogLoadOperation BeginLoad(
            IGameDataCatalogSource source,
            string manifestRelativePath,
            GameDataCatalogSourceKind sourceKind,
            Action<GameDataCatalogLoadResult> completion)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (completion == null) throw new ArgumentNullException(nameof(completion));
            if (!Enum.IsDefined(typeof(GameDataCatalogSourceKind), sourceKind))
            {
                throw new ArgumentOutOfRangeException(nameof(sourceKind));
            }
            return new LoadOperation(
                source,
                manifestRelativePath,
                sourceKind,
                policy,
                schemas,
                clock,
                requestTimeout,
                completion);
        }

        private sealed class LoadOperation : IGameDataCatalogLoadOperation
        {
            private readonly object sync = new object();
            private readonly IGameDataCatalogSource source;
            private readonly string manifestRelativePath;
            private readonly GameDataCatalogSourceKind sourceKind;
            private readonly GameDataCatalogValidationPolicy policy;
            private readonly GameDataCatalogSchemaRegistry schemas;
            private readonly IGameDataCatalogClock clock;
            private readonly TimeSpan requestTimeout;
            private Action<GameDataCatalogLoadResult> completion;
            private readonly DateTimeOffset startedAtUtc;
            private readonly List<GameDataCatalogArtifactInput> inputs = new List<GameDataCatalogArtifactInput>();
            private GameDataCatalogManifest manifest;
            private PendingRead pending;
            private int artifactIndex;
            private int requestToken;
            private int activeHandlerCount;
            private bool disposed;
            private bool isCompleted;
            private bool isCancelled;

            public LoadOperation(
                IGameDataCatalogSource source,
                string manifestRelativePath,
                GameDataCatalogSourceKind sourceKind,
                GameDataCatalogValidationPolicy policy,
                GameDataCatalogSchemaRegistry schemas,
                IGameDataCatalogClock clock,
                TimeSpan requestTimeout,
                Action<GameDataCatalogLoadResult> completion)
            {
                this.source = source;
                this.manifestRelativePath = manifestRelativePath ?? string.Empty;
                this.sourceKind = sourceKind;
                this.policy = policy;
                this.schemas = schemas;
                this.clock = clock;
                this.requestTimeout = requestTimeout;
                this.completion = completion;
                startedAtUtc = clock.UtcNow;

                if (!GameDataCatalogIdentifiers.IsCanonicalRelativeJsonPath(this.manifestRelativePath))
                {
                    CompleteFailure(
                        GameDataCatalogLoadStatus.InvalidEnvelope,
                        "MANIFEST-PATH",
                        "The manifest path is not a canonical relative JSON path.",
                        "Use a normalized path beneath the packaged game-data root.");
                    return;
                }

                RequestBytes(this.manifestRelativePath, policy.MaximumManifestBytes, HandleManifestRead);
            }

            public bool IsCompleted
            {
                get
                {
                    lock (sync) return isCompleted;
                }
            }

            public bool IsCancelled
            {
                get
                {
                    lock (sync) return isCancelled;
                }
            }

            public void Tick()
            {
                PendingRead timedOut;
                lock (sync)
                {
                    if (isCompleted || pending == null) return;
                    if (clock.ElapsedSince(pending.StartTimestamp) < requestTimeout) return;
                    timedOut = pending;
                }

                CompleteFailure(
                    GameDataCatalogLoadStatus.TimedOut,
                    "READ-TIMED-OUT",
                    "The packaged catalog read exceeded its bounded timeout.",
                    "Retry the packaged read or inspect platform packaging diagnostics.",
                    expectedPending: timedOut,
                    cancelOutstanding: true);
            }

            public void Cancel()
            {
                CompleteFailure(
                    GameDataCatalogLoadStatus.Cancelled,
                    "LOAD-CANCELLED",
                    "Catalog loading was cancelled before publication.",
                    "Start a new load when the owning runtime is ready.",
                    markCancelled: true,
                    cancelOutstanding: true);
            }

            public void Dispose()
            {
                CompleteFailure(
                    GameDataCatalogLoadStatus.Disposed,
                    "LOAD-DISPOSED",
                    "The catalog load was disposed before publication.",
                    "Create a new loader operation from a live owner.",
                    markDisposed: true,
                    cancelOutstanding: true);
            }

            private void HandleManifestRead(GameDataCatalogReadResult readResult)
            {
                if (IsCompleted) return;
                if (!string.Equals(readResult.RelativePath, manifestRelativePath, StringComparison.Ordinal))
                {
                    CompleteFailure(
                        GameDataCatalogLoadStatus.ReadFailed,
                        "SOURCE-PATH-MISMATCH",
                        "The byte source returned a result for a different logical path.",
                        "Bind each callback to the exact requested packaged path.");
                    return;
                }

                if (readResult.Status != GameDataCatalogReadStatus.Succeeded)
                {
                    var status = readResult.Status == GameDataCatalogReadStatus.NotFound
                        ? GameDataCatalogLoadStatus.MissingManifest
                        : MapReadStatus(readResult.Status);
                    CompleteFailure(
                        status,
                        status == GameDataCatalogLoadStatus.MissingManifest ? "MANIFEST-MISSING" : "MANIFEST-READ-FAILED",
                        status == GameDataCatalogLoadStatus.MissingManifest
                            ? "The packaged catalog-set manifest was not found."
                            : "The packaged catalog-set manifest could not be read (" + readResult.FailureCode + ").",
                        "Verify the manifest packaging and platform byte transport.");
                    return;
                }

                var validation = GameDataCatalogValidator.ValidateManifest(readResult.UnsafeBytes, policy);
                if (!validation.IsAccepted)
                {
                    Complete(new GameDataCatalogLoadResult(
                        validation.Status,
                        null,
                        validation.Diagnostics,
                        startedAtUtc,
                        clock.UtcNow));
                    return;
                }

                manifest = validation.Manifest;
                artifactIndex = 0;
                ReadNextArtifact();
            }

            private void ReadNextArtifact()
            {
                if (IsCompleted) return;
                if (artifactIndex >= manifest.Artifacts.Count)
                {
                    var result = GameDataCatalogValidator.ValidateCatalogSet(
                        manifest,
                        inputs,
                        schemas,
                        policy,
                        sourceKind,
                        startedAtUtc,
                        clock.UtcNow);
                    Complete(result);
                    return;
                }

                var descriptor = manifest.Artifacts[artifactIndex];
                RequestBytes(descriptor.RelativePath, policy.MaximumFamilyBytes, HandleArtifactRead);
            }

            private void HandleArtifactRead(GameDataCatalogReadResult readResult)
            {
                if (IsCompleted) return;
                var descriptor = manifest.Artifacts[artifactIndex];
                if (!string.Equals(readResult.RelativePath, descriptor.RelativePath, StringComparison.Ordinal))
                {
                    CompleteFailure(
                        GameDataCatalogLoadStatus.ReadFailed,
                        "SOURCE-PATH-MISMATCH",
                        "The byte source returned a result for a different logical path.",
                        "Bind each callback to the exact requested packaged path.");
                    return;
                }

                inputs.Add(new GameDataCatalogArtifactInput(
                    descriptor.RelativePath,
                    readResult.Status,
                    readResult.UnsafeBytes,
                    readResult.FailureCode));
                artifactIndex++;
                ReadNextArtifact();
            }

            private void RequestBytes(
                string relativePath,
                int maximumBytes,
                Action<GameDataCatalogReadResult> handler)
            {
                PendingRead slot;
                lock (sync)
                {
                    if (isCompleted) return;
                    slot = new PendingRead(++requestToken, clock.Timestamp);
                    pending = slot;
                }

                IGameDataCatalogReadOperation handle = null;
                try
                {
                    var request = new GameDataCatalogReadRequest(relativePath, maximumBytes, requestTimeout);
                    handle = source.Read(request, result => OnReadCompleted(slot, result, handler));
                }
                catch (Exception)
                {
                    if (slot.CallbackArrived)
                    {
                        return;
                    }

                    var shouldFail = false;
                    lock (sync)
                    {
                        if (!isCompleted && ReferenceEquals(pending, slot))
                        {
                            pending = null;
                            slot.Invalidate();
                            shouldFail = true;
                        }
                    }

                    if (!shouldFail) return;
                    CompleteFailure(
                        GameDataCatalogLoadStatus.ReadFailed,
                        "SOURCE-THREW",
                        "The packaged byte source threw while starting a read.",
                        "Return a typed read failure instead of throwing.");
                    return;
                }

                slot.Attach(handle);
                if (!slot.CallbackArrived && handle == null)
                {
                    var shouldFail = false;
                    lock (sync)
                    {
                        if (!isCompleted && ReferenceEquals(pending, slot))
                        {
                            pending = null;
                            slot.Invalidate();
                            shouldFail = true;
                        }
                    }

                    if (!shouldFail) return;
                    CompleteFailure(
                        GameDataCatalogLoadStatus.ReadFailed,
                        "SOURCE-HANDLE-MISSING",
                        "The byte source returned no operation and no synchronous result.",
                        "Return a live operation or invoke the completion callback synchronously.");
                }
            }

            private void OnReadCompleted(
                PendingRead slot,
                GameDataCatalogReadResult result,
                Action<GameDataCatalogReadResult> handler)
            {
                lock (sync)
                {
                    if (isCompleted || !ReferenceEquals(pending, slot) || !slot.TryComplete())
                    {
                        return;
                    }

                    pending = null;
                    activeHandlerCount++;
                }

                slot.DisposeHandle();
                try
                {
                    if (result == null)
                    {
                        CompleteFailure(
                            GameDataCatalogLoadStatus.ReadFailed,
                            "SOURCE-RESULT-MISSING",
                            "The byte source invoked its callback without a typed result.",
                            "Return exactly one non-null typed read result.");
                        return;
                    }

                    if (IsCompleted) return;
                    handler(result);
                }
                catch (Exception)
                {
                    CompleteFailure(
                        GameDataCatalogLoadStatus.ReadFailed,
                        "INTERNAL-HANDLER-THREW",
                        "Catalog read handling failed before validation could finish.",
                        "Inspect the loader implementation and return a typed validation result.",
                        cancelOutstanding: true);
                }
                finally
                {
                    lock (sync)
                    {
                        activeHandlerCount--;
                        if (isCompleted && activeHandlerCount == 0)
                        {
                            ReleaseTransientStateUnderLock();
                        }
                    }
                }
            }

            private PendingRead DetachPendingUnderLock()
            {
                var current = pending;
                pending = null;
                if (current != null) current.Invalidate();
                return current;
            }

            private void CompleteFailure(
                GameDataCatalogLoadStatus status,
                string code,
                string message,
                string action,
                PendingRead expectedPending = null,
                bool markCancelled = false,
                bool markDisposed = false,
                bool cancelOutstanding = false)
            {
                TerminalDispatch dispatch;
                lock (sync)
                {
                    var diagnostic = new GameDataCatalogDiagnostic(
                        code,
                        GameDataDiagnosticSeverity.Error,
                        manifest == null ? string.Empty : manifest.CatalogSetId,
                        string.Empty,
                        string.Empty,
                        "$",
                        "catalog.load." + code.ToLowerInvariant().Replace('-', '_'),
                        message,
                        action,
                        true,
                        true,
                        -1,
                        -1);
                    var result = new GameDataCatalogLoadResult(
                        status,
                        null,
                        new[] { diagnostic },
                        startedAtUtc,
                        clock.UtcNow);
                    dispatch = ClaimTerminalUnderLock(
                        result,
                        expectedPending,
                        markCancelled,
                        markDisposed,
                        cancelOutstanding);
                }

                DispatchTerminal(dispatch);
            }

            private void Complete(
                GameDataCatalogLoadResult result,
                PendingRead expectedPending = null,
                bool markCancelled = false,
                bool markDisposed = false,
                bool cancelOutstanding = false)
            {
                TerminalDispatch dispatch;
                lock (sync)
                {
                    dispatch = ClaimTerminalUnderLock(
                        result,
                        expectedPending,
                        markCancelled,
                        markDisposed,
                        cancelOutstanding);
                }

                DispatchTerminal(dispatch);
            }

            private TerminalDispatch ClaimTerminalUnderLock(
                GameDataCatalogLoadResult result,
                PendingRead expectedPending,
                bool markCancelled,
                bool markDisposed,
                bool cancelOutstanding)
            {
                if (markDisposed)
                {
                    if (disposed) return null;
                    disposed = true;
                }

                if (isCompleted) return null;
                if (expectedPending != null && !ReferenceEquals(pending, expectedPending)) return null;
                isCompleted = true;
                if (markCancelled) isCancelled = true;
                var outstanding = DetachPendingUnderLock();
                var callback = completion;
                completion = null;
                if (activeHandlerCount == 0)
                {
                    ReleaseTransientStateUnderLock();
                }

                return new TerminalDispatch(result, outstanding, callback, cancelOutstanding);
            }

            private static void DispatchTerminal(TerminalDispatch dispatch)
            {
                if (dispatch == null) return;
                if (dispatch.Outstanding != null)
                {
                    if (dispatch.CancelOutstanding)
                    {
                        dispatch.Outstanding.CancelAndDispose();
                    }
                    else
                    {
                        dispatch.Outstanding.DisposeHandle();
                    }
                }

                if (dispatch.Callback == null) return;
                try
                {
                    dispatch.Callback(dispatch.Result);
                }
                catch (Exception)
                {
                    // The operation is already terminal. Consumer exceptions must not escape
                    // through a platform source callback or permit a second terminal result.
                }
            }

            private void ReleaseTransientStateUnderLock()
            {
                inputs.Clear();
                manifest = null;
                artifactIndex = 0;
            }

            private sealed class TerminalDispatch
            {
                public TerminalDispatch(
                    GameDataCatalogLoadResult result,
                    PendingRead outstanding,
                    Action<GameDataCatalogLoadResult> callback,
                    bool cancelOutstanding)
                {
                    Result = result;
                    Outstanding = outstanding;
                    Callback = callback;
                    CancelOutstanding = cancelOutstanding;
                }

                public GameDataCatalogLoadResult Result { get; }
                public PendingRead Outstanding { get; }
                public Action<GameDataCatalogLoadResult> Callback { get; }
                public bool CancelOutstanding { get; }
            }

            private static GameDataCatalogLoadStatus MapReadStatus(GameDataCatalogReadStatus status)
            {
                switch (status)
                {
                    case GameDataCatalogReadStatus.NotFound: return GameDataCatalogLoadStatus.MissingArtifact;
                    case GameDataCatalogReadStatus.Cancelled: return GameDataCatalogLoadStatus.Cancelled;
                    case GameDataCatalogReadStatus.TimedOut: return GameDataCatalogLoadStatus.TimedOut;
                    case GameDataCatalogReadStatus.Disposed: return GameDataCatalogLoadStatus.Disposed;
                    default: return GameDataCatalogLoadStatus.ReadFailed;
                }
            }

            private sealed class PendingRead
            {
                private readonly object sync = new object();
                private IGameDataCatalogReadOperation handle;
                private bool valid = true;
                private bool callbackArrived;

                public PendingRead(int token, long startTimestamp)
                {
                    Token = token;
                    StartTimestamp = startTimestamp;
                }

                public int Token { get; }
                public long StartTimestamp { get; }
                public bool CallbackArrived
                {
                    get
                    {
                        lock (sync) return callbackArrived;
                    }
                }

                public void Attach(IGameDataCatalogReadOperation operation)
                {
                    var dispose = false;
                    lock (sync)
                    {
                        if (callbackArrived || !valid)
                        {
                            dispose = true;
                        }
                        else
                        {
                            handle = operation;
                        }
                    }

                    if (dispose) SafeDispose(operation);
                }

                public bool TryComplete()
                {
                    lock (sync)
                    {
                        if (!valid || callbackArrived) return false;
                        callbackArrived = true;
                        return true;
                    }
                }

                public void Invalidate()
                {
                    lock (sync) valid = false;
                }

                public void CancelAndDispose()
                {
                    var operation = TakeHandle();
                    if (operation == null) return;
                    try
                    {
                        operation.Cancel();
                    }
                    catch (Exception)
                    {
                    }

                    SafeDispose(operation);
                }

                public void DisposeHandle()
                {
                    SafeDispose(TakeHandle());
                }

                private IGameDataCatalogReadOperation TakeHandle()
                {
                    lock (sync)
                    {
                        var operation = handle;
                        handle = null;
                        return operation;
                    }
                }

                private static void SafeDispose(IGameDataCatalogReadOperation operation)
                {
                    if (operation == null) return;
                    try
                    {
                        operation.Dispose();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }
    }
}
