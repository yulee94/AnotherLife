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
import org.json.JSONObject

/**
 * Hosts the Unity runtime when the exported Unity Android library is present.
 */
@Composable
fun UnityView(
    modifier: Modifier = Modifier,
    routeTag: String = "Main",
    onReady: () -> Unit = {},
    onOutcome: (UnityRouteOutcome) -> Unit = {}
) {
    val lifecycleOwner = LocalLifecycleOwner.current
    val hostState = remember { mutableStateOf<UnityRuntimeContainer?>(null) }
    val currentRouteTag = rememberUpdatedState(routeTag)
    val currentOnReady = rememberUpdatedState(onReady)
    val currentOnOutcome = rememberUpdatedState(onOutcome)

    AndroidView(
        factory = { context ->
            UnityRuntimeContainer(context).apply {
                layoutParams = ViewGroup.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT,
                    ViewGroup.LayoutParams.MATCH_PARENT
                )
                hostState.value = this
                setRoute(currentRouteTag.value) { currentOnOutcome.value(it) }
                currentOnReady.value()
            }
        },
        update = { host ->
            host.setRoute(routeTag) { currentOnOutcome.value(it) }
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
            UnityBridgeCallbacks.clear()
        }
    }
}

data class UnityRouteOutcome(
    val routeTag: String,
    val status: UnityRouteOutcomeStatus,
    val payload: String?
) {
    companion object {
        fun fromJsonOrFailure(rawJson: String, fallbackRouteTag: String): UnityRouteOutcome {
            return runCatching {
                val json = JSONObject(rawJson)
                val routeTag = json.optString("routeTag", fallbackRouteTag)
                val status = UnityRouteOutcomeStatus.fromWireValue(json.optString("status", "failure"))
                val payload = json.optString("payload").ifBlank { null }

                UnityRouteOutcome(routeTag, status, payload)
            }.getOrElse {
                UnityRouteOutcome(fallbackRouteTag, UnityRouteOutcomeStatus.Failure, rawJson)
            }
        }
    }
}

enum class UnityRouteOutcomeStatus {
    Success,
    Failure,
    Cancelled;

    companion object {
        fun fromWireValue(value: String): UnityRouteOutcomeStatus {
            return when (value.lowercase()) {
                "success" -> Success
                "cancelled", "canceled" -> Cancelled
                else -> Failure
            }
        }
    }
}

object UnityBridgeCallbacks {
    @Volatile
    private var callback: ((String) -> Unit)? = null

    fun register(callback: (String) -> Unit) {
        this.callback = callback
    }

    fun clear() {
        callback = null
    }

    @JvmStatic
    fun reportOutcome(rawJson: String) {
        callback?.invoke(rawJson)
    }
}

private class UnityRuntimeContainer(context: Context) : FrameLayout(context) {
    private val unityPlayer = ReflectionUnityPlayer.create(context)
    private val statusView = createStatusView(context)
    private var currentRouteTag: String? = null
    private var outcomeDeliveredForRoute: String? = null
    private var destroyed = false

    init {
        setBackgroundColor(Color.BLACK)

        if (unityPlayer != null) {
            addView(
                unityPlayer.view,
                LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT)
            )
            unityPlayer.windowFocusChanged(true)
        } else {
            addView(statusView)
        }
    }

    fun setRoute(routeTag: String, onOutcome: (UnityRouteOutcome) -> Unit) {
        UnityBridgeCallbacks.register { rawJson ->
            val activeRoute = currentRouteTag ?: return@register
            val outcome = UnityRouteOutcome.fromJsonOrFailure(rawJson, activeRoute)
            if (outcome.routeTag != activeRoute) return@register
            if (outcomeDeliveredForRoute == outcome.routeTag) return@register

            outcomeDeliveredForRoute = outcome.routeTag
            onOutcome(outcome)
        }

        if (routeTag == currentRouteTag) return

        currentRouteTag = routeTag
        outcomeDeliveredForRoute = null
        if (unityPlayer != null) {
            val payload = JSONObject()
                .put("routeTag", routeTag)
                .put("contractVersion", 1)
                .toString()
            unityPlayer.sendMessage("AndroidBridge", "SetRouteContext", payload)
        } else {
            statusView.text = "Unity runtime unavailable\nRoute: $routeTag"
        }
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
        unityPlayer?.destroy()
        removeAllViews()
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
    val view: View = instance as View

    fun resume() = invokeNoArgs("resume")

    fun pause() = invokeNoArgs("pause")

    fun destroy() = invokeNoArgs("destroy")

    fun windowFocusChanged(hasFocus: Boolean) {
        playerClass.methods
            .firstOrNull { method ->
                method.name == "windowFocusChanged" &&
                    method.parameterTypes.size == 1 &&
                    method.parameterTypes[0] == java.lang.Boolean.TYPE
            }
            ?.invoke(instance, hasFocus)
    }

    fun sendMessage(gameObject: String, method: String, payload: String) {
        playerClass.methods
            .firstOrNull { candidate ->
                candidate.name == "UnitySendMessage" &&
                    candidate.parameterTypes.contentEquals(
                        arrayOf(String::class.java, String::class.java, String::class.java)
                    )
            }
            ?.invoke(instance, gameObject, method, payload)
    }

    private fun invokeNoArgs(name: String) {
        playerClass.methods
            .firstOrNull { method -> method.name == name && method.parameterTypes.isEmpty() }
            ?.invoke(instance)
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
