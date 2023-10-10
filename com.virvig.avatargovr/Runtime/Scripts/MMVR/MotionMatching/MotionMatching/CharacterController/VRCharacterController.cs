using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using MotionMatching;

using TrajectoryFeature = MotionMatching.MotionMatchingData.TrajectoryFeature;
using static AvatarGoVR.DeviceManager;

public class VRCharacterController : MotionMatchingCharacterController
{
    // General ----------------------------------------------------------
    [Header("General")]
    [Range(0.0f, 1.0f)] public float ResponsivenessPositions = 0.7f;
    [Range(0.0f, 1.0f)] public float ResponsivenessDirections = 0.7f;
    [Range(0.0f, 1.0f)] public float ThresholdNotifyVelocityChange = 0.2f;
    // Adjustment & Clamping --------------------------------------------
    [Header("Adjustment")] // Move Simulation Bone towards the Simulation Object (motion matching towards character controller)
    public bool DoAdjustment = true;
    [Range(0.0f, 2.0f)] public float PositionAdjustmentHalflife = 0.1f; // Time needed to move half of the distance between SimulationBone and SimulationObject
    [Range(0.0f, 2.0f)] public float RotationAdjustmentHalflife = 0.1f;
    [Range(0.0f, 2.0f)] public float PosMaximumAdjustmentRatio = 0.1f; // Ratio between the adjustment and the character's velocity to clamp the adjustment
    [Range(0.0f, 2.0f)] public float RotMaximumAdjustmentRatio = 0.1f; // Ratio between the adjustment and the character's velocity to clamp the adjustment
    public bool DoClamping = true;
    [Range(0.0f, 2.0f)] public float MaxDistanceSimulationBoneAndObject = 0.1f; // Max distance between SimulationBone and SimulationObject
    // --------------------------------------------------------------------------

    private Tracker HMDTracker;
    private float3 PositionHMD; // Position of the Simulation Object (controller) for HMD
    private quaternion RotationHMD; // Rotation of the Simulation Object (controller) for HMD
    private float PreviousHMDDesiredSpeedSq;


    // FUNCTIONS ---------------------------------------------------------------
    public void Init()
    {
        HMDTracker = new Tracker(this);

        PositionHMD = new float3();
        RotationHMD = new quaternion();

        Application.targetFrameRate = Mathf.RoundToInt(1.0f / DatabaseDeltaTime);
    }

    protected override void OnUpdate(float3 hmdPos, quaternion hmdRot)
    {
        Tracker tracker = HMDTracker;
        float3 currentPos = GetCurrentHMDPosition();
        quaternion currentRot = GetCurrentHMDRotation();

        // Input
        float3 desiredVelocity = tracker.GetSmoothedVelocity(hmdPos);
        float sqDesiredVelocity = math.lengthsq(desiredVelocity);
        if (sqDesiredVelocity - PreviousHMDDesiredSpeedSq > ThresholdNotifyVelocityChange * ThresholdNotifyVelocityChange)
        {
            NotifyInputChangedQuickly();
        }
        PreviousHMDDesiredSpeedSq = sqDesiredVelocity;
        tracker.DesiredRotation = hmdRot;
        quaternion desiredRotation = tracker.DesiredRotation;

        // Rotations
        tracker.PredictRotations(currentRot, desiredRotation, DatabaseDeltaTime);

        // Positions
        tracker.PredictPositions(currentPos, desiredVelocity, DatabaseDeltaTime);

        // Update Character Controller
        PositionHMD = hmdPos;
        RotationHMD = tracker.ComputeNewRot(currentRot, desiredRotation);

        // Adjust SimulationBone to pull the character (moving SimulationBone) towards the Simulation Object (character controller)
        if (DoAdjustment) AdjustSimulationBone(hmdPos, hmdRot);
        if (DoClamping) ClampSimulationBone(hmdPos);
    }

    private void AdjustSimulationBone(float3 hmdPos, quaternion hmdRot)
    {
        AdjustCharacterPosition(hmdPos);
        AdjustCharacterRotation(hmdRot);
    }

    private void ClampSimulationBone(float3 devicePos)
    {
        // Clamp Position
        float3 simulationObject = devicePos;
        simulationObject.y = 0.0f;
        float3 simulationBone = SimulationBone.GetSkeletonTransforms()[0].LocalPosition;
        simulationBone.y = 0.0f;
        if (math.distance(simulationObject, simulationBone) > MaxDistanceSimulationBoneAndObject)
        {
            float3 newSimulationBonePos = MaxDistanceSimulationBoneAndObject * math.normalize(simulationBone - simulationObject) + simulationObject;
            SimulationBone.SetPosAdjustment(newSimulationBonePos - simulationBone);
        }
    }

    private void AdjustCharacterPosition(float3 devicePos)
    {
        float3 simulationObject = devicePos;
        float3 simulationBone = SimulationBone.GetSkeletonTransforms()[0].LocalPosition;
        float3 differencePosition = simulationObject - simulationBone;
        differencePosition.y = 0; // No vertical Axis
        // Damp the difference using the adjustment halflife and dt
        float3 adjustmentPosition = Spring.DampAdjustmentImplicit(differencePosition, PositionAdjustmentHalflife, Time.deltaTime);
        // Clamp adjustment if the length is greater than the character velocity
        // multiplied by the ratio
        float maxLength = PosMaximumAdjustmentRatio * math.length(SimulationBone.Velocity) * Time.deltaTime;
        if (math.length(adjustmentPosition) > maxLength)
        {
            adjustmentPosition = maxLength * math.normalize(adjustmentPosition);
        }
        // Move the simulation bone towards the simulation object
        SimulationBone.SetPosAdjustment(adjustmentPosition);
    }

    private void AdjustCharacterRotation(quaternion deviceRot)
    {
        float3 simulationObject = math.mul(deviceRot, SimulationBone.MMData.GetLocalForward(0));
        float3 simulationBone = math.mul(SimulationBone.GetSkeletonTransforms()[0].LocalRotation, math.forward());
        // Only Y Axis rotation
        simulationObject.y = 0;
        simulationObject = math.normalize(simulationObject);
        simulationBone.y = 0;
        simulationBone = math.normalize(simulationBone);
        // Find the difference in rotation (from character to simulation object)
        quaternion differenceRotation = MathExtensions.FromToRotation(simulationBone, simulationObject, math.up());
        // Damp the difference using the adjustment halflife and dt
        quaternion adjustmentRotation = Spring.DampAdjustmentImplicit(differenceRotation, RotationAdjustmentHalflife, Time.deltaTime);
        // Clamp adjustment if the length is greater than the character angular velocity
        // multiplied by the ratio
        float maxLength = RotMaximumAdjustmentRatio * math.length(SimulationBone.AngularVelocity) * Time.deltaTime;
        if (math.length(MathExtensions.QuaternionToScaledAngleAxis(adjustmentRotation)) > maxLength)
        {
            adjustmentRotation = MathExtensions.QuaternionFromScaledAngleAxis(maxLength * math.normalize(MathExtensions.QuaternionToScaledAngleAxis(adjustmentRotation)));
        }
        // Rotate the simulation bone towards the simulation object
        SimulationBone.SetRotAdjustment(adjustmentRotation);
    }

    private float3 GetCurrentHMDPosition()
    {
        return PositionHMD;
    }
    private quaternion GetCurrentHMDRotation()
    {
        return RotationHMD;
    }

    private float3 GetWorldSpacePosition(int predictionIndex)
    {
        Tracker tracker = HMDTracker;
        return tracker.PredictedPosition[predictionIndex];
    }

    private float3 GetWorldSpaceDirectionPrediction(int index)
    {
        Tracker tracker = HMDTracker;
        float3 dir = math.mul(tracker.PredictedRotations[index], math.forward());
        return math.normalize(dir);
    }

    public override float3 GetWorldInitPosition()
    {
        return float3.zero;
    }
    public override float3 GetWorldInitDirection()
    {
        return math.forward();
    }

    public override float3 GetWorldSpacePrediction(MotionMatchingData.TrajectoryFeature feature, int predictionIndex)
    {
        Debug.Assert(feature.Project == true, "Project must be true");
        switch (feature.Bone)
        {
            case HumanBodyBones.Head:
                break;
            case HumanBodyBones.LeftHand:
                Debug.Assert(false, "LeftHand is not supported");
                break;
            case HumanBodyBones.RightHand:
                Debug.Assert(false, "RightHand is not supported");
                break;
            default:
                Debug.Assert(false, "Unknown Bone: " + feature.Bone);
                break;
        }
        switch (feature.FeatureType)
        {
            case TrajectoryFeature.Type.Position:
                float3 pos = GetWorldSpacePosition(predictionIndex);
                pos.y = 0.0f; // Project to the ground
                return pos;
            case TrajectoryFeature.Type.Direction:
                float3 dir = GetWorldSpaceDirectionPrediction(predictionIndex);
                dir.y = 0.0f; // Project to the ground
                dir = math.normalize(dir);
                return dir;
            default:
                Debug.Assert(false, "Unknown feature type: " + feature.FeatureType);
                break;
        }
        return float3.zero;
    }

    private class Tracker
    {
        public VRCharacterController Controller;
        // Rotation and Predicted Rotation ------------------------------------------
        public quaternion DesiredRotation;
        public quaternion[] PredictedRotations;
        public float3 AngularVelocity;
        public float3[] PredictedAngularVelocities;
        // Position and Predicted Position ------------------------------------------
        public float3[] PredictedPosition;
        public float3 Velocity;
        public float3 Acceleration;
        public float3[] PredictedVelocity;
        public float3[] PredictedAcceleration;
        // Features -----------------------------------------------------------------
        public int[] TrajectoryPosPredictionFrames;
        public int[] TrajectoryRotPredictionFrames;
        public int NumberPredictionPos { get { return TrajectoryPosPredictionFrames.Length; } }
        public int NumberPredictionRot { get { return TrajectoryRotPredictionFrames.Length; } }
        // Previous -----------------------------------------------------------------
        public float3 PrevInputPos;
        public quaternion PrevInputRot;
        public float3[] PreviousVelocities;
        public int PreviousVelocitiesIndex;
        public float3[] PreviousAngularVelocities;
        public int PreviousAngularVelocitiesIndex;
        public int NumberPastFrames = 1;
        // --------------------------------------------------------------------------
        
        public Tracker(VRCharacterController controller)
        {
            Controller = controller;
            PrevInputPos = float3.zero;
            PreviousVelocities = new float3[NumberPastFrames]; // HARDCODED
            PreviousAngularVelocities = new float3[NumberPastFrames]; // HARDCODED

            TrajectoryPosPredictionFrames = new int[] { 20, 40, 60 }; // HARDCODED
            TrajectoryRotPredictionFrames = new int[] { 20, 40, 60 }; // HARDCODED
                                                                      // TODO: generalize this... allow different number of prediction frames for different features
            Debug.Assert(TrajectoryPosPredictionFrames.Length == TrajectoryRotPredictionFrames.Length, "Trajectory Position and Trajectory Direction Prediction Frames must be the same for SpringCharacterController");
            for (int i = 0; i < TrajectoryPosPredictionFrames.Length; ++i)
            {
                Debug.Assert(TrajectoryPosPredictionFrames[i] == TrajectoryRotPredictionFrames[i], "Trajectory Position and Trajectory Direction Prediction Frames must be the same for SpringCharacterController");
            }
            //if (Controller.AverageFPS != TrajectoryPosPredictionFrames[TrajectoryPosPredictionFrames.Length - 1]) Debug.LogWarning("AverageFPS is not the same as the last Prediction Frame... maybe you forgot changing the hardcoded value?");
            //if (Controller.AverageFPS != TrajectoryRotPredictionFrames[TrajectoryRotPredictionFrames.Length - 1]) Debug.LogWarning("AverageFPS is not the same as the last Prediction Frame... maybe you forgot changing the hardcoded value?");


            PredictedPosition = new float3[NumberPredictionPos];
            PredictedVelocity = new float3[NumberPredictionPos];
            PredictedAcceleration = new float3[NumberPredictionPos];
            PredictedRotations = new quaternion[NumberPredictionRot];
            PredictedAngularVelocities = new float3[NumberPredictionRot];
        }

        public void PredictRotations(quaternion currentRotation, quaternion desiredRotation, float averagedDeltaTime)
        {
            for (int i = 0; i < NumberPredictionRot; i++)
            {
                // Init Predicted values
                PredictedRotations[i] = currentRotation;
                PredictedAngularVelocities[i] = AngularVelocity;
                // Predict
                Spring.SimpleSpringDamperImplicit(ref PredictedRotations[i], ref PredictedAngularVelocities[i],
                                                  desiredRotation, 1.0f - Controller.ResponsivenessDirections, TrajectoryRotPredictionFrames[i] * averagedDeltaTime);
            }
        }

        /* https://theorangeduck.com/page/spring-roll-call#controllers */
        public void PredictPositions(float3 currentPos, float3 desiredVelocity, float averagedDeltaTime)
        {
            int lastPredictionFrames = 0;
            for (int i = 0; i < NumberPredictionPos; ++i)
            {
                if (i == 0)
                {
                    PredictedPosition[i] = currentPos;
                    PredictedVelocity[i] = Velocity;
                    PredictedAcceleration[i] = Acceleration;
                }
                else
                {
                    PredictedPosition[i] = PredictedPosition[i - 1];
                    PredictedVelocity[i] = PredictedVelocity[i - 1];
                    PredictedAcceleration[i] = PredictedAcceleration[i - 1];
                }
                int diffPredictionFrames = TrajectoryPosPredictionFrames[i] - lastPredictionFrames;
                lastPredictionFrames = TrajectoryPosPredictionFrames[i];
                Spring.CharacterPositionUpdate(ref PredictedPosition[i], ref PredictedVelocity[i], ref PredictedAcceleration[i],
                                               desiredVelocity, 1.0f - Controller.ResponsivenessPositions, diffPredictionFrames * averagedDeltaTime);
            }
        }

        public quaternion ComputeNewRot(quaternion currentRotation, quaternion desiredRotation)
        {
            quaternion newRotation = currentRotation;
            Spring.SimpleSpringDamperImplicit(ref newRotation, ref AngularVelocity, desiredRotation, 1.0f - Controller.ResponsivenessDirections, Time.deltaTime);
            return newRotation;
        }

        public float3 GetSmoothedVelocity(float3 devicePos)
        {
            float dt = Controller.DatabaseDeltaTime;
            float3 currentInputPos = devicePos;
            float3 currentSpeed = (currentInputPos - PrevInputPos) / dt; // pretend it's fixed frame rate
            PrevInputPos = currentInputPos;

            PreviousVelocities[PreviousVelocitiesIndex] = currentSpeed;
            PreviousVelocitiesIndex = (PreviousVelocitiesIndex + 1) % PreviousVelocities.Length;

            float3 sum = float3.zero;
            for (int i = 0; i < PreviousVelocities.Length; ++i)
            {
                sum += PreviousVelocities[i];
            }
            currentSpeed = sum / NumberPastFrames;
            return currentSpeed;
        }
    }
}

