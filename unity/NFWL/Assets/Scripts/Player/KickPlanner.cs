using UnityEngine;

namespace NFWL
{
    /// <summary>Modifier on a kick key (configurable in NFWLTuning.input).</summary>
    public enum KickVariant { Normal, Power, Finesse, LowDriven, Through, LoftedThrough }

    /// <summary>Everything decided when the kick starts; the ball result is computed at contact.</summary>
    public struct KickRequest
    {
        public KickType type;
        public KickVariant variant;
        public float power;
        public bool tap;
        public Vector3? aim;
        public Vector3 inputDir;
        public float side;
        public bool firstTime;
        public bool header;
    }

    public struct KickResult
    {
        public Vector3 velocity;
        public Vector3 spin;
        public string label;
        public PlayerController receiver;
        public Vector3 receivePoint;
        public float receiveTime;
    }

    /// <summary>
    /// PHASE 3.2 / 3.4 / 4.2 / 4.3: turns a kick request into ball velocity and spin, solved with the
    /// ball model so passes arrive at the right pace and shots reach the aimed height.
    /// Short passes are side-foot ground passes led to the receiver; through passes go into the space
    /// ahead of a running team-mate, timed to his sprint speed; long passes and lobs are lofted with
    /// backspin onto the receiver; shots aim at a point in the goal and come in normal, power,
    /// finesse (inside-foot curl), low driven (topspin), outside-foot, first-time, volley,
    /// half-volley, weak-foot and header forms. Abilities set pace (ShotPower) and accuracy
    /// (Finishing, ShortPass, LongPass); the weak foot, sprinting, balance, pressure and awkward
    /// contacts add error.
    /// </summary>
    public static class KickPlanner
    {
        public static KickResult Plan(PlayerController p, Ball ball, KickRequest r)
        {
            Vector3 from = ball.Body.position;
            if (from.y < PitchDims.BallRadius) from.y = PitchDims.BallRadius;
            Vector3 dir = r.aim ?? r.inputDir;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : p.Locomotion.Facing;

            if (r.header) return Header(p, ball, r, from, dir);
            switch (r.type)
            {
                case KickType.ShortPass: return r.variant == KickVariant.Through ? ThroughPass(p, r, from, dir, false) : ShortPass(p, r, from, dir);
                case KickType.LongPass: return r.variant == KickVariant.LoftedThrough ? ThroughPass(p, r, from, dir, true) : LongPass(p, r, from, dir);
                default: return Shot(p, ball, r, from, dir);
            }
        }

        // ------------------------------------------------------------------ passes

        static KickResult ShortPass(PlayerController p, KickRequest r, Vector3 from, Vector3 dir)
        {
            float power = r.tap ? 0.45f : r.power;
            PlayerController mate = p.FindPassTarget(dir, 35f, 24f);
            KickResult res = new KickResult { label = "PASS" };
            float speed;
            if (mate != null)
            {
                // Lead the receiver: iterate arrival time and lead point together.
                float arrival = Mathf.Lerp(6.5f, 11f, power);
                Vector3 target = mate.transform.position;
                float t = 0.5f;
                for (int i = 0; i < 4; i++)
                {
                    target = mate.transform.position + mate.Velocity * t;
                    float d = Flat(target - from).magnitude;
                    speed = BallSolver.GroundPassSpeed(d, arrival);
                    t = BallSolver.GroundTravelTime(speed, d);
                }
                Vector3 to = Flat(target - from);
                dir = to.normalized;
                speed = BallSolver.GroundPassSpeed(to.magnitude, arrival);
                res.receiver = mate;
                res.receivePoint = target;
                res.receiveTime = t;
            }
            else speed = Mathf.Lerp(7f, 21f, power);

            float err = Rules.AttrLerp(p.attributes.ShortPass, 6f, 1f) * Common(p, r);
            float speedErr = Rules.AttrLerp(p.attributes.ShortPass, 0.1f, 0.03f);
            dir = Yaw(dir, Random.Range(-err, err));
            speed *= 1f + Random.Range(-speedErr, speedErr);
            res.velocity = dir * speed;
            // Side-foot: the ball rolls true with a little natural spin.
            res.spin = Vector3.Cross(Vector3.up, dir) * (speed / PitchDims.BallRadius) * 0.3f;
            return res;
        }

        static KickResult LongPass(PlayerController p, KickRequest r, Vector3 from, Vector3 dir)
        {
            float power = r.tap ? 0.5f : r.power;
            PlayerController mate = p.FindPassTarget(dir, 30f, 42f);
            float elevation = Mathf.Lerp(26f, 40f, power);
            Vector3 backspin = -Vector3.Cross(Vector3.up, dir) * Mathf.Lerp(14f, 24f, power);
            KickResult res = new KickResult { label = "LONG PASS" };
            float speed;
            if (mate != null)
            {
                Vector3 target = mate.transform.position;
                float t = 1.2f;
                for (int i = 0; i < 3; i++)
                {
                    target = mate.transform.position + mate.Velocity * t;
                    Vector3 to = Flat(target - from);
                    backspin = -Vector3.Cross(Vector3.up, to.normalized) * Mathf.Lerp(14f, 24f, power);
                    speed = BallSolver.LoftedSpeed(from, to.normalized, elevation, backspin, to.magnitude, out t);
                }
                Vector3 final = Flat(target - from);
                dir = final.normalized;
                speed = BallSolver.LoftedSpeed(from, dir, elevation, backspin, final.magnitude, out t);
                res.receiver = mate;
                res.receivePoint = target;
                res.receiveTime = t;
            }
            else speed = Mathf.Lerp(14f, 27f, power);

            float err = Rules.AttrLerp(p.attributes.LongPass, 8f, 2f) * Common(p, r);
            dir = Yaw(dir, Random.Range(-err, err));
            elevation += Random.Range(-err, err) * 0.5f;
            speed *= 1f + Random.Range(-1f, 1f) * Rules.AttrLerp(p.attributes.LongPass, 0.1f, 0.03f);
            res.velocity = BallSolver.Launch(dir, speed, elevation);
            res.spin = backspin;
            return res;
        }

        /// <summary>Into the space ahead of a running team-mate, timed to his sprint (ground or lofted).</summary>
        static KickResult ThroughPass(PlayerController p, KickRequest r, Vector3 from, Vector3 dir, bool lofted)
        {
            float power = r.tap ? 0.5f : r.power;
            PlayerController mate = p.FindPassTarget(dir, 40f, lofted ? 40f : 30f);
            KickResult res = new KickResult { label = lofted ? "LOFTED THROUGH" : "THROUGH PASS" };
            if (mate == null)
            {
                // No runner: play it into the space in front.
                res = lofted ? LongPass(p, r, from, dir) : ShortPass(p, r, from, dir);
                res.label = lofted ? "LOFTED THROUGH" : "THROUGH PASS";
                return res;
            }

            // The runner goes toward goal (or where he is running already).
            Vector3 attack = new Vector3(p.AttackSign, 0f, 0f);
            Vector3 mv = Flat(mate.Velocity);
            Vector3 runDir = (mv.magnitude > 1.5f ? Vector3.Lerp(mv.normalized, attack, 0.4f) : Vector3.Lerp(dir, attack, 0.6f)).normalized;
            // How far the runner gets in t seconds, accelerating from his current speed along the run
            // (v0.6 Real Football Engine: the same movement model the runner really uses).
            float runV0 = Mathf.Max(0f, Vector3.Dot(mv, runDir));
            float extra = Mathf.Lerp(1f, 4f, power);
            float elevation = lofted ? 34f : 0f;
            Vector3 backspin = Vector3.zero;
            Vector3 target = mate.transform.position;
            float t = 0.8f, speed = 10f;
            for (int i = 0; i < 4; i++)
            {
                target = mate.transform.position + runDir * (mate.Locomotion.DistanceInTime(t, runV0, true, false) + extra);
                target.x = Mathf.Clamp(target.x, -PitchDims.HalfLength + 1.5f, PitchDims.HalfLength - 1.5f);
                target.z = Mathf.Clamp(target.z, -PitchDims.HalfWidth + 1f, PitchDims.HalfWidth - 1f);
                Vector3 to = Flat(target - from);
                if (lofted)
                {
                    backspin = -Vector3.Cross(Vector3.up, to.normalized) * 18f;
                    speed = BallSolver.LoftedSpeed(from, to.normalized, elevation, backspin, to.magnitude, out t);
                }
                else
                {
                    speed = BallSolver.GroundPassSpeed(to.magnitude, 4.5f);
                    t = BallSolver.GroundTravelTime(speed, to.magnitude);
                }
            }
            dir = Flat(target - from).normalized;
            float err = Rules.AttrLerp(lofted ? p.attributes.LongPass : p.attributes.ShortPass, 7f, 1.5f) * Common(p, r);
            dir = Yaw(dir, Random.Range(-err, err));
            speed *= 1f + Random.Range(-0.06f, 0.06f);
            res.velocity = BallSolver.Launch(dir, speed, elevation);
            res.spin = lofted ? backspin : Vector3.Cross(Vector3.up, dir) * (speed / PitchDims.BallRadius) * 0.3f;
            res.receiver = mate;
            res.receivePoint = target;
            res.receiveTime = t;
            // Tell the runner where the ball will be.
            mate.CallRun(target, Time.time + t + 0.6f);
            return res;
        }

        // ------------------------------------------------------------------ shots

        static KickResult Shot(PlayerController p, Ball ball, KickRequest r, Vector3 from, Vector3 dir)
        {
            PlayerAttributes a = p.attributes;
            float power = r.tap ? 0.3f : r.power;
            Vector3 goal = p.AttackGoalCenter;
            float goalX = goal.x;

            // Aim: where the aim line crosses the goal line, kept inside the posts.
            float zAim = 0f;
            float dx = goalX - from.x;
            if (Mathf.Abs(dir.x) > 0.2f && Mathf.Sign(dir.x) == Mathf.Sign(dx))
                zAim = from.z + dir.z / dir.x * dx;
            else zAim = Mathf.Sign(from.z) * -0.8f; // aim line away from goal: far side
            float postMargin = 0.3f;
            zAim = Mathf.Clamp(zAim, -PitchDims.GoalHalfWidth + postMargin, PitchDims.GoalHalfWidth - postMargin);

            float shotPower = Rules.AttrLerp(a.ShotPower, 0.85f, 1.05f);
            float speed, height, errMul;
            Vector3 spin;
            string label;
            Vector3 flatDir = Flat(new Vector3(goalX, 0f, zAim) - from).normalized;
            Vector3 roll = Vector3.Cross(Vector3.up, flatDir); // topspin axis
            float footSide = r.side;
            bool weakFoot = p.WeakFootFactor(footSide) > 1.01f;

            // Outside of the foot: shooting across to the kicking foot's own side.
            Vector3 facing = p.Locomotion.Facing;
            Vector3 right = new Vector3(facing.z, 0f, -facing.x);
            bool outside = r.variant == KickVariant.Normal && Vector3.Dot(flatDir, right) * footSide > 0.5f && !weakFoot;

            switch (r.variant)
            {
                case KickVariant.Power:
                    speed = Mathf.Lerp(22f, 34f, power) * shotPower;
                    height = Mathf.Lerp(0.9f, 1.8f, power);
                    spin = Random.insideUnitSphere * 3f; // almost no spin: it wobbles
                    errMul = 1.3f + 0.7f * power;
                    label = "POWER SHOT";
                    break;
                case KickVariant.Finesse:
                    // Inside-foot curl toward the far post.
                    speed = Mathf.Lerp(18f, 25f, power) * shotPower;
                    height = 1.15f;
                    spin = Vector3.up * (-footSide * Rules.AttrLerp(a.Finishing, 35f, 60f) * (1f - 0.3f * power));
                    errMul = 0.6f;
                    label = "FINESSE SHOT";
                    break;
                case KickVariant.LowDriven:
                    speed = Mathf.Lerp(18f, 27f, power) * shotPower;
                    height = 0.22f;
                    spin = roll * Mathf.Lerp(20f, 35f, power);
                    errMul = 0.8f;
                    label = "LOW DRIVEN";
                    break;
                default:
                    speed = Mathf.Lerp(16f, 28f, power) * shotPower;
                    height = Mathf.Lerp(0.45f, 1.5f, power);
                    spin = Vector3.up * Random.Range(-10f, 10f) + roll * 8f;
                    errMul = 0.5f + 0.6f * power;
                    label = "SHOT";
                    if (outside)
                    {
                        spin = Vector3.up * (footSide * 30f);
                        errMul *= 1.2f;
                        label = "OUTSIDE FOOT";
                    }
                    break;
            }

            // Contact quality: first time, volley, half volley.
            float ballH = ball.Body.position.y - PitchDims.BallRadius;
            if (ballH > 0.35f) { errMul *= 1.5f; label = "VOLLEY"; height *= 0.8f; }
            else if (ball.Velocity.y > 0.6f && ballH > 0.05f) { errMul *= 1.25f; label = "HALF VOLLEY"; }
            else if (r.firstTime)
            {
                Vector3 incoming = Flat(ball.Velocity - p.Velocity);
                errMul *= 1f + Mathf.Clamp01(incoming.magnitude / 20f);
                label = "FIRST TIME " + label;
            }
            if (weakFoot) label = "WEAK FOOT " + label;

            Vector3 target = new Vector3(goalX, height, zAim);
            // Solve height and curl together: the elevation changes the flight time, which changes the bend.
            float dist = Flat(target - from).magnitude;
            Vector3 aimDir = Flat(target - from).normalized;
            float elevation = BallSolver.ElevationForHeight(from, aimDir, speed, spin, dist, height);
            for (int i = 0; i < 2; i++)
            {
                aimDir = BallSolver.CompensateCurl(from, target, speed, elevation, spin);
                elevation = BallSolver.ElevationForHeight(from, aimDir, speed, spin, dist, height);
            }

            float err = Rules.AttrLerp(a.Finishing, 6f, 1.2f) * errMul * Common(p, r);
            aimDir = Yaw(aimDir, Random.Range(-err, err));
            elevation += Random.Range(-err, err) * 0.5f + (r.variant == KickVariant.Power ? Random.Range(0f, err * 0.4f) : 0f);

            return new KickResult { velocity = BallSolver.Launch(aimDir, speed, elevation), spin = spin, label = label };
        }

        static KickResult Header(PlayerController p, Ball ball, KickRequest r, Vector3 from, Vector3 dir)
        {
            PlayerAttributes a = p.attributes;
            bool shot = r.type == KickType.Shoot;
            float speed = Mathf.Lerp(7f, shot ? 17f : 14f, r.tap ? 0.5f : r.power);
            float elevation;
            if (shot)
            {
                // Head it down toward the goal.
                Vector3 goal = p.AttackGoalCenter;
                float z = Mathf.Clamp(from.z + (Mathf.Abs(dir.x) > 0.2f ? dir.z / dir.x * (goal.x - from.x) : 0f), -1.2f, 1.2f);
                Vector3 target = new Vector3(goal.x, 0.6f, z);
                dir = Flat(target - from).normalized;
                elevation = BallSolver.ElevationForHeight(from, dir, speed, Vector3.zero, Flat(target - from).magnitude, 0.6f);
            }
            else
            {
                PlayerController mate = p.FindPassTarget(dir, 40f, 20f);
                if (mate != null) dir = Flat(mate.transform.position - from).normalized;
                elevation = 12f;
            }
            float err = Rules.AttrLerp(shot ? a.Finishing : a.ShortPass, 9f, 3f) * Common(p, r);
            dir = Yaw(dir, Random.Range(-err, err));
            elevation += Random.Range(-err, err) * 0.5f;
            return new KickResult { velocity = BallSolver.Launch(dir, speed, elevation), spin = Vector3.zero, label = shot ? "HEADER" : "HEADED PASS" };
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Error multiplier shared by all kicks: sprint, weak foot, balance, pressure.</summary>
        static float Common(PlayerController p, KickRequest r)
        {
            float m = 1f;
            if (p.Sprinting) m *= 1.25f;
            m *= p.WeakFootFactor(r.side);
            m *= r.type == KickType.Shoot ? Skills.ShotError(p) : Skills.PassError(p);
            m *= 1f + (1f - p.Locomotion.Balance);
            if (p.Match != null)
            {
                p.Match.NearestOpponent(p, out float d);
                if (d < 1.5f) m *= 1.2f;
            }
            return m;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        static Vector3 Yaw(Vector3 v, float deg) => Quaternion.AngleAxis(deg, Vector3.up) * v;
    }
}
