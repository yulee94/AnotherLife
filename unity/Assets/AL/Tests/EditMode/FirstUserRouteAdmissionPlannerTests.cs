using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Core;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class FirstUserRouteAdmissionPlannerTests
    {
        private const int PredicateCount = 7;

        [TestCaseSource(nameof(OrderedPrefixCases))]
        public void OrderedPrefixHostWritableAndIntentMatrixResolvesDeterministically(
            int completedPredicates,
            bool hostReady,
            bool writable,
            FirstUserRouteIntent intent)
        {
            FirstUserRouteSnapshot snapshot = CreatePrefixSnapshot(
                completedPredicates,
                hostReady,
                writable);

            FirstUserRoutePlan plan = FirstUserRouteAdmissionPlanner.Plan(intent, snapshot);
            FirstUserJourneyStep expectedStep = StepForPrefix(completedPredicates);

            Assert.AreEqual(expectedStep, plan.JourneyStep);

            if (completedPredicates < PredicateCount)
            {
                Assert.AreEqual(FirstUserRoutePlanStatus.StepRequired, plan.Status);
                Assert.AreEqual(DestinationForStep(expectedStep), plan.Destination);
                Assert.AreEqual(
                    intent == FirstUserRouteIntent.ResolveNext
                        ? FirstUserRouteDiagnostic.None
                        : FirstUserRouteDiagnostic.DirectGameplayDenied,
                    plan.Diagnostic);
                Assert.IsFalse(plan.AllowsGameplay);
                Assert.IsFalse(plan.AllowsIsolatedCharacterGameTest);
                return;
            }

            if (!hostReady)
            {
                Assert.AreEqual(FirstUserRoutePlanStatus.AdmissionBlocked, plan.Status);
                Assert.AreEqual(FirstUserRouteDestination.HostReadiness, plan.Destination);
                Assert.AreEqual(FirstUserRouteDiagnostic.HostNotReady, plan.Diagnostic);
                Assert.IsFalse(plan.AllowsGameplay);
                Assert.IsFalse(plan.AllowsIsolatedCharacterGameTest);
                return;
            }

            if (!writable)
            {
                Assert.AreEqual(FirstUserRoutePlanStatus.AdmissionBlocked, plan.Status);
                Assert.AreEqual(FirstUserRouteDestination.WritableAuthority, plan.Destination);
                Assert.AreEqual(FirstUserRouteDiagnostic.WritableUnavailable, plan.Diagnostic);
                Assert.IsFalse(plan.AllowsGameplay);
                Assert.IsFalse(plan.AllowsIsolatedCharacterGameTest);
                return;
            }

            if (intent == FirstUserRouteIntent.RequestIsolatedCharacterGameTest)
            {
                Assert.AreEqual(FirstUserRoutePlanStatus.Rejected, plan.Status);
                Assert.AreEqual(FirstUserRouteDestination.None, plan.Destination);
                Assert.AreEqual(
                    FirstUserRouteDiagnostic.DevelopmentEvidenceRequired,
                    plan.Diagnostic);
                Assert.IsFalse(plan.AllowsGameplay);
                Assert.IsFalse(plan.AllowsIsolatedCharacterGameTest);
                return;
            }

            Assert.AreEqual(FirstUserRoutePlanStatus.GameplayAdmitted, plan.Status);
            Assert.AreEqual(FirstUserRouteDestination.Gameplay, plan.Destination);
            Assert.AreEqual(FirstUserRouteDiagnostic.None, plan.Diagnostic);
            Assert.IsTrue(plan.AllowsGameplay);
            Assert.IsFalse(plan.AllowsIsolatedCharacterGameTest);
        }

        [TestCaseSource(nameof(OutOfOrderEvidenceCases))]
        public void EveryOrderedDependencyEdgeViolationRejectsAtTheEarliestGap(
            int missingPredicate,
            int laterPredicate)
        {
            FirstUserRouteSnapshot snapshot = CreateOutOfOrderSnapshot(
                missingPredicate,
                laterPredicate);

            FirstUserRoutePlan plan = FirstUserRouteAdmissionPlanner.Plan(
                FirstUserRouteIntent.ResolveNext,
                snapshot);

            Assert.AreEqual(FirstUserRoutePlanStatus.Rejected, plan.Status);
            Assert.AreEqual(StepForPrefix(missingPredicate), plan.JourneyStep);
            Assert.AreEqual(FirstUserRouteDestination.None, plan.Destination);
            Assert.AreEqual(FirstUserRouteDiagnostic.EvidenceOutOfOrder, plan.Diagnostic);
            Assert.IsFalse(plan.AllowsGameplay);
        }

        [TestCaseSource(nameof(CursorEvidenceCases))]
        public void CursorEvidenceMatrixFailsClosedWithoutGrantingProgress(
            int completedPredicates,
            FirstUserRouteCursorState cursorState,
            FirstUserJourneyStep cursorStep,
            FirstUserRouteDiagnostic expectedDiagnostic)
        {
            var cursor = new FirstUserRouteCursorEvidence(cursorState, cursorStep);
            FirstUserRouteSnapshot snapshot = CreatePrefixSnapshot(
                completedPredicates,
                true,
                true,
                cursor);

            FirstUserRoutePlan plan = FirstUserRouteAdmissionPlanner.Plan(
                FirstUserRouteIntent.ResolveNext,
                snapshot);

            Assert.AreEqual(FirstUserRoutePlanStatus.Rejected, plan.Status);
            Assert.AreEqual(StepForPrefix(completedPredicates), plan.JourneyStep);
            Assert.AreEqual(FirstUserRouteDestination.None, plan.Destination);
            Assert.AreEqual(expectedDiagnostic, plan.Diagnostic);
            Assert.IsFalse(plan.AllowsGameplay);
        }

        [Test]
        public void DefaultSnapshotFailsClosedWithoutInventingFreshState()
        {
            FirstUserRoutePlan plan = FirstUserRouteAdmissionPlanner.Plan(
                FirstUserRouteIntent.ResolveNext,
                default(FirstUserRouteSnapshot));

            Assert.AreEqual(FirstUserRoutePlanStatus.Rejected, plan.Status);
            Assert.AreEqual(FirstUserJourneyStep.Realm, plan.JourneyStep);
            Assert.AreEqual(FirstUserRouteDestination.None, plan.Destination);
            Assert.AreEqual(FirstUserRouteDiagnostic.CursorMalformed, plan.Diagnostic);
            Assert.IsFalse(plan.AllowsGameplay);
        }

        [Test]
        public void InvalidIntentValuesRejectBeforeEvidenceInspection()
        {
            FirstUserRouteSnapshot contradictory = CreateOutOfOrderSnapshot(0, 1);
            FirstUserRouteIntent[] invalidIntents =
            {
                FirstUserRouteIntent.Invalid,
                (FirstUserRouteIntent)999
            };

            for (int index = 0; index < invalidIntents.Length; index++)
            {
                FirstUserRoutePlan plan = FirstUserRouteAdmissionPlanner.Plan(
                    invalidIntents[index],
                    contradictory);

                Assert.AreEqual(FirstUserRoutePlanStatus.Rejected, plan.Status);
                Assert.AreEqual(FirstUserJourneyStep.Invalid, plan.JourneyStep);
                Assert.AreEqual(FirstUserRouteDestination.None, plan.Destination);
                Assert.AreEqual(FirstUserRouteDiagnostic.IntentInvalid, plan.Diagnostic);
                Assert.IsFalse(plan.AllowsGameplay);
            }
        }

        [Test]
        public void FixtureInventoryAndAllPredicateBitmasksAcceptOnlyMonotonicPrefixes()
        {
            Assert.AreEqual(96, CountCases(OrderedPrefixCases()));
            Assert.AreEqual(21, CountCases(OutOfOrderEvidenceCases()));
            Assert.AreEqual(14, CountCases(CursorEvidenceCases()));
            Assert.AreEqual(141, 96 + 21 + 14 + 10);

            for (int mask = 0; mask < 1 << PredicateCount; mask++)
            {
                bool[] evidence = EvidenceFromMask(mask);
                int firstMissing = FirstMissingPredicate(evidence);
                bool isOrderedPrefix = IsOrderedPrefix(evidence);
                FirstUserJourneyStep expectedStep = StepForPrefix(firstMissing);
                var cursor = isOrderedPrefix
                    ? CursorForPrefix(firstMissing)
                    : new FirstUserRouteCursorEvidence(
                        FirstUserRouteCursorState.Matching,
                        expectedStep);
                FirstUserRouteSnapshot snapshot = CreateSnapshot(
                    evidence,
                    true,
                    true,
                    cursor);

                FirstUserRoutePlan plan = FirstUserRouteAdmissionPlanner.Plan(
                    FirstUserRouteIntent.ResolveNext,
                    snapshot);

                Assert.AreEqual(expectedStep, plan.JourneyStep, $"mask {mask}");
                if (!isOrderedPrefix)
                {
                    Assert.AreEqual(FirstUserRoutePlanStatus.Rejected, plan.Status, $"mask {mask}");
                    Assert.AreEqual(FirstUserRouteDestination.None, plan.Destination, $"mask {mask}");
                    Assert.AreEqual(
                        FirstUserRouteDiagnostic.EvidenceOutOfOrder,
                        plan.Diagnostic,
                        $"mask {mask}");
                    Assert.IsFalse(plan.AllowsGameplay, $"mask {mask}");
                    continue;
                }

                if (firstMissing == PredicateCount)
                {
                    Assert.AreEqual(
                        FirstUserRoutePlanStatus.GameplayAdmitted,
                        plan.Status,
                        $"mask {mask}");
                    Assert.AreEqual(FirstUserRouteDestination.Gameplay, plan.Destination, $"mask {mask}");
                    Assert.IsTrue(plan.AllowsGameplay, $"mask {mask}");
                }
                else
                {
                    Assert.AreEqual(
                        FirstUserRoutePlanStatus.StepRequired,
                        plan.Status,
                        $"mask {mask}");
                    Assert.AreEqual(DestinationForStep(expectedStep), plan.Destination, $"mask {mask}");
                    Assert.IsFalse(plan.AllowsGameplay, $"mask {mask}");
                }
            }
        }

        [Test]
        public void RepeatedPlanningIsValueDeterministicAndDoesNotMutateTheSnapshot()
        {
            FirstUserRouteSnapshot snapshot = CreatePrefixSnapshot(4, true, false);
            FirstUserRoutePlan expected = FirstUserRouteAdmissionPlanner.Plan(
                FirstUserRouteIntent.RequestGameplay,
                snapshot);

            for (int index = 0; index < 64; index++)
            {
                FirstUserRoutePlan actual = FirstUserRouteAdmissionPlanner.Plan(
                    FirstUserRouteIntent.RequestGameplay,
                    snapshot);

                AssertPlansEqual(expected, actual);
            }

            Assert.IsTrue(snapshot.RealmValidated);
            Assert.IsTrue(snapshot.OriginRaceValidated);
            Assert.IsTrue(snapshot.ClassSelectionValidated);
            Assert.IsTrue(snapshot.CustomizationValidated);
            Assert.IsFalse(snapshot.HandleValidated);
            Assert.IsFalse(snapshot.AuthoritativeReceiptVerified);
            Assert.IsFalse(snapshot.LocalProjectionVerified);
            Assert.IsTrue(snapshot.HostReady);
            Assert.IsFalse(snapshot.Writable);
            Assert.AreEqual(
                FirstUserRouteEvidenceOrigin.ProductionAuthority,
                snapshot.EvidenceOrigin);
            Assert.AreEqual(FirstUserRouteCursorState.Matching, snapshot.Cursor.State);
            Assert.AreEqual(FirstUserJourneyStep.Handle, snapshot.Cursor.Step);
        }

        [Test]
        public void DevelopmentEmulatorEvidenceIsCappedAtIsolatedCharacterGameTest()
        {
            FirstUserRouteSnapshot snapshot = CreatePrefixSnapshot(
                PredicateCount,
                true,
                true,
                null,
                FirstUserRouteEvidenceOrigin.DevelopmentEmulatorV1);

            FirstUserRoutePlan resolved = FirstUserRouteAdmissionPlanner.Plan(
                FirstUserRouteIntent.ResolveNext,
                snapshot);
            FirstUserRoutePlan isolated = FirstUserRouteAdmissionPlanner.Plan(
                FirstUserRouteIntent.RequestIsolatedCharacterGameTest,
                snapshot);
            FirstUserRoutePlan production = FirstUserRouteAdmissionPlanner.Plan(
                FirstUserRouteIntent.RequestGameplay,
                snapshot);

            Assert.AreEqual(
                FirstUserRoutePlanStatus.IsolatedCharacterGameTestEligible,
                resolved.Status);
            Assert.AreEqual(
                FirstUserRouteDestination.IsolatedCharacterGameTest,
                resolved.Destination);
            Assert.IsTrue(resolved.AllowsIsolatedCharacterGameTest);
            Assert.IsFalse(resolved.AllowsGameplay);
            AssertPlansEqual(resolved, isolated);

            Assert.AreEqual(FirstUserRoutePlanStatus.Rejected, production.Status);
            Assert.AreEqual(FirstUserRouteDestination.None, production.Destination);
            Assert.AreEqual(
                FirstUserRouteDiagnostic.DevelopmentEvidenceCeiling,
                production.Diagnostic);
            Assert.IsFalse(production.AllowsGameplay);
            Assert.IsFalse(production.AllowsIsolatedCharacterGameTest);
        }

        [Test]
        public void DevelopmentEvidenceStillRequiresEveryOrderedPredicateHostAndWritableGate()
        {
            for (int completed = 0; completed <= PredicateCount; completed++)
            {
                for (int host = 0; host <= 1; host++)
                {
                    for (int writable = 0; writable <= 1; writable++)
                    {
                        FirstUserRouteSnapshot snapshot = CreatePrefixSnapshot(
                            completed,
                            host == 1,
                            writable == 1,
                            null,
                            FirstUserRouteEvidenceOrigin.DevelopmentEmulatorV1);
                        FirstUserRoutePlan plan = FirstUserRouteAdmissionPlanner.Plan(
                            FirstUserRouteIntent.RequestIsolatedCharacterGameTest,
                            snapshot);

                        Assert.AreEqual(StepForPrefix(completed), plan.JourneyStep);
                        if (completed < PredicateCount)
                        {
                            Assert.AreEqual(FirstUserRoutePlanStatus.StepRequired, plan.Status);
                            Assert.AreEqual(
                                DestinationForStep(StepForPrefix(completed)),
                                plan.Destination);
                            Assert.AreEqual(
                                FirstUserRouteDiagnostic.DirectGameplayDenied,
                                plan.Diagnostic);
                        }
                        else if (host == 0)
                        {
                            Assert.AreEqual(FirstUserRoutePlanStatus.AdmissionBlocked, plan.Status);
                            Assert.AreEqual(FirstUserRouteDestination.HostReadiness, plan.Destination);
                            Assert.AreEqual(FirstUserRouteDiagnostic.HostNotReady, plan.Diagnostic);
                        }
                        else if (writable == 0)
                        {
                            Assert.AreEqual(FirstUserRoutePlanStatus.AdmissionBlocked, plan.Status);
                            Assert.AreEqual(FirstUserRouteDestination.WritableAuthority, plan.Destination);
                            Assert.AreEqual(FirstUserRouteDiagnostic.WritableUnavailable, plan.Diagnostic);
                        }
                        else
                        {
                            Assert.AreEqual(
                                FirstUserRoutePlanStatus.IsolatedCharacterGameTestEligible,
                                plan.Status);
                            Assert.IsTrue(plan.AllowsIsolatedCharacterGameTest);
                        }

                        Assert.IsFalse(plan.AllowsGameplay);
                    }
                }
            }
        }

        [Test]
        public void InvalidEvidenceOriginFailsClosedAfterAllNineAdmissionFactsPass()
        {
            FirstUserRouteEvidenceOrigin[] invalidOrigins =
            {
                FirstUserRouteEvidenceOrigin.Invalid,
                (FirstUserRouteEvidenceOrigin)999
            };

            for (int index = 0; index < invalidOrigins.Length; index++)
            {
                FirstUserRouteSnapshot snapshot = CreatePrefixSnapshot(
                    PredicateCount,
                    true,
                    true,
                    null,
                    invalidOrigins[index]);
                FirstUserRoutePlan plan = FirstUserRouteAdmissionPlanner.Plan(
                    FirstUserRouteIntent.ResolveNext,
                    snapshot);

                Assert.AreEqual(FirstUserRoutePlanStatus.Rejected, plan.Status);
                Assert.AreEqual(FirstUserJourneyStep.Complete, plan.JourneyStep);
                Assert.AreEqual(FirstUserRouteDestination.None, plan.Destination);
                Assert.AreEqual(
                    FirstUserRouteDiagnostic.EvidenceOriginInvalid,
                    plan.Diagnostic);
                Assert.IsFalse(plan.AllowsGameplay);
                Assert.IsFalse(plan.AllowsIsolatedCharacterGameTest);
            }
        }

        [Test]
        public void RequestKingdomIsUnconditionallyDeniedWithoutAppointmentAndGrantAuthority()
        {
            FirstUserRouteEvidenceOrigin[] origins =
            {
                FirstUserRouteEvidenceOrigin.Invalid,
                FirstUserRouteEvidenceOrigin.ProductionAuthority,
                FirstUserRouteEvidenceOrigin.DevelopmentEmulatorV1,
                (FirstUserRouteEvidenceOrigin)999
            };

            for (int mask = 0; mask < 1 << PredicateCount; mask++)
            {
                bool[] evidence = EvidenceFromMask(mask);
                for (int host = 0; host <= 1; host++)
                {
                    for (int writable = 0; writable <= 1; writable++)
                    {
                        for (int originIndex = 0; originIndex < origins.Length; originIndex++)
                        {
                            FirstUserRouteSnapshot snapshot = CreateSnapshot(
                                evidence,
                                host == 1,
                                writable == 1,
                                default(FirstUserRouteCursorEvidence),
                                origins[originIndex]);
                            FirstUserRoutePlan plan = FirstUserRouteAdmissionPlanner.Plan(
                                FirstUserRouteIntent.RequestKingdom,
                                snapshot);

                            Assert.AreEqual(FirstUserRoutePlanStatus.Rejected, plan.Status);
                            Assert.AreEqual(FirstUserJourneyStep.Invalid, plan.JourneyStep);
                            Assert.AreEqual(FirstUserRouteDestination.None, plan.Destination);
                            Assert.AreEqual(
                                FirstUserRouteDiagnostic.KingdomAuthorityUnavailable,
                                plan.Diagnostic);
                            Assert.IsFalse(plan.AllowsGameplay);
                            Assert.IsFalse(plan.AllowsIsolatedCharacterGameTest);
                        }
                    }
                }
            }

            Assert.IsFalse(Enum.IsDefined(typeof(FirstUserRouteDestination), "Kingdom"));
        }

        [Test]
        public void PublicContractIsImmutableOpaqueAndContainsNoRuntimeOrUnityAuthority()
        {
            Type[] immutableTypes =
            {
                typeof(FirstUserRouteCursorEvidence),
                typeof(FirstUserRouteSnapshot),
                typeof(FirstUserRoutePlan)
            };

            for (int typeIndex = 0; typeIndex < immutableTypes.Length; typeIndex++)
            {
                Type type = immutableTypes[typeIndex];
                Assert.Zero(
                    type.GetFields(BindingFlags.Public | BindingFlags.Instance).Length,
                    type.FullName);

                PropertyInfo[] properties = type.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance);
                for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
                {
                    Assert.IsNull(
                        properties[propertyIndex].GetSetMethod(false),
                        $"{type.FullName}.{properties[propertyIndex].Name}");
                }
            }

            Assert.Zero(
                typeof(FirstUserRoutePlan).GetConstructors(
                    BindingFlags.Public | BindingFlags.Instance).Length);

            string[] snapshotProperties = typeof(FirstUserRouteSnapshot)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] expectedSnapshotProperties =
            {
                nameof(FirstUserRouteSnapshot.AuthoritativeReceiptVerified),
                nameof(FirstUserRouteSnapshot.ClassSelectionValidated),
                nameof(FirstUserRouteSnapshot.Cursor),
                nameof(FirstUserRouteSnapshot.CustomizationValidated),
                nameof(FirstUserRouteSnapshot.EvidenceOrigin),
                nameof(FirstUserRouteSnapshot.HandleValidated),
                nameof(FirstUserRouteSnapshot.HostReady),
                nameof(FirstUserRouteSnapshot.LocalProjectionVerified),
                nameof(FirstUserRouteSnapshot.OriginRaceValidated),
                nameof(FirstUserRouteSnapshot.RealmValidated),
                nameof(FirstUserRouteSnapshot.Writable)
            };
            Array.Sort(expectedSnapshotProperties, StringComparer.Ordinal);
            CollectionAssert.AreEqual(expectedSnapshotProperties, snapshotProperties);

            Type[] publicContractTypes =
            {
                typeof(FirstUserRouteCursorEvidence),
                typeof(FirstUserRouteEvidenceOrigin),
                typeof(FirstUserRouteSnapshot),
                typeof(FirstUserRoutePlan),
                typeof(FirstUserRouteAdmissionPlanner)
            };
            for (int typeIndex = 0; typeIndex < publicContractTypes.Length; typeIndex++)
            {
                AssertPublicSurfaceHasNoUnityOrDelegateTypes(publicContractTypes[typeIndex]);
            }

            string corePath = Path.Combine(Application.dataPath, "AL", "Scripts", "Core");
            string contractsSource = File.ReadAllText(Path.Combine(corePath, "FirstUserRouteContracts.cs"));
            string plannerSource = File.ReadAllText(Path.Combine(corePath, "FirstUserRouteAdmissionPlanner.cs"));
            string combined = contractsSource + plannerSource;
            string[] forbiddenTokens =
            {
                "UnityEngine",
                "SceneManager",
                "RealmId",
                "ClassFamily",
                "SubclassId",
                "ServiceLocator",
                "SaveGameData",
                "PlayerPrefs",
                "System.IO",
                "System.Net",
                "DateTime",
                "Guid",
                "Random",
                "Task<",
                "Action<",
                "Func<",
                " event "
            };
            for (int tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
            {
                StringAssert.DoesNotContain(forbiddenTokens[tokenIndex], combined);
            }
        }

        [Test]
        public void WarmPlannerInvocationAllocatesNothingAndHasNoObservableEffects()
        {
            FirstUserRouteSnapshot snapshot = CreatePrefixSnapshot(7, true, true);
            for (int warmup = 0; warmup < 256; warmup++)
            {
                FirstUserRouteAdmissionPlanner.Plan(
                    FirstUserRouteIntent.RequestGameplay,
                    snapshot);
            }

            FirstUserRoutePlan last = default(FirstUserRoutePlan);
            bool everyPlanAdmitted = true;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 4096; iteration++)
            {
                last = FirstUserRouteAdmissionPlanner.Plan(
                    FirstUserRouteIntent.RequestGameplay,
                    snapshot);
                everyPlanAdmitted &= last.AllowsGameplay;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.IsTrue(everyPlanAdmitted);
            Assert.AreEqual(FirstUserRoutePlanStatus.GameplayAdmitted, last.Status);
            Assert.IsTrue(snapshot.AuthoritativeReceiptVerified);
            Assert.IsTrue(snapshot.LocalProjectionVerified);
            Assert.IsTrue(snapshot.HostReady);
            Assert.IsTrue(snapshot.Writable);
            GC.KeepAlive(last);
        }

        private static IEnumerable<TestCaseData> OrderedPrefixCases()
        {
            FirstUserRouteIntent[] intents =
            {
                FirstUserRouteIntent.ResolveNext,
                FirstUserRouteIntent.RequestIsolatedCharacterGameTest,
                FirstUserRouteIntent.RequestGameplay
            };

            for (int completed = 0; completed <= PredicateCount; completed++)
            {
                for (int host = 0; host <= 1; host++)
                {
                    for (int writable = 0; writable <= 1; writable++)
                    {
                        for (int intentIndex = 0; intentIndex < intents.Length; intentIndex++)
                        {
                            FirstUserRouteIntent intent = intents[intentIndex];
                            yield return new TestCaseData(
                                    completed,
                                    host == 1,
                                    writable == 1,
                                    intent)
                                .SetName(
                                    $"Prefix_{completed}_Host_{host}_Writable_{writable}_{intent}");
                        }
                    }
                }
            }
        }

        private static IEnumerable<TestCaseData> OutOfOrderEvidenceCases()
        {
            for (int missing = 0; missing < PredicateCount; missing++)
            {
                for (int later = missing + 1; later < PredicateCount; later++)
                {
                    yield return new TestCaseData(missing, later)
                        .SetName(
                            $"Gap_{StepForPrefix(missing)}_Later_{StepForPrefix(later)}");
                }
            }
        }

        private static IEnumerable<TestCaseData> CursorEvidenceCases()
        {
            yield return CursorCase(
                "DefaultState",
                0,
                FirstUserRouteCursorState.Invalid,
                FirstUserJourneyStep.Invalid,
                FirstUserRouteDiagnostic.CursorMalformed);
            yield return CursorCase(
                "UndefinedState",
                0,
                (FirstUserRouteCursorState)999,
                FirstUserJourneyStep.Invalid,
                FirstUserRouteDiagnostic.CursorMalformed);
            yield return CursorCase(
                "MissingCarriesStep",
                0,
                FirstUserRouteCursorState.Missing,
                FirstUserJourneyStep.Realm,
                FirstUserRouteDiagnostic.CursorMalformed);
            yield return CursorCase(
                "MissingAfterProgress",
                1,
                FirstUserRouteCursorState.Missing,
                FirstUserJourneyStep.Invalid,
                FirstUserRouteDiagnostic.CursorMissing);
            yield return CursorCase(
                "MatchingHasNoStep",
                3,
                FirstUserRouteCursorState.Matching,
                FirstUserJourneyStep.Invalid,
                FirstUserRouteDiagnostic.CursorMalformed);
            yield return CursorCase(
                "MatchingHasUndefinedStep",
                3,
                FirstUserRouteCursorState.Matching,
                (FirstUserJourneyStep)999,
                FirstUserRouteDiagnostic.CursorMalformed);
            yield return CursorCase(
                "MatchingTrailsExpectedStep",
                3,
                FirstUserRouteCursorState.Matching,
                FirstUserJourneyStep.ClassSelection,
                FirstUserRouteDiagnostic.CursorConflict);
            yield return CursorCase(
                "MatchingLeadsExpectedStep",
                3,
                FirstUserRouteCursorState.Matching,
                FirstUserJourneyStep.Handle,
                FirstUserRouteDiagnostic.CursorConflict);
            yield return CursorCase(
                "TypedStale",
                3,
                FirstUserRouteCursorState.Stale,
                FirstUserJourneyStep.Customization,
                FirstUserRouteDiagnostic.CursorStale);
            yield return CursorCase(
                "TypedForward",
                3,
                FirstUserRouteCursorState.Forward,
                FirstUserJourneyStep.Customization,
                FirstUserRouteDiagnostic.CursorForward);
            yield return CursorCase(
                "TypedMalformed",
                3,
                FirstUserRouteCursorState.Malformed,
                FirstUserJourneyStep.Invalid,
                FirstUserRouteDiagnostic.CursorMalformed);
            yield return CursorCase(
                "TypedConflict",
                3,
                FirstUserRouteCursorState.Conflict,
                FirstUserJourneyStep.Customization,
                FirstUserRouteDiagnostic.CursorConflict);
            yield return CursorCase(
                "StaleHasUndefinedStep",
                3,
                FirstUserRouteCursorState.Stale,
                (FirstUserJourneyStep)999,
                FirstUserRouteDiagnostic.CursorMalformed);
            yield return CursorCase(
                "ForwardHasNoStep",
                3,
                FirstUserRouteCursorState.Forward,
                FirstUserJourneyStep.Invalid,
                FirstUserRouteDiagnostic.CursorMalformed);
        }

        private static TestCaseData CursorCase(
            string name,
            int completedPredicates,
            FirstUserRouteCursorState cursorState,
            FirstUserJourneyStep cursorStep,
            FirstUserRouteDiagnostic expectedDiagnostic)
        {
            return new TestCaseData(
                    completedPredicates,
                    cursorState,
                    cursorStep,
                    expectedDiagnostic)
                .SetName($"Cursor_{name}");
        }

        private static FirstUserRouteSnapshot CreatePrefixSnapshot(
            int completedPredicates,
            bool hostReady,
            bool writable,
            FirstUserRouteCursorEvidence? cursorOverride = null,
            FirstUserRouteEvidenceOrigin evidenceOrigin =
                FirstUserRouteEvidenceOrigin.ProductionAuthority)
        {
            bool[] evidence = new bool[PredicateCount];
            for (int index = 0; index < completedPredicates; index++)
            {
                evidence[index] = true;
            }

            FirstUserRouteCursorEvidence cursor = cursorOverride ??
                CursorForPrefix(completedPredicates);
            return CreateSnapshot(
                evidence,
                hostReady,
                writable,
                cursor,
                evidenceOrigin);
        }

        private static FirstUserRouteSnapshot CreateOutOfOrderSnapshot(
            int missingPredicate,
            int laterPredicate)
        {
            bool[] evidence = new bool[PredicateCount];
            for (int index = 0; index < missingPredicate; index++)
            {
                evidence[index] = true;
            }

            evidence[laterPredicate] = true;
            var cursor = new FirstUserRouteCursorEvidence(
                FirstUserRouteCursorState.Matching,
                StepForPrefix(missingPredicate));
            return CreateSnapshot(
                evidence,
                true,
                true,
                cursor,
                FirstUserRouteEvidenceOrigin.ProductionAuthority);
        }

        private static FirstUserRouteSnapshot CreateSnapshot(
            bool[] evidence,
            bool hostReady,
            bool writable,
            FirstUserRouteCursorEvidence cursor,
            FirstUserRouteEvidenceOrigin evidenceOrigin =
                FirstUserRouteEvidenceOrigin.ProductionAuthority)
        {
            Assert.AreEqual(PredicateCount, evidence.Length);
            return new FirstUserRouteSnapshot(
                evidence[0],
                evidence[1],
                evidence[2],
                evidence[3],
                evidence[4],
                evidence[5],
                evidence[6],
                hostReady,
                writable,
                evidenceOrigin,
                cursor);
        }

        private static FirstUserRouteCursorEvidence CursorForPrefix(int completedPredicates)
        {
            return completedPredicates == 0
                ? new FirstUserRouteCursorEvidence(
                    FirstUserRouteCursorState.Missing,
                    FirstUserJourneyStep.Invalid)
                : new FirstUserRouteCursorEvidence(
                    FirstUserRouteCursorState.Matching,
                    StepForPrefix(completedPredicates));
        }

        private static FirstUserJourneyStep StepForPrefix(int completedPredicates)
        {
            switch (completedPredicates)
            {
                case 0:
                    return FirstUserJourneyStep.Realm;
                case 1:
                    return FirstUserJourneyStep.OriginRace;
                case 2:
                    return FirstUserJourneyStep.ClassSelection;
                case 3:
                    return FirstUserJourneyStep.Customization;
                case 4:
                    return FirstUserJourneyStep.Handle;
                case 5:
                    return FirstUserJourneyStep.AuthoritativeReceipt;
                case 6:
                    return FirstUserJourneyStep.LocalProjection;
                case 7:
                    return FirstUserJourneyStep.Complete;
                default:
                    throw new ArgumentOutOfRangeException(nameof(completedPredicates));
            }
        }

        private static FirstUserRouteDestination DestinationForStep(FirstUserJourneyStep step)
        {
            switch (step)
            {
                case FirstUserJourneyStep.Realm:
                    return FirstUserRouteDestination.Realm;
                case FirstUserJourneyStep.OriginRace:
                    return FirstUserRouteDestination.OriginRace;
                case FirstUserJourneyStep.ClassSelection:
                    return FirstUserRouteDestination.ClassSelection;
                case FirstUserJourneyStep.Customization:
                    return FirstUserRouteDestination.Customization;
                case FirstUserJourneyStep.Handle:
                    return FirstUserRouteDestination.Handle;
                case FirstUserJourneyStep.AuthoritativeReceipt:
                    return FirstUserRouteDestination.AuthoritativeReceipt;
                case FirstUserJourneyStep.LocalProjection:
                    return FirstUserRouteDestination.LocalProjection;
                default:
                    return FirstUserRouteDestination.None;
            }
        }

        private static bool[] EvidenceFromMask(int mask)
        {
            var evidence = new bool[PredicateCount];
            for (int index = 0; index < PredicateCount; index++)
            {
                evidence[index] = (mask & (1 << index)) != 0;
            }

            return evidence;
        }

        private static int FirstMissingPredicate(bool[] evidence)
        {
            for (int index = 0; index < evidence.Length; index++)
            {
                if (!evidence[index])
                {
                    return index;
                }
            }

            return PredicateCount;
        }

        private static bool IsOrderedPrefix(bool[] evidence)
        {
            bool foundGap = false;
            for (int index = 0; index < evidence.Length; index++)
            {
                if (!evidence[index])
                {
                    foundGap = true;
                    continue;
                }

                if (foundGap)
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountCases(IEnumerable<TestCaseData> cases)
        {
            int count = 0;
            foreach (TestCaseData ignored in cases)
            {
                count++;
            }

            return count;
        }

        private static void AssertPlansEqual(
            FirstUserRoutePlan expected,
            FirstUserRoutePlan actual)
        {
            Assert.AreEqual(expected.Status, actual.Status);
            Assert.AreEqual(expected.JourneyStep, actual.JourneyStep);
            Assert.AreEqual(expected.Destination, actual.Destination);
            Assert.AreEqual(expected.Diagnostic, actual.Diagnostic);
            Assert.AreEqual(expected.AllowsGameplay, actual.AllowsGameplay);
            Assert.AreEqual(
                expected.AllowsIsolatedCharacterGameTest,
                actual.AllowsIsolatedCharacterGameTest);
        }

        private static void AssertPublicSurfaceHasNoUnityOrDelegateTypes(Type contractType)
        {
            PropertyInfo[] properties = contractType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
            {
                AssertSafePublicType(properties[propertyIndex].PropertyType, properties[propertyIndex].Name);
            }

            MethodInfo[] methods = contractType.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly);
            for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
            {
                MethodInfo method = methods[methodIndex];
                AssertSafePublicType(method.ReturnType, method.Name);
                ParameterInfo[] parameters = method.GetParameters();
                for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    AssertSafePublicType(parameters[parameterIndex].ParameterType, method.Name);
                }
            }
        }

        private static void AssertSafePublicType(Type type, string memberName)
        {
            string typeNamespace = type.Namespace ?? string.Empty;
            Assert.IsFalse(
                typeNamespace.StartsWith("UnityEngine", StringComparison.Ordinal),
                memberName);
            Assert.IsFalse(typeof(Delegate).IsAssignableFrom(type), memberName);
        }
    }
}
