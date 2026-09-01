using System;
using System.Collections.Generic;
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

        public string AssetPath => assetPath;
        public MotionImportPreset Preset => preset;
        public IReadOnlyList<MotionImportClip> Clips => Array.AsReadOnly(clips);
    }

}
