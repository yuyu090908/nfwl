using UnityEngine;

namespace NFWL
{
    public enum TouchKind
    {
        Normal, Sprint, CloseControl, Directional, FirstTouch, Outside, Inside, Sole, Stop, Shield
    }

    /// <summary>Which part of the foot meets the ball (drives the foot pose of a touch or kick).</summary>
    public enum FootStyle { Inside, Outside, Laces, Sole, Toe }

    /// <summary>
    /// PHASE 2: touch dribbling. The ball stays a free physics object; possession only means
    /// "this player is dribbling it". When a touch is due the dribbler picks a foot, the model swings
    /// that foot to where the ball will be, and the new ball velocity is applied at the moment of
    /// contact (not when the key is pressed). Each touch is planned with the ball model so the ball
    /// rolls to where the player will meet it next (normal, sprint push, close control, directional
    /// touch, first touch, sole drag-back, stop, shielding). BallControl sets touch size and accuracy,
    /// Agility how quickly a new touch can follow, the weak foot is less accurate.
    /// </summary>
    public class DribbleController
    {
        readonly PlayerController p;

        public TouchKind LastKind { get; private set; } = TouchKind.Normal;
        public float LastTouchTime { get; private set; } = -10f;
        public bool Shielding { get; private set; }
        public bool TouchPending => pending;

        bool pending;
        bool firstTouchPending;
        TouchKind pendingKind;
        float pendingSide;
        float contactTime;
        float nextTouchTime;
        float lastSide = 1f;

        public DribbleController(PlayerController owner) { p = owner; }

        DribbleTuning T => NFWLTuning.Current.dribble;

        public void OnGainPossession(bool firstTouch)
        {
            pending = false;
            firstTouchPending = firstTouch;
            nextTouchTime = 0f;
        }

        public void Cancel()
        {
            pending = false;
            firstTouchPending = false;
            Shielding = false;
        }

        /// <summary>Called every physics step while the player has the ball (not keeper hands).</summary>
        public void FixedTick(Ball ball, Vector3 moveDir, float moveAmount, bool sprint, float now)
        {
            DribbleTuning t = T;
            PlayerLocomotion loco = p.Locomotion;
            Vector3 P = p.transform.position;
            Vector3 B = ball.Body.position;
            Vector3 vb = ball.Velocity;
            vb.y = 0f;
            Vector3 facing = loco.Facing;
            Vector3 right = new Vector3(facing.z, 0f, -facing.x);
            Vector3 rel = B - P;
            rel.y = 0f;
            float dist = rel.magnitude;
            float lz = Vector3.Dot(rel, facing), lx = Vector3.Dot(rel, right);
            bool hasInput = moveAmount > 0.05f && moveDir.sqrMagnitude > 0.01f;

            float oppDist = float.MaxValue;
            PlayerController opp = p.Match != null ? p.Match.NearestOpponent(p, out oppDist) : null;
            Shielding = opp != null && oppDist < t.shieldDistance && !sprint && loco.Speed < 2.5f;

            if (pending)
            {
                if (now >= contactTime) ExecuteTouch(ball, moveDir, moveAmount, sprint, now, opp);
                else if (p.Model != null) p.Model.UpdateFootActionBall(ball.PredictPosition(contactTime - now));
                return;
            }

            float since = now - LastTouchTime;
            float minRetouch = Rules.AttrLerp(p.attributes.Agility, t.minRetouch.x, t.minRetouch.y);
            bool reachable = B.y < 0.5f && dist < t.reach && lz > -0.35f;
            Vector3 ballDir = vb.magnitude > 0.8f ? vb.normalized : facing;

            TouchKind kind;
            bool urgent = false;
            if (firstTouchPending)
            {
                kind = TouchKind.FirstTouch;
                urgent = true;
            }
            else if (!hasInput || Shielding)
            {
                // Keep the ball at the feet: stop it, or keep it on the far side from the opponent.
                float relSpeed = (vb - loco.Velocity).magnitude;
                bool wrongSide = Shielding && opp != null && Vector3.Dot(rel, P - opp.transform.position) < 0.1f;
                if (relSpeed > 0.7f || dist > 0.75f || wrongSide)
                {
                    kind = Shielding ? TouchKind.Shield : TouchKind.Stop;
                    urgent = true;
                }
                else return;
            }
            else
            {
                float ang = Vector3.Angle(ballDir, moveDir);
                if (ang > 100f && loco.Speed < 3.5f) { kind = TouchKind.Sole; urgent = true; }
                else if (ang > t.directionalAngle) { kind = TouchKind.Directional; urgent = true; }
                else if (sprint) kind = TouchKind.Sprint;
                else if (oppDist < t.closeControlDistance || loco.Speed < 2.2f) kind = TouchKind.CloseControl;
                else kind = TouchKind.Normal;

                // About to run over the ball: touch it now.
                float speedRatio = Mathf.Clamp01(loco.Speed / Mathf.Max(1f, loco.SprintSpeed));
                if (lz < 0.35f + 0.3f * speedRatio) urgent = true;

                if (!urgent) LineAssist(ball, moveDir, ballDir, Time.fixedDeltaTime);
            }

            bool due = now >= nextTouchTime || (urgent && since >= minRetouch);
            if (!due || !reachable) return;
            Schedule(ball, kind, moveDir, hasInput, lx, now);
        }

        void Schedule(Ball ball, TouchKind kind, Vector3 moveDir, bool hasInput, float lx, float now)
        {
            GaitController gait = p.Model != null ? p.Model.Gait : null;
            // Foot choice: the ball's side first, otherwise the foot that is free (swinging), otherwise alternate.
            float side;
            if (lx < -0.06f) side = -1f;
            else if (lx > 0.06f) side = 1f;
            else if (gait != null && gait.Left.swinging && !gait.Right.swinging) side = -1f;
            else if (gait != null && gait.Right.swinging && !gait.Left.swinging) side = 1f;
            else side = -lastSide;
            // Never reach across the body with the far foot.
            if (side > 0f && lx < -0.15f) side = -1f;
            if (side < 0f && lx > 0.15f) side = 1f;

            Vector3 facing = p.Locomotion.Facing;
            Vector3 right = new Vector3(facing.z, 0f, -facing.x);
            Vector3 dir = hasInput ? moveDir : facing;
            float dl = Vector3.Dot(dir, right);
            FootStyle style;
            switch (kind)
            {
                case TouchKind.Sole:
                case TouchKind.Stop:
                case TouchKind.Shield:
                    style = FootStyle.Sole;
                    break;
                case TouchKind.FirstTouch:
                    style = FootStyle.Inside;
                    break;
                default:
                    if (dl * side > 0.3f) { style = FootStyle.Outside; kind = kind == TouchKind.Directional ? TouchKind.Outside : kind; }
                    else if (dl * side < -0.25f) { style = FootStyle.Inside; kind = kind == TouchKind.Directional ? TouchKind.Inside : kind; }
                    else style = kind == TouchKind.CloseControl ? FootStyle.Inside : FootStyle.Laces;
                    break;
            }

            float windup;
            switch (kind)
            {
                case TouchKind.FirstTouch: windup = 0.06f; break;
                case TouchKind.Sprint: windup = 0.13f; break;
                case TouchKind.Normal: windup = 0.11f; break;
                default: windup = 0.09f; break;
            }

            pending = true;
            pendingKind = kind;
            pendingSide = side;
            contactTime = now + windup;
            float backswing = kind == TouchKind.Sprint ? 0.22f : style == FootStyle.Sole ? 0.05f : 0.12f;
            if (p.Model != null)
            {
                p.Model.BeginFootAction(side, now, contactTime, contactTime + 0.14f, style, dir,
                    ball.PredictPosition(windup), backswing, kind == TouchKind.Sprint ? 0.3f : 0.15f);
            }
        }

        void ExecuteTouch(Ball ball, Vector3 moveDir, float moveAmount, bool sprint, float now, PlayerController opp)
        {
            DribbleTuning t = T;
            pending = false;
            PlayerLocomotion loco = p.Locomotion;
            Vector3 P = p.transform.position;
            Vector3 B = ball.Body.position;
            Vector3 rel = B - P;
            rel.y = 0f;
            // The foot must actually reach the ball, or nothing happens.
            if (rel.magnitude > t.reach + 0.15f || B.y > 0.6f)
            {
                nextTouchTime = now + 0.1f;
                return;
            }

            bool hasInput = moveAmount > 0.05f && moveDir.sqrMagnitude > 0.01f;
            PlayerAttributes a = p.attributes;
            TouchKind kind = pendingKind;
            Vector3 facing = loco.Facing;
            Vector3 want = hasInput ? moveDir : facing;
            Vector3 v;
            float interval;

            if (kind == TouchKind.Stop || (kind == TouchKind.FirstTouch && !hasInput))
            {
                // Stop the ball under the sole: it moves on with the player only.
                interval = 0.3f;
                v = loco.Velocity * 0.85f;
                Vector3 toFeet = P + facing * 0.45f - B;
                toFeet.y = 0f;
                v += Vector3.ClampMagnitude(toFeet * 2f, 1.2f);
            }
            else if (kind == TouchKind.Shield)
            {
                interval = 0.3f;
                Vector3 away = opp != null ? P - opp.transform.position : -facing;
                away.y = 0f;
                Vector3 keep = P + away.normalized * 0.5f - B;
                keep.y = 0f;
                v = loco.Velocity + Vector3.ClampMagnitude(keep * 2.5f, 2f);
            }
            else
            {
                interval = TouchInterval(kind, a, sprint) * (p.Model != null ? p.Model.Profile.touchRhythmScale : 1f); // v0.7 personal rhythm
                // Where the player will be when he meets the ball again (he may still be accelerating).
                float top = loco.TopSpeed(sprint, true) * Mathf.Clamp01(moveAmount);
                float cur = Vector3.Dot(loco.Velocity, want);
                float avg = Mathf.Min(top, Mathf.Max(0f, cur) + 0.5f * loco.MaxAccel * interval);
                if (kind == TouchKind.Sole) avg = Mathf.Min(avg, 1.5f);
                if (kind == TouchKind.Directional || kind == TouchKind.Inside || kind == TouchKind.Outside) avg = Mathf.Lerp(Mathf.Max(0f, cur), avg, 0.6f);
                Vector3 meet = P + want * (avg * interval + 0.3f + t.contactAhead);
                Vector3 d = meet - B;
                d.y = 0f;
                float speed = Ball.SpeedToRoll(d.magnitude, interval);
                if (kind == TouchKind.Sole) speed = Mathf.Min(speed, 4f);
                v = d.normalized * speed;

                if (kind == TouchKind.FirstTouch)
                {
                    // A heavy first touch: some of the incoming pace is left on the ball.
                    Vector3 incoming = ball.Velocity - loco.Velocity;
                    incoming.y = 0f;
                    float heavy = Rules.AttrLerp(a.BallControl, 0.35f, 0.06f) * Mathf.Clamp01(incoming.magnitude / 16f);
                    v += incoming * heavy;
                }
            }

            // Accuracy: BallControl, sprinting, agility on direction changes, weak foot.
            float errDeg = Rules.AttrLerp(a.BallControl, t.touchErrorDeg.x, t.touchErrorDeg.y);
            float errSpeed = Rules.AttrLerp(a.BallControl, t.touchSpeedError.x, t.touchSpeedError.y);
            if (sprint) errDeg *= 1.4f;
            if (kind == TouchKind.Directional || kind == TouchKind.Inside || kind == TouchKind.Outside || kind == TouchKind.Sole)
                errDeg *= Rules.AttrLerp(a.Agility, 1.6f, 1f);
            float weak = p.WeakFootFactor(pendingSide);
            errDeg *= weak;
            errSpeed *= weak;
            v = Quaternion.AngleAxis(Random.Range(-errDeg, errDeg), Vector3.up) * v;
            v *= 1f + Random.Range(-errSpeed, errSpeed);

            // Keep the dribble on the grass; a touch on a bouncing ball cushions it.
            v.y = ball.IsGrounded ? 0f : Mathf.Min(0f, ball.Velocity.y) * 0.3f;
            ball.Touch(v, p);

            LastKind = kind;
            LastTouchTime = now;
            lastSide = pendingSide;
            firstTouchPending = false;
            nextTouchTime = now + interval * 0.8f;
        }

        float TouchInterval(TouchKind kind, PlayerAttributes a, bool sprint)
        {
            DribbleTuning t = T;
            switch (kind)
            {
                case TouchKind.Sprint: return Rules.AttrLerp(a.BallControl, t.sprintInterval.x, t.sprintInterval.y);
                case TouchKind.CloseControl:
                case TouchKind.Sole:
                    return Rules.AttrLerp(a.BallControl, t.closeInterval.x, t.closeInterval.y);
                case TouchKind.Directional:
                case TouchKind.Inside:
                case TouchKind.Outside:
                    return Rules.AttrLerp(a.BallControl, t.normalInterval.x, t.normalInterval.y) * 0.8f;
                default:
                    float n = Rules.AttrLerp(a.BallControl, t.normalInterval.x, t.normalInterval.y);
                    return sprint ? Mathf.Max(n, t.sprintInterval.y) : n;
            }
        }

        /// <summary>Small help that keeps a rolling ball on the line the player is running (not a pull to the feet).</summary>
        void LineAssist(Ball ball, Vector3 moveDir, Vector3 ballDir, float dt)
        {
            Vector3 v = ball.Velocity;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            if (flat.magnitude < 0.5f || Vector3.Angle(ballDir, moveDir) > T.directionalAngle) return;
            Vector3 along = moveDir * Vector3.Dot(flat, moveDir);
            Vector3 lateral = flat - along;
            float k = Mathf.Min(1f, Rules.AttrLerp(p.attributes.BallControl, T.lineAssist.x, T.lineAssist.y) * dt);
            flat -= lateral * k;
            ball.Velocity = new Vector3(flat.x, v.y, flat.z);
        }
    }
}
