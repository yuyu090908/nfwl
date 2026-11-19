using System;
using System.Collections.Generic;
using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// PHASE 13: action replay. Records the last few seconds of the match (player roots, every
    /// animated bone of the procedural models, and the ball) 30 times a second into a ring buffer.
    /// Playing a replay pauses the game (timeScale 0), shows the recorded poses with interpolation
    /// from the chosen camera angle, and puts everything back exactly as it was when it ends, so
    /// the live match continues untouched. Runs late in the frame so the recorded pose is the final one.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class ReplaySystem : MonoBehaviour
    {
        const int Rate = 30;
        const float Seconds = 7f;
        const int Capacity = (int)(Rate * Seconds) + 2;

        Transform[] tracked;
        // Per frame: for each tracked transform localPosition (3) + localRotation (4).
        float[][] frames;
        float[] times;
        int head, count;
        float nextSample;

        float[] liveSnapshot;
        Ball ball;
        MatchCamera cam;
        Vector3 ballVel, ballSpin;
        bool ballWasKinematic;
        RigidbodyInterpolation ballInterp;

        public bool Playing { get; private set; }
        public string Title { get; private set; } = "";
        float playFrom, playTo, playClock, playSpeed;
        Func<float, Vector3> focusFn;
        float camYaw, camPitch, camDist;
        Action onEnd;
        float savedTimeScale = 1f;

        public void Init(Ball b, IList<PlayerController> players, MatchCamera camera)
        {
            ball = b;
            cam = camera;
            List<Transform> list = new List<Transform>();
            list.Add(b.transform);
            for (int i = 0; i < players.Count; i++)
            {
                list.Add(players[i].transform);
                if (players[i].Model != null) list.AddRange(players[i].Model.AnimatedParts);
            }
            tracked = list.ToArray();
            frames = new float[Capacity][];
            for (int i = 0; i < Capacity; i++) frames[i] = new float[tracked.Length * 7];
            times = new float[Capacity];
            liveSnapshot = new float[tracked.Length * 7];
        }

        /// <summary>Time of the oldest recorded frame.</summary>
        public float OldestTime => count == 0 ? Time.time : times[(head - count + Capacity) % Capacity];

        void LateUpdate()
        {
            if (tracked == null) return;
            if (Playing) { Step(); return; }
            if (Time.timeScale <= 0f || Time.time < nextSample) return;
            nextSample = Time.time + 1f / Rate;
            Capture(frames[head]);
            times[head] = Time.time;
            head = (head + 1) % Capacity;
            count = Mathf.Min(count + 1, Capacity);
        }

        void Capture(float[] dst)
        {
            int k = 0;
            for (int i = 0; i < tracked.Length; i++)
            {
                Transform t = tracked[i];
                Vector3 p = t.localPosition;
                Quaternion q = t.localRotation;
                dst[k++] = p.x; dst[k++] = p.y; dst[k++] = p.z;
                dst[k++] = q.x; dst[k++] = q.y; dst[k++] = q.z; dst[k++] = q.w;
            }
        }

        void ApplyBlend(float[] a, float[] b, float f)
        {
            int k = 0;
            for (int i = 0; i < tracked.Length; i++)
            {
                Vector3 p = new Vector3(Mathf.Lerp(a[k], b[k], f), Mathf.Lerp(a[k + 1], b[k + 1], f), Mathf.Lerp(a[k + 2], b[k + 2], f));
                Quaternion qa = new Quaternion(a[k + 3], a[k + 4], a[k + 5], a[k + 6]);
                Quaternion qb = new Quaternion(b[k + 3], b[k + 4], b[k + 5], b[k + 6]);
                tracked[i].localPosition = p;
                tracked[i].localRotation = Quaternion.Slerp(qa, qb, f);
                k += 7;
            }
        }

        /// <summary>
        /// Plays the recording from <paramref name="from"/> to <paramref name="to"/> (game time) at
        /// <paramref name="speed"/>. The camera looks at focus(t) from the given yaw / pitch / distance.
        /// </summary>
        public bool Play(string title, float from, float to, float speed, float yaw, float pitch, float dist,
            Func<float, Vector3> focus, Action ended)
        {
            if (Playing || count < Rate) return false;
            from = Mathf.Max(from, OldestTime);
            if (to - from < 0.5f) return false;
            Title = title;
            playFrom = from;
            playTo = to;
            playClock = from;
            playSpeed = speed;
            camYaw = yaw;
            camPitch = pitch;
            camDist = dist;
            focusFn = focus;
            onEnd = ended;

            // Freeze the live game and remember its exact state.
            Capture(liveSnapshot);
            ballVel = ball.Velocity;
            ballSpin = ball.Body.angularVelocity;
            ballWasKinematic = ball.Body.isKinematic;
            ballInterp = ball.Body.interpolation;
            ball.Body.interpolation = RigidbodyInterpolation.None; // the transform is ours during the replay
            ball.Body.isKinematic = true;
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            Playing = true;
            if (cam != null) cam.BeginReplay();
            Step();
            return true;
        }

        /// <summary>Ends the replay now (a key press skips it).</summary>
        public void Stop()
        {
            if (!Playing) return;
            Playing = false;
            ApplyBlend(liveSnapshot, liveSnapshot, 0f);
            ball.Body.position = ball.transform.position;
            ball.Body.isKinematic = ballWasKinematic;
            ball.Body.interpolation = ballInterp;
            Physics.SyncTransforms();
            if (!ballWasKinematic)
            {
                ball.Velocity = ballVel;
                ball.Body.angularVelocity = ballSpin;
            }
            Time.timeScale = savedTimeScale;
            if (cam != null) cam.EndReplay();
            Action e = onEnd;
            onEnd = null;
            e?.Invoke();
        }

        void Step()
        {
            playClock += Mathf.Min(Time.unscaledDeltaTime, 0.1f) * playSpeed;
            if (playClock >= playTo) { Stop(); return; }
            // Find the two frames around playClock.
            int oldest = (head - count + Capacity) % Capacity;
            int a = oldest;
            for (int i = 0; i < count - 1; i++)
            {
                int idx = (oldest + i) % Capacity;
                int nxt = (idx + 1) % Capacity;
                if (times[nxt] >= playClock) { a = idx; break; }
                a = nxt;
            }
            int b = (a + 1) % Capacity;
            if (b == head) b = a;
            float span = times[b] - times[a];
            float f = span > 1e-4f ? Mathf.Clamp01((playClock - times[a]) / span) : 0f;
            ApplyBlend(frames[a], frames[b], f);
            if (cam != null && focusFn != null) cam.SetReplayView(focusFn(playClock), camYaw, camPitch, camDist);
        }

        /// <summary>Recorded ball position at a game time (for replay camera framing).</summary>
        public Vector3 BallAt(float t)
        {
            if (count == 0) return ball.transform.position;
            int oldest = (head - count + Capacity) % Capacity;
            for (int i = 0; i < count; i++)
            {
                int idx = (oldest + i) % Capacity;
                if (times[idx] >= t)
                {
                    float[] fr = frames[idx];
                    return new Vector3(fr[0], fr[1], fr[2]); // tracked[0] is the ball (root object, world = local)
                }
            }
            float[] last = frames[(head - 1 + Capacity) % Capacity];
            return new Vector3(last[0], last[1], last[2]);
        }
    }
}
