package com.example.anotherlife.ui.shell

import com.example.anotherlife.ui.navigation.Route

object ShellRoutePolicy {
    const val DEBUG_ROUTE_UNAVAILABLE_MESSAGE =
        "Developer preview is unavailable in this build. Returned to a safe screen."
    const val QUEST_ROUTE_UNAVAILABLE_MESSAGE =
        "Quest preview is unavailable in this build. Returned to Kingdom."
    const val UNITY_BRIDGE_SMOKE_UNAVAILABLE_MESSAGE =
        "Unity bridge smoke is unavailable in this build. Returned to Kingdom."

    private val debugOnlyRoutes: Set<Route> = setOf(
        Route.Quest,
        Route.NarrativeDebug,
        Route.UnityBridgeSmoke
    )

    val primaryRoutes: List<Route> = listOf(
        Route.Kingdom,
        Route.Dossier,
        Route.Champion,
        Route.Warzone
    )

    fun bottomNavigationRoutes(debugToolsEnabled: Boolean): List<Route> {
        return if (debugToolsEnabled) {
            primaryRoutes + Route.NarrativeDebug
        } else {
            primaryRoutes
        }
    }

    fun navigationSelection(route: Route): Route {
        return when (route) {
            Route.Quest,
            Route.UnityBridgeSmoke -> Route.NarrativeDebug
            else -> route
        }
    }

    fun persistenceKey(route: Route): String {
        return when (route) {
            Route.Kingdom -> "kingdom"
            Route.Dossier -> "dossier"
            Route.Champion -> "champion"
            Route.Battle -> "battle"
            Route.Warzone -> "warzone"
            Route.Quest -> "quest"
            Route.NarrativeDebug -> "narrative_debug"
            Route.UnityBridgeSmoke -> "unity_bridge_smoke"
        }
    }

    fun routeFromPersistenceKey(key: String): Route? {
        return when (key) {
            "kingdom" -> Route.Kingdom
            "dossier" -> Route.Dossier
            "champion" -> Route.Champion
            "battle" -> Route.Battle
            "warzone" -> Route.Warzone
            "quest" -> Route.Quest
            "narrative_debug" -> Route.NarrativeDebug
            "unity_bridge_smoke" -> Route.UnityBridgeSmoke
            else -> null
        }
    }

    fun resolveRoute(route: Any?, debugToolsEnabled: Boolean): RouteResolution {
        return when (route) {
            is Route -> if (route !in debugOnlyRoutes || debugToolsEnabled) {
                RouteResolution.Allowed(route)
            } else {
                RouteResolution.Rejected(
                    requestedRoute = route,
                    fallbackRoute = Route.Kingdom,
                    message = when (route) {
                        Route.Quest -> QUEST_ROUTE_UNAVAILABLE_MESSAGE
                        Route.UnityBridgeSmoke -> UNITY_BRIDGE_SMOKE_UNAVAILABLE_MESSAGE
                        else -> DEBUG_ROUTE_UNAVAILABLE_MESSAGE
                    }
                )
            }
            else -> RouteResolution.Rejected(
                requestedRoute = null,
                fallbackRoute = Route.Kingdom,
                message = DEBUG_ROUTE_UNAVAILABLE_MESSAGE
            )
        }
    }

    fun sanitizeBackStack(backStack: List<Any>, debugToolsEnabled: Boolean): BackStackSanitization {
        val sanitized = mutableListOf<Route>()
        var topRejection: RouteResolution.Rejected? = null
        val topIndex = backStack.lastIndex

        backStack.forEachIndexed { index, key ->
            when (val resolution = resolveRoute(key, debugToolsEnabled)) {
                is RouteResolution.Allowed -> sanitized.addIfNotAdjacentDuplicate(resolution.route)
                is RouteResolution.Rejected -> {
                    if (index == topIndex) {
                        sanitized.addIfNotAdjacentDuplicate(resolution.fallbackRoute)
                        topRejection = resolution
                    }
                }
            }
        }

        if (sanitized.isEmpty()) {
            sanitized.add(Route.Kingdom)
        }

        return BackStackSanitization(
            routes = sanitized,
            rejectedTopRoute = topRejection
        )
    }

    fun reduceRouteNotice(currentMessage: String?, event: RouteNoticeEvent): String? {
        return when (event) {
            is RouteNoticeEvent.BackStackSanitized -> {
                event.rejectedTopRoute?.message ?: currentMessage
            }
            RouteNoticeEvent.NavigationAcknowledged -> null
        }
    }

    private fun MutableList<Route>.addIfNotAdjacentDuplicate(route: Route) {
        if (lastOrNull() != route) {
            add(route)
        }
    }
}

sealed interface RouteResolution {
    val route: Route

    data class Allowed(override val route: Route) : RouteResolution

    data class Rejected(
        val requestedRoute: Route?,
        val fallbackRoute: Route,
        val message: String
    ) : RouteResolution {
        override val route: Route = fallbackRoute
    }
}

data class BackStackSanitization(
    val routes: List<Route>,
    val rejectedTopRoute: RouteResolution.Rejected?
)

sealed interface RouteNoticeEvent {
    data class BackStackSanitized(
        val rejectedTopRoute: RouteResolution.Rejected?
    ) : RouteNoticeEvent

    data object NavigationAcknowledged : RouteNoticeEvent
}
