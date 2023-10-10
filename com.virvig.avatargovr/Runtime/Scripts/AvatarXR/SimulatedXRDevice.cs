using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AvatarGoVR
{
    using DeviceRole = DeviceManager.DeviceRole;

    public class SimulatedXRDevice : MonoBehaviour
    {
        public DeviceRole Role;
        public bool Trigger;

        private bool IsConnected;

        private void OnEnable()
        {
            if (!IsConnected && AvatarManager.Instance != null)
            {
                AvatarManager.Instance.GetDeviceManager().ConnectSimulatedDevice(Role, this);
                IsConnected = true;
            }
        }
        
        private void OnDisable()
        {
            if (IsConnected && AvatarManager.Instance != null)
            {
                AvatarManager.Instance.GetDeviceManager().DisconnectSimulatedDevice(this);
                IsConnected = false;
            }
        }

        private void OnApplicationQuit()
        {
            if (IsConnected && AvatarManager.Instance != null)
            {
                AvatarManager.Instance.GetDeviceManager().DisconnectSimulatedDevice(this);
                IsConnected = false;
            }
        }
    }
}