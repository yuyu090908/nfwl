using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// Player movement (v0.6 Real Football Engine, Phase 1). Edit in the Inspector on Assets/Resources/NFWLTuning.asset
    /// (menu NFWL > Create Tuning File), also during Play.
    /// Ability-driven values are given as [at ability 40, at ability 99]; values in between are interpolated.
    /// The defaults are starting points for tuning, not final values.
    /// Field names changed from v0.6 on purpose, so an older tuning asset cannot bring back the old snap stops.
    /// </summary>
    [System.Serializable]
    public class LocomotionTuning
    {
        [Header("Gameplay assist (42 m pitch)")]
        [Tooltip("0 = real football timing. 1 = arcade: faster acceleration, shorter stops, tighter turns. " +
                 "Real 11-a-side timing feels slow on a 42 m pitch, so a little assist is on by default.")]
        [Range(0f, 1f)] public float gameplayAssist = 0.25f;
        [Tooltip("At assist 1 the time to top speed is multiplied by this")]
        public float assistAccelTime = 0.55f;
        [Tooltip("At assist 1 the stop times are multiplied by this")]
        public float assistStopTime = 0.6f;
        [Tooltip("At assist 1 the sideways grip and turn rate are multiplied by this")]
        public float assistGrip = 1.5f;

        [Header("Speed (Speed ability)")]
        [Tooltip("Sprint top speed m/s at Speed 40 / 99")]
        public Vector2 sprintTopSpeed = new Vector2(7.5f, 9.5f);
        [Tooltip("Normal run speed (no Shift) m/s at Speed 40 / 99")]
        public Vector2 runTopSpeed = new Vector2(3.8f, 5.0f);
        [Tooltip("Top speed with the ball, as a fraction")]
        public float withBallFactor = 0.92f;
        [Tooltip("State boundaries (m/s) for the animation: below walk = Walk, below jog = Jog")]
        public float walkSpeed = 1.8f;
        public float jogSpeed = 3.4f;
        [Tooltip("Hysteresis (m/s) on the Walk / Jog / Run / Sprint boundaries, so the gait does not flicker")]
        public float gaitHysteresis = 0.25f;

        [Header("Acceleration (Acceleration ability)")]
        [Tooltip("Seconds from standstill to 95 % of sprint speed at Acceleration 40 / 99")]
        public Vector2 timeTo95Speed = new Vector2(3.0f, 2.2f);
        [Tooltip("Strongest acceleration (first steps) m/s^2 at Acceleration 40 / 99. The curve is strong at the start and fades near top speed")]
        public Vector2 startAcceleration = new Vector2(6.5f, 8.5f);
        [Tooltip("Extra acceleration for the push-off after a stop or a pivot")]
        public float pushOffBoost = 1.2f;
        public float pushOffSeconds = 0.25f;
        [Tooltip("How long the first step (start from standstill) state lasts, seconds")]
        public float firstStepSeconds = 0.35f;

        [Header("Braking (speed dependent, Agility ability)")]
        [Tooltip("Seconds to stop from normal run speed at Agility 40 / 99")]
        public Vector2 stopTimeFromRun = new Vector2(0.9f, 0.5f);
        [Tooltip("Seconds to stop from sprint speed at Agility 40 / 99")]
        public Vector2 stopTimeFromSprint = new Vector2(1.2f, 0.8f);
        [Tooltip("Slowing down to a lower speed while still running (e.g. letting go of Shift), m/s^2")]
        public float paceDownDecel = 3.0f;
        [Tooltip("A plant-and-cut (sharp turn) brakes this much harder than a plain stop")]
        public float cutBrakeFactor = 1.25f;

        [Header("Turning (Agility ability)")]
        [Tooltip("Body turn rate deg/s when standing, at Agility 40 / 99")]
        public Vector2 bodyTurnRate = new Vector2(420f, 540f);
        [Tooltip("Body turn rate at sprint speed, as a fraction of the standing rate")]
        public float turnRateAtSprint = 0.45f;
        [Tooltip("Sideways grip m/s^2 at Agility 40 / 99. Curve radius = speed^2 / grip")]
        public Vector2 lateralGrip = new Vector2(6f, 10f);
        [Tooltip("Above this angle (deg) between running direction and input, the player plants, brakes and pivots")]
        public float sharpTurnAngle = 110f;
        [Tooltip("Below this speed the player can step in any direction (pivot)")]
        public float pivotSpeed = 1.6f;
        [Tooltip("Fraction of target speed kept while curving at the sharp-turn angle")]
        public float cornerSpeedFactor = 0.6f;
        [Tooltip("Without strafing, speed allowed when the body faces 90 deg away from the running direction (fraction of top speed)")]
        public float facingLimitAt90 = 0.35f;
        [Tooltip("Same at 180 deg (running backwards without a defensive stance)")]
        public float facingLimitAt180 = 0.15f;

        [Header("Defensive / strafing movement")]
        public float sideStepFactor = 0.72f;
        public float backpedalFactor = 0.55f;
        public float jockeyFactor = 0.6f;
        [Tooltip("An opponent with the ball closer than this makes a non-sprinting defender jockey")]
        public float jockeyDistance = 4f;

        [Header("Balance and lean (centre of mass)")]
        [Tooltip("Largest forward lean on the first steps of a start (deg)")]
        public float startLean = 25f;
        [Tooltip("Forward lean at top speed (deg)")]
        public float sprintLean = 8f;
        [Tooltip("Largest sideways lean into a curve or cut (deg)")]
        public float turnLeanMax = 20f;
        [Tooltip("Sideways lean per m/s^2 of sideways acceleration (deg)")]
        public float turnLeanPerAccel = 2.2f;
        [Tooltip("Upper body leans back this much (deg) under full braking")]
        public float brakeLeanBack = 7f;
        [Tooltip("Upper-body inertia: spring frequency (rad/s) and damping ratio. Braking first throws the chest forward")]
        public float inertiaFrequency = 9f;
        public float inertiaDamping = 0.45f;
        [Tooltip("Forward jolt of the chest when a hard stop starts (deg/s per m/s of speed)")]
        public float brakeJolt = 9f;
        [Tooltip("Balance lost by a sharp turn at sprint speed (0..1)")]
        public float sharpTurnBalanceCost = 0.18f;
        [Tooltip("Balance regained per second at Agility 40 / 99")]
        public Vector2 balanceRecovery = new Vector2(0.7f, 1.5f);
    }
}
