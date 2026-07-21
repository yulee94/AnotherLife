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
            SetBootloaderStatic("PostInstallValidationOverride", null);
            SetBootloaderStatic("PublishBeforeRegisterOverride", null);
            SetStackOverride("GameDataFactoryOverride", null);
            SetStackOverride("SaveGameFactoryOverride", null);
            SetStackOverride("ResourceFactoryOverride", null);
            SetStackOverride("NotificationFactoryOverride", null);
            SetStackOverride("BossLootFactoryOverride", null);
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
        public void RequiredServiceTypeInventoryIsImmutable()
        {
            object required = GetRuntimeType("AL.Core.OfflineServiceStack")
                .GetField("RequiredServiceTypes", BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);

            Assert.IsInstanceOf<IList>(required);
            Assert.Throws<NotSupportedException>(() => ((IList)required).Add(typeof(string)));
        }

        [Test]
        public void FailedLoadDoesNotPermanentlyClaimMarkerLoad()
        {
            object created = InitializeIfMissing();
            AssertState(created, "CreatedCompleteStack");

            object marker = GetService(GetRuntimeType("AL.Core.IOfflineServiceStackMarker"));
            const string owner = "load-owner";

            Assert.True((bool)Invoke(marker, "TryClaimRuntimeOwner", owner));
            Assert.True((bool)Invoke(marker, "TryBeginLoad", owner));
            Assert.False((bool)Invoke(marker, "TryBeginLoad", owner));

            Invoke(marker, "MarkLoadFailed", owner);
            Assert.True((bool)Invoke(marker, "TryBeginLoad", owner));

            Invoke(marker, "MarkLoadSucceeded", owner);
            Assert.False((bool)Invoke(marker, "TryBeginLoad", owner));
            Assert.False((bool)Invoke(marker, "TryBeginLoad", "different-owner"));
        }

        [Test]
        public void DuplicateBootloadersLeaveOnlyOneRuntimeOwnerActive()
        {
            Type bootloaderType = GetRuntimeType("AL.Core.Bootloader");
            var firstHost = new GameObject("BootloaderOwnerOne");
            var secondHost = new GameObject("BootloaderOwnerTwo");
            var first = (Behaviour)firstHost.AddComponent(bootloaderType);
            var second = (Behaviour)secondHost.AddComponent(bootloaderType);
            bootloaderType.GetField("_autoLoadOnStart", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(first, false);
            bootloaderType.GetField("_autoLoadOnStart", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(second, false);

            try
            {
                Invoke(first, "Awake");
                Assert.True(first.enabled);

                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_RUNTIME_OWNER_REJECTED\]"));
                Invoke(second, "Awake");

                // Reverse-ordering-safe standby: the second Bootloader stays enabled (so it can
                // reclaim later) but is not the runtime owner and does no runtime work.
                Assert.True(second.enabled);
                Assert.True((bool)GetInstanceField(second, "_standbyForOwnership"));
                Assert.False((bool)GetInstanceField(second, "_runtimeActive"));

                object marker = GetService(MarkerInterface());
                Assert.True((bool)Invoke(marker, "IsRuntimeOwner", OwnerIdOf(first)));
                Assert.False((bool)Invoke(marker, "IsRuntimeOwner", OwnerIdOf(second)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstHost);
                UnityEngine.Object.DestroyImmediate(secondHost);
            }
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
            MethodInfo factory = GetRuntimeType("AL.Core.BootloaderInitializationResult")
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method =>
                    method.Name == "FailedInconsistentMarker" &&
                    method.GetParameters().Length == 3);

            return (TResult)factory
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
            object value = GetRuntimeType("AL.Core.OfflineServiceStack")
                    .GetField("RequiredServiceTypes", BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null);

            return ((IEnumerable)value)
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

        // ---------------------------------------------------------------------
        // Empty and complete registry (spec section 12)
        // ---------------------------------------------------------------------

        [Test]
        public void MarkerRootsMatchSaveAndGameDataRegistrations()
        {
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");

            object marker = GetService(MarkerInterface());
            Assert.AreSame(GetService(Iface("ISaveGameService")), GetInstanceProperty(marker, "SaveRoot"));
            Assert.AreSame(GetService(Iface("IGameDataService")), GetInstanceProperty(marker, "GameDataRoot"));
        }

        [Test]
        public void PublishedAuthorityInventoriesMatchExactRequiredTypeSet()
        {
            var expected = new HashSet<string>
            {
                "IGameDataService", "ISaveGameService", "IRealmService", "IResourceService",
                "IResearchService", "IBuildingService", "ITrainingService", "IBattleSimulator",
                "IWarzoneCreditService", "IWarmasterService", "ITerritoryService", "IQuestService",
                "IStoryService", "IReputationService", "IFactionService", "IPersonaService",
                "INotificationService", "IRealmGemService", "IWorldStateService", "IWorldAtlasService",
                "IBossLootService"
            };

            var requiredTypeNames = RequiredServiceTypes().Select(type => type.Name).ToHashSet();
            Assert.AreEqual(21, requiredTypeNames.Count);
            Assert.That(requiredTypeNames, Is.EquivalentTo(expected));

            object stack = GetRuntimeType("AL.Core.OfflineServiceStack")
                .GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { 1 });
            var requiredInstances = (IReadOnlyDictionary<Type, object>)GetInstanceProperty(stack, "RequiredInstances");
            Assert.That(requiredInstances.Keys.Select(type => type.Name).ToHashSet(), Is.EquivalentTo(expected));

            AssertState(InitializeIfMissing(), "CreatedCompleteStack");
            object marker = GetService(MarkerInterface());
            Assert.That(ExpectedInstancesOf(marker).Keys.Select(type => type.Name).ToHashSet(), Is.EquivalentTo(expected));
        }

        // ---------------------------------------------------------------------
        // Partial registry (spec section 12)
        // ---------------------------------------------------------------------

        [Test]
        public void PartialRegistryWithOnlyResourceServiceFails()
        {
            object save = NewLocal("AL.Services.Local.LocalSaveGameService");
            object resource = NewLocal("AL.Services.Local.LocalResourceService", save);
            Register(Iface("IResourceService"), resource);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_PARTIAL_REGISTRY\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedPartialRegistry");
            Assert.That(TypeNames(result, "PresentTypes"), Is.EquivalentTo(new[] { "IResourceService" }));
            Assert.AreEqual(20, TypeNames(result, "MissingTypes").Count);
            AssertNotRegistered(MarkerInterface());
            Assert.AreSame(resource, GetService(Iface("IResourceService")));
        }

        [Test]
        public void PartialRegistryStoryWithoutQuestFails()
        {
            object save = NewLocal("AL.Services.Local.LocalSaveGameService");
            object gameData = NewLocal("AL.Services.Local.LocalGameDataService");
            object story = NewLocal("AL.Services.Local.LocalStoryService", save, gameData);
            Register(Iface("IStoryService"), story);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_PARTIAL_REGISTRY\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedPartialRegistry");
            var present = TypeNames(result, "PresentTypes");
            Assert.That(present, Is.EquivalentTo(new[] { "IStoryService" }));
            Assert.False(present.Contains("IQuestService"));
            AssertNotRegistered(MarkerInterface());
        }

        [Test]
        public void PartialRegistryQuestWithoutStoryFails()
        {
            object save = NewLocal("AL.Services.Local.LocalSaveGameService");
            object resource = NewLocal("AL.Services.Local.LocalResourceService", save);
            object warzone = NewLocal("AL.Services.Local.LocalWarzoneCreditService", save);
            object quest = NewLocal("AL.Services.Local.LocalQuestService", save, resource, warzone);
            Register(Iface("IQuestService"), quest);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_PARTIAL_REGISTRY\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedPartialRegistry");
            var present = TypeNames(result, "PresentTypes");
            Assert.That(present, Is.EquivalentTo(new[] { "IQuestService" }));
            Assert.False(present.Contains("IStoryService"));
            AssertNotRegistered(MarkerInterface());
        }

        [Test]
        public void PartialRegistrySeveralLocalTypesWithoutMarkerFails()
        {
            object save = NewLocal("AL.Services.Local.LocalSaveGameService");
            object gameData = NewLocal("AL.Services.Local.LocalGameDataService");
            object resource = NewLocal("AL.Services.Local.LocalResourceService", save);
            Register(Iface("ISaveGameService"), save);
            Register(Iface("IGameDataService"), gameData);
            Register(Iface("IResourceService"), resource);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_PARTIAL_REGISTRY\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedPartialRegistry");
            Assert.That(TypeNames(result, "PresentTypes"),
                Is.EquivalentTo(new[] { "ISaveGameService", "IGameDataService", "IResourceService" }));
            AssertNotRegistered(MarkerInterface());
            Assert.AreSame(save, GetService(Iface("ISaveGameService")));
        }

        [Test]
        public void PartialRegistryForeignImplementationWithoutMarkerFails()
        {
            object foreignResource = NewInternal("AL.Core.CountingResourceService");
            Register(Iface("IResourceService"), foreignResource);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_PARTIAL_REGISTRY\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedPartialRegistry");
            Assert.That(TypeNames(result, "PresentTypes"), Is.EquivalentTo(new[] { "IResourceService" }));
            Assert.AreSame(foreignResource, GetService(Iface("IResourceService")));
            AssertNotRegistered(MarkerInterface());
        }

        // ---------------------------------------------------------------------
        // Marker inconsistency (spec section 12 + cross-check hostile/malformed markers)
        // ---------------------------------------------------------------------

        [Test]
        public void MarkerMissingRequiredServiceFails()
        {
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");
            RemoveService(Iface("IResourceService"));

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_MARKER_INCONSISTENT\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedInconsistentMarker");
            Assert.That(TypeNames(result, "MissingTypes"), Does.Contain("IResourceService"));
        }

        [Test]
        public void UnsupportedMarkerVersionFails()
        {
            object marker = CreateConfigurableMarker(2, "unsupported-version",
                new Dictionary<Type, object>(), new object(), new object());
            RegisterMarker(marker);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_MARKER_INCONSISTENT\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedInconsistentMarker");
            Assert.That(TypeNames(result, "InvalidTypes"), Does.Contain("IOfflineServiceStackMarker"));
        }

        [Test]
        public void MismatchedMarkerRootFails()
        {
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");
            object realMarker = GetService(MarkerInterface());

            object marker = CreateConfigurableMarker(1, "mismatched-root",
                ExpectedInstancesOf(realMarker),
                new object(),
                GetInstanceProperty(realMarker, "GameDataRoot"));
            RegisterMarker(marker);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_MARKER_INCONSISTENT\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedInconsistentMarker");
            Assert.That(TypeNames(result, "InvalidTypes"), Does.Contain("IOfflineServiceStackMarker"));
        }

        [Test]
        public void NullExpectedInstanceMarkerFails()
        {
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");
            object realMarker = GetService(MarkerInterface());
            var expected = ExpectedInstancesOf(realMarker);
            expected[Iface("IResourceService")] = null;

            object marker = CreateConfigurableMarker(1, "null-expected", expected,
                GetInstanceProperty(realMarker, "SaveRoot"),
                GetInstanceProperty(realMarker, "GameDataRoot"));
            RegisterMarker(marker);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_MARKER_INCONSISTENT\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedInconsistentMarker");
            Assert.That(TypeNames(result, "MismatchedTypes"), Does.Contain("IResourceService"));
        }

        [Test]
        public void HostileMarkerWithNullExpectedInstancesDoesNotThrow()
        {
            object marker = CreateConfigurableMarker(1, "hostile-null-map", null, new object(), new object());
            RegisterMarker(marker);

            object result = null;
            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_MARKER_INCONSISTENT\]"));
            Assert.DoesNotThrow(() => { result = InitializeIfMissing(); });

            AssertState(result, "FailedInconsistentMarker");
        }

        [Test]
        public void WhitespaceRegistrationIdMarkerFails()
        {
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");
            object realMarker = GetService(MarkerInterface());

            object marker = CreateConfigurableMarker(1, "   ",
                ExpectedInstancesOf(realMarker),
                GetInstanceProperty(realMarker, "SaveRoot"),
                GetInstanceProperty(realMarker, "GameDataRoot"));
            RegisterMarker(marker);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_MARKER_INCONSISTENT\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedInconsistentMarker");
            Assert.That(TypeNames(result, "InvalidTypes"), Does.Contain("IOfflineServiceStackMarker"));
        }

        [Test]
        public void UnexpectedExtraKeyMarkerFails()
        {
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");
            object realMarker = GetService(MarkerInterface());
            var expected = ExpectedInstancesOf(realMarker);
            expected[typeof(string)] = "unexpected";

            object marker = CreateConfigurableMarker(1, "unexpected-extra-key", expected,
                GetInstanceProperty(realMarker, "SaveRoot"),
                GetInstanceProperty(realMarker, "GameDataRoot"));
            RegisterMarker(marker);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_MARKER_INCONSISTENT\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedInconsistentMarker");
            Assert.That(TypeNames(result, "UnexpectedTypes"), Does.Contain("String"));
        }

        [Test]
        public void WrongTypeExpectedInstanceMarkerFails()
        {
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");
            object realMarker = GetService(MarkerInterface());
            var expected = ExpectedInstancesOf(realMarker);
            expected[Iface("IResourceService")] = new object();

            object marker = CreateConfigurableMarker(1, "wrong-type-expected", expected,
                GetInstanceProperty(realMarker, "SaveRoot"),
                GetInstanceProperty(realMarker, "GameDataRoot"));
            RegisterMarker(marker);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_MARKER_INCONSISTENT\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedInconsistentMarker");
            Assert.That(TypeNames(result, "MismatchedTypes"), Does.Contain("IResourceService"));
        }

        [Test]
        public void MarkerWithClaimedRuntimeOwnerStillValidatesAsConsistent()
        {
            // RuntimeOwnerId is mutable runtime claim state, not marker-consistency identity:
            // a stack reused across Bootloaders keeps the first claimant's owner id and must still
            // validate. Reference-identity of the required registrations is the coherence proof.
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");
            object marker = GetService(MarkerInterface());
            Assert.True((bool)Invoke(marker, "TryClaimRuntimeOwner", "external-owner"));

            AssertState(InitializeIfMissing(), "ReusedCompleteStack");
            Assert.AreEqual("external-owner", (string)GetInstanceProperty(marker, "RuntimeOwnerId"));
        }

        // ---------------------------------------------------------------------
        // Construction faults (spec section 12)
        // ---------------------------------------------------------------------

        [Test]
        public void ConstructionFaultAtGameDataLeavesRegistryUntouched()
        {
            SetStackOverride("GameDataFactoryOverride", ThrowingFactory0());
            AssertConstructionFaultOutcome();
        }

        [Test]
        public void ConstructionFaultAtSaveLeavesRegistryUntouched()
        {
            SetStackOverride("SaveGameFactoryOverride", ThrowingFactory0());
            AssertConstructionFaultOutcome();
        }

        [Test]
        public void ConstructionFaultAtDependentResourceLeavesRegistryUntouched()
        {
            SetStackOverride("ResourceFactoryOverride", ThrowingFactory1());
            AssertConstructionFaultOutcome();
        }

        [Test]
        public void ConstructionFaultAtNotificationLeavesRegistryUntouched()
        {
            SetStackOverride("NotificationFactoryOverride", ThrowingFactory0());
            AssertConstructionFaultOutcome();
        }

        [Test]
        public void ConstructionFaultAtBossLootLeavesRegistryUntouched()
        {
            SetStackOverride("BossLootFactoryOverride", ThrowingFactory3());
            AssertConstructionFaultOutcome();
        }

        // ---------------------------------------------------------------------
        // Publication faults and direct PublishBatch pre-validation (spec section 12)
        // ---------------------------------------------------------------------

        [Test]
        public void PublicationFaultBeforeMarkerRollsBackRegistry()
        {
            Type saveType = Iface("ISaveGameService");
            Func<Type, object, bool> before = (type, instance) => type != saveType;
            SetBootloaderStatic("PublishBeforeRegisterOverride", before);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_PUBLICATION_FAILED\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedPublication");
            Assert.False(RequiredServiceTypes().Any(IsRegistered));
            AssertNotRegistered(MarkerInterface());
        }

        [Test]
        public void PublicationFaultAtMarkerLeavesNoResidue()
        {
            Type markerInterface = MarkerInterface();
            Func<Type, object, bool> before = (type, instance) => type != markerInterface;
            SetBootloaderStatic("PublishBeforeRegisterOverride", before);

            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_PUBLICATION_FAILED\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedPublication");
            Assert.False(RequiredServiceTypes().Any(IsRegistered));
            AssertNotRegistered(markerInterface);
        }

        [Test]
        public void PublishBatchRollbackRestoresPreExistingEntry()
        {
            Type resourceInterface = Iface("IResourceService");
            Type notificationInterface = Iface("INotificationService");
            object original = NewInternal("AL.Core.CountingResourceService");
            object replacement = NewInternal("AL.Core.CountingResourceService");
            object notification = NewLocal("AL.Services.Local.LocalNotificationService");

            Register(resourceInterface, original);

            Array entries = MakeEntries(
                resourceInterface, replacement,
                notificationInterface, notification);
            Func<Type, object, bool> before = (type, instance) => type != notificationInterface;

            object result = InvokePublishBatch(entries, before);

            Assert.False(PublicationSucceeded(result));
            Assert.AreSame(original, GetService(resourceInterface));
            AssertNotRegistered(notificationInterface);
        }

        [Test]
        public void PublishBatchRejectsNullType()
        {
            Array entries = MakeEntries(null, new object());
            object result = InvokePublishBatch(entries, null);

            Assert.False(PublicationSucceeded(result));
            Assert.False(RequiredServiceTypes().Any(IsRegistered));
        }

        [Test]
        public void PublishBatchRejectsNullInstance()
        {
            Type resourceInterface = Iface("IResourceService");
            Array entries = MakeEntries(resourceInterface, null);
            object result = InvokePublishBatch(entries, null);

            Assert.False(PublicationSucceeded(result));
            Assert.AreEqual(resourceInterface, PublicationServiceType(result));
            AssertNotRegistered(resourceInterface);
        }

        [Test]
        public void PublishBatchRejectsDuplicateType()
        {
            Type resourceInterface = Iface("IResourceService");
            object first = NewInternal("AL.Core.CountingResourceService");
            object second = NewInternal("AL.Core.CountingResourceService");
            Array entries = MakeEntries(resourceInterface, first, resourceInterface, second);
            object result = InvokePublishBatch(entries, null);

            Assert.False(PublicationSucceeded(result));
            Assert.AreEqual(resourceInterface, PublicationServiceType(result));
            AssertNotRegistered(resourceInterface);
        }

        [Test]
        public void PublishBatchRejectsMismatchedInstance()
        {
            Type resourceInterface = Iface("IResourceService");
            Array entries = MakeEntries(resourceInterface, new object());
            object result = InvokePublishBatch(entries, null);

            Assert.False(PublicationSucceeded(result));
            Assert.AreEqual(resourceInterface, PublicationServiceType(result));
            AssertNotRegistered(resourceInterface);
        }

        // ---------------------------------------------------------------------
        // Load-result matrix (spec section 10 auto-load path)
        // ---------------------------------------------------------------------

        [Test]
        public void AutoLoadCleanSuccessActivatesBootloader()
        {
            object fake = InstallControllableSave();
            var bootloader = NewBootloader(true, out var host);
            try
            {
                Invoke(bootloader, "Awake");
                Assert.True(bootloader.enabled);
                AssertState(InitializationResultOf(bootloader), "CreatedCompleteStack");
                Assert.AreEqual(1, GetInstanceField(fake, "LoadCount"));

                object marker = GetService(MarkerInterface());
                AssertLoadState(marker, "Succeeded");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AutoLoadThrowDisablesAndReleases()
        {
            object fake = InstallControllableSave();
            SetInstanceField(fake, "FailLoadTimes", 1);
            var bootloader = NewBootloader(true, out var host);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_LOAD_FAILED\]"));
                Invoke(bootloader, "Awake");

                Assert.False(bootloader.enabled);
                AssertState(InitializationResultOf(bootloader), "FailedLoad");

                object marker = GetService(MarkerInterface());
                Assert.AreEqual(string.Empty, (string)GetInstanceProperty(marker, "RuntimeOwnerId"));
                AssertLoadState(marker, "Failed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AutoLoadRecoveryFailedStatusIsRejected()
        {
            object fake = InstallControllableSave();
            SetInstanceField(fake, "LoadStatusToReport",
                EnumValue("AL.Core.Interfaces.SaveLoadStatus", "RecoveryFailed"));
            AssertAutoLoadRejected(fake);
        }

        [Test]
        public void AutoLoadNoneStatusIsRejected()
        {
            object fake = InstallControllableSave();
            SetInstanceField(fake, "LoadStatusToReport",
                EnumValue("AL.Core.Interfaces.SaveLoadStatus", "None"));
            AssertAutoLoadRejected(fake);
        }

        [Test]
        public void AutoLoadApprovedStatusWithNullSaveIsRejected()
        {
            object fake = InstallControllableSave();
            SetInstanceField(fake, "ProvideCurrentSaveOnLoad", false);
            AssertAutoLoadRejected(fake);
        }

        [Test]
        public void AutoLoadDriftDuringLoadIsRejected()
        {
            Type resourceInterface = Iface("IResourceService");
            object replacement = NewInternal("AL.Core.CountingResourceService");
            object fake = InstallControllableSave();
            Action drift = () => Register(resourceInterface, replacement);
            SetInstanceField(fake, "LoadCallback", drift);

            var bootloader = NewBootloader(true, out var host);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_LOAD_FAILED\]"));
                Invoke(bootloader, "Awake");

                Assert.False(bootloader.enabled);
                AssertState(InitializationResultOf(bootloader), "FailedLoad");
                Assert.AreSame(replacement, GetService(resourceInterface));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AutoLoadFailedThenSubsequentOwnerRetrySucceeds()
        {
            object fake = InstallControllableSave();
            SetInstanceField(fake, "FailLoadTimes", 1);

            var first = NewBootloader(true, out var firstHost);
            var second = NewBootloader(true, out var secondHost);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_LOAD_FAILED\]"));
                Invoke(first, "Awake");
                Assert.False(first.enabled);

                Invoke(second, "Awake");
                Assert.True(second.enabled);
                AssertState(InitializationResultOf(second), "ReusedCompleteStack");
                Assert.AreEqual(2, GetInstanceField(fake, "LoadCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstHost);
                UnityEngine.Object.DestroyImmediate(secondHost);
            }
        }

        [Test]
        public void AutoLoadInProgressConflictDisablesBootloader()
        {
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");
            object marker = GetService(MarkerInterface());

            var bootloader = NewBootloader(true, out var host);
            try
            {
                string ownerId = OwnerIdOf(bootloader);
                Assert.True((bool)Invoke(marker, "TryClaimRuntimeOwner", ownerId));
                Assert.True((bool)Invoke(marker, "TryBeginLoad", ownerId));

                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_LOAD_IN_PROGRESS\]"));
                Invoke(bootloader, "Awake");

                Assert.False(bootloader.enabled);
                AssertState(InitializationResultOf(bootloader), "FailedLoadInProgress");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------------
        // Save matrix (spec section 10 pause/quit)
        // ---------------------------------------------------------------------

        [Test]
        public void OwnerPauseSavesExactlyOnce()
        {
            object fake = InstallControllableSaveOwner(false, out var bootloader, out var host);
            try
            {
                Assert.True(bootloader.enabled);
                Invoke(fake, "SeedCurrentSave");

                Invoke(bootloader, "OnApplicationPause", true);

                Assert.AreEqual(1, GetInstanceField(fake, "SaveCount"));
                Assert.AreSame(fake, GetService(Iface("ISaveGameService")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PauseWithNullCurrentSaveLogsSaveFailedWithoutThrow()
        {
            object fake = InstallControllableSaveOwner(false, out var bootloader, out var host);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_SAVE_FAILED\]"));
                Assert.DoesNotThrow(() => Invoke(bootloader, "OnApplicationPause", true));
                Assert.AreEqual(0, GetInstanceField(fake, "SaveCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PauseWithThrowingSaveIsCaught()
        {
            object fake = InstallControllableSaveOwner(false, out var bootloader, out var host);
            try
            {
                Invoke(fake, "SeedCurrentSave");
                SetInstanceField(fake, "ThrowOnSave", true);

                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_SAVE_FAILED\]"));
                Assert.DoesNotThrow(() => Invoke(bootloader, "OnApplicationQuit"));
                Assert.AreEqual(1, GetInstanceField(fake, "SaveCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PauseWithNonPrimarySaveStatusLogsFailure()
        {
            object fake = InstallControllableSaveOwner(false, out var bootloader, out var host);
            try
            {
                Invoke(fake, "SeedCurrentSave");
                SetInstanceField(fake, "SaveStatusToReport",
                    EnumValue("AL.Core.Interfaces.SaveOperationStatus", "SaveFailedPreviousPreserved"));

                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_SAVE_FAILED\]"));
                Invoke(bootloader, "OnApplicationPause", true);
                Assert.AreEqual(1, GetInstanceField(fake, "SaveCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NonOwnerBootloaderPauseDoesNotSave()
        {
            object fake = InstallControllableSave();
            var first = NewBootloader(false, out var firstHost);
            var second = NewBootloader(false, out var secondHost);
            try
            {
                Invoke(first, "Awake");
                Assert.True(first.enabled);

                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_RUNTIME_OWNER_REJECTED\]"));
                Invoke(second, "Awake");
                // Standby (enabled) but not the runtime owner: the pause path must still not save.
                Assert.True(second.enabled);

                Invoke(fake, "SeedCurrentSave");
                Invoke(second, "OnApplicationPause", true);
                Assert.AreEqual(0, GetInstanceField(fake, "SaveCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstHost);
                UnityEngine.Object.DestroyImmediate(secondHost);
            }
        }

        [Test]
        public void PauseWithoutInitializedStackDoesNotThrow()
        {
            object save = NewLocal("AL.Services.Local.LocalSaveGameService");
            object resource = NewLocal("AL.Services.Local.LocalResourceService", save);
            Register(Iface("IResourceService"), resource);

            var bootloader = NewBootloader(false, out var host);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_PARTIAL_REGISTRY\]"));
                Invoke(bootloader, "Awake");
                Assert.False(bootloader.enabled);

                Assert.DoesNotThrow(() => Invoke(bootloader, "OnApplicationPause", true));
                Assert.DoesNotThrow(() => Invoke(bootloader, "OnApplicationQuit"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------------
        // Two-owner one-load/one-tick/one-save proof and owner handoff
        // ---------------------------------------------------------------------

        [Test]
        public void TwoOwnersProduceOneLoadOneTickOneSave()
        {
            object save = InstallControllableSave();
            object resource = NewInternal("AL.Core.CountingResourceService");
            Func<object, object> resourceFactory = _ => resource;
            SetStackOverride("ResourceFactoryOverride", resourceFactory);

            var first = NewBootloader(true, out var firstHost);
            var second = NewBootloader(true, out var secondHost);
            try
            {
                Invoke(first, "Awake");
                Assert.True(first.enabled);

                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_RUNTIME_OWNER_REJECTED\]"));
                Invoke(second, "Awake");
                // Second stays enabled in standby but never becomes the owner while the first is alive,
                // so it contributes zero loads/ticks/saves and the one-load/one-tick/one-save invariant holds.
                Assert.True(second.enabled);

                Invoke(first, "Update");
                Invoke(second, "Update");

                Invoke(first, "OnApplicationPause", true);
                Invoke(second, "OnApplicationPause", true);

                Assert.AreEqual(1, GetInstanceField(save, "LoadCount"));
                Assert.AreEqual(1, GetInstanceField(resource, "TickCount"));
                Assert.AreEqual(1, GetInstanceField(save, "SaveCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstHost);
                UnityEngine.Object.DestroyImmediate(secondHost);
            }
        }

        [Test]
        public void OwnerHandoffAfterDestroyClaimsWithoutDuplicateLoad()
        {
            object save = InstallControllableSave();

            var first = NewBootloader(true, out var firstHost);
            var second = NewBootloader(true, out var secondHost);
            try
            {
                Invoke(first, "Awake");
                Assert.True(first.enabled);
                Assert.AreEqual(1, GetInstanceField(save, "LoadCount"));

                // Simulate the owning Bootloader being destroyed during a scene transition.
                Invoke(first, "OnDestroy");

                Invoke(second, "Awake");
                Assert.True(second.enabled);
                AssertState(InitializationResultOf(second), "ReusedCompleteStack");
                Assert.AreEqual(1, GetInstanceField(save, "LoadCount"));

                object marker = GetService(MarkerInterface());
                Assert.True((bool)Invoke(marker, "IsRuntimeOwner", OwnerIdOf(second)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstHost);
                UnityEngine.Object.DestroyImmediate(secondHost);
            }
        }

        [Test]
        public void ReverseOrderingHandoffStandbyReclaimsWithoutDuplicateLoad()
        {
            object save = InstallControllableSave();
            object resource = NewInternal("AL.Core.CountingResourceService");
            Func<object, object> resourceFactory = _ => resource;
            SetStackOverride("ResourceFactoryOverride", resourceFactory);

            var first = NewBootloader(true, out var firstHost);
            var second = NewBootloader(true, out var secondHost);
            try
            {
                Invoke(first, "Awake");
                Assert.True(first.enabled);
                Assert.AreEqual(1, GetInstanceField(save, "LoadCount"));

                // Incoming Bootloader awakens BEFORE the outgoing owner's OnDestroy releases the claim:
                // it must stand by (stay enabled) rather than disable, and must not duplicate the load.
                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_RUNTIME_OWNER_REJECTED\]"));
                Invoke(second, "Awake");
                Assert.True(second.enabled);
                Assert.True((bool)GetInstanceField(second, "_standbyForOwnership"));
                Assert.AreEqual(1, GetInstanceField(save, "LoadCount"));

                // While the first owner is alive, a standby tick reclaims nothing and does no work.
                Invoke(second, "Update");
                Assert.AreEqual(0, GetInstanceField(resource, "TickCount"));
                Assert.AreEqual(1, GetInstanceField(save, "LoadCount"));

                // Outgoing owner is destroyed and releases the claim (its load already Succeeded).
                Invoke(first, "OnDestroy");

                // Next standby tick reclaims ownership and activates WITHOUT a second Load().
                Invoke(second, "Update");
                Assert.True(second.enabled);
                Assert.False((bool)GetInstanceField(second, "_standbyForOwnership"));
                AssertState(InitializationResultOf(second), "ReusedCompleteStack");
                Assert.AreEqual(1, GetInstanceField(save, "LoadCount"));

                object marker = GetService(MarkerInterface());
                Assert.True((bool)Invoke(marker, "IsRuntimeOwner", OwnerIdOf(second)));

                // Now the reclaimed owner ticks production on subsequent frames.
                Invoke(second, "Update");
                Assert.AreEqual(1, GetInstanceField(resource, "TickCount"));
                Assert.AreEqual(1, GetInstanceField(save, "LoadCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstHost);
                UnityEngine.Object.DestroyImmediate(secondHost);
            }
        }

        // ---------------------------------------------------------------------
        // Lifecycle safety and marker owner release
        // ---------------------------------------------------------------------

        [Test]
        public void UpdateBeforeInitializationDoesNothing()
        {
            var bootloader = NewBootloader(false, out var host);
            try
            {
                Assert.DoesNotThrow(() => Invoke(bootloader, "Update"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void UpdateAfterServiceReplacementReportsDriftOnceAndStopsTicking()
        {
            object resource = NewInternal("AL.Core.CountingResourceService");
            Func<object, object> resourceFactory = _ => resource;
            SetStackOverride("ResourceFactoryOverride", resourceFactory);

            var bootloader = NewBootloader(false, out var host);
            try
            {
                Invoke(bootloader, "Awake");
                Assert.True(bootloader.enabled);

                Type resourceInterface = Iface("IResourceService");
                object foreignResource = NewInternal("AL.Core.CountingResourceService");
                Register(resourceInterface, foreignResource);

                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_RUNTIME_DRIFT\]"));
                Invoke(bootloader, "Update");

                Assert.False(bootloader.enabled);
                Assert.AreEqual(0, GetInstanceField(resource, "TickCount"));

                // The single-report guard plus the inactive guard must suppress any further drift log.
                Invoke(bootloader, "Update");
                Assert.AreEqual(0, GetInstanceField(resource, "TickCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ActiveBootloaderWithForeignMarkerOwnerDoesNotSave()
        {
            object fake = InstallControllableSaveOwner(false, out var bootloader, out var host);
            try
            {
                Assert.True(bootloader.enabled);
                Invoke(fake, "SeedCurrentSave");

                // Hand marker ownership to a different owner while this Bootloader stays runtime-active,
                // so the pause path must reject the save through the !IsRuntimeOwner branch (not the
                // _runtimeActive guard and not the null-current-save guard).
                object marker = GetService(MarkerInterface());
                Assert.True((bool)Invoke(marker, "TryReleaseRuntimeOwner", OwnerIdOf(bootloader)));
                Assert.True((bool)Invoke(marker, "TryClaimRuntimeOwner", "foreign-owner"));

                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_SAVE_FAILED\]"));
                Invoke(bootloader, "OnApplicationPause", true);

                Assert.AreEqual(0, GetInstanceField(fake, "SaveCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MarkerReleaseAllowsNextOwnerToRetryInterruptedLoad()
        {
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");
            object marker = GetService(MarkerInterface());

            const string first = "owner-one";
            const string second = "owner-two";

            Assert.True((bool)Invoke(marker, "TryClaimRuntimeOwner", first));
            Assert.True((bool)Invoke(marker, "TryBeginLoad", first));

            Assert.True((bool)Invoke(marker, "TryReleaseRuntimeOwner", first));
            AssertLoadState(marker, "Failed");
            Assert.AreEqual(string.Empty, (string)GetInstanceProperty(marker, "RuntimeOwnerId"));

            Assert.True((bool)Invoke(marker, "TryClaimRuntimeOwner", second));
            Assert.True((bool)Invoke(marker, "TryBeginLoad", second));
            AssertLoadState(marker, "InProgress");
        }

        // ---------------------------------------------------------------------
        // Shared helpers for the expanded matrices
        // ---------------------------------------------------------------------

        private static Type Iface(string simpleName)
        {
            return GetRuntimeType("AL.Core.Interfaces." + simpleName);
        }

        private static Type MarkerInterface()
        {
            return GetRuntimeType("AL.Core.IOfflineServiceStackMarker");
        }

        private static void SetBootloaderStatic(string fieldName, object value)
        {
            GetRuntimeType("AL.Core.Bootloader")
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, value);
        }

        private static void SetStackOverride(string fieldName, object value)
        {
            GetRuntimeType("AL.Core.OfflineServiceStack")
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, value);
        }

        private static object NewInternal(string typeName)
        {
            return Activator.CreateInstance(GetRuntimeType(typeName), true);
        }

        private static object NewLocal(string typeName, params object[] args)
        {
            return Activator.CreateInstance(GetRuntimeType(typeName), args);
        }

        private static void SetInstanceField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .SetValue(target, value);
        }

        private static object GetInstanceField(object target, string name)
        {
            return target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .GetValue(target);
        }

        private static void SetInstanceProperty(object target, string name, object value)
        {
            target.GetType()
                .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static object GetInstanceProperty(object target, string name)
        {
            return target.GetType()
                .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static object EnumValue(string enumTypeName, string valueName)
        {
            return Enum.Parse(GetRuntimeType(enumTypeName), valueName);
        }

        private static void RemoveService(Type serviceType)
        {
            IDictionary services = (IDictionary)GetRuntimeType("AL.Core.ServiceLocator")
                .GetField("Services", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);
            services.Remove(serviceType);
        }

        private static Behaviour NewBootloader(bool autoLoad, out GameObject host)
        {
            Type bootloaderType = GetRuntimeType("AL.Core.Bootloader");
            host = new GameObject("Bootloader");
            var bootloader = (Behaviour)host.AddComponent(bootloaderType);
            bootloaderType.GetField("_autoLoadOnStart", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(bootloader, autoLoad);
            return bootloader;
        }

        private static string OwnerIdOf(Behaviour bootloader)
        {
            return (string)bootloader.GetType()
                .GetField("_runtimeOwnerId", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(bootloader);
        }

        private static object InitializationResultOf(Behaviour bootloader)
        {
            return bootloader.GetType()
                .GetField("_initializationResult", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(bootloader);
        }

        private static HashSet<string> TypeNames(object result, string propertyName)
        {
            var value = (IEnumerable)result.GetType().GetProperty(propertyName).GetValue(result);
            return value.Cast<Type>().Select(type => type.Name).ToHashSet();
        }

        private static void AssertLoadState(object marker, string expected)
        {
            Assert.AreEqual(expected, GetInstanceProperty(marker, "LoadState").ToString());
        }

        private static Dictionary<Type, object> ExpectedInstancesOf(object marker)
        {
            var readOnly = (IReadOnlyDictionary<Type, object>)GetInstanceProperty(marker, "ExpectedInstances");
            return readOnly.ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        private static object CreateConfigurableMarker(
            int stackVersion,
            string registrationId,
            IReadOnlyDictionary<Type, object> expectedInstances,
            object saveRoot,
            object gameDataRoot)
        {
            object marker = NewInternal("AL.Core.ConfigurableOfflineServiceStackMarker");
            SetInstanceProperty(marker, "StackVersion", stackVersion);
            SetInstanceProperty(marker, "RegistrationId", registrationId);
            SetInstanceProperty(marker, "ExpectedInstances", expectedInstances);
            SetInstanceProperty(marker, "SaveRoot", saveRoot);
            SetInstanceProperty(marker, "GameDataRoot", gameDataRoot);
            return marker;
        }

        private static void RegisterMarker(object marker)
        {
            Register(MarkerInterface(), marker);
        }

        private static object InvokePublishBatch(Array entries, object beforeRegister)
        {
            return GetRuntimeType("AL.Core.ServiceLocator")
                .GetMethod("PublishBatch", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { entries, beforeRegister });
        }

        private static Array MakeEntries(params object[] typeInstancePairs)
        {
            Type entryType = GetRuntimeType("AL.Core.ServiceRegistrationEntry");
            var ctor = entryType.GetConstructor(new[] { typeof(Type), typeof(object) });
            int count = typeInstancePairs.Length / 2;
            Array entries = Array.CreateInstance(entryType, count);
            for (int i = 0; i < count; i++)
            {
                object entry = ctor.Invoke(new[] { typeInstancePairs[i * 2], typeInstancePairs[i * 2 + 1] });
                entries.SetValue(entry, i);
            }
            return entries;
        }

        private static bool PublicationSucceeded(object result)
        {
            return (bool)result.GetType().GetProperty("Succeeded").GetValue(result);
        }

        private static Type PublicationServiceType(object result)
        {
            return (Type)result.GetType().GetProperty("ServiceType").GetValue(result);
        }

        private static Func<object> ThrowingFactory0()
        {
            return () => throw new InvalidOperationException("Injected construction fault.");
        }

        private static Func<object, object> ThrowingFactory1()
        {
            return _ => throw new InvalidOperationException("Injected construction fault.");
        }

        private static Func<object, object, object, object> ThrowingFactory3()
        {
            return (a, b, c) => throw new InvalidOperationException("Injected construction fault.");
        }

        private static void AssertConstructionFaultOutcome()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_CONSTRUCTION_FAILED\]"));
            object result = InitializeIfMissing();

            AssertState(result, "FailedConstruction");
            Assert.False(RequiredServiceTypes().Any(IsRegistered));
            AssertNotRegistered(MarkerInterface());
        }

        private static object InstallControllableSave()
        {
            object fake = NewInternal("AL.Core.ControllableSaveGameService");
            Func<object> factory = () => fake;
            SetStackOverride("SaveGameFactoryOverride", factory);
            return fake;
        }

        private static object InstallControllableSaveOwner(bool autoLoad, out Behaviour bootloader, out GameObject host)
        {
            object fake = InstallControllableSave();
            bootloader = NewBootloader(autoLoad, out host);
            Invoke(bootloader, "Awake");
            return fake;
        }

        private static void AssertAutoLoadRejected(object fake)
        {
            var bootloader = NewBootloader(true, out var host);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[BOOT_STACK_LOAD_FAILED\]"));
                Invoke(bootloader, "Awake");
                Assert.False(bootloader.enabled);
                AssertState(InitializationResultOf(bootloader), "FailedLoad");
                Assert.AreEqual(1, GetInstanceField(fake, "LoadCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
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
