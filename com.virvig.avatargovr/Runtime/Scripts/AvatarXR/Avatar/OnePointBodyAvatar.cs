using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace AvatarGoVR
{
    public class OnePointBodyAvatar : MonoBehaviour, IAvatar
    {
        public Transform Head;
        public Transform Body;

        private quaternion[] DefaultPose = new quaternion[] { quaternion.identity, quaternion.identity };

        public void OnCalibrate() { }
        public void ResetCalibration() { }

        public quaternion[] GetDefaultPose()
        {
            return DefaultPose;
        }

        public Transform[] GetSkeleton()
        {
            return new Transform[] { Head, Body };
        }

        public HumanBodyBones[] GetTopology()
        {
            return new HumanBodyBones[] { HumanBodyBones.Head, HumanBodyBones.Hips };
        }

        public bool ShouldCalibrateHeight()
        {
            return false;
        }
    }
}