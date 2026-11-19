using System.Collections.Generic;
using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// Keyboard reader that works whichever input backend the project uses.
    /// It combines the old Input Manager (when it is enabled) with IMGUI key events,
    /// which Unity always delivers to OnGUI, so the game stays playable even when
    /// the Input Manager is deprecated or switched off (Unity 6.x).
    /// MatchManager feeds events in OnGUI and calls EndFrame in LateUpdate.
    /// </summary>
    public static class KeyInput
    {
        static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
        static readonly HashSet<KeyCode> pressed = new HashSet<KeyCode>();
        static readonly HashSet<KeyCode> released = new HashSet<KeyCode>();
        static bool shiftHeld, altHeld, ctrlHeld;

        /// <summary>Time.unscaledTime when Shift was last pressed (power shot vs. sprint, v0.6).</summary>
        public static float ShiftDownTime { get; private set; } = -10f;

        /// <summary>Last key seen (for the HUD), so it is easy to tell whether input arrives at all.</summary>
        public static string LastKey { get; private set; } = "";

        public static void HandleEvent(Event e)
        {
            if (e == null) return;
            if (e.isKey || e.type == EventType.KeyDown || e.type == EventType.KeyUp)
            {
                if (e.shift && !shiftHeld) ShiftDownTime = Time.unscaledTime;
                shiftHeld = e.shift;
                altHeld = e.alt;
                ctrlHeld = e.control;
            }
            if (e.keyCode == KeyCode.None) return;

            if (e.type == EventType.KeyDown)
            {
                if (held.Add(e.keyCode)) pressed.Add(e.keyCode); // ignore key repeat
                LastKey = e.keyCode.ToString();
            }
            else if (e.type == EventType.KeyUp)
            {
                if (held.Remove(e.keyCode)) released.Add(e.keyCode);
            }
        }

        /// <summary>Call once per frame after all Update calls.</summary>
        public static void EndFrame()
        {
            pressed.Clear();
            released.Clear();
        }

        /// <summary>Drop every key, e.g. when the window loses focus.</summary>
        public static void ReleaseAll()
        {
            foreach (KeyCode k in held) released.Add(k);
            held.Clear();
            shiftHeld = altHeld = ctrlHeld = false;
        }

        public static bool Get(KeyCode k)
        {
            if (held.Contains(k)) return true;
            if ((k == KeyCode.LeftShift || k == KeyCode.RightShift) && shiftHeld) return true;
            if ((k == KeyCode.LeftAlt || k == KeyCode.RightAlt) && altHeld) return true;
            if ((k == KeyCode.LeftControl || k == KeyCode.RightControl) && ctrlHeld) return true;
            bool legacy = Legacy(k, 0);
            if (legacy && (k == KeyCode.LeftShift || k == KeyCode.RightShift) && Legacy(k, 1)) ShiftDownTime = Time.unscaledTime;
            return legacy;
        }

        public static bool Down(KeyCode k) => pressed.Contains(k) || Legacy(k, 1);

        public static bool Up(KeyCode k) => released.Contains(k) || Legacy(k, 2);

        static bool legacyBroken;

        static bool Legacy(KeyCode k, int mode)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (legacyBroken) return false;
            try
            {
                switch (mode)
                {
                    case 0: return Input.GetKey(k);
                    case 1: return Input.GetKeyDown(k);
                    default: return Input.GetKeyUp(k);
                }
            }
            catch (System.InvalidOperationException)
            {
                legacyBroken = true; // Input Manager is switched off in Player Settings
                return false;
            }
#else
            return false;
#endif
        }
    }
}
