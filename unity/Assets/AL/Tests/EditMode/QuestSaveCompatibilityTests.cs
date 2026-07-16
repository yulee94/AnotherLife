using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public class QuestSaveCompatibilityTests
    {
        [Test]
        public void NullQuestListNormalizesAndUnknownStateSurvivesReload()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-QuestTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object saveService = CreateSaveService(root);
                CreateNewSave(saveService);
                object save = GetProperty(saveService, "CurrentSave");
                SetField(save, "Quests", null);

                object resourceService = CreateRuntimeService("AL.Services.Local.LocalResourceService", "AL.Core.Interfaces.ISaveGameService", saveService);
                object creditService = CreateRuntimeService("AL.Services.Local.LocalWarzoneCreditService", "AL.Core.Interfaces.ISaveGameService", saveService);
                object questService = CreateQuestService(saveService, resourceService, creditService);

                IList quests = (IList)GetField(save, "Quests");
                Assert.NotNull(quests);
                Assert.AreEqual(5, quests.Cast<object>().Count(q => q != null));

                quests.Add(CreateQuestState("Q_UNKNOWN_FUTURE", 7, true, true));
                Invoke(saveService, "Save");

                object reloadedSaveService = CreateSaveService(root);
                Invoke(reloadedSaveService, "Load");
                object reloadedResourceService = CreateRuntimeService("AL.Services.Local.LocalResourceService", "AL.Core.Interfaces.ISaveGameService", reloadedSaveService);
                object reloadedCreditService = CreateRuntimeService("AL.Services.Local.LocalWarzoneCreditService", "AL.Core.Interfaces.ISaveGameService", reloadedSaveService);
                object reloadedQuestService = CreateQuestService(reloadedSaveService, reloadedResourceService, reloadedCreditService);

                object unknown = FindQuest(reloadedSaveService, "Q_UNKNOWN_FUTURE");
                object[] activeQuests = Enumerate(Invoke(reloadedQuestService, "GetActiveQuests"));

                Assert.NotNull(unknown);
                Assert.AreEqual(7, GetField(unknown, "CurrentValue"));
                Assert.True((bool)GetField(unknown, "IsCompleted"));
                Assert.True((bool)GetField(unknown, "IsClaimed"));
                Assert.False(activeQuests.Any(q => (string)GetField(q, "QuestId") == "Q_UNKNOWN_FUTURE"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void QuestServicesTolerateMalformedAndUnknownSavedStates()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-QuestTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object saveService = CreateSaveService(root);
                CreateNewSave(saveService);
                object resourceService = CreateRuntimeService("AL.Services.Local.LocalResourceService", "AL.Core.Interfaces.ISaveGameService", saveService);
                object creditService = CreateRuntimeService("AL.Services.Local.LocalWarzoneCreditService", "AL.Core.Interfaces.ISaveGameService", saveService);
                object questService = CreateQuestService(saveService, resourceService, creditService);
                object sideQuestService = CreateRuntimeService("AL.Services.Local.SideQuestService", "AL.Core.Interfaces.ISaveGameService", saveService, "AL.Core.Interfaces.IResourceService", resourceService);

                object save = GetProperty(saveService, "CurrentSave");
                IList quests = (IList)GetField(save, "Quests");
                quests.Insert(0, null);
                quests.Insert(1, CreateQuestState(null, 0, false, false));
                quests.Insert(2, CreateQuestState("   ", 0, false, false));
                quests.Add(CreateQuestState("Q_UNKNOWN_FUTURE", 99, true, false));
                quests.Add(CreateQuestState("Q1", 1, true, false));

                int startingGold = Convert.ToInt32(Invoke(resourceService, "GetResourceCount", EnumValue("AL.Core.ResourceType", "Gold")));
                int startingCredits = Convert.ToInt32(Invoke(creditService, "GetCredits"));

                object[] activeQuests = Enumerate(Invoke(questService, "GetActiveQuests"));
                object[] activeSideQuests = Enumerate(Invoke(sideQuestService, "GetActiveSideQuests"));
                Invoke(questService, "UpdateProgress", EnumValue("AL.Core.QuestType", "Side"), 1);
                Invoke(questService, "ClaimReward", "Q_UNKNOWN_FUTURE");
                Invoke(questService, "ClaimReward", " ");

                quests = (IList)GetField(save, "Quests");
                Assert.False(quests.Cast<object>().Any(q => q == null));
                Assert.False(quests.Cast<object>().Any(q => string.IsNullOrWhiteSpace((string)GetField(q, "QuestId"))));
                Assert.AreEqual(1, quests.Cast<object>().Count(q => (string)GetField(q, "QuestId") == "Q1"));
                Assert.AreEqual(1, quests.Cast<object>().Count(q => (string)GetField(q, "QuestId") == "Q_UNKNOWN_FUTURE"));
                Assert.False(activeQuests.Any(q => (string)GetField(q, "QuestId") == "Q_UNKNOWN_FUTURE"));
                Assert.False(activeSideQuests.Any(q => q == null));
                Assert.AreEqual(startingGold, Convert.ToInt32(Invoke(resourceService, "GetResourceCount", EnumValue("AL.Core.ResourceType", "Gold"))));
                Assert.AreEqual(startingCredits, Convert.ToInt32(Invoke(creditService, "GetCredits")));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void KnownQuestStillProgressesAndClaimsOnce()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-QuestTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object saveService = CreateSaveService(root);
                CreateNewSave(saveService);
                object resourceService = CreateRuntimeService("AL.Services.Local.LocalResourceService", "AL.Core.Interfaces.ISaveGameService", saveService);
                object creditService = CreateRuntimeService("AL.Services.Local.LocalWarzoneCreditService", "AL.Core.Interfaces.ISaveGameService", saveService);
                object questService = CreateQuestService(saveService, resourceService, creditService);
                object gold = EnumValue("AL.Core.ResourceType", "Gold");
                int startingGold = Convert.ToInt32(Invoke(resourceService, "GetResourceCount", gold));

                Invoke(questService, "UpdateProgress", EnumValue("AL.Core.QuestType", "BuildBuilding"), 1);
                Invoke(questService, "ClaimReward", "Q1");
                Invoke(questService, "ClaimReward", "Q1");

                object q1 = FindQuest(saveService, "Q1");
                Assert.NotNull(q1);
                Assert.True((bool)GetField(q1, "IsCompleted"));
                Assert.True((bool)GetField(q1, "IsClaimed"));
                Assert.AreEqual(startingGold + 1000, Convert.ToInt32(Invoke(resourceService, "GetResourceCount", gold)));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static object CreateQuestService(object saveService, object resourceService, object creditService)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalQuestService");
            ConstructorInfo constructor = serviceType.GetConstructor(new[]
            {
                GetRuntimeType("AL.Core.Interfaces.ISaveGameService"),
                GetRuntimeType("AL.Core.Interfaces.IResourceService"),
                GetRuntimeType("AL.Core.Interfaces.IWarzoneCreditService")
            });
            Assert.NotNull(constructor);
            return constructor.Invoke(new[] { saveService, resourceService, creditService });
        }

        private static object CreateRuntimeService(string serviceTypeName, string firstArgumentTypeName, object firstArgument, string secondArgumentTypeName = null, object secondArgument = null)
        {
            Type serviceType = GetRuntimeType(serviceTypeName);
            Type[] argumentTypes = secondArgumentTypeName == null
                ? new[] { GetRuntimeType(firstArgumentTypeName) }
                : new[] { GetRuntimeType(firstArgumentTypeName), GetRuntimeType(secondArgumentTypeName) };
            object[] arguments = secondArgumentTypeName == null
                ? new[] { firstArgument }
                : new[] { firstArgument, secondArgument };
            ConstructorInfo constructor = serviceType.GetConstructor(argumentTypes);
            Assert.NotNull(constructor, $"Expected constructor for {serviceTypeName}.");
            return constructor.Invoke(arguments);
        }

        private static object CreateSaveService(string root)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(constructor, "Expected the testable persistence-path constructor.");
            return constructor.Invoke(new object[] { root });
        }

        private static void CreateNewSave(object saveService)
        {
            Invoke(saveService, "CreateNewSave", EnumValue("AL.Core.RealmId", "None"));
        }

        private static object CreateQuestState(string questId, int currentValue, bool isCompleted, bool isClaimed)
        {
            object state = Activator.CreateInstance(GetRuntimeType("AL.Core.Interfaces.QuestState"));
            SetField(state, "QuestId", questId);
            SetField(state, "CurrentValue", currentValue);
            SetField(state, "IsCompleted", isCompleted);
            SetField(state, "IsClaimed", isClaimed);
            return state;
        }

        private static object FindQuest(object saveService, string questId)
        {
            object save = GetProperty(saveService, "CurrentSave");
            IList quests = (IList)GetField(save, "Quests");
            return quests.Cast<object>().FirstOrDefault(q => q != null && (string)GetField(q, "QuestId") == questId);
        }

        private static object[] Enumerate(object result)
        {
            return ((IEnumerable)result).Cast<object>().ToArray();
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
            Assert.NotNull(method, $"Expected method {methodName}.");
            return method.Invoke(target, args);
        }

        private static object EnumValue(string enumTypeName, string value)
        {
            return Enum.Parse(GetRuntimeType(enumTypeName), value);
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp")
                ?.GetType(typeName);
            Assert.NotNull(type, $"Expected runtime type {typeName} in Assembly-CSharp.");
            return type;
        }

        private static object GetProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, $"Expected property {name}.");
            return property.GetValue(target);
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Expected field {name}.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Expected field {name}.");
            field.SetValue(target, value);
        }
    }
}
