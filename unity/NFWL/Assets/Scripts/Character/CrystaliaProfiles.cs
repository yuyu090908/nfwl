using R = NFWL.BodyMeshes.Ring;

namespace NFWL
{
    /// <summary>v0.7 Crystalia body shape (PHASE 7.1): cross-sections of every body part, in metres on the
    /// v0.6 joints (hips 0.95 m, shoulders on the torso at 0.48 m, thigh 0.44 m, shin 0.42 m).</summary>
    public static class CrystaliaProfiles
    {
        // ---------- profiles (metres, unscaled; joints as in v0.6) ----------

        // Shirt on the torso pivot (y = 0 at the waist band, shoulders at 0.48).
        public static readonly R[] ShirtRings =
        {
            new R(0.615f, 0.001f, 0.001f, 0f, 0.6f),
            new R(0.612f, 0.075f, 0.065f, 0f, 0.6f),   // collar opening (neck sits inside)
            new R(0.585f, 0.12f, 0.085f),
            new R(0.55f, 0.2f, 0.105f),                 // trapezius slope
            new R(0.5f, 0.235f, 0.12f, 0.005f),         // shoulder line
            new R(0.42f, 0.225f, 0.13f, 0.012f, 0.85f), // chest (armpit crease at the sides)
            new R(0.32f, 0.205f, 0.125f, 0.01f),
            new R(0.2f, 0.18f, 0.112f, 0.004f),         // waist
            new R(0.08f, 0.175f, 0.108f),
            new R(-0.02f, 0.182f, 0.114f),
            new R(-0.08f, 0.185f, 0.116f, 0f, 0.8f),    // tucked hem
            new R(-0.085f, 0.001f, 0.001f, 0f, 0.6f),
        };

        public static readonly R[] ShortsRings =
        {
            new R(0.07f, 0.001f, 0.001f),
            new R(0.068f, 0.178f, 0.112f, 0f, 0.8f),
            new R(0.0f, 0.19f, 0.122f),
            new R(-0.08f, 0.196f, 0.128f, -0.006f),
            new R(-0.13f, 0.17f, 0.12f, -0.006f, 0.7f), // crotch
            new R(-0.135f, 0.001f, 0.001f, 0f, 0.7f),
        };

        public static readonly R[] ShortsLegRings =
        {
            new R(0.02f, 0.001f, 0.001f),
            new R(0.015f, 0.09f, 0.095f),
            new R(-0.1f, 0.088f, 0.09f, 0.004f),
            new R(-0.2f, 0.083f, 0.085f, 0.006f, 0.85f), // hem
            new R(-0.205f, 0.001f, 0.001f),
        };

        public static readonly R[] ThighRings =
        {
            new R(-0.12f, 0.001f, 0.001f),
            new R(-0.125f, 0.072f, 0.076f, 0.004f, 0.8f),
            new R(-0.2f, 0.07f, 0.075f, 0.01f),         // quadriceps
            new R(-0.31f, 0.062f, 0.066f, 0.008f),
            new R(-0.4f, 0.052f, 0.055f, 0.004f),
            new R(-0.455f, 0.05f, 0.054f, 0.006f, 0.85f), // knee
            new R(-0.46f, 0.001f, 0.001f),
        };

        public static readonly R[] KneeRings =
        {
            new R(0.03f, 0.001f, 0.001f),
            new R(0.025f, 0.05f, 0.054f, 0.006f),
            new R(-0.04f, 0.048f, 0.052f, 0.002f),
            new R(-0.075f, 0.047f, 0.05f, 0f),
            new R(-0.08f, 0.001f, 0.001f),
        };

        // Sock over the calf: calf muscle bulges at the back (negative z).
        public static readonly R[] SockRings =
        {
            new R(-0.055f, 0.001f, 0.001f),
            new R(-0.06f, 0.051f, 0.054f, -0.004f),
            new R(-0.13f, 0.056f, 0.062f, -0.014f),     // calf
            new R(-0.22f, 0.047f, 0.05f, -0.008f),
            new R(-0.32f, 0.036f, 0.038f, -0.002f),
            new R(-0.4f, 0.032f, 0.034f, 0f, 0.85f),    // ankle
            new R(-0.43f, 0.001f, 0.001f),
        };

        // Boot lofted from heel to toe along +Z (baked rotation), relative to the ankle pivot.
        public static readonly R[] BootRings =
        {
            new R(0.075f, 0.001f, 0.001f, -0.035f),
            new R(0.07f, 0.042f, 0.035f, -0.03f, 0.8f),   // heel (x = width, z = height after baking)
            new R(0.03f, 0.045f, 0.045f, -0.02f),
            new R(-0.03f, 0.046f, 0.045f, -0.025f),
            new R(-0.1f, 0.047f, 0.032f, -0.04f),
            new R(-0.16f, 0.04f, 0.024f, -0.047f),
            new R(-0.195f, 0.026f, 0.018f, -0.052f),     // toe
            new R(-0.205f, 0.001f, 0.001f, -0.054f),
        };

        public static readonly R[] NeckRings =
        {
            new R(0.17f, 0.001f, 0.001f),
            new R(0.165f, 0.05f, 0.052f, 0f, 0.8f),
            new R(0.08f, 0.054f, 0.056f, -0.004f),
            new R(0.0f, 0.06f, 0.058f, -0.006f, 0.6f),  // crease at the collar
            new R(-0.01f, 0.001f, 0.001f),
        };

        // Head on the head pivot (y = 0 at the eyes), face toward +Z.
        public static readonly R[] HeadRings =
        {
            new R(0.135f, 0.001f, 0.001f, -0.01f),
            new R(0.125f, 0.055f, 0.06f, -0.01f),
            new R(0.095f, 0.09f, 0.1f, -0.008f),
            new R(0.045f, 0.1f, 0.112f, -0.004f),
            new R(0.0f, 0.098f, 0.11f, 0f),
            new R(-0.045f, 0.09f, 0.1f, 0.004f),        // cheekbones
            new R(-0.085f, 0.072f, 0.085f, 0.012f),
            new R(-0.115f, 0.05f, 0.06f, 0.024f),       // jaw
            new R(-0.135f, 0.026f, 0.03f, 0.04f),       // chin
            new R(-0.14f, 0.001f, 0.001f, 0.042f),
        };

        public static readonly R[] SleeveRings =
        {
            new R(0.07f, 0.001f, 0.001f),
            new R(0.06f, 0.05f, 0.05f),
            new R(0.02f, 0.078f, 0.072f),                // deltoid
            new R(-0.06f, 0.07f, 0.066f),
            new R(-0.15f, 0.06f, 0.058f, 0f, 0.85f),    // sleeve hem
            new R(-0.155f, 0.001f, 0.001f),
        };

        public static readonly R[] UpperArmRings =
        {
            new R(-0.12f, 0.001f, 0.001f),
            new R(-0.125f, 0.047f, 0.046f),
            new R(-0.19f, 0.045f, 0.043f, -0.004f),     // triceps
            new R(-0.25f, 0.038f, 0.036f),
            new R(-0.28f, 0.036f, 0.034f, 0f, 0.8f),    // elbow
            new R(-0.285f, 0.001f, 0.001f),
        };

        public static readonly R[] ForearmRings =
        {
            new R(0.015f, 0.001f, 0.001f),
            new R(0.01f, 0.038f, 0.036f),
            new R(-0.06f, 0.044f, 0.04f, 0.004f),       // forearm muscle
            new R(-0.16f, 0.033f, 0.028f),
            new R(-0.235f, 0.026f, 0.02f, 0f, 0.85f),   // wrist
            new R(-0.24f, 0.001f, 0.001f),
        };

        public static readonly R[] HandRings =
        {
            new R(-0.225f, 0.001f, 0.001f),
            new R(-0.23f, 0.03f, 0.018f),
            new R(-0.27f, 0.04f, 0.02f, 0.004f),        // palm
            new R(-0.31f, 0.036f, 0.016f, 0.008f),      // fingers (curled)
            new R(-0.335f, 0.02f, 0.012f, 0.012f),
            new R(-0.34f, 0.001f, 0.001f, 0.012f),
        };
    }
}
