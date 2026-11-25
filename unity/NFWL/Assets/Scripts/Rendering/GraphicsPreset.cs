using System;
using UnityEngine;

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
}
