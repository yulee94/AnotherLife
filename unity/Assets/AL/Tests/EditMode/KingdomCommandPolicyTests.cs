using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class KingdomCommandPolicyTests
    {
        private static readonly string[] RemovedUnsafeLabels =
        {
            "Warzone",
            "Secure Gem",
            "Wishgate",
            "Claim Wish",
            "War Drill",
            "Reset Save"
        };

        private static readonly string[] RemovedUnsafeControllerTokens =
        {
            "EarnWarzoneCredits",
            "PickTestGem",
            "EarnWishgate",
            "ChooseWishReward",
            "ResetSave",
            "RunTestBattle",
            "AddCredits(250)",
            "AddCredits(300)",
            "Stonehold_Gem_1",
            "offline_player",
            "Offline realm objective test",
            "warmaster_credits",
            "20260708"
        };

        [Test]
        public void DefaultReleaseDescriptorsExcludeDirectGrantTestAndResetCommands()
        {
            object[] descriptors = CreateDescriptors(DefaultContext());

            foreach (string label in RemovedUnsafeLabels)
            {
                Assert.That(descriptors.Select(Label), Does.Not.Contain(label));
            }

            object boardView = descriptors.Single(command => Id(command) == CommandId("BoardView"));
            Assert.That(IsInteractable(boardView), Is.True);
        }

        [Test]
        public void DefaultDeckLeavesEveryMutationUnavailable()
        {
            object[] deck = CreateDeckDescriptors(DefaultContext());

            Assert.That(deck, Is.Not.Empty);
            Assert.That(deck.All(command => Category(command) != "Presentation"), Is.True);
            Assert.That(deck.All(command => !IsInteractable(command)), Is.True);
            Assert.That(deck.All(command => Availability(command) != "Available"), Is.True);
        }

        [Test]
        public void MissingRealmDoesNotSubstituteCrownlandsForRealmCommands()
        {
            object context = CreateContext(
                hasCommittedRealm: false,
                capabilities: CreateCapabilities(territoryCapture: true, championDeployment: true));

            object capture = Resolve(CommandId("BorderlandsCapture"), context);
            object champion = Resolve(CommandId("ChampionDeploy"), context);

            Assert.That(Availability(capture), Is.EqualTo("UnavailableInvalidContext"));
            Assert.That(TechnicalCode(capture), Is.EqualTo("committed-realm-missing"));
            Assert.That(Availability(champion), Is.EqualTo("UnavailableInvalidContext"));
            Assert.That(TechnicalCode(champion), Is.EqualTo("committed-realm-missing"));
        }

        [Test]
        public void CapabilityFlagsAffectOnlyTheirCommandFamily()
        {
            object[] baseline = CreateDeckDescriptors(DefaultContext());
            object buildingEnabled = CreateContext(
                hasCommittedRealm: true,
                capabilities: CreateCapabilities(buildingUpgrade: true));
            object[] enabled = CreateDeckDescriptors(buildingEnabled);

            string[] changedIds = enabled
                .Where(command =>
                {
                    object original = baseline.Single(candidate => Id(candidate) == Id(command));
                    return Availability(original) != Availability(command);
                })
                .Select(Id)
                .ToArray();

            Assert.That(changedIds, Is.EquivalentTo(new[]
            {
                CommandId("TownHallUpgrade"),
                CommandId("FarmUpgrade"),
                CommandId("LumberMillUpgrade"),
                CommandId("QuarryUpgrade"),
                CommandId("GoldMineUpgrade"),
                CommandId("BarracksUpgrade")
            }));
        }

        [Test]
        public void MissingCatalogBuildingsRemainVisibleAndUnavailable()
        {
            object context = CreateContext(
                hasCommittedRealm: true,
                capabilities: CreateCapabilities(buildingUpgrade: true));
            object[] deck = CreateDeckDescriptors(context);

            foreach (string fieldName in new[] { "ManaShrineUpgrade", "MineUpgrade" })
            {
                object command = deck.Single(item => Id(item) == CommandId(fieldName));
                Assert.That(Availability(command), Is.EqualTo("UnavailableBuild"));
                Assert.That(IsInteractable(command), Is.False);
                Assert.That(TechnicalCode(command), Is.EqualTo("building-definition-unavailable"));
            }
        }

        [Test]
        public void UnknownCommandFailsClosed()
        {
            object unknown = Resolve("not.a.real.command", DefaultContext());

            Assert.That(Availability(unknown), Is.EqualTo("Hidden"));
            Assert.That(IsInteractable(unknown), Is.False);
            Assert.That(TechnicalCode(unknown), Is.EqualTo("unknown-command"));
        }

        [Test]
        public void DescriptorOrderingIsStable()
        {
            string[] first = CreateDescriptors(DefaultContext()).Select(Id).ToArray();
            string[] second = CreateDescriptors(DefaultContext()).Select(Id).ToArray();

            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void ProductionControllerContainsNoRemovedCommandHandlersOrFixedTestTokens()
        {
            string controllerPath = Path.Combine(Application.dataPath, "AL", "Scripts", "UI", "Kingdom", "KingdomSceneController.cs");
            string source = File.ReadAllText(controllerPath);

            foreach (string token in RemovedUnsafeControllerTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        private static object DefaultContext()
        {
            return CreateContext(hasCommittedRealm: true, capabilities: CreateCapabilities());
        }

        private static object CreateCapabilities(
            bool buildingUpgrade = false,
            bool troopTraining = false,
            bool questClaim = false,
            bool research = false,
            bool warmaster = false,
            bool territoryCapture = false,
            bool championDeployment = false)
        {
            return Activator.CreateInstance(
                RequiredType("AL.UI.Kingdom.KingdomCommandCapabilities"),
                buildingUpgrade,
                troopTraining,
                questClaim,
                research,
                warmaster,
                territoryCapture,
                championDeployment);
        }

        private static object CreateContext(bool hasCommittedRealm, object capabilities)
        {
            return Activator.CreateInstance(
                RequiredType("AL.UI.Kingdom.KingdomCommandContext"),
                hasCommittedRealm,
                capabilities);
        }

        private static object[] CreateDescriptors(object context)
        {
            return InvokePolicyList("CreateDescriptors", context);
        }

        private static object[] CreateDeckDescriptors(object context)
        {
            return InvokePolicyList("CreateDeckDescriptors", context);
        }

        private static object Resolve(string id, object context)
        {
            return RequiredType("AL.UI.Kingdom.KingdomCommandPolicy")
                .GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { id, context });
        }

        private static object[] InvokePolicyList(string methodName, object context)
        {
            var result = RequiredType("AL.UI.Kingdom.KingdomCommandPolicy")
                .GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { context });
            return ((IEnumerable)result).Cast<object>().ToArray();
        }

        private static string CommandId(string fieldName)
        {
            return (string)RequiredType("AL.UI.Kingdom.KingdomCommandPolicy")
                .GetField(fieldName, BindingFlags.Public | BindingFlags.Static)
                .GetRawConstantValue();
        }

        private static Type RequiredType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, typeName);
            return type;
        }

        private static string Id(object descriptor) => (string)Property(descriptor, "Id");
        private static string Label(object descriptor) => (string)Property(descriptor, "Label");
        private static string Category(object descriptor) => Property(descriptor, "Category").ToString();
        private static string Availability(object descriptor) => Property(descriptor, "Availability").ToString();
        private static string TechnicalCode(object descriptor) => (string)Property(descriptor, "TechnicalCode");
        private static bool IsInteractable(object descriptor) => (bool)Property(descriptor, "IsInteractable");

        private static object Property(object instance, string name)
        {
            return instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance).GetValue(instance);
        }
    }
}
