using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// v0.7 image processing on the match camera (PHASE 8.5 / 8.6), Built-in pipeline:
    /// SSAO (crease occlusion where boots meet grass, arms meet the body, seats meet steps), bloom on
    /// floodlights, LED boards and crystal glow, ACES tone mapping (black kits keep detail, white kits do
    /// not clip), white balance, split toning (cool shadows / warm highlights), contrast, saturation and a
    /// very light vignette. No motion blur, depth of field or lens flare during play.
    /// Strength of everything follows the graphics preset. If the shader is unavailable it does nothing.
    /// Also renders the 3D view at a lower resolution when the preset asks for it (render scale).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class NFWLPostProcess : MonoBehaviour
    {
        [Header("Bloom")]
        public float bloomThreshold = 1.1f;
        public float bloomKnee = 0.5f;
        public float bloomIntensity = 0.5f;
        public float bloomClamp = 20f;

        [Header("Colour")]
        public float exposure = 1.15f;
        public float contrast = 1.08f;
        public float saturation = 1.12f;
        public Color whiteBalance = new Color(1f, 0.99f, 0.97f);
        public Color shadowTint = new Color(0.47f, 0.5f, 0.56f, 0.25f);
        public Color highlightTint = new Color(0.53f, 0.51f, 0.47f, 0.2f);
        [Range(0f, 0.5f)] public float vignette = 0.12f;

        [Header("Ambient occlusion")]
        public float aoRadius = 0.3f;

        Camera cam;
        Material mat;
        GraphicsPreset preset;
        readonly RenderTexture[] down = new RenderTexture[6];
        readonly RenderTexture[] up = new RenderTexture[6];
        RenderTexture scaled;

        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        static readonly int BaseTexId = Shader.PropertyToID("_BaseTex");
        static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        static readonly int AOTexId = Shader.PropertyToID("_AOTex");
        static readonly int UseAOId = Shader.PropertyToID("_UseAO");
        static readonly int AOIntensityId = Shader.PropertyToID("_AOIntensity");
        static readonly int AORadiusId = Shader.PropertyToID("_AORadius");
        static readonly int BlurDirId = Shader.PropertyToID("_BlurDir");

        public bool Active => mat != null && preset.postProcessing && NFWLFeatures.PostProcessing;

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            Shader s = NFWLShaders.Get(NFWLShaders.Post);
            if (s != null) mat = new Material(s) { hideFlags = HideFlags.DontSave };
            GraphicsQuality.Changed += OnQuality;
            OnQuality(GraphicsQuality.Current);
        }

        void OnDisable()
        {
            GraphicsQuality.Changed -= OnQuality;
            if (mat != null) Destroy(mat);
            ReleaseScaled();
        }

        void OnQuality(GraphicsPreset p)
        {
            preset = p;
            if (cam == null) return;
            cam.allowHDR = p.hdr && Active;
            cam.allowMSAA = p.msaa > 0;
            if (Active && p.ssao) cam.depthTextureMode |= DepthTextureMode.Depth;
            else cam.depthTextureMode &= ~DepthTextureMode.Depth;
        }

        // ---------------- render scale ----------------

        void OnPreCull()
        {
            float s = preset.renderScale;
            if (s >= 0.99f || cam == null) { ReleaseScaled(); return; }
            int w = Mathf.Max(64, Mathf.RoundToInt(Screen.width * s));
            int h = Mathf.Max(64, Mathf.RoundToInt(Screen.height * s));
            if (scaled == null || scaled.width != w || scaled.height != h)
            {
                ReleaseScaled();
                scaled = new RenderTexture(w, h, 24, cam.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default)
                {
                    name = "NFWL Scaled View",
                    antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing),
                    filterMode = FilterMode.Bilinear
                };
            }
            cam.targetTexture = scaled;
        }

        void OnPostRender()
        {
            if (scaled == null || cam.targetTexture != scaled) return;
            cam.targetTexture = null;
            Graphics.Blit(scaled, (RenderTexture)null);
        }

        void ReleaseScaled()
        {
            if (scaled == null) return;
            if (cam != null && cam.targetTexture == scaled) cam.targetTexture = null;
            scaled.Release();
            Destroy(scaled);
            scaled = null;
        }

        // ---------------- effects ----------------

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!Active)
            {
                Graphics.Blit(src, dst);
                return;
            }
            RenderTextureFormat fmt = src.format;

            // SSAO (half resolution unless Ultra), depth-aware blur.
            RenderTexture ao = null;
            bool useAO = preset.ssao && preset.ssaoIntensity > 0f && (cam.depthTextureMode & DepthTextureMode.Depth) != 0;
            if (useAO)
            {
                int div = preset.fullResSsao ? 1 : 2;
                RenderTextureFormat aoFmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.Default;
                ao = RenderTexture.GetTemporary(src.width / div, src.height / div, 0, aoFmt);
                RenderTexture tmp = RenderTexture.GetTemporary(ao.width, ao.height, 0, aoFmt);
                mat.SetFloat(AORadiusId, aoRadius);
                Graphics.Blit(src, ao, mat, 3);
                mat.SetVector(BlurDirId, new Vector4(1f, 0f, 0f, 0f));
                Graphics.Blit(ao, tmp, mat, 4);
                mat.SetVector(BlurDirId, new Vector4(0f, 1f, 0f, 0f));
                Graphics.Blit(tmp, ao, mat, 4);
                RenderTexture.ReleaseTemporary(tmp);
            }
            mat.SetFloat(UseAOId, useAO ? 1f : 0f);
            mat.SetFloat(AOIntensityId, preset.ssaoIntensity);
            if (ao != null) mat.SetTexture(AOTexId, ao);

            // Bloom: prefilter at half resolution, down to ~1/64, then back up with a tent filter.
            RenderTexture bloom = null;
            int levels = 0;
            if (preset.bloom)
            {
                float threshold = cam.allowHDR ? bloomThreshold : 0.8f;
                mat.SetVector(ThresholdId, new Vector4(threshold, bloomKnee, bloomClamp, 0f));
                int w = src.width / 2, h = src.height / 2;
                down[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(src, down[0], mat, 0);
                levels = 1;
                while (levels < down.Length && w > 32 && h > 32)
                {
                    w /= 2; h /= 2;
                    down[levels] = RenderTexture.GetTemporary(w, h, 0, fmt);
                    Graphics.Blit(down[levels - 1], down[levels], mat, 1);
                    levels++;
                }
                RenderTexture cur = down[levels - 1];
                for (int i = levels - 2; i >= 0; i--)
                {
                    up[i] = RenderTexture.GetTemporary(down[i].width, down[i].height, 0, fmt);
                    mat.SetTexture(BaseTexId, down[i]);
                    Graphics.Blit(cur, up[i], mat, 2);
                    cur = up[i];
                }
                bloom = cur;
            }
            mat.SetFloat(BloomIntensityId, bloom != null ? bloomIntensity : 0f);
            mat.SetTexture(BloomTexId, bloom != null ? (Texture)bloom : Texture2D.blackTexture);

            mat.SetFloat("_Exposure", cam.allowHDR ? exposure : 1f);
            mat.SetFloat("_Contrast", contrast);
            mat.SetFloat("_Saturation", saturation);
            mat.SetColor("_WhiteBalance", whiteBalance);
            mat.SetColor("_ShadowTint", shadowTint);
            mat.SetColor("_HighlightTint", highlightTint);
            mat.SetFloat("_Vignette", vignette);
            mat.SetFloat("_Tonemap", cam.allowHDR ? 1f : 0f);
            Graphics.Blit(src, dst, mat, 5);

            for (int i = 0; i < levels; i++)
            {
                RenderTexture.ReleaseTemporary(down[i]);
                down[i] = null;
                if (up[i] != null) { RenderTexture.ReleaseTemporary(up[i]); up[i] = null; }
            }
            if (ao != null) RenderTexture.ReleaseTemporary(ao);
        }
    }
}
