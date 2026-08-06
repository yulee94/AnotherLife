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

internal class UnityRuntimeHostWaitToken internal constructor()

internal sealed interface UnityRuntimeHostAcquisition {
    data class Acquired(val lease: UnityRuntimeHostLease) : UnityRuntimeHostAcquisition

    data class Waiting(val token: UnityRuntimeHostWaitToken) : UnityRuntimeHostAcquisition

    object CapacityReached : UnityRuntimeHostAcquisition
}

internal class UnityRuntimeHostRegistry(
    private val maxWaiters: Int = DEFAULT_MAX_WAITERS
) {
    private var activeLease: UnityRuntimeHostLease? = null
    private val waiters = LinkedHashMap<
        UnityRuntimeHostWaitToken,
        (UnityRuntimeHostLease) -> Unit
    >()

    init {
        require(maxWaiters > 0)
    }

    @Synchronized
    fun tryAcquire(): UnityRuntimeHostLease? {
        if (activeLease != null) return null
        return UnityRuntimeHostLease().also { activeLease = it }
    }

    @Synchronized
    fun acquireOrQueue(
        onGranted: (UnityRuntimeHostLease) -> Unit
    ): UnityRuntimeHostAcquisition {
        if (activeLease == null) {
            return UnityRuntimeHostAcquisition.Acquired(
                UnityRuntimeHostLease().also { activeLease = it }
            )
        }
        if (waiters.size >= maxWaiters) {
            return UnityRuntimeHostAcquisition.CapacityReached
        }

        val token = UnityRuntimeHostWaitToken()
        waiters[token] = onGranted
        return UnityRuntimeHostAcquisition.Waiting(token)
    }

    @Synchronized
    fun isOwner(lease: UnityRuntimeHostLease): Boolean = activeLease === lease

    fun release(lease: UnityRuntimeHostLease): Boolean {
        val grant = synchronized(this) {
            if (activeLease !== lease) return false
            activeLease = null

            val next = waiters.entries.firstOrNull() ?: return@synchronized null
            waiters.remove(next.key)
            val replacementLease = UnityRuntimeHostLease()
            activeLease = replacementLease
            next.value to replacementLease
        }

        grant?.let { (callback, replacementLease) ->
            if (runCatching { callback(replacementLease) }.isFailure) {
                release(replacementLease)
            }
        }
        return true
    }

    @Synchronized
    fun cancel(token: UnityRuntimeHostWaitToken): Boolean = waiters.remove(token) != null

    @Synchronized
    fun waitingCount(): Int = waiters.size

    private companion object {
        const val DEFAULT_MAX_WAITERS = 4
    }
}

internal object UnityRuntimeHostOwnership {
    val registry = UnityRuntimeHostRegistry()
}

internal interface UnityHostCallbackRegistrar<CallbackT> {
    fun register(callback: CallbackT): Boolean

    fun unregister(callback: CallbackT): Boolean
}

internal class UnityHostCallbackRegistration<CallbackT>(
    private val registrar: UnityHostCallbackRegistrar<CallbackT>,
    private val callback: CallbackT
) {
    private var state = CallbackRegistrationState.Unregistered

    @Synchronized
    fun register(): Boolean {
        if (state == CallbackRegistrationState.Registered) return true
        return try {
            if (registrar.register(callback)) {
                state = CallbackRegistrationState.Registered
                true
            } else {
                state = CallbackRegistrationState.Unregistered
                false
            }
        } catch (_: Throwable) {
            // Registration may have completed before the external registrar threw. Teardown must
            // attempt an unregister and retain ownership if that result is also uncertain.
            state = CallbackRegistrationState.Uncertain
            false
        }
    }

    @Synchronized
    fun release(): Boolean {
        if (state == CallbackRegistrationState.Unregistered) return true
        if (!runCatching { registrar.unregister(callback) }.getOrDefault(false)) return false
        state = CallbackRegistrationState.Unregistered
        return true
    }

    @Synchronized
    fun isRegistered(): Boolean = state == CallbackRegistrationState.Registered

    private enum class CallbackRegistrationState {
        Unregistered,
        Registered,
        Uncertain
    }
}

internal sealed interface UnityBridgeCallbackAdmission {
    data class Payload(val rawJson: String) : UnityBridgeCallbackAdmission

    data class ProtocolError(
        val error: UnityBridgeProtocolError
    ) : UnityBridgeCallbackAdmission

    object Overflow : UnityBridgeCallbackAdmission

    object Dropped : UnityBridgeCallbackAdmission
}

internal data class UnityBridgeCallbackAdmissionSnapshot(
    val pendingCount: Int,
    val overflowPending: Boolean,
    val closed: Boolean
)

/**
 * Bounds callback work before any raw JNI string is captured by a main-thread Runnable.
 *
 * The one overflow sentinel closes admission without retaining the overflowing payload. Existing
 * admitted work drains in FIFO order, then the sentinel surfaces a typed fail-closed error.
 */
internal class UnityBridgeCallbackAdmissionGate(
    private val maxPendingCallbacks: Int = DEFAULT_MAX_PENDING_CALLBACKS,
    private val maxMessageBytes: Int = MAX_UNITY_BRIDGE_MESSAGE_BYTES
) {
    private var pendingCount = 0
    private var overflowPending = false
    private var closed = false

    init {
        require(maxPendingCallbacks > 0)
        require(maxMessageBytes > 0)
    }

    @Synchronized
    fun tryAdmit(rawJson: String?): UnityBridgeCallbackAdmission {
        if (closed) return UnityBridgeCallbackAdmission.Dropped
        if (pendingCount >= maxPendingCallbacks) {
            overflowPending = true
            closed = true
            return UnityBridgeCallbackAdmission.Overflow
        }

        pendingCount += 1
        return when {
            rawJson == null -> UnityBridgeCallbackAdmission.ProtocolError(
                UnityBridgeProtocolError(UnityBridgeProtocolErrorCode.NullMessage)
            )

            rawJson.exceedsUtf8LimitForAdmission(maxMessageBytes) ->
                UnityBridgeCallbackAdmission.ProtocolError(
                    UnityBridgeProtocolError(UnityBridgeProtocolErrorCode.MessageTooLarge)
                )

            else -> UnityBridgeCallbackAdmission.Payload(rawJson)
        }
    }

    @Synchronized
    fun complete(admission: UnityBridgeCallbackAdmission) {
        when (admission) {
            is UnityBridgeCallbackAdmission.Payload,
            is UnityBridgeCallbackAdmission.ProtocolError -> {
                check(pendingCount > 0)
                pendingCount -= 1
            }

            UnityBridgeCallbackAdmission.Overflow -> overflowPending = false
            UnityBridgeCallbackAdmission.Dropped -> Unit
        }
    }

    @Synchronized
    fun close() {
        closed = true
    }

    @Synchronized
    fun snapshot() = UnityBridgeCallbackAdmissionSnapshot(
        pendingCount = pendingCount,
        overflowPending = overflowPending,
        closed = closed
    )

    private companion object {
        const val DEFAULT_MAX_PENDING_CALLBACKS = 32
    }
}

private fun String.exceedsUtf8LimitForAdmission(maxBytes: Int): Boolean {
    var byteCount = 0
    var index = 0
    while (index < length) {
        val character = this[index]
        byteCount += when {
            character.code <= 0x7f -> 1
            character.code <= 0x7ff -> 2
            character.isHighSurrogate() &&
                index + 1 < length &&
                this[index + 1].isLowSurrogate() -> {
                index += 1
                4
            }

            else -> 3
        }
        if (byteCount > maxBytes) return true
        index += 1
    }
    return false
}
