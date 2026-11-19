using System.Collections.Generic;
using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// PHASE 4.4: the ball against players' bodies (legs / shins, torso, head). Player capsules do not
    /// collide with the ball in PhysX (the dribble would fight them), so fast loose balls are tested
    /// here against simple body parts once per physics step, before the ball moves.
    /// Rules that stop double handling: the dribbler is skipped (his touches move the ball), so is a
    /// player whose foot is about to strike it or who just kicked it, and each player can deflect the
    /// ball at most once per 0.15 s, so one contact gives one impulse.
    /// </summary>
    public static class BallContacts
    {
        struct Part
        {
            public string name;
            public float y0, y1, radius, restitution;
            public Part(string n, float a, float b, float r, float e) { name = n; y0 = a; y1 = b; radius = r; restitution = e; }
        }

        static readonly Part[] parts =
        {
            new Part("Legs", 0.12f, 0.85f, 0.2f, 0.35f),
            new Part("Torso", 0.95f, 1.45f, 0.24f, 0.22f),
            new Part("Head", 1.62f, 1.74f, 0.13f, 0.5f),
        };

        public static void Step(Ball ball, List<PlayerController> players, float dt, float now)
        {
            if (ball == null || ball.HeldInHands) return;
            Vector3 v = ball.Velocity;
            if (v.sqrMagnitude < 2.5f * 2.5f) return; // slower balls are trapped or touched instead
            Vector3 p0 = ball.Body.position;
            Vector3 p1 = p0 + v * dt;
            float br = PitchDims.BallRadius;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerController pl = players[i];
                if (pl == ball.Owner) continue;
                if (pl.Kick.BeforeContact || pl.Dribble.TouchPending) continue;
                if (ball.LastTouchedBy == pl && now - ball.LastTouchTime < 0.25f) continue;
                if (now - pl.LastBodyContactTime < 0.15f) continue;

                Vector3 root = pl.transform.position;
                // Quick reject: far from this player.
                Vector3 mid = (p0 + p1) * 0.5f - root;
                mid.y = 0f;
                if (mid.sqrMagnitude > 2.5f) continue;

                for (int k = 0; k < parts.Length; k++)
                {
                    Part part = parts[k];
                    Vector3 a = root + Vector3.up * part.y0;
                    Vector3 b = root + Vector3.up * part.y1;
                    ClosestPoints(p0, p1, a, b, out Vector3 onBall, out Vector3 onBody);
                    Vector3 sep = onBall - onBody;
                    float dist = sep.magnitude;
                    if (dist > part.radius + br) continue;

                    Vector3 n = dist > 1e-4f ? sep / dist : -v.normalized;
                    Vector3 rel = v - pl.Velocity;
                    float vn = Vector3.Dot(rel, n);
                    if (vn >= 0f) continue; // moving away already
                    // Reflect the normal part, damp the sliding part (the body is soft and grippy).
                    Vector3 tangential = rel - n * vn;
                    Vector3 newRel = tangential * 0.6f - n * vn * part.restitution;
                    Vector3 newV = newRel + pl.Velocity;
                    ball.Body.position = onBody + n * (part.radius + br + 0.01f);
                    ball.Velocity = newV;
                    ball.Body.angularVelocity *= 0.4f;
                    pl.LastBodyContactTime = now;
                    pl.OnBallHitBody(part.name, v.magnitude);
                    ball.LastTouchedBy = pl;
                    return; // one contact per step
                }
            }
        }

        /// <summary>Closest points between segments p0-p1 and a-b.</summary>
        static void ClosestPoints(Vector3 p0, Vector3 p1, Vector3 a, Vector3 b, out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = p1 - p0, d2 = b - a, r = p0 - a;
            float aa = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (aa <= 1e-8f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                float bb = Vector3.Dot(d1, d2);
                float denom = aa * e - bb * bb;
                s = denom > 1e-8f ? Mathf.Clamp01((bb * f - c * e) / denom) : 0f;
                t = (bb * s + f) / e;
                if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / aa); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((bb - c) / aa); }
            }
            c1 = p0 + d1 * s;
            c2 = a + d2 * t;
        }
    }
}
