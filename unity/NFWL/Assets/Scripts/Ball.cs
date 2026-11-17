using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// The match ball: size 4 (radius 11cm, 0.40kg), bounce, friction and damping.
    /// Continuous collision so fast shots do not pass through posts or nets.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class Ball : MonoBehaviour
    {
        public float massKg = 0.40f;
        public float linearDamping = 0.15f;
        public float angularDamping = 0.6f;
        public float restitution = 0.55f;
        public float friction = 0.7f;

        public Rigidbody Body { get; private set; }
        public SphereCollider Sphere { get; private set; }
        public PlayerController LastTouchedBy { get; set; }

        public static Ball Create()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ball";
            // Unity's sphere is 1m wide with a 0.5m collider, so scaling keeps them in step.
            go.transform.localScale = Vector3.one * PitchDims.BallRadius * 2f;
            go.GetComponent<Renderer>().material.color = Color.white;
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

            PhysicsMaterial mat = new PhysicsMaterial("BallPhysMat")
            {
                bounciness = restitution,
                dynamicFriction = friction,
                staticFriction = friction,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Average
            };
            Sphere.sharedMaterial = mat;
        }

        public Vector3 Velocity
        {
            get => Body.linearVelocity;
            set => Body.linearVelocity = value;
        }

        public void Kick(Vector3 velocity, PlayerController kicker)
        {
            LastTouchedBy = kicker;
            Body.angularVelocity = Vector3.zero;
            Body.linearVelocity = velocity;
        }

        public void ResetBall(Vector3 position)
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = position;
            transform.position = position;
            LastTouchedBy = null;
        }
    }
}
