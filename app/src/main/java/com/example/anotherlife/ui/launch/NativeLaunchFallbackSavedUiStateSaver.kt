package com.example.anotherlife.ui.launch

import androidx.compose.runtime.saveable.Saver
import androidx.compose.runtime.saveable.listSaver

internal val NativeLaunchFallbackSavedUiStateSaver:
    Saver<NativeLaunchFallbackSavedUiState, Any> = listSaver(
        save = { state -> NativeLaunchFallbackSavedUiStateCodec.encode(state) },
        restore = { fields -> NativeLaunchFallbackSavedUiStateCodec.decode(fields) }
    )
