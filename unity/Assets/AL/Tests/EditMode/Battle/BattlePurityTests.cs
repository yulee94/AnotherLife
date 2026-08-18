using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Battle.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Battle
{
    public class BattlePurityTests
    {
        [Test]
        public void PureBattleBoundaryHasNoRuntimeServiceRandomClockLoggingOrUnityDependency()
        {
            string battleRoot = Path.Combine(Application.dataPath, "AL/Scripts/Battle");
            string[] scopedDirectories =
            {
                Path.Combine(battleRoot, "Contracts"),
                Path.Combine(battleRoot, "Validation"),
                Path.Combine(battleRoot, "Computation")
            };
            string[] forbidden =
            {
                "ServiceLocator",
                "System.Random",
                "UnityEngine.Random",
                "UnityEngine.",
                "DateTime.Now",
                "DateTime.UtcNow",
                "Debug.Log",
                "PlayerPrefs",
                "SaveService",
                "QuestService",
                "Notification"
            };

            foreach (string directory in scopedDirectories)
            {
                foreach (string path in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(path);
                    foreach (string token in forbidden)
                        Assert.That(source, Does.Not.Contain(token), path + " contains " + token);
                }
            }
        }

        [Test]
        public void EveryPublishedContractClassIsSealedAndHasNoPublicSetter()
        {
            Type[] contractTypes = typeof(BattleComputationRequest).Assembly
                .GetTypes()
                .Where(type =>
                    type.IsClass &&
                    type.IsPublic &&
                    type.Namespace != null &&
                    (string.Equals(type.Namespace, "AL.Battle.Contracts", StringComparison.Ordinal) ||
                     string.Equals(type.Namespace, "AL.Battle.Validation", StringComparison.Ordinal) ||
                     string.Equals(type.Namespace, "AL.Battle.Computation", StringComparison.Ordinal)) &&
                    !type.IsAbstract)
                .ToArray();

            Assert.That(contractTypes, Is.Not.Empty);
            foreach (Type type in contractTypes)
            {
                Assert.That(type.IsSealed, Is.True, type.FullName);
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    Assert.That(property.SetMethod, Is.Null, type.FullName + "." + property.Name);
            }
        }

        [Test]
        public void ComputedOutputGraphCannotBeForgedThroughPublicConstructors()
        {
            Type[] outputTypes =
            {
                typeof(BattleRoundResult),
                typeof(BattleTroopLoss),
                typeof(BattleRewardProposal),
                typeof(BattleContribution),
                typeof(BattleComputedResult),
                typeof(BattleComputationResult)
            };

            foreach (Type type in outputTypes)
                Assert.That(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance), Is.Empty, type.FullName);
        }

        [Test]
        public void LegacyFloatSimulatorIsRemovedAndBootloaderRegistersFixedPointAdapter()
        {
            string legacyPath = Path.Combine(
                Application.dataPath,
                "AL/Scripts/Battle/Simulator/DeterministicBattleSimulator.cs");
            string bootloaderPath = Path.Combine(
                Application.dataPath,
                "AL/Scripts/Core/Bootloader.cs");

            Assert.That(File.Exists(legacyPath), Is.False,
                "The retired float simulator must not remain in the read path.");
            Assert.That(
                File.ReadAllText(bootloaderPath),
                Does.Contain("FixedPointBattleSimulator"));
            Assert.That(
                File.ReadAllText(bootloaderPath),
                Does.Not.Contain("System.Random"));
        }
    }
}
