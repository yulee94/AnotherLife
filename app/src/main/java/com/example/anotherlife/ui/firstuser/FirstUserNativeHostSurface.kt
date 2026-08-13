package com.example.anotherlife.ui.firstuser

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.ime
import androidx.compose.foundation.layout.isImeVisible
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.union
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.MutableState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.input.key.KeyEvent
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.error
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.isTraversalGroup
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.paneTitle
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.unit.dp
import androidx.compose.ui.platform.testTag

internal const val FIRST_USER_HOST_SURFACE_TAG = "first_user_host_surface"
internal const val FIRST_USER_HOST_TITLE_TAG = "first_user_host_title"
internal const val FIRST_USER_USERNAME_FIELD_TAG = "first_user_username_field"
internal const val FIRST_USER_USERNAME_ERROR_TAG = "first_user_username_error"
internal const val FIRST_USER_PRIMARY_ACTION_TAG = "first_user_primary_action"
internal const val FIRST_USER_BACK_ACTION_TAG = "first_user_back_action"

@Composable
internal fun rememberFirstUserUsernameUiState(
    initialDraft: String,
    maxDraftLength: Int
): MutableState<FirstUserUsernameUiState> {
    require(maxDraftLength > 0) { "maxDraftLength must be positive" }
    require(initialDraft.length <= maxDraftLength) {
        "initialDraft exceeds the caller-owned maximum"
    }
    return rememberSaveable(
        maxDraftLength,
        saver = firstUserUsernameUiStateSaver(maxDraftLength)
    ) {
        androidx.compose.runtime.mutableStateOf(
            FirstUserUsernameUiState(
                cursor = FirstUserUsernameCursor(
                    draft = initialDraft,
                    selectionStart = initialDraft.length,
                    selectionEnd = initialDraft.length,
                    focusTarget = FirstUserHostFocusTarget.None
                ),
                restorationPending = false
            )
        )
    }
}

/**
 * Dormant native support for the username step. All player-facing copy and validation authority
 * remain caller-owned; this surface owns only layout, input, focus, and saveable UI cursor behavior.
 */
@Composable
@OptIn(ExperimentalLayoutApi::class)
internal fun FirstUserUsernameHostSurface(
    state: FirstUserUsernameUiState,
    maxDraftLength: Int,
    paneTitleText: String,
    titleText: String,
    usernameLabel: String,
    primaryActionLabel: String,
    backActionLabel: String,
    onStateChange: (FirstUserUsernameUiState) -> Unit,
    onSubmit: (String) -> Unit,
    onBack: () -> Unit,
    modifier: Modifier = Modifier,
    supportingText: String? = null,
    errorMessage: String? = null,
    submitEnabled: Boolean = true,
    contentWindowInsets: WindowInsets = WindowInsets.safeDrawing.union(WindowInsets.ime),
    imeVisibleOverride: Boolean? = null
) {
    require(maxDraftLength > 0) { "maxDraftLength must be positive" }
    val cursor = state.cursor
    val usernameFocusRequester = remember { FocusRequester() }
    val primaryFocusRequester = remember { FocusRequester() }
    val backFocusRequester = remember { FocusRequester() }
    val keyboardController = LocalSoftwareKeyboardController.current
    val focusManager = LocalFocusManager.current
    val imeVisible = imeVisibleOverride ?: WindowInsets.isImeVisible

    fun updateCursor(next: FirstUserUsernameCursor) {
        if (!next.isValid(maxDraftLength)) return
        onStateChange(state.copy(cursor = next))
    }

    fun submit() {
        if (!submitEnabled) return
        keyboardController?.hide()
        focusManager.clearFocus()
        onSubmit(cursor.draft)
    }

    fun back() {
        if (imeVisible) {
            keyboardController?.hide()
        } else {
            onBack()
        }
    }

    LaunchedEffect(cursor.focusTarget) {
        when (cursor.focusTarget) {
            FirstUserHostFocusTarget.Username -> usernameFocusRequester.requestFocus()
            FirstUserHostFocusTarget.PrimaryAction -> primaryFocusRequester.requestFocus()
            FirstUserHostFocusTarget.BackAction -> backFocusRequester.requestFocus()
            FirstUserHostFocusTarget.None -> Unit
        }
    }

    BackHandler(onBack = ::back)

    Box(
        modifier = modifier
            .fillMaxSize()
            .windowInsetsPadding(contentWindowInsets)
            .semantics {
                paneTitle = paneTitleText
                isTraversalGroup = true
            }
            .testTag(FIRST_USER_HOST_SURFACE_TAG)
            .onPreviewKeyEvent { event ->
                when (
                    FirstUserHostInputPolicy.actionFor(
                        key = event.toFirstUserHostInputKey(),
                        isKeyUp = event.type == KeyEventType.KeyUp,
                        focusTarget = cursor.focusTarget,
                        submitEnabled = submitEnabled,
                        imeVisible = imeVisible
                    )
                ) {
                    FirstUserHostInputAction.Submit -> {
                        submit()
                        true
                    }

                    FirstUserHostInputAction.DismissIme -> {
                        keyboardController?.hide()
                        true
                    }

                    FirstUserHostInputAction.NavigateBack -> {
                        onBack()
                        true
                    }

                    null -> false
                }
            }
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp, vertical = 20.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Text(
                text = titleText,
                style = MaterialTheme.typography.headlineSmall,
                modifier = Modifier
                    .semantics { heading() }
                    .testTag(FIRST_USER_HOST_TITLE_TAG)
            )

            supportingText?.let { text ->
                Text(text = text, style = MaterialTheme.typography.bodyMedium)
            }

            OutlinedTextField(
                value = TextFieldValue(
                    text = cursor.draft,
                    selection = TextRange(cursor.selectionStart, cursor.selectionEnd)
                ),
                onValueChange = { value ->
                    if (value.text.length <= maxDraftLength) {
                        updateCursor(
                            cursor.copy(
                                draft = value.text,
                                selectionStart = value.selection.start,
                                selectionEnd = value.selection.end,
                                focusTarget = FirstUserHostFocusTarget.Username
                            )
                        )
                    }
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(min = 56.dp)
                    .focusRequester(usernameFocusRequester)
                    .onFocusChanged { focusState ->
                        if (focusState.isFocused &&
                            cursor.focusTarget != FirstUserHostFocusTarget.Username
                        ) {
                            updateCursor(cursor.copy(focusTarget = FirstUserHostFocusTarget.Username))
                        }
                    }
                    .semantics {
                        if (errorMessage != null) error(errorMessage)
                    }
                    .testTag(FIRST_USER_USERNAME_FIELD_TAG),
                label = { Text(usernameLabel) },
                singleLine = true,
                isError = errorMessage != null,
                keyboardOptions = KeyboardOptions(
                    capitalization = KeyboardCapitalization.None,
                    keyboardType = KeyboardType.Text,
                    imeAction = ImeAction.Done
                ),
                keyboardActions = KeyboardActions(onDone = { submit() })
            )

            errorMessage?.let { message ->
                Text(
                    text = message,
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodyMedium,
                    modifier = Modifier
                        .semantics { liveRegion = LiveRegionMode.Assertive }
                        .testTag(FIRST_USER_USERNAME_ERROR_TAG)
                )
            }

            Button(
                onClick = ::submit,
                enabled = submitEnabled,
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(min = 48.dp)
                    .focusRequester(primaryFocusRequester)
                    .onFocusChanged { focusState ->
                        if (focusState.isFocused &&
                            cursor.focusTarget != FirstUserHostFocusTarget.PrimaryAction
                        ) {
                            updateCursor(
                                cursor.copy(focusTarget = FirstUserHostFocusTarget.PrimaryAction)
                            )
                        }
                    }
                    .testTag(FIRST_USER_PRIMARY_ACTION_TAG)
            ) {
                Text(primaryActionLabel)
            }

            OutlinedButton(
                onClick = ::back,
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(min = 48.dp)
                    .focusRequester(backFocusRequester)
                    .onFocusChanged { focusState ->
                        if (focusState.isFocused &&
                            cursor.focusTarget != FirstUserHostFocusTarget.BackAction
                        ) {
                            updateCursor(cursor.copy(focusTarget = FirstUserHostFocusTarget.BackAction))
                        }
                    }
                    .testTag(FIRST_USER_BACK_ACTION_TAG)
            ) {
                Text(backActionLabel)
            }
        }
    }
}

private fun KeyEvent.toFirstUserHostInputKey(): FirstUserHostInputKey = when (key) {
    Key.Enter -> FirstUserHostInputKey.Enter
    Key.NumPadEnter -> FirstUserHostInputKey.NumberPadEnter
    Key.Spacebar -> FirstUserHostInputKey.Space
    Key.DirectionCenter -> FirstUserHostInputKey.DirectionCenter
    Key.ButtonA -> FirstUserHostInputKey.GamepadPrimary
    Key.Back -> FirstUserHostInputKey.Back
    Key.Escape -> FirstUserHostInputKey.Escape
    Key.ButtonB -> FirstUserHostInputKey.GamepadSecondary
    else -> FirstUserHostInputKey.Other
}
