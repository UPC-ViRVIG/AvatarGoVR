using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AvatarGoVR
{
    using DeviceRole = AvatarGoVR.DeviceManager.DeviceRole;

    public class AvatarManager : MonoBehaviour
    {
        private static AvatarManager instance;
        public static AvatarManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<AvatarManager>();
                    if (instance == null)
                    {
                        Debug.Assert(false, "AvatarManager not found. There should be at least one AvatarManager in the scene.");
                    }
                    instance.Init();
                }
                return instance;
            }
        }

        [Tooltip("List (with priority order) of avatars that can be animated. The first avatar to match with the current device configuration will be used. Each GameObject should have an IAvatar component.")]
        public List<GameObject> AvatarList;
        [Delayed]
        public string[] AnimationProvidersClasses;

        public UnityEvent OnAvatarStartAnimation;
        public UnityEvent OnAvatarStopAnimation;
        

        private DeviceManager DeviceManager;
        private List<IAnimationProvider> AnimationProviders;
        private IAnimationProvider CurrentAnimationProvider;
        private bool ShouldCalibrate;
        private bool ShouldCalibrateDevices;
        private Transform[] Skeleton;
        private HumanBodyBones[] Topology;
        private IAvatar AvatarComponent;
        [HideInInspector] [SerializeField] private bool Verbose;
        private bool HasCalibrationStarted;
        private Canvas TextCanvas;
        private Text Text;
        private bool WasAvatarAnimated;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                Init();
            }
            else if (instance != this)
            {
                Debug.Assert(false, "There should be only one AvatarManager in the scene.");
            }
        }

        private void Init()
        {
            // UI
            TextCanvas = GetComponentInChildren<Canvas>();
            if (TextCanvas == null)
            {
                TextCanvas = new GameObject("TextCanvas").AddComponent<Canvas>();
                TextCanvas.transform.SetParent(transform);
                TextCanvas.transform.localPosition = Vector3.zero;
                TextCanvas.transform.localRotation = Quaternion.identity;
                TextCanvas.transform.localScale = Vector3.one * 0.01f;
                TextCanvas.renderMode = RenderMode.WorldSpace;
                RectTransform rectTransform = TextCanvas.GetComponent<RectTransform>();
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1920);
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1080);
            }
            Text = TextCanvas.GetComponentInChildren<Text>();
            if (Text == null)
            {
                Text = new GameObject("Text").AddComponent<Text>();
                Text.transform.SetParent(TextCanvas.transform);
                Text.transform.localPosition = Vector3.zero;
                Text.transform.localRotation = Quaternion.identity;
                Text.transform.localScale = Vector3.one;
                Text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                Text.alignment = TextAnchor.MiddleCenter;
                Text.color = Color.black;
            }
            TextCanvas.enabled = false;
            // Device Manager
            DeviceManager = new DeviceManager();
            DeviceManager.SetVerbose(Verbose);
            // Animation Providers
            CreateAnimationProviders();
            // Events
            SuscribeEvents();
        }

        private void OnEnable()
        {
            SuscribeEvents();
        }
        private void OnDisable()
        {
            UnsuscribeEvents();
        }

        private void LateUpdate()
        {
            if (CurrentAnimationProvider != null)
            {
                if (ShouldCalibrate)
                {
                    if (!HasCalibrationStarted)
                    {
                        StartCalibration();
                    }
                    DeviceManager.GetDevicePose(DeviceRole.Head, out Vector3 headPos, out Quaternion headRot);
                    bool areHandDevices = DeviceManager.IsDeviceAvailable(DeviceRole.RightHand) || DeviceManager.IsDeviceAvailable(DeviceRole.LeftHand);
                    bool calibrate;
                    if (areHandDevices)
                    {
                        calibrate = (DeviceManager.GetDeviceTrigger(DeviceRole.RightHand, out bool rightTrigger) && rightTrigger) ||
                                    (DeviceManager.GetDeviceTrigger(DeviceRole.LeftHand, out bool leftTrigger) && leftTrigger);
                    }
                    else
                    {
                        calibrate = headPos.y > 1.55f && Vector3.Dot(headRot * Vector3.up, Vector3.up) > 0.9f;
                    }
                    if (calibrate)
                    {
                        AvatarComponent.OnCalibrate();
                        if (!ShouldCalibrateDevices || DeviceManager.CalibrateDevices(Skeleton, Topology))
                        {
                            StopCalibration();
                        }
                    }
                    TextCanvas.transform.position = headPos + headRot * (Vector3.forward * 2);
                    TextCanvas.transform.rotation = headRot;
                }
                else
                {
                    CurrentAnimationProvider.SetAnimation(Skeleton, Topology,
                                                          AvatarComponent.GetDefaultPose(),
                                                          DeviceManager);

                    if (!WasAvatarAnimated)
                    {
                        if (OnAvatarStartAnimation != null) OnAvatarStartAnimation.Invoke();
                        WasAvatarAnimated = true;
                    }

                    return; // Successfully animated...
                }
            }
            if (WasAvatarAnimated)
            {
                if (OnAvatarStopAnimation != null) OnAvatarStopAnimation.Invoke();
                WasAvatarAnimated = false;
            }
        }

        public DeviceManager GetDeviceManager()
        {
            return DeviceManager;
        }
        public IAnimationProvider GetCurrentAnimationProvider()
        {
            return CurrentAnimationProvider;
        }

        public bool GetVerbose()
        {
            return Verbose;
        }
        public void SetVerbose(bool isVerbose)
        {
            Verbose = isVerbose;
            if (DeviceManager != null)
                DeviceManager.SetVerbose(Verbose);
        }

        private void SuscribeEvents()
        {
            DeviceManager.OnDeviceChanged -= OnDeviceChanged;
            DeviceManager.OnDeviceChanged += OnDeviceChanged;
        }

        private void UnsuscribeEvents()
        {
            DeviceManager.OnDeviceChanged -= OnDeviceChanged;
        }

        private void OnDeviceChanged()
        {
            foreach (GameObject avatar in AvatarList)
            {
                DisableAvatar(avatar);
            }
            SelectAvatarAndAnimationProvider();
        }

        private void SelectAvatarAndAnimationProvider()
        {
            if (AnimationProviders == null) return;

            foreach (GameObject avatar in AvatarList)
            {
                EnableAvatar(avatar);
                CurrentAnimationProvider = null;
                foreach (IAnimationProvider provider in AnimationProviders)
                {
                    IAnimationProvider.Compatibility compatibility = provider.IsCompatible(DeviceManager, AvatarComponent);
                    if (compatibility == IAnimationProvider.Compatibility.COMPATIBLE ||
                        compatibility == IAnimationProvider.Compatibility.CALIBRATION_REQUIRED)
                    {
                        CurrentAnimationProvider = provider;
                        Debug.Log("Selected animation provider: " + provider.GetType().Name);
                        ShouldCalibrateDevices = compatibility == IAnimationProvider.Compatibility.CALIBRATION_REQUIRED;
                        ShouldCalibrate = ShouldCalibrateDevices || AvatarComponent.ShouldCalibrateHeight();
                        return;
                    }
                }
                DisableAvatar(avatar);
            }
        }

        private void CreateAnimationProviders()
        {
            AnimationProviders = new List<IAnimationProvider>();
            foreach (string className in AnimationProvidersClasses)
            {
                System.Type type = System.Type.GetType(className);
                if (typeof(IAnimationProvider).IsAssignableFrom(type))
                {
                    IAnimationProvider newProvider = (IAnimationProvider)System.Activator.CreateInstance(type);
                    AnimationProviders.Add(newProvider);
                }
                else
                {
                    Debug.Assert(false, "Class " + className + " does not implement the 'IAnimationProvider' interface");
                }
            }
            Debug.Assert(AnimationProviders.Count > 0, "No animation providers were created");
        }
        
        private void EnableAvatar(GameObject avatar)
        {
            avatar.SetActive(true);
            AvatarComponent = avatar.GetComponent<IAvatar>();
            Debug.Assert(AvatarComponent != null, "Avatar GameObject does not have a component implementing the 'IAvatar' interface");
            Skeleton = AvatarComponent.GetSkeleton();
            Topology = AvatarComponent.GetTopology();
            Debug.Assert(Skeleton.Length == Topology.Length, "The number of bones in the skeleton does not match the number of bones in the topology");
        }

        private void DisableAvatar(GameObject avatar)
        {
            AvatarComponent = null;
            Skeleton = null;
            Topology = null;
            avatar.SetActive(false);
        }

        private void StartCalibration()
        {
            TextCanvas.enabled = true;
            if (ShouldCalibrateDevices)
            {
                Text.text = "Enter the avatar to calibrate the devices. Press 'Trigger' when ready.";
            }
            else
            {
                Text.text = "Please stand upright while calibrating your height. Press 'Trigger' when ready.";
            }
            HasCalibrationStarted = true;
        }

        private void StopCalibration()
        {
            TextCanvas.enabled = false;
            HasCalibrationStarted = false;
            SelectAvatarAndAnimationProvider();
        }

        private void OnDestroy()
        {
            if (AnimationProviders != null)
            {
                foreach (IAnimationProvider provider in AnimationProviders)
                {
                    provider.Dispose();
                }
                AnimationProviders = null;
            }
        }

        private void OnApplicationQuit()
        {
            if (AnimationProviders != null)
            {
                foreach (IAnimationProvider provider in AnimationProviders)
                {
                    provider.Dispose();
                }
                AnimationProviders = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (AvatarList != null)
            {
                foreach (GameObject avatar in AvatarList)
                {
                    Debug.Assert(avatar.GetComponent<IAvatar>() != null, "Avatar GameObject does not have a component implementing the 'IAvatar' interface");
                }
            }
            if (AnimationProvidersClasses != null)
            {
                foreach (string className in AnimationProvidersClasses)
                {
                    System.Type type = System.Type.GetType(className);
                    Debug.Assert(type != null, "Class " + className + " does not exist");
                    Debug.Assert(typeof(IAnimationProvider).IsAssignableFrom(type), "Class " + className + " does not implement the 'IAnimationProvider' interface");
                }
            }
        }
#endif
    }
}