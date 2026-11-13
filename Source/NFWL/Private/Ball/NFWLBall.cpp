#include "Ball/NFWLBall.h"

#include "Components/SphereComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/CollisionProfile.h"
#include "Engine/StaticMesh.h"
#include "PhysicalMaterials/PhysicalMaterial.h"
#include "UObject/ConstructorHelpers.h"
#include "Core/NFWLTypes.h"
#include "Player/NFWLPlayerCharacter.h"

ANFWLBall::ANFWLBall()
{
	PrimaryActorTick.bCanEverTick = false;

	bReplicates = true;
	SetReplicateMovement(true);

	Sphere = CreateDefaultSubobject<USphereComponent>(TEXT("Sphere"));
	Sphere->InitSphereRadius(NFWLPitchDims::BallRadius);
	Sphere->SetCollisionProfileName(UCollisionProfile::PhysicsActor_ProfileName);
	Sphere->SetSimulatePhysics(true);
	Sphere->SetGenerateOverlapEvents(true);
	Sphere->BodyInstance.bUseCCD = true; // fast shots must not tunnel through posts or nets
	RootComponent = Sphere;

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(Sphere);
	Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	static ConstructorHelpers::FObjectFinder<UStaticMesh> SphereMesh(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	if (SphereMesh.Succeeded())
	{
		Mesh->SetStaticMesh(SphereMesh.Object);
		// Basic sphere is 100cm in diameter.
		Mesh->SetRelativeScale3D(FVector(NFWLPitchDims::BallRadius * 2.f / 100.f));
	}
}

void ANFWLBall::BeginPlay()
{
	Super::BeginPlay();

	Sphere->SetMassOverrideInKg(NAME_None, MassKg, true);
	Sphere->SetLinearDamping(LinearDamping);
	Sphere->SetAngularDamping(AngularDamping);

	UPhysicalMaterial* BallMaterial = NewObject<UPhysicalMaterial>(this, TEXT("BallPhysMat"));
	BallMaterial->Restitution = Restitution;
	BallMaterial->Friction = Friction;
	Sphere->SetPhysMaterialOverride(BallMaterial);
}

void ANFWLBall::Kick(const FVector& Velocity, ANFWLPlayerCharacter* Kicker)
{
	if (!HasAuthority())
	{
		return; // only the authority moves the ball
	}
	LastTouchedBy = Kicker;
	Sphere->SetPhysicsAngularVelocityInDegrees(FVector::ZeroVector);
	Sphere->SetPhysicsLinearVelocity(Velocity);
}

void ANFWLBall::ResetBall(const FVector& Location)
{
	Sphere->SetPhysicsLinearVelocity(FVector::ZeroVector);
	Sphere->SetPhysicsAngularVelocityInDegrees(FVector::ZeroVector);
	SetActorLocation(Location, false, nullptr, ETeleportType::TeleportPhysics);
	LastTouchedBy = nullptr;
}

FVector ANFWLBall::GetBallVelocity() const
{
	return Sphere->GetPhysicsLinearVelocity();
}

void ANFWLBall::SetBallVelocity(const FVector& Velocity)
{
	Sphere->SetPhysicsLinearVelocity(Velocity);
}
