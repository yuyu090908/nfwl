using UnityEngine;

namespace NFWL
{
    /// <summary>Kit colours for one team (or the goalkeeper of a team).</summary>
    public struct Kit
    {
        public Color shirt, trim, shorts, socks, boots;

        public Kit(Color shirt, Color trim, Color shorts, Color socks, Color boots)
        {
            this.shirt = shirt; this.trim = trim; this.shorts = shorts; this.socks = socks; this.boots = boots;
        }
    }

    /// <summary>
    /// A stylised footballer built from primitive shapes, with procedural animation:
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

        public static PlayerModel Build(PlayerController owner, Kit kit, Color skin, Color hair, int hairStyle, float heightScale)
        {
            Transform root = owner.transform;
            PlayerModel m = new GameObject("Model").AddComponent<PlayerModel>();
            m.owner = owner;
            m.transform.SetParent(root, false);
            m.transform.localScale = Vector3.one * heightScale;

            Material shirt = Visuals.Mat(kit.shirt, 0.25f);
            Material trim = Visuals.Mat(kit.trim, 0.25f);
            Material shorts = Visuals.Mat(kit.shorts, 0.2f);
            Material socks = Visuals.Mat(kit.socks, 0.2f);
            Material boots = Visuals.Mat(kit.boots, 0.6f);
            Material skinM = Visuals.Mat(skin, 0.3f);
            Material hairM = Visuals.Mat(hair, 0.15f);

            // body: everything that leans / dives.
            m.body = Visuals.Pivot(m.transform, Vector3.zero, "Body");
            m.hips = Visuals.Pivot(m.body, new Vector3(0f, 0.95f, 0f), "Hips");
            Visuals.Part(PrimitiveType.Cube, m.hips, new Vector3(0f, 0.02f, 0f), new Vector3(0.36f, 0.22f, 0.22f), shorts, "Shorts");

            m.torso = Visuals.Pivot(m.hips, new Vector3(0f, 0.1f, 0f), "Torso");
            Visuals.Part(PrimitiveType.Cube, m.torso, new Vector3(0f, 0.27f, 0f), new Vector3(0.42f, 0.52f, 0.24f), shirt, "Shirt");
            Visuals.Part(PrimitiveType.Cube, m.torso, new Vector3(0f, 0.27f, 0f), new Vector3(0.43f, 0.06f, 0.25f), trim, "Band");
            Visuals.Part(PrimitiveType.Cube, m.torso, new Vector3(0f, 0.54f, 0f), new Vector3(0.2f, 0.03f, 0.2f), trim, "Collar");
            Visuals.Part(PrimitiveType.Cylinder, m.torso, new Vector3(0f, 0.58f, 0f), new Vector3(0.1f, 0.05f, 0.1f), skinM, "Neck");

            m.head = Visuals.Pivot(m.torso, new Vector3(0f, 0.72f, 0f), "Head");
            Visuals.Part(PrimitiveType.Sphere, m.head, Vector3.zero, new Vector3(0.22f, 0.25f, 0.23f), skinM, "Face");
            // Hair styles: 0 short, 1 buzz, 2 tall top, 3 long.
            switch (hairStyle)
            {
                case 1:
                    Visuals.Part(PrimitiveType.Sphere, m.head, new Vector3(0f, 0.035f, -0.01f), new Vector3(0.225f, 0.2f, 0.23f), hairM, "Hair");
                    break;
                case 2:
                    Visuals.Part(PrimitiveType.Sphere, m.head, new Vector3(0f, 0.07f, -0.01f), new Vector3(0.23f, 0.2f, 0.235f), hairM, "Hair");
                    Visuals.Part(PrimitiveType.Cube, m.head, new Vector3(0f, 0.13f, 0.01f), new Vector3(0.1f, 0.08f, 0.18f), hairM, "Top");
                    break;
                case 3:
                    Visuals.Part(PrimitiveType.Sphere, m.head, new Vector3(0f, 0.05f, -0.03f), new Vector3(0.24f, 0.23f, 0.25f), hairM, "Hair");
                    Visuals.Part(PrimitiveType.Cube, m.head, new Vector3(0f, -0.06f, -0.09f), new Vector3(0.22f, 0.2f, 0.07f), hairM, "Back");
                    break;
                default:
                    Visuals.Part(PrimitiveType.Sphere, m.head, new Vector3(0f, 0.06f, -0.02f), new Vector3(0.235f, 0.18f, 0.24f), hairM, "Hair");
                    break;
            }
            // Eyes so you can see where he looks.
            Material eye = Visuals.Mat(new Color(0.05f, 0.05f, 0.08f), 0.5f);
            Visuals.Part(PrimitiveType.Sphere, m.head, new Vector3(0.045f, 0.01f, 0.105f), Vector3.one * 0.03f, eye, "EyeL");
            Visuals.Part(PrimitiveType.Sphere, m.head, new Vector3(-0.045f, 0.01f, 0.105f), Vector3.one * 0.03f, eye, "EyeR");

            // Arms: pivot at the shoulder, sleeve + forearm.
            m.armL = BuildArm(m.torso, -1f, shirt, skinM, out m.elbowL);
            m.armR = BuildArm(m.torso, 1f, shirt, skinM, out m.elbowR);

            // Legs: pivot at the hip, thigh (shorts/skin) + shin (sock) + boot.
            m.legL = BuildLeg(m.hips, -1f, shorts, skinM, socks, boots, out m.kneeL);
            m.legR = BuildLeg(m.hips, 1f, shorts, skinM, socks, boots, out m.kneeR);

            // Soft shadow blob for readability from the high camera.
            GameObject blob = Visuals.Part(PrimitiveType.Cylinder, m.transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.6f, 0.002f, 0.6f),
                Visuals.Mat(new Color(0.1f, 0.3f, 0.1f), 0f), "Shadow");
            blob.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return m;
        }

        static Transform BuildArm(Transform torso, float side, Material sleeve, Material skin, out Transform elbow)
        {
            Transform shoulder = Visuals.Pivot(torso, new Vector3(side * 0.27f, 0.48f, 0f), side < 0 ? "ArmL" : "ArmR");
            Visuals.Part(PrimitiveType.Sphere, shoulder, Vector3.zero, Vector3.one * 0.14f, sleeve, "Shoulder");
            Visuals.Part(PrimitiveType.Capsule, shoulder, new Vector3(0f, -0.13f, 0f), new Vector3(0.1f, 0.13f, 0.1f), sleeve, "Upper");
            elbow = Visuals.Pivot(shoulder, new Vector3(0f, -0.26f, 0f), "Elbow");
            Visuals.Part(PrimitiveType.Capsule, elbow, new Vector3(0f, -0.13f, 0f), new Vector3(0.08f, 0.13f, 0.08f), skin, "Fore");
            return shoulder;
        }

        static Transform BuildLeg(Transform hips, float side, Material shorts, Material skin, Material sock, Material boot, out Transform knee)
        {
            Transform hip = Visuals.Pivot(hips, new Vector3(side * 0.1f, 0f, 0f), side < 0 ? "LegL" : "LegR");
            Visuals.Part(PrimitiveType.Capsule, hip, new Vector3(0f, -0.1f, 0f), new Vector3(0.16f, 0.12f, 0.16f), shorts, "ShortsLeg");
            Visuals.Part(PrimitiveType.Capsule, hip, new Vector3(0f, -0.28f, 0f), new Vector3(0.13f, 0.14f, 0.13f), skin, "Thigh");
            knee = Visuals.Pivot(hip, new Vector3(0f, -0.44f, 0f), "Knee");
            Visuals.Part(PrimitiveType.Capsule, knee, new Vector3(0f, -0.22f, 0f), new Vector3(0.12f, 0.22f, 0.12f), sock, "Shin");
            Visuals.Part(PrimitiveType.Cube, knee, new Vector3(0f, -0.46f, 0.05f), new Vector3(0.11f, 0.07f, 0.26f), boot, "Boot");
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
