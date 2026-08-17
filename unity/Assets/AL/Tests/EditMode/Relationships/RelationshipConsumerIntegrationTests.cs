using System;
using System.Collections.Generic;
using System.IO;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.Relationships;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Relationships
{
    public sealed class RelationshipConsumerIntegrationTests
    {
        private const string CatalogRelativePath =
            "AL/StreamingAssets/GameData/al_relationship_authority_content_catalog.json";

        [Test]
        public void RuntimeServicesResolveStableIdsThroughOneImmutableSnapshotPerRead()
        {
            var save = new FakeSaveService(new SaveGameData
            {
                Reputation = new List<NpcAffinityData>
                {
                    new NpcAffinityData { NpcId = "NPC_VALERIUS", Affinity = 17f }
                },
                FactionReputations = new List<FactionRepData>
                {
                    new FactionRepData { FactionId = "FACTION_VEIL_WATCH", Reputation = 125 }
                },
                LordPersona = new PersonaData { Warlord = 2, Diplomat = 9, Sage = 1, Rogue = 0 }
            });
            var resolver = NewResolver();
            var reputation = new ReputationService(save, resolver);
            var faction = new FactionService(save, resolver);
            var persona = new PersonaService(save, resolver);

            RelationshipValueQuery affinity = reputation.QueryAffinity("npc_valerius");
            RelationshipValueQuery factionRep = faction.QueryReputation("faction_veil_watch");
            RelationshipPersonaSnapshot personaSnapshot = persona.CaptureSnapshot();

            Assert.AreEqual(RelationshipValueQueryStatus.Found, affinity.Status);
            Assert.AreEqual("npc_valerius", affinity.CanonicalId);
            Assert.AreEqual(17d, affinity.Value);
            Assert.AreEqual(RelationshipValueQueryStatus.Found, factionRep.Status);
            Assert.AreEqual("faction_veil_watch", factionRep.CanonicalId);
            Assert.AreEqual(125d, factionRep.Value);
            Assert.AreEqual(RelationshipPersonaClassification.UniqueDominant, personaSnapshot.Classification);
            CollectionAssert.AreEqual(new[] { "diplomat" }, personaSnapshot.DominantTraitIds);
        }

        [Test]
        public void CapturedConsumerSnapshotRemainsCoherentWhenMutableSaveChangesLater()
        {
            var saveData = new SaveGameData
            {
                Reputation = new List<NpcAffinityData>
                {
                    new NpcAffinityData { NpcId = "npc_valerius", Affinity = 5f }
                },
                FactionReputations = new List<FactionRepData>(),
                LordPersona = new PersonaData { Warlord = 1, Diplomat = 0, Sage = 0, Rogue = 0 }
            };
            RelationshipConsumerSnapshot captured = RelationshipConsumerSnapshot.Capture(
                NewResolver(), saveData, true);

            saveData.Reputation[0].Affinity = 99f;
            saveData.Reputation.Add(new NpcAffinityData { NpcId = "npc_gruff", Affinity = 20f });
            saveData.LordPersona.Warlord = 0;
            saveData.LordPersona.Sage = 10;

            Assert.AreEqual(5d, captured.Affinity.Values["npc_valerius"]);
            Assert.IsFalse(captured.Affinity.Values.ContainsKey("npc_gruff"));
            Assert.AreEqual(RelationshipPersonaClassification.UniqueDominant, captured.Persona.Classification);
            CollectionAssert.AreEqual(new[] { "warlord" }, captured.Persona.DominantTraitIds);
            Assert.AreEqual(RelationshipValueQueryStatus.Found,
                captured.QueryAffinity(NewResolver(), "npc_valerius").Status);
        }

        [Test]
        public void MissingStaleAndMalformedReferencesFailClosedWithoutLabelGuessing()
        {
            var resolver = NewResolver();
            RelationshipConsumerSnapshot snapshot = RelationshipConsumerSnapshot.Capture(
                resolver,
                new SaveGameData
                {
                    Reputation = new List<NpcAffinityData>
                    {
                        new NpcAffinityData { NpcId = "npc_valerius", Affinity = 5f }
                    },
                    FactionReputations = new List<FactionRepData>(),
                    LordPersona = new PersonaData()
                },
                true);

            Assert.AreEqual(RelationshipValueQueryStatus.UnknownIdentity,
                snapshot.QueryAffinity(resolver, "Captain Valerius").Status);
            Assert.AreEqual(RelationshipValueQueryStatus.UnknownIdentity,
                snapshot.QueryAffinity(resolver, "npc_missing").Status);
            Assert.AreEqual(RelationshipValueQueryStatus.StalePolicy,
                snapshot.QueryAffinity(new RelationshipCatalogResolver(), "npc_valerius").Status);
        }

        [Test]
        public void ExistingRankAndAffiliationOutcomesRemainUnchangedForStableIds()
        {
            var save = new FakeSaveService(new SaveGameData
            {
                Reputation = new List<NpcAffinityData>
                {
                    new NpcAffinityData { NpcId = "npc_valerius", Affinity = 50f }
                },
                FactionReputations = new List<FactionRepData>
                {
                    new FactionRepData { FactionId = "faction_veil_watch", Reputation = -100 }
                },
                LordPersona = new PersonaData()
            });
            var resolver = NewResolver();

            Assert.AreEqual("Friendly", new ReputationService(save, resolver).GetAffinityRank("npc_valerius"));
            Assert.AreEqual("Opponent", new FactionService(save, resolver).GetFactionAffiliation("faction_veil_watch"));
        }

        private static RelationshipCatalogResolver NewResolver()
        {
            return new RelationshipCatalogResolver(File.ReadAllBytes(
                Path.Combine(Application.dataPath, CatalogRelativePath)));
        }

        private sealed class FakeSaveService : ISaveGameService
        {
            internal FakeSaveService(SaveGameData save) { CurrentSave = save; }
            public SaveGameData CurrentSave { get; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus => SaveOperationStatus.None;
            public string LastSaveMessage => string.Empty;
            public void Save() { }
            public void Load() { }
            public bool HasSave() { return CurrentSave != null; }
            public void CreateNewSave(RealmId realmId) { }
            public void DeleteSave() { }
        }
    }
}
