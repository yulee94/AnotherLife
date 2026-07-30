using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using AL.Platform.Android;
using NUnit.Framework;

namespace AL.Tests.EditMode.AndroidBridge
{
    public sealed class UnityBridgeOutcomeSenderTests
    {
        private const string RequestOne = "request-0001";
        private const string RequestTwo = "request-0002";
        private const string Route = "bridge.smoke";

        [Test]
        public void OutcomeEncoderEmitsCanonicalV2JsonAndEscapesStrings()
        {
            var payload =
                "quote\" slash\\ controls\b\f\n\r\t\u0001 café \ud83d\ude00";
            var outcome = new UnityRouteOutcome(
                UnityBridgeContract.ContractVersion,
                RequestOne,
                Route,
                UnityRouteOutcomeStatus.Success,
                "route.ok",
                "result-0001",
                payload);

            var result = UnityBridgeContract.EncodeOutcome(outcome);

            Assert.That(result.IsEncoded, Is.True);
            Assert.That(result.Error, Is.Null);
            Assert.That(result.Outcome, Is.Not.SameAs(outcome));
            Assert.That(
                result.EncodedJson,
                Is.EqualTo(
                    "{\"contractVersion\":2," +
                    "\"requestId\":\"request-0001\"," +
                    "\"routeId\":\"bridge.smoke\"," +
                    "\"status\":\"success\"," +
                    "\"diagnosticCode\":\"route.ok\"," +
                    "\"resultId\":\"result-0001\"," +
                    "\"payload\":\"quote\\\" slash\\\\ controls" +
                    "\\b\\f\\n\\r\\t\\u0001 café \ud83d\ude00\"}"));
        }

        [Test]
        public void OutcomeEncoderEnforcesExactMessageAndUtf16Bounds()
        {
            var exactPayload = new string('"', 16330) + "xxx";
            var exact = UnityBridgeContract.EncodeOutcome(
                new UnityRouteOutcome(
                    UnityBridgeContract.ContractVersion,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Success,
                    payload: exactPayload));

            Assert.That(exact.IsEncoded, Is.True);
            Assert.That(
                new UTF8Encoding(false, true).GetByteCount(
                    exact.EncodedJson),
                Is.EqualTo(UnityBridgeContract.MaximumMessageBytes));

            AssertEncodingRejected(
                new UnityRouteOutcome(
                    UnityBridgeContract.ContractVersion,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Success,
                    payload: exactPayload + "x"),
                UnityBridgeProtocolErrorCode.MessageTooLarge);
            AssertEncodingRejected(
                new UnityRouteOutcome(
                    UnityBridgeContract.ContractVersion,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Success,
                    payload: "x\uD800"),
                UnityBridgeProtocolErrorCode.MalformedJson,
                "payload");
        }

        [Test]
        public void AndroidAdapterDeclaresExactJvmBoundaryAndIsUnavailableInEditor()
        {
            Assert.That(
                AndroidUnityBridgeOutcomePlatformAdapter.CallbackClassName,
                Is.EqualTo(
                    "com.example.anotherlife.ui.unity." +
                    "UnityBridgeCallbacks"));
            Assert.That(
                AndroidUnityBridgeOutcomePlatformAdapter.CallbackMethodName,
                Is.EqualTo("reportOutcome"));
            Assert.That(
                AndroidUnityBridgeOutcomePlatformAdapter
                    .CallbackMethodDescriptor,
                Is.EqualTo("(Ljava/lang/String;)V"));
            Assert.That(
                AndroidUnityBridgeOutcomePlatformAdapter
                    .IsAndroidPlayerBuild,
                Is.False);

            var adapter =
                new AndroidUnityBridgeOutcomePlatformAdapter();
            Assert.That(
                adapter.TryReportOutcome("{}"),
                Is.EqualTo(
                    UnityBridgePlatformCallbackStatus.Unavailable));

            adapter.Dispose();
            adapter.Dispose();
            Assert.That(
                adapter.TryReportOutcome("{}"),
                Is.EqualTo(
                    UnityBridgePlatformCallbackStatus.Unavailable));
        }

        [Test]
        public void SenderRejectsNullAndProtocolFailuresWithoutPlatformCall()
        {
            var adapter = new ScriptedAdapter();
            using (var sender = CreateSender(adapter))
            {
                var nullResult = sender.TryDispatch(null);
                var failureResult = sender.TryDispatch(
                    CreateProtocolFailureReport());

                AssertDispatch(
                    nullResult,
                    UnityBridgeOutcomeDispatchStatus.RejectedReport,
                    false,
                    UnityBridgeProtocolErrorCode.NullMessage);
                AssertDispatch(
                    failureResult,
                    UnityBridgeOutcomeDispatchStatus.RejectedReport,
                    false,
                    UnityBridgeProtocolErrorCode.MalformedJson);
                Assert.That(adapter.InvocationCount, Is.Zero);
            }
        }

        [Test]
        public void SenderRevalidatesCorrelationBeforeEncoding()
        {
            var adapter = new ScriptedAdapter();
            var forged = CreateMismatchedSendableReport();

            using (var sender = CreateSender(adapter))
            {
                var result = sender.TryDispatch(forged);

                Assert.That(forged.IsSendable, Is.True);
                AssertDispatch(
                    result,
                    UnityBridgeOutcomeDispatchStatus.RejectedReport,
                    false,
                    UnityBridgeProtocolErrorCode.RequestMismatch,
                    RequestOne);
                Assert.That(adapter.InvocationCount, Is.Zero);
            }
        }

        [Test]
        public void SenderInvokesCallbackOnceAndSuppressesExactReplay()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.CallbackInvoked);
            var report = CreateSendableReport(RequestOne);

            using (var sender = CreateSender(adapter))
            {
                var first = sender.TryDispatch(report);
                var replay = sender.TryDispatch(report);

                AssertDispatch(
                    first,
                    UnityBridgeOutcomeDispatchStatus.CallbackInvoked,
                    false,
                    null,
                    RequestOne);
                AssertDispatch(
                    replay,
                    UnityBridgeOutcomeDispatchStatus.Duplicate,
                    false,
                    UnityBridgeProtocolErrorCode.DuplicateOutcome,
                    RequestOne);
                Assert.That(adapter.InvocationCount, Is.EqualTo(1));
                Assert.That(
                    adapter.Messages.Single(),
                    Is.EqualTo(
                        "{\"contractVersion\":2," +
                        "\"requestId\":\"request-0001\"," +
                        "\"routeId\":\"bridge.smoke\"," +
                        "\"status\":\"unavailable\"," +
                        "\"diagnosticCode\":\"route.not_available\"}"));
            }
        }

        [Test]
        public void SenderOwnsRequestIdentityAcrossReceiverInstances()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.CallbackInvoked);
            var first = CreateSendableReport(RequestOne, Route);
            var conflicting = CreateSendableReport(
                RequestOne,
                "bridge.other");

            using (var sender = CreateSender(adapter))
            {
                Assert.That(
                    sender.TryDispatch(first).Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus.CallbackInvoked));
                Assert.That(
                    sender.TryDispatch(conflicting).Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus.Duplicate));
                Assert.That(adapter.InvocationCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void DefinitePlatformUnavailableAllowsExactRetry()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.Unavailable,
                UnityBridgePlatformCallbackStatus.CallbackInvoked);
            var report = CreateSendableReport(RequestOne);
            var altered = CreateSendableReport(
                RequestOne,
                "bridge.other");
            var alteredIntent = CreateSendableReport(
                RequestOne,
                Route,
                "authoritative");
            var alteredCapabilities = CreateSendableReport(
                RequestOne,
                Route,
                "preview",
                "[\"route.cancel\"]");

            using (var sender = CreateSender(adapter))
            {
                var unavailable = sender.TryDispatch(report);
                var mismatch = sender.TryDispatch(altered);
                var intentMismatch =
                    sender.TryDispatch(alteredIntent);
                var capabilitiesMismatch =
                    sender.TryDispatch(alteredCapabilities);
                var retried = sender.TryDispatch(report);
                var replay = sender.TryDispatch(report);

                AssertDispatch(
                    unavailable,
                    UnityBridgeOutcomeDispatchStatus.PlatformUnavailable,
                    true,
                    UnityBridgeProtocolErrorCode.SendUnavailable,
                    RequestOne);
                AssertDispatch(
                    mismatch,
                    UnityBridgeOutcomeDispatchStatus.RejectedReport,
                    false,
                    UnityBridgeProtocolErrorCode.RequestMismatch,
                    RequestOne);
                AssertDispatch(
                    intentMismatch,
                    UnityBridgeOutcomeDispatchStatus.RejectedReport,
                    false,
                    UnityBridgeProtocolErrorCode.RequestMismatch,
                    RequestOne);
                AssertDispatch(
                    capabilitiesMismatch,
                    UnityBridgeOutcomeDispatchStatus.RejectedReport,
                    false,
                    UnityBridgeProtocolErrorCode.RequestMismatch,
                    RequestOne);
                AssertDispatch(
                    retried,
                    UnityBridgeOutcomeDispatchStatus.CallbackInvoked,
                    false,
                    null,
                    RequestOne);
                Assert.That(
                    replay.Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus.Duplicate));
                Assert.That(adapter.InvocationCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void PlatformExceptionIsContainedAndTerminal()
        {
            var adapter = new ScriptedAdapter();
            adapter.Handler = _ =>
                throw new InvalidOperationException("synthetic JNI fault");
            var report = CreateSendableReport(RequestOne);

            using (var sender = CreateSender(adapter))
            {
                var failed = sender.TryDispatch(report);
                var replay = sender.TryDispatch(report);

                AssertDispatch(
                    failed,
                    UnityBridgeOutcomeDispatchStatus
                        .PlatformInvocationFailed,
                    false,
                    UnityBridgeProtocolErrorCode.SendUnavailable,
                    RequestOne);
                Assert.That(
                    replay.Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus.Duplicate));
                Assert.That(adapter.InvocationCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void InvalidPlatformStatusFailsClosedAndSuppressesRetry()
        {
            var adapter = new ScriptedAdapter(
                (UnityBridgePlatformCallbackStatus)99);
            var report = CreateSendableReport(RequestOne);

            using (var sender = CreateSender(adapter))
            {
                Assert.That(
                    sender.TryDispatch(report).Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus
                            .PlatformInvocationFailed));
                Assert.That(
                    sender.TryDispatch(report).Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus.Duplicate));
                Assert.That(adapter.InvocationCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void WrongThreadDoesNotTakeOwnershipAndMainThreadCanRetry()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.CallbackInvoked);
            var report = CreateSendableReport(RequestOne);
            UnityBridgeOutcomeDispatchResult workerResult = null;

            using (var sender = CreateSender(adapter))
            {
                var worker = new Thread(
                    () => workerResult = sender.TryDispatch(report));
                worker.Start();
                Assert.That(
                    worker.Join(TimeSpan.FromSeconds(5)),
                    Is.True);

                AssertDispatch(
                    workerResult,
                    UnityBridgeOutcomeDispatchStatus.WrongThread,
                    true,
                    UnityBridgeProtocolErrorCode.SendUnavailable);
                Assert.That(adapter.InvocationCount, Is.Zero);
                Assert.That(
                    sender.TryDispatch(report).Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus.CallbackInvoked));
                Assert.That(adapter.InvocationCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void ReentryIsDepthOneAndRejectedReportCanRetryLater()
        {
            var adapter = new ScriptedAdapter();
            var first = CreateSendableReport(RequestOne);
            var second = CreateSendableReport(RequestTwo);
            UnityBridgeOutcomeDispatchResult nested = null;
            UnityBridgeOutcomeSender sender = null;
            adapter.Handler = _ =>
            {
                adapter.Handler = null;
                nested = sender.TryDispatch(second);
                return UnityBridgePlatformCallbackStatus.CallbackInvoked;
            };

            using (sender = CreateSender(adapter))
            {
                var outer = sender.TryDispatch(first);

                Assert.That(
                    outer.Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus.CallbackInvoked));
                AssertDispatch(
                    nested,
                    UnityBridgeOutcomeDispatchStatus.Busy,
                    true,
                    UnityBridgeProtocolErrorCode.SendUnavailable);
                Assert.That(adapter.InvocationCount, Is.EqualTo(1));

                Assert.That(
                    sender.TryDispatch(second).Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus.CallbackInvoked));
                Assert.That(adapter.InvocationCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void TerminalIdentityRetentionIsBoundedWithoutEviction()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.CallbackInvoked);
            var first = CreateSendableReport("request-0000");

            using (var sender = CreateSender(adapter))
            {
                for (var index = 0;
                     index <
                     UnityBridgeOutcomeSender
                         .MaximumRetainedRequestIdentities;
                     index++)
                {
                    var report = index == 0
                        ? first
                        : CreateSendableReport(
                            "request-" + index.ToString("D4"));
                    Assert.That(
                        sender.TryDispatch(report).Status,
                        Is.EqualTo(
                            UnityBridgeOutcomeDispatchStatus
                                .CallbackInvoked));
                }

                var overflow = sender.TryDispatch(
                    CreateSendableReport("request-overflow"));
                var oldestReplay = sender.TryDispatch(first);

                AssertDispatch(
                    overflow,
                    UnityBridgeOutcomeDispatchStatus.RetentionExhausted,
                    false,
                    UnityBridgeProtocolErrorCode.SessionClosed,
                    "request-overflow");
                Assert.That(
                    oldestReplay.Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus.Duplicate));
                Assert.That(
                        adapter.InvocationCount,
                    Is.EqualTo(
                        UnityBridgeOutcomeSender
                            .MaximumRetainedRequestIdentities));
            }
        }

        [Test]
        public void RetryableIdentityRetentionIsBoundedWithoutEviction()
        {
            var adapter = new ScriptedAdapter();
            adapter.Handler = _ =>
                UnityBridgePlatformCallbackStatus.Unavailable;
            var first = CreateSendableReport("request-0000");

            using (var sender = CreateSender(adapter))
            {
                for (var index = 0;
                     index <
                     UnityBridgeOutcomeSender
                         .MaximumRetainedRequestIdentities;
                     index++)
                {
                    var report = index == 0
                        ? first
                        : CreateSendableReport(
                            "request-" + index.ToString("D4"));
                    Assert.That(
                        sender.TryDispatch(report).Status,
                        Is.EqualTo(
                            UnityBridgeOutcomeDispatchStatus
                                .PlatformUnavailable));
                }

                Assert.That(
                    sender.TryDispatch(
                        CreateSendableReport("request-overflow"))
                        .Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus
                            .RetentionExhausted));
                AssertDispatch(
                    sender.TryDispatch(
                        CreateSendableReport(
                            "request-0000",
                            "bridge.other")),
                    UnityBridgeOutcomeDispatchStatus.RejectedReport,
                    false,
                    UnityBridgeProtocolErrorCode.RequestMismatch,
                    "request-0000");
                Assert.That(
                    sender.TryDispatch(first).Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus
                            .PlatformUnavailable));
                Assert.That(
                    adapter.InvocationCount,
                    Is.EqualTo(
                        UnityBridgeOutcomeSender
                            .MaximumRetainedRequestIdentities + 1));
            }
        }

        [Test]
        public void TerminalAndRetryableIdentitiesShareOneCapacity()
        {
            const int identitiesPerDisposition =
                UnityBridgeOutcomeSender
                    .MaximumRetainedRequestIdentities / 2;
            var adapter = new ScriptedAdapter();
            adapter.Handler = encoded =>
                encoded.Contains("\"requestId\":\"retry-")
                    ? UnityBridgePlatformCallbackStatus.Unavailable
                    : UnityBridgePlatformCallbackStatus
                        .CallbackInvoked;
            var firstRetry =
                CreateSendableReport("retry-0000");

            using (var sender = CreateSender(adapter))
            {
                for (var index = 0;
                     index < identitiesPerDisposition;
                     index++)
                {
                    Assert.That(
                        sender.TryDispatch(
                            CreateSendableReport(
                                "terminal-" +
                                index.ToString("D4"))).Status,
                        Is.EqualTo(
                            UnityBridgeOutcomeDispatchStatus
                                .CallbackInvoked));
                    Assert.That(
                        sender.TryDispatch(
                            index == 0
                                ? firstRetry
                                : CreateSendableReport(
                                    "retry-" +
                                    index.ToString("D4"))).Status,
                        Is.EqualTo(
                            UnityBridgeOutcomeDispatchStatus
                                .PlatformUnavailable));
                }

                Assert.That(
                    sender.TryDispatch(
                        CreateSendableReport("request-overflow"))
                        .Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus
                            .RetentionExhausted));

                adapter.Handler = _ =>
                    UnityBridgePlatformCallbackStatus.CallbackInvoked;
                Assert.That(
                    sender.TryDispatch(firstRetry).Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus
                            .CallbackInvoked));
                Assert.That(
                    sender.TryDispatch(
                        CreateSendableReport("request-still-full"))
                        .Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus
                            .RetentionExhausted));
                Assert.That(
                    adapter.InvocationCount,
                    Is.EqualTo(
                        UnityBridgeOutcomeSender
                            .MaximumRetainedRequestIdentities + 1));
            }
        }

        [Test]
        public void PublishInterfaceContainsPlatformException()
        {
            var adapter = new ScriptedAdapter();
            adapter.Handler = _ =>
                throw new InvalidOperationException("synthetic JNI fault");
            var report = CreateSendableReport(RequestOne);

            using (var sender = CreateSender(adapter))
            {
                Assert.DoesNotThrow(
                    () => ((IUnityBridgeOutcomeSink)sender).Publish(
                        report));
                Assert.That(
                    sender.TryDispatch(report).Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus.Duplicate));
            }
        }

        [Test]
        public void PublishInterfaceReportsTypedUnavailableAndRetainsRetry()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.Unavailable,
                UnityBridgePlatformCallbackStatus.CallbackInvoked);
            var resultSink = new RecordingDispatchResultSink();
            var report = CreateSendableReport(RequestOne);

            using (var sender = new UnityBridgeOutcomeSender(
                       adapter,
                       resultSink))
            {
                ((IUnityBridgeOutcomeSink)sender).Publish(report);

                Assert.That(resultSink.Reports, Has.Count.EqualTo(1));
                Assert.That(
                    resultSink.Reports[0],
                    Is.SameAs(report));
                Assert.That(resultSink.Results, Has.Count.EqualTo(1));
                AssertDispatch(
                    resultSink.Results[0],
                    UnityBridgeOutcomeDispatchStatus.PlatformUnavailable,
                    true,
                    UnityBridgeProtocolErrorCode.SendUnavailable,
                    RequestOne);
                Assert.That(adapter.InvocationCount, Is.EqualTo(1));

                AssertDispatch(
                    sender.TryDispatch(resultSink.Reports[0]),
                    UnityBridgeOutcomeDispatchStatus.CallbackInvoked,
                    false,
                    null,
                    RequestOne);
                Assert.That(adapter.InvocationCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void PublishInterfaceReportsRejectedNonSendableOnce()
        {
            var adapter = new ScriptedAdapter();
            var resultSink = new RecordingDispatchResultSink();
            var report = CreateProtocolFailureReport();

            using (var sender = new UnityBridgeOutcomeSender(
                       adapter,
                       resultSink))
            {
                ((IUnityBridgeOutcomeSink)sender).Publish(report);

                Assert.That(resultSink.Reports, Has.Count.EqualTo(1));
                Assert.That(
                    resultSink.Reports[0],
                    Is.SameAs(report));
                Assert.That(resultSink.Results, Has.Count.EqualTo(1));
                AssertDispatch(
                    resultSink.Results[0],
                    UnityBridgeOutcomeDispatchStatus.RejectedReport,
                    false,
                    UnityBridgeProtocolErrorCode.MalformedJson);
                Assert.That(adapter.InvocationCount, Is.Zero);
            }
        }

        [Test]
        public void ResultSinkExceptionIsContainedAfterExactlyOneReport()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.CallbackInvoked);
            var resultSink = new RecordingDispatchResultSink
            {
                Handler = (_, __) =>
                    throw new InvalidOperationException(
                        "synthetic result observer fault")
            };
            var report = CreateSendableReport(RequestOne);

            using (var sender = new UnityBridgeOutcomeSender(
                       adapter,
                       resultSink))
            {
                Assert.DoesNotThrow(
                    () =>
                        ((IUnityBridgeOutcomeSink)sender).Publish(
                            report));

                Assert.That(resultSink.Reports, Has.Count.EqualTo(1));
                Assert.That(resultSink.Results, Has.Count.EqualTo(1));
                AssertDispatch(
                    resultSink.Results[0],
                    UnityBridgeOutcomeDispatchStatus.CallbackInvoked,
                    false,
                    null,
                    RequestOne);
                Assert.That(adapter.InvocationCount, Is.EqualTo(1));
                Assert.That(
                    sender.TryDispatch(report).Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus.Duplicate));
            }
        }

        [Test]
        public void ResultSinkCannotReenterDispatchAndCanRetryAfterPublish()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.Unavailable,
                UnityBridgePlatformCallbackStatus.CallbackInvoked);
            var resultSink = new RecordingDispatchResultSink();
            var report = CreateSendableReport(RequestOne);
            UnityBridgeOutcomeDispatchResult nested = null;
            UnityBridgeOutcomeSender sender = null;
            resultSink.Handler = (retainedReport, _) =>
                nested = sender.TryDispatch(retainedReport);

            using (sender = new UnityBridgeOutcomeSender(
                       adapter,
                       resultSink))
            {
                ((IUnityBridgeOutcomeSink)sender).Publish(report);

                Assert.That(resultSink.Results, Has.Count.EqualTo(1));
                AssertDispatch(
                    nested,
                    UnityBridgeOutcomeDispatchStatus.Busy,
                    true,
                    UnityBridgeProtocolErrorCode.SendUnavailable);
                Assert.That(adapter.InvocationCount, Is.EqualTo(1));

                AssertDispatch(
                    sender.TryDispatch(resultSink.Reports[0]),
                    UnityBridgeOutcomeDispatchStatus.CallbackInvoked,
                    false,
                    null,
                    RequestOne);
                Assert.That(adapter.InvocationCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void ResultSinkNestedPublishIsSuppressedWithoutRecursion()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.CallbackInvoked,
                UnityBridgePlatformCallbackStatus.CallbackInvoked);
            var resultSink = new RecordingDispatchResultSink();
            var first = CreateSendableReport(RequestOne);
            var second = CreateSendableReport(RequestTwo);
            UnityBridgeOutcomeSender sender = null;
            resultSink.Handler = (_, __) =>
                ((IUnityBridgeOutcomeSink)sender).Publish(second);

            using (sender = new UnityBridgeOutcomeSender(
                       adapter,
                       resultSink))
            {
                ((IUnityBridgeOutcomeSink)sender).Publish(first);

                Assert.That(resultSink.Results, Has.Count.EqualTo(1));
                Assert.That(adapter.InvocationCount, Is.EqualTo(1));
                AssertDispatch(
                    sender.TryDispatch(second),
                    UnityBridgeOutcomeDispatchStatus.CallbackInvoked,
                    false,
                    null,
                    RequestTwo);
                Assert.That(adapter.InvocationCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void ResultSinkDisposalIsDeferredUntilPublishReturns()
        {
            var adapter = new ScriptedAdapter(
                UnityBridgePlatformCallbackStatus.CallbackInvoked);
            var resultSink = new RecordingDispatchResultSink();
            var report = CreateSendableReport(RequestOne);
            UnityBridgeOutcomeSender sender = null;
            var disposeCountDuringCallback = -1;
            resultSink.Handler = (_, __) =>
            {
                sender.Dispose();
                disposeCountDuringCallback = adapter.DisposeCount;
            };

            sender = new UnityBridgeOutcomeSender(adapter, resultSink);
            ((IUnityBridgeOutcomeSink)sender).Publish(report);

            Assert.That(disposeCountDuringCallback, Is.Zero);
            Assert.That(resultSink.Results, Has.Count.EqualTo(1));
            AssertDispatch(
                resultSink.Results[0],
                UnityBridgeOutcomeDispatchStatus.CallbackInvoked,
                false,
                null,
                RequestOne);
            Assert.That(adapter.DisposeCount, Is.EqualTo(1));
            Assert.That(
                sender.TryDispatch(report).Status,
                Is.EqualTo(UnityBridgeOutcomeDispatchStatus.Disposed));
        }

        [Test]
        public void DisposeIsIdempotentAndPreventsLaterDispatch()
        {
            var adapter = new ScriptedAdapter();
            var sender = CreateSender(adapter);

            Assert.DoesNotThrow(sender.Dispose);
            Assert.DoesNotThrow(sender.Dispose);

            Assert.That(adapter.DisposeCount, Is.EqualTo(1));
            AssertDispatch(
                sender.TryDispatch(CreateSendableReport(RequestOne)),
                UnityBridgeOutcomeDispatchStatus.Disposed,
                false,
                UnityBridgeProtocolErrorCode.SessionClosed);
            Assert.That(adapter.InvocationCount, Is.Zero);
        }

        [Test]
        public void DisposeContainsAdapterDisposalException()
        {
            var adapter = new ScriptedAdapter
            {
                ThrowOnDispose = true
            };
            var sender = CreateSender(adapter);

            Assert.DoesNotThrow(sender.Dispose);
            Assert.DoesNotThrow(sender.Dispose);
            Assert.That(adapter.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantDisposeCompletesInvocationThenClosesSender()
        {
            var adapter = new ScriptedAdapter();
            var report = CreateSendableReport(RequestOne);
            UnityBridgeOutcomeSender sender = null;
            adapter.Handler = _ =>
            {
                sender.Dispose();
                return UnityBridgePlatformCallbackStatus.CallbackInvoked;
            };

            sender = CreateSender(adapter);
            var result = sender.TryDispatch(report);

            Assert.That(
                result.Status,
                Is.EqualTo(
                    UnityBridgeOutcomeDispatchStatus.CallbackInvoked));
            Assert.That(adapter.DisposeCount, Is.EqualTo(1));
            Assert.That(
                sender.TryDispatch(report).Status,
                Is.EqualTo(UnityBridgeOutcomeDispatchStatus.Disposed));
        }

        [Test]
        public void CrossThreadDisposeDoesNotDeadlockInFlightInvocation()
        {
            var adapter = new BlockingAdapter();
            var report = CreateSendableReport(RequestOne);
            var senderReady = new ManualResetEventSlim(false);
            UnityBridgeOutcomeSender sender = null;
            UnityBridgeOutcomeDispatchResult result = null;

            var owner = new Thread(
                () =>
                {
                    sender = CreateSender(adapter);
                    senderReady.Set();
                    result = sender.TryDispatch(report);
                });
            owner.Start();

            try
            {
                Assert.That(
                    senderReady.Wait(TimeSpan.FromSeconds(5)),
                    Is.True);
                Assert.That(
                    adapter.Entered.Wait(TimeSpan.FromSeconds(5)),
                    Is.True);

                Assert.DoesNotThrow(sender.Dispose);
                Assert.That(adapter.DisposeCount, Is.Zero);
                adapter.Release.Set();

                Assert.That(
                    owner.Join(TimeSpan.FromSeconds(5)),
                    Is.True);
                Assert.That(
                    result.Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus
                            .CallbackInvoked));
                Assert.That(adapter.DisposeCount, Is.EqualTo(1));
                Assert.That(
                    sender.TryDispatch(report).Status,
                    Is.EqualTo(
                        UnityBridgeOutcomeDispatchStatus.Disposed));
            }
            finally
            {
                adapter.Release.Set();
                if (owner.IsAlive)
                {
                    owner.Join(TimeSpan.FromSeconds(5));
                }
                sender?.Dispose();
                senderReady.Dispose();
                adapter.DisposeSignals();
            }
        }

        private static UnityBridgeReceiverReport CreateSendableReport(
            string requestId,
            string routeId = Route,
            string intent = "preview",
            string capabilitiesJson = "[]")
        {
            var sink = new RecordingSink();
            using (var receiver = new UnityBridgeRouteReceiver(sink))
            {
                receiver.Receive(
                    "{\"contractVersion\":2," +
                    "\"requestId\":\"" + requestId + "\"," +
                    "\"routeId\":\"" + routeId + "\"," +
                    "\"intent\":\"" + intent + "\"," +
                    "\"requestedCapabilities\":" +
                    capabilitiesJson + "}");
            }

            Assert.That(sink.Reports.Count, Is.EqualTo(1));
            Assert.That(sink.Reports[0].IsSendable, Is.True);
            return sink.Reports[0];
        }

        private static UnityBridgeOutcomeSender CreateSender(
            IUnityBridgeOutcomePlatformAdapter adapter)
        {
            return new UnityBridgeOutcomeSender(
                adapter,
                new RecordingDispatchResultSink());
        }

        private static UnityBridgeReceiverReport
            CreateProtocolFailureReport()
        {
            var sink = new RecordingSink();
            using (var receiver = new UnityBridgeRouteReceiver(sink))
            {
                receiver.Receive("{");
            }

            Assert.That(sink.Reports.Count, Is.EqualTo(1));
            Assert.That(sink.Reports[0].IsSendable, Is.False);
            return sink.Reports[0];
        }

        private static UnityBridgeReceiverReport
            CreateMismatchedSendableReport()
        {
            var request = UnityBridgeContract.ParseRequest(
                "{\"contractVersion\":2," +
                "\"requestId\":\"request-0001\"," +
                "\"routeId\":\"bridge.smoke\"," +
                "\"intent\":\"preview\"," +
                "\"requestedCapabilities\":[]}").Request;
            var outcome = new UnityRouteOutcome(
                UnityBridgeContract.ContractVersion,
                RequestTwo,
                Route,
                UnityRouteOutcomeStatus.Unavailable,
                UnityBridgeContract.RouteNotAvailableDiagnostic);
            var constructor = typeof(UnityBridgeReceiverReport)
                .GetConstructors(
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Single();

            return (UnityBridgeReceiverReport)constructor.Invoke(
                new object[]
                {
                    UnityBridgeReceiverReportKind.CorrelatedOutcome,
                    request,
                    outcome,
                    null
                });
        }

        private static void AssertEncodingRejected(
            UnityRouteOutcome outcome,
            UnityBridgeProtocolErrorCode expectedCode,
            string expectedField = null)
        {
            var result = UnityBridgeContract.EncodeOutcome(outcome);

            Assert.That(result.IsEncoded, Is.False);
            Assert.That(result.EncodedJson, Is.Null);
            Assert.That(result.Outcome, Is.Null);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error.Code, Is.EqualTo(expectedCode));
            if (expectedField != null)
            {
                Assert.That(result.Error.Field, Is.EqualTo(expectedField));
            }
        }

        private static void AssertDispatch(
            UnityBridgeOutcomeDispatchResult result,
            UnityBridgeOutcomeDispatchStatus expectedStatus,
            bool canRetry,
            UnityBridgeProtocolErrorCode? expectedError = null,
            string expectedRequestId = null)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.CanRetry, Is.EqualTo(canRetry));
            Assert.That(
                result.RequestId,
                Is.EqualTo(expectedRequestId));
            Assert.That(
                result.CallbackInvoked,
                Is.EqualTo(
                    expectedStatus ==
                    UnityBridgeOutcomeDispatchStatus.CallbackInvoked));
            if (expectedError.HasValue)
            {
                Assert.That(result.Error, Is.Not.Null);
                Assert.That(
                    result.Error.Code,
                    Is.EqualTo(expectedError.Value));
            }
            else
            {
                Assert.That(result.Error, Is.Null);
            }
        }

        private sealed class RecordingSink : IUnityBridgeOutcomeSink
        {
            internal List<UnityBridgeReceiverReport> Reports { get; } =
                new List<UnityBridgeReceiverReport>();

            public void Publish(UnityBridgeReceiverReport report)
            {
                Reports.Add(report);
            }
        }

        private sealed class RecordingDispatchResultSink :
            IUnityBridgeOutcomeDispatchResultSink
        {
            internal Action<
                UnityBridgeReceiverReport,
                UnityBridgeOutcomeDispatchResult> Handler { get; set; }
            internal List<UnityBridgeReceiverReport> Reports { get; } =
                new List<UnityBridgeReceiverReport>();
            internal List<UnityBridgeOutcomeDispatchResult> Results
            {
                get;
            } = new List<UnityBridgeOutcomeDispatchResult>();

            public void Publish(
                UnityBridgeReceiverReport report,
                UnityBridgeOutcomeDispatchResult result)
            {
                Reports.Add(report);
                Results.Add(result);
                Handler?.Invoke(report, result);
            }
        }

        private sealed class ScriptedAdapter :
            IUnityBridgeOutcomePlatformAdapter
        {
            private readonly Queue<UnityBridgePlatformCallbackStatus>
                statuses =
                    new Queue<UnityBridgePlatformCallbackStatus>();

            internal ScriptedAdapter(
                params UnityBridgePlatformCallbackStatus[] statuses)
            {
                foreach (var status in statuses)
                {
                    this.statuses.Enqueue(status);
                }
            }

            internal Func<string, UnityBridgePlatformCallbackStatus>
                Handler { get; set; }
            internal List<string> Messages { get; } =
                new List<string>();
            internal int InvocationCount { get; private set; }
            internal int DisposeCount { get; private set; }
            internal bool ThrowOnDispose { get; set; }

            public UnityBridgePlatformCallbackStatus TryReportOutcome(
                string encodedOutcome)
            {
                InvocationCount++;
                Messages.Add(encodedOutcome);
                if (Handler != null)
                {
                    return Handler(encodedOutcome);
                }

                return statuses.Count == 0
                    ? UnityBridgePlatformCallbackStatus.CallbackInvoked
                    : statuses.Dequeue();
            }

            public void Dispose()
            {
                DisposeCount++;
                if (ThrowOnDispose)
                {
                    throw new InvalidOperationException(
                        "synthetic disposal fault");
                }
            }
        }

        private sealed class BlockingAdapter :
            IUnityBridgeOutcomePlatformAdapter
        {
            internal ManualResetEventSlim Entered { get; } =
                new ManualResetEventSlim(false);
            internal ManualResetEventSlim Release { get; } =
                new ManualResetEventSlim(false);
            internal int DisposeCount { get; private set; }

            public UnityBridgePlatformCallbackStatus TryReportOutcome(
                string encodedOutcome)
            {
                Entered.Set();
                if (!Release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException(
                        "Synthetic platform adapter was not released.");
                }

                return UnityBridgePlatformCallbackStatus.CallbackInvoked;
            }

            public void Dispose()
            {
                DisposeCount++;
            }

            internal void DisposeSignals()
            {
                Entered.Dispose();
                Release.Dispose();
            }
        }
    }
}
