using System;
using System.Collections.Generic;
using UnityEngine;

namespace AL.Motion
{
    public sealed class MotionEventNameRegistry
    {
        [Serializable]
        private sealed class ManifestFile
        {
            public EventDefinitionFile[] eventDefinitions = Array.Empty<EventDefinitionFile>();
        }

        [Serializable]
        private sealed class EventDefinitionFile
        {
            public string id;
            public string eventName;
        }

        private readonly Dictionary<string, string> _eventNames;

        private MotionEventNameRegistry(Dictionary<string, string> eventNames)
        {
            _eventNames = eventNames;
        }

        public int Count => _eventNames.Count;

        public static MotionEventNameRegistry FromManifestJson(string manifestJson)
        {
            if (string.IsNullOrWhiteSpace(manifestJson))
            {
                throw new ArgumentException("Motion manifest JSON is required.", nameof(manifestJson));
            }

            ManifestFile manifest = JsonUtility.FromJson<ManifestFile>(manifestJson);
            if (manifest?.eventDefinitions == null || manifest.eventDefinitions.Length == 0)
            {
                throw new InvalidOperationException(
                    "Motion manifest must contain eventDefinitions.");
            }

            var eventNames = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (EventDefinitionFile definition in manifest.eventDefinitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.id) ||
                    string.IsNullOrWhiteSpace(definition.eventName) ||
                    !eventNames.TryAdd(definition.id, definition.eventName))
                {
                    throw new InvalidOperationException(
                        "Motion event definitions require unique non-empty IDs and names.");
                }
            }

            return new MotionEventNameRegistry(eventNames);
        }

        public bool TryResolve(string eventDefinitionId, out string eventName)
        {
            return _eventNames.TryGetValue(
                eventDefinitionId ?? string.Empty,
                out eventName);
        }
    }
}
