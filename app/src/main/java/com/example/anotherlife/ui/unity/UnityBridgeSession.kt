package com.example.anotherlife.ui.unity

import androidx.annotation.Keep
import java.util.UUID

internal class UnityBridgeSession(
    private val requestIdFactory: () -> String = { UUID.randomUUID().toString() }
) {
    private var activeRequest: UnityRouteRequest? = null
    private var completedRequestId: String? = null
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
        completedRequestId = null
        return UnityBridgeSessionStart.Started(request, payload)
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
        completedRequestId = null
    }
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

internal data class UnityBridgeCallbackToken(val value: Long)

internal class UnityBridgeCallbackRegistry {
    private var nextToken = 0L
    private var activeRegistration: Pair<UnityBridgeCallbackToken, (String?) -> Unit>? = null

    @Synchronized
    fun register(callback: (String?) -> Unit): UnityBridgeCallbackToken {
        val token = UnityBridgeCallbackToken(++nextToken)
        activeRegistration = token to callback
        return token
    }

    @Synchronized
    fun clear(token: UnityBridgeCallbackToken) {
        if (activeRegistration?.first == token) {
            activeRegistration = null
        }
    }

    fun report(rawJson: String?) {
        val callback = synchronized(this) { activeRegistration?.second }
        runCatching { callback?.invoke(rawJson) }
    }
}

@Keep
object UnityBridgeCallbacks {
    private val registry = UnityBridgeCallbackRegistry()

    internal fun register(callback: (String?) -> Unit): UnityBridgeCallbackToken {
        return registry.register(callback)
    }

    internal fun clear(token: UnityBridgeCallbackToken) {
        registry.clear(token)
    }

    @Keep
    @JvmStatic
    fun reportOutcome(rawJson: String?) {
        registry.report(rawJson)
    }
}

private fun rejectedDelivery(
    code: UnityBridgeProtocolErrorCode,
    field: String? = null
): UnityBridgeSessionDelivery {
    return UnityBridgeSessionDelivery.Rejected(UnityBridgeProtocolError(code, field))
}
