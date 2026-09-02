package com.example.anotherlife.ui.unity

internal enum class UnityBridgeSmokeSafeReturnNotice(
    val persistenceKey: String,
    val message: String
) {
    Unavailable(
        persistenceKey = "unavailable",
        message = "Unity bridge smoke route is unavailable as expected. Returned safely to Debug."
    ),
    Cancelled(
        persistenceKey = "cancelled",
        message = "Unity bridge smoke route was cancelled. Returned safely to Debug."
    );

    companion object {
        fun fromPersistenceKey(key: String?): UnityBridgeSmokeSafeReturnNotice? {
            return values().singleOrNull { it.persistenceKey == key }
        }
    }
}
