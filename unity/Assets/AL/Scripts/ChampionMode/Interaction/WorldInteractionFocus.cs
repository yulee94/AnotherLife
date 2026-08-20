using System.Collections.Generic;
using UnityEngine;

namespace AL.ChampionMode.Interaction
{
    /// <summary>
    /// Look-at selection: the candidate inside range whose aim score is best.
    /// Score is angle/limit + distance/limit so the camera must actually look at the target.
    /// </summary>
    public static class WorldInteractionFocus
    {
        public static bool TrySelect(
            Vector3 origin,
            Vector3 forward,
            IReadOnlyList<WorldInteractionCandidate> candidates,
            out int selectedIndex)
        {
            selectedIndex = -1;
            if (candidates == null || candidates.Count == 0)
            {
                return false;
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector3 look = forward.normalized;
            float bestScore = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                WorldInteractionCandidate candidate = candidates[i];
                if (string.IsNullOrEmpty(candidate.CatalogId) ||
                    candidate.MaxDistance <= 0f ||
                    candidate.MaxAngleDegrees <= 0f)
                {
                    continue;
                }

                Vector3 toTarget = candidate.Position - origin;
                float distance = toTarget.magnitude;
                if (distance > candidate.MaxDistance || distance <= 0.001f)
                {
                    continue;
                }

                float angle = Vector3.Angle(look, toTarget);
                if (angle > candidate.MaxAngleDegrees)
                {
                    continue;
                }

                float score = (angle / candidate.MaxAngleDegrees) + (distance / candidate.MaxDistance);
                if (score < bestScore)
                {
                    bestScore = score;
                    selectedIndex = i;
                }
            }

            return selectedIndex >= 0;
        }
    }
}
