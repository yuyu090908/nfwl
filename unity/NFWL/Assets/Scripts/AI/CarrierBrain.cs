using System.Collections.Generic;
using UnityEngine;

namespace NFWL
{
    public enum AIAction { None, Dribble, Pass, ThroughPass, Shoot, Shield, Clear }

    /// <summary>One scored option for the ball carrier.</summary>
    public struct AIDecision
    {
        public AIAction action;
        public float score;
        public KickType kickType;
        public KickVariant variant;
        public float power;
        public Vector3 aim;               // kick direction (flat) or dribble direction
        public PlayerController target;   // pass receiver
        public bool sprint;
    }

    /// <summary>
    /// PHASE 8.1: Utility AI for the player on the ball. Every option (shoot, each pass, through
    /// ball, dribble, shield, clear) gets a 0..1 score from simple considerations - distance and
    /// angle to goal, blockers, how open the lane and the receiver are, forward gain, pressure,
    /// and the player's attributes. The best one is taken; the current choice gets a bonus
    /// (hysteresis) so the carrier does not flip between options every decision.
    /// </summary>
    public class CarrierBrain
    {
        readonly PlayerController p;
        public AIDecision Current;
        public float DecidedAt { get; private set; } = -10f;
        Vector3 dribbleDir;
        /// <summary>The line the carrier is dribbling on (kept between decisions).</summary>
        public Vector3 DribbleDir => dribbleDir;

        const float Hysteresis = 0.12f;

        public CarrierBrain(PlayerController owner) { p = owner; }

        float S => p.AttackSign;

        public void Reset()
        {
            Current = default;
            dribbleDir = Vector3.zero;
        }

        /// <summary>Scores every option and returns the one to do now.</summary>
        public AIDecision Decide(Ball ball, MatchManager m, TeamAI teamAI, float heldFor, float now)
        {
            DecidedAt = now;
            Vector3 pos = Flat(p.transform.position);
            Vector3 goal = p.AttackGoalCenter;
            Vector3 toGoal = goal - pos;
            float goalDist = toGoal.magnitude;
            PlayerController threat = m.NearestOpponent(p, out float threatDist);
            float pressure = Mathf.Clamp01((3f - threatDist) / 2f); // 0 free .. 1 on top of him

            AIDecision best = Score(DribbleOption(m, pos, goal, threatDist), AIAction.Dribble);
            // Options that kick need the ball settled for a moment (not on the first touch).
            bool settled = heldFor > 0.2f;

            if (settled)
            {
                Consider(ref best, ShootOption(m, pos, goal, goalDist, pressure));
                List<PlayerController> mates = m.TeamList(p.teamIndex);
                for (int i = 0; i < mates.Count; i++)
                {
                    PlayerController mate = mates[i];
                    if (mate == p) continue;
                    Consider(ref best, PassOption(m, pos, mate, pressure));
                    if (teamAI != null && teamAI.RoleOf(mate) == AIRole.RunBehind)
                        Consider(ref best, ThroughOption(pos, mate));
                }
                Consider(ref best, ClearOption(pos, goalDist, pressure));
            }
            Consider(ref best, ShieldOption(threat, threatDist, pos, goal));

            Current = best;
            return best;
        }

        AIDecision Score(AIDecision d, AIAction a)
        {
            d.action = a;
            if (Current.action == a && (a == AIAction.Dribble || a == AIAction.Shield)) d.score += Hysteresis;
            return d;
        }

        void Consider(ref AIDecision best, AIDecision d)
        {
            if (d.action == AIAction.None) return;
            if (Current.action == d.action && (d.target == null || d.target == Current.target)) d.score += Hysteresis;
            if (d.score > best.score) best = d;
        }

        // ------------------------------------------------------------------ options

        AIDecision ShootOption(MatchManager m, Vector3 pos, Vector3 goal, float goalDist, float pressure)
        {
            AIDecision d = new AIDecision { action = AIAction.Shoot, kickType = KickType.Shoot };
            if (goalDist > 24f) { d.action = AIAction.None; return d; }
            // Visible goal: angle between the posts.
            Vector3 p1 = goal + new Vector3(0f, 0f, -PitchDims.GoalHalfWidth);
            Vector3 p2 = goal + new Vector3(0f, 0f, PitchDims.GoalHalfWidth);
            float angle = Vector3.Angle(p1 - pos, p2 - pos);
            float angleF = Mathf.Clamp01(angle / 22f);
            float distF = Mathf.Clamp01(1f - (goalDist - 7f) / 16f);

            // Aim at the side the keeper leaves open.
            PlayerController gk = null;
            List<PlayerController> opp = m.TeamList(1 - p.teamIndex);
            for (int i = 0; i < opp.Count; i++) if (opp[i].role == Role.GK) gk = opp[i];
            float gz = gk != null ? gk.transform.position.z : 0f;
            float side = gz > 0f ? -1f : 1f;
            if (Mathf.Abs(gz) < 0.2f) side = Random.value < 0.5f ? -1f : 1f;
            float zAim = side * (PitchDims.GoalHalfWidth - Rules.AttrLerp(p.attributes.Finishing, 0.55f, 0.3f));
            Vector3 target = goal + new Vector3(0f, 0f, zAim);
            d.aim = Flat(target - pos);

            // Blockers in the shooting triangle (not the keeper).
            float block = 1f;
            for (int i = 0; i < opp.Count; i++)
            {
                if (opp[i].role == Role.GK) continue;
                Vector3 o = Flat(opp[i].transform.position) - pos;
                float along = Vector3.Dot(o, d.aim.normalized);
                if (along < 0.5f || along > goalDist) continue;
                float off = (o - d.aim.normalized * along).magnitude;
                if (off < 0.8f + along * 0.08f) block *= 0.5f;
            }

            float fin = Rules.AttrLerp(p.attributes.Finishing, 0.8f, 1.15f);
            d.score = 0.95f * distF * (0.35f + 0.65f * angleF) * block * fin * Mathf.Lerp(1f, 0.85f, pressure);
            // Very close and open: just shoot.
            if (goalDist < 9f && block > 0.9f) d.score = Mathf.Max(d.score, 0.85f);

            // Shot type: placed / finesse from the edge of the box, low and hard close in, power from range.
            d.variant = KickVariant.Normal;
            float r = Random.value;
            if (goalDist > 9f && goalDist < 18f && r < Rules.AttrLerp(p.attributes.Finishing, 0.15f, 0.45f)) d.variant = KickVariant.Finesse;
            else if (goalDist <= 10f && r < 0.35f) d.variant = KickVariant.LowDriven;
            else if (goalDist > 15f && r < Rules.AttrLerp(p.attributes.ShotPower, 0.1f, 0.5f)) d.variant = KickVariant.Power;
            d.power = Mathf.Clamp01(0.5f + goalDist / 30f + Random.Range(-0.08f, 0.1f));
            return d;
        }

        AIDecision PassOption(MatchManager m, Vector3 pos, PlayerController mate, float pressure)
        {
            AIDecision d = new AIDecision { action = AIAction.Pass, target = mate };
            Vector3 to = Flat(mate.transform.position + mate.Velocity * 0.4f) - pos;
            float dist = to.magnitude;
            if (dist < 3.5f || dist > 32f || mate.role == Role.GK && S * (mate.transform.position.x - pos.x) > -2f)
            {
                d.action = AIAction.None;
                return d;
            }
            float lane = LaneToOpponents(m, pos, pos + to);
            float laneF = Mathf.Clamp01((lane - 0.6f) / 2f);
            m.NearestOpponent(mate, out float space);
            float spaceF = Mathf.Clamp01(space / 5f);
            float gain = (mate.transform.position.x - pos.x) * S;
            float progF = Mathf.Clamp01(0.45f + gain / 18f);
            bool longBall = dist > 18f;
            float skill = Rules.AttrLerp(longBall ? p.attributes.LongPass : p.attributes.ShortPass, 0.75f, 1.1f);
            float distF = longBall ? Mathf.Lerp(0.9f, 0.6f, Mathf.InverseLerp(18f, 32f, dist)) : 1f;
            d.score = 0.62f * laneF * (0.35f + 0.65f * spaceF) * (0.45f + 0.55f * progF) * distF * skill;
            d.score *= Mathf.Lerp(1f, 1.45f, pressure); // under pressure, moving the ball on is better
            if (mate.role == Role.GK) d.score *= 0.5f;
            d.kickType = longBall ? KickType.LongPass : KickType.ShortPass;
            d.variant = KickVariant.Normal;
            d.aim = to;
            d.power = 0.5f;
            return d;
        }

        AIDecision ThroughOption(Vector3 pos, PlayerController runner)
        {
            AIDecision d = new AIDecision { action = AIAction.ThroughPass, target = runner };
            Vector3 rv = Flat(runner.Velocity);
            if (rv.x * S < 2.5f) { d.action = AIAction.None; return d; }
            Vector3 spot = Flat(runner.transform.position) + rv.normalized * 6f;
            Vector3 to = spot - pos;
            float dist = to.magnitude;
            if (dist < 6f || dist > 30f) { d.action = AIAction.None; return d; }
            float lane = LaneToOpponents(p.Match, pos, spot);
            float space = NearestOpponentTo(p.Match, spot);
            bool lofted = lane < 1.2f && dist > 14f;
            float laneF = lofted ? 0.8f : Mathf.Clamp01((lane - 0.7f) / 2f);
            float spaceF = Mathf.Clamp01((space - 1.5f) / 4f);
            float skill = Rules.AttrLerp(lofted ? p.attributes.LongPass : p.attributes.ShortPass, 0.7f, 1.1f);
            d.score = 0.8f * laneF * spaceF * skill;
            d.kickType = lofted ? KickType.LongPass : KickType.ShortPass;
            d.variant = lofted ? KickVariant.LoftedThrough : KickVariant.Through;
            d.aim = Flat(runner.transform.position) - pos; // the planner leads the runner
            d.power = Mathf.Clamp01(0.4f + dist / 40f);
            return d;
        }

        AIDecision DribbleOption(MatchManager m, Vector3 pos, Vector3 goal, float threatDist)
        {
            AIDecision d = new AIDecision { action = AIAction.Dribble };
            Vector3 goalDir = (goal - pos).normalized;
            float bestSc = float.MinValue;
            Vector3 bestDir = goalDir;
            float bestClear = 0f;
            for (int k = -4; k <= 4; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 20f, 0f) * goalDir;
                float clear = Mathf.Min(RayClearance(m, pos, dir, 2f), RayClearance(m, pos, dir, 4.5f));
                Vector3 ahead = pos + dir * 4f;
                float edge = Mathf.Max(0f, Mathf.Abs(ahead.z) - (PitchDims.HalfWidth - 2f)) + Mathf.Max(0f, Mathf.Abs(ahead.x) - (PitchDims.HalfLength - 1f));
                float sc = Vector3.Dot(dir, goalDir) * 1.0f + Mathf.Min(clear, 4f) * 0.45f - edge * 1.5f;
                if (dribbleDir != Vector3.zero && Vector3.Angle(dir, dribbleDir) < 25f) sc += 0.25f; // keep the line
                if (sc > bestSc) { bestSc = sc; bestDir = dir; bestClear = clear; }
            }
            dribbleDir = bestDir;
            float drib = Rules.AttrLerp((p.attributes.BallControl + p.attributes.Agility) / 2, 0.8f, 1.15f);
            d.aim = bestDir;
            d.sprint = bestClear > 3.5f && threatDist > 3f;
            d.score = 0.42f * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(bestClear / 4f)) * drib;
            return d;
        }

        AIDecision ShieldOption(PlayerController threat, float threatDist, Vector3 pos, Vector3 goal)
        {
            AIDecision d = new AIDecision { action = AIAction.Shield };
            if (threat == null || threatDist > 1.8f) { d.action = AIAction.None; return d; }
            // Worth it when the opponent is between us and goal (we cannot turn) - hold it for support.
            Vector3 toThreat = Flat(threat.transform.position) - pos;
            float front = Vector3.Dot(toThreat.normalized, (goal - pos).normalized);
            d.score = 0.3f * Mathf.Clamp01(front + 0.3f) * Rules.AttrLerp(p.attributes.BallControl, 0.8f, 1.2f);
            d.aim = -toThreat.normalized;
            if (Current.action == AIAction.Shield) d.score += Hysteresis;
            return d;
        }

        AIDecision ClearOption(Vector3 pos, float goalDist, float pressure)
        {
            AIDecision d = new AIDecision { action = AIAction.Clear, kickType = KickType.LongPass };
            float ownGoalDist = Vector3.Distance(pos, p.OwnGoalCenter);
            if (ownGoalDist > 15f || pressure < 0.4f) { d.action = AIAction.None; return d; }
            d.score = 0.5f * pressure * Mathf.InverseLerp(15f, 6f, ownGoalDist) + 0.1f;
            // Up the line, away from the middle.
            float side = pos.z >= 0f ? 1f : -1f;
            d.aim = new Vector3(S, 0f, side * 0.55f);
            d.power = 0.85f;
            return d;
        }

        // ------------------------------------------------------------------ helpers

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        float LaneToOpponents(MatchManager m, Vector3 a, Vector3 b)
        {
            List<PlayerController> opp = m.TeamList(1 - p.teamIndex);
            Vector3 ab = b - a;
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
            float best = float.MaxValue;
            for (int i = 0; i < opp.Count; i++)
            {
                Vector3 o = Flat(opp[i].transform.position) - a;
                float t = Mathf.Clamp01(Vector3.Dot(o, ab) / len2);
                float d = (o - ab * t).magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        float NearestOpponentTo(MatchManager m, Vector3 at)
        {
            List<PlayerController> opp = m.TeamList(1 - p.teamIndex);
            float best = float.MaxValue;
            for (int i = 0; i < opp.Count; i++)
            {
                float d = Flat(opp[i].transform.position - at).magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        float RayClearance(MatchManager m, Vector3 from, Vector3 dir, float ahead)
        {
            Vector3 point = from + dir * ahead;
            return NearestOpponentTo(m, point);
        }
    }
}
