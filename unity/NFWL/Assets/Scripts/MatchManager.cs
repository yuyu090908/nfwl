using System.Collections.Generic;
using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// Runs a 5 vs 5 match against the CPU: builds the stadium, spawns both teams and the ball,
    /// switches the user's player, checks goals and outs, keeps the clock and score, and draws the HUD.
    /// Ported from the Unreal prototype (GameMode / GameState / HUD) and extended toward Phase 2.
    ///
    /// You never have to place this by hand: if a scene has no MatchManager when Play is pressed,
    /// one is created automatically (see Bootstrap below).
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        [Header("Rules (Master Doc 3.1: 20 minutes = 2 x 10)")]
        public float halfLengthSeconds = 600f;
        public float goalResetDelay = 3f;
        public float outResetDelay = 1.5f;

        [Header("Teams")]
        public string homeName = "NFWL CRYSTALIA";
        public string homeShort = "CRY";
        public string awayName = "FROST UNITED";
        public string awayShort = "FRO";
        [Range(40, 99)] public int awayLevel = 66;

        public int HomeScore { get; private set; }
        public int AwayScore { get; private set; }
        public bool PlayersFrozen => fullTime || (replay != null && replay.Playing);

        public const string Version = "NFWL Unity v0.7 Real Football Motion & Photoreal Stadium";

        static readonly Color HomeColor = new Color(0.55f, 0.32f, 0.98f); // NFWL crystal violet
        static readonly Color AwayColor = new Color(0.55f, 0.7f, 0.95f);  // frost blue
        static readonly Color Gold = new Color(1f, 0.78f, 0.2f);
        static readonly Color Cyan = new Color(0.1f, 0.85f, 1f);
        static readonly Color Navy = new Color(0.03f, 0.06f, 0.15f, 0.88f);

        Ball ball;
        readonly List<PlayerController> home = new List<PlayerController>();
        readonly List<PlayerController> away = new List<PlayerController>();
        readonly List<PlayerController> all = new List<PlayerController>();
        string kickLabel = "";
        float kickLabelUntil;
        PlayerController human;
        Transform marker;
        Transform cameraAnchor;
        Transform ballMarker;

        bool resetPending;
        float resetAt;
        string eventText = "";
        float eventTextUntil;
        float clock;
        int half = 1;
        bool fullTime;
        string lastScorer = "";
        int kickoffTeam;

        GUIStyle bigStyle, eventStyle, smallStyle, tagStyle, cardTitle;
        Texture2D white;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<MatchManager>() == null)
            {
                new GameObject("NFWL Match").AddComponent<MatchManager>();
            }
        }

        public IEnumerable<PlayerController> Team(int teamIndex) => teamIndex == 0 ? home : away;
        /// <summary>The team as a list (no enumerator allocation; read only).</summary>
        public List<PlayerController> TeamList(int teamIndex) => teamIndex == 0 ? home : away;

        readonly TeamAI[] teamAI = new TeamAI[2];
        /// <summary>Team-level AI (PHASE 8): roles and target spots for each AI player.</summary>
        public TeamAI TeamAIFor(int teamIndex) => teamAI[teamIndex];

        void Start()
        {
            // v0.7: graphics preset first, so the stadium builds crowd / grass / net at the right detail.
            GraphicsQuality.Apply(GraphicsQuality.SavedLevel, false);
            new GameObject("Stadium").AddComponent<Pitch>().Build();
            ball = Ball.Create();

            SpawnTeams();
            all.AddRange(home);
            all.AddRange(away);
            teamAI[0] = new TeamAI(this, 0, home, away);
            teamAI[1] = new TeamAI(this, 1, away, home);
            foreach (PlayerController p in AllPlayers())
            {
                p.Match = this;
                p.SetBall(ball);
            }
            SetHuman(home[home.Count - 1]); // start with the forward

            marker = Visuals.Part(PrimitiveType.Cylinder, null, Vector3.zero, new Vector3(1.1f, 0.003f, 1.1f),
                Visuals.Mat(new Color(1f, 0.85f, 0.15f), 0.2f), "ControlMarker").transform;
            marker.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ballMarker = Visuals.Part(PrimitiveType.Cylinder, null, Vector3.zero, new Vector3(0.7f, 0.002f, 0.7f),
                Visuals.Mat(new Color(1f, 1f, 1f), 0.1f), "BallMarker").transform;
            ballMarker.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            cameraAnchor = new GameObject("CameraAnchor").transform;
            EnsureLighting();
            EnsureCamera();
            // v0.6 presentation: replay recorder, procedural sound.
            matchCam = Camera.main != null ? Camera.main.GetComponent<MatchCamera>() : null;
            if (matchCam != null) matchCam.Ball = ball;
            replay = gameObject.AddComponent<ReplaySystem>();
            replay.Init(ball, all, matchCam);
            sfx = SoundFX.Create(transform);
            ball.Impact += (kind, speed) => sfx.OnBallImpact(kind, speed, ball.transform.position);
            // v0.7: crowd / LED atmosphere, ball contact shadow, graphics settings (F3).
            atmosphere = gameObject.AddComponent<MatchAtmosphere>();
            atmosphere.Init(ball);
            graphicsPanel = gameObject.AddComponent<GraphicsSettingsPanel>();
            graphicsPanel.Init(ball);
            ResetKickoff();
        }

        MatchCamera matchCam;
        MatchAtmosphere atmosphere;
        GraphicsSettingsPanel graphicsPanel;
        int framesSinceStart;
        ReplaySystem replay;
        SoundFX sfx;
        bool replayQueued;
        float replayQueuedAt;
        string replayTitle;
        float replayFrom, replayTo, replaySpeed, replayYaw, replayPitch, replayDist;
        PlayerController replaySubject;
        float lastSaveReplay = -100f;
        float goalTime;

        /// <summary>True between a goal / out and the restart.</summary>
        public bool ResetPending => resetPending;
        /// <summary>Optional player data (set before Start). Null = Resources/NFWLPlayers.json if any, else random.</summary>
        public static IPlayerDataSource DataSource { get; set; }
        public int Half => half;
        public bool FullTime => fullTime;
        public float Clock => clock;
        public PlayerController Human => human;
        public Ball MatchBall => ball;
        /// <summary>F4 / tests: the user's player is also played by the AI (CPU vs CPU).</summary>
        public bool AutoPlay { get; set; }
        /// <summary>Tests: AI players stand still (physics, kicks, tackles and saves still run).</summary>
        public bool AIPaused { get; set; }
        /// <summary>Goal / save replays on or off (tests run without them).</summary>
        public bool ReplaysEnabled { get; set; } = true;
        public bool ShowAIDebug { get => showAIDebug; set => showAIDebug = value; }
        public bool ShowPerf { get => showPerf; set => showPerf = value; }
        public bool ReplayPlaying => replay != null && replay.Playing;

        IEnumerable<PlayerController> AllPlayers()
        {
            foreach (PlayerController p in home) yield return p;
            foreach (PlayerController p in away) yield return p;
        }

        // -------------------------------------------------------------------
        // Teams
        // -------------------------------------------------------------------

        struct Slot
        {
            public Role role; public int number; public string name; public Vector2 spot;
            public Slot(Role r, int n, string nm, Vector2 s) { role = r; number = n; name = nm; spot = s; }
        }

        void SpawnTeams()
        {
            var rnd = new System.Random(2026);
            // Formation 2-1-1 (Master Doc 3.5), written for a team attacking +X.
            Slot[] homeSlots =
            {
                new Slot(Role.GK, 1, "K. AOKI", new Vector2(-19.8f, 0f)),
                new Slot(Role.DF, 4, "R. SATO", new Vector2(-12f, -5f)),
                new Slot(Role.DF, 5, "D. NOVAK", new Vector2(-12f, 5f)),
                new Slot(Role.MF, 8, "M. SILVA", new Vector2(-5f, 0f)),
                new Slot(Role.FW, 10, "S. TANAKA", new Vector2(2f, 0f)),
            };
            Slot[] awaySlots =
            {
                new Slot(Role.GK, 1, "B. MORRIS", new Vector2(-19.8f, 0f)),
                new Slot(Role.DF, 3, "L. GARCIA", new Vector2(-12f, -5f)),
                new Slot(Role.DF, 6, "T. HANSEN", new Vector2(-12f, 5f)),
                new Slot(Role.MF, 7, "J. OKAFOR", new Vector2(-5f, 0f)),
                new Slot(Role.FW, 9, "P. ROSSI", new Vector2(2f, 0f)),
            };

            // Crystalia players from yu's designs: each slot has its own crystal element.
            Element[] homeElements = { Element.Amethyst, Element.Sapphire, Element.Emerald, Element.Gold, Element.Violet };
            Element[] awayElements = { Element.Emerald, Element.Ice, Element.Ice, Element.Sapphire, Element.Ice };
            Rarity[] rarities = { Rarity.Rare, Rarity.Common, Rarity.Common, Rarity.Epic, Rarity.Mythic };

            // PHASE 11: players from data (Resources/NFWLPlayers.json, e.g. NFT metadata) when present.
            // The random generator below still runs in the same order, so slots without data are unchanged.
            IPlayerDataSource source = DataSource ?? JsonPlayerSource.FromResources();

            for (int t = 0; t < 2; t++)
            {
                Slot[] slots = t == 0 ? homeSlots : awaySlots;
                for (int i = 0; i < slots.Length; i++)
                {
                    Slot s = slots[i];
                    int level = t == 0 ? 70 + (int)rarities[i] * 4 : awayLevel;
                    PlayerAttributes attrs = PlayerAttributes.Random(level, rnd);
                    if (s.role == Role.GK) attrs.Goalkeeping = Mathf.Clamp(level + 8, 40, 99);
                    // v0.6: preferred foot and weak foot from a separate generator, so the abilities above
                    // stay exactly as in v0.5. About one player in four is left-footed.
                    var footRnd = new System.Random(5000 + t * 100 + s.number);
                    attrs.PreferredFoot = footRnd.NextDouble() < 0.25 ? PreferredFoot.Left : PreferredFoot.Right;
                    attrs.WeakFootRating = Mathf.Clamp(2 + footRnd.Next(0, 3) + (t == 0 && rarities[i] >= Rarity.Mythic ? 1 : 0), 1, 5);
                    // Kits from the design sheet: HOME black, AWAY white, keepers THIRD purple / teal.
                    KitStyle kit = s.role == Role.GK ? (t == 0 ? KitStyle.Third : KitStyle.AwayKeeper) : (t == 0 ? KitStyle.Home : KitStyle.Away);
                    Rarity rarity = t == 0 ? rarities[i] : (Rarity)rnd.Next(0, 3);
                    string name = s.name;
                    PlayerData data = null;
                    if (source != null && source.TryGet(t, i, out data))
                    {
                        attrs = data.BuildAttributes(attrs);
                        if (data.hasRarity) rarity = data.rarity;
                        if (!string.IsNullOrEmpty(data.name)) name = data.name;
                    }
                    // Web platform data may also choose the crystal element and shirt number (Web/WebBridge.cs).
                    Element element = t == 0 ? homeElements[i] : awayElements[i];
                    int number = s.number;
                    if (data != null)
                    {
                        if (CrystalArt.TryElement(data.element, out Element e)) element = e;
                        if (data.number > 0) number = data.number;
                    }
                    PlayerController p = PlayerController.Create(t, s.role, number, name,
                        rarity, attrs, kit,
                        element, rnd.Next(0, 4),
                        0.97f + (float)rnd.NextDouble() * 0.08f);
                    if (data != null) p.SetSkills(data.skills);
                    p.formationSpot = s.spot;
                    (t == 0 ? home : away).Add(p);
                }
            }
        }

        void SetHuman(PlayerController p)
        {
            if (p == null || p == human) return;
            if (human != null) { human.IsHuman = false; human.OnControlChanged(); }
            human = p;
            human.IsHuman = true;
            human.OnControlChanged();
        }

        /// <summary>Called by a player when he wins the ball.</summary>
        public void OnPossession(PlayerController p)
        {
            // Control follows the ball inside the user's team (not to the goalkeeper).
            if (p.teamIndex == 0 && p.role != Role.GK) SetHuman(p);
        }

        /// <summary>The one AI player of a team who presses the ball (the user's player counts for HOME).</summary>
        public PlayerController ChaserFor(int teamIndex)
        {
            PlayerController best = null;
            float bestD = float.MaxValue;
            Vector3 b = ball.transform.position;
            foreach (PlayerController p in TeamList(teamIndex))
            {
                if (p.role == Role.GK) continue;
                float d = (p.transform.position - b).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best != null && best.UserControlled ? null : best;
        }

        public PlayerController NearestOpponent(PlayerController me, out float dist)
        {
            PlayerController best = null;
            float bestD = float.MaxValue;
            foreach (PlayerController p in TeamList(1 - me.teamIndex))
            {
                float d = Vector3.Distance(p.transform.position, me.transform.position);
                if (d < bestD) { bestD = d; best = p; }
            }
            dist = bestD;
            return best;
        }

        /// <summary>A team-mate who is open and preferably further forward.</summary>
        public PlayerController BestPassTarget(PlayerController me)
        {
            PlayerController best = null;
            float bestScore = float.MinValue;
            foreach (PlayerController mate in TeamList(me.teamIndex))
            {
                if (mate == me || mate.role == Role.GK) continue;
                Vector3 to = mate.transform.position - me.transform.position;
                float dist = to.magnitude;
                if (dist < 3f || dist > 30f) continue;
                NearestOpponent(mate, out float space);
                float forward = to.x * me.AttackSign;
                float score = forward * 0.4f + Mathf.Min(space, 6f) * 1.2f - dist * 0.1f + Random.Range(0f, 1.5f);
                if (score > bestScore) { bestScore = score; best = mate; }
            }
            return best;
        }

        // -------------------------------------------------------------------
        // Scene setup
        // -------------------------------------------------------------------

        void EnsureLighting()
        {
            Light sun = null;
            Light existing = FindAnyObjectByType<Light>();
            if (existing != null && existing.type == LightType.Directional) sun = existing;
            if (NFWLFeatures.FloodlightRig)
            {
                // v0.7: one shadow-casting floodlight + coloured fills, night sky, reflections (Rendering/StadiumLighting.cs).
                StadiumLighting.Setup(sun);
                return;
            }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            // Evening match under floodlights.
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.97f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            sun.transform.rotation = Quaternion.Euler(58f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.5f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.36f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.15f, 0.2f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.08f, 0.18f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 110f;
            QualitySettings.shadowDistance = 70f;
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
            bool sky = NFWLFeatures.FloodlightRig && RenderSettings.skybox != null && RenderSettings.skybox.shader == NFWLShaders.Get(NFWLShaders.Sky);
            cam.clearFlags = sky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.08f, 0.18f); // dark navy night sky (Master Doc UI concept)
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 260f;
            cam.allowMSAA = true;
            // v0.7: MSAA, HDR, shadows etc. come from the graphics preset (GraphicsQuality); post effects on the camera.
            if (cam.GetComponent<NFWLPostProcess>() == null) cam.gameObject.AddComponent<NFWLPostProcess>();
            MatchCamera follow = cam.GetComponent<MatchCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<MatchCamera>();
            follow.target = cameraAnchor;
        }

        // -------------------------------------------------------------------
        // Match flow
        // -------------------------------------------------------------------

        void Update()
        {
            PerfStats.EndFrame();
            GraphicsQuality.TickAuto(Time.unscaledDeltaTime);
            if (++framesSinceStart == 3) StadiumLighting.CaptureReflections(); // once the stadium has rendered
            if (atmosphere != null) atmosphere.Tick(fullTime ? 0f : Danger(), PlayersFrozen);
            if (ballMarker != null) ballMarker.gameObject.SetActive(GraphicsSettingsPanel.BallMarker);
            if (KeyInput.Down(KeyCode.F2)) showPerf = !showPerf;
            if (KeyInput.Down(KeyCode.F4)) AutoPlay = !AutoPlay;
            if (replay != null && replay.Playing)
            {
                // Any action key skips the replay.
                if (KeyInput.Down(KeyCode.Space) || KeyInput.Down(KeyCode.J) || KeyInput.Down(KeyCode.K) || KeyInput.Down(KeyCode.L)
                    || KeyInput.Down(KeyCode.Return) || KeyInput.Down(KeyCode.R)) replay.Stop();
                return;
            }
            if (KeyInput.Down(KeyCode.M) && sfx != null) sfx.ToggleMute();
            if (replayQueued && Time.time >= replayQueuedAt)
            {
                replayQueued = false;
                StartQueuedReplay();
            }
            if (KeyInput.Down(KeyCode.R))
            {
                if (fullTime) RestartMatch(); else ResetKickoff();
            }
            if (KeyInput.Down(KeyCode.Q) && !fullTime) SwitchToNearest();
            if (KeyInput.Down(KeyCode.F1)) showAIDebug = !showAIDebug; // v0.6: AI role labels
            if (KeyInput.Down(KeyCode.Z) && Camera.main != null)
            {
                // Z: camera presets (v0.7): Broadcast 16 m -> Wide 24 m -> Tactical -> Close 11 m.
                MatchCamera mc = Camera.main.GetComponent<MatchCamera>();
                if (mc != null)
                {
                    mc.NextPreset();
                    ShowEvent("CAMERA  " + mc.CurrentPreset.name.ToUpper(), 1.2f);
                }
            }

            UpdateCameraAnchor();
            UpdateMarker();

            if (fullTime) return;
            if (sfx != null) sfx.SetDanger(Danger());
            if (teamAI[0] != null && !resetPending)
            {
                long t0 = PerfStats.Begin();
                teamAI[0].Tick(ball, Time.time);
                teamAI[1].Tick(ball, Time.time);
                PerfStats.End(PerfStats.Slot.AI, t0);
            }

            if (!resetPending)
            {
                clock += Time.deltaTime;
                if (clock >= halfLengthSeconds * half)
                {
                    clock = halfLengthSeconds * half;
                    if (half == 1)
                    {
                        half = 2;
                        ShowEvent("HALF TIME", 2.5f);
                        if (sfx != null) sfx.Whistle(true);
                        ScheduleReset(2.5f);
                    }
                    else
                    {
                        fullTime = true;
                        ShowEvent("FULL TIME", 9999f);
                        if (sfx != null) sfx.Whistle(true);
                    }
                    return;
                }
            }

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

        void LateUpdate()
        {
            KeyInput.EndFrame();
        }

        void FixedUpdate()
        {
            // Ball against bodies (legs, torso, head) - v0.6 PHASE 4.4.
            long t0 = PerfStats.Begin();
            if (!fullTime) BallContacts.Step(ball, all, Time.fixedDeltaTime, Time.time);
            PerfStats.End(PerfStats.Slot.Contacts, t0);
        }

        /// <summary>All ten players (read only use).</summary>
        public List<PlayerController> AllPlayersList => all;

        /// <summary>Shows the name of a kick (FINESSE SHOT, THROUGH PASS...) for a moment.</summary>
        public void ShowKickLabel(PlayerController kicker, string label)
        {
            kickLabel = (kicker.teamIndex == 0 ? homeShort : awayShort) + " #" + kicker.number + "  " + label;
            kickLabelUntil = Time.time + 1.6f;
        }

        /// <summary>Two players bumped (shoulder challenge or collision).</summary>
        public void OnPlayerContact(PlayerController a, PlayerController b, float impulse, bool shoulder)
        {
            if (shoulder && a.teamIndex != b.teamIndex && (a.HasBall || b.HasBall)) ShowKickLabel(a.HasBall ? b : a, "SHOULDER CHALLENGE");
        }

        /// <summary>A goalkeeper got his hands to a shot (GoalkeeperController, PHASE 6.2).</summary>
        public void OnSave(PlayerController keeper, SaveType type, string result)
        {
            ShowEvent("SAVE!", 1.0f);
            string how;
            switch (type)
            {
                case SaveType.HighDive: how = "FINGERTIP SAVE"; break;
                case SaveType.LowDive: how = "LOW DIVING SAVE"; break;
                case SaveType.DiveLeft:
                case SaveType.DiveRight: how = "DIVING SAVE"; break;
                case SaveType.HighCatch: how = "HIGH CATCH"; break;
                case SaveType.LowCatch: how = "LOW CATCH"; break;
                case SaveType.Punch: how = "PUNCHED CLEAR"; break;
                default: how = "SAVE"; break;
            }
            ShowKickLabel(keeper, $"{how} ({result})");
            if (sfx != null) sfx.Ooh(keeper.transform.position);
            // Save replay: only when the keeper holds a diving / high catch (play stops anyway), at most every 20 s.
            bool spectacular = type == SaveType.DiveLeft || type == SaveType.DiveRight || type == SaveType.HighDive
                               || type == SaveType.LowDive || type == SaveType.HighCatch;
            if (result == "CATCH" && spectacular && Time.time - lastSaveReplay > 20f && !resetPending)
            {
                lastSaveReplay = Time.time;
                float gx = -keeper.AttackSign * PitchDims.HalfLength;
                QueueReplay("SAVE REPLAY", Time.time + 0.9f, Time.time - 1.6f, Time.time + 0.5f, 0.45f,
                    Mathf.Sign(gx) * 90f, 16f, 7.5f, keeper);
            }
        }

        /// <summary>A shot was struck (PlayerController): shot camera and crowd.</summary>
        public void OnShot(PlayerController shooter)
        {
            if (matchCam != null) matchCam.ShotCam(shooter.AttackGoalCenter.x);
            if (sfx != null) sfx.SetDanger(1f);
        }

        void QueueReplay(string title, float at, float from, float to, float speed, float yaw, float pitch, float dist, PlayerController subject)
        {
            if (!ReplaysEnabled) return;
            replayQueued = true;
            replayQueuedAt = at;
            replayTitle = title;
            replayFrom = from;
            replayTo = to;
            replaySpeed = speed;
            replayYaw = yaw;
            replayPitch = pitch;
            replayDist = dist;
            replaySubject = subject;
        }

        void StartQueuedReplay()
        {
            PlayerController subject = replaySubject;
            Vector3 goal = new Vector3(Mathf.Sign(Mathf.Sin(replayYaw * Mathf.Deg2Rad)) * PitchDims.HalfLength, 0f, 0f);
            replay.Play(replayTitle, replayFrom, Mathf.Min(replayTo, Time.time), replaySpeed, replayYaw, replayPitch, replayDist,
                t =>
                {
                    Vector3 b = replay.BallAt(t);
                    b.y = Mathf.Min(b.y, 1f);
                    // Frame the ball with the goal (goal replay) or with the keeper (save replay).
                    Vector3 other = subject != null ? subject.transform.position : goal;
                    return Vector3.Lerp(b, other, 0.3f);
                }, null);
        }

        /// <summary>The scorer runs off toward the corner; AI team-mates join him.</summary>
        void Celebrate(PlayerController scorer)
        {
            float sx = Mathf.Sign(scorer.AttackSign);
            float sz = scorer.transform.position.z >= 0f ? 1f : -1f;
            Vector3 corner = new Vector3(sx * (PitchDims.HalfLength - 3f), 0f, sz * (PitchDims.HalfWidth - 3f));
            float until = Time.time + goalResetDelay;
            if (!scorer.UserControlled) scorer.CallRun(corner, until);
            int k = 0;
            foreach (PlayerController m in TeamList(scorer.teamIndex))
            {
                if (m == scorer || m.UserControlled || m.role == Role.GK) continue;
                Vector3 offset = new Vector3(-sx * (1.5f + k), 0f, (k % 2 == 0 ? -1f : 1f) * 1.2f);
                m.CallRun((scorer.UserControlled ? scorer.transform.position : corner) + offset, until);
                k++;
            }
        }

        /// <summary>How dangerous the play is right now (0..1): ball near a goal and moving at it.</summary>
        float Danger()
        {
            Vector3 b = ball.transform.position;
            float d = PitchDims.HalfLength - Mathf.Abs(b.x);
            float near = Mathf.InverseLerp(16f, 4f, d) * Mathf.InverseLerp(10f, 3f, Mathf.Abs(b.z));
            float toward = Mathf.Clamp01(ball.Velocity.x * Mathf.Sign(b.x) / 20f);
            return Mathf.Clamp01(near * 0.7f + near * toward * 0.6f);
        }

        /// <summary>The ball hit a player's body (a block, a chest, a head).</summary>
        public void OnBodyContact(PlayerController p, string part, float speed)
        {
            if (speed > 10f && p.teamIndex != (ball.LastTouchedBy != null ? ball.LastTouchedBy.teamIndex : -1))
                ShowKickLabel(p, part == "Head" ? "HEAD BLOCK" : "BLOCK");
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) KeyInput.ReleaseAll();
        }

        /// <summary>Q: control the team-mate nearest the ball.</summary>
        public void SwitchToNearest()
        {
            PlayerController best = null;
            float bestD = float.MaxValue;
            foreach (PlayerController p in home)
            {
                if (p == human || p.role == Role.GK) continue;
                float d = (p.transform.position - ball.transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            SetHuman(best);
        }

        void UpdateCameraAnchor()
        {
            // Follow the ball, leaning toward the user's player so he stays on screen.
            Vector3 b = ball.transform.position;
            Vector3 h = human != null ? human.transform.position : b;
            Vector3 target = Vector3.Lerp(b, h, matchCam != null ? matchCam.PlayerPriority : 0.35f);
            target.y = 0f;
            target.x = Mathf.Clamp(target.x, -PitchDims.HalfLength + 6f, PitchDims.HalfLength - 6f);
            target.z = Mathf.Clamp(target.z, -PitchDims.HalfWidth + 6f, PitchDims.HalfWidth - 4f);
            cameraAnchor.position = target;
        }

        void UpdateMarker()
        {
            if (ballMarker != null)
            {
                // White ring on the grass under the ball, so you can see where it is even in the air.
                Vector3 b = ball.transform.position;
                ballMarker.position = new Vector3(b.x, 0.03f, b.z); // above the v0.7 short grass and lines
                float k = 0.7f + Mathf.Clamp(b.y, 0f, 3f) * 0.15f;
                ballMarker.localScale = new Vector3(k, 0.002f, k);
            }
            if (marker == null || human == null) return;
            marker.position = human.transform.position + Vector3.up * 0.032f;
            float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.06f;
            marker.localScale = new Vector3(1.1f * pulse, 0.003f, 1.1f * pulse);
        }

        void HandleGoal(bool homeScored)
        {
            if (homeScored) HomeScore++; else AwayScore++;
            ShowEvent("GOAL!", goalResetDelay);
            if (atmosphere != null) atmosphere.OnGoal();
            PlayerController scorer = ball.LastTouchedBy;
            if (scorer != null && scorer.teamIndex == (homeScored ? 0 : 1))
            {
                if (scorer.Model != null) scorer.Model.PlayCelebrate();
                lastScorer = $"{scorer.displayName}  #{scorer.number}";
                Celebrate(scorer);
            }
            else lastScorer = "";
            if (sfx != null) { sfx.Goal(); sfx.Whistle(); }
            // Goal replay: from behind and to the side of the goal, slowed down, after the first cheer.
            goalTime = Time.time;
            float gx = (homeScored ? 1f : -1f) * PitchDims.HalfLength;
            QueueReplay("GOAL REPLAY", Time.time + 1.4f, goalTime - 4.2f, goalTime + 0.7f, 0.6f,
                Mathf.Sign(gx) * 90f - Mathf.Sign(gx) * 38f, 20f, 10f, null);
            Debug.Log($"GOAL for {(homeScored ? "HOME" : "AWAY")}. Score {HomeScore} - {AwayScore}");
            ScheduleReset(goalResetDelay);
            kickoffTeam = homeScored ? 1 : 0;
        }

        void HandleOut()
        {
            // Restart from the centre for now. Kick-ins, corners and goal kicks come next (Full Spec 3.3).
            ShowEvent("OUT", outResetDelay);
            // Just wide of a post: the crowd reacts.
            Vector3 bp = ball.transform.position;
            if (sfx != null && Mathf.Abs(bp.x) > PitchDims.HalfLength && Mathf.Abs(bp.z) < PitchDims.GoalHalfWidth + 3f) sfx.Ooh(bp);
            ScheduleReset(outResetDelay);
            PlayerController last = ball.LastTouchedBy;
            if (last != null) kickoffTeam = 1 - last.teamIndex;
        }

        void ScheduleReset(float delay)
        {
            resetPending = true;
            resetAt = Time.time + delay;
        }

        public void ShowEvent(string text, float seconds)
        {
            eventText = text;
            eventTextUntil = Time.time + seconds;
        }

        void RestartMatch()
        {
            HomeScore = AwayScore = 0;
            clock = 0f;
            half = 1;
            fullTime = false;
            eventText = "";
            kickoffTeam = 0;
            ResetKickoff();
        }

        public void ResetKickoff()
        {
            ball.ResetBall(new Vector3(0f, PitchDims.BallRadius + 0.02f, 0f));

            foreach (PlayerController p in AllPlayers())
            {
                float s = p.AttackSign;
                Vector2 spot = p.formationSpot;
                // Everyone in his own half; the kick-off team's forward stands at the ball.
                float x = Mathf.Min(spot.x, -1.2f);
                float z = spot.y;
                if (p.role == Role.FW) { x = p.teamIndex == kickoffTeam ? -0.9f : -3.5f; z = 0f; }
                if (p.role == Role.MF && p.teamIndex != kickoffTeam) x = Mathf.Min(x, -PitchDims.CenterRadius - 1f);
                Quaternion facing = Quaternion.Euler(0f, s > 0f ? 90f : -90f, 0f);
                p.Teleport(new Vector3(x * s, 0f, z), facing);
                p.ReleaseBall(0.2f);
            }
            if (kickoffTeam == 0) SetHuman(home[home.Count - 1]);
            if (teamAI[0] != null) { teamAI[0].Reset(); teamAI[1].Reset(); }
            if (sfx != null) sfx.Whistle();
            replayQueued = false;
            resetPending = false;
        }

        // -------------------------------------------------------------------
        // HUD
        // -------------------------------------------------------------------

        void InitStyles()
        {
            if (bigStyle != null) return;
            white = Texture2D.whiteTexture;
            bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bigStyle.normal.textColor = Color.white;
            eventStyle = new GUIStyle(bigStyle) { fontSize = 84 };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            smallStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            tagStyle = new GUIStyle(smallStyle) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            cardTitle = new GUIStyle(smallStyle) { fontSize = 18, fontStyle = FontStyle.Bold };
        }

        void Box(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void Label(Rect r, string text, GUIStyle style, Color color, bool shadow = true)
        {
            Color old = style.normal.textColor;
            if (shadow)
            {
                style.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            }
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return new Color(0.25f, 0.6f, 1f);
                case Rarity.Epic: return new Color(0.7f, 0.35f, 1f);
                case Rarity.Mythic: return new Color(1f, 0.35f, 0.45f);
                case Rarity.Legendary: return Gold;
                default: return new Color(0.75f, 0.78f, 0.82f);
            }
        }

        void OnGUI()
        {
            KeyInput.HandleEvent(Event.current);
            if (ball == null) return;
            InitStyles();

            DrawPlayerTags();
            DrawScoreboard();
            // Version stamp, so it is easy to tell which build is running.
            Label(new Rect(Screen.width - 260f, 14f, 244f, 20f), Version, smallStyle.WithAlign(TextAnchor.UpperRight), new Color(1f, 1f, 1f, 0.6f), false);
            DrawMotionDebug();
            if (showAIDebug) DrawAIDebug();
            if (ReplayPlaying) DrawReplayBadge(); else DrawEvent();
            if (showPerf) DrawPerf();
            if (AutoPlay) Label(new Rect(16f, Screen.height - 150f, 300f, 20f), "AUTO PLAY (CPU vs CPU)  F4: off", smallStyle, Gold, false);
            DrawPlayerCard();
            DrawPowerGauge();
            if (Time.time < kickLabelUntil)
                Label(new Rect(0f, Screen.height - 128f, Screen.width, 24f), kickLabel, bigStyle.WithSize(18), Gold);

            Label(new Rect(16f, Screen.height - 26f, Screen.width - 32f, 22f),
                "WASD: move   Shift: sprint   J: pass (Alt+J through) / tackle   L: long (Alt+L lofted through)   K: shoot (Shift+K power, Alt+K finesse, Ctrl+K low)   Q: switch   Z: zoom   R: reset   M: sound   F1: AI roles",
                smallStyle, new Color(1f, 1f, 1f, 0.85f));
            if (string.IsNullOrEmpty(KeyInput.LastKey))
            {
                Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 30f),
                    "Click the game screen once, then use the keyboard.", bigStyle.WithSize(20), Cyan);
            }
        }

        void DrawScoreboard()
        {
            const float h = 40f;
            float x = 16f, y = 14f;
            Box(new Rect(x, y, 64f, h), Gold);
            Label(new Rect(x, y, 64f, h), "NFWL", bigStyle.WithSize(17), new Color(0.05f, 0.07f, 0.15f), false);
            x += 64f;
            Box(new Rect(x, y, 8f, h), HomeColor);
            Box(new Rect(x + 8f, y, 72f, h), Navy);
            Label(new Rect(x + 8f, y, 72f, h), homeShort, bigStyle.WithSize(20), Color.white, false);
            x += 80f;
            Box(new Rect(x, y, 92f, h), new Color(1f, 1f, 1f, 0.95f));
            Label(new Rect(x, y, 92f, h), $"{HomeScore} - {AwayScore}", bigStyle.WithSize(24), new Color(0.05f, 0.07f, 0.15f), false);
            x += 92f;
            Box(new Rect(x, y, 72f, h), Navy);
            Label(new Rect(x, y, 72f, h), awayShort, bigStyle.WithSize(20), Color.white, false);
            Box(new Rect(x + 72f, y, 8f, h), AwayColor);
            x += 80f;
            int secs = Mathf.FloorToInt(clock);
            Box(new Rect(x, y, 104f, h), new Color(0.06f, 0.1f, 0.22f, 0.92f));
            Label(new Rect(x, y, 104f, h), $"{secs / 60:00}:{secs % 60:00}", bigStyle.WithSize(20), Cyan, false);
            Label(new Rect(16f, y + h + 2f, 400f, 20f), half == 1 ? "1ST HALF" : "2ND HALF", smallStyle, new Color(1f, 1f, 1f, 0.75f));
        }

        void DrawEvent()
        {
            if (string.IsNullOrEmpty(eventText) || Time.time >= eventTextUntil) return;
            float y = Screen.height * 0.22f;
            Color c = eventText == "GOAL!" ? Gold : eventText == "SAVE!" ? Cyan : Color.white;
            float pop = eventText == "GOAL!" ? 1f + Mathf.Sin(Time.time * 8f) * 0.04f : 1f;
            GUIStyle s = eventStyle.WithSize(Mathf.RoundToInt(84 * pop));
            Box(new Rect(0f, y - 10f, Screen.width, 120f), new Color(0.02f, 0.04f, 0.1f, 0.45f));
            Label(new Rect(0f, y, Screen.width, 100f), eventText, s, c);
            GUIStyle sub = bigStyle.WithSize(22);
            if (eventText == "GOAL!" && lastScorer.Length > 0)
            {
                Label(new Rect(0f, y + 82f, Screen.width, 30f), lastScorer, sub, Color.white);
            }
            if (fullTime)
            {
                string result = HomeScore > AwayScore ? $"{homeName} WIN" : HomeScore < AwayScore ? $"{awayName} WIN" : "DRAW";
                Label(new Rect(0f, y + 82f, Screen.width, 30f), $"{result}   -   press R to play again", sub, Color.white);
            }
        }

        void DrawPlayerTags()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            foreach (PlayerController p in AllPlayers())
            {
                Vector3 sp = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 2.15f);
                if (sp.z < 0f) continue;
                float gx = sp.x, gy = Screen.height - sp.y;
                Color team = p.teamIndex == 0 ? HomeColor : AwayColor;
                if (p.IsHuman)
                {
                    string label = $"{p.number}  {p.displayName}";
                    float w = 22f + label.Length * 8f;
                    Box(new Rect(gx - w * 0.5f, gy - 24f, w, 20f), new Color(1f, 0.85f, 0.15f, 0.95f));
                    Label(new Rect(gx - w * 0.5f, gy - 24f, w, 20f), label, tagStyle, new Color(0.05f, 0.07f, 0.15f), false);
                    Box(new Rect(gx - 2f, gy - 4f, 4f, 8f), new Color(1f, 0.85f, 0.15f, 0.95f));
                }
                else
                {
                    Box(new Rect(gx - 13f, gy - 18f, 26f, 16f), new Color(team.r, team.g, team.b, 0.85f));
                    Label(new Rect(gx - 13f, gy - 18f, 26f, 16f), p.number.ToString(), tagStyle, Color.white, false);
                }
            }
        }

        /// <summary>NFT player card for the controlled player (Master Doc 7: abilities, rarity).</summary>
        void DrawPlayerCard()
        {
            if (human == null) return;
            const float w = 230f, h = 132f;
            float x = 16f, y = Screen.height - h - 40f;
            Color rc = RarityColor(human.rarity);
            Box(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), rc);
            Box(new Rect(x, y, w, h), Navy);

            PlayerAttributes a = human.attributes;
            int ovr = a.Overall(human.role);
            Label(new Rect(x + 12f, y + 6f, 60f, 44f), ovr.ToString(), bigStyle.WithSize(34), rc);
            Label(new Rect(x + 12f, y + 44f, 60f, 18f), human.role.ToString(), tagStyle, Color.white, false);
            Label(new Rect(x + 78f, y + 10f, w - 84f, 24f), human.displayName, cardTitle, Color.white);
            Label(new Rect(x + 78f, y + 34f, w - 84f, 18f), $"#{human.number}  CRYSTALIA / {human.element.name}", smallStyle, new Color(1f, 1f, 1f, 0.7f), false);
            Box(new Rect(x + 78f, y + 54f, 86f, 16f), rc);
            Label(new Rect(x + 78f, y + 54f, 86f, 16f), human.rarity.ToString().ToUpper(), tagStyle, new Color(0.05f, 0.07f, 0.15f), false);
            // v0.6: preferred foot and weak-foot stars.
            Label(new Rect(x + 168f, y + 54f, 60f, 16f), $"{(a.PreferredFoot == PreferredFoot.Left ? "L" : "R")}  WF{a.WeakFootRating}", tagStyle, new Color(1f, 1f, 1f, 0.75f), false);

            string[] names = { "SPD", "DRI", "PAS", "SHO" };
            int[] vals =
            {
                (a.Speed + a.Acceleration) / 2, (a.BallControl + a.Agility) / 2,
                (a.ShortPass + a.LongPass) / 2, (a.ShotPower + a.Finishing) / 2
            };
            for (int i = 0; i < 4; i++)
            {
                float bx = x + 12f + i * 54f;
                Label(new Rect(bx, y + 80f, 50f, 18f), names[i], tagStyle, new Color(1f, 1f, 1f, 0.7f), false);
                Label(new Rect(bx, y + 98f, 50f, 22f), vals[i].ToString(), bigStyle.WithSize(18), Color.white, false);
            }
            if (human.HasBall)
            {
                Box(new Rect(x + w - 54f, y + 8f, 46f, 18f), new Color(0.2f, 0.9f, 0.4f));
                Label(new Rect(x + w - 54f, y + 8f, 46f, 18f), "BALL", tagStyle, new Color(0.05f, 0.07f, 0.15f), false);
            }
        }

        /// <summary>Small readout of the controlled player's movement (v0.6), to check the new motion.</summary>
        void DrawMotionDebug()
        {
            if (human == null) return;
            PlayerLocomotion lc = human.Locomotion;
            float slipCm = human.Model != null ? human.Model.FootReachError * 100f : 0f;
            string turn = lc.TurnBand > 0 ? $"  turn {lc.TurnBand}" : "";
            string text = $"{lc.State}{turn}  {lc.Speed:0.0} m/s  acc {lc.LocalAccel.z:+0.0;-0.0}  lean {lc.LeanPitch:0}/{lc.LeanRoll:0}  bal {lc.Balance:0.00}  foot {slipCm:0.0}cm";
            if (human.Tackle.Active || Time.time - human.Tackle.ContactTime < 1f)
                text = $"TACKLE {(human.Tackle.Active && Time.time < human.Tackle.ContactTime ? "..." : human.Tackle.LastResult)}   " + text;
            else if (human.Kick.Active) text = $"KICK {human.Kick.Phase}   " + text;
            else if (human.HasBall) text = $"TOUCH {human.Dribble.LastKind}{(human.Dribble.Shielding ? " (SHIELD)" : "")}   " + text;
            Label(new Rect(Screen.width - 520f, 34f, 504f, 20f), text, smallStyle.WithAlign(TextAnchor.UpperRight), new Color(0.6f, 1f, 0.8f, 0.8f), false);

            // Keepers: last save decision (type, reachable or not, outcome) for a few seconds.
            float row = 54f;
            for (int i = 0; i < all.Count; i++)
            {
                GoalkeeperController gk = all[i].Keeper;
                if (gk == null || Time.time - gk.LastResultTime > 3f) continue;
                string gkText = $"GK {(all[i].teamIndex == 0 ? homeShort : awayShort)}  {gk.LastSave}  {(gk.LastPlanReachable ? "reachable" : "out of reach")}  {gk.LastResult}";
                Label(new Rect(Screen.width - 420f, row, 404f, 20f), gkText, smallStyle.WithAlign(TextAnchor.UpperRight), new Color(1f, 0.9f, 0.6f, 0.85f), false);
                row += 18f;
            }
        }

        /// <summary>Broadcast-style replay overlay: letterbox bars, REPLAY tag, how to skip.</summary>
        void DrawReplayBadge()
        {
            float bar = Screen.height * 0.07f;
            Box(new Rect(0f, 0f, Screen.width, bar), new Color(0f, 0f, 0f, 0.85f));
            Box(new Rect(0f, Screen.height - bar, Screen.width, bar), new Color(0f, 0f, 0f, 0.85f));
            bool blink = Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;
            Box(new Rect(24f, bar + 14f, 210f, 34f), new Color(Gold.r, Gold.g, Gold.b, 0.9f));
            Label(new Rect(24f, bar + 14f, 210f, 34f), (blink ? "● " : "   ") + replay.Title, smallStyle.WithAlign(TextAnchor.MiddleCenter), Navy, false);
            Label(new Rect(0f, Screen.height - bar + 8f, Screen.width, 24f), "Space / J / K / L : skip", smallStyle.WithAlign(TextAnchor.MiddleCenter), new Color(1f, 1f, 1f, 0.7f), false);
        }

        bool showPerf;
        string perfText = "";
        float perfTextAt;

        /// <summary>F2: measured performance (PHASE 12). Refreshed once a second.</summary>
        void DrawPerf()
        {
            if (Time.unscaledTime >= perfTextAt)
            {
                perfTextAt = Time.unscaledTime + 1f;
                perfText = string.Format("FPS {0:0}  (worst frame {1:0.0} ms)\nAI {2:0.00} ms  Anim/IK {3:0.00} ms  Contacts {4:0.00} ms\nGC/s {5}  Mono heap {6} MB  vSync {7}  target {8}",
                    PerfStats.Fps, PerfStats.WorstFrameMs, PerfStats.Ms(PerfStats.Slot.AI), PerfStats.Ms(PerfStats.Slot.Animation),
                    PerfStats.Ms(PerfStats.Slot.Contacts), PerfStats.GcPerSecond, PerfStats.MonoHeapMB, QualitySettings.vSyncCount, Application.targetFrameRate);
            }
            Box(new Rect(12f, 60f, 430f, 64f), new Color(0f, 0f, 0f, 0.6f));
            Label(new Rect(20f, 64f, 420f, 60f), perfText, smallStyle, Color.white, false);
        }

        bool showAIDebug;
        static readonly string[] roleShort = { "", "BALL", "GK", "INTERCEPT", "PRESS", "COVER", "MARK", "SUPPORT", "RUN", "WIDE", "REST" };

        /// <summary>F1: each player's AI role above his head, and the carrier's chosen option.</summary>
        void DrawAIDebug()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerController p = all[i];
                Vector3 sp = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 2.3f);
                if (sp.z <= 0f) continue;
                string text = p.IsHuman ? "YOU" : p.role == Role.GK ? "GK" : roleShort[(int)p.AIRole];
                if (p.HasBall && !p.IsHuman && p.Brain != null) text = p.Brain.Current.action.ToString().ToUpperInvariant();
                Color c = p.teamIndex == 0 ? new Color(0.6f, 0.9f, 1f, 0.9f) : new Color(1f, 0.7f, 0.6f, 0.9f);
                Label(new Rect(sp.x - 50f, Screen.height - sp.y - 10f, 100f, 20f), text, smallStyle.WithAlign(TextAnchor.MiddleCenter), c);
            }
        }

        void DrawPowerGauge()
        {
            if (human == null) return;
            float ratio = human.ChargeRatio;
            if (ratio < 0f) return;
            const float barW = 320f, barH = 16f;
            float x = Screen.width * 0.5f - barW * 0.5f;
            float y = Screen.height - 96f;
            Box(new Rect(x - 3f, y - 3f, barW + 6f, barH + 6f), Navy);
            Box(new Rect(x, y, barW * ratio, barH), Color.Lerp(Cyan, Gold, ratio));
            Label(new Rect(x, y - 24f, barW, 22f), human.ChargeLabel, smallStyle, Color.white);
        }
    }

    /// <summary>Derived GUI styles are cached (v0.6 PHASE 12: no new GUIStyle every OnGUI call).</summary>
    static class GUIStyleExt
    {
        static readonly Dictionary<(GUIStyle, int, int), GUIStyle> cache = new Dictionary<(GUIStyle, int, int), GUIStyle>();

        public static GUIStyle WithSize(this GUIStyle s, int size) => Get(s, 0, size, () => new GUIStyle(s) { fontSize = size });
        public static GUIStyle WithAlign(this GUIStyle s, TextAnchor a) => Get(s, 1, (int)a, () => new GUIStyle(s) { alignment = a });

        static GUIStyle Get(GUIStyle s, int kind, int value, System.Func<GUIStyle> make)
        {
            var key = (s, kind, value);
            if (!cache.TryGetValue(key, out GUIStyle g))
            {
                g = make();
                cache[key] = g;
            }
            return g;
        }
    }
}
