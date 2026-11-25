using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// v0.7 (PHASE 1.5): each player's own running style, so ten players never move in lock-step.
    /// Stride and cadence, forward lean, arm swing, lean into turns, the starting phase of the gait
    /// and the rhythm of ball touches vary a little per player. The ranges are deliberately narrow
    /// (a few percent / degrees) and do not depend on rarity, so no player ever gets an unnatural form.
    /// Gameplay values (speed, acceleration, turning) are NOT changed: this is presentation only.
    /// Stride length still equals speed / cadence, so the feet stay locked to the ground.
    /// </summary>
    public struct MotionProfile
    {
        public float cadenceScale;     // 0.94 .. 1.06 (also shorter / longer stride at the same speed)
        public float leanOffset;       // degrees added to the forward lean, -2 .. +3
        public float armSwingScale;    // 0.85 .. 1.15
        public float armOutOffset;     // degrees, -3 .. +3
        public float turnLeanScale;    // 0.85 .. 1.15 (how much the body banks into curves)
        public float phaseOffset;      // 0 .. 1 of a gait cycle at kick-off
        public float touchRhythmScale; // 0.95 .. 1.05 (time between dribble touches)

        public static MotionProfile Neutral => new MotionProfile
        {
            cadenceScale = 1f, leanOffset = 0f, armSwingScale = 1f, armOutOffset = 0f, turnLeanScale = 1f, phaseOffset = 0f, touchRhythmScale = 1f
        };

        /// <summary>Deterministic profile for a player (same player, same style every match).</summary>
        public static MotionProfile For(int team, int number, float heightScale)
        {
            var r = new System.Random(9000 + team * 131 + number * 17);
            float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
            MotionProfile p;
            // Taller players take slightly longer, slower strides.
            p.cadenceScale = Mathf.Clamp(R(0.95f, 1.05f) / Mathf.Sqrt(Mathf.Max(0.8f, heightScale)), 0.94f, 1.06f);
            p.leanOffset = R(-2f, 3f);
            p.armSwingScale = R(0.85f, 1.15f);
            p.armOutOffset = R(-3f, 3f);
            p.turnLeanScale = R(0.85f, 1.15f);
            p.phaseOffset = R(0f, 1f);
            p.touchRhythmScale = R(0.95f, 1.05f);
            return p;
        }

        /// <summary>True when every value is inside the natural range (tests).</summary>
        public bool WithinNaturalRange =>
            cadenceScale >= 0.94f && cadenceScale <= 1.06f && leanOffset >= -2f && leanOffset <= 3f &&
            armSwingScale >= 0.85f && armSwingScale <= 1.15f && Mathf.Abs(armOutOffset) <= 3f &&
            turnLeanScale >= 0.85f && turnLeanScale <= 1.15f && phaseOffset >= 0f && phaseOffset <= 1f &&
            touchRhythmScale >= 0.95f && touchRhythmScale <= 1.05f;
    }
}
