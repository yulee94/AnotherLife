package com.example.anotherlife.ui.unity

import android.content.ComponentCallbacks2
import android.content.Context
import android.content.res.Configuration
import android.graphics.Color
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.FrameLayout
import android.widget.TextView
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Modifier
import androidx.compose.ui.viewinterop.AndroidView
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import java.lang.reflect.Method
import java.lang.reflect.Modifier as ReflectionModifier

/**
 * Hosts the Unity runtime when the exported Unity Android library is present.
 */
@Composable
fun UnityView(
    routeId: String,
    modifier: Modifier = Modifier,
    routeLaunchSequence: Long = 0L,
    routeIntent: UnityRouteIntent = UnityRouteIntent.Preview,
    requestedCapabilities: List<String> = emptyList(),
    onRouteDispatched: () -> Unit = {},
    onOutcome: (UnityRouteOutcome) -> Unit = {},
    onProtocolError: (UnityBridgeProtocolError) -> Unit = {}
) {
    val lifecycleOwner = LocalLifecycleOwner.current
    val hostState = remember { mutableStateOf<UnityRuntimeContainer?>(null) }
    val currentRouteId = rememberUpdatedState(routeId)
    val currentRouteLaunchSequence = rememberUpdatedState(routeLaunchSequence)
    val currentRouteIntent = rememberUpdatedState(routeIntent)
    val currentRequestedCapabilities = rememberUpdatedState(requestedCapabilities)
    val currentOnRouteDispatched = rememberUpdatedState(onRouteDispatched)
    val currentOnOutcome = rememberUpdatedState(onOutcome)
    val currentOnProtocolError = rememberUpdatedState(onProtocolError)

    AndroidView(
        factory = { context ->
            UnityRuntimeContainer(context).apply {
                layoutParams = ViewGroup.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT,
                    ViewGroup.LayoutParams.MATCH_PARENT
                )
                hostState.value = this
                if (
                    setRoute(
                        routeId = currentRouteId.value,
                        routeLaunchSequence = currentRouteLaunchSequence.value,
                        routeIntent = currentRouteIntent.value,
                        requestedCapabilities = currentRequestedCapabilities.value,
                        onOutcome = currentOnOutcome.value,
                        onProtocolError = currentOnProtocolError.value
                    )
                ) {
                    currentOnRouteDispatched.value()
                }
            }
        },
        update = { host ->
            if (
                host.setRoute(
                    routeId = routeId,
                    routeLaunchSequence = routeLaunchSequence,
                    routeIntent = routeIntent,
                    requestedCapabilities = requestedCapabilities,
                    onOutcome = currentOnOutcome.value,
                    onProtocolError = currentOnProtocolError.value
                )
            ) {
                currentOnRouteDispatched.value()
            }
        },
        modifier = modifier.fillMaxSize()
    )

    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            val host = hostState.value ?: return@LifecycleEventObserver
            when (event) {
                Lifecycle.Event.ON_RESUME -> host.resumeUnity()
                Lifecycle.Event.ON_PAUSE -> host.pauseUnity()
                Lifecycle.Event.ON_STOP -> host.stopUnity()
                Lifecycle.Event.ON_DESTROY -> host.destroyUnity()
                else -> Unit
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        hostState.value?.synchronizeLifecycle(lifecycleOwner.lifecycle.currentState)

        onDispose {
            lifecycleOwner.lifecycle.removeObserver(observer)
            hostState.value?.destroyUnity()
            hostState.value = null
        }
    }
}

private class UnityRuntimeContainer(context: Context) : FrameLayout(context) {
    private val ownershipToken = UnityRuntimeHostOwnership.registry.tryAcquire()
    private val unityPlayer = ownershipToken?.let { ReflectionUnityPlayer.create(context) }
    private val lifecycleController = unityPlayer?.let { UnityHostLifecycleController(it) }
    private val statusView = createStatusView(context)
    private val bridgeSession = UnityBridgeSession()
    private val callbackApplication = context.applicationContext
    private val componentCallbacks = if (lifecycleController != null) {
        object : ComponentCallbacks2 {
            override fun onConfigurationChanged(newConfig: Configuration) {
                configurationChangedUnity(newConfig)
            }

            @Suppress("OVERRIDE_DEPRECATION")
            override fun onLowMemory() {
                lowMemoryUnity()
            }

            override fun onTrimMemory(level: Int) {
                trimMemoryUnity(level)
            }
        }
    } else {
        null
    }
    private var componentCallbacksRegistered = false
    @Volatile
    private var destroyed = false
    private var activeLaunch: UnityRouteLaunch? = null
    private var onOutcome: (UnityRouteOutcome) -> Unit = {}
    private var onProtocolError: (UnityBridgeProtocolError) -> Unit = {}
    private val callbackToken = ownershipToken?.let {
        UnityBridgeCallbacks.register { rawJson ->
            post {
                if (!destroyed) {
                    handleOutcome(rawJson)
                }
            }
        }
    }

    init {
        setBackgroundColor(Color.BLACK)

        componentCallbacks?.let { callbacks ->
            componentCallbacksRegistered = runCatching {
                callbackApplication.registerComponentCallbacks(callbacks)
                true
            }.getOrDefault(false)
        }

        if (ownershipToken == null) {
            showStatus("Unity runtime unavailable\nHost already active")
        } else if (unityPlayer != null) {
            addView(
                unityPlayer.view,
                LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT)
            )
        } else {
            showStatus("Unity runtime unavailable")
        }
    }

    fun setRoute(
        routeId: String,
        routeLaunchSequence: Long,
        routeIntent: UnityRouteIntent,
        requestedCapabilities: List<String>,
        onOutcome: (UnityRouteOutcome) -> Unit,
        onProtocolError: (UnityBridgeProtocolError) -> Unit
    ): Boolean {
        this.onOutcome = onOutcome
        this.onProtocolError = onProtocolError

        val launch = UnityRouteLaunch(
            routeId = routeId,
            launchSequence = routeLaunchSequence,
            intent = routeIntent,
            requestedCapabilities = requestedCapabilities.toList()
        )
        if (launch == activeLaunch) return false
        activeLaunch = launch

        val start = bridgeSession.startRoute(
            routeId = routeId,
            intent = routeIntent,
            requestedCapabilities = requestedCapabilities
        )
        if (start is UnityBridgeSessionStart.Rejected) {
            showProtocolError(start.error)
            return false
        }

        start as UnityBridgeSessionStart.Started
        if (ownershipToken == null) {
            showStatus("Unity runtime unavailable\nHost already active")
            return false
        }
        val player = unityPlayer
        if (player == null) {
            showStatus("Unity runtime unavailable\nRoute: $routeId")
            return false
        }
        if (!player.sendMessage("AndroidBridge", "SetRouteContext", start.encodedPayload)) {
            showProtocolError(
                UnityBridgeProtocolError(UnityBridgeProtocolErrorCode.SendUnavailable)
            )
            return false
        }

        hideStatus()
        return true
    }

    fun resumeUnity() {
        if (destroyed) return
        val controller = lifecycleController ?: return
        controller.resume()
        closeAfterLifecycleFailure(controller)
    }

    fun pauseUnity() {
        if (destroyed) return
        val controller = lifecycleController ?: return
        controller.pause()
        closeAfterLifecycleFailure(controller)
    }

    fun stopUnity() {
        if (destroyed) return
        val controller = lifecycleController ?: return
        controller.stop()
        closeAfterLifecycleFailure(controller)
    }

    fun synchronizeLifecycle(state: Lifecycle.State) {
        when {
            state == Lifecycle.State.DESTROYED -> destroyUnity()
            state.isAtLeast(Lifecycle.State.RESUMED) -> resumeUnity()
            else -> pauseUnity()
        }
    }

    fun destroyUnity() {
        if (destroyed) return
        destroyed = true
        bridgeSession.close()
        callbackToken?.let(UnityBridgeCallbacks::clear)
        val componentCallbacksReleased = if (componentCallbacksRegistered) {
            val released = componentCallbacks?.let { callbacks ->
                runCatching {
                    callbackApplication.unregisterComponentCallbacks(callbacks)
                }.isSuccess
            } ?: true
            if (released) componentCallbacksRegistered = false
            released
        } else {
            true
        }
        lifecycleController?.destroy()
        removeAllViews()
        if (
            componentCallbacksReleased &&
            lifecycleController?.canReleaseOwnership() != false
        ) {
            ownershipToken?.let(UnityRuntimeHostOwnership.registry::release)
        }
    }

    override fun onAttachedToWindow() {
        super.onAttachedToWindow()
        windowFocusChangedUnity(hasWindowFocus())
    }

    override fun onDetachedFromWindow() {
        windowFocusChangedUnity(false)
        super.onDetachedFromWindow()
    }

    override fun onWindowFocusChanged(hasWindowFocus: Boolean) {
        super.onWindowFocusChanged(hasWindowFocus)
        windowFocusChangedUnity(hasWindowFocus)
    }

    private fun windowFocusChangedUnity(hasWindowFocus: Boolean) {
        if (destroyed) return
        val controller = lifecycleController ?: return
        controller.onWindowFocusChanged(hasWindowFocus)
        closeAfterLifecycleFailure(controller)
    }

    private fun lowMemoryUnity() {
        if (destroyed) return
        val controller = lifecycleController ?: return
        controller.lowMemory()
        closeAfterLifecycleFailure(controller)
    }

    private fun trimMemoryUnity(level: Int) {
        if (destroyed) return
        val controller = lifecycleController ?: return
        controller.trimMemory(level)
        closeAfterLifecycleFailure(controller)
    }

    private fun configurationChangedUnity(configuration: Configuration) {
        if (destroyed) return
        val controller = lifecycleController ?: return
        controller.configurationChanged(configuration)
        closeAfterLifecycleFailure(controller)
    }

    private fun closeAfterLifecycleFailure(
        controller: UnityHostLifecycleController<Configuration>
    ) {
        if (!controller.isDestroyed() || destroyed) return
        destroyUnity()
        showStatus("Unity runtime unavailable\nLifecycle failure")
    }

    private fun handleOutcome(rawJson: String?) {
        when (val delivery = bridgeSession.consumeOutcome(rawJson)) {
            is UnityBridgeSessionDelivery.Delivered -> {
                hideStatus()
                onOutcome(delivery.outcome)
            }

            is UnityBridgeSessionDelivery.Rejected -> showProtocolError(delivery.error)
        }
    }

    private fun showProtocolError(error: UnityBridgeProtocolError) {
        showStatus("Unity bridge unavailable\nCode: ${error.code.wireValue}")
        onProtocolError(error)
    }

    private fun showStatus(message: String) {
        statusView.text = message
        if (statusView.parent == null) {
            addView(statusView)
        }
        statusView.bringToFront()
    }

    private fun hideStatus() {
        if (statusView.parent === this) {
            removeView(statusView)
        }
    }

    private fun createStatusView(context: Context): TextView {
        return TextView(context).apply {
            layoutParams = LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT,
                ViewGroup.LayoutParams.WRAP_CONTENT,
                Gravity.CENTER
            )
            setTextColor(Color.WHITE)
            textSize = 18f
            textAlignment = TextView.TEXT_ALIGNMENT_CENTER
        }
    }
}

private class ReflectionUnityPlayer private constructor(
    private val instance: Any,
    private val resumeMethod: Method,
    private val pauseMethod: Method,
    private val destroyMethod: Method,
    private val windowFocusChangedMethod: Method,
    private val lowMemoryMethod: Method,
    private val configurationChangedMethod: Method,
    private val sendMessageMethod: Method
) : UnityHostLifecycleRuntime<Configuration> {

    val view: View = instance as View

    override fun resume() = resumeMethod.invokeSafely(instance)

    override fun pause() = pauseMethod.invokeSafely(instance)

    override fun destroy() = destroyMethod.invokeSafely(instance)

    override fun windowFocusChanged(hasFocus: Boolean) =
        windowFocusChangedMethod.invokeSafely(instance, hasFocus)

    override fun lowMemory() = lowMemoryMethod.invokeSafely(instance)

    override fun configurationChanged(configuration: Configuration) =
        configurationChangedMethod.invokeSafely(instance, configuration)

    fun sendMessage(gameObject: String, method: String, payload: String): Boolean {
        return runCatching {
            sendMessageMethod.invoke(null, gameObject, method, payload)
            true
        }.getOrDefault(false)
    }

    companion object {
        fun create(context: Context): ReflectionUnityPlayer? {
            return runCatching {
                val playerClass = Class.forName("com.unity3d.player.UnityPlayer")
                if (!View::class.java.isAssignableFrom(playerClass)) return null
                val constructor = playerClass.constructors
                    .filter { candidate ->
                        candidate.parameterTypes.size == 1 &&
                            candidate.parameterTypes[0].isAssignableFrom(context.javaClass)
                    }
                    .minByOrNull { candidate ->
                        if (candidate.parameterTypes[0] == Context::class.java) 0 else 1
                    } ?: return null
                val resumeMethod = playerClass.noArgMethod("resume") ?: return null
                val pauseMethod = playerClass.noArgMethod("pause") ?: return null
                val destroyMethod = playerClass.noArgMethod("destroy") ?: return null
                val lowMemoryMethod = playerClass.noArgMethod("lowMemory") ?: return null
                val windowFocusChangedMethod = playerClass.methods.firstOrNull { method ->
                    method.name == "windowFocusChanged" &&
                        method.parameterTypes.contentEquals(arrayOf(java.lang.Boolean.TYPE))
                } ?: return null
                val configurationChangedMethod = playerClass.methods.firstOrNull { method ->
                    method.name == "configurationChanged" &&
                        method.parameterTypes.contentEquals(arrayOf(Configuration::class.java))
                } ?: return null
                val sendMessageMethod = playerClass.methods.firstOrNull { method ->
                    method.name == "UnitySendMessage" &&
                        ReflectionModifier.isStatic(method.modifiers) &&
                        method.parameterTypes.contentEquals(
                            arrayOf(String::class.java, String::class.java, String::class.java)
                        )
                } ?: return null
                val instance = constructor.newInstance(context)

                if (instance !is View) return null
                ReflectionUnityPlayer(
                    instance = instance,
                    resumeMethod = resumeMethod,
                    pauseMethod = pauseMethod,
                    destroyMethod = destroyMethod,
                    windowFocusChangedMethod = windowFocusChangedMethod,
                    lowMemoryMethod = lowMemoryMethod,
                    configurationChangedMethod = configurationChangedMethod,
                    sendMessageMethod = sendMessageMethod
                )
            }.getOrNull()
        }
    }
}

private fun Method.invokeSafely(instance: Any, vararg arguments: Any?): Boolean {
    return runCatching {
        invoke(instance, *arguments)
        true
    }.getOrDefault(false)
}

private fun Class<*>.noArgMethod(name: String): Method? {
    return methods.firstOrNull { method ->
        method.name == name && method.parameterTypes.isEmpty()
    }
}

private data class UnityRouteLaunch(
    val routeId: String,
    val launchSequence: Long,
    val intent: UnityRouteIntent,
    val requestedCapabilities: List<String>
)
