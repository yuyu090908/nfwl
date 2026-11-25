using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NFWL
{
    /// <summary>
    /// v0.7 spectators (PHASE 9.4) with distance-based detail and no Animator at all:
    ///   * front rows: low-poly 3D fans (body, head, arms, some with team flags), merged into a few
    ///     large meshes and animated in the vertex shader (NFWL/Crowd): sway, clapping, standing up,
    ///     arms up and jumping on a goal, and a wave around the stadium;
    ///   * back rows: impostor strips (painted crowd texture) on the risers.
    /// How many rows are 3D and how full they are follows the graphics preset (Low = impostors only).
    /// Home fans wear black and gold, away fans white and navy; the main stand is mixed.
    /// </summary>
    public class CrowdBuilder : MonoBehaviour
    {
        readonly List<GameObject> built = new List<GameObject>();
        Material crowdMat, impostorMat;

        public int FanCount { get; private set; }

        public static CrowdBuilder Create(Transform parent)
        {
            CrowdBuilder c = new GameObject("Crowd").AddComponent<CrowdBuilder>();
            c.transform.SetParent(parent, false);
            c.crowdMat = NFWLShaders.Simple(NFWLShaders.Crowd, "NFWL Crowd");
            c.impostorMat = NFWLShaders.PBR(Color.white, 0.05f, 0f, null, 1f, Visuals.Crowd, new Vector2(1f, 1f));
            GraphicsQuality.Changed += c.OnQuality;
            c.Rebuild(GraphicsQuality.Current);
            return c;
        }

        void OnDestroy() => GraphicsQuality.Changed -= OnQuality;

        void OnQuality(GraphicsPreset p) => Rebuild(p);

        public void Rebuild(GraphicsPreset p)
        {
            foreach (GameObject g in built) if (g != null) Destroy(g);
            built.Clear();
            FanCount = 0;
            int rows3D = crowdMat != null ? p.crowdRows3D : 0;
            float density = crowdMat != null ? p.crowdDensity : 0f;
            var rnd = new System.Random(1234);
            foreach (StadiumBuilder.StandSpec s in StadiumBuilder.Stands)
            {
                var mb = new MeshBuffer();
                for (int row = 0; row < s.tiers; row++)
                {
                    if (row == s.walkwayTier) continue;
                    bool real = row < rows3D && density > 0f;
                    if (!real)
                    {
                        BuildImpostorRow(s, row);
                        continue;
                    }
                    float half = s.length * 0.5f;
                    for (float along = -half + 0.9f; along < half - 0.9f; along += 0.62f)
                    {
                        if (StadiumBuilder.IsAisle(along, s.length) || StadiumBuilder.IsTunnel(s, row, along)) continue;
                        double roll = rnd.NextDouble();
                        if (roll > density * 0.92) continue; // empty seats
                        bool home = rnd.NextDouble() < s.homeShare;
                        AddFan(mb, s, row, along + (float)(rnd.NextDouble() - 0.5) * 0.08f, home, rnd);
                        FanCount++;
                        if (mb.Count > 60000) { Flush(mb, s.name); mb = new MeshBuffer(); }
                    }
                }
                Flush(mb, s.name);
            }
        }

        // ---------------- 3D fans ----------------

        static readonly Color[] Skins = { new Color(0.95f, 0.8f, 0.68f), new Color(0.82f, 0.62f, 0.48f), new Color(0.55f, 0.38f, 0.27f), new Color(0.9f, 0.84f, 0.95f) };
        static readonly Color HomeA = new Color(0.07f, 0.07f, 0.09f), HomeB = new Color(0.95f, 0.76f, 0.3f);
        static readonly Color AwayA = new Color(0.9f, 0.91f, 0.95f), AwayB = new Color(0.12f, 0.12f, 0.3f);

        void AddFan(MeshBuffer mb, StadiumBuilder.StandSpec s, int row, float along, bool home, System.Random rnd)
        {
            Vector3 seat = StadiumBuilder.SeatPosition(s, row, along);
            Quaternion face = Quaternion.Euler(0f, s.yaw + 180f, 0f); // fans face the pitch
            float phase = (float)rnd.NextDouble();
            Color shirt = home ? (rnd.NextDouble() < 0.7 ? HomeA : HomeB) : (rnd.NextDouble() < 0.7 ? AwayA : AwayB);
            if (rnd.NextDouble() < 0.15) shirt = Color.Lerp(shirt, new Color((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble()), 0.6f);
            Color skin = Skins[rnd.Next(Skins.Length)];
            float scale = 0.92f + (float)rnd.NextDouble() * 0.16f;
            // Seated: hips on the seat, torso up, head on top.
            mb.Box(seat + face * new Vector3(0f, 0.42f, 0.05f) * scale, new Vector3(0.36f, 0.5f, 0.24f) * scale, face, shirt, phase, 0f);
            mb.Box(seat + face * new Vector3(0f, 0.12f, 0.2f) * scale, new Vector3(0.34f, 0.16f, 0.4f) * scale, face, shirt * 0.55f, phase, 0f);
            mb.Box(seat + face * new Vector3(0f, 0.8f, 0.05f) * scale, new Vector3(0.18f, 0.22f, 0.2f) * scale, face, skin, phase, 0f);
            // Hair / cap.
            mb.Box(seat + face * new Vector3(0f, 0.93f, 0.03f) * scale, new Vector3(0.19f, 0.06f, 0.21f) * scale, face, Color.Lerp(skin * 0.3f, shirt, rnd.NextDouble() < 0.3 ? 0.8f : 0f), phase, 0f);
            // Arms (move up with the excitement).
            mb.Box(seat + face * new Vector3(-0.23f, 0.45f, 0.08f) * scale, new Vector3(0.09f, 0.42f, 0.1f) * scale, face, shirt, phase, 1f);
            mb.Box(seat + face * new Vector3(0.23f, 0.45f, 0.08f) * scale, new Vector3(0.09f, 0.42f, 0.1f) * scale, face, shirt, phase, 1f);
            if (rnd.NextDouble() < 0.04)
            {
                // A team flag on a short pole.
                Color flag = home ? (rnd.NextDouble() < 0.5 ? HomeB : HomeA) : (rnd.NextDouble() < 0.5 ? AwayB : AwayA);
                mb.Box(seat + face * new Vector3(0.28f, 1.2f, 0.1f), new Vector3(0.025f, 0.9f, 0.025f), face, new Color(0.7f, 0.7f, 0.7f), phase, 1f);
                mb.Box(seat + face * new Vector3(0.58f, 1.5f, 0.1f), new Vector3(0.6f, 0.38f, 0.02f), face, flag, phase, 2f);
            }
        }

        void Flush(MeshBuffer mb, string name)
        {
            if (mb.Count == 0) return;
            Mesh m = mb.ToMesh("Crowd " + name);
            GameObject go = new GameObject("Crowd3D " + name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = crowdMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            built.Add(go);
        }

        void BuildImpostorRow(StadiumBuilder.StandSpec s, int row)
        {
            if (impostorMat == null) return;
            Vector3 centre = StadiumBuilder.SeatPosition(s, row, 0f) + Vector3.up * 0.45f;
            // Quad's visible side faces -forward, so forward points away from the pitch.
            GameObject q = Visuals.Part(PrimitiveType.Quad, transform, centre, new Vector3(s.length - 1.2f, 0.8f, 1f),
                impostorMat, "CrowdImpostor", Quaternion.Euler(0f, s.yaw, 0f));
            q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Material m = q.GetComponent<Renderer>().material; // own tiling per row length
            m.mainTextureScale = new Vector2(s.length / 6f, 1f);
            m.mainTextureOffset = new Vector2(row * 0.37f, 0f);
            built.Add(q);
        }

        /// <summary>Collects boxes into one mesh: colour, phase (uv2.x) and part (uv2.y) per vertex.</summary>
        class MeshBuffer
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector3> n = new List<Vector3>();
            readonly List<Color> c = new List<Color>();
            readonly List<Vector2> uv2 = new List<Vector2>();
            readonly List<int> t = new List<int>();
            public int Count => v.Count;

            static readonly Vector3[] Normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

            public void Box(Vector3 centre, Vector3 size, Quaternion rot, Color col, float phase, float part)
            {
                Vector3 h = size * 0.5f;
                for (int f = 0; f < 6; f++)
                {
                    Vector3 nn = Normals[f];
                    // Two axes spanning the face.
                    Vector3 a = Mathf.Abs(nn.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 b = Vector3.Cross(nn, a);
                    int start = v.Count;
                    for (int i = 0; i < 4; i++)
                    {
                        float sa = (i & 1) == 0 ? -1f : 1f, sb = (i & 2) == 0 ? -1f : 1f;
                        Vector3 local = Vector3.Scale(nn + a * sa + b * sb, h);
                        v.Add(centre + rot * local);
                        n.Add(rot * nn);
                        c.Add(col * (f == 3 ? 0.5f : 1f));
                        uv2.Add(new Vector2(phase, part));
                    }
                    // Winding: (0, 1, 2) has normal cross(a*2, b*2) = cross(a, b) = -nn ... so use (0, 2, 1).
                    Vector3 test = Vector3.Cross(v[start + 1] - v[start], v[start + 2] - v[start]);
                    if (Vector3.Dot(test, rot * nn) > 0f) { t.Add(start); t.Add(start + 1); t.Add(start + 2); t.Add(start + 1); t.Add(start + 3); t.Add(start + 2); }
                    else { t.Add(start); t.Add(start + 2); t.Add(start + 1); t.Add(start + 1); t.Add(start + 2); t.Add(start + 3); }
                }
            }

            public Mesh ToMesh(string name)
            {
                Mesh m = new Mesh { name = name };
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetColors(c);
                m.SetUVs(1, uv2);
                m.SetTriangles(t, 0);
                m.RecalculateBounds();
                // The shader lifts vertices up to ~0.9 m (standing, arms up): grow the bounds so the
                // mesh is never culled while still visible.
                Bounds bb = m.bounds;
                bb.Expand(new Vector3(0.5f, 2f, 0.5f));
                m.bounds = bb;
                return m;
            }
        }
    }
}
