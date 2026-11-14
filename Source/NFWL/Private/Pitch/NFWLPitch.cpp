#include "Pitch/NFWLPitch.h"

#include "Core/NFWLTypes.h"
#include "Components/SceneComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/CollisionProfile.h"
#include "Engine/StaticMesh.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Materials/MaterialInterface.h"
#include "UObject/ConstructorHelpers.h"

namespace
{
	const FLinearColor GrassDark(0.03f, 0.22f, 0.04f);
	const FLinearColor GrassLight(0.05f, 0.30f, 0.06f);
	const FLinearColor Surround(0.02f, 0.12f, 0.03f);
	const FLinearColor LineWhite(0.9f, 0.9f, 0.9f);
	const FLinearColor NetGrey(0.6f, 0.6f, 0.6f);
	constexpr float LineZ = 0.6f;
}

ANFWLPitch::ANFWLPitch()
{
	PrimaryActorTick.bCanEverTick = false;
	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));

	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cube(TEXT("/Engine/BasicShapes/Cube.Cube"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cylinder(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	static ConstructorHelpers::FObjectFinder<UMaterialInterface> Mat(TEXT("/Engine/BasicShapes/BasicShapeMaterial.BasicShapeMaterial"));
	CubeMesh = Cube.Object;
	CylinderMesh = Cylinder.Object;
	BaseMaterial = Mat.Object;
}

void ANFWLPitch::BeginPlay()
{
	Super::BeginPlay();
	BuildPitch();
}

UStaticMeshComponent* ANFWLPitch::AddMesh(UStaticMesh* Mesh, const FTransform& Transform, const FLinearColor& Color, bool bCollide)
{
	UStaticMeshComponent* Comp = NewObject<UStaticMeshComponent>(this, *FString::Printf(TEXT("Part_%d"), PartCounter++));
	Comp->SetStaticMesh(Mesh);
	Comp->SetupAttachment(RootComponent);
	Comp->SetRelativeTransform(Transform);
	Comp->SetMobility(EComponentMobility::Movable); // created at runtime, no baked lighting
	if (bCollide)
	{
		Comp->SetCollisionProfileName(UCollisionProfile::BlockAll_ProfileName);
	}
	else
	{
		Comp->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	}
	Comp->RegisterComponent();

	if (BaseMaterial)
	{
		UMaterialInstanceDynamic* Mid = UMaterialInstanceDynamic::Create(BaseMaterial, this);
		Mid->SetVectorParameterValue(TEXT("Color"), Color);
		Comp->SetMaterial(0, Mid);
	}
	return Comp;
}

UStaticMeshComponent* ANFWLPitch::AddBox(const FVector& Center, const FVector& SizeCm, const FLinearColor& Color, bool bCollide, float Yaw)
{
	// The basic cube is 100cm and centred on its pivot.
	const FTransform T(FRotator(0.f, Yaw, 0.f), Center, SizeCm / 100.f);
	return AddMesh(CubeMesh, T, Color, bCollide);
}

UStaticMeshComponent* ANFWLPitch::AddCylinder(const FVector& Center, float DiameterCm, float HeightCm, const FLinearColor& Color, bool bCollide)
{
	const FTransform T(FRotator::ZeroRotator, Center, FVector(DiameterCm / 100.f, DiameterCm / 100.f, HeightCm / 100.f));
	return AddMesh(CylinderMesh, T, Color, bCollide);
}

void ANFWLPitch::AddArc(const FVector2D& Center, float Radius, float StartDeg, float EndDeg, int32 Segments)
{
	const float Step = (EndDeg - StartDeg) / Segments;
	const float SegLen = 2.f * Radius * FMath::Sin(FMath::DegreesToRadians(FMath::Abs(Step)) * 0.5f) + 2.f;
	for (int32 i = 0; i < Segments; ++i)
	{
		const float MidDeg = StartDeg + Step * (i + 0.5f);
		const float Rad = FMath::DegreesToRadians(MidDeg);
		const FVector Pos(Center.X + Radius * FMath::Cos(Rad), Center.Y + Radius * FMath::Sin(Rad), LineZ);
		// Segment runs along the tangent (perpendicular to the radius).
		AddBox(Pos, FVector(SegLen, NFWLPitchDims::LineWidth, 1.f), LineWhite, false, MidDeg + 90.f);
	}
}

void ANFWLPitch::BuildPitch()
{
	if (bBuilt || !CubeMesh || !CylinderMesh)
	{
		return;
	}
	bBuilt = true;

	using namespace NFWLPitchDims;
	const float L = HalfLength * 2.f;
	const float W = HalfWidth * 2.f;
	const float LW = LineWidth;

	// Ground: surround (collides) with mown stripes on top (visual only). Top surface is at Z=0.
	AddBox(FVector(0.f, 0.f, -10.f), FVector(L + 1200.f, W + 1200.f, 20.f), Surround, true);
	constexpr int32 Stripes = 8;
	for (int32 i = 0; i < Stripes; ++i)
	{
		const float StripeLen = L / Stripes;
		const float X = -HalfLength + StripeLen * (i + 0.5f);
		AddBox(FVector(X, 0.f, 0.1f), FVector(StripeLen, W, 0.2f), (i % 2 == 0) ? GrassDark : GrassLight, false);
	}

	// Lines.
	AddBox(FVector(0.f,  HalfWidth, LineZ), FVector(L + LW, LW, 1.f), LineWhite, false); // touchlines
	AddBox(FVector(0.f, -HalfWidth, LineZ), FVector(L + LW, LW, 1.f), LineWhite, false);
	AddBox(FVector( HalfLength, 0.f, LineZ), FVector(LW, W + LW, 1.f), LineWhite, false); // goal lines
	AddBox(FVector(-HalfLength, 0.f, LineZ), FVector(LW, W + LW, 1.f), LineWhite, false);
	AddBox(FVector(0.f, 0.f, LineZ), FVector(LW, W, 1.f), LineWhite, false);              // halfway line

	AddArc(FVector2D(0.f, 0.f), CenterRadius, 0.f, 360.f, 48);              // centre circle
	AddArc(FVector2D( HalfLength, 0.f), PenaltyRadius, 90.f, 270.f, 40);   // penalty areas (semicircles)
	AddArc(FVector2D(-HalfLength, 0.f), PenaltyRadius, -90.f, 90.f, 40);

	AddCylinder(FVector(0.f, 0.f, LineZ), 20.f, 1.f, LineWhite, false);                                 // centre spot
	AddCylinder(FVector( HalfLength - PenaltySpotDist, 0.f, LineZ), 20.f, 1.f, LineWhite, false);      // penalty spots
	AddCylinder(FVector(-HalfLength + PenaltySpotDist, 0.f, LineZ), 20.f, 1.f, LineWhite, false);

	BuildGoal(1.f);
	BuildGoal(-1.f);
}

void ANFWLPitch::BuildGoal(float Sign)
{
	using namespace NFWLPitchDims;
	constexpr float Post = 10.f;
	const float LineX = Sign * HalfLength;
	const float BackX = Sign * (HalfLength + GoalDepth);
	const float MidX = Sign * (HalfLength + GoalDepth * 0.5f);

	// Posts sit just outside the goal mouth so the inner width stays 3m.
	AddCylinder(FVector(LineX + Sign * Post * 0.5f,  GoalHalfWidth + Post * 0.5f, GoalHeight * 0.5f), Post, GoalHeight, LineWhite, true);
	AddCylinder(FVector(LineX + Sign * Post * 0.5f, -GoalHalfWidth - Post * 0.5f, GoalHeight * 0.5f), Post, GoalHeight, LineWhite, true);
	AddBox(FVector(LineX + Sign * Post * 0.5f, 0.f, GoalHeight + Post * 0.5f), FVector(Post, GoalHalfWidth * 2.f + Post * 2.f, Post), LineWhite, true); // crossbar

	// Net: back, sides and roof. Thin boxes that stop the ball.
	constexpr float Net = 3.f;
	AddBox(FVector(BackX, 0.f, GoalHeight * 0.5f), FVector(Net, GoalHalfWidth * 2.f, GoalHeight), NetGrey, true);
	AddBox(FVector(MidX,  GoalHalfWidth + Net, GoalHeight * 0.5f), FVector(GoalDepth, Net, GoalHeight), NetGrey, true);
	AddBox(FVector(MidX, -GoalHalfWidth - Net, GoalHeight * 0.5f), FVector(GoalDepth, Net, GoalHeight), NetGrey, true);
	AddBox(FVector(MidX, 0.f, GoalHeight + Net), FVector(GoalDepth, GoalHalfWidth * 2.f, Net), NetGrey, true);
}
