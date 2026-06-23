using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    internal static class BootstrapVisualResources
    {
        public const float SurfaceRadius = 8f;

        private static readonly Dictionary<string, Texture2D> TextureCache =
            new Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);

        public static Color DeepBackground
        {
            get { return new Color(0.012f, 0.020f, 0.035f, 1f); }
        }

        public static Color MainPanel
        {
            get { return new Color(23f / 255f, 32f / 255f, 39f / 255f, 0.72f); }
        }

        public static Color NestedSurface
        {
            get { return new Color(32f / 255f, 47f / 255f, 56f / 255f, 0.62f); }
        }

        public static Color HeaderPanel
        {
            get { return new Color(35f / 255f, 52f / 255f, 61f / 255f, 0.68f); }
        }

        public static Color Border
        {
            get { return new Color(90f / 255f, 111f / 255f, 160f / 255f, 0.35f); }
        }

        public static Color SubtleBorder
        {
            get { return new Color(90f / 255f, 111f / 255f, 160f / 255f, 0.24f); }
        }

        public static Color InteractiveBorder
        {
            get { return new Color(59f / 255f, 166f / 255f, 154f / 255f, 0.55f); }
        }

        public static Color Text
        {
            get { return new Color(0.88f, 0.93f, 0.96f, 1f); }
        }

        public static Color MutedText
        {
            get { return new Color(0.58f, 0.68f, 0.75f, 1f); }
        }

        public static Color Teal
        {
            get { return new Color(0.28f, 0.82f, 0.74f, 1f); }
        }

        public static Color Blue
        {
            get { return new Color(0.30f, 0.54f, 0.80f, 1f); }
        }

        public static Color Amber
        {
            get { return new Color(0.86f, 0.66f, 0.30f, 1f); }
        }

        public static Color Red
        {
            get { return new Color(0.74f, 0.34f, 0.32f, 1f); }
        }

        public static Texture2D TextureForColor(string name, Color color)
        {
            string key = name + "-" + ColorUtility.ToHtmlStringRGBA(color);

            if (TextureCache.TryGetValue(key, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "Deucarian Bootstrap " + name
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            TextureCache[key] = texture;
            return texture;
        }

        public static Texture2D CreateFallbackLogoTexture(int size = 128)
        {
            size = Mathf.Clamp(size, 32, 512);
            string key = "fallback-logo-" + size;

            if (TextureCache.TryGetValue(key, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "Deucarian Bootstrap Fallback Logo",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outerRadius = size * 0.45f;
            float innerRadius = size * 0.28f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    float distance = Vector2.Distance(point, center);
                    float diamond = Mathf.Abs(point.x - center.x) + Mathf.Abs(point.y - center.y);
                    Color color = new Color(0f, 0f, 0f, 0f);

                    if (distance <= outerRadius)
                    {
                        float edge = Mathf.InverseLerp(outerRadius, outerRadius - 8f, distance);
                        color = Color.Lerp(new Color(0.10f, 0.18f, 0.23f, 0.92f), new Color(0.18f, 0.40f, 0.42f, 0.96f), edge);
                    }

                    if (diamond <= innerRadius)
                    {
                        float accent = Mathf.InverseLerp(innerRadius, 0f, diamond);
                        color = Color.Lerp(color, new Color(0.36f, 0.88f, 0.82f, 1f), Mathf.Clamp01(0.45f + accent * 0.35f));
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply(false, true);
            TextureCache[key] = texture;
            return texture;
        }

        public static Texture2D CreateFallbackWallpaperTexture(int width = 512, int height = 320)
        {
            width = Mathf.Clamp(width, 64, 1024);
            height = Mathf.Clamp(height, 64, 1024);
            string key = "fallback-wallpaper-" + width + "x" + height;

            if (TextureCache.TryGetValue(key, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "Deucarian Bootstrap Fallback Wallpaper",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Vector2 tealGlow = new Vector2(width * 0.18f, height * 0.72f);
            Vector2 blueGlow = new Vector2(width * 0.82f, height * 0.20f);
            Vector2 silverGlow = new Vector2(width * 0.58f, height * 0.48f);
            float diagonalScale = Mathf.Max(1f, Mathf.Sqrt(width * width + height * height));

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float vertical = (float)y / Mathf.Max(1, height - 1);
                    float horizontal = (float)x / Mathf.Max(1, width - 1);
                    Color color = Color.Lerp(new Color(0.015f, 0.026f, 0.040f, 1f), new Color(0.045f, 0.075f, 0.090f, 1f), vertical * 0.55f + horizontal * 0.18f);

                    color += Glow(new Vector2(x, y), tealGlow, diagonalScale * 0.32f, new Color(0.03f, 0.34f, 0.32f, 0.42f));
                    color += Glow(new Vector2(x, y), blueGlow, diagonalScale * 0.30f, new Color(0.04f, 0.20f, 0.46f, 0.34f));
                    color += Glow(new Vector2(x, y), silverGlow, diagonalScale * 0.42f, new Color(0.15f, 0.20f, 0.24f, 0.18f));

                    uint noise = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ 0x9E3779B9u;
                    noise ^= noise >> 13;
                    float grain = ((noise & 0xFFu) / 255f - 0.5f) * 0.025f;
                    color.r = Mathf.Clamp01(color.r + grain);
                    color.g = Mathf.Clamp01(color.g + grain);
                    color.b = Mathf.Clamp01(color.b + grain);
                    color.a = 1f;
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply(false, true);
            TextureCache[key] = texture;
            return texture;
        }

        public static void DrawWindowBackdrop(Rect rect, Texture2D wallpaper, Color fallbackColor)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            EditorGUI.DrawRect(rect, fallbackColor);
            Texture2D resolvedWallpaper = wallpaper != null ? wallpaper : CreateFallbackWallpaperTexture();

            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, wallpaper != null ? 0.64f : 0.78f);
            GUI.DrawTexture(rect, resolvedWallpaper, ScaleMode.ScaleAndCrop, true);
            GUI.color = previousColor;

            EditorGUI.DrawRect(rect, new Color(0.004f, 0.012f, 0.024f, 0.35f));
            DrawSoftGlow(new Rect(rect.x - rect.width * 0.10f, rect.y + rect.height * 0.46f, rect.width * 0.46f, rect.height * 0.36f), new Color(0.05f, 0.42f, 0.38f, 0.13f));
            DrawSoftGlow(new Rect(rect.x + rect.width * 0.58f, rect.y + rect.height * 0.08f, rect.width * 0.44f, rect.height * 0.32f), new Color(0.07f, 0.27f, 0.55f, 0.10f));
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.08f));
        }

        public static void DrawFrostedSurface(Rect rect, Color backgroundColor, Color borderColor)
        {
            DrawRoundedSurface(rect, backgroundColor, borderColor, SurfaceRadius, true);
        }

        public static void DrawInsetSurface(Rect rect, Color backgroundColor, Color borderColor, float radius)
        {
            DrawRoundedSurface(rect, backgroundColor, borderColor, radius, false);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static Color Glow(Vector2 point, Vector2 center, float radius, Color color)
        {
            float distance = Vector2.Distance(point, center);
            float amount = 1f - Mathf.SmoothStep(radius * 0.15f, radius, distance);
            return color * Mathf.Clamp01(amount);
        }

        private static void DrawSoftGlow(Rect rect, Color color)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            const int steps = 8;
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / Mathf.Max(1, steps - 1);
                Color stepColor = color;
                stepColor.a *= (1f - t) * 0.18f;
                Rect stepRect = new Rect(
                    Mathf.Lerp(rect.x, rect.center.x, t * 0.58f),
                    Mathf.Lerp(rect.y, rect.center.y, t * 0.58f),
                    rect.width * (1f - t * 0.58f),
                    rect.height * (1f - t * 0.58f));
                EditorGUI.DrawRect(stepRect, stepColor);
            }
        }

        private static void DrawRoundedSurface(
            Rect rect,
            Color backgroundColor,
            Color borderColor,
            float radius,
            bool drawShadow)
        {
            Event currentEvent = Event.current;

            if (currentEvent == null || currentEvent.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Rect alignedRect = AlignToPixels(rect);
            radius = Mathf.Min(radius, Mathf.Min(alignedRect.width, alignedRect.height) * 0.5f);

            if (drawShadow)
            {
                Rect shadowRect = new Rect(
                    alignedRect.x + 1f,
                    alignedRect.y + 2f,
                    alignedRect.width,
                    alignedRect.height);
                DrawRoundedFill(shadowRect, radius, new Color(0f, 0f, 0f, 0.18f));
            }

            DrawRoundedFill(alignedRect, radius, borderColor);

            Rect innerRect = new Rect(
                alignedRect.x + 1f,
                alignedRect.y + 1f,
                Mathf.Max(0f, alignedRect.width - 2f),
                Mathf.Max(0f, alignedRect.height - 2f));
            DrawRoundedFill(innerRect, Mathf.Max(0f, radius - 1f), backgroundColor);

            if (innerRect.width > 8f && innerRect.height > 2f)
            {
                DrawRoundedFill(
                    new Rect(innerRect.x + radius, innerRect.y, Mathf.Max(0f, innerRect.width - radius * 2f), 1f),
                    0f,
                    new Color(0.75f, 0.94f, 1f, 0.08f));
            }
        }

        private static Rect AlignToPixels(Rect rect)
        {
            return new Rect(
                Mathf.Floor(rect.x),
                Mathf.Floor(rect.y),
                Mathf.Ceil(rect.width),
                Mathf.Ceil(rect.height));
        }

        private static void DrawRoundedFill(Rect rect, float radius, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f || color.a <= 0f)
            {
                return;
            }

            radius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);

            if (radius < 1f)
            {
                EditorGUI.DrawRect(rect, color);
                return;
            }

            float middleWidth = Mathf.Max(0f, rect.width - radius * 2f);
            float middleHeight = Mathf.Max(0f, rect.height - radius * 2f);

            if (middleWidth > 0f)
            {
                EditorGUI.DrawRect(new Rect(rect.x + radius, rect.y, middleWidth, rect.height), color);
            }

            if (middleHeight > 0f)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + radius, radius, middleHeight), color);
                EditorGUI.DrawRect(new Rect(rect.xMax - radius, rect.y + radius, radius, middleHeight), color);
            }

            int rows = Mathf.CeilToInt(radius);
            float radiusSquared = radius * radius;

            for (int row = 0; row < rows; row++)
            {
                float sample = radius - row - 0.5f;
                float inset = radius - Mathf.Sqrt(Mathf.Max(0f, radiusSquared - sample * sample));
                float width = rect.width - inset * 2f;

                if (width <= 0f)
                {
                    continue;
                }

                EditorGUI.DrawRect(new Rect(rect.x + inset, rect.y + row, width, 1f), color);
                EditorGUI.DrawRect(new Rect(rect.x + inset, rect.yMax - row - 1f, width, 1f), color);
            }
        }
    }
}
