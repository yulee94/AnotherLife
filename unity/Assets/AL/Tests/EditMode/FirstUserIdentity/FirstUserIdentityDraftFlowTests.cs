using AL.Core;
using AL.UI.FirstUserIdentity;
using NUnit.Framework;

namespace AL.Tests.EditMode.FirstUserIdentity
{
    public sealed class FirstUserIdentityDraftFlowTests
    {
        [TestCase(RealmId.Crownlands, FirstUserRace.Humans)]
        [TestCase(RealmId.Stonehold, FirstUserRace.Dwarves)]
        [TestCase(RealmId.Eldergrove, FirstUserRace.Elves)]
        [TestCase(RealmId.Umbral, FirstUserRace.DarkElves)]
        public void RealmPreviewDerivesExactlyOneCanonicalRace(
            RealmId realm,
            FirstUserRace expectedRace)
        {
            var flow = new FirstUserIdentityDraftFlow();

            FirstUserIdentityDraftTransitionResult result = flow.PreviewRealm(realm);

            Assert.That(result.Status, Is.EqualTo(
                FirstUserIdentityDraftTransitionStatus.Applied));
            Assert.That(result.Snapshot.Realm, Is.EqualTo(realm));
            Assert.That(result.Snapshot.Race, Is.EqualTo(expectedRace));
            Assert.That(result.Snapshot.HasRealm, Is.True);
            Assert.That(result.Snapshot.HasClassFamily, Is.False);
            Assert.That(result.Snapshot.ClassFamily, Is.Null);
        }

        [TestCase(RealmId.None)]
        [TestCase((RealmId)(-1))]
        [TestCase((RealmId)999)]
        public void InvalidRealmFailsClosedWithoutChangingDraft(RealmId realm)
        {
            var flow = new FirstUserIdentityDraftFlow();

            FirstUserIdentityDraftTransitionResult result = flow.PreviewRealm(realm);

            Assert.That(result.Status, Is.EqualTo(
                FirstUserIdentityDraftTransitionStatus.InvalidRealm));
            Assert.That(result.Snapshot.Step, Is.EqualTo(
                FirstUserIdentityDraftStep.Realm));
            Assert.That(result.Snapshot.Realm, Is.EqualTo(RealmId.None));
            Assert.That(result.Snapshot.Race, Is.EqualTo(FirstUserRace.Unknown));
            Assert.That(result.Snapshot.ClassFamily, Is.Null);
        }

        [Test]
        public void ClassHasNoDefaultAndRequiresExplicitSelection()
        {
            var flow = new FirstUserIdentityDraftFlow();
            flow.PreviewRealm(RealmId.Crownlands);
            FirstUserIdentityDraftTransitionResult realmConfirmed =
                flow.ConfirmRealmPreview();

            FirstUserIdentityDraftTransitionResult confirmation =
                flow.ConfirmDraftForCustomization();

            Assert.That(realmConfirmed.Snapshot.Step, Is.EqualTo(
                FirstUserIdentityDraftStep.ClassFamily));
            Assert.That(realmConfirmed.Snapshot.ClassFamily, Is.Null,
                "Warrior is enum zero and must never become an inferred default.");
            Assert.That(confirmation.Status, Is.EqualTo(
                FirstUserIdentityDraftTransitionStatus.SelectionRequired));
            Assert.That(confirmation.Snapshot.IsCustomizationReady, Is.False);
        }

        [TestCase(ClassFamily.Warrior)]
        [TestCase(ClassFamily.Mage)]
        [TestCase(ClassFamily.Ranger)]
        [TestCase(ClassFamily.Assassin)]
        public void EveryAndOnlyCompiledV1ClassFamilyCanBeExplicitlySelected(
            ClassFamily classFamily)
        {
            var flow = CreateClassStepFlow(RealmId.Stonehold);

            FirstUserIdentityDraftTransitionResult preview =
                flow.PreviewClassFamily(classFamily);
            FirstUserIdentityDraftTransitionResult confirmed =
                flow.ConfirmDraftForCustomization();

            Assert.That(preview.Status, Is.EqualTo(
                FirstUserIdentityDraftTransitionStatus.Applied));
            Assert.That(preview.Snapshot.ClassFamily, Is.EqualTo(classFamily));
            Assert.That(confirmed.Status, Is.EqualTo(
                FirstUserIdentityDraftTransitionStatus.Applied));
            Assert.That(confirmed.Snapshot.IsCustomizationReady, Is.True);
            Assert.That(confirmed.Snapshot.Realm, Is.EqualTo(RealmId.Stonehold));
            Assert.That(confirmed.Snapshot.Race, Is.EqualTo(FirstUserRace.Dwarves));
            Assert.That(confirmed.Snapshot.ClassFamily, Is.EqualTo(classFamily));
        }

        [Test]
        public void UndefinedClassFamilyFailsClosedWithoutReplacingPriorPreview()
        {
            var flow = CreateClassStepFlow(RealmId.Eldergrove);
            flow.PreviewClassFamily(ClassFamily.Mage);

            FirstUserIdentityDraftTransitionResult result =
                flow.PreviewClassFamily((ClassFamily)999);

            Assert.That(result.Status, Is.EqualTo(
                FirstUserIdentityDraftTransitionStatus.InvalidClassFamily));
            Assert.That(result.Snapshot.ClassFamily, Is.EqualTo(ClassFamily.Mage));
            Assert.That(result.Snapshot.Step, Is.EqualTo(
                FirstUserIdentityDraftStep.ClassFamily));
        }

        [Test]
        public void ClassCannotBeSelectedBeforeRealmPreviewIsConfirmed()
        {
            var flow = new FirstUserIdentityDraftFlow();
            flow.PreviewRealm(RealmId.Umbral);

            FirstUserIdentityDraftTransitionResult result =
                flow.PreviewClassFamily(ClassFamily.Assassin);

            Assert.That(result.Status, Is.EqualTo(
                FirstUserIdentityDraftTransitionStatus.WrongStep));
            Assert.That(result.Snapshot.ClassFamily, Is.Null);
            Assert.That(result.Snapshot.Step, Is.EqualTo(
                FirstUserIdentityDraftStep.Realm));
        }

        [Test]
        public void ReturningToRealmClearsClassAndChangingRealmReDerivesRace()
        {
            var flow = CreateClassStepFlow(RealmId.Crownlands);
            flow.PreviewClassFamily(ClassFamily.Ranger);

            FirstUserIdentityDraftTransitionResult returned =
                flow.ReturnToRealmPreview();
            FirstUserIdentityDraftTransitionResult changed =
                flow.PreviewRealm(RealmId.Umbral);

            Assert.That(returned.Snapshot.Step, Is.EqualTo(
                FirstUserIdentityDraftStep.Realm));
            Assert.That(returned.Snapshot.ClassFamily, Is.Null);
            Assert.That(changed.Snapshot.Realm, Is.EqualTo(RealmId.Umbral));
            Assert.That(changed.Snapshot.Race, Is.EqualTo(FirstUserRace.DarkElves));
            Assert.That(changed.Snapshot.ClassFamily, Is.Null);
        }

        [Test]
        public void CustomizationReadyIsTerminalAndCannotBeRewritten()
        {
            var flow = CreateClassStepFlow(RealmId.Umbral);
            flow.PreviewClassFamily(ClassFamily.Assassin);
            FirstUserIdentityDraftSnapshot ready =
                flow.ConfirmDraftForCustomization().Snapshot;

            FirstUserIdentityDraftTransitionResult realmRewrite =
                flow.PreviewRealm(RealmId.Crownlands);
            FirstUserIdentityDraftTransitionResult classRewrite =
                flow.PreviewClassFamily(ClassFamily.Warrior);
            FirstUserIdentityDraftTransitionResult repeatedConfirmation =
                flow.ConfirmDraftForCustomization();

            Assert.That(realmRewrite.Status, Is.EqualTo(
                FirstUserIdentityDraftTransitionStatus.DraftClosed));
            Assert.That(classRewrite.Status, Is.EqualTo(
                FirstUserIdentityDraftTransitionStatus.DraftClosed));
            Assert.That(repeatedConfirmation.Status, Is.EqualTo(
                FirstUserIdentityDraftTransitionStatus.DraftClosed));
            Assert.That(repeatedConfirmation.Snapshot.Realm, Is.EqualTo(ready.Realm));
            Assert.That(repeatedConfirmation.Snapshot.Race, Is.EqualTo(ready.Race));
            Assert.That(repeatedConfirmation.Snapshot.ClassFamily,
                Is.EqualTo(ready.ClassFamily));
        }

        [Test]
        public void DevelopmentCopyMapsSupportedValuesWithoutMachineFallback()
        {
            var copy = new DevelopmentFirstUserIdentityDraftCopyProvider();

            Assert.That(copy.TryGetRealmLabel(RealmId.Crownlands, out string realm),
                Is.True);
            Assert.That(copy.TryGetRaceLabel(FirstUserRace.Humans, out string race),
                Is.True);
            Assert.That(copy.TryGetClassFamilyLabel(
                ClassFamily.Warrior,
                out string classFamily), Is.True);
            Assert.That(realm, Is.EqualTo("Crownlands realm"));
            Assert.That(race, Is.EqualTo("Human heritage"));
            Assert.That(classFamily, Is.EqualTo("Warrior path"));
            Assert.That(realm, Is.Not.EqualTo(RealmId.Crownlands.ToString()));
            Assert.That(race, Is.Not.EqualTo(FirstUserRace.Humans.ToString()));
            Assert.That(classFamily, Is.Not.EqualTo(ClassFamily.Warrior.ToString()));

            Assert.That(copy.TryGetRealmLabel((RealmId)999, out string invalidRealm),
                Is.False);
            Assert.That(copy.TryGetRaceLabel((FirstUserRace)999, out string invalidRace),
                Is.False);
            Assert.That(copy.TryGetClassFamilyLabel(
                (ClassFamily)999,
                out string invalidClass), Is.False);
            Assert.That(invalidRealm, Is.Empty);
            Assert.That(invalidRace, Is.Empty);
            Assert.That(invalidClass, Is.Empty);
        }

        private static FirstUserIdentityDraftFlow CreateClassStepFlow(RealmId realm)
        {
            var flow = new FirstUserIdentityDraftFlow();
            Assert.That(flow.PreviewRealm(realm).WasApplied, Is.True);
            Assert.That(flow.ConfirmRealmPreview().WasApplied, Is.True);
            return flow;
        }
    }
}
