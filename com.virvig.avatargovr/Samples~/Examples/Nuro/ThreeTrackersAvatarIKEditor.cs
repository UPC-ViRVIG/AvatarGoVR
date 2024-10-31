using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace AvatarGoVR
{
    [CustomEditor(typeof(ThreeTrackersAvatarIK))]
    public class ThreeTrackersAvatarIKEditor : Editor
    {
        private SetDefaultPoseState SetDefaultPose;

        public override void OnInspectorGUI()
        {
            ThreeTrackersAvatarIK fbAvatar = (ThreeTrackersAvatarIK)target;
            Transform hips = fbAvatar.GetHipsTransform();
            if (hips != null)
            {
                if (SetDefaultPose == SetDefaultPoseState.None)
                {
                    base.OnInspectorGUI();
                    EditorGUILayout.Space();
                    if (!fbAvatar.IsDefaultPoseSet())
                    {
                        EditorGUILayout.HelpBox("Default pose is not set. Please set it in the inspector.", MessageType.Error);
                    }
                    if (GUILayout.Button("Set Default Pose"))
                    {
                        SetDefaultPose = SetDefaultPoseState.OrientAvatar;
                    }
                }
                else if (SetDefaultPose == SetDefaultPoseState.OrientAvatar)
                {
                    EditorGUILayout.HelpBox("Orient the avatar as follows: (1) the hips are facing towards the forward world vector; (2) the avatar is upright towards the up world vector.", MessageType.Info);
                    if (GUILayout.Button("Set Default Pose"))
                    {
                        fbAvatar.ComputeLocalAxes();
                        SetDefaultPose = SetDefaultPoseState.None;
                    }
                    else if (GUILayout.Button("Cancel"))
                    {
                        SetDefaultPose = SetDefaultPoseState.None;
                    }
                }
            }
        }

        public void OnSceneGUI()
        {
            ThreeTrackersAvatarIK fbAvatar = (ThreeTrackersAvatarIK)target;

            if (!fbAvatar.IsDefaultPoseSet()) return;

            Transform hips = fbAvatar.GetHipsTransform();
            if (hips != null)
            {
                float scale = 0.25f;
                Transform head = fbAvatar.GetHeadTransform();
                if (head != null)
                {
                    scale = math.length(head.position - hips.position) * 0.1f;
                }
                for (int i = 0; i < fbAvatar.Joints.Length; ++i)
                {
                    Transform joint = fbAvatar.Joints[i].Transform;
                    if (joint != null)
                    {
                        fbAvatar.GetJointWorldAxes(i, out float3 jointForward, out float3 jointUp, out float3 jointRight);
                        const float thickness = 5.0f;
                        Handles.color = new Color(0.0f, 0.0f, 1.0f, 1.0f);
                        Handles.DrawLine(joint.position, (float3)joint.position + jointForward * scale, thickness * scale);
                        Handles.color = new Color(0.0f, 1.0f, 0.0f, 1.0f);
                        Handles.DrawLine(joint.position, (float3)joint.position + jointUp * scale, thickness * scale);
                        Handles.color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
                        Handles.DrawLine(joint.position, (float3)joint.position + jointRight * scale, thickness * scale);
                        Handles.color = Color.white;
                        Handles.DrawSolidDisc(joint.position, Camera.current.transform.forward, scale * 0.1f);
                    }
                }
            }
        }

        private enum SetDefaultPoseState
        {
            None,
            OrientAvatar
        }
    }
}