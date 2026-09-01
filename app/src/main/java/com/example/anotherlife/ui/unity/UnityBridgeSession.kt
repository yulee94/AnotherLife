package com.example.anotherlife.ui.unity

import androidx.annotation.Keep
import java.util.UUID

internal class UnityBridgeSession(
    private val requestIdFactory: () -> String = { UUID.randomUUID().toString() }
) {
    private var activeRequest: UnityRouteRequest? = null
    private var readyRequestId: String? = null
    private var completedRequestId: String? = null
    private var timedOutRequestId: String? = null
    private var closed = false

    @Synchronized
    fun startRoute(
        routeId: String,
        intent: UnityRouteIntent,
        requestedCapabilities: List<String> = emptyList()
    ): UnityBridgeSessionStart {
        if (closed) {
            return UnityBridgeSessionStart.Rejected(
                UnityBridgeProtocolError(UnityBridgeProtocolErrorCode.SessionClosed)
            )
        }

        val requestId = runCatching(requestIdFactory).getOrNull()
            ?: return UnityBridgeSessionStart.Rejected(
                UnityBridgeProtocolError(
                    UnityBridgeProtocolErrorCode.InvalidRequestId,
                    "requestId"
                )
            )
        val request = when (
            val result = UnityBridgeContract.createRequest(
                requestId = requestId,
                routeId = routeId,
                intent = intent,
                requestedCapabilities = requestedCapabilities
            )
        ) {
            is UnityBridgeContractResult.Accepted -> result.value
            is UnityBridgeContractResult.Rejected -> {
                return UnityBridgeSessionStart.Rejected(result.error)
            }
        }
        val payload = when (val result = UnityBridgeContract.encodeRequest(request)) {
            is UnityBridgeContractResult.Accepted -> result.value
            is UnityBridgeContractResult.Rejected -> {
                return UnityBridgeSessionStart.Rejected(result.error)
            }
        }

        activeRequest = request
        readyRequestId = null
        completedRequestId = null
        timedOutRequestId = null
        return UnityBridgeSessionStart.Started(request, payload)
    }

    @Synchronized
    fun consumeReady(rawJson: String?): UnityBridgeSessionReadyDelivery {
        if (closed) {
            return rejectedReadyDelivery(UnityBridgeProtocolErrorCode.SessionClosed)
        }
        val request = activeRequest
            ?: return rejectedReadyDelivery(UnityBridgeProtocolErrorCode.NoActiveRequest)
        val ready = when (val result = UnityBridgeContract.parseReady(rawJson)) {
            is UnityBridgeContractResult.Accepted -> result.value
            is UnityBridgeContractResult.Rejected -> {
                return UnityBridgeSessionReadyDelivery.Rejected(result.error)
            }
        }

        if (ready.requestId != request.requestId) {
            return rejectedReadyDelivery(
                UnityBridgeProtocolErrorCode.RequestMismatch,
                "requestId"
            )
        }
        if (ready.routeId != request.routeId) {
            return rejectedReadyDelivery(UnityBridgeProtocolErrorCode.RouteMismatch, "routeId")
        }
        if (completedRequestId == request.requestId) {
            return rejectedReadyDelivery(UnityBridgeProtocolErrorCode.ReadyAfterOutcome)
        }
        if (timedOutRequestId == request.requestId) {
            return rejectedReadyDelivery(UnityBridgeProtocolErrorCode.ReadyAfterTimeout)
        }
        if (readyRequestId == request.requestId) {
            return rejectedReadyDelivery(UnityBridgeProtocolErrorCode.DuplicateReady)
        }

        readyRequestId = request.requestId
        return UnityBridgeSessionReadyDelivery.Delivered(ready)
    }

    @Synchronized
    fun expireReady(requestId: String, routeId: String): UnityBridgeSessionReadyTimeout {
        if (closed) {
            return rejectedReadyTimeout(UnityBridgeProtocolErrorCode.SessionClosed)
        }
        val request = activeRequest
            ?: return rejectedReadyTimeout(UnityBridgeProtocolErrorCode.NoActiveRequest)
        if (requestId != request.requestId) {
            return rejectedReadyTimeout(
                UnityBridgeProtocolErrorCode.RequestMismatch,
                "requestId"
            )
        }
        if (routeId != request.routeId) {
            return rejectedReadyTimeout(UnityBridgeProtocolErrorCode.RouteMismatch, "routeId")
        }
        if (completedRequestId == request.requestId) {
            return rejectedReadyTimeout(UnityBridgeProtocolErrorCode.ReadyAfterOutcome)
        }
        if (timedOutRequestId == request.requestId) {
            return rejectedReadyTimeout(UnityBridgeProtocolErrorCode.ReadyAfterTimeout)
        }
        if (readyRequestId == request.requestId) {
            return rejectedReadyTimeout(UnityBridgeProtocolErrorCode.DuplicateReady)
        }

        timedOutRequestId = request.requestId
        return UnityBridgeSessionReadyTimeout.Expired(request)
    }

    @Synchronized
    fun consumeOutcome(rawJson: String?): UnityBridgeSessionDelivery {
        if (closed) {
            return rejectedDelivery(UnityBridgeProtocolErrorCode.SessionClosed)
        }
        val request = activeRequest
            ?: return rejectedDelivery(UnityBridgeProtocolErrorCode.NoActiveRequest)
        val outcome = when (val result = UnityBridgeContract.parseOutcome(rawJson)) {
            is UnityBridgeContractResult.Accepted -> result.value
            is UnityBridgeContractResult.Rejected -> {
                return UnityBridgeSessionDelivery.Rejected(result.error)
            }
        }

        if (outcome.requestId != request.requestId) {
            return rejectedDelivery(
                UnityBridgeProtocolErrorCode.RequestMismatch,
                "requestId"
            )
        }
        if (outcome.routeId != request.routeId) {
            return rejectedDelivery(UnityBridgeProtocolErrorCode.RouteMismatch, "routeId")
        }
        if (
            request.intent == UnityRouteIntent.Authoritative &&
            outcome.status == UnityRouteOutcomeStatus.Success &&
            outcome.resultId == null
        ) {
            return rejectedDelivery(
                UnityBridgeProtocolErrorCode.MissingResultId,
                "resultId"
            )
        }
        if (completedRequestId == request.requestId) {
            return rejectedDelivery(UnityBridgeProtocolErrorCode.DuplicateOutcome)
        }

        completedRequestId = request.requestId
        return UnityBridgeSessionDelivery.Delivered(outcome)
    }

    @Synchronized
    fun close() {
        closed = true
        activeRequest = null
        readyRequestId = null
        completedRequestId = null
        timedOutRequestId = null
    }

    @Synchronized
    internal fun activeRequestForTesting(): UnityRouteRequest? = activeRequest
}

internal sealed interface UnityBridgeSessionStart {
    data class Started(
        val request: UnityRouteRequest,
        val encodedPayload: String
    ) : UnityBridgeSessionStart

    data class Rejected(val error: UnityBridgeProtocolError) : UnityBridgeSessionStart
}

internal sealed interface UnityBridgeSessionDelivery {
    data class Delivered(val outcome: UnityRouteOutcome) : UnityBridgeSessionDelivery
    data class Rejected(val error: UnityBridgeProtocolError) : UnityBridgeSessionDelivery
}

internal sealed interface UnityBridgeSessionReadyDelivery {
    data class Delivered(val ready: UnityRouteReady) : UnityBridgeSessionReadyDelivery
    data class Rejected(val error: UnityBridgeProtocolError) : UnityBridgeSessionReadyDelivery
}

internal sealed interface UnityBridgeSessionReadyTimeout {
    data class Expired(val request: UnityRouteRequest) : UnityBridgeSessionReadyTimeout
    data class Rejected(val error: UnityBridgeProtocolError) : UnityBridgeSessionReadyTimeout
}

internal data class UnityBridgeCallbackToken(val value: Long)

internal class UnityBridgeCallbackRegistry {
    private var nextToken = 0L
    private var activeRegistration: UnityBridgeCallbackRegistration? = null

    @Synchronized
    fun register(outcomeCallback: (String?) -> Unit): UnityBridgeCallbackToken {
        return register(outcomeCallback, {})
    }

    @Synchronized
    fun register(
        outcomeCallback: (String?) -> Unit,
        readyCallback: (String?) -> Unit
    ): UnityBridgeCallbackToken {
        val token = UnityBridgeCallbackToken(++nextToken)
        activeRegistration = UnityBridgeCallbackRegistration(
            token = token,
            outcomeCallback = outcomeCallback,
            readyCallback = readyCallback
        )
        return token
    }

    @Synchronized
    fun clear(token: UnityBridgeCallbackToken) {
        if (activeRegistration?.token == token) {
            activeRegistration = null
        }
    }

    fun reportOutcome(rawJson: String?) {
        val callback = synchronized(this) { activeRegistration?.outcomeCallback }
        runCatching { callback?.invoke(rawJson) }
    }

    fun reportReady(rawJson: String?) {
        val callback = synchronized(this) { activeRegistration?.readyCallback }
        runCatching { callback?.invoke(rawJson) }
    }

    @Synchronized
    fun hasActiveRegistration(): Boolean = activeRegistration != null
}

private data class UnityBridgeCallbackRegistration(
    val token: UnityBridgeCallbackToken,
    val outcomeCallback: (String?) -> Unit,
    val readyCallback: (String?) -> Unit
)

@Keep
object UnityBridgeCallbacks {
    private val registry = UnityBridgeCallbackRegistry()

    internal fun register(outcomeCallback: (String?) -> Unit): UnityBridgeCallbackToken {
        return registry.register(outcomeCallback)
    }

    internal fun register(
        outcomeCallback: (String?) -> Unit,
        readyCallback: (String?) -> Unit
    ): UnityBridgeCallbackToken {
        return registry.register(outcomeCallback, readyCallback)
    }

    internal fun clear(token: UnityBridgeCallbackToken) {
        registry.clear(token)
    }

    internal fun hasActiveRegistrationForTesting(): Boolean =
        registry.hasActiveRegistration()

    @Keep
    @JvmStatic
    fun reportOutcome(rawJson: String?) {
        registry.reportOutcome(rawJson)
    }

    @Keep
    @JvmStatic
    fun reportReady(rawJson: String?) {
        registry.reportReady(rawJson)
    }
}

private fun rejectedReadyDelivery(
    code: UnityBridgeProtocolErrorCode,
    field: String? = null
): UnityBridgeSessionReadyDelivery {
    return UnityBridgeSessionReadyDelivery.Rejected(UnityBridgeProtocolError(code, field))
}

private fun rejectedReadyTimeout(
    code: UnityBridgeProtocolErrorCode,
    field: String? = null
): UnityBridgeSessionReadyTimeout {
    return UnityBridgeSessionReadyTimeout.Rejected(UnityBridgeProtocolError(code, field))
}

private fun rejectedDelivery(
    code: UnityBridgeProtocolErrorCode,
    field: String? = null
): UnityBridgeSessionDelivery {
    return UnityBridgeSessionDelivery.Rejected(UnityBridgeProtocolError(code, field))
}
