#include "Core/NFWLGameMode.h"

#include "NFWL.h"
#include "Ball/NFWLBall.h"
#include "Components/DirectionalLightComponent.h"
#include "Components/SkyLightComponent.h"
#include "Core/NFWLGameState.h"
#include "Engine/DirectionalLight.h"
#include "Engine/SkyLight.h"
#include "EngineUtils.h"
#include "GameFramework/PlayerController.h"
#include "Pitch/NFWLPitch.h"
#include "Player/NFWLPlayerCharacter.h"
#include "TimerManager.h"
#include "UI/NFWLHUD.h"

ANFWLGameMode::ANFWLGameMode()
{
	PrimaryActorTick.bCanEverTick = true;

	DefaultPawnClass = ANFWLPlayerCharacter::StaticClass();
	GameStateClass = ANFWLGameState::StaticClass();
	HUDClass = ANFWLHUD::StaticClass();
	BallClass = ANFWLBall::StaticClass();
	PitchClass = ANFWLPitch::StaticClass();
}

void ANFWLGameMode::StartPlay()
{
	UWorld* World = GetWorld();
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;

	Pitch = World->SpawnActor<ANFWLPitch>(PitchClass, FTransform::Identity, Params);
	Ball = World->SpawnActor<ANFWLBall>(BallClass, FVector(0.f, 0.f, NFWLPitchDims::BallRadius + 2.f), FRotator::ZeroRotator, Params);
	EnsureLighting();

	Super::StartPlay();

	// Players may have spawned at an arbitrary PlayerStart; move everyone to kick-off.
	World->GetTimerManager().SetTimer(ResetTimer, this, &ANFWLGameMode::ResetKickoff, 0.1f, false);
}

void ANFWLGameMode::EnsureLighting()
{
	UWorld* World = GetWorld();
	if (TActorIterator<ADirectionalLight>(World))
	{
		return; // the level already has a sun
	}

	if (ADirectionalLight* Sun = World->SpawnActor<ADirectionalLight>(FVector(0.f, 0.f, 2000.f), FRotator(-50.f, 35.f, 0.f)))
	{
		Sun->GetLightComponent()->SetMobility(EComponentMobility::Movable);
		Sun->GetLightComponent()->SetIntensity(6.f);
	}
	if (!TActorIterator<ASkyLight>(World))
	{
		if (ASkyLight* Sky = World->SpawnActor<ASkyLight>(FVector(0.f, 0.f, 1000.f), FRotator::ZeroRotator))
		{
			USkyLightComponent* SkyComp = Sky->GetLightComponent();
			SkyComp->SetMobility(EComponentMobility::Movable);
			SkyComp->SetIntensity(1.f);
			SkyComp->SetLowerHemisphereColor(FLinearColor(0.2f, 0.25f, 0.2f));
			SkyComp->RecaptureSky();
		}
	}
	UE_LOG(LogNFWL, Log, TEXT("No directional light in level, spawned default lighting."));
}

void ANFWLGameMode::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	if (!Ball || bResetPending)
	{
		return;
	}

	switch (NFWLRules::ClassifyBall(Ball->GetActorLocation()))
	{
	case ENFWLBallState::GoalForHome: HandleGoal(true);  break;
	case ENFWLBallState::GoalForAway: HandleGoal(false); break;
	case ENFWLBallState::Out:         HandleOut();       break;
	default: break;
	}
}

void ANFWLGameMode::HandleGoal(bool bHomeScored)
{
	bResetPending = true;
	if (ANFWLGameState* GS = GetGameState<ANFWLGameState>())
	{
		(bHomeScored ? GS->HomeScore : GS->AwayScore)++;
		GS->ShowEvent(TEXT("GOAL!"), GoalResetDelay);
		UE_LOG(LogNFWL, Log, TEXT("GOAL for %s. Score %d - %d"), bHomeScored ? TEXT("HOME") : TEXT("AWAY"), GS->HomeScore, GS->AwayScore);
	}
	GetWorldTimerManager().SetTimer(ResetTimer, this, &ANFWLGameMode::ResetKickoff, GoalResetDelay, false);
}

void ANFWLGameMode::HandleOut()
{
	// Phase 1: restart from the centre. Kick-ins, corners and goal kicks come in Phase 2 (Full Spec 3.3).
	bResetPending = true;
	if (ANFWLGameState* GS = GetGameState<ANFWLGameState>())
	{
		GS->ShowEvent(TEXT("OUT"), OutResetDelay);
	}
	GetWorldTimerManager().SetTimer(ResetTimer, this, &ANFWLGameMode::ResetKickoff, OutResetDelay, false);
}

void ANFWLGameMode::ResetKickoff()
{
	if (Ball)
	{
		Ball->ResetBall(FVector(0.f, 0.f, NFWLPitchDims::BallRadius + 2.f));
	}

	for (FConstPlayerControllerIterator It = GetWorld()->GetPlayerControllerIterator(); It; ++It)
	{
		APlayerController* PC = It->Get();
		ANFWLPlayerCharacter* Player = PC ? Cast<ANFWLPlayerCharacter>(PC->GetPawn()) : nullptr;
		if (!Player)
		{
			continue;
		}
		const float Side = Player->TeamIndex == 0 ? -1.f : 1.f;
		const FRotator Facing(0.f, Player->TeamIndex == 0 ? 0.f : 180.f, 0.f);
		Player->SetActorLocationAndRotation(FVector(Side * 150.f, 0.f, 92.f), Facing, false, nullptr, ETeleportType::TeleportPhysics);
		PC->SetControlRotation(Facing);
		Player->ReleaseBall(0.f);
	}
	bResetPending = false;
}
