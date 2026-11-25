using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NFWL
{
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
