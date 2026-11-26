using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NFWL
{
    /// <summary>
    /// Crystal element of a Crystalia player (race design sheet: 結晶のエネルギーを宿す種族).
    /// Each element sets the colour of the crystals, hair, eyes and skin tint.
    /// </summary>
    public struct Element
    {
        public string name;
        public Color crystal, hair, eye, skin;
        public bool crystalSkin; // whole skin is faceted crystal (Violet design)

        public Element(string name, Color crystal, Color hair, Color eye, Color skin, bool crystalSkin = false)
        {
            this.name = name; this.crystal = crystal; this.hair = hair; this.eye = eye; this.skin = skin; this.crystalSkin = crystalSkin;
        }

        // Based on the six character designs.
        public static readonly Element Ice = new Element("ICE", new Color(0.62f, 0.75f, 1f), new Color(0.86f, 0.88f, 0.96f), new Color(0.55f, 0.5f, 1f), new Color(0.95f, 0.9f, 0.94f));
        public static readonly Element Sapphire = new Element("SAPPHIRE", new Color(0.28f, 0.38f, 1f), new Color(0.16f, 0.16f, 0.42f), new Color(0.4f, 0.55f, 1f), new Color(0.92f, 0.84f, 0.86f));
        public static readonly Element Amethyst = new Element("AMETHYST", new Color(0.78f, 0.62f, 1f), new Color(0.88f, 0.84f, 0.97f), new Color(0.72f, 0.42f, 1f), new Color(0.93f, 0.86f, 0.9f));
        public static readonly Element Emerald = new Element("EMERALD", new Color(0.18f, 0.92f, 0.52f), new Color(0.58f, 0.9f, 0.76f), new Color(0.2f, 0.85f, 0.5f), new Color(0.93f, 0.85f, 0.8f));
        public static readonly Element Gold = new Element("GOLD", new Color(1f, 0.68f, 0.22f), new Color(0.98f, 0.82f, 0.42f), new Color(1f, 0.65f, 0.2f), new Color(0.96f, 0.8f, 0.66f));
        public static readonly Element Violet = new Element("VIOLET", new Color(0.68f, 0.32f, 1f), new Color(0.82f, 0.74f, 0.96f), new Color(0.88f, 0.32f, 1f), new Color(0.86f, 0.78f, 0.96f), true);
    }

    /// <summary>Kit colours (design sheet: HOME black, AWAY white, THIRD purple).</summary>
    public struct KitStyle
    {
        public Color body, trim, print, boots;
        public KitStyle(Color body, Color trim, Color print, Color boots) { this.body = body; this.trim = trim; this.print = print; this.boots = boots; }

        public static readonly KitStyle Home = new KitStyle(new Color(0.06f, 0.06f, 0.09f), new Color(0.95f, 0.76f, 0.3f), Color.white, new Color(0.85f, 0.86f, 0.92f));
        public static readonly KitStyle Away = new KitStyle(new Color(0.9f, 0.91f, 0.95f), new Color(0.12f, 0.12f, 0.28f), new Color(0.12f, 0.12f, 0.28f), new Color(0.85f, 0.86f, 0.92f));
        public static readonly KitStyle Third = new KitStyle(new Color(0.34f, 0.12f, 0.58f), new Color(0.85f, 0.85f, 0.95f), Color.white, new Color(0.85f, 0.86f, 0.92f));
        public static readonly KitStyle AwayKeeper = new KitStyle(new Color(0.08f, 0.45f, 0.42f), new Color(0.9f, 0.9f, 0.95f), Color.white, new Color(0.1f, 0.1f, 0.12f));
    }

    /// <summary>Crystal materials, faceted textures, printed kit panels and the shard mesh.</summary>
    public static class CrystalArt
    {
        /// <summary>Element preset by name (ICE, SAPPHIRE, AMETHYST, EMERALD, GOLD, VIOLET), as sent by the web platform.</summary>
        public static bool TryElement(string name, out Element element)
        {
            switch ((name ?? "").Trim().ToUpperInvariant())
            {
                case "ICE": element = Element.Ice; return true;
                case "SAPPHIRE": element = Element.Sapphire; return true;
                case "AMETHYST": element = Element.Amethyst; return true;
                case "EMERALD": element = Element.Emerald; return true;
                case "GOLD": element = Element.Gold; return true;
                case "VIOLET": element = Element.Violet; return true;
                default: element = Element.Ice; return false;
            }
        }

        static Mesh shard;
        static Texture2D facets;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> panels = new Dictionary<string, Texture2D>();

        /// <summary>A long six-sided bipyramid: the basic crystal shard (flat shaded).</summary>
        public static Mesh Shard
        {
            get
            {
                if (shard != null) return shard;
                const int sides = 6;
                Vector3 top = new Vector3(0f, 1f, 0f);
                Vector3 bottom = new Vector3(0f, -0.25f, 0f);
                var ring = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    ring[i] = new Vector3(Mathf.Cos(a) * 0.18f, 0f, Mathf.Sin(a) * 0.18f);
                }
                var verts = new List<Vector3>();
                var tris = new List<int>();
                void Tri(Vector3 a, Vector3 b, Vector3 c)
                {
                    int n = verts.Count;
                    verts.Add(a); verts.Add(b); verts.Add(c);
                    tris.Add(n); tris.Add(n + 1); tris.Add(n + 2);
                }
                for (int i = 0; i < sides; i++)
                {
                    Vector3 r0 = ring[i], r1 = ring[(i + 1) % sides];
                    Tri(top, r1, r0);
                    Tri(bottom, r0, r1);
                    // Back faces too, so the crystal looks solid whichever way the winding is read.
                    Tri(top, r0, r1);
                    Tri(bottom, r1, r0);
                }
                shard = new Mesh { name = "CrystalShard" };
                shard.SetVertices(verts);
                shard.SetTriangles(tris, 0);
                shard.RecalculateNormals();
                shard.RecalculateBounds();
                return shard;
            }
        }

        /// <summary>Glowing, slightly see-through crystal.</summary>
        public static Material Crystal(Color c)
        {
            string key = "crystal" + c;
            if (mats.TryGetValue(key, out Material m) && m != null) return m;
            m = Glow(new Color(Mathf.Lerp(c.r, 1f, 0.35f), Mathf.Lerp(c.g, 1f, 0.35f), Mathf.Lerp(c.b, 1f, 0.35f), 0.78f), c, 0.9f, 0.95f, null);
            MakeTransparent(m);
            mats[key] = m;
            return m;
        }

        /// <summary>An opaque material that glows in its own colour.</summary>
        public static Material Glow(Color albedo, Color emission, float strength, float smoothness, Texture2D tex)
        {
            string key = $"glow{albedo}{emission}{strength}{smoothness}{(tex != null ? tex.GetHashCode() : 0)}";
            if (mats.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(Visuals.Mat(Color.white));
            m.color = albedo;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.1f);
            if (tex != null) m.mainTexture = tex;
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission * strength);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            mats[key] = m;
            return m;
        }

        /// <summary>Metallic boots (silver or gold).</summary>
        public static Material Metal(Color c)
        {
            string key = "metal" + c;
            if (mats.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(Visuals.Mat(Color.white));
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.85f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.8f);
            mats[key] = m;
            return m;
        }

        /// <summary>Faceted skin: crystal plates tinted by the element.</summary>
        public static Material CrystalSkin(Color skin, Color crystal)
        {
            Color tint = Color.Lerp(skin, crystal, 0.35f);
            return Glow(tint, crystal, 0.25f, 0.8f, Facets);
        }

        static void MakeTransparent(Material m)
        {
            // Standard shader "Transparent" mode, set up by hand.
            if (!m.HasProperty("_Mode")) return;
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        /// <summary>Greyscale crystal facets (Voronoi cells with bright edges), tileable.</summary>
        public static Texture2D Facets
        {
            get
            {
                if (facets != null) return facets;
                const int n = 128;
                facets = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                var rnd = new System.Random(5);
                const int seeds = 26;
                var pts = new Vector2[seeds];
                var shade = new float[seeds];
                for (int i = 0; i < seeds; i++)
                {
                    pts[i] = new Vector2((float)rnd.NextDouble() * n, (float)rnd.NextDouble() * n);
                    shade[i] = 0.62f + (float)rnd.NextDouble() * 0.38f;
                }
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d1 = float.MaxValue, d2 = float.MaxValue;
                    int best = 0;
                    for (int i = 0; i < seeds; i++)
                    {
                        float dx = Mathf.Abs(x - pts[i].x); dx = Mathf.Min(dx, n - dx);
                        float dy = Mathf.Abs(y - pts[i].y); dy = Mathf.Min(dy, n - dy);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d < d1) { d2 = d1; d1 = d; best = i; }
                        else if (d < d2) d2 = d;
                    }
                    float v = shade[best] + (x + y) % 37 * 0.002f;
                    if (d2 - d1 < 1.2f) v = 1f; // bright crystal edge
                    px[y * n + x] = new Color(v, v, v, 1f);
                }
                facets.SetPixels(px);
                facets.Apply(true);
                return facets;
            }
        }

        // -------------------------------------------------------------------
        // Printed kit panels: crystal pattern + "NFWL" + number
        // -------------------------------------------------------------------

        /// <summary>Shirt front (NFWL + number) or back (big number) or shorts (small number).</summary>
        public static Texture2D KitPanel(KitStyle kit, Color accent, int number, int kind)
        {
            string key = $"{kit.body}{accent}{number}{kind}";
            if (panels.TryGetValue(key, out Texture2D t) && t != null) return t;

            const int w = 96, h = 120;
            t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = kit.body;

            // Crystal shards across the kit, in the player's element colour.
            var rnd = new System.Random(number * 31 + kind);
            for (int s = 0; s < 7; s++)
            {
                bool left = s % 2 == 0;
                float bx = left ? rnd.Next(0, 26) : rnd.Next(w - 26, w);
                float by = rnd.Next(0, h);
                Vector2 a = new Vector2(bx, by);
                Vector2 b = a + new Vector2((left ? 1 : -1) * rnd.Next(10, 28), rnd.Next(18, 46));
                Vector2 c = a + new Vector2((left ? 1 : -1) * rnd.Next(2, 10), rnd.Next(-30, 0));
                float k = 0.55f + (float)rnd.NextDouble() * 0.45f;
                FillTri(px, w, h, a, b, c, Color.Lerp(kit.body, accent, k));
            }
            // A diagonal sash.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = (x - y * 0.55f) - 8f;
                if (d > 0f && d < 5f) px[y * w + x] = Color.Lerp(px[y * w + x], kit.trim, 0.85f);
            }
            // Trim at the top (collar line).
            for (int y = h - 4; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = kit.trim;

            string num = number.ToString();
            if (kind == 0)
            {
                PixelFont.Draw(px, w, h, "NFWL", w / 2, 86, 3, kit.print);
                PixelFont.Draw(px, w, h, num, w / 2, 40, 5, kit.print);
            }
            else if (kind == 1)
            {
                PixelFont.Draw(px, w, h, "NFWL", w / 2, 96, 2, kit.print);
                PixelFont.Draw(px, w, h, num, w / 2, 48, 8, kit.print);
            }
            else
            {
                PixelFont.Draw(px, w, h, num, w / 2, 60, 6, kit.print);
            }
            t.SetPixels(px);
            t.Apply(true);
            panels[key] = t;
            return t;
        }

        static void FillTri(Color[] px, int w, int h, Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0;
                bool pos = d1 > 0 || d2 > 0 || d3 > 0;
                if (!(neg && pos)) px[y * w + x] = col;
            }
        }

        static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }

    /// <summary>Tiny 5x7 pixel font for kit numbers and the NFWL logo.</summary>
    public static class PixelFont
    {
        static readonly Dictionary<char, string[]> glyphs = new Dictionary<char, string[]>
        {
            { '0', new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" } },
            { '1', new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" } },
            { '2', new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" } },
            { '3', new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" } },
            { '4', new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" } },
            { '5', new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" } },
            { '6', new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" } },
            { '7', new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" } },
            { '8', new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" } },
            { '9', new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" } },
            { 'N', new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" } },
            { 'F', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" } },
            { 'W', new[] { "10001", "10001", "10001", "10101", "10101", "11011", "10001" } },
            { 'L', new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" } },
        };

        /// <summary>Draws text centred at (cx, cy) in pixel coordinates (y up).</summary>
        public static void Draw(Color[] px, int w, int h, string text, int cx, int cy, int scale, Color col)
        {
            int width = (text.Length * 6 - 1) * scale;
            int x0 = cx - width / 2;
            int y0 = cy - 7 * scale / 2;
            for (int i = 0; i < text.Length; i++)
            {
                if (!glyphs.TryGetValue(text[i], out string[] g)) continue;
                for (int r = 0; r < 7; r++)
                for (int c = 0; c < 5; c++)
                {
                    if (g[r][c] != '1') continue;
                    for (int sy = 0; sy < scale; sy++)
                    for (int sx = 0; sx < scale; sx++)
                    {
                        int x = x0 + (i * 6 + c) * scale + sx;
                        int y = y0 + (6 - r) * scale + sy;
                        if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = col;
                    }
                }
            }
        }
    }
}
