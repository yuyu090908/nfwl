using NUnit.Framework;
using UnityEngine;

namespace NFWL.Tests
{
    /// <summary>
    /// v0.6 Real Football Engine, Phase 1: movement tests (spec 20, "移動テスト").
    /// Pure logic, no scene: Window > General > Test Runner > EditMode > Run All.
    /// Ranges follow the initial tuning table of the spec at gameplayAssist 0 (real timing).
    /// </summary>
    public class MovementTests
    {
        const float Dt = 1f / 60f;

        static PlayerLocomotion Make(int ability = 70, float assist = 0f)
        {
            PlayerLocomotion loco = new PlayerLocomotion();
            LocomotionTuning t = new LocomotionTuning { gameplayAssist = assist };
            loco.Configure(new PlayerAttributes { Speed = ability, Acceleration = ability, Agility = ability }, t);
            loco.Reset(Vector3.right);
            return loco;
        }

        static void Run(PlayerLocomotion loco, LocoInput input, float seconds)
        {
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++) loco.Tick(input, Dt);
        }

        static LocoInput SprintRight => new LocoInput { moveDir = Vector3.right, moveAmount = 1f, sprint = true };

        static float TimeTo95(PlayerLocomotion loco)
        {
            LocoInput input = SprintRight;
            for (float t = 0f; t < 8f; t += Dt)
            {
                loco.Tick(input, Dt);
                if (loco.Speed >= 0.95f * loco.SprintSpeed) return t + Dt;
            }
            return -1f;
        }

        // ------------------------------------------------------------------ standstill to top speed

        [Test]
        public void StandstillToTopSpeed_TakesRealisticTime([Values(40, 70, 99)] int ability)
        {
            float t = TimeTo95(Make(ability));
            // Spec: 2.2-3.0 s to top speed (Acceleration 99 .. 40).
            Assert.GreaterOrEqual(t, 2.0f, "reaches top speed too quickly");
            Assert.LessOrEqual(t, 3.2f, "reaches top speed too slowly");
        }

        [Test]
        public void Acceleration_IsStrongFirstAndFades()
        {
            PlayerLocomotion loco = Make();
            LocoInput input = SprintRight;
            Run(loco, input, 0.5f);
            float early = loco.Speed / 0.5f;            // mean acceleration over the first 0.5 s
            float s1 = loco.Speed;
            Run(loco, input, 1f);
            float late = (loco.Speed - s1) / 1f;        // mean acceleration from 0.5 s to 1.5 s
            Assert.Greater(early, 5f, "first steps must push hard");
            Assert.Less(late, early * 0.7f, "acceleration must fade toward top speed");
        }

        [Test]
        public void GameplayAssist_MakesAccelerationQuicker()
        {
            Assert.Less(TimeTo95(Make(70, 1f)), TimeTo95(Make(70, 0f)) * 0.8f);
        }

        // ------------------------------------------------------------------ stopping

        [Test]
        public void StopFromSprint_TimeDistanceAndDecelerationAgree([Values(40, 70, 99)] int ability)
        {
            PlayerLocomotion loco = Make(ability);
            Run(loco, SprintRight, 5f);
            float v0 = loco.Speed;
            Assert.Greater(v0, 0.95f * loco.SprintSpeed);

            LocoInput stop = new LocoInput();
            float t = 0f, dist = 0f, peakDecel = 0f, prev = v0;
            while (loco.Speed > 0.01f && t < 5f)
            {
                loco.Tick(stop, Dt);
                dist += loco.Speed * Dt;
                peakDecel = Mathf.Max(peakDecel, (prev - loco.Speed) / Dt);
                prev = loco.Speed;
                t += Dt;
            }
            // Spec: 0.8-1.2 s from high speed.
            Assert.GreaterOrEqual(t, 0.75f, "stops too abruptly");
            Assert.LessOrEqual(t, 1.25f, "slides too long");
            // Constant deceleration: distance = v0 * t / 2, deceleration = v0 / t.
            Assert.AreEqual(v0 * t * 0.5f, dist, v0 * t * 0.08f, "stop distance must match the stop time");
            Assert.AreEqual(loco.StoppingDistance(v0), dist, 0.3f, "AI stopping distance must match the real stop");
            Assert.LessOrEqual(peakDecel, v0 / t * 1.1f, "no hidden hard stop");
            Assert.LessOrEqual(peakDecel, 12.5f, "deceleration above what legs can do");
        }

        [Test]
        public void StopFromRun_IsShorter([Values(40, 99)] int ability)
        {
            PlayerLocomotion loco = Make(ability);
            Run(loco, new LocoInput { moveDir = Vector3.right, moveAmount = 1f }, 4f);
            float t = 0f;
            while (loco.Speed > 0.01f && t < 5f) { loco.Tick(new LocoInput(), Dt); t += Dt; }
            // Spec: 0.5-0.9 s from normal running.
            Assert.GreaterOrEqual(t, 0.45f);
            Assert.LessOrEqual(t, 0.95f);
        }

        [Test]
        public void StopWithBall_AlsoTakesSteps()
        {
            PlayerLocomotion loco = Make();
            LocoInput run = new LocoInput { moveDir = Vector3.right, moveAmount = 1f, sprint = true, hasBall = true };
            Run(loco, run, 5f);
            Assert.Less(loco.Speed, loco.SprintSpeed, "slower with the ball");
            float t = 0f;
            bool sawBraking = false;
            while (loco.Speed > 0.01f && t < 5f)
            {
                loco.Tick(new LocoInput { hasBall = true }, Dt);
                sawBraking |= loco.Braking;
                t += Dt;
            }
            Assert.IsTrue(sawBraking, "braking state for the short braking steps");
            Assert.GreaterOrEqual(t, 0.6f, "no instant stop with the ball either");
        }

        // ------------------------------------------------------------------ turning

        [Test]
        public void Turn90_AtSprint_CurvesWithoutSnapping()
        {
            PlayerLocomotion loco = Make();
            Run(loco, SprintRight, 4f);
            LocoInput input = SprintRight;
            input.moveDir = Vector3.back; // 90 deg to the right of +X seen from above (yaw 180)
            float prevYaw = loco.FacingYaw, maxStep = 0f, minSpeed = 99f, maxRoll = 0f;
            bool sawCurve = false;
            for (int i = 0; i < 120; i++)
            {
                loco.Tick(input, Dt);
                maxStep = Mathf.Max(maxStep, Mathf.Abs(Mathf.DeltaAngle(prevYaw, loco.FacingYaw)));
                prevYaw = loco.FacingYaw;
                minSpeed = Mathf.Min(minSpeed, loco.Speed);
                maxRoll = Mathf.Max(maxRoll, Mathf.Abs(loco.LeanRoll));
                sawCurve |= loco.State == LocoState.Curve;
            }
            Assert.IsTrue(sawCurve, "a 90 degree turn at speed is a curve, not a plant");
            Assert.Greater(minSpeed, 0.5f * loco.SprintSpeed, "keeps momentum through a 90 degree curve");
            Assert.LessOrEqual(maxStep, 10f, "body must not snap round");
            Assert.LessOrEqual(maxRoll, 20.5f, "lean into the curve is at most 20 degrees");
            Assert.Greater(maxRoll, 8f, "the body leans into the curve");
        }

        [Test]
        public void Turn180_PlantsBrakesAndPivots()
        {
            PlayerLocomotion loco = Make();
            Run(loco, SprintRight, 4f);
            LocoInput input = SprintRight;
            input.moveDir = Vector3.left;
            bool sawSharp = false, sawPivot = false;
            float reverse = -1f, t = 0f;
            int band = 0;
            for (int i = 0; i < 240; i++)
            {
                loco.Tick(input, Dt);
                t += Dt;
                sawSharp |= loco.State == LocoState.SharpTurn;
                sawPivot |= loco.State == LocoState.PivotTurn;
                band = Mathf.Max(band, loco.TurnBand);
                if (reverse < 0f && loco.Velocity.x < 0f) reverse = t;
            }
            Assert.IsTrue(sawSharp, "plants and brakes first");
            Assert.IsTrue(sawPivot, "then pivots on the planted foot");
            Assert.AreEqual(180, band, "classified as a 180 degree turn");
            // Braking from about 8 m/s cannot take less than about half a second.
            Assert.GreaterOrEqual(reverse, 0.45f, "velocity must not reverse quickly");
            Assert.Less(loco.Velocity.x, -0.7f * loco.SprintSpeed, "runs the other way at the end");
        }

        [Test]
        public void TurnBands_AreClassified([Values(45f, 90f, 135f, 180f)] float angle)
        {
            PlayerLocomotion loco = Make();
            Run(loco, SprintRight, 4f);
            LocoInput input = SprintRight;
            input.moveDir = PlayerLocomotion.YawToDir(90f + angle); // clockwise from +X
            loco.Tick(input, Dt);
            Assert.AreEqual((int)angle, loco.TurnBand);
            Assert.Greater(loco.TurnAngle, 0f, "clockwise turn is positive");
        }

        [Test]
        public void BackpedalToForward_NeedsAHipTurn()
        {
            PlayerLocomotion loco = Make();
            // Backpedal toward -X while facing +X.
            Run(loco, new LocoInput { moveDir = Vector3.left, moveAmount = 1f, faceDir = Vector3.right }, 2f);
            Assert.AreEqual(LocoState.Backpedal, loco.State);
            // Keep running the same way but drop the defensive stance: the body must turn before speeding up.
            LocoInput go = new LocoInput { moveDir = Vector3.left, moveAmount = 1f, sprint = true };
            Run(loco, go, 0.15f);
            Assert.Less(loco.Speed, 3.5f, "cannot sprint backwards");
            Run(loco, go, 2.5f);
            Assert.Less(Vector3.Angle(loco.Facing, Vector3.left), 10f, "turned to face the run");
            Assert.Greater(loco.Speed, 0.8f * loco.SprintSpeed, "and then sprints");
        }

        [Test]
        public void SideStepToSprint_TurnsTheBodyFirst()
        {
            PlayerLocomotion loco = Make();
            Run(loco, new LocoInput { moveDir = Vector3.forward, moveAmount = 1f, faceDir = Vector3.right }, 1.5f);
            Assert.AreEqual(LocoState.SideStep, loco.State);
            LocoInput go = new LocoInput { moveDir = Vector3.forward, moveAmount = 1f, sprint = true };
            float t = 0f;
            while (Vector3.Angle(loco.Facing, Vector3.forward) > 20f && t < 2f)
            {
                loco.Tick(go, Dt);
                t += Dt;
                Assert.LessOrEqual(loco.Speed, 0.5f * loco.SprintSpeed + 0.5f, "no sprinting sideways");
            }
            Assert.Less(t, 0.6f, "hip turn takes a moment, not long");
        }

        [Test]
        public void CurveRadius_AtSprint_IsRealistic()
        {
            PlayerLocomotion loco = Make();
            Run(loco, SprintRight, 4f);
            LocoInput input = SprintRight;
            float yawSum = 0f, speedSum = 0f;
            int n = 60;
            float prev = PlayerLocomotion.DirToYaw(loco.Velocity, 0f);
            for (int i = 0; i < n; i++)
            {
                // Keep pushing 80 degrees left of the running line (a tight circle).
                input.moveDir = PlayerLocomotion.YawToDir(PlayerLocomotion.DirToYaw(loco.Velocity, 0f) - 80f);
                loco.Tick(input, Dt);
                float y = PlayerLocomotion.DirToYaw(loco.Velocity, 0f);
                yawSum += Mathf.Abs(Mathf.DeltaAngle(prev, y));
                prev = y;
                speedSum += loco.Speed;
            }
            float omega = yawSum * Mathf.Deg2Rad / (n * Dt);
            float radius = speedSum / n / omega;
            // Grip 6-10 m/s^2: at 6-7 m/s the tightest circle is about 4-8 m (v^2 / a).
            Assert.Greater(radius, 3.5f, "turns far too tightly at speed");
        }

        // ------------------------------------------------------------------ lean

        [Test]
        public void StartLean_IsDeepAndFadesAtTopSpeed()
        {
            PlayerLocomotion loco = Make();
            float maxLean = 0f;
            for (int i = 0; i < 30; i++) { loco.Tick(SprintRight, Dt); maxLean = Mathf.Max(maxLean, loco.LeanPitch); }
            Assert.Greater(maxLean, 15f, "first steps lean forward");
            Assert.LessOrEqual(maxLean, 25.01f, "at most 25 degrees");
            Run(loco, SprintRight, 4f);
            Assert.Less(loco.LeanPitch, 12f, "more upright at top speed");
        }

        // ------------------------------------------------------------------ AI planning uses the same model

        [Test]
        public void EstimatedTime_MatchesTheRealRun([Values(5f, 15f, 30f)] float dist)
        {
            PlayerLocomotion loco = Make();
            float est = loco.EstimateTimeToReach(Vector3.zero, new Vector3(dist, 0f, 0f), true, false);
            float t = 0f, x = 0f;
            while (x < dist && t < 10f) { loco.Tick(SprintRight, Dt); x += loco.Speed * Dt; t += Dt; }
            Assert.AreEqual(t, est, Mathf.Max(0.15f, t * 0.1f));
        }

        [Test]
        public void ArrivalSpeed_StopsBeforeTheTarget()
        {
            PlayerLocomotion loco = Make();
            Run(loco, SprintRight, 4f);
            float d = 6f;
            float v = loco.ArrivalSpeed(d);
            Assert.LessOrEqual(loco.StoppingDistance(v), d + 0.05f);
        }

        // ------------------------------------------------------------------ input buffer

        [Test]
        public void InputBuffer_KeepsAPressForTheWindowOnly()
        {
            InputBuffer b = new InputBuffer();
            b.Press(BufferedAction.Shoot, 1f);
            Assert.IsTrue(b.Has(BufferedAction.Shoot, 1.1f, 0.2f));
            Assert.IsFalse(b.Has(BufferedAction.ShortPass, 1.1f, 0.2f), "other actions are separate");
            Assert.IsTrue(b.Consume(BufferedAction.Shoot, 1.15f, 0.2f));
            Assert.IsFalse(b.Has(BufferedAction.Shoot, 1.16f, 0.2f), "used once");
            b.Press(BufferedAction.Tackle, 2f);
            Assert.IsFalse(b.Consume(BufferedAction.Tackle, 2.3f, 0.25f), "expired");
        }

        [Test]
        public void InputBuffer_RemembersTheLastDirection()
        {
            InputBuffer b = new InputBuffer();
            b.RecordMove(new Vector3(0f, 0f, 2f), 1f);
            b.RecordMove(Vector3.zero, 1.1f); // keys let go
            Assert.AreEqual(1f, b.RecentMoveDir(1.2f, 0.3f).z, 1e-4f);
            Assert.AreEqual(Vector3.zero, b.RecentMoveDir(1.5f, 0.3f));
        }
    }
}
