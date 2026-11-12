#include "Core/NFWLGameState.h"

#include "Net/UnrealNetwork.h"

void ANFWLGameState::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(ANFWLGameState, HomeScore);
	DOREPLIFETIME(ANFWLGameState, AwayScore);
}

void ANFWLGameState::ShowEvent(const FString& Text, float Seconds)
{
	EventText = Text;
	EventTextUntil = GetServerWorldTimeSeconds() + Seconds;
}
