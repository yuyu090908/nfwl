using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// A footballer the user controls with the keyboard.
    /// Phase 1: move, sprint, dribble, short pass, long pass, shoot (with power gauge).
    /// Placeholder body (capsule) until real character art and animation.
    /// Ported from the Unreal prototype (NFWLPlayerCharacter.cpp), cm converted to m.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public PlayerAttributes attributes = new PlayerAttributes();

        [Tooltip("0 = HOME (attacks +X), 1 = AWAY (attacks -X)")]
        public int teamIndex = 0;

        [Header("Dribble (Full Spec 3.6 / 3.7)")]
        public float controlRadius = 0.9f;        // ball closer than this can be controlled
        public float loseRadius = 2.5f;           // ball further than this is lost
        public float maxTrapRelativeSpeed = 18f;  // faster balls bounce off instead of being trapped
        public float normalTouchDistance = 0.7f;  // 0.5-1.0m in front
        public float sprintTouchDistance = 1.9f;  // 1.5-2.5m in front

        [Header("Kick")]
        public float kickCooldown = 0.35f;
        public float inputBufferSeconds = 0.15f;  // Full Spec 3.4.3

        [Header("Keys")]
        public KeyCode sprintKey = KeyCode.LeftShift;
        public KeyCode shortPassKey = KeyCode.J;
        public KeyCode shootKey = KeyCode.K;
        public KeyCode longPassKey = KeyCode.L;

        public bool HasBall { get; private set; }
        public bool Sprinting { get; private set; }

        CharacterController controller;
        Ball ball;
        Vector2 moveInput;
        Vector3 velocity;
        float noControlUntil;

        bool charging;
        KickType chargeType = KickType.ShortPass;
        float chargeStartTime;

        // Buffered kick: pressed while not on the ball, executed if the ball arrives in time.
        bool kickBuffered;
        KickType bufferedType;
        float bufferedPower;
        bool bufferedTap;
        float bufferedUntil;

        float dashSpeed = 6.7f;
        float runSpeed = 4.7f;
        float maxAcceleration;
        float brakingDeceleration;
        float rotationRate;

        const float BodyRadius = 0.3f;
        const float BodyHeight = 1.8f;

        public static PlayerController Create(int team, Color color)
        {
            GameObject go = new GameObject(team == 0 ? "Player_HOME" : "Player_AWAY");
            CharacterController cc = go.AddComponent<CharacterController>();
            cc.radius = BodyRadius;
            cc.height = BodyHeight;
            cc.center = new Vector3(0f, BodyHeight * 0.5f, 0f);
            cc.stepOffset = 0.2f;

            // Body (visual only).
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, BodyHeight * 0.5f, 0f);
            body.transform.localScale = new Vector3(BodyRadius * 2f, BodyHeight * 0.5f, BodyRadius * 2f);
            body.GetComponent<Renderer>().material.color = color;

            // Nose that shows which way the player faces.
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingMarker";
            Destroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(go.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.4f, 0.3f);
            nose.transform.localScale = new Vector3(0.15f, 0.15f, 0.25f);
            nose.GetComponent<Renderer>().material.color = Color.white;

            PlayerController pc = go.AddComponent<PlayerController>();
            pc.teamIndex = team;
            return pc;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            ApplyAttributes();
        }

        public void SetBall(Ball b)
        {
            ball = b;
            // The ball is moved by code while dribbling, so the body must not block it.
            if (ball != null) Physics.IgnoreCollision(controller, ball.Sphere, true);
        }

        /// <summary>Recomputes movement values from attributes.</summary>
        public void ApplyAttributes()
        {
            dashSpeed = Rules.AttrLerp(attributes.Speed, 6.0f, 7.4f);              // 6.0-7.4 m/s
            runSpeed = dashSpeed * 0.7f;
            float accelTime = Rules.AttrLerp(attributes.Acceleration, 1.6f, 1.0f);  // seconds to top speed
            maxAcceleration = dashSpeed / accelTime;
            brakingDeceleration = dashSpeed / 0.3f;                                 // stop in ~0.2-0.4s
            rotationRate = Rules.AttrLerp(attributes.Agility, 420f, 720f);          // degrees per second
        }

        float CurrentMaxSpeed()
        {
            float speed = Sprinting ? dashSpeed : runSpeed;
            if (charging && chargeType == KickType.Shoot)
            {
                speed *= 0.7f; // charging a shot slows the player by 30% (Full Spec 3.6.2)
            }
            return speed;
        }

        // -------------------------------------------------------------------
        // Input and movement
        // -------------------------------------------------------------------

        void Update()
        {
            ReadInput();
            Move(Time.deltaTime);

            if (kickBuffered)
            {
                if (Time.time > bufferedUntil)
                {
                    kickBuffered = false;
                }
                else if (HasBall)
                {
                    kickBuffered = false;
                    PerformKick(bufferedType, bufferedPower, bufferedTap);
                }
            }
        }

        void ReadInput()
        {
            float x = 0f, y = 0f;
            if (KeyInput.Get(KeyCode.D) || KeyInput.Get(KeyCode.RightArrow)) x += 1f;
            if (KeyInput.Get(KeyCode.A) || KeyInput.Get(KeyCode.LeftArrow)) x -= 1f;
            if (KeyInput.Get(KeyCode.W) || KeyInput.Get(KeyCode.UpArrow)) y += 1f;
            if (KeyInput.Get(KeyCode.S) || KeyInput.Get(KeyCode.DownArrow)) y -= 1f;
            moveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);

            Sprinting = KeyInput.Get(sprintKey) || KeyInput.Get(KeyCode.RightShift);

            HandleKickKey(shortPassKey, KickType.ShortPass);
            HandleKickKey(longPassKey, KickType.LongPass);
            HandleKickKey(shootKey, KickType.Shoot);
        }

        void HandleKickKey(KeyCode key, KickType type)
        {
            if (KeyInput.Down(key)) BeginCharge(type);
            if (KeyInput.Up(key)) ReleaseCharge(type);
        }

        /// <summary>Directions are relative to the fixed side-on camera: W = +Z (up the screen), D = +X (right).</summary>
        Vector3 InputWorldDirection()
        {
            if (moveInput.sqrMagnitude < 0.04f) return Vector3.zero;
            return new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        }

        void Move(float dt)
        {
            Vector3 dir = InputWorldDirection();
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);

            if (dir != Vector3.zero)
            {
                Vector3 target = dir * CurrentMaxSpeed() * Mathf.Min(1f, moveInput.magnitude);
                horizontal = Vector3.MoveTowards(horizontal, target, maxAcceleration * dt);

                // Orient to the input direction at the agility-based turn rate.
                Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, rotationRate * dt);
            }
            else
            {
                horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, brakingDeceleration * dt);
            }

            velocity = horizontal;
            // Small downward push keeps the controller on the ground.
            controller.Move((horizontal + Vector3.down * 2f) * dt);

            // Do not run off into the void.
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, -PitchDims.HalfLength - 4f, PitchDims.HalfLength + 4f);
            p.z = Mathf.Clamp(p.z, -PitchDims.HalfWidth - 4f, PitchDims.HalfWidth + 4f);
            if (p != transform.position)
            {
                controller.enabled = false;
                transform.position = p;
                controller.enabled = true;
            }
        }

        // -------------------------------------------------------------------
        // Kicking
        // -------------------------------------------------------------------

        void BeginCharge(KickType type)
        {
            charging = true;
            chargeType = type;
            chargeStartTime = Time.time;
        }

        void ReleaseCharge(KickType type)
        {
            if (!charging || chargeType != type) return;
            float held = Time.time - chargeStartTime;
            charging = false;

            float power = Rules.HoldTimeToPower(held);
            bool tap = Rules.IsTap(held);

            if (HasBall)
            {
                PerformKick(type, power, tap);
            }
            else
            {
                // Keep the command for a short time, e.g. pass pressed during a trap (Full Spec 3.4.3).
                kickBuffered = true;
                bufferedType = type;
                bufferedPower = power;
                bufferedTap = tap;
                bufferedUntil = Time.time + inputBufferSeconds;
            }
        }

        /// <summary>Power gauge 0..1 while a kick key is held, otherwise -1.</summary>
        public float ChargeRatio => charging ? Rules.HoldTimeToPower(Time.time - chargeStartTime) : -1f;

        public string ChargeLabel
        {
            get
            {
                switch (chargeType)
                {
                    case KickType.ShortPass: return "PASS";
                    case KickType.LongPass: return "LONG PASS";
                    default: return "SHOT";
                }
            }
        }

        Vector3 KickDirection()
        {
            Vector3 input = InputWorldDirection();
            if (input != Vector3.zero) return input;
            Vector3 f = transform.forward;
            f.y = 0f;
            return f.normalized;
        }

        Vector3 AttackGoalCenter => new Vector3((teamIndex == 0 ? 1f : -1f) * PitchDims.HalfLength, 0f, 0f);

        void PerformKick(KickType type, float power, bool tap)
        {
            if (ball == null) return;

            Vector3 dir = KickDirection();
            float speed, elevationDeg, errorDeg;

            switch (type)
            {
                case KickType.ShortPass:
                {
                    // Phase 1 has no team-mates yet, so a tap uses a medium strength.
                    float p = tap ? 0.4f : power;
                    speed = Mathf.Lerp(7f, 23f, p);
                    elevationDeg = 0f; // along the ground
                    errorDeg = Rules.AttrLerp(attributes.ShortPass, 6f, 1f);
                    break;
                }
                case KickType.LongPass:
                {
                    float p = tap ? 0.5f : power;
                    speed = Mathf.Lerp(14f, 28f, p);
                    elevationDeg = Mathf.Lerp(25f, 38f, p);
                    errorDeg = Rules.AttrLerp(attributes.LongPass, 8f, 2f);
                    break;
                }
                default:
                {
                    float p = tap ? 0.3f : power;
                    speed = Mathf.Lerp(15f, 32f, p) * Rules.AttrLerp(attributes.ShotPower, 0.85f, 1.05f);
                    elevationDeg = Mathf.Lerp(2f, 16f, p * p); // over-hit shots rise (Full Spec 3.6.2)

                    // Light shot assist: pull towards the goal when roughly aiming at it.
                    Vector3 toGoal = AttackGoalCenter - transform.position;
                    toGoal.y = 0f;
                    toGoal.Normalize();
                    if (Vector3.Dot(dir, toGoal) > Mathf.Cos(50f * Mathf.Deg2Rad))
                    {
                        dir = (dir * 0.65f + toGoal * 0.35f).normalized;
                    }
                    errorDeg = Rules.AttrLerp(attributes.Finishing, 7f, 1.5f) * (0.4f + 0.6f * p);
                    break;
                }
            }

            if (Sprinting) errorDeg *= 1.3f;

            // Accuracy is a deviation of the aim, the result itself comes from physics (Full Spec 3.6.2).
            float yawError = Random.Range(-errorDeg, errorDeg);
            float pitchError = type == KickType.ShortPass ? 0f : Random.Range(-errorDeg, errorDeg) * 0.5f;
            dir = Quaternion.AngleAxis(yawError, Vector3.up) * dir;

            float elev = Mathf.Max(0f, elevationDeg + pitchError) * Mathf.Deg2Rad;
            Vector3 kickVelocity = dir * (speed * Mathf.Cos(elev)) + Vector3.up * (speed * Mathf.Sin(elev));

            ball.Kick(kickVelocity, this);
            ReleaseBall(kickCooldown);
        }

        /// <summary>Drops control of the ball and blocks re-taking it for cooldown seconds.</summary>
        public void ReleaseBall(float cooldown)
        {
            HasBall = false;
            kickBuffered = false;
            noControlUntil = Time.time + cooldown;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            velocity = Vector3.zero;
            charging = false;
        }

        // -------------------------------------------------------------------
        // Dribbling (physics step)
        // -------------------------------------------------------------------

        void FixedUpdate()
        {
            if (ball == null) return;

            if (Time.time < noControlUntil)
            {
                HasBall = false;
                return;
            }

            Vector3 ballPos = ball.Body.position;
            Vector3 ballVel = ball.Velocity;
            Vector3 flat = ballPos - transform.position;
            flat.y = 0f;
            float dist2D = flat.magnitude;

            if (!HasBall)
            {
                // Automatic first touch when the ball is close, low and not too fast (Full Spec 3.6.5).
                Vector3 rel = ballVel - velocity;
                rel.y = 0f;
                float trapLimit = maxTrapRelativeSpeed * Rules.AttrLerp(attributes.BallControl, 0.8f, 1.2f);
                if (dist2D < controlRadius && ballPos.y < 0.8f && rel.magnitude < trapLimit)
                {
                    HasBall = true;
                    ball.LastTouchedBy = this;
                }
            }
            else if (dist2D > loseRadius)
            {
                HasBall = false;
            }

            if (!HasBall) return;

            // Dribble: steer the ball towards a point in front of the feet.
            float touch = Sprinting ? sprintTouchDistance : normalTouchDistance;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 target = transform.position + forward * (BodyRadius + touch);
            target.y = ballPos.y;

            float gain = Rules.AttrLerp(attributes.BallControl, 6f, 12f);
            Vector3 desired = velocity + (target - ballPos) * gain;
            desired.y = ballVel.y;
            ball.Velocity = desired;
        }
    }
}
