package com.example.anotherlife.ui.firstuser

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class FirstUserNativeHostStateTest {
    @Test
    fun cursorRoundTripPreservesOnlyBoundedUiState() {
        val cursor = FirstUserUsernameCursor(
            draft = "Aster Vale",
            selectionStart = 2,
            selectionEnd = 8,
            focusTarget = FirstUserHostFocusTarget.Username
        )

        val encoded = FirstUserUsernameCursorCodec.encode(cursor, maxDraftLength = 32)
        val restored = FirstUserUsernameCursorCodec.decode(encoded.orEmpty(), maxDraftLength = 32)

        assertEquals(cursor, restored)
        assertEquals(5, encoded?.size)
        val fieldNames = FirstUserUsernameCursor::class.java.declaredFields
            .map { it.name.lowercase() }
        listOf(
            "profileid",
            "accountid",
            "characterid",
            "receipt",
            "operation",
            "fingerprint",
            "generation",
            "revision"
        ).forEach { forbidden ->
            assertFalse(fieldNames.any { it.contains(forbidden) })
        }
    }

    @Test
    fun cursorRestoreRejectsMalformedOrUnboundedStateWithoutCoercion() {
        val valid = listOf<Any?>(1, "Aster", 0, 5, "username")

        assertNull(FirstUserUsernameCursorCodec.decode(valid.dropLast(1), 32))
        assertNull(FirstUserUsernameCursorCodec.decode(valid + "extra", 32))
        assertNull(FirstUserUsernameCursorCodec.decode(valid.toMutableList().apply { this[0] = 2 }, 32))
        assertNull(FirstUserUsernameCursorCodec.decode(valid.toMutableList().apply { this[1] = 3 }, 32))
        assertNull(FirstUserUsernameCursorCodec.decode(valid.toMutableList().apply { this[2] = -1 }, 32))
        assertNull(FirstUserUsernameCursorCodec.decode(valid.toMutableList().apply { this[3] = 6 }, 32))
        assertNull(
            FirstUserUsernameCursorCodec.decode(
                valid.toMutableList().apply { this[4] = "Username" },
                32
            )
        )
        assertNull(FirstUserUsernameCursorCodec.decode(valid, 4))
        assertNull(FirstUserUsernameCursorCodec.decode(valid, 0))
    }

    @Test
    fun restorationSaverMarksAuthorityRefreshPendingWithoutSavingThatFlag() {
        val cursor = FirstUserUsernameCursor(
            draft = "Aster",
            selectionStart = 5,
            selectionEnd = 5,
            focusTarget = FirstUserHostFocusTarget.PrimaryAction
        )
        val encoded = FirstUserUsernameCursorCodec.encode(cursor, 32).orEmpty()
        val restored = FirstUserUsernameCursorCodec.decode(encoded, 32)

        assertEquals(cursor, restored)
        assertFalse(encoded.any { it == true || it == false })
        assertFalse(encoded.any { it.toString().contains("generation", ignoreCase = true) })
    }

    @Test
    fun keyDownAndUnfocusedActivationNeverTriggerAnAction() {
        FirstUserHostInputKey.entries.forEach { key ->
            assertNull(
                FirstUserHostInputPolicy.actionFor(
                    key = key,
                    isKeyUp = false,
                    focusTarget = FirstUserHostFocusTarget.PrimaryAction,
                    submitEnabled = true,
                    imeVisible = false
                )
            )
        }
        assertNull(
            FirstUserHostInputPolicy.actionFor(
                key = FirstUserHostInputKey.GamepadPrimary,
                isKeyUp = true,
                focusTarget = FirstUserHostFocusTarget.None,
                submitEnabled = true,
                imeVisible = false
            )
        )
    }

    @Test
    fun usernameSpaceRemainsTextWhileDoneAndControllerPrimarySubmit() {
        assertNull(action(FirstUserHostInputKey.Space, FirstUserHostFocusTarget.Username))
        assertEquals(
            FirstUserHostInputAction.Submit,
            action(FirstUserHostInputKey.Enter, FirstUserHostFocusTarget.Username)
        )
        assertEquals(
            FirstUserHostInputAction.Submit,
            action(FirstUserHostInputKey.GamepadPrimary, FirstUserHostFocusTarget.Username)
        )
        assertEquals(
            FirstUserHostInputAction.Submit,
            action(FirstUserHostInputKey.Space, FirstUserHostFocusTarget.PrimaryAction)
        )
        assertNull(
            FirstUserHostInputPolicy.actionFor(
                key = FirstUserHostInputKey.Enter,
                isKeyUp = true,
                focusTarget = FirstUserHostFocusTarget.Username,
                submitEnabled = false,
                imeVisible = false
            )
        )
    }

    @Test
    fun backFamilyDismissesImeBeforeNavigationAndBackActionUsesSamePolicy() {
        listOf(
            FirstUserHostInputKey.Back,
            FirstUserHostInputKey.Escape,
            FirstUserHostInputKey.GamepadSecondary
        ).forEach { key ->
            assertEquals(
                FirstUserHostInputAction.DismissIme,
                action(key, FirstUserHostFocusTarget.Username, imeVisible = true)
            )
            assertEquals(
                FirstUserHostInputAction.NavigateBack,
                action(key, FirstUserHostFocusTarget.Username, imeVisible = false)
            )
        }
        assertEquals(
            FirstUserHostInputAction.DismissIme,
            action(
                FirstUserHostInputKey.GamepadPrimary,
                FirstUserHostFocusTarget.BackAction,
                imeVisible = true
            )
        )
        assertEquals(
            FirstUserHostInputAction.NavigateBack,
            action(
                FirstUserHostInputKey.GamepadPrimary,
                FirstUserHostFocusTarget.BackAction,
                imeVisible = false
            )
        )
    }

    private fun action(
        key: FirstUserHostInputKey,
        focusTarget: FirstUserHostFocusTarget,
        imeVisible: Boolean = false
    ): FirstUserHostInputAction? = FirstUserHostInputPolicy.actionFor(
        key = key,
        isKeyUp = true,
        focusTarget = focusTarget,
        submitEnabled = true,
        imeVisible = imeVisible
    )
}
