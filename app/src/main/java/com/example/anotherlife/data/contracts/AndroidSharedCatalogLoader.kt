package com.example.anotherlife.data.contracts

import android.content.Context
import java.io.ByteArrayOutputStream
import java.io.InputStream
import java.nio.ByteBuffer
import java.nio.charset.CodingErrorAction
import java.nio.charset.StandardCharsets
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext

class AndroidSharedCatalogLoader(context: Context) {
    private val assets = context.applicationContext.assets
    private val loadMutex = Mutex()

    @Volatile
    private var cachedSnapshot: SharedCatalogSnapshot? = null

    suspend fun load(): SharedCatalogSnapshot {
        cachedSnapshot?.let { return it }
        return loadMutex.withLock {
            cachedSnapshot ?: withContext(Dispatchers.IO) {
                SharedCatalogParser.parse(
                    readAsset(CHARACTER_CUSTOMIZATION_ASSET),
                    readAsset(SKILL_WEATHER_ASSET),
                    readAsset(REALM_CATALOG_ASSET)
                )
            }.also { cachedSnapshot = it }
        }
    }

    private fun readAsset(path: String): String {
        return assets.open(path).use { readBoundedUtf8(it, MAX_SHARED_CATALOG_BYTES, path) }
    }

    companion object {
        const val CHARACTER_CUSTOMIZATION_ASSET = "al_character_customization_catalog.json"
        const val SKILL_WEATHER_ASSET = "al_skill_weather_catalog.json"
        const val REALM_CATALOG_ASSET = "al_realm_catalog.json"
    }
}

internal fun readBoundedUtf8(input: InputStream, maxBytes: Int, label: String = "catalog"): String {
    require(maxBytes > 0) { "Maximum byte count must be positive." }
    val output = ByteArrayOutputStream(minOf(maxBytes, DEFAULT_BUFFER_SIZE))
    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
    var totalBytes = 0

    while (true) {
        val bytesRead = input.read(buffer)
        if (bytesRead < 0) break
        if (bytesRead == 0) continue
        totalBytes += bytesRead
        require(totalBytes <= maxBytes) { "$label exceeds the $maxBytes-byte Android limit." }
        output.write(buffer, 0, bytesRead)
    }

    return StandardCharsets.UTF_8.newDecoder()
        .onMalformedInput(CodingErrorAction.REPORT)
        .onUnmappableCharacter(CodingErrorAction.REPORT)
        .decode(ByteBuffer.wrap(output.toByteArray()))
        .toString()
}
