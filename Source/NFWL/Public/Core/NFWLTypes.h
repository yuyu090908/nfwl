#pragma once

#include "CoreMinimal.h"
#include "NFWLTypes.generated.h"

/**
 * Pitch dimensions (Full Spec 3.1). Unreal units are centimetres.
 * The pitch is centred on the world origin, its length runs along X.
 * Team 0 (HOME) attacks +X, team 1 (AWAY) attacks -X.
 */
namespace NFWLPitchDims
{
	constexpr float HalfLength       = 2100.f; // 42m
	constexpr float HalfWidth        = 1250.f; // 25m
	constexpr float GoalHalfWidth    = 150.f;  // 3m
	constexpr float GoalHeight       = 200.f;  // 2m
	constexpr float GoalDepth        = 100.f;
	constexpr float PenaltyRadius    = 600.f;  // 6m semicircle
	constexpr float PenaltySpotDist  = 600.f;
	constexpr float CenterRadius     = 300.f;  // 3m
	constexpr float LineWidth        = 8.f;
	constexpr float BallRadius       = 11.f;   // size 4 ball, ~20cm diameter
}

/** Result of checking where the ball is (server decides, see Master Doc 16.2). */
UENUM(BlueprintType)
enum class ENFWLBallState : uint8
{
	InPlay,
	GoalForHome,   // ball went into the goal at +X
	GoalForAway,   // ball went into the goal at -X
	Out
};

UENUM(BlueprintType)
enum class ENFWLKickType : uint8
{
	ShortPass,
	LongPass,
	Shoot
};

/**
 * Player abilities used in Phase 1 (Full Spec 6.2). Range 40..99.
 * More abilities (Stamina, Strength, Vision, ...) are added in Phase 2.
 */
USTRUCT(BlueprintType)
struct FNFWLPlayerAttributes
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, meta=(ClampMin=40, ClampMax=99)) int32 Speed = 70;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, meta=(ClampMin=40, ClampMax=99)) int32 Acceleration = 70;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, meta=(ClampMin=40, ClampMax=99)) int32 Agility = 70;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, meta=(ClampMin=40, ClampMax=99)) int32 BallControl = 70;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, meta=(ClampMin=40, ClampMax=99)) int32 ShortPass = 70;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, meta=(ClampMin=40, ClampMax=99)) int32 LongPass = 70;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, meta=(ClampMin=40, ClampMax=99)) int32 ShotPower = 70;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, meta=(ClampMin=40, ClampMax=99)) int32 Finishing = 70;
};

/**
 * Match rules as data, so match length / halves / substitutions can be changed
 * by config (Master Doc 24: "試合時間・前後半・交代を設定値で変更できる").
 * Used from Phase 2; defined now so the shape is fixed early.
 */
USTRUCT(BlueprintType)
struct FNFWLMatchRules
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite) float HalfLengthSeconds = 600.f;   // 10 min
	UPROPERTY(EditAnywhere, BlueprintReadWrite) float HalfTimeSeconds = 60.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite) int32 MaxSubstitutionWindows = 2;
	UPROPERTY(EditAnywhere, BlueprintReadWrite) int32 BenchSize = 3;
	UPROPERTY(EditAnywhere, BlueprintReadWrite) float ExtraTimeHalfSeconds = 180.f;
};

namespace NFWLRules
{
	/** Linear map of an ability (40..99) onto [AtMin, AtMax]. */
	inline float AttrLerp(int32 Value, float AtMin, float AtMax)
	{
		const float Alpha = FMath::Clamp((static_cast<float>(Value) - 40.f) / 59.f, 0.f, 1.f);
		return FMath::Lerp(AtMin, AtMax, Alpha);
	}

	/** Hold time (seconds) -> power gauge 0..1. Full at 1.0s (Full Spec 3.4.3). */
	inline float HoldTimeToPower(float HoldSeconds)
	{
		return FMath::Clamp(HoldSeconds / 1.0f, 0.f, 1.f);
	}

	/** A press shorter than 0.15s is a tap (Full Spec 3.4.3). */
	inline bool IsTap(float HoldSeconds)
	{
		return HoldSeconds < 0.15f;
	}

	/**
	 * Classifies the ball position. A goal needs the whole ball over the goal line,
	 * between the posts and under the bar.
	 */
	inline ENFWLBallState ClassifyBall(const FVector& P)
	{
		using namespace NFWLPitchDims;
		const bool bPastLineX = FMath::Abs(P.X) > HalfLength + BallRadius;
		const bool bInsideGoalMouth = FMath::Abs(P.Y) < GoalHalfWidth - BallRadius && P.Z < GoalHeight - BallRadius;

		if (bPastLineX && bInsideGoalMouth)
		{
			return P.X > 0.f ? ENFWLBallState::GoalForHome : ENFWLBallState::GoalForAway;
		}
		if (bPastLineX || FMath::Abs(P.Y) > HalfWidth + BallRadius)
		{
			return ENFWLBallState::Out;
		}
		return ENFWLBallState::InPlay;
	}
}
