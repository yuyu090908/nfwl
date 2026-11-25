using UnityEngine;

namespace NFWL
{
    /// <summary>v0.7 body cross-sections (pure data and math, no meshes).</summary>
    public static partial class BodyMeshes
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
    }
}
