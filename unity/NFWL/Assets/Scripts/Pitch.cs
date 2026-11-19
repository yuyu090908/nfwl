using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// Builds the 42m x 25m pitch (Full Spec 3.1), lines, goals with nets, corner flags,
    /// and the stadium around it (stands with crowd, advertising boards, floodlights).
    /// Everything is made from primitive shapes and procedural textures at runtime.
    /// </summary>
    public class Pitch : MonoBehaviour
    {
        static readonly Color GrassDark = new Color(0.20f, 0.52f, 0.18f);
        static readonly Color GrassLight = new Color(0.26f, 0.60f, 0.22f);
        static readonly Color Surround = new Color(0.15f, 0.40f, 0.14f);
        static readonly Color Track = new Color(0.12f, 0.14f, 0.2f);
        static readonly Color LineWhite = new Color(0.97f, 0.97f, 0.97f);
        static readonly Color NetWhite = new Color(0.92f, 0.94f, 0.96f);
        static readonly Color Concrete = new Color(0.32f, 0.34f, 0.4f);
        static readonly Color Steel = new Color(0.55f, 0.58f, 0.65f);
        const float LineY = 0.006f;

        bool built;

        public void Build()
        {
            if (built) return;
            built = true;

            float L = PitchDims.HalfLength * 2f;
            float W = PitchDims.HalfWidth * 2f;
            float LW = PitchDims.LineWidth;

            // Ground: surround grass (collides), outer track, and mown stripes on the pitch.
            Material surround = Visuals.Mat(Surround, 0.05f, Visuals.Grass, new Vector2(12f, 8f));
            AddBox("Ground", new Vector3(0f, -0.1f, 0f), new Vector3(L + 8f, 0.2f, W + 8f), surround, true);
            AddBox("Track", new Vector3(0f, -0.12f, 0f), new Vector3(L + 30f, 0.2f, W + 30f), Visuals.Mat(Track, 0.1f), true);

            const int stripes = 12;
            Material dark = Visuals.Mat(GrassDark, 0.05f, Visuals.Grass, new Vector2(1f, 6f));
            Material light = Visuals.Mat(GrassLight, 0.05f, Visuals.Grass, new Vector2(1f, 6f));
            for (int i = 0; i < stripes; i++)
            {
                float stripeLen = (L + 2f) / stripes;
                float x = -PitchDims.HalfLength - 1f + stripeLen * (i + 0.5f);
                AddBox("Stripe", new Vector3(x, 0.001f, 0f), new Vector3(stripeLen, 0.002f, W + 2f), i % 2 == 0 ? dark : light, false);
            }

            // Lines.
            Material line = Visuals.Mat(LineWhite, 0.1f);
            AddBox("Touchline", new Vector3(0f, LineY, PitchDims.HalfWidth), new Vector3(L + LW, 0.01f, LW), line, false);
            AddBox("Touchline", new Vector3(0f, LineY, -PitchDims.HalfWidth), new Vector3(L + LW, 0.01f, LW), line, false);
            AddBox("GoalLine", new Vector3(PitchDims.HalfLength, LineY, 0f), new Vector3(LW, 0.01f, W + LW), line, false);
            AddBox("GoalLine", new Vector3(-PitchDims.HalfLength, LineY, 0f), new Vector3(LW, 0.01f, W + LW), line, false);
            AddBox("HalfwayLine", new Vector3(0f, LineY, 0f), new Vector3(LW, 0.01f, W), line, false);

            AddArc(Vector2.zero, PitchDims.CenterRadius, 0f, 360f, 48, line);
            AddArc(new Vector2(PitchDims.HalfLength, 0f), PitchDims.PenaltyRadius, 90f, 270f, 40, line);
            AddArc(new Vector2(-PitchDims.HalfLength, 0f), PitchDims.PenaltyRadius, -90f, 90f, 40, line);

            AddDisc(new Vector3(0f, LineY, 0f), 0.25f, line);
            AddDisc(new Vector3(PitchDims.HalfLength - PitchDims.PenaltySpotDist, LineY, 0f), 0.2f, line);
            AddDisc(new Vector3(-PitchDims.HalfLength + PitchDims.PenaltySpotDist, LineY, 0f), 0.2f, line);

            BuildGoal(1f);
            BuildGoal(-1f);

            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                BuildCornerFlag(new Vector3(sx * PitchDims.HalfLength, 0f, sz * PitchDims.HalfWidth));

            BuildStadium();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        GameObject AddBox(string name, Vector3 center, Vector3 size, Material mat, bool collide, float yaw = 0f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            Finish(go, mat, collide);
            return go;
        }

        void AddDisc(Vector3 center, float diameter, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Spot";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(diameter, 0.005f, diameter);
            Finish(go, mat, false);
        }

        void AddPost(Vector3 center, float diameter, float height, Material mat, bool collide)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Post";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            Finish(go, mat, collide);
        }

        void AddArc(Vector2 center, float radius, float startDeg, float endDeg, int segments, Material mat)
        {
            float step = (endDeg - startDeg) / segments;
            float segLen = 2f * radius * Mathf.Sin(Mathf.Abs(step) * Mathf.Deg2Rad * 0.5f) + 0.02f;
            for (int i = 0; i < segments; i++)
            {
                float midDeg = startDeg + step * (i + 0.5f);
                float rad = midDeg * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(center.x + radius * Mathf.Cos(rad), LineY, center.y + radius * Mathf.Sin(rad));
                // Unity yaw turns +X towards -Z, so the tangent of angle a is a yaw of -(a + 90).
                AddBox("Arc", pos, new Vector3(segLen, 0.01f, PitchDims.LineWidth), mat, false, -(midDeg + 90f));
            }
        }

        static void Finish(GameObject go, Material mat, bool collide)
        {
            if (!collide)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) DestroyImmediate(c);
            }
            Renderer r = go.GetComponent<Renderer>();
            if (mat == null) r.enabled = false; // invisible collider
            else r.sharedMaterial = mat;
        }

        // -------------------------------------------------------------------
        // Goals
        // -------------------------------------------------------------------

        void BuildGoal(float sign)
        {
            const float post = 0.12f;
            float lineX = sign * PitchDims.HalfLength;
            float backX = sign * (PitchDims.HalfLength + PitchDims.GoalDepth);
            float midX = sign * (PitchDims.HalfLength + PitchDims.GoalDepth * 0.5f);
            float gh = PitchDims.GoalHeight;
            float gw = PitchDims.GoalHalfWidth;
            Material white = Visuals.Mat(Color.white, 0.6f);
            Material net = Visuals.Mat(NetWhite, 0.05f);

            // Frame: posts sit just outside the goal mouth so the inner width stays 3m.
            AddPost(new Vector3(lineX + sign * post * 0.5f, gh * 0.5f, gw + post * 0.5f), post, gh, white, true);
            AddPost(new Vector3(lineX + sign * post * 0.5f, gh * 0.5f, -gw - post * 0.5f), post, gh, white, true);
            AddBox("Crossbar", new Vector3(lineX + sign * post * 0.5f, gh + post * 0.5f, 0f), new Vector3(post, post, gw * 2f + post * 2f), white, true);
            // Back frame.
            AddPost(new Vector3(backX, gh * 0.5f, gw), 0.05f, gh, white, false);
            AddPost(new Vector3(backX, gh * 0.5f, -gw), 0.05f, gh, white, false);

            // Invisible solid walls that stop the ball.
            const float wall = 0.05f;
            AddBox("NetCollider", new Vector3(backX + sign * wall, gh * 0.5f, 0f), new Vector3(wall, gh, gw * 2f), null, true);
            AddBox("NetCollider", new Vector3(midX, gh * 0.5f, gw + wall), new Vector3(PitchDims.GoalDepth, gh, wall), null, true);
            AddBox("NetCollider", new Vector3(midX, gh * 0.5f, -gw - wall), new Vector3(PitchDims.GoalDepth, gh, wall), null, true);
            AddBox("NetCollider", new Vector3(midX, gh + wall, 0f), new Vector3(PitchDims.GoalDepth, wall, gw * 2f), null, true);

            // Visible net: a mesh of thin strings.
            const float cell = 0.2f;
            const float t = 0.012f;
            for (float z = -gw; z <= gw + 0.001f; z += cell)
            {
                AddBox("Net", new Vector3(backX, gh * 0.5f, z), new Vector3(t, gh, t), net, false);          // back verticals
                AddBox("Net", new Vector3(midX, gh, z), new Vector3(PitchDims.GoalDepth, t, t), net, false);  // roof strings
            }
            for (float y = 0f; y <= gh + 0.001f; y += cell)
            {
                AddBox("Net", new Vector3(backX, y, 0f), new Vector3(t, t, gw * 2f), net, false);           // back horizontals
                AddBox("Net", new Vector3(midX, y, gw), new Vector3(PitchDims.GoalDepth, t, t), net, false);  // side horizontals
                AddBox("Net", new Vector3(midX, y, -gw), new Vector3(PitchDims.GoalDepth, t, t), net, false);
            }
            for (float x = 0f; x <= PitchDims.GoalDepth + 0.001f; x += cell)
            {
                float px = lineX + sign * x;
                AddBox("Net", new Vector3(px, gh * 0.5f, gw), new Vector3(t, gh, t), net, false);           // side verticals
                AddBox("Net", new Vector3(px, gh * 0.5f, -gw), new Vector3(t, gh, t), net, false);
                AddBox("Net", new Vector3(px, gh, 0f), new Vector3(t, t, gw * 2f), net, false);             // roof cross
            }
        }

        void BuildCornerFlag(Vector3 at)
        {
            AddPost(at + new Vector3(0f, 0.75f, 0f), 0.04f, 1.5f, Visuals.Mat(Color.white, 0.3f), false);
            AddBox("Flag", at + new Vector3(0f, 1.38f, 0.17f * Mathf.Sign(-at.z)), new Vector3(0.01f, 0.24f, 0.32f),
                Visuals.Mat(new Color(1f, 0.8f, 0.15f), 0.2f), false);
        }

        // -------------------------------------------------------------------
        // Stadium
        // -------------------------------------------------------------------

        void BuildStadium()
        {
            float hl = PitchDims.HalfLength;
            float hw = PitchDims.HalfWidth;

            // Advertising boards (collide, so the ball bounces back into view).
            Material board = Visuals.Mat(Color.white, 0.4f, Visuals.Board, new Vector2(6f, 1f));
            Material boardEnd = Visuals.Mat(Color.white, 0.4f, Visuals.Board, new Vector2(3f, 1f));
            float bz = hw + 2.2f, bx = hl + 3f;
            AddBox("Boards", new Vector3(0f, 0.45f, bz), new Vector3(2f * bx, 0.9f, 0.12f), board, true);
            AddBox("Boards", new Vector3(0f, 0.45f, -bz), new Vector3(2f * bx, 0.9f, 0.12f), board, true);
            AddBox("Boards", new Vector3(bx, 0.45f, 0f), new Vector3(0.12f, 0.9f, 2f * bz), boardEnd, true);
            AddBox("Boards", new Vector3(-bx, 0.45f, 0f), new Vector3(0.12f, 0.9f, 2f * bz), boardEnd, true);

            // Stands: stepped tiers with crowd on each riser.
            BuildStand(new Vector3(0f, 0f, bz + 3f), 2f * bx + 6f, 0f, 8, true);       // far side (behind the play)
            BuildStand(new Vector3(0f, 0f, -bz - 3f), 2f * bx + 6f, 180f, 2, false); // near side: low, so it never hides the play
            BuildStand(new Vector3(bx + 3f, 0f, 0f), 2f * bz + 6f, 90f, 6, true);
            BuildStand(new Vector3(-bx - 3f, 0f, 0f), 2f * bz + 6f, -90f, 6, true);

            // Floodlight towers at the corners.
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                BuildFloodlight(new Vector3(sx * (bx + 9f), 0f, sz * (bz + 9f)));

            // Benches / dugouts on the far side.
            Material dug = Visuals.Mat(new Color(0.08f, 0.1f, 0.18f), 0.4f);
            Material seat = Visuals.Mat(new Color(0.1f, 0.6f, 0.95f), 0.3f);
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 c = new Vector3(sx * 6f, 0f, hw + 1.3f);
                AddBox("Dugout", c + new Vector3(0f, 1.1f, 0.35f), new Vector3(4f, 0.08f, 1.0f), dug, false);
                AddBox("Dugout", c + new Vector3(0f, 0.55f, 0.8f), new Vector3(4f, 1.1f, 0.08f), dug, false);
                AddBox("Seats", c + new Vector3(0f, 0.25f, 0.55f), new Vector3(3.6f, 0.5f, 0.4f), seat, false);
            }
        }

        void BuildStand(Vector3 center, float length, float yaw, int tiers, bool withRoof)
        {
            Transform stand = new GameObject("Stand").transform;
            stand.SetParent(transform, false);
            stand.localPosition = center;
            stand.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Material concrete = Visuals.Mat(Concrete, 0.1f);
            Material crowdMat = Visuals.Mat(Color.white, 0.05f, Visuals.Crowd, new Vector2(length / 6f, 1f));
            Material roof = Visuals.Mat(new Color(0.1f, 0.12f, 0.2f), 0.5f);
            const float rise = 0.75f, depth = 1.1f;
            for (int i = 0; i < tiers; i++)
            {
                float y = 0.4f + i * rise;
                float z = i * depth;
                // Step (top surface) and riser with spectators.
                Visuals.Part(PrimitiveType.Cube, stand, new Vector3(0f, y * 0.5f, z + depth * 0.5f), new Vector3(length, y, depth), concrete, "Tier");
                Visuals.Part(PrimitiveType.Cube, stand, new Vector3(0f, y + 0.38f, z + depth * 0.25f), new Vector3(length, 0.75f, 0.06f), crowdMat, "Crowd");
            }
            // Back wall and roof.
            float top = 0.4f + tiers * rise;
            if (!withRoof) return;
            Visuals.Part(PrimitiveType.Cube, stand, new Vector3(0f, top * 0.5f + 1f, tiers * depth + 0.2f), new Vector3(length, top + 2f, 0.4f), concrete, "BackWall");
            Visuals.Part(PrimitiveType.Cube, stand, new Vector3(0f, top + 3f, tiers * depth * 0.55f), new Vector3(length + 1f, 0.2f, tiers * depth * 0.95f), roof, "Roof");
        }

        void BuildFloodlight(Vector3 at)
        {
            Material steel = Visuals.Mat(Steel, 0.5f);
            Material lamp = Visuals.Mat(new Color(1f, 0.98f, 0.85f), 0.9f);
            const float h = 22f;
            AddPost(at + new Vector3(0f, h * 0.5f, 0f), 0.6f, h, steel, false);
            Transform head = new GameObject("FloodlightHead").transform;
            head.SetParent(transform, false);
            head.localPosition = at + new Vector3(0f, h + 1f, 0f);
            Vector3 toCenter = -at;
            toCenter.y = 0f;
            head.localRotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up) * Quaternion.Euler(30f, 0f, 0f);
            Visuals.Part(PrimitiveType.Cube, head, Vector3.zero, new Vector3(4.2f, 2.6f, 0.4f), steel, "Frame");
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 5; c++)
                Visuals.Part(PrimitiveType.Cube, head, new Vector3(-1.6f + c * 0.8f, -0.8f + r * 0.8f, 0.22f), new Vector3(0.6f, 0.6f, 0.08f), lamp, "Lamp");
        }
    }
}
