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
        Transform legL, legR, kneeL, kneeR, armL, armR, elbowL, elbowR;

        PlayerController owner;
        float phase;
        float kickTimer = -1f;
        float diveTimer = -1f;
        float diveSide = 1f;
        float celebrateTimer = -1f;
        float lean;

        const float KickTime = 0.32f;
        const float DiveTime = 0.9f;

        public static PlayerModel Build(PlayerController owner, KitStyle kit, Element el, int number, int hairStyle, float heightScale)
        {
            Transform root = owner.transform;
            PlayerModel m = new GameObject("Model").AddComponent<PlayerModel>();
            m.owner = owner;
            m.transform.SetParent(root, false);
            m.transform.localScale = Vector3.one * heightScale;

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

            m.legL = BuildLeg(m.hips, -1f, shorts, limbSkin, socks, trim, boots, crystal, out m.kneeL);
            m.legR = BuildLeg(m.hips, 1f, shorts, limbSkin, socks, trim, boots, crystal, out m.kneeR);

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

        static Transform BuildLeg(Transform hips, float side, Material shorts, Material skin, Material sock, Material trim, Material boot, Material crystal, out Transform knee)
        {
            Transform hip = Visuals.Pivot(hips, new Vector3(side * 0.1f, 0f, 0f), side < 0 ? "LegL" : "LegR");
            Visuals.Part(PrimitiveType.Capsule, hip, new Vector3(0f, -0.1f, 0f), new Vector3(0.17f, 0.12f, 0.17f), shorts, "ShortsLeg");
            Visuals.Part(PrimitiveType.Capsule, hip, new Vector3(0f, -0.29f, 0f), new Vector3(0.13f, 0.14f, 0.13f), skin, "Thigh");
            knee = Visuals.Pivot(hip, new Vector3(0f, -0.44f, 0f), "Knee");
            Visuals.Part(PrimitiveType.Capsule, knee, new Vector3(0f, -0.22f, 0f), new Vector3(0.12f, 0.22f, 0.12f), sock, "Shin");
            Visuals.Part(PrimitiveType.Cylinder, knee, new Vector3(0f, -0.04f, 0f), new Vector3(0.125f, 0.015f, 0.125f), trim, "SockBand");
            Visuals.Part(PrimitiveType.Cube, knee, new Vector3(0f, -0.46f, 0.05f), new Vector3(0.11f, 0.07f, 0.27f), boot, "Boot");
            // Crystal greaves on the shin, pointing up past the knee (all designs).
            for (int i = 0; i < 3; i++)
            {
                float a = (i - 1) * 35f;
                Shard(knee, new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.05f, -0.3f + i % 2 * 0.04f, 0.04f),
                    Quaternion.Euler(12f, a, side * (i - 1) * 8f), new Vector3(0.12f, 0.32f - (i % 2) * 0.06f, 0.12f), crystal, "LegCrystal");
            }
            return hip;
        }

        public void PlayKick() { kickTimer = 0f; }

        public void PlayDive(float side) { diveTimer = 0f; diveSide = Mathf.Sign(side == 0f ? 1f : side); }

        public void PlayCelebrate() { celebrateTimer = 0f; }

        void Update()
        {
            if (owner == null) return;
            float dt = Time.deltaTime;
            float speed = owner.Speed;
            float run = Mathf.Clamp01(speed / 6.5f);

            // Run cycle: stride frequency rises with speed.
            phase += dt * (4f + speed * 1.6f);
            float s = Mathf.Sin(phase);
            float amp = Mathf.Lerp(0f, 50f, run);

            float legLAngle = s * amp;
            float legRAngle = -s * amp;
            float kneeLAngle = Mathf.Max(0f, Mathf.Sin(phase + 1.2f)) * amp * 1.4f + 5f;
            float kneeRAngle = Mathf.Max(0f, Mathf.Sin(phase + 1.2f + Mathf.PI)) * amp * 1.4f + 5f;

            // Kick: wind up then swing the right leg through.
            if (kickTimer >= 0f)
            {
                kickTimer += dt;
                float k = kickTimer / KickTime;
                if (k >= 1f) kickTimer = -1f;
                else
                {
                    float swing = k < 0.4f ? Mathf.Lerp(0f, 50f, k / 0.4f) : Mathf.Lerp(50f, -75f, (k - 0.4f) / 0.6f);
                    legRAngle = swing;
                    kneeRAngle = k < 0.4f ? 70f * (k / 0.4f) : Mathf.Lerp(70f, 0f, (k - 0.4f) / 0.6f);
                    legLAngle = -10f;
                }
            }

            legL.localRotation = Quaternion.Euler(legLAngle, 0f, 0f);
            legR.localRotation = Quaternion.Euler(legRAngle, 0f, 0f);
            kneeL.localRotation = Quaternion.Euler(kneeLAngle, 0f, 0f);
            kneeR.localRotation = Quaternion.Euler(kneeRAngle, 0f, 0f);

            // Arms swing opposite to legs, bent at the elbow.
            float armAmp = amp * 0.8f;
            armL.localRotation = Quaternion.Euler(-s * armAmp, 0f, -8f);
            armR.localRotation = Quaternion.Euler(s * armAmp, 0f, 8f);
            elbowL.localRotation = Quaternion.Euler(-25f - run * 40f, 0f, 0f);
            elbowR.localRotation = Quaternion.Euler(-25f - run * 40f, 0f, 0f);

            // Body bob and forward lean when running.
            lean = Mathf.Lerp(lean, run * 12f, dt * 6f);
            hips.localPosition = new Vector3(0f, 0.95f + Mathf.Abs(Mathf.Cos(phase)) * 0.05f * run, 0f);
            torso.localRotation = Quaternion.Euler(lean, Mathf.Sin(phase) * 6f * run, 0f);

            Quaternion bodyRot = Quaternion.identity;
            Vector3 bodyPos = Vector3.zero;

            // Goalkeeper dive: roll to the side and drop.
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
                    armL.localRotation = Quaternion.Euler(0f, 0f, -150f * w);
                    armR.localRotation = Quaternion.Euler(0f, 0f, 150f * w);
                }
            }

            // Celebration: jump with both arms up.
            if (celebrateTimer >= 0f)
            {
                celebrateTimer += dt;
                if (celebrateTimer > 2.2f) celebrateTimer = -1f;
                else
                {
                    float j = Mathf.Abs(Mathf.Sin(celebrateTimer * 7f));
                    bodyPos.y += j * 0.35f;
                    armL.localRotation = Quaternion.Euler(0f, 0f, -160f + j * 20f);
                    armR.localRotation = Quaternion.Euler(0f, 0f, 160f - j * 20f);
                }
            }

            body.localRotation = bodyRot;
            body.localPosition = bodyPos;
        }
    }
}
