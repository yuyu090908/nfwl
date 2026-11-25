using System;
using UnityEngine;

namespace NFWL
{
    // =====================================================================================
    // v0.7 animation architecture (PHASE 12)
    //
    // Gameplay and visuals are separate: PlayerController / PlayerLocomotion (FixedUpdate physics
    // and CharacterController movement) decide WHERE a player is and WHAT he does; PlayerModel
    // (LateUpdate, after movement) decides HOW the body looks. The visual side only reads
    // LocoAnimParams, the gait, kick / touch / tackle timelines, and never moves the player.
    //
    // Mapping of the requested components to the code:
    //   PlayerInputController      -> PlayerController (KeyInput + Core/InputBuffer)
    //   PlayerMotor                -> PlayerController.Move + CharacterController
    //   PlayerLocomotionController -> Player/PlayerLocomotion (inertia, turns, lean, balance = centre of mass,
    //                                 body lean, turn balance and motion inertia)
    //   MotionStateMachine         -> LocoState + KickPhase (+ tackle / save timelines)
    //   PlayerAnimationController  -> PlayerModel (procedural pose; IPoseDriver below)
    //   FootIKController           -> Animation/GaitController (plant detection, foot lock, stride prediction)
    //                                 + Animation/LegIK (two-bone IK, knee never bends backwards)
    //   BodyBalanceController      -> PlayerLocomotion balance + PlayerModel stumble / recovery
    //   BallTouchController        -> Player/DribbleController
    //   KickAnimationController    -> Player/KickController + Player/KickPlanner
    //   PlayerCollisionController  -> Player/PlayerContacts + Player/TackleController
    //   PlayerVisualController     -> Character/PlayerModel.Anatomy + CrystalArt
    //
    // Future motion matching / Humanoid clips: implement IPoseDriver on a component that drives an
    // Animator from MotionQuery (current parameters + predicted trajectory), raise MotionEvents
    // from animation events, and keep the gameplay side unchanged.
    // =====================================================================================

    /// <summary>Something that turns the gameplay state into a body pose each frame.</summary>
    public interface IPoseDriver
    {
        /// <summary>True when feet are placed by IK on the gait's planted positions (foot locking).</summary>
        bool SupportsFootIK { get; }
        /// <summary>The gait (planted / swinging feet) used for foot IK, contact shadows and tests.</summary>
        GaitController Gait { get; }
        /// <summary>The player's own running style.</summary>
        MotionProfile Profile { get; }
    }

    /// <summary>What a motion-matching database would be searched with: current parameters, feet, and the future path.</summary>
    public struct MotionQuery
    {
        public LocoAnimParams parameters;
        public Vector3 leftFoot, rightFoot;
        public float gaitPhase;
        public TrajectoryPoint p0, p1, p2; // 0.2 s, 0.4 s, 0.6 s ahead

        public static readonly float[] Times = { 0.2f, 0.4f, 0.6f };

        public static TrajectoryPoint Predict(Vector3 pos, Vector3 vel, Vector3 wantedVel, float facingYaw, float t, float responseTime = 0.45f)
            => MotionTrajectory.Predict(pos, vel, wantedVel, facingYaw, t, responseTime);

        public static MotionQuery Build(PlayerController pc, Vector3 wantedVel)
        {
            PlayerLocomotion lc = pc.Locomotion;
            Vector3 pos = pc.transform.position;
            var q = new MotionQuery { parameters = lc.AnimParams };
            if (pc.Model != null)
            {
                q.leftFoot = pc.Model.Gait.Left.position;
                q.rightFoot = pc.Model.Gait.Right.position;
                q.gaitPhase = pc.Model.Gait.Phase;
            }
            q.p0 = Predict(pos, lc.Velocity, wantedVel, lc.FacingYaw, Times[0]);
            q.p1 = Predict(pos, lc.Velocity, wantedVel, lc.FacingYaw, Times[1]);
            q.p2 = Predict(pos, lc.Velocity, wantedVel, lc.FacingYaw, Times[2]);
            return q;
        }
    }

    public enum FootContactKind { Touch, Kick, Tackle, Plant }

    /// <summary>
    /// Animation-event style notifications. Fired in the physics step where a foot actually meets the
    /// ball (the ball only moves in that step), so sound, effects or a future Animator stay in sync.
    /// </summary>
    public static class MotionEvents
    {
        public static event Action<PlayerController, FootContactKind, Vector3> FootContact;

        public static void Raise(PlayerController p, FootContactKind kind, Vector3 at) => FootContact?.Invoke(p, kind, at);
    }

    public partial class PlayerModel : IPoseDriver
    {
        public bool SupportsFootIK => true;
    }
}
