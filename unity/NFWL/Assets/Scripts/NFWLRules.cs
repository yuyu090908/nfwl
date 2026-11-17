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
    }

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
