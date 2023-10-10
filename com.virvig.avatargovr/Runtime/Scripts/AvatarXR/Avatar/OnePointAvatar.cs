using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace AvatarGoVR
{
    public class OnePointAvatar : MonoBehaviour, IAvatar
    {
        public Transform Head;

        private quaternion[] DefaultPose = new quaternion[] { quaternion.identity };

        public void OnCalibrate() { }
        public void ResetCalibration() { }

        public quaternion[] GetDefaultPose()
        {
            return DefaultPose;
        }

        public Transform[] GetSkeleton()
        {
            return new Transform[] { Head };
        }

        public HumanBodyBones[] GetTopology()
        {
            return new HumanBodyBones[] { HumanBodyBones.Head };
        }

        public bool ShouldCalibrateHeight()
        {
            return false;
        }
    }
}