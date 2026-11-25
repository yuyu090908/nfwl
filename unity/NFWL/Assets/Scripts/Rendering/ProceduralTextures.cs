using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// v0.7 procedural texture maps (generated once at start, no image files in the project):
    /// noise for turf colour, normal maps for turf, knitted shirt fabric, boot leather and concrete,
    /// and soft radial sprites for glows and contact shadows.
    /// Normal maps are written as linear RGB with alpha 1, which Unity's UnpackNormal reads on every platform.
    /// </summary>
    public static class ProceduralTextures
    {
        static Texture2D noise, turfNormal, fabricNormal, leatherNormal, concreteNormal, glow, fabricMask;

        static Texture2D New(int size, bool linear, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            {
                wrapMode = wrap,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
        }

        /// <summary>Three octaves of tileable value noise in R, G and B (turf colour variation).</summary>
        public static Texture2D Noise
        {
            get
            {
                if (noise != null) return noise;
                const int n = 256;
                noise = New(n, true);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = TileNoise(x, y, n, 8, 1);
                    float g = TileNoise(x, y, n, 24, 2);
                    float b = TileNoise(x, y, n, 96, 3);
                    px[y * n + x] = new Color(r, g, b, 1f);
                }
                noise.SetPixels(px);
                noise.Apply(true);
                return noise;
            }
        }

        /// <summary>Tileable smooth value noise (0..1) with the given number of cells across the texture.</summary>
        public static float TileNoise(int x, int y, int size, int cells, int seed)
        {
            float fx = x / (float)size * cells, fy = y / (float)size * cells;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Hash((x0) % cells, (y0) % cells, seed);
            float b = Hash((x0 + 1) % cells, (y0) % cells, seed);
            float c = Hash((x0) % cells, (y0 + 1) % cells, seed);
            float d = Hash((x0 + 1) % cells, (y0 + 1) % cells, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// <summary>Builds a tangent-space normal map from a height function (central differences).</summary>
        static Texture2D NormalFromHeight(int n, System.Func<int, int, float> height, float strength)
        {
            Texture2D t = New(n, true);
            var h = new float[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) h[y * n + x] = height(x, y);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float l = h[y * n + (x + n - 1) % n], r = h[y * n + (x + 1) % n];
                float d = h[((y + n - 1) % n) * n + x], u = h[((y + 1) % n) * n + x];
                Vector3 nn = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                px[y * n + x] = new Color(nn.x * 0.5f + 0.5f, nn.y * 0.5f + 0.5f, nn.z * 0.5f + 0.5f, 1f);
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Short grass blades: many thin, mostly upright strokes.</summary>
        public static Texture2D TurfNormal
        {
            get
            {
                if (turfNormal != null) return turfNormal;
                const int n = 256;
                turfNormal = NormalFromHeight(n, (x, y) =>
                {
                    // Elongated cells (blades) with random height, plus fine grain.
                    int cx = x / 3, cy = y / 9;
                    float blade = Hash(cx, (y + cx * 5) / 9, 11);
                    float within = Mathf.Sin(((y + cx * 5) % 9) / 9f * Mathf.PI);
                    return blade * within * 0.8f + TileNoise(x, y, n, 64, 12) * 0.4f;
                }, 2.2f);
                return turfNormal;
            }
        }

        /// <summary>Knitted jersey fabric: fine rows of loops, soft creases (folds) and a seam band at the
        /// UV edges, so shirts show weave, folds and stitching under the floodlights.</summary>
        public static Texture2D FabricNormal
        {
            get
            {
                if (fabricNormal != null) return fabricNormal;
                const int n = 256;
                fabricNormal = NormalFromHeight(n, (x, y) =>
                {
                    float knitX = Mathf.Abs(Mathf.Sin(x * Mathf.PI / 4f + ((y / 4) % 2) * Mathf.PI * 0.5f));
                    float knitY = Mathf.Abs(Mathf.Sin(y * Mathf.PI / 4f));
                    float knit = knitX * knitY * 0.35f;
                    float folds = TileNoise(x, y, n, 4, 21) * 2.2f + TileNoise(x, y, n, 9, 22) * 0.8f;
                    // Seams: a raised double stitch line near u = 0 and v = 0.
                    float seam = 0f;
                    int sx = Mathf.Min(x, n - x), sy = Mathf.Min(y, n - y);
                    if (sx < 4) seam = (sx % 2 == 0 ? 0.6f : 0.2f);
                    if (sy < 4) seam = Mathf.Max(seam, sy % 2 == 0 ? 0.6f : 0.2f);
                    return knit + folds + seam;
                }, 1.6f);
                return fabricNormal;
            }
        }

        /// <summary>Fabric smoothness mask (alpha): stitches and prints are glossier than the knit.</summary>
        public static Texture2D FabricMask
        {
            get
            {
                if (fabricMask != null) return fabricMask;
                const int n = 64;
                fabricMask = New(n, true);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = 0.75f + TileNoise(x, y, n, 8, 31) * 0.25f;
                    px[y * n + x] = new Color(1f, 1f, 1f, v);
                }
                fabricMask.SetPixels(px);
                fabricMask.Apply(true);
                return fabricMask;
            }
        }

        /// <summary>Boot leather / synthetic upper: fine pebbled grain.</summary>
        public static Texture2D LeatherNormal
        {
            get
            {
                if (leatherNormal != null) return leatherNormal;
                const int n = 128;
                leatherNormal = NormalFromHeight(n, (x, y) => TileNoise(x, y, n, 32, 41) + TileNoise(x, y, n, 64, 42) * 0.5f, 1.2f);
                return leatherNormal;
            }
        }

        /// <summary>Concrete for stands and walls: coarse noise with panel joints.</summary>
        public static Texture2D ConcreteNormal
        {
            get
            {
                if (concreteNormal != null) return concreteNormal;
                const int n = 128;
                concreteNormal = NormalFromHeight(n, (x, y) =>
                {
                    float joint = (x % 64 < 2 || y % 64 < 2) ? -1f : 0f;
                    return TileNoise(x, y, n, 16, 51) * 0.6f + TileNoise(x, y, n, 48, 52) * 0.3f + joint;
                }, 1.5f);
                return concreteNormal;
            }
        }

        /// <summary>Soft round glow (white centre fading to black) for halos and light shafts.</summary>
        public static Texture2D Glow
        {
            get
            {
                if (glow != null) return glow;
                const int n = 64;
                glow = New(n, false, TextureWrapMode.Clamp);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    float v = d * d * d + Mathf.Pow(d, 12f) * 0.8f;
                    px[y * n + x] = new Color(v, v, v, 1f);
                }
                glow.SetPixels(px);
                glow.Apply(true);
                return glow;
            }
        }
    }
}
