using UnityEngine;

namespace Nuro.VRWeb.Core.Avatar
{
    /// <summary>
    /// implement this interface to do you own Inverse Kinematics. Place the script at
    /// the root of the puppet (where it usually has its Animator)
    /// </summary>
    public interface IAvatarIK
    {
        /// <summary>
        /// return the avatar belonging to this puppet's skeleton
        /// </summary>
        public UnityEngine.Avatar avatar { get; }

        /// <summary>
        /// bind to this puppet. Note that the Animator parameter is NOT the Animator at
        /// the root of the Avatar puppet!
        /// </summary>
        /// <param name="avatarGameObject"></param>
        public void BindAvatarToIK(GameObject avatarGameObject, Animator animator);

        /// <summary>
        /// unbind from the puppet and enter passive state
        /// </summary>
        public void UnbindAvatarFromIK();

        /// <summary>
        /// Process position and orientation information. Will be called once
        /// per frame from Unity's OnAnimatorIK() function. Do NOT implement your
        /// own OnAnimatorIK!!
        /// </summary>
        /// <param name="ikInfo"></param>
        public void OnUpdateAvatarIK(Transform avatarTransform, IkInfo ikInfo);

        /// <summary>
        /// This will be called once after BindAvatarToIK() to allow for adjustments
        /// to the height and reach of the avatar puppet.
        /// </summary>
        /// <param name="ikInfo"></param>
        public void Calibrate(IkInfo ikInfo);

        /// <summary>
        /// IkInfo's values are local positions and rotations
        /// </summary>
        public class IkInfo
        {
            public Vector3 LeftHandPosition;
            public Quaternion LeftHandRotation;
            public Vector3 RightHandPosition;
            public Quaternion RightHandRotation;
            public Vector3 LeftFootPosition;
            public Quaternion LeftFootRotation;
            public Vector3 RightFootPosition;
            public Quaternion RightFootRotation;
            public Vector3 HipsPosition;
            public Quaternion HipsRotation;
            public Vector3 HeadPosition;
            public Quaternion HeadRotation;
        }
    }
}