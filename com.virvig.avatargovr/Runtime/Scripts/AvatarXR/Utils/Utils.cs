using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AvatarGoVR
{
    using DeviceRole = AvatarGoVR.DeviceManager.DeviceRole;

    public static class Utils
    {
        /// <summary>
        /// Return true if all six devices were correctly identified, false otherwise.
        /// </summary>
        public static bool IdentifyDevicesSixConfiguration(in Span<Vector3> points, in Span<Quaternion> rots, Span<DeviceRole> roles)
        {
            Debug.Assert(points.Length == 6, "points.Length == 6");
            Debug.Assert(roles.Length == 6, "roles.Length == 6");

            int headIndex = -1, leftHandIndex = -1, rightHandIndex = -1;
            for (int i = 0; i < roles.Length; ++i)
            {
                if (roles[i] == DeviceRole.Head)
                {
                    headIndex = i;
                }
                else if (roles[i] == DeviceRole.LeftHand)
                {
                    leftHandIndex = i;
                }
                else if (roles[i] == DeviceRole.RightHand)
                {
                    rightHandIndex = i;
                }
            }
            Debug.Assert(headIndex != -1, "roles does not contain a head");
            Debug.Assert(leftHandIndex != -1, "roles does not contain a left hand");
            Debug.Assert(rightHandIndex != -1, "roles does not contain a right hand");

            bool res = FitPlane(points.Length, points, out float a, out float b, out float c, out float d);
            if (!res)
            {
                return false;
            }
            Vector3 n = new Vector3(a, b, c);
            n = Vector3.Normalize(n);
            Vector3 f = rots[headIndex] * Vector3.forward;
            f = Vector3.Normalize(f);
            // Make sure plane points in the same direction as the HMD forward
            float deviation = Vector3.Dot(n, f);
            if (Mathf.Abs(deviation) < 0.5f)
            {
                return false;
            }
            if (deviation < 0.0f)
            {
                n = -n;
            }
            // Get a point on the plane
            Vector3 p = new Vector3(0.0f, 0.0f, -d / c);
            // Project points on the plane
            Span<Vector3> projectedPoints = stackalloc Vector3[points.Length];
            for (int i = 0; i < points.Length; ++i)
            {
                projectedPoints[i] = points[i] - Vector3.Dot(points[i] - p, n) * n;
            }
            // Build uv coordinate system
            Vector3 v = Vector3.up;
            Vector3 u = Vector3.Cross(v, n);
            float u0 = Vector3.Dot(u, projectedPoints[headIndex]);
            float v0 = Vector3.Dot(v, projectedPoints[headIndex]);
            // Get uv coordinates
            Span<Vector2> uv = stackalloc Vector2[points.Length];
            uv[headIndex] = Vector2.zero; // HMD is the origin of the uv space
            for (int i = 0; i < points.Length; ++i)
            {
                if (i == headIndex)
                {
                    continue;
                }
                float uCoord = Vector3.Dot(projectedPoints[i], u) - u0;
                float vCoord = Vector3.Dot(projectedPoints[i], v) - v0;
                uv[i] = new Vector2(uCoord, vCoord);
            }
            // Identify trackers according to uv coordinates
            // Hips
            int hipsIndex = -1;
            float minDist = float.MaxValue;
            for (int i = 0; i < points.Length; ++i)
            {
                if (i == headIndex || i == leftHandIndex || i == rightHandIndex)
                {
                    continue;
                }
                float headToTrackerDist = Mathf.Abs(uv[i].y);
                if (headToTrackerDist < minDist)
                {
                    hipsIndex = i;
                    minDist = headToTrackerDist;
                }
            }
            roles[hipsIndex] = DeviceRole.Hips;
            // Feet
            int leftFootIndex = -1, rightFootIndex = -1;
            for (int i = 0; i < points.Length; ++i)
            {
                if (i == headIndex || i == leftHandIndex || i == rightHandIndex || i == hipsIndex)
                {
                    continue;
                }
                if (uv[i].x < 0.0f)
                {
                    roles[i] = DeviceRole.LeftFoot;
                    leftFootIndex = i;
                }
                else
                {
                    roles[i] = DeviceRole.RightFoot;
                    rightFootIndex = i;
                }
            }
            return leftFootIndex != -1 && rightFootIndex != -1;
        }

        /// <summary>
        /// Fit least square errors plane to set of points
        /// </summary>
        public static bool FitPlane(int numPoints, in Span<Vector3> points, out float a, out float b, out float c, out float d)
        {
            a = b = c = d = 0;
            // Check input
            if (numPoints < 3)
            {
                return false;
            }

            // Compute the mean of the points
            Vector3 mean = new Vector3(0.0f, 0.0f, 0.0f);
            for (int i = 0; i < numPoints; ++i)
            {
                mean += points[i];
            }
            mean /= numPoints;

            // Compute the linear system matrix and vector elements
            float xxSum = 0.0f, xySum = 0.0f, xzSum = 0.0f, yySum = 0.0f, yzSum = 0.0f;
            for (int i = 0; i < numPoints; ++i)
            {
                Vector3 diff = points[i] - mean;
                xxSum += diff[0] * diff[0];
                xySum += diff[0] * diff[1];
                xzSum += diff[0] * diff[2];
                yySum += diff[1] * diff[1];
                yzSum += diff[1] * diff[2];
            }

            // Solve the linear system
            float det = xxSum * yySum - xySum * xySum;
            if (det != 0.0f)
            {
                // Compute the fitted plane
                a = (yySum * xzSum - xySum * yzSum) / det;
                b = (xxSum * yzSum - xySum * xzSum) / det;
                c = -1;
                d = -a * mean[0] - b * mean[1] + mean[2];
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}