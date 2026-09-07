using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;

namespace AL.Data.Runtime
{
    public enum KingdomOneBuildPrepareDisposition
    {
        Prepared = 0,
        Duplicate = 1,
        Rejected = 2
    }

    public readonly struct KingdomOneBuildCommitRequest
    {
        public KingdomOneBuildCommitRequest(
            string transactionId,
            RealmId expectedRealm)
        {
            TransactionId = transactionId ?? string.Empty;
            ExpectedRealm = expectedRealm;
        }

        public string TransactionId { get; }
        public RealmId ExpectedRealm { get; }
    }

    /// <summary>
    /// Schema-v2 mutation codec for the single approved Kingdom construct.
    /// Callers cannot choose a building id or level.
    /// </summary>
    public static class KingdomOneBuildSaveCodec
    {
        public const string BuildingId = MvpLoopSaveCodec.DefaultOneBuildId;
        public const int CompletedLevel = 1;

        public static KingdomOneBuildPrepareDisposition PrepareCandidate(
            SaveGameData candidate,
            KingdomOneBuildCommitRequest request,
            out string message)
        {
            message = string.Empty;
            if (candidate == null ||
                string.IsNullOrWhiteSpace(request.TransactionId) ||
                request.ExpectedRealm == RealmId.None ||
                candidate.SelectedRealm != request.ExpectedRealm)
            {
                message = "AL-KINGDOM-ONE-BUILD-REQUEST-INVALID";
                return KingdomOneBuildPrepareDisposition.Rejected;
            }

            MvpLoopSnapshot identity = MvpLoopSaveCodec.Read(candidate);
            if (!identity.HasConfirmedChampion ||
                !AL.ChampionMode.Quests.ProofOfWorthLordship.IsGranted(candidate))
            {
                message = "AL-KINGDOM-ONE-BUILD-LORDSHIP-REQUIRED";
                return KingdomOneBuildPrepareDisposition.Rejected;
            }

            candidate.Buildings ??= new List<BuildingState>();
            BuildingState existing = null;
            for (int index = 0; index < candidate.Buildings.Count; index++)
            {
                BuildingState building = candidate.Buildings[index];
                if (building == null ||
                    !string.Equals(
                        building.BuildingId,
                        BuildingId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (existing != null || building.Level != CompletedLevel)
                {
                    message = "AL-KINGDOM-ONE-BUILD-STATE-CONFLICT";
                    return KingdomOneBuildPrepareDisposition.Rejected;
                }

                existing = building;
            }

            if (existing != null)
            {
                return KingdomOneBuildPrepareDisposition.Duplicate;
            }

            candidate.Buildings.Add(new BuildingState
            {
                BuildingId = BuildingId,
                Level = CompletedLevel
            });
            return KingdomOneBuildPrepareDisposition.Prepared;
        }
    }

    public enum KingdomTeachingPrepareDisposition
    {
        Prepared = 0,
        Duplicate = 1,
        Rejected = 2
    }

    public readonly struct KingdomTeachingCommitRequest
    {
        public KingdomTeachingCommitRequest(
            string transactionId,
            RealmId expectedRealm,
            string questId,
            string stepId,
            string completionEvent,
            int expectedProgress,
            int nextProgress,
            int stepCount)
        {
            TransactionId = transactionId ?? string.Empty;
            ExpectedRealm = expectedRealm;
            QuestId = questId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            CompletionEvent = completionEvent ?? string.Empty;
            ExpectedProgress = expectedProgress;
            NextProgress = nextProgress;
            StepCount = stepCount;
        }

        public string TransactionId { get; }
        public RealmId ExpectedRealm { get; }
        public string QuestId { get; }
        public string StepId { get; }
        public string CompletionEvent { get; }
        public int ExpectedProgress { get; }
        public int NextProgress { get; }
        public int StepCount { get; }
    }

    public static class KingdomTeachingSaveCodec
    {
        public const string PersistenceSlot = "SaveGameData.Quests";
        public const string TownHallBuildingId = MvpLoopSaveCodec.DefaultOneBuildId;
        public const int MaximumStepCount = 16;

        public static KingdomTeachingPrepareDisposition PrepareCandidate(
            SaveGameData candidate,
            KingdomTeachingCommitRequest request,
            bool requiresTownHall,
            out string message)
        {
            message = string.Empty;
            if (candidate == null ||
                string.IsNullOrWhiteSpace(request.TransactionId) ||
                candidate.SelectedRealm != request.ExpectedRealm ||
                request.ExpectedRealm == RealmId.None ||
                !IsCanonicalInternalId(request.QuestId, "quest_") ||
                !IsCanonicalInternalId(request.StepId, "teach_") ||
                !IsCanonicalInternalId(request.CompletionEvent, string.Empty) ||
                request.StepCount < 1 ||
                request.StepCount > MaximumStepCount ||
                request.ExpectedProgress < 0 ||
                request.NextProgress != request.ExpectedProgress + 1 ||
                request.NextProgress > request.StepCount)
            {
                message = "AL-KINGDOM-TEACHING-REQUEST-INVALID";
                return KingdomTeachingPrepareDisposition.Rejected;
            }

            if (requiresTownHall && !HasTownHall(candidate))
            {
                message = "AL-KINGDOM-TEACHING-BUILD-REQUIRED";
                return KingdomTeachingPrepareDisposition.Rejected;
            }

            candidate.Quests ??= new List<QuestState>();
            QuestState state = null;
            for (int index = 0; index < candidate.Quests.Count; index++)
            {
                QuestState current = candidate.Quests[index];
                if (current == null ||
                    !string.Equals(current.QuestId, request.QuestId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (state != null)
                {
                    message = "AL-KINGDOM-TEACHING-STATE-CONFLICT";
                    return KingdomTeachingPrepareDisposition.Rejected;
                }

                state = current;
            }

            if (state != null &&
                state.CurrentValue == request.NextProgress &&
                state.IsCompleted == (request.NextProgress == request.StepCount) &&
                !state.IsClaimed)
            {
                return KingdomTeachingPrepareDisposition.Duplicate;
            }

            int currentProgress = state == null ? 0 : state.CurrentValue;
            if (currentProgress != request.ExpectedProgress ||
                state != null && (state.IsCompleted || state.IsClaimed))
            {
                message = "AL-KINGDOM-TEACHING-ORDER-CONFLICT";
                return KingdomTeachingPrepareDisposition.Rejected;
            }

            if (state == null)
            {
                state = new QuestState
                {
                    QuestId = request.QuestId
                };
                candidate.Quests.Add(state);
            }

            state.CurrentValue = request.NextProgress;
            state.IsCompleted = request.NextProgress == request.StepCount;
            state.IsClaimed = false;
            return KingdomTeachingPrepareDisposition.Prepared;
        }

        private static bool HasTownHall(SaveGameData save)
        {
            if (save?.Buildings == null)
            {
                return false;
            }

            for (int index = 0; index < save.Buildings.Count; index++)
            {
                BuildingState building = save.Buildings[index];
                if (building != null &&
                    string.Equals(
                        building.BuildingId,
                        TownHallBuildingId,
                        StringComparison.Ordinal) &&
                    building.Level >= 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCanonicalInternalId(string value, string requiredPrefix)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > 96 ||
                !value.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if ((character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') &&
                    character != '_')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
