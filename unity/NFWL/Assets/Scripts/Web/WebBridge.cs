using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace NFWL.Web
{
    /// <summary>Match settings the web page hands to the game (web/src/lib/bridge/protocol.ts UnityMatchConfig).
    /// Contains ids and match data only: no auth token, no wallet, no e-mail.</summary>
    [Serializable]
    public class MatchConfigDto
    {
        public int schemaVersion;
        public string sessionId = "";
        public string userId = "";
        public string matchId = "";
        public string mode = "";
        public float halfLengthSeconds;
        public string homeName = "";
        public string homeShort = "";
        public string awayName = "";
        public string awayShort = "";
    }

    /// <summary>
    /// Bridge between the Unity match and the NFWL web platform (prompt 6.3).
    ///
    /// Before the scene loads it asks the page for the match config and the two line-ups and hands the
    /// line-ups to <see cref="MatchManager.DataSource"/> (the same IPlayerDataSource used for NFT metadata),
    /// so the match itself runs exactly as v0.7. It never changes the match rules: it only watches
    /// MatchManager's public state and reports events, and it obeys pause / mute / quality commands.
    ///
    /// Events: GameReady, GameStarted, MatchStarted, GoalScored, HalfTime, MatchFinished, PlayerSwitched,
    /// GamePaused, GameResumed, GameError. Every message has schemaVersion, sessionId, messageId, seq,
    /// eventType and payload. The server re-checks everything; nothing sent from here is trusted.
    ///
    /// Outside WebGL (Editor / standalone) the bridge stays off unless Resources/NFWLWebConfig.json and
    /// Resources/NFWLWebTeams.json exist; then messages are written to the Console for testing.
    /// </summary>
    public class WebBridge : MonoBehaviour
    {
        public const int SchemaVersion = 1;
        public const string ObjectName = "NFWLWebBridge";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string NFWL_GetMatchConfig();
        [DllImport("__Internal")] static extern string NFWL_GetTeams();
        [DllImport("__Internal")] static extern void NFWL_Post(string json);
#endif

        public static MatchConfigDto Config { get; private set; }
        public static bool Active => Config != null;
        /// <summary>Every message sent, newest last (kept for tests and the Editor).</summary>
        public static readonly List<string> Sent = new List<string>();

        MatchManager match;
        int seq;
        bool readySent, startedSent, finishedSent, halfSent;
        int lastHome, lastAway, lastHalf = 1;
        PlayerController lastHuman;
        bool paused;
        float timeScaleBeforePause = 1f;
        int errorsSent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            string cfg = ReadConfig();
            if (string.IsNullOrEmpty(cfg)) return; // no web page around: v0.7 behaviour, nothing changes
            try
            {
                Config = JsonUtility.FromJson<MatchConfigDto>(cfg);
            }
            catch (Exception e)
            {
                Debug.LogError("[WebBridge] bad match config: " + e.Message);
                return;
            }
            if (Config == null || Config.schemaVersion != SchemaVersion || string.IsNullOrEmpty(Config.sessionId))
            {
                Debug.LogError("[WebBridge] unsupported match config");
                Config = null;
                return;
            }
            string teams = ReadTeams();
            if (!string.IsNullOrEmpty(teams)) MatchManager.DataSource = JsonPlayerSource.Parse(teams);
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            go.AddComponent<WebBridge>();
            Application.logMessageReceived += OnLog;
        }

        static string ReadConfig()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return NFWL_GetMatchConfig();
#else
            TextAsset t = Resources.Load<TextAsset>("NFWLWebConfig");
            return t != null ? t.text : null;
#endif
        }

        static string ReadTeams()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return NFWL_GetTeams();
#else
            TextAsset t = Resources.Load<TextAsset>("NFWLWebTeams");
            return t != null ? t.text : null;
#endif
        }

        static WebBridge instance;
        void Awake() { instance = this; }
        void OnDestroy()
        {
            if (instance == this) instance = null;
            Application.logMessageReceived -= OnLog;
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (instance == null || (type != LogType.Exception && type != LogType.Error)) return;
            if (condition.StartsWith("[WebBridge]", StringComparison.Ordinal)) return;
            if (instance.errorsSent >= 5) return; // do not flood the server
            instance.errorsSent++;
            instance.Send("GameError", new Json().Str("code", type.ToString()).Str("message", Truncate(condition, 480)));
        }

        void Update()
        {
            if (match == null)
            {
                match = FindAnyObjectByType<MatchManager>();
                if (match == null) return;
                ApplyConfig();
            }

            if (!readySent)
            {
                readySent = true;
                Send("GameReady", new Json().Str("unityVersion", Application.unityVersion).Str("buildVersion", MatchManager.Version).Str("platform", Application.platform.ToString()));
                Send("GameStarted", new Json().Str("homeName", match.homeName).Str("awayName", match.awayName));
            }
            if (finishedSent) return;

            // Start() has run once the ball and the controlled player exist.
            if (!startedSent && match.MatchBall != null && match.Human != null)
            {
                startedSent = true;
                lastHuman = match.Human;
                Send("MatchStarted", new Json().Int("half", match.Half).Num("gameClock", match.Clock));
            }
            if (!startedSent) return;

            // Goals: MatchManager adds one goal per frame at most; loop anyway so none is ever missed.
            while (match.HomeScore > lastHome || match.AwayScore > lastAway)
            {
                bool home = match.HomeScore > lastHome;
                if (home) lastHome++; else lastAway++;
                PlayerController scorer = match.MatchBall != null ? match.MatchBall.LastTouchedBy : null;
                int team = home ? 0 : 1;
                int slot = scorer != null && scorer.teamIndex == team ? match.TeamList(team).IndexOf(scorer) : -1; // -1 = own goal / unknown
                Send("GoalScored", new Json().Str("team", home ? "home" : "away").IntOrNull("scorerSlot", slot >= 0 ? slot : (int?)null)
                    .Int("homeScore", lastHome).Int("awayScore", lastAway).Int("half", match.Half).Num("gameClock", match.Clock));
            }

            if (!halfSent && match.Half == 2 && lastHalf == 1)
            {
                halfSent = true;
                Send("HalfTime", new Json().Int("homeScore", match.HomeScore).Int("awayScore", match.AwayScore).Num("gameClock", match.Clock));
            }
            lastHalf = match.Half;

            if (match.Human != lastHuman && match.Human != null)
            {
                lastHuman = match.Human;
                int slot = match.TeamList(0).IndexOf(match.Human);
                if (slot >= 0) Send("PlayerSwitched", new Json().Int("slot", slot).Num("gameClock", match.Clock));
            }

            if (match.FullTime)
            {
                finishedSent = true;
                Send("MatchFinished", new Json().Int("homeScore", match.HomeScore).Int("awayScore", match.AwayScore).Num("gameClock", match.Clock));
            }
        }

        void ApplyConfig()
        {
            if (Config.halfLengthSeconds >= 10f) match.halfLengthSeconds = Config.halfLengthSeconds;
            if (!string.IsNullOrEmpty(Config.homeName)) match.homeName = Truncate(Config.homeName, 24);
            if (!string.IsNullOrEmpty(Config.homeShort)) match.homeShort = Truncate(Config.homeShort, 4);
            if (!string.IsNullOrEmpty(Config.awayName)) match.awayName = Truncate(Config.awayName, 24);
            if (!string.IsNullOrEmpty(Config.awayShort)) match.awayShort = Truncate(Config.awayShort, 4);
        }

        /// <summary>Called by the page: SendMessage("NFWLWebBridge", "OnWebCommand", json).</summary>
        public void OnWebCommand(string json)
        {
            WebCommand c;
            try { c = JsonUtility.FromJson<WebCommand>(json); } catch { return; }
            if (c == null) return;
            switch (c.command)
            {
                case "pause":
                    if (paused) return;
                    paused = true;
                    timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
                    Time.timeScale = 0f; // MatchManager's clock uses Time.deltaTime, so it stops too
                    AudioListener.pause = true;
                    KeyInput.ReleaseAll();
                    Send("GamePaused", new Json().Str("reason", "web").Num("gameClock", match != null ? match.Clock : 0f));
                    break;
                case "resume":
                    if (!paused) return;
                    paused = false;
                    Time.timeScale = timeScaleBeforePause;
                    AudioListener.pause = false;
                    Send("GameResumed", new Json().Num("gameClock", match != null ? match.Clock : 0f));
                    break;
                case "setMuted":
                    AudioListener.volume = c.muted ? 0f : 1f;
                    break;
                case "setQuality":
                    GraphicsQuality.Apply((QualityLevel)Mathf.Clamp(c.level, 0, 3), false);
                    break;
            }
        }

        [Serializable]
        class WebCommand
        {
            public string command = "";
            public bool muted;
            public int level;
        }

        void Send(string eventType, Json payload)
        {
            if (Config == null) return;
            seq++;
            string msg = new StringBuilder(256)
                .Append("{\"schemaVersion\":").Append(SchemaVersion)
                .Append(",\"sessionId\":").Append(Json.Quote(Config.sessionId))
                .Append(",\"messageId\":").Append(Json.Quote(Guid.NewGuid().ToString("N")))
                .Append(",\"eventType\":").Append(Json.Quote(eventType))
                .Append(",\"seq\":").Append(seq)
                .Append(",\"payload\":").Append(payload.ToString())
                .Append('}').ToString();
            Sent.Add(msg);
#if UNITY_WEBGL && !UNITY_EDITOR
            NFWL_Post(msg);
#else
            Debug.Log("[WebBridge] " + msg);
#endif
        }

        static string Truncate(string s, int n) => s == null ? "" : s.Length <= n ? s : s.Substring(0, n);
    }

    /// <summary>Tiny JSON object writer (JsonUtility cannot write a free-form payload).</summary>
    public class Json
    {
        readonly StringBuilder sb = new StringBuilder("{");
        bool first = true;

        Json Key(string k)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append(Quote(k)).Append(':');
            return this;
        }

        public Json Str(string k, string v) { Key(k).sb.Append(Quote(v)); return this; }
        public Json Int(string k, int v) { Key(k).sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        public Json IntOrNull(string k, int? v) { Key(k).sb.Append(v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : "null"); return this; }
        public Json Num(string k, float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
            Key(k).sb.Append(Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture));
            return this;
        }

        public override string ToString() => sb.ToString() + "}";

        public static string Quote(string s)
        {
            if (s == null) return "null";
            var b = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4")); else b.Append(c);
                        break;
                }
            }
            return b.Append('"').ToString();
        }
    }
}
