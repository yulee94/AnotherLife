using System;
using System.Text;

namespace AL.ChampionMode.C1
{
    public enum CombatantLifeState
    {
        Uninitialized = 0,
        Alive = 1,
        Defeated = 2,
        Disposed = 3
    }

    public enum CombatantControlState
    {
        Disabled = 0,
        Manual = 1,
        Assist = 2,
        Auto = 3,
        EncounterLocked = 4,
        ActionLocked = 5,
        Defeated = 6,
        Disposed = 7
    }

    public enum CombatActionState
    {
        Requested = 0,
        Rejected = 1,
        Validated = 2,
        ResourceReserved = 3,
        Windup = 4,
        Committed = 5,
        Resolving = 6,
        Completed = 7,
        CancelledBeforeCommit = 8,
        InterruptedAfterCommit = 9,
        Failed = 10,
        Disposed = 11
    }

    public enum CombatEncounterState
    {
        Created = 0,
        Validating = 1,
        Ready = 2,
        Intro = 3,
        Active = 4,
        Resolving = 5,
        CompletionPendingCommit = 6,
        Completed = 7,
        Failed = 8,
        Cancelled = 9,
        RecoveryRequired = 10,
        Disposed = 11
    }

    public enum CombatEncounterMode
    {
        Practice = 0,
        DevelopmentDemo = 1,
        AuthoritativeBoss = 2,
        AuthoritativeQuest = 3
    }

    public enum CombatScalarKind
    {
        Health = 0,
        Mana = 1,
        Damage = 2,
        Healing = 3,
        AttackPower = 4,
        WorldDistance = 5,
        Duration = 6,
        MovementSpeed = 7,
        RegenerationRate = 8,
        Multiplier = 9
    }

    public static class CombatTechnicalLimits
    {
        public const long MicrosPerUnit = 1_000_000L;
        public const long HealthManaDamageHealingAttackPowerMaximumMicros =
            1_000_000_000L * MicrosPerUnit;
        public const long WorldDistanceMaximumMicros = 100_000L * MicrosPerUnit;
        public const long DurationMaximumMicros = 86_400L * MicrosPerUnit;
        public const long MovementSpeedMaximumMicros = 10_000L * MicrosPerUnit;
        public const long RegenerationRateMaximumMicros = 1_000_000L * MicrosPerUnit;
        public const long MultiplierMaximumMicros = 1_000L * MicrosPerUnit;
        public const int MaximumStableIdUtf8Bytes = 256;
        public const int MaximumVersionUtf8Bytes = 128;
        public const int MaximumReferenceEntries = 4_096;
        public const int MaximumLoadoutBindings = 64;
        public const int MaximumDiagnostics = 4_096;
        public const int Sha256HexCharacters = 64;
        public const string SupportedSchemaVersion = "1";

        public static long MaximumMicros(CombatScalarKind kind)
        {
            switch (kind)
            {
                case CombatScalarKind.Health:
                case CombatScalarKind.Mana:
                case CombatScalarKind.Damage:
                case CombatScalarKind.Healing:
                case CombatScalarKind.AttackPower:
                    return HealthManaDamageHealingAttackPowerMaximumMicros;
                case CombatScalarKind.WorldDistance:
                    return WorldDistanceMaximumMicros;
                case CombatScalarKind.Duration:
                    return DurationMaximumMicros;
                case CombatScalarKind.MovementSpeed:
                    return MovementSpeedMaximumMicros;
                case CombatScalarKind.RegenerationRate:
                    return RegenerationRateMaximumMicros;
                case CombatScalarKind.Multiplier:
                    return MultiplierMaximumMicros;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown combat scalar kind.");
            }
        }

        public static bool TryGetMaximumMicros(CombatScalarKind kind, out long maximumMicros)
        {
            switch (kind)
            {
                case CombatScalarKind.Health:
                case CombatScalarKind.Mana:
                case CombatScalarKind.Damage:
                case CombatScalarKind.Healing:
                case CombatScalarKind.AttackPower:
                    maximumMicros = HealthManaDamageHealingAttackPowerMaximumMicros;
                    return true;
                case CombatScalarKind.WorldDistance:
                    maximumMicros = WorldDistanceMaximumMicros;
                    return true;
                case CombatScalarKind.Duration:
                    maximumMicros = DurationMaximumMicros;
                    return true;
                case CombatScalarKind.MovementSpeed:
                    maximumMicros = MovementSpeedMaximumMicros;
                    return true;
                case CombatScalarKind.RegenerationRate:
                    maximumMicros = RegenerationRateMaximumMicros;
                    return true;
                case CombatScalarKind.Multiplier:
                    maximumMicros = MultiplierMaximumMicros;
                    return true;
                default:
                    maximumMicros = 0L;
                    return false;
            }
        }
    }

    public readonly struct CombatStableId : IEquatable<CombatStableId>, IComparable<CombatStableId>
    {
        private readonly string _value;

        private CombatStableId(string value)
        {
            _value = value;
        }

        public string Value => _value ?? string.Empty;
        public bool IsDefault => string.IsNullOrEmpty(_value);

        public static bool TryCreate(string value, out CombatStableId id)
        {
            if (!CombatPrimitiveValidation.IsStableId(value))
            {
                id = default;
                return false;
            }

            id = new CombatStableId(value);
            return true;
        }

        public bool Equals(CombatStableId other) =>
            StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object obj) =>
            obj is CombatStableId other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.Ordinal.GetHashCode(Value);

        public int CompareTo(CombatStableId other) =>
            StringComparer.Ordinal.Compare(Value, other.Value);

        public override string ToString() => Value;

        public static bool operator ==(CombatStableId left, CombatStableId right) =>
            left.Equals(right);

        public static bool operator !=(CombatStableId left, CombatStableId right) =>
            !left.Equals(right);
    }

    public readonly struct CombatContractVersion : IEquatable<CombatContractVersion>
    {
        private readonly string _value;

        private CombatContractVersion(string value)
        {
            _value = value;
        }

        public string Value => _value ?? string.Empty;
        public bool IsDefault => string.IsNullOrEmpty(_value);

        public static bool TryCreate(string value, out CombatContractVersion version)
        {
            if (!CombatPrimitiveValidation.IsVersion(value))
            {
                version = default;
                return false;
            }

            version = new CombatContractVersion(value);
            return true;
        }

        public bool Equals(CombatContractVersion other) =>
            StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object obj) =>
            obj is CombatContractVersion other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;
    }

    public readonly struct CombatSha256 : IEquatable<CombatSha256>
    {
        private readonly string _value;

        private CombatSha256(string value)
        {
            _value = value;
        }

        public string Value => _value ?? string.Empty;
        public bool IsDefault => string.IsNullOrEmpty(_value);

        public static bool TryCreate(string value, out CombatSha256 hash)
        {
            if (!CombatPrimitiveValidation.IsSha256(value))
            {
                hash = default;
                return false;
            }

            hash = new CombatSha256(value);
            return true;
        }

        public bool Equals(CombatSha256 other) =>
            StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object obj) =>
            obj is CombatSha256 other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;
    }

    public readonly struct FiniteCombatScalar
    {
        private FiniteCombatScalar(float value, CombatScalarKind kind, string unitProfileId)
        {
            Value = value;
            Kind = kind;
            UnitProfileId = unitProfileId;
        }

        public float Value { get; }
        public CombatScalarKind Kind { get; }
        public string UnitProfileId { get; }

        public static bool TryCreate(
            float value,
            CombatScalarKind kind,
            string unitProfileId,
            bool requirePositive,
            out FiniteCombatScalar scalar)
        {
            if (!CombatPrimitiveValidation.TryGetMaximumUnits(kind, out float maximumUnits) ||
                !CombatPrimitiveValidation.IsFinite(value) ||
                !CombatPrimitiveValidation.IsStableId(unitProfileId) ||
                value < 0f ||
                (requirePositive && value <= 0f) ||
                value > maximumUnits)
            {
                scalar = default;
                return false;
            }

            scalar = new FiniteCombatScalar(value, kind, unitProfileId);
            return true;
        }
    }

    public readonly struct FiniteCombatVector3
    {
        private FiniteCombatVector3(float x, float y, float z, string unitProfileId)
        {
            X = x;
            Y = y;
            Z = z;
            UnitProfileId = unitProfileId;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public string UnitProfileId { get; }

        public static bool TryCreate(
            float x,
            float y,
            float z,
            string unitProfileId,
            out FiniteCombatVector3 vector)
        {
            if (!CombatPrimitiveValidation.IsFinite(x) ||
                !CombatPrimitiveValidation.IsFinite(y) ||
                !CombatPrimitiveValidation.IsFinite(z) ||
                Math.Abs(x) > CombatPrimitiveValidation.MaximumUnits(CombatScalarKind.WorldDistance) ||
                Math.Abs(y) > CombatPrimitiveValidation.MaximumUnits(CombatScalarKind.WorldDistance) ||
                Math.Abs(z) > CombatPrimitiveValidation.MaximumUnits(CombatScalarKind.WorldDistance) ||
                !CombatPrimitiveValidation.IsStableId(unitProfileId))
            {
                vector = default;
                return false;
            }

            vector = new FiniteCombatVector3(x, y, z, unitProfileId);
            return true;
        }
    }

    public static class CombatPrimitiveValidation
    {
        public static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        public static float MaximumUnits(CombatScalarKind kind) =>
            (float)(
                CombatTechnicalLimits.MaximumMicros(kind) /
                (double)CombatTechnicalLimits.MicrosPerUnit);

        public static bool TryGetMaximumUnits(CombatScalarKind kind, out float maximumUnits)
        {
            if (!CombatTechnicalLimits.TryGetMaximumMicros(kind, out long maximumMicros))
            {
                maximumUnits = 0f;
                return false;
            }

            maximumUnits = (float)(
                maximumMicros /
                (double)CombatTechnicalLimits.MicrosPerUnit);
            return true;
        }

        public static bool IsMicrosInRange(
            long value,
            CombatScalarKind kind,
            bool requirePositive)
        {
            return CombatTechnicalLimits.TryGetMaximumMicros(kind, out long maximumMicros) &&
                   value >= 0L &&
                   (!requirePositive || value > 0L) &&
                   value <= maximumMicros;
        }

        public static bool IsStableId(string value)
        {
            return IsBoundedTechnicalText(
                value,
                CombatTechnicalLimits.MaximumStableIdUtf8Bytes,
                allowSpace: false);
        }

        public static bool IsVersion(string value)
        {
            return IsBoundedTechnicalText(
                value,
                CombatTechnicalLimits.MaximumVersionUtf8Bytes,
                allowSpace: false);
        }

        public static bool IsSupportedSchemaVersion(string value) =>
            StringComparer.Ordinal.Equals(value, CombatTechnicalLimits.SupportedSchemaVersion);

        public static bool IsSha256(string value)
        {
            if (value == null || value.Length != CombatTechnicalLimits.Sha256HexCharacters)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool decimalDigit = character >= '0' && character <= '9';
                bool lowerHex = character >= 'a' && character <= 'f';
                if (!decimalDigit && !lowerHex)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryConvertUnitsToMicros(
            double units,
            CombatScalarKind kind,
            bool requirePositive,
            out long micros)
        {
            micros = 0L;
            if (!CombatTechnicalLimits.TryGetMaximumMicros(kind, out long maximumMicros) ||
                !IsFinite(units) ||
                units < 0d ||
                (requirePositive && units <= 0d))
            {
                return false;
            }

            double scaled = units * CombatTechnicalLimits.MicrosPerUnit;
            if (!IsFinite(scaled) ||
                scaled > maximumMicros ||
                scaled != Math.Truncate(scaled))
            {
                return false;
            }

            micros = checked((long)scaled);
            return IsMicrosInRange(micros, kind, requirePositive);
        }

        private static bool IsBoundedTechnicalText(
            string value,
            int maximumUtf8Bytes,
            bool allowSpace)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !StringComparer.Ordinal.Equals(value, value.Trim()))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsControl(character) ||
                    (!allowSpace && char.IsWhiteSpace(character)))
                {
                    return false;
                }

                if (char.IsSurrogate(character))
                {
                    if (!char.IsHighSurrogate(character) ||
                        index + 1 >= value.Length ||
                        !char.IsLowSurrogate(value[index + 1]))
                    {
                        return false;
                    }

                    index++;
                }
            }

            return Encoding.UTF8.GetByteCount(value) <= maximumUtf8Bytes;
        }
    }
}
