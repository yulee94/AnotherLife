using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions.Narrative;
using AL.RealmWar.World;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public sealed class LocalWorldAtlasServiceTests
    {
        [Test]
        public void QueriesReturnStableCanonicalOrdering()
        {
            var service = new LocalWorldAtlasService(null);

            CollectionAssert.AreEqual(
                new[]
                {
                    "stonehold_inner",
                    "eldergrove_inner",
                    "crownlands_inner",
                    "umbral_inner",
                    "neutral_borderlands",
                    "iron_pass",
                    "worldroot_border",
                    "sovereign_road",
                    "ashen_rift"
                },
                service.GetAllZones().Value.Select(value => value.Id));
            CollectionAssert.AreEqual(
                new[] { "crownlands_inner", "neutral_borderlands", "sovereign_road" },
                service.GetZonesForRealm(RealmId.Crownlands).Value.Select(value => value.Id));
            CollectionAssert.AreEqual(
                new[]
                {
                    "Stonehold_Heart_Gem",
                    "Stonehold_Fortress_Gem",
                    "Eldergrove_Heart_Gem",
                    "Eldergrove_Glade_Gem",
                    "Umbral_Heart_Gem",
                    "Umbral_Void_Gem",
                    "neutral_borderlands_objective",
                    "iron_pass_objective",
                    "worldroot_border_objective",
                    "sovereign_road_objective",
                    "ashen_rift_objective"
                },
                service.GetObjectivesForRealm(RealmId.Crownlands).Value.Select(value => value.Id));
        }

        [Test]
        public void PublicRecordsAndCollectionsAreReadOnly()
        {
            var service = new LocalWorldAtlasService(null);
            WorldZoneData zone = service.GetZone("crownlands_inner").Value;
            WorldObjectiveData objective = zone.Objectives[0];
            WorldAtlasServiceQueryResult<WorldNarrationSnapshot> narrationResult =
                service.GetNarrationSnapshot(RealmId.Crownlands);
            WorldNarrationSnapshot narration = narrationResult.Value;

            Assert.That(
                typeof(WorldZoneData).GetFields(BindingFlags.Instance | BindingFlags.Public),
                Is.Empty);
            Assert.That(
                typeof(WorldObjectiveData).GetFields(BindingFlags.Instance | BindingFlags.Public),
                Is.Empty);
            Assert.That(
                typeof(WorldNarrationSnapshot).GetFields(BindingFlags.Instance | BindingFlags.Public),
                Is.Empty);
            Assert.That(
                typeof(WorldZoneData).GetProperties().Where(value => value.CanWrite),
                Is.Empty);
            Assert.That(
                typeof(WorldObjectiveData).GetProperties().Where(value => value.CanWrite),
                Is.Empty);
            Assert.That(
                typeof(WorldNarrationSnapshot).GetProperties().Where(value => value.CanWrite),
                Is.Empty);
            Assert.That(
                typeof(WorldAtlasServiceQueryResult<WorldZoneData>)
                    .GetProperties()
                    .Where(value => value.CanWrite),
                Is.Empty);
            Assert.That(Attribute.IsDefined(typeof(WorldZoneData), typeof(SerializableAttribute)), Is.False);
            Assert.That(Attribute.IsDefined(typeof(WorldObjectiveData), typeof(SerializableAttribute)), Is.False);
            Assert.That(Attribute.IsDefined(typeof(WorldNarrationSnapshot), typeof(SerializableAttribute)), Is.False);
            Assert.Throws<NotSupportedException>(() => ((IList)zone.Objectives).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList)narration.VisibleZones).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList)narration.ActiveObjectives).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList)narration.ConflictHints).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList)narrationResult.Diagnostics).Clear());
            Assert.That(objective.DisplayName, Is.EqualTo("Crownlands Royal Capital Heart Gem"));
        }

        [Test]
        public void MissingStoryServiceIsTypedWithoutHidingAvailableAtlasData()
        {
            var service = new LocalWorldAtlasService(null);

            WorldAtlasServiceQueryResult<WorldNarrationSnapshot> firstResult =
                service.GetNarrationSnapshot(RealmId.Crownlands);
            WorldAtlasServiceQueryResult<WorldNarrationSnapshot> secondResult =
                service.GetNarrationSnapshot(RealmId.Crownlands);
            WorldNarrationSnapshot first = firstResult.Value;
            WorldNarrationSnapshot second = secondResult.Value;

            Assert.That(firstResult.Status, Is.EqualTo(WorldAtlasServiceQueryStatus.AvailableWithDiagnostics));
            Assert.That(firstResult.IsAvailable, Is.True);
            Assert.That(firstResult.Diagnostics.Select(value => value.Code),
                Is.EqualTo(new[] { "AL-ATLAS-STORY-UNAVAILABLE" }));
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.ViewerRealm, Is.EqualTo(RealmId.Crownlands));
            CollectionAssert.AreEqual(
                first.VisibleZones.Select(value => value.Id),
                second.VisibleZones.Select(value => value.Id));
            CollectionAssert.AreEqual(
                first.ActiveObjectives.Select(value => value.Id),
                second.ActiveObjectives.Select(value => value.Id));
            Assert.That(first.ConflictHints, Is.Empty);
            Assert.That(secondResult.Diagnostics.Select(value => value.Code),
                Is.EqualTo(firstResult.Diagnostics.Select(value => value.Code)));
        }

        [Test]
        public void InvalidInputsReturnTypedFailures()
        {
            var service = new LocalWorldAtlasService(null);

            WorldAtlasServiceQueryResult<IReadOnlyList<WorldZoneData>> noRealm =
                service.GetZonesForRealm(RealmId.None);
            WorldAtlasServiceQueryResult<IReadOnlyList<WorldObjectiveData>> undefinedRealm =
                service.GetObjectivesForRealm((RealmId)99);
            WorldAtlasServiceQueryResult<WorldNarrationSnapshot> invalidNarration =
                service.GetNarrationSnapshot(RealmId.None);
            WorldAtlasServiceQueryResult<WorldZoneData> invalidZone = service.GetZone(null);
            WorldAtlasServiceQueryResult<WorldZoneData> unknownZone =
                service.GetZone("missing_zone");

            Assert.That(noRealm.Status, Is.EqualTo(WorldAtlasServiceQueryStatus.InvalidViewer));
            Assert.That(noRealm.Value, Is.Null);
            Assert.That(noRealm.Diagnostics[0].Code, Is.EqualTo("AL-ATLAS-VIEWER-INVALID"));
            Assert.That(undefinedRealm.Status, Is.EqualTo(WorldAtlasServiceQueryStatus.InvalidViewer));
            Assert.That(invalidNarration.Status, Is.EqualTo(WorldAtlasServiceQueryStatus.InvalidViewer));
            Assert.That(invalidNarration.Value, Is.Null);
            Assert.That(invalidZone.Status, Is.EqualTo(WorldAtlasServiceQueryStatus.InvalidId));
            Assert.That(invalidZone.Diagnostics[0].Code, Is.EqualTo("AL-ATLAS-ID-INVALID"));
            Assert.That(unknownZone.Status, Is.EqualTo(WorldAtlasServiceQueryStatus.UnknownId));
            Assert.That(unknownZone.Diagnostics[0].Code, Is.EqualTo("AL-ATLAS-ID-UNKNOWN"));
        }

        [Test]
        public void StoryFailuresAreTypedAndSuccessfulHintsRemainDeterministic()
        {
            var failing = new LocalWorldAtlasService(new StoryServiceStub(true));
            var available = new LocalWorldAtlasService(new StoryServiceStub(false));

            WorldAtlasServiceQueryResult<WorldNarrationSnapshot> failed =
                failing.GetNarrationSnapshot(RealmId.Stonehold);
            WorldAtlasServiceQueryResult<WorldNarrationSnapshot> succeeded =
                available.GetNarrationSnapshot(RealmId.Stonehold);

            Assert.That(failed.Status, Is.EqualTo(WorldAtlasServiceQueryStatus.AvailableWithDiagnostics));
            Assert.That(failed.Diagnostics[0].Code, Is.EqualTo("AL-ATLAS-STORY-FAILED"));
            Assert.That(failed.Value.ConflictHints, Is.Empty);
            Assert.That(succeeded.Status, Is.EqualTo(WorldAtlasServiceQueryStatus.Available));
            Assert.That(succeeded.Diagnostics, Is.Empty);
            Assert.That(succeeded.Value.ConflictHints,
                Is.EqualTo(new[] { "Archivist: The pass is contested." }));
        }

        private sealed class StoryServiceStub : IStoryService
        {
            private readonly bool shouldThrow;

            public StoryServiceStub(bool shouldThrow)
            {
                this.shouldThrow = shouldThrow;
            }

            public string CurrentChapterId => string.Empty;

            public IEnumerable<DialogueNode> GetConflictHints(RealmId currentRealm)
            {
                if (shouldThrow)
                {
                    throw new InvalidOperationException("test failure");
                }

                return new[]
                {
                    new DialogueNode
                    {
                        CharacterName = "Archivist",
                        Text = "The pass is contested."
                    }
                };
            }

            public void AdvanceStory() { }
            public DialogueNode GetDialogue(string nodeId) => null;
            public void TriggerDialogue(string nodeId) { }
            public event Action<string> OnChapterAdvanced
            {
                add { }
                remove { }
            }
            public event Action<DialogueNode> OnDialogueTriggered
            {
                add { }
                remove { }
            }
        }
    }
}
