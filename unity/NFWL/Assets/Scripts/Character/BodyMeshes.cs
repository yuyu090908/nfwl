using System.Collections.Generic;
using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// v0.7 (PHASE 7.1): smooth body-part meshes for the Crystalia players, built by lofting elliptical
    /// cross-sections along a bone (no downloaded models). Rings shrink to a point at the ends, giving
    /// rounded caps. Vertex colour alpha carries a baked crease occlusion (neck, armpit, knee, ankle) that
    /// NFWL/Lit uses as ambient occlusion. UVs: u around the limb (seam at the side), v along it in metres.
    /// Meshes are cached by shape, so 10 players share them.
    /// </summary>
    public static class BodyMeshes
    {
        /// <summary>One cross-section: centre (x, y, z offset), half width rx, half depth rz, crease occlusion (1 = none).</summary>
        public struct Ring
        {
            public float y, rx, rz, cx, cz, occlusion;
            public Ring(float y, float rx, float rz, float cz = 0f, float occlusion = 1f, float cx = 0f)
            {
                this.y = y; this.rx = rx; this.rz = rz; this.cz = cz; this.occlusion = occlusion; this.cx = cx;
            }
        }

        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <summary>Lofts rings (ordered top to bottom along -Y) into a closed smooth tube.</summary>
        public static Mesh Loft(string name, Ring[] rings, int sides = 14, float vPerMetre = 2.5f, Quaternion? bake = null)
        {
            if (cache.TryGetValue(name, out Mesh cached) && cached != null) return cached;
            int cols = sides + 1;
            var verts = new Vector3[rings.Length * cols];
            var uvs = new Vector2[verts.Length];
            var colors = new Color[verts.Length];
            float v = 0f;
            for (int r = 0; r < rings.Length; r++)
            {
                if (r > 0)
                {
                    Ring a = rings[r - 1], b = rings[r];
                    v += Mathf.Sqrt((a.y - b.y) * (a.y - b.y) + (a.rx - b.rx) * (a.rx - b.rx));
                }
                for (int s = 0; s < cols; s++)
                {
                    // Seam on the inner side (-X for the reference shape) where it is least visible.
                    float ang = (s / (float)sides) * Mathf.PI * 2f + Mathf.PI;
                    Ring ring = rings[r];
                    int i = r * cols + s;
                    verts[i] = new Vector3(ring.cx + Mathf.Cos(ang) * ring.rx, ring.y, ring.cz + Mathf.Sin(ang) * ring.rz);
                    uvs[i] = new Vector2(s / (float)sides, v * vPerMetre);
                    colors[i] = new Color(1f, 1f, 1f, ring.occlusion);
                }
            }
            var tris = new List<int>((rings.Length - 1) * sides * 6);
            for (int r = 0; r < rings.Length - 1; r++)
            for (int s = 0; s < sides; s++)
            {
                int a = r * cols + s, b = a + 1, c = a + cols, d = c + 1;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
            FixWinding(verts, tris, rings);
            if (bake.HasValue)
            {
                for (int i = 0; i < verts.Length; i++) verts[i] = bake.Value * verts[i];
            }
            Mesh m = new Mesh { name = name };
            m.vertices = verts;
            m.uv = uvs;
            m.colors = colors;
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            WeldSeamNormals(m, rings.Length, cols);
            m.RecalculateTangents();
            m.RecalculateBounds();
            cache[name] = m;
            return m;
        }

        /// <summary>Makes every face point away from the bone (checks the widest ring once).</summary>
        static void FixWinding(Vector3[] verts, List<int> tris, Ring[] rings)
        {
            int best = 0;
            for (int r = 1; r < rings.Length - 1; r++) if (rings[r].rx + rings[r].rz > rings[best].rx + rings[best].rz) best = r;
            if (best >= rings.Length - 1) best = rings.Length - 2;
            int cols = verts.Length / rings.Length;
            int quad = best * (cols - 1) * 6;
            if (quad + 2 >= tris.Count) return;
            Vector3 a = verts[tris[quad]], b = verts[tris[quad + 1]], c = verts[tris[quad + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            Vector3 centre = new Vector3(rings[best].cx, (a.y + b.y + c.y) / 3f, rings[best].cz);
            Vector3 outward = (a + b + c) / 3f - centre;
            if (Vector3.Dot(n, outward) >= 0f) return;
            for (int i = 0; i < tris.Count; i += 3)
            {
                int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t;
            }
        }

        /// <summary>The seam column is duplicated for UVs; give both copies the same (averaged) normal.</summary>
        static void WeldSeamNormals(Mesh m, int ringCount, int cols)
        {
            Vector3[] n = m.normals;
            for (int r = 0; r < ringCount; r++)
            {
                int a = r * cols, b = r * cols + cols - 1;
                Vector3 avg = (n[a] + n[b]).normalized;
                n[a] = avg; n[b] = avg;
            }
            m.normals = n;
        }

        /// <summary>
        /// A curved print patch (shirt number / crest) that follows the body surface: the part of the loft
        /// within ±halfAngle of the front (+Z) or back (-Z), lifted slightly, with flat UVs over the patch.
        /// </summary>
        public static Mesh Patch(string name, Ring[] rings, float yTop, float yBottom, float halfAngleDeg, bool back, float lift = 0.004f)
        {
            if (cache.TryGetValue(name, out Mesh cached) && cached != null) return cached;
            const int cols = 9, rows = 10;
            var verts = new Vector3[cols * rows];
            var uvs = new Vector2[verts.Length];
            float half = halfAngleDeg * Mathf.Deg2Rad;
            float xMax = 0f;
            for (int r = 0; r < rows; r++)
            {
                float y = Mathf.Lerp(yTop, yBottom, r / (rows - 1f));
                Ring ring = Sample(rings, y);
                for (int c = 0; c < cols; c++)
                {
                    float t = c / (cols - 1f);
                    float ang = (back ? -Mathf.PI * 0.5f : Mathf.PI * 0.5f) + Mathf.Lerp(-half, half, t) * (back ? -1f : 1f);
                    float x = ring.cx + Mathf.Cos(ang) * (ring.rx + lift);
                    float z = ring.cz + Mathf.Sin(ang) * (ring.rz + lift);
                    verts[r * cols + c] = new Vector3(x, y, z);
                    xMax = Mathf.Max(xMax, Mathf.Abs(x));
                }
            }
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                Vector3 p = verts[r * cols + c];
                float u = back ? 0.5f + p.x / (2f * xMax) : 0.5f - p.x / (2f * xMax);
                uvs[r * cols + c] = new Vector2(u, 1f - r / (rows - 1f)); // u grows to the viewer's right
            }
            var tris = new List<int>();
            for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols - 1; c++)
            {
                int a = r * cols + c, b = a + 1, d = a + cols, e = d + 1;
                tris.Add(a); tris.Add(b); tris.Add(d);
                tris.Add(b); tris.Add(e); tris.Add(d);
            }
            // Orient outward.
            Vector3 n = Vector3.Cross(verts[tris[1]] - verts[tris[0]], verts[tris[2]] - verts[tris[0]]);
            if (Vector3.Dot(n, back ? Vector3.back : Vector3.forward) < 0f)
                for (int i = 0; i < tris.Count; i += 3) { int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; }
            Mesh m = new Mesh { name = name };
            m.vertices = verts;
            m.uv = uvs;
            var colors = new Color[verts.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            m.colors = colors;
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            cache[name] = m;
            return m;
        }

        /// <summary>Linear interpolation of the ring profile at height y.</summary>
        public static Ring Sample(Ring[] rings, float y)
        {
            for (int i = 0; i < rings.Length - 1; i++)
            {
                Ring a = rings[i], b = rings[i + 1];
                if (y <= a.y && y >= b.y)
                {
                    float t = Mathf.Approximately(a.y, b.y) ? 0f : (a.y - y) / (a.y - b.y);
                    return new Ring(y, Mathf.Lerp(a.rx, b.rx, t), Mathf.Lerp(a.rz, b.rz, t), Mathf.Lerp(a.cz, b.cz, t), 1f, Mathf.Lerp(a.cx, b.cx, t));
                }
            }
            return y > rings[0].y ? rings[0] : rings[rings.Length - 1];
        }

        public static GameObject Attach(Transform parent, Mesh mesh, Material mat, string name, Vector3? pos = null, Quaternion? rot = null, bool castShadows = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos ?? Vector3.zero;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
            return go;
        }
    }
}
