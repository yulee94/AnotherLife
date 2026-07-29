package com.example.anotherlife.ui.unity

import android.content.Context
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
                Lifecycle.Event.ON_DESTROY -> host.destroyUnity()
                else -> Unit
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)

        onDispose {
            lifecycleOwner.lifecycle.removeObserver(observer)
            hostState.value?.destroyUnity()
            hostState.value = null
        }
    }
}

private class UnityRuntimeContainer(context: Context) : FrameLayout(context) {
    private val unityPlayer = ReflectionUnityPlayer.create(context)
    private val statusView = createStatusView(context)
    private val bridgeSession = UnityBridgeSession()
    @Volatile
    private var destroyed = false
    private var activeLaunch: UnityRouteLaunch? = null
    private var onOutcome: (UnityRouteOutcome) -> Unit = {}
    private var onProtocolError: (UnityBridgeProtocolError) -> Unit = {}
    private val callbackToken = UnityBridgeCallbacks.register { rawJson ->
        post {
            if (!destroyed) {
                handleOutcome(rawJson)
            }
        }
    }

    init {
        setBackgroundColor(Color.BLACK)

        if (unityPlayer != null) {
            addView(
                unityPlayer.view,
                LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT)
            )
            unityPlayer.windowFocusChanged(true)
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
        if (unityPlayer == null) {
            showStatus("Unity runtime unavailable\nRoute: $routeId")
            return false
        }
        if (!unityPlayer.sendMessage("AndroidBridge", "SetRouteContext", start.encodedPayload)) {
            showProtocolError(
                UnityBridgeProtocolError(UnityBridgeProtocolErrorCode.SendUnavailable)
            )
            return false
        }

        hideStatus()
        return true
    }

    fun resumeUnity() {
        if (!destroyed) unityPlayer?.resume()
    }

    fun pauseUnity() {
        if (!destroyed) unityPlayer?.pause()
    }

    fun destroyUnity() {
        if (destroyed) return
        destroyed = true
        bridgeSession.close()
        UnityBridgeCallbacks.clear(callbackToken)
        unityPlayer?.destroy()
        removeAllViews()
    }

    private fun handleOutcome(rawJson: String) {
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
    private val playerClass: Class<*>
) {
    private val resumeMethod = playerClass.noArgMethod("resume")
    private val pauseMethod = playerClass.noArgMethod("pause")
    private val destroyMethod = playerClass.noArgMethod("destroy")
    private val windowFocusChangedMethod = playerClass.methods.firstOrNull { method ->
        method.name == "windowFocusChanged" &&
            method.parameterTypes.contentEquals(arrayOf(java.lang.Boolean.TYPE))
    }
    private val sendMessageMethod = playerClass.methods.firstOrNull { method ->
        method.name == "UnitySendMessage" &&
            method.parameterTypes.contentEquals(
                arrayOf(String::class.java, String::class.java, String::class.java)
            )
    }

    val view: View = instance as View

    fun resume() = resumeMethod.invokeSafely()

    fun pause() = pauseMethod.invokeSafely()

    fun destroy() = destroyMethod.invokeSafely()

    fun windowFocusChanged(hasFocus: Boolean) {
        runCatching { windowFocusChangedMethod?.invoke(instance, hasFocus) }
    }

    fun sendMessage(gameObject: String, method: String, payload: String): Boolean {
        val target = sendMessageMethod ?: return false
        return runCatching {
            target.invoke(instance, gameObject, method, payload)
            true
        }.getOrDefault(false)
    }

    private fun Method?.invokeSafely() {
        runCatching { this?.invoke(instance) }
    }

    companion object {
        fun create(context: Context): ReflectionUnityPlayer? {
            return runCatching {
                val playerClass = Class.forName("com.unity3d.player.UnityPlayer")
                val constructor = playerClass.constructors
                    .first { it.parameterTypes.size == 1 }
                val instance = constructor.newInstance(context)

                if (instance !is View) return null
                ReflectionUnityPlayer(instance, playerClass)
            }.getOrNull()
        }
    }
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
