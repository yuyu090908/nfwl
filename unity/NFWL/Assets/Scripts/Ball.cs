using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// The match ball: size 4 (radius 11cm, 0.40kg), bounce, rolling resistance and air drag.
    /// Continuous collision so fast shots do not pass through posts or nets.
    /// v0.6: the ball is always a free physics object. Players change its motion only through
    /// touches and kicks (Touch / Kick); nothing pins it to a player's feet.
    /// Forces are explicit (rolling resistance on the ground, quadratic air drag) so the same model
    /// can predict where the ball will be (Simulate), which touches, passes, the AI and the keeper use.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class Ball : MonoBehaviour
    {
        public float massKg = 0.40f;
        [Tooltip("v0.6: 0, drag and rolling resistance are applied explicitly (see NFWLTuning.ball)")]
        public float linearDamping = 0f;
        [Tooltip("v0.6: spin decay is handled explicitly (NFWLTuning.ball.spinDecay)")]
        public float angularDamping = 0.05f;
        public float friction = 0.7f;

        public Rigidbody Body { get; private set; }
        public SphereCollider Sphere { get; private set; }
        public PlayerController LastTouchedBy { get; set; }
        /// <summary>Time.time of the last touch or kick.</summary>
        public float LastTouchTime { get; private set; } = -10f;

        /// <summary>The player dribbling or holding the ball, or null when it is loose.</summary>
        public PlayerController Owner { get; set; }

        /// <summary>The keeper holds the ball in his hands (moved kinematically by him).</summary>
        public bool HeldInHands { get; set; }

        /// <summary>Raised on hard contacts: (what was hit, impact speed). Used for sound and effects.</summary>
        public event System.Action<string, float> Impact;
        /// <summary>v0.7: the ball hit a goal net (contact point, ball velocity before the hit).</summary>
        public static event System.Action<Vector3, Vector3> NetHit;

        /// <summary>Drawn size relative to the physical size: 1 (real, v0.7) or 1.6 (v0.6 readability boost).</summary>
        public static float VisualScale => NFWLFeatures.RealBallSize ? 1f : 1.6f;

        /// <summary>Applies the RealBallSize flag at once (graphics panel).</summary>
        public void RefreshVisualScale()
        {
            if (visual != null) visual.localScale = Vector3.one * VisualScale;
        }

        Transform squashPivot, visual;
        float squashUntil;
        Vector3 squashDir = Vector3.forward;
        float squashAmount;

        public static Ball Create()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ball";
            // Unity's sphere is 1m wide with a 0.5m collider, so scaling keeps them in step.
            go.transform.localScale = Vector3.one * PitchDims.BallRadius * 2f;
            // The real ball is only 22cm wide, tiny from the broadcast camera, so the visible ball
            // is drawn 1.6x bigger (physics keeps the real size).
            go.GetComponent<Renderer>().enabled = false;
            // Squash pivot: briefly flattens the ball along the impact direction (deformation visual).
            Transform pivot = Visuals.Pivot(go.transform, Vector3.zero, "BallSquash");
            // v0.7: with the RealBallSize flag (default) the ball is drawn at its real size; it stays easy
            // to follow through its contact shadow, the optional marker, the camera and the lighting.
            Material ballMat = NFWLShaders.Has(NFWLShaders.Lit)
                ? NFWLShaders.PBR(Color.white, 0.62f, 0f, ProceduralTextures.LeatherNormal, 0.35f, Visuals.Ball, null, new Color(0.04f, 0.04f, 0.045f))
                : Visuals.Mat(Color.white, 0.55f, Visuals.Ball);
            GameObject vis = Visuals.Part(PrimitiveType.Sphere, pivot, Vector3.zero, Vector3.one * VisualScale, ballMat, "BallVisual");
            go.AddComponent<Rigidbody>();
            Ball b = go.AddComponent<Ball>();
            b.squashPivot = pivot;
            b.visual = vis.transform;
            return b;
        }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Sphere = GetComponent<SphereCollider>();

            Body.mass = massKg;
            Body.linearDamping = linearDamping;
            Body.angularDamping = angularDamping;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 300f;

            PhysicsMaterial mat = new PhysicsMaterial("BallPhysMat")
            {
                bounciness = Tuning.restitution,
                dynamicFriction = friction,
                staticFriction = friction,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Average
            };
            Sphere.sharedMaterial = mat;
        }

        static BallTuning Tuning => NFWLTuning.Current.ball;

        public Vector3 Velocity
        {
            get => Body.linearVelocity;
            set => Body.linearVelocity = value;
        }

        public bool IsGrounded => Body.position.y < PitchDims.BallRadius + 0.03f && Mathf.Abs(Body.linearVelocity.y) < 0.6f;

        /// <summary>A kick (pass, shot, clearance): the ball leaves the player.</summary>
        public void Kick(Vector3 velocity, PlayerController kicker) => Kick(velocity, Vector3.zero, kicker);

        /// <summary>A kick with spin (rad/s, world axis): sidespin curls, topspin dips, backspin floats.</summary>
        public void Kick(Vector3 velocity, Vector3 spin, PlayerController kicker)
        {
            LastTouchedBy = kicker;
            LastTouchTime = Time.time;
            Owner = null;
            HeldInHands = false;
            Body.angularVelocity = spin;
            Body.linearVelocity = velocity;
            Squash(velocity, Mathf.Clamp01(velocity.magnitude / 30f));
            Impact?.Invoke("Kick", velocity.magnitude);
        }

        /// <summary>Spin of the ball (rad/s).</summary>
        public Vector3 Spin => Body.angularVelocity;

        void Squash(Vector3 dir, float amount)
        {
            if (dir.sqrMagnitude < 0.01f) return;
            squashDir = dir.normalized;
            squashAmount = Mathf.Clamp01(amount);
            squashUntil = Time.time + 0.07f;
        }

        void LateUpdate()
        {
            if (squashPivot == null) return;
            float w = Time.time < squashUntil ? squashAmount * ((squashUntil - Time.time) / 0.07f) : 0f;
            if (w <= 0.001f)
            {
                squashPivot.localRotation = Quaternion.identity;
                squashPivot.localScale = Vector3.one;
                visual.localRotation = Quaternion.identity;
                return;
            }
            // Flatten along the hit direction, bulge around it; the visual keeps the ball's rotation.
            squashPivot.rotation = Quaternion.LookRotation(squashDir);
            squashPivot.localScale = new Vector3(1f + 0.12f * w, 1f + 0.12f * w, 1f - 0.22f * w);
            visual.rotation = transform.rotation;
        }

        /// <summary>A dribble touch: new velocity from a foot contact, the dribbler keeps possession.</summary>
        public void Touch(Vector3 velocity, PlayerController toucher)
        {
            LastTouchedBy = toucher;
            LastTouchTime = Time.time;
            Body.linearVelocity = velocity;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            Body.angularVelocity = Vector3.Cross(Vector3.up, flat) / PitchDims.BallRadius;
            Squash(velocity, Mathf.Clamp01(velocity.magnitude / 25f) * 0.5f);
            Impact?.Invoke("Touch", velocity.magnitude);
        }

        public void ResetBall(Vector3 position)
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = position;
            transform.position = position;
            LastTouchedBy = null;
            Owner = null;
            HeldInHands = false;
        }

        void FixedUpdate()
        {
            if (HeldInHands) return;
            float dt = Time.fixedDeltaTime;
            Vector3 v = Body.linearVelocity;
            Vector3 w = Body.angularVelocity;
            bool grounded = IsGrounded;
            v = ApplyForces(v, w, grounded, dt, Tuning);
            Body.linearVelocity = v;
            if (!grounded) Body.angularVelocity = w * Mathf.Max(0f, 1f - Tuning.spinDecay * dt);

            if (grounded)
            {
                // On the grass the ball rolls without slipping.
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                if (flat.sqrMagnitude > 0.0004f) Body.angularVelocity = Vector3.Cross(Vector3.up, flat) / PitchDims.BallRadius;
                else Body.angularVelocity *= 0.9f;
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.contactCount == 0) return;
            Vector3 n = c.GetContact(0).normal;
            float impact = c.relativeVelocity.magnitude;
            string what = c.collider.name;
            BallTuning t = Tuning;
            Vector3 v = Body.linearVelocity;

            if (what == "NetCollider")
            {
                NetHit?.Invoke(c.GetContact(0).point, v); // v0.7: the visible net bulges where it was hit
                // The net gives: most of the pace is absorbed instead of bouncing back.
                Body.linearVelocity = v * t.netAbsorb;
                Body.angularVelocity *= 0.3f;
                Impact?.Invoke("Net", impact);
                return;
            }
            if (what == "Post" || what == "Crossbar")
            {
                // Hard woodwork: reflect off the frame (impact response).
                Vector3 inV = -c.relativeVelocity;
                Vector3 refl = Vector3.Reflect(inV, n) * t.postRestitution;
                Body.linearVelocity = refl;
                Squash(n, Mathf.Clamp01(impact / 25f));
                Impact?.Invoke("Post", impact);
                return;
            }
            if (n.y > 0.7f && impact > 1.5f)
            {
                // Bounce on the grass: spin grips the surface (backspin checks, topspin skids on).
                Vector3 contactVel = v + Vector3.Cross(Body.angularVelocity, -n * PitchDims.BallRadius);
                Vector3 slip = Vector3.ProjectOnPlane(contactVel, n);
                Body.linearVelocity = v - slip * t.bounceGrip;
                Squash(Vector3.down, Mathf.Clamp01(impact / 20f) * 0.6f);
                if (impact > 4f) Impact?.Invoke("Bounce", impact);
            }
        }

        /// <summary>Air drag always; Magnus (curve from spin) in the air; rolling resistance on the ground.</summary>
        static Vector3 ApplyForces(Vector3 v, Vector3 spin, bool grounded, float dt, BallTuning t)
        {
            float speed = v.magnitude;
            if (speed > 0.01f)
            {
                float drop = t.DragK * speed * speed * dt;
                v *= Mathf.Max(0f, 1f - drop / speed);
            }
            if (!grounded && spin.sqrMagnitude > 1f)
            {
                Vector3 magnus = Vector3.ClampMagnitude(t.magnusFactor * Vector3.Cross(spin, v), t.maxMagnusAccel);
                v += magnus * dt;
            }
            if (grounded)
            {
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                float fs = flat.magnitude;
                float dec = t.rollingDecel * dt;
                Vector3 newFlat = fs > dec ? flat * (1f - dec / fs) : Vector3.zero;
                v = new Vector3(newFlat.x, v.y, newFlat.z);
            }
            return v;
        }

        /// <summary>Where the ball will be after t seconds (no players, flat pitch). Same model as FixedUpdate.</summary>
        public Vector3 PredictPosition(float t) => Simulate(Body.position, Body.linearVelocity, Body.angularVelocity, t, out _);

        /// <summary>Integrates the ball model from p, v for t seconds (gravity, bounce, drag, rolling).</summary>
        public static Vector3 Simulate(Vector3 p, Vector3 v, float t, out Vector3 vEnd) => Simulate(p, v, Vector3.zero, t, out vEnd);

        /// <summary>Integrates the ball model with spin.</summary>
        public static Vector3 Simulate(Vector3 p, Vector3 v, Vector3 spin, float t, out Vector3 vEnd)
        {
            BallTuning bt = Tuning;
            const float step = 0.02f;
            float r = PitchDims.BallRadius;
            float g = Physics.gravity.y;
            float time = 0f;
            while (time < t - 1e-5f)
            {
                float dt = Mathf.Min(step, t - time);
                bool grounded = p.y < r + 0.03f && Mathf.Abs(v.y) < 0.6f;
                if (!grounded) v.y += g * dt;
                v = ApplyForces(v, spin, grounded, dt, bt);
                if (grounded) spin = Vector3.zero;
                else spin *= Mathf.Max(0f, 1f - bt.spinDecay * dt);
                p += v * dt;
                if (p.y < r)
                {
                    p.y = r;
                    v.y = v.y < -0.6f ? -v.y * bt.restitution : 0f;
                }
                time += dt;
            }
            vEnd = v;
            return p;
        }

        /// <summary>
        /// Ground speed to roll the ball over 'distance' in 'time' seconds (on the grass), or the
        /// speed it should leave with to arrive there. Solved against the same model as Simulate.
        /// </summary>
        public static float SpeedToRoll(float distance, float time)
        {
            if (time <= 0.01f || distance <= 0f) return 0f;
            float guess = distance / time + 0.5f * Tuning.rollingDecel * time;
            Vector3 start = new Vector3(0f, PitchDims.BallRadius, 0f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 end = Simulate(start, new Vector3(guess, 0f, 0f), time, out Vector3 vEnd);
                float travelled = end.x;
                if (vEnd.x <= 0.01f && travelled < distance)
                {
                    // Stopped short: needs more pace.
                    guess *= 1.25f;
                    continue;
                }
                if (travelled < 0.01f) break;
                guess *= distance / travelled;
            }
            return guess;
        }
    }
}
