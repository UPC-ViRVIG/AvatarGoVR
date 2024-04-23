using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace AvatarGoVR
{
    using Target = FBIK.FBIK.Target;

    public class FloatingUpperBodyAnimProvider : IAnimationProvider
    {
        private FBIK.FBIK FBIK = new FBIK.FBIK();

        public IAnimationProvider.Compatibility IsCompatible(DeviceManager deviceManager, IAvatar avatar)
        {
            bool hasHips = false, hasHead = false, isHumanTopology = false;
            Transform[] skeleton = avatar.GetSkeleton();
            HumanBodyBones[] topology = avatar.GetTopology();
            if (skeleton.Length != topology.Length) return IAnimationProvider.Compatibility.INCOMPATIBLE;
            int spineCount = 0, lArmCount = 0, rArmCount = 0;
            for (int i = 0; i < skeleton.Length; ++i)
            {
                hasHips = hasHips || topology[i] == HumanBodyBones.Hips;
                hasHead = hasHead || topology[i] == HumanBodyBones.Head;
                isHumanTopology = isHumanTopology || HumanTopology[i] == topology[i];
                if (topology[i] == HumanBodyBones.Spine ||
                    topology[i] == HumanBodyBones.Chest ||
                    topology[i] == HumanBodyBones.UpperChest ||
                    topology[i] == HumanBodyBones.Neck)
                {
                    ++spineCount;
                }
                else if (topology[i] == HumanBodyBones.LeftShoulder ||
                         topology[i] == HumanBodyBones.LeftUpperArm ||
                         topology[i] == HumanBodyBones.LeftLowerArm ||
                         topology[i] == HumanBodyBones.LeftHand)
                {
                    ++lArmCount;
                }
                else if (topology[i] == HumanBodyBones.RightShoulder ||
                         topology[i] == HumanBodyBones.RightUpperArm ||
                         topology[i] == HumanBodyBones.RightLowerArm ||
                         topology[i] == HumanBodyBones.RightHand)
                {
                    ++rArmCount;
                }
            }
            bool isSpineAtLeast2 = spineCount >= 2;
            bool isLArmAtLeast3 = lArmCount >= 3;
            bool isRArmAtLeast3 = rArmCount >= 3;
            bool hasHeadDevice = deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.Head);
            bool hasLeftHandDevice = deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.LeftHand);
            bool hasRightHandDevice = deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.RightHand);
            if (hasHips && hasHead && isHumanTopology && isSpineAtLeast2 &&
                isLArmAtLeast3 && isRArmAtLeast3 &&
                hasHeadDevice && hasLeftHandDevice && hasRightHandDevice)
            {
                return IAnimationProvider.Compatibility.COMPATIBLE;
            }
            return IAnimationProvider.Compatibility.INCOMPATIBLE;
        }

        private Transform[] Skeleton;
        private HumanBodyBones[] Topology;

        public void SetAnimation(Transform[] skeleton, HumanBodyBones[] topology, quaternion[] defaultPose, DeviceManager deviceManager)
        {
            bool initFBIK = Skeleton == null || Skeleton.Length != skeleton.Length ||
                            Topology == null || Topology.Length != topology.Length;
            if (!initFBIK)
            {
                for (int i = 0; i < skeleton.Length; ++i)
                {
                    if (skeleton[i] != Skeleton[i] || topology[i] != Topology[i])
                    {
                        initFBIK = true;
                        break;
                    }
                }
            }
            if (initFBIK)
            {
                Skeleton = skeleton;
                Topology = topology;
                FBIK.Init(skeleton, defaultPose);
            }

            const int hipsIndex = 0;
            const int headIndex = 13;
            float distanceHeadHips = Vector3.Distance(skeleton[headIndex].position, skeleton[hipsIndex].position);

            deviceManager.GetDevicePose(DeviceManager.DeviceRole.Head, out Vector3 headPos, out Quaternion headRot);
            deviceManager.GetDevicePose(DeviceManager.DeviceRole.LeftHand, out Vector3 lHandPos, out Quaternion lHandRot);
            deviceManager.GetDevicePose(DeviceManager.DeviceRole.RightHand, out Vector3 rHandPos, out Quaternion rHandRot);
            FBIK.Solve(new Target(headPos, Quaternion.identity),
                       new Target(headPos - Vector3.up * distanceHeadHips, headRot),
                       new Target(lHandPos, lHandRot),
                       new Target(rHandPos, rHandRot),
                       solveRoot: true);
        }

        public void Dispose() { }

        private readonly HumanBodyBones[] HumanTopology = new HumanBodyBones[]
        {
            HumanBodyBones.Hips, // 0
            HumanBodyBones.LeftUpperLeg, // 1
            HumanBodyBones.LeftLowerLeg, // 2
            HumanBodyBones.LeftFoot, // 3
            HumanBodyBones.LeftToes, // 4
            HumanBodyBones.RightUpperLeg, // 5
            HumanBodyBones.RightLowerLeg, // 6
            HumanBodyBones.RightFoot, // 7
            HumanBodyBones.RightToes, // 8
            HumanBodyBones.Spine, // 9
            HumanBodyBones.Chest, // 10
            HumanBodyBones.UpperChest, // 11
            HumanBodyBones.Neck, // 12
            HumanBodyBones.Head, // 13
            HumanBodyBones.LeftShoulder, // 14
            HumanBodyBones.LeftUpperArm, // 15
            HumanBodyBones.LeftLowerArm, // 16
            HumanBodyBones.LeftHand, // 17
            HumanBodyBones.RightShoulder, // 18
            HumanBodyBones.RightUpperArm, // 19
            HumanBodyBones.RightLowerArm, // 20
            HumanBodyBones.RightHand, // 21
        };
    }
}
