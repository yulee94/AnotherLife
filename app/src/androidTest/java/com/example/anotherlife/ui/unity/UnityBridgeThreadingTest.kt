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
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class UnityBridgeThreadingTest {
    @get:Rule
    val composeRule = createComposeRule()

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
        private val throwOnRegister: Boolean = false
    ) : UnityHostCallbackRegistrar<ComponentCallbacks2> {
        var registerCount = 0
        var unregisterCount = 0

        override fun register(callback: ComponentCallbacks2): Boolean {
            registerCount += 1
            if (throwOnRegister) error("synthetic registration failure")
            return registerResult
        }

        override fun unregister(callback: ComponentCallbacks2): Boolean {
            unregisterCount += 1
            return unregisterResult
        }
    }

    private class RecordingEmbeddedPlayer(
        context: Context,
        preAttachView: Boolean = false
    ) : UnityEmbeddedPlayer {
        override val view: View = FrameLayout(context).also { playerView ->
            if (preAttachView) FrameLayout(context).addView(playerView)
        }
        var sendCount = 0
        var destroyCount = 0

        override fun resume() = true

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
            assertEquals("AndroidBridge", gameObject)
            assertEquals("SetRouteContext", method)
            assertNotNull(payload)
            return true
        }
    }
}
