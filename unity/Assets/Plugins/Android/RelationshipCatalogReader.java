package com.anotherlife.relationship;

import android.content.res.AssetManager;

import com.unity3d.player.UnityPlayer;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;

public final class RelationshipCatalogReader {
    private static final int MAXIMUM_CATALOG_BYTES = 24576;
    private static final String CATALOG_ROOT = "GameData/";

    private RelationshipCatalogReader() {
    }

    public static byte[] readApprovedCatalog(String fileName) throws IOException {
        if (fileName == null || fileName.isEmpty() ||
                fileName.indexOf('/') >= 0 || fileName.indexOf('\\') >= 0) {
            throw new IOException("Invalid relationship catalog file name.");
        }

        AssetManager assets = UnityPlayer.currentActivity.getAssets();
        try (InputStream input = assets.open(CATALOG_ROOT + fileName, AssetManager.ACCESS_STREAMING);
             ByteArrayOutputStream output = new ByteArrayOutputStream(MAXIMUM_CATALOG_BYTES)) {
            byte[] buffer = new byte[4096];
            int total = 0;
            int read;
            while ((read = input.read(buffer)) != -1) {
                total += read;
                if (total > MAXIMUM_CATALOG_BYTES) {
                    throw new IOException("Relationship catalog exceeds the bounded size.");
                }
                output.write(buffer, 0, read);
            }
            return output.toByteArray();
        }
    }
}