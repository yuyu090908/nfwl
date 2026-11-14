#pragma once

#include "CoreMinimal.h"
#include "GameFramework/HUD.h"
#include "NFWLHUD.generated.h"

/** Minimal canvas HUD: score, power gauge, event text, controls. UMG comes later. */
UCLASS()
class NFWL_API ANFWLHUD : public AHUD
{
	GENERATED_BODY()

public:
	virtual void DrawHUD() override;
};
