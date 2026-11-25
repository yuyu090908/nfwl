using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NFWL
{
    public enum QualityLevel { Low, Medium, High, Ultra }

    /// <summary>Every value a quality preset controls (PHASE 13). Plain data so tests can check presets.</summary>
    [Serializable]
    public struct GraphicsPreset
    {
        public QualityLevel level;
        public ShadowQuality shadows;
        public ShadowResolution shadowResolution;
        public float shadowDistance;
        public int shadowCascades;
        public int pixelLights;          // per-pixel floodlight fills (the key light is always per pixel)
        public int msaa;                 // 0, 2, 4, 8
        public bool hdr;
        public bool postProcessing;
        public bool bloom;
        public bool ssao;
        public float ssaoIntensity;
        public bool fullResSsao;
        public int textureMipLimit;      // 0 = full resolution, 1 = half
        public float crowdDensity;       // fraction of 3D fans that are built
        public int crowdRows3D;          // front rows with 3D fans; the rest are impostor textures
        public int grassShells;          // short 3D grass layers near the camera
        public float renderScale;        // 3D view resolution relative to the screen
        public NetQuality net;
        public bool lightShafts;
        public bool reflectionProbe;
        public int anisotropic;          // 0 off, 1 per texture, 2 forced

        /// <summary>The preset table. Tuned for: Low = old laptops / mobile GPUs in a browser,
        /// Medium = standard WebGL, High = desktop, Ultra = a strong desktop GPU.</summary>
        public static GraphicsPreset For(QualityLevel q)
        {
            switch (q)
            {
                case QualityLevel.Low:
                    return new GraphicsPreset
                    {
                        level = q, shadows = ShadowQuality.HardOnly, shadowResolution = ShadowResolution.Low, shadowDistance = 30f,
                        shadowCascades = 1, pixelLights = 0, msaa = 0, hdr = false, postProcessing = false, bloom = false,
                        ssao = false, ssaoIntensity = 0f, fullResSsao = false, textureMipLimit = 1, crowdDensity = 0f, crowdRows3D = 0,
                        grassShells = 0, renderScale = 0.8f, net = NetQuality.Simple, lightShafts = false, reflectionProbe = false, anisotropic = 0
                    };
                case QualityLevel.Medium:
                    return new GraphicsPreset
                    {
                        level = q, shadows = ShadowQuality.All, shadowResolution = ShadowResolution.Medium, shadowDistance = 45f,
                        shadowCascades = 2, pixelLights = 1, msaa = 2, hdr = true, postProcessing = true, bloom = true,
                        ssao = false, ssaoIntensity = 0f, fullResSsao = false, textureMipLimit = 0, crowdDensity = 0.45f, crowdRows3D = 3,
                        grassShells = 0, renderScale = 1f, net = NetQuality.Dynamic, lightShafts = true, reflectionProbe = true, anisotropic = 1
                    };
                case QualityLevel.High:
                    return new GraphicsPreset
                    {
                        level = q, shadows = ShadowQuality.All, shadowResolution = ShadowResolution.High, shadowDistance = 60f,
                        shadowCascades = 2, pixelLights = 2, msaa = 4, hdr = true, postProcessing = true, bloom = true,
                        ssao = true, ssaoIntensity = 0.45f, fullResSsao = false, textureMipLimit = 0, crowdDensity = 0.75f, crowdRows3D = 5,
                        grassShells = 2, renderScale = 1f, net = NetQuality.Dynamic, lightShafts = true, reflectionProbe = true, anisotropic = 1
                    };
                default:
                    return new GraphicsPreset
                    {
                        level = QualityLevel.Ultra, shadows = ShadowQuality.All, shadowResolution = ShadowResolution.VeryHigh, shadowDistance = 75f,
                        shadowCascades = 4, pixelLights = 3, msaa = 8, hdr = true, postProcessing = true, bloom = true,
                        ssao = true, ssaoIntensity = 0.6f, fullResSsao = true, textureMipLimit = 0, crowdDensity = 1f, crowdRows3D = 8,
                        grassShells = 4, renderScale = 1f, net = NetQuality.Dynamic, lightShafts = true, reflectionProbe = true, anisotropic = 2
                    };
            }
        }
    }

    public enum NetQuality { Static, Simple, Dynamic }

    /// <summary>
    /// v0.7 graphics quality (PHASE 13): applies a preset to Unity's QualitySettings, the camera and the
    /// NFWL systems (post-processing, crowd, grass, net), remembers the choice, and can lower the
    /// quality automatically when the frame rate stays too low.
    /// </summary>
    public static class GraphicsQuality
    {
        const string PrefKey = "NFWL.Graphics.Level";
        const string AutoKey = "NFWL.Graphics.Auto";

        public static GraphicsPreset Current { get; private set; } = GraphicsPreset.For(DefaultLevel);
        /// <summary>Raised after a preset is applied (stadium rebuilds crowd / grass / net detail).</summary>
        public static event Action<GraphicsPreset> Changed;

        /// <summary>WebGL starts at Medium, desktop at High.</summary>
        public static QualityLevel DefaultLevel =>
            Application.platform == RuntimePlatform.WebGLPlayer ? QualityLevel.Medium : QualityLevel.High;

        public static bool AutoAdjust
        {
            get { try { return PlayerPrefs.GetInt(AutoKey, 1) != 0; } catch { return true; } }
            set { try { PlayerPrefs.SetInt(AutoKey, value ? 1 : 0); PlayerPrefs.Save(); } catch { } }
        }

        public static QualityLevel SavedLevel
        {
            get
            {
                try
                {
                    int v = PlayerPrefs.GetInt(PrefKey, (int)DefaultLevel);
                    return (QualityLevel)Mathf.Clamp(v, 0, 3);
                }
                catch { return DefaultLevel; }
            }
        }

        public static void Apply(QualityLevel level, bool save = true)
        {
            GraphicsPreset p = GraphicsPreset.For(level);
            if (save)
            {
                try { PlayerPrefs.SetInt(PrefKey, (int)level); PlayerPrefs.Save(); } catch { }
            }
            QualitySettings.shadows = p.shadows;
            QualitySettings.shadowResolution = p.shadowResolution;
            QualitySettings.shadowDistance = p.shadowDistance;
            QualitySettings.shadowCascades = p.shadowCascades;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            if (p.shadowCascades == 2) QualitySettings.shadowCascade2Split = 0.3f;
            if (p.shadowCascades == 4) QualitySettings.shadowCascade4Split = new Vector3(0.12f, 0.3f, 0.6f);
            QualitySettings.pixelLightCount = p.pixelLights + 1; // + the key light
            QualitySettings.antiAliasing = p.msaa;
            QualitySettings.globalTextureMipmapLimit = p.textureMipLimit;
            QualitySettings.anisotropicFiltering = p.anisotropic == 0 ? AnisotropicFiltering.Disable
                : p.anisotropic == 1 ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
            QualitySettings.softParticles = false;
            QualitySettings.lodBias = level >= QualityLevel.High ? 1.5f : 1f;
            Current = p;
            Changed?.Invoke(p);
        }

        // ---------------- automatic quality ----------------

        static float lowSince = -1f;
        static float lastChange = -100f;
        static float fpsAvg = 60f;

        /// <summary>Called once per frame. Lowers the level one step when the average frame rate stays
        /// under the target for a few seconds (never raises it by itself, so it does not oscillate).</summary>
        public static void TickAuto(float unscaledDt, float targetFps = 45f)
        {
            if (!AutoAdjust || unscaledDt <= 0f) return;
            float fps = 1f / unscaledDt;
            fpsAvg = Mathf.Lerp(fpsAvg, fps, 1f - Mathf.Exp(-unscaledDt * 1.5f));
            float now = Time.unscaledTime;
            if (now < 8f || now - lastChange < 6f) return; // ignore loading hitches and let a change settle
            if (fpsAvg < targetFps)
            {
                if (lowSince < 0f) lowSince = now;
                if (now - lowSince > 4f && Current.level > QualityLevel.Low)
                {
                    lastChange = now;
                    lowSince = -1f;
                    Debug.Log($"NFWL: average {fpsAvg:0} FPS, lowering graphics to {Current.level - 1}");
                    Apply(Current.level - 1, true);
                    LastAutoMessage = $"AUTO: {fpsAvg:0} FPS -> {Current.level}";
                    LastAutoMessageTime = now;
                }
            }
            else lowSince = -1f;
        }

        public static string LastAutoMessage { get; private set; } = "";
        public static float LastAutoMessageTime { get; private set; } = -100f;
        public static float AverageFps => fpsAvg;
    }
}
