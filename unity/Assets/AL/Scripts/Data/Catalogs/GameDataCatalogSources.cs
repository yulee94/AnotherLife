using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Immutable input for one bounded catalog byte read.
    /// Relative paths are accepted only in their canonical packaged form.
    /// </summary>
    public sealed class GameDataCatalogReadRequest
    {
        public GameDataCatalogReadRequest(string relativePath, int maximumBytes, TimeSpan timeout)
        {
            RelativePath = GameDataCatalogPathGuard.RequireCanonicalRelativePath(relativePath, nameof(relativePath));
            if (maximumBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumBytes), "The byte limit must be positive.");
            }

            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "The read timeout must be positive.");
            }

            MaximumBytes = maximumBytes;
            Timeout = timeout;
        }

        public string RelativePath { get; }
        public int MaximumBytes { get; }
        public TimeSpan Timeout { get; }
    }

    /// <summary>
    /// Immutable result for one catalog byte read. Public byte access always returns a copy.
    /// </summary>
    public sealed class GameDataCatalogReadResult
    {
        private readonly byte[] bytes;

        public GameDataCatalogReadResult(
            GameDataCatalogReadStatus status,
            string relativePath,
            byte[] bytes,
            string failureCode)
        {
            if (!Enum.IsDefined(typeof(GameDataCatalogReadStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            RelativePath = GameDataCatalogPathGuard.RequireCanonicalRelativePath(relativePath, nameof(relativePath));
            Status = status;

            if (status == GameDataCatalogReadStatus.Succeeded)
            {
                if (bytes == null)
                {
                    throw new ArgumentNullException(nameof(bytes), "A successful read requires byte content.");
                }

                this.bytes = (byte[])bytes.Clone();
                FailureCode = string.Empty;
            }
            else
            {
                this.bytes = null;
                FailureCode = GameDataCatalogFailureCodes.SafeOrDefault(failureCode, status);
            }
        }

        public GameDataCatalogReadStatus Status { get; }
        public string RelativePath { get; }
        public string FailureCode { get; }
        public bool HasBytes => bytes != null;
        public int ByteLength => bytes == null ? 0 : bytes.Length;

        public byte[] CopyBytes()
        {
            return bytes == null ? null : (byte[])bytes.Clone();
        }

        internal byte[] UnsafeBytes => bytes;
    }

    public interface IGameDataCatalogReadOperation : IDisposable
    {
        bool IsCompleted { get; }
        bool IsCancelled { get; }
        void Cancel();
    }

    public interface IGameDataCatalogSource
    {
        IGameDataCatalogReadOperation Read(
            GameDataCatalogReadRequest request,
            Action<GameDataCatalogReadResult> completed);
    }

    /// <summary>
    /// Delegate contract used by platform-specific adapters, including a later UnityWebRequest bridge.
    /// Implementations receive the same bounded request and report raw bytes through the callback.
    /// </summary>
    public delegate IGameDataCatalogReadOperation GameDataCatalogPlatformReadDelegate(
        GameDataCatalogReadRequest request,
        Action<GameDataCatalogReadStatus, byte[], string> completed);

    /// <summary>
    /// Converts callback-based platform byte transport into the common source contract without
    /// depending on UnityEngine or UnityWebRequest in the catalog foundation assembly.
    /// </summary>
    public sealed class DelegateGameDataCatalogSource : IGameDataCatalogSource
    {
        private readonly GameDataCatalogPlatformReadDelegate platformRead;

        public DelegateGameDataCatalogSource(GameDataCatalogPlatformReadDelegate platformRead)
        {
            this.platformRead = platformRead ?? throw new ArgumentNullException(nameof(platformRead));
        }

        public IGameDataCatalogReadOperation Read(
            GameDataCatalogReadRequest request,
            Action<GameDataCatalogReadResult> completed)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            var operation = new DelegatedReadOperation(request, completed);
            try
            {
                var platformOperation = platformRead(request, operation.CompleteFromPlatform);
                operation.Attach(platformOperation);
            }
            catch (Exception)
            {
                operation.CompleteFromPlatform(
                    GameDataCatalogReadStatus.ReadFailed,
                    null,
                    GameDataCatalogFailureCodes.PlatformException);
            }

            return operation;
        }
    }

    /// <summary>
    /// Reads packaged bytes from an explicit directory. It performs lexical full-path containment,
    /// checks file length before allocating, and never returns the local absolute path. Reads run on
    /// the thread pool so the loader can enforce its injected timeout without blocking its owner.
    /// Cancellation claims the public terminal state immediately and closes an active stream to
    /// interrupt cooperative file systems. A synchronous operating-system read that ignores handle
    /// closure can keep only its bounded worker alive until that operating-system call returns.
    /// </summary>
    public sealed class DirectFileGameDataCatalogSource : IGameDataCatalogSource
    {
        private readonly string rootDirectory;
        private readonly string rootPrefix;
        private readonly StringComparison pathComparison;

        public DirectFileGameDataCatalogSource(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("An explicit root directory is required.", nameof(rootDirectory));
            }

            try
            {
                this.rootDirectory = Path.GetFullPath(rootDirectory);
                rootPrefix = WithTrailingSeparator(this.rootDirectory);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException ||
                exception is System.Security.SecurityException)
            {
                throw new ArgumentException("The catalog root directory is invalid.", nameof(rootDirectory));
            }

            pathComparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        public IGameDataCatalogReadOperation Read(
            GameDataCatalogReadRequest request,
            Action<GameDataCatalogReadResult> completed)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            var operation = new DirectFileReadOperation(this, request, completed);
            operation.Start();
            return operation;
        }

        private GameDataCatalogReadResult ReadCore(
            GameDataCatalogReadRequest request,
            DirectFileReadOperation operation)
        {
            if (!operation.CanContinue)
            {
                return null;
            }

            string fullPath;
            try
            {
                var platformRelativePath = request.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                fullPath = Path.GetFullPath(Path.Combine(rootDirectory, platformRelativePath));
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException ||
                exception is System.Security.SecurityException)
            {
                return Failure(request, GameDataCatalogReadStatus.ReadFailed, GameDataCatalogFailureCodes.InvalidPath);
            }

            if (!fullPath.StartsWith(rootPrefix, pathComparison))
            {
                return Failure(request, GameDataCatalogReadStatus.ReadFailed, GameDataCatalogFailureCodes.PathOutsideRoot);
            }

            FileStream stream = null;
            try
            {
                stream = new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    FileOptions.SequentialScan);
                if (!operation.TryAttachStream(stream))
                {
                    try
                    {
                        stream.Dispose();
                    }
                    catch (Exception)
                    {
                    }

                    return null;
                }

                using (stream)
                {
                    var length = stream.Length;
                    if (length < 0 || length > request.MaximumBytes || length > int.MaxValue)
                    {
                        return Failure(request, GameDataCatalogReadStatus.ReadFailed, GameDataCatalogFailureCodes.SizeLimit);
                    }

                    var content = new byte[(int)length];
                    var offset = 0;
                    while (offset < content.Length)
                    {
                        if (!operation.CanContinue)
                        {
                            return null;
                        }

                        var count = stream.Read(content, offset, content.Length - offset);
                        if (count <= 0)
                        {
                            return Failure(request, GameDataCatalogReadStatus.ReadFailed, GameDataCatalogFailureCodes.SourceChanged);
                        }

                        offset += count;
                    }

                    if (!operation.CanContinue)
                    {
                        return null;
                    }

                    if (stream.ReadByte() >= 0)
                    {
                        return Failure(request, GameDataCatalogReadStatus.ReadFailed, GameDataCatalogFailureCodes.SourceChanged);
                    }

                    return new GameDataCatalogReadResult(
                        GameDataCatalogReadStatus.Succeeded,
                        request.RelativePath,
                        content,
                        string.Empty);
                }
            }
            catch (FileNotFoundException)
            {
                return Failure(request, GameDataCatalogReadStatus.NotFound, GameDataCatalogFailureCodes.NotFound);
            }
            catch (DirectoryNotFoundException)
            {
                return Failure(request, GameDataCatalogReadStatus.NotFound, GameDataCatalogFailureCodes.NotFound);
            }
            catch (UnauthorizedAccessException)
            {
                return Failure(request, GameDataCatalogReadStatus.ReadFailed, GameDataCatalogFailureCodes.AccessDenied);
            }
            catch (System.Security.SecurityException)
            {
                return Failure(request, GameDataCatalogReadStatus.ReadFailed, GameDataCatalogFailureCodes.AccessDenied);
            }
            catch (IOException)
            {
                if (!operation.CanContinue)
                {
                    return null;
                }

                return Failure(request, GameDataCatalogReadStatus.ReadFailed, GameDataCatalogFailureCodes.IoFailure);
            }
            catch (ObjectDisposedException)
            {
                if (!operation.CanContinue)
                {
                    return null;
                }

                return Failure(request, GameDataCatalogReadStatus.ReadFailed, GameDataCatalogFailureCodes.IoFailure);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                return Failure(request, GameDataCatalogReadStatus.ReadFailed, GameDataCatalogFailureCodes.InvalidPath);
            }
            finally
            {
                operation.DetachStream(stream);
            }
        }

        private static string WithTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static GameDataCatalogReadResult Failure(
            GameDataCatalogReadRequest request,
            GameDataCatalogReadStatus status,
            string failureCode)
        {
            return new GameDataCatalogReadResult(status, request.RelativePath, null, failureCode);
        }

        private sealed class DirectFileReadOperation : IGameDataCatalogReadOperation
        {
            private readonly object sync = new object();
            private readonly DirectFileGameDataCatalogSource source;
            private readonly GameDataCatalogReadRequest request;
            private readonly Action<GameDataCatalogReadResult> completed;

            private FileStream activeStream;
            private bool isCompleted;
            private bool isCancelled;
            private bool isDisposed;

            public DirectFileReadOperation(
                DirectFileGameDataCatalogSource source,
                GameDataCatalogReadRequest request,
                Action<GameDataCatalogReadResult> completed)
            {
                this.source = source;
                this.request = request;
                this.completed = completed;
            }

            public bool IsCompleted
            {
                get
                {
                    lock (sync)
                    {
                        return isCompleted;
                    }
                }
            }

            public bool IsCancelled
            {
                get
                {
                    lock (sync)
                    {
                        return isCancelled;
                    }
                }
            }

            internal bool CanContinue
            {
                get
                {
                    lock (sync)
                    {
                        return !isCompleted;
                    }
                }
            }

            public void Start()
            {
                try
                {
                    if (!ThreadPool.QueueUserWorkItem(_ => Execute()))
                    {
                        Complete(Failure(
                            request,
                            GameDataCatalogReadStatus.ReadFailed,
                            GameDataCatalogFailureCodes.WorkerUnavailable));
                    }
                }
                catch (Exception)
                {
                    Complete(Failure(
                        request,
                        GameDataCatalogReadStatus.ReadFailed,
                        GameDataCatalogFailureCodes.WorkerUnavailable));
                }
            }

            public void Cancel()
            {
                Action<GameDataCatalogReadResult> callback;
                FileStream stream;
                lock (sync)
                {
                    if (isCompleted)
                    {
                        return;
                    }

                    isCompleted = true;
                    isCancelled = true;
                    callback = completed;
                    stream = activeStream;
                    activeStream = null;
                }

                SafeDispose(stream);
                callback(Failure(
                    request,
                    GameDataCatalogReadStatus.Cancelled,
                    GameDataCatalogFailureCodes.Cancelled));
            }

            public void Dispose()
            {
                Action<GameDataCatalogReadResult> callback = null;
                FileStream stream;
                lock (sync)
                {
                    if (isDisposed)
                    {
                        return;
                    }

                    isDisposed = true;
                    stream = activeStream;
                    activeStream = null;
                    if (!isCompleted)
                    {
                        isCompleted = true;
                        callback = completed;
                    }
                }

                SafeDispose(stream);
                if (callback != null)
                {
                    callback(Failure(
                        request,
                        GameDataCatalogReadStatus.Disposed,
                        GameDataCatalogFailureCodes.Disposed));
                }
            }

            internal bool TryAttachStream(FileStream stream)
            {
                lock (sync)
                {
                    if (isCompleted)
                    {
                        return false;
                    }

                    activeStream = stream;
                    return true;
                }
            }

            internal void DetachStream(FileStream stream)
            {
                if (stream == null)
                {
                    return;
                }

                lock (sync)
                {
                    if (ReferenceEquals(activeStream, stream))
                    {
                        activeStream = null;
                    }
                }
            }

            private void Execute()
            {
                GameDataCatalogReadResult result;
                try
                {
                    result = source.ReadCore(request, this);
                }
                catch (Exception)
                {
                    result = Failure(
                        request,
                        GameDataCatalogReadStatus.ReadFailed,
                        GameDataCatalogFailureCodes.IoFailure);
                }

                if (result != null)
                {
                    try
                    {
                        Complete(result);
                    }
                    catch (Exception)
                    {
                        // The terminal state was claimed before the callback. There is no caller
                        // stack on which an asynchronous callback exception can be reported safely.
                    }
                }
            }

            private void Complete(GameDataCatalogReadResult result)
            {
                Action<GameDataCatalogReadResult> callback;
                lock (sync)
                {
                    if (isCompleted)
                    {
                        return;
                    }

                    isCompleted = true;
                    if (result.Status == GameDataCatalogReadStatus.Cancelled)
                    {
                        isCancelled = true;
                    }

                    callback = completed;
                }

                callback(result);
            }

            private static void SafeDispose(IDisposable disposable)
            {
                if (disposable == null)
                {
                    return;
                }

                try
                {
                    disposable.Dispose();
                }
                catch (Exception)
                {
                }
            }
        }
    }

    /// <summary>
    /// Deterministic test/source seam. Stored and returned byte buffers are always copied.
    /// Unconfigured paths are typed as not found.
    /// </summary>
    public sealed class InMemoryGameDataCatalogSource : IGameDataCatalogSource
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, Entry> entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);

        public InMemoryGameDataCatalogSource Add(string relativePath, byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            return Configure(
                relativePath,
                GameDataCatalogReadStatus.Succeeded,
                bytes,
                string.Empty);
        }

        public InMemoryGameDataCatalogSource AddNotFound(
            string relativePath,
            string failureCode = GameDataCatalogFailureCodes.NotFound)
        {
            return Configure(relativePath, GameDataCatalogReadStatus.NotFound, null, failureCode);
        }

        public InMemoryGameDataCatalogSource AddReadFailure(
            string relativePath,
            string failureCode = GameDataCatalogFailureCodes.ReadFailed)
        {
            return Configure(relativePath, GameDataCatalogReadStatus.ReadFailed, null, failureCode);
        }

        public IGameDataCatalogReadOperation Read(
            GameDataCatalogReadRequest request,
            Action<GameDataCatalogReadResult> completed)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            Entry entry;
            lock (sync)
            {
                if (!entries.TryGetValue(request.RelativePath, out entry))
                {
                    entry = new Entry(
                        GameDataCatalogReadStatus.NotFound,
                        null,
                        GameDataCatalogFailureCodes.NotFound);
                }
            }

            GameDataCatalogReadResult result;
            if (entry.Status == GameDataCatalogReadStatus.Succeeded &&
                entry.ByteLength > request.MaximumBytes)
            {
                result = new GameDataCatalogReadResult(
                    GameDataCatalogReadStatus.ReadFailed,
                    request.RelativePath,
                    null,
                    GameDataCatalogFailureCodes.SizeLimit);
            }
            else
            {
                result = new GameDataCatalogReadResult(
                    entry.Status,
                    request.RelativePath,
                    entry.CopyBytes(),
                    entry.FailureCode);
            }

            completed(result);
            return new CompletedGameDataCatalogReadOperation(result.Status == GameDataCatalogReadStatus.Cancelled);
        }

        private InMemoryGameDataCatalogSource Configure(
            string relativePath,
            GameDataCatalogReadStatus status,
            byte[] bytes,
            string failureCode)
        {
            var canonicalPath = GameDataCatalogPathGuard.RequireCanonicalRelativePath(
                relativePath,
                nameof(relativePath));
            var entry = new Entry(status, bytes, failureCode);
            lock (sync)
            {
                entries[canonicalPath] = entry;
            }

            return this;
        }

        private sealed class Entry
        {
            private readonly byte[] bytes;

            public Entry(GameDataCatalogReadStatus status, byte[] bytes, string failureCode)
            {
                Status = status;
                this.bytes = bytes == null ? null : (byte[])bytes.Clone();
                FailureCode = failureCode ?? string.Empty;
            }

            public GameDataCatalogReadStatus Status { get; }
            public string FailureCode { get; }
            public int ByteLength => bytes == null ? 0 : bytes.Length;

            public byte[] CopyBytes()
            {
                return bytes == null ? null : (byte[])bytes.Clone();
            }
        }
    }

    internal sealed class CompletedGameDataCatalogReadOperation : IGameDataCatalogReadOperation
    {
        private readonly bool isCancelled;

        public CompletedGameDataCatalogReadOperation(bool isCancelled)
        {
            this.isCancelled = isCancelled;
        }

        public bool IsCompleted => true;
        public bool IsCancelled => isCancelled;

        public void Cancel()
        {
        }

        public void Dispose()
        {
        }
    }

    internal sealed class DelegatedReadOperation : IGameDataCatalogReadOperation
    {
        private readonly object sync = new object();
        private readonly GameDataCatalogReadRequest request;
        private readonly Action<GameDataCatalogReadResult> completed;

        private IGameDataCatalogReadOperation platformOperation;
        private bool isAttached;
        private bool isCompleted;
        private bool isCancelled;
        private bool isDisposed;

        public DelegatedReadOperation(
            GameDataCatalogReadRequest request,
            Action<GameDataCatalogReadResult> completed)
        {
            this.request = request;
            this.completed = completed;
        }

        public bool IsCompleted
        {
            get
            {
                lock (sync)
                {
                    return isCompleted;
                }
            }
        }

        public bool IsCancelled
        {
            get
            {
                lock (sync)
                {
                    return isCancelled;
                }
            }
        }

        public void Attach(IGameDataCatalogReadOperation operation)
        {
            bool cancel;
            bool dispose;
            lock (sync)
            {
                if (isAttached)
                {
                    throw new InvalidOperationException("A platform read operation is already attached.");
                }

                isAttached = true;
                platformOperation = operation;
                cancel = isCancelled;
                dispose = isDisposed;
            }

            if (operation == null)
            {
                CompleteFromPlatform(
                    GameDataCatalogReadStatus.ReadFailed,
                    null,
                    GameDataCatalogFailureCodes.MissingOperation);
                return;
            }

            if (cancel)
            {
                SafeCancel(operation);
            }

            if (dispose)
            {
                SafeDispose(operation);
            }
        }

        public void CompleteFromPlatform(
            GameDataCatalogReadStatus status,
            byte[] bytes,
            string failureCode)
        {
            if (!Enum.IsDefined(typeof(GameDataCatalogReadStatus), status))
            {
                status = GameDataCatalogReadStatus.ReadFailed;
                bytes = null;
                failureCode = GameDataCatalogFailureCodes.InvalidStatus;
            }
            else if (status == GameDataCatalogReadStatus.Succeeded && bytes == null)
            {
                status = GameDataCatalogReadStatus.ReadFailed;
                failureCode = GameDataCatalogFailureCodes.MissingBytes;
            }
            else if (status == GameDataCatalogReadStatus.Succeeded && bytes.Length > request.MaximumBytes)
            {
                status = GameDataCatalogReadStatus.ReadFailed;
                bytes = null;
                failureCode = GameDataCatalogFailureCodes.SizeLimit;
            }

            if (status != GameDataCatalogReadStatus.Succeeded)
            {
                bytes = null;
            }

            Action<GameDataCatalogReadResult> callback;
            lock (sync)
            {
                if (isCompleted)
                {
                    return;
                }

                isCompleted = true;
                if (status == GameDataCatalogReadStatus.Cancelled)
                {
                    isCancelled = true;
                }

                callback = completed;
            }

            callback(new GameDataCatalogReadResult(
                status,
                request.RelativePath,
                bytes,
                failureCode));
        }

        public void Cancel()
        {
            Action<GameDataCatalogReadResult> callback;
            IGameDataCatalogReadOperation operation;
            lock (sync)
            {
                if (isCompleted)
                {
                    return;
                }

                isCancelled = true;
                isCompleted = true;
                operation = platformOperation;
                callback = completed;
            }

            SafeCancel(operation);
            callback(new GameDataCatalogReadResult(
                GameDataCatalogReadStatus.Cancelled,
                request.RelativePath,
                null,
                GameDataCatalogFailureCodes.Cancelled));
        }

        public void Dispose()
        {
            IGameDataCatalogReadOperation operation;
            Action<GameDataCatalogReadResult> callback = null;
            lock (sync)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                operation = platformOperation;
                if (!isCompleted)
                {
                    isCompleted = true;
                    callback = completed;
                }
            }

            SafeDispose(operation);
            if (callback != null)
            {
                callback(new GameDataCatalogReadResult(
                    GameDataCatalogReadStatus.Disposed,
                    request.RelativePath,
                    null,
                    GameDataCatalogFailureCodes.Disposed));
            }
        }

        private static void SafeCancel(IGameDataCatalogReadOperation operation)
        {
            if (operation == null)
            {
                return;
            }

            try
            {
                operation.Cancel();
            }
            catch (Exception)
            {
            }
        }

        private static void SafeDispose(IGameDataCatalogReadOperation operation)
        {
            if (operation == null)
            {
                return;
            }

            try
            {
                operation.Dispose();
            }
            catch (Exception)
            {
            }
        }
    }

    internal static class GameDataCatalogPathGuard
    {
        public static string RequireCanonicalRelativePath(string value, string parameterName)
        {
            if (!IsCanonicalRelativePath(value))
            {
                throw new ArgumentException("A canonical packaged relative path is required.", parameterName);
            }

            return value;
        }

        private static bool IsCanonicalRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > GameDataCatalogContract.MaximumStringLength ||
                value[0] == '/' ||
                value[value.Length - 1] == '/' ||
                value.IndexOf('\\') >= 0 ||
                value.IndexOf(':') >= 0 ||
                value.IndexOf('%') >= 0 ||
                value.IndexOf('?') >= 0 ||
                value.IndexOf('#') >= 0 ||
                Path.IsPathRooted(value))
            {
                return false;
            }

            var segments = value.Split('/');
            for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                var segment = segments[segmentIndex];
                if (segment.Length == 0 ||
                    segment == "." ||
                    segment == ".." ||
                    !string.Equals(segment, segment.Trim(), StringComparison.Ordinal))
                {
                    return false;
                }

                for (var characterIndex = 0; characterIndex < segment.Length; characterIndex++)
                {
                    var character = segment[characterIndex];
                    var allowed =
                        character >= 'a' && character <= 'z' ||
                        character >= 'A' && character <= 'Z' ||
                        character >= '0' && character <= '9' ||
                        character == '_' ||
                        character == '-' ||
                        character == '.';
                    if (!allowed)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    internal static class GameDataCatalogFailureCodes
    {
        public const string NotFound = "not_found";
        public const string ReadFailed = "read_failed";
        public const string AccessDenied = "access_denied";
        public const string IoFailure = "io_failure";
        public const string InvalidPath = "invalid_path";
        public const string PathOutsideRoot = "path_outside_root";
        public const string SizeLimit = "size_limit";
        public const string SourceChanged = "source_changed";
        public const string PlatformException = "platform_exception";
        public const string MissingOperation = "missing_operation";
        public const string MissingCallback = "missing_callback";
        public const string WorkerUnavailable = "worker_unavailable";
        public const string MissingBytes = "missing_bytes";
        public const string InvalidStatus = "invalid_status";
        public const string Cancelled = "cancelled";
        public const string TimedOut = "timed_out";
        public const string Disposed = "disposed";

        public static string SafeOrDefault(string value, GameDataCatalogReadStatus status)
        {
            if (IsSafe(value))
            {
                return value;
            }

            switch (status)
            {
                case GameDataCatalogReadStatus.NotFound:
                    return NotFound;
                case GameDataCatalogReadStatus.Cancelled:
                    return Cancelled;
                case GameDataCatalogReadStatus.TimedOut:
                    return TimedOut;
                case GameDataCatalogReadStatus.Disposed:
                    return Disposed;
                default:
                    return ReadFailed;
            }
        }

        private static bool IsSafe(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var allowed =
                    character >= 'a' && character <= 'z' ||
                    character >= '0' && character <= '9' ||
                    character == '_' ||
                    character == '-' ||
                    character == '.';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
