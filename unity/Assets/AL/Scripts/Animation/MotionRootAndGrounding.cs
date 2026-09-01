using System;
using UnityEngine;

namespace AL.Motion
{
    public readonly struct MotionRootDelta
    {
        public MotionRootDelta(Vector3 position, float yawDegrees)
        {
            Position = position;
            YawDegrees = yawDegrees;
        }

        public Vector3 Position { get; }
        public float YawDegrees { get; }
    }

    public static class MotionRootPolicy
    {
        public static MotionRootDelta Resolve(
            MotionRootMode mode,
            Vector3 requestedPosition,
            float requestedYawDegrees,
            float maximumHorizontalMeters,
            float maximumYawDegrees,
            bool allowVertical)
        {
            if (mode == MotionRootMode.InPlace)
            {
                return new MotionRootDelta(Vector3.zero, 0f);
            }

            maximumHorizontalMeters = Mathf.Max(0f, maximumHorizontalMeters);
            maximumYawDegrees = Mathf.Max(0f, maximumYawDegrees);
            var horizontal = new Vector2(requestedPosition.x, requestedPosition.z);
            if (horizontal.magnitude > maximumHorizontalMeters)
            {
                horizontal = horizontal.normalized * maximumHorizontalMeters;
            }

            float vertical = allowVertical && mode == MotionRootMode.Authored
                ? requestedPosition.y
                : 0f;
            var accepted = new Vector3(horizontal.x, vertical, horizontal.y);
            return new MotionRootDelta(
                accepted,
                Mathf.Clamp(
                    requestedYawDegrees,
                    -maximumYawDegrees,
                    maximumYawDegrees));
        }
    }

    public static class MotionWarp
    {
        public static float CalculateStridePlaybackSpeed(
            float sourceStrideMeters,
            float targetMetersPerSecond,
            float sourceCycleSeconds,
            float minimumSpeed,
            float maximumSpeed)
        {
            if (sourceStrideMeters <= 0f || sourceCycleSeconds <= 0f ||
                targetMetersPerSecond < 0f || minimumSpeed <= 0f ||
                maximumSpeed < minimumSpeed)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceStrideMeters),
                    "Stride, cycle, target speed, and playback bounds are invalid.");
            }

            float requested = targetMetersPerSecond * sourceCycleSeconds /
                              sourceStrideMeters;
            return Mathf.Clamp(requested, minimumSpeed, maximumSpeed);
        }

        public static float CalculateTurnScale(float sourceYawDegrees, float targetYawDegrees)
        {
            if (Mathf.Abs(sourceYawDegrees) <= Mathf.Epsilon)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceYawDegrees),
                    "Source turn yaw must be non-zero.");
            }

            return targetYawDegrees / sourceYawDegrees;
        }
    }

    public static class MotionGroundingMath
    {
        public static Vector3 ClampContactCorrection(
            Vector3 contactPosition,
            Vector3 groundPosition,
            float maximumHorizontalMeters,
            float maximumVerticalMeters)
        {
            if (maximumHorizontalMeters < 0f || maximumVerticalMeters < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumHorizontalMeters),
                    "Grounding correction limits cannot be negative.");
            }

            Vector3 correction = groundPosition - contactPosition;
            var horizontal = new Vector2(correction.x, correction.z);
            if (horizontal.magnitude > maximumHorizontalMeters)
            {
                horizontal = horizontal.normalized * maximumHorizontalMeters;
            }

            return new Vector3(
                horizontal.x,
                Mathf.Clamp(
                    correction.y,
                    -maximumVerticalMeters,
                    maximumVerticalMeters),
                horizontal.y);
        }
    }
}
