using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// Solves kicks against the ball model (Ball.Simulate): ground pass speed for a wanted arrival pace,
    /// lofted pass speed for a landing spot, shot elevation for a target height, and aim correction
    /// for curl. Shared by players and AI, so every pass and shot obeys the same physics.
    /// </summary>
    public static class BallSolver
    {
        const float MaxTime = 4f;
        const float Step = 0.02f;

        /// <summary>Ground pass: start speed so the ball still has arrivalSpeed after rolling dist.</summary>
        public static float GroundPassSpeed(float dist, float arrivalSpeed)
        {
            float lo = Mathf.Max(0.5f, arrivalSpeed), hi = 40f;
            for (int i = 0; i < 18; i++)
            {
                float mid = (lo + hi) * 0.5f;
                float v = SpeedAfterRoll(mid, dist, out _);
                if (v < arrivalSpeed) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>Speed left after rolling dist (0 if it stops short), and the time it took.</summary>
        public static float SpeedAfterRoll(float speed, float dist, out float time)
        {
            Vector3 p = new Vector3(0f, PitchDims.BallRadius, 0f);
            Vector3 v = new Vector3(speed, 0f, 0f);
            time = 0f;
            while (p.x < dist && time < MaxTime)
            {
                p = Ball.Simulate(p, v, Step, out v);
                time += Step;
                if (v.x <= 0.01f) { time = MaxTime; return 0f; }
            }
            return v.x;
        }

        /// <summary>Seconds for a ground pass of this speed to cover dist (MaxTime if it never gets there).</summary>
        public static float GroundTravelTime(float speed, float dist)
        {
            SpeedAfterRoll(speed, dist, out float t);
            return t;
        }

        /// <summary>Flies the ball until it has covered horizDist or come down; height there and time.</summary>
        public static float HeightAtDistance(Vector3 from, Vector3 v, Vector3 spin, float horizDist, out float time, out bool reached)
        {
            Vector3 dir = new Vector3(v.x, 0f, v.z).normalized;
            Vector3 p = from;
            time = 0f;
            reached = false;
            bool wasAir = false;
            while (time < MaxTime)
            {
                p = Ball.Simulate(p, v, spin, Step, out v);
                spin *= 0.998f;
                time += Step;
                float along = Vector3.Dot(p - from, dir);
                if (along >= horizDist) { reached = true; return p.y; }
                if (p.y > PitchDims.BallRadius + 0.05f) wasAir = true;
                if (wasAir && p.y <= PitchDims.BallRadius + 0.001f) return p.y; // landed short
            }
            return p.y;
        }

        /// <summary>Horizontal distance where a lofted ball first lands, and the flight time.</summary>
        public static float LandingDistance(Vector3 from, Vector3 v, Vector3 spin, out float time)
        {
            Vector3 dir = new Vector3(v.x, 0f, v.z).normalized;
            Vector3 p = from;
            time = 0f;
            bool wasAir = false;
            while (time < MaxTime)
            {
                p = Ball.Simulate(p, v, spin, Step, out v);
                time += Step;
                if (p.y > PitchDims.BallRadius + 0.05f) wasAir = true;
                if (wasAir && p.y <= PitchDims.BallRadius + 0.001f) break;
            }
            return Vector3.Dot(p - from, dir);
        }

        /// <summary>Launch speed for a lofted ball at elevation to land landDist away.</summary>
        public static float LoftedSpeed(Vector3 from, Vector3 dirFlat, float elevationDeg, Vector3 spin, float landDist, out float flightTime)
        {
            float lo = 3f, hi = 38f;
            flightTime = 0f;
            for (int i = 0; i < 16; i++)
            {
                float mid = (lo + hi) * 0.5f;
                float d = LandingDistance(from, Launch(dirFlat, mid, elevationDeg), spin, out flightTime);
                if (d < landDist) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>Elevation (deg) so a ball at this speed passes targetHeight after horizDist.</summary>
        public static float ElevationForHeight(Vector3 from, Vector3 dirFlat, float speed, Vector3 spin, float horizDist, float targetHeight)
        {
            float lo = -6f, hi = 40f;
            for (int i = 0; i < 16; i++)
            {
                float mid = (lo + hi) * 0.5f;
                float h = HeightAtDistance(from, Launch(dirFlat, speed, mid), spin, horizDist, out _, out bool reached);
                if (!reached || h < targetHeight) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>
        /// Aim correction for curl: rotates the launch direction so a spinning ball ends at target.
        /// Returns the corrected flat direction.
        /// </summary>
        public static Vector3 CompensateCurl(Vector3 from, Vector3 target, float speed, float elevationDeg, Vector3 spin)
        {
            Vector3 flat = target - from;
            flat.y = 0f;
            float dist = flat.magnitude;
            if (dist < 1f || spin.sqrMagnitude < 1f) return flat.normalized;
            Vector3 dir = flat / dist;
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = from;
                Vector3 v = Launch(dir, speed, elevationDeg);
                Vector3 w = spin;
                float t = 0f;
                Vector3 baseDir = flat / dist;
                while (Vector3.Dot(p - from, baseDir) < dist && t < MaxTime)
                {
                    p = Ball.Simulate(p, v, w, Step, out v);
                    t += Step;
                }
                Vector3 miss = p - target;
                miss.y = 0f;
                Vector3 right = new Vector3(baseDir.z, 0f, -baseDir.x);
                float lateral = Vector3.Dot(miss, right);
                float angle = Mathf.Atan2(lateral, dist) * Mathf.Rad2Deg;
                dir = Quaternion.AngleAxis(-angle, Vector3.up) * dir;
                if (Mathf.Abs(lateral) < 0.05f) break;
            }
            return dir;
        }

        public static Vector3 Launch(Vector3 dirFlat, float speed, float elevationDeg)
        {
            float e = elevationDeg * Mathf.Deg2Rad;
            return dirFlat * (speed * Mathf.Cos(e)) + Vector3.up * (speed * Mathf.Sin(e));
        }
    }
}
