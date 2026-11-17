using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// Builds the 42m x 25m pitch, lines and both goals from primitive shapes at runtime,
    /// so no 3D model files are needed for Phase 1.
    /// </summary>
    public class Pitch : MonoBehaviour
    {
        static readonly Color GrassDark = new Color(0.10f, 0.42f, 0.12f);
        static readonly Color GrassLight = new Color(0.14f, 0.50f, 0.16f);
        static readonly Color Surround = new Color(0.06f, 0.28f, 0.08f);
        static readonly Color LineWhite = new Color(0.95f, 0.95f, 0.95f);
        static readonly Color NetGrey = new Color(0.75f, 0.75f, 0.75f, 1f);
        const float LineY = 0.006f;

        bool built;

        public void Build()
        {
            if (built) return;
            built = true;

            float L = PitchDims.HalfLength * 2f;
            float W = PitchDims.HalfWidth * 2f;
            float LW = PitchDims.LineWidth;

            // Ground: surround (collides) with mown stripes on top (visual only). Top surface at y = 0.
            AddBox("Ground", new Vector3(0f, -0.1f, 0f), new Vector3(L + 12f, 0.2f, W + 12f), Surround, true);
            const int stripes = 8;
            for (int i = 0; i < stripes; i++)
            {
                float stripeLen = L / stripes;
                float x = -PitchDims.HalfLength + stripeLen * (i + 0.5f);
                AddBox("Stripe", new Vector3(x, 0.001f, 0f), new Vector3(stripeLen, 0.002f, W), i % 2 == 0 ? GrassDark : GrassLight, false);
            }

            // Lines.
            AddBox("Touchline", new Vector3(0f, LineY, PitchDims.HalfWidth), new Vector3(L + LW, 0.01f, LW), LineWhite, false);
            AddBox("Touchline", new Vector3(0f, LineY, -PitchDims.HalfWidth), new Vector3(L + LW, 0.01f, LW), LineWhite, false);
            AddBox("GoalLine", new Vector3(PitchDims.HalfLength, LineY, 0f), new Vector3(LW, 0.01f, W + LW), LineWhite, false);
            AddBox("GoalLine", new Vector3(-PitchDims.HalfLength, LineY, 0f), new Vector3(LW, 0.01f, W + LW), LineWhite, false);
            AddBox("HalfwayLine", new Vector3(0f, LineY, 0f), new Vector3(LW, 0.01f, W), LineWhite, false);

            AddArc(Vector2.zero, PitchDims.CenterRadius, 0f, 360f, 48);                        // centre circle
            AddArc(new Vector2(PitchDims.HalfLength, 0f), PitchDims.PenaltyRadius, 90f, 270f, 40);  // penalty areas
            AddArc(new Vector2(-PitchDims.HalfLength, 0f), PitchDims.PenaltyRadius, -90f, 90f, 40);

            AddDisc(new Vector3(0f, LineY, 0f), 0.2f);
            AddDisc(new Vector3(PitchDims.HalfLength - PitchDims.PenaltySpotDist, LineY, 0f), 0.2f);
            AddDisc(new Vector3(-PitchDims.HalfLength + PitchDims.PenaltySpotDist, LineY, 0f), 0.2f);

            BuildGoal(1f);
            BuildGoal(-1f);
        }

        GameObject AddBox(string name, Vector3 center, Vector3 size, Color color, bool collide, float yaw = 0f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            Finish(go, color, collide);
            return go;
        }

        void AddDisc(Vector3 center, float diameter)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Spot";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            // The Unity cylinder is 2m tall and 1m wide.
            go.transform.localScale = new Vector3(diameter, 0.005f, diameter);
            Finish(go, LineWhite, false);
        }

        void AddPost(Vector3 center, float diameter, float height)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Post";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            Finish(go, LineWhite, true);
        }

        void AddArc(Vector2 center, float radius, float startDeg, float endDeg, int segments)
        {
            float step = (endDeg - startDeg) / segments;
            float segLen = 2f * radius * Mathf.Sin(Mathf.Abs(step) * Mathf.Deg2Rad * 0.5f) + 0.02f;
            for (int i = 0; i < segments; i++)
            {
                float midDeg = startDeg + step * (i + 0.5f);
                float rad = midDeg * Mathf.Deg2Rad;
                // Angle is measured from +X towards +Z.
                Vector3 pos = new Vector3(center.x + radius * Mathf.Cos(rad), LineY, center.y + radius * Mathf.Sin(rad));
                // Unity yaw turns +X towards -Z, so the tangent of angle a is a yaw of -(a + 90).
                AddBox("Arc", pos, new Vector3(segLen, 0.01f, PitchDims.LineWidth), LineWhite, false, -(midDeg + 90f));
            }
        }

        void BuildGoal(float sign)
        {
            const float post = 0.1f;
            float lineX = sign * PitchDims.HalfLength;
            float backX = sign * (PitchDims.HalfLength + PitchDims.GoalDepth);
            float midX = sign * (PitchDims.HalfLength + PitchDims.GoalDepth * 0.5f);
            float gh = PitchDims.GoalHeight;
            float gw = PitchDims.GoalHalfWidth;

            // Posts sit just outside the goal mouth so the inner width stays 3m.
            AddPost(new Vector3(lineX + sign * post * 0.5f, gh * 0.5f, gw + post * 0.5f), post, gh);
            AddPost(new Vector3(lineX + sign * post * 0.5f, gh * 0.5f, -gw - post * 0.5f), post, gh);
            AddBox("Crossbar", new Vector3(lineX + sign * post * 0.5f, gh + post * 0.5f, 0f), new Vector3(post, post, gw * 2f + post * 2f), LineWhite, true);

            // Net: back, sides and roof. Thin boxes that stop the ball.
            const float net = 0.03f;
            AddBox("Net", new Vector3(backX, gh * 0.5f, 0f), new Vector3(net, gh, gw * 2f), NetGrey, true);
            AddBox("Net", new Vector3(midX, gh * 0.5f, gw + net), new Vector3(PitchDims.GoalDepth, gh, net), NetGrey, true);
            AddBox("Net", new Vector3(midX, gh * 0.5f, -gw - net), new Vector3(PitchDims.GoalDepth, gh, net), NetGrey, true);
            AddBox("Net", new Vector3(midX, gh + net, 0f), new Vector3(PitchDims.GoalDepth, net, gw * 2f), NetGrey, true);
        }

        static void Finish(GameObject go, Color color, bool collide)
        {
            if (!collide)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
            go.GetComponent<Renderer>().material.color = color;
        }
    }
}
