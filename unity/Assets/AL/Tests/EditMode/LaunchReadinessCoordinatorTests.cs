using System.Reflection;
using AL.Core.Interfaces;
using AL.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.Tests.EditMode
{
    public sealed class LaunchReadinessCoordinatorTests
    {
        [Test]
        public void EveryIndependentCurrentPredicateIsRequiredBeforeContinue()
        {
            var coordinator = new LaunchReadinessCoordinator();
            int generation = coordinator.AttemptGeneration;

            Assert.That(
                coordinator.Snapshot.State,
                Is.EqualTo(LaunchReadinessState.WaitingForBootLoad));
            Assert.That(coordinator.TryPublishBootLoad(Boot(generation)), Is.True);
            Assert.That(
                coordinator.Snapshot.State,
                Is.EqualTo(LaunchReadinessState.WaitingForRequiredCatalogs));
            Assert.That(coordinator.TryPublishCatalog(Catalog(generation)), Is.True);
            Assert.That(
                coordinator.Snapshot.State,
                Is.EqualTo(LaunchReadinessState.WaitingForMediaPresentation));
            Assert.That(
                coordinator.TryEstablishMedia(
                    generation,
                    LaunchMediaPresentation.StaticFallbackEstablished),
                Is.True);
            Assert.That(
                coordinator.Snapshot.State,
                Is.EqualTo(LaunchReadinessState.WaitingForDestination));
            Assert.That(
                coordinator.TryPublishDestination(Destination(generation)),
                Is.True);

            Assert.That(coordinator.Snapshot.CanContinue, Is.True);
            Assert.That(
                coordinator.Snapshot.State,
                Is.EqualTo(LaunchReadinessState.AwaitingExplicitContinue));
        }

        [Test]
        public void MediaEventsAloneCannotClaimFinishedLoadingOrTransition()
        {
            var coordinator = new LaunchReadinessCoordinator();
            int generation = coordinator.AttemptGeneration;

            Assert.That(
                coordinator.TryEstablishMedia(
                    generation,
                    LaunchMediaPresentation.LoopingVideoEstablished),
                Is.True);
            Assert.That(coordinator.Snapshot.CanContinue, Is.False);
            Assert.That(coordinator.TryBeginTransition(generation), Is.False);
            Assert.That(
                coordinator.Snapshot.State,
                Is.EqualTo(LaunchReadinessState.WaitingForBootLoad));
        }

        [Test]
        public void StaleAttemptEvidenceAndSubmitAreRejectedWithoutMutation()
        {
            var coordinator = new LaunchReadinessCoordinator();
            int generation = coordinator.AttemptGeneration;

            Assert.That(
                coordinator.TryFail(
                    generation,
                    LaunchReadinessFailure.RequiredCatalogUnavailable,
                    retryAllowed: true),
                Is.True);
            Assert.That(coordinator.TryBeginRetry(), Is.True);
            int current = coordinator.AttemptGeneration;

            Assert.That(coordinator.TryPublishBootLoad(Boot(generation)), Is.False);
            Assert.That(coordinator.TryPublishCatalog(Catalog(generation)), Is.False);
            Assert.That(
                coordinator.TryEstablishMedia(
                    generation,
                    LaunchMediaPresentation.StaticFallbackEstablished),
                Is.False);
            Assert.That(
                coordinator.TryPublishDestination(Destination(generation)),
                Is.False);
            Assert.That(coordinator.TryBeginTransition(generation), Is.False);
            Assert.That(coordinator.AttemptGeneration, Is.EqualTo(current));
            Assert.That(
                coordinator.Snapshot.State,
                Is.EqualTo(LaunchReadinessState.WaitingForBootLoad));
        }

        [Test]
        public void ExplicitContinueWinsExactlyOnce()
        {
            var coordinator = ReadyCoordinator();
            int generation = coordinator.AttemptGeneration;

            Assert.That(coordinator.TryBeginTransition(generation), Is.True);
            Assert.That(coordinator.TryBeginTransition(generation), Is.False);
            Assert.That(coordinator.TryBeginTransition(generation + 1), Is.False);
            Assert.That(
                coordinator.Snapshot.State,
                Is.EqualTo(LaunchReadinessState.Transitioning));
        }

        [Test]
        public void RetryIsBoundedAndPublishesNoPriorAttemptEvidence()
        {
            var coordinator = new LaunchReadinessCoordinator();

            FailCatalogAndRetry(coordinator);
            Assert.That(coordinator.Snapshot.AttemptNumber, Is.EqualTo(2));
            Assert.That(
                coordinator.Snapshot.State,
                Is.EqualTo(LaunchReadinessState.WaitingForBootLoad));

            FailCatalogAndRetry(coordinator);
            Assert.That(coordinator.Snapshot.AttemptNumber, Is.EqualTo(3));

            int generation = coordinator.AttemptGeneration;
            Assert.That(
                coordinator.TryFail(
                    generation,
                    LaunchReadinessFailure.RequiredCatalogUnavailable,
                    retryAllowed: true),
                Is.True);
            Assert.That(
                coordinator.Snapshot.Failure,
                Is.EqualTo(LaunchReadinessFailure.RetryLimitReached));
            Assert.That(coordinator.Snapshot.RetryAllowed, Is.False);
            Assert.That(coordinator.TryBeginRetry(), Is.False);
            Assert.That(coordinator.AttemptGeneration, Is.EqualTo(generation));
        }

        [Test]
        public void DestinationActivationFailureReturnsToBoundedRecovery()
        {
            var coordinator = ReadyCoordinator();
            int generation = coordinator.AttemptGeneration;

            Assert.That(coordinator.TryBeginTransition(generation), Is.True);
            Assert.That(
                coordinator.TryFailTransition(
                    generation,
                    LaunchReadinessFailure.DestinationUnavailable,
                    retryAllowed: true),
                Is.True);
            Assert.That(
                coordinator.Snapshot.State,
                Is.EqualTo(LaunchReadinessState.Failed));
            Assert.That(coordinator.Snapshot.RetryAllowed, Is.True);
            Assert.That(coordinator.TryBeginRetry(), Is.True);
        }

        [Test]
        public void SameGenerationFailureReplacesTrackedActionWithRetry()
        {
            var eventSystemObject = new GameObject("LaunchTestEventSystem");
            var controllerObject = new GameObject("LaunchTestController");
            var continueObject = new GameObject("LaunchTestContinue");
            var retryObject = new GameObject("LaunchTestRetry");

            try
            {
                eventSystemObject.AddComponent<EventSystem>();
                var controller = controllerObject.AddComponent<BootController>();
                var continueButton = continueObject.AddComponent<Button>();
                var retryButton = retryObject.AddComponent<Button>();
                MethodInfo focus = typeof(BootController).GetMethod(
                    "FocusCurrentAction",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo focusedAction = typeof(BootController).GetField(
                    "_focusedAction",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(focus, Is.Not.Null);
                Assert.That(focusedAction, Is.Not.Null);
                focus.Invoke(controller, new object[] { 7, continueButton });
                Assert.That(
                    focusedAction.GetValue(controller),
                    Is.SameAs(continueButton));

                focus.Invoke(controller, new object[] { 7, retryButton });
                Assert.That(
                    focusedAction.GetValue(controller),
                    Is.SameAs(retryButton));
            }
            finally
            {
                Object.DestroyImmediate(retryObject);
                Object.DestroyImmediate(continueObject);
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void ExhaustedDestinationFailureCopyDoesNotOfferUnavailableRetry()
        {
            var coordinator = ReadyCoordinator();
            int generation = coordinator.AttemptGeneration;
            Assert.That(coordinator.TryBeginTransition(generation), Is.True);
            Assert.That(
                coordinator.TryFailTransition(
                    generation,
                    LaunchReadinessFailure.DestinationUnavailable,
                    retryAllowed: false),
                Is.True);

            MethodInfo detailFor = typeof(BootController).GetMethod(
                "DetailFor",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(detailFor, Is.Not.Null);
            var detail = (string)detailFor.Invoke(
                null,
                new object[] { coordinator.Snapshot });

            Assert.That(detail, Does.Contain("Restart"));
            Assert.That(detail, Does.Not.Contain("Retry"));
        }

        [Test]
        public void RuntimeSplashIdentifiesTemporaryFallbackWithoutPreAlphaDebugChrome()
        {
            var controllerObject = new GameObject("LaunchLabelTestController");

            try
            {
                var controller = controllerObject.AddComponent<BootController>();
                FieldInfo buildLabel = typeof(BootController).GetField(
                    "_buildLabel",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(buildLabel, Is.Not.Null);
                var label = (string)buildLabel.GetValue(controller);
                Assert.That(label, Does.StartWith("TEMPORARY"));
                Assert.That(label, Does.Not.Contain("PRE-ALPHA"));

                string scene = System.IO.File.ReadAllText(
                    System.IO.Path.Combine(Application.dataPath, "AL/Scenes/Boot.unity"));
                Assert.That(scene, Does.Contain("_buildLabel: " + label));
                Assert.That(scene, Does.Not.Contain("_buildLabel: PRE-ALPHA"));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }

        private static LaunchReadinessCoordinator ReadyCoordinator()
        {
            var coordinator = new LaunchReadinessCoordinator();
            int generation = coordinator.AttemptGeneration;
            Assert.That(coordinator.TryPublishBootLoad(Boot(generation)), Is.True);
            Assert.That(coordinator.TryPublishCatalog(Catalog(generation)), Is.True);
            Assert.That(
                coordinator.TryEstablishMedia(
                    generation,
                    LaunchMediaPresentation.StaticFallbackEstablished),
                Is.True);
            Assert.That(
                coordinator.TryPublishDestination(Destination(generation)),
                Is.True);
            return coordinator;
        }

        private static void FailCatalogAndRetry(
            LaunchReadinessCoordinator coordinator)
        {
            int generation = coordinator.AttemptGeneration;
            Assert.That(
                coordinator.TryFail(
                    generation,
                    LaunchReadinessFailure.RequiredCatalogUnavailable,
                    retryAllowed: true),
                Is.True);
            Assert.That(coordinator.TryBeginRetry(), Is.True);
        }

        private static LaunchBootLoadEvidence Boot(int generation)
        {
            return new LaunchBootLoadEvidence(
                generation,
                "stack-001",
                1,
                SaveLoadStatus.CreatedNew,
                1,
                1);
        }

        private static LaunchCatalogEvidence Catalog(int generation)
        {
            return new LaunchCatalogEvidence(
                generation,
                7,
                "0.1.0",
                4);
        }

        private static LaunchDestinationEvidence Destination(int generation)
        {
            return new LaunchDestinationEvidence(generation, "RealmSelection");
        }
    }
}
