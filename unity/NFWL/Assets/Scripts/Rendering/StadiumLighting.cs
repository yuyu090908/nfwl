using UnityEngine;
using UnityEngine.Rendering;

namespace NFWL
{
    /// <summary>
    /// v0.7 night-match lighting (PHASE 8.2 / 8.3).
    ///
    /// Real stadiums are lit by hundreds of lamps, but real-time shadows from each would be far too slow
    /// (especially in WebGL). So lighting is split in two:
    ///   * Lights that actually light players and grass: ONE shadow-casting "key" floodlight (soft shadows,
    ///     cascades, self-shadowing on the players) from the near-left tower, plus three fill lights from
    ///     the other towers without shadows (per-pixel on High / Ultra, cheap per-vertex on Low). Each comes
    ///     from a real tower direction with its own colour temperature, so a player turning right / left
    ///     changes which side of the face and body is bright, independent of the camera.
    ///   * Decoration that only looks bright: lamp panels, halos and faint light shafts (Pitch / Stadium)
    ///     drawn with an unlit additive shader and picked up by the bloom.
    /// Contact shadows under boots and the ball are separate soft decals (ContactShadow).
    /// </summary>
    public static class StadiumLighting
    {
        public static Light Key { get; private set; }
        static readonly Light[] fills = new Light[3];
        static ReflectionProbe probe;

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

        public static void Setup(Light existingSun)
        {
            // Key: near-left tower (behind the broadcast camera, which sits on -Z), so faces toward the
            // camera are lit and shadows fall away into the pitch where they read clearly.
            Key = existingSun != null ? existingSun : new GameObject("Floodlight Key").AddComponent<Light>();
            Key.name = "Floodlight Key";
            Key.type = LightType.Directional;
            Key.transform.rotation = FromTower(TowerPosition(-1f, -1f));
            Key.color = Kelvin(5200f);
            Key.intensity = 1.05f;
            Key.shadows = LightShadows.Soft;
            Key.shadowStrength = 0.82f;
            Key.shadowBias = 0.03f;
            Key.shadowNormalBias = 0.25f;
            Key.shadowNearPlane = 0.2f;
            Key.shadowResolution = LightShadowResolution.FromQualitySettings;
            Key.renderMode = LightRenderMode.ForcePixel;
            RenderSettings.sun = Key;

            // Fills from the other three towers: no shadows. Warm on the near side, cool on the far side
            // (a back / rim light that separates players from the grass).
            float[] sx = { 1f, -1f, 1f };
            float[] sz = { -1f, 1f, 1f };
            float[] kelvin = { 4300f, 6800f, 6200f };
            float[] intensity = { 0.5f, 0.38f, 0.34f };
            for (int i = 0; i < 3; i++)
            {
                if (fills[i] == null) fills[i] = new GameObject("Floodlight Fill " + (i + 1)).AddComponent<Light>();
                Light l = fills[i];
                l.type = LightType.Directional;
                l.transform.rotation = FromTower(TowerPosition(sx[i], sz[i]));
                l.color = Kelvin(kelvin[i]);
                l.intensity = intensity[i];
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.Auto; // per-pixel only while the quality allows (pixel light count)
            }

            // Ambient: dark night sky above, the lit green pitch bouncing light from below.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.19f, 0.3f);
            RenderSettings.ambientEquatorColor = new Color(0.17f, 0.19f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.16f, 0.09f);
            RenderSettings.ambientIntensity = 1f;

            // Distance fog only far away (stadium structure fades into the night); never over the pitch.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.035f, 0.045f, 0.085f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 170f;

            Material sky = NFWLShaders.Simple(NFWLShaders.Sky, "NFWL Night Sky");
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                Camera cam = Camera.main;
                if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
            }
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.8f;
            DynamicGI.UpdateEnvironment();

            GraphicsQuality.Changed -= OnQuality;
            GraphicsQuality.Changed += OnQuality;
            OnQuality(GraphicsQuality.Current);
        }

        static void OnQuality(GraphicsPreset p)
        {
            if (Key != null) Key.shadows = p.shadows == ShadowQuality.HardOnly ? LightShadows.Hard : p.shadows == ShadowQuality.Disable ? LightShadows.None : LightShadows.Soft;
            // Fills: ranked by importance so the pixel light budget goes to the warm near fill first.
            for (int i = 0; i < fills.Length; i++)
            {
                if (fills[i] == null) continue;
                fills[i].renderMode = i < p.pixelLights ? LightRenderMode.ForcePixel : LightRenderMode.ForceVertex;
            }
            if (p.reflectionProbe) EnsureProbe(); else if (probe != null) probe.enabled = false;
        }

        /// <summary>A reflection probe rendered once from the centre spot: glossy kits, boots, posts and
        /// crystals reflect the actual floodlights and stands instead of a generic sky.</summary>
        static void EnsureProbe()
        {
            if (probe == null)
            {
                probe = new GameObject("Stadium Reflections").AddComponent<ReflectionProbe>();
                probe.transform.position = new Vector3(0f, 3f, 0f);
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                probe.resolution = 128;
                probe.size = new Vector3(140f, 60f, 110f);
                probe.boxProjection = false;
                probe.intensity = 0.9f;
                probe.hdr = true;
                probe.clearFlags = ReflectionProbeClearFlags.Skybox;
                probe.farClipPlane = 150f;
            }
            probe.enabled = true;
        }

        /// <summary>Call after the stadium is built (the probe captures it once).</summary>
        public static void CaptureReflections()
        {
            if (probe != null && probe.enabled) probe.RenderProbe();
        }
    }
}
