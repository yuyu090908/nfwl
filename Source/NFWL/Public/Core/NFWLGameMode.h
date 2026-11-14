#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "Core/NFWLTypes.h"
#include "NFWLGameMode.generated.h"

class ANFWLBall;
class ANFWLPitch;

/**
 * Phase 1 game mode: builds the pitch, spawns the ball, decides goals / outs
 * and resets to kick-off. All decisions run on the authority only
 * (Master Doc 16.2: the server decides match results).
 */
UCLASS()
class NFWL_API ANFWLGameMode : public AGameModeBase
{
	GENERATED_BODY()

public:
	ANFWLGameMode();

	virtual void StartPlay() override;
	virtual void Tick(float DeltaSeconds) override;

	UFUNCTION(BlueprintCallable, Category="NFWL|Match")
	void ResetKickoff();

	ANFWLBall* GetBall() const { return Ball; }

protected:
	UPROPERTY(EditDefaultsOnly, Category="NFWL|Classes")
	TSubclassOf<ANFWLBall> BallClass;

	UPROPERTY(EditDefaultsOnly, Category="NFWL|Classes")
	TSubclassOf<ANFWLPitch> PitchClass;

	UPROPERTY(EditDefaultsOnly, Category="NFWL|Match")
	float GoalResetDelay = 2.5f;

	UPROPERTY(EditDefaultsOnly, Category="NFWL|Match")
	float OutResetDelay = 1.5f;

	/** Not used by Phase 1 logic yet; the match clock arrives in Phase 2. */
	UPROPERTY(EditDefaultsOnly, Category="NFWL|Match")
	FNFWLMatchRules MatchRules;

private:
	void HandleGoal(bool bHomeScored);
	void HandleOut();
	void EnsureLighting();

	UPROPERTY(Transient)
	TObjectPtr<ANFWLBall> Ball;

	UPROPERTY(Transient)
	TObjectPtr<ANFWLPitch> Pitch;

	bool bResetPending = false;
	FTimerHandle ResetTimer;
};
