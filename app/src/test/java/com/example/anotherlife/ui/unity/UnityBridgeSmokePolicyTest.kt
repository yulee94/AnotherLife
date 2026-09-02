package com.example.anotherlife.ui.unity

import org.junit.Assert.assertEquals
import org.junit.Test

class UnityBridgeSmokePolicyTest {
    @Test
    fun unavailableAndCancelledReturnToTheSafeShell() {
        assertEquals(
            UnityBridgeSmokeDecision.SafeReturn(
                UnityBridgeSmokeSafeReturnNotice.Unavailable
            ),
            UnityBridgeSmokePolicy.decide(outcome(UnityRouteOutcomeStatus.Unavailable))
        )
        assertEquals(
            UnityBridgeSmokeDecision.SafeReturn(
                UnityBridgeSmokeSafeReturnNotice.Cancelled
            ),
            UnityBridgeSmokePolicy.decide(outcome(UnityRouteOutcomeStatus.Cancelled))
        )
    }

    @Test
    fun safeReturnNoticesRestoreOnlyFromBoundedPersistenceKeys() {
        UnityBridgeSmokeSafeReturnNotice.values().forEach { notice ->
            assertEquals(
                notice,
                UnityBridgeSmokeSafeReturnNotice.fromPersistenceKey(notice.persistenceKey)
            )
        }
        assertEquals(null, UnityBridgeSmokeSafeReturnNotice.fromPersistenceKey(null))
        assertEquals(null, UnityBridgeSmokeSafeReturnNotice.fromPersistenceKey("raw-bridge-payload"))
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
