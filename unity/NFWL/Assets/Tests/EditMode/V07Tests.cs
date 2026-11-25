using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NFWL.Tests
{
    /// <summary>
    /// v0.7 EditMode tests (pure logic, no scene): graphics presets, per-player motion styles, kick
    /// flight types, motion-matching trajectory, floodlight colours and directions, body proportions,
    /// crystal rarity, goal net springs and knee IK. Window > General > Test Runner > EditMode > Run All.
    /// </summary>
    public class V07Tests
    {
        // ---------------- PHASE 13: graphics presets ----------------

        [Test]
        public void GraphicsPresets_IncreaseWithQuality()
        {
            for (int i = 1; i < 4; i++)
            {
                GraphicsPreset lo = GraphicsPreset.For((QualityLevel)(i - 1)), hi = GraphicsPreset.For((QualityLevel)i);
                Assert.GreaterOrEqual(hi.shadowDistance, lo.shadowDistance, "shadow distance");
                Assert.GreaterOrEqual(hi.shadowCascades, lo.shadowCascades, "cascades");
                Assert.GreaterOrEqual((int)hi.shadowResolution, (int)lo.shadowResolution, "shadow resolution");
                Assert.GreaterOrEqual(hi.msaa, lo.msaa, "MSAA");
                Assert.GreaterOrEqual(hi.crowdDensity, lo.crowdDensity, "crowd");
                Assert.GreaterOrEqual(hi.crowdRows3D, lo.crowdRows3D, "crowd rows");
                Assert.GreaterOrEqual(hi.grassShells, lo.grassShells, "grass");
                Assert.GreaterOrEqual(hi.pixelLights, lo.pixelLights, "pixel lights");
                Assert.GreaterOrEqual(hi.renderScale, lo.renderScale, "render scale");
                Assert.LessOrEqual(hi.textureMipLimit, lo.textureMipLimit, "texture quality");
                Assert.GreaterOrEqual(hi.ssaoIntensity, lo.ssaoIntensity, "SSAO");
            }
        }

        [Test]
        public void GraphicsPresets_LowIsLightweight_UltraHasEverything()
        {
            GraphicsPreset low = GraphicsPreset.For(QualityLevel.Low);
            Assert.IsFalse(low.postProcessing);
            Assert.IsFalse(low.ssao);
            Assert.AreEqual(0, low.grassShells);
            Assert.AreEqual(0f, low.crowdDensity);
            Assert.Less(low.renderScale, 1f);
            GraphicsPreset ultra = GraphicsPreset.For(QualityLevel.Ultra);
            Assert.IsTrue(ultra.postProcessing && ultra.bloom && ultra.ssao && ultra.fullResSsao);
            Assert.AreEqual(4, ultra.shadowCascades);
            Assert.AreEqual(NetQuality.Dynamic, ultra.net);
            // SSAO is never set so strong that contacts look dirty (spec 8.5).
            for (int i = 0; i < 4; i++) Assert.LessOrEqual(GraphicsPreset.For((QualityLevel)i).ssaoIntensity, 0.7f);
        }

        // ---------------- PHASE 1.5: per-player running style ----------------

        [Test]
        public void MotionProfile_EveryPlayerStaysNatural()
        {
            for (int team = 0; team < 2; team++)
            for (int number = 1; number <= 99; number++)
            for (float h = 0.95f; h <= 1.06f; h += 0.0275f)
            {
                MotionProfile p = MotionProfile.For(team, number, h);
                Assert.IsTrue(p.WithinNaturalRange, $"team {team} #{number} h {h}");
            }
            Assert.IsTrue(MotionProfile.Neutral.WithinNaturalRange);
        }

        [Test]
        public void MotionProfile_IsDeterministicAndVaried()
        {
            MotionProfile a = MotionProfile.For(0, 10, 1f), b = MotionProfile.For(0, 10, 1f);
            Assert.AreEqual(a.cadenceScale, b.cadenceScale);
            Assert.AreEqual(a.phaseOffset, b.phaseOffset);
            // Ten players: not all in step (phases spread over the cycle).
            float min = 1f, max = 0f;
            int[] numbers = { 1, 2, 4, 6, 10 };
            for (int t = 0; t < 2; t++)
            foreach (int n in numbers)
            {
                float ph = MotionProfile.For(t, n, 1f).phaseOffset;
                min = Mathf.Min(min, ph); max = Mathf.Max(max, ph);
            }
            Assert.Greater(max - min, 0.4f);
        }

        // ---------------- PHASE 5.2: flight types ----------------

        [Test]
        public void ShotTrajectory_ClassifiesTheFiveTypes()
        {
            Assert.AreEqual(ShotTrajectory.Ground, ShotTrajectories.Classify(new Vector3(15f, 0.3f, 0f), Vector3.zero));
            Assert.AreEqual(ShotTrajectory.Driven, ShotTrajectories.Classify(new Vector3(24f, 3f, 0f), Vector3.zero));
            Assert.AreEqual(ShotTrajectory.Rising, ShotTrajectories.Classify(new Vector3(16f, 5f, 0f), Vector3.zero));
            Assert.AreEqual(ShotTrajectory.Lofted, ShotTrajectories.Classify(new Vector3(10f, 8f, 0f), Vector3.zero));
            // Sidespin about the vertical axis: Magnus pushes the ball sideways = curved.
            Assert.AreEqual(ShotTrajectory.Curved, ShotTrajectories.Classify(new Vector3(20f, 2f, 0f), new Vector3(0f, 40f, 0f)));
        }

        // ---------------- PHASE 12: motion-matching trajectory ----------------

        [Test]
        public void MotionQuery_PredictionIsContinuousAndConverges()
        {
            Vector3 pos = new Vector3(1f, 0f, 2f), vel = new Vector3(5f, 0f, 0f), want = new Vector3(0f, 0f, 6f);
            TrajectoryPoint p0 = MotionTrajectory.Predict(pos, vel, want, 90f, 0f);
            Assert.Less(Vector3.Distance(p0.position, pos), 1e-4f, "starts at the player");
            Assert.Less(Vector3.Distance(p0.velocity, vel), 1e-4f, "starts with the current velocity");
            TrajectoryPoint far = MotionTrajectory.Predict(pos, vel, want, 90f, 5f);
            Assert.Less(Vector3.Distance(far.velocity, want), 0.05f, "ends at the wanted velocity");
            // Position is the integral of the velocity (finite difference check).
            TrajectoryPoint a = MotionTrajectory.Predict(pos, vel, want, 90f, 0.3f), b = MotionTrajectory.Predict(pos, vel, want, 90f, 0.31f);
            Vector3 fd = (b.position - a.position) / 0.01f;
            Assert.Less(Vector3.Distance(fd, (a.velocity + b.velocity) * 0.5f), 0.05f);
            // No instant turn: halfway through the response the heading is in between.
            float yaw = MotionTrajectory.Predict(pos, vel, want, 90f, 0.3f).facingYaw;
            Assert.Greater(yaw, 5f); Assert.Less(yaw, 85f);
        }

        // ---------------- PHASE 8: floodlights ----------------

        [Test]
        public void Kelvin_WarmAndCoolLamps()
        {
            Color warm = FloodlightMath.Kelvin(3500f), cool = FloodlightMath.Kelvin(7500f);
            Assert.Greater(warm.r, warm.b);
            Assert.GreaterOrEqual(cool.b, cool.r);
            Color mid = FloodlightMath.Kelvin(6600f);
            Assert.Greater(mid.g, 0.9f);
        }

        [Test]
        public void Floodlights_ShineDownOntoThePitchFromEveryTower()
        {
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                Vector3 tower = FloodlightMath.TowerPosition(sx, sz);
                Vector3 dir = FloodlightMath.FromTower(tower) * Vector3.forward;
                Assert.Less(dir.y, -0.3f, "pointing down");
                Assert.Less(Vector3.Dot(new Vector3(dir.x, 0f, dir.z), new Vector3(tower.x, 0f, tower.z)), 0f, "toward the centre");
                // Towers stand outside the pitch and the stands' front edge.
                Assert.Greater(Mathf.Abs(tower.x), PitchDims.HalfLength + 5f);
                Assert.Greater(Mathf.Abs(tower.z), PitchDims.HalfWidth + 5f);
            }
        }

        // ---------------- PHASE 7: body and crystals ----------------

        static BodyMeshes.Ring[] Profile(string field)
        {
            FieldInfo f = typeof(CrystaliaProfiles).GetField(field, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(f, field);
            return (BodyMeshes.Ring[])f.GetValue(null);
        }

        [Test]
        public void BodyProfiles_AreWellFormed()
        {
            string[] names = { "ShirtRings", "ShortsRings", "ShortsLegRings", "ThighRings", "KneeRings", "SockRings", "BootRings",
                               "NeckRings", "HeadRings", "SleeveRings", "UpperArmRings", "ForearmRings", "HandRings" };
            foreach (string n in names)
            {
                BodyMeshes.Ring[] r = Profile(n);
                Assert.GreaterOrEqual(r.Length, 3, n);
                for (int i = 1; i < r.Length; i++) Assert.Less(r[i].y, r[i - 1].y, n + " rings go down the bone");
                for (int i = 0; i < r.Length; i++)
                {
                    Assert.Greater(r[i].rx, 0f, n); Assert.Greater(r[i].rz, 0f, n);
                    Assert.LessOrEqual(r[i].occlusion, 1f, n); Assert.Greater(r[i].occlusion, 0.3f, n + ": no black crease");
                }
                // Closed ends (rounded caps).
                Assert.Less(r[0].rx, 0.01f, n); Assert.Less(r[r.Length - 1].rx, 0.01f, n);
            }
        }

        [Test]
        public void BodyProportions_AreHuman()
        {
            BodyMeshes.Ring shoulders = BodyMeshes.Sample(Profile("ShirtRings"), 0.5f);
            BodyMeshes.Ring waist = BodyMeshes.Sample(Profile("ShirtRings"), 0.2f);
            Assert.Greater(shoulders.rx, waist.rx * 1.15f, "V-shaped torso");
            BodyMeshes.Ring head = BodyMeshes.Sample(Profile("HeadRings"), 0f);
            Assert.Greater(head.rz, head.rx, "head longer front-to-back than wide");
            Assert.Less(head.rx * 2f, shoulders.rx * 2f * 0.6f, "head narrower than shoulders");
            BodyMeshes.Ring calf = BodyMeshes.Sample(Profile("SockRings"), -0.13f);
            BodyMeshes.Ring ankle = BodyMeshes.Sample(Profile("SockRings"), -0.4f);
            Assert.Greater(calf.rx, ankle.rx * 1.4f, "calf thicker than ankle");
            Assert.Less(calf.cz, 0f, "calf muscle at the back");
            // Shirt hem (torso pivot is 0.1 above the hips) overlaps the shorts' waistband: no gap at the waist.
            BodyMeshes.Ring[] shirt = Profile("ShirtRings"), shorts = Profile("ShortsRings");
            float shirtBottom = 0.1f + shirt[shirt.Length - 2].y;
            float shortsTop = 0.02f + shorts[1].y;
            Assert.Less(shirtBottom, shortsTop);
        }

        [Test]
        public void CrystalRarity_MoreOrnateButNeverBlinding()
        {
            for (int i = 1; i < 5; i++)
            {
                CrystalLook lo = CrystalLook.For((Rarity)(i - 1)), hi = CrystalLook.For((Rarity)i);
                Assert.GreaterOrEqual(hi.shoulderShards + hi.armShards + hi.legShards + hi.hairShards,
                    lo.shoulderShards + lo.armShards + lo.legShards + lo.hairShards);
                Assert.GreaterOrEqual(hi.glow, lo.glow);
            }
            for (int i = 0; i < 5; i++) Assert.LessOrEqual(CrystalLook.For((Rarity)i).glow, 1f, "glow stays moderate in matches");
            Assert.Greater(CrystalLook.For(Rarity.Mythic).pulseAmount, 0f, "Mythic pulses");
            Assert.Greater(CrystalLook.For(Rarity.Legendary).sparkle, 0f, "Legendary sparkles");
            Assert.AreEqual(0f, CrystalLook.For(Rarity.Common).pulseAmount);
        }

        // ---------------- PHASE 9.3: goal net ----------------

        [Test]
        public void NetSpring_BulgesThenSettlesWithFixedEdges()
        {
            const int cols = 21, rows = 14;
            var disp = new float[cols * rows];
            var vel = new float[cols * rows];
            var pinned = new bool[cols * rows];
            for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++)
                pinned[r * cols + c] = r == 0 || c == 0 || r == rows - 1 || c == cols - 1;
            int hit = 7 * cols + 10;
            vel[hit] = 8f; vel[hit - 1] = 5f; vel[hit + 1] = 5f;
            float peak = 0f;
            for (int i = 0; i < 30; i++)
            {
                NetSpring.Step(disp, vel, pinned, cols, rows, 90f, 6f, 260f, 0.45f, 1f / 60f);
                peak = Mathf.Max(peak, disp[hit]);
            }
            Assert.Greater(peak, 0.03f, "the net bulges where it was hit");
            Assert.Greater(Mathf.Abs(disp[hit - 3]), 0f, "the bulge spreads to neighbouring knots");
            float energy = 0f;
            for (int i = 0; i < 600; i++) energy = NetSpring.Step(disp, vel, pinned, cols, rows, 90f, 6f, 260f, 0.45f, 1f / 30f);
            Assert.Less(energy / disp.Length, 0.0002f, "settles back (stable even at 30 FPS)");
            for (int i = 0; i < disp.Length; i++) if (pinned[i]) Assert.AreEqual(0f, disp[i], "frame edges never move");
        }

        // ---------------- PHASE 1.4: foot IK ----------------

        [Test]
        public void LegIK_KneeNeverBendsBackward()
        {
            var rnd = new System.Random(3);
            for (int i = 0; i < 500; i++)
            {
                Vector3 hip = new Vector3(0f, 0.95f, 0f);
                Vector3 target = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() * 0.6f, (float)rnd.NextDouble() * 1.2f - 0.6f);
                Vector3 pole = new Vector3((float)rnd.NextDouble() * 0.2f - 0.1f, 0.05f, 1f).normalized;
                LegIK.SolveKnee(hip, target, 0.44f, 0.42f, pole, out Vector3 knee, out Vector3 reached);
                Assert.AreEqual(0.44f, Vector3.Distance(hip, knee), 0.002f, "thigh length kept");
                Assert.AreEqual(0.42f, Vector3.Distance(knee, reached), 0.002f, "shin length kept");
                Vector3 axis = (reached - hip).normalized;
                Vector3 bendDir = Vector3.ProjectOnPlane(pole, axis);
                Vector3 offAxis = Vector3.ProjectOnPlane(knee - hip, axis);
                Assert.GreaterOrEqual(Vector3.Dot(offAxis, bendDir), -1e-4f, "knee on the pole (front) side");
            }
        }
    }
}
