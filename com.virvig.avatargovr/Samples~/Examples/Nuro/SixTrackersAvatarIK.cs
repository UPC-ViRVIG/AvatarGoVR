using UnityEngine;
using Nuro.VRWeb.Core.Avatar;
using Unity.Mathematics;

namespace AvatarGoVR
{
    using Target = FBIK.FBIK.Target;
    using IkInfo = IAvatarIK.IkInfo;

    public class SixTrackersAvatarIK : MonoBehaviour, IAvatarIK
    {
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

        private TrackerCalibration m_HeadCalibration;
        private TrackerCalibration m_HipsCalibration;
        private TrackerCalibration m_LeftHandCalibration;
        private TrackerCalibration m_RightHandCalibration;
        private TrackerCalibration m_LeftFootCalibration;
        private TrackerCalibration m_RightFootCalibration;

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
            m_WasCalibrated = false;
        }

        public void Calibrate(IkInfo ikInfo)
        {
            // Calibrate Avatar Height
            float heightScale = ikInfo.HeadPosition.y / EyesHeight;
            transform.localScale = new Vector3(heightScale, heightScale, heightScale);

            HumanBodyBones[] topology = GetTopology();
            Transform[] skeleton = GetSkeleton();

            Transform SearchJoint(HumanBodyBones role)
            {
                for (int i = 0; i < topology.Length; ++i)
                {
                    if (topology[i] == role)
                    {
                        return skeleton[i];
                    }
                }
                Debug.Assert(false, "Joint not found");
                return null;
            }

            TrackerCalibration CalibrateTracker(Vector3 jointPosition, Vector3 position, Quaternion rotation)
            {
                return new TrackerCalibration
                {
                    InvInitialRot = Quaternion.Inverse(rotation),
                    LocalOffset = Quaternion.Inverse(rotation) * (jointPosition - position)
                };
            }

            m_HeadCalibration = CalibrateTracker(SearchJoint(HumanBodyBones.Head).position, ikInfo.HeadPosition, ikInfo.HeadRotation);
            m_HipsCalibration = CalibrateTracker(SearchJoint(HumanBodyBones.Hips).position, ikInfo.HipsPosition, ikInfo.HipsRotation);
            m_LeftHandCalibration = CalibrateTracker(SearchJoint(HumanBodyBones.LeftHand).position, ikInfo.LeftHandPosition, ikInfo.LeftHandRotation);
            m_RightHandCalibration = CalibrateTracker(SearchJoint(HumanBodyBones.RightHand).position, ikInfo.RightHandPosition, ikInfo.RightHandRotation);
            m_LeftFootCalibration = CalibrateTracker(SearchJoint(HumanBodyBones.LeftFoot).position, ikInfo.LeftFootPosition, ikInfo.LeftFootRotation);
            m_RightFootCalibration = CalibrateTracker(SearchJoint(HumanBodyBones.RightFoot).position, ikInfo.RightFootPosition, ikInfo.RightFootRotation);

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

            void GetCalibrated(Vector3 inPosition, Quaternion inRotation, 
                               out Vector3 position, out Quaternion rotation,
                               TrackerCalibration calibration)
            {
                position = inRotation * calibration.LocalOffset + inPosition;
                rotation = inRotation * calibration.InvInitialRot;
            }

            GetCalibrated(m_IkInfo.HeadPosition, m_IkInfo.HeadRotation, 
                          out Vector3 headPosition, out Quaternion headRotation, m_HeadCalibration);
            GetCalibrated(m_IkInfo.HipsPosition, m_IkInfo.HipsRotation,
                          out Vector3 hipsPosition, out Quaternion hipsRotation, m_HipsCalibration);    
            GetCalibrated(m_IkInfo.LeftHandPosition, m_IkInfo.LeftHandRotation,
                          out Vector3 leftHandPosition, out Quaternion leftHandRotation, m_LeftHandCalibration);    
            GetCalibrated(m_IkInfo.RightHandPosition, m_IkInfo.RightHandRotation,
                          out Vector3 rightHandPosition, out Quaternion rightHandRotation, m_RightHandCalibration);
            GetCalibrated(m_IkInfo.LeftFootPosition, m_IkInfo.LeftFootRotation,
                          out Vector3 leftFootPosition, out Quaternion leftFootRotation, m_LeftFootCalibration);
            GetCalibrated(m_IkInfo.RightFootPosition, m_IkInfo.RightFootRotation,
                          out Vector3 rightFootPosition, out Quaternion rightFootRotation, m_RightFootCalibration);

            m_FBIK.Solve(new Target(headPosition, headRotation),
                         new Target(hipsPosition, hipsRotation),
                         new Target(leftHandPosition, leftHandRotation),
                         new Target(rightHandPosition, rightHandRotation),
                         new Target(leftFootPosition, leftFootRotation),
                         new Target(rightFootPosition, rightFootRotation));
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

        public HumanBodyBones[] GetTopology()
        {
            HumanBodyBones[] topology = new HumanBodyBones[Joints.Length];
            for (int i = 0; i < Joints.Length; i++)
            {
                topology[i] = Joints[i].HumanBodyBone;
            }
            return topology;
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

        private struct TrackerCalibration
        {
            public Quaternion InvInitialRot;
            public Vector3 LocalOffset;
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