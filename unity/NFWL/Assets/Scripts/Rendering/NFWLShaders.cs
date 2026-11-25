using System.Collections.Generic;
using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// v0.7: finds the NFWL shaders (Assets/Resources/Shaders, so they are always in the build) and
    /// makes PBR materials from them. If a shader is missing or not supported by the GPU, callers get
    /// null / the v0.6 material, so a shader problem can never turn the game pink.
    /// </summary>
    public static class NFWLShaders
    {
        static readonly Dictionary<string, Shader> cache = new Dictionary<string, Shader>();
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        public const string Lit = "NFWLLit";
        public const string Crystal = "NFWLCrystal";
        public const string Grass = "NFWLGrass";
        public const string GrassShell = "NFWLGrassShell";
        public const string Additive = "NFWLAdditive";
        public const string ContactShadow = "NFWLBlob";
        public const string Net = "NFWLNet";
        public const string Sky = "NFWLSky";
        public const string Crowd = "NFWLCrowd";
        public const string Post = "NFWLPost";

        /// <summary>The shader if it loaded and the GPU supports it; otherwise null.</summary>
        public static Shader Get(string file)
        {
            if (!NFWLFeatures.NewShaders && file != Post) return null;
            if (cache.TryGetValue(file, out Shader s)) return s;
            s = Resources.Load<Shader>("Shaders/" + file);
            if (s != null && !s.isSupported)
            {
                Debug.LogWarning("NFWL: shader " + file + " is not supported on this GPU, using the v0.6 look for it.");
                s = null;
            }
            cache[file] = s;
            return s;
        }

        public static bool Has(string file) => Get(file) != null;

        /// <summary>
        /// A cached PBR material. normal / tex may be null. Falls back to Visuals.Mat (v0.6) if NFWL/Lit is unavailable.
        /// </summary>
        public static Material PBR(Color color, float smoothness, float metallic = 0f, Texture2D normal = null, float normalScale = 1f,
            Texture2D tex = null, Vector2? tiling = null, Color? emission = null, Color? sheen = null, float cavity = 0f)
        {
            Shader sh = Get(Lit);
            if (sh == null) return Visuals.Mat(color, smoothness, tex, tiling);
            Vector2 t = tiling ?? Vector2.one;
            Color e = emission ?? Color.black;
            Color sn = sheen ?? Color.clear;
            string key = $"pbr{color}{smoothness}{metallic}{(normal != null ? normal.GetInstanceID() : 0)}{normalScale}{(tex != null ? tex.GetInstanceID() : 0)}{t}{e}{sn}{cavity}";
            if (mats.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(sh) { name = "NFWL PBR", enableInstancing = true };
            m.SetColor("_Color", color);
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Occlusion", cavity);
            if (tex != null) m.SetTexture("_MainTex", tex);
            if (normal != null) m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", normalScale);
            m.SetTextureScale("_MainTex", t);
            m.SetColor("_EmissionColor", e);
            m.SetColor("_SheenColor", sn);
            mats[key] = m;
            return m;
        }

        /// <summary>Transparent glowing crystal, with a pulse pattern per rarity. Null if unsupported.</summary>
        public static Material CrystalMat(Color crystal, Rarity rarity, bool emphasise = false)
        {
            Shader sh = Get(Crystal);
            if (sh == null) return null;
            string key = $"crys{crystal}{rarity}{emphasise}";
            if (mats.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(sh) { name = "NFWL Crystal " + rarity, enableInstancing = true };
            CrystalLook look = CrystalLook.For(rarity);
            Color tint = Color.Lerp(crystal, Color.white, 0.35f);
            tint.a = look.opacity;
            m.SetColor("_Color", tint);
            m.SetColor("_GlowColor", crystal * look.glow * (emphasise ? 1.3f : 1f));
            m.SetFloat("_FresnelBoost", look.fresnel);
            m.SetFloat("_PulseSpeed", look.pulseSpeed);
            m.SetFloat("_PulseAmount", look.pulseAmount);
            m.SetFloat("_Sparkle", look.sparkle);
            mats[key] = m;
            return m;
        }

        public static Material Simple(string file, string name)
        {
            Shader sh = Get(file);
            if (sh == null) return null;
            return new Material(sh) { name = name };
        }
    }

    /// <summary>How a rarity looks (PHASE 7.3). Glow stays moderate so outlines and the ball read first.</summary>
    public struct CrystalLook
    {
        public float opacity, glow, fresnel, pulseSpeed, pulseAmount, sparkle;
        public int shoulderShards, armShards, legShards, hairShards;
        public float saturation;

        public static CrystalLook For(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return new CrystalLook { opacity = 0.7f, glow = 0.35f, fresnel = 0.6f, saturation = 0.55f, shoulderShards = 2, armShards = 1, legShards = 1, hairShards = 1 };
                case Rarity.Rare: return new CrystalLook { opacity = 0.72f, glow = 0.55f, fresnel = 0.9f, saturation = 1f, shoulderShards = 3, armShards = 2, legShards = 2, hairShards = 1 };
                case Rarity.Epic: return new CrystalLook { opacity = 0.75f, glow = 0.7f, fresnel = 1.1f, saturation = 1f, shoulderShards = 4, armShards = 3, legShards = 3, hairShards = 2 };
                case Rarity.Mythic: return new CrystalLook { opacity = 0.78f, glow = 0.8f, fresnel = 1.3f, pulseSpeed = 2.2f, pulseAmount = 0.35f, saturation = 1f, shoulderShards = 4, armShards = 3, legShards = 3, hairShards = 3 };
                default: return new CrystalLook { opacity = 0.8f, glow = 0.95f, fresnel = 1.5f, pulseSpeed = 3f, pulseAmount = 0.3f, sparkle = 0.6f, saturation = 1f, shoulderShards = 5, armShards = 4, legShards = 3, hairShards = 4 };
            }
        }
    }
}
