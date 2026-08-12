package com.example.anotherlife.ui.unity

import android.content.ComponentCallbacks2
import android.content.Context
import android.content.res.Configuration
import android.view.View
import android.widget.FrameLayout
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class UnityRuntimeContainerStatusAndroidTest {
    @Test
    fun routeDispatchRemainsDistinctFromRuntimeReadinessAndTeardownIsProven() {
        onMainThread { context ->
            val player = StatusTestPlayer(context)
            val host = UnityRuntimeContainer(context, dependencies { player })
            val attempt = UnityRuntimeContainerNativeLaunchAttempt(
                container = host,
                generation = 1L,
                routeId = "launch.cinematic"
            )

            assertEquals(
                UnityRuntimeContainerSnapshot(
                    phase = UnityRuntimeContainerPhase.Active,
                    ownership = UnityRuntimeContainerOwnership.Active,
                    teardown = UnityRuntimeContainerTeardownEvidence.NotStarted
                ),
                host.runtimeStatusSnapshot()
            )

            val dispatched = attempt.start()
            assertEquals(UnityRuntimeContainerPhase.Active, dispatched.phase)
            assertTrue(dispatched.routeDispatched)
            assertEquals(1, player.sendCount)
            assertTrue(attempt.revokeInputAndFocus())

            assertEquals(
                UnityRuntimeContainerTeardownResult.Confirmed,
                attempt.destroy()
            )
            val destroyed = host.runtimeStatusSnapshot()
            assertEquals(UnityRuntimeContainerPhase.Destroyed, destroyed.phase)
            assertEquals(UnityRuntimeContainerOwnership.NeverCreated, destroyed.ownership)
            assertEquals(
                UnityRuntimeContainerTeardownEvidence.Confirmed,
                destroyed.teardown
            )
            assertEquals(1, player.destroyCount)
        }
    }

    @Test
    fun absentRuntimeReportsNeverCreatedAndCanReleaseItsReservation() {
        onMainThread { context ->
            val host = UnityRuntimeContainer(context, dependencies { null })

            val unavailable = host.runtimeStatusSnapshot()
            assertEquals(UnityRuntimeContainerPhase.Failed, unavailable.phase)
            assertEquals(UnityRuntimeContainerOwnership.NeverCreated, unavailable.ownership)
            assertEquals(
                UnityRuntimeContainerFailure.RuntimeUnavailable,
                unavailable.failure
            )

            assertEquals(
                UnityRuntimeContainerTeardownResult.Confirmed,
                host.destroyUnity()
            )
        }
    }

    @Test
    fun throwingConstructionRetainsOwnershipAndReportsUncertainTeardown() {
        onMainThread { context ->
            val host = UnityRuntimeContainer(
                context,
                dependencies { error("synthetic native constructor failure") }
            )

            val failed = host.runtimeStatusSnapshot()
            assertEquals(UnityRuntimeContainerPhase.Failed, failed.phase)
            assertEquals(UnityRuntimeContainerOwnership.Uncertain, failed.ownership)
            assertEquals(UnityRuntimeContainerFailure.ConstructionFailed, failed.failure)

            assertEquals(
                UnityRuntimeContainerTeardownResult.Uncertain,
                host.destroyUnity()
            )
            assertEquals(
                UnityRuntimeContainerTeardownEvidence.Uncertain,
                host.runtimeStatusSnapshot().teardown
            )
        }
    }

    @Test
    fun lifecycleFailurePreservesItsTypedCauseThroughConfirmedTeardown() {
        onMainThread { context ->
            val player = StatusTestPlayer(context, resumeResult = false)
            val host = UnityRuntimeContainer(context, dependencies { player })

            host.resumeUnity()

            val destroyed = host.runtimeStatusSnapshot()
            assertEquals(UnityRuntimeContainerPhase.Destroyed, destroyed.phase)
            assertEquals(UnityRuntimeContainerFailure.LifecycleFailed, destroyed.failure)
            assertEquals(
                UnityRuntimeContainerTeardownEvidence.Confirmed,
                destroyed.teardown
            )
            assertFalse(destroyed.routeDispatched)
        }
    }

    @Test
    fun invalidLaunchRouteRemainsANonAuthoritativeActiveFailure() {
        onMainThread { context ->
            val host = UnityRuntimeContainer(
                context,
                dependencies { StatusTestPlayer(context) }
            )
            val attempt = UnityRuntimeContainerNativeLaunchAttempt(
                container = host,
                generation = 11L,
                routeId = "invalid route id"
            )

            val failed = attempt.start()

            assertEquals(UnityRuntimeContainerPhase.Failed, failed.phase)
            assertEquals(UnityRuntimeContainerOwnership.Active, failed.ownership)
            assertEquals(
                UnityRuntimeContainerFailure.BridgeProtocolFailed,
                failed.failure
            )
            assertFalse(failed.routeDispatched)
            assertTrue(attempt.revokeInputAndFocus())
            assertEquals(
                UnityRuntimeContainerTeardownResult.Confirmed,
                attempt.destroy()
            )
        }
    }

    private fun dependencies(
        createPlayer: (Context) -> UnityEmbeddedPlayer?
    ) = UnityRuntimeHostDependencies(
        ownershipRegistry = UnityRuntimeHostRegistry(),
        playerFactory = UnityEmbeddedPlayerFactory(createPlayer),
        callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory {
            object : UnityHostCallbackRegistrar<ComponentCallbacks2> {
                override fun register(callback: ComponentCallbacks2) = true

                override fun unregister(callback: ComponentCallbacks2) = true
            }
        }
    )

    private fun onMainThread(block: (Context) -> Unit) {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        instrumentation.runOnMainSync {
            block(instrumentation.targetContext)
        }
    }

    private class StatusTestPlayer(
        context: Context,
        private val resumeResult: Boolean = true
    ) : UnityEmbeddedPlayer {
        override val view: View = FrameLayout(context)
        var sendCount = 0
        var destroyCount = 0

        override fun resume() = resumeResult

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
            return true
        }
    }
}
