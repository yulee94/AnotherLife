using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace AL.Platform.Android
{
    public interface IUnityBridgeReadyPlatformAdapter : IDisposable
    {
        UnityBridgePlatformCallbackStatus TryReportReady(
            string encodedReady);
    }

    public sealed class AndroidUnityBridgeReadyPlatformAdapter :
        IUnityBridgeReadyPlatformAdapter
    {
        public const string CallbackClassName =
            "com.example.anotherlife.ui.unity.UnityBridgeCallbacks";
        public const string CallbackMethodName = "reportReady";
        public const string CallbackMethodDescriptor =
            "(Ljava/lang/String;)V";

        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(false, true);

        private readonly object synchronization = new object();
        private readonly int ownerThreadId =
            Thread.CurrentThread.ManagedThreadId;

        private bool dispatching;
        private bool disposeRequested;
        private bool disposed;

        public static bool IsAndroidPlayerBuild
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public UnityBridgePlatformCallbackStatus TryReportReady(
            string encodedReady)
        {
            lock (synchronization)
            {
                if (disposed ||
                    disposeRequested ||
                    Thread.CurrentThread.ManagedThreadId !=
                    ownerThreadId ||
                    dispatching)
                {
                    return UnityBridgePlatformCallbackStatus.Unavailable;
                }

                dispatching = true;
            }

            var status =
                UnityBridgePlatformCallbackStatus.InvocationFailed;
            try
            {
                if (!IsBoundedUtf8(encodedReady))
                {
                    return status;
                }

                status = InvokePlatformCallback(encodedReady);
                return status;
            }
            catch (Exception)
            {
                return UnityBridgePlatformCallbackStatus.InvocationFailed;
            }
            finally
            {
                lock (synchronization)
                {
                    dispatching = false;
                    if (disposeRequested)
                    {
                        disposed = true;
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (synchronization)
            {
                if (disposed || disposeRequested)
                {
                    return;
                }

                disposeRequested = true;
                if (!dispatching)
                {
                    disposed = true;
                }
            }
        }

        private static bool IsBoundedUtf8(string encodedReady)
        {
            if (string.IsNullOrEmpty(encodedReady) ||
                encodedReady.Length >
                UnityBridgeContract.MaximumMessageBytes)
            {
                return false;
            }

            try
            {
                return StrictUtf8.GetByteCount(encodedReady) <=
                       UnityBridgeContract.MaximumMessageBytes;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }

        private static UnityBridgePlatformCallbackStatus
            InvokePlatformCallback(string encodedReady)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.Android)
            {
                return UnityBridgePlatformCallbackStatus.Unavailable;
            }

            using (var callbacks =
                   new AndroidJavaClass(CallbackClassName))
            {
                callbacks.CallStatic(
                    CallbackMethodName,
                    encodedReady);
            }

            return UnityBridgePlatformCallbackStatus.CallbackInvoked;
#else
            return UnityBridgePlatformCallbackStatus.Unavailable;
#endif
        }
    }

    public enum UnityBridgeReadyDispatchStatus
    {
        CallbackInvoked = 0,
        RejectedRequest = 1,
        EncodingRejected = 2,
        WrongThread = 3,
        Busy = 4,
        Duplicate = 5,
        RetentionExhausted = 6,
        PlatformUnavailable = 7,
        PlatformInvocationFailed = 8,
        Disposed = 9
    }

    public sealed class UnityBridgeReadyDispatchResult
    {
        private UnityBridgeReadyDispatchResult(
            UnityBridgeReadyDispatchStatus status,
            bool canRetry,
            UnityBridgeProtocolError error)
        {
            Status = status;
            CanRetry = canRetry;
            Error = error;
        }

        public UnityBridgeReadyDispatchStatus Status { get; }
        public bool CanRetry { get; }
        public UnityBridgeProtocolError Error { get; }
        public bool CallbackInvoked =>
            Status == UnityBridgeReadyDispatchStatus.CallbackInvoked;

        internal static UnityBridgeReadyDispatchResult Completed()
        {
            return new UnityBridgeReadyDispatchResult(
                UnityBridgeReadyDispatchStatus.CallbackInvoked,
                false,
                null);
        }

        internal static UnityBridgeReadyDispatchResult Failed(
            UnityBridgeReadyDispatchStatus status,
            bool canRetry,
            UnityBridgeProtocolErrorCode code,
            string field = null)
        {
            return new UnityBridgeReadyDispatchResult(
                status,
                canRetry,
                new UnityBridgeProtocolError(code, field));
        }

        internal static UnityBridgeReadyDispatchResult Failed(
            UnityBridgeReadyDispatchStatus status,
            bool canRetry,
            UnityBridgeProtocolError error)
        {
            return new UnityBridgeReadyDispatchResult(
                status,
                canRetry,
                error ?? throw new ArgumentNullException(nameof(error)));
        }
    }

    /// <summary>
    /// Sends a route-ready acknowledgement only when route code explicitly
    /// supplies the validated request whose presentation is ready. Receipt of
    /// SetRouteContext never invokes this sender automatically.
    /// </summary>
    public sealed class UnityBridgeReadySender : IDisposable
    {
        public const int MaximumRetainedRequestIdentities = 256;

        private readonly object synchronization = new object();
        private readonly HashSet<string> terminalRequestIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, UnityRouteRequest>
            retryableRequests =
                new Dictionary<string, UnityRouteRequest>(
                    StringComparer.Ordinal);
        private readonly int ownerThreadId =
            Thread.CurrentThread.ManagedThreadId;

        private IUnityBridgeReadyPlatformAdapter platformAdapter;
        private bool dispatching;
        private bool disposeRequested;
        private bool disposed;

        public UnityBridgeReadySender(
            IUnityBridgeReadyPlatformAdapter platformAdapter)
        {
            this.platformAdapter = platformAdapter ??
                throw new ArgumentNullException(nameof(platformAdapter));
        }

        public int OwnerThreadId => ownerThreadId;

        public UnityBridgeReadyDispatchResult TryDispatch(
            UnityRouteRequest request)
        {
            lock (synchronization)
            {
                var stateFailure = GetStateFailureLocked();
                if (stateFailure != null)
                {
                    return stateFailure;
                }
            }

            var requestValidation =
                UnityBridgeContract.ValidateRequest(request);
            if (!requestValidation.IsAccepted)
            {
                return UnityBridgeReadyDispatchResult.Failed(
                    UnityBridgeReadyDispatchStatus.RejectedRequest,
                    false,
                    requestValidation.Error);
            }

            var validRequest = requestValidation.Request;
            var readyValidation =
                UnityBridgeContract.ValidateReadyForRequest(
                    new UnityRouteReady(
                        UnityBridgeContract.ContractVersion,
                        validRequest.RequestId,
                        validRequest.RouteId),
                    validRequest);
            if (!readyValidation.IsAccepted)
            {
                return UnityBridgeReadyDispatchResult.Failed(
                    UnityBridgeReadyDispatchStatus.RejectedRequest,
                    false,
                    readyValidation.Error);
            }

            var encoding = UnityBridgeContract.EncodeReady(
                readyValidation.Ready);
            if (!encoding.IsEncoded)
            {
                return UnityBridgeReadyDispatchResult.Failed(
                    UnityBridgeReadyDispatchStatus.EncodingRejected,
                    false,
                    encoding.Error);
            }

            IUnityBridgeReadyPlatformAdapter adapter;
            lock (synchronization)
            {
                var stateFailure = GetStateFailureLocked();
                if (stateFailure != null)
                {
                    return stateFailure;
                }

                if (terminalRequestIds.Contains(validRequest.RequestId))
                {
                    return Failure(
                        UnityBridgeReadyDispatchStatus.Duplicate,
                        false,
                        UnityBridgeProtocolErrorCode.DuplicateReady);
                }

                UnityRouteRequest retainedRequest;
                var hasRetryableIdentity =
                    retryableRequests.TryGetValue(
                        validRequest.RequestId,
                        out retainedRequest);
                if (hasRetryableIdentity &&
                    !HasSameEnvelope(retainedRequest, validRequest))
                {
                    return Failure(
                        UnityBridgeReadyDispatchStatus.RejectedRequest,
                        false,
                        UnityBridgeProtocolErrorCode.RequestMismatch);
                }

                if (!hasRetryableIdentity &&
                    terminalRequestIds.Count +
                    retryableRequests.Count >=
                    MaximumRetainedRequestIdentities)
                {
                    return Failure(
                        UnityBridgeReadyDispatchStatus.RetentionExhausted,
                        false,
                        UnityBridgeProtocolErrorCode.SessionClosed);
                }

                dispatching = true;
                adapter = platformAdapter;
            }

            var platformStatus =
                UnityBridgePlatformCallbackStatus.InvocationFailed;
            try
            {
                platformStatus = adapter.TryReportReady(
                    encoding.EncodedJson);
                if (!Enum.IsDefined(
                        typeof(UnityBridgePlatformCallbackStatus),
                        platformStatus))
                {
                    platformStatus =
                        UnityBridgePlatformCallbackStatus.InvocationFailed;
                }
            }
            catch (Exception)
            {
                platformStatus =
                    UnityBridgePlatformCallbackStatus.InvocationFailed;
            }

            IUnityBridgeReadyPlatformAdapter adapterToDispose = null;
            bool closedAfterInvocation;
            lock (synchronization)
            {
                if (platformStatus !=
                    UnityBridgePlatformCallbackStatus.Unavailable)
                {
                    retryableRequests.Remove(validRequest.RequestId);
                    terminalRequestIds.Add(validRequest.RequestId);
                }
                else if (!disposeRequested)
                {
                    retryableRequests[validRequest.RequestId] =
                        validRequest;
                }

                dispatching = false;
                if (disposeRequested)
                {
                    adapterToDispose = FinalizeDisposalLocked();
                }

                closedAfterInvocation = disposed || disposeRequested;
            }

            DisposeSafely(adapterToDispose);

            switch (platformStatus)
            {
                case UnityBridgePlatformCallbackStatus.CallbackInvoked:
                    return UnityBridgeReadyDispatchResult.Completed();
                case UnityBridgePlatformCallbackStatus.Unavailable:
                    return Failure(
                        UnityBridgeReadyDispatchStatus.PlatformUnavailable,
                        !closedAfterInvocation,
                        UnityBridgeProtocolErrorCode.SendUnavailable);
                default:
                    return Failure(
                        UnityBridgeReadyDispatchStatus
                            .PlatformInvocationFailed,
                        false,
                        UnityBridgeProtocolErrorCode.SendUnavailable);
            }
        }

        public void Dispose()
        {
            IUnityBridgeReadyPlatformAdapter adapterToDispose = null;
            lock (synchronization)
            {
                if (disposed || disposeRequested)
                {
                    return;
                }

                disposeRequested = true;
                if (!dispatching)
                {
                    adapterToDispose = FinalizeDisposalLocked();
                }
            }

            DisposeSafely(adapterToDispose);
        }

        private UnityBridgeReadyDispatchResult GetStateFailureLocked()
        {
            if (disposed || disposeRequested)
            {
                return Failure(
                    UnityBridgeReadyDispatchStatus.Disposed,
                    false,
                    UnityBridgeProtocolErrorCode.SessionClosed);
            }

            if (Thread.CurrentThread.ManagedThreadId != ownerThreadId)
            {
                return Failure(
                    UnityBridgeReadyDispatchStatus.WrongThread,
                    true,
                    UnityBridgeProtocolErrorCode.SendUnavailable);
            }

            if (dispatching)
            {
                return Failure(
                    UnityBridgeReadyDispatchStatus.Busy,
                    true,
                    UnityBridgeProtocolErrorCode.SendUnavailable);
            }

            return null;
        }

        private IUnityBridgeReadyPlatformAdapter FinalizeDisposalLocked()
        {
            disposed = true;
            disposeRequested = true;
            dispatching = false;
            terminalRequestIds.Clear();
            retryableRequests.Clear();
            var adapter = platformAdapter;
            platformAdapter = null;
            return adapter;
        }

        private static bool HasSameEnvelope(
            UnityRouteRequest left,
            UnityRouteRequest right)
        {
            if (left.ContractVersion != right.ContractVersion ||
                left.Intent != right.Intent ||
                !string.Equals(
                    left.RequestId,
                    right.RequestId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    left.RouteId,
                    right.RouteId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var leftCapabilities = left.RequestedCapabilities;
            var rightCapabilities = right.RequestedCapabilities;
            if (leftCapabilities.Count != rightCapabilities.Count)
            {
                return false;
            }

            for (var index = 0;
                 index < leftCapabilities.Count;
                 index++)
            {
                if (!string.Equals(
                        leftCapabilities[index],
                        rightCapabilities[index],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static UnityBridgeReadyDispatchResult Failure(
            UnityBridgeReadyDispatchStatus status,
            bool canRetry,
            UnityBridgeProtocolErrorCode code)
        {
            return UnityBridgeReadyDispatchResult.Failed(
                status,
                canRetry,
                code);
        }

        private static void DisposeSafely(
            IUnityBridgeReadyPlatformAdapter adapter)
        {
            if (adapter == null)
            {
                return;
            }

            try
            {
                adapter.Dispose();
            }
            catch (Exception)
            {
                // Disposal remains idempotent and cannot cross boundaries.
            }
        }
    }
}
