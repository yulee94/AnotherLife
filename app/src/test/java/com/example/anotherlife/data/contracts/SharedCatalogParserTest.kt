package com.example.anotherlife.data.contracts

import java.io.ByteArrayInputStream
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Test

class SharedCatalogParserTest {
    @Test
    fun parsesCompactSnapshotInCanonicalRealmOrder() {
        val snapshot = SharedCatalogParser.parse(characterCatalog, skillCatalog, realmCatalog)

        assertEquals(listOf("Head", "ArmorChest"), snapshot.characterSlots)
        assertEquals(
            listOf("crownlands", "stonehold", "eldergrove", "umbral"),
            snapshot.realms.map(RealmContractSummary::id)
        )
        assertEquals(listOf(0, 1, 2, 3), snapshot.activeSkills.map(ActiveSkillContract::slot))
        assertEquals("same_realm_only", snapshot.realmSelectionPolicy.subCharacterPolicy)
    }

    @Test
    fun rejectsPolicyDriftAndInvalidRealmOrder() {
        val policyDrift = realmCatalog.replace(
            "\"crossRealmCreationPolicy\": \"reject\"",
            "\"crossRealmCreationPolicy\": \"allow\""
        )
        val duplicateOrder = realmCatalog.replace(
            "\"eldergrove\",\"umbral\"",
            "\"eldergrove\",\"crownlands\""
        )

        assertThrows(IllegalArgumentException::class.java) {
            SharedCatalogParser.parse(characterCatalog, skillCatalog, policyDrift)
        }
        assertThrows(IllegalArgumentException::class.java) {
            SharedCatalogParser.parse(characterCatalog, skillCatalog, duplicateOrder)
        }
    }

    @Test
    fun boundedInputsRejectOversizedPayloads() {
        assertThrows(IllegalArgumentException::class.java) {
            SharedCatalogParser.parse(
                " ".repeat(MAX_SHARED_CATALOG_BYTES + 1),
                skillCatalog,
                realmCatalog
            )
        }
        assertThrows(IllegalArgumentException::class.java) {
            ByteArrayInputStream(ByteArray(9)).use { readBoundedUtf8(it, maxBytes = 8) }
        }
    }

    @Test
    fun canonicalRepositoryCatalogsRemainConsumableWithoutCopies() {
        val gameData = File(findRepositoryRoot(), "unity/Assets/AL/StreamingAssets/GameData")
        val snapshot = SharedCatalogParser.parse(
            File(gameData, AndroidSharedCatalogLoader.CHARACTER_CUSTOMIZATION_ASSET).readText(),
            File(gameData, AndroidSharedCatalogLoader.SKILL_WEATHER_ASSET).readText(),
            File(gameData, AndroidSharedCatalogLoader.REALM_CATALOG_ASSET).readText()
        )

        assertEquals(4, snapshot.realms.size)
        assertEquals(4, snapshot.activeSkills.size)
        assertEquals(5, snapshot.weatherProfileKeys.size)
    }

    private fun findRepositoryRoot(): File {
        var current = File(System.getProperty("user.dir")).canonicalFile
        repeat(6) {
            if (
                File(current, "settings.gradle.kts").isFile &&
                File(current, "unity/Assets/AL/StreamingAssets/GameData").isDirectory
            ) {
                return current
            }
            current = current.parentFile
                ?: throw IllegalStateException("Could not locate the AnotherLife repository root.")
        }
        throw IllegalStateException("Could not locate the AnotherLife repository root.")
    }

    private companion object {
        val characterCatalog = """
            {
              "version":"0.5.0",
              "characterSlots":["Head","ArmorChest"],
              "realms":[
                {"id":"Stonehold"},{"id":"Eldergrove"},{"id":"Crownlands"},{"id":"Umbral"}
              ]
            }
        """.trimIndent()

        val skillCatalog = """
            {
              "version":"0.3.0",
              "skillLoadouts":[
                {"slot":3,"id":"breaker"},{"slot":1,"id":"guard"},
                {"slot":0,"id":"strike"},{"slot":2,"id":"burst"}
              ],
              "skillEffects":[{"key":"realm_slash"}],
              "weatherProfiles":[{"key":"neutral_battle_fog"}]
            }
        """.trimIndent()

        val realmCatalog = """
            {
              "version":"0.1.0",
              "selectionPolicy":{
                "selectionMode":"one_realm_per_account",
                "realmLockScope":"account",
                "subCharacterPolicy":"same_realm_only",
                "sharedStoragePolicy":"same_realm_account_storage",
                "crossRealmCreationPolicy": "reject",
                "realmChangePolicy":"not_supported_after_commit"
              },
              "realmOrder":["crownlands","stonehold","eldergrove","umbral"],
              "realms":[
                {"id":"crownlands","legacyRuntimeId":"Crownlands","realmGemIds":["c1","c2"]},
                {"id":"stonehold","legacyRuntimeId":"Stonehold","realmGemIds":["s1","s2"]},
                {"id":"eldergrove","legacyRuntimeId":"Eldergrove","realmGemIds":["e1","e2"]},
                {"id":"umbral","legacyRuntimeId":"Umbral","realmGemIds":["u1","u2"]}
              ]
            }
        """.trimIndent()
    }
}
