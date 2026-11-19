using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NFWL.Tests
{
    /// <summary>
    /// v0.6 PlayMode tests: each test builds a real match (MatchManager creates the pitch, ball,
    /// ten players, camera) and checks one behaviour. Window > General > Test Runner > PlayMode > Run All.
    /// Long tests speed the game up with Time.timeScale.
    /// </summary>
    public class NFWLPlayModeTests
    {
        MatchManager mm;

        IEnumerator StartMatch(float halfSeconds = 600f, bool aiPaused = false, bool autoPlay = false)
        {
            Time.timeScale = 1f;
            GameObject go = new GameObject("Match");
            mm = go.AddComponent<MatchManager>();
            mm.halfLengthSeconds = halfSeconds;
            mm.ReplaysEnabled = false;
            mm.AIPaused = aiPaused;
            mm.AutoPlay = autoPlay;
            yield return null; // Start builds the match
            yield return new WaitForFixedUpdate();
            Assert.IsNotNull(mm.MatchBall, "match did not build");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(root);
            mm = null;
            yield return null;
        }

        PlayerController Find(int team, Role role)
        {
            foreach (PlayerController p in mm.TeamList(team)) if (p.role == role) return p;
            return null;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static void Place(PlayerController p, Vector3 at, Vector3 facing)
        {
            p.Teleport(at, Quaternion.LookRotation(facing, Vector3.up));
        }

        /// <summary>Puts the ball at a player's feet and gives it to him.</summary>
        IEnumerator GiveBall(PlayerController p)
        {
            Ball b = mm.MatchBall;
            b.ResetBall(p.transform.position + p.Locomotion.Facing * 0.45f + Vector3.up * PitchDims.BallRadius);
            yield return new WaitForFixedUpdate();
            p.GainBall();
            yield return new WaitForFixedUpdate();
        }

        // ---------------------------------------------------------------- QA 1: feet do not slide

        [UnityTest]
        public IEnumerator Running_FeetDoNotSlide()
        {
            yield return StartMatch(autoPlay: true);
            Time.timeScale = 1f;
            float maxErr = 0f, sumErr = 0f;
            int n = 0;
            float maxPlantDrift = 0f;
            PlayerController[] watch = { Find(0, Role.FW), Find(1, Role.MF), Find(0, Role.DF) };
            Vector3[] lastL = new Vector3[3], lastR = new Vector3[3];
            bool[] wasPlantedL = new bool[3], wasPlantedR = new bool[3];
            for (float t = 0f; t < 6f; t += Time.deltaTime)
            {
                yield return null;
                for (int i = 0; i < watch.Length; i++)
                {
                    PlayerModel m = watch[i].Model;
                    float e = m.FootReachError;
                    maxErr = Mathf.Max(maxErr, e);
                    sumErr += e;
                    n++;
                    GaitController.Foot L = m.Gait.Left, R = m.Gait.Right;
                    bool pl = !L.swinging && !L.overridden, pr = !R.swinging && !R.overridden;
                    if (pl && wasPlantedL[i]) maxPlantDrift = Mathf.Max(maxPlantDrift, Flat(L.plant - lastL[i]).magnitude);
                    if (pr && wasPlantedR[i]) maxPlantDrift = Mathf.Max(maxPlantDrift, Flat(R.plant - lastR[i]).magnitude);
                    lastL[i] = L.plant; lastR[i] = R.plant;
                    wasPlantedL[i] = pl; wasPlantedR[i] = pr;
                }
            }
            Debug.Log($"[QA1] foot reach error avg {sumErr / Mathf.Max(1, n) * 100f:0.0} cm, max {maxErr * 100f:0.0} cm, planted drift max {maxPlantDrift * 100f:0.0} cm/frame");
            Assert.Less(sumErr / Mathf.Max(1, n), 0.02f, "planted feet out of leg reach on average (sliding)");
            Assert.Less(maxPlantDrift, 0.06f, "a planted foot moved along the ground");
        }

        // ---------------------------------------------------------------- QA 3: dribble without teleporting ball

        [UnityTest]
        public IEnumerator Dribble_BallNeverTeleports()
        {
            yield return StartMatch(aiPaused: true);
            PlayerController fw = Find(0, Role.FW);
            Place(fw, new Vector3(-5f, 0f, 0f), Vector3.right);
            yield return GiveBall(fw);
            Assert.IsTrue(fw.HasBall, "setup: forward should have the ball");
            mm.AIPaused = false;
            mm.AutoPlay = true; // the AI dribbles / passes
            Ball b = mm.MatchBall;
            Vector3 last = b.Body.position;
            float maxSpeed = 0f;
            for (float t = 0f; t < 4f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                Vector3 p = b.Body.position;
                float s = (p - last).magnitude / Time.fixedDeltaTime;
                if (b.Owner != null && !b.HeldInHands) maxSpeed = Mathf.Max(maxSpeed, s);
                last = p;
            }
            Debug.Log($"[QA3] max ball speed while owned {maxSpeed:0.0} m/s");
            Assert.Less(maxSpeed, 16f, "the ball jumped while being dribbled");
        }

        // ---------------------------------------------------------------- QA 4: kick contact is synced to the foot

        [UnityTest]
        public IEnumerator Kick_BallLeavesAtFootContact()
        {
            yield return StartMatch(aiPaused: true);
            PlayerController fw = Find(0, Role.FW);
            Place(fw, new Vector3(0f, 0f, 0f), Vector3.right);
            yield return GiveBall(fw);
            Ball b = mm.MatchBall;
            float impactTime = -1f;
            Vector3 footAtImpact = Vector3.zero, ballAtImpact = Vector3.zero;
            System.Action<string, float> onImpact = (kind, speed) =>
            {
                if (kind != "Kick" || impactTime >= 0f) return;
                impactTime = Time.time;
                footAtImpact = fw.Model.Gait.Get(fw.Kick.Side).position;
                ballAtImpact = b.Body.position;
            };
            b.Impact += onImpact;
            float pressed = Time.time;
            fw.DebugKick(KickType.Shoot, 0.8f, Vector3.right);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(fw.Kick.Active, "kick should have started");
            float planned = fw.Kick.ContactTime;
            for (float t = 0f; t < 1.5f && impactTime < 0f; t += Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
            b.Impact -= onImpact;
            Assert.GreaterOrEqual(impactTime, 0f, "ball was never struck");
            Debug.Log($"[QA4] planned contact {planned - pressed:0.000}s after the key, ball left {impactTime - pressed:0.000}s, foot-ball {Flat(footAtImpact - ballAtImpact).magnitude * 100f:0} cm");
            Assert.Greater(impactTime - pressed, 0.1f, "the key press must not launch the ball by itself");
            Assert.AreEqual(planned, impactTime, Time.fixedDeltaTime + 0.001f, "ball must leave in the contact step");
            Assert.Less(Flat(footAtImpact - ballAtImpact).magnitude, 0.6f, "the kicking foot is at the ball when it leaves");
        }

        // ---------------------------------------------------------------- QA 5: pass flies toward the aim

        [UnityTest]
        public IEnumerator Pass_FliesTowardAim()
        {
            yield return StartMatch(aiPaused: true);
            PlayerController mf = Find(0, Role.MF);
            PlayerController fw = Find(0, Role.FW);
            Place(mf, new Vector3(-6f, 0f, 0f), Vector3.right);
            Place(fw, new Vector3(4f, 0f, 4f), Vector3.left);
            yield return GiveBall(mf);
            Vector3 aim = Flat(fw.transform.position - mf.transform.position);
            Ball b = mm.MatchBall;
            Vector3 launch = Vector3.zero;
            System.Action<string, float> onImpact = (kind, speed) => { if (kind == "Kick" && launch == Vector3.zero) launch = b.Velocity; };
            b.Impact += onImpact;
            mf.DebugKick(KickType.ShortPass, 0.5f, aim);
            for (float t = 0f; t < 1.5f && launch == Vector3.zero; t += Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
            b.Impact -= onImpact;
            Assert.AreNotEqual(Vector3.zero, launch, "pass was never struck");
            float err = Vector3.Angle(Flat(launch), aim);
            Debug.Log($"[QA5] pass direction error {err:0.0} deg, speed {launch.magnitude:0.0} m/s");
            Assert.Less(err, 12f, "pass went well away from the aim");
        }

        // ---------------------------------------------------------------- QA 6: collisions never add speed

        [UnityTest]
        public IEnumerator BallCollisions_NoAbnormalSpeed()
        {
            yield return StartMatch(aiPaused: true);
            Ball b = mm.MatchBall;
            float L = PitchDims.HalfLength;
            // Into the post, then the crossbar, then the ground at an angle, then through the players.
            Vector3[] starts = { new Vector3(L - 8f, 1f, PitchDims.GoalHalfWidth), new Vector3(L - 8f, 2f, 0f), new Vector3(0f, 3f, 0f), new Vector3(-8f, 0.5f, 0f) };
            Vector3[] targets = { new Vector3(L, 1f, PitchDims.GoalHalfWidth), new Vector3(L, PitchDims.GoalHeight, 0f), new Vector3(6f, 0f, 2f), new Vector3(-20f, 0.5f, 0f) };
            foreach (PlayerController p in mm.TeamList(1)) if (p.role != Role.GK) Place(p, new Vector3(-14f, 0f, p.transform.position.z * 0.3f), Vector3.right);
            for (int k = 0; k < starts.Length; k++)
            {
                b.ResetBall(starts[k]);
                yield return new WaitForFixedUpdate();
                Vector3 v = (targets[k] - starts[k]).normalized * 30f;
                b.Kick(v, new Vector3(0f, 40f, 0f), null);
                float max = 0f;
                for (float t = 0f; t < 2.5f; t += Time.fixedDeltaTime)
                {
                    yield return new WaitForFixedUpdate();
                    Vector3 bv = b.Velocity;
                    Assert.IsFalse(float.IsNaN(bv.x) || float.IsNaN(b.Body.position.x), "NaN ball state");
                    max = Mathf.Max(max, bv.magnitude);
                }
                Debug.Log($"[QA6] shot {k}: max speed after launch {max:0.0} m/s (launched at 30)");
                Assert.LessOrEqual(max, 31.5f, "a collision gave the ball extra speed");
            }
        }

        // ---------------------------------------------------------------- QA 7: tackle needs real contact

        [UnityTest]
        public IEnumerator Tackle_OutOfReachNeverWins()
        {
            yield return StartMatch(aiPaused: true);
            PlayerController carrier = Find(1, Role.FW);
            PlayerController defender = Find(0, Role.DF);
            Place(carrier, new Vector3(0f, 0f, 0f), Vector3.left);
            yield return GiveBall(carrier);
            Place(defender, new Vector3(-3f, 0f, 0f), Vector3.right);
            for (int i = 0; i < 5; i++)
            {
                Place(carrier, new Vector3(0f, 0f, 0f), Vector3.left);
                Place(defender, new Vector3(-3f, 0f, 0f), Vector3.right);
                if (!carrier.HasBall) yield return GiveBall(carrier);
                yield return new WaitForSeconds(0.9f); // tackle cooldown
                Assert.IsTrue(defender.Tackle.Begin(mm.MatchBall, Time.time), "tackle should start");
                yield return new WaitForSeconds(0.3f);
                Assert.AreEqual("OUT OF REACH", defender.Tackle.LastResult, "a tackle 3 m away must miss");
                Assert.IsFalse(defender.HasBall, "won the ball without reaching it");
            }

            // In reach and in front: the tackle is judged (any of WON / POKED / MISSED, never OUT OF REACH).
            Place(carrier, new Vector3(0f, 0f, 0f), Vector3.left);
            if (!carrier.HasBall) yield return GiveBall(carrier);
            Vector3 ballAt = mm.MatchBall.Body.position;
            Place(defender, ballAt + Vector3.left * 0.7f - Vector3.up * ballAt.y, Vector3.right);
            yield return new WaitForSeconds(0.9f);
            defender.Tackle.Begin(mm.MatchBall, Time.time);
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[QA7] close tackle result {defender.Tackle.LastResult}");
            Assert.AreNotEqual("OUT OF REACH", defender.Tackle.LastResult, "a tackle at the ball should be judged");
        }

        // ---------------------------------------------------------------- QA 8: keeper never saves the unreachable

        [UnityTest]
        public IEnumerator Keeper_DoesNotSaveUnreachableShot()
        {
            yield return StartMatch(aiPaused: true);
            float L = PitchDims.HalfLength;
            PlayerController gk = Find(1, Role.GK); // AWAY keeper guards +X
            Assert.IsNotNull(gk.Keeper);
            int before = mm.HomeScore;
            for (int k = 0; k < 3; k++)
            {
                // Keeper at the far post, shot into the other top corner from close range: no keeper can get there.
                Place(gk, new Vector3(L - 0.6f, 0f, -1.3f), Vector3.left);
                foreach (PlayerController p in mm.AllPlayersList) if (p != gk) Place(p, new Vector3(-10f, 0f, p.transform.position.z), Vector3.right);
                mm.MatchBall.ResetBall(new Vector3(L - 7f, PitchDims.BallRadius, 0.5f));
                yield return new WaitForFixedUpdate();
                Vector3 target = new Vector3(L + 0.3f, 1.6f, 1.15f);
                Vector3 from = mm.MatchBall.Body.position;
                float speed = 28f;
                float tFlight = Flat(target - from).magnitude / speed;
                Vector3 v = Flat(target - from).normalized * speed;
                v.y = (target.y - from.y) / tFlight + 0.5f * 9.81f * tFlight;
                mm.MatchBall.Kick(v, null);
                for (float t = 0f; t < 1.2f; t += Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
                string r = gk.Keeper.LastResult;
                Debug.Log($"[QA8] attempt {k}: keeper result '{r}', plan reachable {gk.Keeper.LastPlanReachable}");
                Assert.IsFalse(r == "CATCH" || r == "PARRY" || r == "PUNCH" || r == "FUMBLE", "keeper saved a ball he could not reach");
                yield return new WaitForSeconds(3.2f); // goal restart
            }
            Assert.Greater(mm.HomeScore, before, "the unreachable shots should go in");
        }

        // ---------------------------------------------------------------- QA 9: no clustering on the ball

        [UnityTest]
        public IEnumerator AI_DoesNotClusterOnBall()
        {
            yield return StartMatch(autoPlay: true);
            Time.timeScale = 3f;
            int samples = 0, crowded = 0;
            float sumNear = 0f;
            int maxNear = 0;
            for (float t = 0f; t < 30f; t += 0.25f)
            {
                yield return new WaitForSeconds(0.25f);
                if (mm.ResetPending) continue;
                Vector3 b = mm.MatchBall.Body.position;
                for (int team = 0; team < 2; team++)
                {
                    int near = 0;
                    foreach (PlayerController p in mm.TeamList(team))
                        if (p.role != Role.GK && Flat(p.transform.position - b).magnitude < 3f) near++;
                    samples++;
                    sumNear += near;
                    maxNear = Mathf.Max(maxNear, near);
                    if (near >= 3) crowded++;
                }
            }
            Time.timeScale = 1f;
            float avg = sumNear / Mathf.Max(1, samples);
            Debug.Log($"[QA9] same-team outfield players within 3 m of the ball: avg {avg:0.00}, max {maxNear}, samples with 3+ {crowded}/{samples}");
            Assert.Less(avg, 1.6f, "too many players around the ball on average");
            Assert.Less(crowded / (float)Mathf.Max(1, samples), 0.08f, "three or more team-mates bunched on the ball too often");
        }

        // ---------------------------------------------------------------- QA 11 + 15: halves, full time, a whole match

        [UnityTest]
        public IEnumerator FullMatch_RunsToFullTime()
        {
            yield return StartMatch(halfSeconds: 15f, autoPlay: true);
            Time.timeScale = 4f;
            bool sawSecondHalf = false;
            float realStart = Time.realtimeSinceStartup;
            while (!mm.FullTime && Time.realtimeSinceStartup - realStart < 120f)
            {
                yield return null;
                if (mm.Half == 2) sawSecondHalf = true;
                foreach (PlayerController p in mm.AllPlayersList)
                {
                    Vector3 pos = p.transform.position;
                    Assert.IsFalse(float.IsNaN(pos.x) || float.IsNaN(pos.z), "NaN player position");
                    Assert.Less(Mathf.Abs(pos.x), PitchDims.HalfLength + 3f, "player left the pitch area");
                }
                Assert.IsFalse(float.IsNaN(mm.MatchBall.Body.position.x), "NaN ball position");
            }
            Time.timeScale = 1f;
            Debug.Log($"[QA15] full time {mm.HomeScore}-{mm.AwayScore}, clock {mm.Clock:0}s");
            Assert.IsTrue(sawSecondHalf, "second half never started");
            Assert.IsTrue(mm.FullTime, "match did not reach full time");
            Assert.AreEqual(30f, mm.Clock, 0.5f, "clock stops at 2 x half length");
        }

        // ---------------------------------------------------------------- QA 12: R restarts cleanly

        [UnityTest]
        public IEnumerator Reset_RestartsFromKickoff()
        {
            yield return StartMatch(autoPlay: true);
            yield return new WaitForSeconds(3f);
            mm.ResetKickoff();
            yield return new WaitForFixedUpdate();
            Assert.Less(Flat(mm.MatchBall.Body.position).magnitude, 0.3f, "ball back on the centre spot");
            foreach (PlayerController p in mm.AllPlayersList)
            {
                Assert.LessOrEqual(p.transform.position.x * p.AttackSign, 0.5f, $"{p.name} not in his own half");
                Assert.IsFalse(p.Kick.Active || p.Tackle.Active, "actions carried over the restart");
            }
            Assert.IsNotNull(mm.Human, "a player is under control after the restart");
        }

        // ---------------------------------------------------------------- QA 13: switching player

        [UnityTest]
        public IEnumerator Switch_GoesToNearestTeamMate()
        {
            yield return StartMatch(aiPaused: true);
            PlayerController df = Find(0, Role.DF);
            mm.MatchBall.ResetBall(df.transform.position + Vector3.right * 1.5f + Vector3.up * PitchDims.BallRadius);
            yield return new WaitForFixedUpdate();
            mm.SwitchToNearest();
            Assert.AreEqual(df, mm.Human, "control goes to the team-mate nearest the ball");
            Assert.IsTrue(df.IsHuman);
            int humans = 0;
            foreach (PlayerController p in mm.AllPlayersList) if (p.IsHuman) humans++;
            Assert.AreEqual(1, humans, "exactly one controlled player");
        }

        // ---------------------------------------------------------------- QA 14: UI keeps working

        [UnityTest]
        public IEnumerator UI_DrawsWithoutErrors()
        {
            yield return StartMatch(autoPlay: true);
            mm.ShowAIDebug = true;
            mm.ShowPerf = true;
            for (int i = 0; i < 120; i++) yield return null;
            mm.ShowAIDebug = false;
            mm.ShowPerf = false;
            for (int i = 0; i < 30; i++) yield return null;
            // Any exception in OnGUI is logged as an error and fails this test automatically.
            LogAssert.NoUnexpectedReceived();
            Assert.IsNotNull(Camera.main, "camera exists");
        }
    }
}
