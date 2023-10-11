using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace AvatarGoVR
{
    public class UpperBodyAvatar : MonoBehaviour, IAvatar
    {
        public Joint[] Joints;

        [HideInInspector]
        [SerializeField]
        private quaternion[] DefaultPose;

        public void OnCalibrate() { }

        public void ResetCalibration() { }

        public bool ShouldCalibrateHeight()
        {
            return false;
        }

        public quaternion[] GetDefaultPose()
        {
            return DefaultPose;
        }

        public Transform[] GetSkeleton()
        {
            Transform[] skeleton = new Transform[Joints.Length];
            for (int i = 0; i < Joints.Length; i++)
            {
                skeleton[i] = Joints[i].Transform;
            }
            return skeleton;
        }

        public HumanBodyBones[] GetTopology()
        {
            HumanBodyBones[] topology = new HumanBodyBones[Joints.Length];
            for (int i = 0; i < Joints.Length; i++)
            {
                topology[i] = Joints[i].HumanBodyBone;
            }
            return topology;
        }

        private void Reset()
        {
            Joints = new Joint[]
            {
                new Joint(HumanBodyBones.Hips), // 0
                new Joint(HumanBodyBones.LeftUpperLeg), // 1
                new Joint(HumanBodyBones.LeftLowerLeg), // 2
                new Joint(HumanBodyBones.LeftFoot), // 3
                new Joint(HumanBodyBones.LeftToes), // 4
                new Joint(HumanBodyBones.RightUpperLeg), // 5
                new Joint(HumanBodyBones.RightLowerLeg), // 6
                new Joint(HumanBodyBones.RightFoot), // 7
                new Joint(HumanBodyBones.RightToes), // 8
                new Joint(HumanBodyBones.Spine), // 9
                new Joint(HumanBodyBones.Chest), // 10
                new Joint(HumanBodyBones.UpperChest), // 11
                new Joint(HumanBodyBones.Neck), // 12
                new Joint(HumanBodyBones.Head), // 13
                new Joint(HumanBodyBones.LeftShoulder), // 14
                new Joint(HumanBodyBones.LeftUpperArm), // 15
                new Joint(HumanBodyBones.LeftLowerArm), // 16
                new Joint(HumanBodyBones.LeftHand), // 17
                new Joint(HumanBodyBones.RightShoulder), // 18
                new Joint(HumanBodyBones.RightUpperArm), // 19
                new Joint(HumanBodyBones.RightLowerArm), // 20
                new Joint(HumanBodyBones.RightHand), // 21
            };
        }

        public bool IsDefaultPoseSet()
        {
            return DefaultPose != null && DefaultPose.Length == Joints.Length;
        }

        public Transform GetJointTransform(int index)
        {
            return Joints[index].Transform;
        }
        public Transform GetHipsTransform()
        {
            return Joints[0].Transform;
        }
        public Transform GetHeadTransform()
        {
            return Joints[13].Transform;
        }
        [ContextMenu("Compute Local Axes")]
        public void ComputeLocalAxes()
        {
            DefaultPose = new quaternion[Joints.Length];
            for (int i = 0; i < Joints.Length; ++i)
            {
                if (Joints[i].Transform != null)
                {
                    DefaultPose[i] = Joints[i].Transform.rotation;
                }
            }
        }

        public void GetJointWorldAxes(int index, out float3 forward, out float3 up, out float3 right)
        {
            Debug.Assert(Joints[index].Transform != null, "Joint " + index + " is not set");
            forward = math.mul(math.mul(Joints[index].Transform.rotation, math.inverse(DefaultPose[index])), math.forward());
            up = math.mul(math.mul(Joints[index].Transform.rotation, math.inverse(DefaultPose[index])), math.up());
            right = math.mul(math.mul(Joints[index].Transform.rotation, math.inverse(DefaultPose[index])), math.right());
        }

        [System.Serializable]
        public struct Joint
        {
            public HumanBodyBones HumanBodyBone;
            public Transform Transform;

            public Joint(HumanBodyBones humanBodyBones) : this()
            {
                HumanBodyBone = humanBodyBones;
            }
        }
    }
}