using System;
using System.Collections.Generic;
using System.Numerics;
using AL.Battle.Contracts;

namespace AL.Battle.Computation
{
    public static class BattleFixedPoint
    {
        public static long MultiplyAndRoundOnce(
            long value,
            IEnumerable<long> multipliersMicros)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (multipliersMicros == null)
                throw new ArgumentNullException(nameof(multipliersMicros));

            BigInteger numerator = value;
            BigInteger denominator = BigInteger.One;
            foreach (long multiplier in multipliersMicros)
            {
                if (multiplier < 0)
                    throw new ArgumentOutOfRangeException(nameof(multipliersMicros));
                numerator *= multiplier;
                denominator *= BattleTechnicalLimits.MicrosPerUnit;
            }

            return ToInt64Checked(RoundToNearestTiesToEven(numerator, denominator));
        }

        public static long MultiplyAndRound(long value, long multiplierMicros)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (multiplierMicros < 0)
                throw new ArgumentOutOfRangeException(nameof(multiplierMicros));
            return ToInt64Checked(RoundToNearestTiesToEven(
                (BigInteger)value * multiplierMicros,
                BattleTechnicalLimits.MicrosPerUnit));
        }

        public static long DivideAndRound(long numerator, long denominator)
        {
            if (numerator < 0) throw new ArgumentOutOfRangeException(nameof(numerator));
            if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
            return ToInt64Checked(RoundToNearestTiesToEven(numerator, denominator));
        }

        public static long RatioMicros(long numerator, long denominator)
        {
            if (numerator < 0) throw new ArgumentOutOfRangeException(nameof(numerator));
            if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
            return ToInt64Checked(RoundToNearestTiesToEven(
                (BigInteger)numerator * BattleTechnicalLimits.MicrosPerUnit,
                denominator));
        }

        public static bool RatioAtLeast(
            long leftNumerator,
            long leftDenominator,
            long rightNumerator,
            long rightDenominator)
        {
            if (leftNumerator < 0 || rightNumerator < 0)
                throw new ArgumentOutOfRangeException();
            if (leftDenominator <= 0 || rightDenominator <= 0)
                throw new ArgumentOutOfRangeException();
            return (BigInteger)leftNumerator * rightDenominator >=
                   (BigInteger)rightNumerator * leftDenominator;
        }

        public static long MapUInt32(uint draw, long minimum, long maximumExclusive)
        {
            if (minimum < 0 || maximumExclusive <= minimum)
                throw new ArgumentOutOfRangeException();
            BigInteger span = maximumExclusive - minimum;
            BigInteger offset = ((BigInteger)draw * span) >> 32;
            return checked(minimum + (long)offset);
        }

        internal static BigInteger RoundToNearestTiesToEven(
            BigInteger numerator,
            BigInteger denominator)
        {
            if (numerator < 0) throw new ArgumentOutOfRangeException(nameof(numerator));
            if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));

            BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
            BigInteger doubledRemainder = remainder * 2;
            if (doubledRemainder > denominator ||
                (doubledRemainder == denominator && !quotient.IsEven))
                quotient += BigInteger.One;
            return quotient;
        }

        private static long ToInt64Checked(BigInteger value)
        {
            if (value < long.MinValue || value > long.MaxValue)
                throw new OverflowException("Fixed-point result exceeds Int64.");
            return (long)value;
        }
    }
}
