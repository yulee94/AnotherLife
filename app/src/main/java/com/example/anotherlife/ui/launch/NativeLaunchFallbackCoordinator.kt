package com.example.anotherlife.ui.launch

/**
 * Serializes ownership of the native launch surface and one embedded Unity runtime attempt.
 *
 * This coordinator deliberately has no completion, navigation, save, or profile effect. Showing a
 * fallback proves presentation availability only. It never grants cinematic or onboarding
 * completion. A fatal process failure cannot be recovered in-process; the next cold start creates
 * a new coordinator and infers no completion.
 */
internal class NativeLaunchFallbackCoordinator(
    initialGeneration: Long = 0L,
    private val generationSource: NativeLaunchAttemptGenerationSource =
        SequentialNativeLaunchAttemptGenerationSource(initialGeneration)
) {
    private var current = NativeLaunchFallbackSnapshot(
        state = NativeLaunchFallbackState.NativeReady,
        generation = initialGeneration
    )

    init {
        require(initialGeneration >= 0L)
    }

    @Synchronized
    fun snapshot(): NativeLaunchFallbackSnapshot = current

    @Synchronized
    fun begin(
        preference: NativeLaunchPresentationPreference
    ): NativeLaunchFallbackTransition {
        if (current.state != NativeLaunchFallbackState.NativeReady) return unchanged()
        if (preference == NativeLaunchPresentationPreference.StaticFallback) {
            current = current.copy(
                state = NativeLaunchFallbackState.FallbackVisible,
                fallbackReason = NativeLaunchFallbackReason.ReducedMotion,
                retryAvailable = false
            )
            return transition()
        }

        return startNextGeneration()
    }

    @Synchronized
    fun runtimeReady(generation: Long): NativeLaunchFallbackTransition {
        if (!accepts(generation, NativeLaunchFallbackState.StartingUnity)) return unchanged()

        current = current.copy(state = NativeLaunchFallbackState.UnityActive)
        return transition()
    }

    @Synchronized
    fun fail(
        generation: Long,
        failure: NativeLaunchFailure,
        ownership: NativeLaunchRuntimeOwnership
    ): NativeLaunchFallbackTransition {
        val currentState = current.state
        val isLaunchState = currentState == NativeLaunchFallbackState.StartingUnity ||
            currentState == NativeLaunchFallbackState.UnityActive
        if (generation != current.generation || !isLaunchState) {
            return unchanged()
        }

        val fallbackReason = reasonFor(failure)
        if (
            currentState == NativeLaunchFallbackState.UnityActive &&
            ownership == NativeLaunchRuntimeOwnership.NeverCreated
        ) {
            return terminalCleanupUncertain()
        }

        return when (ownership) {
            NativeLaunchRuntimeOwnership.NeverCreated -> {
                current = fallbackSnapshot(fallbackReason)
                transition()
            }

            NativeLaunchRuntimeOwnership.Active -> {
                current = current.copy(
                    state = NativeLaunchFallbackState.StoppingUnity,
                    fallbackReason = fallbackReason,
                    retryAvailable = false
                )
                transition(NativeLaunchFallbackEffect.StopUnity(generation))
            }

            NativeLaunchRuntimeOwnership.Uncertain -> terminalCleanupUncertain()
        }
    }

    @Synchronized
    fun teardownConfirmed(generation: Long): NativeLaunchFallbackTransition {
        if (!accepts(generation, NativeLaunchFallbackState.StoppingUnity)) return unchanged()

        val reason = current.fallbackReason ?: NativeLaunchFallbackReason.UnknownFailure
        current = current.copy(
            state = NativeLaunchFallbackState.FallbackVisible,
            fallbackReason = reason,
            retryAvailable = true
        )
        return transition()
    }

    @Synchronized
    fun teardownUncertain(generation: Long): NativeLaunchFallbackTransition {
        if (!accepts(generation, NativeLaunchFallbackState.StoppingUnity)) return unchanged()
        return terminalCleanupUncertain()
    }

    @Synchronized
    fun retry(generation: Long): NativeLaunchFallbackTransition {
        if (
            generation != current.generation ||
            current.state != NativeLaunchFallbackState.FallbackVisible ||
            !current.retryAvailable
        ) {
            return unchanged()
        }

        return startNextGeneration()
    }

    private fun startNextGeneration(): NativeLaunchFallbackTransition {
        val generation = runCatching(generationSource::nextGeneration).getOrNull()
        if (generation == null || generation <= current.generation) {
            current = current.copy(
                state = NativeLaunchFallbackState.TerminalRecovery,
                fallbackReason = NativeLaunchFallbackReason.GenerationExhausted,
                retryAvailable = false
            )
            return transition()
        }

        current = NativeLaunchFallbackSnapshot(
            state = NativeLaunchFallbackState.StartingUnity,
            generation = generation
        )
        return transition(NativeLaunchFallbackEffect.StartUnity(generation))
    }

    private fun fallbackSnapshot(
        reason: NativeLaunchFallbackReason
    ): NativeLaunchFallbackSnapshot {
        return current.copy(
            state = NativeLaunchFallbackState.FallbackVisible,
            fallbackReason = reason,
            retryAvailable = true
        )
    }

    private fun terminalCleanupUncertain(): NativeLaunchFallbackTransition {
        current = current.copy(
            state = NativeLaunchFallbackState.TerminalRecovery,
            fallbackReason = NativeLaunchFallbackReason.CleanupUncertain,
            retryAvailable = false
        )
        return transition()
    }

    private fun accepts(
        generation: Long,
        state: NativeLaunchFallbackState
    ): Boolean {
        return generation == current.generation && current.state == state
    }

    private fun transition(
        effect: NativeLaunchFallbackEffect? = null
    ): NativeLaunchFallbackTransition {
        return NativeLaunchFallbackTransition(current, effect)
    }

    private fun unchanged(): NativeLaunchFallbackTransition {
        return NativeLaunchFallbackTransition(current)
    }

    private fun reasonFor(failure: NativeLaunchFailure): NativeLaunchFallbackReason {
        return when (failure) {
            NativeLaunchFailure.RuntimeUnavailable ->
                NativeLaunchFallbackReason.RuntimeUnavailable
            NativeLaunchFailure.ConstructionFailed ->
                NativeLaunchFallbackReason.StartupFailed
            NativeLaunchFailure.RouteAdmissionFailed ->
                NativeLaunchFallbackReason.RouteUnavailable
            NativeLaunchFailure.ReadyTimeout ->
                NativeLaunchFallbackReason.ReadyTimeout
            NativeLaunchFailure.MediaUnavailable ->
                NativeLaunchFallbackReason.MediaUnavailable
            NativeLaunchFailure.MediaFailed ->
                NativeLaunchFallbackReason.MediaFailed
            NativeLaunchFailure.Unknown ->
                NativeLaunchFallbackReason.UnknownFailure
        }
    }
}
