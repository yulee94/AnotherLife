using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace AL.Motion
{
    public enum MotionRootMode
    {
        InPlace = 0,
        Bounded = 1,
        Authored = 2
    }

    public enum MotionPriority
    {
        Idle = 0,
        Locomotion = 10,
        Traversal = 20,
        Interaction = 30,
        Attack = 40,
        Skill = 50,
        Reaction = 60,
        Interruption = 70,
        HardControl = 80,
        Defeat = 90
    }

    public sealed class MotionClipDefinition
    {
        public MotionClipDefinition(
            string clipId,
            string motionKey,
            AnimationClip clip,
            string fallbackMotionKey,
            MotionRootMode rootMode,
            MotionPriority priority,
            bool loop,
            bool additive)
        {
            if (string.IsNullOrWhiteSpace(clipId))
            {
                throw new ArgumentException("Clip ID is required.", nameof(clipId));
            }

            if (string.IsNullOrWhiteSpace(motionKey))
            {
                throw new ArgumentException("Motion key is required.", nameof(motionKey));
            }

            ClipId = clipId;
            MotionKey = motionKey;
            Clip = clip != null
                ? clip
                : throw new ArgumentNullException(nameof(clip));
            FallbackMotionKey = string.IsNullOrWhiteSpace(fallbackMotionKey)
                ? null
                : fallbackMotionKey;
            RootMode = rootMode;
            Priority = priority;
            Loop = loop;
            Additive = additive;
        }

        public string ClipId { get; }
        public string MotionKey { get; }
        public AnimationClip Clip { get; }
        public string FallbackMotionKey { get; }
        public MotionRootMode RootMode { get; }
        public MotionPriority Priority { get; }
        public bool Loop { get; }
        public bool Additive { get; }
    }

    public sealed class MotionFallbackRule
    {
        public MotionFallbackRule(string motionKey, string fallbackMotionKey)
        {
            if (string.IsNullOrWhiteSpace(motionKey))
            {
                throw new ArgumentException("Motion key is required.", nameof(motionKey));
            }

            if (string.IsNullOrWhiteSpace(fallbackMotionKey))
            {
                throw new ArgumentException(
                    "Fallback motion key is required.",
                    nameof(fallbackMotionKey));
            }

            MotionKey = motionKey;
            FallbackMotionKey = fallbackMotionKey;
        }

        public string MotionKey { get; }
        public string FallbackMotionKey { get; }
    }

    public sealed class MotionCatalogSnapshot
    {
        private readonly IReadOnlyDictionary<string, MotionClipDefinition> _clipsByKey;
        private readonly IReadOnlyDictionary<string, MotionClipDefinition> _clipsById;
        private readonly IReadOnlyDictionary<string, string> _fallbacksByKey;

        public MotionCatalogSnapshot(
            string safeMotionKey,
            IEnumerable<MotionClipDefinition> clips,
            IEnumerable<MotionFallbackRule> fallbackRules = null)
        {
            if (string.IsNullOrWhiteSpace(safeMotionKey))
            {
                throw new ArgumentException(
                    "A safe motion key is required.",
                    nameof(safeMotionKey));
            }

            MotionClipDefinition[] ordered = (clips ??
                    throw new ArgumentNullException(nameof(clips)))
                .OrderBy(value => value.MotionKey, StringComparer.Ordinal)
                .ToArray();
            var byKey = new Dictionary<string, MotionClipDefinition>(StringComparer.Ordinal);
            var byId = new Dictionary<string, MotionClipDefinition>(StringComparer.Ordinal);
            for (int index = 0; index < ordered.Length; index++)
            {
                MotionClipDefinition clip = ordered[index] ??
                                              throw new InvalidOperationException(
                                                  "Motion catalog contains a null clip definition.");
                if (!byKey.TryAdd(clip.MotionKey, clip))
                {
                    throw new InvalidOperationException(
                        "Duplicate motion key: " + clip.MotionKey);
                }

                if (!byId.TryAdd(clip.ClipId, clip))
                {
                    throw new InvalidOperationException(
                        "Duplicate clip ID: " + clip.ClipId);
                }
            }

            if (!byKey.ContainsKey(safeMotionKey))
            {
                throw new InvalidOperationException(
                    "Safe motion is not bound: " + safeMotionKey);
            }

            var fallbacks = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (MotionFallbackRule rule in fallbackRules ?? Array.Empty<MotionFallbackRule>())
            {
                if (rule == null || !fallbacks.TryAdd(rule.MotionKey, rule.FallbackMotionKey))
                {
                    throw new InvalidOperationException(
                        "Motion fallback rules must be non-null and unique.");
                }
            }

            foreach (MotionClipDefinition clip in ordered)
            {
                if (clip.FallbackMotionKey != null &&
                    !fallbacks.TryAdd(clip.MotionKey, clip.FallbackMotionKey))
                {
                    throw new InvalidOperationException(
                        "Duplicate fallback rule: " + clip.MotionKey);
                }
            }

            SafeMotionKey = safeMotionKey;
            Clips = Array.AsReadOnly(ordered);
            _clipsByKey = new ReadOnlyDictionary<string, MotionClipDefinition>(byKey);
            _clipsById = new ReadOnlyDictionary<string, MotionClipDefinition>(byId);
            _fallbacksByKey = new ReadOnlyDictionary<string, string>(fallbacks);
            ValidateFallbacks();
        }

        public string SafeMotionKey { get; }
        public IReadOnlyList<MotionClipDefinition> Clips { get; }

        public bool TryGetExact(string motionKey, out MotionClipDefinition clip)
        {
            return _clipsByKey.TryGetValue(motionKey ?? string.Empty, out clip);
        }

        public bool TryGetClipById(string clipId, out MotionClipDefinition clip)
        {
            return _clipsById.TryGetValue(clipId ?? string.Empty, out clip);
        }

        public bool TryResolve(string motionKey, out MotionClipDefinition clip)
        {
            string current = motionKey ?? string.Empty;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (visited.Add(current))
            {
                if (_clipsByKey.TryGetValue(current, out clip))
                {
                    return true;
                }

                if (!_fallbacksByKey.TryGetValue(current, out current))
                {
                    break;
                }
            }

            return _clipsByKey.TryGetValue(SafeMotionKey, out clip);
        }

        private void ValidateFallbacks()
        {
            foreach (KeyValuePair<string, string> pair in _fallbacksByKey)
            {
                string current = pair.Key;
                var visited = new HashSet<string>(StringComparer.Ordinal);
                while (_fallbacksByKey.TryGetValue(current, out string fallback))
                {
                    if (!visited.Add(current))
                    {
                        throw new InvalidOperationException(
                            "Motion fallback cycle begins at: " + pair.Key);
                    }

                    current = fallback;
                }

                if (!_clipsByKey.ContainsKey(current))
                {
                    throw new InvalidOperationException(
                        "Fallback chain has no bound destination: " + pair.Key);
                }
            }
        }
    }
}
