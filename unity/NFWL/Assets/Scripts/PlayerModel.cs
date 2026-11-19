using UnityEngine;
using UnityEngine.Rendering;

namespace NFWL
{
    /// <summary>
    /// A Crystalia footballer (yu's character designs): elf ears, crystal shards on the shoulders,
    /// forearms and shins, glowing eyes, spiky hair, and the NFWL kit with crystal pattern and number.
    /// Built from primitive shapes and a procedural shard mesh, with procedural animation:
    /// run cycle scaled by speed, kick swing, goalkeeper dive and goal celebration.
    /// Lightweight on purpose (no downloaded models), so it runs well on an 8GB Mac.
    /// </summary>
    public class PlayerModel : MonoBehaviour
    {
        Transform body, hips, torso, head;
        Transform legL, legR, kneeL, kneeR, ankleL, ankleR, armL, armR, elbowL, elbowR;

        PlayerController owner;
        float kickTimer = -1f;
        float diveTimer = -1f;
        float diveSide = 1f;
        float celebrateTimer = -1f;
        float lean;
        float heightScale = 1f;
        readonly GaitController gait = new GaitController();
        float ikWeightL = 1f, ikWeightR = 1f;
        float pelvisDrop;

        // Leg proportions of the built model (unscaled metres).
        const float HipHeight = 0.95f;
        const float ThighLength = 0.44f;
        const float ShinLength = 0.42f;   // knee to ankle
        const float AnkleHeight = 0.075f; // ankle above the sole

        /// <summary>The feet (planted / swinging), for touches, kicks and tests.</summary>
        public GaitController Gait => gait;
        /// <summary>How far a planted foot was out of the leg's reach last frame (m). ~0 = no sliding.</summary>
        public float FootReachError { get; private set; }

        const float KickTime = 0.32f;
        const float DiveTime = 0.9f;

        public static PlayerModel Build(PlayerController owner, KitStyle kit, Element el, int number, int hairStyle, float heightScale)
        {
            Transform root = owner.transform;
            PlayerModel m = new GameObject("Model").AddComponent<PlayerModel>();
            m.owner = owner;
            m.transform.SetParent(root, false);
            m.transform.localScale = Vector3.one * heightScale;
            m.heightScale = heightScale;

            Material shirt = Visuals.Mat(kit.body, 0.3f);
            Material trim = Visuals.Mat(kit.trim, 0.5f);
            Material front = Visuals.Mat(Color.white, 0.3f, CrystalArt.KitPanel(kit, el.crystal, number, 0));
            Material back = Visuals.Mat(Color.white, 0.3f, CrystalArt.KitPanel(kit, el.crystal, number, 1));
            Material shortsPanel = Visuals.Mat(Color.white, 0.3f, CrystalArt.KitPanel(kit, el.crystal, number, 2));
            Material shorts = Visuals.Mat(kit.body, 0.25f);
            Material socks = Visuals.Mat(Color.Lerp(kit.body, el.crystal, 0.12f), 0.25f, CrystalArt.Facets, new Vector2(1f, 2f));
            Material boots = CrystalArt.Metal(kit.boots);
            Material skin = Visuals.Mat(el.skin, 0.45f);
            Material limbSkin = el.crystalSkin ? CrystalArt.CrystalSkin(el.skin, el.crystal) : Visuals.Mat(el.skin, 0.45f, CrystalArt.Facets, new Vector2(1f, 1f));
            Material hair = CrystalArt.Glow(el.hair, el.crystal, 0.15f, 0.7f, null);
            Material crystal = CrystalArt.Crystal(el.crystal);
            Material eyes = CrystalArt.Glow(el.eye, el.eye, 1.6f, 0.9f, null);

            // body: everything that leans / dives.
            m.body = Visuals.Pivot(m.transform, Vector3.zero, "Body");
            m.hips = Visuals.Pivot(m.body, new Vector3(0f, 0.95f, 0f), "Hips");
            Visuals.Part(PrimitiveType.Cube, m.hips, new Vector3(0f, 0.02f, 0f), new Vector3(0.37f, 0.22f, 0.23f), shorts, "Shorts");
            Visuals.Part(PrimitiveType.Quad, m.hips, new Vector3(0.1f, -0.02f, 0.117f), new Vector3(0.12f, 0.12f, 1f), shortsPanel, "ShortsNumber", Quaternion.Euler(0f, 180f, 0f));

            m.torso = Visuals.Pivot(m.hips, new Vector3(0f, 0.1f, 0f), "Torso");
            Visuals.Part(PrimitiveType.Cube, m.torso, new Vector3(0f, 0.27f, 0f), new Vector3(0.44f, 0.54f, 0.25f), shirt, "Shirt");
            Visuals.Part(PrimitiveType.Quad, m.torso, new Vector3(0f, 0.27f, 0.127f), new Vector3(0.44f, 0.54f, 1f), front, "ShirtFront", Quaternion.Euler(0f, 180f, 0f));
            Visuals.Part(PrimitiveType.Quad, m.torso, new Vector3(0f, 0.27f, -0.127f), new Vector3(0.44f, 0.54f, 1f), back, "ShirtBack");
            Visuals.Part(PrimitiveType.Cube, m.torso, new Vector3(0f, 0.545f, 0f), new Vector3(0.24f, 0.03f, 0.2f), trim, "Collar");
            Visuals.Part(PrimitiveType.Cylinder, m.torso, new Vector3(0f, 0.59f, 0f), new Vector3(0.1f, 0.05f, 0.1f), skin, "Neck");
            // Shoulder crystals (Sapphire / Emerald / Gold designs).
            AddShardCluster(m.torso, new Vector3(-0.24f, 0.52f, 0f), -1f, crystal, 3, 0.26f);
            AddShardCluster(m.torso, new Vector3(0.24f, 0.52f, 0f), 1f, crystal, 3, 0.26f);

            m.head = Visuals.Pivot(m.torso, new Vector3(0f, 0.73f, 0f), "Head");
            Visuals.Part(PrimitiveType.Sphere, m.head, Vector3.zero, new Vector3(0.2f, 0.25f, 0.22f), skin, "Face");
            // Elf ears.
            Shard(m.head, new Vector3(0.1f, 0.01f, -0.01f), Quaternion.Euler(0f, 0f, -70f), new Vector3(0.18f, 0.13f, 0.1f), skin, "EarR", true);
            Shard(m.head, new Vector3(-0.1f, 0.01f, -0.01f), Quaternion.Euler(0f, 0f, 70f), new Vector3(0.18f, 0.13f, 0.1f), skin, "EarL", true);
            // Glowing eyes.
            Visuals.Part(PrimitiveType.Sphere, m.head, new Vector3(0.045f, 0.015f, 0.098f), new Vector3(0.04f, 0.028f, 0.02f), eyes, "EyeL");
            Visuals.Part(PrimitiveType.Sphere, m.head, new Vector3(-0.045f, 0.015f, 0.098f), new Vector3(0.04f, 0.028f, 0.02f), eyes, "EyeR");
            BuildHair(m.head, hair, crystal, hairStyle);

            m.armL = BuildArm(m.torso, -1f, shirt, trim, limbSkin, crystal, out m.elbowL);
            m.armR = BuildArm(m.torso, 1f, shirt, trim, limbSkin, crystal, out m.elbowR);

            m.legL = BuildLeg(m.hips, -1f, shorts, limbSkin, socks, trim, boots, crystal, out m.kneeL, out m.ankleL);
            m.legR = BuildLeg(m.hips, 1f, shorts, limbSkin, socks, trim, boots, crystal, out m.kneeR, out m.ankleR);

            // Soft glow on the grass in the element colour (also helps to see players from the high camera).
            GameObject blob = Visuals.Part(PrimitiveType.Cylinder, m.transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.65f, 0.002f, 0.65f),
                Visuals.Mat(Color.Lerp(new Color(0.08f, 0.25f, 0.08f), el.crystal, 0.25f), 0f), "Shadow");
            blob.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return m;
        }

        /// <summary>One crystal shard. Pivot at its base, pointing along local +Y.</summary>
        static GameObject Shard(Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat, string name, bool castShadow = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = CrystalArt.Shard;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A fan of shards growing outward (side = -1 left, +1 right).</summary>
        static void AddShardCluster(Transform parent, Vector3 at, float side, Material mat, int count, float size)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : i / (count - 1f) - 0.5f;
                Quaternion rot = Quaternion.Euler(t * 40f, 0f, -side * (25f + i * 12f));
                float s = size * (1f - Mathf.Abs(t) * 0.5f);
                Shard(parent, at + new Vector3(0f, 0f, t * 0.12f), rot, new Vector3(s * 0.6f, s, s * 0.6f), mat, "Crystal");
            }
        }

        static void BuildHair(Transform head, Material hair, Material crystal, int style)
        {
            Visuals.Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.06f, -0.015f), new Vector3(0.215f, 0.17f, 0.235f), hair, "Hair");
            // Spiky locks swept back and up (all designs have tousled, spiky hair).
            int spikes = 7 + style;
            for (int i = 0; i < spikes; i++)
            {
                float a = (i / (float)spikes) * 300f - 150f;
                Quaternion rot = Quaternion.Euler(-35f - (i % 3) * 15f, a * 0.6f, (i % 2 == 0 ? 1f : -1f) * (10f + i * 4f));
                Vector3 pos = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.07f, 0.1f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.05f - 0.02f);
                Shard(head, pos, rot, new Vector3(0.22f, 0.2f + (i % 3) * 0.04f, 0.22f), hair, "Lock", true);
            }
            // A small crystal in the hair (Ice / Sapphire / Gold designs).
            Shard(head, new Vector3(0.07f, 0.12f, 0.03f), Quaternion.Euler(-20f, 0f, -35f), new Vector3(0.12f, 0.14f, 0.12f), crystal, "HairCrystal");
        }

        static Transform BuildArm(Transform torso, float side, Material sleeve, Material trim, Material skin, Material crystal, out Transform elbow)
        {
            Transform shoulder = Visuals.Pivot(torso, new Vector3(side * 0.28f, 0.48f, 0f), side < 0 ? "ArmL" : "ArmR");
            Visuals.Part(PrimitiveType.Sphere, shoulder, Vector3.zero, Vector3.one * 0.15f, sleeve, "Shoulder");
            Visuals.Part(PrimitiveType.Capsule, shoulder, new Vector3(0f, -0.08f, 0f), new Vector3(0.13f, 0.09f, 0.13f), sleeve, "Sleeve");
            Visuals.Part(PrimitiveType.Cylinder, shoulder, new Vector3(0f, -0.16f, 0f), new Vector3(0.135f, 0.015f, 0.135f), trim, "Cuff");
            Visuals.Part(PrimitiveType.Capsule, shoulder, new Vector3(0f, -0.2f, 0f), new Vector3(0.09f, 0.08f, 0.09f), skin, "Upper");
            elbow = Visuals.Pivot(shoulder, new Vector3(0f, -0.27f, 0f), "Elbow");
            Visuals.Part(PrimitiveType.Capsule, elbow, new Vector3(0f, -0.13f, 0f), new Vector3(0.08f, 0.13f, 0.08f), skin, "Fore");
            Visuals.Part(PrimitiveType.Sphere, elbow, new Vector3(0f, -0.29f, 0f), new Vector3(0.07f, 0.09f, 0.05f), skin, "Hand");
            // Crystal blades along the outside of the forearm.
            for (int i = 0; i < 3; i++)
            {
                Shard(elbow, new Vector3(side * 0.04f, -0.06f - i * 0.07f, -0.01f),
                    Quaternion.Euler(0f, 0f, side * (-150f + i * 12f)), new Vector3(0.09f, 0.16f + i * 0.03f, 0.09f), crystal, "ArmCrystal");
            }
            return shoulder;
        }

        static Transform BuildLeg(Transform hips, float side, Material shorts, Material skin, Material sock, Material trim, Material boot, Material crystal, out Transform knee, out Transform ankle)
        {
            Transform hip = Visuals.Pivot(hips, new Vector3(side * 0.1f, 0f, 0f), side < 0 ? "LegL" : "LegR");
            Visuals.Part(PrimitiveType.Capsule, hip, new Vector3(0f, -0.1f, 0f), new Vector3(0.17f, 0.12f, 0.17f), shorts, "ShortsLeg");
            Visuals.Part(PrimitiveType.Capsule, hip, new Vector3(0f, -0.29f, 0f), new Vector3(0.13f, 0.14f, 0.13f), skin, "Thigh");
            knee = Visuals.Pivot(hip, new Vector3(0f, -0.44f, 0f), "Knee");
            Visuals.Part(PrimitiveType.Capsule, knee, new Vector3(0f, -0.22f, 0f), new Vector3(0.12f, 0.22f, 0.12f), sock, "Shin");
            Visuals.Part(PrimitiveType.Cylinder, knee, new Vector3(0f, -0.04f, 0f), new Vector3(0.125f, 0.015f, 0.125f), trim, "SockBand");
            // Ankle joint, so the boot can stay flat on the grass while the shin moves (foot IK).
            ankle = Visuals.Pivot(knee, new Vector3(0f, -ShinLength, 0f), "Ankle");
            Visuals.Part(PrimitiveType.Cube, ankle, new Vector3(0f, -0.04f, 0.05f), new Vector3(0.11f, 0.07f, 0.27f), boot, "Boot");
            // Crystal greaves on the shin, pointing up past the knee (all designs).
            for (int i = 0; i < 3; i++)
            {
                float a = (i - 1) * 35f;
                Shard(knee, new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.05f, -0.3f + i % 2 * 0.04f, 0.04f),
                    Quaternion.Euler(12f, a, side * (i - 1) * 8f), new Vector3(0.12f, 0.32f - (i % 2) * 0.06f, 0.12f), crystal, "LegCrystal");
            }
            return hip;
        }

        // ---------------- foot actions (touches now, kicks and tackles later) ----------------

        struct FootAction
        {
            public bool active, started;
            public float side;
            public float start, contact, end;
            public Vector3 startPos;
            public float startYaw;
            public Vector3 ball;   // ball centre at contact (updated until contact)
            public Vector3 dir;    // direction the ball is played
            public FootStyle style;
            public float backswing, follow;
            public Vector3 contactPos;
            public float contactYaw, contactPitch;
        }
        FootAction action;

        public bool FootActionActive => action.active;

        /// <summary>
        /// Swings one foot so it arrives at the ball exactly at contactTime, then follows through.
        /// The ball velocity is changed by the caller at contactTime, so foot and ball stay in sync.
        /// </summary>
        public void BeginFootAction(float side, float startTime, float contactTime, float endTime, FootStyle style,
            Vector3 dir, Vector3 ballAtContact, float backswing, float followThrough)
        {
            dir.y = 0f;
            action = new FootAction
            {
                active = true,
                started = false,
                side = side < 0f ? -1f : 1f,
                start = startTime,
                contact = Mathf.Max(contactTime, startTime + 0.02f),
                end = Mathf.Max(endTime, contactTime + 0.05f),
                ball = ballAtContact,
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : owner.Locomotion.Facing,
                style = style,
                backswing = backswing,
                follow = followThrough
            };
        }

        /// <summary>Keeps the swinging foot aimed at where the ball will be at contact.</summary>
        public void UpdateFootActionBall(Vector3 ballAtContact)
        {
            if (action.active && Time.time < action.contact) action.ball = ballAtContact;
        }

        public void CancelFootAction() => action.active = false;

        /// <summary>Sole point, yaw and pitch of a foot that meets the ball with the given part.</summary>
        void ContactPose(out Vector3 pos, out float yaw, out float pitch)
        {
            float r = PitchDims.BallRadius;
            Vector3 g = new Vector3(action.ball.x, 0f, action.ball.z);
            float dirYaw = PlayerLocomotion.DirToYaw(action.dir, owner.Locomotion.FacingYaw);
            Vector3 d = action.dir;
            float side = action.side;
            switch (action.style)
            {
                case FootStyle.Inside:
                    // Foot turned out so its inside faces the play.
                    yaw = dirYaw + side * 70f;
                    pitch = 0f;
                    pos = g - d * (r + 0.06f) - PlayerLocomotion.YawToDir(yaw) * 0.05f;
                    break;
                case FootStyle.Outside:
                    yaw = dirYaw - side * 40f;
                    pitch = 5f;
                    pos = g - d * (r + 0.05f) - PlayerLocomotion.YawToDir(yaw) * 0.08f;
                    break;
                case FootStyle.Sole:
                    yaw = owner.Locomotion.FacingYaw;
                    pitch = -18f;
                    pos = g + Vector3.up * (2f * r - 0.02f) - PlayerLocomotion.YawToDir(yaw) * 0.1f;
                    break;
                case FootStyle.Toe:
                    yaw = dirYaw;
                    pitch = 0f;
                    pos = g - d * (r + 0.22f);
                    break;
                default: // laces / instep: toe pointed down, ankle locked
                    yaw = dirYaw;
                    pitch = 40f;
                    pos = g - d * (r + 0.1f) + Vector3.up * 0.06f;
                    break;
            }
        }

        /// <summary>Writes the action foot's pose into the gait foot. Returns false when no action runs.</summary>
        bool ApplyFootAction(float now)
        {
            if (!action.active) return false;
            GaitController.Foot foot = gait.Get(action.side);
            if (!action.started)
            {
                action.started = true;
                action.startPos = foot.position;
                action.startYaw = foot.yaw;
            }
            if (now >= action.end)
            {
                action.active = false;
                return false;
            }

            if (now < action.contact)
            {
                ContactPose(out action.contactPos, out action.contactYaw, out action.contactPitch);
                float u = Mathf.Clamp01((now - action.start) / (action.contact - action.start));
                // Quadratic curve through a backswing point behind and above the contact point.
                Vector3 back = action.contactPos - action.dir * action.backswing + Vector3.up * (0.05f + action.backswing * 0.6f);
                Vector3 a = Vector3.Lerp(action.startPos, back, u);
                Vector3 b = Vector3.Lerp(back, action.contactPos, u);
                foot.position = Vector3.Lerp(a, b, u);
                float e = u * u * (3f - 2f * u);
                foot.yaw = Mathf.LerpAngle(action.startYaw, action.contactYaw, e);
                foot.pitch = Mathf.Lerp(15f, action.contactPitch, e);
            }
            else
            {
                // Follow through along the line of the ball, then come down to plant.
                float v = Mathf.Clamp01((now - action.contact) / (action.end - action.contact));
                float ease = 1f - (1f - v) * (1f - v);
                Vector3 basePos = action.contactPos + action.dir * (action.follow * ease);
                basePos.y = Mathf.Lerp(action.contactPos.y, 0f, ease);
                foot.position = basePos + Vector3.up * (Mathf.Sin(v * Mathf.PI) * (0.04f + action.follow * 0.25f));
                foot.yaw = Mathf.LerpAngle(action.contactYaw, owner.Locomotion.FacingYaw, v * v);
                foot.pitch = Mathf.Lerp(action.contactPitch, 0f, v);
            }
            return true;
        }

        public void PlayKick() { kickTimer = 0f; }

        public void PlayDive(float side) { diveTimer = 0f; diveSide = Mathf.Sign(side == 0f ? 1f : side); }

        public void PlayCelebrate() { celebrateTimer = 0f; }

        /// <summary>Animation runs after all movement this frame, so planted feet stay exactly in place.</summary>
        void LateUpdate()
        {
            if (owner == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            PlayerLocomotion lc = owner.Locomotion;
            float speed = lc.Speed;
            LocoState st = lc.State;
            float sc = heightScale;
            Vector3 root = owner.transform.position;
            bool crouch = st == LocoState.Jockey || st == LocoState.SideStep || st == LocoState.Backpedal;

            // ---------- full-body overrides (dive, celebration) ----------
            Quaternion bodyRot = Quaternion.Euler(0f, 0f, lc.LeanRoll); // lean into curves from the feet
            Vector3 bodyPos = Vector3.zero;
            bool airborne = false;
            float armOverrideL = float.NaN, armOverrideR = float.NaN;

            if (diveTimer >= 0f)
            {
                diveTimer += dt;
                float d = diveTimer / DiveTime;
                if (d >= 1f) diveTimer = -1f;
                else
                {
                    float w = d < 0.3f ? d / 0.3f : 1f - Mathf.Clamp01((d - 0.6f) / 0.4f);
                    bodyRot = Quaternion.Euler(0f, 0f, -diveSide * 75f * w);
                    bodyPos = new Vector3(diveSide * 0.6f * w, 0.25f * w, 0f);
                    armOverrideL = -150f * w;
                    armOverrideR = 150f * w;
                    airborne = w > 0.1f;
                }
            }
            if (celebrateTimer >= 0f)
            {
                celebrateTimer += dt;
                if (celebrateTimer > 2.2f) celebrateTimer = -1f;
                else
                {
                    float j = Mathf.Abs(Mathf.Sin(celebrateTimer * 7f));
                    bodyPos.y += j * 0.35f;
                    armOverrideL = -160f + j * 20f;
                    armOverrideR = 160f - j * 20f;
                    airborne = true;
                }
            }
            body.localRotation = bodyRot;
            body.localPosition = bodyPos;

            // ---------- feet (gait with foot locking) ----------
            bool kicking = kickTimer >= 0f;
            float now = Time.time;
            bool actionLeft = action.active && action.side < 0f && now < action.end;
            bool actionRight = action.active && action.side > 0f && now < action.end;
            gait.Right.overridden = kicking || actionRight;
            gait.Left.overridden = actionLeft;
            bool pivoting = st == LocoState.PivotTurn || st == LocoState.SharpTurn || speed < 0.35f;
            gait.Tick(root, lc.Velocity, lc.FacingYaw, dt, sc, crouch ? 1.9f : 1f, pivoting);
            ApplyFootAction(now);
            float run = gait.RunAmount;

            // IK weights: feet follow the ground unless the body is in the air or a kick drives the leg.
            ikWeightL = Mathf.MoveTowards(ikWeightL, airborne ? 0f : 1f, dt * 8f);
            ikWeightR = Mathf.MoveTowards(ikWeightR, airborne || kicking ? 0f : 1f, dt * (kicking ? 30f : 8f));

            // ---------- pelvis: height, bob, twist ----------
            // Bob: lowest at mid-stance, twice per cycle.
            float bob = Mathf.Cos(gait.Phase * Mathf.PI * 4f - gait.StanceFraction * Mathf.PI * 2f) * 0.035f * run;
            float crouchDrop = crouch ? 0.1f : st == LocoState.SharpTurn ? 0.07f : st == LocoState.Recovery ? 0.05f : 0f;
            // Pelvis twists toward the leg that swings forward; shoulders twist the other way.
            float legSignal = LegForwardSignal();
            float pelvisYaw = legSignal * 9f * run;
            float pelvisRoll = -legSignal * 2.5f * run;
            hips.localRotation = Quaternion.Euler(0f, pelvisYaw, pelvisRoll);
            hips.localPosition = new Vector3(0f, HipHeight - crouchDrop - bob, 0f);

            // Pelvis height adjustment: lower the hips just enough that both planted feet stay in reach,
            // so the feet never have to slide.
            float legLen = (ThighLength + ShinLength) * sc * 0.985f;
            float needDrop = Mathf.Max(RequiredDrop(legL, gait.Left, ikWeightL, legLen, sc), RequiredDrop(legR, gait.Right, ikWeightR, legLen, sc));
            float targetDrop = Mathf.Clamp(needDrop / sc, 0f, 0.16f);
            pelvisDrop = Mathf.Max(targetDrop, Mathf.MoveTowards(pelvisDrop, targetDrop, dt * 0.9f));
            hips.localPosition = new Vector3(0f, HipHeight - crouchDrop - bob - pelvisDrop, 0f);

            // ---------- upper body ----------
            lean = Mathf.Lerp(lean, lc.LeanPitch + (crouch ? 14f : 0f), 1f - Mathf.Exp(-14f * dt));
            torso.localRotation = Quaternion.Euler(lean, -pelvisYaw * 1.6f, -pelvisRoll);
            head.localRotation = Quaternion.Euler(-lean * 0.5f, pelvisYaw * 0.6f, 0f); // eyes stay on the play

            // Arms swing opposite to the legs, bent at the elbow. Off balance or jockeying: arms out wide.
            float armAmp = Mathf.Lerp(12f, 55f, run);
            float armOut = st == LocoState.Recovery ? 45f : crouch ? 24f : 8f + run * 4f;
            armL.localRotation = Quaternion.Euler(float.IsNaN(armOverrideL) ? legSignal * armAmp : 0f, 0f, float.IsNaN(armOverrideL) ? -armOut : armOverrideL);
            armR.localRotation = Quaternion.Euler(float.IsNaN(armOverrideR) ? -legSignal * armAmp : 0f, 0f, float.IsNaN(armOverrideR) ? armOut : armOverrideR);
            float elbow = -20f - run * 65f;
            elbowL.localRotation = Quaternion.Euler(elbow + Mathf.Max(0f, legSignal) * 15f * run, 0f, 0f);
            elbowR.localRotation = Quaternion.Euler(elbow + Mathf.Max(0f, -legSignal) * 15f * run, 0f, 0f);

            // ---------- legs ----------
            FootReachError = 0f;
            SolveLeg(legL, kneeL, ankleL, gait.Left, ikWeightL, 0f, 0f, sc);
            float kickHip = 0f, kickKnee = 0f;
            if (kicking)
            {
                kickTimer += dt;
                float k = kickTimer / KickTime;
                if (k >= 1f) kickTimer = -1f;
                else
                {
                    // Wind up then swing the right leg through (full kick system comes in STEP 5).
                    kickHip = k < 0.4f ? Mathf.Lerp(0f, 50f, k / 0.4f) : Mathf.Lerp(50f, -75f, (k - 0.4f) / 0.6f);
                    kickKnee = k < 0.4f ? 70f * (k / 0.4f) : Mathf.Lerp(70f, 0f, (k - 0.4f) / 0.6f);
                    // Keep the swinging foot's position known to the gait so it re-plants where it ends.
                    gait.Right.position = ankleR.position - Vector3.up * AnkleHeight * sc;
                    gait.Right.yaw = lc.FacingYaw;
                }
            }
            SolveLeg(legR, kneeR, ankleR, gait.Right, ikWeightR, kickHip, kickKnee, sc);
        }

        /// <summary>+1 when the left leg is swinging forward / right leg back, -1 the other way.</summary>
        float LegForwardSignal()
        {
            if (!gait.Moving && !gait.Left.swinging && !gait.Right.swinging) return 0f;
            Vector3 fwd = owner.Locomotion.Facing;
            float l = Vector3.Dot(gait.Left.position - owner.transform.position, fwd);
            float r = Vector3.Dot(gait.Right.position - owner.transform.position, fwd);
            return Mathf.Clamp((l - r) / (0.6f * heightScale), -1f, 1f);
        }

        /// <summary>How far (world m) the hips must drop for this foot to be reachable.</summary>
        float RequiredDrop(Transform hip, GaitController.Foot foot, float weight, float legLen, float sc)
        {
            if (weight < 0.5f) return 0f;
            Vector3 target = foot.position + foot.normal * (AnkleHeight * sc);
            Vector3 h = hip.position;
            float dx = h.x - target.x, dz = h.z - target.z;
            float hd2 = dx * dx + dz * dz;
            if (hd2 >= legLen * legLen) return 0.16f * sc; // too far sideways: drop as much as allowed
            float maxY = target.y + Mathf.Sqrt(legLen * legLen - hd2);
            return Mathf.Max(0f, h.y - maxY);
        }

        /// <summary>Two-bone IK toward the gait foot, blended with a forward-kinematics pose by weight.</summary>
        void SolveLeg(Transform hip, Transform knee, Transform ankle, GaitController.Foot foot, float weight, float fkHip, float fkKnee, float sc)
        {
            // FK pose (used in the air and while a kick drives the leg).
            hip.localRotation = Quaternion.Euler(fkHip, 0f, 0f);
            knee.localRotation = Quaternion.Euler(fkKnee + 5f, 0f, 0f);
            ankle.localRotation = Quaternion.Euler(fkHip != 0f ? 25f : 0f, 0f, 0f);
            if (weight <= 0.001f) return;

            Quaternion fkHipW = hip.rotation, fkKneeW = knee.rotation, fkAnkleW = ankle.rotation;
            Vector3 footFwd = Quaternion.Euler(0f, foot.yaw, 0f) * Vector3.forward;
            Vector3 target = foot.position + foot.normal * (AnkleHeight * sc);
            // Knee points where the foot points, a little outward.
            Vector3 pole = footFwd + Quaternion.Euler(0f, foot.yaw, 0f) * Vector3.right * (foot.side * 0.12f) + Vector3.up * 0.05f;
            float miss = LegIK.SolveKnee(hip.position, target, ThighLength * sc, ShinLength * sc, pole, out Vector3 kneePos, out Vector3 reached);
            if (!foot.swinging && !foot.overridden) FootReachError = Mathf.Max(FootReachError, miss);

            Quaternion hipW = LegIK.BoneRotation(hip.position, kneePos, pole);
            Quaternion kneeW = LegIK.BoneRotation(kneePos, reached, pole);
            Quaternion ankleW = Quaternion.FromToRotation(Vector3.up, foot.normal) * Quaternion.Euler(foot.pitch, foot.yaw, 0f);

            hip.rotation = Quaternion.Slerp(fkHipW, hipW, weight);
            knee.rotation = Quaternion.Slerp(fkKneeW, kneeW, weight);
            ankle.rotation = Quaternion.Slerp(fkAnkleW, ankleW, weight);
        }
    }
}
