package com.example.anotherlife.data.contracts

import java.io.File
import java.security.MessageDigest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test

class Nvs01PreviewParserTest {
    @Test
    fun canonicalCatalogCreatesReadOnlyLocalizedPreview() {
        val preview = Nvs01PreviewParser.parse(canonicalCatalog)

        assertEquals(1, preview.schemaVersion)
        assertEquals(EXPECTED_NVS01_PACKET_VERSION, preview.sourceVersion)
        assertEquals("NVS-01", preview.milestoneId)
        assertEquals("OMEN_1", preview.questId)
        assertEquals("The First Signal", preview.title)
        assertEquals("Captain Valerius", preview.speakerName)
        assertEquals(
            listOf(
                "Speak with Captain Valerius.",
                "Deploy your Champion and investigate the Sky Castle anomaly.",
                "Present the Celestial Tear to Valerius."
            ),
            preview.objectives.map(QuestPreviewObjective::text)
        )
        assertEquals(
            listOf("Celestial Tear", "500 Gold", "Valerius affinity +5"),
            preview.rewardSummaries
        )
        assertEquals(QuestPreviewRole.ReadOnlyCatalog, preview.role)
        assertFalse(preview.hasAuthoritativeProgress)
        assertFalse(preview.hasRuntimeActions)
    }

    @Test
    fun canonicalCatalogPinsV004HashAndRemovesKingdomCommandAuthority() {
        val bytes = canonicalCatalog.toByteArray(Charsets.UTF_8)
        val hash = MessageDigest.getInstance("SHA-256")
            .digest(bytes)
            .joinToString("") { byte -> "%02x".format(byte) }

        assertEquals(8_247, bytes.size)
        assertEquals(EXPECTED_NVS01_CATALOG_SHA256, hash)
        assertTrue(
            canonicalCatalog.contains(
                "\"completionDestination\": \"CH1_REALM_INTRO\""
            )
        )
        assertTrue(canonicalCatalog.contains("\"id\":\"CH1_REALM_INTRO\""))
        assertFalse(canonicalCatalog.contains("KINGDOM_COMMAND_VIEW"))
    }

    @Test
    fun duplicateObjectiveIdsFailClosed() {
        val duplicateObjective = canonicalCatalog.replaceFirst(
            "\"id\":\"OBJ_OMEN_1_ARENA\"",
            "\"id\":\"OBJ_OMEN_1_TALK\""
        )

        val error = assertThrows(IllegalArgumentException::class.java) {
            Nvs01PreviewParser.parse(duplicateObjective)
        }

        assertTrue(error.message.orEmpty().contains("Duplicate objective ID"))
    }

    @Test
    fun missingDialogueTargetFailsClosed() {
        val missingTarget = canonicalCatalog.replaceFirst(
            "\"target\":\"DLG_OMEN_1_GO\"",
            "\"target\":\"DLG_OMEN_1_MISSING\""
        )

        val error = assertThrows(IllegalArgumentException::class.java) {
            Nvs01PreviewParser.parse(missingTarget)
        }

        assertTrue(error.message.orEmpty().contains("unknown target"))
    }

    @Test
    fun unknownObjectiveStateFailsClosed() {
        val unknownState = canonicalCatalog.replaceFirst(
            "\"activatesIn\":\"REPORT_TO_VALERIUS\"",
            "\"activatesIn\":\"MISSING_STATE\""
        )

        val error = assertThrows(IllegalArgumentException::class.java) {
            Nvs01PreviewParser.parse(unknownState)
        }

        assertTrue(error.message.orEmpty().contains("unknown state"))
    }

    @Test
    fun missingLocalizationFailsClosed() {
        val missingLocalization = canonicalCatalog.replaceFirst(
            "\"textKey\":\"objective.omen1.report\"",
            "\"textKey\":\"objective.omen1.missing\""
        )

        val error = assertThrows(IllegalArgumentException::class.java) {
            Nvs01PreviewParser.parse(missingLocalization)
        }

        assertTrue(error.message.orEmpty().contains("Missing localization"))
    }

    @Test
    fun unsupportedSchemaLegacyIdentityAndContentDriftFailClosed() {
        val unsupportedSchema = canonicalCatalog.replaceFirst(
            "\"schemaVersion\": 1",
            "\"schemaVersion\": 2"
        )
        val legacyIdentity = canonicalCatalog.replaceFirst(
            EXPECTED_NVS01_PACKET_VERSION,
            "omen1-a1-2026-07-22-v002"
        )
        val validButUnapprovedCopy = canonicalCatalog.replaceFirst(
            "a strange resonance",
            "an altered resonance"
        )

        assertThrows(IllegalArgumentException::class.java) {
            Nvs01PreviewParser.parse(unsupportedSchema)
        }
        val legacyIdentityError = assertThrows(IllegalArgumentException::class.java) {
            Nvs01PreviewParser.parse(legacyIdentity)
        }
        assertTrue(legacyIdentityError.message.orEmpty().contains("packet version"))
        val driftError = assertThrows(IllegalArgumentException::class.java) {
            Nvs01PreviewParser.parse(validButUnapprovedCopy)
        }
        assertTrue(driftError.message.orEmpty().contains("content drifted"))
    }

    private companion object {
        val canonicalCatalog: String by lazy {
            File(
                findRepositoryRoot(),
                "unity/Assets/StreamingAssets/AL/Narrative/$NVS01_PREVIEW_ASSET"
            ).readText()
        }

        fun findRepositoryRoot(): File {
            var current = File(requireNotNull(System.getProperty("user.dir"))).canonicalFile
            repeat(6) {
                if (
                    File(current, "settings.gradle.kts").isFile &&
                    File(current, "unity/Assets/StreamingAssets/AL/Narrative").isDirectory
                ) {
                    return current
                }
                current = current.parentFile
                    ?: throw IllegalStateException("Could not locate the AnotherLife repository root.")
            }
            throw IllegalStateException("Could not locate the AnotherLife repository root.")
        }
    }
}
