using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System;

namespace MotionMatching
{
    [RequireComponent(typeof(Animator))]
    public class MotionMatchingSkinnedMeshRenderer : MonoBehaviour
    {
        public MotionMatchingController MotionMatching;
        [Tooltip("Local vector (axis) pointing in the forward direction of the character")] public Vector3 ForwardLocalVector = new Vector3(0, 0, 1);

        private Animator _Animator;
        private Animator Animator
        {
            get
            {
                if (_Animator == null)
                {
                    _Animator = GetComponent<Animator>();
                }
                return _Animator;
            }
        }

        // Retargeting
        // Initial orientations of the bones The code assumes the initial orientations are in T-Pose
        private Quaternion[] SourceTPose;
        private Quaternion[] TargetTPose;
        // Mapping from BodyJoints to the actual transforms
        private MotionMatchingController.Transform[] SourceBones;
        private Transform[] TargetBones;
        public bool ShouldRetarget { get { return MotionMatching.MMData.BVHTPose != null; } }

        public void Enable()
        {
            MotionMatching.OnSkeletonTransformUpdated -= OnSkeletonTransformUpdated;
            MotionMatching.OnSkeletonTransformUpdated += OnSkeletonTransformUpdated;
        }

        public void Disable()
        {
            MotionMatching.OnSkeletonTransformUpdated -= OnSkeletonTransformUpdated;
        }

        public void Init()
        {
            // BindSkinnedMeshRenderers();
            if (ShouldRetarget && SourceTPose == null) InitRetargeting();
        }

        private void InitRetargeting()
        {
            MotionMatchingData mmData = MotionMatching.MMData;
            SourceTPose = new Quaternion[BodyJoints.Length];
            TargetTPose = new Quaternion[BodyJoints.Length];
            SourceBones = new MotionMatchingController.Transform[BodyJoints.Length];
            TargetBones = new Transform[BodyJoints.Length];
            // Source TPose (BVH with TPose)
            BVHImporter bvhImporter = new BVHImporter();
            // Animation containing in the first frame a TPose
            BVHAnimation tposeAnimation = bvhImporter.Import(mmData.BVHTPose, mmData.UnitScale, true);
            // Store Rotations
            // Source
            Skeleton skeleton = tposeAnimation.Skeleton;
            for (int i = 0; i < BodyJoints.Length; i++)
            {
                if (mmData.GetJointName(BodyJoints[i], out string jointName) &&
                    skeleton.Find(jointName, out Skeleton.Joint joint))
                {
                    // Get the rotation for the first frame of the animation
                    SourceTPose[i] = tposeAnimation.GetWorldRotation(joint, 0);
                }
            }
            // Correct rotations so they are facing the same direction as the target
            // Correct Source
            float3 currentDirection = math.mul(SourceTPose[0], mmData.HipsForwardLocalVector);
            currentDirection.y = 0;
            currentDirection = math.normalize(currentDirection);
            float3 targetDirection = transform.TransformDirection(ForwardLocalVector);
            targetDirection.y = 0;
            targetDirection = math.normalize(targetDirection);
            quaternion correctionRot = MathExtensions.FromToRotation(currentDirection, targetDirection, new float3(0, 1, 0));
            for (int i = 0; i < BodyJoints.Length; i++)
            {
                SourceTPose[i] = math.mul(correctionRot, SourceTPose[i]);
            }
            // Target
            Quaternion rot = Animator.transform.rotation;
            Animator.transform.rotation = Quaternion.identity;
            for (int i = 0; i < BodyJoints.Length; i++)
            {
                TargetTPose[i] = Animator.GetBoneTransform(BodyJoints[i]).rotation;
            }
            Animator.transform.rotation = rot;
            // Store Transforms
            MotionMatchingController.Transform[] mmBones = MotionMatching.GetSkeletonTransforms();
            Dictionary<string, MotionMatchingController.Transform> boneDict = new Dictionary<string, MotionMatchingController.Transform>();
            foreach (MotionMatchingController.Transform bone in mmBones)
            {
                boneDict.Add(bone.Name, bone);
            }
            // Source
            for (int i = 0; i < BodyJoints.Length; i++)
            {
                if (mmData.GetJointName(BodyJoints[i], out string jointName) &&
                    boneDict.TryGetValue(jointName, out MotionMatchingController.Transform bone))
                {
                    SourceBones[i] = bone;
                }
            }
            // Target
            for (int i = 0; i < BodyJoints.Length; i++)
            {
                TargetBones[i] = Animator.GetBoneTransform(BodyJoints[i]);
            }
        }

        private void OnSkeletonTransformUpdated()
        {
            if (!ShouldRetarget) return;
            // Motion
            transform.position = MotionMatching.GetSkeletonTransforms()[0].LocalPosition;
            // Retargeting
            for (int i = 0; i < BodyJoints.Length; i++)
            {
                Quaternion sourceTPoseRotation = SourceTPose[i];
                Quaternion targetTPoseRotation = TargetTPose[i];
                MotionMatching.GetWorldPosAndRot(SourceBones[i], out _, out quaternion sourceRotation);
                /*
                    R_t = Rotation transforming from target local space to world space
                    R_s = Rotation transforming from source local space to world space
                    R_t = R_s * R_st (R_st is a matrix transforming from target to source space)
                    // It makes sense because R_st will be mapping from target to source, and R_s from source to world.
                    // The result is transforming from T to world, which is what R_t does.
                    RTPose_t = RTPose_s * R_st
                    R_st = (RTPose_s)^-1 * RTPose_t
                    R_t = R_s * (R_st)^-1 * RTPose_t
                */
                TargetBones[i].rotation = ((Quaternion)sourceRotation) * Quaternion.Inverse(sourceTPoseRotation) * targetTPoseRotation;
            }
            // Hips Height
            MotionMatching.GetWorldPosAndRot(MotionMatching.GetSkeletonTransforms()[1], out float3 hipsPos, out _);
            TargetBones[0].position = hipsPos;
            // Correct Toe if under ground
            //Transform leftToes = Animator.GetBoneTransform(HumanBodyBones.LeftToes);
            //if (leftToes.position.y < 0.0f)
            //{
            //    Transform leftFoot = Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            //    CorrectToes(leftToes, leftFoot);
            //}
            //Transform rightToes = Animator.GetBoneTransform(HumanBodyBones.RightToes);
            //if (rightToes.position.y < 0.0f)
            //{
            //    Transform rightFoot = Animator.GetBoneTransform(HumanBodyBones.RightFoot);
            //    CorrectToes(rightToes, rightFoot);
            //}
        }

        private void CorrectToes(Transform toesT, Transform footT)
        {
            float3 toes = toesT.position;
            float3 foot = footT.position;
            float3 toesFoot = math.normalize(toes - foot);
            float3 desiredToesFoot = math.normalize(new float3(toes.x, 0.0f, toes.z) - foot);
            float angleCorrection = math.acos(math.clamp(math.dot(desiredToesFoot, toesFoot), -1.0f, 1.0f));
            float3 axisCorrection = math.normalize(math.cross(toesFoot, desiredToesFoot));
            quaternion rotCorrection = quaternion.AxisAngle(axisCorrection, angleCorrection);
            footT.rotation = math.mul(rotCorrection, footT.rotation);
        }

        // Used for retargeting. First parent, then children
        private HumanBodyBones[] BodyJoints =
        {
            HumanBodyBones.Hips,

            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,

            HumanBodyBones.Neck,
            HumanBodyBones.Head,

            HumanBodyBones.LeftShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,

            HumanBodyBones.RightShoulder,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,

            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.LeftToes,

            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.RightToes
        };

        private void OnValidate()
        {
            if (math.abs(math.length(ForwardLocalVector)) < 1E-3f)
            {
                Debug.LogWarning("ForwardLocalVector is too close to zero. Object: " + name);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Skeleton
            if (MotionMatching == null || MotionMatching.GetSkeletonTransforms() == null) return;

            Gizmos.color = Color.red;
            for (int i = 2; i < MotionMatching.GetSkeletonTransforms().Length; i++) // skip Simulation Bone
            {
                MotionMatchingController.Transform t = MotionMatching.GetSkeletonTransforms()[i];
                MotionMatching.GetWorldPosAndRot(t, out float3 pos, out _);
                MotionMatching.GetWorldPosAndRot(MotionMatching.GetSkeletonTransforms()[t.Parent], out float3 parentPos, out _);
                GizmosExtensions.DrawLine(parentPos, pos, 3);
            }
        }
#endif
    }
}