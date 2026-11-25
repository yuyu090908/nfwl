using UnityEngine;

namespace NFWL
{
    /// <summary>A point of the predicted path (motion-matching style trajectory).</summary>
    public struct TrajectoryPoint
    {
        public float time;      // seconds ahead
        public Vector3 position;
        public Vector3 velocity;
        public float facingYaw;
    }

    /// <summary>v0.7 trajectory prediction for motion matching (pure math).</summary>
    public static class MotionTrajectory
    {
        /// <summary>Predicts the path with the current velocity relaxing toward the wanted velocity
        /// (critically damped, the same shape the locomotion produces) - no allocation.</summary>
        public static TrajectoryPoint Predict(Vector3 pos, Vector3 vel, Vector3 wantedVel, float facingYaw, float t, float responseTime = 0.45f)
        {
            float k = 1f - Mathf.Exp(-t / Mathf.Max(0.05f, responseTime));
            Vector3 v = Vector3.Lerp(vel, wantedVel, k);
            // Integral of the exponential blend: position moves with the old velocity at first, then the new one.
            float tau = Mathf.Max(0.05f, responseTime);
            Vector3 p = pos + wantedVel * t + (vel - wantedVel) * tau * (1f - Mathf.Exp(-t / tau));
            float yaw = v.sqrMagnitude > 0.25f ? Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg : facingYaw;
            return new TrajectoryPoint { time = t, position = p, velocity = v, facingYaw = yaw };
        }
    }
}
