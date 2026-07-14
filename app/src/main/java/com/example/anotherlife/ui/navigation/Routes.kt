package com.example.anotherlife.ui.navigation

import kotlinx.serialization.Serializable

sealed interface Route {
    @Serializable
    data object Kingdom : Route
    @Serializable
    data object Quests : Route
    @Serializable
    data object Champion : Route
    @Serializable
    data object Battle : Route
    @Serializable
    data object Warzone : Route
}
