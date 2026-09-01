using System;
using System.Collections.Generic;
using UnityEngine;

namespace AL.Platform.Android
{
    public enum UnityBridgeReceiverReportKind
    {
        CorrelatedOutcome = 0,
        ProtocolFailure = 1
    }

    public sealed class UnityBridgeReceiverReport
    {
        private UnityBridgeReceiverReport(
            UnityBridgeReceiverReportKind kind,
            UnityRouteRequest request,
            UnityRouteOutcome outcome,
            UnityBridgeProtocolError error)
        {
            Kind = kind;
            Request = request;
            Outcome = outcome;
            Error = error;
        }

        public UnityBridgeReceiverReportKind Kind { get; }
        public UnityRouteRequest Request { get; }
        public UnityRouteOutcome Outcome { get; }
        public UnityBridgeProtocolError Error { get; }
        public UnityRouteOutcomeStatus Status =>
            Outcome == null
                ? UnityRouteOutcomeStatus.Failure
                : Outcome.Status;
        public string DiagnosticCode =>
            Outcome == null
                ? Error?.WireCode
                : Outcome.DiagnosticCode;
        public bool IsSendable =>
            Kind == UnityBridgeReceiverReportKind.CorrelatedOutcome &&
            Outcome != null;

        internal static UnityBridgeReceiverReport Correlated(
            UnityRouteRequest request,
            UnityRouteOutcome outcome)
        {
            return new UnityBridgeReceiverReport(
                UnityBridgeReceiverReportKind.CorrelatedOutcome,
                request ?? throw new ArgumentNullException(nameof(request)),
                outcome ?? throw new ArgumentNullException(nameof(outcome)),
                null);
        }

        internal static UnityBridgeReceiverReport Failed(
            UnityBridgeProtocolError error,
            UnityRouteRequest request = null)
        {
            return new UnityBridgeReceiverReport(
                UnityBridgeReceiverReportKind.ProtocolFailure,
                request,
                null,
                error ?? throw new ArgumentNullException(nameof(error)));
        }
    }

    public interface IUnityBridgeOutcomeSink
    {
        void Publish(UnityBridgeReceiverReport report);
    }

    public sealed class UnityBridgeRouteReceiver : IDisposable
    {
        public const int MaximumRetainedRequestIdentities = 256;
        public const int MaximumPendingReports = 64;

        private readonly object synchronization = new object();
        private readonly HashSet<string> consumedRequestIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<UnityBridgeReceiverReport> pendingReports =
            new Queue<UnityBridgeReceiverReport>();

        private IUnityBridgeOutcomeSink outcomeSink;
        private UnityRouteRequest latestRequest;
        private UnityBridgeReceiverReport terminalReport;
        private bool closed;
        private bool disposed;
        private bool disposeRequested;
        private bool dispatching;
        private bool terminalDispatch;

        public UnityBridgeRouteReceiver(IUnityBridgeOutcomeSink outcomeSink)
        {
            this.outcomeSink = outcomeSink ??
                throw new ArgumentNullException(nameof(outcomeSink));
        }

        public void Receive(string rawJson)
        {
            var shouldDrain = false;
            lock (synchronization)
            {
                if (disposed || disposeRequested || terminalDispatch)
                {
                    return;
                }

                UnityBridgeReceiverReport report;
                if (closed)
                {
                    report = Failure(
                        UnityBridgeProtocolErrorCode.SessionClosed);
                }
                else
                {
                    var parseResult =
                        UnityBridgeContract.ParseRequest(rawJson);
                    report = parseResult.IsAccepted
                        ? HandleAcceptedRequest(parseResult.Request)
                        : UnityBridgeReceiverReport.Failed(
                            parseResult.Error);
                }

                if (pendingReports.Count >= MaximumPendingReports)
                {
                    BeginTerminalDispatchLocked();
                    terminalReport = Failure(
                        UnityBridgeProtocolErrorCode.SessionClosed,
                        null,
                        report.Request);
                    if (!dispatching)
                    {
                        dispatching = true;
                        shouldDrain = true;
                    }
                }
                else
                {
                    pendingReports.Enqueue(report);
                    if (!dispatching)
                    {
                        dispatching = true;
                        shouldDrain = true;
                    }
                }
            }

            if (shouldDrain)
            {
                DrainReports();
            }
        }

        public void Close()
        {
            lock (synchronization)
            {
                if (disposed || closed)
                {
                    return;
                }

                closed = true;
                latestRequest = null;
                consumedRequestIds.Clear();
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
                closed = true;
                latestRequest = null;
                consumedRequestIds.Clear();
                if (!dispatching)
                {
                    FinalizeDisposalLocked();
                }
            }
        }

        private void DrainReports()
        {
            var dispatchedCount = 0;
            while (true)
            {
                UnityBridgeReceiverReport report;
                IUnityBridgeOutcomeSink sink;
                lock (synchronization)
                {
                    if (dispatchedCount >= MaximumPendingReports &&
                        !terminalDispatch &&
                        !disposeRequested &&
                        pendingReports.Count > 0)
                    {
                        BeginTerminalDispatchLocked();
                        PromoteLastPendingReportToTerminalLocked();
                    }

                    if (pendingReports.Count > 0)
                    {
                        report = pendingReports.Dequeue();
                    }
                    else if (terminalReport != null)
                    {
                        report = terminalReport;
                        terminalReport = null;
                    }
                    else
                    {
                        dispatching = false;
                        if (terminalDispatch || disposeRequested)
                        {
                            FinalizeDisposalLocked();
                        }

                        return;
                    }

                    sink = outcomeSink;
                }

                PublishSafely(sink, report);
                dispatchedCount++;
            }
        }

        private void BeginTerminalDispatchLocked()
        {
            terminalDispatch = true;
            closed = true;
            latestRequest = null;
            consumedRequestIds.Clear();
        }

        private void PromoteLastPendingReportToTerminalLocked()
        {
            var pendingCount = pendingReports.Count;
            for (var index = 1; index < pendingCount; index++)
            {
                pendingReports.Enqueue(pendingReports.Dequeue());
            }

            var replacedReport = pendingReports.Dequeue();
            terminalReport = Failure(
                UnityBridgeProtocolErrorCode.SessionClosed,
                null,
                replacedReport.Request);
        }

        private void FinalizeDisposalLocked()
        {
            disposed = true;
            disposeRequested = true;
            closed = true;
            terminalDispatch = true;
            dispatching = false;
            latestRequest = null;
            terminalReport = null;
            consumedRequestIds.Clear();
            pendingReports.Clear();
            outcomeSink = null;
        }

        private static void PublishSafely(
            IUnityBridgeOutcomeSink sink,
            UnityBridgeReceiverReport report)
        {
            try
            {
                sink.Publish(report);
            }
            catch (Exception)
            {
                // A sink failure cannot unwind through UnitySendMessage
                // or roll back an already-consumed request identity.
            }
        }

        private UnityBridgeReceiverReport HandleAcceptedRequest(
            UnityRouteRequest request)
        {
            if (latestRequest != null &&
                string.Equals(
                    latestRequest.RequestId,
                    request.RequestId,
                    StringComparison.Ordinal))
            {
                if (!string.Equals(
                        latestRequest.RouteId,
                        request.RouteId,
                        StringComparison.Ordinal))
                {
                    return Failure(
                        UnityBridgeProtocolErrorCode.RouteMismatch,
                        "routeId",
                        request);
                }

                if (!HasSameEnvelope(latestRequest, request))
                {
                    return Failure(
                        UnityBridgeProtocolErrorCode.RequestMismatch,
                        "requestId",
                        request);
                }

                return Failure(
                    UnityBridgeProtocolErrorCode.DuplicateOutcome,
                    null,
                    request);
            }

            if (consumedRequestIds.Contains(request.RequestId))
            {
                return Failure(
                    UnityBridgeProtocolErrorCode.RequestMismatch,
                    "requestId",
                    request);
            }

            if (consumedRequestIds.Count >=
                MaximumRetainedRequestIdentities)
            {
                closed = true;
                latestRequest = null;
                consumedRequestIds.Clear();
                return Failure(
                    UnityBridgeProtocolErrorCode.SessionClosed,
                    null,
                    request);
            }

            consumedRequestIds.Add(request.RequestId);
            latestRequest = request;

            var outcome = new UnityRouteOutcome(
                UnityBridgeContract.ContractVersion,
                request.RequestId,
                request.RouteId,
                UnityRouteOutcomeStatus.Unavailable,
                UnityBridgeContract.RouteNotAvailableDiagnostic);
            var validation =
                UnityBridgeContract.ValidateOutcomeForRequest(
                    outcome,
                    request);
            if (!validation.IsAccepted)
            {
                return Failure(
                    UnityBridgeProtocolErrorCode.SendUnavailable,
                    null,
                    request);
            }

            return UnityBridgeReceiverReport.Correlated(
                request,
                validation.Outcome);
        }

        private static UnityBridgeReceiverReport Failure(
            UnityBridgeProtocolErrorCode code,
            string field = null,
            UnityRouteRequest request = null)
        {
            return UnityBridgeReceiverReport.Failed(
                new UnityBridgeProtocolError(code, field),
                request);
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
    }

    [DisallowMultipleComponent]
    public sealed class AndroidBridge : MonoBehaviour, IDisposable
    {
        private readonly object lifecycleSynchronization = new object();

        private IUnityBridgeOutcomeSink configuredSink;
        private UnityBridgeRouteReceiver receiver;
        private bool destroyed;

        public void ConfigureOutcomeSink(IUnityBridgeOutcomeSink outcomeSink)
        {
            if (outcomeSink == null)
            {
                throw new ArgumentNullException(nameof(outcomeSink));
            }

            lock (lifecycleSynchronization)
            {
                if (destroyed)
                {
                    throw new ObjectDisposedException(
                        nameof(AndroidBridge));
                }

                if (receiver != null)
                {
                    throw new InvalidOperationException(
                        "The Android bridge receiver is already active.");
                }

                configuredSink = outcomeSink;
            }
        }

        public void SetRouteContext(string rawJson)
        {
            UnityBridgeRouteReceiver activeReceiver;
            lock (lifecycleSynchronization)
            {
                if (destroyed)
                {
                    return;
                }

                activeReceiver = EnsureReceiverLocked();
            }

            activeReceiver.Receive(rawJson);
        }

        public void Dispose()
        {
            TearDownReceiver();
        }

        private UnityBridgeRouteReceiver EnsureReceiverLocked()
        {
            if (receiver == null)
            {
                receiver = new UnityBridgeRouteReceiver(
                    configuredSink ??
                    DiscardingUnityBridgeOutcomeSink.Instance);
            }

            return receiver;
        }

        private void OnDisable()
        {
            Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void TearDownReceiver()
        {
            UnityBridgeRouteReceiver receiverToDispose;
            lock (lifecycleSynchronization)
            {
                if (destroyed)
                {
                    return;
                }

                destroyed = true;
                receiverToDispose = receiver;
                receiver = null;
                configuredSink = null;
            }

            receiverToDispose?.Dispose();
        }

        private sealed class DiscardingUnityBridgeOutcomeSink :
            IUnityBridgeOutcomeSink
        {
            internal static readonly
                DiscardingUnityBridgeOutcomeSink Instance =
                    new DiscardingUnityBridgeOutcomeSink();

            private DiscardingUnityBridgeOutcomeSink()
            {
            }

            public void Publish(UnityBridgeReceiverReport report)
            {
                // Standalone fallback only. Production wiring registers the real sender
                // through AndroidBridgeRuntimeHost.ConfigureOutcomeSink.
            }
        }
    }
}
