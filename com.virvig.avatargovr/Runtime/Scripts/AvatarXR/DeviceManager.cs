using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace AvatarGoVR
{
    using InputDeviceTrackerCharacteristics = HTCViveTrackerProfile.InputDeviceTrackerCharacteristics;
    
    public class DeviceManager
    {
        public event Action OnDeviceChanged;

        private readonly List<string> DevicesName;
        private readonly List<Device> Devices;

        private bool Verbose;

        public DeviceManager()
        {
            DevicesName = new List<string>();
            Devices = new List<Device>();

            InputDevices.deviceConnected += OnDeviceConnected;
            InputDevices.deviceDisconnected += OnDeviceDisconnected;
        }

        public List<string> GetDevicesName()
        {
            return DevicesName;
        }

        public bool GetDevicePose(DeviceRole role, out Vector3 position, out Quaternion rotation)
        {
            Device device = GetDevice(role);
            if (device != null)
            {
                position = device.GetPosition();
                rotation = device.GetRotation();
                return true;
            }
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        /// <summary>
        /// Return whether trigger is available in this device. The actual trigger value is returned in the out parameter.
        /// </summary>
        public bool GetDeviceTrigger(DeviceRole role, out bool trigger)
        {
            Device device = GetDevice(role);
            if (device != null)
            {
                return device.GetTrigger(out trigger);
            }
            trigger = false;
            return false;
        }

        public bool IsDeviceAvailable(DeviceRole role)
        {
            return GetDevice(role) != null;
        }

        public int GetNumberUncalibratedDevices()
        {
            int numberUncalibratedDevices = 0;
            foreach (Device device in Devices)
            {
                if (!device.IsCalibrated)
                {
                    numberUncalibratedDevices++;
                }
            }
            return numberUncalibratedDevices;
        }

        /// <summary>
        /// Return true if all six devices were correctly identified, false otherwise.
        /// </summary>
        public bool CalibrateDevices(Transform[] skeleton, HumanBodyBones[] topology)
        {
            int numberDevices = Devices.Count;
            if (numberDevices == 6)
            {
                // Identify devices
                Span<Vector3> positions = stackalloc Vector3[6];
                Span<Quaternion> rotations = stackalloc Quaternion[6];
                Span<DeviceRole> roles = stackalloc DeviceRole[6];
                for (int i = 0; i < Devices.Count; ++i)
                {
                    positions[i] = Devices[i].GetPosition();
                    rotations[i] = Devices[i].GetRotation();
                    roles[i] = Devices[i].GetRole();
                }
                bool res = Utils.IdentifyDevicesSixConfiguration(in positions, in rotations, roles);
                if (!res)
                {
                    return false;
                }
                for (int i = 0; i < Devices.Count; ++i)
                {
                    Devices[i].SetRole(roles[i]);
                }
            }
            else
            {
                Debug.LogError("Calibration failed: number of devices is " + numberDevices + ". Currently, only 6 devices calibration is supported.");
                return false;
            }
            // Calibrate
            foreach (Device device in Devices)
            {
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

                Transform joint = null; 
                if (device.GetRole() == DeviceRole.Head) joint = SearchJoint(HumanBodyBones.Head);
                if (device.GetRole() == DeviceRole.LeftHand) joint = SearchJoint(HumanBodyBones.LeftHand);
                if (device.GetRole() == DeviceRole.RightHand) joint = SearchJoint(HumanBodyBones.RightHand);
                if (device.GetRole() == DeviceRole.LeftFoot) joint = SearchJoint(HumanBodyBones.LeftFoot);
                if (device.GetRole() == DeviceRole.RightFoot) joint = SearchJoint(HumanBodyBones.RightFoot);
                if (device.GetRole() == DeviceRole.Hips) joint = SearchJoint(HumanBodyBones.Hips);
                Debug.Assert(false, "Joint not found");

                device.Calibrate(joint);
            }
            return true;
        }

        private void OnDeviceConnected(InputDevice device)
        {
            DeviceXR d = new DeviceXR(device);
            d.FindRole();
            if (d.GetRole() == DeviceRole.Undefined)
            {
                // Check if this device is a tracker and disable if it is not left/right feet or hips
                // TODO: this can be changed when Unity incorporates the OpenXR extension XR_HTCX_vive_tracker_interaction
                //       to detect when trackers are enabled/disabled (right now all 12 types of trackers are enabled even when not used)
                //       Check the OpenXR package changelog to see when this extension is added.
                if (device.name == "HTC Vive Tracker OpenXR")
                {
                    var trackerCharacteristics = (InputDeviceCharacteristics)InputDeviceTrackerCharacteristics.TrackerLeftFoot |
                                                 (InputDeviceCharacteristics)InputDeviceTrackerCharacteristics.TrackerRightFoot |
                                                 (InputDeviceCharacteristics)InputDeviceTrackerCharacteristics.TrackerWaist;
                    if ((device.characteristics & trackerCharacteristics) == 0)
                    {
                        return; // Tracker is not left/right foot or hips, so don't add it
                    }
                }
            }
            Debug.Log("Device connected: " + device.name + " (" + d.GetRole() + ") ");
            if (Verbose) d.PrintCharacteristics();
            Devices.Add(d);
            DevicesName.Add(device.name);
            InvokeOnDeviceChanged();
        }

        private void OnDeviceDisconnected(InputDevice device)
        {
            Debug.Log("Device disconnected: " + device.name);
            Devices.Remove(new DeviceXR(device));
            DevicesName.Remove(device.name);
            InvokeOnDeviceChanged();
        }

        public void ConnectSimulatedDevice(DeviceRole role, SimulatedXRDevice deviceTransform)
        {
            Debug.Log("Simulated device connected: " + deviceTransform.name + " (" + role + ")");
            DeviceSimulated device = new DeviceSimulated(role, deviceTransform);
            Devices.Add(device);
            DevicesName.Add("[Simulated] " + deviceTransform.name);
            InvokeOnDeviceChanged();
        }

        public void DisconnectSimulatedDevice(SimulatedXRDevice deviceTransform)
        {
            Debug.Log("Simulated device disconnected: " + deviceTransform.name);
            Devices.Remove(new DeviceSimulated(deviceTransform.Role, deviceTransform));
            DevicesName.Remove("[Simulated] " + deviceTransform.name);
            InvokeOnDeviceChanged();
        }

        public void SetVerbose(bool isVerbose)
        {
            Verbose = isVerbose;
        }

        private void InvokeOnDeviceChanged()
        {
            if (OnDeviceChanged != null) OnDeviceChanged.Invoke();
        }
        
        private Device GetDevice(DeviceRole role)
        {
            foreach (Device d in Devices)
            {
                if (d.GetRole() == role)
                {
                    return d;
                }
            }
            return null;
        }
        
        private abstract class Device : IEquatable<Device>
        {
            protected DeviceRole Role;

            private Vector3 LocalOffset;
            private Quaternion InvInitialRot;

            public Device(DeviceRole role)
            {
                Role = role;
                LocalOffset = Vector3.zero;
                InvInitialRot = Quaternion.identity;
            }

            public bool IsCalibrated { get { return Role != DeviceRole.Undefined; } }

            public Vector3 GetPosition()
            {
                return GetRawRotation() * LocalOffset + GetRawPosition();
            }
            public Quaternion GetRotation()
            {
                return GetRawRotation() * InvInitialRot;
            }
            protected abstract Vector3 GetRawPosition();
            protected abstract Quaternion GetRawRotation();

            /// <summary>
            /// Return whether trigger is available in this device. The actual trigger value is returned in the out parameter.
            /// </summary>
            public abstract bool GetTrigger(out bool value);

            public void Calibrate(Transform joint)
            {
                InvInitialRot = Quaternion.Inverse(GetRawRotation());
                LocalOffset = InvInitialRot * (joint.position - GetRawPosition());
            }

            public virtual DeviceRole GetRole()
            {
                return Role;
            }
            public virtual void SetRole(DeviceRole newRole)
            {
                Role = newRole;
            }
            
            public virtual bool Equals(Device other)
            {
                return other.Role == Role;
            }
        }

        private class DeviceXR : Device, IEquatable<DeviceXR>
        {
            private readonly InputDevice InputDevice;

            public DeviceXR(InputDevice inputDevice) : base(DeviceRole.Undefined)
            {
                InputDevice = inputDevice;
            }

            protected override Vector3 GetRawPosition()
            {
                if (InputDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
                {
                    return position;
                }
                return Vector3.zero;
            }

            protected override Quaternion GetRawRotation()
            {
                if (InputDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
                {
                    return rotation;
                }
                return Quaternion.identity;
            }

            public void FindRole()
            {
                var headCharacteristics = InputDeviceCharacteristics.HeadMounted;
                var leftHandCharacteristics = InputDeviceCharacteristics.Left | InputDeviceCharacteristics.HeldInHand;
                var rightHandCharacteristics = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.HeldInHand;
                if ((InputDevice.characteristics & headCharacteristics) == headCharacteristics)
                {
                    Role = DeviceRole.Head;
                }
                else if ((InputDevice.characteristics & leftHandCharacteristics) == leftHandCharacteristics)
                {
                    Role = DeviceRole.LeftHand;
                }
                else if ((InputDevice.characteristics & rightHandCharacteristics) == rightHandCharacteristics)
                {
                    Role = DeviceRole.RightHand;
                }
            }

            public bool Equals(DeviceXR other)
            {
                return InputDevice.Equals(other);
            }
            public override bool Equals(Device other)
            {
                if (other is not DeviceXR)
                {
                    return false;
                }
                return InputDevice.Equals(((DeviceXR)other).InputDevice);
            }

            public void PrintCharacteristics()
            {
                string characteristics = "";
                foreach (InputDeviceCharacteristics c in Enum.GetValues(typeof(InputDeviceCharacteristics)))
                {
                    if ((InputDevice.characteristics & c) == c)
                    {
                        characteristics += c + ", ";
                    }
                }
                foreach (InputDeviceTrackerCharacteristics c in Enum.GetValues(typeof(InputDeviceTrackerCharacteristics)))
                {
                    if ((InputDevice.characteristics & (InputDeviceCharacteristics)c) == (InputDeviceCharacteristics)c)
                    {
                        characteristics += c + ", ";
                    }
                }
                Debug.Log("Characteristics: " + characteristics);
            }

            public override bool GetTrigger(out bool value)
            {
                return InputDevice.TryGetFeatureValue(CommonUsages.triggerButton, out value);
            }
        }

        private class DeviceSimulated : Device, IEquatable<DeviceSimulated>
        {
            private readonly SimulatedXRDevice Device;

            public DeviceSimulated(DeviceRole role, SimulatedXRDevice device) : base(role)
            {
                Device = device;
            }

            protected override Vector3 GetRawPosition()
            {
                return Device.transform.position;
            }

            protected override Quaternion GetRawRotation()
            {
                return Device.transform.rotation;
            }

            public bool Equals(DeviceSimulated other)
            {
                return Device == other.Device;
            }

            public override bool GetTrigger(out bool value)
            {
                value = Device.Trigger;
                Device.Trigger = false;
                return true;
            }

            public override void SetRole(DeviceRole newRole)
            {
                base.SetRole(newRole);
                Device.Role = newRole;
            }
        }

        public enum DeviceRole
        {
            Head,
            LeftHand,
            RightHand,
            Hips,
            LeftFoot,
            RightFoot,
            Undefined
        }
    }
}