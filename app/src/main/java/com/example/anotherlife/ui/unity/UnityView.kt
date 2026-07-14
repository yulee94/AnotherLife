package com.example.anotherlife.ui.unity

import android.content.Context
import android.graphics.Color
import android.view.Gravity
import android.view.ViewGroup
import android.widget.FrameLayout
import android.widget.TextView
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.viewinterop.AndroidView
import com.example.anotherlife.R

/**
 * A robust wrapper for the Unity Engine view.
 *
 * In a production scenario, this component would host the [com.unity3d.player.UnityPlayer] instance.
 * It ensures the Unity view fills the available screen space and follows the Android lifecycle.
 */
@Composable
fun UnityView(
    modifier: Modifier = Modifier,
    routeTag: String = "Main",
    onReady: () -> Unit = {}
) {
    AndroidView(
        factory = { context ->
            // Create a container for Unity
            FrameLayout(context).apply {
                layoutParams = ViewGroup.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT,
                    ViewGroup.LayoutParams.MATCH_PARENT
                )
                
                // Add a placeholder that mimics the Unity Engine background
                setBackgroundColor(Color.BLACK)
                
                // Unity integration logic:
                // 1. Get singleton UnityPlayer instance
                // 2. addView(unityPlayer.view)
                
                // Placeholder visuals for the vertical slice:
                addView(createUnityPlaceholderView(context, routeTag))
                
                onReady()
            }
        },
        update = { view ->
            // Handle updates or route changes if necessary
            val container = view as FrameLayout
            val placeholder = container.getChildAt(0) as? TextView
            placeholder?.text = view.context.getString(R.string.unity_placeholder_status, routeTag)
        },
        modifier = modifier.fillMaxSize()
    )
}

private fun createUnityPlaceholderView(context: Context, tag: String): TextView {
    return TextView(context).apply {
        layoutParams = FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.WRAP_CONTENT,
            ViewGroup.LayoutParams.WRAP_CONTENT,
            Gravity.CENTER
        )
        text = context.getString(R.string.unity_placeholder_status, tag)
        setTextColor(Color.WHITE)
        textSize = 20f
        textAlignment = TextView.TEXT_ALIGNMENT_CENTER
    }
}
