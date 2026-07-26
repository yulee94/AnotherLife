package com.example.anotherlife.data.contracts

import java.nio.charset.StandardCharsets
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.intOrNull

const val MAX_SHARED_CATALOG_BYTES = 64 * 1024

private const val MAX_CATALOG_ENTRIES = 128
private const val EXPECTED_REALM_COUNT = 4
private val semanticVersion = Regex("""\d+\.\d+\.\d+""")
private val lowerSnakeCaseId = Regex("""[a-z][a-z0-9]*(?:_[a-z0-9]+)*""")
private val jsonParser = Json { isLenient = false }

data class SharedCatalogSnapshot(
    val characterCustomizationVersion: String,
    val skillWeatherVersion: String,
    val realmCatalogVersion: String,
    val characterSlots: List<String>,
    val realms: List<RealmContractSummary>,
    val realmSelectionPolicy: RealmSelectionPolicy,
    val activeSkills: List<ActiveSkillContract>,
    val skillEffectKeys: List<String>,
    val weatherProfileKeys: List<String>
)

data class RealmContractSummary(
    val id: String,
    val legacyRuntimeId: String,
    val realmGemIds: List<String>
)

data class RealmSelectionPolicy(
    val selectionMode: String,
    val realmLockScope: String,
    val subCharacterPolicy: String,
    val sharedStoragePolicy: String,
    val crossRealmCreationPolicy: String,
    val realmChangePolicy: String
)

data class ActiveSkillContract(val slot: Int, val id: String)

object SharedCatalogParser {
    fun parse(
        characterCustomizationJson: String,
        skillWeatherJson: String,
        realmCatalogJson: String
    ): SharedCatalogSnapshot {
        val characterRoot = parseRoot(characterCustomizationJson, "character customization catalog")
        val skillRoot = parseRoot(skillWeatherJson, "skill and weather catalog")
        val realmRoot = parseRoot(realmCatalogJson, "realm catalog")

        val characterSlots = characterRoot.stringList("characterSlots", "character customization catalog")
            .also { requireUnique(it, "character slot") }
        val characterRealms = characterRoot.array("realms", "character customization catalog")
            .mapIndexed { index, value ->
                value.objectAt("character realm[$index]").string("id", "character realm[$index]")
            }
            .also { requireUnique(it, "character realm ID") }

        val activeSkills = skillRoot.array("skillLoadouts", "skill and weather catalog")
            .mapIndexed { index, value ->
                val skill = value.objectAt("skill loadout[$index]")
                ActiveSkillContract(
                    slot = skill.integer("slot", "skill loadout[$index]"),
                    id = skill.string("id", "skill loadout[$index]")
                )
            }
            .sortedBy(ActiveSkillContract::slot)
        require(activeSkills.size == 4 && activeSkills.map(ActiveSkillContract::slot) == listOf(0, 1, 2, 3)) {
            "Active skills must uniquely cover slots 0 through 3."
        }
        requireUnique(activeSkills.map(ActiveSkillContract::id), "active skill ID")

        val skillEffectKeys = skillRoot.keysFromObjects("skillEffects", "key", "skill effect")
        val weatherProfileKeys = skillRoot.keysFromObjects("weatherProfiles", "key", "weather profile")

        val policyObject = realmRoot.objectValue("selectionPolicy", "realm catalog")
        val policy = RealmSelectionPolicy(
            selectionMode = policyObject.string("selectionMode", "realm selection policy"),
            realmLockScope = policyObject.string("realmLockScope", "realm selection policy"),
            subCharacterPolicy = policyObject.string("subCharacterPolicy", "realm selection policy"),
            sharedStoragePolicy = policyObject.string("sharedStoragePolicy", "realm selection policy"),
            crossRealmCreationPolicy =
                policyObject.string("crossRealmCreationPolicy", "realm selection policy"),
            realmChangePolicy = policyObject.string("realmChangePolicy", "realm selection policy")
        )
        validatePolicy(policy)

        val realms = realmRoot.array("realms", "realm catalog").mapIndexed { index, value ->
            val realm = value.objectAt("realm[$index]")
            val id = realm.string("id", "realm[$index]")
            require(lowerSnakeCaseId.matches(id)) { "Realm ID '$id' must use lowercase snake case." }
            val gems = realm.stringList("realmGemIds", "realm[$index]")
            require(gems.size == 2) { "Realm '$id' must define exactly two realm gems." }
            RealmContractSummary(
                id = id,
                legacyRuntimeId = realm.string("legacyRuntimeId", "realm[$index]"),
                realmGemIds = gems
            )
        }
        require(realms.size == EXPECTED_REALM_COUNT) {
            "Realm catalog must define exactly $EXPECTED_REALM_COUNT realms."
        }
        requireUnique(realms.map(RealmContractSummary::id), "realm ID")
        requireUnique(realms.map(RealmContractSummary::legacyRuntimeId), "legacy realm ID")
        requireUnique(realms.flatMap(RealmContractSummary::realmGemIds), "realm gem ID")

        val realmOrder = realmRoot.stringList("realmOrder", "realm catalog")
        require(
            realmOrder.size == EXPECTED_REALM_COUNT &&
                realmOrder.size == realmOrder.toSet().size &&
                realmOrder.toSet() == realms.map(RealmContractSummary::id).toSet()
        ) { "Realm order must reference all four realms exactly once." }
        require(characterRealms.toSet() == realms.map(RealmContractSummary::legacyRuntimeId).toSet()) {
            "Character customization realms must match realm catalog legacy IDs."
        }

        val realmsById = realms.associateBy(RealmContractSummary::id)
        return SharedCatalogSnapshot(
            characterCustomizationVersion = characterRoot.supportedVersion("character customization catalog"),
            skillWeatherVersion = skillRoot.supportedVersion("skill and weather catalog"),
            realmCatalogVersion = realmRoot.supportedVersion("realm catalog"),
            characterSlots = characterSlots,
            realms = realmOrder.map(realmsById::getValue),
            realmSelectionPolicy = policy,
            activeSkills = activeSkills,
            skillEffectKeys = skillEffectKeys,
            weatherProfileKeys = weatherProfileKeys
        )
    }
}

private fun parseRoot(raw: String, label: String): JsonObject {
    require(raw.toByteArray(StandardCharsets.UTF_8).size <= MAX_SHARED_CATALOG_BYTES) {
        "$label exceeds the $MAX_SHARED_CATALOG_BYTES-byte Android limit."
    }
    return runCatching { jsonParser.parseToJsonElement(raw) as? JsonObject }
        .getOrElse { throw IllegalArgumentException("$label is not valid JSON.", it) }
        ?: throw IllegalArgumentException("$label root must be an object.")
}

private fun JsonObject.supportedVersion(label: String): String {
    val value = string("version", label)
    require(semanticVersion.matches(value) && value.substringBefore('.').toInt() == 0) {
        "$label version '$value' is unsupported."
    }
    return value
}

private fun JsonObject.keysFromObjects(arrayKey: String, itemKey: String, label: String): List<String> {
    return array(arrayKey, "skill and weather catalog")
        .mapIndexed { index, value -> value.objectAt("$label[$index]").string(itemKey, "$label[$index]") }
        .also { requireUnique(it, "$label key") }
}

private fun JsonObject.stringList(key: String, context: String): List<String> {
    val values = array(key, context).mapIndexed { index, value ->
        val primitive = value as? JsonPrimitive
            ?: throw IllegalArgumentException("$context.$key[$index] must be a string.")
        require(primitive.isString && primitive.content.isNotBlank()) {
            "$context.$key[$index] must be a non-blank string."
        }
        primitive.content
    }
    require(values.isNotEmpty()) { "$context.$key must not be empty." }
    return values
}

private fun JsonObject.string(key: String, context: String): String {
    val primitive = this[key] as? JsonPrimitive
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
    require(value.size <= MAX_CATALOG_ENTRIES) {
        "$context.$key exceeds the $MAX_CATALOG_ENTRIES-entry Android limit."
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

private fun validatePolicy(policy: RealmSelectionPolicy) {
    require(policy.selectionMode == "one_realm_per_account")
    require(policy.realmLockScope == "account")
    require(policy.subCharacterPolicy == "same_realm_only")
    require(policy.sharedStoragePolicy == "same_realm_account_storage")
    require(policy.crossRealmCreationPolicy == "reject")
    require(policy.realmChangePolicy == "not_supported_after_commit")
}

private fun requireUnique(values: List<String>, label: String) {
    require(values.size == values.toSet().size) { "Duplicate $label values are not allowed." }
}
