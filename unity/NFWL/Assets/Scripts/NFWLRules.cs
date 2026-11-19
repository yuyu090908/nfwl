using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// Pitch dimensions in metres (Full Spec 3.1).
    /// The pitch is centred on the world origin. Its length runs along X, its width along Z, Y is up.
    /// Team 0 (HOME) attacks +X, team 1 (AWAY) attacks -X.
    /// Same numbers as the Unreal prototype (NFWLTypes.h), converted from cm to m.
    /// </summary>
    public static class PitchDims
    {
        public const float HalfLength = 21f;     // 42m
        public const float HalfWidth = 12.5f;    // 25m
        public const float GoalHalfWidth = 1.5f; // 3m
        public const float GoalHeight = 2f;      // 2m
        public const float GoalDepth = 1f;
        public const float PenaltyRadius = 6f;   // 6m semicircle
        public const float PenaltySpotDist = 6f;
        public const float CenterRadius = 3f;
        public const float LineWidth = 0.08f;
        public const float BallRadius = 0.11f;   // size 4 ball
    }

    public enum BallState
    {
        InPlay,
        GoalForHome, // ball went into the goal at +X
        GoalForAway, // ball went into the goal at -X
        Out
    }

    public enum KickType
    {
        ShortPass,
        LongPass,
        Shoot
    }

    /// <summary>Player abilities used in Phase 1 (Full Spec 6.2). Range 40..99.</summary>
    [System.Serializable]
    public class PlayerAttributes
    {
        [Range(40, 99)] public int Speed = 70;
        [Range(40, 99)] public int Acceleration = 70;
        [Range(40, 99)] public int Agility = 70;
        [Range(40, 99)] public int BallControl = 70;
        [Range(40, 99)] public int ShortPass = 70;
        [Range(40, 99)] public int LongPass = 70;
        [Range(40, 99)] public int ShotPower = 70;
        [Range(40, 99)] public int Finishing = 70;
        [Range(40, 99)] public int Tackling = 70;
        [Range(40, 99)] public int Goalkeeping = 70;

        [Header("v0.6 (defaults keep older data compatible)")]
        public PreferredFoot PreferredFoot = PreferredFoot.Right;
        [Range(1, 5)] public int WeakFootRating = 3;

        /// <summary>Overall rating shown on the player card (simple average of the key abilities).</summary>
        public int Overall(Role role)
        {
            if (role == Role.GK) return Mathf.RoundToInt(Goalkeeping * 0.7f + Agility * 0.15f + LongPass * 0.15f);
            float v = (Speed + Acceleration + Agility + BallControl + ShortPass + LongPass + ShotPower + Finishing + Tackling) / 9f;
            return Mathf.RoundToInt(v);
        }

        public static PlayerAttributes Random(int baseLevel, System.Random rnd)
        {
            int R() => Mathf.Clamp(baseLevel + rnd.Next(-8, 9), 40, 99);
            return new PlayerAttributes
            {
                Speed = R(), Acceleration = R(), Agility = R(), BallControl = R(), ShortPass = R(),
                LongPass = R(), ShotPower = R(), Finishing = R(), Tackling = R(), Goalkeeping = R()
            };
        }
    }

    public enum Role { GK, DF, MF, FW }

    public enum PreferredFoot { Left, Right }

    /// <summary>NFT rarity tiers (Master Doc 7.4).</summary>
    public enum Rarity { Common, Rare, Epic, Mythic, Legendary }

    public static class Rules
    {
        /// <summary>Linear map of an ability (40..99) onto [atMin, atMax].</summary>
        public static float AttrLerp(int value, float atMin, float atMax)
        {
            float alpha = Mathf.Clamp01((value - 40f) / 59f);
            return Mathf.Lerp(atMin, atMax, alpha);
        }

        /// <summary>Hold time (seconds) to power gauge 0..1. Full at 1.0s (Full Spec 3.4.3).</summary>
        public static float HoldTimeToPower(float holdSeconds)
        {
            return Mathf.Clamp01(holdSeconds / 1f);
        }

        /// <summary>A press shorter than 0.15s is a tap (Full Spec 3.4.3).</summary>
        public static bool IsTap(float holdSeconds)
        {
            return holdSeconds < 0.15f;
        }

        /// <summary>
        /// Classifies the ball position. A goal needs the whole ball over the goal line,
        /// between the posts and under the bar.
        /// </summary>
        public static BallState ClassifyBall(Vector3 p)
        {
            bool pastLineX = Mathf.Abs(p.x) > PitchDims.HalfLength + PitchDims.BallRadius;
            bool insideGoalMouth = Mathf.Abs(p.z) < PitchDims.GoalHalfWidth - PitchDims.BallRadius
                                   && p.y < PitchDims.GoalHeight - PitchDims.BallRadius;

            if (pastLineX && insideGoalMouth)
            {
                return p.x > 0f ? BallState.GoalForHome : BallState.GoalForAway;
            }
            if (pastLineX || Mathf.Abs(p.z) > PitchDims.HalfWidth + PitchDims.BallRadius)
            {
                return BallState.Out;
            }
            return BallState.InPlay;
        }
    }
}
