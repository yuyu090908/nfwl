using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NFWL
{
    /// <summary>
    /// v0.7 next-gen stadium (PHASE 9). The 42 m x 25 m pitch is unchanged; around it:
    /// LED advertising boards (animated), stands with concrete tiers, coloured seats, aisles with stairs,
    /// railings, a mid-level walkway with vomitories, cantilever roofs with steel trusses and a light strip,
    /// the players' tunnel, the outer facade, floodlight towers with lamp panels, halos and faint light
    /// shafts (decoration only: real lighting is StadiumLighting), and a low skyline in the fog.
    /// Static geometry is static-batched at the end so the whole stadium costs few draw calls.
    /// Crowd is built separately (CrowdBuilder) so its density can follow the graphics quality.
    /// </summary>
    public static class StadiumBuilder
    {
        public struct StandSpec
        {
            public string name;
            public Vector3 front;      // centre of the front edge (pitch side), local origin of the stand
            public float yaw;          // stand local +Z points away from the pitch
            public float length;
            public int tiers;
            public int walkwayTier;    // -1 = none
            public bool roof;
            public bool tunnel;
            public float homeShare;    // 0..1: fraction of home fans (end stands are one-sided)
        }

        public const float Rise = 0.5f, Depth = 0.85f, WalkwayDepth = 1.6f;

        static Material concrete, concreteLight, steel, roofMat, seatHome, seatAway, seatNeutral, dark, facade, glass, railMat;

        public static List<StandSpec> Stands { get; } = new List<StandSpec>();
        static Transform dynamicRoot;

        public static Transform Build(Transform parent)
        {
            Transform root = new GameObject("Stadium v0.7").transform;
            root.SetParent(parent, false);
            // Moving decoration (camera-facing halos) must stay out of static batching.
            dynamicRoot = new GameObject("Stadium v0.7 Dynamic").transform;
            dynamicRoot.SetParent(parent, false);
            LightShafts.Clear();
            CreateMaterials();
            Stands.Clear();

            float hl = PitchDims.HalfLength, hw = PitchDims.HalfWidth;
            float bz = hw + 2.2f, bx = hl + 3f;

            BuildBoards(root, bx, bz);

            // Far side (behind the play, the main TV stand), ends, and a low near side under the camera gantry.
            Stands.Add(new StandSpec { name = "Main Stand", front = new Vector3(0f, 0f, bz + 2.4f), yaw = 0f, length = 2f * bx + 8f, tiers = 12, walkwayTier = 5, roof = true, tunnel = true, homeShare = 0.6f });
            Stands.Add(new StandSpec { name = "Home End", front = new Vector3(-bx - 2.4f, 0f, 0f), yaw = -90f, length = 2f * bz + 6f, tiers = 9, walkwayTier = 4, roof = true, homeShare = 1f });
            Stands.Add(new StandSpec { name = "Away End", front = new Vector3(bx + 2.4f, 0f, 0f), yaw = 90f, length = 2f * bz + 6f, tiers = 9, walkwayTier = 4, roof = true, homeShare = 0f });
            Stands.Add(new StandSpec { name = "Gantry Side", front = new Vector3(0f, 0f, -bz - 2.4f), yaw = 180f, length = 2f * bx + 8f, tiers = 3, walkwayTier = -1, roof = false, homeShare = 0.5f });
            foreach (StandSpec s in Stands) BuildStand(root, s);

            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                BuildFloodlightTower(root, StadiumLighting.TowerPosition(sx, sz));

            BuildDugouts(root, hw);
            BuildSkyline(root);

            StaticBatchingUtility.Combine(root.gameObject);
            return root;
        }

        static void CreateMaterials()
        {
            concrete = NFWLShaders.PBR(new Color(0.36f, 0.37f, 0.4f), 0.18f, 0f, ProceduralTextures.ConcreteNormal, 0.6f, null, new Vector2(4f, 1f));
            concreteLight = NFWLShaders.PBR(new Color(0.52f, 0.53f, 0.56f), 0.2f, 0f, ProceduralTextures.ConcreteNormal, 0.6f, null, new Vector2(2f, 1f));
            steel = NFWLShaders.PBR(new Color(0.62f, 0.64f, 0.68f), 0.62f, 0.85f);
            railMat = NFWLShaders.PBR(new Color(0.75f, 0.76f, 0.8f), 0.7f, 0.9f);
            roofMat = NFWLShaders.PBR(new Color(0.12f, 0.13f, 0.17f), 0.4f, 0.5f);
            seatHome = NFWLShaders.PBR(new Color(0.08f, 0.08f, 0.1f), 0.55f);
            seatAway = NFWLShaders.PBR(new Color(0.82f, 0.83f, 0.88f), 0.55f);
            seatNeutral = NFWLShaders.PBR(new Color(0.85f, 0.66f, 0.18f), 0.55f);
            dark = NFWLShaders.PBR(new Color(0.03f, 0.03f, 0.04f), 0.1f);
            facade = NFWLShaders.PBR(new Color(0.2f, 0.22f, 0.28f), 0.35f, 0.3f, ProceduralTextures.ConcreteNormal, 0.4f, null, new Vector2(8f, 2f));
            glass = NFWLShaders.PBR(new Color(0.05f, 0.07f, 0.1f), 0.9f, 0.2f, null, 1f, null, null, new Color(0.35f, 0.42f, 0.6f));
        }

        static GameObject Box(Transform parent, Vector3 centre, Vector3 size, Material m, string name, float yaw = 0f, bool shadows = true)
        {
            GameObject go = Visuals.Part(PrimitiveType.Cube, parent, centre, size, m, name, Quaternion.Euler(0f, yaw, 0f));
            go.isStatic = true;
            Renderer r = go.GetComponent<Renderer>();
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static GameObject Rod(Transform parent, Vector3 a, Vector3 b, float thickness, Material m, string name)
        {
            Vector3 mid = (a + b) * 0.5f;
            float len = Vector3.Distance(a, b);
            GameObject go = Visuals.Part(PrimitiveType.Cylinder, parent, mid, new Vector3(thickness, len * 0.5f, thickness), m, name,
                Quaternion.FromToRotation(Vector3.up, (b - a).normalized));
            go.isStatic = true;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // ---------------- LED boards ----------------

        public static readonly List<Material> LedMaterials = new List<Material>();

        static void BuildBoards(Transform root, float bx, float bz)
        {
            LedMaterials.Clear();
            Material frame = NFWLShaders.PBR(new Color(0.04f, 0.045f, 0.06f), 0.5f, 0.2f);
            // Boards collide (the ball bounces back into view), same placement as v0.6.
            void Board(Vector3 c, Vector3 size, float yaw, float tiles)
            {
                GameObject b = Box(root, c, size, frame, "Boards", yaw);
                b.AddComponent<BoxCollider>();
                Material led = NFWLShaders.Simple(NFWLShaders.Additive, "LED Board");
                if (led == null) return;
                led.mainTexture = LedTexture;
                led.mainTextureScale = new Vector2(tiles, 1f);
                led.SetFloat("_Intensity", 1.35f);
                led.SetFloat("_ScrollX", 0.04f);
                LedMaterials.Add(led);
                // The screen faces the pitch.
                Vector3 inward;
                if (Mathf.Abs(c.x) > Mathf.Abs(c.z)) inward = new Vector3(-Mathf.Sign(c.x) * (size.x * 0.5f + 0.005f), 0f, 0f);
                else inward = new Vector3(0f, 0f, -Mathf.Sign(c.z) * (size.z * 0.5f + 0.005f));
                GameObject q = Visuals.Part(PrimitiveType.Quad, root, c + inward + Vector3.up * 0.04f,
                    new Vector3(Mathf.Max(size.x, size.z) - 0.1f, size.y - 0.22f, 1f), led, "LED",
                    Quaternion.LookRotation(-inward.normalized, Vector3.up));
                q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                q.GetComponent<Renderer>().receiveShadows = false;
            }
            Board(new Vector3(0f, 0.45f, bz), new Vector3(2f * bx, 0.9f, 0.12f), 0f, 6f);
            Board(new Vector3(0f, 0.45f, -bz), new Vector3(2f * bx, 0.9f, 0.12f), 0f, 6f);
            Board(new Vector3(bx, 0.45f, 0f), new Vector3(0.12f, 0.9f, 2f * bz), 0f, 3f);
            Board(new Vector3(-bx, 0.45f, 0f), new Vector3(0.12f, 0.9f, 2f * bz), 0f, 3f);
        }

        static Texture2D ledTex;

        /// <summary>Dot-matrix LED version of the NFWL board artwork (each LED a soft dot).</summary>
        static Texture2D LedTexture
        {
            get
            {
                if (ledTex != null) return ledTex;
                Texture2D src = Visuals.Board;
                int w = src.width * 2, h = src.height * 2;
                ledTex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 2 };
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = src.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
                    float dx = (x % 2) - 0.5f, dy = (y % 2) - 0.5f;
                    float dot = 1f - (dx * dx + dy * dy) * 0.6f;
                    px[y * w + x] = c * dot;
                }
                ledTex.SetPixels(px);
                ledTex.Apply(true);
                return ledTex;
            }
        }

        // ---------------- stands ----------------

        /// <summary>World position of a seat (row from the front, along = metres from the stand centre).</summary>
        public static Vector3 SeatPosition(StandSpec s, int row, float along)
        {
            Vector3 local = new Vector3(along, TierTop(s, row), TierZ(s, row) + Depth * 0.45f);
            return s.front + Quaternion.Euler(0f, s.yaw, 0f) * local;
        }

        public static float TierTop(StandSpec s, int row) => 0.9f + row * Rise;

        public static float TierZ(StandSpec s, int row)
        {
            float z = 0f;
            for (int i = 0; i < row; i++) z += i == s.walkwayTier ? WalkwayDepth : Depth;
            return z;
        }

        public static bool IsAisle(float along, float length)
        {
            float spacing = 9f;
            float k = (along + length * 0.5f) / spacing;
            float f = k - Mathf.Round(k);
            return Mathf.Abs(f * spacing) < 0.55f && Mathf.Abs(along) < length * 0.5f - 1f;
        }

        public static bool IsTunnel(StandSpec s, int row, float along) => s.tunnel && row < 4 && Mathf.Abs(along) < 2f;

        static void BuildStand(Transform root, StandSpec s)
        {
            Transform t = new GameObject(s.name).transform;
            t.SetParent(root, false);
            t.localPosition = s.front;
            t.localRotation = Quaternion.Euler(0f, s.yaw, 0f);
            float half = s.length * 0.5f;

            // Pitch-level wall in front of the first row.
            Box(t, new Vector3(0f, 0.45f, -0.1f), new Vector3(s.length, 0.9f, 0.2f), concrete, "FrontWall");

            for (int i = 0; i < s.tiers; i++)
            {
                float top = TierTop(s, i);
                float z0 = TierZ(s, i);
                float d = i == s.walkwayTier ? WalkwayDepth : Depth;
                // The tier: a solid step from the ground (cheap, and closes the underside).
                if (s.tunnel && i < 4)
                {
                    float sideLen = half - 2f;
                    Box(t, new Vector3(-(2f + sideLen * 0.5f), top * 0.5f, z0 + d * 0.5f), new Vector3(sideLen, top, d), concrete, "Tier");
                    Box(t, new Vector3(2f + sideLen * 0.5f, top * 0.5f, z0 + d * 0.5f), new Vector3(sideLen, top, d), concrete, "Tier");
                }
                else Box(t, new Vector3(0f, top * 0.5f, z0 + d * 0.5f), new Vector3(s.length, top, d), concrete, "Tier");

                if (i == s.walkwayTier)
                {
                    // Walkway: railing on the front edge, vomitory openings in the riser behind.
                    Railing(t, -half + 0.5f, half - 0.5f, top, z0 + 0.08f);
                    for (float x = -half + 6f; x < half - 4f; x += 12f)
                        Box(t, new Vector3(x, top + Rise * 0.5f, z0 + d + 0.02f), new Vector3(2.2f, Rise * 0.98f, 0.06f), dark, "Vomitory", 0f, false);
                    continue;
                }
                // Seat rows: one long seat strip per section between aisles (seat colour by fan side).
                Material seat = s.homeShare > 0.75f ? seatHome : s.homeShare < 0.25f ? seatAway : (i % 3 == 1 ? seatNeutral : seatHome);
                float start = -half + 0.6f;
                for (float x = start; x < half - 0.6f;)
                {
                    float end = x;
                    while (end < half - 0.6f && !IsAisle(end, s.length) && !IsTunnel(s, i, end)) end += 0.25f;
                    if (end - x > 0.8f)
                        Box(t, new Vector3((x + end) * 0.5f, top + 0.21f, z0 + d * 0.55f), new Vector3(end - x - 0.1f, 0.08f, 0.42f), seat, "Seats", 0f, false);
                    x = end + 0.25f;
                }
            }

            // Stairs in each aisle (half-height steps, lighter concrete) with a centre handrail.
            for (float along = -half; along < half; along += 0.25f)
            {
                if (!IsAisle(along, s.length)) continue;
                float ax = Mathf.Round((along + half) / 9f) * 9f - half;
                for (int i = 0; i < s.tiers; i++)
                {
                    if (i == s.walkwayTier) continue;
                    float top = TierTop(s, i), z0 = TierZ(s, i);
                    Box(t, new Vector3(ax, top + Rise * 0.25f, z0 + Depth * 0.25f), new Vector3(1.0f, Rise * 0.5f, Depth * 0.5f), concreteLight, "Stair", 0f, false);
                }
                float zEnd = TierZ(s, s.tiers - 1) + Depth;
                Rod(t, new Vector3(ax, TierTop(s, 0) + 1f, 0.3f), new Vector3(ax, TierTop(s, s.tiers - 1) + 1f, zEnd - 0.3f), 0.04f, railMat, "Handrail");
                along += 1.5f;
            }

            // Front railing above the pitch wall.
            Railing(t, -half + 0.3f, half - 0.3f, 0.9f, 0.05f);

            float topY = TierTop(s, s.tiers - 1);
            float backZ = TierZ(s, s.tiers - 1) + Depth;

            if (s.tunnel) BuildTunnel(t, s);

            if (!s.roof)
            {
                // Camera gantry side: a low back wall only, so the broadcast view is never blocked.
                Box(t, new Vector3(0f, (topY + 1f) * 0.5f, backZ + 0.15f), new Vector3(s.length, topY + 1f, 0.3f), facade, "BackWall");
                return;
            }

            // Back wall / outer facade with lit windows and vertical fins.
            float wallH = topY + 4.5f;
            Box(t, new Vector3(0f, wallH * 0.5f, backZ + 0.2f), new Vector3(s.length + 1f, wallH, 0.4f), facade, "BackWall");
            for (float x = -half + 2f; x < half - 1f; x += 4f)
            {
                Box(t, new Vector3(x, wallH * 0.5f, backZ + 0.55f), new Vector3(0.25f, wallH, 0.4f), concreteLight, "Fin");
                Box(t, new Vector3(x + 2f, topY * 0.55f, backZ + 0.41f), new Vector3(2.6f, 0.9f, 0.04f), glass, "Window", 0f, false);
            }

            // Cantilever roof: slab sloping slightly up toward the pitch, trusses, fascia and a light strip.
            float roofY = wallH + 0.2f;
            float reach = backZ * 0.85f;
            GameObject slab = Box(t, new Vector3(0f, roofY + 0.4f, backZ - reach * 0.5f + 0.4f), new Vector3(s.length + 2f, 0.25f, reach + 1f), roofMat, "Roof");
            slab.transform.localRotation = Quaternion.Euler(-4f, 0f, 0f);
            for (float x = -half; x <= half + 0.01f; x += 6f)
            {
                Rod(t, new Vector3(x, wallH - 2.5f, backZ), new Vector3(x, roofY + 0.2f, backZ - reach), 0.18f, steel, "Truss");
                Rod(t, new Vector3(x, roofY + 0.6f, backZ), new Vector3(x, roofY + 1.0f, backZ - reach), 0.15f, steel, "Truss");
            }
            Box(t, new Vector3(0f, roofY + 1.1f, backZ - reach), new Vector3(s.length + 2f, 0.9f, 0.2f), roofMat, "Fascia");
            Material strip = NFWLShaders.Simple(NFWLShaders.Additive, "Roof Light Strip");
            if (strip != null)
            {
                strip.mainTexture = ProceduralTextures.Glow;
                strip.SetColor("_Color", StadiumLighting.Kelvin(5600f));
                strip.SetFloat("_Intensity", 0.9f);
                GameObject q = Visuals.Part(PrimitiveType.Quad, t, new Vector3(0f, roofY + 0.62f, backZ - reach + 0.15f),
                    new Vector3(s.length, 0.35f, 1f), strip, "LightStrip", Quaternion.Euler(0f, 180f, 0f));
                q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void Railing(Transform t, float x0, float x1, float y, float z)
        {
            Rod(t, new Vector3(x0, y + 1.0f, z), new Vector3(x1, y + 1.0f, z), 0.05f, railMat, "Rail");
            Rod(t, new Vector3(x0, y + 0.55f, z), new Vector3(x1, y + 0.55f, z), 0.03f, railMat, "Rail");
            for (float x = x0; x <= x1 + 0.01f; x += 2.5f)
                Rod(t, new Vector3(x, y, z), new Vector3(x, y + 1.0f, z), 0.045f, railMat, "RailPost");
        }

        static void BuildTunnel(Transform t, StandSpec s)
        {
            float depth = TierZ(s, 4);
            float h = TierTop(s, 3) - 0.3f;
            Box(t, new Vector3(0f, h * 0.5f, depth - 0.2f), new Vector3(4f, h, 0.3f), dark, "TunnelBack", 0f, false);
            Box(t, new Vector3(-1.95f, h * 0.5f, depth * 0.5f), new Vector3(0.1f, h, depth), concreteLight, "TunnelWall");
            Box(t, new Vector3(1.95f, h * 0.5f, depth * 0.5f), new Vector3(0.1f, h, depth), concreteLight, "TunnelWall");
            Box(t, new Vector3(0f, h + 0.1f, depth * 0.5f), new Vector3(4f, 0.2f, depth), concrete, "TunnelRoof");
            // Extendable canopy in club colours, reaching onto the track.
            Material canopy = NFWLShaders.PBR(new Color(0.06f, 0.06f, 0.08f), 0.5f, 0.1f, ProceduralTextures.FabricNormal, 0.5f);
            Material gold = NFWLShaders.PBR(new Color(0.95f, 0.76f, 0.3f), 0.6f, 0.7f);
            for (int i = 0; i < 4; i++)
            {
                float z = -0.4f - i * 0.6f;
                float hh = 2.3f - i * 0.12f;
                Box(t, new Vector3(0f, hh, z), new Vector3(3.2f, 0.08f, 0.62f), canopy, "Canopy");
                Box(t, new Vector3(-1.6f, hh * 0.5f, z), new Vector3(0.06f, hh, 0.06f), gold, "CanopyFrame");
                Box(t, new Vector3(1.6f, hh * 0.5f, z), new Vector3(0.06f, hh, 0.06f), gold, "CanopyFrame");
            }
            Material sign = NFWLShaders.Simple(NFWLShaders.Additive, "Tunnel Sign");
            if (sign != null)
            {
                sign.mainTexture = LedTexture;
                sign.SetFloat("_Intensity", 1.2f);
                Visuals.Part(PrimitiveType.Quad, t, new Vector3(0f, h + 0.5f, -0.05f), new Vector3(3.6f, 0.6f, 1f), sign, "TunnelSign",
                    Quaternion.identity).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ---------------- floodlights ----------------

        static void BuildFloodlightTower(Transform root, Vector3 top)
        {
            Transform t = new GameObject("Floodlight Tower").transform;
            t.SetParent(root, false);
            Vector3 foot = new Vector3(top.x, 0f, top.z);
            // Tapered lattice mast: four legs and cross braces.
            float h = top.y - 1.5f;
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                Vector3 off = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Rod(t, foot + off * 1.4f, foot + off * 0.45f + Vector3.up * h, 0.22f, steel, "Leg");
            }
            for (float y = 2f; y < h - 1f; y += 3f)
            {
                float k = y / h;
                float r = Mathf.Lerp(1.4f, 0.45f, k);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = foot + Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward * r + Vector3.up * y;
                    Vector3 b = foot + Quaternion.Euler(0f, (i + 1) * 90f + 45f, 0f) * Vector3.forward * Mathf.Lerp(1.4f, 0.45f, (y + 3f) / h) + Vector3.up * (y + 3f);
                    Rod(t, a, b, 0.08f, steel, "Brace");
                }
            }

            // Lamp head aimed at the centre spot.
            Transform head = new GameObject("FloodlightHead").transform;
            head.SetParent(t, false);
            head.position = top;
            Vector3 toCentre = new Vector3(0f, 1f, 0f) - top;
            head.rotation = Quaternion.LookRotation(toCentre.normalized, Vector3.up);
            Box(head, new Vector3(0f, 0f, -0.25f), new Vector3(6f, 3.4f, 0.4f), steel, "Frame");
            Material lamp = NFWLShaders.PBR(new Color(1f, 0.98f, 0.9f), 0.9f, 0f, null, 1f, null, null, StadiumLighting.Kelvin(5200f) * 5f);
            for (int r = 0; r < 4; r++)
            for (int c = 0; c < 6; c++)
                Box(head, new Vector3(-2.5f + c * 1f, -1.2f + r * 0.8f, 0f), new Vector3(0.8f, 0.6f, 0.12f), lamp, "Lamp", 0f, false);

            Material halo = NFWLShaders.Simple(NFWLShaders.Additive, "Floodlight Halo");
            if (halo != null)
            {
                halo.mainTexture = ProceduralTextures.Glow;
                halo.SetColor("_Color", StadiumLighting.Kelvin(5200f));
                halo.SetFloat("_Intensity", 0.9f);
                GameObject g = Visuals.Part(PrimitiveType.Quad, dynamicRoot, head.TransformPoint(new Vector3(0f, 0f, 0.5f)), new Vector3(14f, 10f, 1f), halo, "Halo");
                g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                g.AddComponent<FaceCamera>();
            }
            Material shaft = NFWLShaders.Simple(NFWLShaders.Additive, "Light Shaft");
            if (shaft != null)
            {
                shaft.SetColor("_Color", StadiumLighting.Kelvin(5400f));
                shaft.SetFloat("_Intensity", 0.05f);
                shaft.SetFloat("_SoftEdge", 1f);
                shaft.SetFloat("_FadeNear", 14f);
                GameObject cone = new GameObject("LightShaft");
                cone.transform.SetParent(head, false);
                cone.transform.localPosition = new Vector3(0f, 0f, 0.3f);
                cone.AddComponent<MeshFilter>().sharedMesh = ConeMesh;
                MeshRenderer mr = cone.AddComponent<MeshRenderer>();
                mr.sharedMaterial = shaft;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                LightShafts.Add(cone);
            }
        }

        public static readonly List<GameObject> LightShafts = new List<GameObject>();

        static Mesh cone;

        /// <summary>Open cone along +Z, 38 m long; vertex alpha fades from the lamp to the far end.</summary>
        static Mesh ConeMesh
        {
            get
            {
                if (cone != null) return cone;
                const int sides = 16;
                const float len = 38f, r0 = 2.2f, r1 = 9f;
                var v = new List<Vector3>();
                var n = new List<Vector3>();
                var c = new List<Color>();
                var uv = new List<Vector2>();
                var tri = new List<int>();
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    v.Add(dir * r0); v.Add(dir * r1 + Vector3.forward * len);
                    n.Add(dir); n.Add(dir);
                    c.Add(new Color(1f, 1f, 1f, 1f)); c.Add(new Color(1f, 1f, 1f, 0f));
                    uv.Add(new Vector2(0.5f, 0.5f)); uv.Add(new Vector2(0.5f, 0.5f));
                }
                for (int i = 0; i < sides; i++)
                {
                    int a = i * 2;
                    tri.Add(a); tri.Add(a + 1); tri.Add(a + 2);
                    tri.Add(a + 1); tri.Add(a + 3); tri.Add(a + 2);
                }
                cone = new Mesh { name = "LightShaft" };
                cone.SetVertices(v); cone.SetNormals(n); cone.SetColors(c); cone.SetUVs(0, uv);
                cone.SetTriangles(tri, 0);
                cone.RecalculateBounds();
                return cone;
            }
        }

        // ---------------- extras ----------------

        static void BuildDugouts(Transform root, float hw)
        {
            Material shell = NFWLShaders.PBR(new Color(0.08f, 0.1f, 0.16f), 0.75f, 0.2f);
            Material seat = NFWLShaders.PBR(new Color(0.1f, 0.55f, 0.92f), 0.4f);
            Material perspex = NFWLShaders.PBR(new Color(0.3f, 0.4f, 0.5f), 0.95f, 0f, null, 1f, null, null, new Color(0.02f, 0.03f, 0.04f));
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 c = new Vector3(sx * 6f, 0f, hw + 1.3f);
                Box(root, c + new Vector3(0f, 1.15f, 0.35f), new Vector3(4f, 0.08f, 1.0f), perspex, "Dugout");
                Box(root, c + new Vector3(0f, 0.6f, 0.82f), new Vector3(4f, 1.2f, 0.08f), shell, "Dugout");
                Box(root, c + new Vector3(-2f, 0.6f, 0.35f), new Vector3(0.08f, 1.2f, 1f), shell, "Dugout");
                Box(root, c + new Vector3(2f, 0.6f, 0.35f), new Vector3(0.08f, 1.2f, 1f), shell, "Dugout");
                Box(root, c + new Vector3(0f, 0.25f, 0.55f), new Vector3(3.6f, 0.5f, 0.4f), seat, "Seats");
            }
        }

        /// <summary>Low skyline far outside (fades into the fog): dark blocks with a few lit windows.</summary>
        static void BuildSkyline(Transform root)
        {
            Material block = NFWLShaders.PBR(new Color(0.05f, 0.055f, 0.07f), 0.3f, 0f, null, 1f, null, null, new Color(0.02f, 0.02f, 0.03f));
            Material windows = NFWLShaders.PBR(new Color(0.1f, 0.1f, 0.1f), 0.5f, 0f, null, 1f, null, null, new Color(0.9f, 0.7f, 0.4f) * 0.6f);
            var rnd = new System.Random(77);
            for (int i = 0; i < 26; i++)
            {
                float a = i / 26f * Mathf.PI * 2f;
                float r = 95f + (float)rnd.NextDouble() * 25f;
                Vector3 p = new Vector3(Mathf.Cos(a) * r * 1.2f, 0f, Mathf.Sin(a) * r);
                float h = 10f + (float)rnd.NextDouble() * 28f;
                float w = 8f + (float)rnd.NextDouble() * 10f;
                Box(root, p + Vector3.up * h * 0.5f, new Vector3(w, h, w), block, "Building", a * Mathf.Rad2Deg, false);
                if (rnd.NextDouble() < 0.6)
                    Box(root, p + Vector3.up * h * 0.6f, new Vector3(w * 1.01f, h * 0.05f, w * 1.01f), windows, "Windows", a * Mathf.Rad2Deg, false);
            }
        }
    }

    /// <summary>Billboard: keeps a glow quad facing the active camera.</summary>
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            Camera c = Camera.main;
            if (c == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - c.transform.position, Vector3.up);
        }
    }
}
