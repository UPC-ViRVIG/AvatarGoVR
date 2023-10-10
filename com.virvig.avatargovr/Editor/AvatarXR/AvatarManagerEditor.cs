using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarGoVR
{
    [CustomEditor(typeof(AvatarManager))]
    public class AvatarManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            AvatarManager avatarManager = (AvatarManager)target;

            EditorGUILayout.LabelField("--- Debug ---", EditorStyles.boldLabel);
            bool verbose = avatarManager.GetVerbose();
            bool newVerbose = EditorGUILayout.Toggle("Verbose", verbose);
            if (verbose != newVerbose)
                avatarManager.SetVerbose(newVerbose);

            if (Application.isPlaying && avatarManager.GetCurrentAnimationProvider() == null)
            {
                EditorGUILayout.HelpBox("No animation provider is compatible with the current devices configuration and the selected avatar.", MessageType.Warning);
            }

            DeviceManager deviceManager = avatarManager.GetDeviceManager();
            if (deviceManager != null)
            {
                EditorGUILayout.LabelField("--- Devices ---", EditorStyles.boldLabel);
                List<string> devices = deviceManager.GetDevicesName();
                for (int i = 0; i < devices.Count; ++i)
                {
                    EditorGUILayout.LabelField(devices[i]);
                }
            }
        }
    }
}
