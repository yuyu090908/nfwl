using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// Oblique overhead, side-on broadcast camera (Master Doc 3.1: 斜め俯瞰), v0.6 "Broadcast Camera 2.0".
    /// Sits on the -Z side of the pitch looking at +Z, so +X (HOME's attack) is screen right.
    /// - Predictive follow: frames where the ball is going, not where it was (lead from its velocity).
    /// - Critically damped smoothing with a small dead zone, so the picture does not shake on touches.
    /// - Dynamic zoom: pulls out for fast / long play, tightens a little near the goals.
    /// - Shot camera: on a shot, lowers and angles toward the goal for a moment.
    /// - Replay views are driven from outside (ReplaySystem) with SetReplayView.
    /// All smoothing uses unscaled time, so it also runs while a replay pauses the game.
    /// </summary>
    public class MatchCamera : MonoBehaviour
    {
        public enum Mode { Broadcast, Shot, Replay }

        public Transform target;
        /// <summary>Base distance; the Z key cycles it (close / normal / wide).</summary>
        public float distance = 16f;
        public float pitchDeg = 42f;
        public float followSpeed = 6f;
        public float fieldOfView = 55f;

        [Header("Broadcast 2.0")]
        public float leadSeconds = 0.35f;
        public float maxLead = 4f;
        public float smoothTime = 0.32f;
        public float deadZone = 0.6f;
        public float zoomOutAtSpeed = 0.22f;   // +22 % distance for a 25 m/s ball

        public Mode CurrentMode { get; private set; } = Mode.Broadcast;

        /// <summary>v0.7 camera preset (PHASE 10.2). All values can be edited in the Inspector.</summary>
        [System.Serializable]
        public struct Preset
        {
            public string name;
            public float distance, pitch, fov;
            [Tooltip("0 = follow the ball, 1 = follow your player")] public float playerPriority;
            [Tooltip("Seconds of ball motion the camera looks ahead")] public float lead;
            [Tooltip("Follow smoothing (s): larger = calmer")] public float smoothTime;
            public Preset(string n, float d, float p, float f, float pp, float l, float s)
            { name = n; distance = d; pitch = p; fov = f; playerPriority = pp; lead = l; smoothTime = s; }
        }

        [Header("v0.7 presets (Z cycles; the v0.6 distances 16 / 24 / 11 m are kept)")]
        public Preset[] presets =
        {
            new Preset("Broadcast", 16f, 42f, 55f, 0.35f, 0.35f, 0.32f),
            new Preset("Wide", 24f, 44f, 55f, 0.3f, 0.4f, 0.36f),
            new Preset("Tactical", 30f, 62f, 50f, 0.2f, 0.45f, 0.4f),
            new Preset("Close", 11f, 36f, 55f, 0.5f, 0.3f, 0.26f),
        };
        public int presetIndex;
        public Preset CurrentPreset => presets != null && presets.Length > 0 ? presets[Mathf.Clamp(presetIndex, 0, presets.Length - 1)] : new Preset("Broadcast", distance, pitchDeg, fieldOfView, 0.35f, leadSeconds, smoothTime);
        /// <summary>How much the anchor leans toward the user's player (read by MatchManager).</summary>
        public float PlayerPriority => CurrentPreset.playerPriority;
        float fovVel, curFov = -1f;

        /// <summary>Z: next preset (Broadcast -> Wide -> Tactical -> Close -> Broadcast).</summary>
        public void NextPreset()
        {
            if (presets == null || presets.Length == 0) return;
            presetIndex = (presetIndex + 1) % presets.Length;
            ApplyPresetValues();
        }

        public void SetPreset(int index)
        {
            if (presets == null || presets.Length == 0) return;
            presetIndex = Mathf.Clamp(index, 0, presets.Length - 1);
            ApplyPresetValues();
        }

        void ApplyPresetValues()
        {
            Preset p = CurrentPreset;
            distance = p.distance;
            pitchDeg = p.pitch;
            fieldOfView = p.fov;
            leadSeconds = p.lead;
            smoothTime = p.smoothTime;
        }
        public Ball Ball { get; set; }

        Vector3 focus, focusVel;
        float curDistance, distVel;
        float curPitch, pitchVel;
        float curYaw, yawVel;
        float shotUntil;
        float shotGoalX;

        // Replay view
        Vector3 replayFocus;
        float replayYaw, replayPitch, replayDistance;
        bool replaySnap;

        void Start()
        {
            Camera cam = GetComponent<Camera>();
            ApplyPresetValues();
            if (cam != null) cam.fieldOfView = fieldOfView;
            curFov = fieldOfView;
            curDistance = distance;
            curPitch = pitchDeg;
            if (target != null) focus = target.position;
            Apply(focus, curYaw, curPitch, curDistance);
        }

        /// <summary>A shot was struck toward the goal at goalX: lower, angled view until it is over.</summary>
        public void ShotCam(float goalX, float seconds = 1.3f)
        {
            if (CurrentMode == Mode.Replay) return;
            CurrentMode = Mode.Shot;
            shotGoalX = goalX;
            shotUntil = Time.unscaledTime + seconds;
        }

        public void BeginReplay() { CurrentMode = Mode.Replay; replaySnap = true; }

        public void EndReplay()
        {
            CurrentMode = Mode.Broadcast;
            if (target != null) focus = target.position;
            focusVel = Vector3.zero;
        }

        /// <summary>Replay: look at focus from a yaw (0 = broadcast side, 90 = from behind toward +X).</summary>
        public void SetReplayView(Vector3 focusPoint, float yaw, float pitch, float dist)
        {
            replayFocus = focusPoint;
            replayYaw = yaw;
            replayPitch = pitch;
            replayDistance = dist;
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (dt <= 0f) return;

            if (CurrentMode == Mode.Replay)
            {
                if (replaySnap)
                {
                    replaySnap = false;
                    focus = replayFocus; curYaw = replayYaw; curPitch = replayPitch; curDistance = replayDistance;
                    focusVel = Vector3.zero;
                }
                focus = Vector3.SmoothDamp(focus, replayFocus, ref focusVel, 0.18f, Mathf.Infinity, dt);
                curYaw = Mathf.SmoothDampAngle(curYaw, replayYaw, ref yawVel, 0.6f, Mathf.Infinity, dt);
                curPitch = Mathf.SmoothDamp(curPitch, replayPitch, ref pitchVel, 0.6f, Mathf.Infinity, dt);
                curDistance = Mathf.SmoothDamp(curDistance, replayDistance, ref distVel, 0.6f, Mathf.Infinity, dt);
                Apply(focus, curYaw, curPitch, curDistance);
                return;
            }
            if (target == null) return;

            // ---------- where to look: the anchor plus a lead toward where the ball is going ----------
            Vector3 want = target.position;
            float ballSpeed = 0f;
            if (Ball != null)
            {
                Vector3 v = Ball.Velocity;
                v.y = 0f;
                ballSpeed = v.magnitude;
                Vector3 lead = Vector3.ClampMagnitude(v * leadSeconds, maxLead);
                lead.z *= 0.5f; // depth changes read less on a side-on camera
                want += lead;
            }
            want.y = 0f;
            want.x = Mathf.Clamp(want.x, -PitchDims.HalfLength + 5f, PitchDims.HalfLength - 5f);
            want.z = Mathf.Clamp(want.z, -PitchDims.HalfWidth + 5f, PitchDims.HalfWidth - 4f);
            // Dead zone: small moves of the anchor do not move the camera.
            Vector3 diff = want - focus;
            diff.y = 0f;
            if (diff.magnitude < deadZone) want = focus;
            else want = focus + diff * (1f - deadZone / diff.magnitude);

            // ---------- framing: zoom and angle ----------
            float wantDist = distance * (1f + zoomOutAtSpeed * Mathf.Clamp01(ballSpeed / 25f));
            // v0.7: a high ball pulls the camera back a little so it does not leave the top of the frame.
            if (Ball != null) wantDist += Mathf.Clamp(Ball.transform.position.y - 1.5f, 0f, 6f) * 0.5f;
            float nearGoal = Mathf.InverseLerp(PitchDims.HalfLength - 12f, PitchDims.HalfLength - 4f, Mathf.Abs(want.x));
            wantDist *= 1f - 0.08f * nearGoal;
            float wantPitch = pitchDeg;
            float wantYaw = 0f;
            float st = smoothTime;

            if (CurrentMode == Mode.Shot)
            {
                bool live = Ball != null && Time.unscaledTime < shotUntil && Ball.Velocity.sqrMagnitude > 9f;
                if (!live) CurrentMode = Mode.Broadcast;
                else
                {
                    // Lower and closer, turned toward the goal, framing ball and goal mouth together.
                    Vector3 goal = new Vector3(shotGoalX, 0f, 0f);
                    Vector3 b = Ball.Body.position;
                    b.y = 0f;
                    want = Vector3.Lerp(b, goal, 0.4f);
                    wantDist = distance * 0.85f;
                    wantPitch = pitchDeg - 12f;
                    wantYaw = Mathf.Sign(shotGoalX) * 14f;
                    st = 0.22f;
                }
            }

            focus = Vector3.SmoothDamp(focus, want, ref focusVel, st, Mathf.Infinity, dt);
            curDistance = Mathf.SmoothDamp(curDistance, wantDist, ref distVel, 0.5f, Mathf.Infinity, dt);
            curPitch = Mathf.SmoothDamp(curPitch, wantPitch, ref pitchVel, 0.45f, Mathf.Infinity, dt);
            curYaw = Mathf.SmoothDampAngle(curYaw, wantYaw, ref yawVel, 0.5f, Mathf.Infinity, dt);
            SmoothFov(dt);
            Apply(focus, curYaw, curPitch, curDistance);
        }

        void SmoothFov(float dt)
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) return;
            if (curFov < 0f) curFov = fieldOfView;
            curFov = Mathf.SmoothDamp(curFov, fieldOfView, ref fovVel, 0.5f, Mathf.Infinity, dt);
            cam.fieldOfView = curFov;
        }

        void Apply(Vector3 f, float yaw, float pitch, float dist)
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.rotation = rot;
            transform.position = f - rot * Vector3.forward * dist;
        }
    }
}
