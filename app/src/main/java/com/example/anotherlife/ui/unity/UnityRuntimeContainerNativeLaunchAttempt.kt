package com.example.anotherlife.ui.unity

import com.example.anotherlife.ui.launch.NativeLaunchUnityRuntimeAttempt

/**
 * Generation-scoped, non-authoritative launch admission for an already-created container.
 *
 * The route identity stays injected until the Unity-side launch route is formally approved. This
 * wrapper never interprets a route outcome as completion and never treats dispatch as readiness.
 */
internal class UnityRuntimeContainerNativeLaunchAttempt(
    private val container: UnityRuntimeContainer,
    private val generation: Long,
    private val routeId: String
) : NativeLaunchUnityRuntimeAttempt {
    private var started = false

    init {
        require(generation > 0L)
    }

    override fun start(): UnityRuntimeContainerSnapshot {
        check(!started) { "A native launch runtime attempt can start only once." }
        started = true
        val admitted = container.setRoute(
            routeId = routeId,
            routeLaunchSequence = generation,
            routeIntent = UnityRouteIntent.Preview,
            requestedCapabilities = emptyList(),
            onRouteDispatched = {},
            onOutcome = {},
            onProtocolError = {}
        )
        val snapshot = container.runtimeStatusSnapshot()
        return if (
            !admitted &&
            snapshot.phase == UnityRuntimeContainerPhase.Active &&
            snapshot.failure == null
        ) {
            snapshot.copy(
                phase = UnityRuntimeContainerPhase.Failed,
                failure = UnityRuntimeContainerFailure.BridgeProtocolFailed
            )
        } else {
            snapshot
        }
    }

    override fun revokeInputAndFocus(): Boolean = container.revokeInputAndFocus()

    override fun destroy(): UnityRuntimeContainerTeardownResult = container.destroyUnity()
}
