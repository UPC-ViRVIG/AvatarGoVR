using AvatarGoVR;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MotionMatching;

[RequireComponent(typeof(MotionMatchingSkinnedMeshRenderer))]
public class FullBodyDataDrivenAvatar : FullBodyAvatar
{
    [Header("Data Driven")]
    public MotionMatchingData MMData;
    public MotionMatchingController.SquatDataset[] MMDataSquat;

    public Vector3 LeftHandRotationOffset;
    public Vector3 RightHandRotationOffset;
}
