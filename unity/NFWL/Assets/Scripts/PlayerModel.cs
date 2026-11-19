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
        // Goalkeeper save pose (PHASE 6): a timeline from GoalkeeperController.
        SaveType saveType = SaveType.None;
        float saveSide = 1f, saveHeight, saveStart, saveContact, saveEnd;
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
            public float backTime;       // time the foot reaches the top of the backswing (kicks)
            public float followHeight;
            public Vector3 backPos;
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
            Vector3 dir, Vector3 ballAtContact, float backswing, float followThrough, float backTime = -1f, float followHeight = -1f)
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
                follow = followThrough,
                backTime = backTime > startTime ? Mathf.Min(backTime, contactTime - 0.02f) : -1f,
                followHeight = followHeight >= 0f ? followHeight : 0.04f + followThrough * 0.25f
            };
        }

        /// <summary>Keeps the swinging foot aimed at where the ball will be at contact.</summary>
        public void UpdateFootActionBall(Vector3 ballAtContact)
        {
            if (action.active && Time.time < action.contact) action.ball = ballAtContact;
        }

        public void CancelFootAction() => action.active = false;

        /// <summary>Stops every foot action and kick pose (kick-off reset).</summary>
        public void CancelAllActions()
        {
            action.active = false;
            plantAction.active = false;
            kick = null;
            kickTimer = -1f;
            saveType = SaveType.None;
        }

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
            // A bouncing or dropping ball: meet it at its height (volley / half volley / drop kick).
            if (action.style != FootStyle.Sole) pos.y += Mathf.Clamp(action.ball.y - r, 0f, 1.0f);
        }

        /// <summary>Writes the action foot's pose into the gait foot. Returns false when no action runs.</summary>
        bool ApplyFootAction(float now)
        {
            if (!action.active || now < action.start) return false;
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
                // Backswing point: behind the contact point, heel kicked up.
                action.backPos = action.contactPos - action.dir * action.backswing
                                 + Vector3.up * (0.05f + action.backswing * 0.7f);
                float e;
                if (action.backTime > 0f && now < action.backTime)
                {
                    // Backswing: the foot leaves the ground and goes back (decelerating at the top).
                    float u = Mathf.Clamp01((now - action.start) / (action.backTime - action.start));
                    e = 1f - (1f - u) * (1f - u);
                    foot.position = Vector3.Lerp(action.startPos, action.backPos, e);
                    foot.yaw = Mathf.LerpAngle(action.startYaw, action.contactYaw, e * 0.6f);
                    foot.pitch = Mathf.Lerp(10f, 45f, e);
                }
                else if (action.backTime > 0f)
                {
                    // Downswing: accelerates through an arc that bottoms out at the ball.
                    float u = Mathf.Clamp01((now - action.backTime) / (action.contact - action.backTime));
                    e = u * u;
                    Vector3 lineP = Vector3.Lerp(action.backPos, action.contactPos, e);
                    lineP.y = Mathf.Lerp(action.backPos.y, action.contactPos.y, Mathf.Sin(u * Mathf.PI * 0.5f));
                    foot.position = lineP;
                    foot.yaw = Mathf.LerpAngle(Mathf.LerpAngle(action.startYaw, action.contactYaw, 0.6f), action.contactYaw, u);
                    foot.pitch = Mathf.Lerp(45f, action.contactPitch, u);
                }
                else
                {
                    // Touch: one short arc through a small backswing point.
                    float u = Mathf.Clamp01((now - action.start) / (action.contact - action.start));
                    Vector3 a1 = Vector3.Lerp(action.startPos, action.backPos, u);
                    Vector3 b1 = Vector3.Lerp(action.backPos, action.contactPos, u);
                    foot.position = Vector3.Lerp(a1, b1, u);
                    e = u * u * (3f - 2f * u);
                    foot.yaw = Mathf.LerpAngle(action.startYaw, action.contactYaw, e);
                    foot.pitch = Mathf.Lerp(15f, action.contactPitch, e);
                }
            }
            else
            {
                // Follow through along the line of the ball (rising), then come down to plant.
                float v = Mathf.Clamp01((now - action.contact) / (action.end - action.contact));
                float ease = 1f - (1f - v) * (1f - v);
                Vector3 basePos = action.contactPos + action.dir * (action.follow * ease);
                basePos.y = Mathf.Lerp(action.contactPos.y, 0f, ease);
                foot.position = basePos + Vector3.up * (Mathf.Sin(v * Mathf.PI) * action.followHeight);
                foot.yaw = Mathf.LerpAngle(action.contactYaw, owner.Locomotion.FacingYaw, v * v);
                foot.pitch = Mathf.Lerp(action.contactPitch, 0f, v);
            }
            return true;
        }

        // ---------------- kick: plant foot and body form (PHASE 3) ----------------

        struct PlantAction
        {
            public bool active, started;
            public float side, start, land, end;
            public Vector3 from, target;
            public float fromYaw, yaw;
        }
        PlantAction plantAction;
        KickController kick;
        KickController.Form kickForm;

        /// <summary>Support foot steps beside the ball and stays planted through the strike.</summary>
        public void BeginPlantFoot(float side, float start, float land, float end, Vector3 target, float yaw)
        {
            plantAction = new PlantAction
            {
                active = true, side = side < 0f ? -1f : 1f, start = start, land = Mathf.Max(land, start + 0.04f),
                end = end, target = target, yaw = yaw
            };
        }

        public void BeginKickPose(KickController k, KickController.Form form)
        {
            kick = k;
            kickForm = form;
        }

        bool ApplyPlantFoot(float now)
        {
            if (!plantAction.active || now < plantAction.start) return false;
            GaitController.Foot foot = gait.Get(plantAction.side);
            if (!plantAction.started)
            {
                plantAction.started = true;
                plantAction.from = foot.position;
                plantAction.fromYaw = foot.yaw;
                plantAction.target.y = GaitController.GroundHeight(plantAction.target, out _);
            }
            if (now >= plantAction.end) { plantAction.active = false; return false; }
            if (now < plantAction.land)
            {
                float u = Mathf.Clamp01((now - plantAction.start) / (plantAction.land - plantAction.start));
                float e = u * u * (3f - 2f * u);
                Vector3 p = Vector3.Lerp(plantAction.from, plantAction.target, e);
                p.y += Mathf.Sin(u * Mathf.PI) * 0.12f;
                foot.position = p;
                foot.yaw = Mathf.LerpAngle(plantAction.fromYaw, plantAction.yaw, e);
                foot.pitch = Mathf.Lerp(10f, -8f, u);
            }
            else
            {
                foot.position = plantAction.target;
                foot.yaw = plantAction.yaw;
                foot.pitch = 0f;
            }
            return true;
        }

        /// <summary>Hip turn, lean and arm balance during a kick (0 when no kick runs).</summary>
        void KickBody(float now, out float weight, out float pelvisYaw, out float torsoPitch, out float torsoRoll,
            out Vector3 armLEuler, out Vector3 armREuler, out float lift)
        {
            weight = 0f; pelvisYaw = 0f; torsoPitch = 0f; torsoRoll = 0f; lift = 0f;
            armLEuler = armREuler = Vector3.zero;
            if (kick == null || !kick.Active) return;
            if (kickForm.header)
            {
                // Header: rise to the ball, neck back, then snap the forehead through it.
                float hIn = Mathf.InverseLerp(kick.StartTime, kick.BackswingTime, now);
                float hOut = 1f - Mathf.InverseLerp(kick.FollowEndTime, kick.RecoverEndTime, now);
                weight = Mathf.Clamp01(Mathf.Min(hIn * 1.5f, hOut));
                float snap = Mathf.InverseLerp(kick.BackswingTime, kick.ContactTime, now);
                torsoPitch = Mathf.Lerp(-22f, 26f, snap * snap) * weight;
                float jump = Mathf.Clamp(kick.BallAtContact.y - 1.65f * heightScale, 0f, 0.6f);
                // Jump arc peaking at contact.
                float tUp = Mathf.InverseLerp(kick.StartTime, kick.ContactTime, now);
                float tDown = Mathf.InverseLerp(kick.ContactTime, kick.FollowEndTime + 0.1f, now);
                lift = jump * (now < kick.ContactTime ? Mathf.Sin(tUp * Mathf.PI * 0.5f) : 1f - tDown * tDown);
                armLEuler = new Vector3(-40f, 0f, -55f);
                armREuler = new Vector3(-40f, 0f, 55f);
                return;
            }
            float side = kick.Side;
            float wIn = Mathf.InverseLerp(kick.StartTime, kick.PlantTime, now);
            float wOut = 1f - Mathf.InverseLerp(kick.FollowEndTime, kick.RecoverEndTime, now);
            weight = Mathf.Clamp01(Mathf.Min(wIn * 1.5f, wOut));
            float back = Mathf.InverseLerp(kick.PlantTime, kick.BackswingTime, now);
            float through = Mathf.InverseLerp(kick.BackswingTime, kick.FollowEndTime, now);
            float pw = 0.6f + 0.4f * kickForm.power;
            // Kicking hip goes back in the backswing, then the pelvis rotates through the ball.
            pelvisYaw = side * (-28f * back * (1f - through) + 32f * through) * pw;
            torsoPitch = Mathf.Lerp(14f, -10f, kickForm.loft) * weight;
            // Lean away from the kicking leg, over the plant foot.
            torsoRoll = side * 11f * weight;
            // Arm on the plant-foot side out wide for balance, the other one back.
            Vector3 balance = new Vector3(-25f, 0f, -70f);
            Vector3 counter = new Vector3(35f - 60f * through, 0f, 20f);
            if (side > 0f) { armLEuler = balance; armREuler = new Vector3(counter.x, 0f, counter.z); }
            else { armREuler = new Vector3(balance.x, 0f, -balance.z); armLEuler = new Vector3(counter.x, 0f, -counter.z); }
        }

        /// <summary>v0.5 compatibility: a quick kick swing with no timeline (kicks now use KickController).</summary>
        public void PlayKick() { kickTimer = 0f; }

        /// <summary>v0.5 compatibility: a sideways dive with a default timeline (side in local right).</summary>
        public void PlayDive(float side)
        {
            float now = Time.time;
            saveType = SaveType.DiveLeft;
            saveSide = Mathf.Sign(side == 0f ? 1f : side);
            saveHeight = 0.8f;
            saveStart = now;
            saveContact = now + 0.3f;
            saveEnd = now + DiveTime;
        }

        /// <summary>
        /// Goalkeeper save (PHASE 6.2): the pose builds up to the contact time, is held briefly, then the
        /// keeper gets up by <paramref name="end"/>. <paramref name="worldSide"/> is the world-Z side of the
        /// ball (+1 / -1), <paramref name="height"/> the ball height at contact.
        /// </summary>
        public void PlaySave(SaveType type, float worldSide, float height, float start, float contact, float end)
        {
            saveType = type;
            Vector3 right = owner != null ? owner.transform.right : Vector3.right;
            float local = Vector3.Dot(new Vector3(0f, 0f, worldSide), right);
            saveSide = local >= 0f ? 1f : -1f;
            saveHeight = height;
            saveStart = start;
            saveContact = Mathf.Max(contact, start + 0.05f);
            saveEnd = Mathf.Max(end, saveContact + 0.1f);
        }

        /// <summary>The keeper is mid-dive (feet off the ground).</summary>
        public bool SaveAirborne { get; private set; }

        static bool IsDive(SaveType t) => t == SaveType.DiveLeft || t == SaveType.DiveRight || t == SaveType.LowDive
                                          || t == SaveType.HighDive || t == SaveType.Parry || t == SaveType.Punch;

        /// <summary>Body roll / offset, torso pitch, crouch and arm poses for the save at this moment.</summary>
        void SavePose(float now, ref Quaternion bodyRot, ref Vector3 bodyPos, ref bool airborne,
            out float torsoPitch, out float crouch, out bool armsOn, out Vector3 armLPose, out Vector3 armRPose, out float armW)
        {
            torsoPitch = 0f; crouch = 0f; armsOn = false; armLPose = armRPose = Vector3.zero; armW = 0f;
            SaveAirborne = false;
            if (saveType == SaveType.None) return;
            if (now > saveEnd) { saveType = SaveType.None; return; }
            if (now < saveStart) return;
            // Build up to contact, hold a moment (the hands on the ball), then get up.
            float hold = saveContact + 0.15f;
            float w = now < saveContact ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(saveStart, saveContact, now))
                    : now < hold ? 1f
                    : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hold, saveEnd, now));
            armsOn = true;
            armW = Mathf.Clamp01(w * 1.4f);
            float s = saveSide;
            if (IsDive(saveType))
            {
                // Dive: launch sideways off the near foot, body flat out at full stretch.
                float lift = saveType == SaveType.HighDive || saveType == SaveType.Punch ? 0.55f
                           : saveType == SaveType.LowDive ? 0.05f
                           : Mathf.Clamp(saveHeight * 0.35f, 0.15f, 0.45f);
                float roll = saveType == SaveType.HighDive || saveType == SaveType.Punch ? 55f : saveType == SaveType.LowDive ? 85f : 72f;
                // After the hold the body lands on its side before getting up.
                float air = now < hold ? w : w * 0.35f;
                bodyRot = Quaternion.Euler(0f, 0f, -s * roll * w);
                bodyPos = new Vector3(s * 0.3f * w, lift * air, 0f);
                // Both arms stretch toward the ball (up over the head = along the dive once the body rolls).
                float reach = saveType == SaveType.Punch ? 175f : 160f;
                armLPose = new Vector3(-20f, 0f, -reach);
                armRPose = new Vector3(-20f, 0f, reach);
                torsoPitch = -5f;
                airborne = w > 0.1f;
                SaveAirborne = airborne && now < hold;
            }
            else if (saveType == SaveType.HighCatch)
            {
                // Spring up with the hands above the head, a little in front.
                bodyPos = new Vector3(0f, Mathf.Clamp(saveHeight - 1.9f, 0f, 0.45f) * w, 0f);
                armLPose = new Vector3(-155f, 0f, -12f);
                armRPose = new Vector3(-155f, 0f, 12f);
                airborne = bodyPos.y > 0.05f;
                SaveAirborne = airborne;
            }
            else if (saveType == SaveType.LowCatch)
            {
                // Get down behind the ball: knees bend, chest over it, hands scoop under.
                crouch = 0.22f * w;
                torsoPitch = 38f * w;
                armLPose = new Vector3(-45f, 0f, -8f);
                armRPose = new Vector3(-45f, 0f, 8f);
            }
            else
            {
                // Standing catch: arms forward at chest height to take the ball into the body.
                torsoPitch = 10f * w;
                armLPose = new Vector3(-85f + Mathf.Clamp(saveHeight - 1.2f, -0.6f, 0.6f) * -60f, 0f, -10f);
                armRPose = new Vector3(armLPose.x, 0f, 10f);
            }
        }

        public void PlayCelebrate() { celebrateTimer = 0f; }

        float lungeStart = -1f, lungeContact, lungeEnd;
        /// <summary>Standing tackle: drop the hips and stretch toward the ball, then recover.</summary>
        public void PlayLunge(float start, float contact, float end) { lungeStart = start; lungeContact = contact; lungeEnd = end; }

        float LungeWeight(float now)
        {
            if (lungeStart < 0f || now > lungeEnd) return 0f;
            if (now < lungeContact) return Mathf.InverseLerp(lungeStart, lungeContact, now);
            return 1f - Mathf.InverseLerp(lungeContact, lungeEnd, now);
        }

        float chestTimer = -1f;
        /// <summary>Chest control: lean back and cushion the ball.</summary>
        public void PlayChest() { chestTimer = 0f; }

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
            Quaternion bodyRot = Quaternion.Euler(0f, 0f, lc.LeanRoll + lc.SwayRoll); // lean into curves, sway from bumps
            Vector3 bodyPos = Vector3.zero;
            bool airborne = false;
            float armOverrideL = float.NaN, armOverrideR = float.NaN;

            float now = Time.time;
            SavePose(now, ref bodyRot, ref bodyPos, ref airborne, out float saveTorso, out float saveCrouch,
                out bool saveArms, out Vector3 saveArmL, out Vector3 saveArmR, out float saveArmW);
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
            bool actionOn = action.active && now >= action.start && now < action.end;
            bool plantOn = plantAction.active && now >= plantAction.start && now < plantAction.end;
            bool actionLeft = actionOn && action.side < 0f;
            bool actionRight = actionOn && action.side > 0f;
            bool plantLeft = plantOn && plantAction.side < 0f;
            bool plantRight = plantOn && plantAction.side > 0f;
            gait.Right.overridden = kicking || actionRight || plantRight;
            gait.Left.overridden = actionLeft || plantLeft;
            bool pivoting = st == LocoState.PivotTurn || st == LocoState.SharpTurn || speed < 0.35f;
            gait.Tick(root, lc.Velocity, lc.FacingYaw, dt, sc, crouch ? 1.9f : 1f, pivoting);
            ApplyFootAction(now);
            ApplyPlantFoot(now);
            KickBody(now, out float kickW, out float kickPelvisYaw, out float kickTorsoPitch, out float kickTorsoRoll,
                out Vector3 kickArmL, out Vector3 kickArmR, out float kickLift);
            if (kickLift > 0.001f)
            {
                bodyPos.y += kickLift;
                body.localPosition = bodyPos;
                if (kickLift > 0.08f) airborne = true;
            }
            float chestLean = 0f;
            if (chestTimer >= 0f)
            {
                chestTimer += dt;
                if (chestTimer > 0.45f) chestTimer = -1f;
                else chestLean = -28f * Mathf.Sin(chestTimer / 0.45f * Mathf.PI);
            }
            float run = gait.RunAmount;

            // IK weights: feet follow the ground unless the body is in the air or a kick drives the leg.
            ikWeightL = Mathf.MoveTowards(ikWeightL, airborne ? 0f : 1f, dt * 8f);
            ikWeightR = Mathf.MoveTowards(ikWeightR, airborne || kicking ? 0f : 1f, dt * (kicking ? 30f : 8f));

            // ---------- pelvis: height, bob, twist ----------
            // Bob: lowest at mid-stance, twice per cycle.
            float bob = Mathf.Cos(gait.Phase * Mathf.PI * 4f - gait.StanceFraction * Mathf.PI * 2f) * 0.035f * run;
            float lunge = LungeWeight(now);
            float crouchDrop = crouch ? 0.1f : st == LocoState.SharpTurn ? 0.07f : st == LocoState.Recovery ? 0.05f : 0f;
            crouchDrop = Mathf.Max(crouchDrop, Mathf.Max(lunge * 0.16f, saveCrouch));
            // Pelvis twists toward the leg that swings forward; shoulders twist the other way.
            float legSignal = LegForwardSignal();
            float pelvisYaw = legSignal * 9f * run * (1f - kickW) + kickPelvisYaw;
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
            torso.localRotation = Quaternion.Euler(lean + kickTorsoPitch + chestLean + lunge * 18f + saveTorso, -pelvisYaw * (1.6f - kickW * 1.2f), -pelvisRoll + kickTorsoRoll);
            head.localRotation = Quaternion.Euler(-lean * 0.5f, pelvisYaw * 0.6f, 0f); // eyes stay on the play

            // Arms swing opposite to the legs, bent at the elbow. Off balance or jockeying: arms out wide.
            float armAmp = Mathf.Lerp(12f, 55f, run);
            float armOut = st == LocoState.Recovery ? 45f : crouch ? 24f : 8f + run * 4f;
            armL.localRotation = Quaternion.Euler(float.IsNaN(armOverrideL) ? legSignal * armAmp : 0f, 0f, float.IsNaN(armOverrideL) ? -armOut : armOverrideL);
            armR.localRotation = Quaternion.Euler(float.IsNaN(armOverrideR) ? -legSignal * armAmp : 0f, 0f, float.IsNaN(armOverrideR) ? armOut : armOverrideR);
            float elbow = -20f - run * 65f;
            elbowL.localRotation = Quaternion.Euler(elbow + Mathf.Max(0f, legSignal) * 15f * run, 0f, 0f);
            elbowR.localRotation = Quaternion.Euler(elbow + Mathf.Max(0f, -legSignal) * 15f * run, 0f, 0f);
            if (kickW > 0.01f && float.IsNaN(armOverrideL))
            {
                armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.Euler(kickArmL), kickW);
                armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Euler(kickArmR), kickW);
            }
            if (saveArms && saveArmW > 0.01f)
            {
                armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.Euler(saveArmL), saveArmW);
                armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Euler(saveArmR), saveArmW);
                elbowL.localRotation = Quaternion.Slerp(elbowL.localRotation, Quaternion.Euler(-10f, 0f, 0f), saveArmW);
                elbowR.localRotation = Quaternion.Slerp(elbowR.localRotation, Quaternion.Euler(-10f, 0f, 0f), saveArmW);
            }

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
