package com.example.anotherlife.ui.unity

internal sealed interface UnityBridgeSmokeDecision {
    data class SafeReturn(val notice: String) : UnityBridgeSmokeDecision
    data class StayVisible(val notice: String) : UnityBridgeSmokeDecision
}

internal object UnityBridgeSmokePolicy {
    const val ROUTE_ID = "bridge.smoke.unavailable"

    fun decide(outcome: UnityRouteOutcome): UnityBridgeSmokeDecision {
        return when (outcome.status) {
            UnityRouteOutcomeStatus.Unavailable -> UnityBridgeSmokeDecision.SafeReturn(
                "Unity bridge smoke route is unavailable as expected. Returned safely to Debug."
            )
            UnityRouteOutcomeStatus.Cancelled -> UnityBridgeSmokeDecision.SafeReturn(
                "Unity bridge smoke route was cancelled. Returned safely to Debug."
            )
            UnityRouteOutcomeStatus.Failure -> UnityBridgeSmokeDecision.StayVisible(
                "Unity bridge smoke failed (${outcome.diagnosticCode ?: "unknown"}). " +
                    "No result was applied."
            )
            UnityRouteOutcomeStatus.Success -> UnityBridgeSmokeDecision.StayVisible(
                "Unity bridge smoke returned an unapproved success. No result was applied."
            )
        }
    }
}
