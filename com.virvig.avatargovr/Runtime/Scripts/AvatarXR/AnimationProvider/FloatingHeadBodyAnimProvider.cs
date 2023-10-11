using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace AvatarGoVR
{
    public class FloatingHeadBodyAnimProvider : IAnimationProvider
    {
        public IAnimationProvider.Compatibility IsCompatible(DeviceManager deviceManager, IAvatar avatar)
        {
            HumanBodyBones[] topology = avatar.GetTopology();
            if (topology.Length != 2) return IAnimationProvider.Compatibility.INCOMPATIBLE;
            bool hasHead = false;
            bool hasHips = false;
            foreach (HumanBodyBones boneType in topology)
            {
                hasHead = hasHead || boneType == HumanBodyBones.Head;
                hasHips = hasHips || boneType == HumanBodyBones.Hips;
            }
            if (hasHead && hasHips && deviceManager.IsDeviceAvailable(DeviceManager.DeviceRole.Head))
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
