using UnityEngine;

namespace NFWL
{
    /// <summary>v0.7 floodlight geometry and colour temperature (pure math, shared by lighting, stadium and tests).</summary>
    public static class FloodlightMath
    {
        /// <summary>Kelvin to an approximate linear RGB colour (Tanner Helland's fit), for warm / cool lamps.</summary>
        public static Color Kelvin(float k)
        {
            float t = k / 100f;
            float r, g, b;
            if (t <= 66f) r = 1f; else r = Mathf.Clamp01(1.29293618606f * Mathf.Pow(t - 60f, -0.1332047592f));
            if (t <= 66f) g = Mathf.Clamp01(0.39008157876f * Mathf.Log(t) - 0.63184144378f);
            else g = Mathf.Clamp01(1.12989086089f * Mathf.Pow(t - 60f, -0.0755148492f));
            if (t >= 66f) b = 1f; else if (t <= 19f) b = 0f; else b = Mathf.Clamp01(0.54320678911f * Mathf.Log(t - 10f) - 1.19625408914f);
            return new Color(r, g, b);
        }

        /// <summary>Direction of light coming from a floodlight tower at (x, height, z) toward the centre spot.</summary>
        public static Quaternion FromTower(Vector3 tower, float aimHeight = 0.9f)
        {
            Vector3 dir = new Vector3(0f, aimHeight, 0f) - tower;
            return Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        /// <summary>Corner tower positions shared with the stadium builder.</summary>
        public static Vector3 TowerPosition(float sx, float sz)
        {
            float bx = PitchDims.HalfLength + 3f, bz = PitchDims.HalfWidth + 2.2f;
            return new Vector3(sx * (bx + 9f), 24f, sz * (bz + 9f));
        }
    }
}
