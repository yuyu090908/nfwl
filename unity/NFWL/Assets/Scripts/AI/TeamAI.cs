using System.Collections.Generic;
using UnityEngine;

namespace NFWL
{
    /// <summary>What an off-ball (or on-ball) player is doing for the team right now.</summary>
    public enum AIRole
    {
        None,
        Carrier,      // has the ball (CarrierBrain decides)
        Keeper,       // goalkeeper (GoalkeeperController positions him)
        Intercept,    // loose moving ball: get to where it will be first
        Press,        // first defender: close the carrier down, goal-side
        Cover,        // second defender: behind the presser, on the shot line
        Mark,         // goal-side of an opponent, leaning into the passing lane
        Support,      // attacking: offer a short, open angle to the carrier
        RunBehind,    // attacking: run into the space behind the last defender
        Width,        // attacking: stretch the play on the far side
        RestDefence,  // attacking: stay back against the counter
    }

    /// <summary>
    /// PHASE 8: team-level Utility AI. Several times a second each team works out its phase
    /// (attack / defend / loose ball) and gives every AI outfield player one role with a target
    /// spot. Assignments use hysteresis (a role is kept unless another player is clearly better
    /// suited, and only after a minimum hold time) so players do not flicker between jobs, and
    /// the targets are spread out so the team never clusters on the ball: only the presser and
    /// the interceptor go to the ball, everyone else holds a position with a purpose.
    /// </summary>
    public class TeamAI
    {
        public class Slot
        {
            public AIRole role = AIRole.None;
            public Vector3 target;
            public bool sprint;
            public PlayerController mark;
            public float since;
            public float score;
        }

        readonly MatchManager match;
        readonly int team;
        readonly List<PlayerController> mates;
        readonly List<PlayerController> opps;
        readonly Dictionary<PlayerController, Slot> slots = new Dictionary<PlayerController, Slot>();
        readonly List<PlayerController> free = new List<PlayerController>(8);
        readonly List<PlayerController> toMark = new List<PlayerController>(8);
        readonly Vector3[] path = new Vector3[PathSteps + 1];

        const float TickInterval = 0.15f;
        const float MinHold = 0.6f;
        const int PathSteps = 20;          // ball path every 0.1 s for 2 s
        const float PathDt = 0.1f;

        float nextTick;
        public PlayerController Presser { get; private set; }
        public PlayerController Interceptor { get; private set; }
        public Vector3 InterceptPoint { get; private set; }
        /// <summary>True while the team is in its attacking shape.</summary>
        public bool Attacking { get; private set; }

        public TeamAI(MatchManager m, int teamIndex, List<PlayerController> own, List<PlayerController> other)
        {
            match = m;
            team = teamIndex;
            nextTick = teamIndex * TickInterval * 0.5f; // the two teams think on different frames
            mates = own;
            opps = other;
        }

        float S => team == 0 ? 1f : -1f;
        Vector3 OwnGoal => new Vector3(-S * PitchDims.HalfLength, 0f, 0f);
        Vector3 AttackGoal => new Vector3(S * PitchDims.HalfLength, 0f, 0f);

        public Slot SlotOf(PlayerController p)
        {
            if (!slots.TryGetValue(p, out Slot s)) { s = new Slot(); slots[p] = s; }
            return s;
        }

        public AIRole RoleOf(PlayerController p) => slots.TryGetValue(p, out Slot s) ? s.role : AIRole.None;

        public void Reset()
        {
            foreach (Slot s in slots.Values) { s.role = AIRole.None; s.mark = null; s.since = -10f; }
            Presser = Interceptor = null;
            nextTick = Time.time + team * TickInterval * 0.5f;
        }

        // ------------------------------------------------------------------ main tick

        public void Tick(Ball ball, float now)
        {
            if (ball == null || now < nextTick) return;
            nextTick = now + TickInterval;

            PlayerController owner = ball.Owner;
            bool loose = owner == null;
            if (loose) BuildPath(ball);

            // Phase: ours / theirs / loose (loose: the side that touched it last keeps its shape).
            if (!loose) Attacking = owner.teamIndex == team;
            else if (ball.LastTouchedBy != null) Attacking = ball.LastTouchedBy.teamIndex == team;

            free.Clear();
            for (int i = 0; i < mates.Count; i++)
            {
                PlayerController p = mates[i];
                Slot s = SlotOf(p);
                if (p.role == Role.GK) { SetRole(s, AIRole.Keeper, now); continue; }
                if (p == owner) { SetRole(s, AIRole.Carrier, now); continue; }
                free.Add(p);
            }

            // Loose moving ball: the player who gets there first goes for it (both phases).
            Interceptor = null;
            if (loose) AssignInterceptor(ball, now);

            if (Attacking) AssignAttack(ball, owner, now);
            else AssignDefence(ball, owner, now);

            Spread(ball);
        }

        void SetRole(Slot s, AIRole r, float now)
        {
            if (s.role != r) { s.role = r; s.since = now; }
        }

        bool CanChange(Slot s, float now) => now - s.since >= MinHold;

        // ------------------------------------------------------------------ loose ball

        void BuildPath(Ball ball)
        {
            Vector3 p = ball.Body.position, v = ball.Velocity, w = ball.Spin;
            path[0] = p;
            for (int i = 1; i <= PathSteps; i++)
            {
                p = Ball.Simulate(p, v, w, PathDt, out v);
                path[i] = p;
            }
        }

        /// <summary>Seconds this player needs to get to a point (run + a turn if it is behind him).</summary>
        static float TimeToReach(PlayerController p, Vector3 point)
        {
            Vector3 d = point - p.transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            float top = p.Locomotion.TopSpeed(true, false);
            float turn = dist > 0.5f ? Vector3.Angle(p.Locomotion.Facing, d) / 180f * 0.35f : 0f;
            // Already moving that way helps, moving away costs.
            float along = dist > 0.01f ? Vector3.Dot(p.Velocity, d / dist) : 0f;
            float accel = Mathf.Clamp((top - along) / top, 0f, 2f) * 0.35f;
            return dist / top + turn + accel;
        }

        /// <summary>Earliest point on the ball's path this player can be at in time, and when.</summary>
        float InterceptTime(PlayerController p, out Vector3 at)
        {
            for (int i = 1; i <= PathSteps; i++)
            {
                Vector3 b = path[i];
                if (b.y > 2.1f) continue; // over his head
                float t = i * PathDt;
                if (TimeToReach(p, b) <= t + 0.05f) { at = new Vector3(b.x, 0f, b.z); return t; }
            }
            Vector3 last = path[PathSteps];
            at = new Vector3(last.x, 0f, last.z);
            return PathSteps * PathDt + TimeToReach(p, at);
        }

        void AssignInterceptor(Ball ball, float now)
        {
            PlayerController best = null;
            float bestT = float.MaxValue;
            Vector3 bestAt = Vector3.zero;
            float currentT = float.MaxValue;
            Vector3 currentAt = Vector3.zero;
            PlayerController current = null;
            for (int i = 0; i < mates.Count; i++)
            {
                PlayerController p = mates[i];
                if (p.role == Role.GK) continue;
                float t = InterceptTime(p, out Vector3 at);
                if (t < bestT) { bestT = t; best = p; bestAt = at; }
                if (SlotOf(p).role == AIRole.Intercept) { current = p; currentT = t; currentAt = at; }
            }
            // Hysteresis: the one already going keeps going unless someone is clearly quicker.
            if (current != null && currentT < bestT + 0.25f) { best = current; bestAt = currentAt; }
            if (best == null) return;
            Interceptor = best;
            InterceptPoint = bestAt;
            if (best.UserControlled) { free.Remove(best); return; }
            Slot s = SlotOf(best);
            SetRole(s, AIRole.Intercept, now);
            s.target = bestAt;
            s.sprint = true;
            free.Remove(best);
        }

        // ------------------------------------------------------------------ defence

        void AssignDefence(Ball ball, PlayerController carrier, float now)
        {
            Vector3 B = ball.Body.position;
            B.y = 0f;
            Vector3 threat = carrier != null ? carrier.transform.position + carrier.Velocity * 0.3f : B;
            threat.y = 0f;

            // 1) Presser: quickest to the ball (the user's player counts if he is nearest - then no AI presses).
            Presser = null;
            if (carrier != null || Interceptor == null)
            {
                PlayerController best = null, cur = null;
                float bestT = float.MaxValue, curT = float.MaxValue;
                for (int i = 0; i < mates.Count; i++)
                {
                    PlayerController p = mates[i];
                    if (p.role == Role.GK || p == Interceptor) continue;
                    float t = TimeToReach(p, threat);
                    if (t < bestT) { bestT = t; best = p; }
                    if (SlotOf(p).role == AIRole.Press) { cur = p; curT = t; }
                }
                if (cur != null && (curT < bestT + 0.35f || !CanChange(SlotOf(cur), now))) best = cur;
                Presser = best;
                if (best != null)
                {
                    free.Remove(best);
                    if (!best.UserControlled)
                    {
                        Slot s = SlotOf(best);
                        SetRole(s, AIRole.Press, now);
                        // Approach goal-side of the ball, the PlayerController closes the last metres and tackles.
                        Vector3 toGoal = (OwnGoal - threat).normalized;
                        s.target = threat + toGoal * 0.7f;
                        s.sprint = true;
                    }
                }
            }
            // The user's own player is not driven by the AI.
            for (int i = free.Count - 1; i >= 0; i--) if (free[i].UserControlled) free.RemoveAt(i);

            // 2) Cover: between the ball and our goal, on the shot line, behind the presser.
            float ballToGoal = Vector3.Distance(threat, OwnGoal);
            Vector3 coverSpot = threat + (OwnGoal - threat).normalized * Mathf.Lerp(2.6f, 5f, Mathf.InverseLerp(8f, 25f, ballToGoal));
            coverSpot.z = Mathf.Lerp(coverSpot.z, 0f, 0.25f);
            PlayerController cover = PickNearest(coverSpot, AIRole.Cover, now);
            if (cover != null)
            {
                Slot s = SlotOf(cover);
                SetRole(s, AIRole.Cover, now);
                s.target = coverSpot;
                s.sprint = Vector3.Distance(cover.transform.position, coverSpot) > 4f;
                free.Remove(cover);
            }

            // 3) Markers: goal-side of the most dangerous free opponents, leaning into the passing lane.
            toMark.Clear();
            for (int i = 0; i < opps.Count; i++)
            {
                PlayerController o = opps[i];
                if (o == carrier || o.role == Role.GK) continue;
                toMark.Add(o);
            }
            SortByDistance(toMark, OwnGoal);

            // Keep existing pairs first (sticky marking), then give the rest to the nearest free defender.
            for (int i = free.Count - 1; i >= 0; i--)
            {
                PlayerController d = free[i];
                Slot s = SlotOf(d);
                if (s.role == AIRole.Mark && s.mark != null && toMark.Contains(s.mark)
                    && Vector3.Distance(d.transform.position, s.mark.transform.position) < 14f)
                {
                    toMark.Remove(s.mark);
                    s.target = MarkSpot(s.mark, threat, carrier != null);
                    s.sprint = Vector3.Distance(d.transform.position, s.target) > 5f;
                    free.RemoveAt(i);
                }
            }
            for (int k = 0; k < toMark.Count && free.Count > 0; k++)
            {
                PlayerController o = toMark[k];
                int bi = -1;
                float bd = float.MaxValue;
                for (int i = 0; i < free.Count; i++)
                {
                    float d = Vector3.Distance(free[i].transform.position, o.transform.position);
                    if (d < bd) { bd = d; bi = i; }
                }
                PlayerController def = free[bi];
                Slot s = SlotOf(def);
                SetRole(s, AIRole.Mark, now);
                s.mark = o;
                s.target = MarkSpot(o, threat, carrier != null);
                s.sprint = bd > 5f;
                free.RemoveAt(bi);
            }

            // 4) Anyone left: drop into a compact zone between the ball and goal.
            for (int i = 0; i < free.Count; i++)
            {
                Slot s = SlotOf(free[i]);
                SetRole(s, AIRole.Cover, now);
                Vector3 zone = Vector3.Lerp(threat, OwnGoal, 0.55f);
                zone.z = Mathf.Clamp(zone.z + (i % 2 == 0 ? 3f : -3f), -PitchDims.HalfWidth + 2f, PitchDims.HalfWidth - 2f);
                s.target = zone;
                s.sprint = false;
            }
            free.Clear();
        }

        /// <summary>Insertion sort by distance to a point (tiny lists, no allocation).</summary>
        static void SortByDistance(List<PlayerController> list, Vector3 to)
        {
            for (int i = 1; i < list.Count; i++)
            {
                PlayerController x = list[i];
                float dx = (x.transform.position - to).sqrMagnitude;
                int j = i - 1;
                while (j >= 0 && (list[j].transform.position - to).sqrMagnitude > dx) { list[j + 1] = list[j]; j--; }
                list[j + 1] = x;
            }
        }

        /// <summary>Goal-side of the opponent; the further he is from the ball, the more we sit in the lane to him.</summary>
        Vector3 MarkSpot(PlayerController o, Vector3 ballAt, bool carrierExists)
        {
            Vector3 op = o.transform.position + o.Velocity * 0.35f;
            op.y = 0f;
            Vector3 goalSide = op + (OwnGoal - op).normalized * 1.6f;
            if (!carrierExists) return goalSide;
            float far = Mathf.InverseLerp(5f, 15f, Vector3.Distance(op, ballAt));
            Vector3 lane = Vector3.Lerp(ballAt, op, 0.65f);
            return Vector3.Lerp(goalSide, lane, 0.35f * far);
        }

        PlayerController PickNearest(Vector3 spot, AIRole keepRole, float now)
        {
            PlayerController best = null, cur = null;
            float bd = float.MaxValue, cd = float.MaxValue;
            for (int i = 0; i < free.Count; i++)
            {
                PlayerController p = free[i];
                float d = Vector3.Distance(p.transform.position, spot);
                if (d < bd) { bd = d; best = p; }
                if (SlotOf(p).role == keepRole) { cur = p; cd = d; }
            }
            if (cur != null && (cd < bd + 2f || !CanChange(SlotOf(cur), now))) return cur;
            return best;
        }

        // ------------------------------------------------------------------ attack

        void AssignAttack(Ball ball, PlayerController carrier, float now)
        {
            Presser = null;
            for (int i = free.Count - 1; i >= 0; i--) if (free[i].UserControlled) free.RemoveAt(i);
            if (free.Count == 0) return;
            Vector3 C = carrier != null ? carrier.transform.position : ball.Body.position;
            C.y = 0f;

            // 1) Support: the free player best placed to offer a short, open angle.
            PlayerController support = null;
            Vector3 supportSpot = Vector3.zero;
            float bestScore = float.MinValue;
            for (int i = 0; i < free.Count; i++)
            {
                PlayerController p = free[i];
                Slot s = SlotOf(p);
                Vector3 spot = BestSupportSpot(C, p, s, out float sc);
                sc -= Vector3.Distance(p.transform.position, spot) * 0.08f;
                if (s.role == AIRole.Support) sc += 0.4f; // hysteresis
                if (sc > bestScore) { bestScore = sc; support = p; supportSpot = spot; }
            }
            if (support != null)
            {
                Slot s = SlotOf(support);
                SetRole(s, AIRole.Support, now);
                s.target = supportSpot;
                s.sprint = Vector3.Distance(support.transform.position, supportSpot) > 5f;
                free.Remove(support);
            }

            // 2) The forward runs in behind; 3) a defender stays back; 4) the rest give width.
            float lastLine = LastDefenderX();
            for (int i = 0; i < free.Count; i++)
            {
                PlayerController p = free[i];
                Slot s = SlotOf(p);
                if (p.role == Role.FW)
                {
                    SetRole(s, AIRole.RunBehind, now);
                    float x = lastLine + S * 2.5f;
                    x = S > 0f ? Mathf.Min(x, PitchDims.HalfLength - 3f) : Mathf.Max(x, -PitchDims.HalfLength + 3f);
                    // Do not run past the ball by more than 14 m (stay a passing option).
                    float maxAhead = C.x + S * 14f;
                    x = S > 0f ? Mathf.Min(x, maxAhead) : Mathf.Max(x, maxAhead);
                    float z = Mathf.Clamp(-Mathf.Sign(C.z) * 3f + p.transform.position.z * 0.3f, -PitchDims.HalfWidth + 2f, PitchDims.HalfWidth - 2f);
                    s.target = new Vector3(x, 0f, z);
                    s.sprint = true;
                }
                else if (p.role == Role.DF)
                {
                    SetRole(s, AIRole.RestDefence, now);
                    float x = Mathf.Lerp(C.x, OwnGoal.x, 0.45f);
                    x = S > 0f ? Mathf.Min(x, 4f) : Mathf.Max(x, -4f); // not past halfway
                    s.target = new Vector3(x, 0f, Mathf.Clamp(C.z * 0.3f, -4f, 4f));
                    s.sprint = false;
                }
                else
                {
                    SetRole(s, AIRole.Width, now);
                    float z = -Mathf.Sign(C.z == 0f ? 1f : C.z) * (PitchDims.HalfWidth - 3f);
                    s.target = new Vector3(Mathf.Clamp(C.x + S * 3f, -PitchDims.HalfLength + 3f, PitchDims.HalfLength - 3f), 0f, z);
                    s.sprint = false;
                }
            }
            free.Clear();
        }

        float LastDefenderX()
        {
            // The deepest opponent outfield player (toward their goal).
            float best = 0f;
            bool any = false;
            for (int i = 0; i < opps.Count; i++)
            {
                if (opps[i].role == Role.GK) continue;
                float x = opps[i].transform.position.x;
                if (!any || x * S > best * S) { best = x; any = true; }
            }
            return any ? best : AttackGoal.x - S * 8f;
        }

        /// <summary>Samples spots 7-11 m around the carrier: open lane, space, forward, not on top of team-mates.</summary>
        Vector3 BestSupportSpot(Vector3 C, PlayerController p, Slot s, out float score)
        {
            Vector3 best = C;
            score = float.MinValue;
            for (int k = 0; k < 10; k++)
            {
                float ang = (k / 10f) * Mathf.PI * 2f;
                float r = k % 2 == 0 ? 8f : 10.5f;
                Vector3 spot = C + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                float sc = SpotScore(C, spot, p);
                if (sc > score) { score = sc; best = spot; }
            }
            // Keep the old spot unless the new one is clearly better (no dithering).
            if (s.role == AIRole.Support)
            {
                float old = SpotScore(C, s.target, p);
                if (old > score - 0.5f) { score = old; best = s.target; }
            }
            return best;
        }

        float SpotScore(Vector3 C, Vector3 spot, PlayerController p)
        {
            if (Mathf.Abs(spot.x) > PitchDims.HalfLength - 1.5f || Mathf.Abs(spot.z) > PitchDims.HalfWidth - 1.5f) return -10f;
            float lane = LaneClearance(C, spot);
            float space = NearestOpponentDist(spot);
            float forward = (spot.x - C.x) * S;
            float crowd = 0f;
            for (int i = 0; i < mates.Count; i++)
            {
                PlayerController m = mates[i];
                if (m == p || m.role == Role.GK) continue;
                float d = Vector3.Distance(m.transform.position, spot);
                if (d < 5f) crowd += (5f - d) * 0.4f;
            }
            return Mathf.Min(lane, 3f) * 1.2f + Mathf.Min(space, 6f) * 0.5f + Mathf.Clamp(forward, -6f, 8f) * 0.15f - crowd;
        }

        /// <summary>Distance from the nearest opponent to the segment a-b (how open a pass lane is).</summary>
        public float LaneClearance(Vector3 a, Vector3 b)
        {
            float best = float.MaxValue;
            Vector3 ab = b - a;
            ab.y = 0f;
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
            for (int i = 0; i < opps.Count; i++)
            {
                Vector3 o = opps[i].transform.position - a;
                o.y = 0f;
                float t = Mathf.Clamp01(Vector3.Dot(o, ab) / len2);
                float d = (o - ab * t).magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        public float NearestOpponentDist(Vector3 at)
        {
            float best = float.MaxValue;
            for (int i = 0; i < opps.Count; i++)
            {
                Vector3 d = opps[i].transform.position - at;
                d.y = 0f;
                float m = d.magnitude;
                if (m < best) best = m;
            }
            return best;
        }

        // ------------------------------------------------------------------ spacing

        /// <summary>No clustering: push apart AI targets that are closer than 4.5 m (and away from the ball).</summary>
        void Spread(Ball ball)
        {
            Vector3 B = ball.Body.position;
            B.y = 0f;
            for (int iter = 0; iter < 3; iter++)
            {
                for (int i = 0; i < mates.Count; i++)
                {
                    PlayerController a = mates[i];
                    if (a.UserControlled) continue;
                    Slot sa = SlotOf(a);
                    if (!Spreadable(sa.role)) continue;
                    Vector3 push = Vector3.zero;
                    for (int j = 0; j < mates.Count; j++)
                    {
                        if (i == j) continue;
                        PlayerController b = mates[j];
                        Vector3 other = b.UserControlled || !slots.ContainsKey(b) || SlotOf(b).role == AIRole.Carrier
                            ? b.transform.position : SlotOf(b).target;
                        if (b.role == Role.GK) continue;
                        Vector3 d = sa.target - other;
                        d.y = 0f;
                        float m = d.magnitude;
                        if (m < 4.5f) push += (m > 0.01f ? d / m : new Vector3(0f, 0f, i < j ? 1f : -1f)) * (4.5f - m) * 0.5f;
                    }
                    // Off-ball attackers keep a little away from the ball itself too.
                    if (sa.role == AIRole.Support || sa.role == AIRole.Width || sa.role == AIRole.RunBehind)
                    {
                        Vector3 d = sa.target - B;
                        d.y = 0f;
                        if (d.magnitude < 4f) push += d.normalized * (4f - d.magnitude);
                    }
                    sa.target += push;
                    sa.target.x = Mathf.Clamp(sa.target.x, -PitchDims.HalfLength + 1.5f, PitchDims.HalfLength - 1.5f);
                    sa.target.z = Mathf.Clamp(sa.target.z, -PitchDims.HalfWidth + 1.2f, PitchDims.HalfWidth - 1.2f);
                }
            }
        }

        static bool Spreadable(AIRole r) => r == AIRole.Support || r == AIRole.Width || r == AIRole.RunBehind
                                            || r == AIRole.RestDefence || r == AIRole.Cover;
    }
}
