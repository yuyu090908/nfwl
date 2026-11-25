using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// v0.7 graphics settings screen (PHASE 13), F3 to open / close.
    /// Quality presets Low / Medium / High / Ultra (applied at once and remembered), automatic quality
    /// (lowers the preset when the frame rate stays under 45 FPS), the ball marker, the v0.7 feature
    /// flags and a live readout of what the preset is doing.
    /// </summary>
    public class GraphicsSettingsPanel : MonoBehaviour
    {
        public bool Open { get; private set; }
        public static bool BallMarker
        {
            get { try { return PlayerPrefs.GetInt("NFWL.BallMarker", 1) != 0; } catch { return true; } }
            set { try { PlayerPrefs.SetInt("NFWL.BallMarker", value ? 1 : 0); PlayerPrefs.Save(); } catch { } }
        }

        GUIStyle box, label, title, button, selected;
        Texture2D bg;
        Ball ball;
        public void Init(Ball b) => ball = b;

        void Update()
        {
            if (KeyInput.Down(KeyCode.F3)) Open = !Open;
        }

        void Styles()
        {
            if (box != null) return;
            bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0.03f, 0.06f, 0.15f, 0.93f));
            bg.Apply();
            box = new GUIStyle { normal = { background = bg }, padding = new RectOffset(16, 16, 12, 12) };
            label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white }, wordWrap = true };
            title = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.78f, 0.2f) } };
            button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            selected = new GUIStyle(button) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.78f, 0.2f) } };
        }

        void OnGUI()
        {
            if (!Open)
            {
                if (Time.unscaledTime - GraphicsQuality.LastAutoMessageTime < 4f)
                {
                    Styles();
                    GUI.Label(new Rect(20f, Screen.height - 60f, 600f, 30f), GraphicsQuality.LastAutoMessage, label);
                }
                return;
            }
            Styles();
            float w = Mathf.Min(560f, Screen.width - 32f), h = Mathf.Min(640f, Screen.height - 32f);
            GUILayout.BeginArea(new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h), box);
            GUILayout.Label("GRAPHICS  画質設定  (F3)", title);
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < 4; i++)
            {
                QualityLevel q = (QualityLevel)i;
                if (GUILayout.Button(q.ToString(), GraphicsQuality.Current.level == q ? selected : button, GUILayout.Height(34f)))
                    GraphicsQuality.Apply(q);
            }
            GUILayout.EndHorizontal();

            GraphicsPreset p = GraphicsQuality.Current;
            GUILayout.Space(6f);
            GUILayout.Label($"Shadows {p.shadowResolution} / {p.shadowDistance:0} m / {p.shadowCascades} cascades   MSAA x{Mathf.Max(1, p.msaa)}   " +
                            $"SSAO {(p.ssao ? p.ssaoIntensity.ToString("0.00") : "off")}   Bloom {(p.bloom ? "on" : "off")}   " +
                            $"Crowd {p.crowdDensity * 100f:0}% ({p.crowdRows3D} rows 3D)   Grass layers {p.grassShells}   " +
                            $"Render scale {p.renderScale * 100f:0}%   Net {p.net}   Textures {(p.textureMipLimit == 0 ? "full" : "half")}", label);
            GUILayout.Label($"FPS {PerfStats.Fps:0} (avg {GraphicsQuality.AverageFps:0})", label);

            GUILayout.Space(8f);
            bool auto = GUILayout.Toggle(GraphicsQuality.AutoAdjust, "  自動で画質を下げる (Auto quality: lower when under 45 FPS)", label);
            if (auto != GraphicsQuality.AutoAdjust) GraphicsQuality.AutoAdjust = auto;
            bool marker = GUILayout.Toggle(BallMarker, "  ボールマーカー (ring under the ball)", label);
            if (marker != BallMarker) BallMarker = marker;
            bool real = GUILayout.Toggle(NFWLFeatures.RealBallSize, "  ボールを実寸で表示 (real ball size; off = 1.6x as v0.6)", label);
            if (real != NFWLFeatures.RealBallSize)
            {
                NFWLFeatures.RealBallSize = real;
                if (ball != null) ball.RefreshVisualScale();
            }
            bool post = GUILayout.Toggle(NFWLFeatures.PostProcessing, "  ポストエフェクト (bloom, colour, SSAO)", label);
            if (post != NFWLFeatures.PostProcessing)
            {
                NFWLFeatures.PostProcessing = post;
                GraphicsQuality.Apply(GraphicsQuality.Current.level, false);
            }

            GUILayout.Space(8f);
            GUILayout.Label("v0.7 機能フラグ（次の試合から反映。R で試合終了後に再開、または Play し直し）", label);
            Flag("NFWL シェーダー (PBR / 芝 / 結晶)", NFWLFeatures.NewShaders, v => NFWLFeatures.NewShaders = v);
            Flag("夜間照明リグ (floodlight rig)", NFWLFeatures.FloodlightRig, v => NFWLFeatures.FloodlightRig = v);
            Flag("人体メッシュの選手 (anatomical model)", NFWLFeatures.AnatomicalModel, v => NFWLFeatures.AnatomicalModel = v);
            Flag("v0.7 スタジアム", NFWLFeatures.NextGenStadium, v => NFWLFeatures.NextGenStadium = v);
            Flag("選手ごとの走り方の違い (motion variation)", NFWLFeatures.MotionVariation, v => NFWLFeatures.MotionVariation = v);
            GUILayout.Space(6f);
            if (GUILayout.Button("すべて初期値に戻す (reset)", button)) { NFWLFeatures.ResetAll(); GraphicsQuality.Apply(GraphicsQuality.DefaultLevel); }
            GUILayout.EndArea();
        }

        void Flag(string text, bool value, System.Action<bool> set)
        {
            bool v = GUILayout.Toggle(value, "  " + text, label);
            if (v != value) set(v);
        }
    }
}
