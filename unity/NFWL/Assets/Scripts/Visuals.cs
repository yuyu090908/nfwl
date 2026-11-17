using System.Collections.Generic;
using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// Shared materials, procedural textures and primitive helpers.
    /// Everything is generated in code, so the project needs no downloaded art
    /// and stays small on disk.
    /// </summary>
    public static class Visuals
    {
        static Material baseMaterial;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static Material BaseMaterial
        {
            get
            {
                if (baseMaterial == null)
                {
                    // Borrow Unity's default material (works with whatever shader the project uses).
                    GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    baseMaterial = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
                    Object.DestroyImmediate(tmp);
                }
                return baseMaterial;
            }
        }

        /// <summary>A shared material of one colour (cached, so many parts share one draw state).</summary>
        public static Material Mat(Color color, float smoothness = 0.2f, Texture2D tex = null, Vector2? tiling = null)
        {
            Vector2 t = tiling ?? Vector2.one;
            string key = $"{color}_{smoothness}_{(tex != null ? tex.GetHashCode() : 0)}_{t}";
            if (cache.TryGetValue(key, out Material m) && m != null) return m;

            m = new Material(BaseMaterial);
            m.color = color;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (tex != null)
            {
                m.mainTexture = tex;
                m.mainTextureScale = t;
            }
            cache[key] = m;
            return m;
        }

        /// <summary>Creates a primitive without a collider, parented and placed in local space.</summary>
        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, string name = null, Quaternion? localRot = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            return go;
        }

        /// <summary>An empty pivot transform.</summary>
        public static Transform Pivot(Transform parent, Vector3 localPos, string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        // -------------------------------------------------------------------
        // Procedural textures
        // -------------------------------------------------------------------

        static Texture2D grass, ball, crowd, board;

        static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
        }

        /// <summary>Fine grass noise (greyscale, tinted by the material colour).</summary>
        public static Texture2D Grass
        {
            get
            {
                if (grass != null) return grass;
                const int n = 256;
                grass = NewTex(n, n);
                var px = new Color[n * n];
                var rnd = new System.Random(7);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.12f
                                  + Mathf.PerlinNoise(x * 0.5f + 100f, y * 0.5f) * 0.08f
                                  + (float)rnd.NextDouble() * 0.10f;
                    float v = 0.82f + noise;
                    px[y * n + x] = new Color(v, v, v, 1f);
                }
                grass.SetPixels(px);
                grass.Apply(true);
                return grass;
            }
        }

        /// <summary>Classic black-and-white football: 12 black patches at icosahedron vertices.</summary>
        public static Texture2D Ball
        {
            get
            {
                if (ball != null) return ball;
                const int w = 256, h = 128;
                ball = NewTex(w, h);
                ball.wrapMode = TextureWrapMode.Clamp;

                float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
                var verts = new List<Vector3>();
                foreach (var a in new[] { -1f, 1f })
                foreach (var b in new[] { -g, g })
                {
                    verts.Add(new Vector3(0f, a, b).normalized);
                    verts.Add(new Vector3(a, b, 0f).normalized);
                    verts.Add(new Vector3(b, 0f, a).normalized);
                }

                var px = new Color[w * h];
                float patch = Mathf.Cos(19f * Mathf.Deg2Rad);
                float seam = Mathf.Cos(21f * Mathf.Deg2Rad);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float lon = (x + 0.5f) / w * Mathf.PI * 2f;
                    float lat = ((y + 0.5f) / h - 0.5f) * Mathf.PI;
                    Vector3 d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    float best = -1f;
                    foreach (Vector3 v in verts) best = Mathf.Max(best, Vector3.Dot(d, v));
                    Color c = best > patch ? new Color(0.08f, 0.08f, 0.1f) : best > seam ? new Color(0.55f, 0.55f, 0.6f) : Color.white;
                    px[y * w + x] = c;
                }
                ball.SetPixels(px);
                ball.Apply(true);
                return ball;
            }
        }

        /// <summary>Crowd: rows of coloured fans on dark seats.</summary>
        public static Texture2D Crowd
        {
            get
            {
                if (crowd != null) return crowd;
                const int w = 256, h = 64;
                crowd = NewTex(w, h);
                var px = new Color[w * h];
                var rnd = new System.Random(11);
                Color seat = new Color(0.07f, 0.09f, 0.16f);
                for (int i = 0; i < px.Length; i++) px[i] = seat;

                Color[] shirts =
                {
                    new Color(0.15f, 0.45f, 0.95f), new Color(0.1f, 0.85f, 0.95f), Color.white,
                    new Color(0.9f, 0.15f, 0.2f), new Color(0.95f, 0.8f, 0.2f), new Color(0.3f, 0.3f, 0.35f)
                };
                Color[] skins = { new Color(0.96f, 0.8f, 0.66f), new Color(0.8f, 0.6f, 0.45f), new Color(0.5f, 0.35f, 0.25f) };

                for (int row = 0; row < h / 8; row++)
                for (int col = 0; col < w / 4; col++)
                {
                    if (rnd.NextDouble() < 0.12) continue; // empty seat
                    int bx = col * 4 + rnd.Next(0, 1);
                    int by = row * 8 + 1;
                    Color shirt = shirts[rnd.Next(shirts.Length)];
                    Color skin = skins[rnd.Next(skins.Length)];
                    for (int yy = 0; yy < 4; yy++) for (int xx = 0; xx < 3; xx++) Set(px, w, h, bx + xx, by + yy, shirt);
                    for (int yy = 4; yy < 6; yy++) for (int xx = 0; xx < 3; xx++) Set(px, w, h, bx + xx, by + yy, skin);
                }
                crowd.SetPixels(px);
                crowd.Apply(true);
                return crowd;
            }
        }

        /// <summary>Advertising board: navy panel with "NFWL" in cyan and a gold band (Master Doc UI colours).</summary>
        public static Texture2D Board
        {
            get
            {
                if (board != null) return board;
                const int w = 256, h = 64;
                board = NewTex(w, h);
                var px = new Color[w * h];
                Color navy = new Color(0.04f, 0.07f, 0.2f);
                Color cyan = new Color(0.1f, 0.85f, 1f);
                Color gold = new Color(1f, 0.78f, 0.2f);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = y < 6 || y > h - 7 ? gold : navy;

                // 5x7 block letters.
                string[] N = { "10001", "11001", "10101", "10011", "10001", "10001", "10001" };
                string[] F = { "11111", "10000", "10000", "11110", "10000", "10000", "10000" };
                string[] W = { "10001", "10001", "10001", "10101", "10101", "11011", "10001" };
                string[] L = { "10000", "10000", "10000", "10000", "10000", "10000", "11111" };
                string[][] word = { N, F, W, L };
                const int scale = 5;
                int startX = (w - (4 * 6 - 1) * scale) / 2;
                int startY = (h - 7 * scale) / 2;
                for (int li = 0; li < word.Length; li++)
                for (int r = 0; r < 7; r++)
                for (int c = 0; c < 5; c++)
                {
                    if (word[li][r][c] != '1') continue;
                    for (int sy = 0; sy < scale; sy++)
                    for (int sx = 0; sx < scale; sx++)
                        Set(px, w, h, startX + (li * 6 + c) * scale + sx, startY + (6 - r) * scale + sy, cyan);
                }
                board.SetPixels(px);
                board.Apply(true);
                return board;
            }
        }

        static void Set(Color[] px, int w, int h, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            px[y * w + x] = c;
        }
    }
}
