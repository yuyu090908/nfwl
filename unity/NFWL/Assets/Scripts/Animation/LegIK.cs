using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// PHASE 1.2 / 8.3: analytic two-bone IK for the procedural Crystalia legs (hip - knee - ankle).
    /// Bones are built along their local -Y axis with local +Z pointing forward, so a bone that points
    /// along direction d with its front toward f has the world rotation LookRotation(f, -d).
    /// Works the same for a future humanoid rig if its bones are given the same axis convention.
    /// </summary>
    public static class LegIK
    {
        /// <summary>
        /// Knee position for a leg from hip to target with the knee bending toward pole.
        /// Returns how far the target is out of reach (0 when reachable).
        /// </summary>
        public static float SolveKnee(Vector3 hip, Vector3 target, float upper, float lower, Vector3 pole,
            out Vector3 knee, out Vector3 reachedTarget)
        {
            Vector3 toTarget = target - hip;
            float dist = toTarget.magnitude;
            float maxLen = (upper + lower) * 0.999f;
            float minLen = Mathf.Abs(upper - lower) + 0.01f;
            float overshoot = Mathf.Max(0f, dist - maxLen);
            Vector3 axis = dist > 1e-4f ? toTarget / dist : Vector3.down;
            float d = Mathf.Clamp(dist, minLen, maxLen);
            reachedTarget = hip + axis * d;

            // Law of cosines: distance along the hip-target axis and height of the knee off it.
            float a = (upper * upper - lower * lower + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, upper * upper - a * a));
            Vector3 bend = Vector3.ProjectOnPlane(pole, axis);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.forward, axis);
            bend.Normalize();
            knee = hip + axis * a + bend * h;
            return overshoot;
        }

        /// <summary>World rotation for a bone running from 'from' to 'to', front facing toward 'front'.</summary>
        public static Quaternion BoneRotation(Vector3 from, Vector3 to, Vector3 front)
        {
            Vector3 dir = to - from;
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.down;
            dir.Normalize();
            Vector3 f = Vector3.ProjectOnPlane(front, dir);
            if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(Vector3.forward, dir);
            return Quaternion.LookRotation(f.normalized, -dir);
        }
    }
}
