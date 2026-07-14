using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public class RuntimeContractSmokeTests
    {
        [Test]
        public void RareResourceMappingCoversPlayableRealms()
        {
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Type resourceType = GetRuntimeType("AL.Core.ResourceType");
            MethodInfo getRareResource = GetRuntimeType("AL.Core.ResourceRules").GetMethod(
                "GetRareResourceForRealm",
                BindingFlags.Public | BindingFlags.Static);

            Assert.NotNull(getRareResource);
            Assert.AreEqual(EnumValue(resourceType, "DeepOre"), getRareResource.Invoke(null, new[] { EnumValue(realmType, "Stonehold") }));
            Assert.AreEqual(EnumValue(resourceType, "WorldSap"), getRareResource.Invoke(null, new[] { EnumValue(realmType, "Eldergrove") }));
            Assert.AreEqual(EnumValue(resourceType, "RoyalSigil"), getRareResource.Invoke(null, new[] { EnumValue(realmType, "Crownlands") }));
            Assert.AreEqual(EnumValue(resourceType, "DarkCrystal"), getRareResource.Invoke(null, new[] { EnumValue(realmType, "Umbral") }));
        }

        [Test]
        public void WalletResourcesContainUniqueRareResources()
        {
            Type resourceType = GetRuntimeType("AL.Core.ResourceType");
            FieldInfo walletResourcesField = GetRuntimeType("AL.Core.ResourceRules").GetField(
                "WalletResources",
                BindingFlags.Public | BindingFlags.Static);

            Assert.NotNull(walletResourcesField);
            var walletResources = ((Array)walletResourcesField.GetValue(null)).Cast<object>().ToArray();

            Assert.AreEqual(walletResources.Length, walletResources.Distinct().Count());
            CollectionAssert.Contains(walletResources, EnumValue(resourceType, "DeepOre"));
            CollectionAssert.Contains(walletResources, EnumValue(resourceType, "WorldSap"));
            CollectionAssert.Contains(walletResources, EnumValue(resourceType, "RoyalSigil"));
            CollectionAssert.Contains(walletResources, EnumValue(resourceType, "DarkCrystal"));
        }

        [Test]
        public void ServiceLocatorReplacesExistingRegistration()
        {
            Type serviceLocatorType = GetRuntimeType("AL.Core.ServiceLocator");
            MethodInfo register = serviceLocatorType.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
            MethodInfo get = serviceLocatorType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var first = new DummyRuntimeService("first");
            var replacement = new DummyRuntimeService("replacement");

            Assert.NotNull(register);
            Assert.NotNull(get);

            MethodInfo registerDummy = register.MakeGenericMethod(typeof(IDummyRuntimeService));
            MethodInfo getDummy = get.MakeGenericMethod(typeof(IDummyRuntimeService));

            registerDummy.Invoke(null, new object[] { first });
            Assert.AreSame(first, getDummy.Invoke(null, null));

            registerDummy.Invoke(null, new object[] { replacement });
            Assert.AreSame(replacement, getDummy.Invoke(null, null));
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp")
                ?.GetType(typeName);

            Assert.NotNull(type, $"Expected runtime type {typeName} in Assembly-CSharp.");
            return type;
        }

        private static object EnumValue(Type enumType, string value)
        {
            return Enum.Parse(enumType, value);
        }

        private interface IDummyRuntimeService
        {
        }

        private sealed class DummyRuntimeService : IDummyRuntimeService
        {
            public DummyRuntimeService(string id)
            {
                Id = id;
            }

            public string Id { get; }
        }
    }
}
