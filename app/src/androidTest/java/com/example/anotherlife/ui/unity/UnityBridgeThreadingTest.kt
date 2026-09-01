package com.example.anotherlife.ui.unity

import android.content.ComponentCallbacks2
import android.content.Context
import android.content.res.Configuration
import android.os.Looper
import android.view.View
import android.widget.FrameLayout
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.LifecycleRegistry
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference
import kotlin.concurrent.thread
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class UnityBridgeThreadingTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun unknownRouteUnavailableOutcomeCompletesOneCorrelatedHostSession() {
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val registrar = RecordingComponentCallbackRegistrar()
        val outcomes = CopyOnWriteArrayList<UnityRouteOutcome>()
        val protocolErrors = AtomicInteger()
        val dependencies = testDependencies(players, registrar)

        composeRule.setContent {
            UnityViewForTest(
                routeId = UnityBridgeSmokePolicy.ROUTE_ID,
                dependencies = dependencies,
                onOutcome = outcomes::add,
                onProtocolError = { protocolErrors.incrementAndGet() }
            )
        }
        composeRule.waitForIdle()

        val player = players.single()
        val dispatchedPayload = requireNotNull(player.lastPayload)
        val request = when (
            val parsed = UnityBridgeContract.parseRequest(dispatchedPayload)
        ) {
            is UnityBridgeContractResult.Accepted -> parsed.value
            is UnityBridgeContractResult.Rejected -> error(
                "Dispatched request was rejected: ${parsed.error.code}"
            )
        }
        val unavailable = buildJsonObject {
            put("contractVersion", UNITY_BRIDGE_CONTRACT_VERSION)
            put("requestId", request.requestId)
            put("routeId", request.routeId)
            put("status", UnityRouteOutcomeStatus.Unavailable.wireValue)
            put("diagnosticCode", "route.not_available")
        }.toString()

        Thread { UnityBridgeCallbacks.reportOutcome(unavailable) }.apply {
            start()
            join()
        }

        composeRule.waitUntil(timeoutMillis = 5_000) { outcomes.size == 1 }
        assertEquals(UnityRouteOutcomeStatus.Unavailable, outcomes.single().status)
        assertEquals(request.requestId, outcomes.single().requestId)
        assertEquals(request.routeId, outcomes.single().routeId)
        assertEquals("route.not_available", outcomes.single().diagnosticCode)
        assertEquals(0, protocolErrors.get())

        UnityBridgeCallbacks.reportOutcome(unavailable)
        composeRule.waitUntil(timeoutMillis = 5_000) { protocolErrors.get() == 1 }
        assertEquals(1, outcomes.size)
    }

    @Test
    fun offMainProtocolCallbackReachesUiOnMainThread() {
        val deliveredOnMain = AtomicReference<Boolean?>()
        composeRule.setContent {
            UnityView(
                routeId = "bridge.smoke",
                onProtocolError = {
                    deliveredOnMain.set(Looper.myLooper() == Looper.getMainLooper())
                }
            )
        }
        composeRule.waitForIdle()

        Thread {
            UnityBridgeCallbacks.reportOutcome("{")
        }.apply {
            start()
            join()
        }

        composeRule.waitUntil(timeoutMillis = 5_000) {
            deliveredOnMain.get() != null
        }
        assertEquals(true, deliveredOnMain.get())
    }

    @Test
    fun disposedHostDoesNotReceiveLateCallback() {
        val callbackCount = AtomicInteger()
        var mounted by mutableStateOf(true)
        composeRule.setContent {
            if (mounted) {
                UnityView(
                    routeId = "bridge.smoke",
                    onProtocolError = { callbackCount.incrementAndGet() }
                )
            }
        }
        composeRule.waitForIdle()

        composeRule.runOnUiThread { mounted = false }
        composeRule.waitForIdle()
        UnityBridgeCallbacks.reportOutcome("{")
        composeRule.waitForIdle()

        assertEquals(0, callbackCount.get())
    }

    @Test
    fun replacementHostReceivesCallbacksAfterPriorHostIsDisposed() {
        val firstCount = AtomicInteger()
        val replacementCount = AtomicInteger()
        var generation by mutableStateOf(1)
        composeRule.setContent {
            when (generation) {
                1 -> UnityView(
                    routeId = "bridge.smoke",
                    onProtocolError = { firstCount.incrementAndGet() }
                )

                2 -> UnityView(
                    routeId = "bridge.smoke",
                    onProtocolError = { replacementCount.incrementAndGet() }
                )
            }
        }
        composeRule.waitForIdle()

        composeRule.runOnUiThread { generation = 0 }
        composeRule.waitForIdle()
        composeRule.runOnUiThread { generation = 2 }
        composeRule.waitForIdle()

        UnityBridgeCallbacks.reportOutcome("{")
        composeRule.waitForIdle()

        assertEquals(0, firstCount.get())
        assertEquals(1, replacementCount.get())
    }

    @Test
    fun lifecycleOwnerSwapReleasesTheActualViewAndDispatchesThroughOneReplacement() {
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val registrar = RecordingComponentCallbackRegistrar()
        val dependencies = testDependencies(players, registrar)
        var owner by mutableStateOf<LifecycleOwner>(MutableLifecycleOwner())
        var mounted by mutableStateOf(true)
        val dispatchCount = AtomicInteger()
        composeRule.setContent {
            CompositionLocalProvider(LocalLifecycleOwner provides owner) {
                if (mounted) {
                    UnityViewForTest(
                        routeId = "bridge.smoke",
                        dependencies = dependencies,
                        onRouteDispatched = { dispatchCount.incrementAndGet() }
                    )
                }
            }
        }
        composeRule.waitForIdle()

        assertEquals(1, players.size)
        assertEquals(1, players[0].sendCount)

        composeRule.runOnUiThread { owner = MutableLifecycleOwner() }
        composeRule.waitForIdle()

        assertEquals(2, players.size)
        assertEquals(1, players[0].destroyCount)
        assertEquals(1, players[1].sendCount)
        assertEquals(2, dispatchCount.get())
        assertEquals(2, registrar.registerCount)
        assertEquals(1, registrar.unregisterCount)

        composeRule.runOnUiThread { mounted = false }
        composeRule.waitForIdle()
        assertEquals(1, players[1].destroyCount)
        assertEquals(2, registrar.unregisterCount)
    }

    @Test
    fun overlappingDeniedHostAcquiresAfterOutgoingReleaseAndDispatchesPendingRoute() {
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val registrar = RecordingComponentCallbackRegistrar()
        val dependencies = testDependencies(players, registrar)
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        var first: UnityRuntimeContainer? = null
        var second: UnityRuntimeContainer? = null
        var firstDispatch = false
        var secondDispatch = false

        composeRule.runOnUiThread {
            first = UnityRuntimeContainer(context, dependencies)
            second = UnityRuntimeContainer(context, dependencies)
            assertTrue(
                first!!.setRoute(
                    routeId = "bridge.first",
                    routeLaunchSequence = 1,
                    routeIntent = UnityRouteIntent.Preview,
                    requestedCapabilities = emptyList(),
                    onRouteDispatched = { firstDispatch = true },
                    onOutcome = {},
                    onProtocolError = {}
                )
            )
            assertFalse(
                second!!.setRoute(
                    routeId = "bridge.second",
                    routeLaunchSequence = 2,
                    routeIntent = UnityRouteIntent.Preview,
                    requestedCapabilities = emptyList(),
                    onRouteDispatched = { secondDispatch = true },
                    onOutcome = {},
                    onProtocolError = {}
                )
            )
            assertEquals(1, players.size)
            assertEquals(1, dependencies.ownershipRegistry.waitingCount())

            first!!.destroyUnity()

            assertEquals(2, players.size)
            assertTrue(secondDispatch)
            assertEquals(1, players[1].sendCount)
            assertEquals(0, dependencies.ownershipRegistry.waitingCount())
            second!!.destroyUnity()
        }

        assertTrue(firstDispatch)
        assertEquals(1, players[0].destroyCount)
        assertEquals(1, players[1].destroyCount)
    }

    @Test
    fun componentCallbackRegistrationFailureDestroysRuntimeAndBlocksRouteDispatch() {
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val registrar = RecordingComponentCallbackRegistrar(registerResult = false)
        val dependencies = testDependencies(players, registrar)
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        var host: UnityRuntimeContainer? = null
        var dispatched = false

        composeRule.runOnUiThread {
            host = UnityRuntimeContainer(context, dependencies)
            assertFalse(
                host!!.setRoute(
                    routeId = "bridge.registration-failure",
                    routeLaunchSequence = 1,
                    routeIntent = UnityRouteIntent.Preview,
                    requestedCapabilities = emptyList(),
                    onRouteDispatched = { dispatched = true },
                    onOutcome = {},
                    onProtocolError = {}
                )
            )
            assertEquals(
                "Unity runtime unavailable\nLifecycle callback registration failed",
                host!!.statusTextForTesting()
            )
            assertEquals(1, host!!.statusTextUpdateCountForTesting())
            assertEquals(
                View.ACCESSIBILITY_LIVE_REGION_POLITE,
                host!!.statusAccessibilityLiveRegionForTesting()
            )
            assertEquals(
                View.IMPORTANT_FOR_ACCESSIBILITY_YES,
                host!!.statusImportantForAccessibilityForTesting()
            )
            host!!.destroyUnity()
        }

        assertFalse(dispatched)
        assertEquals(1, players.size)
        assertEquals(0, players[0].sendCount)
        assertEquals(1, players[0].destroyCount)
        assertEquals(1, registrar.registerCount)
        assertEquals(0, registrar.unregisterCount)
    }

    @Test
    fun postRegistrationActivationFailureRollsBackAndReplacementAcquiresCleanly() {
        val registry = UnityRuntimeHostRegistry()
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val registrar = RecordingComponentCallbackRegistrar()
        val failingDependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { context ->
                RecordingEmbeddedPlayer(context, preAttachView = true).also(players::add)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { registrar }
        )
        val replacementDependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { context ->
                RecordingEmbeddedPlayer(context).also(players::add)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { registrar }
        )
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val failedCallbackCount = AtomicInteger()
        val replacementCallbackCount = AtomicInteger()
        var failed: UnityRuntimeContainer? = null
        var replacement: UnityRuntimeContainer? = null

        composeRule.runOnUiThread {
            failed = UnityRuntimeContainer(context, failingDependencies)
            assertFalse(
                failed!!.setRoute(
                    routeId = "bridge.activation-failure",
                    routeLaunchSequence = 1,
                    routeIntent = UnityRouteIntent.Preview,
                    requestedCapabilities = emptyList(),
                    onRouteDispatched = {},
                    onOutcome = {},
                    onProtocolError = { failedCallbackCount.incrementAndGet() }
                )
            )
            assertEquals(
                "Unity runtime unavailable\nHost activation failed",
                failed!!.statusTextForTesting()
            )
        }

        UnityBridgeCallbacks.reportOutcome("{")
        composeRule.waitForIdle()
        assertEquals(0, failedCallbackCount.get())
        assertEquals(1, players.size)
        assertEquals(1, players[0].destroyCount)
        assertEquals(null, players[0].view.parent)
        assertEquals(1, registrar.registerCount)
        assertEquals(1, registrar.unregisterCount)

        composeRule.runOnUiThread {
            replacement = UnityRuntimeContainer(context, replacementDependencies)
            assertTrue(
                replacement!!.setRoute(
                    routeId = "bridge.replacement",
                    routeLaunchSequence = 2,
                    routeIntent = UnityRouteIntent.Preview,
                    requestedCapabilities = emptyList(),
                    onRouteDispatched = {},
                    onOutcome = {},
                    onProtocolError = { replacementCallbackCount.incrementAndGet() }
                )
            )
        }
        UnityBridgeCallbacks.reportOutcome("{")
        composeRule.waitUntil(timeoutMillis = 5_000) { replacementCallbackCount.get() == 1 }

        assertEquals(2, players.size)
        assertEquals(1, players[1].sendCount)
        assertEquals(1, replacementCallbackCount.get())
        composeRule.runOnUiThread {
            failed!!.destroyUnity()
            replacement!!.destroyUnity()
        }
    }

    @Test
    fun rejectedRegistrarCleanupReleasesForImmediateReplacement() {
        val registry = UnityRuntimeHostRegistry()
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val rejectedRegistrar = RecordingComponentCallbackRegistrar(registerResult = false)
        val goodRegistrar = RecordingComponentCallbackRegistrar()
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val rejectedDependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { playerContext ->
                RecordingEmbeddedPlayer(playerContext).also(players::add)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory {
                rejectedRegistrar
            }
        )
        val replacementDependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { playerContext ->
                RecordingEmbeddedPlayer(playerContext).also(players::add)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { goodRegistrar }
        )

        composeRule.runOnUiThread {
            val rejected = UnityRuntimeContainer(context, rejectedDependencies)
            assertEquals(1, players.size)
            assertEquals(1, players[0].destroyCount)
            assertNull(players[0].view.parent)

            val replacement = UnityRuntimeContainer(context, replacementDependencies)
            assertEquals(2, players.size)
            replacement.destroyUnity()
            rejected.destroyUnity()
        }

        assertEquals(1, rejectedRegistrar.registerCount)
        assertEquals(0, rejectedRegistrar.unregisterCount)
        assertEquals(1, goodRegistrar.registerCount)
        assertEquals(1, goodRegistrar.unregisterCount)
        assertEquals(1, players[1].destroyCount)
    }

    @Test
    fun reentrantDestroyInsideConstructionCleansBeforeReplacementCanAcquire() {
        val registry = UnityRuntimeHostRegistry()
        val reservedOwner = registry.tryAcquire()!!
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val registrar = RecordingComponentCallbackRegistrar()
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        var waiting: UnityRuntimeContainer? = null
        val dependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { playerContext ->
                RecordingEmbeddedPlayer(playerContext).also { player ->
                    players += player
                    waiting!!.destroyUnity()
                }
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { registrar }
        )

        composeRule.runOnUiThread {
            waiting = UnityRuntimeContainer(context, dependencies)
            assertEquals(1, registry.waitingCount())
            assertTrue(registry.release(reservedOwner))
        }

        assertEquals(1, players.size)
        assertEquals(1, players.single().destroyCount)
        assertNull(players.single().view.parent)
        assertEquals(0, registrar.registerCount)
        val replacement = registry.tryAcquire()
        assertNotNull(replacement)
        assertTrue(registry.release(replacement!!))
    }

    @Test
    fun concurrentDestroyInsideConstructionCannotReleaseUntilPlayerCleanupCompletes() {
        val registry = UnityRuntimeHostRegistry()
        val reservedOwner = registry.tryAcquire()!!
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val registrar = RecordingComponentCallbackRegistrar()
        val factoryEntered = CountDownLatch(1)
        val allowFactoryReturn = CountDownLatch(1)
        val destroyCompleted = CountDownLatch(1)
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        var waiting: UnityRuntimeContainer? = null
        val dependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { playerContext ->
                factoryEntered.countDown()
                assertTrue(allowFactoryReturn.await(5, TimeUnit.SECONDS))
                RecordingEmbeddedPlayer(playerContext).also(players::add)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { registrar }
        )
        composeRule.runOnUiThread {
            waiting = UnityRuntimeContainer(context, dependencies)
        }
        val destroyThread = thread(name = "destroy-inside-player-construction") {
            assertTrue(factoryEntered.await(5, TimeUnit.SECONDS))
            waiting!!.destroyUnity()
            assertNull(registry.tryAcquire())
            destroyCompleted.countDown()
            allowFactoryReturn.countDown()
        }

        composeRule.runOnUiThread { assertTrue(registry.release(reservedOwner)) }
        assertTrue(destroyCompleted.await(5, TimeUnit.SECONDS))
        destroyThread.join(5_000)
        assertFalse(destroyThread.isAlive)
        assertEquals(1, players.single().destroyCount)
        assertNull(players.single().view.parent)
        val replacement = registry.tryAcquire()
        assertNotNull(replacement)
        assertTrue(registry.release(replacement!!))
    }

    @Test
    fun reentrantDestroyAfterExternalRegistrationCleansBeforeViewAttachment() {
        val registry = UnityRuntimeHostRegistry()
        val reservedOwner = registry.tryAcquire()!!
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        var waiting: UnityRuntimeContainer? = null
        val registrar = RecordingComponentCallbackRegistrar(
            onRegister = { waiting!!.destroyUnity() }
        )
        val dependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { playerContext ->
                RecordingEmbeddedPlayer(playerContext).also(players::add)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { registrar }
        )

        composeRule.runOnUiThread {
            waiting = UnityRuntimeContainer(context, dependencies)
            assertTrue(registry.release(reservedOwner))
        }

        assertEquals(1, registrar.registerCount)
        assertEquals(1, registrar.unregisterCount)
        assertEquals(1, players.single().destroyCount)
        assertNull(players.single().view.parent)
        val replacement = registry.tryAcquire()
        assertNotNull(replacement)
        assertTrue(registry.release(replacement!!))
    }

    @Test
    fun reentrantDestroyAfterAttachmentCleansPlayerCallbacksAndViewBeforeReplacement() {
        val registry = UnityRuntimeHostRegistry()
        val reservedOwner = registry.tryAcquire()!!
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val registrar = RecordingComponentCallbackRegistrar()
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        var waiting: UnityRuntimeContainer? = null
        val dependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { playerContext ->
                RecordingEmbeddedPlayer(
                    context = playerContext,
                    onResume = { waiting!!.destroyUnity() }
                ).also(players::add)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { registrar }
        )

        composeRule.runOnUiThread {
            waiting = UnityRuntimeContainer(context, dependencies)
            waiting!!.resumeUnity()
            assertTrue(registry.release(reservedOwner))
        }

        assertEquals(1, registrar.registerCount)
        assertEquals(1, registrar.unregisterCount)
        assertEquals(1, players.single().destroyCount)
        assertNull(players.single().view.parent)
        val replacement = registry.tryAcquire()
        assertNotNull(replacement)
        assertTrue(registry.release(replacement!!))
    }

    @Test
    fun throwingPlayerViewRetainsLeaseAndPreventsReplacementActivation() {
        val registry = UnityRuntimeHostRegistry()
        val playerDestroyCount = AtomicInteger()
        val replacementFactoryCount = AtomicInteger()
        val registrar = RecordingComponentCallbackRegistrar()
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val throwingViewPlayer = object : UnityEmbeddedPlayer {
            override val view: View
                get() = error("synthetic view lookup failure")

            override fun resume() = true
            override fun pause() = true
            override fun destroy(): Boolean {
                playerDestroyCount.incrementAndGet()
                return true
            }
            override fun windowFocusChanged(hasFocus: Boolean) = true
            override fun lowMemory() = true
            override fun configurationChanged(configuration: Configuration) = true
            override fun sendMessage(
                gameObject: String,
                method: String,
                payload: String
            ) = true
        }
        val failingDependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { throwingViewPlayer },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { registrar }
        )
        val replacementDependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { playerContext ->
                replacementFactoryCount.incrementAndGet()
                RecordingEmbeddedPlayer(playerContext)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { registrar }
        )

        composeRule.runOnUiThread {
            val failed = UnityRuntimeContainer(context, failingDependencies)
            val waiting = UnityRuntimeContainer(context, replacementDependencies)
            assertEquals(1, playerDestroyCount.get())
            assertEquals(0, replacementFactoryCount.get())
            assertEquals(1, registry.waitingCount())
            failed.destroyUnity()
            assertEquals(0, replacementFactoryCount.get())
            waiting.destroyUnity()
        }

        assertEquals(0, registrar.registerCount)
        assertEquals(0, replacementFactoryCount.get())
        assertEquals(0, registry.waitingCount())
    }

    @Test
    fun uncertainRegistrationCleanupRetainsLeaseAndCancelledReplacementCannotActivate() {
        val registry = UnityRuntimeHostRegistry()
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val uncertainRegistrar = RecordingComponentCallbackRegistrar(
            unregisterResult = false,
            throwOnRegister = true
        )
        val goodRegistrar = RecordingComponentCallbackRegistrar()
        val failingDependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { context ->
                RecordingEmbeddedPlayer(context).also(players::add)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory {
                uncertainRegistrar
            }
        )
        val replacementDependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { context ->
                RecordingEmbeddedPlayer(context).also(players::add)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { goodRegistrar }
        )
        val context = InstrumentationRegistry.getInstrumentation().targetContext

        composeRule.runOnUiThread {
            val failed = UnityRuntimeContainer(context, failingDependencies)
            val waiting = UnityRuntimeContainer(context, replacementDependencies)
            assertEquals(1, players.size)
            assertEquals(1, players[0].destroyCount)
            assertEquals(1, registry.waitingCount())

            failed.destroyUnity()
            assertEquals(1, players.size)
            assertEquals(1, registry.waitingCount())

            waiting.destroyUnity()
            assertEquals(0, registry.waitingCount())
        }

        assertEquals(1, uncertainRegistrar.registerCount)
        assertEquals(1, uncertainRegistrar.unregisterCount)
        assertEquals(0, goodRegistrar.registerCount)
    }

    @Test
    fun oversizedOffMainCallbackIsRejectedOnMainThreadBeforeParsing() {
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val registrar = RecordingComponentCallbackRegistrar()
        val dependencies = testDependencies(players, registrar)
        val delivered = AtomicReference<Pair<UnityBridgeProtocolErrorCode, Boolean>?>()
        var mounted by mutableStateOf(true)
        composeRule.setContent {
            if (mounted) {
                UnityViewForTest(
                    routeId = "bridge.smoke",
                    dependencies = dependencies,
                    onProtocolError = { error ->
                        delivered.set(
                            error.code to (Looper.myLooper() == Looper.getMainLooper())
                        )
                    }
                )
            }
        }
        composeRule.waitForIdle()

        Thread {
            UnityBridgeCallbacks.reportOutcome("a".repeat(MAX_UNITY_BRIDGE_MESSAGE_BYTES + 1))
        }.apply {
            start()
            join()
        }

        composeRule.waitUntil(timeoutMillis = 5_000) { delivered.get() != null }
        assertEquals(UnityBridgeProtocolErrorCode.MessageTooLarge, delivered.get()!!.first)
        assertTrue(delivered.get()!!.second)

        composeRule.runOnUiThread { mounted = false }
        composeRule.waitForIdle()
    }

    @Test
    fun offMainGrantToUnattachedDisposedWaiterReleasesForALaterReplacement() {
        val registry = UnityRuntimeHostRegistry()
        val reservedOwner = registry.tryAcquire()!!
        val players = CopyOnWriteArrayList<RecordingEmbeddedPlayer>()
        val registrar = RecordingComponentCallbackRegistrar()
        val dependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { context ->
                RecordingEmbeddedPlayer(context).also(players::add)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { registrar }
        )
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        var waiting: UnityRuntimeContainer? = null
        var replacement: UnityRuntimeContainer? = null

        composeRule.runOnUiThread {
            waiting = UnityRuntimeContainer(context, dependencies)
            assertEquals(1, registry.waitingCount())
        }
        val released = CountDownLatch(1)
        val releaseThread = thread(name = "off-main-registry-release") {
            assertTrue(registry.release(reservedOwner))
            released.countDown()
        }
        assertTrue(released.await(5, TimeUnit.SECONDS))
        releaseThread.join(5_000)
        assertFalse(releaseThread.isAlive)

        composeRule.runOnUiThread {
            waiting!!.destroyUnity()
            replacement = UnityRuntimeContainer(context, dependencies)
            assertTrue(
                replacement!!.setRoute(
                    routeId = "bridge.after-unattached-disposal",
                    routeLaunchSequence = 1,
                    routeIntent = UnityRouteIntent.Preview,
                    requestedCapabilities = emptyList(),
                    onRouteDispatched = {},
                    onOutcome = {},
                    onProtocolError = {}
                )
            )
            replacement!!.destroyUnity()
        }

        assertEquals(1, players.size)
        assertEquals(1, players.single().sendCount)
        assertEquals(1, players.single().destroyCount)
        assertEquals(1, registrar.registerCount)
        assertEquals(1, registrar.unregisterCount)
    }

    @Test
    fun reflectiveConstructorFailureRetainsLeaseAndPreventsASecondRuntime() {
        val registry = UnityRuntimeHostRegistry()
        val registrar = RecordingComponentCallbackRegistrar()
        val replacementFactoryCalls = AtomicInteger()
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val failingDependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { reflectionContext ->
                ReflectionUnityPlayer.createFromResolvedClass(
                    reflectionContext,
                    ThrowingReflectionPlayer::class.java
                )
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { registrar }
        )
        val replacementDependencies = UnityRuntimeHostDependencies(
            ownershipRegistry = registry,
            playerFactory = UnityEmbeddedPlayerFactory { replacementContext ->
                replacementFactoryCalls.incrementAndGet()
                RecordingEmbeddedPlayer(replacementContext)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { registrar }
        )

        composeRule.runOnUiThread {
            val failed = UnityRuntimeContainer(context, failingDependencies)
            assertEquals(
                "Unity runtime unavailable\nHost activation failed",
                failed.statusTextForTesting()
            )
            val waiting = UnityRuntimeContainer(context, replacementDependencies)
            assertEquals(0, replacementFactoryCalls.get())
            assertEquals(1, registry.waitingCount())

            failed.destroyUnity()
            assertEquals(0, replacementFactoryCalls.get())
            assertEquals(1, registry.waitingCount())
            waiting.destroyUnity()
            assertEquals(0, registry.waitingCount())
        }

        assertEquals(0, registrar.registerCount)
        assertEquals(0, registrar.unregisterCount)
    }

    @Test
    fun reflectiveFactoryRejectsStaticAndNonVoidApisBeforeConstruction() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext

        composeRule.runOnUiThread {
            assertNull(
                ReflectionUnityPlayer.createFromResolvedClass(
                    context,
                    StaticLifecycleReflectionPlayer::class.java
                )
            )
            assertNull(
                ReflectionUnityPlayer.createFromResolvedClass(
                    context,
                    NonVoidLifecycleReflectionPlayer::class.java
                )
            )
            assertNull(
                ReflectionUnityPlayer.createFromResolvedClass(
                    context,
                    NonVoidSenderReflectionPlayer::class.java
                )
            )
        }

        assertEquals(0, StaticLifecycleReflectionPlayer.constructionCount.get())
        assertEquals(0, NonVoidLifecycleReflectionPlayer.constructionCount.get())
        assertEquals(0, NonVoidSenderReflectionPlayer.constructionCount.get())
    }

    private fun testDependencies(
        players: MutableList<RecordingEmbeddedPlayer>,
        registrar: RecordingComponentCallbackRegistrar
    ): UnityRuntimeHostDependencies {
        return UnityRuntimeHostDependencies(
            ownershipRegistry = UnityRuntimeHostRegistry(),
            playerFactory = UnityEmbeddedPlayerFactory { context ->
                RecordingEmbeddedPlayer(context).also(players::add)
            },
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory { registrar }
        )
    }

    private class MutableLifecycleOwner : LifecycleOwner {
        override val lifecycle = LifecycleRegistry(this)
    }

    private class RecordingComponentCallbackRegistrar(
        private val registerResult: Boolean = true,
        private val unregisterResult: Boolean = true,
        private val throwOnRegister: Boolean = false,
        private val onRegister: () -> Unit = {}
    ) : UnityHostCallbackRegistrar<ComponentCallbacks2> {
        var registerCount = 0
        var unregisterCount = 0

        override fun register(callback: ComponentCallbacks2): Boolean {
            registerCount += 1
            if (throwOnRegister) error("synthetic registration failure")
            onRegister()
            return registerResult
        }

        override fun unregister(callback: ComponentCallbacks2): Boolean {
            unregisterCount += 1
            return unregisterResult
        }
    }

    private class RecordingEmbeddedPlayer(
        context: Context,
        preAttachView: Boolean = false,
        private val onFirstViewRead: () -> Unit = {},
        private val onResume: () -> Unit = {}
    ) : UnityEmbeddedPlayer {
        private val playerView = FrameLayout(context).also { playerView ->
            if (preAttachView) FrameLayout(context).addView(playerView)
        }
        private var viewRead = false
        override val view: View
            get() {
                if (!viewRead) {
                    viewRead = true
                    onFirstViewRead()
                }
                return playerView
            }
        var sendCount = 0
        var destroyCount = 0
        var lastPayload: String? = null

        override fun resume(): Boolean {
            onResume()
            return true
        }

        override fun pause() = true

        override fun destroy(): Boolean {
            destroyCount += 1
            return true
        }

        override fun windowFocusChanged(hasFocus: Boolean) = true

        override fun lowMemory() = true

        override fun configurationChanged(configuration: Configuration) = true

        override fun sendMessage(gameObject: String, method: String, payload: String): Boolean {
            sendCount += 1
            lastPayload = payload
            assertEquals("AndroidBridge", gameObject)
            assertEquals("SetRouteContext", method)
            assertNotNull(payload)
            return true
        }
    }

    @Suppress("FunctionName", "UNUSED_PARAMETER")
    class ThrowingReflectionPlayer(context: Context) : FrameLayout(context) {
        init {
            error("synthetic reflective constructor failure")
        }

        fun resume() = Unit

        fun pause() = Unit

        fun destroy() = Unit

        fun windowFocusChanged(hasFocus: Boolean) = Unit

        fun lowMemory() = Unit

        fun configurationChanged(configuration: Configuration) = Unit

        companion object {
            @JvmStatic
            fun UnitySendMessage(gameObject: String, method: String, payload: String) = Unit
        }
    }

    @Suppress("FunctionName", "UNUSED_PARAMETER")
    class StaticLifecycleReflectionPlayer(context: Context) : FrameLayout(context) {
        init {
            constructionCount.incrementAndGet()
        }

        fun pause() = Unit

        fun destroy() = Unit

        fun windowFocusChanged(hasFocus: Boolean) = Unit

        fun lowMemory() = Unit

        fun configurationChanged(configuration: Configuration) = Unit

        companion object {
            val constructionCount = AtomicInteger()

            @JvmStatic
            fun resume() = Unit

            @JvmStatic
            fun UnitySendMessage(gameObject: String, method: String, payload: String) = Unit
        }
    }

    @Suppress("FunctionName", "UNUSED_PARAMETER")
    class NonVoidLifecycleReflectionPlayer(context: Context) : FrameLayout(context) {
        init {
            constructionCount.incrementAndGet()
        }

        fun resume(): Boolean = true

        fun pause() = Unit

        fun destroy() = Unit

        fun windowFocusChanged(hasFocus: Boolean) = Unit

        fun lowMemory() = Unit

        fun configurationChanged(configuration: Configuration) = Unit

        companion object {
            val constructionCount = AtomicInteger()

            @JvmStatic
            fun UnitySendMessage(gameObject: String, method: String, payload: String) = Unit
        }
    }

    @Suppress("FunctionName", "UNUSED_PARAMETER")
    class NonVoidSenderReflectionPlayer(context: Context) : FrameLayout(context) {
        init {
            constructionCount.incrementAndGet()
        }

        fun resume() = Unit

        fun pause() = Unit

        fun destroy() = Unit

        fun windowFocusChanged(hasFocus: Boolean) = Unit

        fun lowMemory() = Unit

        fun configurationChanged(configuration: Configuration) = Unit

        companion object {
            val constructionCount = AtomicInteger()

            @JvmStatic
            fun UnitySendMessage(
                gameObject: String,
                method: String,
                payload: String
            ): Boolean = true
        }
    }
}
