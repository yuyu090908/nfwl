using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NFWL
{
    /// <summary>
    /// v0.7 short 3D grass (PHASE 9.2): 0 to 4 shell layers a few millimetres above the turf, drawn with
    /// NFWL/GrassShell. Blades exist only near the camera (they fade out by ~22 m in the shader), mid
    /// distance shows the turf normal map, far distance only the colour / stripe shader. The layer count
    /// follows the graphics preset (Low / Medium: none, High: 2, Ultra: 4).
    /// </summary>
    public class GrassShells : MonoBehaviour
    {
        readonly List<GameObject> layers = new List<GameObject>();
        Material source;
        float sizeX, sizeZ;

        public const float BladeHeight = 0.02f;
        public int LayerCount => layers.Count;

        public static GrassShells Create(Transform parent, float sizeX, float sizeZ, Material turf)
        {
            GrassShells g = new GameObject("GrassShells").AddComponent<GrassShells>();
            g.transform.SetParent(parent, false);
            g.sizeX = sizeX;
            g.sizeZ = sizeZ;
            g.source = turf;
            GraphicsQuality.Changed += g.Rebuild;
            g.Rebuild(GraphicsQuality.Current);
            return g;
        }

        void OnDestroy() => GraphicsQuality.Changed -= Rebuild;

        void Rebuild(GraphicsPreset p)
        {
            foreach (GameObject l in layers) if (l != null) Destroy(l);
            layers.Clear();
            Shader sh = NFWLShaders.Get(NFWLShaders.GrassShell);
            if (sh == null || p.grassShells <= 0) return;
            for (int i = 0; i < p.grassShells; i++)
            {
                float k = (i + 1f) / (p.grassShells + 1f);
                Material m = new Material(sh) { name = "GrassShell " + i };
                m.CopyPropertiesFromMaterial(source);
                m.SetFloat("_Layer", k);
                m.renderQueue = 2450 + i;
                GameObject q = Visuals.Part(PrimitiveType.Quad, transform, new Vector3(0f, BladeHeight * k, 0f),
                    new Vector3(sizeX, sizeZ, 1f), m, "GrassShell", Quaternion.Euler(90f, 0f, 0f));
                MeshRenderer r = q.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
                layers.Add(q);
            }
        }
    }
}
