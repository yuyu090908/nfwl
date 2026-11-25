using UnityEngine;
using UnityEngine.Rendering;
using R = NFWL.BodyMeshes.Ring;
using static NFWL.CrystaliaProfiles;

namespace NFWL
{
    /// <summary>
    /// v0.7 next-gen Crystalia body (PHASE 7). Same joints as v0.6 (so all procedural animation, foot IK,
    /// kicks and saves are unchanged), but the visible body is made of smooth anatomical meshes:
    /// head with jaw and chin, neck, chest / waist (V taper), pelvis, deltoids, upper arms, forearms,
    /// hands, thighs, knees, calves, ankles and boots. Crystals (shoulders, forearms, shins, hair) sit on
    /// the joints without colliders, so they never block movement, and their number and glow follow
    /// rarity. PBR materials: knitted shirt with folds and side seams, skin with crease occlusion,
    /// leather boots, transparent glowing crystals.
    /// </summary>
    public partial class PlayerModel
    {
        Transform[] contactShadows;
        MaterialPropertyBlock shadowBlock;
        static Mesh quadMesh;

        /// <summary>v0.7 anatomical body: builds the same joints as the v0.6 body.</summary>
        static void BuildAnatomical(PlayerModel m, KitStyle kit, Element el, int number, int hairStyle, Rarity rarity, bool keeper)
        {
            CrystalLook look = CrystalLook.For(rarity);
            Texture2D fabric = ProceduralTextures.FabricNormal;
            Material shirt = NFWLShaders.PBR(kit.body, 0.32f, 0f, fabric, 0.55f, ProceduralTextures.FabricMask, null, null, new Color(1f, 1f, 1f, 0.5f), 0.6f);
            Material trim = NFWLShaders.PBR(kit.trim, 0.55f, 0.15f, null, 1f, null, null, null, null, 0f);
            Material shorts = NFWLShaders.PBR(kit.body, 0.28f, 0f, fabric, 0.45f, ProceduralTextures.FabricMask, null, null, new Color(1f, 1f, 1f, 0.5f), 0.6f);
            Material socks = NFWLShaders.PBR(Color.Lerp(kit.body, el.crystal, 0.12f), 0.22f, 0f, fabric, 0.8f, null, new Vector2(1f, 2f), null, null, 0.5f);
            Material boots = NFWLShaders.PBR(kit.boots, 0.72f, 0.25f, ProceduralTextures.LeatherNormal, 0.5f, null, new Vector2(2f, 2f), null, null, 0.6f);
            Material skin = NFWLShaders.PBR(el.skin, 0.42f, 0f, null, 1f, null, null, null, null, 0.7f);
            Material limbSkin = el.crystalSkin ? CrystalArt.CrystalSkin(el.skin, el.crystal) : skin;
            Material hair = CrystalArt.Glow(el.hair, el.crystal * 0.6f, 0.12f, 0.7f, null);
            Color crystalColour = Color.Lerp(new Color(0.8f, 0.82f, 0.88f), el.crystal, look.saturation);
            Material crystal = NFWLShaders.CrystalMat(crystalColour, rarity) ?? CrystalArt.Crystal(el.crystal);
            Material eyes = CrystalArt.Glow(el.eye, el.eye, 1.4f, 0.9f, null);
            Material front = NFWLShaders.PBR(Color.white, 0.32f, 0f, fabric, 0.4f, CrystalArt.KitPanel(kit, el.crystal, number, 0), null, null, null, 0f);
            Material back = NFWLShaders.PBR(Color.white, 0.32f, 0f, fabric, 0.4f, CrystalArt.KitPanel(kit, el.crystal, number, 1), null, null, null, 0f);
            Material shortsPanel = NFWLShaders.PBR(Color.white, 0.3f, 0f, null, 1f, CrystalArt.KitPanel(kit, el.crystal, number, 2), null, null, null, 0f);

            m.body = Visuals.Pivot(m.transform, Vector3.zero, "Body");
            m.hips = Visuals.Pivot(m.body, new Vector3(0f, HipHeight, 0f), "Hips");
            BodyMeshes.Attach(m.hips, BodyMeshes.Loft("Shorts", ShortsRings), shorts, "Shorts", new Vector3(0f, 0.02f, 0f));
            BodyMeshes.Attach(m.hips, BodyMeshes.Patch("ShortsNumber", ShortsRings, -0.01f, -0.09f, 22f, false), shortsPanel, "ShortsNumber",
                new Vector3(0f, 0.02f, 0f), Quaternion.Euler(0f, -28f, 0f), false);

            m.torso = Visuals.Pivot(m.hips, new Vector3(0f, 0.1f, 0f), "Torso");
            BodyMeshes.Attach(m.torso, BodyMeshes.Loft("Shirt", ShirtRings, 18), shirt, "Shirt");
            BodyMeshes.Attach(m.torso, BodyMeshes.Patch("ShirtFront", ShirtRings, 0.47f, 0.04f, 62f, false), front, "ShirtFront", null, null, false);
            BodyMeshes.Attach(m.torso, BodyMeshes.Patch("ShirtBack", ShirtRings, 0.47f, 0.04f, 62f, true), back, "ShirtBack", null, null, false);
            BodyMeshes.Attach(m.torso, BodyMeshes.Loft("Collar", new[]
            {
                new R(0.63f, 0.072f, 0.064f), new R(0.6f, 0.082f, 0.072f), new R(0.592f, 0.082f, 0.072f),
            }), trim, "Collar", null, null, false);
            BodyMeshes.Attach(m.torso, BodyMeshes.Loft("Neck", NeckRings, 12), skin, "Neck", new Vector3(0f, 0.6f, 0f));

            int shoulderShards = keeper ? Mathf.Min(2, look.shoulderShards) : look.shoulderShards;
            AddShardCluster(m.torso, new Vector3(-0.235f, 0.53f, 0f), -1f, crystal, shoulderShards, 0.22f);
            AddShardCluster(m.torso, new Vector3(0.235f, 0.53f, 0f), 1f, crystal, shoulderShards, 0.22f);

            m.head = Visuals.Pivot(m.torso, new Vector3(0f, 0.73f, 0f), "Head");
            BodyMeshes.Attach(m.head, BodyMeshes.Loft("Head", HeadRings, 16), skin, "Face");
            // Nose and brow ridge for a readable face under the lights.
            Visuals.Part(PrimitiveType.Sphere, m.head, new Vector3(0f, -0.025f, 0.108f), new Vector3(0.022f, 0.04f, 0.03f), skin, "Nose");
            Visuals.Part(PrimitiveType.Sphere, m.head, new Vector3(0f, 0.035f, 0.098f), new Vector3(0.15f, 0.025f, 0.03f), skin, "Brow");
            // Elf ears.
            Shard(m.head, new Vector3(0.095f, 0.0f, -0.01f), Quaternion.Euler(0f, 0f, -70f), new Vector3(0.16f, 0.13f, 0.08f), skin, "EarR", true);
            Shard(m.head, new Vector3(-0.095f, 0.0f, -0.01f), Quaternion.Euler(0f, 0f, 70f), new Vector3(0.16f, 0.13f, 0.08f), skin, "EarL", true);
            // Glowing eyes.
            Visuals.Part(PrimitiveType.Sphere, m.head, new Vector3(0.04f, 0.012f, 0.098f), new Vector3(0.034f, 0.022f, 0.016f), eyes, "EyeL");
            Visuals.Part(PrimitiveType.Sphere, m.head, new Vector3(-0.04f, 0.012f, 0.098f), new Vector3(0.034f, 0.022f, 0.016f), eyes, "EyeR");
            BuildHairV7(m.head, hair, crystal, hairStyle, look.hairShards);

            m.armL = BuildArmV7(m.torso, -1f, shirt, trim, limbSkin, skin, crystal, keeper ? 1 : look.armShards, out m.elbowL);
            m.armR = BuildArmV7(m.torso, 1f, shirt, trim, limbSkin, skin, crystal, keeper ? 1 : look.armShards, out m.elbowR);
            m.legL = BuildLegV7(m.hips, -1f, shorts, limbSkin, socks, trim, boots, crystal, look.legShards, out m.kneeL, out m.ankleL);
            m.legR = BuildLegV7(m.hips, 1f, shorts, limbSkin, socks, trim, boots, crystal, look.legShards, out m.kneeR, out m.ankleR);

            m.BuildContactShadows();
        }

        static void BuildHairV7(Transform head, Material hair, Material crystal, int style, int crystals)
        {
            // Hair cap follows the skull, a little larger and set back from the face.
            BodyMeshes.Attach(head, BodyMeshes.Loft("HairCap", new[]
            {
                new R(0.155f, 0.001f, 0.001f, -0.02f),
                new R(0.145f, 0.07f, 0.075f, -0.02f),
                new R(0.11f, 0.102f, 0.112f, -0.016f),
                new R(0.06f, 0.11f, 0.12f, -0.018f),
                new R(0.02f, 0.105f, 0.112f, -0.03f),
                new R(-0.04f, 0.09f, 0.085f, -0.05f),
                new R(-0.045f, 0.001f, 0.001f, -0.05f),
            }, 14), hair, "Hair");
            int spikes = 7 + style;
            for (int i = 0; i < spikes; i++)
            {
                float a = (i / (float)spikes) * 300f - 150f;
                Quaternion rot = Quaternion.Euler(-35f - (i % 3) * 15f, a * 0.6f, (i % 2 == 0 ? 1f : -1f) * (10f + i * 4f));
                Vector3 pos = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.07f, 0.11f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.05f - 0.03f);
                Shard(head, pos, rot, new Vector3(0.2f, 0.18f + (i % 3) * 0.04f, 0.2f), hair, "Lock", true);
            }
            // Crystal hair ornaments: more of them for higher rarity.
            for (int i = 0; i < crystals; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Shard(head, new Vector3(side * (0.06f + i * 0.012f), 0.12f - i * 0.015f, 0.03f - i * 0.02f),
                    Quaternion.Euler(-20f, 0f, -side * (30f + i * 8f)), new Vector3(0.1f, 0.13f, 0.1f), crystal, "HairCrystal");
            }
        }

        static Transform BuildArmV7(Transform torso, float side, Material sleeve, Material trim, Material skin, Material faceSkin, Material crystal, int shards, out Transform elbow)
        {
            Transform shoulder = Visuals.Pivot(torso, new Vector3(side * 0.28f, 0.48f, 0f), side < 0 ? "ArmL" : "ArmR");
            BodyMeshes.Attach(shoulder, BodyMeshes.Loft("Sleeve", SleeveRings), sleeve, "Sleeve");
            BodyMeshes.Attach(shoulder, BodyMeshes.Loft("Cuff", new[] { new R(-0.14f, 0.062f, 0.06f), new R(-0.155f, 0.063f, 0.061f), new R(-0.16f, 0.055f, 0.053f) }), trim, "Cuff", null, null, false);
            BodyMeshes.Attach(shoulder, BodyMeshes.Loft("UpperArm", UpperArmRings), skin, "Upper");
            elbow = Visuals.Pivot(shoulder, new Vector3(0f, -0.27f, 0f), "Elbow");
            BodyMeshes.Attach(elbow, BodyMeshes.Loft("Forearm", ForearmRings), skin, "Fore");
            BodyMeshes.Attach(elbow, BodyMeshes.Loft("Hand", HandRings), faceSkin, "Hand", new Vector3(0f, -0.01f, 0f), Quaternion.Euler(0f, side * 20f, 0f));
            for (int i = 0; i < shards; i++)
            {
                Shard(elbow, new Vector3(side * 0.035f, -0.05f - i * 0.06f, -0.01f),
                    Quaternion.Euler(0f, 0f, side * (-150f + i * 12f)), new Vector3(0.08f, 0.14f + i * 0.025f, 0.08f), crystal, "ArmCrystal");
            }
            return shoulder;
        }

        static Transform BuildLegV7(Transform hips, float side, Material shorts, Material skin, Material sock, Material trim, Material boot, Material crystal, int shards, out Transform knee, out Transform ankle)
        {
            Transform hip = Visuals.Pivot(hips, new Vector3(side * 0.1f, 0f, 0f), side < 0 ? "LegL" : "LegR");
            BodyMeshes.Attach(hip, BodyMeshes.Loft("ShortsLeg", ShortsLegRings), shorts, "ShortsLeg", null, Quaternion.Euler(0f, 0f, side * 3f));
            BodyMeshes.Attach(hip, BodyMeshes.Loft("Thigh", ThighRings), skin, "Thigh");
            knee = Visuals.Pivot(hip, new Vector3(0f, -ThighLength, 0f), "Knee");
            BodyMeshes.Attach(knee, BodyMeshes.Loft("Knee", KneeRings), skin, "KneeCap");
            BodyMeshes.Attach(knee, BodyMeshes.Loft("Sock", SockRings), sock, "Shin");
            BodyMeshes.Attach(knee, BodyMeshes.Loft("SockBand", new[] { new R(-0.05f, 0.053f, 0.056f), new R(-0.07f, 0.055f, 0.058f), new R(-0.085f, 0.054f, 0.058f, -0.004f) }), trim, "SockBand", null, null, false);
            ankle = Visuals.Pivot(knee, new Vector3(0f, -ShinLength, 0f), "Ankle");
            // Boot: lofted heel-to-toe; the baked rotation turns the loft axis (-Y) into +Z (forward).
            Mesh bootMesh = BodyMeshes.Loft("Boot", BootRings, 14, 2.5f, Quaternion.Euler(-90f, 0f, 0f));
            BodyMeshes.Attach(ankle, bootMesh, boot, "Boot", new Vector3(0f, -0.0f, 0f));
            for (int i = 0; i < shards; i++)
            {
                float a = (i - (shards - 1) * 0.5f) * 35f;
                Shard(knee, new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.045f, -0.3f + i % 2 * 0.04f, 0.04f),
                    Quaternion.Euler(12f, a, side * a * 0.2f), new Vector3(0.1f, 0.28f - (i % 2) * 0.06f, 0.1f), crystal, "LegCrystal");
            }
            return hip;
        }

        // ---------------- contact shadows ----------------

        static Mesh Quad
        {
            get
            {
                if (quadMesh != null) return quadMesh;
                quadMesh = new Mesh { name = "ContactQuad" };
                quadMesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) };
                quadMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                quadMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                quadMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                quadMesh.RecalculateBounds();
                return quadMesh;
            }
        }

        static Material contactMat;

        /// <summary>Soft dark contact under each boot and the body (ambient occlusion on the grass).</summary>
        void BuildContactShadows()
        {
            if (contactMat == null)
            {
                contactMat = NFWLShaders.Simple(NFWLShaders.ContactShadow, "NFWL Contact Shadow");
                if (contactMat == null) return;
                contactMat.enableInstancing = true;
            }
            contactShadows = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject go = new GameObject(i == 0 ? "ContactBody" : i == 1 ? "ContactFootL" : "ContactFootR");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = Quad;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = contactMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                contactShadows[i] = go.transform;
            }
            shadowBlock = new MaterialPropertyBlock();
        }

        static readonly int StrengthId = Shader.PropertyToID("_Strength");

        /// <summary>Keeps the contact shadows on the ground under the feet; they fade as a foot lifts.</summary>
        void UpdateContactShadows()
        {
            if (contactShadows == null) return;
            float sc = heightScale;
            Vector3 root = owner.transform.position;
            float bodyLift = body.localPosition.y;
            Place(contactShadows[0], new Vector3(root.x, root.y + 0.022f, root.z), 0.75f, Mathf.Clamp01(0.42f - bodyLift * 0.5f), 0f);
            PlaceFoot(contactShadows[1], ankleL, gait.Left, sc);
            PlaceFoot(contactShadows[2], ankleR, gait.Right, sc);
        }

        void PlaceFoot(Transform t, Transform ankle, GaitController.Foot foot, float sc)
        {
            Vector3 a = ankle.position;
            float ground = owner.transform.position.y;
            float h = Mathf.Max(0f, a.y - ground - AnkleHeight * sc);
            float strength = Mathf.Clamp01(0.55f - h * 2.2f);
            Vector3 fwd = ankle.forward; fwd.y = 0f;
            float yaw = fwd.sqrMagnitude > 1e-4f ? Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg : 0f;
            Vector3 centre = new Vector3(a.x, ground + 0.022f, a.z) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, 0.06f * sc);
            Place(t, centre, 0.3f, strength, yaw);
            t.localScale = new Vector3(0.16f, 1f, 0.34f); // parent (model) scale adds the player's height
        }

        void Place(Transform t, Vector3 pos, float size, float strength, float yaw)
        {
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            t.localScale = new Vector3(size, 1f, size);
            shadowBlock.SetFloat(StrengthId, strength);
            t.GetComponent<MeshRenderer>().SetPropertyBlock(shadowBlock);
        }
    }
}
