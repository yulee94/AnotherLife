package com.example.anotherlife.ui.unity

import android.os.Looper
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference
import org.junit.Assert.assertEquals
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
}
