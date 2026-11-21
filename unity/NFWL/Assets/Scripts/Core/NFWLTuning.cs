using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// All gameplay tuning values in one place (v0.6).
    /// The game works without any asset: the defaults below are used.
    /// To tweak values without touching code, use the menu NFWL > Create Tuning File, which creates
    /// Assets/Resources/NFWLTuning.asset. Select it and edit the numbers in the Inspector (also during Play).
    /// Movement values are in Core/LocomotionTuning.cs (v0.6 Real Football Engine).
    /// </summary>
    [CreateAssetMenu(fileName = "NFWLTuning", menuName = "NFWL/Tuning")]
    public class NFWLTuning : ScriptableObject
    {
        public LocomotionTuning locomotion = new LocomotionTuning();
        public DribbleTuning dribble = new DribbleTuning();
        public BallTuning ball = new BallTuning();
        public InputTuning input = new InputTuning();

        static NFWLTuning instance;

        /// <summary>The tuning in use: Resources/NFWLTuning.asset if it exists, otherwise built-in defaults.</summary>
        public static NFWLTuning Current
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<NFWLTuning>("NFWLTuning");
                    if (instance == null)
                    {
                        instance = CreateInstance<NFWLTuning>();
                        instance.name = "NFWLTuning (defaults)";
                        instance.hideFlags = HideFlags.DontSave;
                    }
                }
                return instance;
            }
        }
    }

    /// <summary>Ball flight and roll (PHASE 4). Mass 0.40 kg and radius 0.11 m stay in PitchDims / Ball.</summary>
    [System.Serializable]
    public class BallTuning
    {
        [Tooltip("Rolling resistance on grass (m/s^2)")]
        public float rollingDecel = 0.8f;
        [Tooltip("Drag coefficient of the ball (about 0.25 at shot speed)")]
        public float dragCoefficient = 0.25f;
        public float airDensity = 1.2f;
        public float restitution = 0.55f;

        [Tooltip("Magnus (curve) strength: a = k * (spin x velocity). Physical value about 0.0063")]
        public float magnusFactor = 0.0063f;
        [Tooltip("Upper limit of the sideways curve acceleration (m/s^2), keeps curl realistic")]
        public float maxMagnusAccel = 9f;
        [Tooltip("Spin lost per second in the air (fraction)")]
        public float spinDecay = 0.08f;
        [Tooltip("Grip between ball and grass on a bounce (spin vs. skid)")]
        public float bounceGrip = 0.35f;
        [Tooltip("Speed kept when the ball hits the net (fraction)")]
        public float netAbsorb = 0.2f;
        [Tooltip("Bounce off posts and crossbar")]
        public float postRestitution = 0.7f;

        /// <summary>Quadratic drag factor k in a = k * v^2 (1/m).</summary>
        public float DragK => 0.5f * airDensity * dragCoefficient * Mathf.PI * PitchDims.BallRadius * PitchDims.BallRadius / 0.40f;
    }

    /// <summary>Touch dribbling (PHASE 2). Ability-driven values are [at 40, at 99].</summary>
    [System.Serializable]
    public class DribbleTuning
    {
        [Tooltip("Seconds between touches: normal dribble, BallControl 40 / 99")]
        public Vector2 normalInterval = new Vector2(0.62f, 0.44f);
        [Tooltip("Seconds between touches at sprint: a longer interval = the ball is knocked further ahead")]
        public Vector2 sprintInterval = new Vector2(1.6f, 1.25f);
        [Tooltip("Seconds between touches in close control")]
        public Vector2 closeInterval = new Vector2(0.4f, 0.27f);
        [Tooltip("Fastest re-touch for a change of direction, Agility 40 / 99")]
        public Vector2 minRetouch = new Vector2(0.3f, 0.16f);
        [Tooltip("Touch direction error in degrees, BallControl 40 / 99")]
        public Vector2 touchErrorDeg = new Vector2(7f, 1.5f);
        [Tooltip("Touch speed error (fraction), BallControl 40 / 99")]
        public Vector2 touchSpeedError = new Vector2(0.12f, 0.03f);
        [Tooltip("Where the next touch meets the ball, ahead of the body (m)")]
        public float contactAhead = 0.4f;
        [Tooltip("The ball can be touched within this distance of the player")]
        public float reach = 0.95f;
        [Tooltip("Opponent closer than this switches to close control")]
        public float closeControlDistance = 2.6f;
        [Tooltip("An input this many degrees off the ball's path makes a directional touch")]
        public float directionalAngle = 35f;
        [Tooltip("Owner loses the ball beyond this distance (m)")]
        public float loseDistance = 3.2f;
        [Tooltip("Small assist that keeps the ball on the planned line, per second, BallControl 40 / 99")]
        public Vector2 lineAssist = new Vector2(0.8f, 2.5f);
        [Tooltip("Opponent closer than this behind or beside a slow player makes him shield the ball")]
        public float shieldDistance = 1.8f;
    }

    /// <summary>Kick modifiers (PHASE 3.2). Existing keys J / K / L / Shift stay as they were.</summary>
    [System.Serializable]
    public class InputTuning
    {
        [Tooltip("Held with K: finesse (curl) shot. Held with J: through pass. Held with L: lofted through pass")]
        public KeyCode finesseModifier = KeyCode.LeftAlt;
        [Tooltip("Held with K: power shot (only when pressed just before or during the K press, so sprinting + K stays a normal shot)")]
        public KeyCode powerModifier = KeyCode.LeftShift;
        [Tooltip("Held with K: low driven shot")]
        public KeyCode lowModifier = KeyCode.LeftControl;
        [Tooltip("Shift pressed at most this long before K counts as power shot instead of sprint")]
        public float powerShiftWindow = 0.25f;

        [Header("Input buffer (v0.6 Real Football Engine, Phase 1)")]
        [Tooltip("A pass / shot pressed while another kick is still playing waits this long (s)")]
        public float kickBusyBufferSeconds = 0.3f;
        [Tooltip("J pressed to tackle a little too early (out of reach or still recovering) waits this long (s)")]
        public float tackleBufferSeconds = 0.25f;
        [Tooltip("The last movement direction is remembered this long, for the first touch (s)")]
        public float moveDirMemorySeconds = 0.3f;
    }
}
