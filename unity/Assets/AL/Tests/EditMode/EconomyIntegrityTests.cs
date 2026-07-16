using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public class EconomyIntegrityTests
    {
        [Test]
        public void ResourceServiceRejectsInvalidAmountsAndOverflow()
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateSaveService(root);
                CreateNewSave(saveService);
                object resourceService = CreateRuntimeService("AL.Services.Local.LocalResourceService", "AL.Core.Interfaces.ISaveGameService", saveService);
                object gold = EnumValue("AL.Core.ResourceType", "Gold");
                long startingGold = ResourceCount(resourceService, gold);

                Invoke(resourceService, "AddResource", gold, -10L);
                Invoke(resourceService, "AddResource", gold, 0L);
                Assert.False((bool)Invoke(resourceService, "ConsumeResource", gold, -100L));
                Assert.False((bool)Invoke(resourceService, "ConsumeResource", gold, 0L));
                Assert.False((bool)Invoke(resourceService, "HasEnough", gold, -1L));
                Assert.AreEqual(startingGold, ResourceCount(resourceService, gold));

                object goldEntry = FindResource(saveService, "Gold");
                SetField(goldEntry, "Amount", long.MaxValue);
                Invoke(resourceService, "AddResource", gold, 1L);
                Assert.AreEqual(long.MaxValue, ResourceCount(resourceService, gold));

                Assert.True((bool)Invoke(resourceService, "ConsumeResource", gold, 5L));
                Assert.AreEqual(long.MaxValue - 5L, ResourceCount(resourceService, gold));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void WalletCompatibilityRepairsMalformedEntriesAndSurvivesReload()
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateSaveService(root);
                CreateNewSave(saveService);
                object resourceService = CreateRuntimeService("AL.Services.Local.LocalResourceService", "AL.Core.Interfaces.ISaveGameService", saveService);
                object save = GetProperty(saveService, "CurrentSave");
                IList resources = (IList)GetField(save, "Resources");

                resources.Add(null);
                resources.Add(CreateResourceData("Gold", 25L));
                resources.Add(CreateResourceData("DeepOre", -50L));

                Assert.AreEqual(525L, ResourceCount(resourceService, EnumValue("AL.Core.ResourceType", "Gold")));
                Assert.AreEqual(0L, ResourceCount(resourceService, EnumValue("AL.Core.ResourceType", "DeepOre")));
                Assert.False(resources.Cast<object>().Any(entry => entry == null));
                Assert.AreEqual(1, resources.Cast<object>().Count(entry => GetField(entry, "Type").ToString() == "Gold"));
                Assert.AreEqual(1, resources.Cast<object>().Count(entry => GetField(entry, "Type").ToString() == "DeepOre"));

                Invoke(saveService, "Save");
                object reloadedSaveService = CreateSaveService(root);
                Invoke(reloadedSaveService, "Load");
                object reloadedResourceService = CreateRuntimeService("AL.Services.Local.LocalResourceService", "AL.Core.Interfaces.ISaveGameService", reloadedSaveService);

                Assert.AreEqual(525L, ResourceCount(reloadedResourceService, EnumValue("AL.Core.ResourceType", "Gold")));
                Assert.AreEqual(0L, ResourceCount(reloadedResourceService, EnumValue("AL.Core.ResourceType", "DeepOre")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void WarzoneCreditsRejectInvalidAmountsAndOverflow()
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateSaveService(root);
                CreateNewSave(saveService);
                object creditService = CreateRuntimeService("AL.Services.Local.LocalWarzoneCreditService", "AL.Core.Interfaces.ISaveGameService", saveService);
                object save = GetProperty(saveService, "CurrentSave");

                SetField(save, "WarzoneCredits", 10);
                Invoke(creditService, "AddCredits", -5);
                Assert.False((bool)Invoke(creditService, "SpendCredits", -5));
                Assert.False((bool)Invoke(creditService, "SpendCredits", 0));
                Assert.AreEqual(10, Invoke(creditService, "GetCredits"));

                SetField(save, "WarzoneCredits", int.MaxValue);
                Invoke(creditService, "AddCredits", 1);
                Assert.AreEqual(int.MaxValue, Invoke(creditService, "GetCredits"));

                SetField(save, "WarzoneCredits", -20);
                Assert.AreEqual(0, Invoke(creditService, "GetCredits"));
                Invoke(creditService, "AddCredits", 5);
                Assert.True((bool)Invoke(creditService, "SpendCredits", 3));
                Assert.AreEqual(2, Invoke(creditService, "GetCredits"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static long ResourceCount(object resourceService, object resourceType)
        {
            return Convert.ToInt64(Invoke(resourceService, "GetResourceCount", resourceType));
        }

        private static object FindResource(object saveService, string resourceTypeName)
        {
            object save = GetProperty(saveService, "CurrentSave");
            IList resources = (IList)GetField(save, "Resources");
            return resources.Cast<object>().First(entry => entry != null && GetField(entry, "Type").ToString() == resourceTypeName);
        }

        private static object CreateResourceData(string resourceTypeName, long amount)
        {
            object data = Activator.CreateInstance(GetRuntimeType("AL.Data.Runtime.ResourceData"));
            SetField(data, "Type", EnumValue("AL.Core.ResourceType", resourceTypeName));
            SetField(data, "Amount", amount);
            return data;
        }

        private static string CreateTempRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-EconomyTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private static object CreateRuntimeService(string serviceTypeName, string argumentTypeName, object argument)
        {
            Type serviceType = GetRuntimeType(serviceTypeName);
            ConstructorInfo constructor = serviceType.GetConstructor(new[] { GetRuntimeType(argumentTypeName) });
            Assert.NotNull(constructor, $"Expected constructor for {serviceTypeName}.");
            return constructor.Invoke(new[] { argument });
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
