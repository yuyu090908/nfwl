#include "UI/NFWLHUD.h"

#include "Core/NFWLGameState.h"
#include "Engine/Canvas.h"
#include "Engine/Engine.h"
#include "Engine/Font.h"
#include "GameFramework/PlayerController.h"
#include "Player/NFWLPlayerCharacter.h"

void ANFWLHUD::DrawHUD()
{
	Super::DrawHUD();
	if (!Canvas || !GEngine)
	{
		return;
	}

	UFont* Large = GEngine->GetLargeFont();
	UFont* Small = GEngine->GetSmallFont();
	const float CX = Canvas->ClipX * 0.5f;

	// Score
	if (const ANFWLGameState* GS = GetWorld()->GetGameState<ANFWLGameState>())
	{
		const FString Score = FString::Printf(TEXT("HOME  %d - %d  AWAY"), GS->HomeScore, GS->AwayScore);
		float W = 0.f, H = 0.f;
		GetTextSize(Score, W, H, Large, 1.5f);
		DrawRect(FLinearColor(0.f, 0.f, 0.f, 0.5f), CX - W * 0.5f - 16.f, 16.f, W + 32.f, H + 12.f);
		DrawText(Score, FLinearColor::White, CX - W * 0.5f, 22.f, Large, 1.5f);

		if (!GS->EventText.IsEmpty() && GS->GetServerWorldTimeSeconds() < GS->EventTextUntil)
		{
			GetTextSize(GS->EventText, W, H, Large, 4.f);
			DrawText(GS->EventText, FLinearColor::Yellow, CX - W * 0.5f, Canvas->ClipY * 0.3f, Large, 4.f);
		}
	}

	// Power gauge
	const APlayerController* PC = GetOwningPlayerController();
	if (const ANFWLPlayerCharacter* Player = PC ? Cast<ANFWLPlayerCharacter>(PC->GetPawn()) : nullptr)
	{
		const float Ratio = Player->GetChargeRatio();
		if (Ratio >= 0.f)
		{
			const float BarW = 300.f, BarH = 18.f;
			const float X = CX - BarW * 0.5f;
			const float Y = Canvas->ClipY - 110.f;
			DrawRect(FLinearColor(0.f, 0.f, 0.f, 0.6f), X - 3.f, Y - 3.f, BarW + 6.f, BarH + 6.f);
			const FLinearColor Fill = FLinearColor::LerpUsingHSV(FLinearColor::Green, FLinearColor::Red, Ratio);
			DrawRect(Fill, X, Y, BarW * Ratio, BarH);
			DrawText(Player->GetChargeLabel(), FLinearColor::White, X, Y - 22.f, Small, 1.2f);
		}

		if (Player->HasBall())
		{
			DrawText(TEXT("BALL"), FLinearColor::Green, 20.f, Canvas->ClipY - 70.f, Small, 1.2f);
		}
	}

	DrawText(TEXT("Move: L-Stick / WASD   Sprint: R1 / Shift   Pass: A / J   Shoot: B / K (hold = power)   Long pass: X / L"),
		FLinearColor(1.f, 1.f, 1.f, 0.8f), 20.f, Canvas->ClipY - 40.f, Small, 1.f);
}
