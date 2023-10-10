using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using Unity.Mathematics;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UIElements;

namespace FBIK
{
    public static class CCD
    {
        /// <summary>
        /// Solve CCD for the last joint of the chain (joints array)
        /// </summary>
        public static void Solve(float3 target, Span<Transform> joints, Span<float> weights, float3 rotAxis, int numberIterations = 5)
        {
            if (joints.Length != weights.Length)
                throw new ArgumentException("joints and weights must have the same length.");

            Transform endEffector = joints[joints.Length - 1];

            if (math.distancesq(endEffector.position, target) < 0.001f)
                return;

            for (int it = 0; it < numberIterations; ++it)
            {
                for (int i = joints.Length - 2; i >= 0; --i)
                {
                    Transform joint = joints[i];
                    float weight = weights[i];
                    float3 eeJoint = endEffector.position - joint.position;
                    float3 targetJoint = target - (float3)joint.position;
                    float3 eeJointUnit = math.normalize(eeJoint);
                    float3 targetJointUnit = math.normalize(targetJoint);
                    quaternion rot = MathExtensions.FromToRotation(eeJointUnit, targetJointUnit);
                    rot = MathExtensions.ScaleRotation(rot, weight);  // Assuming ScaleRotation scales the quaternion angle by weight
                    joint.rotation = math.mul(rot, joint.rotation);
                    float3 rotatedAxis = math.mul(rot, rotAxis);
                    quaternion rotBack = MathExtensions.FromToRotation(rotatedAxis, rotAxis);
                    joint.rotation = math.mul(rotBack, joint.rotation);
                }
            }
        }
    }
}