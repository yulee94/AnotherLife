package com.example.anotherlife

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import com.example.anotherlife.ui.shell.AnotherLifeShell
import com.example.anotherlife.ui.theme.AnotherLifeTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        enableEdgeToEdge()
        super.onCreate(savedInstanceState)
        setContent {
            AnotherLifeTheme {
                AnotherLifeShell()
            }
        }
    }
}
