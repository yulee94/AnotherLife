package com.example.anotherlife

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Scaffold
import androidx.compose.ui.Modifier
import com.example.anotherlife.ui.shell.AnotherLifeShell
import com.example.anotherlife.ui.theme.AnotherLifeTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            AnotherLifeTheme {
                Scaffold(modifier = Modifier.fillMaxSize()) { innerPadding ->
                    // The Shell handles internal navigation and padding
                    AnotherLifeShell()
                }
            }
        }
    }
}
