using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AL.Data.Runtime;

namespace AL.UI.Kingdom
{
    internal static class PrivateKingdomHudTimer
    {
        internal static string Format(IEnumerable<BuildingState> states, long observedAtTimestamp)
        {
            BuildingState[] upgrading = (states ?? Array.Empty<BuildingState>())
                .Where(state => state != null && state.IsUpgrading)
                .OrderBy(state => state.UpgradeCompleteTimestamp)
                .ToArray();
            if (upgrading.Length == 0)
            {
                return "BUILD TIMER\nREADY";
            }

            BuildingState next = upgrading.FirstOrDefault(
                state => state.UpgradeCompleteTimestamp > observedAtTimestamp);
            if (next == null)
            {
                return "BUILD TIMER\nCOMPLETE";
            }

            long remainingSeconds = next.UpgradeCompleteTimestamp - observedAtTimestamp;
            long hours = remainingSeconds / 3600;
            long minutes = remainingSeconds % 3600 / 60;
            long seconds = remainingSeconds % 60;
            string duration = hours > 0
                ? $"{hours:00}:{minutes:00}:{seconds:00}"
                : $"{minutes:00}:{seconds:00}";
            string buildingId = string.IsNullOrWhiteSpace(next.BuildingId)
                ? "BUILD"
                : FormatBuildingId(next.BuildingId);
            return $"{buildingId} TIMER\n{duration}";
        }

        private static string FormatBuildingId(string buildingId)
        {
            var builder = new StringBuilder(buildingId.Length + 4);
            for (int index = 0; index < buildingId.Length; index++)
            {
                char current = buildingId[index];
                if (index > 0 &&
                    char.IsUpper(current) &&
                    char.IsLower(buildingId[index - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToUpperInvariant(current));
            }

            return builder.ToString();
        }
    }
}
