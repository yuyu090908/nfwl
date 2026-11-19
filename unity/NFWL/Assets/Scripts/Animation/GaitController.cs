using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// PHASE 1.2: where the feet are. Each foot is either planted (locked to the ground, so it cannot slide)
    /// or swinging to a landing spot predicted from the body's velocity. Running uses a gait clock whose
    /// cadence follows the speed (stride length = speed / cadence, so steps match the ground speed)
    /// and the stance time is set so a planted foot always stays within the leg's reach; standing still uses corrective steps when the body turns or shifts.
    /// The ground under each landing spot is probed, so feet follow slopes and raised surfaces.
    /// Plain C# (Physics only for the ground probe) so the same logic can drive a future humanoid rig.
    /// </summary>
    public class GaitController
    {
        public class Foot
        {
            public readonly float side; // -1 left, +1 right
            public Vector3 plant;       // sole point while planted (world)
            public float plantYaw;
            public Vector3 normal = Vector3.up;
            public bool swinging;
            public float swingT, swingDur;
            public Vector3 from, to;
            public float fromYaw, toYaw;
            /// <summary>Sole point this frame (world).</summary>
            public Vector3 position;
            public float yaw;
            /// <summary>Foot pitch in degrees (+ = toe down).</summary>
            public float pitch;
            /// <summary>0..1 progress through the current stance (planted) or swing.</summary>
            public float progress;
            /// <summary>Set by kicks / touches: the foot is animated by someone else this frame.</summary>
            public bool overridden;
            public int lastLiftCycle = int.MinValue;
            public Foot(float side) { this.side = side; }
        }

        public readonly Foot Left = new Foot(-1f);
        public readonly Foot Right = new Foot(1f);
        /// <summary>Gait clock 0..1 (left foot lifts at StanceFraction, right half a cycle later).</summary>
        public float Phase { get; private set; }
        public float CycleHz { get; private set; }
        public float StanceFraction { get; private set; } = 0.6f;
        public bool Moving { get; private set; }
        /// <summary>0 standing .. 1 sprinting, for arm swing and bob.</summary>
        public float RunAmount { get; private set; }

        float cycles; // gait clock without wrapping, so each foot lifts once per cycle
        float scale = 1f;
        float hipHalfWidth = 0.1f;
        float maxReach = 0.45f;
        Vector3 lastRoot;
        bool initialised;

        static readonly RaycastHit[] hits = new RaycastHit[8];

        public Foot Get(float side) => side < 0f ? Left : Right;
        public Foot Other(Foot f) => f == Left ? Right : Left;

        public void Reset(Vector3 root, float yaw, float bodyScale)
        {
            scale = bodyScale;
            hipHalfWidth = 0.1f * scale;
            maxReach = 0.48f * scale;
            foreach (Foot f in new[] { Left, Right })
            {
                Vector3 p = root + Right3(yaw) * (f.side * hipHalfWidth);
                p.y = GroundHeight(p, out f.normal);
                f.plant = f.position = p;
                f.plantYaw = f.yaw = yaw;
                f.swinging = false;
                f.pitch = 0f;
                f.progress = 0f;
            }
            Phase = 0f;
            cycles = 0f;
            Moving = false;
            lastRoot = root;
            initialised = true;
        }

        /// <param name="root">Player position on the ground.</param>
        /// <param name="vel">Ground velocity.</param>
        /// <param name="yaw">Body facing in degrees.</param>
        /// <param name="stanceWidth">1 = normal, more for a wide defensive stance.</param>
        public void Tick(Vector3 root, Vector3 vel, float yaw, float dt, float bodyScale, float stanceWidth, bool pivoting)
        {
            if (!initialised || (root - lastRoot).sqrMagnitude > 2.25f || Mathf.Abs(bodyScale - scale) > 0.001f)
            {
                Reset(root, yaw, bodyScale);
            }
            lastRoot = root;
            vel.y = 0f;
            float speed = vel.magnitude;

            // Cadence (full cycles per second) and stance share by speed: walk ~1 Hz / 62 %, sprint ~1.9 Hz / 26 %.
            CycleHz = 0.75f + 0.17f * speed;
            // Stance share chosen so the body travels about 0.8 m over a planted foot (what a leg of this
            // length can cover with a little knee flex), less in a wide defensive stance.
            float contactLength = 0.8f * scale * (stanceWidth > 1.2f ? 0.7f : 1f);
            StanceFraction = Mathf.Clamp(contactLength * CycleHz / Mathf.Max(0.1f, speed), 0.22f, 0.62f);
            RunAmount = Mathf.Lerp(RunAmount, Mathf.Clamp01(speed / 6.5f), 1f - Mathf.Exp(-10f * dt));

            bool wantMove = speed > 0.35f;
            if (wantMove && !Moving)
            {
                // Start walking: the foot that is further from where it should be lifts first.
                float start = StanceFraction - 0.01f + (FootError(Right, root, yaw, stanceWidth) > FootError(Left, root, yaw, stanceWidth) ? 0.5f : 0f);
                cycles = Mathf.Floor(cycles) + 1f + Mathf.Repeat(start, 1f);
            }
            Moving = wantMove;
            if (Moving) cycles += CycleHz * dt;
            if (cycles > 100000f) cycles -= 99999f;
            Phase = Mathf.Repeat(cycles, 1f);

            float stanceTime = StanceFraction / Mathf.Max(0.1f, CycleHz);
            TickFoot(Left, 0f, root, vel, yaw, dt, stanceTime, stanceWidth, pivoting);
            TickFoot(Right, 0.5f, root, vel, yaw, dt, stanceTime, stanceWidth, pivoting);
        }

        void TickFoot(Foot f, float offset, Vector3 root, Vector3 vel, float yaw, float dt, float stanceTime, float stanceWidth, bool pivoting)
        {
            Foot other = Other(f);
            if (f.overridden)
            {
                // A kick or touch is moving this foot; re-plant where it ends up.
                f.swinging = false;
                f.plant = f.position;
                f.plant.y = GroundHeight(f.position, out f.normal);
                f.plantYaw = f.yaw;
                f.pitch = 0f;
                return;
            }

            if (f.swinging)
            {
                f.swingT += dt;
                float t = Mathf.Clamp01(f.swingT / Mathf.Max(0.01f, f.swingDur));
                // Keep re-aiming the landing spot while the body turns or changes speed.
                float timeToLand = Mathf.Max(0f, f.swingDur - f.swingT);
                f.to = LandingSpot(f, other, root, vel, yaw, timeToLand, Moving ? stanceTime : 0f, stanceWidth, out f.normal);
                f.toYaw = yaw;
                float e = t * t * (3f - 2f * t);
                Vector3 p = Vector3.Lerp(f.from, f.to, e);
                float liftHeight = Mathf.Lerp(0.07f, 0.24f, RunAmount) * scale;
                p.y += Mathf.Sin(t * Mathf.PI) * liftHeight;
                f.position = p;
                f.yaw = Mathf.LerpAngle(f.fromYaw, f.toYaw, e);
                // Toe down after lift-off, toe up just before the heel strikes.
                f.pitch = Mathf.Lerp(30f * RunAmount + 10f, -12f, t);
                f.progress = t;
                if (t >= 1f) Plant(f);
                return;
            }

            // Planted: the sole stays locked where it landed (no sliding).
            f.position = f.plant;
            f.yaw = f.plantYaw;
            if (pivoting)
            {
                // Pivot on the ball of the planted foot toward the new direction.
                f.plantYaw = Mathf.MoveTowardsAngle(f.plantYaw, yaw, 260f * dt);
                f.yaw = f.plantYaw;
            }

            Vector3 hip = root + Right3(yaw) * (f.side * hipHalfWidth);
            Vector3 fromHip = f.plant - hip;
            fromHip.y = 0f;
            bool overReach = fromHip.magnitude > maxReach;

            if (Moving)
            {
                float footCycles = cycles + offset;
                float local = Mathf.Repeat(footCycles, 1f);
                int cycleIndex = Mathf.FloorToInt(footCycles - StanceFraction);
                f.progress = Mathf.Clamp01(local / Mathf.Max(0.01f, StanceFraction));
                // Heel rises at the end of the stance (toe-off).
                f.pitch = Mathf.Lerp(0f, 25f, Mathf.InverseLerp(0.65f, 1f, f.progress)) * Mathf.Clamp01(vel.magnitude / 2f);
                bool due = local >= StanceFraction && cycleIndex != f.lastLiftCycle;
                if (due || (overReach && !other.swinging) || (overReach && f.progress > 0.5f))
                {
                    f.lastLiftCycle = cycleIndex;
                    float remaining = (1f - Mathf.Max(local, StanceFraction)) / Mathf.Max(0.1f, CycleHz);
                    BeginSwing(f, Mathf.Max(remaining, 0.6f * (1f - StanceFraction) / Mathf.Max(0.1f, CycleHz)));
                }
            }
            else
            {
                f.progress = 0f;
                f.pitch = Mathf.MoveTowards(f.pitch, 0f, 120f * dt);
                // Standing: step when the body has turned or shifted away from this foot.
                if (!other.swinging && (FootError(f, root, yaw, stanceWidth) > 0.15f * scale || Mathf.Abs(Mathf.DeltaAngle(f.plantYaw, yaw)) > 35f || overReach))
                {
                    BeginSwing(f, 0.22f);
                }
            }
        }

        void BeginSwing(Foot f, float duration)
        {
            f.swinging = true;
            f.swingT = 0f;
            f.swingDur = Mathf.Max(0.08f, duration);
            f.from = f.position;
            f.fromYaw = f.yaw;
        }

        void Plant(Foot f)
        {
            f.swinging = false;
            f.plant = f.to;
            f.plantYaw = f.toYaw;
            f.position = f.plant;
            f.yaw = f.plantYaw;
            f.pitch = 0f;
        }

        /// <summary>Where a foot should land: under the hip when the body will be over it.</summary>
        Vector3 LandingSpot(Foot f, Foot other, Vector3 root, Vector3 vel, float yaw, float timeToLand, float stanceTime, float stanceWidth, out Vector3 normal)
        {
            Vector3 right = Right3(yaw);
            Vector3 center = root + vel * (timeToLand + stanceTime * 0.5f);
            Vector3 p = center + right * (f.side * hipHalfWidth * stanceWidth);

            // Never cross the feet: keep this foot on its own side of the other one.
            float minGap = 0.1f * scale;
            float rel = Vector3.Dot(p - other.position, right) * f.side;
            if (rel < minGap) p += right * (f.side * (minGap - rel));

            p.y = GroundHeight(p, out normal);
            return p;
        }

        float FootError(Foot f, Vector3 root, float yaw, float stanceWidth)
        {
            Vector3 rest = root + Right3(yaw) * (f.side * hipHalfWidth * stanceWidth);
            Vector3 d = f.plant - rest;
            d.y = 0f;
            return d.magnitude;
        }

        static Vector3 Right3(float yaw)
        {
            float r = yaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(r), 0f, -Mathf.Sin(r));
        }

        /// <summary>Ground height under p (static colliders only; players and the ball are ignored).</summary>
        public static float GroundHeight(Vector3 p, out Vector3 normal)
        {
            normal = Vector3.up;
            int n = Physics.RaycastNonAlloc(new Vector3(p.x, p.y + 0.6f, p.z), Vector3.down, hits, 1.6f, ~0, QueryTriggerInteraction.Ignore);
            float best = 0f;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.attachedRigidbody != null || c is CharacterController) continue;
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    best = hits[i].point.y;
                    normal = hits[i].normal;
                }
            }
            if (bestDist == float.MaxValue) return 0f;
            return Mathf.Clamp(best, -0.3f, 0.3f);
        }
    }
}
