package com.example.anotherlife.ui.launch

import com.example.anotherlife.ui.unity.UnityRuntimeContainerFailure
import com.example.anotherlife.ui.unity.UnityRuntimeContainerOwnership
import com.example.anotherlife.ui.unity.UnityRuntimeContainerPhase
import com.example.anotherlife.ui.unity.UnityRuntimeContainerSnapshot
import com.example.anotherlife.ui.unity.UnityRuntimeContainerTeardownEvidence
import com.example.anotherlife.ui.unity.UnityRuntimeContainerTeardownResult
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class NativeLaunchUnityRuntimePortTest {
    @Test
    fun missingRuntimeNeverClaimsAnOwner() {
        val port = NativeLaunchUnityRuntimePort { null }

        val result = port.startUnity(1L)

        assertEquals(
            NativeLaunchRuntimeStartResult.Failed(
                NativeLaunchFailure.RuntimeUnavailable,
                NativeLaunchRuntimeOwnership.NeverCreated
            ),
            result
        )
        assertNull(port.activeGenerationForTesting())
    }

    @Test
    fun throwingFactoryFailsClosedBecauseNoHandleCanProveCleanup() {
        val port = NativeLaunchUnityRuntimePort {
            error("synthetic native factory failure")
        }

        val result = port.startUnity(1L)

        assertEquals(
            NativeLaunchRuntimeStartResult.Failed(
                NativeLaunchFailure.ConstructionFailed,
                NativeLaunchRuntimeOwnership.Uncertain
            ),
            result
        )
    }

    @Test
    fun dispatchedRouteStillWaitsForCorrelatedUnityReadyAcknowledgement() {
        val attempt = FakeAttempt(
            startSnapshot = snapshot(
                phase = UnityRuntimeContainerPhase.Active,
                ownership = UnityRuntimeContainerOwnership.Active,
                routeDispatched = true
            )
        )
        val port = NativeLaunchUnityRuntimePort { attempt }

        val result = port.startUnity(7L)

        assertEquals(NativeLaunchRuntimeStartResult.AwaitingReady, result)
        assertEquals(7L, port.activeGenerationForTesting())
    }

    @Test
    fun hostTransfersPresentationOnlyAfterExplicitCorrelatedReadySignal() {
        val attempt = FakeAttempt(
            startSnapshot = snapshot(
                phase = UnityRuntimeContainerPhase.Active,
                ownership = UnityRuntimeContainerOwnership.Active,
                routeDispatched = true
            )
        )
        val port = NativeLaunchUnityRuntimePort { attempt }
        val host = NativeLaunchFallbackHostController(
            runtime = port,
            generationSource = SequentialNativeLaunchAttemptGenerationSource()
        )

        val dispatched = host.begin(NativeLaunchPresentationPreference.Cinematic)
        val ready = host.runtimeReady(dispatched.launch.generation)

        assertEquals(NativeLaunchFallbackState.StartingUnity, dispatched.launch.state)
        assertEquals(NativeLaunchFallbackState.UnityActive, ready.launch.state)
        assertEquals(listOf("start"), attempt.calls)
    }

    @Test
    fun bridgeAdmissionFailureIsStoppedBeforeFallbackBecomesInteractive() {
        val attempt = FakeAttempt(
            startSnapshot = snapshot(
                phase = UnityRuntimeContainerPhase.Failed,
                ownership = UnityRuntimeContainerOwnership.Active,
                failure = UnityRuntimeContainerFailure.BridgeProtocolFailed
            )
        )
        val port = NativeLaunchUnityRuntimePort { attempt }
        val host = NativeLaunchFallbackHostController(
            runtime = port,
            generationSource = SequentialNativeLaunchAttemptGenerationSource()
        )

        val fallback = host.begin(NativeLaunchPresentationPreference.Cinematic)

        assertEquals(NativeLaunchFallbackState.FallbackVisible, fallback.launch.state)
        assertEquals(
            NativeLaunchFallbackReason.RouteUnavailable,
            fallback.launch.fallbackReason
        )
        assertEquals(listOf("start", "revoke", "destroy"), attempt.calls)
    }

    @Test
    fun failedReservationIsReleasedBeforeFallbackCanAppear() {
        val attempts = ArrayDeque(
            listOf(
                FakeAttempt(
                    startSnapshot = snapshot(
                        phase = UnityRuntimeContainerPhase.Failed,
                        failure = UnityRuntimeContainerFailure.RuntimeUnavailable
                    ),
                    teardownResult = UnityRuntimeContainerTeardownResult.Confirmed
                ),
                FakeAttempt(
                    startSnapshot = snapshot(
                        UnityRuntimeContainerPhase.Active,
                        UnityRuntimeContainerOwnership.Active
                    )
                )
            )
        )
        val port = NativeLaunchUnityRuntimePort { attempts.removeFirst() }

        val failed = port.startUnity(1L)
        val retry = port.startUnity(2L)

        assertEquals(
            NativeLaunchRuntimeStartResult.Failed(
                NativeLaunchFailure.RuntimeUnavailable,
                NativeLaunchRuntimeOwnership.NeverCreated
            ),
            failed
        )
        assertEquals(NativeLaunchRuntimeStartResult.AwaitingReady, retry)
        assertEquals(2L, port.activeGenerationForTesting())
    }

    @Test
    fun activeFailureRevokesInputBeforeConfirmedDestroy() {
        val attempt = FakeAttempt(
            startSnapshot = snapshot(
                UnityRuntimeContainerPhase.Active,
                UnityRuntimeContainerOwnership.Active
            )
        )
        val port = NativeLaunchUnityRuntimePort { attempt }
        port.startUnity(3L)

        val result = port.revokeInputAndStopUnity(3L)

        assertEquals(NativeLaunchRuntimeStopResult.TeardownConfirmed, result)
        assertEquals(listOf("start", "revoke", "destroy"), attempt.calls)
        assertNull(port.activeGenerationForTesting())
    }

    @Test
    fun failedInputFenceMakesOtherwiseConfirmedDestroyUncertain() {
        val attempt = FakeAttempt(
            startSnapshot = snapshot(
                UnityRuntimeContainerPhase.Active,
                UnityRuntimeContainerOwnership.Active
            ),
            inputRevoked = false
        )
        val port = NativeLaunchUnityRuntimePort { attempt }
        port.startUnity(4L)

        val result = port.revokeInputAndStopUnity(4L)

        assertEquals(NativeLaunchRuntimeStopResult.TeardownUncertain, result)
        assertEquals(listOf("start", "revoke", "destroy"), attempt.calls)
        assertEquals(4L, port.activeGenerationForTesting())
    }

    @Test
    fun asynchronousTeardownKeepsAttemptUntilMatchingConfirmation() {
        val attempt = FakeAttempt(
            startSnapshot = snapshot(
                UnityRuntimeContainerPhase.Active,
                UnityRuntimeContainerOwnership.Active
            ),
            teardownResult = UnityRuntimeContainerTeardownResult.AwaitingCleanup
        )
        val port = NativeLaunchUnityRuntimePort { attempt }
        port.startUnity(5L)

        val awaiting = port.revokeInputAndStopUnity(5L)
        val stale = port.recordTeardown(
            4L,
            UnityRuntimeContainerTeardownResult.Confirmed
        )
        val confirmed = port.recordTeardown(
            5L,
            UnityRuntimeContainerTeardownResult.Confirmed
        )

        assertEquals(NativeLaunchRuntimeStopResult.AwaitingTeardown, awaiting)
        assertEquals(NativeLaunchRuntimeStopResult.TeardownUncertain, stale)
        assertEquals(NativeLaunchRuntimeStopResult.TeardownConfirmed, confirmed)
        assertNull(port.activeGenerationForTesting())
    }

    @Test
    fun teardownConfirmationCannotBypassTheInputFence() {
        val attempt = FakeAttempt(
            startSnapshot = snapshot(
                UnityRuntimeContainerPhase.Active,
                UnityRuntimeContainerOwnership.Active
            )
        )
        val port = NativeLaunchUnityRuntimePort { attempt }
        port.startUnity(6L)

        val result = port.recordTeardown(
            6L,
            UnityRuntimeContainerTeardownResult.Confirmed
        )

        assertEquals(NativeLaunchRuntimeStopResult.TeardownUncertain, result)
        assertEquals(6L, port.activeGenerationForTesting())
    }

    @Test
    fun lateConfirmationCannotRepairAFailedInputFence() {
        val attempt = FakeAttempt(
            startSnapshot = snapshot(
                UnityRuntimeContainerPhase.Active,
                UnityRuntimeContainerOwnership.Active
            ),
            inputRevoked = false,
            teardownResult = UnityRuntimeContainerTeardownResult.AwaitingCleanup
        )
        val port = NativeLaunchUnityRuntimePort { attempt }
        port.startUnity(9L)
        port.revokeInputAndStopUnity(9L)

        val result = port.recordTeardown(
            9L,
            UnityRuntimeContainerTeardownResult.Confirmed
        )

        assertEquals(NativeLaunchRuntimeStopResult.TeardownUncertain, result)
        assertEquals(9L, port.activeGenerationForTesting())
    }

    @Test
    fun lateConfirmationCannotRepairAnUncertainDestroy() {
        val attempt = FakeAttempt(
            startSnapshot = snapshot(
                UnityRuntimeContainerPhase.Active,
                UnityRuntimeContainerOwnership.Active
            ),
            teardownResult = UnityRuntimeContainerTeardownResult.Uncertain
        )
        val port = NativeLaunchUnityRuntimePort { attempt }
        port.startUnity(10L)
        port.revokeInputAndStopUnity(10L)

        val result = port.recordTeardown(
            10L,
            UnityRuntimeContainerTeardownResult.Confirmed
        )

        assertEquals(NativeLaunchRuntimeStopResult.TeardownUncertain, result)
        assertEquals(10L, port.activeGenerationForTesting())
    }

    @Test
    fun secondGenerationCannotStartBeforeFirstTeardown() {
        var factoryCalls = 0
        val port = NativeLaunchUnityRuntimePort {
            factoryCalls += 1
            FakeAttempt(
                startSnapshot = snapshot(
                    UnityRuntimeContainerPhase.Active,
                    UnityRuntimeContainerOwnership.Active
                )
            )
        }
        port.startUnity(1L)

        val result = port.startUnity(2L)

        assertEquals(1, factoryCalls)
        assertEquals(
            NativeLaunchRuntimeStartResult.Failed(
                NativeLaunchFailure.ConstructionFailed,
                NativeLaunchRuntimeOwnership.Uncertain
            ),
            result
        )
    }

    @Test
    fun startThrowCanRecoverOnlyAfterOrderedConfirmedCleanup() {
        val attempt = FakeAttempt(
            startSnapshot = snapshot(UnityRuntimeContainerPhase.Activating),
            throwOnStart = true
        )
        val port = NativeLaunchUnityRuntimePort { attempt }

        val result = port.startUnity(8L)

        assertEquals(listOf("start", "revoke", "destroy"), attempt.calls)
        assertEquals(
            NativeLaunchRuntimeStartResult.Failed(
                NativeLaunchFailure.ConstructionFailed,
                NativeLaunchRuntimeOwnership.NeverCreated
            ),
            result
        )
        assertNull(port.activeGenerationForTesting())
    }

    @Test
    fun destroyedWithoutConfirmedEvidenceMapsToUncertainOwnership() {
        val result = NativeLaunchUnityRuntimeEvidenceMapper.startResult(
            snapshot(
                phase = UnityRuntimeContainerPhase.Destroyed,
                ownership = UnityRuntimeContainerOwnership.Uncertain,
                teardown = UnityRuntimeContainerTeardownEvidence.Uncertain
            )
        )

        assertEquals(
            NativeLaunchRuntimeStartResult.Failed(
                NativeLaunchFailure.Unknown,
                NativeLaunchRuntimeOwnership.Uncertain
            ),
            result
        )
    }

    @Test
    fun inconsistentActiveEvidenceFailsClosed() {
        val result = NativeLaunchUnityRuntimeEvidenceMapper.startResult(
            snapshot(
                phase = UnityRuntimeContainerPhase.Active,
                ownership = UnityRuntimeContainerOwnership.NeverCreated,
                routeDispatched = true
            )
        )

        assertEquals(
            NativeLaunchRuntimeStartResult.Failed(
                NativeLaunchFailure.Unknown,
                NativeLaunchRuntimeOwnership.Uncertain
            ),
            result
        )
    }

    private class FakeAttempt(
        private val startSnapshot: UnityRuntimeContainerSnapshot,
        private val inputRevoked: Boolean = true,
        private val teardownResult: UnityRuntimeContainerTeardownResult =
            UnityRuntimeContainerTeardownResult.Confirmed,
        private val throwOnStart: Boolean = false
    ) : NativeLaunchUnityRuntimeAttempt {
        val calls = mutableListOf<String>()

        override fun start(): UnityRuntimeContainerSnapshot {
            calls += "start"
            if (throwOnStart) error("synthetic route start failure")
            return startSnapshot
        }

        override fun revokeInputAndFocus(): Boolean {
            calls += "revoke"
            return inputRevoked
        }

        override fun destroy(): UnityRuntimeContainerTeardownResult {
            calls += "destroy"
            return teardownResult
        }
    }

    private fun snapshot(
        phase: UnityRuntimeContainerPhase,
        ownership: UnityRuntimeContainerOwnership =
            UnityRuntimeContainerOwnership.NeverCreated,
        teardown: UnityRuntimeContainerTeardownEvidence =
            UnityRuntimeContainerTeardownEvidence.NotStarted,
        routeDispatched: Boolean = false,
        failure: UnityRuntimeContainerFailure? = null
    ) = UnityRuntimeContainerSnapshot(
        phase = phase,
        ownership = ownership,
        teardown = teardown,
        routeDispatched = routeDispatched,
        failure = failure
    )
}
