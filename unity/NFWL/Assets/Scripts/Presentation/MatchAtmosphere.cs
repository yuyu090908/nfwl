using UnityEngine;
using UnityEngine.Rendering;

namespace NFWL
{
    /// <summary>
    /// v0.7 stadium atmosphere (PHASE 9.5) and ball readability (PHASE 5.3):
    /// crowd excitement (rises near the goals, explodes on a goal, then a wave around the stadium),
    /// LED boards flashing on a goal, and the ball's contact shadow on the grass (grows and fades with
    /// height, so the ball's height and landing spot are readable at real size, also at night).
    /// </summary>
    public class MatchAtmosphere : MonoBehaviour
    {
        static readonly int ExciteId = Shader.PropertyToID("_NFWLCrowdExcite");
        static readonly int WaveId = Shader.PropertyToID("_NFWLCrowdWave");
        static readonly int GlowScaleId = Shader.PropertyToID("_NFWLGlowScale");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int StrengthId = Shader.PropertyToID("_Strength");

        Ball ball;
        float excite, goalAt = -100f;
        Transform ballShadow;
        MaterialPropertyBlock block;
        MeshRenderer ballShadowRenderer;

        /// <summary>0 calm .. 1 goal celebration (also read by tests).</summary>
        public float Excitement => excite;

        public void Init(Ball b)
        {
            ball = b;
            Shader.SetGlobalFloat(GlowScaleId, 0.85f); // crystals glow, but never over the ball and outlines
            Shader.SetGlobalFloat(ExciteId, 0f);
            Shader.SetGlobalFloat(WaveId, 0f);
            Material m = NFWLShaders.Simple(NFWLShaders.ContactShadow, "Ball Contact Shadow");
            if (m != null)
            {
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "BallContactShadow";
                Destroy(q.GetComponent<Collider>());
                q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                ballShadowRenderer = q.GetComponent<MeshRenderer>();
                ballShadowRenderer.sharedMaterial = m;
                ballShadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
                ballShadowRenderer.receiveShadows = false;
                ballShadow = q.transform;
                block = new MaterialPropertyBlock();
            }
        }

        public void OnGoal()
        {
            goalAt = Time.time;
            excite = 1f;
        }

        /// <summary>danger: 0..1 how close the attack is to a goal (MatchManager.Danger).</summary>
        public void Tick(float danger, bool frozen)
        {
            float sinceGoal = Time.time - goalAt;
            float target = sinceGoal < 4f ? 1f : Mathf.Clamp01(danger) * 0.35f;
            excite = Mathf.MoveTowards(excite, target, Time.deltaTime * (target > excite ? 2.5f : 0.35f));
            Shader.SetGlobalFloat(ExciteId, excite);
            // After the cheer, a wave runs around the stadium for a few seconds.
            Shader.SetGlobalFloat(WaveId, sinceGoal > 3.5f && sinceGoal < 11f ? 1f : 0f);

            // LED boards flash on a goal.
            float flash = sinceGoal < 3f ? 1.35f + Mathf.Abs(Mathf.Sin(sinceGoal * 9f)) * 1.6f : 1.35f;
            foreach (Material led in StadiumBuilder.LedMaterials) if (led != null) led.SetFloat(IntensityId, flash);

            if (ballShadow != null && ball != null)
            {
                Vector3 b = ball.transform.position;
                float h = Mathf.Max(0f, b.y - PitchDims.BallRadius);
                float size = PitchDims.BallRadius * 2.6f * (1f + h * 0.35f);
                ballShadow.position = new Vector3(b.x, 0.026f, b.z);
                ballShadow.localScale = new Vector3(size, size, 1f);
                block.SetFloat(StrengthId, Mathf.Clamp01(0.7f / (1f + h * 0.8f)));
                ballShadowRenderer.SetPropertyBlock(block);
            }
        }
    }
}
