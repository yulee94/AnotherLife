package com.example.anotherlife.data.contracts

import java.nio.charset.StandardCharsets
import kotlinx.serialization.Serializable
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.json.Json

const val QUEST_PREVIEW_CONTENT_ASSET = "al_quest_preview_content_catalog.json"
const val MAX_QUEST_PREVIEW_CONTENT_BYTES = 64 * 1024
const val EXPECTED_QUEST_PREVIEW_CONTENT_VERSION = "0.1.0"
const val EXPECTED_QUEST_PREVIEW_CONTENT_SOURCE_ID =
    "al_narrative_quest_preview_source_v001"

private const val EXPECTED_QUEST_PREVIEW_CATALOG_ID =
    "al_quest_preview_content_catalog"
private const val EXPECTED_APPROVED_QUEST_PREVIEW_ID = "quest_preview_omen_1"
private const val EXPECTED_LOCATION_ID = "location_sky_castle_marker"
private const val EXPECTED_RUNTIME_STATUS_ID = "status_runtime_hook_requested"
private const val MAX_QUEST_PREVIEW_CONTENT_RECORDS = 128

private val questPreviewContentJson = Json {
    ignoreUnknownKeys = false
    isLenient = false
}

object QuestPreviewContentParser {
    fun parse(
        raw: String,
        canonicalQuest: Nvs01CanonicalQuest
    ): QuestPreviewCatalog {
        validateCanonicalText(raw)
        val document = runCatching {
            questPreviewContentJson.decodeFromString<QuestPreviewContentDocument>(raw)
        }.getOrElse {
            throw IllegalArgumentException(
                "Quest preview content catalog is not valid schema-conforming JSON.",
                it
            )
        }

        require(document.version == EXPECTED_QUEST_PREVIEW_CONTENT_VERSION) {
            "Quest preview content version '${document.version}' is unsupported."
        }
        require(document.catalogId == EXPECTED_QUEST_PREVIEW_CATALOG_ID) {
            "Quest preview content catalog identity is invalid."
        }
        require(document.sourcePacketId == EXPECTED_QUEST_PREVIEW_CONTENT_SOURCE_ID) {
            "Quest preview content source identity is invalid."
        }
        require(document.sourceAuthorities.consumerIssue == 186) {
            "Quest preview content must remain scoped to issue #186."
        }
        require(document.sourceAuthorities.nvsPacketVersion == canonicalQuest.sourceVersion) {
            "Quest preview content does not match the canonical OMEN_1 packet version."
        }
        require(
            document.sourceAuthorities.nvsPacketVersion == EXPECTED_NVS01_PACKET_VERSION &&
                document.sourceAuthorities.nvsCatalog ==
                "unity/Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json"
        ) {
            "Quest preview content references an unsupported canonical source."
        }
        validatePreviewPolicy(document.previewPolicy)

        val localizedText = document.draftLocalization
            .also { requireBounded(it, "draft localization") }
            .associateUnique(
                keySelector = QuestPreviewLocalization::key,
                label = "draft localization key"
            )
            .mapValues { (key, entry) ->
                require(entry.text.isNotBlank()) {
                    "Quest preview localization '$key' must be non-blank."
                }
                entry.text
            }

        val actionsById = document.actions
            .also { requireBounded(it, "action") }
            .associateUnique(QuestPreviewAction::id, "action ID")
        actionsById.values.forEach { action ->
            require(!action.mutatesAuthoritativeState) {
                "Android quest preview action '${action.id}' cannot mutate authoritative state."
            }
            localizedText.resolve(action.displayNameKey, "action '${action.id}'")
        }
        require(actionsById["action_preview_read"]?.status == "approved_debug_only") {
            "The read-only preview action must remain debug-only."
        }

        val prohibitedActionIds = document.prohibitedReleaseActions
            .also { requireBounded(it, "prohibited release action") }
            .associateUnique(QuestPreviewProhibitedAction::id, "prohibited release action ID")
            .keys
        require(
            prohibitedActionIds.containsAll(
                setOf(
                    "action_start_story",
                    "action_claim_generic_reward",
                    "action_locate_internal_marker"
                )
            )
        ) {
            "Generic start, claim, and internal-marker actions must remain prohibited."
        }

        val previewsById = document.questPreviews
            .also { requireBounded(it, "quest preview") }
            .associateUnique(QuestPreviewSourceEntry::id, "quest preview ID")
        val approvedPreview = requireNotNull(previewsById[EXPECTED_APPROVED_QUEST_PREVIEW_ID]) {
            "The approved OMEN_1 quest preview is missing."
        }
        validateApprovedPreview(
            preview = approvedPreview,
            actionsById = actionsById,
            localizedText = localizedText,
            canonicalQuest = canonicalQuest
        )
        validateLegacyPreview(
            previewsById["quest_preview_omen_2_legacy_demo"],
            expectedRole = "legacy_demo_row_only"
        )
        validateLegacyPreview(
            previewsById["quest_preview_android_kingdom_legacy_rows"],
            expectedRole = "legacy_demo_rows_only"
        )

        val objectives = approvedPreview.displayObjectives.map { objective ->
            val canonicalObjective = canonicalQuest.objectives.singleOrNull {
                it.id == objective.objectiveId
            } ?: throw IllegalArgumentException(
                "Quest preview objective '${objective.objectiveId}' is not in canonical OMEN_1."
            )
            val approvedText = localizedText.resolve(
                objective.textKey,
                "objective '${objective.objectiveId}'"
            )
            require(approvedText == canonicalObjective.text) {
                "Quest preview objective '${objective.objectiveId}' conflicts with canonical OMEN_1."
            }
            canonicalObjective.copy(text = approvedText)
        }
        require(objectives.map(QuestPreviewObjective::id) ==
            canonicalQuest.objectives.map(QuestPreviewObjective::id)) {
            "Quest preview objectives must preserve canonical OMEN_1 order and coverage."
        }

        val title = localizedText.resolve(
            requireNotNull(approvedPreview.titleKey),
            "OMEN_1 title"
        )
        val description = localizedText.resolve(
            requireNotNull(approvedPreview.descriptionKey),
            "OMEN_1 description"
        )
        require(title == canonicalQuest.title && description == canonicalQuest.description) {
            "Quest preview title or description conflicts with canonical OMEN_1."
        }

        val rewardSummaries = approvedPreview.rewardSummaryKeys.map { key ->
            localizedText.resolve(key, "OMEN_1 reward")
        }
        require(rewardSummaries == canonicalQuest.rewardSummaries) {
            "Quest preview rewards conflict with canonical OMEN_1."
        }
        require(approvedPreview.rewardTiming.all { !it.manualClaimAllowed }) {
            "OMEN_1 rewards cannot expose manual claim behavior."
        }

        val locationsById = document.locationMarkers
            .also { requireBounded(it, "location marker") }
            .associateUnique(QuestPreviewLocationMarker::id, "location marker ID")
        val location = requireNotNull(locationsById[approvedPreview.locationMarkerId]) {
            "OMEN_1 quest preview references an unknown location marker."
        }
        require(
            location.id == EXPECTED_LOCATION_ID &&
                location.legacyMarkerId == "SKY_CASTLE" &&
                location.status == "requested_unavailable"
        ) {
            "The Sky Castle marker boundary is invalid."
        }

        val statusesById = document.statusCopy
            .also { requireBounded(it, "status copy") }
            .associateUnique(QuestPreviewStatusCopy::id, "status copy ID")
        val runtimeStatus = requireNotNull(statusesById[EXPECTED_RUNTIME_STATUS_ID]) {
            "The runtime-hook status copy is missing."
        }

        require(
            document.engineeringHandoff.blockedRuntimeClaims.containsAll(
                setOf(
                    "authoritative quest progress",
                    "quest acceptance",
                    "Unity arena launch",
                    "reward claim",
                    "save mutation",
                    "notification emission",
                    "production navigation exposure"
                )
            )
        ) {
            "Quest preview runtime claims are incomplete."
        }

        return QuestPreviewCatalog(
            schemaVersion = canonicalQuest.schemaVersion,
            sourceVersion = canonicalQuest.sourceVersion,
            presentationVersion = document.version,
            presentationSourceId = document.sourcePacketId,
            milestoneId = canonicalQuest.milestoneId,
            questId = canonicalQuest.questId,
            title = title,
            description = description,
            speakerName = canonicalQuest.speakerName,
            speakerRole = canonicalQuest.speakerRole,
            objectives = objectives,
            rewardSummaries = rewardSummaries,
            locationName = localizedText.resolve(location.displayNameKey, "Sky Castle marker"),
            locationSummary = localizedText.resolve(location.summaryKey, "Sky Castle marker"),
            runtimeStatusTitle = localizedText.resolve(
                runtimeStatus.displayNameKey,
                "runtime-hook status"
            ),
            runtimeStatusSummary = localizedText.resolve(
                runtimeStatus.summaryKey,
                "runtime-hook status"
            )
        )
    }

    private fun validateCanonicalText(raw: String) {
        val bytes = raw.toByteArray(StandardCharsets.UTF_8)
        require(bytes.size <= MAX_QUEST_PREVIEW_CONTENT_BYTES) {
            "Quest preview content exceeds the $MAX_QUEST_PREVIEW_CONTENT_BYTES-byte Android limit."
        }
        require(!raw.startsWith('\uFEFF')) {
            "Quest preview content must not contain a UTF-8 BOM."
        }
        require(raw.endsWith('\n')) {
            "Quest preview content must end with a newline."
        }
    }

    private fun validatePreviewPolicy(policy: QuestPreviewPolicy) {
        require(
            policy.releaseRole == "unavailable_until_engineering_contract" &&
                policy.approvedDebugRole == "read_only_catalog_preview" &&
                policy.authoritativeProgressSource == "future_unity_quest_runtime" &&
                policy.androidSimulationRows == "legacy_demo_input_only" &&
                policy.internalIdsPlayerFacingPolicy == "debug_only" &&
                policy.unavailableRuntimeBehavior == "visible_nonmutating_status"
        ) {
            "Quest preview source-of-truth policy is unsupported."
        }
    }

    private fun validateApprovedPreview(
        preview: QuestPreviewSourceEntry,
        actionsById: Map<String, QuestPreviewAction>,
        localizedText: Map<String, String>,
        canonicalQuest: Nvs01CanonicalQuest
    ) {
        require(
            preview.questId == canonicalQuest.questId &&
                preview.sourceVersion == canonicalQuest.sourceVersion &&
                preview.previewRole == "read_only_catalog_preview" &&
                preview.releaseAvailability == "hidden_until_engineering_contract"
        ) {
            "Approved OMEN_1 preview identity or availability is invalid."
        }
        val progress = requireNotNull(preview.progressModel) {
            "Approved OMEN_1 preview progress policy is missing."
        }
        require(
            progress.kind == "authoritative_state_machine" &&
                !progress.rawIntegerProgressAllowed &&
                progress.androidProgressBarAuthority == "none_until_validated_runtime_snapshot"
        ) {
            "Android cannot infer authoritative OMEN_1 progress."
        }
        require(preview.availableActions.isNotEmpty()) {
            "Approved OMEN_1 preview actions are missing."
        }
        preview.availableActions.forEach { actionId ->
            val action = requireNotNull(actionsById[actionId]) {
                "Quest preview references unknown action '$actionId'."
            }
            require(actionId !in setOf("action_start_story", "action_claim_generic_reward")) {
                "Quest preview exposes prohibited action '$actionId'."
            }
            localizedText.resolve(action.displayNameKey, "action '$actionId'")
        }
    }

    private fun validateLegacyPreview(
        preview: QuestPreviewSourceEntry?,
        expectedRole: String
    ) {
        require(
            preview != null &&
                preview.previewRole == expectedRole &&
                preview.releaseAvailability == "hidden" &&
                preview.status == "not_approved_source"
        ) {
            "Legacy Android quest rows must remain hidden and non-authoritative."
        }
    }
}

private fun Map<String, String>.resolve(key: String, context: String): String {
    return requireNotNull(this[key]) {
        "Missing quest preview localization '$key' for $context."
    }
}

private fun <T> List<T>.associateUnique(
    keySelector: (T) -> String,
    label: String
): Map<String, T> {
    val result = associateBy(keySelector)
    require(result.size == size) { "Duplicate $label values are not allowed." }
    require(result.keys.none(String::isBlank)) { "$label values must be non-blank." }
    return result
}

private fun requireBounded(values: List<*>, label: String) {
    require(values.size <= MAX_QUEST_PREVIEW_CONTENT_RECORDS) {
        "Quest preview $label exceeds the $MAX_QUEST_PREVIEW_CONTENT_RECORDS-entry limit."
    }
}

@Serializable
private data class QuestPreviewContentDocument(
    val version: String,
    val catalogId: String,
    val game: String,
    val sourcePacketId: String,
    val idFormat: String,
    val sourceAuthorities: QuestPreviewSourceAuthorities,
    val previewPolicy: QuestPreviewPolicy,
    val actions: List<QuestPreviewAction>,
    val prohibitedReleaseActions: List<QuestPreviewProhibitedAction>,
    val locationMarkers: List<QuestPreviewLocationMarker>,
    val questPreviews: List<QuestPreviewSourceEntry>,
    val statusCopy: List<QuestPreviewStatusCopy>,
    val draftLocalization: List<QuestPreviewLocalization>,
    val engineeringHandoff: QuestPreviewEngineeringHandoff
)

@Serializable
private data class QuestPreviewSourceAuthorities(
    val primaryMode: String,
    val consumerIssue: Int,
    val nvsCatalog: String,
    val nvsPacketVersion: String,
    val worldAtlasCatalog: String,
    val routeBoundaryIssue: Int,
    val notificationIssue: Int
)

@Serializable
private data class QuestPreviewPolicy(
    val releaseRole: String,
    val approvedDebugRole: String,
    val authoritativeProgressSource: String,
    val androidSimulationRows: String,
    val internalIdsPlayerFacingPolicy: String,
    val unavailableRuntimeBehavior: String,
    val duplicateTapPolicy: String,
    val nonGoals: List<String>
)

@Serializable
private data class QuestPreviewAction(
    val id: String,
    val displayNameKey: String,
    val sourceSemanticAction: String? = null,
    val requiredCapability: String? = null,
    val requiredState: String? = null,
    val status: String,
    val mutatesAuthoritativeState: Boolean
)

@Serializable
private data class QuestPreviewProhibitedAction(
    val id: String,
    val reason: String
)

@Serializable
private data class QuestPreviewLocationMarker(
    val id: String,
    val legacyMarkerId: String,
    val displayNameKey: String,
    val summaryKey: String,
    val worldAtlasZoneId: String,
    val status: String
)

@Serializable
private data class QuestPreviewSourceEntry(
    val id: String,
    val questId: String,
    val sourceVersion: String? = null,
    val previewRole: String,
    val releaseAvailability: String,
    val status: String? = null,
    val reason: String? = null,
    val titleKey: String? = null,
    val descriptionKey: String? = null,
    val speakerId: String? = null,
    val locationMarkerId: String? = null,
    val progressModel: QuestPreviewProgressModel? = null,
    val displayObjectives: List<QuestPreviewDisplayObjective> = emptyList(),
    val rewardSummaryKeys: List<String> = emptyList(),
    val rewardTiming: List<QuestPreviewRewardTiming> = emptyList(),
    val availableActions: List<String> = emptyList()
)

@Serializable
private data class QuestPreviewProgressModel(
    val kind: String,
    val validStates: List<String>,
    val rawIntegerProgressAllowed: Boolean,
    val androidProgressBarAuthority: String
)

@Serializable
private data class QuestPreviewDisplayObjective(
    val objectiveId: String,
    val textKey: String
)

@Serializable
private data class QuestPreviewRewardTiming(
    val rewardId: String,
    val trigger: String,
    val manualClaimAllowed: Boolean
)

@Serializable
private data class QuestPreviewStatusCopy(
    val id: String,
    val displayNameKey: String,
    val summaryKey: String
)

@Serializable
private data class QuestPreviewLocalization(
    val key: String,
    val text: String
)

@Serializable
private data class QuestPreviewEngineeringHandoff(
    val consumerIssue: Int,
    val requiredValidation: List<String>,
    val blockedRuntimeClaims: List<String>
)
