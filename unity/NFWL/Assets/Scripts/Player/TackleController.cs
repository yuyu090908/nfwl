using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// PHASE 5.2: standing tackle as an action, not an automatic dice roll.
    /// The tackler reaches the nearer foot toward where the ball will be; at the contact moment the
    /// tackle only counts if that foot can actually reach the ball (distance, angle, height).
    /// Then the outcome depends on Tackling vs. the dribbler's BallControl, how exposed the ball is
    /// (a long touch is easy to nick, a ball under the sole is not), the approach direction (from
    /// behind or into a shielding player is hard) and the dribbler's touch timing.
    /// A missed tackle costs balance, so a defender who dives in gets beaten.
    /// </summary>
    public class TackleController
    {
        readonly PlayerController p;

        public bool Active { get; private set; }
        public float ContactTime { get; private set; }
        public float EndTime { get; private set; }
        public Vector3 Direction { get; private set; }
        /// <summary>Result of the last tackle, for the HUD / tests: "", "WON", "POKED", "MISSED", "OUT OF REACH".</summary>
        public string LastResult { get; private set; } = "";

        float cooldownUntil;
        bool resolved;
        float side = 1f;

        const float Windup = 0.14f;
        const float Reach = 1.15f;     // planted foot to the far boot with the lunge (m)
        const float MaxAngle = 75f;    // ball must be in front of the tackler

        public TackleController(PlayerController owner) { p = owner; }

        public bool Ready(float now) => !Active && now >= cooldownUntil;

        public bool Begin(Ball ball, float now)
        {
            if (!Ready(now) || ball == null) return false;
            Active = true;
            resolved = false;
            ContactTime = now + Windup;
            EndTime = ContactTime + 0.45f;
            cooldownUntil = EndTime + 0.35f;

            Vector3 ballAt = ball.PredictPosition(Windup);
            Vector3 to = ballAt - p.transform.position;
            to.y = 0f;
            Direction = to.sqrMagnitude > 0.01f ? to.normalized : p.Locomotion.Facing;
            side = Vector3.Dot(ballAt - p.transform.position, new Vector3(p.Locomotion.Facing.z, 0f, -p.Locomotion.Facing.x)) < 0f ? -1f : 1f;
            if (p.Model != null)
            {
                // Stab the near foot at the ball: short backswing, the foot stays low.
                p.Model.BeginFootAction(side, now, ContactTime, ContactTime + 0.2f, FootStyle.Inside, Direction, ballAt, 0.08f, 0.12f, -1f, 0.03f);
                p.Model.PlayLunge(now, ContactTime, EndTime);
            }
            return true;
        }

        public void TrackBall(Ball ball, float now)
        {
            if (Active && !resolved && now < ContactTime && p.Model != null)
                p.Model.UpdateFootActionBall(ball.PredictPosition(ContactTime - now));
        }

        /// <summary>Physics step: resolves the tackle at the contact moment.</summary>
        public void FixedTick(Ball ball, float now)
        {
            if (!Active) return;
            if (now >= EndTime) { Active = false; return; }
            if (resolved || now < ContactTime) return;
            resolved = true;
            Resolve(ball);
        }

        void Resolve(Ball ball)
        {
            Vector3 P = p.transform.position;
            Vector3 B = ball.Body.position;
            Vector3 rel = B - P;
            rel.y = 0f;
            float dist = rel.magnitude;
            float angle = Vector3.Angle(p.Locomotion.Facing, rel);
            bool reachable = dist <= Reach && angle <= MaxAngle && B.y < 0.5f && !ball.HeldInHands;
            if (!reachable)
            {
                LastResult = "OUT OF REACH";
                p.Locomotion.LoseBalance(0.25f);
                return;
            }

            PlayerController owner = ball.Owner;
            if (owner == null)
            {
                // Loose ball: the foot gets there, so it is ours.
                LastResult = "WON";
                p.GainBall();
                return;
            }
            if (owner.teamIndex == p.teamIndex) return;

            PlayerAttributes a = p.attributes, o = owner.attributes;
            float chance = Rules.AttrLerp(a.Tackling, 0.35f, 0.85f) / Rules.AttrLerp(o.BallControl, 0.75f, 1.25f);
            // How exposed the ball is: far from the dribbler = easy to nick.
            Vector3 ownerToBall = B - owner.transform.position;
            ownerToBall.y = 0f;
            float exposure = Mathf.Clamp01((ownerToBall.magnitude - 0.35f) / 0.6f);
            chance *= Mathf.Lerp(0.8f, 1.4f, exposure);
            // Coming from behind the dribbler, or into a shield: much harder.
            Vector3 ownerToMe = P - owner.transform.position;
            ownerToMe.y = 0f;
            if (Vector3.Angle(owner.Locomotion.Facing, ownerToMe) > 120f) chance *= 0.45f;
            if (owner.Dribble.Shielding) chance *= 0.6f;
            // The dribbler's foot is just arriving on the ball: he protects it.
            if (owner.Dribble.TouchPending) chance *= 0.8f;
            // Off-balance tackler is weaker.
            chance *= Mathf.Lerp(0.6f, 1f, p.Locomotion.Balance);
            chance = Mathf.Clamp(chance, 0.05f, 0.95f);

            if (Random.value < chance)
            {
                owner.ReleaseBall(0.6f);
                owner.Locomotion.LoseBalance(0.2f);
                if (Random.value < Rules.AttrLerp(a.Tackling, 0.4f, 0.75f))
                {
                    LastResult = "WON";
                    p.GainBall();
                }
                else
                {
                    // Poked away: the ball squirts off, away from the dribbler.
                    LastResult = "POKED";
                    Vector3 away = (rel.normalized + ownerToBall.normalized * 0.5f + Random.insideUnitSphere * 0.4f);
                    away.y = 0f;
                    ball.Touch(away.normalized * Random.Range(3f, 6f), p);
                }
                if (p.Match != null) p.Match.ShowKickLabel(p, LastResult == "WON" ? "TACKLE" : "TACKLE (LOOSE)");
            }
            else
            {
                LastResult = "MISSED";
                p.Locomotion.LoseBalance(0.3f);
            }
        }

        public void Cancel()
        {
            Active = false;
            resolved = true;
        }
    }
}
