using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AvatarGoVR
{
    public class ArmStretch
    {
        private const float MAX_STRETCH = 0.5f; // w.r.t. to the initial distance

        private readonly Transform RLowerArm;
        private readonly Transform RHand;
        private readonly Transform LLowerArm;
        private readonly Transform LHand;

        private readonly Vector3 RLowerArmLocalPos;
        private readonly Vector3 LLowerArmLocalPos;

        private readonly float RInitDist;
        private readonly float LInitDist;


        public ArmStretch(Animator animator)
        {
            RLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            RLowerArmLocalPos = RLowerArm.localPosition;
            RHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            RInitDist = Vector3.Distance(RLowerArm.position, RHand.position);
            LLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            LLowerArmLocalPos = LLowerArm.localPosition;
            LHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            LInitDist = Vector3.Distance(LLowerArm.position, LHand.position);
        }

        public void StretchRight(Vector3 rightHandTarget)
        {
            RLowerArm.localPosition = RLowerArmLocalPos;
            Vector3 dir = (RHand.position - RLowerArm.position).normalized;
            float dist = Vector3.Distance(RHand.position, rightHandTarget);
            dist = Mathf.Clamp(dist, 0.0f, RInitDist * MAX_STRETCH);
            RLowerArm.position += dir * dist;
        }

        public void StretchLeft(Vector3 leftHandTarget)
        {
            LLowerArm.localPosition = LLowerArmLocalPos;
            Vector3 dir = (LHand.position - LLowerArm.position).normalized;
            float dist = Vector3.Distance(LHand.position, leftHandTarget);
            dist = Mathf.Clamp(dist, 0.0f, LInitDist * MAX_STRETCH);
            LLowerArm.position += dir * dist;
        }
    }
}