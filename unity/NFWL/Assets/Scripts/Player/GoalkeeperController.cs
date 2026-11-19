using UnityEngine;

namespace NFWL
{
    public enum SaveType
    {
        None, StandingCatch, LowCatch, HighCatch, DiveLeft, DiveRight, LowDive, HighDive, Parry, Punch, Recovery
    }

    /// <summary>
    /// PHASE 6: goalkeeper positioning and reach-based saves.
    /// Positioning: on the line that halves the angle between the ball and the two posts, further off
    /// the line when the ball is close, shifting toward where a moving ball is going, rushing out at
    /// an attacker who is through on goal.
    /// Saves: after a reaction time (Goalkeeping), the keeper predicts where the shot crosses his
    /// position, works out whether he can get a hand there in time (shuffle + dive speed and length,
    /// arm reach, height) and picks a save. The ball is only stopped if the hands actually get to it
    /// at that moment: a ball out of reach is never saved by chance. When reachable, Goalkeeping,
    /// shot pace and how stretched he is decide catch or parry (and the rare fumble).
    /// </summary>
    public class GoalkeeperController
    {
        readonly PlayerController p;

        public SaveType Current { get; private set; } = SaveType.None;
        public SaveType LastSave { get; private set; } = SaveType.None;
        /// <summary>"", "CATCH", "PARRY", "PUNCH", "BEATEN", "OUT OF REACH".</summary>
        public string LastResult { get; private set; } = "";
        public float LastResultTime { get; private set; } = -10f;
        /// <summary>Planned save was judged reachable (for tests / HUD).</summary>
        public bool LastPlanReachable { get; private set; }

        float lastShotTouchTime = -100f;
        bool shotActive;
        float decideAt;
        bool planned, resolved;
        float diveStart, contactTime, diveEnd, recoverEnd;
        Vector3 contactPoint;
        Vector3 diveVelocity;
        bool reachable, catchIt;
        float side = 1f;

        const float ArmReach = 0.75f;     // hand from body centre, standing
        const float DiveDelay = 0.06f;    // load the legs before take-off

        public GoalkeeperController(PlayerController owner) { p = owner; }

        float GoalX => -p.AttackSign * PitchDims.HalfLength;
        public float ReactionTime => Rules.AttrLerp(p.attributes.Goalkeeping, 0.32f, 0.16f) - Skills.ReactionBonus(p);
        public float DiveSpeed => Rules.AttrLerp(p.attributes.Goalkeeping, 5f, 7f);
        public float MaxDive => Rules.AttrLerp(p.attributes.Goalkeeping, 1.9f, 2.7f);

        /// <summary>True while diving or getting up: the keeper cannot run.</summary>
        public bool Busy(float now) => now < recoverEnd;

        /// <summary>During the dive the keeper's body is launched; returns the launch velocity.</summary>
        public bool OverrideMove(float now, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!planned || now < diveStart || now > recoverEnd) return now < recoverEnd && planned;
            velocity = now < diveEnd ? diveVelocity : Vector3.zero;
            return true;
        }

        /// <summary>Lateral reach (m from body centre) after t seconds: shuffle/dive + arm.</summary>
        public float ReachAfter(float t) => ArmReach + Mathf.Min(MaxDive, DiveSpeed * Mathf.Max(0f, t - DiveDelay));

        /// <summary>Highest point the hands get to at a lateral distance (m).</summary>
        public static float MaxHeightAt(float lateral) => 2.45f - 0.25f * Mathf.Max(0f, lateral - ArmReach);

        // ------------------------------------------------------------------ positioning

        /// <summary>Where the keeper should stand (6.1).</summary>
        public Vector3 PositionTarget(Ball ball)
        {
            float s = p.AttackSign;
            float goalX = GoalX;
            Vector3 B = ball.Body.position;
            // Anticipate a moving loose ball (a pass or cross).
            if (ball.Owner == null && ball.Velocity.sqrMagnitude > 9f) B = Vector3.Lerp(B, ball.PredictPosition(0.4f), 0.6f);
            B.y = 0f;

            Vector3 p1 = new Vector3(goalX, 0f, -PitchDims.GoalHalfWidth);
            Vector3 p2 = new Vector3(goalX, 0f, PitchDims.GoalHalfWidth);
            Vector3 bis = (p1 - B).normalized + (p2 - B).normalized;
            float ballDist = Mathf.Abs(B.x - goalX);
            float off = Mathf.Lerp(2.2f, 0.8f, Mathf.InverseLerp(4f, 22f, ballDist)) * Rules.AttrLerp(p.attributes.Goalkeeping, 0.9f, 1.1f);
            float x = goalX + s * off;
            float z = 0f;
            if (Mathf.Abs(bis.x) > 0.05f)
            {
                float k = (x - B.x) / bis.x;
                z = B.z + bis.z * k;
            }
            z = Mathf.Clamp(z, -PitchDims.GoalHalfWidth - 0.3f, PitchDims.GoalHalfWidth + 0.3f);

            // 1 v 1: an attacker through on goal with nobody between - come out and narrow the angle.
            PlayerController carrier = ball.Owner;
            if (carrier != null && carrier.teamIndex != p.teamIndex && ballDist < 10f && p.Match != null)
            {
                Vector3 toGoal = new Vector3(goalX, 0f, 0f) - B;
                bool covered = false;
                foreach (PlayerController d in p.Match.TeamList(p.teamIndex))
                {
                    if (d == p) continue;
                    Vector3 rel = d.transform.position - B;
                    rel.y = 0f;
                    if (Vector3.Dot(rel, toGoal.normalized) > 0f && rel.magnitude < toGoal.magnitude && Vector3.Angle(rel, toGoal) < 25f) { covered = true; break; }
                }
                if (!covered)
                {
                    Vector3 rush = B + toGoal.normalized * Mathf.Lerp(1.2f, 3f, Mathf.InverseLerp(3f, 10f, ballDist));
                    x = rush.x;
                    z = rush.z;
                }
            }
            return new Vector3(x, 0f, z);
        }

        // ------------------------------------------------------------------ saves

        public void FixedTick(Ball ball, float now)
        {
            if (Current != SaveType.None && now > recoverEnd)
            {
                Current = SaveType.None;
                planned = false;
            }

            // A new strike toward our goal (a shot, or a deflection that changes it).
            if (ball.Owner == null && !ball.HeldInHands && ball.LastTouchTime != lastShotTouchTime && ball.LastTouchedBy != p)
            {
                Vector3 v = ball.Velocity;
                bool towardUs = Mathf.Sign(v.x) == Mathf.Sign(GoalX) && Mathf.Abs(v.x) > 4f && v.magnitude > 6f;
                if (towardUs)
                {
                    lastShotTouchTime = ball.LastTouchTime;
                    shotActive = true;
                    decideAt = ball.LastTouchTime + ReactionTime;
                    if (!Busy(now)) planned = false;
                }
            }
            if (ball.Owner != null || ball.HeldInHands) shotActive = false;

            if (shotActive && !planned && now >= decideAt && !Busy(now)) Plan(ball, now);

            if (planned && !resolved && now >= contactTime)
            {
                resolved = true;
                Resolve(ball, now);
            }
        }

        void Plan(Ball ball, float now)
        {
            shotActive = false;
            Vector3 G = p.transform.position;
            float planeX = G.x;
            // Fly the shot forward until it reaches the keeper's depth (or the goal line).
            Vector3 bp = ball.Body.position, bv = ball.Velocity, w = ball.Spin;
            float t = 0f;
            bool crossed = false;
            float goalX = GoalX;
            while (t < 1.6f)
            {
                Vector3 next = Ball.Simulate(bp, bv, w, 0.02f, out Vector3 nv);
                t += 0.02f;
                bool pastPlane = (next.x - planeX) * Mathf.Sign(goalX) >= 0f;
                if (pastPlane)
                {
                    // Interpolate to the plane.
                    float f = Mathf.Abs(next.x - bp.x) > 1e-4f ? Mathf.Clamp01((planeX - bp.x) / (next.x - bp.x)) : 1f;
                    bp = Vector3.Lerp(bp, next, f);
                    crossed = true;
                    break;
                }
                bp = next;
                bv = nv;
            }
            if (!crossed) return;

            // Is it on target at all? Check where it crosses the goal line.
            Vector3 atLine = bp;
            if (Mathf.Abs(planeX - goalX) > 0.05f && Mathf.Abs(bv.x) > 0.1f)
            {
                float tl = (goalX - bp.x) / bv.x;
                if (tl > 0f) atLine = bp + bv * tl + 0.5f * Physics.gravity * tl * tl;
            }
            bool onTarget = Mathf.Abs(atLine.z) < PitchDims.GoalHalfWidth + 0.25f && atLine.y < PitchDims.GoalHeight + 0.3f;
            if (!onTarget) return;

            contactPoint = bp;
            float T = t;
            float lateral = contactPoint.z - G.z;
            float D = Mathf.Abs(lateral);
            float H = Mathf.Max(0f, contactPoint.y);
            side = lateral >= 0f ? 1f : -1f;
            reachable = D <= ReachAfter(T) && H <= MaxHeightAt(D);
            LastPlanReachable = reachable;

            // Save type from where the ball arrives relative to the keeper.
            SaveType type;
            bool dive = D > ArmReach * 0.8f;
            if (!dive)
            {
                if (H > 1.75f) type = SaveType.HighCatch;
                else if (H < 0.45f) type = SaveType.LowCatch;
                else type = SaveType.StandingCatch;
            }
            else if (H > 1.4f) type = SaveType.HighDive;
            else if (H < 0.55f) type = SaveType.LowDive;
            else type = side > 0f ? SaveType.DiveLeft : SaveType.DiveRight;

            // Catch or parry: fast shots and full stretch are pushed away instead of held.
            float speed = ball.Velocity.magnitude;
            float stretch = Mathf.Clamp01((D - ArmReach) / Mathf.Max(0.1f, MaxDive));
            float catchChance = Rules.AttrLerp(p.attributes.Goalkeeping, 0.5f, 0.92f)
                                * Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(14f, 30f, speed))
                                * Mathf.Lerp(1f, 0.5f, stretch);
            catchIt = Random.value < catchChance;
            if (!catchIt && dive) type = H > 1.6f ? SaveType.Punch : type;

            // Launch: move the body so the hands meet the ball, as fast as the dive allows.
            float need = Mathf.Max(0f, D - ArmReach);
            float flight = Mathf.Max(0.08f, T - DiveDelay);
            float lateralSpeed = Mathf.Min(DiveSpeed, need / flight);
            diveVelocity = new Vector3(0f, 0f, side * lateralSpeed);
            diveStart = now + DiveDelay;
            contactTime = now + T;
            diveEnd = contactTime + 0.05f;
            recoverEnd = contactTime + (dive ? 0.95f : 0.35f);
            planned = true;
            resolved = false;
            Current = type;
            LastSave = type;
            if (p.Model != null) p.Model.PlaySave(type, side, H, now, contactTime, recoverEnd);
        }

        void Resolve(Ball ball, float now)
        {
            // The shot was touched again (blocked, deflected, trapped) after we planned: nothing to save.
            if (ball.Owner != null || ball.HeldInHands || ball.LastTouchTime != lastShotTouchTime) return;
            Vector3 B = ball.Body.position;
            Vector3 G = p.transform.position;
            // Hands: where the body got to plus arm reach toward the ball, at the ball's height.
            Vector3 lateralDir = new Vector3(0f, 0f, side);
            float bodyToBall = Mathf.Abs(B.z - G.z);
            float handReach = ArmReach + (Current == SaveType.HighDive || Current == SaveType.LowDive || Current == SaveType.DiveLeft || Current == SaveType.DiveRight || Current == SaveType.Parry || Current == SaveType.Punch ? 0.35f : 0f);
            bool depthOk = Mathf.Abs(B.x - G.x) < 0.7f;
            bool heightOk = B.y <= MaxHeightAt(bodyToBall) + 0.1f;
            bool hands = reachable && depthOk && heightOk && bodyToBall <= handReach + 0.15f;
            if (!hands)
            {
                Result(reachable ? "BEATEN" : "OUT OF REACH", now);
                return;
            }

            // Rare fumble even when the hands get there.
            float clean = Rules.AttrLerp(p.attributes.Goalkeeping, 0.86f, 0.98f);
            if (Random.value > clean)
            {
                Vector3 spill = ball.Velocity * 0.25f + Vector3.up * 1.5f;
                ball.Kick(spill, p);
                Result("FUMBLE", now);
                return;
            }

            if (catchIt && Current != SaveType.Punch)
            {
                ball.Velocity = Vector3.zero;
                ball.Body.angularVelocity = Vector3.zero;
                p.GainBall();
                Result("CATCH", now);
            }
            else
            {
                // Parry / punch: away from goal and wide of the posts.
                Vector3 v = ball.Velocity;
                float outward = Current == SaveType.Punch ? 9f : Mathf.Abs(v.x) * 0.3f + 2f;
                Vector3 parry = new Vector3(-Mathf.Sign(GoalX) * outward, Current == SaveType.Punch ? 5f : Random.Range(1.5f, 4f),
                    lateralDir.z * Mathf.Max(3f, Mathf.Abs(v.magnitude) * 0.35f));
                ball.Kick(parry, p);
                Result(Current == SaveType.Punch ? "PUNCH" : "PARRY", now);
            }
            if (p.Match != null) p.Match.OnSave(p, Current, LastResult);
        }

        void Result(string r, float now)
        {
            LastResult = r;
            LastResultTime = now;
        }

        public void Cancel()
        {
            planned = false;
            shotActive = false;
            Current = SaveType.None;
            recoverEnd = 0f;
        }
    }
}
