package com.example.anotherlife.ui.unity

import java.io.File
import javax.xml.parsers.DocumentBuilderFactory
import org.junit.Assert.assertEquals
import org.junit.Test
import org.w3c.dom.Element

class UnityFallbackResourceTest {
    @Test
    fun baseModuleOwnsCompleteDefaultFallbackCopy() {
        val expected = mapOf(
            "unity_runtime_starting" to "Starting interactive experience…",
            "unity_runtime_unavailable" to "Unity runtime unavailable",
            "unity_runtime_unavailable_lifecycle_failure" to
                "Unity runtime unavailable\\nLifecycle failure",
            "unity_runtime_unavailable_handoff_pending" to
                "Unity runtime unavailable\\nHost handoff pending",
            "unity_runtime_unavailable_handoff_capacity" to
                "Unity runtime unavailable\\nHost handoff capacity reached",
            "unity_runtime_unavailable_activation_failed" to
                "Unity runtime unavailable\\nHost activation failed",
            "unity_runtime_unavailable_callback_registration_failed" to
                "Unity runtime unavailable\\nLifecycle callback registration failed",
            "unity_runtime_unavailable_route" to
                "Unity runtime unavailable\\nRoute: %1\$s",
            "unity_bridge_unavailable_code" to
                "Unity bridge unavailable\\nCode: %1\$s"
        )

        val strings = document()
            .getElementsByTagName("string")
            .let { nodes ->
                buildMap {
                    for (index in 0 until nodes.length) {
                        val element = nodes.item(index) as Element
                        put(element.getAttribute("name"), element.textContent.trim())
                    }
                }
            }

        expected.forEach { (name, value) ->
            assertEquals("Unexpected default copy for $name", value, strings[name])
        }
    }

    private fun document() = DocumentBuilderFactory.newInstance()
        .newDocumentBuilder()
        .parse(repositoryFile("app/src/main/res/values/strings.xml"))

    private fun repositoryFile(path: String): File {
        var current = File(requireNotNull(System.getProperty("user.dir"))).canonicalFile
        while (true) {
            if (File(current, "settings.gradle.kts").isFile) return File(current, path)
            current = current.parentFile ?: error("Repository root not found from user.dir")
        }
    }
}
