using UnityEngine;

namespace NFWL
{
    /// <summary>Actions that can wait in the input buffer.</summary>
    public enum BufferedAction { ShortPass, LongPass, Shoot, Tackle, Trap, SkillMove }

    /// <summary>
    /// v0.6 Real Football Engine, Phase 1 (14.1): keeps a button press for a short time, so a press made
    /// a little too early (during another action, just out of reach, before the ball arrives) is not lost.
    /// Also remembers the last movement direction, so a first touch can use the direction the player
    /// pushed just before the ball arrived (used by Phase 2).
    /// Plain C#, no allocation after construction.
    /// </summary>
    public class InputBuffer
    {
        struct Entry
        {
            public bool set;
            public float time;
        }

        readonly Entry[] entries = new Entry[System.Enum.GetValues(typeof(BufferedAction)).Length];
        Vector3 lastMoveDir;
        float lastMoveTime = -10f;

        /// <summary>Stores a press at time 'now' (a newer press of the same action replaces the older one).</summary>
        public void Press(BufferedAction a, float now)
        {
            entries[(int)a].set = true;
            entries[(int)a].time = now;
        }

        /// <summary>True when the action was pressed within 'window' seconds before now (and not used yet).</summary>
        public bool Has(BufferedAction a, float now, float window)
        {
            Entry e = entries[(int)a];
            if (!e.set) return false;
            if (now - e.time > window)
            {
                entries[(int)a].set = false;
                return false;
            }
            return true;
        }

        /// <summary>Like Has, and removes the press when it is there.</summary>
        public bool Consume(BufferedAction a, float now, float window)
        {
            if (!Has(a, now, window)) return false;
            entries[(int)a].set = false;
            return true;
        }

        /// <summary>Seconds since the press, or -1 when nothing is stored.</summary>
        public float Age(BufferedAction a, float now) => entries[(int)a].set ? now - entries[(int)a].time : -1f;

        public void Clear(BufferedAction a) => entries[(int)a].set = false;

        public void ClearAll()
        {
            for (int i = 0; i < entries.Length; i++) entries[i].set = false;
            lastMoveTime = -10f;
        }

        /// <summary>Call every frame with the movement input (zero when no key is held).</summary>
        public void RecordMove(Vector3 dir, float now)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            lastMoveDir = dir.normalized;
            lastMoveTime = now;
        }

        /// <summary>The last direction pushed within 'window' seconds, or zero.</summary>
        public Vector3 RecentMoveDir(float now, float window) => now - lastMoveTime <= window ? lastMoveDir : Vector3.zero;
    }
}
