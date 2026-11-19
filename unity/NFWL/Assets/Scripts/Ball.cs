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
        public float angularDamping = 0.6f;
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

        public static Ball Create()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ball";
            // Unity's sphere is 1m wide with a 0.5m collider, so scaling keeps them in step.
            go.transform.localScale = Vector3.one * PitchDims.BallRadius * 2f;
            // The real ball is only 22cm wide, tiny from the broadcast camera, so the visible ball
            // is drawn 1.6x bigger (physics keeps the real size).
            go.GetComponent<Renderer>().enabled = false;
            Visuals.Part(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 1.6f,
                Visuals.Mat(Color.white, 0.55f, Visuals.Ball), "BallVisual");
            go.AddComponent<Rigidbody>();
            return go.AddComponent<Ball>();
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
        public void Kick(Vector3 velocity, PlayerController kicker)
        {
            LastTouchedBy = kicker;
            LastTouchTime = Time.time;
            Owner = null;
            HeldInHands = false;
            // Spin around the axis perpendicular to the flight, so the patches roll visibly.
            Body.angularVelocity = Vector3.Cross(Vector3.up, velocity) / PitchDims.BallRadius;
            Body.linearVelocity = velocity;
        }

        /// <summary>A dribble touch: new velocity from a foot contact, the dribbler keeps possession.</summary>
        public void Touch(Vector3 velocity, PlayerController toucher)
        {
            LastTouchedBy = toucher;
            LastTouchTime = Time.time;
            Body.linearVelocity = velocity;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            Body.angularVelocity = Vector3.Cross(Vector3.up, flat) / PitchDims.BallRadius;
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
            v = ApplyForces(v, IsGrounded, dt, Tuning);
            Body.linearVelocity = v;

            if (IsGrounded)
            {
                // On the grass the ball rolls without slipping.
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                if (flat.sqrMagnitude > 0.0004f) Body.angularVelocity = Vector3.Cross(Vector3.up, flat) / PitchDims.BallRadius;
                else Body.angularVelocity *= 0.9f;
            }
        }

        /// <summary>Air drag always; rolling resistance while on the ground.</summary>
        static Vector3 ApplyForces(Vector3 v, bool grounded, float dt, BallTuning t)
        {
            float speed = v.magnitude;
            if (speed > 0.01f)
            {
                float drop = t.DragK * speed * speed * dt;
                v *= Mathf.Max(0f, 1f - drop / speed);
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
        public Vector3 PredictPosition(float t) => Simulate(Body.position, Body.linearVelocity, t, out _);

        /// <summary>Integrates the ball model from p, v for t seconds (gravity, bounce, drag, rolling).</summary>
        public static Vector3 Simulate(Vector3 p, Vector3 v, float t, out Vector3 vEnd)
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
                v = ApplyForces(v, grounded, dt, bt);
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
