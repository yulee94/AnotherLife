package com.example.anotherlife.ui.firstuser

import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assert
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.assertHeightIsAtLeast
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.StateRestorationTester
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performImeAction
import androidx.compose.ui.test.performTextInput
import androidx.compose.ui.test.requestFocus
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.dp
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.example.anotherlife.ui.theme.AnotherLifeTheme
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class FirstUserNativeHostSurfaceTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun semanticsExposePaneHeadingFieldErrorAndBoundedActions() {
        setHostContent(
            initialState = FirstUserUsernameUiState(
                cursor = FirstUserUsernameCursor.empty(),
                restorationPending = false
            ),
            errorMessage = "Name unavailable"
        )

        composeRule.onNodeWithTag(FIRST_USER_HOST_SURFACE_TAG)
            .assert(
                SemanticsMatcher.expectValue(
                    SemanticsProperties.PaneTitle,
                    "Identity setup"
                )
            )
            .assertIsDisplayed()
        composeRule.onNodeWithTag(FIRST_USER_HOST_TITLE_TAG)
            .assert(SemanticsMatcher.keyIsDefined(SemanticsProperties.Heading))
            .assertIsDisplayed()
        composeRule.onNodeWithTag(FIRST_USER_USERNAME_FIELD_TAG)
            .assert(hasText("Display name"))
            .assert(
                SemanticsMatcher.expectValue(
                    SemanticsProperties.Error,
                    "Name unavailable"
                )
            )
            .assertHeightIsAtLeast(48.dp)
        composeRule.onNodeWithTag(FIRST_USER_USERNAME_ERROR_TAG)
            .assert(
                SemanticsMatcher.expectValue(
                    SemanticsProperties.LiveRegion,
                    LiveRegionMode.Assertive
                )
            )
        composeRule.onNodeWithTag(FIRST_USER_PRIMARY_ACTION_TAG)
            .assertHasClickAction()
            .assertHeightIsAtLeast(48.dp)
        composeRule.onNodeWithTag(FIRST_USER_BACK_ACTION_TAG)
            .assertHasClickAction()
            .assertHeightIsAtLeast(48.dp)
    }

    @Test
    fun imeDoneSubmitsExactlyOnceAndDisabledActionDoesNotSubmit() {
        val submits = AtomicInteger()
        setHostContent(onSubmit = { submits.incrementAndGet() })

        val usernameField = composeRule.onNodeWithTag(FIRST_USER_USERNAME_FIELD_TAG)
        usernameField.performTextInput("Aster")
        usernameField.performImeAction()
        composeRule.waitForIdle()
        assertEquals(1, submits.get())
    }

    @Test
    fun disabledImeActionDoesNotSubmit() {
        val submits = AtomicInteger()
        setHostContent(submitEnabled = false, onSubmit = { submits.incrementAndGet() })
        val usernameField = composeRule.onNodeWithTag(FIRST_USER_USERNAME_FIELD_TAG)
        usernameField.performTextInput("Vale")
        usernameField.performImeAction()
        composeRule.waitForIdle()
        assertEquals(0, submits.get())
    }

    @Test
    fun saveableCursorRestoresDraftSelectionFocusAndMarksRefreshPending() {
        val restorationTester = StateRestorationTester(composeRule)
        val observed = AtomicReference<FirstUserUsernameUiState>()
        restorationTester.setContent {
            var state by rememberFirstUserUsernameUiState(
                initialDraft = "",
                maxDraftLength = 32
            )
            observed.set(state)
            AnotherLifeTheme {
                FirstUserUsernameHostSurface(
                    state = state,
                    maxDraftLength = 32,
                    paneTitleText = "Identity setup",
                    titleText = "Choose a display name",
                    usernameLabel = "Display name",
                    primaryActionLabel = "Continue",
                    backActionLabel = "Back",
                    onStateChange = { state = it },
                    onSubmit = {},
                    onBack = {},
                    contentWindowInsets = WindowInsets(0, 0, 0, 0),
                    imeVisibleOverride = false
                )
            }
        }

        val usernameField = composeRule.onNodeWithTag(FIRST_USER_USERNAME_FIELD_TAG)
        usernameField.performTextInput("Aster Vale")
        usernameField.requestFocus()
        composeRule.waitForIdle()
        assertFalse(observed.get().restorationPending)

        restorationTester.emulateSavedInstanceStateRestore()
        composeRule.waitForIdle()

        composeRule.onNodeWithTag(FIRST_USER_USERNAME_FIELD_TAG)
            .assert(hasText("Aster Vale"))
            .assertIsFocused()
        assertEquals("Aster Vale", observed.get().cursor.draft)
        assertEquals(FirstUserHostFocusTarget.Username, observed.get().cursor.focusTarget)
        assertEquals(true, observed.get().restorationPending)
    }

    @Test
    fun twoHundredPercentTextKeepsFieldAndActionsReachable() {
        composeRule.setContent {
            CompositionLocalProvider(
                LocalDensity provides Density(density = 1f, fontScale = 2f)
            ) {
                HostHarness(
                    supportingText = "A deliberately long localized instruction that wraps.",
                    contentWindowInsets = WindowInsets(12, 20, 12, 24)
                )
            }
        }

        composeRule.onNodeWithTag(FIRST_USER_USERNAME_FIELD_TAG).assertIsDisplayed()
        composeRule.onNodeWithTag(FIRST_USER_PRIMARY_ACTION_TAG)
            .assertIsDisplayed()
            .assertHeightIsAtLeast(48.dp)
        composeRule.onNodeWithTag(FIRST_USER_BACK_ACTION_TAG)
            .assertIsDisplayed()
            .assertHeightIsAtLeast(48.dp)
    }

    @Test
    fun backActionUsesImeFirstPolicyBeforeNavigation() {
        val backCount = AtomicInteger()
        val imeVisible = mutableStateOf(true)
        composeRule.setContent {
            HostHarness(
                imeVisibleOverride = imeVisible.value,
                onBack = { backCount.incrementAndGet() }
            )
        }
        composeRule.onNodeWithTag(FIRST_USER_BACK_ACTION_TAG).performClick()
        composeRule.waitForIdle()
        assertEquals(0, backCount.get())

        composeRule.runOnUiThread { imeVisible.value = false }
        composeRule.waitForIdle()
        composeRule.onNodeWithTag(FIRST_USER_BACK_ACTION_TAG).performClick()
        composeRule.waitForIdle()
        assertEquals(1, backCount.get())
    }

    private fun setHostContent(
        initialState: FirstUserUsernameUiState = FirstUserUsernameUiState(
            cursor = FirstUserUsernameCursor.empty(),
            restorationPending = false
        ),
        errorMessage: String? = null,
        submitEnabled: Boolean = true,
        imeVisibleOverride: Boolean = false,
        onSubmit: (String) -> Unit = {},
        onBack: () -> Unit = {}
    ) {
        composeRule.setContent {
            HostHarness(
                initialState = initialState,
                errorMessage = errorMessage,
                submitEnabled = submitEnabled,
                imeVisibleOverride = imeVisibleOverride,
                onSubmit = onSubmit,
                onBack = onBack
            )
        }
    }

    @Composable
    private fun HostHarness(
        initialState: FirstUserUsernameUiState = FirstUserUsernameUiState(
            cursor = FirstUserUsernameCursor.empty(),
            restorationPending = false
        ),
        supportingText: String? = null,
        errorMessage: String? = null,
        submitEnabled: Boolean = true,
        contentWindowInsets: WindowInsets = WindowInsets(0, 0, 0, 0),
        imeVisibleOverride: Boolean = false,
        onSubmit: (String) -> Unit = {},
        onBack: () -> Unit = {}
    ) {
        var state by androidx.compose.runtime.remember { androidx.compose.runtime.mutableStateOf(initialState) }
        AnotherLifeTheme {
            FirstUserUsernameHostSurface(
                state = state,
                maxDraftLength = 32,
                paneTitleText = "Identity setup",
                titleText = "Choose a display name",
                usernameLabel = "Display name",
                primaryActionLabel = "Continue",
                backActionLabel = "Back",
                supportingText = supportingText,
                errorMessage = errorMessage,
                submitEnabled = submitEnabled,
                onStateChange = { state = it },
                onSubmit = onSubmit,
                onBack = onBack,
                contentWindowInsets = contentWindowInsets,
                imeVisibleOverride = imeVisibleOverride
            )
        }
    }
}
