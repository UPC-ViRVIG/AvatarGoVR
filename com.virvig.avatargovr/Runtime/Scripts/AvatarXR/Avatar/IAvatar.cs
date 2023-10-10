using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace AvatarGoVR
{
    public interface IAvatar
    {
        Transform[] GetSkeleton();
        HumanBodyBones[] GetTopology();
        quaternion[] GetDefaultPose();
        void OnCalibrate();
        void ResetCalibration();
        /// <summary>
        /// Returns true if the height should be calibrated
        /// </summary>
        bool ShouldCalibrateHeight();
    }
}