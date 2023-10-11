using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace AvatarGoVR
{
    public class FloatingHeadArmsBodyAnimProvider : IAnimationProvider
    {
        public IAnimationProvider.Compatibility IsCompatible(DeviceManager deviceManager, IAvatar avatar)
        {
            HumanBodyBones[] topology = avatar.GetTopology();
            if (topology.Length != 4) return IAnimationProvider.Compatibility.INCOMPATIBLE;
            bool hasHead, hasLeftHand, hasRightHand, hasHips;
            hasHead = hasLeftHand = hasRightHand = hasHips = false;
            foreach (HumanBodyBones boneType in topology)
            {
                hasHead = hasHead || boneType == HumanBodyBones.Head;
                hasLeftHand = hasLeftHand || boneType == HumanBodyBones.LeftHand;
                hasRightHand = hasRightHand || boneType == HumanBodyBones.RightHand;
                hasHips = hasHips || boneType == HumanBodyBones.Hips;
            }
            if (hasHead && hasLeftHand && hasRightHand && hasHips &&
                deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.Head) &&
                deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.LeftHand) &&
                deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.RightHand))
            {
                return IAnimationProvider.Compatibility.COMPATIBLE;
            }
            return IAnimationProvider.Compatibility.INCOMPATIBLE;
        }

        public void SetAnimation(Transform[] skeleton, HumanBodyBones[] topology, quaternion[] defaultPose, DeviceManager deviceManager)
        {
            for (int i = 0; i < topology.Length; ++i)
            {
                if (topology[i] == HumanBodyBones.Head)
                {
                    if (deviceManager.GetDevicePose(DeviceManager.DeviceRole.Head, out Vector3 headPos, out Quaternion headRot))
                    {
                        skeleton[i].SetPositionAndRotation(headPos, headRot);
                    }
                }
                else if (topology[i] == HumanBodyBones.LeftHand)
                {
                    if (deviceManager.GetDevicePose(DeviceManager.DeviceRole.LeftHand, out Vector3 leftHandPos, out Quaternion leftHandRot))
                    {
                        skeleton[i].SetPositionAndRotation(leftHandPos, leftHandRot);
                    }
                }
                else if (topology[i] == HumanBodyBones.RightHand)
                {
                    if (deviceManager.GetDevicePose(DeviceManager.DeviceRole.RightHand, out Vector3 rightHandPos, out Quaternion rightHandRot))
                    {
                        skeleton[i].SetPositionAndRotation(rightHandPos, rightHandRot);
                    }
                }
                else if (topology[i] == HumanBodyBones.Hips)
                {
                    if (deviceManager.GetDevicePose(DeviceManager.DeviceRole.Head, out Vector3 headPos, out _))
                    {
                        skeleton[i].SetPositionAndRotation(headPos, quaternion.identity);
                    }
                }
            }
        }

        public void Dispose() { }
    }
}
