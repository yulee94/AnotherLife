using System.IO;
using System.Linq;
using NUnit.Framework;
using R = AL.Tests.EditMode.ProductionScenes.ProductionSceneTestReflection;

namespace AL.Tests.EditMode.ProductionScenes
{
    /// <summary>
    /// Scene-flow contract (#223 "Required tests" scene-flow family), asserted from the descriptor with
    /// NO Build Settings access (decision D3a). Serialized transition strings resolve to committed scene
    /// assets; Kingdom -> ChampionArena is a deferred descriptor record; reset-to-Boot is classified
    /// blocked/unsafe; case mismatch fails; the ChampionArena active-scene-name self-reload is classified
    /// unresolvable outside Build Settings (classify, not fix).
    /// </summary>
    public sealed class ProductionSceneFlowTests
    {
        [Test]
        public void BootTransitionsResolveToCommittedRealmSelectionAndKingdom()
        {
            AssertActiveTransitionResolves("al_scene_boot", "al_scene_realm_selection", "_realmSelectionScene", "RealmSelection");
            AssertActiveTransitionResolves("al_scene_boot", "al_scene_kingdom", "_kingdomScene", "Kingdom");
        }

        [Test]
        public void RealmSelectionTransitionResolvesToCharacterCreation()
        {
            AssertActiveTransitionResolves("al_scene_realm_selection", "al_scene_character_creation", "_nextScene", "CharacterCreation");
        }

        [Test]
        public void CharacterCreationTransitionResolvesToChampionArena()
        {
            AssertActiveTransitionResolves("al_scene_character_creation", "al_scene_champion_arena", "_combatSceneName", "ChampionArena");
        }

        [Test]
        public void KingdomChampionArenaTransitionIsActiveDescriptorRecord()
        {
            object kingdom = R.RecordById("al_scene_kingdom");
            var transitions = R.Transitions(kingdom);
            object arena = transitions.FirstOrDefault(t => R.PropString(t, "TargetSceneId") == "al_scene_champion_arena");
            Assert.NotNull(arena, "Kingdom must carry a descriptor transition to ChampionArena.");
            Assert.AreEqual("Active", R.Prop(arena, "Status").ToString());
            Assert.IsFalse(R.PropBool(arena, "HasSerializedField"), "Kingdom->ChampionArena remains descriptor-only (no serialized field).");

            object arenaRecord = R.RecordById("al_scene_champion_arena");
            Assert.AreEqual("ChampionArena", R.PropString(arenaRecord, "SceneName"));
            Assert.That(R.AsStrings(R.Prop(arenaRecord, "BuildProfiles")), Does.Contain("ShellFoundation"));
        }

        [Test]
        public void ChampionArenaReturnResolvesToKingdom()
        {
            AssertActiveTransitionResolves("al_scene_champion_arena", "al_scene_kingdom", "_kingdomSceneName", "Kingdom");
        }

        [Test]
        public void ResetToBootIsClassifiedBlockedUnsafeAndNotProductionReachable()
        {
            Assert.IsFalse((bool)R.StaticProperty(R.DescriptorType, "IsResetToBootProductionReachable"),
                "reset-to-Boot must not be production reachable (#178/#137).");

            object kingdom = R.RecordById("al_scene_kingdom");
            var targetsBoot = R.Transitions(kingdom).Any(t => R.PropString(t, "TargetSceneId") == "al_scene_boot");
            Assert.IsFalse(targetsBoot, "Kingdom must have no descriptor transition back to Boot.");
        }

        [Test]
        public void CaseMismatchDoesNotResolve()
        {
            // Case-sensitive scene name lookup: wrong casing must not resolve.
            object hit = R.StaticMethod(R.DescriptorType, "TryGetBySceneName", "realmselection", null);
            Assert.IsFalse((bool)hit, "Wrong-case scene name must not resolve.");

            object miss = R.StaticMethod(R.DescriptorType, "TryGetBySceneName", "RealmSelection", null);
            Assert.IsTrue((bool)miss, "Exact-case scene name must resolve.");
        }

        [Test]
        public void ChampionArenaSelfReloadIsClassifiedUnresolvableOutsideBuildSettings()
        {
            string classification = (string)R.StaticField(R.DescriptorType, "ChampionArenaSelfReloadClassification");
            Assert.AreEqual("unresolvable_outside_build_settings", classification);
        }

        [Test]
        public void EveryProductionSceneAssetExistsOnDisk()
        {
            foreach (object record in R.DescriptorProduction())
            {
                string path = R.PropString(record, "AssetPath");
                Assert.IsTrue(File.Exists(path), $"Committed scene asset missing: {path}");
                Assert.IsTrue(File.Exists(path + ".meta"), $"Committed scene .meta missing: {path}.meta");
            }
        }

        private static void AssertActiveTransitionResolves(string sourceId, string targetId, string field, string value)
        {
            object source = R.RecordById(sourceId);
            object transition = R.Transitions(source).FirstOrDefault(t => R.PropString(t, "TargetSceneId") == targetId);
            Assert.NotNull(transition, $"{sourceId} must have a transition to {targetId}.");
            Assert.AreEqual("Active", R.Prop(transition, "Status").ToString(), $"{sourceId}->{targetId} status");
            Assert.AreEqual(field, R.PropString(transition, "SerializedFieldName"), $"{sourceId}->{targetId} field");
            Assert.AreEqual(value, R.PropString(transition, "SerializedValue"), $"{sourceId}->{targetId} value");

            object target = R.RecordById(targetId);
            Assert.AreEqual(value, R.PropString(target, "SceneName"), $"{targetId} scene name must equal the transition string.");
        }
    }
}
