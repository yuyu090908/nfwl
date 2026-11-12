#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "NFWLPitch.generated.h"

class UStaticMesh;
class UMaterialInterface;
class UStaticMeshComponent;

/**
 * Builds the 42m x 25m pitch, markings and both goals from engine basic shapes
 * (Full Spec 3.1). Replaced by an art-built stadium later; the dimensions stay.
 */
UCLASS()
class NFWL_API ANFWLPitch : public AActor
{
	GENERATED_BODY()

public:
	ANFWLPitch();

protected:
	virtual void BeginPlay() override;

private:
	void BuildPitch();
	void BuildGoal(float Sign);
	UStaticMeshComponent* AddBox(const FVector& Center, const FVector& SizeCm, const FLinearColor& Color, bool bCollide, float Yaw = 0.f);
	UStaticMeshComponent* AddCylinder(const FVector& Center, float DiameterCm, float HeightCm, const FLinearColor& Color, bool bCollide);
	void AddArc(const FVector2D& Center, float Radius, float StartDeg, float EndDeg, int32 Segments);
	UStaticMeshComponent* AddMesh(UStaticMesh* Mesh, const FTransform& Transform, const FLinearColor& Color, bool bCollide);

	UPROPERTY(Transient)
	TObjectPtr<UStaticMesh> CubeMesh;

	UPROPERTY(Transient)
	TObjectPtr<UStaticMesh> CylinderMesh;

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInterface> BaseMaterial;

	int32 PartCounter = 0;
	bool bBuilt = false;
};
