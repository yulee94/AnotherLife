package com.example.anotherlife.data.simulation

import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.snapshots.SnapshotStateList
import androidx.compose.runtime.snapshots.SnapshotStateMap

enum class ResourceType {
    Food, Wood, Stone, Gold
}

data class Building(
    val id: String,
    val name: String,
    val level: Int = 1,
    val isUpgrading: Boolean = false,
    val upgradeEndTime: Long = 0
)

data class Troop(
    val type: String,
    val count: Int
)

enum class QuestMode {
    Kingdom,
    Arena3D
}

class KingdomState {
    val resources = mutableStateMapOf(
        ResourceType.Food to 1000L,
        ResourceType.Wood to 1000L,
        ResourceType.Stone to 500L,
        ResourceType.Gold to 100L
    )

    val buildings = mutableStateListOf(
        Building("farm", "Farm", level = 1),
        Building("lumber_mill", "Lumber Mill", level = 1),
        Building("quarry", "Quarry", level = 1),
        Building("gold_mine", "Gold Mine", level = 1),
        Building("barracks", "Barracks", level = 1)
    )

    val troops = mutableStateListOf(
        Troop("Infantry", 100),
        Troop("Cavalry", 50),
        Troop("Ranged", 75)
    )

    val territories = mutableStateListOf(
        Territory("T1", "Iron Peaks", "Stonehold"),
        Territory("T2", "Silver Woods", "Eldergrove"),
        Territory("T3", "Golden Plains", "Crownlands"),
        Territory("T4", "Shadow Vale", "Umbral"),
        Territory("T5", "Neutral Borderlands", "None")
    )

    val researches = mutableStateListOf(
        Research("steel_forging", "Steel Forging", "Increases troop Attack Power"),
        Research("plate_armor", "Plate Armor", "Increases troop Defense"),
        Research("masonry", "Advanced Masonry", "Reduces building upgrade times"),
        Research("irrigation", "Irrigation", "Increases Food production")
    )

    val quests = mutableStateListOf(
        Quest("Q1", "Building the Future", "Upgrade any building to Level 2.", target = 1),
        Quest("Q2", "Expansion Force", "Train 100 total troops.", target = 100),
        Quest("Q3", "Technological Edge", "Complete 2 research projects.", target = 2),
        Quest("Q4", "Proven in Battle", "Win 3 battle simulations.", target = 3),
        Quest("OMEN_1", "The First Signal", "Investigate strange celestial vibrations at the Sky Castle.", target = 1),
        Quest("OMEN_2", "Dimensional Rift", "Stabilize the portal to the Otherworld.", target = 1)
    )
}

data class Quest(
    val id: String,
    val title: String,
    val description: String,
    var progress: Int = 0,
    val target: Int,
    var isCompleted: Boolean = false,
    var isClaimed: Boolean = false,
    val mode: QuestMode = QuestMode.Kingdom,
    val mapMarkerId: String? = null
)

data class Research(
    val id: String,
    val name: String,
    val description: String,
    var level: Int = 0,
    var isResearching: Boolean = false
)

data class Territory(
    val id: String,
    val name: String,
    var owner: String
)
