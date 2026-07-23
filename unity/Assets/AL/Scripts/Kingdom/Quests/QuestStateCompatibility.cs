using System;
using System.Collections.Generic;
using AL.Core.Interfaces;
using UnityEngine;

namespace AL.Services.Local
{
    [Flags]
    internal enum QuestStateCompatibilityIssues
    {
        None = 0,
        NullState = 1 << 0,
        BlankId = 1 << 1,
        DuplicateId = 1 << 2,
        UnknownDefinition = 1 << 3,
        ContradictoryState = 1 << 4
    }

    internal static class QuestStateCompatibility
    {
        private static readonly QuestState[] EmptyStates = Array.Empty<QuestState>();

        public static QuestState[] CreateSupportedView<TDefinition>(
            IReadOnlyList<QuestState> states,
            IReadOnlyDictionary<string, TDefinition> definitions,
            Func<TDefinition, string> idSelector,
            Func<TDefinition, int> targetSelector,
            out QuestStateCompatibilityIssues issues)
            where TDefinition : class
        {
            issues = QuestStateCompatibilityIssues.None;
            if (states == null || states.Count == 0)
            {
                return EmptyStates;
            }

            var idCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < states.Count; index++)
            {
                QuestState state = states[index];
                if (state == null)
                {
                    issues |= QuestStateCompatibilityIssues.NullState;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(state.QuestId))
                {
                    issues |= QuestStateCompatibilityIssues.BlankId;
                    continue;
                }

                idCounts.TryGetValue(state.QuestId, out int count);
                idCounts[state.QuestId] = count < 2 ? count + 1 : 2;
            }

            List<QuestState> supported = null;
            for (int index = 0; index < states.Count; index++)
            {
                QuestState state = states[index];
                if (state == null || string.IsNullOrWhiteSpace(state.QuestId))
                {
                    continue;
                }

                if (!idCounts.TryGetValue(state.QuestId, out int count) || count != 1)
                {
                    issues |= QuestStateCompatibilityIssues.DuplicateId;
                    continue;
                }

                if (definitions == null ||
                    !definitions.TryGetValue(state.QuestId, out TDefinition definition) ||
                    definition == null ||
                    !string.Equals(idSelector(definition), state.QuestId, StringComparison.Ordinal))
                {
                    issues |= QuestStateCompatibilityIssues.UnknownDefinition;
                    continue;
                }

                if (!IsConsistent(state, targetSelector(definition)))
                {
                    issues |= QuestStateCompatibilityIssues.ContradictoryState;
                    continue;
                }

                supported ??= new List<QuestState>();
                supported.Add(state);
            }

            return supported == null ? EmptyStates : supported.ToArray();
        }

        public static bool ContainsExactId(IReadOnlyList<QuestState> states, string questId)
        {
            if (states == null || string.IsNullOrWhiteSpace(questId))
            {
                return false;
            }

            for (int index = 0; index < states.Count; index++)
            {
                QuestState state = states[index];
                if (state != null && string.Equals(state.QuestId, questId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsConsistent(QuestState state, int targetValue)
        {
            if (targetValue <= 0 || state.CurrentValue < 0 || state.CurrentValue > targetValue)
            {
                return false;
            }

            if (state.IsClaimed && !state.IsCompleted)
            {
                return false;
            }

            return state.IsCompleted
                ? state.CurrentValue == targetValue
                : state.CurrentValue < targetValue;
        }
    }

    internal static class QuestStateCompatibilityDiagnostics
    {
        public static void ReportOnce(
            ref QuestStateCompatibilityIssues reported,
            QuestStateCompatibilityIssues observed,
            string serviceName)
        {
            QuestStateCompatibilityIssues pending = observed & ~reported;
            reported |= observed;

            if ((pending & QuestStateCompatibilityIssues.NullState) != 0)
            {
                Debug.LogWarning($"[AL-QST-NULL-STATE] {serviceName} preserved and disabled one or more null quest rows.");
            }

            if ((pending & QuestStateCompatibilityIssues.BlankId) != 0)
            {
                Debug.LogWarning($"[AL-QST-INVALID-ID] {serviceName} preserved and disabled one or more blank-id quest rows.");
            }

            if ((pending & QuestStateCompatibilityIssues.DuplicateId) != 0)
            {
                Debug.LogWarning($"[AL-QST-DUPLICATE-ID] {serviceName} preserved and disabled every row in one or more duplicate-id groups.");
            }

            if ((pending & QuestStateCompatibilityIssues.UnknownDefinition) != 0)
            {
                Debug.LogWarning($"[AL-QST-UNKNOWN-ID] {serviceName} preserved and disabled one or more definitionless quest rows.");
            }

            if ((pending & QuestStateCompatibilityIssues.ContradictoryState) != 0)
            {
                Debug.LogWarning($"[AL-QST-CONTRADICTORY-STATE] {serviceName} preserved and disabled one or more contradictory quest rows.");
            }
        }
    }
}
