#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "Core/NFWLTypes.h"
#include "NFWLPlayerCharacter.generated.h"

class USpringArmComponent;
class UCameraComponent;
class UStaticMeshComponent;
class UInputMappingContext;
class UInputAction;
class ANFWLBall;
struct FInputActionValue;

/**
 * A footballer the user controls.
 * Phase 1: move, sprint, dribble, short pass, long pass, shoot (with power gauge).
 * Placeholder body (capsule + cylinder) until the skeletal mesh / animation work.
 */
UCLASS()
class NFWL_API ANFWLPlayerCharacter : public ACharacter
{
	GENERATED_BODY()

public:
	ANFWLPlayerCharacter();

	virtual void Tick(float DeltaSeconds) override;
	virtual void SetupPlayerInputComponent(UInputComponent* PlayerInputComponent) override;
	virtual void NotifyControllerChanged() override;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="NFWL")
	FNFWLPlayerAttributes Attributes;

	/** 0 = HOME (attacks +X), 1 = AWAY (attacks -X). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="NFWL")
	int32 TeamIndex = 0;

	UFUNCTION(BlueprintPure, Category="NFWL")
	bool HasBall() const { return bHasBall; }

	/** Power gauge 0..1 while a kick button is held, otherwise -1. */
	UFUNCTION(BlueprintPure, Category="NFWL")
	float GetChargeRatio() const;

	FString GetChargeLabel() const;

	/** Recomputes movement values from Attributes. */
	UFUNCTION(BlueprintCallable, Category="NFWL")
	void ApplyAttributes();

	/** Drops control of the ball and blocks re-taking it for Cooldown seconds. */
	void ReleaseBall(float Cooldown);

protected:
	virtual void BeginPlay() override;

	// --- Components ---
	UPROPERTY(VisibleAnywhere, Category="NFWL|Camera")
	TObjectPtr<USpringArmComponent> CameraBoom;

	UPROPERTY(VisibleAnywhere, Category="NFWL|Camera")
	TObjectPtr<UCameraComponent> FollowCamera;

	UPROPERTY(VisibleAnywhere, Category="NFWL|Body")
	TObjectPtr<UStaticMeshComponent> BodyMesh;

	UPROPERTY(VisibleAnywhere, Category="NFWL|Body")
	TObjectPtr<UStaticMeshComponent> FacingMarker;

	// --- Tuning (Full Spec 3.6 / 3.7) ---
	UPROPERTY(EditAnywhere, Category="NFWL|Dribble")
	float ControlRadius = 90.f;          // ball closer than this can be controlled

	UPROPERTY(EditAnywhere, Category="NFWL|Dribble")
	float LoseRadius = 250.f;            // ball further than this is lost

	UPROPERTY(EditAnywhere, Category="NFWL|Dribble")
	float MaxTrapRelativeSpeed = 1800.f; // faster balls bounce off instead of being trapped

	UPROPERTY(EditAnywhere, Category="NFWL|Dribble")
	float NormalTouchDistance = 70.f;    // 0.5-1.0m in front

	UPROPERTY(EditAnywhere, Category="NFWL|Dribble")
	float SprintTouchDistance = 190.f;   // 1.5-2.5m in front

	UPROPERTY(EditAnywhere, Category="NFWL|Kick")
	float KickCooldown = 0.35f;

	UPROPERTY(EditAnywhere, Category="NFWL|Kick")
	float InputBufferSeconds = 0.15f;    // Full Spec 3.4.3

private:
	// --- Input (created in code so no .uasset is needed for Phase 1) ---
	UPROPERTY(Transient) TObjectPtr<UInputMappingContext> MappingContext;
	UPROPERTY(Transient) TObjectPtr<UInputAction> MoveAction;
	UPROPERTY(Transient) TObjectPtr<UInputAction> SprintAction;
	UPROPERTY(Transient) TObjectPtr<UInputAction> ShortPassAction;
	UPROPERTY(Transient) TObjectPtr<UInputAction> LongPassAction;
	UPROPERTY(Transient) TObjectPtr<UInputAction> ShootAction;

	void CreateInputAssets();

	void OnMove(const FInputActionValue& Value);
	void OnMoveReleased(const FInputActionValue& Value);
	void OnSprintStarted(const FInputActionValue& Value);
	void OnSprintCompleted(const FInputActionValue& Value);
	void OnShortPassStarted(const FInputActionValue& Value);
	void OnShortPassCompleted(const FInputActionValue& Value);
	void OnLongPassStarted(const FInputActionValue& Value);
	void OnLongPassCompleted(const FInputActionValue& Value);
	void OnShootStarted(const FInputActionValue& Value);
	void OnShootCompleted(const FInputActionValue& Value);

	void BeginCharge(ENFWLKickType Type);
	void ReleaseCharge(ENFWLKickType Type);
	void PerformKick(ENFWLKickType Type, float Power, bool bTap);

	void UpdateBallControl(float DeltaSeconds);
	void UpdateMoveSpeed();
	FVector GetInputWorldDirection() const;
	FVector GetKickDirection() const;
	FVector GetAttackGoalCenter() const;
	ANFWLBall* FindBall();

	TWeakObjectPtr<ANFWLBall> CachedBall;

	FVector2D MoveInput = FVector2D::ZeroVector;
	bool bSprinting = false;
	bool bHasBall = false;
	float NoControlUntil = 0.f;

	bool bCharging = false;
	ENFWLKickType ChargeType = ENFWLKickType::ShortPass;
	float ChargeStartTime = 0.f;

	// Buffered kick: pressed while not on the ball, executed if the ball arrives in time.
	bool bKickBuffered = false;
	ENFWLKickType BufferedType = ENFWLKickType::ShortPass;
	float BufferedPower = 0.f;
	bool bBufferedTap = false;
	float BufferedUntil = 0.f;

	float DashSpeed = 670.f;
	float RunSpeed = 470.f;
};
