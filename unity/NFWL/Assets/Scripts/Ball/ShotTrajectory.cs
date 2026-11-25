using UnityEngine;

namespace NFWL
{
    /// <summary>v0.7 (PHASE 5.2): the five flight types, decided from launch velocity and spin.</summary>
    public enum ShotTrajectory { Ground, Driven, Rising, Curved, Lofted }

    public static class ShotTrajectories
    {
        /// <summary>
        /// Ground: rolls (launch under ~4 deg). Driven: fast and flat (under 12 deg, 18 m/s+).
        /// Curved: strong sidespin bends it (sideways Magnus over ~2.5 m/s^2). Lofted: steep (over 24 deg).
        /// Rising: in between, climbing (backspin or a moderate angle).
        /// </summary>
        public static ShotTrajectory Classify(Vector3 velocity, Vector3 spin, float magnusFactor = 0.0063f)
        {
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = velocity.magnitude;
            if (speed < 0.01f) return ShotTrajectory.Ground;
            float angle = Mathf.Atan2(velocity.y, Mathf.Max(0.01f, flat.magnitude)) * Mathf.Rad2Deg;
            Vector3 magnus = Vector3.Cross(spin, velocity) * magnusFactor;
            float side = Vector3.Dot(magnus, Vector3.Cross(Vector3.up, flat.normalized));
            if (Mathf.Abs(side) > 2.5f && angle < 24f) return ShotTrajectory.Curved;
            if (angle < 4f) return ShotTrajectory.Ground;
            if (angle > 24f) return ShotTrajectory.Lofted;
            if (angle < 12f && speed >= 18f) return ShotTrajectory.Driven;
            return ShotTrajectory.Rising;
        }

        public static string Label(ShotTrajectory t)
        {
            switch (t)
            {
                case ShotTrajectory.Ground: return "GROUND";
                case ShotTrajectory.Driven: return "DRIVEN";
                case ShotTrajectory.Rising: return "RISING";
                case ShotTrajectory.Curved: return "CURVED";
                default: return "LOFTED";
            }
        }
    }
}
