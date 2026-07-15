using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public class BootloaderServiceStackIntegrityTests
    {
        [SetUp]
        public void ClearServiceLocator()
        {
            IDictionary services = (IDictionary)GetRuntimeType("AL.Core.ServiceLocator")
                .GetField("Services", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);
            services.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ClearServiceLocator();
        }

        [Test]
        public void EmptyRegistryCreatesCompleteStackAndMarker()
        {
            object result = InitializeIfMissing();

            AssertState(result, "CreatedCompleteStack");

            foreach (Type serviceType in RequiredServiceTypes())
            {
                AssertRegistered(serviceType);
            }

            AssertRegistered(GetRuntimeType("AL.Core.IOfflineServiceStackMarker"));
        }

        [Test]
        public void RepeatedInitializationReusesExactRegisteredInstances()
        {
            object created = InitializeIfMissing();
            AssertState(created, "CreatedCompleteStack");

            Dictionary<Type, object> before = RequiredServiceTypes()
                .ToDictionary(type => type, GetService);

            object reused = InitializeIfMissing();
            AssertState(reused, "ReusedCompleteStack");

            foreach (var pair in before)
            {
                Assert.AreSame(pair.Value, GetService(pair.Key), $"Expected {pair.Key.Name} to be reused.");
            }
        }

        [Test]
        public void PartialRegistryFailsWithoutPublishingMarker()
        {
            Type saveType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            Type saveInterface = GetRuntimeType("AL.Core.Interfaces.ISaveGameService");
            object save = Activator.CreateInstance(saveType);

            Register(saveInterface, save);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_PARTIAL_REGISTRY\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedPartialRegistry");
            Assert.AreSame(save, GetService(saveInterface));
            AssertNotRegistered(GetRuntimeType("AL.Core.IOfflineServiceStackMarker"));
        }

        [Test]
        public void InconsistentMarkerFailsWithoutOverwritingReplacement()
        {
            object created = InitializeIfMissing();
            AssertState(created, "CreatedCompleteStack");

            Type saveInterface = GetRuntimeType("AL.Core.Interfaces.ISaveGameService");
            Type resourceInterface = GetRuntimeType("AL.Core.Interfaces.IResourceService");
            Type resourceType = GetRuntimeType("AL.Services.Local.LocalResourceService");
            object save = GetService(saveInterface);
            object replacementResource = Activator.CreateInstance(resourceType, save);

            Register(resourceInterface, replacementResource);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_MARKER_INCONSISTENT\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedInconsistentMarker");
            Assert.AreSame(replacementResource, GetService(resourceInterface));
        }

        [Test]
        public void TryGetReturnsFalseForMissingService()
        {
            Type serviceLocator = GetRuntimeType("AL.Core.ServiceLocator");
            Type resourceInterface = GetRuntimeType("AL.Core.Interfaces.IResourceService");
            MethodInfo tryGet = serviceLocator.GetMethod("TryGet", BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(resourceInterface);
            object[] args = { null };

            bool found = (bool)tryGet.Invoke(null, args);

            Assert.False(found);
            Assert.Null(args[0]);
        }

        private static object InitializeIfMissing()
        {
            return GetRuntimeType("AL.Core.Bootloader")
                .GetMethod("InitializeIfMissing", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
        }

        private static IReadOnlyList<Type> RequiredServiceTypes()
        {
            return ((Array)GetRuntimeType("AL.Core.OfflineServiceStack")
                    .GetField("RequiredServiceTypes", BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null))
                .Cast<Type>()
                .ToArray();
        }

        private static void AssertState(object result, string expected)
        {
            string state = result.GetType().GetProperty("State").GetValue(result).ToString();
            Assert.AreEqual(expected, state);
        }

        private static void AssertRegistered(Type serviceType)
        {
            Type serviceLocator = GetRuntimeType("AL.Core.ServiceLocator");
            MethodInfo isRegistered = serviceLocator.GetMethod("IsRegistered", BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(serviceType);
            Assert.True((bool)isRegistered.Invoke(null, null), $"Expected {serviceType.Name} to be registered.");
        }

        private static void AssertNotRegistered(Type serviceType)
        {
            Type serviceLocator = GetRuntimeType("AL.Core.ServiceLocator");
            MethodInfo isRegistered = serviceLocator.GetMethod("IsRegistered", BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(serviceType);
            Assert.False((bool)isRegistered.Invoke(null, null), $"Expected {serviceType.Name} to be absent.");
        }

        private static object GetService(Type serviceType)
        {
            Type serviceLocator = GetRuntimeType("AL.Core.ServiceLocator");
            MethodInfo get = serviceLocator.GetMethod("Get", BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(serviceType);
            return get.Invoke(null, null);
        }

        private static void Register(Type serviceType, object service)
        {
            Type serviceLocator = GetRuntimeType("AL.Core.ServiceLocator");
            MethodInfo register = serviceLocator.GetMethod("Register", BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(serviceType);
            register.Invoke(null, new[] { service });
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp")
                ?.GetType(typeName);

            Assert.NotNull(type, $"Expected runtime type {typeName} in Assembly-CSharp.");
            return type;
        }
    }
}
