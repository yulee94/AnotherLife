package com.example.anotherlife.ui.unity

import org.junit.Assert.assertEquals
import org.junit.Test

class UnityBridgeSmokePolicyTest {
    @Test
    fun unavailableAndCancelledReturnToTheSafeShell() {
        assertEquals(
            UnityBridgeSmokeDecision.SafeReturn(
                "Unity bridge smoke route is unavailable as expected. Returned safely to Debug."
            ),
            UnityBridgeSmokePolicy.decide(outcome(UnityRouteOutcomeStatus.Unavailable))
        )
        assertEquals(
            UnityBridgeSmokeDecision.SafeReturn(
                "Unity bridge smoke route was cancelled. Returned safely to Debug."
            ),
            UnityBridgeSmokePolicy.decide(outcome(UnityRouteOutcomeStatus.Cancelled))
        )
    }

    @Test
    fun failureAndUnapprovedSuccessStayVisibleWithoutApplyingAResult() {
        assertEquals(
            UnityBridgeSmokeDecision.StayVisible(
                "Unity bridge smoke failed (route.failed). No result was applied."
            ),
            UnityBridgeSmokePolicy.decide(
                outcome(
                    status = UnityRouteOutcomeStatus.Failure,
                    diagnosticCode = "route.failed"
                )
            )
        )
        assertEquals(
            UnityBridgeSmokeDecision.StayVisible(
                "Unity bridge smoke returned an unapproved success. No result was applied."
            ),
            UnityBridgeSmokePolicy.decide(outcome(UnityRouteOutcomeStatus.Success))
        )
    }

    private fun outcome(
        status: UnityRouteOutcomeStatus,
        diagnosticCode: String? = null
    ) = UnityRouteOutcome(
        contractVersion = UNITY_BRIDGE_CONTRACT_VERSION,
        requestId = "request-0001",
        routeId = UnityBridgeSmokePolicy.ROUTE_ID,
        status = status,
        diagnosticCode = diagnosticCode,
        resultId = null,
        payload = null
    )
}
