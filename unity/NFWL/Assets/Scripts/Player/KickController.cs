using UnityEngine;

namespace NFWL
{
    public enum KickPhase { None, Preparation, Approach, PlantFoot, Backswing, Contact, FollowThrough, Recovery }

    /// <summary>
    /// PHASE 3.1: every kick runs through Preparation, Approach, Plant Foot, Backswing, Contact,
    /// Follow Through and Recovery. The timeline is fixed when the kick starts; the model animates
    /// the plant foot, the kicking foot and the body from it, and the ball is struck in the physics
    /// step at the contact time - exactly when the kicking foot reaches the ball. Pressing the key
    /// therefore never launches the ball by itself.
    /// Phase lengths scale with power (bigger backswing for harder kicks) and with how far the body
    /// must turn toward the target.
    /// </summary>
    public class KickController
    {
        readonly PlayerController p;

        public KickPhase Phase { get; private set; } = KickPhase.None;
        public bool Active => Phase != KickPhase.None;
        /// <summary>True until the ball has been struck (the player still owns it).</summary>
        public bool BeforeContact => Active && !contactDone;
        public float Side { get; private set; } = 1f;
        public Vector3 Direction { get; private set; }

        public float StartTime { get; private set; }
        public float PlantTime { get; private set; }
        public float BackswingTime { get; private set; }
        public float ContactTime { get; private set; }
        public float FollowEndTime { get; private set; }
        public float RecoverEndTime { get; private set; }

        bool contactDone;

        public KickController(PlayerController owner) { p = owner; }

        public struct Form
        {
            public FootStyle style;
            public float backswing;   // m behind the contact point
            public float follow;      // m of follow through
            public float followHeight;
            public float loft;        // 0 driven .. 1 lofted (body leans back)
            public float power;       // 0..1
        }

        /// <summary>Starts a kick. Returns false if one is already running.</summary>
        public bool Begin(Ball ball, Vector3 dir, float side, Form form, float now)
        {
            if (Active) return false;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = p.Locomotion.Facing;
            dir.Normalize();
            Direction = dir;
            Side = side < 0f ? -1f : 1f;
            contactDone = false;

            // Preparation: longer when the body must turn toward the target first.
            float turn = Vector3.Angle(p.Locomotion.Facing, dir);
            float prep = 0.03f + Mathf.Clamp01((turn - 30f) / 150f) * 0.18f;

            // Approach: step so the plant foot lands beside the ball.
            Vector3 rel = ball.Body.position - p.transform.position;
            rel.y = 0f;
            float closing = Mathf.Max(2f, p.Locomotion.Speed);
            float approach = Mathf.Clamp((rel.magnitude - 0.45f) / closing, 0.04f, 0.22f);

            // Backswing grows with power; the downswing is quick.
            float backswing = 0.08f + 0.1f * form.power + 0.06f * form.loft;
            float downswing = 0.06f + 0.03f * form.power;
            float follow = 0.16f + 0.12f * form.power;
            float recover = 0.18f + 0.12f * form.power;

            StartTime = now;
            PlantTime = now + prep + approach;
            BackswingTime = PlantTime - 0.02f + backswing; // backswing starts as the plant foot lands
            ContactTime = BackswingTime + downswing;
            FollowEndTime = ContactTime + follow;
            RecoverEndTime = FollowEndTime + recover;
            Phase = KickPhase.Preparation;

            if (p.Model != null)
            {
                float tContact = ContactTime - now;
                Vector3 ballAt = ball.PredictPosition(tContact);
                Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                // Plant foot beside the ball on the other side, toes toward the target.
                Vector3 plant = new Vector3(ballAt.x, 0f, ballAt.z) - dir * 0.08f - right * (Side * 0.24f);
                p.Model.BeginKickPose(this, form);
                p.Model.BeginPlantFoot(-Side, now + prep * 0.5f, PlantTime, FollowEndTime, plant, PlayerLocomotion.DirToYaw(dir, 0f));
                p.Model.BeginFootAction(Side, PlantTime - 0.04f, ContactTime, FollowEndTime, form.style, dir, ballAt,
                    form.backswing, form.follow, BackswingTime, form.followHeight);
            }
            return true;
        }

        /// <summary>Updates the kicking foot's aim while the ball still moves before contact.</summary>
        public void TrackBall(Ball ball, float now)
        {
            if (BeforeContact && p.Model != null && now < ContactTime)
                p.Model.UpdateFootActionBall(ball.PredictPosition(ContactTime - now));
        }

        /// <summary>Physics step: true exactly once, in the step where the foot meets the ball.</summary>
        public bool ContactDue(float now)
        {
            if (!BeforeContact || now < ContactTime) return false;
            contactDone = true;
            return true;
        }

        public void Cancel()
        {
            Phase = KickPhase.None;
            contactDone = false;
        }

        /// <summary>Advances the phase label; ends the kick after recovery.</summary>
        public void Tick(float now)
        {
            if (!Active) return;
            if (now >= RecoverEndTime) { Phase = KickPhase.None; return; }
            if (now >= FollowEndTime) Phase = KickPhase.Recovery;
            else if (contactDone && now >= ContactTime + 0.03f) Phase = KickPhase.FollowThrough;
            else if (now >= ContactTime - 0.01f) Phase = KickPhase.Contact;
            else if (now >= PlantTime) Phase = KickPhase.Backswing;
            else if (now >= PlantTime - 0.06f) Phase = KickPhase.PlantFoot;
            else if (now >= StartTime + 0.03f) Phase = KickPhase.Approach;
            else Phase = KickPhase.Preparation;
        }

        /// <summary>Running speed multiplier during the kick (the player checks his stride to strike).</summary>
        public float SpeedScale(float now)
        {
            if (!Active) return 1f;
            if (now < ContactTime) return 0.55f;
            if (now < FollowEndTime) return 0.45f;
            return Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(FollowEndTime, RecoverEndTime, now));
        }
    }
}
