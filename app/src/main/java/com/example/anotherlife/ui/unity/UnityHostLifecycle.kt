package com.example.anotherlife.ui.unity

internal enum class UnityHostLifecycleState {
    Paused,
    Resumed,
    Destroyed
}

internal data class UnityHostLifecycleSnapshot(
    val state: UnityHostLifecycleState,
    val hasWindowFocus: Boolean,
    val forwardedWindowFocus: Boolean,
    val destroySucceeded: Boolean? = null
)

internal interface UnityHostLifecycleRuntime<in ConfigurationT> {
    fun resume(): Boolean

    fun pause(): Boolean

    fun destroy(): Boolean

    fun windowFocusChanged(hasFocus: Boolean): Boolean

    fun lowMemory(): Boolean

    fun configurationChanged(configuration: ConfigurationT): Boolean
}

/**
 * Serializes Android host signals before they reach one embedded Unity runtime.
 *
 * Android delivers these callbacks on the main thread in production. The synchronized state keeps
 * teardown fail-closed if a late callback races disposal in a test or vendor-specific host.
 */
internal class UnityHostLifecycleController<ConfigurationT>(
    private val runtime: UnityHostLifecycleRuntime<ConfigurationT>
) {
    private var state = UnityHostLifecycleState.Paused
    private var hasWindowFocus = false
    private var forwardedWindowFocus = false
    private var destroySucceeded: Boolean? = null

    @Synchronized
    fun resume() {
        if (state != UnityHostLifecycleState.Paused) return
        state = UnityHostLifecycleState.Resumed
        if (!invokeSafely(runtime::resume)) {
            failClosed()
            return
        }
        if (hasWindowFocus && !forwardedWindowFocus) {
            forwardedWindowFocus = true
            if (!invokeSafely { runtime.windowFocusChanged(true) }) failClosed()
        }
    }

    @Synchronized
    fun pause() {
        if (state != UnityHostLifecycleState.Resumed) return
        state = UnityHostLifecycleState.Paused

        var succeeded = true
        if (forwardedWindowFocus) {
            forwardedWindowFocus = false
            if (!invokeSafely { runtime.windowFocusChanged(false) }) succeeded = false
        }
        if (!invokeSafely(runtime::pause)) succeeded = false
        if (!succeeded) failClosed()
    }

    fun stop() = pause()

    @Synchronized
    fun onWindowFocusChanged(hasFocus: Boolean) {
        if (state == UnityHostLifecycleState.Destroyed) return
        hasWindowFocus = hasFocus
        if (state == UnityHostLifecycleState.Resumed && forwardedWindowFocus != hasFocus) {
            forwardedWindowFocus = hasFocus
            if (!invokeSafely { runtime.windowFocusChanged(hasFocus) }) failClosed()
        }
    }

    @Synchronized
    fun lowMemory() {
        if (state != UnityHostLifecycleState.Destroyed && !invokeSafely(runtime::lowMemory)) {
            failClosed()
        }
    }

    @Synchronized
    fun trimMemory(level: Int) {
        if (level >= RUNNING_LOW_MEMORY_TRIM_LEVEL) lowMemory()
    }

    @Synchronized
    fun configurationChanged(configuration: ConfigurationT) {
        if (
            state != UnityHostLifecycleState.Destroyed &&
            !invokeSafely { runtime.configurationChanged(configuration) }
        ) {
            failClosed()
        }
    }

    @Synchronized
    fun destroy() {
        if (state == UnityHostLifecycleState.Destroyed) return
        val shouldClearFocus = forwardedWindowFocus
        val shouldPause = state == UnityHostLifecycleState.Resumed
        state = UnityHostLifecycleState.Destroyed
        hasWindowFocus = false
        forwardedWindowFocus = false

        if (shouldClearFocus) invokeSafely { runtime.windowFocusChanged(false) }
        if (shouldPause) invokeSafely(runtime::pause)
        destroySucceeded = invokeSafely(runtime::destroy)
    }

    @Synchronized
    fun isDestroyed(): Boolean = state == UnityHostLifecycleState.Destroyed

    @Synchronized
    fun canReleaseOwnership(): Boolean =
        state == UnityHostLifecycleState.Destroyed && destroySucceeded == true

    @Synchronized
    fun snapshot() = UnityHostLifecycleSnapshot(
        state,
        hasWindowFocus,
        forwardedWindowFocus,
        destroySucceeded
    )

    private fun failClosed() {
        destroy()
    }

    private inline fun invokeSafely(action: () -> Boolean): Boolean {
        return runCatching(action).getOrDefault(false)
    }

    private companion object {
        // ComponentCallbacks2.TRIM_MEMORY_RUNNING_LOW without depending on its deprecated symbol.
        const val RUNNING_LOW_MEMORY_TRIM_LEVEL = 10
    }
}

internal class UnityRuntimeHostLease internal constructor()

internal class UnityRuntimeHostRegistry {
    private var activeLease: UnityRuntimeHostLease? = null

    @Synchronized
    fun tryAcquire(): UnityRuntimeHostLease? {
        if (activeLease != null) return null
        return UnityRuntimeHostLease().also { activeLease = it }
    }

    @Synchronized
    fun isOwner(lease: UnityRuntimeHostLease): Boolean = activeLease === lease

    @Synchronized
    fun release(lease: UnityRuntimeHostLease): Boolean {
        if (activeLease !== lease) return false
        activeLease = null
        return true
    }
}

internal object UnityRuntimeHostOwnership {
    val registry = UnityRuntimeHostRegistry()
}
