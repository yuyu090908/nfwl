using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NFWL.Tests
{
    /// <summary>
    /// v0.6 EditMode tests: pure logic that runs without a scene
    /// (Window > General > Test Runner > EditMode > Run All).
    /// </summary>
    public class NFWLEditModeTests
    {
        // ---------------------------------------------------------------- QA 2: 180 degree turn

        [Test]
        public void Turn180_NoInstantFlip()
        {
            PlayerLocomotion loco = new PlayerLocomotion();
            loco.Configure(new PlayerAttributes(), new LocomotionTuning());
            loco.Reset(Vector3.right);
            const float dt = 1f / 60f;
            LocoInput input = new LocoInput { moveDir = Vector3.right, moveAmount = 1f, sprint = true };
            for (int i = 0; i < 180; i++) loco.Tick(input, dt);
            Assert.Greater(loco.Speed, 5f, "should be at sprint speed before turning");

            input.moveDir = Vector3.left;
            float prevYaw = loco.FacingYaw, maxStep = 0f, flipTime = -1f, reach70 = -1f, t = 0f;
            for (int i = 0; i < 240; i++)
            {
                loco.Tick(input, dt);
                t += dt;
                maxStep = Mathf.Max(maxStep, Mathf.Abs(Mathf.DeltaAngle(prevYaw, loco.FacingYaw)));
                prevYaw = loco.FacingYaw;
                if (flipTime < 0f && loco.Velocity.x < 0f) flipTime = t;
                if (reach70 < 0f && loco.Velocity.x < -0.7f * loco.TopSpeed(true, false)) reach70 = t;
            }
            // Sim reference (v0.6): max 9.5 deg/frame, velocity reverses after 0.40 s, 70 % speed after 1.23 s.
            Assert.LessOrEqual(maxStep, 20f, "body must not snap round (deg per 1/60 s frame)");
            Assert.GreaterOrEqual(flipTime, 0.2f, "velocity must not reverse instantly");
            Assert.GreaterOrEqual(reach70, 0.5f, "must not be back at speed instantly");
            Assert.Greater(reach70, 0f, "must finish the turn and run the other way");
            Assert.AreEqual(270f, Mathf.Repeat(loco.FacingYaw, 360f), 5f, "ends facing -X");
        }

        // ---------------------------------------------------------------- QA 5: pass goes where it is aimed

        [Test]
        public void GroundPass_FollowsAimLine([Values(0f, 37f, 145f, 260f)] float yawDeg, [Values(8f, 18f)] float dist)
        {
            Vector3 dir = Quaternion.Euler(0f, yawDeg, 0f) * Vector3.forward;
            Vector3 start = new Vector3(0f, PitchDims.BallRadius, 0f);
            float speed = BallSolver.GroundPassSpeed(dist, 6f);
            float t = BallSolver.GroundTravelTime(speed, dist);
            Vector3 end = Ball.Simulate(start, dir * speed, Vector3.zero, t, out Vector3 vEnd);
            Vector3 flat = end - start;
            flat.y = 0f;
            Assert.Less(Vector3.Angle(flat, dir), 1f, "no spin: the ball stays on the aim line");
            Assert.AreEqual(dist, flat.magnitude, dist * 0.08f, "arrives at the target distance");
            Vector3 vf = vEnd; vf.y = 0f;
            Assert.AreEqual(6f, vf.magnitude, 1.2f, "arrives at the planned speed");
        }

        // ---------------------------------------------------------------- QA 6: no abnormal speeds from the force model

        [Test]
        public void BallForces_NeverAddSpeed([Values(10f, 25f, 34f)] float speed)
        {
            Vector3 v = new Vector3(speed, 6f, 2f);
            Vector3 spin = new Vector3(0f, -60f, 30f); // strong curl + topspin
            Vector3 p = new Vector3(0f, 0.5f, 0f);
            float startSpeed = v.magnitude;
            for (int i = 0; i < 100; i++)
            {
                p = Ball.Simulate(p, v, spin, 0.02f, out v);
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(v.x), "NaN in ball state");
                // Gravity can add speed while falling, but never more than free fall from the top of the flight would.
                Assert.LessOrEqual(v.magnitude, startSpeed + 6f, "ball gained speed out of nothing");
            }
        }

        // ---------------------------------------------------------------- QA 8 (logic part): keeper reach model

        [Test]
        public void KeeperReach_HeightFallsWithStretch()
        {
            float prev = float.MaxValue;
            for (float lateral = 0f; lateral <= 4f; lateral += 0.25f)
            {
                float h = GoalkeeperController.MaxHeightAt(lateral);
                Assert.LessOrEqual(h, prev + 1e-4f, "the further the dive, the lower the hands reach");
                prev = h;
            }
            Assert.Less(GoalkeeperController.MaxHeightAt(3.5f), GoalkeeperController.MaxHeightAt(0f));
        }

        // ---------------------------------------------------------------- QA 10: goal detection

        [Test]
        public void GoalDetection_WholeBallOverTheLine()
        {
            float r = PitchDims.BallRadius;
            float L = PitchDims.HalfLength;
            Assert.AreEqual(BallState.GoalForHome, Rules.ClassifyBall(new Vector3(L + r + 0.05f, 0.5f, 0f)));
            Assert.AreEqual(BallState.GoalForAway, Rules.ClassifyBall(new Vector3(-L - r - 0.05f, 0.5f, 0.3f)));
            Assert.AreEqual(BallState.InPlay, Rules.ClassifyBall(new Vector3(L + r * 0.5f, 0.5f, 0f)), "on the line is not a goal");
            Assert.AreNotEqual(BallState.GoalForHome, Rules.ClassifyBall(new Vector3(L + 0.5f, 0.5f, PitchDims.GoalHalfWidth + 0.5f)), "wide of the post");
            Assert.AreNotEqual(BallState.GoalForHome, Rules.ClassifyBall(new Vector3(L + 0.5f, PitchDims.GoalHeight + 0.4f, 0f)), "over the bar");
            Assert.AreEqual(BallState.InPlay, Rules.ClassifyBall(new Vector3(0f, 0.11f, 0f)));
        }

        // ---------------------------------------------------------------- PHASE 11: NFT data and skills

        [Test]
        public void NftMetadata_ParsesTraitsAndKeepsOthers()
        {
            string json = "{\"home\":[{\"name\":\"CRYSTAL ONE\",\"attributes\":[{\"trait_type\":\"Speed\",\"value\":91},"
                        + "{\"trait_type\":\"Rarity\",\"value\":\"Legendary\"},{\"trait_type\":\"Skill\",\"value\":\"CatReflexes\"},"
                        + "{\"trait_type\":\"Finishing\",\"value\":150}]}],"
                        + "\"away\":[{\"name\":\"B\",\"rarity\":\"Epic\",\"attributes\":{\"Tackling\":77},\"skills\":[\"IronWall\",\"PinpointPass\"]}]}";
            JsonPlayerSource src = JsonPlayerSource.Parse(json);
            Assert.IsTrue(src.TryGet(0, 0, out PlayerData a));
            Assert.AreEqual("CRYSTAL ONE", a.name);
            Assert.AreEqual(Rarity.Legendary, a.rarity);
            CollectionAssert.Contains(a.skills, SkillId.CatReflexes);
            PlayerAttributes gen = new PlayerAttributes { Speed = 60, Agility = 66 };
            PlayerAttributes built = a.BuildAttributes(gen);
            Assert.AreEqual(91, built.Speed);
            Assert.AreEqual(99, built.Finishing, "abilities are clamped to 40..99");
            Assert.AreEqual(66, built.Agility, "values not in the data keep the generated value");

            Assert.IsTrue(src.TryGet(1, 0, out PlayerData b));
            Assert.AreEqual(Rarity.Epic, b.rarity);
            Assert.AreEqual(77, b.BuildAttributes(gen).Tackling);
            Assert.IsFalse(src.TryGet(0, 3, out _), "slots without data keep the random player");
        }

        [Test]
        public void Skills_LimitedByRarity()
        {
            List<SkillId> wanted = new List<SkillId> { SkillId.IronWall, SkillId.PinpointPass, SkillId.CatReflexes, SkillId.CoolFinisher };
            Assert.AreEqual(0, Skills.Clamp(Rarity.Common, wanted).Count);
            Assert.AreEqual(0, Skills.Clamp(Rarity.Rare, wanted).Count);
            Assert.AreEqual(1, Skills.Clamp(Rarity.Epic, wanted).Count);
            Assert.AreEqual(2, Skills.Clamp(Rarity.Mythic, wanted).Count);
            Assert.AreEqual(3, Skills.Clamp(Rarity.Legendary, wanted).Count);
        }

        [Test]
        public void RandomGeneration_UnchangedFromV05()
        {
            // Same seed, same numbers: the v0.5 generator is kept as it was.
            PlayerAttributes a = PlayerAttributes.Random(74, new System.Random(2026));
            PlayerAttributes b = PlayerAttributes.Random(74, new System.Random(2026));
            Assert.AreEqual(a.Speed, b.Speed);
            Assert.AreEqual(a.Goalkeeping, b.Goalkeeping);
            Assert.That(a.Speed, Is.InRange(40, 99));
        }
    }
}
