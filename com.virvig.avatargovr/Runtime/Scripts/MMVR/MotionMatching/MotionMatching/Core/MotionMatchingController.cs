using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using AvatarGoVR;

namespace MotionMatching
{
    using TrajectoryFeature = MotionMatchingData.TrajectoryFeature;

    // Simulation bone is the transform
    public class MotionMatchingController
    {
        public event Action OnSkeletonTransformUpdated;

        public float EyesHeight;
        public MotionMatchingCharacterController CharacterController;
        public MotionMatchingData MMData;
        public SquatDataset[] SquatDatasets;
        public bool LockFPS = true;
        public float SearchTime = 10.0f / 72.0f; // Motion Matching search every SearchTime seconds
        public bool UseBVHSearch = true; // Use Bounding Volume Hierarchy acceleration structure for the search.
        public bool Inertialize = true; // Should inertialize transitions after a big change of the pose
        public bool FootLock = true; // Should lock the feet to the ground when contact information is true
        public float FootUnlockDistance = 0.2f; // Distance from actual pose to IK target to unlock the feet
        [Range(0.0f, 1.0f)] public float InertializeHalfLife = 0.1f; // Time needed to move half of the distance between the source to the target pose
        [Tooltip("How important is the trajectory (future positions + future directions)")][Range(0.0f, 1.0f)] public float Responsiveness = 1.0f;
        [Tooltip("How important is the current pose")][Range(0.0f, 1.0f)] public float Quality = 1.0f;
        public float[] FeatureWeights;

        public float CurrentHeadHeight;


        public float3 Velocity { get; private set; }
        public float3 AngularVelocity { get; private set; }
        public float DatabaseFrameTime { get; private set; }
        public int DatabaseFrameRate { get; private set; }

        private PoseSet PoseSet;
        private FeatureSet FeatureSet;
        private PoseSet[] SquatPoseSets;
        private FeatureSet[] SquatFeatureSets;


        private Transform[] SkeletonTransforms;
        private float3 AnimationSpaceOriginPos;
        private quaternion InverseAnimationSpaceOriginRot;
        private float3 MMTransformOriginPose; // Position of the transform right after motion matching search
        private quaternion MMTransformOriginRot; // Rotation of the transform right after motion matching search
        private int LastMMSearchFrame; // Frame before the last Motion Matching Search
        public int CurrentFrame { get; private set; } // Current frame index in the pose/feature set
        private float CurrentFrameTime; // Current frame index as float to keep track of variable frame rate
        private float SearchTimeLeft;
        private NativeArray<float> QueryFeature;
        private NativeArray<int> SearchResult;
        private NativeArray<float> FeaturesWeightsNativeArray;
        private Inertialization Inertialization;
        // BVH Acceleration Structure
        public NativeArray<float>[] LargeBoundingBoxMin;
        public NativeArray<float>[] LargeBoundingBoxMax;
        public NativeArray<float>[] SmallBoundingBoxMin;
        public NativeArray<float>[] SmallBoundingBoxMax;
        // Foot Lock
        private bool IsLeftFootContact, IsRightFootContact;
        private float3 LeftToesContactTarget, RightToesContactTarget; // Target position of the toes
        private float3 LeftFootContact, RightFootContact; // Position of the foot
        private float3 LeftFootPoleContact, RightFootPoleContact; // Forward vector of the knee
        private float3 LeftLowerLegLocalForward, RightLowerLegLocalForward;
        private int LeftToesIndex, LeftFootIndex, LeftLowerLegIndex, LeftUpperLegIndex;
        private int RightToesIndex, RightFootIndex, RightLowerLegIndex, RightUpperLegIndex;
        // Squat
        private int SquatIndex; // 0 - No Squat, 1 - First Squat Dataset, 2 - Second...


        public void Init()
        {
            // PoseSet
            PoseSet = MMData.GetOrImportPoseSet();
            SquatPoseSets = new PoseSet[SquatDatasets.Length];
            for (int i = 0; i < SquatDatasets.Length; i++)
            {
                SquatPoseSets[i] = SquatDatasets[i].MMData.GetOrImportPoseSet();
            }

            // FeatureSet
            FeatureSet = MMData.GetOrImportFeatureSet();
            SquatFeatureSets = new FeatureSet[SquatDatasets.Length];
            for (int i = 0; i < SquatDatasets.Length; i++)
            {
                SquatFeatureSets[i] = SquatDatasets[i].MMData.GetOrImportFeatureSet();
            }

            // Skeleton
            SkeletonTransforms = new Transform[PoseSet.Skeleton.Joints.Count];
            SkeletonTransforms[0] = new Transform(float3.zero, quaternion.identity, 0, "SimulationBone", 0); // Simulation Bone
            for (int j = 1; j < PoseSet.Skeleton.Joints.Count; j++)
            {
                // Joints
                Skeleton.Joint joint = PoseSet.Skeleton.Joints[j];
                SkeletonTransforms[j] = new Transform(joint.LocalOffset, quaternion.identity, joint.ParentIndex, joint.Name, j);
            }

            // Inertialization
            Inertialization = new Inertialization(PoseSet.Skeleton);

            // FPS
            DatabaseFrameTime = PoseSet.FrameTime;
            DatabaseFrameRate = Mathf.RoundToInt(1.0f / DatabaseFrameTime);
            if (LockFPS)
            {
                Application.targetFrameRate = DatabaseFrameRate;
                Debug.Log("[Motion Matching] Updated Target FPS: " + Application.targetFrameRate);
            }
            else
            {
                Application.targetFrameRate = -1;
                Debug.LogWarning("[Motion Matching] LockFPS is not set. Motion Matching will malfunction if the application frame rate is higher than the animation database.");
            }

            // Other initialization
            SearchResult = new NativeArray<int>(1, Allocator.Persistent);
            int numberFeatures = (MMData.TrajectoryFeatures.Count + MMData.PoseFeatures.Count);
            if (FeatureWeights == null || FeatureWeights.Length != numberFeatures)
            {
                float[] newWeights = new float[numberFeatures];
                for (int i = 0; i < newWeights.Length; ++i) newWeights[i] = 1.0f;
                for (int i = 0; i < Mathf.Min(FeatureWeights == null ? 0 : FeatureWeights.Length, newWeights.Length); i++) newWeights[i] = FeatureWeights[i];
                FeatureWeights = newWeights;
            }
            FeaturesWeightsNativeArray = new NativeArray<float>(FeatureSet.FeatureSize, Allocator.Persistent);
            QueryFeature = new NativeArray<float>(FeatureSet.FeatureSize, Allocator.Persistent);
            // Build BVH Acceleration Structure
            LargeBoundingBoxMin = new NativeArray<float>[1 + SquatFeatureSets.Length];
            LargeBoundingBoxMax = new NativeArray<float>[1 + SquatFeatureSets.Length];
            SmallBoundingBoxMin = new NativeArray<float>[1 + SquatFeatureSets.Length];
            SmallBoundingBoxMax = new NativeArray<float>[1 + SquatFeatureSets.Length];
            BuildBVHAccelerationStructure(0, FeatureSet);
            for (int i = 1; i < SquatFeatureSets.Length + 1; ++i)
            {
                BuildBVHAccelerationStructure(i, SquatFeatureSets[i - 1]);
            }
            // Search first Frame valid (to start with a valid pose)
            for (int i = 0; i < FeatureSet.NumberFeatureVectors; i++)
            {
                if (FeatureSet.IsValidFeature(i))
                {
                    LastMMSearchFrame = i;
                    CurrentFrame = i;
                    break;
                }
            }
            // Foot Lock
            if (!PoseSet.Skeleton.Find(HumanBodyBones.LeftToes, out Skeleton.Joint leftToesJoint)) Debug.LogError("[Motion Matching] LeftToes not found");
            LeftToesIndex = leftToesJoint.Index;
            if (!PoseSet.Skeleton.Find(HumanBodyBones.LeftFoot, out Skeleton.Joint leftFootJoint)) Debug.LogError("[Motion Matching] LeftFoot not found");
            LeftFootIndex = leftFootJoint.Index;
            if (!PoseSet.Skeleton.Find(HumanBodyBones.LeftLowerLeg, out Skeleton.Joint leftLowerLegJoint)) Debug.LogError("[Motion Matching] LeftLowerLeg not found");
            LeftLowerLegIndex = leftLowerLegJoint.Index;
            if (!PoseSet.Skeleton.Find(HumanBodyBones.LeftUpperLeg, out Skeleton.Joint leftUpperLegJoint)) Debug.LogError("[Motion Matching] LeftUpperLeg not found");
            LeftUpperLegIndex = leftUpperLegJoint.Index;
            if (!PoseSet.Skeleton.Find(HumanBodyBones.RightToes, out Skeleton.Joint rightToesJoint)) Debug.LogError("[Motion Matching] RightToes not found");
            RightToesIndex = rightToesJoint.Index;
            if (!PoseSet.Skeleton.Find(HumanBodyBones.RightFoot, out Skeleton.Joint rightFootJoint)) Debug.LogError("[Motion Matching] RightFoot not found");
            RightFootIndex = rightFootJoint.Index;
            if (!PoseSet.Skeleton.Find(HumanBodyBones.RightLowerLeg, out Skeleton.Joint rightLowerLegJoint)) Debug.LogError("[Motion Matching] RightLowerLeg not found");
            RightLowerLegIndex = rightLowerLegJoint.Index;
            if (!PoseSet.Skeleton.Find(HumanBodyBones.RightUpperLeg, out Skeleton.Joint rightUpperLegJoint)) Debug.LogError("[Motion Matching] RightUpperLeg not found");
            RightUpperLegIndex = rightUpperLegJoint.Index;
            LeftLowerLegLocalForward = MMData.GetLocalForward(LeftLowerLegIndex);
            RightLowerLegLocalForward = MMData.GetLocalForward(RightLowerLegIndex);
        }

        private void BuildBVHAccelerationStructure(int i, FeatureSet featureSet)
        {
            int nFrames = (featureSet.GetFeatures().Length / featureSet.FeatureSize);
            int numberBoundingBoxLarge = (nFrames + BVHConsts.LargeBVHSize - 1) / BVHConsts.LargeBVHSize;
            int numberBoundingBoxSmall = (nFrames + BVHConsts.SmallBVHSize - 1) / BVHConsts.SmallBVHSize;
            LargeBoundingBoxMin[i] = new NativeArray<float>(numberBoundingBoxLarge * featureSet.FeatureSize, Allocator.Persistent);
            LargeBoundingBoxMax[i] = new NativeArray<float>(numberBoundingBoxLarge * featureSet.FeatureSize, Allocator.Persistent);
            SmallBoundingBoxMin[i] = new NativeArray<float>(numberBoundingBoxSmall * featureSet.FeatureSize, Allocator.Persistent);
            SmallBoundingBoxMax[i] = new NativeArray<float>(numberBoundingBoxSmall * featureSet.FeatureSize, Allocator.Persistent);
            var job = new BVHMotionMatchingComputeBounds
            {
                Features = featureSet.GetFeatures(),
                FeatureSize = featureSet.FeatureSize,
                NumberBoundingBoxLarge = numberBoundingBoxLarge,
                NumberBoundingBoxSmall = numberBoundingBoxSmall,
                LargeBoundingBoxMin = LargeBoundingBoxMin[i],
                LargeBoundingBoxMax = LargeBoundingBoxMax[i],
                SmallBoundingBoxMin = SmallBoundingBoxMin[i],
                SmallBoundingBoxMax = SmallBoundingBoxMax[i],
            };
            job.Schedule().Complete();
        }

        public void Enable()
        {
            SearchTimeLeft = 0;
            CharacterController.OnUpdated += OnCharacterControllerUpdated;
            CharacterController.OnInputChangedQuickly += OnInputChangedQuickly;
            // Init Pose
            SkeletonTransforms[0].LocalPosition = CharacterController.GetWorldInitPosition();
            SkeletonTransforms[0].LocalRotation = quaternion.LookRotation(CharacterController.GetWorldInitDirection(), Vector3.up);
        }

        public void Disable()
        {
            CharacterController.OnUpdated -= OnCharacterControllerUpdated;
            CharacterController.OnInputChangedQuickly -= OnInputChangedQuickly;
        }

        private void OnCharacterControllerUpdated(float deltaTime)
        {
            PROFILE.BEGIN_SAMPLE_PROFILING("Motion Matching Total");
            if (SearchTimeLeft <= 0)
            {
                // Determine squat level
                int prevSquatIndex = SquatIndex;
                //AvatarManager.Instance.GetDeviceManager().GetDevicePose(DeviceManager.DeviceRole.Head, out Vector3 hmdPos, out _);
                float heightPercentage = CurrentHeadHeight / EyesHeight;
                int newSquatIndex = 0;
                for (int i = 0; i < SquatDatasets.Length; i++)
                {
                    float p = SquatDatasets[i].PercentageHeight;
                    if ((p < 1.0f && heightPercentage < p) || (p > 1.0f && heightPercentage > p))
                    {
                        newSquatIndex = i + 1; // 0 is the default
                    }
                }
                SquatIndex = newSquatIndex;

                // Motion Matching
                PROFILE.BEGIN_SAMPLE_PROFILING("Motion Matching Search");
                int bestFrame = SearchMotionMatching(prevSquatIndex);
                PROFILE.END_SAMPLE_PROFILING("Motion Matching Search");
                if (bestFrame != CurrentFrame || prevSquatIndex != SquatIndex)
                {
                    PoseSet sourcePoseSet = PoseSet;
                    if (prevSquatIndex > 0) sourcePoseSet = SquatPoseSets[prevSquatIndex - 1];
                    PoseSet targetPoseSet = PoseSet;
                    if (SquatIndex > 0) targetPoseSet = SquatPoseSets[SquatIndex - 1];
                    // Inertialize
                    if (Inertialize)
                    {
                        Inertialization.PoseTransition(sourcePoseSet, targetPoseSet, CurrentFrame, bestFrame);
                    }
                    LastMMSearchFrame = CurrentFrame;
                    CurrentFrameTime = bestFrame + math.frac(CurrentFrameTime); // the fractional part is the error accumulated, add it to the current to avoid drifting
                    CurrentFrame = bestFrame;
                    // Update Current Animation Space Origin
                    targetPoseSet.GetPose(CurrentFrame, out PoseVector mmPose);
                    AnimationSpaceOriginPos = mmPose.JointLocalPositions[0];
                    InverseAnimationSpaceOriginRot = math.inverse(mmPose.JointLocalRotations[0]);
                    MMTransformOriginPose = SkeletonTransforms[0].LocalPosition;
                    MMTransformOriginRot = SkeletonTransforms[0].LocalRotation;
                }
                SearchTimeLeft = SearchTime;
            }
            else
            {
                // Advance
                SearchTimeLeft -= deltaTime;
            }
            // Always advance one (bestFrame from motion matching is the best match to the current frame, but we want to move to the next frame)
            // Ideally the applications runs at 1.0f/FrameTime fps (to match the database) however, as this may not happen, we may need to skip some frames
            // from the database, e.g., if 1.0f/FrameTime = 60 and our game runes at 30, we need to advance 2 frames at each update
            // However, as we are using Application.targetFrameRate=1.0f/FrameTime, we do not consider the case where the application runs faster than the database
            CurrentFrameTime += DatabaseFrameRate * deltaTime; // DatabaseFrameRate / (1.0f / deltaTime)
            CurrentFrame = (int)math.floor(CurrentFrameTime);

            UpdateTransformAndSkeleton(CurrentFrame);
            PROFILE.END_SAMPLE_PROFILING("Motion Matching Total");
        }

        private void OnInputChangedQuickly()
        {
            SearchTimeLeft = 0; // Force search
        }
        
        private int SearchMotionMatching(int previousSquatIndex)
        {
            // Weights
            UpdateAndGetFeatureWeights();

            // Previous FeatureSet
            FeatureSet previousFeatureSet = FeatureSet;
            if (previousSquatIndex > 0) previousFeatureSet = SquatFeatureSets[previousSquatIndex - 1];
            // Current FeatureSet
            FeatureSet currentFeatureSet = FeatureSet;
            int currentFeatureSetIndex = 0;
            if (SquatIndex > 0)
            {
                currentFeatureSet = SquatFeatureSets[SquatIndex - 1];
                currentFeatureSetIndex = SquatIndex; // SquatIndex - 1 + 1
            }

            // Init Query Vector (with previous information)
            previousFeatureSet.GetFeature(QueryFeature, CurrentFrame);
            previousFeatureSet.DenormalizeFeatureVector(QueryFeature);
            // Now Current Feature Set
            currentFeatureSet.NormalizeFeatureVector(QueryFeature);
            FillTrajectory(QueryFeature, currentFeatureSet);

            // Get next feature vector (when doing motion matching search, they need less error than this)
            float currentDistance = float.MaxValue;
            bool currentValid = false;
            if (previousFeatureSet.IsValidFeature(CurrentFrame) && SquatIndex == previousSquatIndex)
            {
                currentValid = true;
                currentDistance = 0.0f;
                // the pose is the same... the distance is only the trajectory
                for (int j = 0; j < previousFeatureSet.PoseOffset; j++)
                {
                    float diff = previousFeatureSet.GetFeatures()[CurrentFrame * previousFeatureSet.FeatureSize + j] - QueryFeature[j];
                    currentDistance += diff * diff * FeaturesWeightsNativeArray[j];
                }
            }

            // Search
            if (UseBVHSearch)
            {
                var job = new BVHMotionMatchingSearchBurst
                {
                    Valid = currentFeatureSet.GetValid(),
                    Features = currentFeatureSet.GetFeatures(),
                    QueryFeature = QueryFeature,
                    FeatureWeights = FeaturesWeightsNativeArray,
                    FeatureSize = currentFeatureSet.FeatureSize,
                    PoseOffset = currentFeatureSet.PoseOffset,
                    CurrentDistance = currentDistance,
                    LargeBoundingBoxMin = LargeBoundingBoxMin[currentFeatureSetIndex],
                    LargeBoundingBoxMax = LargeBoundingBoxMax[currentFeatureSetIndex],
                    SmallBoundingBoxMin = SmallBoundingBoxMin[currentFeatureSetIndex],
                    SmallBoundingBoxMax = SmallBoundingBoxMax[currentFeatureSetIndex],
                    BestIndex = SearchResult
                };
                job.Schedule().Complete();
            }
            else
            {
                var job = new LinearMotionMatchingSearchBurst
                {
                    Valid = currentFeatureSet.GetValid(),
                    Features = currentFeatureSet.GetFeatures(),
                    QueryFeature = QueryFeature,
                    FeatureWeights = FeaturesWeightsNativeArray,
                    FeatureSize = currentFeatureSet.FeatureSize,
                    PoseOffset = currentFeatureSet.PoseOffset,
                    CurrentDistance = currentDistance,
                    BestIndex = SearchResult
                };
                job.Schedule().Complete();
            }

            // Check if use current or best
            int best = SearchResult[0];
            if (currentValid && best == -1) best = CurrentFrame;

            return best;
        }

        private void FillTrajectory(NativeArray<float> vector, FeatureSet featureSet)
        {
            int offset = 0;
            for (int i = 0; i < MMData.TrajectoryFeatures.Count; i++)
            {
                TrajectoryFeature feature = MMData.TrajectoryFeatures[i];
                for (int p = 0; p < feature.FramesPrediction.Length; ++p)
                {
                    float3 value = CharacterController.GetWorldSpacePrediction(feature, p);
                    switch (feature.FeatureType)
                    {
                        case TrajectoryFeature.Type.Position:
                            value = GetPositionLocalCharacter(value);
                            break;
                        case TrajectoryFeature.Type.Direction:
                            value = GetDirectionLocalCharacter(value);
                            break;
                        default:
                            Debug.Assert(false, "Unknown feature type: " + feature.FeatureType);
                            break;
                    }
                    if (feature.Project)
                    {
                        vector[offset + 0] = value.x;
                        vector[offset + 1] = value.z;
                        offset += 2;
                    }
                    else
                    {
                        vector[offset + 0] = value.x;
                        vector[offset + 1] = value.y;
                        vector[offset + 2] = value.z;
                        offset += 3;
                    }
                }
            }
            // Normalize (only trajectory... because current FeatureVector is already normalized)
            featureSet.NormalizeTrajectory(vector);
        }

        private void UpdateTransformAndSkeleton(int frameIndex)
        {
            PoseSet poseSet = PoseSet;
            if (SquatIndex > 0) poseSet = SquatPoseSets[SquatIndex - 1];
            poseSet.GetPose(frameIndex, out PoseVector pose);
            // Update Inertialize if enabled
            if (Inertialize)
            {
                Inertialization.Update(pose, InertializeHalfLife, Time.deltaTime);
            }
            // Simulation Bone
            float3 previousPosition = SkeletonTransforms[0].LocalPosition;
            quaternion previousRotation = SkeletonTransforms[0].LocalRotation;
            // animation space to local space
            float3 localSpacePos = math.mul(InverseAnimationSpaceOriginRot, pose.JointLocalPositions[0] - AnimationSpaceOriginPos);
            quaternion localSpaceRot = math.mul(InverseAnimationSpaceOriginRot, pose.JointLocalRotations[0]);
            // local space to world space
            SkeletonTransforms[0].LocalPosition = math.mul(MMTransformOriginRot, localSpacePos) + MMTransformOriginPose;
            SkeletonTransforms[0].LocalRotation = math.mul(MMTransformOriginRot, localSpaceRot);
            // update velocity and angular velocity
            Velocity = ((float3)SkeletonTransforms[0].LocalPosition - previousPosition) / Time.deltaTime;
            AngularVelocity = MathExtensions.AngularVelocity(previousRotation, SkeletonTransforms[0].LocalRotation, Time.deltaTime);
            // Joints
            if (Inertialize)
            {
                for (int i = 1; i < Inertialization.InertializedRotations.Length; i++)
                {
                    SkeletonTransforms[i].LocalRotation = Inertialization.InertializedRotations[i];
                }
            }
            else
            {
                for (int i = 1; i < pose.JointLocalRotations.Length; i++)
                {
                    SkeletonTransforms[i].LocalRotation = pose.JointLocalRotations[i];
                }
            }
            // Hips Position
            SkeletonTransforms[1].LocalPosition = Inertialize ? Inertialization.InertializedHips : pose.JointLocalPositions[1];
            // Foot Lock
            UpdateFootLock(pose);
            // Post processing the transforms
            if (OnSkeletonTransformUpdated != null) OnSkeletonTransformUpdated.Invoke();
        }

        private void UpdateFootLock(PoseVector pose)
        {
            GetWorldPosAndRot(SkeletonTransforms[LeftToesIndex], out float3 currentLeftToesPosition, out _);
            GetWorldPosAndRot(SkeletonTransforms[RightToesIndex], out float3 currentRightToesPosition, out _);
            // Compute input contact position velocity
            float3 currentLeftToesVelocity = (currentLeftToesPosition - (float3)LeftToesContactTarget) / Time.deltaTime;
            float3 currentRightToesVelocity = (currentRightToesPosition - (float3)RightToesContactTarget) / Time.deltaTime;
            LeftToesContactTarget = currentLeftToesPosition;
            RightToesContactTarget = currentRightToesPosition;

            // Update Inertializer
            Inertialization.UpdateContact(IsLeftFootContact ? LeftFootContact : currentLeftToesPosition,
                                          IsLeftFootContact ? float3.zero : currentLeftToesVelocity,
                                          IsRightFootContact ? RightFootContact : currentRightToesPosition,
                                          IsRightFootContact ? float3.zero : currentRightToesVelocity,
                                          InertializeHalfLife, Time.deltaTime);
            float3 leftContactPosition = Inertialization.InertializedLeftContact;
            float3 leftContactVelocity = Inertialization.InertializedLeftContactVelocity;
            float3 rightContactPosition = Inertialization.InertializedRightContact;
            float3 rightContactVelocity = Inertialization.InertializedRightContactVelocity;

            // If the contact point is too far from the current input position
            // unlock the contact
            bool unlockLeftContact = IsLeftFootContact && (math.length(LeftFootContact - currentLeftToesPosition) > FootUnlockDistance);
            bool unlockRightContact = IsRightFootContact && (math.length(RightFootContact - currentRightToesPosition) > FootUnlockDistance);

            // If the contact was previously inactive and now it is active,
            // transition to the locked contact state
            // Also, make sure the inertialization returns an almost 0 velocity before locking
            if (!IsLeftFootContact && pose.LeftFootContact && math.length(leftContactVelocity) < MMData.ContactVelocityThreshold)
            {
                // Contact point is the current position of the foot
                // projected onto the ground + foot height
                IsLeftFootContact = true;
                LeftFootContact = leftContactPosition;
                // LeftFootContact.y = 0.0f;
                GetWorldPosAndRot(SkeletonTransforms[LeftLowerLegIndex], out _, out quaternion leftLowerLegRot);
                LeftFootPoleContact = math.mul(leftLowerLegRot, LeftLowerLegLocalForward);

                if (Inertialize)
                {
                    Inertialization.LeftContactTransition(currentLeftToesPosition, currentLeftToesVelocity, LeftFootContact, float3.zero);
                }
                else
                {
                    Inertialization.LeftContactTransition(currentLeftToesPosition, currentLeftToesVelocity, currentLeftToesPosition, currentLeftToesVelocity);
                }
            }
            // If we need to unlock or previously in contact but now not
            // we transition to the input position
            else if (unlockLeftContact || (IsLeftFootContact && !pose.LeftFootContact))
            {
                IsLeftFootContact = false;

                if (Inertialize)
                {
                    Inertialization.LeftContactTransition(LeftFootContact, float3.zero, currentLeftToesPosition, currentLeftToesVelocity);
                }
                else
                {
                    Inertialization.LeftContactTransition(currentLeftToesPosition, currentLeftToesVelocity, currentLeftToesPosition, currentLeftToesVelocity);
                }
            }

            // Same for Right Foot
            if (!IsRightFootContact && pose.RightFootContact && math.length(rightContactVelocity) < MMData.ContactVelocityThreshold)
            {
                IsRightFootContact = true;
                RightFootContact = rightContactPosition;
                // RightFootContact.y = 0.0f;
                GetWorldPosAndRot(SkeletonTransforms[RightLowerLegIndex], out _, out quaternion rightLowerLegRot);
                RightFootPoleContact = math.mul(rightLowerLegRot, RightLowerLegLocalForward);

                if (Inertialize)
                {
                    Inertialization.RightContactTransition(currentRightToesPosition, currentRightToesVelocity, RightFootContact, float3.zero);
                }
                else
                {
                    Inertialization.RightContactTransition(currentRightToesPosition, currentRightToesVelocity, currentRightToesPosition, currentRightToesVelocity);
                }
            }
            else if (unlockRightContact || (IsRightFootContact && !pose.RightFootContact))
            {
                IsRightFootContact = false;

                if (Inertialize)
                {
                    Inertialization.RightContactTransition(RightFootContact, float3.zero, currentRightToesPosition, currentRightToesVelocity);
                }
                else
                {
                    Inertialization.RightContactTransition(currentRightToesPosition, currentRightToesVelocity, currentRightToesPosition, currentRightToesVelocity);
                }
            }

            // IK to place the foot
            if (FootLock)
            {
                // Left Foot IK
                GetWorldPosAndRot(SkeletonTransforms[LeftFootIndex], out float3 leftFootPos, out quaternion leftFootRot);
                GetWorldPosAndRot(SkeletonTransforms[LeftToesIndex], out float3 leftToesPos, out _);
                GetWorldPosAndRot(SkeletonTransforms[LeftUpperLegIndex], out float3 leftUpperLegPos, out quaternion leftUpperLegRot);
                GetWorldPosAndRot(SkeletonTransforms[LeftLowerLegIndex], out float3 leftLowerLegPos, out quaternion leftLowerLegRot);
                TwoJointIK.Solve(leftContactPosition + (leftFootPos - leftToesPos),
                                 new TwoJointIK.Transform(leftUpperLegPos, leftUpperLegRot),
                                 new TwoJointIK.Transform(leftLowerLegPos, leftLowerLegRot),
                                 new TwoJointIK.Transform(leftFootPos, leftFootRot),
                                 LeftFootPoleContact);
                // Right Foot IK
                GetWorldPosAndRot(SkeletonTransforms[RightFootIndex], out float3 rightFootPos, out quaternion rightFootRot);
                GetWorldPosAndRot(SkeletonTransforms[RightToesIndex], out float3 rightToesPos, out _);
                GetWorldPosAndRot(SkeletonTransforms[RightUpperLegIndex], out float3 rigthUpperLegPos, out quaternion rigthUpperLegRot);
                GetWorldPosAndRot(SkeletonTransforms[RightLowerLegIndex], out float3 rigthLowerLegPos, out quaternion rigthLowerLegRot);
                TwoJointIK.Solve(rightContactPosition + (rightFootPos - rightToesPos),
                                 new TwoJointIK.Transform(rigthUpperLegPos, rigthUpperLegRot),
                                 new TwoJointIK.Transform(rigthLowerLegPos, rigthLowerLegRot),
                                 new TwoJointIK.Transform(rightFootPos, rightFootRot),
                                 RightFootPoleContact);
            }
        }

        private float3 GetPositionLocalCharacter(float3 worldPosition)
        {
            return math.mul(math.inverse(SkeletonTransforms[0].LocalRotation), worldPosition - SkeletonTransforms[0].LocalPosition);
        }

        private float3 GetDirectionLocalCharacter(float3 worldDir)
        {
            return math.mul(math.inverse(SkeletonTransforms[0].LocalRotation), worldDir);
        }

        /// <summary>
        /// Adds an offset to the current transform space (useful to move the character to a different position)
        /// Simply changing the transform won't work because motion matching applies root motion based on the current motion matching search space
        /// </summary>
        public void SetPosAdjustment(float3 posAdjustment)
        {
            MMTransformOriginPose += posAdjustment;
        }
        /// <summary>
        /// Adds a rot offset to the current transform space (useful to rotate the character to a different direction)
        /// Simply changing the transform won't work because motion matching applies root motion based on the current motion matching search space
        /// </summary>
        public void SetRotAdjustment(quaternion rotAdjustment)
        {
            MMTransformOriginRot = math.mul(rotAdjustment, MMTransformOriginRot);
        }

        public int GetCurrentFrame()
        {
            return CurrentFrame;
        }
        public int GetLastFrame()
        {
            return LastMMSearchFrame;
        }
        public void SetCurrentFrame(int frame)
        {
            CurrentFrame = frame;
        }
        public FeatureSet GetFeatureSet()
        {
            FeatureSet featureSet = FeatureSet;
            if (SquatIndex > 0) featureSet = SquatFeatureSets[SquatIndex - 1];
            return featureSet;
        }
        public NativeArray<float> GetQueryFeature()
        {
            return QueryFeature;
        }
        public NativeArray<float> UpdateAndGetFeatureWeights()
        {
            int offset = 0;
            for (int i = 0; i < MMData.TrajectoryFeatures.Count; i++)
            {
                TrajectoryFeature feature = MMData.TrajectoryFeatures[i];
                float weight = FeatureWeights[i] * Responsiveness;
                for (int p = 0; p < feature.FramesPrediction.Length; ++p)
                {
                    for (int f = 0; f < (feature.Project ? 2 : 3); f++)
                    {
                        FeaturesWeightsNativeArray[offset + f] = weight;
                    }
                    offset += (feature.Project ? 2 : 3);
                }
            }
            for (int i = 0; i < MMData.PoseFeatures.Count; i++)
            {
                float weight = FeatureWeights[i + MMData.TrajectoryFeatures.Count] * Quality;
                FeaturesWeightsNativeArray[offset + 0] = weight;
                FeaturesWeightsNativeArray[offset + 1] = weight;
                FeaturesWeightsNativeArray[offset + 2] = weight;
                offset += 3;
            }
            return FeaturesWeightsNativeArray;
        }

        /// <summary>
        /// Returns the skeleton used by Motion Matching
        /// </summary>
        public Skeleton GetSkeleton()
        {
            return PoseSet.Skeleton;
        }

        /// <summary>
        /// Returns the transforms used by Motion Matching to simulate the skeleton
        /// </summary>
        public Transform[] GetSkeletonTransforms()
        {
            return SkeletonTransforms;
        }

        public void Dispose()
        {
            if (FeatureSet != null) FeatureSet.Dispose();
            if (QueryFeature != null && QueryFeature.IsCreated) QueryFeature.Dispose();
            if (SearchResult != null && SearchResult.IsCreated) SearchResult.Dispose();
            if (FeaturesWeightsNativeArray != null && FeaturesWeightsNativeArray.IsCreated) FeaturesWeightsNativeArray.Dispose();
            if (LargeBoundingBoxMin != null)
            {
                for (int i = 0; i < LargeBoundingBoxMin.Length; ++i)
                {
                    if (LargeBoundingBoxMin[i] != null && LargeBoundingBoxMin[i].IsCreated) LargeBoundingBoxMin[i].Dispose();
                }
            }
            if (LargeBoundingBoxMax != null)
            {
                for (int i = 0; i < LargeBoundingBoxMax.Length; ++i)
                {
                    if (LargeBoundingBoxMax[i] != null && LargeBoundingBoxMax[i].IsCreated) LargeBoundingBoxMax[i].Dispose();
                }
            }
            if (SmallBoundingBoxMin != null)
            {
                for (int i = 0; i < SmallBoundingBoxMin.Length; ++i)
                {
                    if (SmallBoundingBoxMin[i] != null && SmallBoundingBoxMin[i].IsCreated) SmallBoundingBoxMin[i].Dispose();
                }
            }
            if (SmallBoundingBoxMax != null)
            {
                for (int i = 0; i < SmallBoundingBoxMax.Length; ++i)
                {
                    if (SmallBoundingBoxMax[i] != null && SmallBoundingBoxMax[i].IsCreated) SmallBoundingBoxMax[i].Dispose();
                }
            }
        }

        public void GetWorldPosAndRot(Transform transform, out float3 worldPos, out quaternion worldRot)
        {
            Matrix4x4 localToWorld = Matrix4x4.identity;
            worldRot = quaternion.identity;
            while (transform.Index != 0) // while not root
            {
                quaternion localRot = math.normalize(transform.LocalRotation);
                localToWorld = Matrix4x4.TRS(transform.LocalPosition, localRot, new float3(1.0f, 1.0f, 1.0f)) * localToWorld;
                worldRot = math.mul(localRot, worldRot);
                transform = SkeletonTransforms[transform.Parent];
            }
            quaternion localRotRoot = math.normalize(transform.LocalRotation);
            localToWorld = Matrix4x4.TRS(transform.LocalPosition, localRotRoot, new float3(1.0f, 1.0f, 1.0f)) * localToWorld; // root
            worldPos = localToWorld.MultiplyPoint3x4(Vector3.zero);
            worldRot = math.mul(localRotRoot, worldRot); // root
        }

        [System.Serializable]
        public class Transform
        {
            public float3 LocalPosition;
            public quaternion LocalRotation;
            public int Parent;
            public string Name;
            public int Index;

            public Transform(float3 localPosition, quaternion localRotation, int parent, string name, int index)
            {
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                Parent = parent;
                Name = name;
                Index = index;
            }
        }

        [System.Serializable]
        public struct SquatDataset
        {
            public MotionMatchingData MMData;
            public float PercentageHeight; // Below this percentage of height it will activate this squat dataset
        }
    }
}