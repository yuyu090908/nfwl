using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;

namespace NFWL
{
    /// <summary>
    /// PHASE 12: lightweight in-game measurements (F2 shows them), so real numbers can be read on
    /// the target machine: FPS (average and worst frame over the last second), CPU time spent in the
    /// AI, the procedural animation / IK and the ball-body contacts, GC collections and heap size.
    /// Timers are Stopwatch ticks summed per frame; the cost of measuring is a few microseconds.
    /// </summary>
    public static class PerfStats
    {
        public enum Slot { AI, Animation, Contacts, Count }

        static readonly Stopwatch watch = Stopwatch.StartNew();
        static readonly long[] frameTicks = new long[(int)Slot.Count];
        static readonly double[] accumMs = new double[(int)Slot.Count];
        static readonly double[] shownMs = new double[(int)Slot.Count];

        static int frames;
        static float windowStart;
        static float worstFrame;
        static int gcAtWindow;

        public static float Fps { get; private set; }
        public static float WorstFrameMs { get; private set; }
        public static int GcPerSecond { get; private set; }
        public static long MonoHeapMB { get; private set; }

        public static long Begin() => watch.ElapsedTicks;

        public static void End(Slot slot, long start) => frameTicks[(int)slot] += watch.ElapsedTicks - start;

        /// <summary>Average ms per frame over the last second for a slot.</summary>
        public static double Ms(Slot slot) => shownMs[(int)slot];

        /// <summary>Call once per frame (MatchManager.Update).</summary>
        public static void EndFrame()
        {
            double tickMs = 1000.0 / Stopwatch.Frequency;
            for (int i = 0; i < frameTicks.Length; i++)
            {
                accumMs[i] += frameTicks[i] * tickMs;
                frameTicks[i] = 0;
            }
            frames++;
            worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
            float now = Time.unscaledTime;
            if (now - windowStart >= 1f)
            {
                float span = now - windowStart;
                Fps = frames / span;
                WorstFrameMs = worstFrame * 1000f;
                for (int i = 0; i < accumMs.Length; i++) { shownMs[i] = accumMs[i] / Mathf.Max(1, frames); accumMs[i] = 0; }
                int gc = System.GC.CollectionCount(0);
                GcPerSecond = gc - gcAtWindow;
                gcAtWindow = gc;
                MonoHeapMB = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
                frames = 0;
                worstFrame = 0f;
                windowStart = now;
            }
        }
    }
}
