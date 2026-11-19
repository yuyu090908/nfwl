using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// PHASE 5.3: player-to-player contact. When two players run into each other (the
    /// CharacterControllers stop the overlap), the bump is turned into a reaction scaled by its
    /// strength: a sideways sway of the upper body, a change of running speed, lost balance and,
    /// for the ball carrier, a chance to lose control. Running shoulder to shoulder in the same
    /// direction is a shoulder challenge: the player who is quicker and better balanced holds his line.
    /// </summary>
    public static class PlayerContacts
    {
        public const float MinClosingSpeed = 0.6f;

        /// <param name="a">The player whose move hit b.</param>
        /// <param name="aVelocity">a's velocity before the controller stopped him.</param>
        public static void Resolve(PlayerController a, PlayerController b, Vector3 aVelocity, float now)
        {
            if (a == null || b == null || a == b) return;
            if (now - a.LastBumpTime < 0.3f && a.LastBumpWith == b) return;

            Vector3 n = b.transform.position - a.transform.position;
            n.y = 0f;
            if (n.sqrMagnitude < 1e-4f) return;
            n.Normalize();
            Vector3 rel = aVelocity - b.Velocity;
            rel.y = 0f;
            float closing = Vector3.Dot(rel, n);
            if (closing < MinClosingSpeed) return;

            a.LastBumpTime = b.LastBumpTime = now;
            a.LastBumpWith = b;
            b.LastBumpWith = a;

            PlayerLocomotion la = a.Locomotion, lb = b.Locomotion;
            // Who holds his ground: balance, speed, and a set stance (jockey / shielding) help.
            float pa = Strength(a, aVelocity.magnitude);
            float pb = Strength(b, b.Velocity.magnitude);
            float shareA = pb / (pa + pb); // how much of the bump a takes
            float shareB = pa / (pa + pb);

            bool shoulder = Vector3.Dot(la.Facing, lb.Facing) > 0.6f && Mathf.Abs(Vector3.Dot(n, la.Facing)) < 0.5f;
            float impulse = closing * (shoulder ? 0.8f : 0.6f);

            float lossA = impulse * 0.07f * shareA * 2f;
            float lossB = impulse * 0.07f * shareB * 2f;
            if (shoulder)
            {
                // The weaker one is knocked off his line.
                if (shareA > shareB) lossA += 0.1f; else lossB += 0.1f;
            }
            la.Push(-n * impulse * shareA, lossA);
            lb.Push(n * impulse * shareB, lossB);

            CheckBall(a, lossA);
            CheckBall(b, lossB);
            if (a.Match != null && (impulse > 2.5f || shoulder)) a.Match.OnPlayerContact(a, b, impulse, shoulder);
        }

        static float Strength(PlayerController p, float speed)
        {
            float s = 0.4f + p.Locomotion.Balance * (1f + 0.12f * speed);
            LocoState st = p.Locomotion.State;
            if (st == LocoState.Jockey || (p.HasBall && p.Dribble.Shielding)) s *= 1.3f;
            s *= Rules.AttrLerp(p.attributes.Tackling, 0.9f, 1.15f);
            return Mathf.Max(0.1f, s);
        }

        /// <summary>A hard bump can knock the ball off the dribbler's foot.</summary>
        static void CheckBall(PlayerController p, float loss)
        {
            if (!p.HasBall || p.role == Role.GK) return;
            float keep = Rules.AttrLerp(p.attributes.BallControl, 0.2f, 0.4f);
            if (loss > keep || p.Locomotion.Balance < 0.35f) p.ReleaseBall(0.4f);
        }
    }
}
