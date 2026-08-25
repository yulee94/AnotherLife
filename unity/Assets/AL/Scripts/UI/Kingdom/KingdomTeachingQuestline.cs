using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using AL.ChampionMode.Quests;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Services.Local;
using UnityEngine;

namespace AL.UI.Kingdom
{
    public sealed class KingdomTeachingEntry
    {
        internal KingdomTeachingEntry(
            string id,
            string title,
            string whatToDo,
            string location)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            WhatToDo = whatToDo ?? string.Empty;
            Location = location ?? string.Empty;
        }

        public string Id { get; }
        public string Title { get; }
        public string WhatToDo { get; }
        public string Location { get; }
    }

    public sealed class KingdomTeachingStep
    {
        internal KingdomTeachingStep(
            string id,
            string title,
            string whatToDo,
            string location,
            string interaction,
            string completionEvent,
            string action)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            WhatToDo = whatToDo ?? string.Empty;
            Location = location ?? string.Empty;
            Interaction = interaction ?? string.Empty;
            CompletionEvent = completionEvent ?? string.Empty;
            Action = action ?? string.Empty;
        }

        public string Id { get; }
        public string Title { get; }
        public string WhatToDo { get; }
        public string Location { get; }
        public string Interaction { get; }
        public string CompletionEvent { get; }
        public string Action { get; }
    }

    public sealed class KingdomTeachingCatalog
    {
        public const string FileName = "al_kingdom_teaching_catalog.json";
        public const string CatalogId = "al_kingdom_teaching_v1";
        public const string EntryId = "teach_enter_private_kingdom";

        private readonly IReadOnlyList<KingdomTeachingStep> _steps;

        private KingdomTeachingCatalog(
            string questId,
            KingdomTeachingEntry entry,
            IReadOnlyList<KingdomTeachingStep> steps)
        {
            QuestId = questId;
            Entry = entry;
            _steps = steps;
        }

        public string QuestId { get; }
        public KingdomTeachingEntry Entry { get; }
        public IReadOnlyList<KingdomTeachingStep> Steps => _steps;

        public static KingdomTeachingCatalog LoadCanonical()
        {
            if (!SixFamilyRuntimeCatalog.TryResolveGameDataDirectory(
                    out string directory))
            {
                throw new DirectoryNotFoundException(
                    "Packaged GameData directory is missing.");
            }

            string path = Path.Combine(directory, FileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Packaged kingdom teaching catalog is missing.",
                    path);
            }

            return Parse(File.ReadAllText(path), path);
        }

        private static KingdomTeachingCatalog Parse(string json, string source)
        {
            KingdomTeachingCatalogFile file = JsonUtility.FromJson<KingdomTeachingCatalogFile>(json);
            if (file == null ||
                !string.Equals(file.catalog_id, CatalogId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(file.quest_id) ||
                !IsValidEntry(file.entry) ||
                file.steps == null ||
                file.steps.Length == 0)
            {
                throw new InvalidDataException(
                    "Invalid kingdom teaching catalog: " + (source ?? string.Empty));
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var steps = new List<KingdomTeachingStep>(file.steps.Length);
            for (int index = 0; index < file.steps.Length; index++)
            {
                KingdomTeachingStepFile row = file.steps[index];
                if (row == null ||
                    string.IsNullOrWhiteSpace(row.id) ||
                    !ids.Add(row.id) ||
                    string.IsNullOrWhiteSpace(row.title) ||
                    string.IsNullOrWhiteSpace(row.what_to_do) ||
                    string.IsNullOrWhiteSpace(row.location) ||
                    string.IsNullOrWhiteSpace(row.interaction) ||
                    string.IsNullOrWhiteSpace(row.completion_event) ||
                    string.IsNullOrWhiteSpace(row.action) ||
                    ContainsForbiddenDestination(row.title) ||
                    ContainsForbiddenDestination(row.what_to_do) ||
                    ContainsForbiddenDestination(row.location))
                {
                    throw new InvalidDataException(
                        "Invalid kingdom teaching step at index " + index + ": " +
                        (source ?? string.Empty));
                }

                steps.Add(new KingdomTeachingStep(
                    row.id,
                    row.title,
                    row.what_to_do,
                    row.location,
                    row.interaction,
                    row.completion_event,
                    row.action));
            }

            return new KingdomTeachingCatalog(
                file.quest_id,
                new KingdomTeachingEntry(
                    file.entry.id,
                    file.entry.title,
                    file.entry.what_to_do,
                    file.entry.location),
                new ReadOnlyCollection<KingdomTeachingStep>(steps));
        }

        private static bool IsValidEntry(KingdomTeachingEntryFile entry)
        {
            return entry != null &&
                   string.Equals(
                       entry.id,
                       EntryId,
                       StringComparison.Ordinal) &&
                   !string.IsNullOrWhiteSpace(entry.title) &&
                   !string.IsNullOrWhiteSpace(entry.what_to_do) &&
                   !string.IsNullOrWhiteSpace(entry.location) &&
                   !ContainsForbiddenDestination(entry.title) &&
                   !ContainsForbiddenDestination(entry.what_to_do) &&
                   !ContainsForbiddenDestination(entry.location);
        }

        private static bool ContainsForbiddenDestination(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   (value.IndexOf("warzone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("outer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("accordant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("zone_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("poi_", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Serializable]
        private sealed class KingdomTeachingCatalogFile
        {
            public string catalog_id;
            public string quest_id;
            public KingdomTeachingEntryFile entry;
            public KingdomTeachingStepFile[] steps;
        }

        [Serializable]
        private sealed class KingdomTeachingEntryFile
        {
            public string id;
            public string title;
            public string what_to_do;
            public string location;
        }

        [Serializable]
        private sealed class KingdomTeachingStepFile
        {
            public string id;
            public string title;
            public string what_to_do;
            public string location;
            public string interaction;
            public string completion_event;
            public string action;
        }
    }

    public sealed class KingdomTeachingState
    {
        internal KingdomTeachingState(
            bool isAvailable,
            bool isComplete,
            int progressValue,
            KingdomTeachingStep currentStep)
        {
            IsAvailable = isAvailable;
            IsComplete = isComplete;
            ProgressValue = progressValue;
            CurrentStep = currentStep;
        }

        public bool IsAvailable { get; }
        public bool IsComplete { get; }
        public int ProgressValue { get; }
        public KingdomTeachingStep CurrentStep { get; }
    }

    public static class KingdomTeachingQuestline
    {
        public static KingdomTeachingState Evaluate(
            SaveGameData save,
            KingdomTeachingCatalog catalog)
        {
            if (catalog == null ||
                save == null ||
                !ProofOfWorthLordship.IsGranted(save))
            {
                return new KingdomTeachingState(false, false, 0, null);
            }

            int progress = ReadProgress(save.Quests, catalog.QuestId);
            if (progress < 0)
            {
                return new KingdomTeachingState(false, false, 0, null);
            }

            if (progress >= catalog.Steps.Count)
            {
                return new KingdomTeachingState(true, true, catalog.Steps.Count, null);
            }

            return new KingdomTeachingState(
                true,
                false,
                progress,
                catalog.Steps[progress]);
        }

        private static int ReadProgress(
            IReadOnlyList<QuestState> quests,
            string questId)
        {
            if (quests == null)
            {
                return 0;
            }

            QuestState match = null;
            for (int index = 0; index < quests.Count; index++)
            {
                QuestState candidate = quests[index];
                if (candidate == null ||
                    !string.Equals(candidate.QuestId, questId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null || candidate.CurrentValue < 0)
                {
                    return -1;
                }

                match = candidate;
            }

            return match == null ? 0 : match.CurrentValue;
        }
    }
}
