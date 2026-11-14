#include "Player/NFWLPlayerCharacter.h"

#include "NFWL.h"
#include "Ball/NFWLBall.h"
#include "Camera/CameraComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/StaticMeshComponent.h"
#include "EngineUtils.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "Engine/LocalPlayer.h"
#include "Engine/StaticMesh.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/SpringArmComponent.h"
#include "InputAction.h"
#include "InputActionValue.h"
#include "InputMappingContext.h"
#include "InputModifiers.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Materials/MaterialInterface.h"
#include "UObject/ConstructorHelpers.h"

ANFWLPlayerCharacter::ANFWLPlayerCharacter()
{
	PrimaryActorTick.bCanEverTick = true;

	GetCapsuleComponent()->InitCapsuleSize(30.f, 90.f);
	// The ball is controlled by code while dribbling, so the body does not physically block it.
	GetCapsuleComponent()->SetCollisionResponseToChannel(ECC_PhysicsBody, ECR_Overlap);

	bUseControllerRotationPitch = false;
	bUseControllerRotationYaw = false;
	bUseControllerRotationRoll = false;

	UCharacterMovementComponent* Move = GetCharacterMovement();
	Move->bOrientRotationToMovement = true;
	Move->GroundFriction = 8.f;
	Move->bUseSeparateBrakingFriction = false;

	// Oblique overhead, side-on broadcast style camera (Master Doc 3.1: 斜め俯瞰).
	CameraBoom = CreateDefaultSubobject<USpringArmComponent>(TEXT("CameraBoom"));
	CameraBoom->SetupAttachment(RootComponent);
	CameraBoom->SetUsingAbsoluteRotation(true);
	CameraBoom->SetWorldRotation(FRotator(-45.f, -90.f, 0.f)); // camera sits on +Y side looking at -Y
	CameraBoom->TargetArmLength = 2600.f;
	CameraBoom->bDoCollisionTest = false;
	CameraBoom->bUsePawnControlRotation = false;
	CameraBoom->bInheritPitch = false;
	CameraBoom->bInheritYaw = false;
	CameraBoom->bInheritRoll = false;
	CameraBoom->bEnableCameraLag = true;
	CameraBoom->CameraLagSpeed = 6.f;

	FollowCamera = CreateDefaultSubobject<UCameraComponent>(TEXT("FollowCamera"));
	FollowCamera->SetupAttachment(CameraBoom, USpringArmComponent::SocketName);
	FollowCamera->SetFieldOfView(55.f);

	// Placeholder body until real character art and animation.
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CylinderMesh(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeMesh(TEXT("/Engine/BasicShapes/Cube.Cube"));

	BodyMesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("BodyMesh"));
	BodyMesh->SetupAttachment(RootComponent);
	BodyMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	BodyMesh->SetRelativeScale3D(FVector(0.55f, 0.55f, 1.8f));

	FacingMarker = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("FacingMarker"));
	FacingMarker->SetupAttachment(RootComponent);
	FacingMarker->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	FacingMarker->SetRelativeLocation(FVector(30.f, 0.f, 50.f));
	FacingMarker->SetRelativeScale3D(FVector(0.25f, 0.15f, 0.15f));

	if (CylinderMesh.Succeeded()) { BodyMesh->SetStaticMesh(CylinderMesh.Object); }
	if (CubeMesh.Succeeded()) { FacingMarker->SetStaticMesh(CubeMesh.Object); }

	ApplyAttributes();
}

void ANFWLPlayerCharacter::BeginPlay()
{
	Super::BeginPlay();
	ApplyAttributes();

	if (UMaterialInterface* BaseMat = LoadObject<UMaterialInterface>(nullptr, TEXT("/Engine/BasicShapes/BasicShapeMaterial.BasicShapeMaterial")))
	{
		UMaterialInstanceDynamic* Mid = UMaterialInstanceDynamic::Create(BaseMat, this);
		Mid->SetVectorParameterValue(TEXT("Color"), TeamIndex == 0 ? FLinearColor(0.05f, 0.2f, 0.9f) : FLinearColor(0.9f, 0.1f, 0.1f));
		BodyMesh->SetMaterial(0, Mid);
	}
}

void ANFWLPlayerCharacter::ApplyAttributes()
{
	DashSpeed = NFWLRules::AttrLerp(Attributes.Speed, 600.f, 740.f);   // 6.0-7.4 m/s
	RunSpeed = DashSpeed * 0.7f;
	const float AccelTime = NFWLRules::AttrLerp(Attributes.Acceleration, 1.6f, 1.0f);

	UCharacterMovementComponent* Move = GetCharacterMovement();
	Move->MaxAcceleration = DashSpeed / AccelTime;
	Move->BrakingDecelerationWalking = DashSpeed / 0.3f;                // stop in ~0.2-0.4s
	Move->RotationRate = FRotator(0.f, NFWLRules::AttrLerp(Attributes.Agility, 420.f, 720.f), 0.f);
	UpdateMoveSpeed();
}

void ANFWLPlayerCharacter::UpdateMoveSpeed()
{
	float Speed = bSprinting ? DashSpeed : RunSpeed;
	if (bCharging && ChargeType == ENFWLKickType::Shoot)
	{
		Speed *= 0.7f; // charging a shot slows the player by 30% (Full Spec 3.6.2)
	}
	GetCharacterMovement()->MaxWalkSpeed = Speed;
}

// ---------------------------------------------------------------------------
// Input
// ---------------------------------------------------------------------------

void ANFWLPlayerCharacter::CreateInputAssets()
{
	if (MappingContext)
	{
		return;
	}

	auto MakeAction = [this](const TCHAR* Name, EInputActionValueType Type)
	{
		UInputAction* Action = NewObject<UInputAction>(this, Name);
		Action->ValueType = Type;
		return Action;
	};

	MoveAction      = MakeAction(TEXT("IA_Move"), EInputActionValueType::Axis2D);
	SprintAction    = MakeAction(TEXT("IA_Sprint"), EInputActionValueType::Boolean);
	ShortPassAction = MakeAction(TEXT("IA_ShortPass"), EInputActionValueType::Boolean);
	LongPassAction  = MakeAction(TEXT("IA_LongPass"), EInputActionValueType::Boolean);
	ShootAction     = MakeAction(TEXT("IA_Shoot"), EInputActionValueType::Boolean);

	MappingContext = NewObject<UInputMappingContext>(this, TEXT("IMC_Football"));

	// Gamepad first (Master Doc 3.1). Button positions follow Full Spec 3.4.1.
	MappingContext->MapKey(MoveAction, EKeys::Gamepad_Left2D);
	MappingContext->MapKey(SprintAction, EKeys::Gamepad_RightShoulder);      // R1
	MappingContext->MapKey(ShortPassAction, EKeys::Gamepad_FaceButton_Bottom);
	MappingContext->MapKey(ShootAction, EKeys::Gamepad_FaceButton_Right);
	MappingContext->MapKey(LongPassAction, EKeys::Gamepad_FaceButton_Left);

	// Keyboard: WASD to move, Shift sprint, J pass, K shoot, L long pass.
	MappingContext->MapKey(MoveAction, EKeys::D);
	{
		FEnhancedActionKeyMapping& M = MappingContext->MapKey(MoveAction, EKeys::A);
		M.Modifiers.Add(NewObject<UInputModifierNegate>(MappingContext));
	}
	{
		FEnhancedActionKeyMapping& M = MappingContext->MapKey(MoveAction, EKeys::W);
		M.Modifiers.Add(NewObject<UInputModifierSwizzleAxis>(MappingContext));
	}
	{
		FEnhancedActionKeyMapping& M = MappingContext->MapKey(MoveAction, EKeys::S);
		M.Modifiers.Add(NewObject<UInputModifierSwizzleAxis>(MappingContext));
		M.Modifiers.Add(NewObject<UInputModifierNegate>(MappingContext));
	}
	MappingContext->MapKey(SprintAction, EKeys::LeftShift);
	MappingContext->MapKey(ShortPassAction, EKeys::J);
	MappingContext->MapKey(ShootAction, EKeys::K);
	MappingContext->MapKey(LongPassAction, EKeys::L);
}

void ANFWLPlayerCharacter::NotifyControllerChanged()
{
	Super::NotifyControllerChanged();

	CreateInputAssets();
	if (APlayerController* PC = Cast<APlayerController>(Controller))
	{
		if (UEnhancedInputLocalPlayerSubsystem* Subsystem = ULocalPlayer::GetSubsystem<UEnhancedInputLocalPlayerSubsystem>(PC->GetLocalPlayer()))
		{
			Subsystem->AddMappingContext(MappingContext, 0);
		}
	}
}

void ANFWLPlayerCharacter::SetupPlayerInputComponent(UInputComponent* PlayerInputComponent)
{
	Super::SetupPlayerInputComponent(PlayerInputComponent);
	CreateInputAssets();

	UEnhancedInputComponent* Input = Cast<UEnhancedInputComponent>(PlayerInputComponent);
	if (!Input)
	{
		UE_LOG(LogNFWL, Error, TEXT("Enhanced Input is not enabled. Check Config/DefaultInput.ini."));
		return;
	}

	Input->BindAction(MoveAction, ETriggerEvent::Triggered, this, &ANFWLPlayerCharacter::OnMove);
	Input->BindAction(MoveAction, ETriggerEvent::Completed, this, &ANFWLPlayerCharacter::OnMoveReleased);
	Input->BindAction(SprintAction, ETriggerEvent::Started, this, &ANFWLPlayerCharacter::OnSprintStarted);
	Input->BindAction(SprintAction, ETriggerEvent::Completed, this, &ANFWLPlayerCharacter::OnSprintCompleted);
	Input->BindAction(ShortPassAction, ETriggerEvent::Started, this, &ANFWLPlayerCharacter::OnShortPassStarted);
	Input->BindAction(ShortPassAction, ETriggerEvent::Completed, this, &ANFWLPlayerCharacter::OnShortPassCompleted);
	Input->BindAction(LongPassAction, ETriggerEvent::Started, this, &ANFWLPlayerCharacter::OnLongPassStarted);
	Input->BindAction(LongPassAction, ETriggerEvent::Completed, this, &ANFWLPlayerCharacter::OnLongPassCompleted);
	Input->BindAction(ShootAction, ETriggerEvent::Started, this, &ANFWLPlayerCharacter::OnShootStarted);
	Input->BindAction(ShootAction, ETriggerEvent::Completed, this, &ANFWLPlayerCharacter::OnShootCompleted);
}

void ANFWLPlayerCharacter::OnMove(const FInputActionValue& Value)
{
	MoveInput = Value.Get<FVector2D>();
}

void ANFWLPlayerCharacter::OnMoveReleased(const FInputActionValue& Value)
{
	MoveInput = FVector2D::ZeroVector;
}

void ANFWLPlayerCharacter::OnSprintStarted(const FInputActionValue& Value)   { bSprinting = true;  UpdateMoveSpeed(); }
void ANFWLPlayerCharacter::OnSprintCompleted(const FInputActionValue& Value) { bSprinting = false; UpdateMoveSpeed(); }
void ANFWLPlayerCharacter::OnShortPassStarted(const FInputActionValue& Value)   { BeginCharge(ENFWLKickType::ShortPass); }
void ANFWLPlayerCharacter::OnShortPassCompleted(const FInputActionValue& Value) { ReleaseCharge(ENFWLKickType::ShortPass); }
void ANFWLPlayerCharacter::OnLongPassStarted(const FInputActionValue& Value)    { BeginCharge(ENFWLKickType::LongPass); }
void ANFWLPlayerCharacter::OnLongPassCompleted(const FInputActionValue& Value)  { ReleaseCharge(ENFWLKickType::LongPass); }
void ANFWLPlayerCharacter::OnShootStarted(const FInputActionValue& Value)       { BeginCharge(ENFWLKickType::Shoot); }
void ANFWLPlayerCharacter::OnShootCompleted(const FInputActionValue& Value)     { ReleaseCharge(ENFWLKickType::Shoot); }

// ---------------------------------------------------------------------------
// Kicking
// ---------------------------------------------------------------------------

void ANFWLPlayerCharacter::BeginCharge(ENFWLKickType Type)
{
	bCharging = true;
	ChargeType = Type;
	ChargeStartTime = GetWorld()->GetTimeSeconds();
	UpdateMoveSpeed();
}

void ANFWLPlayerCharacter::ReleaseCharge(ENFWLKickType Type)
{
	if (!bCharging || ChargeType != Type)
	{
		return;
	}
	const float Held = GetWorld()->GetTimeSeconds() - ChargeStartTime;
	bCharging = false;
	UpdateMoveSpeed();

	const float Power = NFWLRules::HoldTimeToPower(Held);
	const bool bTap = NFWLRules::IsTap(Held);

	if (bHasBall)
	{
		PerformKick(Type, Power, bTap);
	}
	else
	{
		// Keep the command for a short time: e.g. pass pressed during a trap (Full Spec 3.4.3).
		bKickBuffered = true;
		BufferedType = Type;
		BufferedPower = Power;
		bBufferedTap = bTap;
		BufferedUntil = GetWorld()->GetTimeSeconds() + InputBufferSeconds;
	}
}

float ANFWLPlayerCharacter::GetChargeRatio() const
{
	if (!bCharging || !GetWorld())
	{
		return -1.f;
	}
	return NFWLRules::HoldTimeToPower(GetWorld()->GetTimeSeconds() - ChargeStartTime);
}

FString ANFWLPlayerCharacter::GetChargeLabel() const
{
	switch (ChargeType)
	{
	case ENFWLKickType::ShortPass: return TEXT("PASS");
	case ENFWLKickType::LongPass:  return TEXT("LONG PASS");
	case ENFWLKickType::Shoot:     return TEXT("SHOT");
	}
	return FString();
}

FVector ANFWLPlayerCharacter::GetInputWorldDirection() const
{
	if (MoveInput.SizeSquared() < 0.04f)
	{
		return FVector::ZeroVector;
	}
	// Directions are relative to the fixed camera yaw.
	const FRotator YawOnly(0.f, CameraBoom->GetComponentRotation().Yaw, 0.f);
	const FVector Forward = FRotationMatrix(YawOnly).GetUnitAxis(EAxis::X);
	const FVector Right = FRotationMatrix(YawOnly).GetUnitAxis(EAxis::Y);
	return (Forward * MoveInput.Y + Right * MoveInput.X).GetSafeNormal2D();
}

FVector ANFWLPlayerCharacter::GetKickDirection() const
{
	const FVector InputDir = GetInputWorldDirection();
	return InputDir.IsNearlyZero() ? GetActorForwardVector().GetSafeNormal2D() : InputDir;
}

FVector ANFWLPlayerCharacter::GetAttackGoalCenter() const
{
	const float X = (TeamIndex == 0 ? 1.f : -1.f) * NFWLPitchDims::HalfLength;
	return FVector(X, 0.f, 0.f);
}

void ANFWLPlayerCharacter::PerformKick(ENFWLKickType Type, float Power, bool bTap)
{
	ANFWLBall* Ball = FindBall();
	if (!Ball)
	{
		return;
	}

	FVector Dir = GetKickDirection();
	float Speed = 0.f;
	float ElevationDeg = 0.f;
	float ErrorDeg = 0.f;

	switch (Type)
	{
	case ENFWLKickType::ShortPass:
	{
		// Phase 1 has no team-mates yet, so a tap uses a medium strength.
		const float P = bTap ? 0.4f : Power;
		Speed = FMath::Lerp(700.f, 2300.f, P);
		ElevationDeg = 0.f; // along the ground
		ErrorDeg = NFWLRules::AttrLerp(Attributes.ShortPass, 6.f, 1.f);
		break;
	}
	case ENFWLKickType::LongPass:
	{
		const float P = bTap ? 0.5f : Power;
		Speed = FMath::Lerp(1400.f, 2800.f, P);
		ElevationDeg = FMath::Lerp(25.f, 38.f, P);
		ErrorDeg = NFWLRules::AttrLerp(Attributes.LongPass, 8.f, 2.f);
		break;
	}
	case ENFWLKickType::Shoot:
	{
		const float P = bTap ? 0.3f : Power;
		Speed = FMath::Lerp(1500.f, 3200.f, P) * NFWLRules::AttrLerp(Attributes.ShotPower, 0.85f, 1.05f);
		ElevationDeg = FMath::Lerp(2.f, 16.f, P * P); // over-hit shots rise (Full Spec 3.6.2)

		// Light shot assist: pull towards the goal when roughly aiming at it.
		const FVector ToGoal = (GetAttackGoalCenter() - GetActorLocation()).GetSafeNormal2D();
		if (FVector::DotProduct(Dir, ToGoal) > FMath::Cos(FMath::DegreesToRadians(50.f)))
		{
			Dir = (Dir * 0.65f + ToGoal * 0.35f).GetSafeNormal2D();
		}
		ErrorDeg = NFWLRules::AttrLerp(Attributes.Finishing, 7.f, 1.5f) * (0.4f + 0.6f * P);
		break;
	}
	}

	if (bSprinting)
	{
		ErrorDeg *= 1.3f;
	}

	// Accuracy is a deviation of the aim, the result itself comes from physics (Full Spec 3.6.2).
	const float YawError = FMath::FRandRange(-ErrorDeg, ErrorDeg);
	const float PitchError = Type == ENFWLKickType::ShortPass ? 0.f : FMath::FRandRange(-ErrorDeg, ErrorDeg) * 0.5f;
	Dir = Dir.RotateAngleAxis(YawError, FVector::UpVector);

	const float Elev = FMath::DegreesToRadians(FMath::Max(0.f, ElevationDeg + PitchError));
	const FVector Velocity = Dir * (Speed * FMath::Cos(Elev)) + FVector::UpVector * (Speed * FMath::Sin(Elev));

	Ball->Kick(Velocity, this);
	ReleaseBall(KickCooldown);

	UE_LOG(LogNFWL, Verbose, TEXT("Kick type=%d power=%.2f speed=%.0f"), static_cast<int32>(Type), Power, Speed);
}

void ANFWLPlayerCharacter::ReleaseBall(float Cooldown)
{
	bHasBall = false;
	bKickBuffered = false;
	if (UWorld* World = GetWorld())
	{
		NoControlUntil = World->GetTimeSeconds() + Cooldown;
	}
}

// ---------------------------------------------------------------------------
// Tick: movement and dribbling
// ---------------------------------------------------------------------------

void ANFWLPlayerCharacter::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	const FVector Dir = GetInputWorldDirection();
	if (!Dir.IsNearlyZero())
	{
		AddMovementInput(Dir, FMath::Min(1.f, MoveInput.Size()));
	}

	UpdateBallControl(DeltaSeconds);

	if (bKickBuffered)
	{
		if (GetWorld()->GetTimeSeconds() > BufferedUntil)
		{
			bKickBuffered = false;
		}
		else if (bHasBall)
		{
			bKickBuffered = false;
			PerformKick(BufferedType, BufferedPower, bBufferedTap);
		}
	}
}

void ANFWLPlayerCharacter::UpdateBallControl(float DeltaSeconds)
{
	ANFWLBall* Ball = FindBall();
	if (!Ball || !HasAuthority())
	{
		return;
	}

	if (GetWorld()->GetTimeSeconds() < NoControlUntil)
	{
		bHasBall = false;
		return;
	}

	const FVector BallLoc = Ball->GetActorLocation();
	const FVector BallVel = Ball->GetBallVelocity();
	const float Dist2D = FVector::Dist2D(BallLoc, GetActorLocation());

	if (!bHasBall)
	{
		// Automatic first touch when the ball is close, low and not too fast (Full Spec 3.6.5).
		const float RelSpeed = (BallVel - GetVelocity()).Size2D();
		const float TrapLimit = MaxTrapRelativeSpeed * NFWLRules::AttrLerp(Attributes.BallControl, 0.8f, 1.2f);
		if (Dist2D < ControlRadius && BallLoc.Z < 80.f && RelSpeed < TrapLimit)
		{
			bHasBall = true;
			Ball->SetLastTouchedBy(this);
		}
	}
	else if (Dist2D > LoseRadius)
	{
		bHasBall = false;
	}

	if (!bHasBall)
	{
		return;
	}

	// Dribble: steer the ball towards a point in front of the feet.
	const float Touch = bSprinting ? SprintTouchDistance : NormalTouchDistance;
	FVector Target = GetActorLocation() + GetActorForwardVector().GetSafeNormal2D() * (GetCapsuleComponent()->GetScaledCapsuleRadius() + Touch);
	Target.Z = BallLoc.Z;

	const float Gain = NFWLRules::AttrLerp(Attributes.BallControl, 6.f, 12.f);
	FVector Desired = GetVelocity() + (Target - BallLoc) * Gain;
	Desired.Z = BallVel.Z;
	Ball->SetBallVelocity(Desired);
}

ANFWLBall* ANFWLPlayerCharacter::FindBall()
{
	if (CachedBall.IsValid())
	{
		return CachedBall.Get();
	}
	TActorIterator<ANFWLBall> It(GetWorld());
	if (It)
	{
		CachedBall = *It;
	}
	return CachedBall.Get();
}
