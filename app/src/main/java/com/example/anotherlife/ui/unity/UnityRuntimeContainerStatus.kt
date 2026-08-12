package com.example.anotherlife.ui.unity

internal enum class UnityRuntimeContainerPhase {
    RequestingOwnership,
    WaitingForOwnership,
    Activating,
    Active,
    Failed,
    Destroying,
    Destroyed
}

/** What the Android host can prove about the embedded runtime at this instant. */
internal enum class UnityRuntimeContainerOwnership {
    NeverCreated,
    Active,
    Uncertain
}

internal enum class UnityRuntimeContainerTeardownEvidence {
    NotStarted,
    InProgress,
    Confirmed,
    Uncertain
}

internal enum class UnityRuntimeContainerFailure {
    OwnershipCapacityReached,
    RuntimeUnavailable,
    ConstructionFailed,
    ActivationFailed,
    LifecycleCallbacksUnavailable,
    LifecycleFailed,
    BridgeProtocolFailed
}

/**
 * Machine-readable host evidence. This is deliberately separate from the status text rendered by
 * [UnityRuntimeContainer], and route dispatch is not a Unity-ready acknowledgement.
 */
internal data class UnityRuntimeContainerSnapshot(
    val phase: UnityRuntimeContainerPhase,
    val ownership: UnityRuntimeContainerOwnership,
    val teardown: UnityRuntimeContainerTeardownEvidence,
    val routeDispatched: Boolean = false,
    val failure: UnityRuntimeContainerFailure? = null
)

internal sealed interface UnityRuntimeContainerTeardownResult {
    data object AwaitingCleanup : UnityRuntimeContainerTeardownResult

    data object Confirmed : UnityRuntimeContainerTeardownResult

    data object Uncertain : UnityRuntimeContainerTeardownResult
}

internal fun interface UnityRuntimeContainerObserver {
    fun onChanged(snapshot: UnityRuntimeContainerSnapshot)
}

/**
 * Stores the latest evidence and lets a dormant launch adapter subscribe without racing initial
 * synchronous activation. Observer failures cannot alter runtime ownership or cleanup.
 */
internal class UnityRuntimeContainerStatusPublisher(
    initialSnapshot: UnityRuntimeContainerSnapshot,
    private val dispatch: (
        UnityRuntimeContainerObserver,
        UnityRuntimeContainerSnapshot
    ) -> Unit = { observer, snapshot -> observer.onChanged(snapshot) }
) {
    private var current = initialSnapshot
    private var observer: UnityRuntimeContainerObserver? = null

    fun snapshot(): UnityRuntimeContainerSnapshot = synchronized(this) { current }

    fun observe(newObserver: UnityRuntimeContainerObserver?) {
        val snapshot = synchronized(this) {
            observer = newObserver
            current
        }
        if (newObserver != null) deliver(newObserver, snapshot)
    }

    fun publish(snapshot: UnityRuntimeContainerSnapshot): UnityRuntimeContainerSnapshot {
        val delivery = synchronized(this) {
            if (current.phase == UnityRuntimeContainerPhase.Destroyed) return current
            current = snapshot
            observer to current
        }
        delivery.first?.let { listener -> deliver(listener, delivery.second) }
        return delivery.second
    }

    fun update(
        transform: (UnityRuntimeContainerSnapshot) -> UnityRuntimeContainerSnapshot
    ): UnityRuntimeContainerSnapshot {
        val delivery = synchronized(this) {
            if (current.phase == UnityRuntimeContainerPhase.Destroyed) return current
            current = transform(current)
            observer to current
        }
        delivery.first?.let { listener -> deliver(listener, delivery.second) }
        return delivery.second
    }

    private fun deliver(
        listener: UnityRuntimeContainerObserver,
        snapshot: UnityRuntimeContainerSnapshot
    ) {
        runCatching { dispatch(listener, snapshot) }
    }

    fun teardownResult(): UnityRuntimeContainerTeardownResult = when (snapshot().teardown) {
        UnityRuntimeContainerTeardownEvidence.InProgress ->
            UnityRuntimeContainerTeardownResult.AwaitingCleanup

        UnityRuntimeContainerTeardownEvidence.Confirmed ->
            UnityRuntimeContainerTeardownResult.Confirmed

        UnityRuntimeContainerTeardownEvidence.Uncertain,
        UnityRuntimeContainerTeardownEvidence.NotStarted ->
            UnityRuntimeContainerTeardownResult.Uncertain
    }
}
