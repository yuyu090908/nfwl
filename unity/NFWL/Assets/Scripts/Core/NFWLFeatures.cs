using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// v0.7 feature flags. Every big v0.7 change can be switched off to get the v0.6 behaviour back,
    /// without touching code: the graphics panel (F3) shows them, and they are remembered (PlayerPrefs).
    /// Values are read when the match is built, so changing one restarts the visuals on the next match
    /// (R at full time, or re-enter Play mode). Ball size and the ball marker apply at once.
    /// </summary>
    public static class NFWLFeatures
    {
        /// <summary>NFWL shaders (PBR, grass, crystal, net, sky). Off = Unity's default material as in v0.6.</summary>
        public static bool NewShaders { get => Get("NewShaders", true); set => Set("NewShaders", value); }
        /// <summary>Bloom, tone mapping, colour grading and SSAO.</summary>
        public static bool PostProcessing { get => Get("PostProcessing", true); set => Set("PostProcessing", value); }
        /// <summary>Night floodlight rig (one shadow light + coloured fills). Off = the v0.6 single light.</summary>
        public static bool FloodlightRig { get => Get("FloodlightRig", true); set => Set("FloodlightRig", value); }
        /// <summary>Smooth anatomical body meshes. Off = the v0.6 primitive body.</summary>
        public static bool AnatomicalModel { get => Get("AnatomicalModel", true); set => Set("AnatomicalModel", value); }
        /// <summary>v0.7 stadium (roof, stairs, railings, tunnel, 3D crowd, LED). Off = v0.6 stands.</summary>
        public static bool NextGenStadium { get => Get("NextGenStadium", true); set => Set("NextGenStadium", value); }
        /// <summary>Ball drawn at its real size (0.22 m). Off = drawn 1.6x bigger as in v0.6.</summary>
        public static bool RealBallSize { get => Get("RealBallSize", true); set => Set("RealBallSize", value); }
        /// <summary>Per-player running style (stride, cadence, lean, arm swing).</summary>
        public static bool MotionVariation { get => Get("MotionVariation", true); set => Set("MotionVariation", value); }

        static bool Get(string key, bool def)
        {
            try { return PlayerPrefs.GetInt("NFWL.Feature." + key, def ? 1 : 0) != 0; }
            catch { return def; }
        }

        static void Set(string key, bool v)
        {
            try { PlayerPrefs.SetInt("NFWL.Feature." + key, v ? 1 : 0); PlayerPrefs.Save(); }
            catch { /* storage unavailable (WebGL private mode): keep running with defaults */ }
        }

        public static void ResetAll()
        {
            NewShaders = PostProcessing = FloodlightRig = AnatomicalModel = NextGenStadium = RealBallSize = MotionVariation = true;
        }
    }
}
