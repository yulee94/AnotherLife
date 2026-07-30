using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace AL.Platform.Android
{
    public enum UnityBridgePlatformCallbackStatus
    {
        CallbackInvoked = 0,
        Unavailable = 1,
        InvocationFailed = 2
    }

    public interface IUnityBridgeOutcomePlatformAdapter : IDisposable
    {
        UnityBridgePlatformCallbackStatus TryReportOutcome(
            string encodedOutcome);
    }

    public sealed class AndroidUnityBridgeOutcomePlatformAdapter :
        IUnityBridgeOutcomePlatformAdapter
    {
        public const string CallbackClassName =
            "com.example.anotherlife.ui.unity.UnityBridgeCallbacks";
        public const string CallbackMethodName = "reportOutcome";
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

        public UnityBridgePlatformCallbackStatus TryReportOutcome(
            string encodedOutcome)
        {
            lock (synchronization)
            {
                if (disposed ||
                    disposeRequested ||
                    Thread.CurrentThread.ManagedThreadId !=
                    ownerThreadId)
                {
                    return UnityBridgePlatformCallbackStatus.Unavailable;
                }

                if (dispatching)
                {
                    return UnityBridgePlatformCallbackStatus.Unavailable;
                }

                dispatching = true;
            }

            var status =
                UnityBridgePlatformCallbackStatus.InvocationFailed;
            try
            {
                if (!IsBoundedUtf8(encodedOutcome))
                {
                    return status;
                }

                status = InvokePlatformCallback(encodedOutcome);
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

        private static bool IsBoundedUtf8(string encodedOutcome)
        {
            if (string.IsNullOrEmpty(encodedOutcome) ||
                encodedOutcome.Length >
                UnityBridgeContract.MaximumMessageBytes)
            {
                return false;
            }

            try
            {
                return StrictUtf8.GetByteCount(encodedOutcome) <=
                       UnityBridgeContract.MaximumMessageBytes;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }

        private static UnityBridgePlatformCallbackStatus
            InvokePlatformCallback(string encodedOutcome)
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
                    encodedOutcome);
            }

            return UnityBridgePlatformCallbackStatus.CallbackInvoked;
#else
            return UnityBridgePlatformCallbackStatus.Unavailable;
#endif
        }
    }

    public enum UnityBridgeOutcomeDispatchStatus
    {
        CallbackInvoked = 0,
        RejectedReport = 1,
        EncodingRejected = 2,
        WrongThread = 3,
        Busy = 4,
        Duplicate = 5,
        RetentionExhausted = 6,
        PlatformUnavailable = 7,
        PlatformInvocationFailed = 8,
        Disposed = 9
    }

    public sealed class UnityBridgeOutcomeDispatchResult
    {
        private UnityBridgeOutcomeDispatchResult(
            UnityBridgeOutcomeDispatchStatus status,
            string requestId,
            bool canRetry,
            UnityBridgeProtocolError error)
        {
            Status = status;
            RequestId = requestId;
            CanRetry = canRetry;
            Error = error;
        }

        public UnityBridgeOutcomeDispatchStatus Status { get; }
        public string RequestId { get; }
        public bool CanRetry { get; }
        public UnityBridgeProtocolError Error { get; }
        public bool CallbackInvoked =>
            Status ==
            UnityBridgeOutcomeDispatchStatus.CallbackInvoked;

        internal static UnityBridgeOutcomeDispatchResult Completed(
            string requestId)
        {
            return new UnityBridgeOutcomeDispatchResult(
                UnityBridgeOutcomeDispatchStatus.CallbackInvoked,
                requestId,
                false,
                null);
        }

        internal static UnityBridgeOutcomeDispatchResult Failed(
            UnityBridgeOutcomeDispatchStatus status,
            string requestId,
            bool canRetry,
            UnityBridgeProtocolErrorCode code,
            string field = null)
        {
            return new UnityBridgeOutcomeDispatchResult(
                status,
                requestId,
                canRetry,
                new UnityBridgeProtocolError(code, field));
        }

        internal static UnityBridgeOutcomeDispatchResult Failed(
            UnityBridgeOutcomeDispatchStatus status,
            string requestId,
            bool canRetry,
            UnityBridgeProtocolError error)
        {
            return new UnityBridgeOutcomeDispatchResult(
                status,
                requestId,
                canRetry,
                error ??
                new UnityBridgeProtocolError(
                    UnityBridgeProtocolErrorCode.SendUnavailable));
        }
    }

    public interface IUnityBridgeOutcomeDispatchResultSink
    {
        void Publish(
            UnityBridgeReceiverReport report,
            UnityBridgeOutcomeDispatchResult result);
    }

    public sealed class UnityBridgeOutcomeSender :
        IUnityBridgeOutcomeSink,
        IDisposable
    {
        public const int MaximumRetainedRequestIdentities = 256;

        private static readonly UTF8Encoding StrictUtf8 =
            new UTF8Encoding(false, true);

        private readonly object synchronization = new object();
        private readonly HashSet<string> terminalRequestIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]>
            retryableOutcomeFingerprints =
                new Dictionary<string, byte[]>(
                    StringComparer.Ordinal);
        private readonly int ownerThreadId =
            Thread.CurrentThread.ManagedThreadId;

        private IUnityBridgeOutcomePlatformAdapter platformAdapter;
        private IUnityBridgeOutcomeDispatchResultSink dispatchResultSink;
        private bool dispatching;
        private bool publishing;
        private bool disposeRequested;
        private bool disposed;

        public UnityBridgeOutcomeSender(
            IUnityBridgeOutcomePlatformAdapter platformAdapter,
            IUnityBridgeOutcomeDispatchResultSink dispatchResultSink)
        {
            this.platformAdapter = platformAdapter ??
                throw new ArgumentNullException(nameof(platformAdapter));
            this.dispatchResultSink = dispatchResultSink ??
                throw new ArgumentNullException(
                    nameof(dispatchResultSink));
        }

        public int OwnerThreadId => ownerThreadId;

        public UnityBridgeOutcomeDispatchResult TryDispatch(
            UnityBridgeReceiverReport report)
        {
            return TryDispatchCore(report, false);
        }

        private UnityBridgeOutcomeDispatchResult TryDispatchCore(
            UnityBridgeReceiverReport report,
            bool calledFromPublish)
        {
            lock (synchronization)
            {
                if (disposed || disposeRequested)
                {
                    return Failure(
                        UnityBridgeOutcomeDispatchStatus.Disposed,
                        null,
                        false,
                        UnityBridgeProtocolErrorCode.SessionClosed);
                }

                if (Thread.CurrentThread.ManagedThreadId !=
                    ownerThreadId)
                {
                    return Failure(
                        UnityBridgeOutcomeDispatchStatus.WrongThread,
                        null,
                        true,
                        UnityBridgeProtocolErrorCode.SendUnavailable);
                }

                if (dispatching ||
                    (publishing && !calledFromPublish))
                {
                    return Failure(
                        UnityBridgeOutcomeDispatchStatus.Busy,
                        null,
                        true,
                        UnityBridgeProtocolErrorCode.SendUnavailable);
                }
            }

            var reportValidation = ValidateReport(report);
            if (!reportValidation.IsAccepted)
            {
                return UnityBridgeOutcomeDispatchResult.Failed(
                    UnityBridgeOutcomeDispatchStatus.RejectedReport,
                    reportValidation.RequestId,
                    false,
                    reportValidation.Error);
            }

            var encoding = UnityBridgeContract.EncodeOutcome(
                reportValidation.Outcome);
            if (!encoding.IsEncoded)
            {
                return UnityBridgeOutcomeDispatchResult.Failed(
                    UnityBridgeOutcomeDispatchStatus.EncodingRejected,
                    reportValidation.RequestId,
                    false,
                    encoding.Error);
            }

            byte[] outcomeFingerprint;
            try
            {
                outcomeFingerprint = ComputeFingerprint(
                    reportValidation.Request,
                    encoding.EncodedJson);
            }
            catch (Exception)
            {
                return Failure(
                    UnityBridgeOutcomeDispatchStatus.EncodingRejected,
                    reportValidation.RequestId,
                    false,
                    UnityBridgeProtocolErrorCode.SendUnavailable);
            }

            IUnityBridgeOutcomePlatformAdapter adapter;
            lock (synchronization)
            {
                if (disposed || disposeRequested)
                {
                    return Failure(
                        UnityBridgeOutcomeDispatchStatus.Disposed,
                        reportValidation.RequestId,
                        false,
                        UnityBridgeProtocolErrorCode.SessionClosed);
                }

                if (dispatching ||
                    (publishing && !calledFromPublish))
                {
                    return Failure(
                        UnityBridgeOutcomeDispatchStatus.Busy,
                        reportValidation.RequestId,
                        true,
                        UnityBridgeProtocolErrorCode.SendUnavailable);
                }

                if (terminalRequestIds.Contains(
                        reportValidation.RequestId))
                {
                    return Failure(
                        UnityBridgeOutcomeDispatchStatus.Duplicate,
                        reportValidation.RequestId,
                        false,
                        UnityBridgeProtocolErrorCode.DuplicateOutcome);
                }

                byte[] retainedFingerprint;
                var hasRetryableIdentity =
                    retryableOutcomeFingerprints.TryGetValue(
                        reportValidation.RequestId,
                        out retainedFingerprint);
                if (hasRetryableIdentity &&
                    !HasSameFingerprint(
                        retainedFingerprint,
                        outcomeFingerprint))
                {
                    return Failure(
                        UnityBridgeOutcomeDispatchStatus
                            .RejectedReport,
                        reportValidation.RequestId,
                        false,
                        UnityBridgeProtocolErrorCode.RequestMismatch);
                }

                if (!hasRetryableIdentity &&
                    terminalRequestIds.Count +
                    retryableOutcomeFingerprints.Count >=
                    MaximumRetainedRequestIdentities)
                {
                    return Failure(
                        UnityBridgeOutcomeDispatchStatus
                            .RetentionExhausted,
                        reportValidation.RequestId,
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
                platformStatus = adapter.TryReportOutcome(
                    encoding.EncodedJson);
                if (!Enum.IsDefined(
                        typeof(UnityBridgePlatformCallbackStatus),
                        platformStatus))
                {
                    platformStatus =
                        UnityBridgePlatformCallbackStatus
                            .InvocationFailed;
                }
            }
            catch (Exception)
            {
                platformStatus =
                    UnityBridgePlatformCallbackStatus.InvocationFailed;
            }

            IUnityBridgeOutcomePlatformAdapter adapterToDispose = null;
            bool closedAfterInvocation;
            lock (synchronization)
            {
                if (platformStatus !=
                    UnityBridgePlatformCallbackStatus.Unavailable)
                {
                    retryableOutcomeFingerprints.Remove(
                        reportValidation.RequestId);
                    terminalRequestIds.Add(
                        reportValidation.RequestId);
                }
                else if (!disposeRequested)
                {
                    retryableOutcomeFingerprints[
                        reportValidation.RequestId] =
                            outcomeFingerprint;
                }

                dispatching = false;
                if (disposeRequested && !publishing)
                {
                    adapterToDispose =
                        FinalizeDisposalLocked();
                }

                closedAfterInvocation =
                    disposed || disposeRequested;
            }

            DisposeSafely(adapterToDispose);

            switch (platformStatus)
            {
                case UnityBridgePlatformCallbackStatus
                    .CallbackInvoked:
                    return UnityBridgeOutcomeDispatchResult.Completed(
                        reportValidation.RequestId);
                case UnityBridgePlatformCallbackStatus.Unavailable:
                    return Failure(
                        UnityBridgeOutcomeDispatchStatus
                            .PlatformUnavailable,
                        reportValidation.RequestId,
                        !closedAfterInvocation,
                        UnityBridgeProtocolErrorCode.SendUnavailable);
                default:
                    return Failure(
                        UnityBridgeOutcomeDispatchStatus
                            .PlatformInvocationFailed,
                        reportValidation.RequestId,
                        false,
                        UnityBridgeProtocolErrorCode.SendUnavailable);
            }
        }

        public void Publish(UnityBridgeReceiverReport report)
        {
            IUnityBridgeOutcomeDispatchResultSink resultSink;
            lock (synchronization)
            {
                if (disposed || publishing)
                {
                    return;
                }

                publishing = true;
                resultSink = dispatchResultSink;
            }

            UnityBridgeOutcomeDispatchResult result;
            try
            {
                result = TryDispatchCore(report, true);
            }
            catch (Exception)
            {
                result = Failure(
                    UnityBridgeOutcomeDispatchStatus
                        .PlatformInvocationFailed,
                    null,
                    false,
                    UnityBridgeProtocolErrorCode.SendUnavailable);
            }

            try
            {
                resultSink.Publish(report, result);
            }
            catch (Exception)
            {
                // Result observers cannot unwind into the receiver or JNI.
            }
            finally
            {
                IUnityBridgeOutcomePlatformAdapter adapterToDispose =
                    null;
                lock (synchronization)
                {
                    publishing = false;
                    if (disposeRequested &&
                        !dispatching &&
                        !disposed)
                    {
                        adapterToDispose =
                            FinalizeDisposalLocked();
                    }
                }

                DisposeSafely(adapterToDispose);
            }
        }

        public void Dispose()
        {
            IUnityBridgeOutcomePlatformAdapter adapterToDispose = null;
            lock (synchronization)
            {
                if (disposed || disposeRequested)
                {
                    return;
                }

                disposeRequested = true;
                if (!dispatching && !publishing)
                {
                    adapterToDispose =
                        FinalizeDisposalLocked();
                }
            }

            DisposeSafely(adapterToDispose);
        }

        private static SenderReportValidation ValidateReport(
            UnityBridgeReceiverReport report)
        {
            if (report == null)
            {
                return SenderReportValidation.Rejected(
                    null,
                    new UnityBridgeProtocolError(
                        UnityBridgeProtocolErrorCode.NullMessage));
            }

            if (!report.IsSendable ||
                report.Request == null ||
                report.Outcome == null)
            {
                return SenderReportValidation.Rejected(
                    report.Request?.RequestId,
                    report.Error ??
                    new UnityBridgeProtocolError(
                        UnityBridgeProtocolErrorCode.SendUnavailable));
            }

            var requestValidation =
                UnityBridgeContract.ValidateRequest(report.Request);
            if (!requestValidation.IsAccepted)
            {
                return SenderReportValidation.Rejected(
                    report.Request.RequestId,
                    requestValidation.Error);
            }

            var validation =
                UnityBridgeContract.ValidateOutcomeForRequest(
                    report.Outcome,
                    requestValidation.Request);
            if (!validation.IsAccepted)
            {
                return SenderReportValidation.Rejected(
                    report.Request.RequestId,
                    validation.Error);
            }

            return SenderReportValidation.Accepted(
                requestValidation.Request,
                validation.Outcome);
        }

        private IUnityBridgeOutcomePlatformAdapter
            FinalizeDisposalLocked()
        {
            disposed = true;
            disposeRequested = true;
            dispatching = false;
            publishing = false;
            terminalRequestIds.Clear();
            retryableOutcomeFingerprints.Clear();
            var adapter = platformAdapter;
            platformAdapter = null;
            dispatchResultSink = null;
            return adapter;
        }

        private static byte[] ComputeFingerprint(
            UnityRouteRequest request,
            string encodedOutcome)
        {
            var framed = new StringBuilder(
                encodedOutcome.Length + 1536);
            AppendFingerprintSegment(
                framed,
                "unity-bridge-outcome-sender-v1");
            AppendFingerprintSegment(
                framed,
                request.ContractVersion.ToString(
                    CultureInfo.InvariantCulture));
            AppendFingerprintSegment(framed, request.RequestId);
            AppendFingerprintSegment(framed, request.RouteId);
            AppendFingerprintSegment(
                framed,
                UnityBridgeContract.GetIntentWireValue(
                    request.Intent));
            AppendFingerprintSegment(
                framed,
                request.RequestedCapabilities.Count.ToString(
                    CultureInfo.InvariantCulture));
            for (var index = 0;
                 index < request.RequestedCapabilities.Count;
                 index++)
            {
                AppendFingerprintSegment(
                    framed,
                    request.RequestedCapabilities[index]);
            }

            AppendFingerprintSegment(framed, encodedOutcome);

            var bytes = StrictUtf8.GetBytes(framed.ToString());
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(bytes);
            }
        }

        private static void AppendFingerprintSegment(
            StringBuilder output,
            string value)
        {
            output.Append(
                value.Length.ToString(
                    CultureInfo.InvariantCulture));
            output.Append(':');
            output.Append(value);
        }

        private static bool HasSameFingerprint(
            byte[] left,
            byte[] right)
        {
            if (left == null ||
                right == null ||
                left.Length != right.Length)
            {
                return false;
            }

            var difference = 0;
            for (var index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }

        private static UnityBridgeOutcomeDispatchResult Failure(
            UnityBridgeOutcomeDispatchStatus status,
            string requestId,
            bool canRetry,
            UnityBridgeProtocolErrorCode code)
        {
            return UnityBridgeOutcomeDispatchResult.Failed(
                status,
                requestId,
                canRetry,
                code);
        }

        private static void DisposeSafely(
            IUnityBridgeOutcomePlatformAdapter adapter)
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

        private sealed class SenderReportValidation
        {
            private SenderReportValidation(
                UnityRouteRequest request,
                UnityRouteOutcome outcome,
                string requestId,
                UnityBridgeProtocolError error)
            {
                Request = request;
                Outcome = outcome;
                RequestId = requestId;
                Error = error;
            }

            internal bool IsAccepted =>
                Request != null &&
                Outcome != null &&
                Error == null;
            internal UnityRouteRequest Request { get; }
            internal UnityRouteOutcome Outcome { get; }
            internal string RequestId { get; }
            internal UnityBridgeProtocolError Error { get; }

            internal static SenderReportValidation Accepted(
                UnityRouteRequest request,
                UnityRouteOutcome outcome)
            {
                return new SenderReportValidation(
                    request,
                    outcome,
                    outcome.RequestId,
                    null);
            }

            internal static SenderReportValidation Rejected(
                string requestId,
                UnityBridgeProtocolError error)
            {
                return new SenderReportValidation(
                    null,
                    null,
                    requestId,
                    error);
            }
        }
    }
}
