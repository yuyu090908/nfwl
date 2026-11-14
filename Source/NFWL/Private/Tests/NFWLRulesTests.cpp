#include "Misc/AutomationTest.h"
#include "Core/NFWLTypes.h"

#if WITH_DEV_AUTOMATION_TESTS

// Run in the editor: Tools > Session Frontend > Automation, filter "NFWL".

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FNFWLAttrLerpTest, "NFWL.Rules.AttrLerp",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)

bool FNFWLAttrLerpTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("Speed 40 -> 6.0 m/s"), NFWLRules::AttrLerp(40, 600.f, 740.f), 600.f);
	TestEqual(TEXT("Speed 99 -> 7.4 m/s"), NFWLRules::AttrLerp(99, 600.f, 740.f), 740.f);
	TestEqual(TEXT("Below range clamps"), NFWLRules::AttrLerp(10, 600.f, 740.f), 600.f);
	TestEqual(TEXT("Above range clamps"), NFWLRules::AttrLerp(120, 600.f, 740.f), 740.f);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FNFWLChargeTest, "NFWL.Rules.Charge",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)

bool FNFWLChargeTest::RunTest(const FString& Parameters)
{
	TestTrue(TEXT("0.10s is a tap"), NFWLRules::IsTap(0.10f));
	TestFalse(TEXT("0.20s is a hold"), NFWLRules::IsTap(0.20f));
	TestEqual(TEXT("0.5s = 50%"), NFWLRules::HoldTimeToPower(0.5f), 0.5f);
	TestEqual(TEXT("2.0s caps at 100%"), NFWLRules::HoldTimeToPower(2.0f), 1.0f);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FNFWLGoalLineTest, "NFWL.Rules.GoalLine",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)

bool FNFWLGoalLineTest::RunTest(const FString& Parameters)
{
	using namespace NFWLPitchDims;
	const float R = BallRadius;

	TestTrue(TEXT("Centre spot is in play"), NFWLRules::ClassifyBall(FVector(0, 0, R)) == ENFWLBallState::InPlay);
	TestTrue(TEXT("Ball on the line is not a goal"), NFWLRules::ClassifyBall(FVector(HalfLength, 0, R)) == ENFWLBallState::InPlay);
	TestTrue(TEXT("Whole ball over the line at +X is a HOME goal"), NFWLRules::ClassifyBall(FVector(HalfLength + R + 1, 0, R)) == ENFWLBallState::GoalForHome);
	TestTrue(TEXT("Whole ball over the line at -X is an AWAY goal"), NFWLRules::ClassifyBall(FVector(-HalfLength - R - 1, 0, R)) == ENFWLBallState::GoalForAway);
	TestTrue(TEXT("Wide of the post is out"), NFWLRules::ClassifyBall(FVector(HalfLength + R + 1, GoalHalfWidth + 20, R)) == ENFWLBallState::Out);
	TestTrue(TEXT("Over the bar is out"), NFWLRules::ClassifyBall(FVector(HalfLength + R + 1, 0, GoalHeight + 50)) == ENFWLBallState::Out);
	TestTrue(TEXT("Over the touchline is out"), NFWLRules::ClassifyBall(FVector(0, HalfWidth + R + 1, R)) == ENFWLBallState::Out);
	return true;
}

#endif
