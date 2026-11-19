using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// All gameplay tuning values in one place (v0.6).
    /// The game works without any asset: the defaults below are used.
    /// To tweak values without touching code, use the menu NFWL > Create Tuning File, which creates
    /// Assets/Resources/NFWLTuning.asset. Select it and edit the numbers in the Inspector (also during Play).
    /// The defaults keep the v0.5 speeds as the baseline.
    /// </summary>
    [CreateAssetMenu(fileName = "NFWLTuning", menuName = "NFWL/Tuning")]
    public class NFWLTuning : ScriptableObject
    {
        public LocomotionTuning locomotion = new LocomotionTuning();

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

    /// <summary>Player movement (PHASE 1). Ability-driven values are given as [at 40, at 99].</summary>
    [System.Serializable]
    public class LocomotionTuning
    {
        [Header("Speed (Speed ability) - v0.5 baseline")]
        [Tooltip("Sprint top speed m/s at Speed 40 / 99")]
        public Vector2 sprintSpeed = new Vector2(6.0f, 7.4f);
        [Tooltip("Normal run speed as a fraction of sprint speed")]
        public float runFactor = 0.7f;
        public float withBallFactor = 0.92f;
        public float walkSpeed = 1.8f;
        public float jogSpeed = 3.6f;

        [Header("Acceleration (Acceleration ability)")]
        [Tooltip("Seconds from standstill to sprint speed at Acceleration 40 / 99")]
        public Vector2 timeToTopSpeed = new Vector2(1.6f, 1.0f);
        [Tooltip("Seconds to stop from sprint speed")]
        public float brakeTime = 0.3f;
        [Tooltip("Extra acceleration for the first steps after a stop or pivot (push-off)")]
        public float pushOffBoost = 1.35f;
        public float pushOffSeconds = 0.25f;

        [Header("Turning (Agility ability)")]
        [Tooltip("Body turn rate deg/s at Agility 40 / 99 (v0.5 values)")]
        public Vector2 bodyTurnRate = new Vector2(420f, 720f);
        [Tooltip("Sideways grip m/s^2 at Agility 40 / 99. Higher = tighter curves at speed")]
        public Vector2 lateralGrip = new Vector2(13f, 20f);
        [Tooltip("Above this angle (deg) between running direction and input, the player plants and turns")]
        public float sharpTurnAngle = 100f;
        [Tooltip("Below this speed the player can step in any direction (pivot / side step)")]
        public float pivotSpeed = 1.9f;
        [Tooltip("Fraction of target speed kept while curving at the sharp-turn angle")]
        public float cornerSpeedFactor = 0.55f;
        [Tooltip("Body turn rate at sprint speed, as a fraction of the standing turn rate")]
        public float turnRateAtSprint = 0.5f;

        [Header("Defensive / strafing movement")]
        public float sideStepFactor = 0.72f;
        public float backpedalFactor = 0.55f;
        public float jockeyFactor = 0.6f;
        [Tooltip("An opponent with the ball closer than this makes a non-sprinting defender jockey")]
        public float jockeyDistance = 4f;

        [Header("Balance and lean (centre of mass)")]
        [Tooltip("Forward lean at sprint speed (deg)")]
        public float sprintLean = 11f;
        [Tooltip("Lean per m/s^2 of forward / sideways acceleration (deg)")]
        public float accelLean = 1.1f;
        public float maxLean = 20f;
        [Tooltip("Upper-body inertia when braking: spring frequency (rad/s) and damping ratio")]
        public float inertiaFrequency = 9f;
        public float inertiaDamping = 0.45f;
        [Tooltip("Balance lost by a sharp turn at sprint speed (0..1)")]
        public float sharpTurnBalanceCost = 0.18f;
        [Tooltip("Balance regained per second at Agility 40 / 99")]
        public Vector2 balanceRecovery = new Vector2(0.7f, 1.5f);
    }
}
