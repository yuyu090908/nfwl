using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NFWL
{
    /// <summary>
    /// v0.7 goal net (PHASE 9.3): four deforming mesh panels (back, two sides, roof) with a knotted
    /// diamond pattern. When the ball hits the net it bulges locally around the contact point, the bulge
    /// spreads to the neighbouring cells and dies away (spring + damping + neighbour coupling), and the
    /// edges tied to the frame stay fixed. The invisible colliders that stop the ball are unchanged.
    /// Quality: Dynamic (full), Simple (whole-panel sway, for slow devices), Static (no movement).
    /// Only panels that are still moving are updated, so a resting net costs nothing.
    /// </summary>
    public class GoalNet : MonoBehaviour
    {
        class Panel
        {
            public Mesh mesh;
            public Vector3[] rest, current;
            public float[] disp, vel;
            public bool[] pinned;
            public int cols, rows;
            public Vector3 normal;   // outward (away from the goal mouth)
            public bool active;
            public float sway, swayVel; // simple mode
        }

        public float stiffness = 90f;
        public float damping = 6f;
        public float coupling = 260f;
        public float maxBulge = 0.45f;

        readonly List<Panel> panels = new List<Panel>();
        float sign;
        Bounds area;
        static readonly List<GoalNet> nets = new List<GoalNet>();
        static bool subscribed;

        public NetQuality Quality { get; set; } = NetQuality.Dynamic;

        public static GoalNet Build(Transform parent, float sign, Material mat)
        {
            GoalNet g = new GameObject(sign > 0f ? "GoalNet +X" : "GoalNet -X").AddComponent<GoalNet>();
            g.transform.SetParent(parent, false);
            g.sign = sign;
            float line = sign * PitchDims.HalfLength;
            float back = sign * (PitchDims.HalfLength + PitchDims.GoalDepth);
            float gw = PitchDims.GoalHalfWidth, gh = PitchDims.GoalHeight;
            const float cell = 0.15f;
            // Back: plane x = back, spanning z and y; the bottom edge is pegged to the ground.
            g.AddPanel(new Vector3(back, 0f, -gw), new Vector3(0f, 0f, 2f * gw), new Vector3(0f, gh, 0f), new Vector3(sign, 0f, 0f), cell, mat, true);
            // Sides: from the goal line to the back, at z = +/- gw.
            g.AddPanel(new Vector3(line, 0f, gw), new Vector3(back - line, 0f, 0f), new Vector3(0f, gh, 0f), new Vector3(0f, 0f, 1f), cell, mat, true);
            g.AddPanel(new Vector3(line, 0f, -gw), new Vector3(back - line, 0f, 0f), new Vector3(0f, gh, 0f), new Vector3(0f, 0f, -1f), cell, mat, true);
            // Roof.
            g.AddPanel(new Vector3(line, gh, -gw), new Vector3(back - line, 0f, 0f), new Vector3(0f, 0f, 2f * gw), new Vector3(0f, 1f, 0f), cell, mat, false);
            g.area = new Bounds(new Vector3((line + back) * 0.5f, gh * 0.5f, 0f), new Vector3(Mathf.Abs(back - line) + 0.6f, gh + 0.6f, 2f * gw + 0.6f));
            nets.Add(g);
            if (!subscribed) { Ball.NetHit += OnNetHit; subscribed = true; }
            GraphicsQuality.Changed += g.OnQuality;
            g.OnQuality(GraphicsQuality.Current);
            return g;
        }

        void OnDestroy()
        {
            nets.Remove(this);
            GraphicsQuality.Changed -= OnQuality;
        }

        void OnQuality(GraphicsPreset p) => Quality = p.net;

        void AddPanel(Vector3 origin, Vector3 uAxis, Vector3 vAxis, Vector3 outward, float cell, Material mat, bool pegged)
        {
            var p = new Panel();
            p.cols = Mathf.Max(2, Mathf.RoundToInt(uAxis.magnitude / cell) + 1);
            p.rows = Mathf.Max(2, Mathf.RoundToInt(vAxis.magnitude / cell) + 1);
            int n = p.cols * p.rows;
            p.rest = new Vector3[n];
            p.current = new Vector3[n];
            p.disp = new float[n];
            p.vel = new float[n];
            p.pinned = new bool[n];
            p.normal = outward;
            var uv = new Vector2[n];
            for (int r = 0; r < p.rows; r++)
            for (int c = 0; c < p.cols; c++)
            {
                int i = r * p.cols + c;
                float u = c / (p.cols - 1f), v = r / (p.rows - 1f);
                p.rest[i] = origin + uAxis * u + vAxis * v;
                p.current[i] = p.rest[i];
                uv[i] = new Vector2(u * uAxis.magnitude, v * vAxis.magnitude); // metres, for the mesh pattern
                // Edges tied to posts / bar / back frame are fixed; the bottom only when pegged.
                p.pinned[i] = c == 0 || c == p.cols - 1 || r == p.rows - 1 || r == 0; // bottom pegged to the grass / roof edges on the frame
            }
            var tris = new List<int>();
            for (int r = 0; r < p.rows - 1; r++)
            for (int c = 0; c < p.cols - 1; c++)
            {
                int a = r * p.cols + c, b = a + 1, d = a + p.cols, e = d + 1;
                tris.Add(a); tris.Add(d); tris.Add(b);
                tris.Add(b); tris.Add(d); tris.Add(e);
            }
            p.mesh = new Mesh { name = "GoalNetPanel" };
            p.mesh.MarkDynamic();
            p.mesh.vertices = p.current;
            p.mesh.uv = uv;
            p.mesh.SetTriangles(tris, 0);
            p.mesh.RecalculateNormals();
            p.mesh.RecalculateBounds();
            Bounds b0 = p.mesh.bounds;
            b0.Expand(maxBulge * 2f + 0.2f);
            p.mesh.bounds = b0;

            GameObject go = new GameObject("NetPanel");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = p.mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            panels.Add(p);
        }

        static void OnNetHit(Vector3 point, Vector3 ballVel)
        {
            foreach (GoalNet g in nets)
                if (g.area.Contains(point)) g.Hit(point, ballVel);
        }

        /// <summary>Pushes the net outward around the contact point (impulse along each panel's normal).</summary>
        public void Hit(Vector3 point, Vector3 ballVel)
        {
            if (Quality == NetQuality.Static) return;
            foreach (Panel p in panels)
            {
                float into = Vector3.Dot(ballVel, p.normal);
                if (into <= 0.2f) continue;
                if (Quality == NetQuality.Simple)
                {
                    p.swayVel += into * 0.08f;
                    p.active = true;
                    continue;
                }
                const float radius = 0.55f;
                for (int i = 0; i < p.rest.Length; i++)
                {
                    if (p.pinned[i]) continue;
                    Vector3 d = p.rest[i] - point;
                    d -= p.normal * Vector3.Dot(d, p.normal);
                    float w = Mathf.Exp(-d.sqrMagnitude / (radius * radius));
                    if (w < 0.02f) continue;
                    p.vel[i] += into * 0.55f * w;
                    p.active = true;
                }
            }
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (dt <= 0f) return;
            foreach (Panel p in panels)
            {
                if (!p.active) continue;
                if (Quality == NetQuality.Simple) StepSimple(p, dt);
                else StepDynamic(p, dt);
            }
        }

        void StepDynamic(Panel p, float dt)
        {
            // Two sub-steps keep the spring network stable at 30 FPS.
            float h = dt * 0.5f;
            float energy = 0f;
            for (int s = 0; s < 2; s++)
            {
                for (int r = 0; r < p.rows; r++)
                for (int c = 0; c < p.cols; c++)
                {
                    int i = r * p.cols + c;
                    if (p.pinned[i]) continue;
                    float lap = p.disp[i - 1] + p.disp[i + 1] + p.disp[i - p.cols] + p.disp[i + p.cols] - 4f * p.disp[i];
                    float a = -stiffness * p.disp[i] - damping * p.vel[i] + coupling * lap;
                    p.vel[i] += a * h;
                }
                for (int i = 0; i < p.disp.Length; i++)
                {
                    if (p.pinned[i]) continue;
                    p.disp[i] = Mathf.Clamp(p.disp[i] + p.vel[i] * h, -maxBulge * 0.3f, maxBulge);
                    energy += Mathf.Abs(p.disp[i]) + Mathf.Abs(p.vel[i]) * 0.1f;
                }
            }
            for (int i = 0; i < p.current.Length; i++) p.current[i] = p.rest[i] + p.normal * p.disp[i];
            p.mesh.vertices = p.current;
            if (energy / p.disp.Length < 0.0002f)
            {
                for (int i = 0; i < p.disp.Length; i++) { p.disp[i] = 0f; p.vel[i] = 0f; }
                p.mesh.vertices = p.rest;
                p.active = false;
            }
        }

        void StepSimple(Panel p, float dt)
        {
            p.swayVel += (-stiffness * p.sway - damping * p.swayVel) * dt;
            p.sway += p.swayVel * dt;
            for (int r = 0; r < p.rows; r++)
            for (int c = 0; c < p.cols; c++)
            {
                int i = r * p.cols + c;
                // Bulge shape: zero at the frame, largest in the middle.
                float u = c / (p.cols - 1f), v = r / (p.rows - 1f);
                float shape = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(Mathf.Max(v, 0.05f) * Mathf.PI);
                p.current[i] = p.rest[i] + p.normal * p.sway * shape;
            }
            p.mesh.vertices = p.current;
            if (Mathf.Abs(p.sway) < 0.001f && Mathf.Abs(p.swayVel) < 0.01f)
            {
                p.sway = p.swayVel = 0f;
                p.mesh.vertices = p.rest;
                p.active = false;
            }
        }

        /// <summary>Largest current bulge (m), for tests.</summary>
        public float MaxDisplacement
        {
            get
            {
                float m = 0f;
                foreach (Panel p in panels)
                {
                    foreach (float d in p.disp) m = Mathf.Max(m, Mathf.Abs(d));
                    m = Mathf.Max(m, Mathf.Abs(p.sway));
                }
                return m;
            }
        }

        public bool Moving
        {
            get { foreach (Panel p in panels) if (p.active) return true; return false; }
        }
    }
}
