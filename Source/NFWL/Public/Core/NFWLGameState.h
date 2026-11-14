#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameStateBase.h"
#include "NFWLGameState.generated.h"

/** Match state shared with all clients (and spectators from Phase 3). */
UCLASS()
class NFWL_API ANFWLGameState : public AGameStateBase
{
	GENERATED_BODY()

public:
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	UPROPERTY(Replicated, BlueprintReadOnly, Category="NFWL|Match")
	int32 HomeScore = 0;

	UPROPERTY(Replicated, BlueprintReadOnly, Category="NFWL|Match")
	int32 AwayScore = 0;

	/** Short on-screen message such as "GOAL!" (local only in Phase 1). */
	FString EventText;
	float EventTextUntil = 0.f;

	void ShowEvent(const FString& Text, float Seconds);
};
