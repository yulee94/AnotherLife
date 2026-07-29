package com.example.anotherlife.data.contracts

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test

class QuestPreviewContentParserTest {
    @Test
    fun approvedContentMergesWithCanonicalQuestWithoutRuntimeAuthority() {
        val preview = parseCanonicalPreview()

        assertEquals(EXPECTED_QUEST_PREVIEW_CONTENT_VERSION, preview.presentationVersion)
        assertEquals(EXPECTED_QUEST_PREVIEW_CONTENT_SOURCE_ID, preview.presentationSourceId)
        assertEquals(EXPECTED_NVS01_PACKET_VERSION, preview.sourceVersion)
        assertEquals("The First Signal", preview.title)
        assertEquals("Sky Castle Anomaly", preview.locationName)
        assertEquals("Runtime hook requested", preview.runtimeStatusTitle)
        assertEquals(
            listOf("Celestial Tear", "500 Gold", "Valerius affinity +5"),
            preview.rewardSummaries
        )
        assertFalse(preview.hasAuthoritativeProgress)
        assertFalse(preview.hasRuntimeActions)
    }

    @Test
    fun mismatchedCanonicalPacketVersionFailsClosed() {
        val mismatched = canonicalContent.replaceFirst(
            EXPECTED_NVS01_PACKET_VERSION,
            "omen1-a1-unsupported"
        )

        val error = assertThrows(IllegalArgumentException::class.java) {
            QuestPreviewContentParser.parse(mismatched, canonicalQuest)
        }

        assertTrue(error.message.orEmpty().contains("canonical OMEN_1 packet version"))
    }

    @Test
    fun unknownAvailableActionFailsClosed() {
        val unknownAction = canonicalContent.replaceFirst(
            "\n        \"action_preview_read\",",
            "\n        \"action_missing\","
        )

        val error = assertThrows(IllegalArgumentException::class.java) {
            QuestPreviewContentParser.parse(unknownAction, canonicalQuest)
        }

        assertTrue(error.message.orEmpty().contains("unknown action"))
    }

    @Test
    fun duplicateLocalizationKeyFailsClosed() {
        val duplicateKey = canonicalContent.replaceFirst(
            "\"key\": \"quest_preview.action.deploy_champion.name\"",
            "\"key\": \"quest_preview.action.preview_read.name\""
        )

        val error = assertThrows(IllegalArgumentException::class.java) {
            QuestPreviewContentParser.parse(duplicateKey, canonicalQuest)
        }

        assertTrue(error.message.orEmpty().contains("Duplicate draft localization key"))
    }

    @Test
    fun genericClaimMustRemainProhibited() {
        val missingClaimProhibition = canonicalContent.replaceFirst(
            "\"action_claim_generic_reward\"",
            "\"action_claim_unrelated\""
        )

        val error = assertThrows(IllegalArgumentException::class.java) {
            QuestPreviewContentParser.parse(missingClaimProhibition, canonicalQuest)
        }

        assertTrue(error.message.orEmpty().contains("must remain prohibited"))
    }

    @Test
    fun legacySimulationRowsCannotBecomeApprovedSource() {
        val approvedLegacyRow = canonicalContent.replaceFirst(
            "\"previewRole\": \"legacy_demo_rows_only\"",
            "\"previewRole\": \"read_only_catalog_preview\""
        )

        val error = assertThrows(IllegalArgumentException::class.java) {
            QuestPreviewContentParser.parse(approvedLegacyRow, canonicalQuest)
        }

        assertTrue(error.message.orEmpty().contains("hidden and non-authoritative"))
    }

    @Test
    fun presentationCopyCannotConflictWithCanonicalQuest() {
        val conflictingTitle = canonicalContent.replaceFirst(
            "\"text\": \"The First Signal\"",
            "\"text\": \"A Different Signal\""
        )

        val error = assertThrows(IllegalArgumentException::class.java) {
            QuestPreviewContentParser.parse(conflictingTitle, canonicalQuest)
        }

        assertTrue(error.message.orEmpty().contains("title or description conflicts"))
    }

    private fun parseCanonicalPreview(): QuestPreviewCatalog {
        return QuestPreviewContentParser.parse(canonicalContent, canonicalQuest)
    }

    private companion object {
        val repositoryRoot: File by lazy {
            var current = File(requireNotNull(System.getProperty("user.dir"))).canonicalFile
            repeat(6) {
                if (
                    File(current, "settings.gradle.kts").isFile &&
                    File(current, "unity/Assets/AL/StreamingAssets/GameData").isDirectory
                ) {
                    return@lazy current
                }
                current = current.parentFile
                    ?: throw IllegalStateException(
                        "Could not locate the AnotherLife repository root."
                    )
            }
            throw IllegalStateException("Could not locate the AnotherLife repository root.")
        }

        val canonicalQuest: Nvs01CanonicalQuest by lazy {
            Nvs01PreviewParser.parse(
                File(
                    repositoryRoot,
                    "unity/Assets/StreamingAssets/AL/Narrative/$NVS01_PREVIEW_ASSET"
                ).readText()
            )
        }

        val canonicalContent: String by lazy {
            File(
                repositoryRoot,
                "unity/Assets/AL/StreamingAssets/GameData/$QUEST_PREVIEW_CONTENT_ASSET"
            ).readText()
        }
    }
}
