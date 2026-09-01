package com.example.anotherlife.ui.unity

import kotlinx.serialization.KSerializer
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.descriptors.buildClassSerialDescriptor
import kotlinx.serialization.descriptors.element
import kotlinx.serialization.encoding.CompositeDecoder
import kotlinx.serialization.encoding.Decoder
import kotlinx.serialization.encoding.Encoder
import kotlinx.serialization.encoding.decodeStructure
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.intOrNull
import kotlinx.serialization.json.put

const val UNITY_BRIDGE_CONTRACT_VERSION = 2
const val MAX_UNITY_BRIDGE_MESSAGE_BYTES = 32 * 1024
const val MAX_UNITY_BRIDGE_PAYLOAD_BYTES = 16 * 1024

private const val MAX_ROUTE_ID_LENGTH = 64
private const val MAX_REQUEST_ID_LENGTH = 128
private const val MAX_RESULT_ID_LENGTH = 128
private const val MAX_DIAGNOSTIC_CODE_LENGTH = 64
private const val MAX_CAPABILITIES = 16

private val bridgeJson = Json { isLenient = false }
private val routeIdPattern = Regex("""[A-Za-z][A-Za-z0-9._-]*""")
private val correlationIdPattern = Regex("""[A-Za-z0-9][A-Za-z0-9._:-]*""")
private val diagnosticCodePattern = Regex("""[a-z][a-z0-9]*(?:[._-][a-z0-9]+)*""")

data class UnityRouteRequest(
    val contractVersion: Int,
    val requestId: String,
    val routeId: String,
    val intent: UnityRouteIntent,
    val requestedCapabilities: List<String>
)

enum class UnityRouteIntent(val wireValue: String) {
    Preview("preview"),
    Authoritative("authoritative");

    companion object {
        fun fromWireValue(value: String): UnityRouteIntent? {
            return values().firstOrNull { it.wireValue == value }
        }
    }
}

data class UnityRouteOutcome(
    val contractVersion: Int,
    val requestId: String,
    val routeId: String,
    val status: UnityRouteOutcomeStatus,
    val diagnosticCode: String?,
    val resultId: String?,
    val payload: String?
)

data class UnityRouteReady(
    val contractVersion: Int,
    val requestId: String,
    val routeId: String
)

enum class UnityRouteOutcomeStatus(val wireValue: String) {
    Success("success"),
    Failure("failure"),
    Cancelled("cancelled"),
    Unavailable("unavailable");

    companion object {
        fun fromWireValue(value: String): UnityRouteOutcomeStatus? {
            return values().firstOrNull { it.wireValue == value }
        }
    }
}

data class UnityBridgeProtocolError(
    val code: UnityBridgeProtocolErrorCode,
    val field: String? = null
)

enum class UnityBridgeProtocolErrorCode(val wireValue: String) {
    NullMessage("bridge.null_message"),
    EmptyMessage("bridge.empty_message"),
    MessageTooLarge("bridge.message_too_large"),
    MalformedJson("bridge.malformed_json"),
    DuplicateField("bridge.duplicate_field"),
    UnexpectedField("bridge.unexpected_field"),
    MissingField("bridge.missing_field"),
    InvalidContractVersion("bridge.invalid_contract_version"),
    InvalidRequestId("bridge.invalid_request_id"),
    InvalidRouteId("bridge.invalid_route_id"),
    InvalidIntent("bridge.invalid_intent"),
    TooManyCapabilities("bridge.too_many_capabilities"),
    InvalidCapability("bridge.invalid_capability"),
    DuplicateCapability("bridge.duplicate_capability"),
    InvalidStatus("bridge.invalid_status"),
    InvalidDiagnosticCode("bridge.invalid_diagnostic_code"),
    InvalidResultId("bridge.invalid_result_id"),
    MissingResultId("bridge.missing_result_id"),
    PayloadTooLarge("bridge.payload_too_large"),
    MissingDiagnosticCode("bridge.missing_diagnostic_code"),
    NoActiveRequest("bridge.no_active_request"),
    RequestMismatch("bridge.request_mismatch"),
    RouteMismatch("bridge.route_mismatch"),
    DuplicateReady("bridge.duplicate_ready"),
    ReadyAfterOutcome("bridge.ready_after_outcome"),
    DuplicateOutcome("bridge.duplicate_outcome"),
    SessionClosed("bridge.session_closed"),
    SendUnavailable("bridge.send_unavailable")
}

sealed interface UnityBridgeContractResult<out T> {
    data class Accepted<T>(val value: T) : UnityBridgeContractResult<T>
    data class Rejected(val error: UnityBridgeProtocolError) : UnityBridgeContractResult<Nothing>
}

object UnityBridgeContract {
    private val requestMemberNames = listOf(
        "contractVersion",
        "requestId",
        "routeId",
        "intent",
        "requestedCapabilities"
    )
    private val requestKeys = requestMemberNames.toSet()
    private val requestDuplicateSchema = DuplicateObjectMemberSchema(
        descriptorName = "UnityRouteRequestDuplicateGuard",
        memberNames = requestMemberNames
    )
    private val outcomeMemberNames = listOf(
        "contractVersion",
        "requestId",
        "routeId",
        "status",
        "diagnosticCode",
        "resultId",
        "payload"
    )
    private val outcomeKeys = outcomeMemberNames.toSet()
    private val outcomeDuplicateSchema = DuplicateObjectMemberSchema(
        descriptorName = "UnityRouteOutcomeDuplicateGuard",
        memberNames = outcomeMemberNames
    )
    private val readyMemberNames = listOf(
        "contractVersion",
        "requestId",
        "routeId"
    )
    private val readyKeys = readyMemberNames.toSet()
    private val readyDuplicateSchema = DuplicateObjectMemberSchema(
        descriptorName = "UnityRouteReadyDuplicateGuard",
        memberNames = readyMemberNames
    )

    fun createRequest(
        requestId: String,
        routeId: String,
        intent: UnityRouteIntent,
        requestedCapabilities: List<String> = emptyList()
    ): UnityBridgeContractResult<UnityRouteRequest> {
        return validateRequest(
            UnityRouteRequest(
                contractVersion = UNITY_BRIDGE_CONTRACT_VERSION,
                requestId = requestId,
                routeId = routeId,
                intent = intent,
                requestedCapabilities = requestedCapabilities.toList()
            )
        )
    }

    fun encodeRequest(request: UnityRouteRequest): UnityBridgeContractResult<String> {
        return when (val validation = validateRequest(request)) {
            is UnityBridgeContractResult.Rejected -> validation
            is UnityBridgeContractResult.Accepted -> {
                val validRequest = validation.value
                val encoded = buildJsonObject {
                    put("contractVersion", validRequest.contractVersion)
                    put("requestId", validRequest.requestId)
                    put("routeId", validRequest.routeId)
                    put("intent", validRequest.intent.wireValue)
                    put(
                        "requestedCapabilities",
                        JsonArray(validRequest.requestedCapabilities.map(::JsonPrimitive))
                    )
                }.toString()

                if (encoded.exceedsUtf8Limit(MAX_UNITY_BRIDGE_MESSAGE_BYTES)) {
                    rejected(UnityBridgeProtocolErrorCode.MessageTooLarge)
                } else {
                    UnityBridgeContractResult.Accepted(encoded)
                }
            }
        }
    }

    fun parseRequest(rawJson: String): UnityBridgeContractResult<UnityRouteRequest> {
        if (rawJson.exceedsUtf8Limit(MAX_UNITY_BRIDGE_MESSAGE_BYTES)) {
            return rejected(UnityBridgeProtocolErrorCode.MessageTooLarge)
        }
        if (rawJson.isBlank()) {
            return rejected(UnityBridgeProtocolErrorCode.EmptyMessage)
        }

        findDuplicateObjectMember(
            rawJson = rawJson,
            schema = requestDuplicateSchema
        )?.let { duplicate ->
            return rejected(UnityBridgeProtocolErrorCode.DuplicateField, duplicate)
        }

        val root = runCatching { bridgeJson.parseToJsonElement(rawJson) as? JsonObject }
            .getOrNull()
            ?: return rejected(UnityBridgeProtocolErrorCode.MalformedJson)

        return try {
            root.requireOnlyKeys(requestKeys)
            val contractVersion = root.requiredVersion()
            val requestId = root.requiredString(
                key = "requestId",
                maxLength = MAX_REQUEST_ID_LENGTH,
                pattern = correlationIdPattern,
                errorCode = UnityBridgeProtocolErrorCode.InvalidRequestId
            )
            val routeId = root.requiredString(
                key = "routeId",
                maxLength = MAX_ROUTE_ID_LENGTH,
                pattern = routeIdPattern,
                errorCode = UnityBridgeProtocolErrorCode.InvalidRouteId
            )
            val intentValue = root.requiredStringValue(
                key = "intent",
                errorCode = UnityBridgeProtocolErrorCode.InvalidIntent
            )
            val intent = UnityRouteIntent.fromWireValue(intentValue)
                ?: violation(UnityBridgeProtocolErrorCode.InvalidIntent, "intent")
            val capabilities = root.requiredCapabilities()

            validateRequest(
                UnityRouteRequest(
                    contractVersion = contractVersion,
                    requestId = requestId,
                    routeId = routeId,
                    intent = intent,
                    requestedCapabilities = capabilities
                )
            )
        } catch (violation: BridgeContractViolation) {
            UnityBridgeContractResult.Rejected(violation.error)
        }
    }

    fun parseOutcome(rawJson: String?): UnityBridgeContractResult<UnityRouteOutcome> {
        if (rawJson == null) {
            return rejected(UnityBridgeProtocolErrorCode.NullMessage)
        }
        if (rawJson.exceedsUtf8Limit(MAX_UNITY_BRIDGE_MESSAGE_BYTES)) {
            return rejected(UnityBridgeProtocolErrorCode.MessageTooLarge)
        }
        if (rawJson.isBlank()) {
            return rejected(UnityBridgeProtocolErrorCode.EmptyMessage)
        }

        findDuplicateObjectMember(
            rawJson = rawJson,
            schema = outcomeDuplicateSchema
        )?.let { duplicate ->
            return rejected(UnityBridgeProtocolErrorCode.DuplicateField, duplicate)
        }

        val root = runCatching { bridgeJson.parseToJsonElement(rawJson) as? JsonObject }
            .getOrNull()
            ?: return rejected(UnityBridgeProtocolErrorCode.MalformedJson)

        return try {
            root.requireOnlyKeys(outcomeKeys)
            val contractVersion = root.requiredVersion()
            val requestId = root.requiredString(
                key = "requestId",
                maxLength = MAX_REQUEST_ID_LENGTH,
                pattern = correlationIdPattern,
                errorCode = UnityBridgeProtocolErrorCode.InvalidRequestId
            )
            val routeId = root.requiredString(
                key = "routeId",
                maxLength = MAX_ROUTE_ID_LENGTH,
                pattern = routeIdPattern,
                errorCode = UnityBridgeProtocolErrorCode.InvalidRouteId
            )
            val statusValue = root.requiredStringValue(
                key = "status",
                errorCode = UnityBridgeProtocolErrorCode.InvalidStatus
            )
            val status = UnityRouteOutcomeStatus.fromWireValue(statusValue)
                ?: violation(UnityBridgeProtocolErrorCode.InvalidStatus, "status")
            val diagnosticCode = root.optionalString(
                key = "diagnosticCode",
                maxLength = MAX_DIAGNOSTIC_CODE_LENGTH,
                pattern = diagnosticCodePattern,
                errorCode = UnityBridgeProtocolErrorCode.InvalidDiagnosticCode
            )
            val resultId = root.optionalString(
                key = "resultId",
                maxLength = MAX_RESULT_ID_LENGTH,
                pattern = correlationIdPattern,
                errorCode = UnityBridgeProtocolErrorCode.InvalidResultId
            )
            val payload = root.optionalPayload()

            if (
                (status == UnityRouteOutcomeStatus.Failure ||
                    status == UnityRouteOutcomeStatus.Unavailable) &&
                diagnosticCode == null
            ) {
                violation(
                    UnityBridgeProtocolErrorCode.MissingDiagnosticCode,
                    "diagnosticCode"
                )
            }

            UnityBridgeContractResult.Accepted(
                UnityRouteOutcome(
                    contractVersion = contractVersion,
                    requestId = requestId,
                    routeId = routeId,
                    status = status,
                    diagnosticCode = diagnosticCode,
                    resultId = resultId,
                    payload = payload
                )
            )
        } catch (violation: BridgeContractViolation) {
            UnityBridgeContractResult.Rejected(violation.error)
        }
    }

    fun parseReady(rawJson: String?): UnityBridgeContractResult<UnityRouteReady> {
        if (rawJson == null) {
            return rejected(UnityBridgeProtocolErrorCode.NullMessage)
        }
        if (rawJson.exceedsUtf8Limit(MAX_UNITY_BRIDGE_MESSAGE_BYTES)) {
            return rejected(UnityBridgeProtocolErrorCode.MessageTooLarge)
        }
        if (rawJson.isBlank()) {
            return rejected(UnityBridgeProtocolErrorCode.EmptyMessage)
        }

        findDuplicateObjectMember(
            rawJson = rawJson,
            schema = readyDuplicateSchema
        )?.let { duplicate ->
            return rejected(UnityBridgeProtocolErrorCode.DuplicateField, duplicate)
        }

        val root = runCatching { bridgeJson.parseToJsonElement(rawJson) as? JsonObject }
            .getOrNull()
            ?: return rejected(UnityBridgeProtocolErrorCode.MalformedJson)

        return try {
            root.requireOnlyKeys(readyKeys)
            UnityBridgeContractResult.Accepted(
                UnityRouteReady(
                    contractVersion = root.requiredVersion(),
                    requestId = root.requiredString(
                        key = "requestId",
                        maxLength = MAX_REQUEST_ID_LENGTH,
                        pattern = correlationIdPattern,
                        errorCode = UnityBridgeProtocolErrorCode.InvalidRequestId
                    ),
                    routeId = root.requiredString(
                        key = "routeId",
                        maxLength = MAX_ROUTE_ID_LENGTH,
                        pattern = routeIdPattern,
                        errorCode = UnityBridgeProtocolErrorCode.InvalidRouteId
                    )
                )
            )
        } catch (violation: BridgeContractViolation) {
            UnityBridgeContractResult.Rejected(violation.error)
        }
    }

    private fun validateRequest(
        request: UnityRouteRequest
    ): UnityBridgeContractResult<UnityRouteRequest> {
        return try {
            if (request.contractVersion != UNITY_BRIDGE_CONTRACT_VERSION) {
                violation(
                    UnityBridgeProtocolErrorCode.InvalidContractVersion,
                    "contractVersion"
                )
            }
            requireStableId(
                value = request.requestId,
                field = "requestId",
                maxLength = MAX_REQUEST_ID_LENGTH,
                pattern = correlationIdPattern,
                errorCode = UnityBridgeProtocolErrorCode.InvalidRequestId
            )
            requireStableId(
                value = request.routeId,
                field = "routeId",
                maxLength = MAX_ROUTE_ID_LENGTH,
                pattern = routeIdPattern,
                errorCode = UnityBridgeProtocolErrorCode.InvalidRouteId
            )
            if (request.requestedCapabilities.size > MAX_CAPABILITIES) {
                violation(
                    UnityBridgeProtocolErrorCode.TooManyCapabilities,
                    "requestedCapabilities"
                )
            }
            request.requestedCapabilities.forEachIndexed { index, capability ->
                requireStableId(
                    value = capability,
                    field = "requestedCapabilities[$index]",
                    maxLength = MAX_ROUTE_ID_LENGTH,
                    pattern = routeIdPattern,
                    errorCode = UnityBridgeProtocolErrorCode.InvalidCapability
                )
            }
            if (request.requestedCapabilities.size != request.requestedCapabilities.toSet().size) {
                violation(
                    UnityBridgeProtocolErrorCode.DuplicateCapability,
                    "requestedCapabilities"
                )
            }

            UnityBridgeContractResult.Accepted(
                request.copy(requestedCapabilities = request.requestedCapabilities.toList())
            )
        } catch (violation: BridgeContractViolation) {
            UnityBridgeContractResult.Rejected(violation.error)
        }
    }

    private fun JsonObject.requireOnlyKeys(allowedKeys: Set<String>) {
        val unexpected = keys.firstOrNull { it !in allowedKeys }
        if (unexpected != null) {
            violation(UnityBridgeProtocolErrorCode.UnexpectedField, unexpected)
        }
    }

    private fun JsonObject.requiredVersion(): Int {
        val value = this["contractVersion"]
            ?: violation(UnityBridgeProtocolErrorCode.MissingField, "contractVersion")
        val primitive = value as? JsonPrimitive
            ?: violation(
                UnityBridgeProtocolErrorCode.InvalidContractVersion,
                "contractVersion"
            )
        val version = if (primitive.isString) null else primitive.intOrNull
        if (version == null) {
            violation(
                UnityBridgeProtocolErrorCode.InvalidContractVersion,
                "contractVersion"
            )
        }
        if (version != UNITY_BRIDGE_CONTRACT_VERSION) {
            violation(
                UnityBridgeProtocolErrorCode.InvalidContractVersion,
                "contractVersion"
            )
        }
        return version
    }

    private fun JsonObject.requiredString(
        key: String,
        maxLength: Int,
        pattern: Regex,
        errorCode: UnityBridgeProtocolErrorCode
    ): String {
        val value = requiredStringValue(key)
        requireStableId(value, key, maxLength, pattern, errorCode)
        return value
    }

    private fun JsonObject.requiredStringValue(
        key: String,
        errorCode: UnityBridgeProtocolErrorCode = UnityBridgeProtocolErrorCode.MissingField
    ): String {
        val value = this[key]
            ?: violation(UnityBridgeProtocolErrorCode.MissingField, key)
        val primitive = value as? JsonPrimitive
            ?: violation(errorCode, key)
        if (!primitive.isString || primitive.content.isBlank()) {
            violation(errorCode, key)
        }
        return primitive.content
    }

    private fun JsonObject.requiredCapabilities(): List<String> {
        val values = this["requestedCapabilities"]
            ?: violation(
                UnityBridgeProtocolErrorCode.MissingField,
                "requestedCapabilities"
            )
        val capabilities = values as? JsonArray
            ?: violation(
                UnityBridgeProtocolErrorCode.InvalidCapability,
                "requestedCapabilities"
            )
        if (capabilities.size > MAX_CAPABILITIES) {
            violation(
                UnityBridgeProtocolErrorCode.TooManyCapabilities,
                "requestedCapabilities"
            )
        }
        return capabilities.mapIndexed { index, value ->
            val primitive = value as? JsonPrimitive
                ?: violation(
                    UnityBridgeProtocolErrorCode.InvalidCapability,
                    "requestedCapabilities[$index]"
                )
            if (!primitive.isString || primitive.content.isBlank()) {
                violation(
                    UnityBridgeProtocolErrorCode.InvalidCapability,
                    "requestedCapabilities[$index]"
                )
            }
            primitive.content
        }
    }

    private fun JsonObject.optionalString(
        key: String,
        maxLength: Int,
        pattern: Regex,
        errorCode: UnityBridgeProtocolErrorCode
    ): String? {
        if (key !in this) return null
        val value = requiredStringValue(key)
        requireStableId(value, key, maxLength, pattern, errorCode)
        return value
    }

    private fun JsonObject.optionalPayload(): String? {
        if ("payload" !in this) return null
        val payload = requiredStringValue("payload")
        if (payload.exceedsUtf8Limit(MAX_UNITY_BRIDGE_PAYLOAD_BYTES)) {
            violation(UnityBridgeProtocolErrorCode.PayloadTooLarge, "payload")
        }
        return payload
    }

    private fun requireStableId(
        value: String,
        field: String,
        maxLength: Int,
        pattern: Regex,
        errorCode: UnityBridgeProtocolErrorCode
    ) {
        if (value.length > maxLength || !pattern.matches(value)) {
            violation(errorCode, field)
        }
    }
}

private class DuplicateObjectMemberSchema(
    descriptorName: String,
    val memberNames: List<String>
) {
    val descriptor: SerialDescriptor = buildClassSerialDescriptor(descriptorName) {
        memberNames.forEach { memberName ->
            element(memberName, JsonElement.serializer().descriptor)
        }
    }
}

private class DuplicateObjectMemberGuard(
    private val schema: DuplicateObjectMemberSchema
) : KSerializer<Unit> {
    override val descriptor: SerialDescriptor = schema.descriptor

    var duplicateMember: String? = null
        private set

    override fun deserialize(decoder: Decoder) {
        decoder.decodeStructure(descriptor) {
            val seenMembers = BooleanArray(schema.memberNames.size)
            while (true) {
                val index = decodeElementIndex(descriptor)
                if (index == CompositeDecoder.DECODE_DONE) break

                if (seenMembers[index]) {
                    duplicateMember = schema.memberNames[index]
                    throw DuplicateObjectMemberFound()
                }
                seenMembers[index] = true
                decodeSerializableElement(
                    descriptor = descriptor,
                    index = index,
                    deserializer = JsonElement.serializer()
                )
            }
        }
    }

    override fun serialize(encoder: Encoder, value: Unit) {
        error("DuplicateObjectMemberGuard is decode-only")
    }
}

private class DuplicateObjectMemberFound :
    RuntimeException(null, null, false, false)

private fun findDuplicateObjectMember(
    rawJson: String,
    schema: DuplicateObjectMemberSchema
): String? {
    val guard = DuplicateObjectMemberGuard(schema)
    runCatching { bridgeJson.decodeFromString(guard, rawJson) }
    return guard.duplicateMember
}

private class BridgeContractViolation(
    val error: UnityBridgeProtocolError
) : RuntimeException(null, null, false, false)

private fun violation(
    code: UnityBridgeProtocolErrorCode,
    field: String? = null
): Nothing {
    throw BridgeContractViolation(UnityBridgeProtocolError(code, field))
}

private fun <T> rejected(
    code: UnityBridgeProtocolErrorCode,
    field: String? = null
): UnityBridgeContractResult<T> {
    return UnityBridgeContractResult.Rejected(UnityBridgeProtocolError(code, field))
}

private fun String.exceedsUtf8Limit(maxBytes: Int): Boolean {
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
