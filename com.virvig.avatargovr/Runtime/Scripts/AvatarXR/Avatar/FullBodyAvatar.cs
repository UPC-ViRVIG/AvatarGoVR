using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace AvatarGoVR
{
    [DefaultExecutionOrder(10000)]
    [RequireComponent(typeof(Animator))]
    public class FullBodyAvatar : MonoBehaviour, IAvatar
    {
        public Joint[] Joints;

        [Header("Head Clipping")]
        [Tooltip("Center of the head used by the clipping shader.")]
        public Vector3 HeadCenter = new Vector3(0.0f, 0.0f, 0.0f);
        [Tooltip("Radius of the head used by the clipping shader.")]
        public float HeadRadius = 0.2f;

        [Header("Avatar Scaling")]
        [Tooltip("Height of the eyes from the floor. Used to scale the avatar.")]
        public float EyesHeight = 1.625f;

        [Header("Settings")]
        public bool DoArmStretch = true;

        [HideInInspector]
        [SerializeField]
        private quaternion[] DefaultPose;

        private Animator Animator;
        private ArmStretch ArmStretch;
        private Transform HeadJoint;
        private List<Material> Materials;
        private int ClippingSpherePositionKey;
        private int ClippingSphereRadiusKey;
        private bool IsCalibrated = false;

        private void Awake()
        {
            // Check Materials
            ClippingSpherePositionKey = Shader.PropertyToID("_ClippingSpherePosition");
            ClippingSphereRadiusKey = Shader.PropertyToID("_ClippingSphereRadius");
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            Materials = new List<Material>();
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.materials)
                {
                    if (material.shader.name.Contains("AvatarShader"))
                    {
                        Materials.Add(material);
                    }
                    else
                    {
                        Debug.LogWarning("Material " + material.name + " does not have the required properties to perform head clipping. Make sure to assign the shader 'AvatarShader' or 'AvatarTransparentShader' to the material.");
                    }
                }
            }
            Animator = GetComponent<Animator>();
            Debug.Assert(Animator != null, "FullBodyAvatar requires an Animator component.");
            ArmStretch = new ArmStretch(Animator);
        }

        public void OnCalibrate()
        {
            // Calibrate Avatar Height
            if (AvatarManager.Instance.GetDeviceManager().GetDevicePose(DeviceManager.DeviceRole.Head, out Vector3 eyesPos, out _))
            {
                float heightScale = eyesPos.y / EyesHeight;
                transform.localScale = new Vector3(heightScale, heightScale, heightScale);
                IsCalibrated = true;
            }
        }

        public void ResetCalibration()
        {
            transform.localScale = Vector3.one;
            IsCalibrated = false;
        }

        public bool ShouldCalibrateHeight()
        {
            return !IsCalibrated;
        }

        public quaternion[] GetDefaultPose()
        {
            return DefaultPose;
        }

        public Transform[] GetSkeleton()
        {
            Transform[] skeleton = new Transform[Joints.Length];
            for (int i = 0; i < Joints.Length; i++)
            {
                skeleton[i] = Joints[i].Transform;
            }
            return skeleton;
        }

        public HumanBodyBones[] GetTopology()
        {
            HumanBodyBones[] topology = new HumanBodyBones[Joints.Length];
            for (int i = 0; i < Joints.Length; i++)
            {
                topology[i] = Joints[i].HumanBodyBone;
            }
            return topology;
        }

        private void Update()
        {
            Vector3 headCenter = GetHeadCenter();
            // Materials
            foreach (Material material in Materials)
            {
                material.SetVector(ClippingSpherePositionKey, headCenter);
                material.SetFloat(ClippingSphereRadiusKey, HeadRadius);
            }
        }

        private void LateUpdate()
        {
            if (!IsCalibrated) return;

            // Arm Stretch
            if (DoArmStretch)
            {
                DeviceManager deviceManager = AvatarManager.Instance.GetDeviceManager();
                if (deviceManager.GetDevicePose(DeviceManager.DeviceRole.LeftHand, out Vector3 leftHandPos, out _))
                {
                    ArmStretch.StretchLeft(leftHandPos);
                }
                if (deviceManager.GetDevicePose(DeviceManager.DeviceRole.RightHand, out Vector3 rightHandPos, out _))
                {
                    ArmStretch.StretchRight(rightHandPos);
                }
            }
        }

        public Vector3 GetHeadCenter()
        {
            if (Animator == null || HeadJoint == null)
            {
                Animator = GetComponent<Animator>();
                HeadJoint = Animator.GetBoneTransform(HumanBodyBones.Head);
                if (HeadJoint == null)
                {
                    Debug.LogError("Animator has no reference to the head joint.");
                }
            }
            return HeadJoint.TransformPoint(HeadCenter);
        }

        private void Reset()
        {
            Joints = new Joint[]
            {
                new Joint(HumanBodyBones.Hips), // 0
                new Joint(HumanBodyBones.LeftUpperLeg), // 1
                new Joint(HumanBodyBones.LeftLowerLeg), // 2
                new Joint(HumanBodyBones.LeftFoot), // 3
                new Joint(HumanBodyBones.LeftToes), // 4
                new Joint(HumanBodyBones.RightUpperLeg), // 5
                new Joint(HumanBodyBones.RightLowerLeg), // 6
                new Joint(HumanBodyBones.RightFoot), // 7
                new Joint(HumanBodyBones.RightToes), // 8
                new Joint(HumanBodyBones.Spine), // 9
                new Joint(HumanBodyBones.Chest), // 10
                new Joint(HumanBodyBones.UpperChest), // 11
                new Joint(HumanBodyBones.Neck), // 12
                new Joint(HumanBodyBones.Head), // 13
                new Joint(HumanBodyBones.LeftShoulder), // 14
                new Joint(HumanBodyBones.LeftUpperArm), // 15
                new Joint(HumanBodyBones.LeftLowerArm), // 16
                new Joint(HumanBodyBones.LeftHand), // 17
                new Joint(HumanBodyBones.RightShoulder), // 18
                new Joint(HumanBodyBones.RightUpperArm), // 19
                new Joint(HumanBodyBones.RightLowerArm), // 20
                new Joint(HumanBodyBones.RightHand), // 21
            };
            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.avatar != null)
            {
                for (int i = 0; i < Joints.Length; ++i)
                {
                    Joints[i].Transform = animator.GetBoneTransform(Joints[i].HumanBodyBone);
                }
            }
        }

        public bool IsDefaultPoseSet()
        {
            return DefaultPose != null && DefaultPose.Length == Joints.Length;
        }

        public Transform GetJointTransform(int index)
        {
            return Joints[index].Transform;
        }
        public Transform GetHipsTransform()
        {
            return Joints[0].Transform;
        }
        public Transform GetHeadTransform()
        {
            return Joints[13].Transform;
        }
        [ContextMenu("Compute Local Axes")]
        public void ComputeLocalAxes()
        {
            DefaultPose = new quaternion[Joints.Length];
            for (int i = 0; i < Joints.Length; ++i)
            {
                if (Joints[i].Transform != null)
                {
                    DefaultPose[i] = Joints[i].Transform.rotation;
                }
            }
        }

        public void GetJointWorldAxes(int index, out float3 forward, out float3 up, out float3 right)
        {
            Debug.Assert(Joints[index].Transform != null, "Joint " + index + " is not set");
            forward = math.mul(math.mul(Joints[index].Transform.rotation, math.inverse(DefaultPose[index])), math.forward());
            up = math.mul(math.mul(Joints[index].Transform.rotation, math.inverse(DefaultPose[index])), math.up());
            right = math.mul(math.mul(Joints[index].Transform.rotation, math.inverse(DefaultPose[index])), math.right());
        }

        [System.Serializable]
        public struct Joint
        {
            public HumanBodyBones HumanBodyBone;
            public Transform Transform;

            public Joint(HumanBodyBones humanBodyBones) : this()
            {
                HumanBodyBone = humanBodyBones;
            }
        }


#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 headCenter = GetHeadCenter();
            Gizmos.DrawWireSphere(headCenter, HeadRadius);

            Gizmos.color = Color.blue;
            Vector3 eyes = headCenter + Vector3.forward * HeadRadius;
            eyes.y = EyesHeight;
            Gizmos.DrawSphere(eyes, 0.01f);
        }
#endif
    }
}