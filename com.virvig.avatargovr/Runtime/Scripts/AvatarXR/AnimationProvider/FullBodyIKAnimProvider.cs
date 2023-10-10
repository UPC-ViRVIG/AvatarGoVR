using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace AvatarGoVR
{
    using Target = FBIK.FBIK.Target;

    public class FullBodyIKAnimProvider : IAnimationProvider
    {
        private FBIK.FBIK FBIK = new FBIK.FBIK();

        public IAnimationProvider.Compatibility IsCompatible(DeviceManager deviceManager, IAvatar avatar)
        {
            bool hasHips = false, hasHead = false, isHumanTopology = false;
            Transform[] skeleton = avatar.GetSkeleton();
            HumanBodyBones[] topology = avatar.GetTopology();
            if (skeleton.Length != topology.Length) return IAnimationProvider.Compatibility.INCOMPATIBLE;
            int spineCount = 0, lLegCount = 0, rLegCount = 0, lArmCount = 0, rArmCount = 0;
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
                else if (topology[i] == HumanBodyBones.LeftUpperLeg ||
                         topology[i] == HumanBodyBones.LeftLowerLeg ||
                         topology[i] == HumanBodyBones.LeftFoot ||
                         topology[i] == HumanBodyBones.LeftToes)
                {
                    ++lLegCount;
                }
                else if (topology[i] == HumanBodyBones.RightUpperLeg ||
                         topology[i] == HumanBodyBones.RightLowerLeg ||
                         topology[i] == HumanBodyBones.RightFoot ||
                         topology[i] == HumanBodyBones.RightToes)
                {
                    ++rLegCount;
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
            bool isLLegAtLeast3 = lLegCount >= 3;
            bool isRLegAtLeast3 = rLegCount >= 3;
            bool isLArmAtLeast3 = lArmCount >= 3;
            bool isRArmAtLeast3 = rArmCount >= 3;
            bool hasHeadDevice = deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.Head);
            bool hasHipsDevice = deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.Hips);
            bool hasLeftHandDevice = deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.LeftHand);
            bool hasRightHandDevice = deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.RightHand);
            bool hasLeftFootDevice = deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.LeftFoot);
            bool hasRightFootDevice = deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.RightFoot);
            if (hasHips && hasHead && isHumanTopology && isSpineAtLeast2 &&
                isLLegAtLeast3 && isRLegAtLeast3 && isLArmAtLeast3 && isRArmAtLeast3)
            {
                int numberUncalibratedDevices = deviceManager.GetNumberUncalibratedDevices();
                if (hasHeadDevice && hasHipsDevice && hasLeftHandDevice && hasRightHandDevice &&
                    hasLeftFootDevice && hasRightFootDevice)
                {
                    return IAnimationProvider.Compatibility.COMPATIBLE;
                }
                else if (hasHeadDevice && hasLeftHandDevice && hasRightHandDevice)
                {
                    int numberCalibratedDevices = (hasHipsDevice ? 1 : 0) + (hasLeftFootDevice ? 1 : 0) + (hasRightFootDevice ? 1 : 0);
                    if (3 - numberCalibratedDevices <= numberUncalibratedDevices)
                    {
                        return IAnimationProvider.Compatibility.CALIBRATION_REQUIRED;
                    }
                }
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
            deviceManager.GetDevicePose(DeviceManager.DeviceRole.Hips, out Vector3 hipsPos, out Quaternion hipsRot);
            deviceManager.GetDevicePose(DeviceManager.DeviceRole.Head, out Vector3 headPos, out Quaternion headRot);
            deviceManager.GetDevicePose(DeviceManager.DeviceRole.LeftHand, out Vector3 lHandPos, out Quaternion lHandRot);
            deviceManager.GetDevicePose(DeviceManager.DeviceRole.RightHand, out Vector3 rHandPos, out Quaternion rHandRot);
            deviceManager.GetDevicePose(DeviceManager.DeviceRole.LeftFoot, out Vector3 lFootPos, out Quaternion lFootRot);
            deviceManager.GetDevicePose(DeviceManager.DeviceRole.RightFoot, out Vector3 rFootPos, out Quaternion rFootRot);
            FBIK.Solve(new Target(headPos, headRot),
                       new Target(hipsPos, hipsRot),
                       new Target(lHandPos, lHandRot),
                       new Target(rHandPos, rHandRot),
                       new Target(lFootPos, lFootRot),
                       new Target(rFootPos, rFootRot));
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