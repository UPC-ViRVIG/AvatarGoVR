using UnityEngine;
using Nuro.VRWeb.Core.Avatar;
using Unity.Mathematics;
using MotionMatching;

namespace AvatarGoVR
{
    using Target = FBIK.FBIK.Target;
    using IkInfo = IAvatarIK.IkInfo;

    public class ThreeTrackersAvatarIK : MonoBehaviour, IAvatarIK
    {
        [Header("Data Driven")]
        public MotionMatchingData MMData;
        public MotionMatchingController.SquatDataset[] MMDataSquat;

        public Vector3 LeftHandRotationOffset;
        public Vector3 RightHandRotationOffset;

        public Joint[] Joints;

        [Header("Avatar Scaling")]
        [Tooltip("Height of the eyes from the floor. Used to scale the avatar.")]
        public float EyesHeight = 1.625f;

        private Avatar m_Avatar = null;
        private GameObject m_LastBoundAvatar = null;
        private Animator m_Animator;
        private bool m_LastStateAnimator;

        private FBIK.FBIK m_FBIK = new();
        [HideInInspector]
        [SerializeField]
        private quaternion[] m_DefaultPose;

        private bool m_DoUpdate = false;
        private bool m_WasCalibrated = false;
        private IkInfo m_IkInfo;

        private MotionMatchingController MotionMatchingController;
        private VRCharacterController VRCharacterController;
        private MotionMatchingSkinnedMeshRenderer MotionMatchingSkinnedMeshRenderer;

        public Avatar avatar => m_Avatar;

        public void BindAvatarToIK(GameObject avatarGameObject, Animator externalAnimator)
        {
            if (m_LastBoundAvatar != null)
                UnbindAvatarFromIK();

            m_Avatar = avatarGameObject.GetComponent<Animator>().avatar;
            externalAnimator.avatar = m_Avatar;

            m_LastBoundAvatar = avatarGameObject;
            m_Animator = externalAnimator;

            if (!IsDefaultPoseSet())
                throw new System.Exception("Default pose is not set. Please set it in the inspector.");

            m_LastStateAnimator = m_Animator.enabled;
            m_Animator.enabled = false;
        }
        public void UnbindAvatarFromIK()
        {
            m_Animator.enabled = m_LastStateAnimator;
            m_LastBoundAvatar = null;
            m_Animator = null;
        }

        public void Calibrate(IkInfo ikInfo)
        {
            // Calibrate Avatar Height
            float heightScale = ikInfo.HeadPosition.y / EyesHeight;
            transform.localScale = new Vector3(heightScale, heightScale, heightScale);

            MotionMatchingController = new MotionMatchingController
            {
                MMData = MMData,
                EyesHeight = EyesHeight,
                SquatDatasets = MMDataSquat
            };
            MotionMatchingController.Init();
            VRCharacterController = new VRCharacterController
            {
                SimulationBone = MotionMatchingController,
                MaxDistanceSimulationBoneAndObject = 0.15f
            };
            VRCharacterController.Init();
            MotionMatchingController.CharacterController = VRCharacterController;
            MotionMatchingController.Enable();
            MotionMatchingSkinnedMeshRenderer = GetComponent<MotionMatchingSkinnedMeshRenderer>();
            MotionMatchingSkinnedMeshRenderer.MotionMatching = MotionMatchingController;
            MotionMatchingSkinnedMeshRenderer.Enable();
            MotionMatchingSkinnedMeshRenderer.Init();

            // Set every time because Motion Matching Skinned Mesh Renderer is a MonoBehaviour shared by different animation providers
            if (MotionMatchingSkinnedMeshRenderer.MotionMatching == null ||
                MotionMatchingSkinnedMeshRenderer.MotionMatching != MotionMatchingController)
            {
                MotionMatchingSkinnedMeshRenderer.MotionMatching = MotionMatchingController;
                MotionMatchingSkinnedMeshRenderer.Enable();
            }

            float3 bodyOffset = new float3(0.0f, -0.15f, -0.15f);
            float3 bodyCenter = (float3)ikInfo.HeadPosition + math.mul(ikInfo.HeadRotation, bodyOffset);
            VRCharacterController.Update(bodyCenter, ikInfo.HeadRotation);

            Transform[] skeleton = GetSkeleton();

            m_FBIK.Init(skeleton, m_DefaultPose);
            m_WasCalibrated = true;
        }

        public void OnUpdateAvatarIK(Transform avatarTransform, IkInfo ikInfo)
        {
            if (m_Animator == null)
            {
                return; // forgot to call BindAvatarToIK()
            }

            if (!m_WasCalibrated)
            {
                return;
            }

            m_DoUpdate = true;
            m_IkInfo = ikInfo;
        }

        private void LateUpdate()
        {
            if (!m_DoUpdate)
            {
                return;
            }
            m_DoUpdate = false;

            // Set every time because Motion Matching Skinned Mesh Renderer is a MonoBehaviour shared by different animation providers
            if (MotionMatchingSkinnedMeshRenderer.MotionMatching == null ||
                MotionMatchingSkinnedMeshRenderer.MotionMatching != MotionMatchingController)
            {
                MotionMatchingSkinnedMeshRenderer.MotionMatching = MotionMatchingController;
                MotionMatchingSkinnedMeshRenderer.Enable();
            }

            MotionMatchingController.CurrentHeadHeight = m_IkInfo.HeadPosition.y;

            float3 bodyOffset = new float3(0.0f, -0.15f, -0.15f);
            float3 bodyCenter = (float3)m_IkInfo.HeadPosition + math.mul(m_IkInfo.HeadRotation, bodyOffset);
            VRCharacterController.Update(bodyCenter, m_IkInfo.HeadRotation);

            Vector3 headPos = (float3)m_IkInfo.HeadPosition + math.mul(m_IkInfo.HeadRotation, new float3(bodyOffset.x, 0.0f, bodyOffset.z));
            Quaternion lHandRot = math.mul((quaternion)m_IkInfo.LeftHandRotation, quaternion.EulerXYZ(math.radians(LeftHandRotationOffset)));
            Quaternion rHandRot = math.mul((quaternion)m_IkInfo.RightHandRotation, quaternion.EulerXYZ(math.radians(RightHandRotationOffset)));
            m_FBIK.Solve(new Target(headPos, m_IkInfo.HeadRotation),
                         new Target(Joints[0].Transform.position, Joints[0].Transform.rotation),
                         new Target(m_IkInfo.LeftHandPosition, lHandRot),
                         new Target(m_IkInfo.RightHandPosition, rHandRot));
        }

        public Transform GetHipsTransform()
        {
            if (Joints == null || Joints.Length == 0)
                return null;
            return Joints[0].Transform;
        }
        public Transform GetHeadTransform()
        {
            if (Joints == null || Joints.Length == 0)
                return null;
            return Joints[13].Transform;
        }

        public void ComputeLocalAxes()
        {
            m_DefaultPose = new quaternion[Joints.Length];
            for (int i = 0; i < Joints.Length; ++i)
            {
                if (Joints[i].Transform != null)
                {
                    m_DefaultPose[i] = Joints[i].Transform.rotation;
                }
            }
        }
        public void GetJointWorldAxes(int index, out float3 forward, out float3 up, out float3 right)
        {
            Debug.Assert(Joints[index].Transform != null, "Joint " + index + " is not set");
            forward = math.mul(math.mul(Joints[index].Transform.rotation, math.inverse(m_DefaultPose[index])), math.forward());
            up = math.mul(math.mul(Joints[index].Transform.rotation, math.inverse(m_DefaultPose[index])), math.up());
            right = math.mul(math.mul(Joints[index].Transform.rotation, math.inverse(m_DefaultPose[index])), math.right());
        }

        public bool IsDefaultPoseSet()
        {
            return m_DefaultPose != null && m_DefaultPose.Length == Joints.Length;
        }

        private Transform[] GetSkeleton()
        {
            Transform[] skeleton = new Transform[Joints.Length];
            for (int i = 0; i < Joints.Length; i++)
            {
                skeleton[i] = Joints[i].Transform;
            }
            return skeleton;
        }

        public void Dispose()
        {
            if (MotionMatchingController != null)
            {
                MotionMatchingController.Dispose();
                MotionMatchingController = null;
            }
        }

        private void OnDestroy()
        {
            Dispose();
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Vector3 eyes = transform.position + transform.forward * 0.3f;
            eyes.y = EyesHeight;
            Gizmos.DrawSphere(eyes, 0.01f);
        }
#endif
    }
}