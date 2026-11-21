using UnityEngine;

namespace NFWL
{
    /// <summary>What the legs are doing. Read by the animation (PlayerModel), contacts and the HUD debug view.</summary>
    public enum LocoState
    {
        Idle, Walk, Jog, Run, Sprint,
        Accelerate, Decelerate, Stop,
        SharpTurn, PivotTurn,
        SideStep, Backpedal, Jockey,
        Recovery,
        // v0.6 Real Football Engine (Phase 1). Added at the end so the older values keep their numbers.
        /// <summary>First steps out of a standstill: deep forward lean, short quick steps.</summary>
        StartStep,
        /// <summary>Running a curve at speed: leaning into it, speed limited by grip.</summary>
        Curve
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
    /// Values an animation system needs each frame (Blend Tree parameters today, motion matching
    /// query features later). Filled by PlayerLocomotion.Tick.
    /// </summary>
    public struct LocoAnimParams
    {
        /// <summary>Ground speed m/s.</summary>
        public float moveSpeed;
        /// <summary>Running direction relative to the body, deg (0 forward, +90 right, 180 backwards).</summary>
        public float moveDirection;
        /// <summary>Forward acceleration in body space, m/s^2 (negative = braking).</summary>
        public float acceleration;
        /// <summary>Signed angle of the current / last turn, deg (+ = clockwise seen from above).</summary>
        public float turnAngle;
        /// <summary>The turn rounded to 0 / 45 / 90 / 135 / 180 deg (0 = no turn).</summary>
        public int turnBand;
        public bool isSprinting;
        public bool hasBall;
        public bool isDefending;
        /// <summary>Stamina arrives in Phase 5; always false until then.</summary>
        public bool isFatigued;
        public float balance;
    }

    /// <summary>
    /// v0.6 Real Football Engine, Phase 1: player locomotion with momentum, shared by user-controlled and AI players.
    /// - The running direction (velocity) and the body facing are separate. Without a defensive stance the
    ///   player can only run fast where the body faces, so side-to-sprint and backpedal-to-forward need a hip turn.
    /// - Acceleration is strong on the first steps and fades near top speed (exponential approach), so top speed
    ///   takes 2.2-3.0 s, not 1 s.
    /// - Braking depends on the speed: the deceleration is fixed when the stop begins (speed / stop time), so
    ///   stop time, stop distance and deceleration always agree (distance = speed x time / 2).
    /// - Above pivot speed a hard change of direction makes the player plant, brake along the old line and
    ///   pivot; curves are limited by sideways grip (Agility). No instant reversal.
    /// - Lean: deep forward lean on the first steps, upright at top speed, lean into curves, chest thrown
    ///   forward and then back when braking.
    /// Plain C# (no MonoBehaviour, no Quaternion) so it can be unit-tested; PlayerController owns one.
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
        public Vector3 LocalVelocity => RotateYaw(Velocity, -facingYaw);
        /// <summary>Body turn speed in deg/s (+ = clockwise seen from above).</summary>
        public float YawRate { get; private set; }
        /// <summary>True while a stop or a cut brakes the player (choppy braking steps).</summary>
        public bool Braking { get; private set; }
        /// <summary>Signed angle of the current / last turn (deg), and the same rounded to 45 deg steps.</summary>
        public float TurnAngle { get; private set; }
        public int TurnBand { get; private set; }
        /// <summary>Parameters for an animation system (Blend Tree / motion matching).</summary>
        public LocoAnimParams AnimParams { get; private set; }

        // ---- derived from abilities and tuning ----
        public float SprintSpeed { get; private set; } = 8.5f;
        public float RunSpeed { get; private set; } = 4.4f;
        /// <summary>Strongest acceleration (first steps), m/s^2.</summary>
        public float MaxAccel { get; private set; } = 7.5f;
        /// <summary>Time constant of the exponential approach to the target speed (s).</summary>
        public float AccelTau { get; private set; } = 0.8f;
        public float StopTimeRun { get; private set; } = 0.7f;
        public float StopTimeSprint { get; private set; } = 1.0f;
        public float BodyTurnRate { get; private set; } = 480f;
        public float LateralGrip { get; private set; } = 8f;
        float balanceRecovery = 1f;

        LocomotionTuning tune;
        float facingYaw;
        float pushOffTimer;
        float firstStepTimer;
        float pivotHold;      // keeps the pivot state visible for a moment
        float turnHold;       // keeps the last turn angle for the animation
        bool inCut;           // planted and braking for a sharp turn
        float brakeDecel;     // latched deceleration of the current stop (0 = not stopping)
        int gaitLevel;        // 0 walk, 1 jog, 2 run, 3 sprint (with hysteresis)
        Vector3 accelSmoothed;
        float inertia, inertiaVel; // upper-body inertia spring (deg)
        float sway, swayVel;       // sideways sway from contacts (deg)

        public void Configure(PlayerAttributes a, LocomotionTuning t)
        {
            tune = t ?? new LocomotionTuning();
            float assist = Mathf.Clamp01(tune.gameplayAssist);
            SprintSpeed = Rules.AttrLerp(a.Speed, tune.sprintTopSpeed.x, tune.sprintTopSpeed.y);
            RunSpeed = Mathf.Min(Rules.AttrLerp(a.Speed, tune.runTopSpeed.x, tune.runTopSpeed.y), SprintSpeed * 0.9f);

            float accelTimeScale = Mathf.Lerp(1f, tune.assistAccelTime, assist);
            float t95 = Mathf.Max(0.3f, Rules.AttrLerp(a.Acceleration, tune.timeTo95Speed.x, tune.timeTo95Speed.y) * accelTimeScale);
            MaxAccel = Rules.AttrLerp(a.Acceleration, tune.startAcceleration.x, tune.startAcceleration.y)
                       / Mathf.Max(0.2f, Mathf.Lerp(1f, Mathf.Sqrt(tune.assistAccelTime), assist));
            AccelTau = SolveTau(SprintSpeed, MaxAccel, t95);

            float stopScale = Mathf.Lerp(1f, tune.assistStopTime, assist);
            StopTimeRun = Mathf.Max(0.15f, Rules.AttrLerp(a.Agility, tune.stopTimeFromRun.x, tune.stopTimeFromRun.y) * stopScale);
            StopTimeSprint = Mathf.Max(StopTimeRun, Rules.AttrLerp(a.Agility, tune.stopTimeFromSprint.x, tune.stopTimeFromSprint.y) * stopScale);

            float gripScale = Mathf.Lerp(1f, tune.assistGrip, assist);
            BodyTurnRate = Rules.AttrLerp(a.Agility, tune.bodyTurnRate.x, tune.bodyTurnRate.y) * gripScale;
            LateralGrip = Rules.AttrLerp(a.Agility, tune.lateralGrip.x, tune.lateralGrip.y) * gripScale;
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
            inCut = false;
            brakeDecel = 0f;
            pushOffTimer = firstStepTimer = pivotHold = turnHold = 0f;
            gaitLevel = 0;
            TurnAngle = 0f;
            TurnBand = 0;
            Braking = false;
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
        /// A bump from another player: changes the velocity a little and sways the upper body away from the hit.
        /// </summary>
        public void Push(Vector3 worldDeltaV, float balanceLoss)
        {
            worldDeltaV.y = 0f;
            Velocity += worldDeltaV;
            Vector3 local = RotateYaw(worldDeltaV, -facingYaw);
            // Pushed to the right = body tips right (negative roll); pushed back = upper body jerks back.
            swayVel += -local.x * 70f;
            inertiaVel += -local.z * 45f;
            LoseBalance(balanceLoss);
        }

        /// <summary>Knocks the player off balance (0..1), e.g. a hard turn or a shoulder charge.</summary>
        public void LoseBalance(float amount) => Balance = Mathf.Clamp01(Balance - amount);

        /// <summary>Speed the player would run at now with this input (used by the AI to plan).</summary>
        public float TopSpeed(bool sprint, bool hasBall) =>
            (sprint ? SprintSpeed : RunSpeed) * (hasBall ? (tune != null ? tune.withBallFactor : 0.92f) : 1f);

        // ------------------------------------------------------------------ braking model

        /// <summary>Seconds a full stop takes from speed s (run speed: StopTimeRun, sprint: StopTimeSprint).</summary>
        public float StopTime(float s)
        {
            s = Mathf.Max(0f, s);
            if (s <= RunSpeed) return StopTimeRun * s / Mathf.Max(0.1f, RunSpeed); // constant deceleration below run speed
            if (s <= SprintSpeed)
                return Mathf.Lerp(StopTimeRun, StopTimeSprint, (s - RunSpeed) / Mathf.Max(0.1f, SprintSpeed - RunSpeed));
            return StopTimeSprint * s / Mathf.Max(0.1f, SprintSpeed);
        }

        /// <summary>Deceleration (m/s^2) of a full stop that starts at speed s.</summary>
        public float StopDecel(float s)
        {
            s = Mathf.Max(s, 0.5f);
            return s / Mathf.Max(0.05f, StopTime(s));
        }

        /// <summary>Metres needed to stop from speed s (constant deceleration: v * t / 2).</summary>
        public float StoppingDistance(float s) => 0.5f * s * StopTime(s);

        /// <summary>Highest speed at which the player can still stop within dist metres (AI arrival).</summary>
        public float ArrivalSpeed(float dist)
        {
            // Comfortable braking: 80 % of the full-stop deceleration from run speed.
            float a = 0.8f * StopDecel(RunSpeed);
            return Mathf.Sqrt(2f * a * Mathf.Max(0f, dist));
        }

        /// <summary>
        /// Seconds to get from 'from' to 'point', counting a turn, braking if moving away and the
        /// accelerating run (exponential approach to top speed). Used by the AI instead of distance / top speed.
        /// </summary>
        public float EstimateTimeToReach(Vector3 from, Vector3 point, bool sprint, bool hasBall)
        {
            Vector3 d = point - from;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.05f) return 0f;
            Vector3 dir = d / dist;
            float vmax = Mathf.Max(0.5f, TopSpeed(sprint, hasBall));
            float along = Vector3.Dot(Velocity, dir);
            float time = 0f;
            if (along < -0.3f)
            {
                // Moving away: stop first (drifting further away), then start from zero.
                float back = -along;
                time += StopTime(back);
                dist += StoppingDistance(back);
                along = 0f;
            }
            float turn = Vector3.Angle(Facing, dir);
            if (dist > 0.5f && turn > 30f) time += (turn - 30f) / Mathf.Max(90f, BodyTurnRate);
            float v0 = Mathf.Clamp(along, 0f, vmax);
            return time + RunTime(dist, v0, vmax);
        }

        /// <summary>Metres covered in t seconds starting at speed v0 and accelerating toward top speed.</summary>
        public float DistanceInTime(float t, float v0, bool sprint, bool hasBall)
        {
            float vmax = Mathf.Max(0.5f, TopSpeed(sprint, hasBall));
            float tau = Mathf.Max(0.05f, AccelTau);
            v0 = Mathf.Clamp(v0, 0f, vmax);
            return vmax * t - (vmax - v0) * tau * (1f - Mathf.Exp(-Mathf.Max(0f, t) / tau));
        }

        /// <summary>Time to cover dist with v(t) = vmax - (vmax - v0) e^(-t/tau) (Newton's method).</summary>
        float RunTime(float dist, float v0, float vmax)
        {
            float tau = Mathf.Max(0.05f, AccelTau);
            float t = dist / vmax + tau * (1f - v0 / vmax);
            for (int i = 0; i < 6; i++)
            {
                float e = Mathf.Exp(-t / tau);
                float x = vmax * t - (vmax - v0) * tau * (1f - e);
                float v = vmax - (vmax - v0) * e;
                t -= (x - dist) / Mathf.Max(0.1f, v);
                if (t < 0f) t = 0f;
            }
            return t;
        }

        // ------------------------------------------------------------------ per frame

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
            float inputYaw = moving ? DirToYaw(d, headingYaw) : headingYaw;

            float balFactor = Mathf.Lerp(0.55f, 1f, Balance);
            float scale = input.speedScale > 0f ? input.speedScale : 1f;
            float top = TopSpeed(input.sprint, input.hasBall) * scale * balFactor;
            bool strafing = moving && (input.jockey || input.faceDir.sqrMagnitude > 0.0001f);

            pushOffTimer -= dt;
            firstStepTimer -= dt;
            pivotHold -= dt;
            turnHold -= dt;
            float accelBoost = (pushOffTimer > 0f ? tune.pushOffBoost : 1f) * balFactor;

            bool sharpNow = false, curving = false, braking = false;

            if (moving && s < 0.2f && prevVelocity.magnitude < 0.2f && firstStepTimer <= 0f)
            {
                // Start from a standstill: the first step pushes off with the body low and leaning.
                firstStepTimer = tune.firstStepSeconds;
                float rel = Mathf.DeltaAngle(facingYaw, inputYaw);
                if (Mathf.Abs(rel) > 30f) RegisterTurn(rel);
            }

            if (!moving)
            {
                // Full stop. The deceleration is fixed when the stop begins, so time and distance agree.
                if (s > 0.02f)
                {
                    if (brakeDecel <= 0f) BeginBrake(StopDecel(s), s);
                    s = Mathf.MoveTowards(s, 0f, brakeDecel * dt);
                    v = YawToDir(headingYaw) * s;
                    braking = true;
                }
                else
                {
                    v = Vector3.zero;
                    brakeDecel = 0f;
                }
                inCut = false;
            }
            else if (strafing)
            {
                // Shuffle while facing the target: forward is full speed, sideways and backwards slower.
                brakeDecel = 0f;
                inCut = false;
                Vector3 face = input.faceDir.sqrMagnitude > 0.0001f ? Flat(input.faceDir) : Facing;
                float rel = Vector3.Angle(face, d);
                float dirFactor = rel < 60f ? 1f
                    : rel < 120f ? Mathf.Lerp(1f, tune.sideStepFactor, (rel - 60f) / 60f)
                    : Mathf.Lerp(tune.sideStepFactor, tune.backpedalFactor, (rel - 120f) / 60f);
                float cap = input.jockey ? RunSpeed * tune.jockeyFactor : top;
                float targetSpeed = Mathf.Min(top, cap) * dirFactor * amount;
                Vector3 target = d * targetSpeed;
                float rate = targetSpeed >= s ? AccelToward(targetSpeed, s, accelBoost) * 1.1f : StopDecel(s);
                v = Vector3.MoveTowards(v, target, rate * dt);
            }
            else if (inCut || (s > tune.pivotSpeed && Mathf.Abs(Mathf.DeltaAngle(headingYaw, inputYaw)) > tune.sharpTurnAngle))
            {
                float off = Mathf.Abs(Mathf.DeltaAngle(headingYaw, inputYaw));
                if (!inCut)
                {
                    // Too sharp to curve: plant the outside foot and brake along the old line first.
                    inCut = true;
                    float r = s / Mathf.Max(0.1f, SprintSpeed);
                    LoseBalance(tune.sharpTurnBalanceCost * r * r);
                    BeginBrake(StopDecel(s) * tune.cutBrakeFactor, s);
                    RegisterTurn(Mathf.DeltaAngle(headingYaw, inputYaw));
                }
                if (off < tune.sharpTurnAngle * 0.6f)
                {
                    // Changed his mind: the input is back near the running line, carry on curving.
                    inCut = false;
                    brakeDecel = 0f;
                }
                else
                {
                    sharpNow = true;
                    braking = true;
                    s = Mathf.MoveTowards(s, 0f, brakeDecel * dt);
                    v = YawToDir(headingYaw) * s;
                    if (s <= tune.pivotSpeed)
                    {
                        // Slow enough: pivot on the planted foot and push off the other way.
                        inCut = false;
                        brakeDecel = 0f;
                        pushOffTimer = tune.pushOffSeconds;
                        pivotHold = 0.3f;
                    }
                }
            }
            else if (s <= tune.pivotSpeed)
            {
                // Slow: step in any direction, but only run fast where the body faces (turn the hips first).
                brakeDecel = 0f;
                float rel = Mathf.Abs(Mathf.DeltaAngle(facingYaw, inputYaw));
                float targetSpeed = Mathf.Min(top * amount, Mathf.Max(FacingLimit(rel) * top, 0.6f));
                Vector3 target = d * targetSpeed;
                float rate = targetSpeed >= s ? AccelToward(targetSpeed, s, accelBoost) : StopDecel(s);
                v = Vector3.MoveTowards(v, target, rate * dt);
            }
            else
            {
                // Running a curve: the turn rate is limited by grip (yaw rate = sideways acceleration / speed).
                brakeDecel = 0f;
                float ang = Mathf.Abs(Mathf.DeltaAngle(headingYaw, inputYaw));
                if (ang > 30f && turnHold <= 0f) RegisterTurn(Mathf.DeltaAngle(headingYaw, inputYaw));
                curving = ang > 20f;
                float targetSpeed = top * amount * Mathf.Lerp(1f, tune.cornerSpeedFactor, Mathf.Clamp01(ang / tune.sharpTurnAngle));
                // Body still turned away from the running line (e.g. after a backpedal): hips must turn first.
                float bodyOff = Mathf.Abs(Mathf.DeltaAngle(facingYaw, headingYaw));
                targetSpeed = Mathf.Min(targetSpeed, Mathf.Max(FacingLimit(bodyOff) * top, tune.pivotSpeed));
                float maxYawDeg = LateralGrip * balFactor / Mathf.Max(s, 0.5f) * Mathf.Rad2Deg * dt;
                headingYaw = Mathf.MoveTowardsAngle(headingYaw, inputYaw, maxYawDeg);
                if (s > targetSpeed)
                {
                    float drop = Mathf.Clamp01((s - targetSpeed) / Mathf.Max(0.1f, s));
                    s = Mathf.MoveTowards(s, targetSpeed, Mathf.Lerp(tune.paceDownDecel, StopDecel(s), drop) * dt);
                    braking = drop > 0.35f;
                }
                else s = Mathf.MoveTowards(s, targetSpeed, AccelToward(targetSpeed, s, accelBoost) * dt);
                v = YawToDir(headingYaw) * s;
            }
            Velocity = v;
            s = v.magnitude;
            Braking = braking;

            // ---- body facing (separate from the running direction) ----
            float faceTargetYaw = facingYaw;
            if (input.faceDir.sqrMagnitude > 0.0001f) faceTargetYaw = DirToYaw(input.faceDir, facingYaw);
            else if (moving)
            {
                // While planting the body already turns toward the new direction (the pivot);
                // when running, the body follows the running line.
                faceTargetYaw = sharpNow || s <= tune.pivotSpeed ? inputYaw : DirToYaw(v, facingYaw);
            }
            else if (s > 0.5f) faceTargetYaw = DirToYaw(v, facingYaw);

            float rate2 = BodyTurnRate * Mathf.Lerp(1f, tune.turnRateAtSprint, Mathf.Clamp01(s / SprintSpeed)) * balFactor;
            if (sharpNow) rate2 *= 0.6f;
            float newYaw = Mathf.MoveTowardsAngle(facingYaw, faceTargetYaw, rate2 * dt);
            YawRate = Mathf.DeltaAngle(facingYaw, newYaw) / dt;
            facingYaw = Mathf.Repeat(newYaw, 360f);

            // ---- balance, acceleration and lean (centre of mass proxy) ----
            Balance = Mathf.MoveTowards(Balance, 1f, balanceRecovery * dt);

            Vector3 accW = (Velocity - prevVelocity) / dt;
            accelSmoothed = Vector3.Lerp(accelSmoothed, accW, 1f - Mathf.Exp(-12f * dt));
            Vector3 la = RotateYaw(accelSmoothed, -facingYaw);
            LocalAccel = la;

            float speedRatio = Mathf.Clamp01(s / SprintSpeed);
            // Start: deep lean while the push is strong and the speed is low; upright-ish at top speed.
            float startFactor = Mathf.Clamp01(la.z / Mathf.Max(1f, 0.8f * MaxAccel)) * (1f - 0.7f * speedRatio);
            float pitchActive = tune.sprintLean * speedRatio * speedRatio + tune.startLean * startFactor;
            // Braking: the chest is first thrown forward (jolt in BeginBrake), then the trunk leans back.
            float inertiaTarget = -tune.brakeLeanBack * Mathf.Clamp01(-la.z / 8f);
            float w = tune.inertiaFrequency;
            float inertiaAcc = w * w * (inertiaTarget - inertia) - 2f * tune.inertiaDamping * w * inertiaVel;
            inertiaVel += inertiaAcc * dt;
            inertia += inertiaVel * dt;
            // Off balance: the body sags forward a little.
            float stumble = (1f - Balance) * 10f;
            LeanPitch = Mathf.Clamp(pitchActive + inertia + stumble, -12f, Mathf.Max(tune.startLean, tune.sprintLean));
            // Lean into curves (centripetal acceleration to the right = lean right = negative Z).
            float rollTarget = Mathf.Clamp(-la.x * tune.turnLeanPerAccel, -tune.turnLeanMax, tune.turnLeanMax);
            float swayAcc = 80f * (0f - sway) - 2f * 0.5f * 9f * swayVel;
            swayVel += swayAcc * dt;
            sway += swayVel * dt;
            sway = Mathf.Clamp(sway, -25f, 25f);
            LeanRoll = Mathf.Lerp(LeanRoll, rollTarget, 1f - Mathf.Exp(-10f * dt));
            SwayRoll = sway;

            if (turnHold <= 0f && !sharpNow) { TurnBand = 0; }
            State = Classify(input, moving, strafing, sharpNow, curving, braking, s, top, la);

            Vector3 lv = LocalVelocity;
            AnimParams = new LocoAnimParams
            {
                moveSpeed = s,
                moveDirection = s > 0.05f ? Mathf.Atan2(lv.x, lv.z) * Mathf.Rad2Deg : 0f,
                acceleration = la.z,
                turnAngle = TurnAngle,
                turnBand = TurnBand,
                isSprinting = State == LocoState.Sprint || (input.sprint && s > RunSpeed),
                hasBall = input.hasBall,
                isDefending = input.jockey,
                isFatigued = false,
                balance = Balance
            };
        }

        /// <summary>Acceleration toward a target speed: strong at first, fading as the speed gets close (exponential).</summary>
        float AccelToward(float target, float s, float boost)
        {
            float a = Mathf.Min(MaxAccel * boost, (target - s) / Mathf.Max(0.05f, AccelTau));
            return Mathf.Max(a, 0.5f);
        }

        void BeginBrake(float decel, float speed)
        {
            brakeDecel = decel;
            // The upper body carries on forward for a moment.
            inertiaVel += tune.brakeJolt * Mathf.Clamp01(speed / Mathf.Max(1f, SprintSpeed)) * 10f;
        }

        void RegisterTurn(float signedAngle)
        {
            TurnAngle = signedAngle;
            float a = Mathf.Abs(signedAngle);
            TurnBand = a < 30f ? 0 : a < 67.5f ? 45 : a < 112.5f ? 90 : a < 157.5f ? 135 : 180;
            turnHold = 0.5f;
        }

        /// <summary>Fraction of top speed allowed when the body faces 'rel' degrees away from the running direction (no strafing).</summary>
        float FacingLimit(float rel)
        {
            if (rel <= 30f) return 1f;
            if (rel <= 90f) return Mathf.Lerp(1f, tune.facingLimitAt90, (rel - 30f) / 60f);
            return Mathf.Lerp(tune.facingLimitAt90, tune.facingLimitAt180, Mathf.Clamp01((rel - 90f) / 90f));
        }

        LocoState Classify(in LocoInput input, bool moving, bool strafing, bool sharpNow, bool curving, bool braking, float s, float top, Vector3 la)
        {
            if (sharpNow) return LocoState.SharpTurn;
            if (pivotHold > 0f) return LocoState.PivotTurn;
            if (Balance < 0.7f) return LocoState.Recovery;
            if (s < 0.15f && !(moving && firstStepTimer > 0f)) { gaitLevel = 0; return LocoState.Idle; }
            if (input.jockey && s < 0.6f) return LocoState.Jockey;
            Vector3 lv = LocalVelocity;
            if (strafing || lv.z < -0.3f || Mathf.Abs(lv.x) > Mathf.Abs(lv.z) + 0.2f)
            {
                if (lv.z < -0.3f && -lv.z >= Mathf.Abs(lv.x)) return LocoState.Backpedal;
                if (Mathf.Abs(lv.x) > Mathf.Abs(lv.z)) return LocoState.SideStep;
                if (input.jockey) return LocoState.Jockey;
            }
            if (moving && firstStepTimer > 0f && s < RunSpeed) return LocoState.StartStep;
            if (braking || !moving || la.z < -3.5f) return s < 1.2f ? LocoState.Stop : LocoState.Decelerate;
            if (curving && s > tune.jogSpeed) return LocoState.Curve;
            if (la.z > 2f && s < top * 0.85f) return LocoState.Accelerate;

            // Walk / Jog / Run / Sprint as separate states, with hysteresis so the gait does not flicker.
            float h = tune.gaitHysteresis * 0.5f;
            float sprintAt = RunSpeed * 1.02f;
            int want = s < tune.walkSpeed ? 0 : s < tune.jogSpeed ? 1 : (input.sprint && s > sprintAt) ? 3 : 2;
            if (want > gaitLevel)
            {
                float edge = gaitLevel == 0 ? tune.walkSpeed : gaitLevel == 1 ? tune.jogSpeed : sprintAt;
                if (s > edge + h || want == 3) gaitLevel = want;
            }
            else if (want < gaitLevel)
            {
                float edge = gaitLevel == 1 ? tune.walkSpeed : gaitLevel == 2 ? tune.jogSpeed : sprintAt;
                if (s < edge - h || (gaitLevel == 3 && !input.sprint)) gaitLevel = want;
            }
            switch (gaitLevel)
            {
                case 0: return LocoState.Walk;
                case 1: return LocoState.Jog;
                case 3: return LocoState.Sprint;
                default: return LocoState.Run;
            }
        }

        // ---- helpers ----

        /// <summary>
        /// Finds the time constant tau so that, with acceleration min(a0, (vmax - v) / tau), the speed reaches
        /// 95 % of vmax after t95 seconds.
        /// </summary>
        public static float SolveTau(float vmax, float a0, float t95)
        {
            float lo = 0.02f, hi = Mathf.Max(0.05f, t95);
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (TimeTo95(vmax, a0, mid) > t95) hi = mid; else lo = mid;
            }
            return 0.5f * (lo + hi);
        }

        static float TimeTo95(float vmax, float a0, float tau)
        {
            if (a0 * tau >= vmax) return tau * 2.9957f;           // never capped: pure exponential, ln 20
            if (a0 * tau <= 0.05f * vmax) return 0.95f * vmax / a0; // capped all the way
            float t1 = (vmax - a0 * tau) / a0;                       // constant push until the curve takes over
            return t1 + tau * Mathf.Log(a0 * tau / (0.05f * vmax));
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }

        /// <summary>Rotates v around the up axis by yaw degrees (same as Quaternion.Euler(0, yaw, 0) * v).</summary>
        public static Vector3 RotateYaw(Vector3 v, float yaw)
        {
            float r = yaw * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector3(v.x * c + v.z * sn, v.y, -v.x * sn + v.z * c);
        }

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
