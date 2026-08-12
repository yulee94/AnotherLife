package com.example.anotherlife.ui.launch

import java.util.Collections
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlin.concurrent.thread
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class NativeLaunchFallbackCoordinatorTest {
    @Test
    fun initialStateIsNativeOwnedAndHasNoAuthoritySideEffects() {
        val snapshot = NativeLaunchFallbackCoordinator().snapshot()

        assertEquals(NativeLaunchFallbackState.NativeReady, snapshot.state)
        assertEquals(0L, snapshot.generation)
        assertNull(snapshot.fallbackReason)
        assertFalse(snapshot.retryAvailable)
    }

    @Test
    fun cinematicBeginStartsExactlyOneNewGeneration() {
        val coordinator = NativeLaunchFallbackCoordinator()

        val first = coordinator.begin(NativeLaunchPresentationPreference.Cinematic)
        val duplicate = coordinator.begin(NativeLaunchPresentationPreference.Cinematic)

        assertEquals(NativeLaunchFallbackState.StartingUnity, first.snapshot.state)
        assertEquals(1L, first.snapshot.generation)
        assertEquals(NativeLaunchFallbackEffect.StartUnity(1L), first.effect)
        assertEquals(first.snapshot, duplicate.snapshot)
        assertNull(duplicate.effect)
    }

    @Test
    fun concurrentBeginCallsEmitOnlyOneStartCommand() {
        val coordinator = NativeLaunchFallbackCoordinator()
        val ready = CountDownLatch(1)
        val done = CountDownLatch(24)
        val effects = Collections.synchronizedList(
            mutableListOf<NativeLaunchFallbackEffect>()
        )

        repeat(24) {
            thread {
                ready.await()
                coordinator.begin(NativeLaunchPresentationPreference.Cinematic)
                    .effect
                    ?.let(effects::add)
                done.countDown()
            }
        }
        ready.countDown()

        assertTrue(done.await(5, TimeUnit.SECONDS))
        assertEquals(listOf(NativeLaunchFallbackEffect.StartUnity(1L)), effects)
        assertEquals(NativeLaunchFallbackState.StartingUnity, coordinator.snapshot().state)
    }

    @Test
    fun staticPreferenceShowsDecoderIndependentFallbackWithoutStartingUnity() {
        val transition = NativeLaunchFallbackCoordinator().begin(
            NativeLaunchPresentationPreference.StaticFallback
        )

        assertEquals(NativeLaunchFallbackState.FallbackVisible, transition.snapshot.state)
        assertEquals(
            NativeLaunchFallbackReason.ReducedMotion,
            transition.snapshot.fallbackReason
        )
        assertFalse(transition.snapshot.retryAvailable)
        assertNull(transition.effect)
    }

    @Test
    fun readyAcknowledgementActivatesOnlyTheCurrentStartingGeneration() {
        val coordinator = NativeLaunchFallbackCoordinator()
        val start = coordinator.begin(NativeLaunchPresentationPreference.Cinematic)

        val stale = coordinator.runtimeReady(start.snapshot.generation - 1L)
        val ready = coordinator.runtimeReady(start.snapshot.generation)
        val duplicate = coordinator.runtimeReady(start.snapshot.generation)

        assertEquals(NativeLaunchFallbackState.StartingUnity, stale.snapshot.state)
        assertEquals(NativeLaunchFallbackState.UnityActive, ready.snapshot.state)
        assertEquals(ready.snapshot, duplicate.snapshot)
        assertNull(duplicate.effect)
    }

    @Test
    fun failureBeforeRuntimeCreationCanRevealFallbackImmediately() {
        val coordinator = startedCoordinator()

        val transition = coordinator.fail(
            generation = 1L,
            failure = NativeLaunchFailure.RuntimeUnavailable,
            ownership = NativeLaunchRuntimeOwnership.NeverCreated
        )

        assertEquals(NativeLaunchFallbackState.FallbackVisible, transition.snapshot.state)
        assertEquals(
            NativeLaunchFallbackReason.RuntimeUnavailable,
            transition.snapshot.fallbackReason
        )
        assertTrue(transition.snapshot.retryAvailable)
        assertNull(transition.effect)
    }

    @Test
    fun activeRuntimeFailureMustStopBeforeFallbackBecomesVisible() {
        val coordinator = startedCoordinator()
        coordinator.runtimeReady(1L)

        val failure = coordinator.fail(
            generation = 1L,
            failure = NativeLaunchFailure.MediaFailed,
            ownership = NativeLaunchRuntimeOwnership.Active
        )
        val stopped = coordinator.teardownConfirmed(1L)

        assertEquals(NativeLaunchFallbackState.StoppingUnity, failure.snapshot.state)
        assertEquals(NativeLaunchFallbackEffect.StopUnity(1L), failure.effect)
        assertEquals(NativeLaunchFallbackState.FallbackVisible, stopped.snapshot.state)
        assertEquals(NativeLaunchFallbackReason.MediaFailed, stopped.snapshot.fallbackReason)
        assertTrue(stopped.snapshot.retryAvailable)
    }

    @Test
    fun uncertainOwnershipNeverClaimsAUsableFallback() {
        val coordinator = startedCoordinator()

        val transition = coordinator.fail(
            generation = 1L,
            failure = NativeLaunchFailure.ConstructionFailed,
            ownership = NativeLaunchRuntimeOwnership.Uncertain
        )

        assertEquals(NativeLaunchFallbackState.TerminalRecovery, transition.snapshot.state)
        assertEquals(
            NativeLaunchFallbackReason.CleanupUncertain,
            transition.snapshot.fallbackReason
        )
        assertFalse(transition.snapshot.retryAvailable)
        assertNull(transition.effect)
    }

    @Test
    fun uncertainTeardownEntersTerminalRecovery() {
        val coordinator = startedCoordinator()
        coordinator.fail(
            generation = 1L,
            failure = NativeLaunchFailure.ReadyTimeout,
            ownership = NativeLaunchRuntimeOwnership.Active
        )

        val transition = coordinator.teardownUncertain(1L)

        assertEquals(NativeLaunchFallbackState.TerminalRecovery, transition.snapshot.state)
        assertEquals(
            NativeLaunchFallbackReason.CleanupUncertain,
            transition.snapshot.fallbackReason
        )
        assertFalse(transition.snapshot.retryAvailable)
    }

    @Test
    fun retryCreatesANewGenerationAndSuppressesDoubleTap() {
        val coordinator = startedCoordinator()
        coordinator.fail(
            generation = 1L,
            failure = NativeLaunchFailure.RouteAdmissionFailed,
            ownership = NativeLaunchRuntimeOwnership.NeverCreated
        )

        val retry = coordinator.retry(1L)
        val duplicate = coordinator.retry(1L)

        assertEquals(NativeLaunchFallbackState.StartingUnity, retry.snapshot.state)
        assertEquals(2L, retry.snapshot.generation)
        assertEquals(NativeLaunchFallbackEffect.StartUnity(2L), retry.effect)
        assertEquals(retry.snapshot, duplicate.snapshot)
        assertNull(duplicate.effect)
    }

    @Test
    fun staleFailureAndTeardownCallbacksPerformNoWork() {
        val coordinator = startedCoordinator()
        coordinator.fail(
            generation = 1L,
            failure = NativeLaunchFailure.RuntimeUnavailable,
            ownership = NativeLaunchRuntimeOwnership.NeverCreated
        )
        coordinator.retry(1L)

        val staleFailure = coordinator.fail(
            generation = 1L,
            failure = NativeLaunchFailure.Unknown,
            ownership = NativeLaunchRuntimeOwnership.Uncertain
        )
        val staleTeardown = coordinator.teardownConfirmed(1L)

        assertEquals(NativeLaunchFallbackState.StartingUnity, staleFailure.snapshot.state)
        assertEquals(2L, staleFailure.snapshot.generation)
        assertEquals(staleFailure.snapshot, staleTeardown.snapshot)
        assertNull(staleFailure.effect)
        assertNull(staleTeardown.effect)
    }

    @Test
    fun duplicateFailureWhileStoppingCannotEmitAnotherStop() {
        val coordinator = startedCoordinator()
        val first = coordinator.fail(
            generation = 1L,
            failure = NativeLaunchFailure.ReadyTimeout,
            ownership = NativeLaunchRuntimeOwnership.Active
        )
        val duplicate = coordinator.fail(
            generation = 1L,
            failure = NativeLaunchFailure.MediaFailed,
            ownership = NativeLaunchRuntimeOwnership.Active
        )

        assertEquals(NativeLaunchFallbackEffect.StopUnity(1L), first.effect)
        assertEquals(first.snapshot, duplicate.snapshot)
        assertNull(duplicate.effect)
    }

    @Test
    fun generationExhaustionFailsClosedWithoutStartingAnotherRuntime() {
        val coordinator = NativeLaunchFallbackCoordinator(initialGeneration = Long.MAX_VALUE)

        val transition = coordinator.begin(NativeLaunchPresentationPreference.Cinematic)

        assertEquals(NativeLaunchFallbackState.TerminalRecovery, transition.snapshot.state)
        assertEquals(
            NativeLaunchFallbackReason.GenerationExhausted,
            transition.snapshot.fallbackReason
        )
        assertFalse(transition.snapshot.retryAvailable)
        assertNull(transition.effect)
    }

    @Test(expected = IllegalArgumentException::class)
    fun negativeInitialGenerationIsRejected() {
        NativeLaunchFallbackCoordinator(initialGeneration = -1L)
    }

    @Test
    fun allFailuresMapToTypedPresentationReasons() {
        val expected = mapOf(
            NativeLaunchFailure.RuntimeUnavailable to NativeLaunchFallbackReason.RuntimeUnavailable,
            NativeLaunchFailure.ConstructionFailed to NativeLaunchFallbackReason.StartupFailed,
            NativeLaunchFailure.RouteAdmissionFailed to NativeLaunchFallbackReason.RouteUnavailable,
            NativeLaunchFailure.ReadyTimeout to NativeLaunchFallbackReason.ReadyTimeout,
            NativeLaunchFailure.MediaUnavailable to NativeLaunchFallbackReason.MediaUnavailable,
            NativeLaunchFailure.MediaFailed to NativeLaunchFallbackReason.MediaFailed,
            NativeLaunchFailure.Unknown to NativeLaunchFallbackReason.UnknownFailure
        )

        expected.forEach { (failure, reason) ->
            val coordinator = startedCoordinator()
            val transition = coordinator.fail(
                generation = 1L,
                failure = failure,
                ownership = NativeLaunchRuntimeOwnership.NeverCreated
            )

            assertEquals(reason, transition.snapshot.fallbackReason)
        }
    }

    @Test
    fun contradictoryActiveRuntimeOwnershipFailsClosed() {
        val coordinator = startedCoordinator()
        coordinator.runtimeReady(1L)

        val transition = coordinator.fail(
            generation = 1L,
            failure = NativeLaunchFailure.RuntimeUnavailable,
            ownership = NativeLaunchRuntimeOwnership.NeverCreated
        )

        assertEquals(NativeLaunchFallbackState.TerminalRecovery, transition.snapshot.state)
        assertEquals(
            NativeLaunchFallbackReason.CleanupUncertain,
            transition.snapshot.fallbackReason
        )
        assertFalse(transition.snapshot.retryAvailable)
        assertNull(transition.effect)
    }

    @Test
    fun terminalRecoveryRejectsRetryAndLateCallbacks() {
        val coordinator = startedCoordinator()
        val terminal = coordinator.fail(
            generation = 1L,
            failure = NativeLaunchFailure.ConstructionFailed,
            ownership = NativeLaunchRuntimeOwnership.Uncertain
        )

        val retry = coordinator.retry(1L)
        val ready = coordinator.runtimeReady(1L)
        val teardown = coordinator.teardownConfirmed(1L)

        assertEquals(terminal.snapshot, retry.snapshot)
        assertEquals(terminal.snapshot, ready.snapshot)
        assertEquals(terminal.snapshot, teardown.snapshot)
        assertNull(retry.effect)
        assertNull(ready.effect)
        assertNull(teardown.effect)
    }

    @Test
    fun everyEffectIsLimitedToUnityOwnershipCommands() {
        val coordinator = startedCoordinator()
        val startEffect = NativeLaunchFallbackCoordinator()
            .begin(NativeLaunchPresentationPreference.Cinematic)
            .effect
        val stopEffect = coordinator.fail(
            generation = 1L,
            failure = NativeLaunchFailure.MediaUnavailable,
            ownership = NativeLaunchRuntimeOwnership.Active
        ).effect

        assertTrue(startEffect is NativeLaunchFallbackEffect.StartUnity)
        assertTrue(stopEffect is NativeLaunchFallbackEffect.StopUnity)
    }

    private fun startedCoordinator(): NativeLaunchFallbackCoordinator {
        return NativeLaunchFallbackCoordinator().also {
            it.begin(NativeLaunchPresentationPreference.Cinematic)
        }
    }
}
