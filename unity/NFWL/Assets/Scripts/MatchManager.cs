using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// Runs a Phase 1 match: builds the pitch, spawns the ball and the player, checks goals and
    /// outs every frame, keeps the score and draws the HUD.
    /// Ported from the Unreal prototype (NFWLGameMode / NFWLGameState / NFWLHUD).
    ///
    /// You never have to place this by hand: if a scene has no MatchManager when Play is pressed,
    /// one is created automatically (see Bootstrap below).
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public float goalResetDelay = 2.5f;
        public float outResetDelay = 1.5f;

        public int HomeScore { get; private set; }
        public int AwayScore { get; private set; }

        Ball ball;
        PlayerController player;
        bool resetPending;
        float resetAt;
        string eventText = "";
        float eventTextUntil;
        float matchStartTime;

        GUIStyle scoreStyle, eventStyle, smallStyle;
        Texture2D white;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<MatchManager>() == null)
            {
                new GameObject("NFWL Match").AddComponent<MatchManager>();
            }
        }

        void Start()
        {
            new GameObject("Pitch").AddComponent<Pitch>().Build();

            ball = Ball.Create();
            player = PlayerController.Create(0, new Color(0.1f, 0.3f, 0.95f));
            player.SetBall(ball);

            EnsureLighting();
            EnsureCamera();
            ResetKickoff();
            matchStartTime = Time.time;
        }

        void EnsureLighting()
        {
            Light existing = FindAnyObjectByType<Light>();
            if (existing != null && existing.type == LightType.Directional) return; // the scene already has a sun
            GameObject sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        void EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.07f, 0.16f); // dark navy (Master Doc UI concept)
            MatchCamera follow = cam.GetComponent<MatchCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<MatchCamera>();
            follow.target = player.transform;
        }

        void Update()
        {
            if (KeyInput.Down(KeyCode.R)) ResetKickoff();
            if (resetPending)
            {
                if (Time.time >= resetAt) ResetKickoff();
                return;
            }

            switch (Rules.ClassifyBall(ball.transform.position))
            {
                case BallState.GoalForHome: HandleGoal(true); break;
                case BallState.GoalForAway: HandleGoal(false); break;
                case BallState.Out: HandleOut(); break;
            }
        }

        void HandleGoal(bool homeScored)
        {
            if (homeScored) HomeScore++; else AwayScore++;
            ShowEvent("GOAL!", goalResetDelay);
            Debug.Log($"GOAL for {(homeScored ? "HOME" : "AWAY")}. Score {HomeScore} - {AwayScore}");
            ScheduleReset(goalResetDelay);
        }

        void HandleOut()
        {
            // Phase 1: restart from the centre. Kick-ins, corners and goal kicks come in Phase 2 (Full Spec 3.3).
            ShowEvent("OUT", outResetDelay);
            ScheduleReset(outResetDelay);
        }

        void ScheduleReset(float delay)
        {
            resetPending = true;
            resetAt = Time.time + delay;
        }

        void ShowEvent(string text, float seconds)
        {
            eventText = text;
            eventTextUntil = Time.time + seconds;
        }

        public void ResetKickoff()
        {
            ball.ResetBall(new Vector3(0f, PitchDims.BallRadius + 0.02f, 0f));

            float side = player.teamIndex == 0 ? -1f : 1f;
            Quaternion facing = Quaternion.Euler(0f, player.teamIndex == 0 ? 90f : -90f, 0f); // +X or -X
            player.Teleport(new Vector3(side * 1.5f, 0f, 0f), facing);
            player.ReleaseBall(0f);
            resetPending = false;
        }

        // -------------------------------------------------------------------
        // HUD
        // -------------------------------------------------------------------

        void InitStyles()
        {
            if (scoreStyle != null) return;
            white = Texture2D.whiteTexture;
            scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            scoreStyle.normal.textColor = Color.white;
            eventStyle = new GUIStyle(scoreStyle) { fontSize = 72 };
            eventStyle.normal.textColor = new Color(1f, 0.85f, 0.1f);
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            smallStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        }

        void DrawRect(float x, float y, float w, float h, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), white);
            GUI.color = old;
        }

        void LateUpdate()
        {
            KeyInput.EndFrame();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) KeyInput.ReleaseAll();
        }

        void OnGUI()
        {
            KeyInput.HandleEvent(Event.current);
            InitStyles();
            float cx = Screen.width * 0.5f;

            // Score and match clock.
            int secs = Mathf.FloorToInt(Time.time - matchStartTime);
            string score = $"HOME  {HomeScore} - {AwayScore}  AWAY";
            DrawRect(cx - 190f, 12f, 380f, 72f, new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(new Rect(cx - 190f, 14f, 380f, 40f), score, scoreStyle);
            GUI.Label(new Rect(cx - 190f, 52f, 380f, 28f), $"{secs / 60:00}:{secs % 60:00}", smallStyle.WithCenter());

            if (!string.IsNullOrEmpty(eventText) && Time.time < eventTextUntil)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.25f, Screen.width, 100f), eventText, eventStyle);
            }

            if (player != null)
            {
                // Power gauge.
                float ratio = player.ChargeRatio;
                if (ratio >= 0f)
                {
                    const float barW = 300f, barH = 18f;
                    float x = cx - barW * 0.5f;
                    float y = Screen.height - 110f;
                    DrawRect(x - 3f, y - 3f, barW + 6f, barH + 6f, new Color(0f, 0f, 0f, 0.6f));
                    DrawRect(x, y, barW * ratio, barH, Color.Lerp(Color.green, Color.red, ratio));
                    GUI.Label(new Rect(x, y - 24f, barW, 22f), player.ChargeLabel, smallStyle);
                }

                if (player.HasBall)
                {
                    GUIStyle s = new GUIStyle(smallStyle) { fontStyle = FontStyle.Bold };
                    s.normal.textColor = Color.green;
                    GUI.Label(new Rect(20f, Screen.height - 75f, 200f, 24f), "BALL", s);
                }
            }

            GUI.Label(new Rect(20f, Screen.height - 45f, Screen.width - 40f, 24f),
                "Move: WASD / Arrows   Sprint: Shift   Pass: J   Shoot: K (hold = power)   Long pass: L   Reset: R",
                smallStyle);

            string keyInfo = string.IsNullOrEmpty(KeyInput.LastKey)
                ? "Click the game screen once, then use the keyboard."
                : "Last key: " + KeyInput.LastKey;
            GUI.Label(new Rect(20f, Screen.height - 22f, Screen.width - 40f, 22f), keyInfo, smallStyle);
        }
    }

    static class GUIStyleExt
    {
        public static GUIStyle WithCenter(this GUIStyle s) => new GUIStyle(s) { alignment = TextAnchor.MiddleCenter };
        public static GUIStyle WithSize(this GUIStyle s, int size) => new GUIStyle(s) { fontSize = size, alignment = TextAnchor.UpperLeft };
    }
}
