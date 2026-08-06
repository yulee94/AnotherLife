package com.example.anotherlife.ui.unity

import java.util.Collections
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference
import kotlin.concurrent.thread
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
    fun selfRequeueingThrowingWaiterIsRejectedAndHandoffTerminatesWithoutRecursion() {
        val registry = UnityRuntimeHostRegistry(maxWaiters = 1)
        val owner = registry.tryAcquire()!!
        val callbackCount = AtomicInteger()

        assertTrue(
            registry.acquireOrQueue {
                callbackCount.incrementAndGet()
                assertEquals(
                    UnityRuntimeHostAcquisition.CapacityReached,
                    registry.acquireOrQueue { error("must never be granted") }
                )
                error("synthetic self-requeueing waiter failure")
            } is UnityRuntimeHostAcquisition.Waiting
        )

        assertTrue(registry.release(owner))
        assertEquals(1, callbackCount.get())
        assertEquals(0, registry.waitingCount())
        val recovered = registry.tryAcquire()
        assertNotNull(recovered)
        assertTrue(registry.release(recovered!!))
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

    @Test
    fun offMainGrantToUnattachedWaiterDisposalCannotStrandTheLease() {
        val registry = UnityRuntimeHostRegistry()
        val owner = registry.tryAcquire()!!
        val handoff = UnityRuntimeGrantedLeaseHandoff()
        val retained = CountDownLatch(1)
        val deferredActivation = AtomicReference<() -> Unit>()
        val activated = AtomicBoolean(false)

        assertTrue(
            registry.acquireOrQueue { lease ->
                assertTrue(handoff.retain(lease))
                deferredActivation.set {
                    if (handoff.claim(lease)) activated.set(true)
                }
                retained.countDown()
            } is UnityRuntimeHostAcquisition.Waiting
        )
        val releaseThread = thread(name = "off-main-host-release") {
            registry.release(owner)
        }

        assertTrue(retained.await(5, TimeUnit.SECONDS))
        releaseThread.join(5_000)
        assertFalse(releaseThread.isAlive)
        assertNull(registry.tryAcquire())

        val strandedGrant = handoff.close()
        assertNotNull(strandedGrant)
        assertTrue(registry.release(strandedGrant!!))
        deferredActivation.get().invoke()
        assertFalse(activated.get())

        val replacement = registry.tryAcquire()
        assertNotNull(replacement)
        assertTrue(registry.release(replacement!!))
    }

    @Test
    fun destructionBetweenGrantClaimAndOwnershipPublicationReleasesExactlyOnce() {
        val registry = UnityRuntimeHostRegistry()
        val lease = registry.tryAcquire()!!
        val handoff = UnityRuntimeGrantedLeaseHandoff()
        val owned = UnityRuntimeOwnedLeaseState()
        val claimed = CountDownLatch(1)
        val allowPublication = CountDownLatch(1)
        val published = AtomicBoolean(true)
        assertTrue(handoff.retain(lease))

        val activation = thread(name = "claim-before-publish") {
            assertTrue(handoff.claim(lease))
            claimed.countDown()
            assertTrue(allowPublication.await(5, TimeUnit.SECONDS))
            published.set(owned.publish(lease))
            if (!published.get()) assertTrue(registry.release(lease))
        }
        assertTrue(claimed.await(5, TimeUnit.SECONDS))

        val closure = owned.close()
        assertNotNull(closure)
        assertNull(closure!!.lease)
        assertNull(handoff.close())
        allowPublication.countDown()
        activation.join(5_000)

        assertFalse(activation.isAlive)
        assertFalse(published.get())
        val replacement = registry.tryAcquire()
        assertNotNull(replacement)
        assertTrue(registry.release(replacement!!))
    }

    @Test
    fun oldestLiveWaiterWinsWhileAnIntermediateWaiterCancelsConcurrently() {
        val registry = UnityRuntimeHostRegistry(maxWaiters = 3)
        val owner = registry.tryAcquire()!!
        val order = Collections.synchronizedList(mutableListOf<String>())
        val firstGranted = CountDownLatch(1)
        val continueFirst = CountDownLatch(1)

        assertTrue(
            registry.acquireOrQueue { lease ->
                order += "first"
                firstGranted.countDown()
                assertTrue(continueFirst.await(5, TimeUnit.SECONDS))
                assertTrue(registry.release(lease))
            } is UnityRuntimeHostAcquisition.Waiting
        )
        val cancelled = registry.acquireOrQueue { order += "cancelled" }
            as UnityRuntimeHostAcquisition.Waiting
        assertTrue(
            registry.acquireOrQueue { lease ->
                order += "third"
                assertTrue(registry.release(lease))
            } is UnityRuntimeHostAcquisition.Waiting
        )

        val releaseThread = thread(name = "fifo-owner-release") {
            registry.release(owner)
        }
        assertTrue(firstGranted.await(5, TimeUnit.SECONDS))
        assertTrue(registry.cancel(cancelled.token))
        continueFirst.countDown()
        releaseThread.join(5_000)

        assertFalse(releaseThread.isAlive)
        assertEquals(listOf("first", "third"), order)
        assertEquals(0, registry.waitingCount())
        val replacement = registry.tryAcquire()
        assertNotNull(replacement)
        assertTrue(registry.release(replacement!!))
    }

    @Test
    fun cancelRacingDequeueEitherCancelsOrGrantsExactlyOnceWithoutLeaking() {
        repeat(64) { iteration ->
            val registry = UnityRuntimeHostRegistry()
            val owner = registry.tryAcquire()!!
            val granted = AtomicReference<UnityRuntimeHostLease?>()
            val waiting = registry.acquireOrQueue(granted::set)
                as UnityRuntimeHostAcquisition.Waiting
            val start = CountDownLatch(1)
            val cancelled = AtomicBoolean(false)
            val releaseResult = AtomicBoolean(false)

            val cancelThread = thread(name = "cancel-$iteration") {
                assertTrue(start.await(5, TimeUnit.SECONDS))
                cancelled.set(registry.cancel(waiting.token))
            }
            val releaseThread = thread(name = "dequeue-$iteration") {
                assertTrue(start.await(5, TimeUnit.SECONDS))
                releaseResult.set(registry.release(owner))
            }
            start.countDown()
            cancelThread.join(5_000)
            releaseThread.join(5_000)

            assertFalse(cancelThread.isAlive)
            assertFalse(releaseThread.isAlive)
            assertTrue(releaseResult.get())
            assertEquals(cancelled.get(), granted.get() == null)
            granted.get()?.let { assertTrue(registry.release(it)) }
            val probe = registry.tryAcquire()
            assertNotNull(probe)
            assertTrue(registry.release(probe!!))
        }
    }

    @Test
    fun concurrentCallbackProducersPreserveAdmissionAndPostOrder() {
        val posted = Collections.synchronizedList(mutableListOf<() -> Unit>())
        val delivered = Collections.synchronizedList(mutableListOf<String>())
        val postCount = AtomicInteger()
        val firstPostEntered = CountDownLatch(1)
        val allowFirstPost = CountDownLatch(1)
        val secondProducerStarted = CountDownLatch(1)
        val dispatcher = UnityBridgeCallbackDispatcher(
            maxPendingCallbacks = 4,
            postToMain = { action ->
                if (postCount.incrementAndGet() == 1) {
                    firstPostEntered.countDown()
                    assertTrue(allowFirstPost.await(5, TimeUnit.SECONDS))
                }
                posted += action
                true
            },
            onPayload = delivered::add,
            onProtocolError = {},
            onOverflow = {}
        )

        val first = thread(name = "callback-first") { dispatcher.enqueue("first") }
        assertTrue(firstPostEntered.await(5, TimeUnit.SECONDS))
        val second = thread(name = "callback-second") {
            secondProducerStarted.countDown()
            dispatcher.enqueue("second")
        }
        assertTrue(secondProducerStarted.await(5, TimeUnit.SECONDS))
        assertEquals(1, postCount.get())
        allowFirstPost.countDown()
        first.join(5_000)
        second.join(5_000)

        assertFalse(first.isAlive)
        assertFalse(second.isAlive)
        assertEquals(2, posted.size)
        posted.forEach { it.invoke() }
        assertEquals(listOf("first", "second"), delivered)
    }

    @Test
    fun concurrentOverflowPostsExactlyOnePayloadFreeSentinel() {
        val posted = Collections.synchronizedList(mutableListOf<() -> Unit>())
        val delivered = Collections.synchronizedList(mutableListOf<String>())
        val overflowCount = AtomicInteger()
        val dispatcher = UnityBridgeCallbackDispatcher(
            maxPendingCallbacks = 2,
            postToMain = { action -> posted.add(action) },
            onPayload = delivered::add,
            onProtocolError = {},
            onOverflow = { overflowCount.incrementAndGet() }
        )
        dispatcher.enqueue("first")
        dispatcher.enqueue("second")
        val start = CountDownLatch(1)
        val producers = (0 until 12).map { index ->
            thread(name = "overflow-$index") {
                assertTrue(start.await(5, TimeUnit.SECONDS))
                dispatcher.enqueue("overflowing-$index")
            }
        }

        start.countDown()
        producers.forEach { producer ->
            producer.join(5_000)
            assertFalse(producer.isAlive)
        }
        assertEquals(3, posted.size)
        posted.forEach { it.invoke() }

        assertEquals(listOf("first", "second"), delivered)
        assertEquals(1, overflowCount.get())
        assertEquals(
            UnityBridgeCallbackAdmissionSnapshot(0, false, true),
            dispatcher.snapshot()
        )
    }

    @Test
    fun dispatcherCloseSuppressesAlreadyPostedAndLaterUiDelivery() {
        val posted = mutableListOf<() -> Unit>()
        val delivered = mutableListOf<String>()
        val dispatcher = UnityBridgeCallbackDispatcher(
            maxPendingCallbacks = 2,
            postToMain = { action -> posted.add(action) },
            onPayload = delivered::add,
            onProtocolError = {},
            onOverflow = {}
        )
        dispatcher.enqueue("already-posted")

        dispatcher.close()
        dispatcher.enqueue("after-close")
        posted.forEach { it.invoke() }

        assertTrue(delivered.isEmpty())
        assertEquals(1, posted.size)
        assertEquals(0, dispatcher.snapshot().pendingCount)
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
