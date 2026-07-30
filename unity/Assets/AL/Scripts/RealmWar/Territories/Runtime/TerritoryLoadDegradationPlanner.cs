using System;

namespace AL.RealmWar.Territories.Runtime
{
    public enum TerritoryLoadLevel
    {
        Normal = 0,
        Elevated = 1,
        Heavy = 2,
        Critical = 3
    }

    public enum TerritoryRenderTier
    {
        FullDetail = 0,
        MediumDetail = 1,
        LowDetail = 2,
        Impostor = 3,
        Culled = 4
    }

    public readonly struct TerritoryLoadBudget : IEquatable<TerritoryLoadBudget>
    {
        public TerritoryLoadBudget(
            TerritoryLoadLevel level,
            int fullDetailCapacity,
            int mediumDetailCapacity,
            int lowDetailCapacity,
            int impostorCapacity,
            float decorativeVfxMultiplier,
            float weatherMultiplier,
            bool decorativeLightsEnabled,
            float environmentLodTransitionMultiplier)
        {
            Level = level;
            FullDetailCapacity = fullDetailCapacity;
            MediumDetailCapacity = mediumDetailCapacity;
            LowDetailCapacity = lowDetailCapacity;
            ImpostorCapacity = impostorCapacity;
            DecorativeVfxMultiplier = decorativeVfxMultiplier;
            WeatherMultiplier = weatherMultiplier;
            DecorativeLightsEnabled = decorativeLightsEnabled;
            EnvironmentLodTransitionMultiplier = environmentLodTransitionMultiplier;
        }

        public TerritoryLoadLevel Level { get; }
        public int FullDetailCapacity { get; }
        public int MediumDetailCapacity { get; }
        public int LowDetailCapacity { get; }
        public int ImpostorCapacity { get; }
        public float DecorativeVfxMultiplier { get; }
        public float WeatherMultiplier { get; }
        public bool DecorativeLightsEnabled { get; }
        public float EnvironmentLodTransitionMultiplier { get; }

        public int RepresentedCapacity =>
            FullDetailCapacity + MediumDetailCapacity + LowDetailCapacity + ImpostorCapacity;

        public int AnimatedCapacity => FullDetailCapacity + MediumDetailCapacity;

        public bool Equals(TerritoryLoadBudget other)
        {
            return Level == other.Level &&
                   FullDetailCapacity == other.FullDetailCapacity &&
                   MediumDetailCapacity == other.MediumDetailCapacity &&
                   LowDetailCapacity == other.LowDetailCapacity &&
                   ImpostorCapacity == other.ImpostorCapacity &&
                   DecorativeVfxMultiplier.Equals(other.DecorativeVfxMultiplier) &&
                   WeatherMultiplier.Equals(other.WeatherMultiplier) &&
                   DecorativeLightsEnabled == other.DecorativeLightsEnabled &&
                   EnvironmentLodTransitionMultiplier.Equals(other.EnvironmentLodTransitionMultiplier);
        }

        public override bool Equals(object obj)
        {
            return obj is TerritoryLoadBudget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Level;
                hash = (hash * 397) ^ FullDetailCapacity;
                hash = (hash * 397) ^ MediumDetailCapacity;
                hash = (hash * 397) ^ LowDetailCapacity;
                hash = (hash * 397) ^ ImpostorCapacity;
                hash = (hash * 397) ^ DecorativeVfxMultiplier.GetHashCode();
                hash = (hash * 397) ^ WeatherMultiplier.GetHashCode();
                hash = (hash * 397) ^ DecorativeLightsEnabled.GetHashCode();
                hash = (hash * 397) ^ EnvironmentLodTransitionMultiplier.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct TerritoryLoadPlan : IEquatable<TerritoryLoadPlan>
    {
        public TerritoryLoadPlan(
            TerritoryLoadBudget budget,
            int visibleUserCount,
            int fullDetailCount,
            int mediumDetailCount,
            int lowDetailCount,
            int impostorCount,
            int culledCount)
        {
            Budget = budget;
            VisibleUserCount = visibleUserCount;
            FullDetailCount = fullDetailCount;
            MediumDetailCount = mediumDetailCount;
            LowDetailCount = lowDetailCount;
            ImpostorCount = impostorCount;
            CulledCount = culledCount;
        }

        public TerritoryLoadBudget Budget { get; }
        public int VisibleUserCount { get; }
        public int FullDetailCount { get; }
        public int MediumDetailCount { get; }
        public int LowDetailCount { get; }
        public int ImpostorCount { get; }
        public int CulledCount { get; }

        public int RepresentedCount =>
            FullDetailCount + MediumDetailCount + LowDetailCount + ImpostorCount;

        public int AssignedCount => RepresentedCount + CulledCount;

        public bool Equals(TerritoryLoadPlan other)
        {
            return Budget.Equals(other.Budget) &&
                   VisibleUserCount == other.VisibleUserCount &&
                   FullDetailCount == other.FullDetailCount &&
                   MediumDetailCount == other.MediumDetailCount &&
                   LowDetailCount == other.LowDetailCount &&
                   ImpostorCount == other.ImpostorCount &&
                   CulledCount == other.CulledCount;
        }

        public override bool Equals(object obj)
        {
            return obj is TerritoryLoadPlan other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Budget.GetHashCode();
                hash = (hash * 397) ^ VisibleUserCount;
                hash = (hash * 397) ^ FullDetailCount;
                hash = (hash * 397) ^ MediumDetailCount;
                hash = (hash * 397) ^ LowDetailCount;
                hash = (hash * 397) ^ ImpostorCount;
                hash = (hash * 397) ^ CulledCount;
                return hash;
            }
        }
    }

    public static class TerritoryLoadDegradationPlanner
    {
        public const int SafeRepresentedUserCapacity = 100;
        public const int ElevatedUserThreshold = 70;
        public const int HeavyUserThreshold = 100;

        public static TerritoryLoadLevel EvaluateRequiredLevel(
            int activeUserCount,
            float averageFrameTimeMilliseconds,
            float targetFrameTimeMilliseconds)
        {
            ValidateFiniteNonNegative(averageFrameTimeMilliseconds, nameof(averageFrameTimeMilliseconds));
            ValidateFinitePositive(targetFrameTimeMilliseconds, nameof(targetFrameTimeMilliseconds));

            TerritoryLoadLevel userLevel = EvaluateUserLevel(activeUserCount);

            float framePressure = averageFrameTimeMilliseconds / targetFrameTimeMilliseconds;
            TerritoryLoadLevel frameLevel = framePressure >= 1.75f
                ? TerritoryLoadLevel.Critical
                : framePressure >= 1.35f
                    ? TerritoryLoadLevel.Heavy
                    : framePressure >= 1.10f
                        ? TerritoryLoadLevel.Elevated
                        : TerritoryLoadLevel.Normal;

            return (TerritoryLoadLevel)Math.Max((int)userLevel, (int)frameLevel);
        }

        public static TerritoryLoadLevel EvaluateUserLevel(int activeUserCount)
        {
            if (activeUserCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(activeUserCount));
            }

            return activeUserCount > SafeRepresentedUserCapacity
                ? TerritoryLoadLevel.Critical
                : activeUserCount >= HeavyUserThreshold
                    ? TerritoryLoadLevel.Heavy
                    : activeUserCount >= ElevatedUserThreshold
                        ? TerritoryLoadLevel.Elevated
                        : TerritoryLoadLevel.Normal;
        }

        public static TerritoryLoadPlan CreatePlan(int visibleUserCount, TerritoryLoadLevel level)
        {
            if (visibleUserCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(visibleUserCount));
            }

            TerritoryLoadBudget budget = CreateBudget(level);
            int remaining = visibleUserCount;
            int fullDetail = Take(ref remaining, budget.FullDetailCapacity);
            int mediumDetail = Take(ref remaining, budget.MediumDetailCapacity);
            int lowDetail = Take(ref remaining, budget.LowDetailCapacity);
            int impostor = Take(ref remaining, budget.ImpostorCapacity);

            return new TerritoryLoadPlan(
                budget,
                visibleUserCount,
                fullDetail,
                mediumDetail,
                lowDetail,
                impostor,
                remaining);
        }

        public static TerritoryLoadBudget CreateBudget(TerritoryLoadLevel level)
        {
            switch (level)
            {
                case TerritoryLoadLevel.Normal:
                    return new TerritoryLoadBudget(level, 24, 32, 32, 12, 1f, 1f, true, 1f);
                case TerritoryLoadLevel.Elevated:
                    return new TerritoryLoadBudget(level, 16, 28, 28, 28, 0.65f, 0.70f, true, 1.15f);
                case TerritoryLoadLevel.Heavy:
                    return new TerritoryLoadBudget(level, 12, 20, 20, 48, 0.25f, 0.35f, false, 1.35f);
                case TerritoryLoadLevel.Critical:
                    return new TerritoryLoadBudget(level, 8, 12, 16, 64, 0f, 0.10f, false, 1.60f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown territory load level.");
            }
        }

        private static int Take(ref int remaining, int capacity)
        {
            int count = Math.Min(remaining, capacity);
            remaining -= count;
            return count;
        }

        private static void ValidateFiniteNonNegative(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateFinitePositive(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class TerritoryLoadStateMachine
    {
        private readonly float _degradeDelaySeconds;
        private readonly float _recoverDelaySeconds;
        private TerritoryLoadLevel _pendingLevel;
        private float _pendingSeconds;
        private bool _hasPendingLevel;

        public TerritoryLoadStateMachine(float degradeDelaySeconds = 0.5f, float recoverDelaySeconds = 3f)
        {
            if (float.IsNaN(degradeDelaySeconds) || float.IsInfinity(degradeDelaySeconds) || degradeDelaySeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(degradeDelaySeconds));
            }

            if (float.IsNaN(recoverDelaySeconds) || float.IsInfinity(recoverDelaySeconds) || recoverDelaySeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(recoverDelaySeconds));
            }

            _degradeDelaySeconds = degradeDelaySeconds;
            _recoverDelaySeconds = recoverDelaySeconds;
            CurrentLevel = TerritoryLoadLevel.Normal;
        }

        public TerritoryLoadLevel CurrentLevel { get; private set; }

        public bool Step(TerritoryLoadLevel requestedLevel, float elapsedSeconds)
        {
            if (!Enum.IsDefined(typeof(TerritoryLoadLevel), requestedLevel))
            {
                throw new ArgumentOutOfRangeException(nameof(requestedLevel));
            }

            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            if (requestedLevel == CurrentLevel)
            {
                ClearPending();
                return false;
            }

            if (!_hasPendingLevel || _pendingLevel != requestedLevel)
            {
                _pendingLevel = requestedLevel;
                _pendingSeconds = 0f;
                _hasPendingLevel = true;
            }

            _pendingSeconds += elapsedSeconds;
            bool degrading = requestedLevel > CurrentLevel;
            float requiredSeconds = degrading ? _degradeDelaySeconds : _recoverDelaySeconds;
            if (_pendingSeconds < requiredSeconds)
            {
                return false;
            }

            if (degrading)
            {
                CurrentLevel = requestedLevel;
            }
            else
            {
                int nextLevel = Math.Max((int)requestedLevel, (int)CurrentLevel - 1);
                CurrentLevel = (TerritoryLoadLevel)nextLevel;
            }

            ClearPending();
            return true;
        }

        public void Reset(TerritoryLoadLevel level = TerritoryLoadLevel.Normal)
        {
            if (!Enum.IsDefined(typeof(TerritoryLoadLevel), level))
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            CurrentLevel = level;
            ClearPending();
        }

        private void ClearPending()
        {
            _hasPendingLevel = false;
            _pendingSeconds = 0f;
        }
    }
}
