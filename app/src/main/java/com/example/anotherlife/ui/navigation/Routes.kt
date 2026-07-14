package com.example.anotherlife.ui.navigation

import kotlinx.serialization.Serializable

sealed interface Route {
    @Serializable
    data object Kingdom : Route
    @Serializable
    data object Champion : Route
    @Serializable
    data object Battle : Route
    @Serializable
    data object Warzone : Route
    @Serializable
    data object Quest : Route
    @Serializable
    data object Dossier : Route
    @Serializable
    data object NarrativeDebug : Route
}
