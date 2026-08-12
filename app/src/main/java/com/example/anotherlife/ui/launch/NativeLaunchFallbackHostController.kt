package com.example.anotherlife.ui.launch

internal sealed interface NativeLaunchRuntimeStartResult {
    data object AwaitingReady : NativeLaunchRuntimeStartResult

    data class Failed(
        val failure: NativeLaunchFailure,
        val ownership: NativeLaunchRuntimeOwnership
    ) : NativeLaunchRuntimeStartResult
}

internal sealed interface NativeLaunchRuntimeStopResult {
    data object AwaitingTeardown : NativeLaunchRuntimeStopResult

    data object TeardownConfirmed : NativeLaunchRuntimeStopResult

    data object TeardownUncertain : NativeLaunchRuntimeStopResult
}

/**
 * A non-blocking adapter implemented by the Android Unity host. Revocation must fence Unity focus
 * and input before stop or detach begins. Neither method may emit completion or navigation.
 */
internal interface NativeLaunchRuntimePort {
    fun startUnity(generation: Long): NativeLaunchRuntimeStartResult

    fun revokeInputAndStopUnity(generation: Long): NativeLaunchRuntimeStopResult
}

internal data class NativeLaunchFallbackHostSnapshot(
    val launch: NativeLaunchFallbackSnapshot,
    val presentation: NativeLaunchFallbackPresentation
)

/**
 * Drives one native launch surface and at most one Unity owner at a time.
 *
 * The controller contains no save, profile, onboarding, completion, reward, or navigation effect.
 * Runtime callbacks only move presentation ownership for the matching process-local generation.
 */
internal class NativeLaunchFallbackHostController(
    private val runtime: NativeLaunchRuntimePort,
    generationSource: NativeLaunchAttemptGenerationSource =
        NativeLaunchProcessGenerationSource
) {
    private val coordinator = NativeLaunchFallbackCoordinator(
        generationSource = generationSource
    )

    @Synchronized
    fun snapshot(): NativeLaunchFallbackHostSnapshot = hostSnapshot()

    @Synchronized
    fun begin(
        preference: NativeLaunchPresentationPreference
    ): NativeLaunchFallbackHostSnapshot {
        return drive(coordinator.begin(preference))
    }

    @Synchronized
    fun runtimeReady(generation: Long): NativeLaunchFallbackHostSnapshot {
        return drive(coordinator.runtimeReady(generation))
    }

    @Synchronized
    fun runtimeFailed(
        generation: Long,
        failure: NativeLaunchFailure,
        ownership: NativeLaunchRuntimeOwnership
    ): NativeLaunchFallbackHostSnapshot {
        return drive(coordinator.fail(generation, failure, ownership))
    }

    @Synchronized
    fun teardownConfirmed(generation: Long): NativeLaunchFallbackHostSnapshot {
        return drive(coordinator.teardownConfirmed(generation))
    }

    @Synchronized
    fun teardownUncertain(generation: Long): NativeLaunchFallbackHostSnapshot {
        return drive(coordinator.teardownUncertain(generation))
    }

    @Synchronized
    fun retry(generation: Long): NativeLaunchFallbackHostSnapshot {
        return drive(coordinator.retry(generation))
    }

    private fun drive(
        initialTransition: NativeLaunchFallbackTransition
    ): NativeLaunchFallbackHostSnapshot {
        var transition = initialTransition
        while (true) {
            when (val effect = transition.effect) {
                null -> return hostSnapshot()

                is NativeLaunchFallbackEffect.StartUnity -> {
                    val startResult = runCatching {
                        runtime.startUnity(effect.generation)
                    }.getOrElse {
                        NativeLaunchRuntimeStartResult.Failed(
                            failure = NativeLaunchFailure.ConstructionFailed,
                            ownership = NativeLaunchRuntimeOwnership.Uncertain
                        )
                    }
                    transition = when (startResult) {
                        NativeLaunchRuntimeStartResult.AwaitingReady -> return hostSnapshot()
                        is NativeLaunchRuntimeStartResult.Failed -> coordinator.fail(
                            generation = effect.generation,
                            failure = startResult.failure,
                            ownership = startResult.ownership
                        )
                    }
                }

                is NativeLaunchFallbackEffect.StopUnity -> {
                    val stopResult = runCatching {
                        runtime.revokeInputAndStopUnity(effect.generation)
                    }.getOrDefault(NativeLaunchRuntimeStopResult.TeardownUncertain)
                    transition = when (stopResult) {
                        NativeLaunchRuntimeStopResult.AwaitingTeardown -> return hostSnapshot()
                        NativeLaunchRuntimeStopResult.TeardownConfirmed ->
                            coordinator.teardownConfirmed(effect.generation)
                        NativeLaunchRuntimeStopResult.TeardownUncertain ->
                            coordinator.teardownUncertain(effect.generation)
                    }
                }
            }
        }
    }

    private fun hostSnapshot(): NativeLaunchFallbackHostSnapshot {
        val launch = coordinator.snapshot()
        return NativeLaunchFallbackHostSnapshot(
            launch = launch,
            presentation = NativeLaunchFallbackPresentationMapper.from(launch)
        )
    }
}
