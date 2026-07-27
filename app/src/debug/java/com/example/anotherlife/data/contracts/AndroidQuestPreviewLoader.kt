package com.example.anotherlife.data.contracts

import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext

class AndroidQuestPreviewLoader(context: Context) {
    private val assets = context.applicationContext.assets
    private val loadMutex = Mutex()

    @Volatile
    private var cachedCatalog: QuestPreviewCatalog? = null

    suspend fun load(): QuestPreviewCatalog {
        cachedCatalog?.let { return it }
        return loadMutex.withLock {
            cachedCatalog ?: withContext(Dispatchers.IO) {
                val rawCatalog = assets.open(NVS01_PREVIEW_ASSET).use {
                    readBoundedUtf8(
                        input = it,
                        maxBytes = MAX_NVS01_PREVIEW_CATALOG_BYTES,
                        label = NVS01_PREVIEW_ASSET
                    )
                }
                Nvs01PreviewParser.parse(rawCatalog)
            }.also { cachedCatalog = it }
        }
    }

    companion object {
        @Volatile
        private var sharedInstance: AndroidQuestPreviewLoader? = null

        fun shared(context: Context): AndroidQuestPreviewLoader {
            sharedInstance?.let { return it }
            return synchronized(this) {
                sharedInstance ?: AndroidQuestPreviewLoader(context).also { sharedInstance = it }
            }
        }
    }
}
