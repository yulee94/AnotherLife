package com.example.anotherlife.ui.launch

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class NativeLaunchFallbackHostControllerTest {
    @Test
    fun reducedMotionNeverConstructsUnity() {
        val runtime = FakeRuntime()
        val host = host(runtime)

        val result = host.begin(NativeLaunchPresentationPreference.StaticFallback)

        assertEquals(NativeLaunchFallbackState.FallbackVisible, result.launch.state)
        assertEquals(NativeLaunchMessage.StaticPresentation, result.presentation.descriptor?.message)
        assertFalse(result.presentation.retryAvailable)
        assertTrue(runtime.starts.isEmpty())
        assertTrue(runtime.stops.isEmpty())
    }

    @Test
    fun onlyCurrentReadyAcknowledgementTransfersTheSurfaceToUnity() {
        val runtime = FakeRuntime()
        val host = host(runtime)
        val starting = host.begin(NativeLaunchPresentationPreference.Cinematic)

        val stale = host.runtimeReady(starting.launch.generation - 1L)
        val active = host.runtimeReady(starting.launch.generation)
        val duplicate = host.runtimeReady(starting.launch.generation)

        assertEquals(listOf(1L), runtime.starts)
        assertEquals(NativeLaunchFallbackState.StartingUnity, stale.launch.state)
        assertEquals(NativeLaunchFallbackState.UnityActive, active.launch.state)
        assertFalse(active.presentation.isVisible)
        assertEquals(active, duplicate)
    }

    @Test
    fun failureBeforeConstructionShowsFallbackWithoutStoppingUnity() {
        val runtime = FakeRuntime(
            startResult = NativeLaunchRuntimeStartResult.Failed(
                NativeLaunchFailure.RuntimeUnavailable,
                NativeLaunchRuntimeOwnership.NeverCreated
            )
        )
        val host = host(runtime)

        val result = host.begin(NativeLaunchPresentationPreference.Cinematic)

        assertEquals(NativeLaunchFallbackState.FallbackVisible, result.launch.state)
        assertTrue(result.presentation.retryAvailable)
        assertEquals(listOf(1L), runtime.starts)
        assertTrue(runtime.stops.isEmpty())
    }

    @Test
    fun activeFailureRevokesAndStopsBeforeFallbackCanBeUsed() {
        val runtime = FakeRuntime(
            stopResult = NativeLaunchRuntimeStopResult.AwaitingTeardown
        )
        val host = host(runtime)
        val starting = host.begin(NativeLaunchPresentationPreference.Cinematic)
        host.runtimeReady(starting.launch.generation)

        val stopping = host.runtimeFailed(
            generation = starting.launch.generation,
            failure = NativeLaunchFailure.MediaFailed,
            ownership = NativeLaunchRuntimeOwnership.Active
        )

        assertEquals(listOf(1L), runtime.stops)
        assertEquals(NativeLaunchFallbackState.StoppingUnity, stopping.launch.state)
        assertFalse(stopping.presentation.retryAvailable)
        assertFalse(stopping.presentation.exitAvailable)

        val stale = host.teardownConfirmed(starting.launch.generation - 1L)
        assertEquals(NativeLaunchFallbackState.StoppingUnity, stale.launch.state)

        val fallback = host.teardownConfirmed(starting.launch.generation)
        assertEquals(NativeLaunchFallbackState.FallbackVisible, fallback.launch.state)
        assertTrue(fallback.presentation.retryAvailable)
        assertTrue(fallback.presentation.exitAvailable)
    }

    @Test
    fun synchronousFailureAndConfirmedTeardownConvergeInOneDrive() {
        val runtime = FakeRuntime(
            startResult = NativeLaunchRuntimeStartResult.Failed(
                NativeLaunchFailure.ConstructionFailed,
                NativeLaunchRuntimeOwnership.Active
            ),
            stopResult = NativeLaunchRuntimeStopResult.TeardownConfirmed
        )
        val host = host(runtime)

        val result = host.begin(NativeLaunchPresentationPreference.Cinematic)

        assertEquals(listOf(1L), runtime.starts)
        assertEquals(listOf(1L), runtime.stops)
        assertEquals(NativeLaunchFallbackState.FallbackVisible, result.launch.state)
        assertEquals(NativeLaunchFallbackReason.StartupFailed, result.launch.fallbackReason)
    }

    @Test
    fun uncertainOrThrowingRuntimeNeverCreatesAReplacement() {
        val throwingRuntime = object : NativeLaunchRuntimePort {
            var startCalls = 0

            override fun startUnity(generation: Long): NativeLaunchRuntimeStartResult {
                startCalls += 1
                error("construction failed without an ownership receipt")
            }

            override fun revokeInputAndStopUnity(
                generation: Long
            ): NativeLaunchRuntimeStopResult {
                error("must not be called")
            }
        }
        val host = NativeLaunchFallbackHostController(
            runtime = throwingRuntime,
            generationSource = SequentialNativeLaunchAttemptGenerationSource()
        )

        val terminal = host.begin(NativeLaunchPresentationPreference.Cinematic)
        val retry = host.retry(terminal.launch.generation)

        assertEquals(1, throwingRuntime.startCalls)
        assertEquals(NativeLaunchFallbackState.TerminalRecovery, terminal.launch.state)
        assertEquals(NativeLaunchFallbackReason.CleanupUncertain, terminal.launch.fallbackReason)
        assertEquals(terminal, retry)
    }

    @Test
    fun retryUsesANewGenerationAndRejectsOldCallbacks() {
        val runtime = FakeRuntime(
            startResult = NativeLaunchRuntimeStartResult.Failed(
                NativeLaunchFailure.RouteAdmissionFailed,
                NativeLaunchRuntimeOwnership.NeverCreated
            )
        )
        val host = host(runtime)
        val fallback = host.begin(NativeLaunchPresentationPreference.Cinematic)
        runtime.startResult = NativeLaunchRuntimeStartResult.AwaitingReady

        val retry = host.retry(fallback.launch.generation)
        val stale = host.runtimeReady(fallback.launch.generation)

        assertEquals(listOf(1L, 2L), runtime.starts)
        assertEquals(2L, retry.launch.generation)
        assertEquals(NativeLaunchFallbackState.StartingUnity, stale.launch.state)
        assertEquals(2L, stale.launch.generation)
    }

    @Test
    fun sharedSourceKeepsRecreatedHostsOnDistinctGenerations() {
        val source = SequentialNativeLaunchAttemptGenerationSource()
        val firstRuntime = FakeRuntime()
        val secondRuntime = FakeRuntime()
        val firstHost = NativeLaunchFallbackHostController(firstRuntime, source)
        val secondHost = NativeLaunchFallbackHostController(secondRuntime, source)

        val first = firstHost.begin(NativeLaunchPresentationPreference.Cinematic)
        val second = secondHost.begin(NativeLaunchPresentationPreference.Cinematic)

        assertEquals(1L, first.launch.generation)
        assertEquals(2L, second.launch.generation)
        assertEquals(listOf(1L), firstRuntime.starts)
        assertEquals(listOf(2L), secondRuntime.starts)
    }

    private fun host(runtime: FakeRuntime): NativeLaunchFallbackHostController {
        return NativeLaunchFallbackHostController(
            runtime = runtime,
            generationSource = SequentialNativeLaunchAttemptGenerationSource()
        )
    }

    private class FakeRuntime(
        var startResult: NativeLaunchRuntimeStartResult =
            NativeLaunchRuntimeStartResult.AwaitingReady,
        var stopResult: NativeLaunchRuntimeStopResult =
            NativeLaunchRuntimeStopResult.AwaitingTeardown
    ) : NativeLaunchRuntimePort {
        val starts = mutableListOf<Long>()
        val stops = mutableListOf<Long>()

        override fun startUnity(generation: Long): NativeLaunchRuntimeStartResult {
            starts += generation
            return startResult
        }

        override fun revokeInputAndStopUnity(
            generation: Long
        ): NativeLaunchRuntimeStopResult {
            stops += generation
            return stopResult
        }
    }
}
