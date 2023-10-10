using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace AvatarGoVR
{
    public class ThreePointAvatar : MonoBehaviour, IAvatar
    {
        public Transform Head;
        public Transform LeftHand;
        public Transform RightHand;

        quaternion[] DefaultPose = new quaternion[] { quaternion.identity, quaternion.identity, quaternion.identity };

        public void OnCalibrate() { }
        public void ResetCalibration() { }

        public quaternion[] GetDefaultPose()
        {
            return DefaultPose;
        }

        public Transform[] GetSkeleton()
        {
            return new Transform[] { Head, LeftHand, RightHand };
        }

        public HumanBodyBones[] GetTopology()
        {
            return new HumanBodyBones[] { HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
        }

        public bool ShouldCalibrateHeight()
        {
            return false;
        }
    }
}