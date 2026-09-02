using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AL.Motion
{
    [Serializable]
    public sealed class MotionImportEvent
    {
        [SerializeField] private string eventDefinitionId;
        [SerializeField] private int sourceFrame;
        [SerializeField] private int eventOrdinal;
        [SerializeField] private MotionStaticPayload staticPayload = new MotionStaticPayload();

        public MotionImportEvent(
            string eventDefinitionId,
            int sourceFrame,
            int eventOrdinal,
            MotionStaticPayload staticPayload)
        {
            if (string.IsNullOrWhiteSpace(eventDefinitionId))
            {
                throw new ArgumentException(
                    "Event definition ID is required.",
                    nameof(eventDefinitionId));
            }

            if (sourceFrame < 0 || eventOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceFrame),
                    "Event frame and ordinal cannot be negative.");
            }

            this.eventDefinitionId = eventDefinitionId;
            this.sourceFrame = sourceFrame;
            this.eventOrdinal = eventOrdinal;
            this.staticPayload = staticPayload ?? new MotionStaticPayload();
        }

        public string EventDefinitionId => eventDefinitionId;
        public int SourceFrame => sourceFrame;
        public int EventOrdinal => eventOrdinal;
        public MotionStaticPayload StaticPayload => staticPayload;
    }

    [Serializable]
    public sealed class MotionImportClip
    {
        [SerializeField] private string clipId;
        [SerializeField] private string motionKey;
        [SerializeField] private string sourceTake;
        [SerializeField] private int firstFrameInclusive;
        [SerializeField] private int lastFrameInclusive;
        [SerializeField] private bool loop;
        [SerializeField] private MotionRootMode rootMode;
        [SerializeField] private MotionImportEvent[] events = Array.Empty<MotionImportEvent>();

        public MotionImportClip(
            string clipId,
            string motionKey,
            string sourceTake,
            int firstFrameInclusive,
            int lastFrameInclusive,
            bool loop,
            MotionRootMode rootMode,
            IEnumerable<MotionImportEvent> events)
        {
            if (string.IsNullOrWhiteSpace(clipId) ||
                string.IsNullOrWhiteSpace(motionKey) ||
                string.IsNullOrWhiteSpace(sourceTake))
            {
                throw new ArgumentException(
                    "Clip ID, motion key, and source take are required.");
            }

            if (firstFrameInclusive < 0 || lastFrameInclusive < firstFrameInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(firstFrameInclusive),
                    "Clip frame range is invalid.");
            }

            this.clipId = clipId;
            this.motionKey = motionKey;
            this.sourceTake = sourceTake;
            this.firstFrameInclusive = firstFrameInclusive;
            this.lastFrameInclusive = lastFrameInclusive;
            this.loop = loop;
            this.rootMode = rootMode;
            this.events = events?.ToArray() ?? Array.Empty<MotionImportEvent>();
        }

        public string ClipId => clipId;
        public string MotionKey => motionKey;
        public string SourceTake => sourceTake;
        public int FirstFrameInclusive => firstFrameInclusive;
        public int LastFrameInclusive => lastFrameInclusive;
        public bool Loop => loop;
        public MotionRootMode RootMode => rootMode;
        public IReadOnlyList<MotionImportEvent> Events => Array.AsReadOnly(events);
    }

    [Serializable]
    public sealed class MotionImportBinding
    {
        [SerializeField] private string assetPath;
        [SerializeField] private MotionImportPreset preset;
        [SerializeField] private MotionImportClip[] clips = Array.Empty<MotionImportClip>();

        public MotionImportBinding(
            string assetPath,
            MotionImportPreset preset,
            IEnumerable<MotionImportClip> clips)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("Asset path is required.", nameof(assetPath));
            }

            this.assetPath = assetPath.Replace('\\', '/');
            this.preset = preset != null
                ? preset
                : throw new ArgumentNullException(nameof(preset));
            this.clips = clips?.ToArray() ?? Array.Empty<MotionImportClip>();
        }

        public string AssetPath => assetPath;
        public MotionImportPreset Preset => preset;
        public IReadOnlyList<MotionImportClip> Clips => Array.AsReadOnly(clips);
    }

}
