package com.example.anotherlife.ui.firstuser

import androidx.compose.runtime.MutableState
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.Saver
import androidx.compose.runtime.saveable.listSaver

internal const val FIRST_USER_USERNAME_CURSOR_SCHEMA_VERSION = 1

internal enum class FirstUserHostFocusTarget(val wireValue: String) {
    None("none"),
    Username("username"),
    PrimaryAction("primary_action"),
    BackAction("back_action");

    companion object {
        fun fromWireValue(value: String): FirstUserHostFocusTarget? =
            entries.singleOrNull { it.wireValue == value }
    }
}

/**
 * Saveable UI-only cursor. It deliberately carries no account, profile, operation, receipt,
 * route, commit, projection, or generation authority.
 */
internal data class FirstUserUsernameCursor(
    val draft: String,
    val selectionStart: Int,
    val selectionEnd: Int,
    val focusTarget: FirstUserHostFocusTarget
) {
    fun isValid(maxDraftLength: Int): Boolean =
        maxDraftLength > 0 &&
            draft.length <= maxDraftLength &&
            selectionStart in 0..draft.length &&
            selectionEnd in 0..draft.length

    companion object {
        fun empty(): FirstUserUsernameCursor = FirstUserUsernameCursor(
            draft = "",
            selectionStart = 0,
            selectionEnd = 0,
            focusTarget = FirstUserHostFocusTarget.None
        )
    }
}

internal data class FirstUserUsernameUiState(
    val cursor: FirstUserUsernameCursor,
    val restorationPending: Boolean
)

internal object FirstUserUsernameCursorCodec {
    private const val FieldCount = 5

    fun encode(cursor: FirstUserUsernameCursor, maxDraftLength: Int): List<Any>? {
        if (!cursor.isValid(maxDraftLength)) return null
        return listOf(
            FIRST_USER_USERNAME_CURSOR_SCHEMA_VERSION,
            cursor.draft,
            cursor.selectionStart,
            cursor.selectionEnd,
            cursor.focusTarget.wireValue
        )
    }

    fun decode(fields: List<Any?>, maxDraftLength: Int): FirstUserUsernameCursor? {
        if (fields.size != FieldCount || maxDraftLength <= 0) return null
        if (fields[0] != FIRST_USER_USERNAME_CURSOR_SCHEMA_VERSION) return null

        val draft = fields[1] as? String ?: return null
        val selectionStart = fields[2] as? Int ?: return null
        val selectionEnd = fields[3] as? Int ?: return null
        val focusValue = fields[4] as? String ?: return null
        val focusTarget = FirstUserHostFocusTarget.fromWireValue(focusValue) ?: return null
        return FirstUserUsernameCursor(
            draft = draft,
            selectionStart = selectionStart,
            selectionEnd = selectionEnd,
            focusTarget = focusTarget
        ).takeIf { it.isValid(maxDraftLength) }
    }
}

internal fun firstUserUsernameUiStateSaver(
    maxDraftLength: Int
): Saver<MutableState<FirstUserUsernameUiState>, Any> {
    require(maxDraftLength > 0) { "maxDraftLength must be positive" }
    return listSaver(
        save = { state ->
            FirstUserUsernameCursorCodec.encode(state.value.cursor, maxDraftLength).orEmpty()
        },
        restore = { fields ->
            FirstUserUsernameCursorCodec.decode(fields, maxDraftLength)?.let { cursor ->
                mutableStateOf(
                    FirstUserUsernameUiState(
                        cursor = cursor,
                        restorationPending = true
                    )
                )
            }
        }
    )
}

internal enum class FirstUserHostInputKey {
    Enter,
    NumberPadEnter,
    Space,
    DirectionCenter,
    GamepadPrimary,
    Back,
    Escape,
    GamepadSecondary,
    Other
}

internal enum class FirstUserHostInputAction {
    Submit,
    DismissIme,
    NavigateBack
}

internal object FirstUserHostInputPolicy {
    fun actionFor(
        key: FirstUserHostInputKey,
        isKeyUp: Boolean,
        focusTarget: FirstUserHostFocusTarget,
        submitEnabled: Boolean,
        imeVisible: Boolean
    ): FirstUserHostInputAction? {
        if (!isKeyUp) return null

        if (key.isBackKey()) {
            return if (imeVisible) {
                FirstUserHostInputAction.DismissIme
            } else {
                FirstUserHostInputAction.NavigateBack
            }
        }

        if (!key.isActivationKey()) return null
        return when (focusTarget) {
            FirstUserHostFocusTarget.Username -> {
                if (submitEnabled && key != FirstUserHostInputKey.Space) {
                    FirstUserHostInputAction.Submit
                } else {
                    null
                }
            }

            FirstUserHostFocusTarget.PrimaryAction -> {
                if (submitEnabled) FirstUserHostInputAction.Submit else null
            }

            FirstUserHostFocusTarget.BackAction -> {
                if (imeVisible) {
                    FirstUserHostInputAction.DismissIme
                } else {
                    FirstUserHostInputAction.NavigateBack
                }
            }

            FirstUserHostFocusTarget.None -> null
        }
    }

    private fun FirstUserHostInputKey.isActivationKey(): Boolean = when (this) {
        FirstUserHostInputKey.Enter,
        FirstUserHostInputKey.NumberPadEnter,
        FirstUserHostInputKey.Space,
        FirstUserHostInputKey.DirectionCenter,
        FirstUserHostInputKey.GamepadPrimary -> true

        else -> false
    }

    private fun FirstUserHostInputKey.isBackKey(): Boolean = when (this) {
        FirstUserHostInputKey.Back,
        FirstUserHostInputKey.Escape,
        FirstUserHostInputKey.GamepadSecondary -> true

        else -> false
    }
}
