#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "NFWLBall.generated.h"

class USphereComponent;
class UStaticMeshComponent;
class ANFWLPlayerCharacter;

/**
 * The match ball. Physics simulated; in Phase 3 the dedicated server owns the
 * simulation and clients only receive replicated movement.
 */
UCLASS()
class NFWL_API ANFWLBall : public AActor
{
	GENERATED_BODY()

public:
	ANFWLBall();

	/** Sets the ball velocity as the result of a kick. */
	UFUNCTION(BlueprintCallable, Category="NFWL|Ball")
	void Kick(const FVector& Velocity, ANFWLPlayerCharacter* Kicker);

	/** Teleports the ball and stops it. */
	UFUNCTION(BlueprintCallable, Category="NFWL|Ball")
	void ResetBall(const FVector& Location);

	FVector GetBallVelocity() const;
	void SetBallVelocity(const FVector& Velocity);

	ANFWLPlayerCharacter* GetLastTouchedBy() const { return LastTouchedBy.Get(); }
	void SetLastTouchedBy(ANFWLPlayerCharacter* Player) { LastTouchedBy = Player; }

protected:
	virtual void BeginPlay() override;

	UPROPERTY(VisibleAnywhere, Category="NFWL|Ball")
	TObjectPtr<USphereComponent> Sphere;

	UPROPERTY(VisibleAnywhere, Category="NFWL|Ball")
	TObjectPtr<UStaticMeshComponent> Mesh;

	UPROPERTY(EditAnywhere, Category="NFWL|Ball|Physics")
	float MassKg = 0.40f;

	UPROPERTY(EditAnywhere, Category="NFWL|Ball|Physics")
	float LinearDamping = 0.15f;

	UPROPERTY(EditAnywhere, Category="NFWL|Ball|Physics")
	float AngularDamping = 0.6f;

	UPROPERTY(EditAnywhere, Category="NFWL|Ball|Physics")
	float Restitution = 0.55f;

	UPROPERTY(EditAnywhere, Category="NFWL|Ball|Physics")
	float Friction = 0.7f;

private:
	TWeakObjectPtr<ANFWLPlayerCharacter> LastTouchedBy;
};
