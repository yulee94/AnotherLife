package com.example.anotherlife.data.contracts

import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.intOrNull

const val NVS01_PREVIEW_ASSET = "OMEN_1.catalog.json"
const val MAX_NVS01_PREVIEW_CATALOG_BYTES = 64 * 1024
const val EXPECTED_NVS01_PACKET_VERSION = "omen1-a1-2026-07-22-v002"
const val EXPECTED_NVS01_CATALOG_SHA256 =
    "b22c166310617657cf9716f988e697d4c4992b4d1877b6fd4d0a3311af9a9a1f"

private const val SUPPORTED_NVS01_SCHEMA_VERSION = 1
private const val MAX_NVS01_RECORDS = 128
private val nvs01Json = Json { isLenient = false }
private val rootKeys = setOf(
    "schemaVersion",
    "packetVersion",
    "milestoneId",
    "questId",
    "titleKey",
    "descriptionKey",
    "approval",
    "placement",
    "speaker",
    "states",
    "objectives",
    "dialogue",
    "transitions",
    "externalCapabilities",
    "consequences",
    "abandonment",
    "localization"
)

enum class QuestPreviewRole {
    ReadOnlyCatalog
}

data class Nvs01CanonicalQuest(
    val schemaVersion: Int,
    val sourceVersion: String,
    val milestoneId: String,
    val questId: String,
    val title: String,
    val description: String,
    val speakerName: String,
    val speakerRole: String,
    val objectives: List<QuestPreviewObjective>,
    val rewardSummaries: List<String>,
    val role: QuestPreviewRole = QuestPreviewRole.ReadOnlyCatalog,
    val hasAuthoritativeProgress: Boolean = false,
    val hasRuntimeActions: Boolean = false
)

data class QuestPreviewCatalog(
    val schemaVersion: Int,
    val sourceVersion: String,
    val presentationVersion: String,
    val presentationSourceId: String,
    val milestoneId: String,
    val questId: String,
    val title: String,
    val description: String,
    val speakerName: String,
    val speakerRole: String,
    val objectives: List<QuestPreviewObjective>,
    val rewardSummaries: List<String>,
    val locationName: String,
    val locationSummary: String,
    val runtimeStatusTitle: String,
    val runtimeStatusSummary: String,
    val role: QuestPreviewRole = QuestPreviewRole.ReadOnlyCatalog,
    val hasAuthoritativeProgress: Boolean = false,
    val hasRuntimeActions: Boolean = false
)

data class QuestPreviewObjective(
    val id: String,
    val text: String
)

object Nvs01PreviewParser {
    fun parse(raw: String): Nvs01CanonicalQuest {
        val bytes = raw.toByteArray(StandardCharsets.UTF_8)
        require(bytes.size <= MAX_NVS01_PREVIEW_CATALOG_BYTES) {
            "OMEN_1 catalog exceeds the $MAX_NVS01_PREVIEW_CATALOG_BYTES-byte Android limit."
        }
        require(!raw.startsWith('\uFEFF')) { "OMEN_1 catalog must not contain a UTF-8 BOM." }
        require('\r' !in raw) { "OMEN_1 catalog must use canonical LF line endings." }
        require(raw.endsWith('\n')) { "OMEN_1 catalog must end with a canonical LF." }

        val root = runCatching { nvs01Json.parseToJsonElement(raw) as? JsonObject }
            .getOrElse { throw IllegalArgumentException("OMEN_1 catalog is not valid JSON.", it) }
            ?: throw IllegalArgumentException("OMEN_1 catalog root must be an object.")
        require(root.keys == rootKeys) {
            "OMEN_1 catalog root fields do not match schema version 1."
        }

        val schemaVersion = root.integer("schemaVersion", "OMEN_1 catalog")
        require(schemaVersion == SUPPORTED_NVS01_SCHEMA_VERSION) {
            "OMEN_1 schema version '$schemaVersion' is unsupported."
        }
        val sourceVersion = root.string("packetVersion", "OMEN_1 catalog")
        require(sourceVersion == EXPECTED_NVS01_PACKET_VERSION) {
            "OMEN_1 packet version '$sourceVersion' is unsupported."
        }
        val milestoneId = root.string("milestoneId", "OMEN_1 catalog")
        val questId = root.string("questId", "OMEN_1 catalog")
        require(milestoneId == "NVS-01" && questId == "OMEN_1") {
            "OMEN_1 catalog identity does not match the approved preview contract."
        }

        val localizedText = root.objectValue("localization", "OMEN_1 catalog")
            .localizedStrings()
        val title = localizedText.resolve(
            root.string("titleKey", "OMEN_1 catalog"),
            "quest title"
        )
        val description = localizedText.resolve(
            root.string("descriptionKey", "OMEN_1 catalog"),
            "quest description"
        )

        val speaker = root.objectValue("speaker", "OMEN_1 catalog")
        val speakerId = speaker.string("id", "OMEN_1 speaker")
        val speakerName = localizedText.resolve(
            speaker.string("nameKey", "OMEN_1 speaker"),
            "speaker name"
        )
        val speakerRole = localizedText.resolve(
            speaker.string("roleKey", "OMEN_1 speaker"),
            "speaker role"
        )

        val stateIds = root.array("states", "OMEN_1 catalog")
            .mapIndexed { index, value ->
                value.objectAt("state[$index]").string("id", "state[$index]")
            }
            .also { requireUnique(it, "state ID") }
            .toSet()

        val objectives = root.array("objectives", "OMEN_1 catalog")
            .mapIndexed { index, value ->
                val objective = value.objectAt("objective[$index]")
                val objectiveId = objective.string("id", "objective[$index]")
                val activeState = objective.string("activatesIn", "objective[$index]")
                require(activeState in stateIds) {
                    "Objective '$objectiveId' references unknown state '$activeState'."
                }
                QuestPreviewObjective(
                    id = objectiveId,
                    text = localizedText.resolve(
                        objective.string("textKey", "objective[$index]"),
                        "objective '$objectiveId'"
                    )
                )
            }
            .also { requireUnique(it.map(QuestPreviewObjective::id), "objective ID") }

        val dialogue = root.array("dialogue", "OMEN_1 catalog")
            .mapIndexed { index, value -> value.objectAt("dialogue[$index]") }
        val dialogueIds = dialogue
            .mapIndexed { index, node -> node.string("id", "dialogue[$index]") }
            .also { requireUnique(it, "dialogue ID") }
            .toSet()
        dialogue.forEachIndexed { index, node ->
            val nodeId = node.string("id", "dialogue[$index]")
            require(node.string("speakerId", "dialogue '$nodeId'") == speakerId) {
                "Dialogue '$nodeId' references an unknown speaker."
            }
            localizedText.resolve(
                node.string("textKey", "dialogue '$nodeId'"),
                "dialogue '$nodeId'"
            )
            node.array("choices", "dialogue '$nodeId'").forEachIndexed { choiceIndex, value ->
                val choice = value.objectAt("dialogue '$nodeId' choice[$choiceIndex]")
                localizedText.resolve(
                    choice.string("key", "dialogue '$nodeId' choice[$choiceIndex]"),
                    "dialogue choice"
                )
                val target = choice.optionalString("target", "dialogue '$nodeId' choice[$choiceIndex]")
                val semanticAction =
                    choice.optionalString("semanticAction", "dialogue '$nodeId' choice[$choiceIndex]")
                require((target == null) != (semanticAction == null)) {
                    "Dialogue '$nodeId' choice[$choiceIndex] must define one target or semantic action."
                }
                require(target == null || target == "end" || target in dialogueIds) {
                    "Dialogue '$nodeId' references unknown target '$target'."
                }
            }
        }

        val transitionKeys = root.array("transitions", "OMEN_1 catalog")
            .mapIndexed { index, value ->
                val transition = value.objectAt("transition[$index]")
                val from = transition.string("from", "transition[$index]")
                val event = transition.string("event", "transition[$index]")
                val to = transition.string("to", "transition[$index]")
                require(from in stateIds && to in stateIds) {
                    "Transition '$from/$event' references an unknown state."
                }
                transition.optionalString("objective", "transition[$index]")?.let { objectiveId ->
                    require(objectives.any { it.id == objectiveId }) {
                        "Transition '$from/$event' references unknown objective '$objectiveId'."
                    }
                }
                transition.optionalString("dialogue", "transition[$index]")?.let { dialogueId ->
                    require(dialogueId in dialogueIds) {
                        "Transition '$from/$event' references unknown dialogue '$dialogueId'."
                    }
                }
                "$from\u0000$event"
            }
        requireUnique(transitionKeys, "transition identity")

        val externalCapabilityIds = root.array("externalCapabilities", "OMEN_1 catalog")
            .mapIndexed { index, value ->
                val capability = value.objectAt("externalCapability[$index]")
                val id = capability.string("id", "externalCapability[$index]")
                require(capability.string("status", "externalCapability '$id'") == "requested") {
                    "Android preview cannot advertise unverified capability '$id'."
                }
                id
            }
        requireUnique(externalCapabilityIds, "external capability ID")

        val consequenceIds = root.array("consequences", "OMEN_1 catalog")
            .mapIndexed { index, value ->
                val consequence = value.objectAt("consequence[$index]")
                val id = consequence.string("id", "consequence[$index]")
                consequence.string("target", "consequence '$id'")
                consequence.string("trigger", "consequence '$id'")
                require(consequence.string("repeatability", "consequence '$id'") == "once") {
                    "Consequence '$id' must remain one-time in the approved preview."
                }
                id
            }
        requireUnique(consequenceIds, "consequence ID")

        val questLocalizationPrefix = "reward.${questId.lowercase().replace("_", "")}."
        val rewardSummaries = localizedText.entries
            .filter { (key, _) ->
                (key.startsWith("artifact.") && key.endsWith(".name")) ||
                    key.startsWith(questLocalizationPrefix)
            }
            .map { it.value }
        require(rewardSummaries.isNotEmpty()) {
            "OMEN_1 catalog does not provide approved reward summaries."
        }
        requireUnique(rewardSummaries, "reward summary")

        val actualHash = sha256(bytes)
        require(actualHash == EXPECTED_NVS01_CATALOG_SHA256) {
            "OMEN_1 catalog content drifted from the approved canonical source."
        }

        return Nvs01CanonicalQuest(
            schemaVersion = schemaVersion,
            sourceVersion = sourceVersion,
            milestoneId = milestoneId,
            questId = questId,
            title = title,
            description = description,
            speakerName = speakerName,
            speakerRole = speakerRole,
            objectives = objectives,
            rewardSummaries = rewardSummaries
        )
    }
}

private fun JsonObject.localizedStrings(): Map<String, String> {
    require(size <= MAX_NVS01_RECORDS) {
        "OMEN_1 localization exceeds the $MAX_NVS01_RECORDS-entry Android limit."
    }
    return entries.associate { (key, value) ->
        val primitive = value as? JsonPrimitive
            ?: throw IllegalArgumentException("Localization '$key' must be a string.")
        require(primitive.isString && primitive.content.isNotBlank()) {
            "Localization '$key' must be a non-blank string."
        }
        key to primitive.content
    }
}

private fun Map<String, String>.resolve(key: String, context: String): String {
    return requireNotNull(this[key]) { "Missing localization '$key' for $context." }
}

private fun JsonObject.string(key: String, context: String): String {
    val primitive = this[key] as? JsonPrimitive
        ?: throw IllegalArgumentException("$context.$key must be a string.")
    require(primitive.isString && primitive.content.isNotBlank()) {
        "$context.$key must be a non-blank string."
    }
    return primitive.content
}

private fun JsonObject.optionalString(key: String, context: String): String? {
    val value = this[key] ?: return null
    val primitive = value as? JsonPrimitive
        ?: throw IllegalArgumentException("$context.$key must be a string.")
    require(primitive.isString && primitive.content.isNotBlank()) {
        "$context.$key must be a non-blank string."
    }
    return primitive.content
}

private fun JsonObject.integer(key: String, context: String): Int {
    return requireNotNull((this[key] as? JsonPrimitive)?.intOrNull) {
        "$context.$key must be an integer."
    }
}

private fun JsonObject.array(key: String, context: String): JsonArray {
    val value = this[key] as? JsonArray
        ?: throw IllegalArgumentException("$context.$key must be an array.")
    require(value.size <= MAX_NVS01_RECORDS) {
        "$context.$key exceeds the $MAX_NVS01_RECORDS-entry Android limit."
    }
    return value
}

private fun JsonObject.objectValue(key: String, context: String): JsonObject {
    return this[key] as? JsonObject
        ?: throw IllegalArgumentException("$context.$key must be an object.")
}

private fun Any?.objectAt(context: String): JsonObject {
    return this as? JsonObject ?: throw IllegalArgumentException("$context must be an object.")
}

private fun requireUnique(values: List<String>, label: String) {
    require(values.size == values.toSet().size) { "Duplicate $label values are not allowed." }
}

private fun sha256(bytes: ByteArray): String {
    val digest = MessageDigest.getInstance("SHA-256").digest(bytes)
    val result = CharArray(digest.size * 2)
    val digits = "0123456789abcdef"
    digest.forEachIndexed { index, byte ->
        val value = byte.toInt() and 0xff
        result[index * 2] = digits[value ushr 4]
        result[index * 2 + 1] = digits[value and 0x0f]
    }
    return String(result)
}
