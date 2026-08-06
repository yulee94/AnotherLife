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
    private val grantedLeaseHandoff = UnityRuntimeGrantedLeaseHandoff()
    private val activationLeaseState = UnityRuntimeActivationLeaseState()
    private val callbackDispatcher = UnityBridgeCallbackDispatcher(
        postToMain = { action -> post(action) },
        onPayload = ::handleOutcome,
        onProtocolError = ::showProtocolError,
        onOverflow = {
            bridgeSession.close()
            showProtocolError(
                UnityBridgeProtocolError(UnityBridgeProtocolErrorCode.SessionClosed)
            )
        }
    )
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
    private val destroyed: Boolean
        get() = activationLeaseState.isClosed()
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
        val closeDecision = activationLeaseState.close() ?: return
        callbackDispatcher.close()
        bridgeSession.close()
        grantedLeaseHandoff.close()?.let(dependencies.ownershipRegistry::release)
        ownershipWaitToken?.let(dependencies.ownershipRegistry::cancel)
        ownershipWaitToken = null

        // An activation permit owns all partial resources until its next close checkpoint. It
        // performs the only cleanup and cannot return the lease until that cleanup is proven.
        if (closeDecision is UnityRuntimeActivationCloseDecision.ActivationWillClean) return

        closeDecision as UnityRuntimeActivationCloseDecision.DestroyWillClean
        callbackToken?.let(UnityBridgeCallbacks::clear)
        callbackToken = null
        val componentCallbacksReleased = componentCallbackRegistration?.release() != false
        val controller = lifecycleController
        controller?.destroy()
        val player = unityPlayer
        val playerView = runCatching { player?.view }.getOrNull()
        val viewsReleased = runCatching { removeAllViews() }.isSuccess &&
            (player == null || (playerView != null && playerView.parent == null))
        if (
            componentCallbacksReleased &&
            controller?.canReleaseOwnership() != false &&
            viewsReleased &&
            !ownershipReleaseBlocked
        ) {
            closeDecision.lease?.let(dependencies.ownershipRegistry::release)
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

    internal fun callbackAdmissionSnapshotForTesting() = callbackDispatcher.snapshot()

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
        if (!grantedLeaseHandoff.retain(lease)) {
            dependencies.ownershipRegistry.release(lease)
            return
        }
        if (Looper.myLooper() == Looper.getMainLooper()) {
            activateGrantedLease(lease)
            return
        }

        val accepted = runCatching { post { activateGrantedLease(lease) } }.getOrDefault(false)
        if (!accepted) {
            grantedLeaseHandoff.withdraw(lease)?.let(dependencies.ownershipRegistry::release)
        }
    }

    private fun activateGrantedLease(lease: UnityRuntimeHostLease) {
        if (!grantedLeaseHandoff.claim(lease)) return
        if (destroyed) {
            dependencies.ownershipRegistry.release(lease)
            return
        }
        activateOwnedRuntime(lease)
    }

    private fun activateOwnedRuntime(lease: UnityRuntimeHostLease) {
        ownershipWaitToken = null
        val permit = activationLeaseState.begin(lease)
        if (permit == null) {
            dependencies.ownershipRegistry.release(lease)
            return
        }
        var player: UnityEmbeddedPlayer? = null
        var controller: UnityHostLifecycleController<Configuration>? = null
        var callbackRegistration: UnityHostCallbackRegistration<ComponentCallbacks2>? = null
        var activationCallbackToken: UnityBridgeCallbackToken? = null
        var playerView: View? = null
        var playerViewObserved = false

        fun abortActivation(
            message: String?,
            cleanupUncertain: Boolean = false
        ) {
            finishActivationCleanup(
                permit = permit,
                message = message,
                player = player,
                controller = controller,
                callbackRegistration = callbackRegistration,
                activationCallbackToken = activationCallbackToken,
                playerView = playerView,
                playerViewObserved = playerViewObserved,
                cleanupUncertain = cleanupUncertain
            )
        }

        if (!activationLeaseState.canContinue(permit)) {
            abortActivation(message = null)
            return
        }

        val playerResult = runCatching { dependencies.playerFactory.create(context) }
        if (playerResult.isFailure) {
            // A throwing constructor may have initialized native state without returning a handle.
            abortActivation(
                message = "Unity runtime unavailable\nHost activation failed",
                cleanupUncertain = true
            )
            return
        }
        player = playerResult.getOrNull()

        if (player == null) {
            if (!activationLeaseState.canContinue(permit)) {
                abortActivation(message = null)
                return
            }
            activationCallbackToken = UnityBridgeCallbacks.register(callbackDispatcher::enqueue)
            callbackToken = activationCallbackToken
            if (!activationLeaseState.canContinue(permit)) {
                abortActivation(message = null)
                return
            }
            val statusShown = runCatching { showStatus("Unity runtime unavailable") }.isSuccess
            if (!statusShown || !activationLeaseState.canContinue(permit)) {
                abortActivation(
                    message = if (statusShown) null else {
                        "Unity runtime unavailable\nHost activation failed"
                    }
                )
                return
            }
            if (!activationLeaseState.complete(permit)) abortActivation(message = null)
            return
        }

        controller = UnityHostLifecycleController(player!!)
        // Observe the exact view immediately after construction. Registrar failure remains
        // recoverable only when teardown can prove this view detached; a throwing getter is an
        // uncertain native activation and deliberately retains the lease.
        val playerViewResult = runCatching { player!!.view }
        if (playerViewResult.isFailure) {
            abortActivation(
                message = "Unity runtime unavailable\nHost activation failed",
                cleanupUncertain = true
            )
            return
        }
        playerView = playerViewResult.getOrNull()
        playerViewObserved = playerView != null
        if (!playerViewObserved || !activationLeaseState.canContinue(permit)) {
            abortActivation(
                message = if (playerViewObserved) null else {
                    "Unity runtime unavailable\nHost activation failed"
                },
                cleanupUncertain = !playerViewObserved
            )
            return
        }

        val callbacks = createComponentCallbacks()
        val registrarResult = runCatching {
            dependencies.callbackRegistrarFactory.create(context)
        }
        if (registrarResult.isFailure) {
            abortActivation(
                message = "Unity runtime unavailable\nLifecycle callback registration failed"
            )
            return
        }
        if (!activationLeaseState.canContinue(permit)) {
            abortActivation(message = null)
            return
        }
        val registrar = registrarResult.getOrNull()
        if (registrar == null) {
            abortActivation(
                message = "Unity runtime unavailable\nLifecycle callback registration failed"
            )
            return
        }

        callbackRegistration = UnityHostCallbackRegistration(registrar, callbacks)
        val callbacksRegistered = callbackRegistration!!.register()
        if (!activationLeaseState.canContinue(permit)) {
            abortActivation(message = null)
            return
        }
        if (!callbacksRegistered) {
            abortActivation(
                message = "Unity runtime unavailable\nLifecycle callback registration failed"
            )
            return
        }

        activationCallbackToken = UnityBridgeCallbacks.register(callbackDispatcher::enqueue)
        callbackToken = activationCallbackToken
        if (!activationLeaseState.canContinue(permit)) {
            abortActivation(message = null)
            return
        }

        unityPlayer = player
        lifecycleController = controller
        componentCallbackRegistration = callbackRegistration
        if (!activationLeaseState.canContinue(permit)) {
            abortActivation(message = null)
            return
        }

        val attached = runCatching {
            addView(
                playerView!!,
                LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT)
            )
        }.isSuccess
        if (!attached || !activationLeaseState.canContinue(permit)) {
            abortActivation(
                message = if (attached) null else {
                    "Unity runtime unavailable\nHost activation failed"
                }
            )
            return
        }

        val statusHidden = runCatching { hideStatus() }.isSuccess
        if (!statusHidden || !activationLeaseState.canContinue(permit)) {
            abortActivation(
                message = if (statusHidden) null else {
                    "Unity runtime unavailable\nHost activation failed"
                }
            )
            return
        }

        val retainedStateApplied = runCatching {
            applyRetainedHostState(controller!!, permit)
        }.getOrDefault(false)
        if (!retainedStateApplied || !activationLeaseState.canContinue(permit)) {
            abortActivation(
                message = if (destroyed) null else {
                    "Unity runtime unavailable\nHost activation failed"
                }
            )
            return
        }

        val routeDispatchCompleted = runCatching {
            dispatchActiveRoute(permit)
        }.isSuccess
        if (!routeDispatchCompleted || !activationLeaseState.canContinue(permit)) {
            abortActivation(
                message = if (destroyed) null else {
                    "Unity runtime unavailable\nHost activation failed"
                }
            )
            return
        }

        // The success transition is deliberately last. If close wins this race, activation still
        // owns all resources and performs the only cleanup/release path.
        if (!activationLeaseState.complete(permit)) abortActivation(message = null)
    }

    private fun finishActivationCleanup(
        permit: UnityRuntimeActivationPermit,
        message: String?,
        player: UnityEmbeddedPlayer?,
        controller: UnityHostLifecycleController<Configuration>?,
        callbackRegistration: UnityHostCallbackRegistration<ComponentCallbacks2>?,
        activationCallbackToken: UnityBridgeCallbackToken?,
        playerView: View?,
        playerViewObserved: Boolean,
        cleanupUncertain: Boolean
    ) {
        if (message != null) terminalRuntimeFailure = message
        callbackDispatcher.close()
        bridgeSession.close()
        activationCallbackToken?.let(UnityBridgeCallbacks::clear)
        if (callbackToken == activationCallbackToken) callbackToken = null

        val callbacksReleased = callbackRegistration?.release() != false
        controller?.destroy()
        val exactPlayerViewRemoved = if (playerView == null) {
            player == null
        } else {
            runCatching {
                (playerView.parent as? ViewGroup)?.removeView(playerView)
            }.isSuccess
        }
        val ownedViewsRemoved = runCatching { removeAllViews() }.isSuccess
        val viewDetached = when {
            player == null -> true
            !playerViewObserved || playerView == null -> false
            else -> playerView.parent == null
        }

        if (unityPlayer === player) unityPlayer = null
        if (lifecycleController === controller) lifecycleController = null
        if (componentCallbackRegistration === callbackRegistration) {
            componentCallbackRegistration = null
        }

        val cleanupProven = !cleanupUncertain &&
            callbacksReleased &&
            (player == null || controller?.canReleaseOwnership() == true) &&
            exactPlayerViewRemoved &&
            ownedViewsRemoved &&
            viewDetached
        val releasableLease = activationLeaseState.finishCleanup(permit, cleanupProven)
        if (cleanupProven && releasableLease != null) {
            if (!dependencies.ownershipRegistry.release(releasableLease)) {
                ownershipReleaseBlocked = true
            }
        } else if (!cleanupProven) {
            ownershipReleaseBlocked = true
        }

        if (!destroyed && message != null) runCatching { showStatus(message) }
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
        controller: UnityHostLifecycleController<Configuration>,
        permit: UnityRuntimeActivationPermit
    ): Boolean {
        controller.onWindowFocusChanged(latestWindowFocus)
        if (!activationLeaseState.canContinue(permit)) return false
        when {
            lifecycleState == Lifecycle.State.DESTROYED -> {
                destroyUnity()
                return false
            }
            lifecycleState.isAtLeast(Lifecycle.State.RESUMED) -> controller.resume()
            else -> controller.pause()
        }
        if (!activationLeaseState.canContinue(permit)) return false
        if (controller.isDestroyed()) {
            destroyUnity()
            return false
        }
        return activationLeaseState.canContinue(permit)
    }

    private fun dispatchActiveRoute(
        activationPermit: UnityRuntimeActivationPermit? = null
    ): Boolean {
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
            if (!canContinueDispatch(activationPermit)) return false
            showProtocolError(
                UnityBridgeProtocolError(UnityBridgeProtocolErrorCode.SendUnavailable)
            )
            return false
        }
        if (!canContinueDispatch(activationPermit)) return false

        hideStatus()
        if (!canContinueDispatch(activationPermit)) return false
        runCatching(onRouteDispatched)
        return canContinueDispatch(activationPermit)
    }

    private fun canContinueDispatch(permit: UnityRuntimeActivationPermit?): Boolean =
        if (permit == null) !destroyed else activationLeaseState.canContinue(permit)

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

internal class ReflectionUnityPlayer private constructor(
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
            val playerClass = try {
                Class.forName(
                    "com.unity3d.player.UnityPlayer",
                    false,
                    context.classLoader ?: ReflectionUnityPlayer::class.java.classLoader
                )
            } catch (_: ClassNotFoundException) {
                return null
            }
            return createFromResolvedClass(context, playerClass)
        }

        internal fun createFromResolvedClass(
            context: Context,
            playerClass: Class<*>
        ): ReflectionUnityPlayer? {
            if (!View::class.java.isAssignableFrom(playerClass)) return null
            val constructor = playerClass.constructors
                .filter { candidate ->
                    candidate.parameterTypes.size == 1 &&
                        candidate.parameterTypes[0].isAssignableFrom(context.javaClass)
                }
                .minByOrNull { candidate ->
                    if (candidate.parameterTypes[0] == Context::class.java) 0 else 1
                } ?: return null
            val resumeMethod = playerClass.instanceVoidNoArgMethod("resume") ?: return null
            val pauseMethod = playerClass.instanceVoidNoArgMethod("pause") ?: return null
            val destroyMethod = playerClass.instanceVoidNoArgMethod("destroy") ?: return null
            val lowMemoryMethod = playerClass.instanceVoidNoArgMethod("lowMemory") ?: return null
            val windowFocusChangedMethod = playerClass.methods.firstOrNull { method ->
                method.isInstanceVoidMethod(
                    "windowFocusChanged",
                    arrayOf(java.lang.Boolean.TYPE)
                )
            } ?: return null
            val configurationChangedMethod = playerClass.methods.firstOrNull { method ->
                method.isInstanceVoidMethod(
                    "configurationChanged",
                    arrayOf(Configuration::class.java)
                )
            } ?: return null
            val sendMessageMethod = playerClass.methods.firstOrNull { method ->
                method.name == "UnitySendMessage" &&
                    ReflectionModifier.isStatic(method.modifiers) &&
                    method.returnType == java.lang.Void.TYPE &&
                    method.parameterTypes.contentEquals(
                        arrayOf(String::class.java, String::class.java, String::class.java)
                    )
            } ?: return null
            val instance = constructor.newInstance(context)

            if (instance !is View) return null
            return ReflectionUnityPlayer(
                instance = instance,
                resumeMethod = resumeMethod,
                pauseMethod = pauseMethod,
                destroyMethod = destroyMethod,
                windowFocusChangedMethod = windowFocusChangedMethod,
                lowMemoryMethod = lowMemoryMethod,
                configurationChangedMethod = configurationChangedMethod,
                sendMessageMethod = sendMessageMethod
            )
        }
    }
}

private fun Method.invokeSafely(instance: Any, vararg arguments: Any?): Boolean {
    return runCatching {
        invoke(instance, *arguments)
        true
    }.getOrDefault(false)
}

private fun Class<*>.instanceVoidNoArgMethod(name: String): Method? {
    return methods.firstOrNull { method ->
        method.isInstanceVoidMethod(name, emptyArray())
    }
}

private fun Method.isInstanceVoidMethod(
    expectedName: String,
    expectedParameters: Array<Class<*>>
): Boolean =
    name == expectedName &&
        !ReflectionModifier.isStatic(modifiers) &&
        returnType == java.lang.Void.TYPE &&
        parameterTypes.contentEquals(expectedParameters)

private data class UnityRouteLaunch(
    val routeId: String,
    val launchSequence: Long,
    val intent: UnityRouteIntent,
    val requestedCapabilities: List<String>
)
