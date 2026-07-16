using UnityEngine;

namespace ColorBlocks.Presentation
{
    internal static class UiShapeFactory
    {
        private static Sprite _roundedRect;
        private static Sprite _restartIcon;

        public static Sprite RoundedRect
        {
            get
            {
                if (_roundedRect == null) _roundedRect = CreateRoundedRect(64, 15f);
                return _roundedRect;
            }
        }

        public static Sprite RestartIcon
        {
            get
            {
                if (_restartIcon == null) _restartIcon = CreateRestartIcon(96);
                return _restartIcon;
            }
        }

        private static Sprite CreateRoundedRect(int size, float radius)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime_RoundedRect",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color32[] pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            Vector2 half = Vector2.one * (size - 1) * 0.5f;
            Vector2 inner = half - Vector2.one * radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new(Mathf.Abs(x - center.x), Mathf.Abs(y - center.y));
                    Vector2 q = p - inner;
                    float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
                    float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
                    float distance = outside + inside - radius;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        private static Sprite CreateRestartIcon(int size)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime_RestartIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color32[] pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float radius = size * 0.27f;
            float halfThickness = size * 0.045f;
            Vector2 arrowTip = center + Polar(radius + size * 0.10f, 28f);
            Vector2 arrowA = center + Polar(radius - size * 0.14f, 12f);
            Vector2 arrowB = center + Polar(radius + size * 0.03f, 58f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new(x, y);
                    Vector2 delta = point - center;
                    float angle = Mathf.Repeat(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 360f, 360f);
                    float ringDistance = Mathf.Abs(delta.magnitude - radius) - halfThickness;
                    bool withinArc = angle >= 42f && angle <= 340f;
                    float ringAlpha = withinArc ? Mathf.Clamp01(0.75f - ringDistance) : 0f;
                    float triangleAlpha = PointInTriangle(point, arrowTip, arrowA, arrowB) ? 1f : 0f;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Max(ringAlpha, triangleAlpha) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Vector2 Polar(float radius, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        }

        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(point, a, b);
            float d2 = Sign(point, b, c);
            float d3 = Sign(point, c, a);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }
    }
}
