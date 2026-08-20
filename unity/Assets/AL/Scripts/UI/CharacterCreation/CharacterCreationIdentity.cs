using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AL.UI.CharacterCreation
{
    /// <summary>
    /// Local username rules for character creation. Production uniqueness is simulated in-process
    /// only — SaveGameData stays schema-v1 locked, so this never writes a new top-level save field.
    /// </summary>
    public static class CharacterCreationIdentity
    {
        public const int MinLength = 3;
        public const int MaxLength = 16;

        private static readonly Regex Allowed = new Regex(
            "^[A-Za-z0-9_]+$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly HashSet<string> ClaimedNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> ClaimedUsernames => ClaimedNames;

        public static bool TryNormalize(string raw, out string normalized, out string error)
        {
            normalized = (raw ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                error = "Enter a username.";
                return false;
            }

            if (normalized.Length < MinLength || normalized.Length > MaxLength)
            {
                error = $"Username must be {MinLength}–{MaxLength} characters.";
                return false;
            }

            if (!Allowed.IsMatch(normalized))
            {
                error = "Username may use letters, numbers, and underscore only.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool IsAvailable(string raw, string alreadyOwnedByThisChampion)
        {
            if (!TryNormalize(raw, out string normalized, out _))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(alreadyOwnedByThisChampion) &&
                string.Equals(normalized, alreadyOwnedByThisChampion.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !ClaimedNames.Contains(normalized);
        }

        public static bool TryClaim(string raw, string alreadyOwnedByThisChampion, out string normalized, out string error)
        {
            if (!TryNormalize(raw, out normalized, out error))
            {
                return false;
            }

            if (!IsAvailable(normalized, alreadyOwnedByThisChampion))
            {
                error = "That username is already taken.";
                return false;
            }

            ClaimedNames.Add(normalized);
            error = string.Empty;
            return true;
        }

        public static void ResetClaims()
        {
            ClaimedNames.Clear();
        }
    }
}
