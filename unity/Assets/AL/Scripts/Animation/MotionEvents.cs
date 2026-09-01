using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Motion
{
    [Serializable]
    public sealed class MotionStaticPayload
    {
        public string Phase;
        public string ContactId;
        public string WindowId;
        public string CueId;
    }

    [Serializable]
    public sealed class MotionAnimationEventPayload
    {
        public int schemaVersion;
        public string eventId;
        public long actionSequence;
        public int eventOrdinal;
        public float normalizedTime;
        public string phase;
        public string contactId;
        public string windowId;
        public string cueId;
    }

    public sealed class MotionEventDefinition
    {
        public MotionEventDefinition(
            string eventDefinitionId,
            string eventName,
            int sourceFrame,
            int eventOrdinal,
            MotionStaticPayload payload)
        {
            if (string.IsNullOrWhiteSpace(eventDefinitionId) ||
                string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("Motion event ID and name are required.");
            }

            if (sourceFrame < 0 || eventOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceFrame),
                    "Motion event frame and ordinal cannot be negative.");
            }

            EventDefinitionId = eventDefinitionId;
            EventName = eventName;
            SourceFrame = sourceFrame;
            EventOrdinal = eventOrdinal;
            Payload = payload ?? new MotionStaticPayload();
        }

        public string EventDefinitionId { get; }
        public string EventName { get; }
        public int SourceFrame { get; }
        public int EventOrdinal { get; }
        public MotionStaticPayload Payload { get; }
    }

    public sealed class MotionEventDispatch
    {
        internal MotionEventDispatch(
            MotionEventDefinition definition,
            long actionSequence,
            float normalizedTime)
        {
            EventDefinitionId = definition.EventDefinitionId;
            EventName = definition.EventName;
            EventOrdinal = definition.EventOrdinal;
            ActionSequence = actionSequence;
            NormalizedTime = normalizedTime;
            Payload = definition.Payload;
        }

        public string EventDefinitionId { get; }
        public string EventName { get; }
        public int EventOrdinal { get; }
        public long ActionSequence { get; }
        public float NormalizedTime { get; }
        public MotionStaticPayload Payload { get; }
    }

    public sealed class MotionEventTimeline
    {
        private readonly MotionEventDefinition[] _events;
        private readonly int _sampleRateHz;
        private readonly int _lastFrame;

        public MotionEventTimeline(
            int sampleRateHz,
            int frameCount,
            IEnumerable<MotionEventDefinition> events)
        {
            if (sampleRateHz <= 0 || frameCount <= 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleRateHz),
                    "Motion timelines need a positive sample rate and at least two frames.");
            }

            _sampleRateHz = sampleRateHz;
            _lastFrame = frameCount - 1;
            _events = (events ?? throw new ArgumentNullException(nameof(events)))
                .OrderBy(value => value.SourceFrame)
                .ThenBy(value => value.EventOrdinal)
                .ToArray();
            var ordinals = new HashSet<int>();
            for (int index = 0; index < _events.Length; index++)
            {
                MotionEventDefinition definition = _events[index] ??
                                                    throw new InvalidOperationException(
                                                        "Motion timeline contains a null event.");
                if (definition.SourceFrame > _lastFrame)
                {
                    throw new InvalidOperationException(
                        "Motion event is outside the clip frame range.");
                }

                if (!ordinals.Add(definition.EventOrdinal))
                {
                    throw new InvalidOperationException(
                        "Motion event ordinals must be unique within a clip.");
                }
            }
        }

        public IReadOnlyList<MotionEventDispatch> Collect(
            double previousRuntimeSeconds,
            double currentRuntimeSeconds,
            float playbackSpeed,
            long actionSequence)
        {
            if (previousRuntimeSeconds < 0d || currentRuntimeSeconds < previousRuntimeSeconds ||
                playbackSpeed <= 0f || actionSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentRuntimeSeconds),
                    "Motion event collection range, speed, or action sequence is invalid.");
            }

            double previousLocal = previousRuntimeSeconds * playbackSpeed;
            double currentLocal = currentRuntimeSeconds * playbackSpeed;
            var dispatches = new List<MotionEventDispatch>();
            for (int index = 0; index < _events.Length; index++)
            {
                MotionEventDefinition definition = _events[index];
                double eventSeconds = (double)definition.SourceFrame / _sampleRateHz;
                bool atInitialBoundary = previousLocal <= 0d && eventSeconds == 0d;
                if ((eventSeconds > previousLocal || atInitialBoundary) &&
                    eventSeconds <= currentLocal)
                {
                    dispatches.Add(
                        new MotionEventDispatch(
                            definition,
                            actionSequence,
                            (float)definition.SourceFrame / _lastFrame));
                }
            }

            return dispatches.AsReadOnly();
        }
    }

    public sealed class MotionEventDeduplicator
    {
        private readonly HashSet<string> _accepted =
            new HashSet<string>(StringComparer.Ordinal);

        public bool TryAccept(int actorInstanceId, MotionEventDispatch dispatch)
        {
            if (dispatch == null || dispatch.ActionSequence <= 0)
            {
                return false;
            }

            string key = actorInstanceId + "+" + dispatch.ActionSequence + "+" +
                         dispatch.EventDefinitionId + "+" + dispatch.EventOrdinal;
            return _accepted.Add(key);
        }

        public void RetireThrough(long actionSequence)
        {
            string marker = "+" + actionSequence + "+";
            _accepted.RemoveWhere(value => value.Contains(marker));
        }

        public void Clear()
        {
            _accepted.Clear();
        }
    }

    public sealed class MotionWindowTracker
    {
        private const string BeginEventName = "al.motion.hitbox.request_begin";
        private const string EndEventName = "al.motion.hitbox.request_end";

        private readonly Func<long, string, bool> _authority;
        private readonly Dictionary<string, long> _open =
            new Dictionary<string, long>(StringComparer.Ordinal);

        public MotionWindowTracker(Func<long, string, bool> authority)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        }

        public bool Apply(MotionEventDispatch dispatch)
        {
            if (dispatch == null || string.IsNullOrWhiteSpace(dispatch.Payload?.WindowId))
            {
                return false;
            }

            string windowId = dispatch.Payload.WindowId;
            if (string.Equals(dispatch.EventName, BeginEventName, StringComparison.Ordinal))
            {
                if (!_authority(dispatch.ActionSequence, windowId) || _open.ContainsKey(windowId))
                {
                    return false;
                }

                _open.Add(windowId, dispatch.ActionSequence);
                return true;
            }

            if (!string.Equals(dispatch.EventName, EndEventName, StringComparison.Ordinal) ||
                !_open.TryGetValue(windowId, out long sequence) ||
                sequence != dispatch.ActionSequence)
            {
                return false;
            }

            return _open.Remove(windowId);
        }

        public bool IsOpen(string windowId)
        {
            return _open.ContainsKey(windowId ?? string.Empty);
        }

        public void CloseAll(long actionSequence)
        {
            string[] closing = _open
                .Where(pair => pair.Value == actionSequence)
                .Select(pair => pair.Key)
                .ToArray();
            for (int index = 0; index < closing.Length; index++)
            {
                _open.Remove(closing[index]);
            }
        }

        public void CloseAll()
        {
            _open.Clear();
        }
    }
}
