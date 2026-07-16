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
            GetRuntimeType("AL.Core.Bootloader")
                .GetField("PostInstallValidationOverride", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, null);
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
        public void MarkerExpectedInstancesAreImmutable()
        {
            object created = InitializeIfMissing();
            AssertState(created, "CreatedCompleteStack");

            object marker = GetService(GetRuntimeType("AL.Core.IOfflineServiceStackMarker"));
            var expectedInstances = (IDictionary<Type, object>)marker.GetType()
                .GetProperty("ExpectedInstances")
                .GetValue(marker);

            Assert.Throws<NotSupportedException>(() =>
            {
                expectedInstances[typeof(string)] = null;
            });
        }

        [Test]
        public void FailedLoadDoesNotPermanentlyClaimMarkerLoad()
        {
            object created = InitializeIfMissing();
            AssertState(created, "CreatedCompleteStack");

            object marker = GetService(GetRuntimeType("AL.Core.IOfflineServiceStackMarker"));

            Assert.True((bool)Invoke(marker, "TryBeginLoad"));
            Assert.False((bool)Invoke(marker, "TryBeginLoad"));

            Invoke(marker, "MarkLoadFailed");
            Assert.True((bool)Invoke(marker, "TryBeginLoad"));

            Invoke(marker, "MarkLoadSucceeded");
            Assert.False((bool)Invoke(marker, "TryBeginLoad"));
        }

        [Test]
        public void PauseAfterServiceReplacementReportsDriftAndDisablesBootloader()
        {
            Type bootloaderType = GetRuntimeType("AL.Core.Bootloader");
            var host = new GameObject("BootloaderDriftTest");
            var bootloader = (Behaviour)host.AddComponent(bootloaderType);
            bootloaderType.GetField("_autoLoadOnStart", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(bootloader, false);

            try
            {
                Invoke(bootloader, "Awake");
                Assert.True(bootloader.enabled);

                Type saveType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
                Type saveInterface = GetRuntimeType("AL.Core.Interfaces.ISaveGameService");
                object replacementSave = Activator.CreateInstance(saveType);
                Register(saveInterface, replacementSave);

                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_RUNTIME_DRIFT\]"));
                Invoke(bootloader, "OnApplicationPause", true);

                Assert.False(bootloader.enabled);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PostInstallVerificationFailureRollsBackAttemptedStack()
        {
            Type bootloaderType = GetRuntimeType("AL.Core.Bootloader");
            Type resultType = GetRuntimeType("AL.Core.BootloaderInitializationResult");
            Type markerType = GetRuntimeType("AL.Core.IOfflineServiceStackMarker");
            Type funcType = typeof(Func<>).MakeGenericType(resultType);
            MethodInfo failedPublication = typeof(BootloaderServiceStackIntegrityTests)
                .GetMethod(nameof(CreateForcedPostInstallFailure), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(resultType, markerType);
            Delegate forcedFailure = Delegate.CreateDelegate(funcType, failedPublication);

            bootloaderType.GetField("PostInstallValidationOverride", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, forcedFailure);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_PUBLICATION_FAILED\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedPublication");
            Assert.False(RequiredServiceTypes().Any(IsRegistered));
            AssertNotRegistered(markerType);
        }

        private static TResult CreateForcedPostInstallFailure<TResult, TMarker>()
        {
            return (TResult)GetRuntimeType("AL.Core.BootloaderInitializationResult")
                .GetMethod("FailedInconsistentMarker", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[]
                {
                    "forced-post-install-failure",
                    new[] { typeof(TMarker) },
                    "Forced post-install verification failure."
                });
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

        private static bool IsRegistered(Type serviceType)
        {
            Type serviceLocator = GetRuntimeType("AL.Core.ServiceLocator");
            MethodInfo isRegistered = serviceLocator.GetMethod("IsRegistered", BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(serviceType);
            return (bool)isRegistered.Invoke(null, null);
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

        private static object Invoke(object target, string methodName, params object[] args)
        {
            return target.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(target, args);
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
