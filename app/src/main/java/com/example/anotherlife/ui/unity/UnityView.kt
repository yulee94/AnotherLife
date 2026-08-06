package com.example.anotherlife.ui.unity

import android.content.ComponentCallbacks2
import android.content.Context
import android.content.res.Configuration
import android.graphics.Color
import android.os.Looper
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.FrameLayout
import android.widget.TextView
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.key
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
    UnityViewContent(
        routeId = routeId,
        modifier = modifier,
        routeLaunchSequence = routeLaunchSequence,
        routeIntent = routeIntent,
        requestedCapabilities = requestedCapabilities,
        onRouteDispatched = onRouteDispatched,
        onOutcome = onOutcome,
        onProtocolError = onProtocolError,
        dependencies = UnityRuntimeHostDependencies.Production
    )
}

@Composable
internal fun UnityViewForTest(
    routeId: String,
    dependencies: UnityRuntimeHostDependencies,
    routeLaunchSequence: Long = 0L,
    routeIntent: UnityRouteIntent = UnityRouteIntent.Preview,
    requestedCapabilities: List<String> = emptyList(),
    onRouteDispatched: () -> Unit = {},
    onOutcome: (UnityRouteOutcome) -> Unit = {},
    onProtocolError: (UnityBridgeProtocolError) -> Unit = {}
) {
    UnityViewContent(
        routeId = routeId,
        modifier = Modifier,
        routeLaunchSequence = routeLaunchSequence,
        routeIntent = routeIntent,
        requestedCapabilities = requestedCapabilities,
        onRouteDispatched = onRouteDispatched,
        onOutcome = onOutcome,
        onProtocolError = onProtocolError,
        dependencies = dependencies
    )
}

@Composable
private fun UnityViewContent(
    routeId: String,
    modifier: Modifier,
    routeLaunchSequence: Long,
    routeIntent: UnityRouteIntent,
    requestedCapabilities: List<String>,
    onRouteDispatched: () -> Unit,
    onOutcome: (UnityRouteOutcome) -> Unit,
    onProtocolError: (UnityBridgeProtocolError) -> Unit,
    dependencies: UnityRuntimeHostDependencies
) {
    val lifecycleOwner = LocalLifecycleOwner.current
    val currentRouteId = rememberUpdatedState(routeId)
    val currentRouteLaunchSequence = rememberUpdatedState(routeLaunchSequence)
    val currentRouteIntent = rememberUpdatedState(routeIntent)
    val currentRequestedCapabilities = rememberUpdatedState(requestedCapabilities)
    val currentOnRouteDispatched = rememberUpdatedState(onRouteDispatched)
    val currentOnOutcome = rememberUpdatedState(onOutcome)
    val currentOnProtocolError = rememberUpdatedState(onProtocolError)

    key(lifecycleOwner) {
        val hostState = remember { mutableStateOf<UnityRuntimeContainer?>(null) }

        AndroidView(
            factory = { context ->
                UnityRuntimeContainer(context, dependencies).apply {
                    layoutParams = ViewGroup.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT,
                        ViewGroup.LayoutParams.MATCH_PARENT
                    )
                    hostState.value = this
                    setRoute(
                        routeId = currentRouteId.value,
                        routeLaunchSequence = currentRouteLaunchSequence.value,
                        routeIntent = currentRouteIntent.value,
                        requestedCapabilities = currentRequestedCapabilities.value,
                        onRouteDispatched = currentOnRouteDispatched.value,
                        onOutcome = currentOnOutcome.value,
                        onProtocolError = currentOnProtocolError.value
                    )
                }
            },
            update = { host ->
                host.setRoute(
                    routeId = routeId,
                    routeLaunchSequence = routeLaunchSequence,
                    routeIntent = routeIntent,
                    requestedCapabilities = requestedCapabilities,
                    onRouteDispatched = currentOnRouteDispatched.value,
                    onOutcome = currentOnOutcome.value,
                    onProtocolError = currentOnProtocolError.value
                )
            },
            onRelease = { host ->
                host.destroyUnity()
                if (hostState.value === host) hostState.value = null
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
            }
        }
    }
}

internal interface UnityEmbeddedPlayer : UnityHostLifecycleRuntime<Configuration> {
    val view: View

    fun sendMessage(gameObject: String, method: String, payload: String): Boolean
}

internal fun interface UnityEmbeddedPlayerFactory {
    fun create(context: Context): UnityEmbeddedPlayer?
}

internal fun interface UnityComponentCallbackRegistrarFactory {
    fun create(context: Context): UnityHostCallbackRegistrar<ComponentCallbacks2>
}

internal data class UnityRuntimeHostDependencies(
    val ownershipRegistry: UnityRuntimeHostRegistry,
    val playerFactory: UnityEmbeddedPlayerFactory,
    val callbackRegistrarFactory: UnityComponentCallbackRegistrarFactory
) {
    companion object {
        val Production = UnityRuntimeHostDependencies(
            ownershipRegistry = UnityRuntimeHostOwnership.registry,
            playerFactory = UnityEmbeddedPlayerFactory(ReflectionUnityPlayer::create),
            callbackRegistrarFactory = UnityComponentCallbackRegistrarFactory(
                ::ApplicationComponentCallbackRegistrar
            )
        )
    }
}

private class ApplicationComponentCallbackRegistrar(
    context: Context
) : UnityHostCallbackRegistrar<ComponentCallbacks2> {
    private val application = context.applicationContext

    override fun register(callback: ComponentCallbacks2): Boolean {
        application.registerComponentCallbacks(callback)
        return true
    }

    override fun unregister(callback: ComponentCallbacks2): Boolean {
        application.unregisterComponentCallbacks(callback)
        return true
    }
}

internal class UnityRuntimeContainer internal constructor(
    context: Context,
    private val dependencies: UnityRuntimeHostDependencies
) : FrameLayout(context) {
    internal constructor(context: Context) : this(
        context,
        UnityRuntimeHostDependencies.Production
    )

    private val statusView = createStatusView(context)
    private val bridgeSession = UnityBridgeSession()
    private val callbackAdmission = UnityBridgeCallbackAdmissionGate()
    private var ownershipLease: UnityRuntimeHostLease? = null
    private var ownershipWaitToken: UnityRuntimeHostWaitToken? = null
    private var unityPlayer: UnityEmbeddedPlayer? = null
    private var lifecycleController: UnityHostLifecycleController<Configuration>? = null
    private var componentCallbackRegistration:
        UnityHostCallbackRegistration<ComponentCallbacks2>? = null
    private var callbackToken: UnityBridgeCallbackToken? = null
    private var lifecycleState = Lifecycle.State.INITIALIZED
    private var latestWindowFocus = false
    private var terminalRuntimeFailure: String? = null
    private var ownershipReleaseBlocked = false
    @Volatile
    private var destroyed = false
    private var activeLaunch: UnityRouteLaunch? = null
    private var activeRoutePayload: String? = null
    private var routeDispatchAttempted = false
    private var onRouteDispatched: () -> Unit = {}
    private var onOutcome: (UnityRouteOutcome) -> Unit = {}
    private var onProtocolError: (UnityBridgeProtocolError) -> Unit = {}

    init {
        setBackgroundColor(Color.BLACK)
        requestOwnership()
    }

    fun setRoute(
        routeId: String,
        routeLaunchSequence: Long,
        routeIntent: UnityRouteIntent,
        requestedCapabilities: List<String>,
        onRouteDispatched: () -> Unit,
        onOutcome: (UnityRouteOutcome) -> Unit,
        onProtocolError: (UnityBridgeProtocolError) -> Unit
    ): Boolean {
        this.onRouteDispatched = onRouteDispatched
        this.onOutcome = onOutcome
        this.onProtocolError = onProtocolError

        if (destroyed) return false
        terminalRuntimeFailure?.let {
            showStatus(it)
            return false
        }

        val launch = UnityRouteLaunch(
            routeId = routeId,
            launchSequence = routeLaunchSequence,
            intent = routeIntent,
            requestedCapabilities = requestedCapabilities.toList()
        )
        if (launch == activeLaunch) return false

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
        activeLaunch = launch
        activeRoutePayload = start.encodedPayload
        routeDispatchAttempted = false
        return dispatchActiveRoute()
    }

    fun resumeUnity() {
        if (destroyed) return
        lifecycleState = Lifecycle.State.RESUMED
        val controller = lifecycleController ?: return
        controller.resume()
        closeAfterLifecycleFailure(controller)
    }

    fun pauseUnity() {
        if (destroyed) return
        lifecycleState = Lifecycle.State.STARTED
        val controller = lifecycleController ?: return
        controller.pause()
        closeAfterLifecycleFailure(controller)
    }

    fun stopUnity() {
        if (destroyed) return
        lifecycleState = Lifecycle.State.CREATED
        val controller = lifecycleController ?: return
        controller.stop()
        closeAfterLifecycleFailure(controller)
    }

    fun synchronizeLifecycle(state: Lifecycle.State) {
        lifecycleState = state
        when {
            state == Lifecycle.State.DESTROYED -> destroyUnity()
            state.isAtLeast(Lifecycle.State.RESUMED) -> resumeUnity()
            else -> pauseUnity()
        }
    }

    fun destroyUnity() {
        if (destroyed) return
        destroyed = true
        callbackAdmission.close()
        bridgeSession.close()
        callbackToken?.let(UnityBridgeCallbacks::clear)
        callbackToken = null
        ownershipWaitToken?.let(dependencies.ownershipRegistry::cancel)
        ownershipWaitToken = null
        val componentCallbacksReleased = componentCallbackRegistration?.release() != false
        val controller = lifecycleController
        controller?.destroy()
        val player = unityPlayer
        val viewsReleased = runCatching { removeAllViews() }.isSuccess &&
            player?.view?.parent == null
        if (
            componentCallbacksReleased &&
            controller?.canReleaseOwnership() != false &&
            viewsReleased &&
            !ownershipReleaseBlocked
        ) {
            ownershipLease?.let(dependencies.ownershipRegistry::release)
            ownershipLease = null
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
        latestWindowFocus = hasWindowFocus
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

    internal fun statusTextForTesting(): String = statusView.text.toString()

    internal fun callbackAdmissionSnapshotForTesting() = callbackAdmission.snapshot()

    private fun requestOwnership() {
        when (
            val acquisition = dependencies.ownershipRegistry.acquireOrQueue(
                ::onOwnershipGranted
            )
        ) {
            is UnityRuntimeHostAcquisition.Acquired -> activateOwnedRuntime(acquisition.lease)
            is UnityRuntimeHostAcquisition.Waiting -> {
                ownershipWaitToken = acquisition.token
                showStatus("Unity runtime unavailable\nHost handoff pending")
            }

            UnityRuntimeHostAcquisition.CapacityReached -> {
                val message =
                    "Unity runtime unavailable\nHost handoff capacity reached"
                terminalRuntimeFailure = message
                showStatus(message)
            }
        }
    }

    private fun onOwnershipGranted(lease: UnityRuntimeHostLease) {
        if (Looper.myLooper() == Looper.getMainLooper()) {
            activateOwnedRuntime(lease)
            return
        }

        val accepted = runCatching { post { activateOwnedRuntime(lease) } }.getOrDefault(false)
        if (!accepted) {
            dependencies.ownershipRegistry.release(lease)
        }
    }

    private fun activateOwnedRuntime(lease: UnityRuntimeHostLease) {
        ownershipWaitToken = null
        if (destroyed) {
            dependencies.ownershipRegistry.release(lease)
            return
        }

        ownershipLease = lease
        val playerResult = runCatching { dependencies.playerFactory.create(context) }
        if (playerResult.isFailure) {
            val message = "Unity runtime unavailable\nHost activation failed"
            terminalRuntimeFailure = message
            ownershipReleaseBlocked = true
            callbackAdmission.close()
            bridgeSession.close()
            showStatus(message)
            return
        }
        val player = playerResult.getOrNull()
        if (player == null) {
            registerBridgeCallback()
            showStatus("Unity runtime unavailable")
            return
        }

        val controller = UnityHostLifecycleController(player)
        val callbacks = createComponentCallbacks()
        val registrar = runCatching {
            dependencies.callbackRegistrarFactory.create(context)
        }.getOrNull()
        if (registrar == null) {
            rollBackActivation(
                message = "Unity runtime unavailable\nLifecycle callback registration failed",
                player = player,
                controller = controller,
                callbackRegistration = null,
                lease = lease
            )
            return
        }
        val callbackRegistration = UnityHostCallbackRegistration(
            registrar,
            callbacks
        )
        if (!callbackRegistration.register()) {
            rollBackActivation(
                message = "Unity runtime unavailable\nLifecycle callback registration failed",
                player = player,
                controller = controller,
                callbackRegistration = callbackRegistration,
                lease = lease
            )
            return
        }

        val activated = runCatching {
            unityPlayer = player
            lifecycleController = controller
            componentCallbackRegistration = callbackRegistration
            registerBridgeCallback()
            addView(
                player.view,
                LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT)
            )
            hideStatus()
            applyRetainedHostState(controller)
            if (!destroyed) dispatchActiveRoute()
        }.isSuccess
        if (!activated && !destroyed) {
            rollBackActivation(
                message = "Unity runtime unavailable\nHost activation failed",
                player = player,
                controller = controller,
                callbackRegistration = callbackRegistration,
                lease = lease
            )
        }
    }

    private fun rollBackActivation(
        message: String,
        player: UnityEmbeddedPlayer,
        controller: UnityHostLifecycleController<Configuration>,
        callbackRegistration: UnityHostCallbackRegistration<ComponentCallbacks2>?,
        lease: UnityRuntimeHostLease
    ) {
        terminalRuntimeFailure = message
        callbackAdmission.close()
        bridgeSession.close()
        callbackToken?.let(UnityBridgeCallbacks::clear)
        callbackToken = null
        val callbacksReleased = callbackRegistration?.release() != false
        controller.destroy()
        (player.view.parent as? ViewGroup)?.let { parent ->
            runCatching { parent.removeView(player.view) }
        }
        unityPlayer = null
        lifecycleController = null
        componentCallbackRegistration = null
        val viewDetached = player.view.parent == null
        if (callbacksReleased && controller.canReleaseOwnership() && viewDetached) {
            if (dependencies.ownershipRegistry.release(lease)) {
                ownershipLease = null
            } else {
                ownershipReleaseBlocked = true
            }
        } else {
            ownershipReleaseBlocked = true
        }
        showStatus(message)
    }

    private fun registerBridgeCallback() {
        if (callbackToken != null || destroyed) return
        callbackToken = UnityBridgeCallbacks.register(::enqueueOutcome)
    }

    private fun createComponentCallbacks() = object : ComponentCallbacks2 {
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

    private fun applyRetainedHostState(
        controller: UnityHostLifecycleController<Configuration>
    ) {
        controller.onWindowFocusChanged(latestWindowFocus)
        when {
            lifecycleState == Lifecycle.State.DESTROYED -> destroyUnity()
            lifecycleState.isAtLeast(Lifecycle.State.RESUMED) -> controller.resume()
            else -> controller.pause()
        }
        closeAfterLifecycleFailure(controller)
    }

    private fun dispatchActiveRoute(): Boolean {
        if (destroyed || terminalRuntimeFailure != null || routeDispatchAttempted) return false
        val payload = activeRoutePayload ?: return false
        val player = unityPlayer
        if (player == null) {
            val routeId = activeLaunch?.routeId.orEmpty()
            val status = if (ownershipWaitToken != null) {
                "Unity runtime unavailable\nHost handoff pending"
            } else {
                "Unity runtime unavailable\nRoute: $routeId"
            }
            showStatus(status)
            return false
        }

        routeDispatchAttempted = true
        if (!player.sendMessage("AndroidBridge", "SetRouteContext", payload)) {
            showProtocolError(
                UnityBridgeProtocolError(UnityBridgeProtocolErrorCode.SendUnavailable)
            )
            return false
        }

        hideStatus()
        runCatching(onRouteDispatched)
        return true
    }

    // Admission and posting share this monitor so the one overflow sentinel cannot overtake work
    // admitted before it when multiple JNI producer threads report concurrently.
    @Synchronized
    private fun enqueueOutcome(rawJson: String?) {
        when (val admission = callbackAdmission.tryAdmit(rawJson)) {
            is UnityBridgeCallbackAdmission.Payload -> postAdmission(admission) {
                handleOutcome(admission.rawJson)
            }

            is UnityBridgeCallbackAdmission.ProtocolError -> postAdmission(admission) {
                showProtocolError(admission.error)
            }

            UnityBridgeCallbackAdmission.Overflow -> postAdmission(admission) {
                bridgeSession.close()
                showProtocolError(
                    UnityBridgeProtocolError(UnityBridgeProtocolErrorCode.SessionClosed)
                )
            }

            UnityBridgeCallbackAdmission.Dropped -> Unit
        }
    }

    private fun postAdmission(
        admission: UnityBridgeCallbackAdmission,
        action: () -> Unit
    ) {
        val accepted = runCatching {
            post {
                try {
                    if (!destroyed && terminalRuntimeFailure == null) action()
                } finally {
                    callbackAdmission.complete(admission)
                }
            }
        }.getOrDefault(false)
        if (!accepted) callbackAdmission.complete(admission)
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
) : UnityEmbeddedPlayer {

    override val view: View = instance as View

    override fun resume() = resumeMethod.invokeSafely(instance)

    override fun pause() = pauseMethod.invokeSafely(instance)

    override fun destroy() = destroyMethod.invokeSafely(instance)

    override fun windowFocusChanged(hasFocus: Boolean) =
        windowFocusChangedMethod.invokeSafely(instance, hasFocus)

    override fun lowMemory() = lowMemoryMethod.invokeSafely(instance)

    override fun configurationChanged(configuration: Configuration) =
        configurationChangedMethod.invokeSafely(instance, configuration)

    override fun sendMessage(gameObject: String, method: String, payload: String): Boolean {
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
