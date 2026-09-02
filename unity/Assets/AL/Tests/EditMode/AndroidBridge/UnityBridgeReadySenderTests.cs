using System;
using System.Collections.Generic;
using System.Threading;
using AL.Platform.Android;
using NUnit.Framework;

namespace AL.Tests.EditMode.AndroidBridge
{
    public sealed class UnityBridgeReadySenderTests
    {
        private const string RequestOne = "request-0001";
        private const string Route = "bridge.smoke";

        [Test]
        public void ReadyEncoderEmitsCanonicalV2CorrelationJson()
        {
            var ready = new UnityRouteReady(
                UnityBridgeContract.ContractVersion,
                RequestOne,
                Route);

            var result = UnityBridgeContract.EncodeReady(ready);

            Assert.That(result.IsEncoded, Is.True);
            Assert.That(result.Error, Is.Null);
            Assert.That(result.Ready, Is.Not.SameAs(ready));
            Assert.That(
                result.EncodedJson,
                Is.EqualTo(
                    "{\"contractVersion\":2," +
                    "\"requestId\":\"request-0001\"," +
                    "\"routeId\":\"bridge.smoke\"}"));
        }

        [Test]
        public void ReadyValidationRejectsMismatchedCorrelation()
        {
            var request = CreateRequest();

            var requestMismatch =
                UnityBridgeContract.ValidateReadyForRequest(
                    new UnityRouteReady(
                        UnityBridgeContract.ContractVersion,
                        "request-0002",
                        Route),
                    request);
            var routeMismatch =
                UnityBridgeContract.ValidateReadyForRequest(
                    new UnityRouteReady(
                        UnityBridgeContract.ContractVersion,
                        RequestOne,
                        "bridge.other"),
                    request);

            AssertRejected(
                requestMismatch,
                UnityBridgeProtocolErrorCode.RequestMismatch,
                "requestId");
            AssertRejected(
                routeMismatch,
                UnityBridgeProtocolErrorCode.RouteMismatch,
                "routeId");
            Assert.That(
                UnityBridgeContract.GetProtocolErrorWireValue(
                    UnityBridgeProtocolErrorCode.DuplicateReady),
                Is.EqualTo("bridge.duplicate_ready"));
            Assert.That(
                UnityBridgeContract.GetProtocolErrorWireValue(
                    UnityBridgeProtocolErrorCode.ReadyAfterOutcome),
                Is.EqualTo("bridge.ready_after_outcome"));
        }

        [Test]
        public void AndroidAdapterDeclaresExactReadyJvmBoundary()
        {
            Assert.That(
                AndroidUnityBridgeReadyPlatformAdapter.CallbackClassName,
                Is.EqualTo(
                    "com.example.anotherlife.ui.unity." +
                    "UnityBridgeCallbacks"));
            Assert.That(
                AndroidUnityBridgeReadyPlatformAdapter.CallbackMethodName,
                Is.EqualTo("reportReady"));
            Assert.That(
                AndroidUnityBridgeReadyPlatformAdapter
                    .CallbackMethodDescriptor,
                Is.EqualTo("(Ljava/lang/String;)V"));
            Assert.That(
                AndroidUnityBridgeReadyPlatformAdapter
                    .IsAndroidPlayerBuild,
                Is.False);

            var adapter =
                new AndroidUnityBridgeReadyPlatformAdapter();
            Assert.That(
                adapter.TryReportReady("{}"),
                Is.EqualTo(
                    UnityBridgePlatformCallbackStatus.Unavailable));

            adapter.Dispose();
            adapter.Dispose();
            Assert.That(
                adapter.TryReportReady("{}"),
                Is.EqualTo(
                    UnityBridgePlatformCallbackStatus.Unavailable));
        }

        [Test]
        public void SenderInvokesCallbackOnceAndSuppressesReplay()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.CallbackInvoked);

            using (var sender = new UnityBridgeReadySender(adapter))
            {
                var first = sender.TryDispatch(CreateRequest());
                var replay = sender.TryDispatch(CreateRequest());

                AssertDispatch(
                    first,
                    UnityBridgeReadyDispatchStatus.CallbackInvoked,
                    false,
                    null);
                AssertDispatch(
                    replay,
                    UnityBridgeReadyDispatchStatus.Duplicate,
                    false,
                    UnityBridgeProtocolErrorCode.DuplicateReady);
                Assert.That(adapter.InvocationCount, Is.EqualTo(1));
                Assert.That(
                    adapter.LastPayload,
                    Is.EqualTo(
                        "{\"contractVersion\":2," +
                        "\"requestId\":\"request-0001\"," +
                        "\"routeId\":\"bridge.smoke\"}"));
            }
        }

        [Test]
        public void SenderRetriesUnavailableCallbackButPinsRequestEnvelope()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.Unavailable,
                UnityBridgePlatformCallbackStatus.CallbackInvoked);

            using (var sender = new UnityBridgeReadySender(adapter))
            {
                var unavailable = sender.TryDispatch(CreateRequest());
                var changedEnvelope = sender.TryDispatch(
                    CreateRequest(routeId: "bridge.other"));
                var changedIntent = sender.TryDispatch(
                    CreateRequest(intent: UnityRouteIntent.Authoritative));
                var retry = sender.TryDispatch(CreateRequest());

                AssertDispatch(
                    unavailable,
                    UnityBridgeReadyDispatchStatus.PlatformUnavailable,
                    true,
                    UnityBridgeProtocolErrorCode.SendUnavailable);
                AssertDispatch(
                    changedEnvelope,
                    UnityBridgeReadyDispatchStatus.RejectedRequest,
                    false,
                    UnityBridgeProtocolErrorCode.RequestMismatch);
                AssertDispatch(
                    changedIntent,
                    UnityBridgeReadyDispatchStatus.RejectedRequest,
                    false,
                    UnityBridgeProtocolErrorCode.RequestMismatch);
                AssertDispatch(
                    retry,
                    UnityBridgeReadyDispatchStatus.CallbackInvoked,
                    false,
                    null);
                Assert.That(adapter.InvocationCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void SenderRejectsInvalidRequestWithoutPlatformCall()
        {
            var adapter = new ScriptedAdapter();
            using (var sender = new UnityBridgeReadySender(adapter))
            {
                var result = sender.TryDispatch(
                    CreateRequest(requestId: "not valid"));

                AssertDispatch(
                    result,
                    UnityBridgeReadyDispatchStatus.RejectedRequest,
                    false,
                    UnityBridgeProtocolErrorCode.InvalidRequestId);
                Assert.That(adapter.InvocationCount, Is.Zero);
            }
        }

        [Test]
        public void SenderTreatsInvocationFailureAsTerminal()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.InvocationFailed);

            using (var sender = new UnityBridgeReadySender(adapter))
            {
                var failed = sender.TryDispatch(CreateRequest());
                var replay = sender.TryDispatch(CreateRequest());

                AssertDispatch(
                    failed,
                    UnityBridgeReadyDispatchStatus
                        .PlatformInvocationFailed,
                    false,
                    UnityBridgeProtocolErrorCode.SendUnavailable);
                AssertDispatch(
                    replay,
                    UnityBridgeReadyDispatchStatus.Duplicate,
                    false,
                    UnityBridgeProtocolErrorCode.DuplicateReady);
                Assert.That(adapter.InvocationCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void SenderFailsClosedAtRetentionBound()
        {
            var adapter = new ScriptedAdapter();
            using (var sender = new UnityBridgeReadySender(adapter))
            {
                for (var index = 0;
                     index <
                     UnityBridgeReadySender
                         .MaximumRetainedRequestIdentities;
                     index++)
                {
                    var result = sender.TryDispatch(
                        CreateRequest(
                            requestId:
                            "request-" + index.ToString("D4")));
                    AssertDispatch(
                        result,
                        UnityBridgeReadyDispatchStatus
                            .PlatformUnavailable,
                        true,
                        UnityBridgeProtocolErrorCode.SendUnavailable);
                }

                var exhausted = sender.TryDispatch(
                    CreateRequest(requestId: "request-overflow"));

                AssertDispatch(
                    exhausted,
                    UnityBridgeReadyDispatchStatus.RetentionExhausted,
                    false,
                    UnityBridgeProtocolErrorCode.SessionClosed);
                Assert.That(
                    adapter.InvocationCount,
                    Is.EqualTo(
                        UnityBridgeReadySender
                            .MaximumRetainedRequestIdentities));
            }
        }

        [Test]
        public void SenderIsOwnerThreadBoundAndRejectsAfterDisposal()
        {
            var adapter = new ScriptedAdapter();
            var sender = new UnityBridgeReadySender(adapter);
            UnityBridgeReadyDispatchResult wrongThread = null;

            var thread = new Thread(
                () => wrongThread = sender.TryDispatch(CreateRequest()));
            thread.Start();
            Assert.That(thread.Join(5000), Is.True);

            AssertDispatch(
                wrongThread,
                UnityBridgeReadyDispatchStatus.WrongThread,
                true,
                UnityBridgeProtocolErrorCode.SendUnavailable);
            Assert.That(adapter.InvocationCount, Is.Zero);

            sender.Dispose();
            sender.Dispose();
            var disposed = sender.TryDispatch(CreateRequest());
            AssertDispatch(
                disposed,
                UnityBridgeReadyDispatchStatus.Disposed,
                false,
                UnityBridgeProtocolErrorCode.SessionClosed);
            Assert.That(adapter.DisposeCount, Is.EqualTo(1));
        }

        private static UnityRouteRequest CreateRequest(
            string requestId = RequestOne,
            string routeId = Route,
            UnityRouteIntent intent = UnityRouteIntent.Preview)
        {
            return new UnityRouteRequest(
                UnityBridgeContract.ContractVersion,
                requestId,
                routeId,
                intent,
                Array.Empty<string>());
        }

        private static void AssertRejected(
            UnityBridgeReadyValidationResult result,
            UnityBridgeProtocolErrorCode code,
            string field)
        {
            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Ready, Is.Null);
            Assert.That(result.Error.Code, Is.EqualTo(code));
            Assert.That(result.Error.Field, Is.EqualTo(field));
        }

        private static void AssertDispatch(
            UnityBridgeReadyDispatchResult result,
            UnityBridgeReadyDispatchStatus status,
            bool canRetry,
            UnityBridgeProtocolErrorCode? errorCode)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.CanRetry, Is.EqualTo(canRetry));
            Assert.That(result.CallbackInvoked,
                Is.EqualTo(
                    status ==
                    UnityBridgeReadyDispatchStatus.CallbackInvoked));

            if (errorCode.HasValue)
            {
                Assert.That(result.Error, Is.Not.Null);
                Assert.That(
                    result.Error.Code,
                    Is.EqualTo(errorCode.Value));
            }
            else
            {
                Assert.That(result.Error, Is.Null);
            }
        }

        private sealed class ScriptedAdapter :
            IUnityBridgeReadyPlatformAdapter
        {
            private readonly Queue<UnityBridgePlatformCallbackStatus>
                statuses;

            internal ScriptedAdapter(
                params UnityBridgePlatformCallbackStatus[] statuses)
            {
                this.statuses =
                    new Queue<UnityBridgePlatformCallbackStatus>(statuses);
            }

            internal int InvocationCount { get; private set; }
            internal int DisposeCount { get; private set; }
            internal string LastPayload { get; private set; }

            public UnityBridgePlatformCallbackStatus TryReportReady(
                string encodedReady)
            {
                InvocationCount++;
                LastPayload = encodedReady;
                return statuses.Count == 0
                    ? UnityBridgePlatformCallbackStatus.Unavailable
                    : statuses.Dequeue();
            }

            public void Dispose()
            {
                DisposeCount++;
            }
        }
    }
}
