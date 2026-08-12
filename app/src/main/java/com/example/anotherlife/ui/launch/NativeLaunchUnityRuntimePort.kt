package com.example.anotherlife.ui.launch

import com.example.anotherlife.ui.unity.UnityRuntimeContainerFailure
import com.example.anotherlife.ui.unity.UnityRuntimeContainerOwnership
import com.example.anotherlife.ui.unity.UnityRuntimeContainerPhase
import com.example.anotherlife.ui.unity.UnityRuntimeContainerSnapshot
import com.example.anotherlife.ui.unity.UnityRuntimeContainerTeardownEvidence
import com.example.anotherlife.ui.unity.UnityRuntimeContainerTeardownResult

/** One generation-scoped container owned by the future native launch surface. */
internal interface NativeLaunchUnityRuntimeAttempt {
    /** Starts route admission and returns current host evidence, never a readiness claim. */
    fun start(): UnityRuntimeContainerSnapshot

    /** Fences Android focus and input before teardown. */
    fun revokeInputAndFocus(): Boolean

    fun destroy(): UnityRuntimeContainerTeardownResult
}

internal fun interface NativeLaunchUnityRuntimeAttemptFactory {
    fun create(generation: Long): NativeLaunchUnityRuntimeAttempt?
}

/**
 * Enforces one embedded runtime attempt and the revoke-before-destroy order.
 *
 * This adapter intentionally has no route-completion, save, profile, onboarding, reward, or
 * navigation behavior. A dispatched route still waits for a separate correlated Unity-ready
 * acknowledgement before [NativeLaunchFallbackHostController] can transfer presentation.
 */
internal class NativeLaunchUnityRuntimePort(
    private val factory: NativeLaunchUnityRuntimeAttemptFactory
) : NativeLaunchRuntimePort {
    private var active: ActiveAttempt? = null

    @Synchronized
    override fun startUnity(generation: Long): NativeLaunchRuntimeStartResult {
        if (generation <= 0L || active != null) {
            return failedUncertain(NativeLaunchFailure.ConstructionFailed)
        }

        val attempt = try {
            factory.create(generation)
        } catch (_: Throwable) {
            // A throwing factory may have entered native construction without returning a handle.
            return failedUncertain(NativeLaunchFailure.ConstructionFailed)
        } ?: return NativeLaunchRuntimeStartResult.Failed(
            failure = NativeLaunchFailure.RuntimeUnavailable,
            ownership = NativeLaunchRuntimeOwnership.NeverCreated
        )

        val record = ActiveAttempt(generation, attempt)
        active = record
        val snapshot = try {
            attempt.start()
        } catch (_: Throwable) {
            return cleanupAfterStartFailure(record)
        }
        val result = NativeLaunchUnityRuntimeEvidenceMapper.startResult(snapshot)
        if (
            result is NativeLaunchRuntimeStartResult.Failed &&
            result.ownership == NativeLaunchRuntimeOwnership.NeverCreated
        ) {
            return releaseFailedReservation(record, result.failure)
        }
        return result
    }

    @Synchronized
    override fun revokeInputAndStopUnity(
        generation: Long
    ): NativeLaunchRuntimeStopResult {
        val record = active
            ?: return NativeLaunchRuntimeStopResult.TeardownUncertain
        if (record.generation != generation) {
            return NativeLaunchRuntimeStopResult.TeardownUncertain
        }

        record.stopRequested = true
        record.inputRevoked = runCatching(record.attempt::revokeInputAndFocus)
            .getOrDefault(false)
        val teardown = runCatching(record.attempt::destroy)
            .getOrDefault(UnityRuntimeContainerTeardownResult.Uncertain)
        val mapped = NativeLaunchUnityRuntimeEvidenceMapper.stopResult(teardown)
        if (
            !record.inputRevoked ||
            mapped == NativeLaunchRuntimeStopResult.TeardownUncertain
        ) {
            record.cleanupPoisoned = true
            return NativeLaunchRuntimeStopResult.TeardownUncertain
        }
        if (mapped == NativeLaunchRuntimeStopResult.TeardownConfirmed) active = null
        return mapped
    }

    /** Records an asynchronous cleanup result before forwarding it to the host controller. */
    @Synchronized
    fun recordTeardown(
        generation: Long,
        teardown: UnityRuntimeContainerTeardownResult
    ): NativeLaunchRuntimeStopResult {
        val record = active
            ?: return NativeLaunchRuntimeStopResult.TeardownUncertain
        if (record.generation != generation) {
            return NativeLaunchRuntimeStopResult.TeardownUncertain
        }
        if (
            !record.stopRequested ||
            !record.inputRevoked ||
            record.cleanupPoisoned
        ) {
            return NativeLaunchRuntimeStopResult.TeardownUncertain
        }
        val mapped = NativeLaunchUnityRuntimeEvidenceMapper.stopResult(teardown)
        if (mapped == NativeLaunchRuntimeStopResult.TeardownUncertain) {
            record.cleanupPoisoned = true
            return mapped
        }
        if (mapped == NativeLaunchRuntimeStopResult.TeardownConfirmed) active = null
        return mapped
    }

    @Synchronized
    internal fun activeGenerationForTesting(): Long? = active?.generation

    private fun cleanupAfterStartFailure(
        record: ActiveAttempt
    ): NativeLaunchRuntimeStartResult {
        record.stopRequested = true
        record.inputRevoked = runCatching(record.attempt::revokeInputAndFocus)
            .getOrDefault(false)
        val teardown = runCatching(record.attempt::destroy)
            .getOrDefault(UnityRuntimeContainerTeardownResult.Uncertain)
        if (
            record.inputRevoked &&
            teardown == UnityRuntimeContainerTeardownResult.Confirmed
        ) {
            active = null
            return NativeLaunchRuntimeStartResult.Failed(
                failure = NativeLaunchFailure.ConstructionFailed,
                ownership = NativeLaunchRuntimeOwnership.NeverCreated
            )
        }
        record.cleanupPoisoned = true
        return failedUncertain(NativeLaunchFailure.ConstructionFailed)
    }

    private fun releaseFailedReservation(
        record: ActiveAttempt,
        failure: NativeLaunchFailure
    ): NativeLaunchRuntimeStartResult {
        return when (
            runCatching(record.attempt::destroy)
                .getOrDefault(UnityRuntimeContainerTeardownResult.Uncertain)
        ) {
            UnityRuntimeContainerTeardownResult.Confirmed -> {
                active = null
                NativeLaunchRuntimeStartResult.Failed(
                    failure = failure,
                    ownership = NativeLaunchRuntimeOwnership.NeverCreated
                )
            }

            UnityRuntimeContainerTeardownResult.AwaitingCleanup,
            UnityRuntimeContainerTeardownResult.Uncertain -> {
                record.cleanupPoisoned = true
                failedUncertain(failure)
            }
        }
    }

    private fun failedUncertain(
        failure: NativeLaunchFailure
    ) = NativeLaunchRuntimeStartResult.Failed(
        failure = failure,
        ownership = NativeLaunchRuntimeOwnership.Uncertain
    )

    private data class ActiveAttempt(
        val generation: Long,
        val attempt: NativeLaunchUnityRuntimeAttempt,
        var stopRequested: Boolean = false,
        var inputRevoked: Boolean = false,
        var cleanupPoisoned: Boolean = false
    )
}

/** Maps typed container evidence without ever treating route dispatch as runtime readiness. */
internal object NativeLaunchUnityRuntimeEvidenceMapper {
    fun startResult(
        snapshot: UnityRuntimeContainerSnapshot
    ): NativeLaunchRuntimeStartResult = when (snapshot.phase) {
        UnityRuntimeContainerPhase.RequestingOwnership,
        UnityRuntimeContainerPhase.WaitingForOwnership -> {
            if (
                snapshot.ownership == UnityRuntimeContainerOwnership.NeverCreated &&
                isCleanStartEvidence(snapshot)
            ) {
                NativeLaunchRuntimeStartResult.AwaitingReady
            } else {
                failedFromUntrustedSnapshot(snapshot)
            }
        }

        UnityRuntimeContainerPhase.Activating -> {
            if (
                snapshot.ownership != UnityRuntimeContainerOwnership.Uncertain &&
                isCleanStartEvidence(snapshot)
            ) {
                NativeLaunchRuntimeStartResult.AwaitingReady
            } else {
                failedFromUntrustedSnapshot(snapshot)
            }
        }

        UnityRuntimeContainerPhase.Active -> {
            if (
                snapshot.ownership == UnityRuntimeContainerOwnership.Active &&
                isCleanStartEvidence(snapshot)
            ) {
                NativeLaunchRuntimeStartResult.AwaitingReady
            } else {
                failedFromUntrustedSnapshot(snapshot)
            }
        }

        UnityRuntimeContainerPhase.Failed -> NativeLaunchRuntimeStartResult.Failed(
            failure = mapFailure(snapshot.failure),
            ownership = mapFailureOwnership(snapshot)
        )

        UnityRuntimeContainerPhase.Destroying -> failedFromUntrustedSnapshot(snapshot)

        UnityRuntimeContainerPhase.Destroyed -> {
            if (
                snapshot.teardown == UnityRuntimeContainerTeardownEvidence.Confirmed &&
                snapshot.ownership == UnityRuntimeContainerOwnership.NeverCreated
            ) {
                NativeLaunchRuntimeStartResult.Failed(
                    failure = mapFailure(snapshot.failure),
                    ownership = NativeLaunchRuntimeOwnership.NeverCreated
                )
            } else {
                failedFromUntrustedSnapshot(snapshot)
            }
        }
    }

    fun stopResult(
        result: UnityRuntimeContainerTeardownResult
    ): NativeLaunchRuntimeStopResult = when (result) {
        UnityRuntimeContainerTeardownResult.AwaitingCleanup ->
            NativeLaunchRuntimeStopResult.AwaitingTeardown

        UnityRuntimeContainerTeardownResult.Confirmed ->
            NativeLaunchRuntimeStopResult.TeardownConfirmed

        UnityRuntimeContainerTeardownResult.Uncertain ->
            NativeLaunchRuntimeStopResult.TeardownUncertain
    }

    private fun failedFromUntrustedSnapshot(
        snapshot: UnityRuntimeContainerSnapshot
    ) = NativeLaunchRuntimeStartResult.Failed(
        failure = mapFailure(snapshot.failure),
        ownership = NativeLaunchRuntimeOwnership.Uncertain
    )

    private fun isCleanStartEvidence(
        snapshot: UnityRuntimeContainerSnapshot
    ): Boolean =
        snapshot.teardown == UnityRuntimeContainerTeardownEvidence.NotStarted &&
            snapshot.failure == null

    private fun mapFailureOwnership(
        snapshot: UnityRuntimeContainerSnapshot
    ): NativeLaunchRuntimeOwnership = when (snapshot.teardown) {
        UnityRuntimeContainerTeardownEvidence.InProgress,
        UnityRuntimeContainerTeardownEvidence.Uncertain ->
            NativeLaunchRuntimeOwnership.Uncertain

        UnityRuntimeContainerTeardownEvidence.Confirmed -> {
            if (snapshot.ownership == UnityRuntimeContainerOwnership.NeverCreated) {
                NativeLaunchRuntimeOwnership.NeverCreated
            } else {
                NativeLaunchRuntimeOwnership.Uncertain
            }
        }

        UnityRuntimeContainerTeardownEvidence.NotStarted ->
            mapOwnership(snapshot.ownership)
    }

    private fun mapOwnership(
        ownership: UnityRuntimeContainerOwnership
    ): NativeLaunchRuntimeOwnership = when (ownership) {
        UnityRuntimeContainerOwnership.NeverCreated ->
            NativeLaunchRuntimeOwnership.NeverCreated
        UnityRuntimeContainerOwnership.Active -> NativeLaunchRuntimeOwnership.Active
        UnityRuntimeContainerOwnership.Uncertain -> NativeLaunchRuntimeOwnership.Uncertain
    }

    private fun mapFailure(
        failure: UnityRuntimeContainerFailure?
    ): NativeLaunchFailure = when (failure) {
        UnityRuntimeContainerFailure.OwnershipCapacityReached,
        UnityRuntimeContainerFailure.RuntimeUnavailable ->
            NativeLaunchFailure.RuntimeUnavailable

        UnityRuntimeContainerFailure.ConstructionFailed,
        UnityRuntimeContainerFailure.ActivationFailed,
        UnityRuntimeContainerFailure.LifecycleCallbacksUnavailable,
        UnityRuntimeContainerFailure.LifecycleFailed ->
            NativeLaunchFailure.ConstructionFailed

        UnityRuntimeContainerFailure.BridgeProtocolFailed ->
            NativeLaunchFailure.RouteAdmissionFailed

        null -> NativeLaunchFailure.Unknown
    }
}
