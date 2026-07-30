using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using AL.Platform.Android;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.AndroidBridge
{
    public sealed class UnityBridgeReceiverContractTests
    {
        private const string RequestOne = "request-0001";
        private const string RequestTwo = "request-0002";
        private const string Route = "bridge.smoke";

        private GameObject bridgeObject;

        [TearDown]
        public void TearDown()
        {
            if (bridgeObject != null)
            {
                UnityEngine.Object.DestroyImmediate(bridgeObject);
                bridgeObject = null;
            }
        }

        [Test]
        public void ContractConstantsWireValuesAndDiagnosticsMatchAndroidV2()
        {
            Assert.That(UnityBridgeContract.ContractVersion, Is.EqualTo(2));
            Assert.That(
                UnityBridgeContract.MaximumMessageBytes,
                Is.EqualTo(32 * 1024));
            Assert.That(
                UnityBridgeContract.MaximumPayloadBytes,
                Is.EqualTo(16 * 1024));
            Assert.That(
                UnityBridgeContract.MaximumCapabilities,
                Is.EqualTo(16));
            Assert.That(
                UnityBridgeContract.MaximumRouteIdLength,
                Is.EqualTo(64));
            Assert.That(
                UnityBridgeContract.MaximumRequestIdLength,
                Is.EqualTo(128));
            Assert.That(
                UnityBridgeContract.MaximumResultIdLength,
                Is.EqualTo(128));
            Assert.That(
                UnityBridgeContract.MaximumDiagnosticCodeLength,
                Is.EqualTo(64));
            Assert.That(
                UnityBridgeContract.RouteNotAvailableDiagnostic,
                Is.EqualTo("route.not_available"));
            Assert.That(
                UnityBridgeRouteReceiver.MaximumRetainedRequestIdentities,
                Is.EqualTo(256));
            Assert.That(
                UnityBridgeRouteReceiver.MaximumPendingReports,
                Is.EqualTo(64));
            Assert.That(
                UnityBridgeContract.GetIntentWireValue(
                    UnityRouteIntent.Preview),
                Is.EqualTo("preview"));
            Assert.That(
                UnityBridgeContract.GetIntentWireValue(
                    UnityRouteIntent.Authoritative),
                Is.EqualTo("authoritative"));

            var statusValues = new[]
            {
                "success",
                "failure",
                "cancelled",
                "unavailable"
            };
            foreach (UnityRouteOutcomeStatus status in
                     Enum.GetValues(typeof(UnityRouteOutcomeStatus)))
            {
                Assert.That(
                    UnityBridgeContract.GetOutcomeStatusWireValue(status),
                    Is.EqualTo(statusValues[(int)status]));
            }

            var diagnosticValues = new[]
            {
                "bridge.null_message",
                "bridge.empty_message",
                "bridge.message_too_large",
                "bridge.malformed_json",
                "bridge.duplicate_field",
                "bridge.unexpected_field",
                "bridge.missing_field",
                "bridge.invalid_contract_version",
                "bridge.invalid_request_id",
                "bridge.invalid_route_id",
                "bridge.invalid_intent",
                "bridge.too_many_capabilities",
                "bridge.invalid_capability",
                "bridge.duplicate_capability",
                "bridge.invalid_status",
                "bridge.invalid_diagnostic_code",
                "bridge.invalid_result_id",
                "bridge.missing_result_id",
                "bridge.payload_too_large",
                "bridge.missing_diagnostic_code",
                "bridge.no_active_request",
                "bridge.request_mismatch",
                "bridge.route_mismatch",
                "bridge.duplicate_outcome",
                "bridge.session_closed",
                "bridge.send_unavailable"
            };
            foreach (UnityBridgeProtocolErrorCode code in
                     Enum.GetValues(typeof(UnityBridgeProtocolErrorCode)))
            {
                Assert.That(
                    UnityBridgeContract.GetProtocolErrorWireValue(code),
                    Is.EqualTo(diagnosticValues[(int)code]));
            }
        }

        [Test]
        public void RequestParsesExactFieldsEscapesIntentAndCapabilities()
        {
            var result = UnityBridgeContract.ParseRequest(
                "{\"requestedCapabilities\":[\"route.acknowledge\"," +
                "\"route.\\u0063ancel\"],\"intent\":\"authoritative\"," +
                "\"route\\u0049d\":\"bridge.\\u0073moke\"," +
                "\"request\\u0049d\":\"request\\u002d0001\"," +
                "\"contractVersion\":2}");

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.Error, Is.Null);
            Assert.That(result.Request.ContractVersion, Is.EqualTo(2));
            Assert.That(result.Request.RequestId, Is.EqualTo(RequestOne));
            Assert.That(result.Request.RouteId, Is.EqualTo(Route));
            Assert.That(
                result.Request.Intent,
                Is.EqualTo(UnityRouteIntent.Authoritative));
            CollectionAssert.AreEqual(
                new[] { "route.acknowledge", "route.cancel" },
                result.Request.RequestedCapabilities);
        }

        [Test]
        public void RequestRejectsNullBlankMalformedAndNonObjectInputs()
        {
            AssertRejected(
                UnityBridgeContract.ParseRequest(null),
                UnityBridgeProtocolErrorCode.NullMessage);
            AssertRejected(
                UnityBridgeContract.ParseRequest(" \t\r\n"),
                UnityBridgeProtocolErrorCode.EmptyMessage);

            var malformed = new[]
            {
                "{",
                "[]",
                "null",
                ValidRequestJson() + "false",
                ValidRequestJson().TrimEnd('}') + ",}",
                ValidRequestJson().Replace(
                    "\"intent\":\"preview\"",
                    "\"intent\":/*comment*/\"preview\"")
            };
            foreach (var fixture in malformed)
            {
                AssertRejected(
                    UnityBridgeContract.ParseRequest(fixture),
                    UnityBridgeProtocolErrorCode.MalformedJson);
            }
        }

        [Test]
        public void RequestEnforcesExactUtf8MessageBoundary()
        {
            var valid = ValidRequestJson();
            var exact = valid + new string(
                ' ',
                UnityBridgeContract.MaximumMessageBytes - valid.Length);

            Assert.That(
                UnityBridgeContract.ParseRequest(exact).IsAccepted,
                Is.True);
            AssertRejected(
                UnityBridgeContract.ParseRequest(exact + " "),
                UnityBridgeProtocolErrorCode.MessageTooLarge);

            var exactMultibyte = new string(
                '\u00e9',
                UnityBridgeContract.MaximumMessageBytes / 2);
            AssertRejected(
                UnityBridgeContract.ParseRequest(exactMultibyte),
                UnityBridgeProtocolErrorCode.MalformedJson);
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    exactMultibyte + "\u00e9"),
                UnityBridgeProtocolErrorCode.MessageTooLarge);
        }

        [Test]
        public void RequestRejectsRawAndEscapedUnpairedSurrogates()
        {
            var raw = ValidRequestJson().Replace(
                RequestOne,
                "request-\ud800");
            var escaped = ValidRequestJson().Replace(
                RequestOne,
                "request-\\uD800");

            AssertRejected(
                UnityBridgeContract.ParseRequest(raw),
                UnityBridgeProtocolErrorCode.MalformedJson);
            AssertRejected(
                UnityBridgeContract.ParseRequest(escaped),
                UnityBridgeProtocolErrorCode.MalformedJson);
        }

        [TestCase(
            "contractVersion",
            "contract\\u0056ersion",
            "2",
            "1")]
        [TestCase(
            "requestId",
            "request\\u0049d",
            "\"request-0001\"",
            "\"request-0002\"")]
        [TestCase(
            "routeId",
            "route\\u0049d",
            "\"bridge.smoke\"",
            "\"bridge.other\"")]
        [TestCase(
            "intent",
            "in\\u0074ent",
            "\"preview\"",
            "\"authoritative\"")]
        [TestCase(
            "requestedCapabilities",
            "requestedCapabil\\u0069ties",
            "[]",
            "[\"route.cancel\"]")]
        public void RequestRejectsDecodedDuplicateKnownMembers(
            string canonical,
            string escaped,
            string firstValue,
            string secondValue)
        {
            var result = UnityBridgeContract.ParseRequest(
                DuplicateRequestJson(
                    canonical,
                    escaped,
                    firstValue,
                    secondValue));

            AssertRejected(
                result,
                UnityBridgeProtocolErrorCode.DuplicateField,
                canonical);
        }

        [Test]
        public void RequestRequiresDuplicateMemberColonBeforeDuplicateDiagnostic()
        {
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    "{\"contractVersion\":2," +
                    "\"requestId\":\"request-0001\"," +
                    "\"requestId\"}"),
                UnityBridgeProtocolErrorCode.MalformedJson);
        }

        [Test]
        public void RequestRejectsUnexpectedAndCaseVariantFields()
        {
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson().TrimEnd('}') +
                    ",\"unknown\":true}"),
                UnityBridgeProtocolErrorCode.UnexpectedField,
                "unknown");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson().Replace(
                        "\"routeId\"",
                        "\"RouteId\"")),
                UnityBridgeProtocolErrorCode.UnexpectedField,
                "RouteId");
        }

        [TestCase(
            "contractVersion",
            UnityBridgeProtocolErrorCode.MissingField)]
        [TestCase("requestId", UnityBridgeProtocolErrorCode.MissingField)]
        [TestCase("routeId", UnityBridgeProtocolErrorCode.MissingField)]
        [TestCase("intent", UnityBridgeProtocolErrorCode.MissingField)]
        [TestCase(
            "requestedCapabilities",
            UnityBridgeProtocolErrorCode.MissingField)]
        public void RequestRequiresEveryContractMember(
            string omittedField,
            UnityBridgeProtocolErrorCode expected)
        {
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    RequestWithout(omittedField)),
                expected,
                omittedField);
        }

        [Test]
        public void RequestRejectsWrongFieldTypesInAndroidOrder()
        {
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson().Replace(
                        "\"contractVersion\":2",
                        "\"contractVersion\":\"2\"")),
                UnityBridgeProtocolErrorCode.InvalidContractVersion,
                "contractVersion");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson().Replace(
                        "\"requestId\":\"request-0001\"",
                        "\"requestId\":false")),
                UnityBridgeProtocolErrorCode.MissingField,
                "requestId");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson().Replace(
                        "\"routeId\":\"bridge.smoke\"",
                        "\"routeId\":null")),
                UnityBridgeProtocolErrorCode.MissingField,
                "routeId");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson().Replace(
                        "\"intent\":\"preview\"",
                        "\"intent\":[]")),
                UnityBridgeProtocolErrorCode.InvalidIntent,
                "intent");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson().Replace(
                        "\"requestedCapabilities\":[]",
                        "\"requestedCapabilities\":{}")),
                UnityBridgeProtocolErrorCode.InvalidCapability,
                "requestedCapabilities");
        }

        [Test]
        public void RequestRequiresExactVersionAndIntentWithoutNormalization()
        {
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson().Replace(
                        "\"contractVersion\":2",
                        "\"contractVersion\":2.0")),
                UnityBridgeProtocolErrorCode.InvalidContractVersion,
                "contractVersion");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson().Replace(
                        "\"contractVersion\":2",
                        "\"contractVersion\":1")),
                UnityBridgeProtocolErrorCode.InvalidContractVersion,
                "contractVersion");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson().Replace(
                        "\"intent\":\"preview\"",
                        "\"intent\":\"Preview\"")),
                UnityBridgeProtocolErrorCode.InvalidIntent,
                "intent");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson().Replace(
                        "\"intent\":\"preview\"",
                        "\"intent\":\" preview\"")),
                UnityBridgeProtocolErrorCode.InvalidIntent,
                "intent");
        }

        [Test]
        public void RequestEnforcesExactAsciiStableIdShapesAndLengths()
        {
            var maximumRequestId =
                "r" + new string(
                    'a',
                    UnityBridgeContract.MaximumRequestIdLength - 1);
            var maximumRouteId =
                "r" + new string(
                    'a',
                    UnityBridgeContract.MaximumRouteIdLength - 1);
            Assert.That(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(
                        maximumRequestId,
                        maximumRouteId)).IsAccepted,
                Is.True);

            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(
                        maximumRequestId + "a",
                        Route)),
                UnityBridgeProtocolErrorCode.InvalidRequestId,
                "requestId");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(
                        RequestOne,
                        maximumRouteId + "a")),
                UnityBridgeProtocolErrorCode.InvalidRouteId,
                "routeId");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson("r\u00e9quest", Route)),
                UnityBridgeProtocolErrorCode.InvalidRequestId,
                "requestId");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(RequestOne, "1bridge.smoke")),
                UnityBridgeProtocolErrorCode.InvalidRouteId,
                "routeId");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(RequestOne, "../bridge")),
                UnityBridgeProtocolErrorCode.InvalidRouteId,
                "routeId");

            Assert.That(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson("REQUEST:ONE", "Bridge.Smoke"))
                    .IsAccepted,
                Is.True);
        }

        [Test]
        public void RequestEnforcesCapabilityCountTypeShapeAndUniqueness()
        {
            var sixteen = Enumerable.Range(0, 16)
                .Select(index => "route.cap" + index)
                .ToArray();
            var seventeen = Enumerable.Range(0, 17)
                .Select(index => "route.cap" + index)
                .ToArray();

            Assert.That(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(
                        capabilitiesJson:
                        CapabilityArrayJson(sixteen))).IsAccepted,
                Is.True);
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(
                        capabilitiesJson:
                        CapabilityArrayJson(seventeen))),
                UnityBridgeProtocolErrorCode.TooManyCapabilities,
                "requestedCapabilities");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(
                        capabilitiesJson: "[\"route.cancel\",null]")),
                UnityBridgeProtocolErrorCode.InvalidCapability,
                "requestedCapabilities[1]");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(
                        capabilitiesJson: "[\"route.cancel\",\"\"]")),
                UnityBridgeProtocolErrorCode.InvalidCapability,
                "requestedCapabilities[1]");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(
                        capabilitiesJson: "[\"route.cancel\",\"bad:cap\"]")),
                UnityBridgeProtocolErrorCode.InvalidCapability,
                "requestedCapabilities[1]");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(
                        capabilitiesJson:
                        "[\"route.cancel\",\"route.\\u0063ancel\"]")),
                UnityBridgeProtocolErrorCode.DuplicateCapability,
                "requestedCapabilities");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(
                        capabilitiesJson:
                        "[\"route.cancel\",\"route.cancel\",\"bad:cap\"]")),
                UnityBridgeProtocolErrorCode.InvalidCapability,
                "requestedCapabilities[2]");
            AssertRejected(
                UnityBridgeContract.ParseRequest(
                    ValidRequestJson(
                        capabilitiesJson: "[\"bad:cap\",null]")),
                UnityBridgeProtocolErrorCode.InvalidCapability,
                "requestedCapabilities[1]");
        }

        [Test]
        public void DtoValidationBoundsCallerControlledCapabilitiesBeforeRejecting()
        {
            var callerCapabilities = Enumerable.Range(0, 4096)
                .Select(index => "route.cap" + index)
                .ToArray();
            var request = new UnityRouteRequest(
                UnityBridgeContract.ContractVersion,
                RequestOne,
                Route,
                UnityRouteIntent.Preview,
                callerCapabilities);

            Assert.That(
                request.RequestedCapabilities.Count,
                Is.EqualTo(UnityBridgeContract.MaximumCapabilities + 1));
            AssertRejected(
                UnityBridgeContract.ValidateRequest(request),
                UnityBridgeProtocolErrorCode.TooManyCapabilities,
                "requestedCapabilities");

            AssertRejected(
                UnityBridgeContract.ValidateRequest(
                    new UnityRouteRequest(
                        UnityBridgeContract.ContractVersion,
                        RequestOne,
                        Route,
                        UnityRouteIntent.Preview,
                        null)),
                UnityBridgeProtocolErrorCode.MissingField,
                "requestedCapabilities");
            AssertRejected(
                UnityBridgeContract.ValidateRequest(
                    new UnityRouteRequest(
                        UnityBridgeContract.ContractVersion,
                        RequestOne,
                        Route,
                        UnityRouteIntent.Preview,
                        new[] { "bad:cap", null })),
                UnityBridgeProtocolErrorCode.InvalidCapability,
                "requestedCapabilities[1]");
        }

        [Test]
        public void OutcomeValidationMatchesStatusDiagnosticAndIdRules()
        {
            Assert.That(
                UnityBridgeContract.ValidateOutcome(
                    UnavailableOutcome()).IsAccepted,
                Is.True);
            Assert.That(
                UnityBridgeContract.ValidateOutcome(
                    new UnityRouteOutcome(
                        2,
                        RequestOne,
                        Route,
                        UnityRouteOutcomeStatus.Success)).IsAccepted,
                Is.True);

            AssertOutcomeRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Failure),
                UnityBridgeProtocolErrorCode.MissingDiagnosticCode,
                "diagnosticCode");
            AssertOutcomeRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Unavailable,
                    "Route.NotAvailable"),
                UnityBridgeProtocolErrorCode.InvalidDiagnosticCode,
                "diagnosticCode");
            AssertOutcomeRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Unavailable,
                    " "),
                UnityBridgeProtocolErrorCode.MissingField,
                "diagnosticCode");
            AssertOutcomeRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    (UnityRouteOutcomeStatus)99),
                UnityBridgeProtocolErrorCode.InvalidStatus,
                "status");
            AssertOutcomeRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Success,
                    resultId: "bad/result"),
                UnityBridgeProtocolErrorCode.InvalidResultId,
                "resultId");
        }

        [Test]
        public void OutcomeValidationEnforcesPayloadAndCompleteMessageBounds()
        {
            Assert.That(
                UnityBridgeContract.ValidateOutcome(
                    new UnityRouteOutcome(
                        2,
                        RequestOne,
                        Route,
                        UnityRouteOutcomeStatus.Success,
                        payload: new string(
                            'x',
                            UnityBridgeContract.MaximumPayloadBytes)))
                    .IsAccepted,
                Is.True);
            var exactEncodedLimitPayload =
                new string('"', 16330) + "xxx";
            Assert.That(
                UnityBridgeContract.ValidateOutcome(
                    new UnityRouteOutcome(
                        2,
                        RequestOne,
                        Route,
                        UnityRouteOutcomeStatus.Success,
                        payload: exactEncodedLimitPayload)).IsAccepted,
                Is.True);
            AssertOutcomeRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Success,
                    payload: exactEncodedLimitPayload + "x"),
                UnityBridgeProtocolErrorCode.MessageTooLarge);
            AssertOutcomeRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Success,
                    payload: new string(
                        'x',
                        UnityBridgeContract.MaximumPayloadBytes + 1)),
                UnityBridgeProtocolErrorCode.PayloadTooLarge,
                "payload");
            AssertOutcomeRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Success,
                    payload: new string(
                        '"',
                        UnityBridgeContract.MaximumPayloadBytes)),
                UnityBridgeProtocolErrorCode.MessageTooLarge);
            AssertOutcomeRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Success,
                    payload: string.Empty),
                UnityBridgeProtocolErrorCode.MissingField,
                "payload");
            Assert.That(
                UnityBridgeContract.ValidateOutcome(
                    new UnityRouteOutcome(
                        2,
                        RequestOne,
                        Route,
                        UnityRouteOutcomeStatus.Success,
                        payload: "x" + new string('\n', 6000))).IsAccepted,
                Is.True);
            AssertOutcomeRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Success,
                    diagnosticCode: new string(
                        ' ',
                        UnityBridgeContract.MaximumMessageBytes + 1)),
                UnityBridgeProtocolErrorCode.MessageTooLarge);
            AssertOutcomeRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Success,
                    resultId: new string(
                        ' ',
                        UnityBridgeContract.MaximumMessageBytes + 1)),
                UnityBridgeProtocolErrorCode.MessageTooLarge);
            AssertOutcomeRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Success,
                    payload: new string(
                        ' ',
                        UnityBridgeContract.MaximumMessageBytes + 1)),
                UnityBridgeProtocolErrorCode.MessageTooLarge);
        }

        [Test]
        public void OutcomeContextEnforcesExactCorrelationAndAuthoritativeReceipt()
        {
            var previewRequest =
                UnityBridgeContract.ParseRequest(ValidRequestJson()).Request;
            var authoritativeRequest = UnityBridgeContract.ParseRequest(
                ValidRequestJson(intent: "authoritative")).Request;

            AssertOutcomeContextRejected(
                new UnityRouteOutcome(
                    2,
                    RequestTwo,
                    Route,
                    UnityRouteOutcomeStatus.Success),
                previewRequest,
                UnityBridgeProtocolErrorCode.RequestMismatch,
                "requestId");
            AssertOutcomeContextRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    "bridge.other",
                    UnityRouteOutcomeStatus.Success),
                previewRequest,
                UnityBridgeProtocolErrorCode.RouteMismatch,
                "routeId");
            AssertOutcomeContextRejected(
                new UnityRouteOutcome(
                    2,
                    RequestOne,
                    Route,
                    UnityRouteOutcomeStatus.Success),
                authoritativeRequest,
                UnityBridgeProtocolErrorCode.MissingResultId,
                "resultId");
            Assert.That(
                UnityBridgeContract.ValidateOutcomeForRequest(
                    new UnityRouteOutcome(
                        2,
                        RequestOne,
                        Route,
                        UnityRouteOutcomeStatus.Success,
                        resultId: "result-0001"),
                    authoritativeRequest).IsAccepted,
                Is.True);
        }

        [Test]
        public void ReceiverReportsEveryInvalidCallOnceWithoutConsumingIdentity()
        {
            var sink = new RecordingSink();
            using (var receiver = new UnityBridgeRouteReceiver(sink))
            {
                receiver.Receive(null);
                receiver.Receive(" ");
                receiver.Receive("{");
                receiver.Receive(
                    ValidRequestJson(intent: "Preview"));
                receiver.Receive(ValidRequestJson());
            }

            Assert.That(sink.Reports.Count, Is.EqualTo(5));
            AssertFailure(
                sink.Reports[0],
                UnityBridgeProtocolErrorCode.NullMessage);
            AssertFailure(
                sink.Reports[1],
                UnityBridgeProtocolErrorCode.EmptyMessage);
            AssertFailure(
                sink.Reports[2],
                UnityBridgeProtocolErrorCode.MalformedJson);
            AssertFailure(
                sink.Reports[3],
                UnityBridgeProtocolErrorCode.InvalidIntent);
            AssertUnavailable(
                sink.Reports[4],
                RequestOne,
                Route);
        }

        [TestCase("preview")]
        [TestCase("authoritative")]
        public void ReceiverReturnsCorrelatedUnavailableForEveryValidUnknownRoute(
            string intent)
        {
            var sink = new RecordingSink();
            using (var receiver = new UnityBridgeRouteReceiver(sink))
            {
                receiver.Receive(
                    ValidRequestJson(
                        requestId: RequestOne,
                        routeId: "unknown.route",
                        intent: intent,
                        capabilitiesJson:
                        "[\"route.acknowledge\",\"route.cancel\"]"));
            }

            Assert.That(sink.Reports.Count, Is.EqualTo(1));
            AssertUnavailable(
                sink.Reports[0],
                RequestOne,
                "unknown.route");
            CollectionAssert.AreEqual(
                new[] { "route.acknowledge", "route.cancel" },
                sink.Reports[0].Request.RequestedCapabilities);
        }

        [Test]
        public void ReceiverSuppressesExactReplayWithoutSecondWireOutcome()
        {
            var sink = new RecordingSink();
            using (var receiver = new UnityBridgeRouteReceiver(sink))
            {
                var payload = ValidRequestJson();
                receiver.Receive(payload);
                receiver.Receive(payload);
                receiver.Receive(ValidRequestJson(RequestTwo));
            }

            Assert.That(sink.Reports.Count, Is.EqualTo(3));
            AssertUnavailable(sink.Reports[0], RequestOne, Route);
            AssertFailure(
                sink.Reports[1],
                UnityBridgeProtocolErrorCode.DuplicateOutcome,
                expectedRequestId: RequestOne,
                expectedRouteId: Route);
            AssertUnavailable(sink.Reports[2], RequestTwo, Route);
            Assert.That(
                sink.Reports.Count(report => report.IsSendable),
                Is.EqualTo(2));
        }

        [Test]
        public void ReceiverAcceptsNewIdentityForSameRouteAndRejectsStaleReplay()
        {
            var sink = new RecordingSink();
            using (var receiver = new UnityBridgeRouteReceiver(sink))
            {
                receiver.Receive(ValidRequestJson(RequestOne));
                receiver.Receive(ValidRequestJson(RequestTwo));
                receiver.Receive(ValidRequestJson(RequestOne));
            }

            Assert.That(sink.Reports.Count, Is.EqualTo(3));
            AssertUnavailable(sink.Reports[0], RequestOne, Route);
            AssertUnavailable(sink.Reports[1], RequestTwo, Route);
            AssertFailure(
                sink.Reports[2],
                UnityBridgeProtocolErrorCode.RequestMismatch,
                "requestId",
                RequestOne,
                Route);
            Assert.That(
                sink.Reports.Count(report => report.IsSendable),
                Is.EqualTo(2));
        }

        [Test]
        public void ReceiverRejectsReusedIdentityWithAlteredEnvelope()
        {
            var sink = new RecordingSink();
            using (var receiver = new UnityBridgeRouteReceiver(sink))
            {
                receiver.Receive(ValidRequestJson());
                receiver.Receive(
                    ValidRequestJson(routeId: "bridge.other"));
                receiver.Receive(
                    ValidRequestJson(intent: "authoritative"));
                receiver.Receive(
                    ValidRequestJson(
                        capabilitiesJson: "[\"route.cancel\"]"));
            }

            Assert.That(sink.Reports.Count, Is.EqualTo(4));
            AssertUnavailable(sink.Reports[0], RequestOne, Route);
            AssertFailure(
                sink.Reports[1],
                UnityBridgeProtocolErrorCode.RouteMismatch,
                "routeId",
                RequestOne,
                "bridge.other");
            AssertFailure(
                sink.Reports[2],
                UnityBridgeProtocolErrorCode.RequestMismatch,
                "requestId",
                RequestOne,
                Route);
            AssertFailure(
                sink.Reports[3],
                UnityBridgeProtocolErrorCode.RequestMismatch,
                "requestId",
                RequestOne,
                Route);
            Assert.That(
                sink.Reports.Count(report => report.IsSendable),
                Is.EqualTo(1));
        }

        [Test]
        public void ReceiverClosesAtBoundedIdentityCapacityWithoutEviction()
        {
            var sink = new RecordingSink();
            using (var receiver = new UnityBridgeRouteReceiver(sink))
            {
                for (var index = 0;
                     index <
                     UnityBridgeRouteReceiver
                         .MaximumRetainedRequestIdentities;
                     index++)
                {
                    receiver.Receive(
                        ValidRequestJson(
                            "request-" + index.ToString("D4")));
                }

                receiver.Receive(ValidRequestJson("request-overflow"));
                receiver.Receive(ValidRequestJson("request-after-close"));
            }

            Assert.That(
                sink.Reports.Count(report => report.IsSendable),
                Is.EqualTo(
                    UnityBridgeRouteReceiver
                        .MaximumRetainedRequestIdentities));
            Assert.That(
                sink.Reports.Count,
                Is.EqualTo(
                    UnityBridgeRouteReceiver
                        .MaximumRetainedRequestIdentities + 2));
            AssertFailure(
                sink.Reports[
                    UnityBridgeRouteReceiver
                        .MaximumRetainedRequestIdentities],
                UnityBridgeProtocolErrorCode.SessionClosed,
                expectedRequestId: "request-overflow",
                expectedRouteId: Route);
            AssertFailure(
                sink.Reports[
                    UnityBridgeRouteReceiver
                        .MaximumRetainedRequestIdentities + 1],
                UnityBridgeProtocolErrorCode.SessionClosed);
        }

        [Test]
        public void ReceiverCloseAndDisposeAreIdempotentAndDeterministic()
        {
            var sink = new RecordingSink();
            var receiver = new UnityBridgeRouteReceiver(sink);

            receiver.Close();
            receiver.Close();
            receiver.Receive(ValidRequestJson());
            Assert.That(sink.Reports.Count, Is.EqualTo(1));
            AssertFailure(
                sink.Reports[0],
                UnityBridgeProtocolErrorCode.SessionClosed);

            receiver.Dispose();
            receiver.Dispose();
            receiver.Receive(ValidRequestJson(RequestTwo));
            Assert.That(sink.Reports.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReceiverContainsThrowingAndReentrantSinks()
        {
            var throwingSink = new RecordingSink { ThrowAfterRecord = true };
            using (var receiver =
                   new UnityBridgeRouteReceiver(throwingSink))
            {
                Assert.DoesNotThrow(
                    () => receiver.Receive(ValidRequestJson()));
                Assert.DoesNotThrow(
                    () => receiver.Receive(ValidRequestJson()));
            }

            Assert.That(throwingSink.Reports.Count, Is.EqualTo(2));
            AssertUnavailable(
                throwingSink.Reports[0],
                RequestOne,
                Route);
            AssertFailure(
                throwingSink.Reports[1],
                UnityBridgeProtocolErrorCode.DuplicateOutcome,
                expectedRequestId: RequestOne,
                expectedRouteId: Route);

            var throwBeforeRecordSink = new ThrowBeforeRecordSink();
            using (var receiver =
                   new UnityBridgeRouteReceiver(throwBeforeRecordSink))
            {
                receiver.Receive(ValidRequestJson());
                receiver.Receive(ValidRequestJson());
            }

            Assert.That(throwBeforeRecordSink.PublishAttempts, Is.EqualTo(2));
            Assert.That(throwBeforeRecordSink.Reports.Count, Is.EqualTo(1));
            AssertFailure(
                throwBeforeRecordSink.Reports[0],
                UnityBridgeProtocolErrorCode.DuplicateOutcome,
                expectedRequestId: RequestOne,
                expectedRouteId: Route);

            var reentrantSink = new ReentrantSink();
            using (var receiver =
                   new UnityBridgeRouteReceiver(reentrantSink))
            {
                reentrantSink.Receiver = receiver;
                reentrantSink.Payload = ValidRequestJson();
                Assert.DoesNotThrow(
                    () => receiver.Receive(reentrantSink.Payload));
            }

            Assert.That(reentrantSink.Reports.Count, Is.EqualTo(2));
            AssertUnavailable(
                reentrantSink.Reports[0],
                RequestOne,
                Route);
            AssertFailure(
                reentrantSink.Reports[1],
                UnityBridgeProtocolErrorCode.DuplicateOutcome,
                expectedRequestId: RequestOne,
                expectedRouteId: Route);
        }

        [Test]
        public void ReceiverPublishesOutsideStateLockWithoutDeadlock()
        {
            var sink = new CoordinatingSink
            {
                SecondPayload = ValidRequestJson(RequestTwo)
            };
            using (var receiver = new UnityBridgeRouteReceiver(sink))
            {
                sink.Receiver = receiver;
                receiver.Receive(ValidRequestJson(RequestOne));
            }

            Assert.That(sink.WorkerCompletedDuringFirstPublish, Is.True);
            Assert.That(sink.Reports.Count, Is.EqualTo(2));
            AssertUnavailable(sink.Reports[0], RequestOne, Route);
            AssertUnavailable(sink.Reports[1], RequestTwo, Route);
        }

        [Test]
        public void ReceiverBoundsPendingReentrantReportsAndClosesFailClosed()
        {
            var sink = new BurstReentrantSink
            {
                Payload = ValidRequestJson()
            };
            using (var receiver = new UnityBridgeRouteReceiver(sink))
            {
                sink.Receiver = receiver;
                receiver.Receive(sink.Payload);
                receiver.Receive(ValidRequestJson(RequestTwo));
            }

            var expectedReports =
                UnityBridgeRouteReceiver.MaximumPendingReports + 2;
            Assert.That(
                sink.ReceiveCallCount,
                Is.EqualTo(
                    UnityBridgeRouteReceiver.MaximumPendingReports + 1));
            Assert.That(sink.Reports.Count, Is.EqualTo(expectedReports));
            Assert.That(sink.MaximumPublishDepth, Is.EqualTo(1));
            AssertUnavailable(sink.Reports[0], RequestOne, Route);
            AssertFailure(
                sink.Reports[sink.Reports.Count - 1],
                UnityBridgeProtocolErrorCode.SessionClosed,
                expectedRequestId: RequestOne,
                expectedRouteId: Route);
            Assert.That(
                sink.Reports.Count(report => report.IsSendable),
                Is.EqualTo(1));
            Assert.That(
                sink.Reports.Count(
                    report =>
                        report.Error != null &&
                        report.Error.Code ==
                        UnityBridgeProtocolErrorCode.SessionClosed),
                Is.EqualTo(1));
            Assert.That(
                sink.Reports.Count(
                    report =>
                        report.Error != null &&
                        report.Error.Code ==
                        UnityBridgeProtocolErrorCode.DuplicateOutcome),
                Is.EqualTo(
                    UnityBridgeRouteReceiver.MaximumPendingReports));
        }

        [Test]
        public void ReceiverDispatchBudgetEmitsTerminalFailureBeforeTeardown()
        {
            var sink = new ContinuousReentrantSink
            {
                Payload = ValidRequestJson()
            };
            using (var receiver = new UnityBridgeRouteReceiver(sink))
            {
                sink.Receiver = receiver;
                receiver.Receive(sink.Payload);
                receiver.Receive(ValidRequestJson(RequestTwo));
            }

            Assert.That(
                sink.ReceiveCallCount,
                Is.EqualTo(
                    UnityBridgeRouteReceiver.MaximumPendingReports + 2));
            Assert.That(
                sink.Reports.Count,
                Is.EqualTo(
                    UnityBridgeRouteReceiver.MaximumPendingReports + 2));
            Assert.That(sink.MaximumPublishDepth, Is.EqualTo(1));
            AssertUnavailable(sink.Reports[0], RequestOne, Route);
            AssertFailure(
                sink.Reports[sink.Reports.Count - 1],
                UnityBridgeProtocolErrorCode.SessionClosed,
                expectedRequestId: RequestOne,
                expectedRouteId: Route);
            Assert.That(
                sink.Reports.Count(
                    report =>
                        report.Error != null &&
                        report.Error.Code ==
                        UnityBridgeProtocolErrorCode.DuplicateOutcome),
                Is.EqualTo(
                    UnityBridgeRouteReceiver.MaximumPendingReports));
        }

        [Test]
        public void ReceiverDisposeDrainsAcceptedReportsWithoutBlocking()
        {
            var sink = new BlockingSink();
            var receiver = new UnityBridgeRouteReceiver(sink);
            Exception receiveException = null;
            Exception disposeException = null;
            var disposeStarted = new ManualResetEventSlim();
            var disposeCompleted = new ManualResetEventSlim();
            var receiveThread = new Thread(
                () =>
                {
                    try
                    {
                        receiver.Receive(ValidRequestJson());
                    }
                    catch (Exception exception)
                    {
                        receiveException = exception;
                    }
                })
            {
                IsBackground = true
            };
            var disposeThread = new Thread(
                () =>
                {
                    disposeStarted.Set();
                    try
                    {
                        receiver.Dispose();
                    }
                    catch (Exception exception)
                    {
                        disposeException = exception;
                    }
                    finally
                    {
                        disposeCompleted.Set();
                    }
                })
            {
                IsBackground = true
            };

            try
            {
                receiveThread.Start();
                Assert.That(
                    sink.PublishStarted.Wait(TimeSpan.FromSeconds(5)),
                    Is.True);
                receiver.Receive(ValidRequestJson(RequestTwo));
                disposeThread.Start();
                Assert.That(
                    disposeStarted.Wait(TimeSpan.FromSeconds(5)),
                    Is.True);
                Assert.That(
                    disposeCompleted.Wait(TimeSpan.FromSeconds(5)),
                    Is.True);
            }
            finally
            {
                sink.ReleasePublication.Set();
            }

            Assert.That(
                receiveThread.Join(TimeSpan.FromSeconds(5)),
                Is.True);
            Assert.That(
                disposeThread.Join(TimeSpan.FromSeconds(5)),
                Is.True);
            Assert.That(receiveException, Is.Null);
            Assert.That(disposeException, Is.Null);
            Assert.That(sink.PublishCount, Is.EqualTo(2));
            Assert.That(sink.Reports.Count, Is.EqualTo(2));
            AssertUnavailable(sink.Reports[0], RequestOne, Route);
            AssertUnavailable(sink.Reports[1], RequestTwo, Route);
            receiver.Receive(ValidRequestJson("request-0003"));
            Assert.That(sink.PublishCount, Is.EqualTo(2));
            receiver.Dispose();

            disposeStarted.Dispose();
            disposeCompleted.Dispose();
            sink.Dispose();
        }

        [Test]
        public void ReceiverAllowsCoordinatedCrossThreadDispose()
        {
            var sink = new CoordinatedDisposeSink();
            var receiver = new UnityBridgeRouteReceiver(sink);
            sink.Receiver = receiver;

            receiver.Receive(ValidRequestJson());

            Assert.That(sink.PublishCount, Is.EqualTo(1));
            Assert.That(sink.DisposeWorkerCompletedDuringPublish, Is.True);
            receiver.Receive(ValidRequestJson(RequestTwo));
            Assert.That(sink.PublishCount, Is.EqualTo(1));
            receiver.Dispose();
        }

        [Test]
        public void ReceiverAllowsReentrantDisposeWithoutDeadlock()
        {
            var sink = new ReentrantDisposeSink();
            var receiver = new UnityBridgeRouteReceiver(sink);
            sink.Receiver = receiver;
            Exception receiveException = null;
            var worker = new Thread(
                () =>
                {
                    try
                    {
                        receiver.Receive(ValidRequestJson());
                    }
                    catch (Exception exception)
                    {
                        receiveException = exception;
                    }
                })
            {
                IsBackground = true
            };

            worker.Start();
            Assert.That(worker.Join(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(receiveException, Is.Null);
            Assert.That(sink.PublishCount, Is.EqualTo(1));
            Assert.That(sink.DisposeReturned, Is.True);

            receiver.Receive(ValidRequestJson(RequestTwo));
            Assert.That(sink.PublishCount, Is.EqualTo(1));
            receiver.Dispose();
        }

        [Test]
        public void MonoBehaviourExposesExactUnregisteredBoundaryAndTearsDown()
        {
            var method = typeof(AL.Platform.Android.AndroidBridge).GetMethod(
                "SetRouteContext",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));

            var sink = new RecordingSink();
            bridgeObject = new GameObject("AndroidBridge");
            var bridge =
                bridgeObject.AddComponent<AL.Platform.Android.AndroidBridge>();
            bridge.ConfigureOutcomeSink(sink);
            bridge.SetRouteContext(ValidRequestJson());

            Assert.That(sink.Reports.Count, Is.EqualTo(1));
            AssertUnavailable(sink.Reports[0], RequestOne, Route);

            var onDisable =
                typeof(AL.Platform.Android.AndroidBridge).GetMethod(
                    "OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            var onDestroy =
                typeof(AL.Platform.Android.AndroidBridge).GetMethod(
                    "OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onDisable, Is.Not.Null);
            Assert.That(onDestroy, Is.Not.Null);

            onDisable.Invoke(bridge, null);
            onDestroy.Invoke(bridge, null);
            bridge.Dispose();
            bridge.SetRouteContext(ValidRequestJson(RequestTwo));
            Assert.That(sink.Reports.Count, Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(bridgeObject);
            bridgeObject = null;
        }

        private static string ValidRequestJson(
            string requestId = RequestOne,
            string routeId = Route,
            string intent = "preview",
            string capabilitiesJson = "[]")
        {
            return "{\"contractVersion\":2," +
                   "\"requestId\":\"" + requestId + "\"," +
                   "\"routeId\":\"" + routeId + "\"," +
                   "\"intent\":\"" + intent + "\"," +
                   "\"requestedCapabilities\":" +
                   capabilitiesJson + "}";
        }

        private static string CapabilityArrayJson(
            IEnumerable<string> capabilities)
        {
            return "[" + string.Join(
                ",",
                capabilities.Select(value => "\"" + value + "\"")) + "]";
        }

        private static string DuplicateRequestJson(
            string canonical,
            string escaped,
            string firstValue,
            string secondValue)
        {
            var fields = new List<string>
            {
                "\"contractVersion\":2",
                "\"requestId\":\"request-0001\"",
                "\"routeId\":\"bridge.smoke\"",
                "\"intent\":\"preview\"",
                "\"requestedCapabilities\":[]"
            };
            fields.RemoveAll(
                value => value.StartsWith(
                    "\"" + canonical + "\"",
                    StringComparison.Ordinal));
            fields.Insert(0, "\"" + escaped + "\":" + secondValue);
            fields.Insert(0, "\"" + canonical + "\":" + firstValue);
            return "{" + string.Join(",", fields) + "}";
        }

        private static string RequestWithout(string field)
        {
            var fields = new List<string>
            {
                "\"contractVersion\":2",
                "\"requestId\":\"request-0001\"",
                "\"routeId\":\"bridge.smoke\"",
                "\"intent\":\"preview\"",
                "\"requestedCapabilities\":[]"
            };
            fields.RemoveAll(
                value => value.StartsWith(
                    "\"" + field + "\"",
                    StringComparison.Ordinal));
            return "{" + string.Join(",", fields) + "}";
        }

        private static UnityRouteOutcome UnavailableOutcome()
        {
            return new UnityRouteOutcome(
                UnityBridgeContract.ContractVersion,
                RequestOne,
                Route,
                UnityRouteOutcomeStatus.Unavailable,
                UnityBridgeContract.RouteNotAvailableDiagnostic);
        }

        private static void AssertRejected(
            UnityBridgeRequestResult result,
            UnityBridgeProtocolErrorCode expectedCode,
            string expectedField = null)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error.Code, Is.EqualTo(expectedCode));
            Assert.That(
                result.Error.WireCode,
                Is.EqualTo(
                    UnityBridgeContract.GetProtocolErrorWireValue(
                        expectedCode)));
            if (expectedField != null)
            {
                Assert.That(
                    result.Error.Field,
                    Is.EqualTo(expectedField));
            }
        }

        private static void AssertOutcomeRejected(
            UnityRouteOutcome outcome,
            UnityBridgeProtocolErrorCode expectedCode,
            string expectedField = null)
        {
            var result = UnityBridgeContract.ValidateOutcome(outcome);
            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Outcome, Is.Null);
            Assert.That(result.Error.Code, Is.EqualTo(expectedCode));
            if (expectedField != null)
            {
                Assert.That(
                    result.Error.Field,
                    Is.EqualTo(expectedField));
            }
        }

        private static void AssertUnavailable(
            UnityBridgeReceiverReport report,
            string requestId,
            string routeId)
        {
            Assert.That(report, Is.Not.Null);
            Assert.That(
                report.Kind,
                Is.EqualTo(
                    UnityBridgeReceiverReportKind.CorrelatedOutcome));
            Assert.That(report.IsSendable, Is.True);
            Assert.That(
                report.Status,
                Is.EqualTo(UnityRouteOutcomeStatus.Unavailable));
            Assert.That(
                report.DiagnosticCode,
                Is.EqualTo(
                    UnityBridgeContract.RouteNotAvailableDiagnostic));
            Assert.That(report.Error, Is.Null);
            Assert.That(report.Request, Is.Not.Null);
            Assert.That(report.Outcome, Is.Not.Null);
            Assert.That(report.Request.RequestId, Is.EqualTo(requestId));
            Assert.That(report.Request.RouteId, Is.EqualTo(routeId));
            Assert.That(
                report.Outcome.ContractVersion,
                Is.EqualTo(UnityBridgeContract.ContractVersion));
            Assert.That(
                report.Outcome.RequestId,
                Is.EqualTo(requestId));
            Assert.That(report.Outcome.RouteId, Is.EqualTo(routeId));
            Assert.That(
                report.Outcome.Status,
                Is.EqualTo(UnityRouteOutcomeStatus.Unavailable));
            Assert.That(
                report.Outcome.DiagnosticCode,
                Is.EqualTo(
                    UnityBridgeContract.RouteNotAvailableDiagnostic));
            Assert.That(report.Outcome.ResultId, Is.Null);
            Assert.That(report.Outcome.Payload, Is.Null);
            Assert.That(
                UnityBridgeContract.ValidateOutcome(report.Outcome)
                    .IsAccepted,
                Is.True);
        }

        private static void AssertFailure(
            UnityBridgeReceiverReport report,
            UnityBridgeProtocolErrorCode expectedCode,
            string expectedField = null,
            string expectedRequestId = null,
            string expectedRouteId = null)
        {
            Assert.That(report, Is.Not.Null);
            Assert.That(
                report.Kind,
                Is.EqualTo(UnityBridgeReceiverReportKind.ProtocolFailure));
            Assert.That(report.IsSendable, Is.False);
            Assert.That(
                report.Status,
                Is.EqualTo(UnityRouteOutcomeStatus.Failure));
            Assert.That(report.Outcome, Is.Null);
            Assert.That(report.Error, Is.Not.Null);
            Assert.That(report.Error.Code, Is.EqualTo(expectedCode));
            Assert.That(
                report.DiagnosticCode,
                Is.EqualTo(report.Error.WireCode));
            if (expectedRequestId == null)
            {
                Assert.That(report.Request, Is.Null);
            }
            else
            {
                Assert.That(report.Request, Is.Not.Null);
                Assert.That(
                    report.Request.RequestId,
                    Is.EqualTo(expectedRequestId));
                Assert.That(
                    report.Request.RouteId,
                    Is.EqualTo(expectedRouteId));
            }

            if (expectedField != null)
            {
                Assert.That(
                    report.Error.Field,
                    Is.EqualTo(expectedField));
            }
        }

        private static void AssertOutcomeContextRejected(
            UnityRouteOutcome outcome,
            UnityRouteRequest request,
            UnityBridgeProtocolErrorCode expectedCode,
            string expectedField)
        {
            var result =
                UnityBridgeContract.ValidateOutcomeForRequest(
                    outcome,
                    request);
            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(expectedCode));
            Assert.That(result.Error.Field, Is.EqualTo(expectedField));
        }

        private sealed class RecordingSink : IUnityBridgeOutcomeSink
        {
            internal List<UnityBridgeReceiverReport> Reports { get; } =
                new List<UnityBridgeReceiverReport>();
            internal bool ThrowAfterRecord { get; set; }

            public void Publish(UnityBridgeReceiverReport report)
            {
                Reports.Add(report);
                if (ThrowAfterRecord)
                {
                    throw new InvalidOperationException("sink failure");
                }
            }
        }

        private sealed class ReentrantSink : IUnityBridgeOutcomeSink
        {
            internal UnityBridgeRouteReceiver Receiver { get; set; }
            internal string Payload { get; set; }
            internal List<UnityBridgeReceiverReport> Reports { get; } =
                new List<UnityBridgeReceiverReport>();
            private bool hasReentered;

            public void Publish(UnityBridgeReceiverReport report)
            {
                Reports.Add(report);
                if (!hasReentered)
                {
                    hasReentered = true;
                    Receiver.Receive(Payload);
                }
            }
        }

        private sealed class ThrowBeforeRecordSink :
            IUnityBridgeOutcomeSink
        {
            internal int PublishAttempts { get; private set; }
            internal List<UnityBridgeReceiverReport> Reports { get; } =
                new List<UnityBridgeReceiverReport>();

            public void Publish(UnityBridgeReceiverReport report)
            {
                PublishAttempts++;
                if (PublishAttempts == 1)
                {
                    throw new InvalidOperationException(
                        "pre-observation sink failure");
                }

                Reports.Add(report);
            }
        }

        private sealed class CoordinatingSink : IUnityBridgeOutcomeSink
        {
            internal UnityBridgeRouteReceiver Receiver { get; set; }
            internal string SecondPayload { get; set; }
            internal bool WorkerCompletedDuringFirstPublish { get; private set; }
            internal List<UnityBridgeReceiverReport> Reports { get; } =
                new List<UnityBridgeReceiverReport>();

            public void Publish(UnityBridgeReceiverReport report)
            {
                Reports.Add(report);
                if (Reports.Count != 1)
                {
                    return;
                }

                var worker = new Thread(
                    () => Receiver.Receive(SecondPayload));
                worker.IsBackground = true;
                worker.Start();
                WorkerCompletedDuringFirstPublish =
                    worker.Join(TimeSpan.FromSeconds(5));
            }
        }

        private sealed class BurstReentrantSink :
            IUnityBridgeOutcomeSink
        {
            internal UnityBridgeRouteReceiver Receiver { get; set; }
            internal string Payload { get; set; }
            internal int ReceiveCallCount { get; private set; }
            internal int MaximumPublishDepth { get; private set; }
            internal List<UnityBridgeReceiverReport> Reports { get; } =
                new List<UnityBridgeReceiverReport>();
            private bool hasBurst;
            private int publishDepth;

            public void Publish(UnityBridgeReceiverReport report)
            {
                publishDepth++;
                MaximumPublishDepth =
                    Math.Max(MaximumPublishDepth, publishDepth);
                try
                {
                    Reports.Add(report);
                    if (hasBurst)
                    {
                        return;
                    }

                    hasBurst = true;
                    for (var index = 0;
                         index <
                         UnityBridgeRouteReceiver.MaximumPendingReports + 1;
                         index++)
                    {
                        ReceiveCallCount++;
                        Receiver.Receive(Payload);
                    }
                }
                finally
                {
                    publishDepth--;
                }
            }
        }

        private sealed class ContinuousReentrantSink :
            IUnityBridgeOutcomeSink
        {
            internal UnityBridgeRouteReceiver Receiver { get; set; }
            internal string Payload { get; set; }
            internal int ReceiveCallCount { get; private set; }
            internal int MaximumPublishDepth { get; private set; }
            internal List<UnityBridgeReceiverReport> Reports { get; } =
                new List<UnityBridgeReceiverReport>();
            private int publishDepth;
            private bool isFirstPublication = true;

            public void Publish(UnityBridgeReceiverReport report)
            {
                publishDepth++;
                MaximumPublishDepth =
                    Math.Max(MaximumPublishDepth, publishDepth);
                try
                {
                    Reports.Add(report);
                    if (report.Error != null &&
                        report.Error.Code ==
                        UnityBridgeProtocolErrorCode.SessionClosed)
                    {
                        return;
                    }

                    var receiveCount = isFirstPublication ? 2 : 1;
                    isFirstPublication = false;
                    for (var index = 0; index < receiveCount; index++)
                    {
                        ReceiveCallCount++;
                        Receiver.Receive(Payload);
                    }
                }
                finally
                {
                    publishDepth--;
                }
            }
        }

        private sealed class BlockingSink :
            IUnityBridgeOutcomeSink,
            IDisposable
        {
            internal ManualResetEventSlim PublishStarted { get; } =
                new ManualResetEventSlim();
            internal ManualResetEventSlim ReleasePublication { get; } =
                new ManualResetEventSlim();
            internal int PublishCount => publishCount;
            internal IReadOnlyList<UnityBridgeReceiverReport> Reports
            {
                get
                {
                    lock (reportsSynchronization)
                    {
                        return reports.ToArray();
                    }
                }
            }

            private int publishCount;
            private readonly object reportsSynchronization = new object();
            private readonly List<UnityBridgeReceiverReport> reports =
                new List<UnityBridgeReceiverReport>();

            public void Publish(UnityBridgeReceiverReport report)
            {
                Interlocked.Increment(ref publishCount);
                lock (reportsSynchronization)
                {
                    reports.Add(report);
                }

                PublishStarted.Set();
                ReleasePublication.Wait(TimeSpan.FromSeconds(10));
            }

            public void Dispose()
            {
                PublishStarted.Dispose();
                ReleasePublication.Dispose();
            }
        }

        private sealed class CoordinatedDisposeSink :
            IUnityBridgeOutcomeSink
        {
            internal UnityBridgeRouteReceiver Receiver { get; set; }
            internal int PublishCount { get; private set; }
            internal bool DisposeWorkerCompletedDuringPublish
            {
                get;
                private set;
            }

            public void Publish(UnityBridgeReceiverReport report)
            {
                PublishCount++;
                var worker = new Thread(() => Receiver.Dispose())
                {
                    IsBackground = true
                };
                worker.Start();
                DisposeWorkerCompletedDuringPublish =
                    worker.Join(TimeSpan.FromSeconds(5));
            }
        }

        private sealed class ReentrantDisposeSink :
            IUnityBridgeOutcomeSink
        {
            internal UnityBridgeRouteReceiver Receiver { get; set; }
            internal int PublishCount { get; private set; }
            internal bool DisposeReturned { get; private set; }

            public void Publish(UnityBridgeReceiverReport report)
            {
                PublishCount++;
                Receiver.Dispose();
                DisposeReturned = true;
            }
        }
    }
}
