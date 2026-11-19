using UnityEngine;

namespace NFWL
{
    /// <summary>What the legs are doing. Read by the animation (PlayerModel) and the HUD debug view.</summary>
    public enum LocoState
    {
        Idle, Walk, Jog, Run, Sprint,
        Accelerate, Decelerate, Stop,
        SharpTurn, PivotTurn,
        SideStep, Backpedal, Jockey,
        Recovery
    }

    /// <summary>Movement request for one frame, from the keyboard or the AI.</summary>
    public struct LocoInput
    {
        /// <summary>Wanted running direction on the ground (unit length) or zero to stop.</summary>
        public Vector3 moveDir;
        /// <summary>0..1, how fast relative to the current gait (AI uses less than 1 to arrive softly).</summary>
        public float moveAmount;
        public bool sprint;
        public bool hasBall;
        /// <summary>Defensive stance: face faceDir and shuffle instead of turning to run.</summary>
        public bool jockey;
        /// <summary>Direction the body should face while moving (strafing). Zero = face where you run.</summary>
        public Vector3 faceDir;
        /// <summary>Extra speed multiplier, e.g. while charging a shot.</summary>
        public float speedScale;
    }

    /// <summary>
    /// PHASE 1.1 / 1.3: player locomotion with momentum.
    /// The running direction (velocity) and the body facing are separate, so the player cannot flip
    /// round instantly: above pivot speed a hard change of direction makes him plant, brake and pivot,
    /// curves are limited by sideways grip (Agility), and the upper body leans with speed and
    /// acceleration, sways forward from inertia when stopping, and loses balance on hard cuts.
    /// Plain C# (no MonoBehaviour) so it can be unit-tested; PlayerController owns one.
    /// </summary>
    public class PlayerLocomotion
    {
        // ---- results, read every frame ----
        public Vector3 Velocity { get; private set; }
        /// <summary>Body facing on the ground (unit).</summary>
        public Vector3 Facing => YawToDir(facingYaw);
        public float FacingYaw => facingYaw;
        public LocoState State { get; private set; }
        public float Speed => Velocity.magnitude;
        /// <summary>0..1. Below 1 the player is off balance (slower, turns worse).</summary>
        public float Balance { get; private set; } = 1f;
        /// <summary>Upper-body lean in degrees: + forward.</summary>
        public float LeanPitch { get; private set; }
        /// <summary>Whole-body lean in degrees around the facing axis (Unity Z): + = to the left.</summary>
        public float LeanRoll { get; private set; }
        /// <summary>Sideways sway from bumps (deg, same sign as LeanRoll), springs back.</summary>
        public float SwayRoll { get; private set; }
        /// <summary>Smoothed acceleration in body space (x right, z forward), m/s^2.</summary>
        public Vector3 LocalAccel { get; private set; }
        /// <summary>Velocity in body space (x right, z forward).</summary>
        public Vector3 LocalVelocity => Quaternion.Euler(0f, -facingYaw, 0f) * Velocity;
        /// <summary>Body turn speed in deg/s (+ = clockwise seen from above).</summary>
        public float YawRate { get; private set; }

        // ---- derived from abilities ----
        public float SprintSpeed { get; private set; } = 6.7f;
        public float RunSpeed { get; private set; } = 4.7f;
        public float MaxAccel { get; private set; }
        public float BrakeDecel { get; private set; }
        public float BodyTurnRate { get; private set; }
        public float LateralGrip { get; private set; }
        float balanceRecovery;

        LocomotionTuning tune;
        float facingYaw;
        float pushOffTimer;
        float stateHold;      // keeps short states (pivot) visible for a moment
        bool inSharpTurn;
        Vector3 accelSmoothed;
        float inertia, inertiaVel; // upper-body inertia spring (deg)
        float sway, swayVel;       // sideways sway from contacts (deg)

        public void Configure(PlayerAttributes a, LocomotionTuning t)
        {
            tune = t ?? new LocomotionTuning();
            SprintSpeed = Rules.AttrLerp(a.Speed, tune.sprintSpeed.x, tune.sprintSpeed.y);
            RunSpeed = SprintSpeed * tune.runFactor;
            float accelTime = Rules.AttrLerp(a.Acceleration, tune.timeToTopSpeed.x, tune.timeToTopSpeed.y);
            MaxAccel = SprintSpeed / Mathf.Max(0.1f, accelTime);
            BrakeDecel = SprintSpeed / Mathf.Max(0.05f, tune.brakeTime);
            BodyTurnRate = Rules.AttrLerp(a.Agility, tune.bodyTurnRate.x, tune.bodyTurnRate.y);
            LateralGrip = Rules.AttrLerp(a.Agility, tune.lateralGrip.x, tune.lateralGrip.y);
            balanceRecovery = Rules.AttrLerp(a.Agility, tune.balanceRecovery.x, tune.balanceRecovery.y);
        }

        public void Reset(Vector3 facing)
        {
            Velocity = Vector3.zero;
            facingYaw = DirToYaw(facing, 0f);
            State = LocoState.Idle;
            Balance = 1f;
            accelSmoothed = Vector3.zero;
            inertia = inertiaVel = 0f;
            sway = swayVel = 0f;
            LeanPitch = LeanRoll = 0f;
            inSharpTurn = false;
        }

        /// <summary>Turns the body at once (used by kicks until the kick system takes over facing).</summary>
        public void SnapFacing(Vector3 dir) => facingYaw = DirToYaw(dir, facingYaw);

        /// <summary>After the CharacterController moved, use the real velocity so bumps slow the player.</summary>
        public void SetActualVelocity(Vector3 v)
        {
            v.y = 0f;
            Velocity = v;
        }

        /// <summary>
        /// A bump from another player (PHASE 5.3): changes the velocity a little and sways the upper
        /// body away from the hit. localPush is in body space (x right, z forward), in m/s.
        /// </summary>
        public void Push(Vector3 worldDeltaV, float balanceLoss)
        {
            worldDeltaV.y = 0f;
            Velocity += worldDeltaV;
            Vector3 local = Quaternion.Euler(0f, -facingYaw, 0f) * worldDeltaV;
            // Pushed to the right = body tips right (negative roll); pushed back = upper body jerks back.
            swayVel += -local.x * 70f;
            inertiaVel += -local.z * 45f;
            LoseBalance(balanceLoss);
        }

        /// <summary>Knocks the player off balance (0..1), e.g. a hard turn or a shoulder charge.</summary>
        public void LoseBalance(float amount) => Balance = Mathf.Clamp01(Balance - amount);

        /// <summary>Speed the player would run at now with this input (used by the AI to plan).</summary>
        public float TopSpeed(bool sprint, bool hasBall) =>
            (sprint ? SprintSpeed : RunSpeed) * (hasBall ? tune.withBallFactor : 1f);

        public void Tick(in LocoInput input, float dt)
        {
            if (tune == null) tune = new LocomotionTuning();
            if (dt <= 0f) return;

            Vector3 prevVelocity = Velocity;
            Vector3 v = Velocity;
            float s = v.magnitude;
            float headingYaw = s > 0.05f ? DirToYaw(v, facingYaw) : facingYaw;

            Vector3 d = input.moveDir;
            d.y = 0f;
            float amount = Mathf.Clamp01(input.moveAmount);
            bool moving = d.sqrMagnitude > 0.0001f && amount > 0.01f;
            if (moving) d.Normalize();

            float balFactor = Mathf.Lerp(0.55f, 1f, Balance);
            float scale = input.speedScale > 0f ? input.speedScale : 1f;
            float top = TopSpeed(input.sprint, input.hasBall) * scale * balFactor;
            bool strafing = moving && (input.jockey || input.faceDir.sqrMagnitude > 0.0001f);

            pushOffTimer -= dt;
            stateHold -= dt;
            float accel = MaxAccel * Mathf.Lerp(1.25f, 0.7f, Mathf.Clamp01(s / SprintSpeed)) * balFactor;
            if (pushOffTimer > 0f) accel *= tune.pushOffBoost;

            bool sharpNow = false;
            float turnAngle = 0f;

            if (!moving)
            {
                s = Mathf.MoveTowards(s, 0f, BrakeDecel * dt);
                v = YawToDir(headingYaw) * s;
                inSharpTurn = false;
            }
            else if (strafing)
            {
                // Shuffle while facing the target: forward is full speed, sideways and backwards slower.
                Vector3 face = input.faceDir.sqrMagnitude > 0.0001f ? Flat(input.faceDir) : Facing;
                float rel = Vector3.Angle(face, d);
                float dirFactor = rel < 60f ? 1f
                    : rel < 120f ? Mathf.Lerp(1f, tune.sideStepFactor, (rel - 60f) / 60f)
                    : Mathf.Lerp(tune.sideStepFactor, tune.backpedalFactor, (rel - 120f) / 60f);
                float cap = input.jockey ? RunSpeed * tune.jockeyFactor : top;
                Vector3 target = d * Mathf.Min(top, cap) * dirFactor * amount;
                v = Vector3.MoveTowards(v, target, accel * 1.1f * dt);
                inSharpTurn = false;
            }
            else if (s < tune.pivotSpeed)
            {
                // Slow enough to step in any direction. Coming out of a plant = pivot and push off.
                if (inSharpTurn)
                {
                    inSharpTurn = false;
                    pushOffTimer = tune.pushOffSeconds;
                    State = LocoState.PivotTurn;
                    stateHold = 0.3f;
                }
                v = Vector3.MoveTowards(v, d * top * amount, accel * dt);
            }
            else
            {
                turnAngle = Mathf.Abs(Mathf.DeltaAngle(headingYaw, DirToYaw(d, headingYaw)));
                if (turnAngle > tune.sharpTurnAngle)
                {
                    // Too sharp to curve: plant the foot and brake along the old line first.
                    sharpNow = true;
                    if (!inSharpTurn)
                    {
                        inSharpTurn = true;
                        float r = s / SprintSpeed;
                        LoseBalance(tune.sharpTurnBalanceCost * r * r);
                    }
                    s = Mathf.MoveTowards(s, 0f, BrakeDecel * 1.15f * dt);
                    v = YawToDir(headingYaw) * s;
                }
                else
                {
                    inSharpTurn = false;
                    float targetSpeed = top * amount;
                    float corner = targetSpeed * Mathf.Lerp(1f, tune.cornerSpeedFactor, turnAngle / tune.sharpTurnAngle);
                    // Curving is limited by grip: yaw rate = lateral acceleration / speed.
                    float maxYawDeg = LateralGrip * balFactor / Mathf.Max(s, 0.5f) * Mathf.Rad2Deg * dt;
                    headingYaw = Mathf.MoveTowardsAngle(headingYaw, DirToYaw(d, headingYaw), maxYawDeg);
                    s = s > corner
                        ? Mathf.MoveTowards(s, corner, BrakeDecel * 0.5f * dt)
                        : Mathf.MoveTowards(s, corner, accel * dt);
                    v = YawToDir(headingYaw) * s;
                }
            }
            Velocity = v;
            s = v.magnitude;

            // ---- body facing (separate from the running direction) ----
            float faceTargetYaw = facingYaw;
            if (input.faceDir.sqrMagnitude > 0.0001f) faceTargetYaw = DirToYaw(input.faceDir, facingYaw);
            else if (moving)
            {
                // While planting the body already turns toward the new direction (the pivot);
                // when running, the body follows the running line.
                faceTargetYaw = sharpNow || s < tune.pivotSpeed ? DirToYaw(d, facingYaw) : DirToYaw(v, facingYaw);
            }
            else if (s > 0.5f) faceTargetYaw = DirToYaw(v, facingYaw);

            float rate = BodyTurnRate * Mathf.Lerp(1f, tune.turnRateAtSprint, Mathf.Clamp01(s / SprintSpeed)) * balFactor;
            if (sharpNow) rate *= 0.6f;
            float newYaw = Mathf.MoveTowardsAngle(facingYaw, faceTargetYaw, rate * dt);
            YawRate = Mathf.DeltaAngle(facingYaw, newYaw) / dt;
            facingYaw = Mathf.Repeat(newYaw, 360f);

            // ---- balance, acceleration and lean (centre of mass proxy) ----
            Balance = Mathf.MoveTowards(Balance, 1f, balanceRecovery * dt);

            Vector3 accW = (Velocity - prevVelocity) / dt;
            accelSmoothed = Vector3.Lerp(accelSmoothed, accW, 1f - Mathf.Exp(-12f * dt));
            Vector3 la = Quaternion.Euler(0f, -facingYaw, 0f) * accelSmoothed;
            LocalAccel = la;

            float speedRatio = Mathf.Clamp01(s / SprintSpeed);
            float pitchActive = tune.sprintLean * speedRatio * speedRatio + tune.accelLean * Mathf.Max(0f, la.z) * 0.8f;
            // Braking: the upper body carries on forward (inertia), then settles back.
            float inertiaTarget = Mathf.Max(0f, -la.z) * tune.accelLean * 0.55f;
            float w = tune.inertiaFrequency;
            float inertiaAcc = w * w * (inertiaTarget - inertia) - 2f * tune.inertiaDamping * w * inertiaVel;
            inertiaVel += inertiaAcc * dt;
            inertia += inertiaVel * dt;
            // Off balance: the body sags forward a little.
            float stumble = (1f - Balance) * 10f;
            LeanPitch = Mathf.Clamp(pitchActive + inertia + stumble, -10f, tune.maxLean);
            // Lean into curves (centripetal acceleration to the right = lean right = negative Z).
            float rollTarget = Mathf.Clamp(-la.x * tune.accelLean * 0.8f, -tune.maxLean, tune.maxLean);
            float swayAcc = 80f * (0f - sway) - 2f * 0.5f * 9f * swayVel;
            swayVel += swayAcc * dt;
            sway += swayVel * dt;
            sway = Mathf.Clamp(sway, -25f, 25f);
            LeanRoll = Mathf.Lerp(LeanRoll, rollTarget, 1f - Mathf.Exp(-10f * dt));
            SwayRoll = sway;

            State = Classify(input, moving, strafing, sharpNow, s, top, la);
        }

        LocoState Classify(in LocoInput input, bool moving, bool strafing, bool sharpNow, float s, float top, Vector3 la)
        {
            if (sharpNow) return LocoState.SharpTurn;
            if (stateHold > 0f && State == LocoState.PivotTurn) return LocoState.PivotTurn;
            if (Balance < 0.7f) return LocoState.Recovery;
            if (s < 0.15f) return LocoState.Idle;
            if (input.jockey && s < 0.6f) return LocoState.Jockey;
            Vector3 lv = LocalVelocity;
            if (strafing || lv.z < -0.3f || Mathf.Abs(lv.x) > Mathf.Abs(lv.z) + 0.2f)
            {
                if (lv.z < -0.3f && -lv.z >= Mathf.Abs(lv.x)) return LocoState.Backpedal;
                if (Mathf.Abs(lv.x) > Mathf.Abs(lv.z)) return LocoState.SideStep;
                if (input.jockey) return LocoState.Jockey;
            }
            if (!moving || la.z < -4f) return s < 1.2f ? LocoState.Stop : LocoState.Decelerate;
            if (la.z > 2.5f && s < top * 0.85f) return LocoState.Accelerate;
            if (s < tune.walkSpeed) return LocoState.Walk;
            if (s < tune.jogSpeed) return LocoState.Jog;
            if (input.sprint && s > RunSpeed * 1.02f) return LocoState.Sprint;
            return LocoState.Run;
        }

        // ---- helpers ----
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }

        /// <summary>Yaw in degrees (0 = +Z, 90 = +X), or fallback for a zero vector.</summary>
        public static float DirToYaw(Vector3 v, float fallback)
        {
            if (v.x * v.x + v.z * v.z < 1e-8f) return fallback;
            return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
        }

        public static Vector3 YawToDir(float yaw)
        {
            float r = yaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }
    }
}
