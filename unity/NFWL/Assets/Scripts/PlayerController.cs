using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// One footballer. Either the user drives it (keyboard) or a simple rule-based AI does
    /// (Master Doc 14.3: rule-based AI first). Movement, dribbling, passing and shooting
    /// follow the Unreal prototype (NFWLPlayerCharacter.cpp), cm converted to m.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public PlayerAttributes attributes = new PlayerAttributes();

        [Tooltip("0 = HOME (attacks +X), 1 = AWAY (attacks -X)")]
        public int teamIndex = 0;
        public Role role = Role.MF;
        public int number = 10;
        public string displayName = "PLAYER";
        public Rarity rarity = Rarity.Common;
        public Element element;

        /// <summary>Formation spot, written for a team that attacks +X (mirrored for AWAY).</summary>
        public Vector2 formationSpot;

        [Header("Dribble (Full Spec 3.6 / 3.7)")]
        public float controlRadius = 0.9f;
        public float maxTrapRelativeSpeed = 18f;
        public float tackleRadius = 1.0f;

        [Header("Kick")]
        public float kickCooldown = 0.35f;
        public float inputBufferSeconds = 0.15f;

        [Header("Keys")]
        public KeyCode sprintKey = KeyCode.LeftShift;
        public KeyCode shortPassKey = KeyCode.J;
        public KeyCode shootKey = KeyCode.K;
        public KeyCode longPassKey = KeyCode.L;

        public bool IsHuman { get; set; }
        public bool HasBall { get; private set; }
        public bool Sprinting { get; private set; }
        public float Speed => loco.Speed;
        public Vector3 Velocity => loco.Velocity;
        /// <summary>Movement with momentum, body facing and balance (v0.6 PHASE 1).</summary>
        public PlayerLocomotion Locomotion => loco;
        public PlayerModel Model { get; private set; }
        public MatchManager Match { get; set; }
        public float AttackSign => teamIndex == 0 ? 1f : -1f;
        /// <summary>Touch dribbling (v0.6 PHASE 2).</summary>
        public DribbleController Dribble => dribble;
        /// <summary>Kick timeline (v0.6 PHASE 3).</summary>
        public KickController Kick => kick;

        /// <summary>Error multiplier for a foot (side -1 left, +1 right): 1 on the preferred foot, more on the weak foot.</summary>
        public float WeakFootFactor(float side)
        {
            bool preferred = (side < 0f) == (attributes.PreferredFoot == PreferredFoot.Left);
            return preferred ? 1f : Mathf.Lerp(1.9f, 1f, (Mathf.Clamp(attributes.WeakFootRating, 1, 5) - 1) / 4f);
        }

        CharacterController controller;
        Ball ball;
        Vector2 moveInput;
        readonly PlayerLocomotion loco = new PlayerLocomotion();
        DribbleController dribble;
        KickController kick;
        // Kick waiting for its contact (decided when the kick starts, struck at contact).
        KickRequest strike;
        float chestCooldownUntil;
        Vector3 runTarget;
        float runTargetUntil;

        /// <summary>Time of the last ball deflection off this player's body (BallContacts).</summary>
        public float LastBodyContactTime { get; set; } = -10f;

        /// <summary>The ball bounced off this player's legs, torso or head.</summary>
        public void OnBallHitBody(string part, float speed)
        {
            if (Model != null && part == "Torso") Model.PlayChest();
            if (speed > 12f) loco.LoseBalance(Mathf.Clamp01(speed / 60f));
            if (Match != null) Match.OnBodyContact(this, part, speed);
        }

        /// <summary>Name of the last kick (e.g. FINESSE SHOT, THROUGH PASS), for the HUD.</summary>
        public string LastKickLabel { get; private set; } = "";
        public float LastKickTime { get; private set; } = -10f;
        float noControlUntil;
        float saveCooldownUntil;

        bool charging;
        KickType chargeType = KickType.ShortPass;
        KickVariant chargeVariant = KickVariant.Normal;
        float chargeStartTime;

        bool kickBuffered;
        KickType bufferedType;
        KickVariant bufferedVariant;
        float bufferedPower;
        bool bufferedTap;
        Vector3? bufferedAim;
        float bufferedUntil;

        // AI state
        float aiNextDecision;
        float hadBallSince;
        bool aiSprint;

        const float BodyRadius = 0.3f;
        const float BodyHeight = 1.8f;

        public static PlayerController Create(int team, Role role, int number, string name, Rarity rarity,
            PlayerAttributes attrs, KitStyle kit, Element element, int hairStyle, float heightScale)
        {
            GameObject go = new GameObject($"{(team == 0 ? "HOME" : "AWAY")}_{number}_{name}");
            CharacterController cc = go.AddComponent<CharacterController>();
            cc.radius = BodyRadius;
            cc.height = BodyHeight;
            cc.center = new Vector3(0f, BodyHeight * 0.5f, 0f);
            cc.stepOffset = 0.2f;

            PlayerController pc = go.AddComponent<PlayerController>();
            pc.teamIndex = team;
            pc.role = role;
            pc.number = number;
            pc.displayName = name;
            pc.rarity = rarity;
            pc.attributes = attrs;
            if (role == Role.GK)
            {
                pc.controlRadius = 1.3f;
                pc.maxTrapRelativeSpeed = 24f;
            }
            pc.ApplyAttributes();
            pc.element = element;
            pc.Model = PlayerModel.Build(pc, kit, element, number, hairStyle, heightScale);
            return pc;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            dribble = new DribbleController(this);
            kick = new KickController(this);
            ApplyAttributes();
        }

        public void SetBall(Ball b)
        {
            ball = b;
            // The ball is moved by code while dribbling, so the body must not block it.
            if (ball != null) Physics.IgnoreCollision(controller, ball.Sphere, true);
        }

        public void ApplyAttributes()
        {
            // Speed / Acceleration / Agility drive the locomotion (values in NFWLTuning, v0.5 baseline).
            loco.Configure(attributes, NFWLTuning.Current.locomotion);
        }

        // -------------------------------------------------------------------
        // Per frame
        // -------------------------------------------------------------------

        void Update()
        {
            bool frozen = Match != null && Match.PlayersFrozen;
            if (frozen)
            {
                moveInput = Vector2.zero;
                Sprinting = false;
            }
            else if (IsHuman)
            {
                ReadInput();
            }
            else
            {
                if (charging) charging = false;
                ThinkAI();
            }
            kick.Tick(Time.time);
            Move(Time.deltaTime);

            if (kickBuffered)
            {
                if (Time.time > bufferedUntil) kickBuffered = false;
                else if (HasBall && BallInKickReach())
                {
                    kickBuffered = false;
                    PerformKick(bufferedType, bufferedPower, bufferedTap, bufferedAim, bufferedVariant);
                }
                else if (!HasBall && TryFirstTime(bufferedType, bufferedPower, bufferedTap, bufferedAim, bufferedVariant))
                {
                    kickBuffered = false;
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
            if (charging && chargeType == type) chargeVariant = ReadVariant(type);
            if (KeyInput.Up(key)) ReleaseCharge(type);
        }

        /// <summary>
        /// Modifier keys (configurable in NFWLTuning.input). Shift is the sprint key, so it only means
        /// "power shot" when it was pressed just before or during the K press, not while already sprinting.
        /// </summary>
        KickVariant ReadVariant(KickType type)
        {
            InputTuning it = NFWLTuning.Current.input;
            bool alt = KeyInput.Get(it.finesseModifier) || KeyInput.Get(KeyCode.RightAlt);
            switch (type)
            {
                case KickType.ShortPass: return alt ? KickVariant.Through : KickVariant.Normal;
                case KickType.LongPass: return alt ? KickVariant.LoftedThrough : KickVariant.Normal;
            }
            if (alt) return KickVariant.Finesse;
            if (KeyInput.Get(it.lowModifier) || KeyInput.Get(KeyCode.RightControl)) return KickVariant.LowDriven;
            bool shift = KeyInput.Get(it.powerModifier) || KeyInput.Get(KeyCode.RightShift);
            if (shift && KeyInput.ShiftDownTime >= chargeStartTime - it.powerShiftWindow) return KickVariant.Power;
            return KickVariant.Normal;
        }

        /// <summary>Called when the user takes over another player.</summary>
        public void OnControlChanged()
        {
            charging = false;
            kickBuffered = false;
            moveInput = Vector2.zero;
        }

        /// <summary>W = +Z (up the screen), D = +X (right), matching the side-on camera.</summary>
        Vector3 InputWorldDirection()
        {
            if (moveInput.sqrMagnitude < 0.04f) return Vector3.zero;
            return new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        }

        void Move(float dt)
        {
            // In the Editor, pick up tuning edits made in the Inspector during Play.
            if (Application.isEditor && Time.frameCount % 60 == 0) ApplyAttributes();
            Vector3 dir = InputWorldDirection();
            LocomotionTuning lt = NFWLTuning.Current.locomotion;

            LocoInput input = new LocoInput
            {
                moveDir = dir,
                moveAmount = Mathf.Min(1f, moveInput.magnitude),
                sprint = Sprinting,
                hasBall = HasBall,
                speedScale = (charging && chargeType == KickType.Shoot ? 0.7f : 1f) // Full Spec 3.6.2
                             * kick.SpeedScale(Time.time)
            };

            PlayerController carrier = ball != null ? ball.Owner : null;
            if (kick.Active && Time.time < kick.FollowEndTime)
            {
                // Square up to the target while striking (the body turns at its normal rate, no snap).
                input.faceDir = kick.Direction;
            }
            else if (HasBall && dribble.Shielding && Match != null)
            {
                // Shielding: keep the body between the opponent and the ball (back to him).
                PlayerController opp = Match.NearestOpponent(this, out _);
                if (opp != null)
                {
                    Vector3 away = transform.position - opp.transform.position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 0.01f) input.faceDir = away;
                }
            }
            else if (!HasBall && role != Role.GK && carrier != null && carrier.teamIndex != teamIndex && !Sprinting)
            {
                // Defensive stance: face the ball carrier and shuffle instead of turning away.
                Vector3 toCarrier = carrier.transform.position - transform.position;
                toCarrier.y = 0f;
                if (toCarrier.magnitude < lt.jockeyDistance)
                {
                    input.jockey = true;
                    input.faceDir = toCarrier;
                }
            }
            else if (dir == Vector3.zero && !HasBall && ball != null && loco.Speed < 0.7f)
            {
                // Idle players turn to watch the ball.
                Vector3 toBall = ball.transform.position - transform.position;
                toBall.y = 0f;
                if (toBall.sqrMagnitude > 0.25f) input.faceDir = toBall;
            }

            loco.Tick(input, dt);

            Vector3 before = transform.position;
            controller.Move((loco.Velocity + Vector3.down * 2f) * dt);
            // Use the real displacement, so bumping into someone slows you down.
            if (dt > 0f) loco.SetActualVelocity((transform.position - before) / dt);
            transform.rotation = Quaternion.Euler(0f, loco.FacingYaw, 0f);

            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, -PitchDims.HalfLength - 2.5f, PitchDims.HalfLength + 2.5f);
            p.z = Mathf.Clamp(p.z, -PitchDims.HalfWidth - 2.5f, PitchDims.HalfWidth + 2.5f);
            if (p.y < -0.5f || p.y > 0.5f) p.y = 0f;
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
            chargeVariant = KickVariant.Normal;
            chargeStartTime = Time.unscaledTime;
        }

        void ReleaseCharge(KickType type)
        {
            if (!charging || chargeType != type) return;
            float held = Time.unscaledTime - chargeStartTime;
            charging = false;

            float power = Rules.HoldTimeToPower(held);
            bool tap = Rules.IsTap(held);

            RequestKick(type, power, tap, null, chargeVariant);
        }

        /// <summary>
        /// Kicks now if the ball is at the feet, otherwise waits (the player runs onto his dribble,
        /// or the ball arrives) for a short time. The ball never leaves before a foot reaches it.
        /// </summary>
        void RequestKick(KickType type, float power, bool tap, Vector3? aim, KickVariant variant = KickVariant.Normal)
        {
            if (HasBall && BallInKickReach())
            {
                PerformKick(type, power, tap, aim, variant);
                return;
            }
            if (!HasBall && TryFirstTime(type, power, tap, aim, variant)) return;
            kickBuffered = true;
            bufferedType = type;
            bufferedVariant = variant;
            bufferedPower = power;
            bufferedTap = tap;
            bufferedAim = aim;
            bufferedUntil = Time.time + (HasBall ? 0.8f : inputBufferSeconds);
        }

        bool BallInKickReach()
        {
            if (ball == null) return false;
            if (ball.HeldInHands && ball.Owner == this) return true; // keeper drop kick
            Vector3 rel = ball.Body.position - transform.position;
            float y = rel.y;
            rel.y = 0f;
            return rel.magnitude < 1.0f && Vector3.Dot(rel, loco.Facing) > -0.3f && y < 1.0f;
        }

        public float ChargeRatio => charging ? Rules.HoldTimeToPower(Time.unscaledTime - chargeStartTime) : -1f;

        public string ChargeLabel
        {
            get
            {
                switch (chargeType)
                {
                    case KickType.ShortPass: return chargeVariant == KickVariant.Through ? "THROUGH PASS" : "PASS";
                    case KickType.LongPass: return chargeVariant == KickVariant.LoftedThrough ? "LOFTED THROUGH" : "LONG PASS";
                    default:
                        switch (chargeVariant)
                        {
                            case KickVariant.Power: return "POWER SHOT";
                            case KickVariant.Finesse: return "FINESSE SHOT";
                            case KickVariant.LowDriven: return "LOW DRIVEN SHOT";
                            default: return "SHOT";
                        }
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

        public Vector3 AttackGoalCenter => new Vector3(AttackSign * PitchDims.HalfLength, 0f, 0f);
        public Vector3 OwnGoalCenter => new Vector3(-AttackSign * PitchDims.HalfLength, 0f, 0f);

        /// <summary>Pass assist: the team-mate closest to the aim line, within a cone.</summary>
        public PlayerController FindPassTarget(Vector3 dir, float maxAngle, float maxDist)
        {
            if (Match == null) return null;
            PlayerController best = null;
            float bestScore = float.MaxValue;
            foreach (PlayerController mate in Match.Team(teamIndex))
            {
                if (mate == this) continue;
                Vector3 to = mate.transform.position - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist < 2f || dist > maxDist) continue;
                float angle = Vector3.Angle(dir, to);
                if (angle > maxAngle) continue;
                float score = angle * 0.15f + dist * 0.05f;
                if (score < bestScore) { bestScore = score; best = mate; }
            }
            return best;
        }

        /// <summary>
        /// Starts a kick (PHASE 3): picks the foot and the form and runs the kick timeline.
        /// The ball is struck later, in OnKickContact, when the foot (or head) reaches it.
        /// aimOverride is used by the AI (direction on the ground).
        /// contactAt &gt; 0 fits the timeline to a ball that is still arriving (first time / volley / header).
        /// </summary>
        void PerformKick(KickType type, float power, bool tap, Vector3? aimOverride, KickVariant variant = KickVariant.Normal,
            bool firstTime = false, bool header = false, float contactAt = -1f)
        {
            if (ball == null || kick.Active) return;
            if (!HasBall && !firstTime) return;

            Vector3 inputDir = KickDirection();
            Vector3 dir = aimOverride ?? inputDir;
            dir.y = 0f;
            if (type == KickType.Shoot)
            {
                Vector3 toGoal = AttackGoalCenter - transform.position;
                toGoal.y = 0f;
                if (aimOverride == null && Vector3.Dot(dir.normalized, toGoal.normalized) > 0.64f) dir = dir.normalized * 0.65f + toGoal.normalized * 0.35f;
            }
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : loco.Facing;

            // Foot: the preferred foot, unless the ball sits clearly on the other side of the body.
            Vector3 right = new Vector3(dir.z, 0f, -dir.x);
            Vector3 rel = ball.Body.position - transform.position;
            float lx = Vector3.Dot(rel, right);
            float pref = attributes.PreferredFoot == PreferredFoot.Left ? -1f : 1f;
            float side = lx * pref < -0.12f ? -pref : pref;

            float p = tap ? (type == KickType.Shoot ? 0.3f : 0.4f) : power;
            KickController.Form form;
            if (header)
            {
                form = new KickController.Form { header = true, power = p };
            }
            else switch (type)
            {
                case KickType.ShortPass:
                    // Side-foot pass: short backswing, accuracy first.
                    form = new KickController.Form { style = FootStyle.Inside, backswing = 0.16f + 0.14f * p, follow = 0.25f, followHeight = 0.08f, loft = 0.1f, power = p * 0.6f };
                    if (variant == KickVariant.Through) form.backswing += 0.05f;
                    break;
                case KickType.LongPass:
                    // Lofted: big backswing, body leans back, foot gets under the ball.
                    form = new KickController.Form { style = FootStyle.Laces, backswing = 0.35f + 0.15f * p, follow = 0.45f, followHeight = 0.5f, loft = 0.9f, power = 0.5f + 0.5f * p };
                    break;
                default:
                    switch (variant)
                    {
                        case KickVariant.Power:
                            form = new KickController.Form { style = FootStyle.Laces, backswing = 0.5f + 0.25f * p, follow = 0.55f + 0.2f * p, followHeight = 0.45f + 0.25f * p, loft = 0.2f + 0.4f * p, power = Mathf.Min(1f, p + 0.25f) };
                            break;
                        case KickVariant.Finesse:
                            // Inside of the foot wrapped round the ball, follow through across the body.
                            form = new KickController.Form { style = FootStyle.Inside, backswing = 0.3f + 0.15f * p, follow = 0.45f, followHeight = 0.35f, loft = 0.35f, power = p * 0.8f };
                            break;
                        case KickVariant.LowDriven:
                            form = new KickController.Form { style = FootStyle.Laces, backswing = 0.3f + 0.2f * p, follow = 0.3f, followHeight = 0.15f, loft = 0f, power = p };
                            break;
                        default:
                            form = new KickController.Form { style = FootStyle.Laces, backswing = 0.3f + 0.25f * p, follow = 0.4f + 0.2f * p, followHeight = 0.3f + 0.25f * p, loft = p * p * 0.6f, power = p };
                            break;
                    }
                    break;
            }

            if (!kick.Begin(ball, dir, side, form, Time.time, contactAt)) return;
            strike = new KickRequest
            {
                type = type, variant = variant, power = power, tap = tap, aim = aimOverride,
                inputDir = inputDir, side = side, firstTime = firstTime, header = header
            };
        }

        /// <summary>
        /// First time kick / volley / header: if a loose ball (or a pass) will reach this player soon,
        /// start the kick now so the foot or head meets it on arrival.
        /// </summary>
        bool TryFirstTime(KickType type, float power, bool tap, Vector3? aim, KickVariant variant)
        {
            if (ball == null || kick.Active || role == Role.GK) return false;
            if (ball.Owner != null && ball.Owner != this) return false;
            if (ball.HeldInHands) return false;
            for (float t = 0.1f; t <= 0.7f; t += 0.03f)
            {
                Vector3 bp = ball.PredictPosition(t);
                Vector3 me = transform.position + loco.Velocity * (t * 0.7f);
                Vector3 d = bp - me;
                d.y = 0f;
                if (d.magnitude < 0.7f && bp.y < 2.3f)
                {
                    bool header = bp.y > 1.25f;
                    if (!header && bp.y > 1.0f) return false; // chest height: control it instead
                    PerformKick(type, power, tap, aim, variant, true, header, Time.time + t);
                    return kick.Active;
                }
            }
            return false;
        }

        /// <summary>The kicking foot (or head) reaches the ball: now the ball leaves (PHASE 3.1 contact sync).</summary>
        void OnKickContact()
        {
            if (ball == null) return;
            if (!HasBall && !strike.firstTime) return; // lost the ball during the backswing: a fresh-air kick
            if (strike.firstTime && ball.Owner != null && ball.Owner != this) return;
            Vector3 rel = ball.Body.position - transform.position;
            float y = rel.y;
            rel.y = 0f;
            if (role == Role.GK && ball.HeldInHands)
            {
                // Keeper: the ball was dropped from the hands onto the foot (drop kick).
                ball.HeldInHands = false;
            }
            else if (rel.magnitude > (strike.firstTime ? 1.0f : 1.2f) || y > (strike.header ? 2.4f : 1.3f))
            {
                return; // the ball is not at the foot: no contact
            }
            KickResult res = KickPlanner.Plan(this, ball, strike);
            ball.Kick(res.velocity, res.spin, this);
            LastKickLabel = res.label;
            LastKickTime = Time.time;
            if (Match != null) Match.ShowKickLabel(this, res.label);
            // The intended receiver moves to meet the ball (AI players; the user steers his own).
            if (res.receiver != null && !res.receiver.IsHuman && res.receiveTime > 0f)
                res.receiver.CallRun(res.receivePoint, Time.time + res.receiveTime + 0.4f);
            if (HasBall) ReleaseBall(kickCooldown);
            else noControlUntil = Time.time + kickCooldown;
        }

        /// <summary>Through ball: the AI runner goes to target until the given time.</summary>
        public void CallRun(Vector3 target, float until)
        {
            runTarget = target;
            runTargetUntil = until;
        }

        // -------------------------------------------------------------------
        // Ball possession
        // -------------------------------------------------------------------

        void TakeBall()
        {
            bool wasLoose = ball.Owner == null;
            if (ball.Owner != null && ball.Owner != this) ball.Owner.ReleaseBall(0.6f);
            HasBall = true;
            dribble.OnGainPossession(wasLoose);
            ball.Owner = this;
            ball.LastTouchedBy = this;
            hadBallSince = Time.time;
            aiNextDecision = Time.time + Random.Range(0.25f, 0.6f);
            if (Match != null) Match.OnPossession(this);
        }

        /// <summary>Drops control of the ball and blocks re-taking it for cooldown seconds.</summary>
        public void ReleaseBall(float cooldown)
        {
            HasBall = false;
            kickBuffered = false;
            if (dribble != null && dribble.TouchPending && Model != null) Model.CancelFootAction();
            dribble?.Cancel();
            if (ball != null && ball.Owner == this) { ball.Owner = null; ball.HeldInHands = false; }
            noControlUntil = Time.time + cooldown;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            loco.Reset(rotation * Vector3.forward);
            kick.Cancel();
            if (Model != null) Model.CancelAllActions();
            charging = false;
            kickBuffered = false;
        }

        void FixedUpdate()
        {
            if (ball == null) return;
            if (Match != null && Match.PlayersFrozen) return;

            // Kick timeline: aim the foot at the moving ball and strike it in the contact step.
            if (kick.BeforeContact)
            {
                kick.TrackBall(ball, Time.time);
                if (kick.ContactDue(Time.time)) OnKickContact();
                if (HasBall || strike.firstTime) return; // no touches or traps while winding up
            }

            if (Time.time < noControlUntil)
            {
                if (HasBall) ReleaseBall(0f);
                return;
            }

            Vector3 ballPos = ball.Body.position;
            Vector3 ballVel = ball.Velocity;
            Vector3 flat = ballPos - transform.position;
            flat.y = 0f;
            float dist2D = flat.magnitude;

            if (!HasBall)
            {
                PlayerController owner = ball.Owner;
                if (owner != null)
                {
                    // Tackle: an opponent close to the ball can win it. Goalkeepers holding the ball are safe.
                    if (owner.teamIndex != teamIndex && owner.role != Role.GK && dist2D < tackleRadius && ballPos.y < 0.8f)
                    {
                        float rate = Rules.AttrLerp(attributes.Tackling, 0.9f, 2.6f)
                                     / Rules.AttrLerp(owner.attributes.BallControl, 0.8f, 1.4f);
                        if (owner.Sprinting) rate *= 1.4f;
                        if (Random.value < rate * Time.fixedDeltaTime) TakeBall();
                    }
                }
                else
                {
                    Vector3 rel = ballVel - loco.Velocity;
                    rel.y = 0f;
                    float trapLimit = maxTrapRelativeSpeed * Rules.AttrLerp(attributes.BallControl, 0.8f, 1.2f);
                    float maxHeight = role == Role.GK ? 2.3f : 0.8f;
                    if (role != Role.GK && dist2D < 0.7f && ballPos.y >= 0.8f && ballPos.y <= 1.6f
                        && rel.magnitude < trapLimit && Time.time > chestCooldownUntil && !kick.Active)
                    {
                        // Chest control: cushion the ball and drop it to the feet.
                        chestCooldownUntil = Time.time + 0.5f;
                        Vector3 cushion = loco.Velocity * 0.7f + loco.Facing * 0.7f + Vector3.down * 1.5f;
                        cushion *= Rules.AttrLerp(attributes.BallControl, 1.25f, 1f);
                        ball.Touch(cushion, this);
                        if (Model != null) Model.PlayChest();
                        LastKickLabel = "CHEST CONTROL";
                        LastKickTime = Time.time;
                    }
                    else if (dist2D < controlRadius && ballPos.y < maxHeight)
                    {
                        if (rel.magnitude < trapLimit) TakeBall();
                        else if (role == Role.GK) TrySave(ballPos, ballVel);
                    }
                    else if (role == Role.GK && dist2D < 2.4f && ballPos.y < 2.4f)
                    {
                        TrySave(ballPos, ballVel);
                    }
                }
            }
            else if (dist2D > NFWLTuning.Current.dribble.loseDistance || ball.Owner != this)
            {
                ReleaseBall(0f);
            }

            if (!HasBall) return;

            if (role == Role.GK && !IsHuman)
            {
                // Keeper holds the ball in his hands.
                Vector3 hands = transform.position + transform.forward * 0.45f + Vector3.up * 1.1f;
                ball.HeldInHands = true;
                ball.Body.position = hands;
                ball.Velocity = loco.Velocity;
                return;
            }

            // Touch dribbling: the ball is only moved by foot contacts (plus a small line assist).
            // While a kick waits for the ball, no new touch pushes it away.
            if (!(kickBuffered && HasBall) && !kick.Active)
                dribble.FixedTick(ball, InputWorldDirection(), Mathf.Min(1f, moveInput.magnitude), Sprinting, Time.time);
        }

        /// <summary>Goalkeeper: a dive that may push a fast shot away.</summary>
        void TrySave(Vector3 ballPos, Vector3 ballVel)
        {
            if (Time.time < saveCooldownUntil) return;
            // Only shots heading towards our own goal.
            if (Mathf.Sign(ballVel.x) != -AttackSign || Mathf.Abs(ballVel.x) < 4f) return;
            saveCooldownUntil = Time.time + 0.8f;

            float side = Vector3.Dot(ballPos - transform.position, transform.right);
            if (Model != null) Model.PlayDive(side);

            float chance = Rules.AttrLerp(attributes.Goalkeeping, 0.35f, 0.8f);
            if (Random.value < chance)
            {
                Vector3 v = new Vector3(-ballVel.x * 0.35f, Mathf.Abs(ballVel.y) * 0.3f + 2.5f, ballVel.z * 0.4f + Random.Range(-4f, 4f));
                ball.Kick(v, this);
                noControlUntil = Time.time + 0.4f;
                if (Match != null) Match.ShowEvent("SAVE!", 1.0f);
            }
        }

        // -------------------------------------------------------------------
        // AI (simple rules, Master Doc 14.1 / 14.2)
        // -------------------------------------------------------------------

        void MoveTowards(Vector3 target, bool sprint)
        {
            Vector3 d = target - transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.35f)
            {
                moveInput = Vector2.zero;
                Sprinting = false;
                return;
            }
            Vector3 n = d / dist;
            float mag = Mathf.Clamp(dist / 1.5f, 0.3f, 1f);
            moveInput = new Vector2(n.x, n.z) * mag;
            Sprinting = sprint && dist > 3f;
        }

        void ThinkAI()
        {
            if (ball == null || Match == null) { moveInput = Vector2.zero; return; }
            Vector3 ballP = ball.transform.position;

            if (role == Role.GK) { ThinkKeeper(ballP); return; }
            if (HasBall) { ThinkWithBall(); return; }

            if (Time.time < runTargetUntil && ball.Owner == null)
            {
                // Called onto a through ball: sprint to where it will be.
                MoveTowards(runTarget, true);
                return;
            }
            bool teamHasBall = ball.Owner != null && ball.Owner.teamIndex == teamIndex;
            if (!teamHasBall && Match.ChaserFor(teamIndex) == this)
            {
                Vector3 target = ballP + ball.Velocity * 0.25f;
                MoveTowards(target, true);
                return;
            }
            MoveTowards(FormationTarget(ballP, teamHasBall), false);
        }

        Vector3 FormationTarget(Vector3 ballP, bool teamHasBall)
        {
            float s = AttackSign;
            float shift = Mathf.Clamp(ballP.x * 0.5f, -9f, 9f);
            float push = teamHasBall ? 3f * s : -1.5f * s;
            if (role == Role.FW && teamHasBall) push += 3f * s;
            float x = formationSpot.x * s + shift + push;
            float z = formationSpot.y + ballP.z * 0.3f;
            x = Mathf.Clamp(x, -PitchDims.HalfLength + 2f, PitchDims.HalfLength - 2f);
            z = Mathf.Clamp(z, -PitchDims.HalfWidth + 1.5f, PitchDims.HalfWidth - 1.5f);
            return new Vector3(x, 0f, z);
        }

        void ThinkKeeper(Vector3 ballP)
        {
            float s = AttackSign;
            float lineX = -s * PitchDims.HalfLength;
            if (HasBall)
            {
                moveInput = Vector2.zero;
                if (Time.time - hadBallSince > 1.0f)
                {
                    PlayerController mate = Match.BestPassTarget(this);
                    Vector3 aim = mate != null ? mate.transform.position - transform.position : new Vector3(s, 0f, Random.Range(-0.5f, 0.5f));
                    RequestKick(mate != null && aim.magnitude < 16f ? KickType.ShortPass : KickType.LongPass, 0.6f, false, aim);
                }
                return;
            }

            bool loose = ball.Owner == null || ball.Owner.teamIndex != teamIndex;
            float distToLine = Mathf.Abs(ballP.x - lineX);
            if (loose && distToLine < 6f && Mathf.Abs(ballP.z) < 6f && ball.Owner == null)
            {
                MoveTowards(ballP, true); // come off the line for a loose ball in the box
                return;
            }
            float z = Mathf.Clamp(ballP.z * 0.35f, -1.2f, 1.2f);
            float x = lineX + s * (0.7f + Mathf.Clamp01(1f - distToLine / 25f) * 0.8f);
            MoveTowards(new Vector3(x, 0f, z), false);
        }

        void ThinkWithBall()
        {
            Vector3 pos = transform.position;
            Vector3 toGoal = AttackGoalCenter - pos;
            toGoal.y = 0f;
            float goalDist = toGoal.magnitude;

            PlayerController threat = Match.NearestOpponent(this, out float threatDist);

            if (Time.time >= aiNextDecision)
            {
                aiNextDecision = Time.time + 0.3f;
                float held = Time.time - hadBallSince;

                if (goalDist < 13f && Random.value < (goalDist < 8f ? 0.8f : 0.45f))
                {
                    Vector3 aim = AttackGoalCenter + new Vector3(0f, 0f, Random.Range(-1.1f, 1.1f)) - pos;
                    float power = Mathf.Clamp01(0.45f + goalDist / 30f + Random.Range(-0.1f, 0.15f));
                    RequestKick(KickType.Shoot, power, false, aim);
                    return;
                }

                bool pressured = threat != null && threatDist < 2.6f;
                if ((pressured && Random.value < 0.55f) || (held > 2.5f && Random.value < 0.4f))
                {
                    PlayerController mate = Match.BestPassTarget(this);
                    if (mate != null)
                    {
                        Vector3 to = mate.transform.position - pos;
                        RequestKick(to.magnitude > 18f ? KickType.LongPass : KickType.ShortPass, 0.5f, false, to);
                        return;
                    }
                }
            }

            // Dribble towards goal, stepping around the nearest opponent.
            Vector3 dir = toGoal.normalized;
            if (threat != null && threatDist < 3.5f)
            {
                Vector3 away = pos - threat.transform.position;
                away.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                if (Vector3.Dot(side, away) < 0f) side = -side;
                dir = (dir + side * 0.9f).normalized;
            }
            aiSprint = threat == null || threatDist > 4f;
            moveInput = new Vector2(dir.x, dir.z);
            Sprinting = aiSprint;
        }
    }
}
