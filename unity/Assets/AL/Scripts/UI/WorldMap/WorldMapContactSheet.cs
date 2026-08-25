using System.IO;
using UnityEngine;

namespace AL.UI.WorldMap
{
    /// <summary>
    /// Software contact sheet of the same chrome topology the overlay draws.
    /// Used for EditMode screenshot evidence when PlayMode is unavailable.
    /// </summary>
    public static class WorldMapContactSheet
    {
        public static Texture2D Render(WorldMapPresentation presentation, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "WorldMapContactSheet_TEMPORARY",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Fill(texture, new Color(0.035f, 0.038f, 0.048f, 1f));
            DrawFrame(texture, new Color(0.78f, 0.68f, 0.42f, 0.7f));

            int mapX = Mathf.RoundToInt(width * 0.08f);
            int mapY = Mathf.RoundToInt(height * 0.1f);
            int mapW = Mathf.RoundToInt(width * 0.84f);
            int mapH = Mathf.RoundToInt(height * 0.76f);
            FillRect(texture, mapX, mapY, mapW, mapH, new Color(0.04f, 0.045f, 0.055f, 1f));

            for (int i = 0; i < presentation.Inners.Count; i++)
            {
                WorldMapInnerRealm inner = presentation.Inners[i];
                DrawLand(texture, mapX, mapY, mapW, mapH, inner);
                DrawWall(texture, mapX, mapY, mapW, mapH, inner);
                DrawDot(texture, mapX, mapY, mapW, mapH, inner.Capital.Uv, 8, new Color(0.92f, 0.78f, 0.42f));
                DrawDot(texture, mapX, mapY, mapW, mapH, inner.OutpostA.Uv, 4, new Color(0.78f, 0.76f, 0.7f));
                DrawDot(texture, mapX, mapY, mapW, mapH, inner.OutpostB.Uv, 4, new Color(0.78f, 0.76f, 0.7f));
            }

            WorldMapUv isle = presentation.AccordantIsle.Uv;
            DrawDot(texture, mapX, mapY, mapW, mapH, isle, 16, new Color(0.22f, 0.24f, 0.28f));
            texture.Apply();
            return texture;
        }

        public static string WritePng(WorldMapPresentation presentation, string absolutePath)
        {
            Texture2D texture = Render(presentation, 1600, 900);
            byte[] png = texture.EncodeToPNG();
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(absolutePath, png);
            Object.DestroyImmediate(texture);
            return absolutePath;
        }

        private static void DrawLand(Texture2D texture, int mapX, int mapY, int mapW, int mapH, WorldMapInnerRealm inner)
        {
            bool west = inner.Capital.Uv.X < 0.5f;
            bool south = inner.Capital.Uv.Y < 0.5f;
            float x0 = west ? 0.01f : 0.68f;
            float x1 = west ? 0.32f : 0.99f;
            float y0 = south ? 0.01f : 0.68f;
            float y1 = south ? 0.32f : 0.99f;
            int px0 = mapX + Mathf.RoundToInt(x0 * mapW);
            int py0 = mapY + Mathf.RoundToInt(y0 * mapH);
            int px1 = mapX + Mathf.RoundToInt(x1 * mapW);
            int py1 = mapY + Mathf.RoundToInt(y1 * mapH);
            FillRect(texture, px0, py0, px1 - px0, py1 - py0, LandColor(inner.RealmId));
        }

        private static void DrawWall(Texture2D texture, int mapX, int mapY, int mapW, int mapH, WorldMapInnerRealm inner)
        {
            int x0 = mapX + Mathf.RoundToInt(inner.WallFrom.X * mapW);
            int y0 = mapY + Mathf.RoundToInt(inner.WallFrom.Y * mapH);
            int x1 = mapX + Mathf.RoundToInt(inner.WallTo.X * mapW);
            int y1 = mapY + Mathf.RoundToInt(inner.WallTo.Y * mapH);
            DrawLine(texture, x0, y0, x1, y1, new Color(0.42f, 0.55f, 0.72f, 1f));
        }

        private static void DrawDot(Texture2D texture, int mapX, int mapY, int mapW, int mapH, WorldMapUv uv, int radius, Color color)
        {
            int cx = mapX + Mathf.RoundToInt(uv.X * mapW);
            int cy = mapY + Mathf.RoundToInt(uv.Y * mapH);
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        Set(texture, cx + x, cy + y, color);
                    }
                }
            }
        }

        private static Color LandColor(string realmId)
        {
            switch (realmId)
            {
                case "stonehold":
                    return new Color(0.28f, 0.24f, 0.2f);
                case "eldergrove":
                    return new Color(0.16f, 0.26f, 0.18f);
                case "crownlands":
                    return new Color(0.26f, 0.26f, 0.2f);
                case "umbral":
                    return new Color(0.2f, 0.14f, 0.26f);
                default:
                    return new Color(0.2f, 0.2f, 0.22f);
            }
        }

        private static void DrawFrame(Texture2D texture, Color color)
        {
            int w = texture.width;
            int h = texture.height;
            FillRect(texture, 18, 18, w - 36, 6, color);
            FillRect(texture, 18, h - 24, w - 36, 6, color);
            FillRect(texture, 18, 18, 6, h - 36, color);
            FillRect(texture, w - 24, 18, 6, h - 36, color);
        }

        private static void Fill(Texture2D texture, Color color)
        {
            FillRect(texture, 0, 0, texture.width, texture.height, color);
        }

        private static void FillRect(Texture2D texture, int x, int y, int w, int h, Color color)
        {
            int x1 = Mathf.Min(texture.width, x + w);
            int y1 = Mathf.Min(texture.height, y + h);
            x = Mathf.Max(0, x);
            y = Mathf.Max(0, y);
            for (int py = y; py < y1; py++)
            {
                for (int px = x; px < x1; px++)
                {
                    texture.SetPixel(px, py, color);
                }
            }
        }

        private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            while (true)
            {
                for (int t = -2; t <= 2; t++)
                {
                    Set(texture, x0, y0 + t, color);
                    Set(texture, x0 + t, y0, color);
                }

                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private static void Set(Texture2D texture, int x, int y, Color color)
        {
            if (x >= 0 && y >= 0 && x < texture.width && y < texture.height)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }
}
