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
                val canonicalQuest = Nvs01PreviewParser.parse(
                    readAsset(
                        assetName = NVS01_PREVIEW_ASSET,
                        maxBytes = MAX_NVS01_PREVIEW_CATALOG_BYTES
                    )
                )
                QuestPreviewContentParser.parse(
                    raw = readAsset(
                        assetName = QUEST_PREVIEW_CONTENT_ASSET,
                        maxBytes = MAX_QUEST_PREVIEW_CONTENT_BYTES
                    ),
                    canonicalQuest = canonicalQuest
                )
            }.also { cachedCatalog = it }
        }
    }

    private fun readAsset(assetName: String, maxBytes: Int): String {
        return assets.open(assetName).use {
            readBoundedUtf8(
                input = it,
                maxBytes = maxBytes,
                label = assetName
            )
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
