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
    fun runtimeCallbackFailuresAreContainedAndTeardownStillCompletes() {
        val runtime = RecordingRuntime(throwOn = setOf("resume", "focus:true", "pause"))
        val lifecycle = UnityHostLifecycleController(runtime)

        lifecycle.onWindowFocusChanged(true)
        lifecycle.resume()
        lifecycle.destroy()

        assertEquals(UnityHostLifecycleState.Destroyed, lifecycle.snapshot().state)
        assertEquals(listOf("resume", "pause", "destroy"), runtime.calls)
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
}
