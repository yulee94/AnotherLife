using AL.Core.Interfaces;
using AL.UI;
using NUnit.Framework;

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
