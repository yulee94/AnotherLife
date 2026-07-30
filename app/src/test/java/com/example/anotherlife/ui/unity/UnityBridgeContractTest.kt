package com.example.anotherlife.ui.unity

import java.util.ArrayDeque
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class UnityBridgeContractTest {
    @Test
    fun requestRoundTripsWithExactVersionIntentAndCapabilities() {
        val request = accepted(
            UnityBridgeContract.createRequest(
                requestId = REQUEST_ONE,
                routeId = ROUTE,
                intent = UnityRouteIntent.Preview,
                requestedCapabilities = listOf("route.acknowledge", "route.cancel")
            )
        )
        val encoded = accepted(UnityBridgeContract.encodeRequest(request))
        val parsed = accepted(UnityBridgeContract.parseRequest(encoded))

        assertEquals(request, parsed)
        assertEquals(UNITY_BRIDGE_CONTRACT_VERSION, parsed.contractVersion)
    }

    @Test
    fun requestRejectsUnsafeIdsDuplicateCapabilitiesAndUnknownFields() {
        assertRejected(
            UnityBridgeContract.createRequest(
                requestId = REQUEST_ONE,
                routeId = "../main",
                intent = UnityRouteIntent.Preview
            ),
            UnityBridgeProtocolErrorCode.InvalidRouteId
        )
        assertRejected(
            UnityBridgeContract.createRequest(
                requestId = REQUEST_ONE,
                routeId = ROUTE,
                intent = UnityRouteIntent.Preview,
                requestedCapabilities = listOf("route.cancel", "route.cancel")
            ),
            UnityBridgeProtocolErrorCode.DuplicateCapability
        )

        val withUnknownField = accepted(
            UnityBridgeContract.encodeRequest(validRequest())
        ).dropLast(1) + ""","unknown":true}"""
        assertRejected(
            UnityBridgeContract.parseRequest(withUnknownField),
            UnityBridgeProtocolErrorCode.UnexpectedField
        )
    }

    @Test
    fun requestRejectsUnsupportedVersionAndIntentWithoutNormalization() {
        val encoded = accepted(UnityBridgeContract.encodeRequest(validRequest()))

        assertRejected(
            UnityBridgeContract.parseRequest(
                encoded.replace(
                    "\"contractVersion\":2",
                    "\"contractVersion\":1"
                )
            ),
            UnityBridgeProtocolErrorCode.InvalidContractVersion
        )
        assertRejected(
            UnityBridgeContract.parseRequest(
                encoded.replace("\"intent\":\"preview\"", "\"intent\":\"Preview\"")
            ),
            UnityBridgeProtocolErrorCode.InvalidIntent
        )
    }

    @Test
    fun requestRejectsDuplicateCorrelationMembersBeforeTreeMaterialization() {
        val fixtures = listOf(
            "requestId" to
                """{"contractVersion":2,"requestId":"request-0001","requestId":"request-0002","routeId":"bridge.smoke","intent":"preview","requestedCapabilities":[]}""",
            "routeId" to
                """{"contractVersion":2,"requestId":"request-0001","routeId":"bridge.smoke","routeId":"bridge.other","intent":"preview","requestedCapabilities":[]}"""
        )

        fixtures.forEach { (field, rawJson) ->
            assertRejected(
                UnityBridgeContract.parseRequest(rawJson),
                UnityBridgeProtocolErrorCode.DuplicateField,
                field
            )
        }
    }

    @Test
    fun outcomeParsesTypedUnavailableWithCorrelationAndDiagnostic() {
        val outcome = accepted(
            UnityBridgeContract.parseOutcome(
                outcomeJson(
                    requestId = REQUEST_ONE,
                    status = "unavailable",
                    diagnosticCode = "route.not_available",
                    resultId = "result-0001",
                    payload = """{"retryable":false}"""
                )
            )
        )

        assertEquals(UnityRouteOutcomeStatus.Unavailable, outcome.status)
        assertEquals(REQUEST_ONE, outcome.requestId)
        assertEquals("route.not_available", outcome.diagnosticCode)
        assertEquals("result-0001", outcome.resultId)
    }

    @Test
    fun outcomeRejectsMalformedUnknownStatusAndFailureWithoutDiagnostic() {
        val valid = outcomeJson(requestId = REQUEST_ONE)
        assertRejected(
            UnityBridgeContract.parseOutcome("{"),
            UnityBridgeProtocolErrorCode.MalformedJson
        )
        assertRejected(
            UnityBridgeContract.parseOutcome(
                valid.replace("\"contractVersion\":2", "\"contractVersion\":1")
            ),
            UnityBridgeProtocolErrorCode.InvalidContractVersion
        )
        assertRejected(
            UnityBridgeContract.parseOutcome(
                outcomeJson(requestId = REQUEST_ONE, status = "Success")
            ),
            UnityBridgeProtocolErrorCode.InvalidStatus
        )
        assertRejected(
            UnityBridgeContract.parseOutcome(
                outcomeJson(requestId = REQUEST_ONE, status = "failure")
            ),
            UnityBridgeProtocolErrorCode.MissingDiagnosticCode
        )
        assertRejected(
            UnityBridgeContract.parseOutcome(valid.dropLast(1) + ""","unknown":true}"""),
            UnityBridgeProtocolErrorCode.UnexpectedField
        )
    }

    @Test
    fun outcomeRejectsDuplicateCorrelationStatusAndPayloadMembers() {
        val fixtures = listOf(
            "requestId" to
                """{"contractVersion":2,"requestId":"request-0001","requestId":"request-0002","routeId":"bridge.smoke","status":"success"}""",
            "routeId" to
                """{"contractVersion":2,"requestId":"request-0001","routeId":"bridge.smoke","routeId":"bridge.other","status":"success"}""",
            "status" to
                """{"contractVersion":2,"requestId":"request-0001","routeId":"bridge.smoke","status":"success","status":"failure","diagnosticCode":"route.failed"}""",
            "payload" to
                """{"contractVersion":2,"requestId":"request-0001","routeId":"bridge.smoke","status":"success","payload":"first","pay\u006coad":"second"}"""
        )

        fixtures.forEach { (field, rawJson) ->
            assertRejected(
                UnityBridgeContract.parseOutcome(rawJson),
                UnityBridgeProtocolErrorCode.DuplicateField,
                field
            )
        }
    }

    @Test
    fun outcomeRejectsOversizedMessageAndPayloadBeforeDelivery() {
        assertRejected(
            UnityBridgeContract.parseOutcome("x".repeat(MAX_UNITY_BRIDGE_MESSAGE_BYTES + 1)),
            UnityBridgeProtocolErrorCode.MessageTooLarge
        )
        assertRejected(
            UnityBridgeContract.parseOutcome(
                outcomeJson(
                    requestId = REQUEST_ONE,
                    payload = "x".repeat(MAX_UNITY_BRIDGE_PAYLOAD_BYTES + 1)
                )
            ),
            UnityBridgeProtocolErrorCode.PayloadTooLarge
        )
    }

    @Test
    fun malformedOutcomeDoesNotConsumeLaterValidOutcome() {
        val session = UnityBridgeSession { REQUEST_ONE }
        val start = started(session.startRoute(ROUTE, UnityRouteIntent.Preview))

        assertDeliveryRejected(
            session.consumeOutcome("{"),
            UnityBridgeProtocolErrorCode.MalformedJson
        )
        val delivered = delivered(
            session.consumeOutcome(outcomeJson(start.request.requestId))
        )

        assertEquals(start.request.requestId, delivered.requestId)
    }

    @Test
    fun sameRouteRelaunchGetsNewCorrelationAndRejectsPriorResult() {
        val requestIds = ArrayDeque(listOf(REQUEST_ONE, REQUEST_TWO))
        val session = UnityBridgeSession { requestIds.removeFirst() }
        val first = started(session.startRoute(ROUTE, UnityRouteIntent.Preview))
        val second = started(session.startRoute(ROUTE, UnityRouteIntent.Preview))

        assertNotEquals(first.request.requestId, second.request.requestId)
        assertDeliveryRejected(
            session.consumeOutcome(outcomeJson(first.request.requestId)),
            UnityBridgeProtocolErrorCode.RequestMismatch
        )
        assertEquals(
            second.request.requestId,
            delivered(session.consumeOutcome(outcomeJson(second.request.requestId))).requestId
        )
    }

    @Test
    fun validOutcomeIsDeliveredOnceAndRouteMismatchDoesNotConsumeIt() {
        val session = UnityBridgeSession { REQUEST_ONE }
        val start = started(session.startRoute(ROUTE, UnityRouteIntent.Preview))

        assertDeliveryRejected(
            session.consumeOutcome(
                outcomeJson(requestId = start.request.requestId, routeId = "other.route")
            ),
            UnityBridgeProtocolErrorCode.RouteMismatch
        )
        val valid = outcomeJson(start.request.requestId)
        assertEquals(start.request.requestId, delivered(session.consumeOutcome(valid)).requestId)
        assertDeliveryRejected(
            session.consumeOutcome(valid),
            UnityBridgeProtocolErrorCode.DuplicateOutcome
        )
    }

    @Test
    fun authoritativeSuccessRequiresResultIdentityBeforeDelivery() {
        val session = UnityBridgeSession { REQUEST_ONE }
        val start = started(session.startRoute(ROUTE, UnityRouteIntent.Authoritative))

        assertDeliveryRejected(
            session.consumeOutcome(outcomeJson(start.request.requestId)),
            UnityBridgeProtocolErrorCode.MissingResultId
        )
        val identified = outcomeJson(
            requestId = start.request.requestId,
            resultId = "result-0001"
        )
        assertEquals(
            "result-0001",
            delivered(session.consumeOutcome(identified)).resultId
        )
    }

    @Test
    fun closedSessionRejectsNewRoutesAndLateOutcomes() {
        val session = UnityBridgeSession { REQUEST_ONE }
        session.startRoute(ROUTE, UnityRouteIntent.Preview)
        session.close()

        val routeStart = session.startRoute(ROUTE, UnityRouteIntent.Preview)
        assertTrue(routeStart is UnityBridgeSessionStart.Rejected)
        assertEquals(
            UnityBridgeProtocolErrorCode.SessionClosed,
            (routeStart as UnityBridgeSessionStart.Rejected).error.code
        )
        assertDeliveryRejected(
            session.consumeOutcome(outcomeJson(REQUEST_ONE)),
            UnityBridgeProtocolErrorCode.SessionClosed
        )
    }

    @Test
    fun callbackOwnershipPreventsOldHostFromClearingReplacement() {
        val registry = UnityBridgeCallbackRegistry()
        var oldHostPayload: String? = null
        var activeHostPayload: String? = null
        val oldToken = registry.register { oldHostPayload = it }
        val activeToken = registry.register { activeHostPayload = it }

        registry.clear(oldToken)
        registry.report("active")
        assertNull(oldHostPayload)
        assertEquals("active", activeHostPayload)

        registry.clear(activeToken)
        registry.report("late")
        assertEquals("active", activeHostPayload)
    }

    @Test
    fun jvmCallbackBoundaryTurnsNullIntoTypedRejectionWithoutThrowing() {
        val session = UnityBridgeSession { REQUEST_ONE }
        val start = started(session.startRoute(ROUTE, UnityRouteIntent.Preview))
        var delivery: UnityBridgeSessionDelivery? = null
        val token = UnityBridgeCallbacks.register { rawJson ->
            delivery = session.consumeOutcome(rawJson)
        }

        try {
            val boundary = UnityBridgeCallbacks::class.java.getMethod(
                "reportOutcome",
                String::class.java
            )
            val invocation = runCatching {
                boundary.invoke(null, *arrayOfNulls<Any>(1))
            }

            assertNull(invocation.exceptionOrNull())
            assertTrue(delivery != null)
            assertDeliveryRejected(
                delivery!!,
                UnityBridgeProtocolErrorCode.NullMessage
            )
            assertEquals(
                start.request.requestId,
                delivered(
                    session.consumeOutcome(outcomeJson(start.request.requestId))
                ).requestId
            )
        } finally {
            UnityBridgeCallbacks.clear(token)
        }
    }

    private fun validRequest(): UnityRouteRequest {
        return accepted(
            UnityBridgeContract.createRequest(
                requestId = REQUEST_ONE,
                routeId = ROUTE,
                intent = UnityRouteIntent.Preview
            )
        )
    }

    private fun outcomeJson(
        requestId: String,
        routeId: String = ROUTE,
        status: String = "success",
        diagnosticCode: String? = null,
        resultId: String? = null,
        payload: String? = null
    ): String {
        return buildJsonObject {
            put("contractVersion", UNITY_BRIDGE_CONTRACT_VERSION)
            put("requestId", requestId)
            put("routeId", routeId)
            put("status", status)
            diagnosticCode?.let { put("diagnosticCode", it) }
            resultId?.let { put("resultId", it) }
            payload?.let { put("payload", it) }
        }.toString()
    }

    private fun <T> accepted(result: UnityBridgeContractResult<T>): T {
        assertTrue(result is UnityBridgeContractResult.Accepted)
        return (result as UnityBridgeContractResult.Accepted).value
    }

    private fun assertRejected(
        result: UnityBridgeContractResult<*>,
        expected: UnityBridgeProtocolErrorCode,
        expectedField: String? = null
    ) {
        assertTrue(result is UnityBridgeContractResult.Rejected)
        val error = (result as UnityBridgeContractResult.Rejected).error
        assertEquals(
            expected,
            error.code
        )
        expectedField?.let { assertEquals(it, error.field) }
    }

    private fun started(start: UnityBridgeSessionStart): UnityBridgeSessionStart.Started {
        assertTrue(start is UnityBridgeSessionStart.Started)
        return start as UnityBridgeSessionStart.Started
    }

    private fun delivered(
        delivery: UnityBridgeSessionDelivery
    ): UnityRouteOutcome {
        assertTrue(delivery is UnityBridgeSessionDelivery.Delivered)
        return (delivery as UnityBridgeSessionDelivery.Delivered).outcome
    }

    private fun assertDeliveryRejected(
        delivery: UnityBridgeSessionDelivery,
        expected: UnityBridgeProtocolErrorCode
    ) {
        assertTrue(delivery is UnityBridgeSessionDelivery.Rejected)
        assertEquals(
            expected,
            (delivery as UnityBridgeSessionDelivery.Rejected).error.code
        )
    }

    private companion object {
        const val ROUTE = "bridge.smoke"
        const val REQUEST_ONE = "request-0001"
        const val REQUEST_TWO = "request-0002"
    }
}
