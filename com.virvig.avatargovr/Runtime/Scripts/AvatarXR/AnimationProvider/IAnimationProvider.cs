using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace AvatarGoVR
{
    public interface IAnimationProvider
    {
        /// <summary>
        /// Animates the skeleton
        /// </summary>
        void SetAnimation(Transform[] skeleton, HumanBodyBones[] topology, quaternion[] defaultPose, DeviceManager deviceManager);
        /// <summary>
        /// Returns true if the given devices are enough for this animation provider to work (e.g., 1 HMD, Left Controller and Right Controller)
        /// </summary>
        Compatibility IsCompatible(DeviceManager deviceManager, IAvatar avatar);
        /// <summary>
        /// Dispose the animation provider
        /// </summary>
        void Dispose();

        public enum Compatibility
        {
            COMPATIBLE,
            INCOMPATIBLE,
            CALIBRATION_REQUIRED
        }
    }
}