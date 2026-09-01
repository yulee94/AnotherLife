using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Motion
{
    public sealed class MotionBlendPoint
    {
        public MotionBlendPoint(float threshold, MotionClipDefinition motion)
        {
            Threshold = threshold;
            Motion = motion ?? throw new ArgumentNullException(nameof(motion));
        }

        public float Threshold { get; }
        public MotionClipDefinition Motion { get; }
    }

    public readonly struct MotionBlendSample
    {
        public MotionBlendSample(
            MotionClipDefinition lower,
            MotionClipDefinition upper,
            float lowerWeight,
            float upperWeight)
        {
            Lower = lower;
            Upper = upper;
            LowerWeight = lowerWeight;
            UpperWeight = upperWeight;
        }

        public MotionClipDefinition Lower { get; }
        public MotionClipDefinition Upper { get; }
        public float LowerWeight { get; }
        public float UpperWeight { get; }
    }

    public sealed class MotionBlendTree1D
    {
        private readonly MotionBlendPoint[] _points;

        public MotionBlendTree1D(IEnumerable<MotionBlendPoint> points)
        {
            _points = (points ?? throw new ArgumentNullException(nameof(points)))
                .OrderBy(value => value.Threshold)
                .ToArray();
            if (_points.Length == 0 || _points.Any(value => value == null))
            {
                throw new InvalidOperationException(
                    "A motion blend tree needs at least one non-null point.");
            }

            for (int index = 1; index < _points.Length; index++)
            {
                if (Math.Abs(_points[index].Threshold - _points[index - 1].Threshold) <=
                    float.Epsilon)
                {
                    throw new InvalidOperationException(
                        "Motion blend thresholds must be unique.");
                }
            }
        }

        public MotionBlendSample Evaluate(float value)
        {
            if (value <= _points[0].Threshold)
            {
                return Single(_points[0].Motion);
            }

            int last = _points.Length - 1;
            if (value >= _points[last].Threshold)
            {
                return Single(_points[last].Motion);
            }

            for (int index = 1; index < _points.Length; index++)
            {
                MotionBlendPoint upper = _points[index];
                if (value > upper.Threshold)
                {
                    continue;
                }

                MotionBlendPoint lower = _points[index - 1];
                float upperWeight = (value - lower.Threshold) /
                                    (upper.Threshold - lower.Threshold);
                return new MotionBlendSample(
                    lower.Motion,
                    upper.Motion,
                    1f - upperWeight,
                    upperWeight);
            }

            return Single(_points[last].Motion);
        }

        private static MotionBlendSample Single(MotionClipDefinition motion)
        {
            return new MotionBlendSample(motion, motion, 1f, 0f);
        }
    }
}
