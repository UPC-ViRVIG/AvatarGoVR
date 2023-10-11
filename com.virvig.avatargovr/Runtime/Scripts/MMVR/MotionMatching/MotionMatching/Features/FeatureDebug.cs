using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MotionMatching;
using Unity.Mathematics;

/// <summary>
/// Import a BVH, create PoseSet and FeatureSet and visualize it using Gizmos.
/// </summary>
public class FeatureDebug : MonoBehaviour
{
    public MotionMatchingData MMData;
    public bool Play;
    public float SpheresRadius = 0.1f;
    public bool LockFPS = true;

    private PoseSet PoseSet;
    private FeatureSet FeatureSet;
    private Transform[] SkeletonTransforms;
    [SerializeField] private int CurrentFrame;

    private void Awake()
    {
        // PoseSet
        PoseSet = MMData.GetOrImportPoseSet();

        // FeatureSet
        FeatureSet = MMData.GetOrImportFeatureSet();

        // Skeleton
        SkeletonTransforms = new Transform[PoseSet.Skeleton.Joints.Count];
        SkeletonTransforms[0] = transform; // Simulation Bone
        for (int j = 1; j < PoseSet.Skeleton.Joints.Count; j++)
        {
            // Joints
            Skeleton.Joint joint = PoseSet.Skeleton.Joints[j];
            Transform t = (new GameObject()).transform;
            t.name = joint.Name;
            t.SetParent(SkeletonTransforms[joint.ParentIndex], false);
            t.localPosition = joint.LocalOffset;
            SkeletonTransforms[j] = t;
        }

        // FPS
        if (LockFPS)
        {
            Application.targetFrameRate = Mathf.RoundToInt(1.0f / PoseSet.FrameTime);
            Debug.Log("[BVHDebug] Updated Target FPS: " + Application.targetFrameRate);
        }
        else
        {
            Application.targetFrameRate = -1;
        }
    }

    private void Update()
    {
        if (Play)
        {
            PoseSet.GetPose(CurrentFrame, out PoseVector pose);
            SkeletonTransforms[0].localPosition = pose.JointLocalPositions[0];
            SkeletonTransforms[1].localPosition = pose.JointLocalPositions[1];
            for (int i = 0; i < pose.JointLocalRotations.Length; i++)
            {
                SkeletonTransforms[i].localRotation = pose.JointLocalRotations[i];
            }
            CurrentFrame = (CurrentFrame + 1) % PoseSet.NumberPoses;
        }
        else
        {
            CurrentFrame = 0;
            SkeletonTransforms[0].localPosition = float3.zero;
            for (int i = 0; i < SkeletonTransforms.Length; i++)
            {
                SkeletonTransforms[i].localRotation = quaternion.identity;
            }
        }
    }

    private void OnDestroy()
    {
        FeatureSet.Dispose();
    }

    private void OnApplicationQuit()
    {
        FeatureSet.Dispose();
    }


#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Skeleton
        if (SkeletonTransforms == null || PoseSet == null) return;

        Gizmos.color = Color.red;
        for (int i = 2; i < SkeletonTransforms.Length; i++) // skip Simulation Bone
        {
            Transform t = SkeletonTransforms[i];
            GizmosExtensions.DrawLine(t.parent.position, t.position, 3);
        }

        if (!Play) return;
        // Character
        int currentFrame = CurrentFrame - 1; // FeatureDebug increments CurrentFrame after update... OnDrawGizmos is called after update
        PoseSet.GetPose(currentFrame, out PoseVector pose);
        FeatureSet.GetWorldOriginCharacter(pose, out float3 characterOrigin, out float3 characterForward);
        Gizmos.color = new Color(1.0f, 0.0f, 0.5f, 1.0f);
        Gizmos.DrawSphere(characterOrigin, SpheresRadius);
        GizmosExtensions.DrawArrow(characterOrigin, characterOrigin + characterForward, thickness: 3);

        // Forward Trajectory Direction Features
        Gizmos.color = Color.gray;
        for (int t = 0; t < MMData.TrajectoryFeatures.Count; t++)
        {
            var trajectoryFeature = MMData.TrajectoryFeatures[t];
            if (trajectoryFeature.FeatureType == MotionMatchingData.TrajectoryFeature.Type.Direction &&
                !trajectoryFeature.SimulationBone)
            {
                if (!PoseSet.Skeleton.Find(trajectoryFeature.Bone, out Skeleton.Joint joint)) Debug.Assert(false, "Bone not found");
                float3 dir = SkeletonTransforms[joint.Index].TransformDirection(MMData.GetLocalForward(joint.Index));
                float3 jointPos = SkeletonTransforms[joint.Index].position;
                GizmosExtensions.DrawArrow(jointPos, jointPos + dir * 0.5f, 0.1f, thickness: 3);
            }
        }

        // Feature Set
        if (FeatureSet == null) return;

        DrawFeatureGizmos(FeatureSet, MMData, SpheresRadius, currentFrame, characterOrigin, characterForward,
                          SkeletonTransforms, PoseSet.Skeleton);
    }

    public static void DrawFeatureGizmos(FeatureSet set, MotionMatchingData mmData, float spheresRadius, int currentFrame,
                                         float3 characterOrigin, float3 characterForward, Transform[] joints, Skeleton skeleton,
                                         bool debugPose = true, bool debugTrajectory = true)
    {
        if (!set.IsValidFeature(currentFrame)) return;

        quaternion characterRot = quaternion.LookRotation(characterForward, new float3(0, 1, 0));
        // Trajectory
        if (debugTrajectory)
        {
            for (int t = 0; t < mmData.TrajectoryFeatures.Count; t++)
            {
                var trajectoryFeature = mmData.TrajectoryFeatures[t];
                for (int p = 0; p < trajectoryFeature.FramesPrediction.Length; p++)
                {
                    Gizmos.color = Color.blue * (1.25f - (float)p / trajectoryFeature.FramesPrediction.Length);
                    float3 value;
                    if (trajectoryFeature.Project)
                    {
                        float2 value2D = set.GetProjectedTrajectoryFeature(currentFrame, t, p, true);
                        value = new float3(value2D.x, 0.0f, value2D.y);
                    }
                    else
                    {
                        value = set.GetTrajectoryFeature(currentFrame, t, p, true);
                    }
                    switch (trajectoryFeature.FeatureType)
                    {
                        case MotionMatchingData.TrajectoryFeature.Type.Position:
                            value = characterOrigin + math.mul(characterRot, value);
                            Gizmos.DrawSphere(value, spheresRadius);
                            break;
                        case MotionMatchingData.TrajectoryFeature.Type.Direction:
                            float3 jointPos;
                            if (trajectoryFeature.SimulationBone)
                            {
                                jointPos = characterOrigin;
                            }
                            else
                            {
                                if (!skeleton.Find(trajectoryFeature.Bone, out Skeleton.Joint joint)) Debug.Assert(false, "Bone not found");
                                jointPos = joints[joint.Index].position;
                            }
                            value = math.mul(characterRot, value);
                            GizmosExtensions.DrawArrow(jointPos, jointPos + value * 0.5f, 0.1f, thickness: 3);
                            break;
                    }
                }
            }
        }
        // Pose
        if (debugPose)
        {
            Gizmos.color = Color.cyan;
            for (int p = 0; p < mmData.PoseFeatures.Count; p++)
            {
                var poseFeature = mmData.PoseFeatures[p];
                float3 value = set.GetPoseFeature(currentFrame, p, true);
                switch (poseFeature.FeatureType)
                {
                    case MotionMatchingData.PoseFeature.Type.Position:
                        value = characterOrigin + math.mul(characterRot, value);
                        Gizmos.DrawSphere(value, spheresRadius);
                        break;
                    case MotionMatchingData.PoseFeature.Type.Velocity:
                        value = math.mul(characterRot, value);
                        if (math.length(value) > 0.001f)
                        {
                            skeleton.Find(poseFeature.Bone, out Skeleton.Joint joint);
                            float3 jointPos = joints[joint.Index].position;
                            GizmosExtensions.DrawArrow(jointPos, jointPos + value * 0.1f, 0.25f * math.length(value) * 0.1f, thickness: 3);
                        }
                        break;
                }
            }
        }
    }
#endif
}