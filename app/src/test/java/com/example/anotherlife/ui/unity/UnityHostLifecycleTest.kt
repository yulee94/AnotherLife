package com.example.anotherlife.ui.unity

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class UnityHostLifecycleTest {
    @Test
    fun focusIsDeferredUntilResumeAndDuplicateSignalsAreIdempotent() {
        val runtime = RecordingRuntime()
        val lifecycle = UnityHostLifecycleController(runtime)

        lifecycle.onWindowFocusChanged(true)
        lifecycle.resume()
        lifecycle.resume()
        lifecycle.onWindowFocusChanged(true)

        assertEquals(listOf("resume", "focus:true"), runtime.calls)
        assertEquals(
            UnityHostLifecycleSnapshot(
                state = UnityHostLifecycleState.Resumed,
                hasWindowFocus = true,
                forwardedWindowFocus = true
            ),
            lifecycle.snapshot()
        )
    }

    @Test
    fun pauseAndStopClearFocusBeforePausingExactlyOnce() {
        val runtime = RecordingRuntime()
        val lifecycle = UnityHostLifecycleController(runtime)
        lifecycle.onWindowFocusChanged(true)
        lifecycle.resume()

        lifecycle.pause()
        lifecycle.stop()
        lifecycle.pause()

        assertEquals(
            listOf("resume", "focus:true", "focus:false", "pause"),
            runtime.calls
        )
        assertEquals(UnityHostLifecycleState.Paused, lifecycle.snapshot().state)
    }

    @Test
    fun focusRestoresOnlyAfterARealResume() {
        val runtime = RecordingRuntime()
        val lifecycle = UnityHostLifecycleController(runtime)
        lifecycle.onWindowFocusChanged(true)
        lifecycle.resume()
        lifecycle.pause()

        lifecycle.onWindowFocusChanged(true)
        lifecycle.resume()

        assertEquals(
            listOf(
                "resume",
                "focus:true",
                "focus:false",
                "pause",
                "resume",
                "focus:true"
            ),
            runtime.calls
        )
    }

    @Test
    fun destroyFromFocusedResumeOrdersFocusPauseDestroyAndRejectsLaterSignals() {
        val runtime = RecordingRuntime()
        val lifecycle = UnityHostLifecycleController(runtime)
        lifecycle.onWindowFocusChanged(true)
        lifecycle.resume()

        lifecycle.destroy()
        lifecycle.destroy()
        lifecycle.resume()
        lifecycle.onWindowFocusChanged(true)
        lifecycle.lowMemory()
        lifecycle.configurationChanged(Unit)

        assertEquals(
            listOf(
                "resume",
                "focus:true",
                "focus:false",
                "pause",
                "destroy"
            ),
            runtime.calls
        )
        assertEquals(UnityHostLifecycleState.Destroyed, lifecycle.snapshot().state)
    }

    @Test
    fun trimAndConfigurationCallbacksKeepExactOrderAndStopAfterDestroy() {
        val runtime = RecordingRuntime()
        val lifecycle = UnityHostLifecycleController(runtime)

        lifecycle.resume()
        lifecycle.trimMemory(9)
        lifecycle.trimMemory(10)
        lifecycle.configurationChanged(Unit)
        lifecycle.trimMemory(80)
        lifecycle.pause()
        lifecycle.configurationChanged(Unit)
        lifecycle.destroy()
        lifecycle.lowMemory()
        lifecycle.trimMemory(Int.MAX_VALUE)
        lifecycle.configurationChanged(Unit)

        assertEquals(
            listOf(
                "resume",
                "lowMemory",
                "configurationChanged",
                "lowMemory",
                "pause",
                "configurationChanged",
                "destroy"
            ),
            runtime.calls
        )
    }

    @Test
    fun resumeFailureIsContainedAndTeardownStillCompletes() {
        val runtime = RecordingRuntime(throwOn = setOf("resume"))
        val lifecycle = UnityHostLifecycleController(runtime)

        lifecycle.resume()

        assertEquals(UnityHostLifecycleState.Destroyed, lifecycle.snapshot().state)
        assertEquals(listOf("resume", "pause", "destroy"), runtime.calls)
        assertTrue(lifecycle.canReleaseOwnership())
    }

    @Test
    fun focusGainFailureIsContainedAndReachesTheFocusTruePath() {
        val runtime = RecordingRuntime(throwOn = setOf("focus:true"))
        val lifecycle = UnityHostLifecycleController(runtime)

        lifecycle.onWindowFocusChanged(true)
        lifecycle.resume()

        assertEquals(
            listOf("resume", "focus:true", "focus:false", "pause", "destroy"),
            runtime.calls
        )
        assertEquals(UnityHostLifecycleState.Destroyed, lifecycle.snapshot().state)
        assertTrue(lifecycle.canReleaseOwnership())
    }

    @Test
    fun directPauseFailureIsContainedAndReachesThePausePath() {
        val runtime = RecordingRuntime(throwOn = setOf("pause"))
        val lifecycle = UnityHostLifecycleController(runtime)
        lifecycle.resume()

        lifecycle.pause()

        assertEquals(listOf("resume", "pause", "destroy"), runtime.calls)
        assertEquals(UnityHostLifecycleState.Destroyed, lifecycle.snapshot().state)
        assertTrue(lifecycle.canReleaseOwnership())
    }

    @Test
    fun lowMemoryFailureIsContainedAndReachesTheLowMemoryPath() {
        val runtime = RecordingRuntime(throwOn = setOf("lowMemory"))
        val lifecycle = UnityHostLifecycleController(runtime)

        lifecycle.lowMemory()

        assertEquals(listOf("lowMemory", "destroy"), runtime.calls)
        assertEquals(UnityHostLifecycleState.Destroyed, lifecycle.snapshot().state)
        assertTrue(lifecycle.canReleaseOwnership())
    }

    @Test
    fun configurationFailureIsContainedAndReachesTheConfigurationPath() {
        val runtime = RecordingRuntime(throwOn = setOf("configurationChanged"))
        val lifecycle = UnityHostLifecycleController(runtime)

        lifecycle.configurationChanged(Unit)

        assertEquals(listOf("configurationChanged", "destroy"), runtime.calls)
        assertEquals(UnityHostLifecycleState.Destroyed, lifecycle.snapshot().state)
        assertTrue(lifecycle.canReleaseOwnership())
    }

    @Test
    fun failedRuntimeDestroyKeepsOwnershipFailClosed() {
        val runtime = RecordingRuntime(throwOn = setOf("destroy"))
        val lifecycle = UnityHostLifecycleController(runtime)

        lifecycle.destroy()
        lifecycle.resume()

        assertEquals(listOf("destroy"), runtime.calls)
        assertEquals(UnityHostLifecycleState.Destroyed, lifecycle.snapshot().state)
        assertEquals(false, lifecycle.snapshot().destroySucceeded)
        assertFalse(lifecycle.canReleaseOwnership())
    }

    @Test
    fun registryAllowsOnlyOneOwnerUntilExactLeaseIsReleased() {
        val registry = UnityRuntimeHostRegistry()

        val first = registry.tryAcquire()
        val denied = registry.tryAcquire()

        assertNotNull(first)
        assertNull(denied)
        assertTrue(registry.isOwner(first!!))
        assertTrue(registry.release(first))
        assertFalse(registry.isOwner(first))
        assertNotNull(registry.tryAcquire())
    }

    @Test
    fun staleLeaseCannotReleaseOrReplaceTheCurrentOwner() {
        val registry = UnityRuntimeHostRegistry()
        val stale = registry.tryAcquire()!!
        assertTrue(registry.release(stale))
        val current = registry.tryAcquire()!!

        assertFalse(registry.release(stale))
        assertTrue(registry.isOwner(current))
        assertNull(registry.tryAcquire())
        assertTrue(registry.release(current))
    }

    @Test
    fun deniedOwnerReceivesTheLeaseExactlyOnceAfterTheCurrentOwnerReleases() {
        val registry = UnityRuntimeHostRegistry(maxWaiters = 2)
        val owner = registry.tryAcquire()!!
        val grants = mutableListOf<UnityRuntimeHostLease>()

        val acquisition = registry.acquireOrQueue(grants::add)

        assertTrue(acquisition is UnityRuntimeHostAcquisition.Waiting)
        assertEquals(1, registry.waitingCount())
        assertTrue(registry.release(owner))
        assertEquals(1, grants.size)
        assertTrue(registry.isOwner(grants.single()))
        assertEquals(0, registry.waitingCount())
    }

    @Test
    fun waitQueueIsBoundedAndCancelledWaitersCannotAcquireLater() {
        val registry = UnityRuntimeHostRegistry(maxWaiters = 1)
        val owner = registry.tryAcquire()!!
        val grants = mutableListOf<UnityRuntimeHostLease>()
        val waiting = registry.acquireOrQueue(grants::add)

        assertTrue(waiting is UnityRuntimeHostAcquisition.Waiting)
        assertEquals(
            UnityRuntimeHostAcquisition.CapacityReached,
            registry.acquireOrQueue(grants::add)
        )
        assertTrue(registry.cancel((waiting as UnityRuntimeHostAcquisition.Waiting).token))
        assertTrue(registry.release(owner))
        assertTrue(grants.isEmpty())
        assertEquals(0, registry.waitingCount())
        val replacement = registry.tryAcquire()
        assertNotNull(replacement)
        assertTrue(registry.release(replacement!!))
    }

    @Test
    fun throwingWaiterCannotConsumeOrStrandTheTransferredLease() {
        val registry = UnityRuntimeHostRegistry(maxWaiters = 2)
        val owner = registry.tryAcquire()!!
        val grants = mutableListOf<UnityRuntimeHostLease>()

        assertTrue(
            registry.acquireOrQueue { error("synthetic rejected waiter") } is
                UnityRuntimeHostAcquisition.Waiting
        )
        assertTrue(
            registry.acquireOrQueue(grants::add) is UnityRuntimeHostAcquisition.Waiting
        )

        assertTrue(registry.release(owner))
        assertEquals(1, grants.size)
        assertTrue(registry.isOwner(grants.single()))
        assertTrue(registry.release(grants.single()))
        assertNotNull(registry.tryAcquire())
    }

    @Test
    fun callbackRegistrationFailureStaysUnregisteredAndNeverUnregisters() {
        val registrar = RecordingCallbackRegistrar(registerResult = false)
        val registration = UnityHostCallbackRegistration(registrar, "callbacks")

        assertFalse(registration.register())
        assertFalse(registration.isRegistered())
        assertTrue(registration.release())
        assertEquals(listOf("register:callbacks"), registrar.calls)
    }

    @Test
    fun callbackRegistrationReleaseIsIdempotentAndFailsClosedOnUncertainUnregister() {
        val registrar = RecordingCallbackRegistrar(unregisterResult = false)
        val registration = UnityHostCallbackRegistration(registrar, "callbacks")

        assertTrue(registration.register())
        assertFalse(registration.release())
        assertTrue(registration.isRegistered())
        assertEquals(
            listOf("register:callbacks", "unregister:callbacks"),
            registrar.calls
        )
    }

    @Test
    fun throwingRegistrationAttemptsCleanupBeforeOwnershipCanBeReleased() {
        val registrar = RecordingCallbackRegistrar(throwOnRegister = true)
        val registration = UnityHostCallbackRegistration(registrar, "callbacks")

        assertFalse(registration.register())
        assertFalse(registration.isRegistered())
        assertTrue(registration.release())
        assertEquals(
            listOf("register:callbacks", "unregister:callbacks"),
            registrar.calls
        )
    }

    @Test
    fun throwingRegistrationAndFailedCleanupRemainFailClosed() {
        val registrar = RecordingCallbackRegistrar(
            unregisterResult = false,
            throwOnRegister = true
        )
        val registration = UnityHostCallbackRegistration(registrar, "callbacks")

        assertFalse(registration.register())
        assertFalse(registration.release())
        assertEquals(
            listOf("register:callbacks", "unregister:callbacks"),
            registrar.calls
        )
    }

    @Test
    fun callbackAdmissionRejectsOversizedPayloadBeforeRetainingRawText() {
        val admission = UnityBridgeCallbackAdmissionGate(
            maxPendingCallbacks = 2,
            maxMessageBytes = 4
        )

        val exact = admission.tryAdmit("1234")
        val oversized = admission.tryAdmit("12345")

        assertEquals(UnityBridgeCallbackAdmission.Payload("1234"), exact)
        assertEquals(
            UnityBridgeCallbackAdmission.ProtocolError(
                UnityBridgeProtocolError(UnityBridgeProtocolErrorCode.MessageTooLarge)
            ),
            oversized
        )
        assertEquals(2, admission.snapshot().pendingCount)
        admission.complete(exact)
        admission.complete(oversized)
        assertEquals(0, admission.snapshot().pendingCount)
    }

    @Test
    fun callbackAdmissionBoundsBurstWithOneFailClosedOverflowSentinel() {
        val admission = UnityBridgeCallbackAdmissionGate(maxPendingCallbacks = 2)
        val first = admission.tryAdmit("{")
        val second = admission.tryAdmit(null)

        assertTrue(first is UnityBridgeCallbackAdmission.Payload)
        assertEquals(
            UnityBridgeCallbackAdmission.ProtocolError(
                UnityBridgeProtocolError(UnityBridgeProtocolErrorCode.NullMessage)
            ),
            second
        )
        assertEquals(UnityBridgeCallbackAdmission.Overflow, admission.tryAdmit("third"))
        assertEquals(UnityBridgeCallbackAdmission.Dropped, admission.tryAdmit("fourth"))
        assertEquals(
            UnityBridgeCallbackAdmissionSnapshot(
                pendingCount = 2,
                overflowPending = true,
                closed = true
            ),
            admission.snapshot()
        )

        admission.complete(first)
        admission.complete(second)
        admission.complete(UnityBridgeCallbackAdmission.Overflow)
        assertEquals(
            UnityBridgeCallbackAdmissionSnapshot(
                pendingCount = 0,
                overflowPending = false,
                closed = true
            ),
            admission.snapshot()
        )
    }

    @Test
    fun disposedCallbackAdmissionDropsNewWorkWhileExistingWorkDrains() {
        val admission = UnityBridgeCallbackAdmissionGate(maxPendingCallbacks = 2)
        val admitted = admission.tryAdmit("pending")

        admission.close()

        assertEquals(UnityBridgeCallbackAdmission.Dropped, admission.tryAdmit("late"))
        assertEquals(1, admission.snapshot().pendingCount)
        admission.complete(admitted)
        assertEquals(0, admission.snapshot().pendingCount)
    }

    private class RecordingRuntime(
        private val throwOn: Set<String> = emptySet()
    ) : UnityHostLifecycleRuntime<Unit> {
        val calls = mutableListOf<String>()

        override fun resume() = record("resume")

        override fun pause() = record("pause")

        override fun destroy() = record("destroy")

        override fun windowFocusChanged(hasFocus: Boolean) = record("focus:$hasFocus")

        override fun lowMemory() = record("lowMemory")

        override fun configurationChanged(configuration: Unit) = record("configurationChanged")

        private fun record(call: String): Boolean {
            calls += call
            if (call in throwOn) error("synthetic $call failure")
            return true
        }
    }

    private class RecordingCallbackRegistrar(
        private val registerResult: Boolean = true,
        private val unregisterResult: Boolean = true,
        private val throwOnRegister: Boolean = false
    ) : UnityHostCallbackRegistrar<String> {
        val calls = mutableListOf<String>()

        override fun register(callback: String): Boolean {
            calls += "register:$callback"
            if (throwOnRegister) error("synthetic registration failure")
            return registerResult
        }

        override fun unregister(callback: String): Boolean {
            calls += "unregister:$callback"
            return unregisterResult
        }
    }
}
