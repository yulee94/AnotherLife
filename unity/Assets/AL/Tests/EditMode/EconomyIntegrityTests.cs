using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public class EconomyIntegrityTests
    {
        private static readonly string[] WalletResourceNames =
        {
            "Food",
            "Wood",
            "Stone",
            "Gold",
            "ManaStone",
            "Ore",
            "DeepOre",
            "WorldSap",
            "RoyalSigil",
            "DarkCrystal"
        };

        private static readonly string[] CoreResourceNames =
        {
            "Food",
            "Wood",
            "Stone",
            "Gold",
            "ManaStone",
            "Ore"
        };

        private static readonly string[] RareResourceNames =
        {
            "DeepOre",
            "WorldSap",
            "RoyalSigil",
            "DarkCrystal"
        };

        [Test]
        public void ResourceAuthorityIsExactUniqueDefinedAndReadOnly()
        {
            Type rules = GetRuntimeType("AL.Core.ResourceRules");
            Type resourceType = GetRuntimeType("AL.Core.ResourceType");
            PropertyInfo walletProperty = rules.GetProperty("WalletResources", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(walletProperty);

            object walletAuthority = walletProperty.GetValue(null);
            string[] names = ((IEnumerable)walletAuthority).Cast<object>().Select(value => value.ToString()).ToArray();
            CollectionAssert.AreEqual(WalletResourceNames, names);
            Assert.AreEqual(names.Length, names.Distinct(StringComparer.Ordinal).Count());
            Assert.True(((IList)walletAuthority).IsReadOnly);
            Assert.Throws<NotSupportedException>(() => ((IList)walletAuthority)[0] = EnumValue(resourceType, "Gold"));

            MethodInfo supported = rules.GetMethod("IsSupportedWalletResource", BindingFlags.Public | BindingFlags.Static);
            MethodInfo core = rules.GetMethod("IsCoreResource", BindingFlags.Public | BindingFlags.Static);
            MethodInfo rare = rules.GetMethod("IsRareResource", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(supported);
            Assert.NotNull(core);
            Assert.NotNull(rare);

            foreach (string name in WalletResourceNames)
            {
                object value = EnumValue(resourceType, name);
                Assert.True((bool)supported.Invoke(null, new[] { value }), name);
                Assert.AreEqual(CoreResourceNames.Contains(name), (bool)core.Invoke(null, new[] { value }), name);
                Assert.AreEqual(RareResourceNames.Contains(name), (bool)rare.Invoke(null, new[] { value }), name);
            }

            object unsupported = Enum.ToObject(resourceType, 9001);
            Assert.False((bool)supported.Invoke(null, new[] { unsupported }));
            Assert.False((bool)core.Invoke(null, new[] { unsupported }));
            Assert.False((bool)rare.Invoke(null, new[] { unsupported }));

            MethodInfo tryRare = rules.GetMethod("TryGetRareResourceForRealm", BindingFlags.Public | BindingFlags.Static);
            Type realmType = GetRuntimeType("AL.Core.RealmId");
            Assert.NotNull(tryRare);
            AssertTryRare(tryRare, EnumValue(realmType, "Stonehold"), "DeepOre", true);
            AssertTryRare(tryRare, EnumValue(realmType, "Eldergrove"), "WorldSap", true);
            AssertTryRare(tryRare, EnumValue(realmType, "Crownlands"), "RoyalSigil", true);
            AssertTryRare(tryRare, EnumValue(realmType, "Umbral"), "DarkCrystal", true);
            AssertTryRare(tryRare, EnumValue(realmType, "None"), null, false);
            AssertTryRare(tryRare, Enum.ToObject(realmType, 9001), null, false);

            AssertDefaultResultIsSafe("AL.Core.Interfaces.EconomyBalanceReadResult", "Diagnostics");
            AssertDefaultResultIsSafe("AL.Core.Interfaces.EconomyMutationResult", "Diagnostics");
            AssertDefaultResultIsSafe("AL.Core.Interfaces.EconomyProductionTickResult", "Diagnostics");
            object defaultTick = Activator.CreateInstance(GetRuntimeType("AL.Core.Interfaces.EconomyProductionTickResult"));
            Assert.NotNull(GetProperty(defaultTick, "BalanceChanges"));
        }

        [TestCase("NullWallet", "AL-ECO-MALFORMED-WALLET")]
        [TestCase("NullRow", "AL-ECO-MALFORMED-WALLET")]
        [TestCase("DuplicateGold", "AL-ECO-DUPLICATE-RESOURCE")]
        [TestCase("NegativeGold", "AL-ECO-NEGATIVE-BALANCE")]
        [TestCase("MissingFood", "AL-ECO-MISSING-CORE-RESOURCE")]
        [TestCase("MissingWood", "AL-ECO-MISSING-CORE-RESOURCE")]
        [TestCase("MissingStone", "AL-ECO-MISSING-CORE-RESOURCE")]
        [TestCase("MissingGold", "AL-ECO-MISSING-CORE-RESOURCE")]
        [TestCase("MissingManaStone", "AL-ECO-MISSING-CORE-RESOURCE")]
        [TestCase("MissingOre", "AL-ECO-MISSING-CORE-RESOURCE")]
        [TestCase("TooManyRows", "AL-ECO-MALFORMED-WALLET")]
        public void ResourceReadAndMutationsPreserveMalformedWallet(string scenario, string diagnosticCode)
        {
            object save = CreateValidSave();
            IList resources = Resources(save);
            switch (scenario)
            {
                case "NullWallet":
                    SetField(save, "Resources", null);
                    resources = null;
                    break;
                case "NullRow":
                    resources.Add(null);
                    break;
                case "DuplicateGold":
                    resources.Add(CreateResourceData("Gold", 25));
                    break;
                case "NegativeGold":
                    SetField(FindResource(save, "Gold"), "Amount", -5L);
                    break;
                case "TooManyRows":
                    for (int index = resources.Count; index <= 256; index++)
                    {
                        resources.Add(CreateResourceData(
                            Enum.ToObject(GetRuntimeType("AL.Core.ResourceType"), 5000 + index),
                            index));
                    }
                    break;
                default:
                    RemoveResource(save, scenario.Substring("Missing".Length));
                    break;
            }

            WalletSnapshot before = SnapshotWallet(save);
            var fixture = CreateSaveFixture(save);
            object service = CreateResourceService(fixture);
            object gold = Resource("Gold");
            var events = new List<ResourceEvent>();
            AddResourceEventHandler(service, (type, balance) => events.Add(new ResourceEvent(type.ToString(), balance)));

            object firstRead = Invoke(service, "ReadResource", gold);
            object secondRead = Invoke(service, "ReadResource", gold);
            AssertStatus(firstRead, "UnavailableMalformedState");
            AssertStatus(secondRead, "UnavailableMalformedState");
            Assert.AreEqual(diagnosticCode, GetProperty(firstRead, "DiagnosticCode"));

            object add = Invoke(service, "TryAddResource", gold, 1L);
            object consume = Invoke(service, "TryConsumeResource", gold, 1L);
            AssertStatus(add, "RejectedMalformedState");
            AssertStatus(consume, "RejectedMalformedState");
            Assert.AreEqual(0, fixture.State.SaveCount);
            Assert.IsEmpty(events);
            AssertWalletUnchanged(save, before);
        }

        [Test]
        public void UnsupportedResourceIsRejectedWithoutInspectingOrMutatingWallet()
        {
            object save = CreateValidSave();
            WalletSnapshot before = SnapshotWallet(save);
            var fixture = CreateSaveFixture(save);
            object service = CreateResourceService(fixture);
            object unsupported = Enum.ToObject(GetRuntimeType("AL.Core.ResourceType"), 7001);

            object read = Invoke(service, "ReadResource", unsupported);
            object add = Invoke(service, "TryAddResource", unsupported, 5L);
            object consume = Invoke(service, "TryConsumeResource", unsupported, 5L);

            AssertStatus(read, "UnavailableUnsupportedCurrency");
            AssertStatus(add, "RejectedUnsupportedCurrency");
            AssertStatus(consume, "RejectedUnsupportedCurrency");
            Assert.AreEqual("AL-ECO-UNSUPPORTED-RESOURCE", GetProperty(read, "DiagnosticCode"));
            AssertWalletUnchanged(save, before);
            Assert.AreEqual(0, fixture.State.SaveCount);
        }

        [Test]
        public void UnknownRowsAndMissingOptionalResourcesArePureAndPreserved()
        {
            object save = CreateValidSave();
            RemoveResource(save, "DeepOre");
            IList resources = Resources(save);
            object unknown = CreateResourceData(Enum.ToObject(GetRuntimeType("AL.Core.ResourceType"), 8123), -500L);
            resources.Insert(2, unknown);
            WalletSnapshot before = SnapshotWallet(save);
            var fixture = CreateSaveFixture(save);
            object service = CreateResourceService(fixture);

            object goldRead = Invoke(service, "ReadResource", Resource("Gold"));
            AssertStatus(goldRead, "Available");
            Assert.AreEqual(100L, NullableLong(goldRead, "Balance"));
            CollectionAssert.Contains(DiagnosticCodes(goldRead), "AL-ECO-PRESERVED-UNKNOWN-RESOURCE");

            object rareRead = Invoke(service, "ReadResource", Resource("DeepOre"));
            AssertStatus(rareRead, "CompatibleMissingOptional");
            Assert.AreEqual(0L, NullableLong(rareRead, "Balance"));
            CollectionAssert.Contains(DiagnosticCodes(rareRead), "AL-ECO-MISSING-OPTIONAL-RESOURCE");
            AssertWalletUnchanged(save, before);
            Assert.AreSame(unknown, Resources(save)[2]);
            Assert.AreEqual(-500L, Convert.ToInt64(GetField(unknown, "Amount")));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReadOnlyGateExposesBalancesButRejectsMutation(bool gateThrows)
        {
            object save = CreateValidSave();
            SetField(save, "WarzoneCredits", 25);
            WalletSnapshot walletBefore = SnapshotWallet(save);
            var fixture = CreateSaveFixture(save);
            Func<bool> gate = gateThrows
                ? new Func<bool>(() => throw new InvalidOperationException("gate"))
                : new Func<bool>(() => false);
            object resources = CreateResourceService(fixture, gate);
            object credits = CreateCreditService(fixture, gate);

            object resourceRead = Invoke(resources, "ReadResource", Resource("Gold"));
            object creditRead = Invoke(credits, "ReadCredits");
            AssertStatus(resourceRead, "AvailableReadOnly");
            AssertStatus(creditRead, "AvailableReadOnly");
            Assert.AreEqual(100L, NullableLong(resourceRead, "Balance"));
            Assert.AreEqual(25L, NullableLong(creditRead, "Balance"));
            Assert.False((bool)Invoke(resources, "HasEnough", Resource("Gold"), 1L));

            AssertStatus(Invoke(resources, "TryAddResource", Resource("Gold"), 1L), "RejectedProfileNotWritable");
            AssertStatus(Invoke(resources, "TryConsumeResource", Resource("Gold"), 1L), "RejectedProfileNotWritable");
            AssertStatus(Invoke(credits, "TryAddCredits", 1), "RejectedProfileNotWritable");
            AssertStatus(Invoke(credits, "TrySpendCredits", 1), "RejectedProfileNotWritable");
            AssertWalletUnchanged(save, walletBefore);
            Assert.AreEqual(25, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, fixture.State.SaveCount);
        }

        [Test]
        public void PublicEconomyConstructorsFailClosedWithoutTypedAuthorityProvider()
        {
            AssertOnlyPublicSaveConstructor(
                "AL.Services.Local.LocalResourceService");
            AssertOnlyPublicSaveConstructor(
                "AL.Services.Local.LocalWarzoneCreditService");
            object save = CreateValidSave();
            SetField(save, "WarzoneCredits", 25);
            WalletSnapshot walletBefore = SnapshotWallet(save);
            var fixture = CreateSaveFixture(save);
            object resources = CreateRuntimeService(
                "AL.Services.Local.LocalResourceService",
                "AL.Core.Interfaces.ISaveGameService",
                fixture.Proxy);
            object credits = CreateRuntimeService(
                "AL.Services.Local.LocalWarzoneCreditService",
                "AL.Core.Interfaces.ISaveGameService",
                fixture.Proxy);
            var events = new List<ResourceEvent>();
            AddResourceEventHandler(
                resources,
                (type, balance) =>
                    events.Add(new ResourceEvent(type.ToString(), balance)));

            AssertStatus(
                Invoke(resources, "ReadResource", Resource("Gold")),
                "AvailableReadOnly");
            AssertStatus(Invoke(credits, "ReadCredits"), "AvailableReadOnly");
            AssertStatus(
                Invoke(resources, "TryAddResource", Resource("Gold"), 1L),
                "RejectedProfileNotWritable");
            AssertStatus(
                Invoke(resources, "TryConsumeResource", Resource("Gold"), 1L),
                "RejectedProfileNotWritable");
            AssertStatus(
                Invoke(credits, "TryAddCredits", 1),
                "RejectedProfileNotWritable");
            AssertStatus(
                Invoke(credits, "TrySpendCredits", 1),
                "RejectedProfileNotWritable");

            AssertWalletUnchanged(save, walletBefore);
            Assert.AreEqual(25, GetField(save, "WarzoneCredits"));
            Assert.IsEmpty(events);
            Assert.AreEqual(0, fixture.State.SaveCount);
        }

        [TestCase("MissingProvider")]
        [TestCase("NullSnapshot")]
        [TestCase("ThrowingProvider")]
        [TestCase("InvalidSnapshot")]
        [TestCase("MissingProfile")]
        [TestCase("MigrationRequired")]
        [TestCase("ForwardSchemaReadOnly")]
        [TestCase("DegradedReadOnly")]
        [TestCase("RecoveryRequired")]
        [TestCase("CommitUncertain")]
        [TestCase("Deleted")]
        [TestCase("Unavailable")]
        public void TypedNonWritableAuthorityPreservesReadsAndRejectsEveryEconomyMutation(
            string scenario)
        {
            object save = CreateValidSave();
            SetField(save, "WarzoneCredits", 25);
            WalletSnapshot walletBefore = SnapshotWallet(save);
            var saveFixture = CreateSaveFixture(save);
            AuthorityProviderFixture authority =
                CreateAuthorityProviderForScenario(scenario);
            ProductionProviderFixture production = CreateProductionProvider(
                CreateProductionSnapshot(
                    "Available",
                    "revision-a",
                    new ContributionSpec(Resource("Gold"), 0.5d)));
            object resources = CreateResourceServiceWithAuthority(
                saveFixture,
                authority?.Proxy,
                production.Proxy);
            object credits = CreateCreditServiceWithAuthority(
                saveFixture,
                authority?.Proxy);
            var events = new List<ResourceEvent>();
            AddResourceEventHandler(
                resources,
                (type, balance) =>
                    events.Add(new ResourceEvent(type.ToString(), balance)));

            AssertStatus(
                Invoke(resources, "ReadResource", Resource("Gold")),
                "AvailableReadOnly");
            AssertStatus(Invoke(credits, "ReadCredits"), "AvailableReadOnly");
            AssertStatus(
                Invoke(resources, "TryAddResource", Resource("Gold"), 1L),
                "RejectedProfileNotWritable");
            AssertStatus(
                Invoke(resources, "TryConsumeResource", Resource("Gold"), 1L),
                "RejectedProfileNotWritable");
            AssertStatus(
                Invoke(resources, "TryTickProduction", 0.5d),
                "RejectedProfileNotWritable");
            AssertStatus(
                Invoke(credits, "TryAddCredits", 1),
                "RejectedProfileNotWritable");
            AssertStatus(
                Invoke(credits, "TrySpendCredits", 1),
                "RejectedProfileNotWritable");

            AssertWalletUnchanged(save, walletBefore);
            Assert.AreEqual(25, GetField(save, "WarzoneCredits"));
            Assert.IsEmpty(events);
            Assert.AreEqual(0, saveFixture.State.SaveCount);
            Assert.AreEqual(0, production.State.CallCount);
            if (authority != null)
            {
                Assert.AreEqual(7, authority.State.CallCount);
            }
        }

        [Test]
        public void TypedWritableAuthorityIsReReadAtEveryEconomyBoundary()
        {
            object save = CreateValidSave();
            SetField(save, "WarzoneCredits", 25);
            var saveFixture = CreateSaveFixture(save);
            AuthorityProviderFixture authority = CreateAuthorityProvider(
                CreateAuthoritySnapshot("Writable"));
            object resources = CreateResourceServiceWithAuthority(
                saveFixture,
                authority.Proxy);
            object credits = CreateCreditServiceWithAuthority(
                saveFixture,
                authority.Proxy);

            AssertStatus(
                Invoke(resources, "ReadResource", Resource("Gold")),
                "Available");
            AssertStatus(
                Invoke(resources, "TryAddResource", Resource("Gold"), 5L),
                "Applied");
            AssertStatus(Invoke(credits, "TryAddCredits", 5), "Applied");
            Assert.AreEqual(3, authority.State.CallCount);

            authority.State.Snapshot =
                CreateAuthoritySnapshot("MigrationRequired");

            AssertStatus(
                Invoke(resources, "ReadResource", Resource("Gold")),
                "AvailableReadOnly");
            AssertStatus(
                Invoke(resources, "TryAddResource", Resource("Gold"), 5L),
                "RejectedProfileNotWritable");
            AssertStatus(Invoke(credits, "ReadCredits"), "AvailableReadOnly");
            AssertStatus(
                Invoke(credits, "TryAddCredits", 5),
                "RejectedProfileNotWritable");
            Assert.AreEqual(7, authority.State.CallCount);
            Assert.AreEqual(105L, GetField(FindResource(save, "Gold"), "Amount"));
            Assert.AreEqual(30, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, saveFixture.State.SaveCount);
        }

        [Test]
        public void ProductionCallbackAuthorityRevocationRejectsBeforeMutation()
        {
            object save = CreateValidSave();
            WalletSnapshot walletBefore = SnapshotWallet(save);
            var saveFixture = CreateSaveFixture(save);
            AuthorityProviderFixture authority = CreateAuthorityProvider(
                CreateAuthoritySnapshot("Writable"));
            ProductionProviderFixture production = CreateProductionProvider(
                CreateProductionSnapshot(
                    "Available",
                    "revision-a",
                    new ContributionSpec(Resource("Gold"), 1d)),
                onBuild: () => authority.State.Snapshot =
                    CreateAuthoritySnapshot("MigrationRequired"));
            object resources = CreateResourceServiceWithAuthority(
                saveFixture,
                authority.Proxy,
                production.Proxy);
            var events = new List<ResourceEvent>();
            AddResourceEventHandler(
                resources,
                (type, balance) =>
                    events.Add(new ResourceEvent(type.ToString(), balance)));

            AssertStatus(
                Invoke(resources, "TryTickProduction", 0.5d),
                "RejectedProfileNotWritable");

            AssertWalletUnchanged(save, walletBefore);
            Assert.IsEmpty(events);
            Assert.AreEqual(0, saveFixture.State.SaveCount);
            Assert.AreEqual(1, production.State.CallCount);
            Assert.AreEqual(2, authority.State.CallCount);
        }

        [Test]
        public void ProductionCallbackPublishedSaveSwapRejectsBeforeMutation()
        {
            object save = CreateValidSave();
            object replacement = CreateValidSave();
            WalletSnapshot walletBefore = SnapshotWallet(save);
            WalletSnapshot replacementBefore = SnapshotWallet(replacement);
            var saveFixture = CreateSaveFixture(save);
            AuthorityProviderFixture authority = CreateAuthorityProvider(
                CreateAuthoritySnapshot("Writable"));
            ProductionProviderFixture production = CreateProductionProvider(
                CreateProductionSnapshot(
                    "Available",
                    "revision-a",
                    new ContributionSpec(Resource("Gold"), 1d)),
                onBuild: () => saveFixture.State.CurrentSave = replacement);
            object resources = CreateResourceServiceWithAuthority(
                saveFixture,
                authority.Proxy,
                production.Proxy);

            AssertStatus(
                Invoke(resources, "TryTickProduction", 0.5d),
                "RejectedProfileNotWritable");

            AssertWalletUnchanged(save, walletBefore);
            AssertWalletUnchanged(replacement, replacementBefore);
            Assert.AreEqual(0, saveFixture.State.SaveCount);
            Assert.AreEqual(1, production.State.CallCount);
            Assert.AreEqual(2, authority.State.CallCount);
        }

        [Test]
        public void LocalSaveAuthorityKeepsLegacySchemaReadOnlyUntilMigration()
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateActualSaveService(root);
                Invoke(
                    saveService,
                    "CreateNewSave",
                    EnumValue(GetRuntimeType("AL.Core.RealmId"), "Crownlands"));
                object authority = Invoke(saveService, "GetCurrentAuthority");
                AssertStatus(authority, "MigrationRequired");

                object resources = CreateRuntimeService(
                    "AL.Services.Local.LocalResourceService",
                    "AL.Core.Interfaces.ISaveGameService",
                    saveService);
                object credits = CreateRuntimeService(
                    "AL.Services.Local.LocalWarzoneCreditService",
                    "AL.Core.Interfaces.ISaveGameService",
                    saveService);
                object save = GetProperty(saveService, "CurrentSave");
                WalletSnapshot before = SnapshotWallet(save);

                AssertStatus(
                    Invoke(resources, "ReadResource", Resource("Gold")),
                    "AvailableReadOnly");
                AssertStatus(Invoke(credits, "ReadCredits"), "AvailableReadOnly");
                AssertStatus(
                    Invoke(resources, "TryAddResource", Resource("Gold"), 1L),
                    "RejectedProfileNotWritable");
                AssertStatus(
                    Invoke(credits, "TryAddCredits", 1),
                    "RejectedProfileNotWritable");
                AssertWalletUnchanged(save, before);
                Assert.AreEqual(0, GetField(save, "WarzoneCredits"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestCase("LoadedPrimaryNormalized")]
        [TestCase("LoadedPrimaryWithPreservedUnknown")]
        public void LocalSaveLegacyReadOnlyCompatibilityViewsRemainMigrationRequired(
            string loadStatus)
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateActualSaveService(root);
                Invoke(
                    saveService,
                    "CreateNewSave",
                    EnumValue(GetRuntimeType("AL.Core.RealmId"), "Crownlands"));
                object legacyView = GetProperty(saveService, "CurrentSave");

                SetField(saveService, "_currentSave", null);
                SetField(saveService, "_readOnlyCandidate", legacyView);
                SetField(saveService, "_profileWritable", false);
                SetField(saveService, "_hasObservedAuthoritySource", true);
                SetField(
                    saveService,
                    "_observedAuthoritySource",
                    EnumValue(
                        GetRuntimeType(
                            "AL.Core.SaveAuthority.ProfileAuthoritySourceGeneration"),
                        "Primary"));
                SetProperty(
                    saveService,
                    "LastLoadStatus",
                    EnumValue(
                        GetRuntimeType("AL.Core.Interfaces.SaveLoadStatus"),
                        loadStatus));

                object authority = Invoke(saveService, "GetCurrentAuthority");
                AssertStatus(authority, "MigrationRequired");
                Assert.AreEqual(1, GetProperty(authority, "SaveSchemaVersion"));
                Assert.AreEqual(
                    1,
                    GetProperty(authority, "ProfileInitializationVersion"));
                Assert.AreEqual(
                    "Primary",
                    GetProperty(authority, "SelectedSourceGeneration")
                        .ToString());
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ManualAndGenericCandidateWritesStayContainedBeforeMutation()
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateActualSaveService(root);
                Invoke(
                    saveService,
                    "CreateNewSave",
                    EnumValue(GetRuntimeType("AL.Core.RealmId"), "Crownlands"));
                object published = GetProperty(saveService, "CurrentSave");
                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                byte[] primaryBefore = File.ReadAllBytes(primaryPath);
                byte[] backupBefore = File.ReadAllBytes(backupPath);

                LogAssert.Expect(
                    LogType.Log,
                    new Regex("^AL-SAVE-MANUAL-WRITE-CONTAINED:"));
                Invoke(saveService, "Save");
                Assert.AreEqual(
                    "SaveFailedPreviousPreserved",
                    GetProperty(saveService, "LastSaveStatus").ToString());

                object commit = InvokePreparedCandidate(
                    saveService,
                    "authority-transition-chapter");
                Assert.AreEqual(
                    "ReadOnly",
                    GetProperty(commit, "Outcome").ToString());
                Assert.AreSame(published, GetProperty(saveService, "CurrentSave"));
                Assert.AreNotEqual(
                    "authority-transition-chapter",
                    GetField(published, "CurrentChapterId"));
                CollectionAssert.AreEqual(primaryBefore, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(backupPath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LocalSaveFailureAndRecoveryPrecedenceCannotReturnMigrationRequired()
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateActualSaveService(root);
                Invoke(
                    saveService,
                    "CreateNewSave",
                    EnumValue(GetRuntimeType("AL.Core.RealmId"), "Crownlands"));
                SetObservedAuthoritySource(saveService, "Primary");
                SetField(saveService, "_profileWritable", false);
                SetProperty(
                    saveService,
                    "LastSaveStatus",
                    EnumValue(
                        GetRuntimeType("AL.Core.Interfaces.SaveOperationStatus"),
                        "SaveFailedPreviousPreserved"));
                AssertStatus(
                    Invoke(saveService, "GetCurrentAuthority"),
                    "DegradedReadOnly");

                SetProperty(
                    saveService,
                    "LastSaveStatus",
                    EnumValue(
                        GetRuntimeType("AL.Core.Interfaces.SaveOperationStatus"),
                        "None"));
                SetProperty(
                    saveService,
                    "LastLoadStatus",
                    EnumValue(
                        GetRuntimeType("AL.Core.Interfaces.SaveLoadStatus"),
                        "RecoveryRequired"));
                AssertStatus(
                    Invoke(saveService, "GetCurrentAuthority"),
                    "RecoveryRequired");
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void TypedResourceMutationMatrixIsCheckedAndSaveFree()
        {
            object save = CreateValidSave();
            var fixture = CreateSaveFixture(save);
            object service = CreateResourceService(fixture);
            var events = new List<ResourceEvent>();
            AddResourceEventHandler(service, (type, balance) => events.Add(new ResourceEvent(type.ToString(), balance)));

            object add = Invoke(service, "TryAddResource", Resource("Gold"), 25L);
            AssertMutation(add, "Applied", 25L, 100L, 125L, true);
            Assert.AreEqual(125L, ResourceCount(service, "Gold"));
            CollectionAssert.AreEqual(new[] { "Gold:125" }, events.Select(item => item.ToString()).ToArray());
            Assert.AreEqual(0, fixture.State.SaveCount);

            events.Clear();
            AssertMutation(Invoke(service, "TryAddResource", Resource("Gold"), 0L), "NoChange", 0L, null, null, false);
            AssertMutation(Invoke(service, "TryAddResource", Resource("Gold"), -1L), "RejectedInvalidAmount", -1L, null, null, false);
            Assert.IsEmpty(events);

            SetField(FindResource(save, "Gold"), "Amount", long.MaxValue);
            object overflow = Invoke(service, "TryAddResource", Resource("Gold"), 1L);
            AssertMutation(overflow, "RejectedOverflow", 1L, long.MaxValue, long.MaxValue, false);
            Assert.AreEqual(long.MaxValue, ResourceCount(service, "Gold"));
            Assert.IsEmpty(events);

            SetField(FindResource(save, "Gold"), "Amount", 10L);
            object consume = Invoke(service, "TryConsumeResource", Resource("Gold"), 4L);
            AssertMutation(consume, "Applied", 4L, 10L, 6L, true);
            Assert.AreEqual(6L, ResourceCount(service, "Gold"));
            CollectionAssert.AreEqual(new[] { "Gold:6" }, events.Select(item => item.ToString()).ToArray());
            events.Clear();
            AssertStatus(Invoke(service, "TryConsumeResource", Resource("Gold"), 7L), "RejectedInsufficientBalance");
            AssertStatus(Invoke(service, "TryConsumeResource", Resource("Gold"), 0L), "RejectedInvalidAmount");
            AssertStatus(Invoke(service, "TryConsumeResource", Resource("Gold"), -100L), "RejectedInvalidAmount");
            Assert.AreEqual(6L, ResourceCount(service, "Gold"));
            Assert.IsEmpty(events);

            object exact = Invoke(service, "TryConsumeResource", Resource("Gold"), 6L);
            AssertMutation(exact, "Applied", 6L, 6L, 0L, true);
            CollectionAssert.AreEqual(new[] { "Gold:0" }, events.Select(item => item.ToString()).ToArray());
            events.Clear();
            Assert.False((bool)Invoke(service, "HasEnough", Resource("Gold"), 1L));
            Assert.False((bool)Invoke(service, "HasEnough", Resource("Gold"), 0L));

            Invoke(service, "AddResource", Resource("Gold"), 2L);
            Assert.True((bool)Invoke(service, "ConsumeResource", Resource("Gold"), 1L));
            CollectionAssert.AreEqual(
                new[] { "Gold:2", "Gold:1" },
                events.Select(item => item.ToString()).ToArray());
            Assert.AreEqual(0, fixture.State.SaveCount, "Resource compatibility wrappers must remain save-free.");
        }

        [Test]
        public void ResourceUnavailableAndAffordabilityFailuresAreEventAndSaveFree()
        {
            var noSaveFixture = CreateSaveFixture(null);
            object noSaveService = CreateResourceService(noSaveFixture);
            var noSaveEvents = new List<ResourceEvent>();
            AddResourceEventHandler(noSaveService, (type, balance) => noSaveEvents.Add(new ResourceEvent(type.ToString(), balance)));

            AssertStatus(Invoke(noSaveService, "TryAddResource", Resource("Gold"), 1L), "RejectedNoCurrentSave");
            AssertStatus(Invoke(noSaveService, "TryConsumeResource", Resource("Gold"), 1L), "RejectedNoCurrentSave");
            Assert.False((bool)Invoke(noSaveService, "HasEnough", Resource("Gold"), 1L));
            Assert.AreEqual(0, noSaveFixture.State.SaveCount);
            Assert.IsEmpty(noSaveEvents);

            object malformedSave = CreateValidSave();
            Resources(malformedSave).Add(CreateResourceData("Gold", 1L));
            var malformedFixture = CreateSaveFixture(malformedSave);
            object malformedService = CreateResourceService(malformedFixture);
            var malformedEvents = new List<ResourceEvent>();
            AddResourceEventHandler(malformedService, (type, balance) => malformedEvents.Add(new ResourceEvent(type.ToString(), balance)));

            Assert.False((bool)Invoke(malformedService, "HasEnough", Resource("Gold"), 1L));
            Assert.False((bool)Invoke(malformedService, "HasEnough", Resource("DeepOre"), 1L));
            Assert.False((bool)Invoke(malformedService, "HasEnough", Enum.ToObject(GetRuntimeType("AL.Core.ResourceType"), 9000), 1L));
            Assert.AreEqual(0, malformedFixture.State.SaveCount);
            Assert.IsEmpty(malformedEvents);
        }

        [Test]
        public void OptionalRareInsertionIsPositiveOnlyAppendOnlyAndBounded()
        {
            object save = CreateValidSave();
            RemoveResource(save, "DeepOre");
            IList resources = Resources(save);
            object tail = resources[resources.Count - 1];
            var fixture = CreateSaveFixture(save);
            object service = CreateResourceService(fixture);

            AssertStatus(Invoke(service, "TryAddResource", Resource("DeepOre"), 0L), "NoChange");
            AssertStatus(Invoke(service, "TryConsumeResource", Resource("DeepOre"), 1L), "RejectedInsufficientBalance");
            Assert.Null(FindResourceOrDefault(save, "DeepOre"));

            object applied = Invoke(service, "TryAddResource", Resource("DeepOre"), 7L);
            AssertMutation(applied, "Applied", 7L, 0L, 7L, true);
            Assert.AreSame(tail, resources[resources.Count - 2]);
            Assert.AreEqual("DeepOre", GetField(resources[resources.Count - 1], "Type").ToString());
            Assert.AreEqual(7L, GetField(resources[resources.Count - 1], "Amount"));

            object boundedSave = CreateValidSave(includeOptional: false);
            IList boundedWallet = Resources(boundedSave);
            for (int index = boundedWallet.Count; index < 256; index++)
            {
                boundedWallet.Add(CreateResourceData(Enum.ToObject(GetRuntimeType("AL.Core.ResourceType"), 1000 + index), index));
            }

            var boundedFixture = CreateSaveFixture(boundedSave);
            object boundedService = CreateResourceService(boundedFixture);
            WalletSnapshot before = SnapshotWallet(boundedSave);
            object rejected = Invoke(boundedService, "TryAddResource", Resource("DeepOre"), 1L);
            AssertStatus(rejected, "RejectedMalformedState");
            Assert.AreEqual("AL-ECO-MALFORMED-WALLET", GetProperty(rejected, "DiagnosticCode"));
            AssertWalletUnchanged(boundedSave, before);
        }

        [Test]
        public void ResourceEventSubscribersArePostCommitAndFailureIsolated()
        {
            object save = CreateValidSave();
            var fixture = CreateSaveFixture(save);
            object service = CreateResourceService(fixture);
            var calls = new List<string>();

            AddResourceEventHandler(service, (type, balance) =>
            {
                calls.Add($"throw:{type}:{balance}:{ResourceCount(service, type.ToString())}");
                throw new InvalidOperationException("subscriber");
            });
            AddResourceEventHandler(service, (type, balance) =>
            {
                calls.Add($"later:{type}:{balance}:{ResourceCount(service, type.ToString())}");
            });

            LogAssert.Expect(LogType.Warning, new Regex("AL-ECO-EVENT-HANDLER"));
            object result = Invoke(service, "TryAddResource", Resource("Gold"), 3L);
            AssertStatus(result, "Applied");
            CollectionAssert.AreEqual(
                new[] { "throw:Gold:103:103", "later:Gold:103:103" },
                calls);
            Assert.AreEqual(103L, ResourceCount(service, "Gold"));
        }

        [Test]
        public void CreditReadsArePureForMissingValidNegativeAndReadOnlyState()
        {
            var noSaveFixture = CreateSaveFixture(null);
            object noSaveService = CreateCreditService(noSaveFixture);
            AssertStatus(Invoke(noSaveService, "ReadCredits"), "UnavailableNoCurrentSave");
            Assert.AreEqual(0, Invoke(noSaveService, "GetCredits"));
            AssertStatus(Invoke(noSaveService, "TryAddCredits", 1), "RejectedNoCurrentSave");
            AssertStatus(Invoke(noSaveService, "TrySpendCredits", 1), "RejectedNoCurrentSave");

            object save = CreateValidSave();
            SetField(save, "WarzoneCredits", 12);
            var fixture = CreateSaveFixture(save);
            object service = CreateCreditService(fixture);
            object read = Invoke(service, "ReadCredits");
            AssertStatus(read, "Available");
            Assert.AreEqual(12L, NullableLong(read, "Balance"));
            Assert.AreEqual(12, Invoke(service, "GetCredits"));

            SetField(save, "WarzoneCredits", -20);
            object malformed = Invoke(service, "ReadCredits");
            AssertStatus(malformed, "UnavailableMalformedState");
            Assert.AreEqual("AL-ECO-INVALID-CREDITS", GetProperty(malformed, "DiagnosticCode"));
            Assert.AreEqual(0, Invoke(service, "GetCredits"));
            Assert.AreEqual(-20, GetField(save, "WarzoneCredits"), "Read must preserve malformed persisted credits.");
            AssertStatus(Invoke(service, "TryAddCredits", 5), "RejectedMalformedState");
            AssertStatus(Invoke(service, "TrySpendCredits", 5), "RejectedMalformedState");
            Assert.AreEqual(-20, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, fixture.State.SaveCount);
        }

        [Test]
        public void TypedAndLegacyCreditMutationMatrixHasExactSaveCounts()
        {
            object save = CreateValidSave();
            SetField(save, "WarzoneCredits", 10);
            var fixture = CreateSaveFixture(save);
            object service = CreateCreditService(fixture);

            object add = Invoke(service, "TryAddCredits", 5);
            AssertMutation(add, "Applied", 5L, 10L, 15L, true);
            Assert.AreEqual(0, fixture.State.SaveCount);
            AssertStatus(Invoke(service, "TryAddCredits", 0), "NoChange");
            AssertStatus(Invoke(service, "TryAddCredits", -1), "RejectedInvalidAmount");

            SetField(save, "WarzoneCredits", int.MaxValue);
            object overflow = Invoke(service, "TryAddCredits", 1);
            AssertMutation(overflow, "RejectedOverflow", 1L, int.MaxValue, int.MaxValue, false);
            Assert.AreEqual(int.MaxValue, GetField(save, "WarzoneCredits"));

            SetField(save, "WarzoneCredits", 10);
            object spend = Invoke(service, "TrySpendCredits", 4);
            AssertMutation(spend, "Applied", 4L, 10L, 6L, true);
            AssertStatus(Invoke(service, "TrySpendCredits", 7), "RejectedInsufficientBalance");
            AssertStatus(Invoke(service, "TrySpendCredits", 0), "RejectedInvalidAmount");
            AssertStatus(Invoke(service, "TrySpendCredits", -9), "RejectedInvalidAmount");
            Assert.AreEqual(6, GetField(save, "WarzoneCredits"));
            AssertStatus(Invoke(service, "TrySpendCredits", 6), "Applied");
            Assert.AreEqual(0, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, fixture.State.SaveCount, "Typed credit methods never save.");

            Invoke(service, "AddCredits", 3);
            Assert.AreEqual(1, fixture.State.SaveCount);
            Assert.True((bool)Invoke(service, "SpendCredits", 2));
            Assert.AreEqual(2, fixture.State.SaveCount);
            Assert.AreEqual(1, GetField(save, "WarzoneCredits"));

            Invoke(service, "AddCredits", 0);
            Assert.False((bool)Invoke(service, "SpendCredits", 2));
            Assert.AreEqual(2, fixture.State.SaveCount, "No-change and insufficient wrappers save zero times.");
        }

        [Test]
        public void LegacyCreditWrappersNeverSaveRejectedOperations()
        {
            object save = CreateValidSave();
            SetField(save, "WarzoneCredits", 10);
            var fixture = CreateSaveFixture(save);
            object service = CreateCreditService(fixture);

            Invoke(service, "AddCredits", -1);
            Assert.AreEqual(10, GetField(save, "WarzoneCredits"));
            SetField(save, "WarzoneCredits", int.MaxValue);
            Invoke(service, "AddCredits", 1);
            Assert.AreEqual(int.MaxValue, GetField(save, "WarzoneCredits"));
            SetField(save, "WarzoneCredits", 10);
            Assert.False((bool)Invoke(service, "SpendCredits", 0));
            Assert.False((bool)Invoke(service, "SpendCredits", -1));
            Assert.AreEqual(10, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, fixture.State.SaveCount);

            var noSaveFixture = CreateSaveFixture(null);
            object noSaveService = CreateCreditService(noSaveFixture);
            Invoke(noSaveService, "AddCredits", 1);
            Assert.False((bool)Invoke(noSaveService, "SpendCredits", 1));
            Assert.AreEqual(0, noSaveFixture.State.SaveCount);

            object readOnlySave = CreateValidSave();
            SetField(readOnlySave, "WarzoneCredits", 10);
            var readOnlyFixture = CreateSaveFixture(readOnlySave);
            object readOnlyService = CreateCreditService(readOnlyFixture, () => false);
            Invoke(readOnlyService, "AddCredits", 1);
            Assert.False((bool)Invoke(readOnlyService, "SpendCredits", 1));
            Assert.AreEqual(10, GetField(readOnlySave, "WarzoneCredits"));
            Assert.AreEqual(0, readOnlyFixture.State.SaveCount);

            object malformedSave = CreateValidSave();
            SetField(malformedSave, "WarzoneCredits", -10);
            var malformedFixture = CreateSaveFixture(malformedSave);
            object malformedService = CreateCreditService(malformedFixture);
            Invoke(malformedService, "AddCredits", 1);
            Assert.False((bool)Invoke(malformedService, "SpendCredits", 1));
            Assert.AreEqual(-10, GetField(malformedSave, "WarzoneCredits"));
            Assert.AreEqual(0, malformedFixture.State.SaveCount);
        }

        [TestCase(0d)]
        [TestCase(-0.1d)]
        [TestCase(1.0001d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void ProductionRejectsInvalidDeltaBeforeProvider(double deltaSeconds)
        {
            object save = CreateValidSave();
            WalletSnapshot before = SnapshotWallet(save);
            var saveFixture = CreateSaveFixture(save);
            var providerFixture = CreateProductionProvider(CreateProductionSnapshot(
                "Available",
                "source-v1",
                new ContributionSpec("Food", 1d)));
            object service = CreateResourceService(saveFixture, () => true, providerFixture.Proxy);
            double[] remaindersBefore = ProductionRemainders(service).ToArray();

            object result = Invoke(service, "TryTickProduction", deltaSeconds);
            AssertStatus(result, "RejectedInvalidAmount");
            Assert.AreEqual("AL-ECO-PRODUCTION-INVALID-DELTA", GetProperty(result, "DiagnosticCode"));
            Assert.AreEqual(0, providerFixture.State.CallCount);
            AssertWalletUnchanged(save, before);
            CollectionAssert.AreEqual(remaindersBefore, ProductionRemainders(service));
            Assert.AreEqual(0, saveFixture.State.SaveCount);
        }

        [Test]
        public void ProductionForwardsValidDeltaExactly()
        {
            object save = CreateValidSave();
            var saveFixture = CreateSaveFixture(save);
            var provider = CreateProductionProvider(CreateProductionSnapshot("Available", "source-v1"));
            object service = CreateResourceService(saveFixture, () => true, provider.Proxy);

            object result = Invoke(service, "TryTickProduction", 0.25d);

            AssertStatus(result, "NoChange");
            Assert.AreEqual(1, provider.State.CallCount);
            Assert.That(provider.State.LastDeltaSeconds, Is.EqualTo(0.25d));
            Assert.AreEqual(0, saveFixture.State.SaveCount);
        }

        [Test]
        public void ProductionRejectsProfileAndWalletGatesBeforeProviderCall()
        {
            var provider = CreateProductionProvider(CreateProductionSnapshot("Available", "source-v1"));

            var noSaveFixture = CreateSaveFixture(null);
            object noSaveService = CreateResourceService(noSaveFixture, () => true, provider.Proxy);
            AssertStatus(Invoke(noSaveService, "TryTickProduction", 1d), "RejectedNoCurrentSave");
            Assert.AreEqual(0, provider.State.CallCount);

            var readOnlyFixture = CreateSaveFixture(CreateValidSave());
            object readOnlyService = CreateResourceService(readOnlyFixture, () => false, provider.Proxy);
            AssertStatus(Invoke(readOnlyService, "TryTickProduction", 1d), "RejectedProfileNotWritable");
            Assert.AreEqual(0, provider.State.CallCount);

            object malformedSave = CreateValidSave();
            Resources(malformedSave).Add(CreateResourceData("Gold", 1L));
            var malformedFixture = CreateSaveFixture(malformedSave);
            object malformedService = CreateResourceService(malformedFixture, () => true, provider.Proxy);
            AssertStatus(Invoke(malformedService, "TryTickProduction", 1d), "RejectedMalformedState");
            Assert.AreEqual(0, provider.State.CallCount);
            Assert.AreEqual(0, noSaveFixture.State.SaveCount + readOnlyFixture.State.SaveCount + malformedFixture.State.SaveCount);
        }

        [Test]
        public void ProductionFailsClosedForMissingUnavailableNullAndThrowingSource()
        {
            object save = CreateValidSave();
            var saveFixture = CreateSaveFixture(save);
            WalletSnapshot before = SnapshotWallet(save);

            object missingService = CreateResourceService(saveFixture);
            object missing = Invoke(missingService, "TryTickProduction", 1d);
            AssertStatus(missing, "RejectedDependencyUnavailable");
            Assert.AreEqual("AL-ECO-PRODUCTION-DEPENDENCY", GetProperty(missing, "DiagnosticCode"));
            AssertWalletUnchanged(save, before);

            Type contributionType = GetRuntimeType("AL.Core.Interfaces.EconomyProductionContribution");
            Type diagnosticType = GetRuntimeType("AL.Core.Interfaces.EconomyDiagnostic");
            IList arbitraryDiagnostics = CreateRuntimeList(diagnosticType);
            arbitraryDiagnostics.Add(CreateDiagnostic("CALLER-PRIVATE-CODE", "C:/private/provider/path"));
            object unavailableSnapshot = CreateProductionSnapshot(
                "Unavailable",
                "profile-a",
                "source-v1",
                CreateRuntimeList(contributionType),
                arbitraryDiagnostics);
            var unavailableProvider = CreateProductionProvider(unavailableSnapshot);
            object unavailableService = CreateResourceService(saveFixture, () => true, unavailableProvider.Proxy);
            object unavailable = Invoke(unavailableService, "TryTickProduction", 1d);
            AssertStatus(unavailable, "RejectedDependencyUnavailable");
            Assert.AreEqual("AL-ECO-PRODUCTION-DEPENDENCY", GetProperty(unavailable, "DiagnosticCode"));
            object publicDiagnostic = ((IEnumerable)GetProperty(unavailable, "Diagnostics")).Cast<object>().Single();
            Assert.AreEqual("Production.Source", GetProperty(publicDiagnostic, "RecordPath"));

            var nullProvider = CreateProductionProvider(null);
            object nullService = CreateResourceService(saveFixture, () => true, nullProvider.Proxy);
            AssertStatus(Invoke(nullService, "TryTickProduction", 1d), "RejectedDependencyUnavailable");

            var throwingProvider = CreateProductionProvider(null, throws: true);
            object throwingService = CreateResourceService(saveFixture, () => true, throwingProvider.Proxy);
            AssertStatus(Invoke(throwingService, "TryTickProduction", 1d), "RejectedDependencyUnavailable");

            var blankRevisionProvider = CreateProductionProvider(CreateProductionSnapshot(
                "Available",
                string.Empty,
                new ContributionSpec("Food", 1d)));
            object blankRevisionService = CreateResourceService(saveFixture, () => true, blankRevisionProvider.Proxy);
            AssertStatus(Invoke(blankRevisionService, "TryTickProduction", 1d), "RejectedDependencyUnavailable");
            AssertWalletUnchanged(save, before);
            Assert.AreEqual(0, saveFixture.State.SaveCount);
        }

        [TestCase("Unsupported", 1d, "RejectedDependencyUnavailable")]
        [TestCase("Food", -1d, "RejectedDependencyUnavailable")]
        [TestCase("Food", double.NaN, "RejectedDependencyUnavailable")]
        [TestCase("Food", double.PositiveInfinity, "RejectedDependencyUnavailable")]
        public void ProductionRejectsInvalidContributionsAtomically(string resourceName, double amount, string status)
        {
            object save = CreateValidSave();
            var saveFixture = CreateSaveFixture(save);
            object resourceType = resourceName == "Unsupported"
                ? Enum.ToObject(GetRuntimeType("AL.Core.ResourceType"), 9090)
                : Resource(resourceName);
            object snapshot = CreateProductionSnapshot(
                "Available",
                "source-v1",
                new ContributionSpec(resourceType, amount));
            var provider = CreateProductionProvider(snapshot);
            object service = CreateResourceService(saveFixture, () => true, provider.Proxy);
            WalletSnapshot before = SnapshotWallet(save);
            double[] remainders = ProductionRemainders(service).ToArray();

            object result = Invoke(service, "TryTickProduction", 1d);
            AssertStatus(result, status);
            AssertWalletUnchanged(save, before);
            CollectionAssert.AreEqual(remainders, ProductionRemainders(service));
            Assert.AreEqual(0, saveFixture.State.SaveCount);
        }

        [Test]
        public void ProductionRejectsLateInvalidAggregateAndWholeOverflowAtomically()
        {
            object save = CreateValidSave();
            var saveFixture = CreateSaveFixture(save);
            object unsupported = Enum.ToObject(GetRuntimeType("AL.Core.ResourceType"), 9090);
            object service = CreateResourceService(saveFixture, () => true, CreateProductionProvider(
                CreateProductionSnapshot(
                    "Available",
                    "source-v1",
                    new ContributionSpec("Food", 1d),
                    new ContributionSpec(unsupported, 1d))).Proxy);
            WalletSnapshot before = SnapshotWallet(save);
            var events = new List<ResourceEvent>();
            AddResourceEventHandler(service, (type, balance) => events.Add(new ResourceEvent(type.ToString(), balance)));

            AssertStatus(Invoke(service, "TryTickProduction", 1d), "RejectedDependencyUnavailable");
            AssertWalletUnchanged(save, before);
            Assert.IsEmpty(events);

            var aggregateProvider = CreateProductionProvider(CreateProductionSnapshot(
                "Available",
                "source-v2",
                new ContributionSpec("Food", double.MaxValue),
                new ContributionSpec("Food", double.MaxValue)));
            object aggregateService = CreateResourceService(saveFixture, () => true, aggregateProvider.Proxy);
            AssertStatus(Invoke(aggregateService, "TryTickProduction", 1d), "RejectedOverflow");
            AssertWalletUnchanged(save, before);

            var wholeProvider = CreateProductionProvider(CreateProductionSnapshot(
                "Available",
                "source-v3",
                new ContributionSpec("Food", 9223372036854775808d)));
            object wholeService = CreateResourceService(saveFixture, () => true, wholeProvider.Proxy);
            AssertStatus(Invoke(wholeService, "TryTickProduction", 1d), "RejectedOverflow");
            AssertWalletUnchanged(save, before);
            Assert.AreEqual(0, saveFixture.State.SaveCount);
        }

        [Test]
        public void ProductionCommitsWholeBatchThenEventsInWalletOrder()
        {
            object save = CreateValidSave();
            RemoveResource(save, "DeepOre");
            var saveFixture = CreateSaveFixture(save);
            object snapshot = CreateProductionSnapshot(
                "Available",
                "source-v1",
                new ContributionSpec("DeepOre", 1.5d),
                new ContributionSpec("Gold", 1.25d),
                new ContributionSpec("Food", 0.2d),
                new ContributionSpec("Food", 0.2d));
            var provider = CreateProductionProvider(snapshot);
            object service = CreateResourceService(saveFixture, () => true, provider.Proxy);
            var events = new List<string>();
            AddResourceEventHandler(service, (type, balance) =>
            {
                events.Add($"{type}:{balance}:gold={ResourceCount(service, "Gold")}:deep={ResourceCount(service, "DeepOre")}");
            });

            object first = Invoke(service, "TryTickProduction", 1d);
            AssertStatus(first, "Applied");
            CollectionAssert.AreEqual(new[] { "Gold", "DeepOre" }, BalanceChangeTypes(first));
            CollectionAssert.AreEqual(
                new[]
                {
                    "Gold:101:gold=101:deep=1",
                    "DeepOre:1:gold=101:deep=1"
                },
                events);
            Assert.AreEqual(100L, ResourceCount(service, "Food"));
            Assert.AreEqual(101L, ResourceCount(service, "Gold"));
            Assert.AreEqual(1L, ResourceCount(service, "DeepOre"));
            Assert.That(Remainder(service, "Food"), Is.EqualTo(0.4d).Within(1e-12));
            Assert.That(Remainder(service, "Gold"), Is.EqualTo(0.25d).Within(1e-12));
            Assert.That(Remainder(service, "DeepOre"), Is.EqualTo(0.5d).Within(1e-12));
            Assert.AreEqual(0, saveFixture.State.SaveCount);

            events.Clear();
            object second = Invoke(service, "TryTickProduction", 1d);
            AssertStatus(second, "Applied");
            Assert.AreEqual(100L, ResourceCount(service, "Food"));
            Assert.AreEqual(102L, ResourceCount(service, "Gold"));
            Assert.AreEqual(3L, ResourceCount(service, "DeepOre"));
            CollectionAssert.AreEqual(new[] { "Gold", "DeepOre" }, BalanceChangeTypes(second));
            Assert.That(Remainder(service, "Food"), Is.EqualTo(0.8d).Within(1e-12));
            Assert.That(Remainder(service, "Gold"), Is.EqualTo(0.5d).Within(1e-12));
            Assert.That(Remainder(service, "DeepOre"), Is.EqualTo(0d).Within(1e-12));
        }

        [Test]
        public void OptionalRareProductionWaitsForWholeBeforeInsertion()
        {
            object save = CreateValidSave();
            RemoveResource(save, "WorldSap");
            var saveFixture = CreateSaveFixture(save);
            var provider = CreateProductionProvider(CreateProductionSnapshot(
                "Available",
                "source-v1",
                new ContributionSpec("WorldSap", 0.5d)));
            object service = CreateResourceService(saveFixture, () => true, provider.Proxy);
            var events = new List<ResourceEvent>();
            AddResourceEventHandler(service, (type, balance) => events.Add(new ResourceEvent(type.ToString(), balance)));

            object first = Invoke(service, "TryTickProduction", 1d);
            AssertStatus(first, "Applied");
            Assert.Null(FindResourceOrDefault(save, "WorldSap"));
            Assert.IsEmpty(events);
            Assert.That(Remainder(service, "WorldSap"), Is.EqualTo(0.5d).Within(1e-12));

            object second = Invoke(service, "TryTickProduction", 1d);
            AssertStatus(second, "Applied");
            Assert.AreEqual(1L, ResourceCount(service, "WorldSap"));
            CollectionAssert.AreEqual(new[] { "WorldSap:1" }, events.Select(item => item.ToString()).ToArray());
            Assert.That(Remainder(service, "WorldSap"), Is.EqualTo(0d).Within(1e-12));
        }

        [Test]
        public void ProductionOverflowAndMalformedRemainderRollBackEverything()
        {
            object save = CreateValidSave();
            RemoveResource(save, "DeepOre");
            var saveFixture = CreateSaveFixture(save);
            var provider = CreateProductionProvider(CreateProductionSnapshot(
                "Available",
                "source-seed",
                new ContributionSpec("Food", 0.5d)));
            object service = CreateResourceService(saveFixture, () => true, provider.Proxy);
            AssertStatus(Invoke(service, "TryTickProduction", 1d), "Applied");
            Assert.That(Remainder(service, "Food"), Is.EqualTo(0.5d).Within(1e-12));

            SetField(FindResource(save, "Gold"), "Amount", long.MaxValue);
            provider.State.Snapshot = CreateProductionSnapshot(
                "Available",
                "source-v1",
                new ContributionSpec("Food", 1d),
                new ContributionSpec("Gold", 1d),
                new ContributionSpec("DeepOre", 1d));
            WalletSnapshot before = SnapshotWallet(save);
            double[] remaindersBefore = ProductionRemainders(service).ToArray();
            var events = new List<ResourceEvent>();
            AddResourceEventHandler(service, (type, balance) => events.Add(new ResourceEvent(type.ToString(), balance)));

            object overflow = Invoke(service, "TryTickProduction", 1d);
            AssertStatus(overflow, "RejectedOverflow");
            AssertWalletUnchanged(save, before);
            CollectionAssert.AreEqual(remaindersBefore, ProductionRemainders(service));
            Assert.IsEmpty(events);

            SetField(FindResource(save, "Gold"), "Amount", 100L);
            double[] remainders = ProductionRemainders(service);
            remainders[0] = double.NaN;
            WalletSnapshot beforeMalformed = SnapshotWallet(save);
            object malformed = Invoke(service, "TryTickProduction", 1d);
            AssertStatus(malformed, "RejectedMalformedState");
            Assert.AreEqual("AL-ECO-PRODUCTION-INVALID-REMAINDER", GetProperty(malformed, "DiagnosticCode"));
            Assert.True(double.IsNaN(remainders[0]), "Malformed remainder must not be silently reset.");
            AssertWalletUnchanged(save, beforeMalformed);
            Assert.IsEmpty(events);
        }

        [Test]
        public void ProductionRemaindersAreServiceLifetimeOnly()
        {
            object save = CreateValidSave();
            var saveFixture = CreateSaveFixture(save);
            var provider = CreateProductionProvider(CreateProductionSnapshot(
                "Available",
                "source-v1",
                new ContributionSpec("Food", 0.75d)));
            object firstService = CreateResourceService(saveFixture, () => true, provider.Proxy);

            AssertStatus(Invoke(firstService, "TryTickProduction", 1d), "Applied");
            Assert.AreEqual(100L, ResourceCount(firstService, "Food"));
            Assert.That(Remainder(firstService, "Food"), Is.EqualTo(0.75d).Within(1e-12));

            object secondService = CreateResourceService(saveFixture, () => true, provider.Proxy);
            Assert.That(Remainder(secondService, "Food"), Is.EqualTo(0d).Within(1e-12));
            AssertStatus(Invoke(secondService, "TryTickProduction", 1d), "Applied");
            Assert.AreEqual(100L, ResourceCount(secondService, "Food"));
            Assert.That(Remainder(secondService, "Food"), Is.EqualTo(0.75d).Within(1e-12));
        }

        [Test]
        public void ProductionProfileIdentityPreventsCrossProfileRemainderMinting()
        {
            object firstSave = CreateValidSave();
            var saveFixture = CreateSaveFixture(firstSave);
            var provider = CreateProductionProvider(CreateProductionSnapshotForProfile(
                "Available",
                "profile-a",
                "source-v1",
                new ContributionSpec("Food", 0.75d)));
            object service = CreateResourceService(saveFixture, () => true, provider.Proxy);

            AssertStatus(Invoke(service, "TryTickProduction", 1d), "Applied");
            Assert.That(Remainder(service, "Food"), Is.EqualTo(0.75d).Within(1e-12));

            object secondSave = CreateValidSave();
            saveFixture.State.CurrentSave = secondSave;
            provider.State.Snapshot = CreateProductionSnapshotForProfile(
                "Available",
                "profile-b",
                "source-v1",
                new ContributionSpec("Food", 0.5d));
            AssertStatus(Invoke(service, "TryTickProduction", 1d), "Applied");
            Assert.AreEqual(100L, ResourceCount(service, "Food"));
            Assert.That(Remainder(service, "Food"), Is.EqualTo(0.5d).Within(1e-12));

            AssertStatus(Invoke(service, "TryTickProduction", 1d), "Applied");
            Assert.AreEqual(101L, ResourceCount(service, "Food"));
            Assert.That(Remainder(service, "Food"), Is.EqualTo(0d).Within(1e-12));
        }

        [Test]
        public void ProductionProfileIdentitySurvivesSaveObjectReplacement()
        {
            object firstSave = CreateValidSave();
            var saveFixture = CreateSaveFixture(firstSave);
            var provider = CreateProductionProvider(CreateProductionSnapshotForProfile(
                "Available",
                "profile-stable",
                "source-v1",
                new ContributionSpec("Food", 0.75d)));
            object service = CreateResourceService(saveFixture, () => true, provider.Proxy);
            AssertStatus(Invoke(service, "TryTickProduction", 1d), "Applied");

            object persistedClone = CreateValidSave();
            saveFixture.State.CurrentSave = persistedClone;
            provider.State.Snapshot = CreateProductionSnapshotForProfile(
                "Available",
                "profile-stable",
                "source-v2",
                new ContributionSpec("Food", 0.25d));
            AssertStatus(Invoke(service, "TryTickProduction", 1d), "Applied");
            Assert.AreEqual(101L, ResourceCount(service, "Food"));
            Assert.That(Remainder(service, "Food"), Is.EqualTo(0d).Within(1e-12));
        }

        [Test]
        public void RejectedProfileSwitchPreservesOldRemainderUntilSuccessfulBind()
        {
            object save = CreateValidSave();
            var saveFixture = CreateSaveFixture(save);
            var provider = CreateProductionProvider(CreateProductionSnapshotForProfile(
                "Available",
                "profile-a",
                "source-v1",
                new ContributionSpec("Food", 0.75d)));
            object service = CreateResourceService(saveFixture, () => true, provider.Proxy);

            AssertStatus(Invoke(service, "TryTickProduction", 1d), "Applied");
            Assert.That(Remainder(service, "Food"), Is.EqualTo(0.75d).Within(1e-12));
            Assert.AreEqual("profile-a", GetField(service, "_productionProfileIdentity"));

            provider.State.Snapshot = CreateProductionSnapshotForProfile(
                "Available",
                "profile-b",
                "source-v2",
                new ContributionSpec(Enum.ToObject(GetRuntimeType("AL.Core.ResourceType"), 9999), 1d));
            AssertStatus(Invoke(service, "TryTickProduction", 1d), "RejectedDependencyUnavailable");
            Assert.That(Remainder(service, "Food"), Is.EqualTo(0.75d).Within(1e-12));
            Assert.AreEqual("profile-a", GetField(service, "_productionProfileIdentity"));

            provider.State.Snapshot = CreateProductionSnapshotForProfile(
                "Available",
                "profile-b",
                "source-v3");
            AssertStatus(Invoke(service, "TryTickProduction", 1d), "NoChange");
            Assert.That(Remainder(service, "Food"), Is.EqualTo(0d).Within(1e-12));
            Assert.AreEqual("profile-b", GetField(service, "_productionProfileIdentity"));
            Assert.AreEqual(0, saveFixture.State.SaveCount);
        }

        [Test]
        public void ProductionOptionalInsertionCannotExceedWalletRecordBound()
        {
            object save = CreateValidSave(includeOptional: false);
            IList wallet = Resources(save);
            for (int index = wallet.Count; index < 256; index++)
            {
                wallet.Add(CreateResourceData(Enum.ToObject(GetRuntimeType("AL.Core.ResourceType"), 2000 + index), index));
            }

            var saveFixture = CreateSaveFixture(save);
            var provider = CreateProductionProvider(CreateProductionSnapshot(
                "Available",
                "source-v1",
                new ContributionSpec("DeepOre", 1d)));
            object service = CreateResourceService(saveFixture, () => true, provider.Proxy);
            WalletSnapshot before = SnapshotWallet(save);
            double[] remaindersBefore = ProductionRemainders(service).ToArray();

            object result = Invoke(service, "TryTickProduction", 1d);
            AssertStatus(result, "RejectedMalformedState");
            Assert.AreEqual("AL-ECO-MALFORMED-WALLET", GetProperty(result, "DiagnosticCode"));
            AssertWalletUnchanged(save, before);
            CollectionAssert.AreEqual(remaindersBefore, ProductionRemainders(service));
        }

        [Test]
        public void ProductionSnapshotFreezesInputsAndEnforcesBounds()
        {
            Type contributionType = GetRuntimeType("AL.Core.Interfaces.EconomyProductionContribution");
            Type diagnosticType = GetRuntimeType("AL.Core.Interfaces.EconomyDiagnostic");
            IList contributions = CreateRuntimeList(contributionType);
            IList diagnostics = CreateRuntimeList(diagnosticType);
            contributions.Add(CreateContribution(new ContributionSpec("Food", 1d)));
            diagnostics.Add(CreateDiagnostic("AL-ECO-PRODUCTION-DEPENDENCY", "source"));

            object snapshot = CreateProductionSnapshot("Available", "profile-a", "source-v1", contributions, diagnostics);
            contributions.Add(CreateContribution(new ContributionSpec("Gold", 1d)));
            diagnostics.Add(CreateDiagnostic("AL-ECO-OVERFLOW", "later"));
            Assert.AreEqual(1, ((IEnumerable)GetProperty(snapshot, "Contributions")).Cast<object>().Count());
            Assert.AreEqual(1, ((IEnumerable)GetProperty(snapshot, "Diagnostics")).Cast<object>().Count());

            IList wrappedContributions = (IList)contributions.GetType().GetMethod("AsReadOnly").Invoke(contributions, null);
            IList wrappedDiagnostics = (IList)diagnostics.GetType().GetMethod("AsReadOnly").Invoke(diagnostics, null);
            object wrappedSnapshot = CreateProductionSnapshot(
                "Available",
                "profile-a",
                "source-v2",
                wrappedContributions,
                wrappedDiagnostics);
            contributions.Add(CreateContribution(new ContributionSpec("Stone", 1d)));
            diagnostics.Add(CreateDiagnostic("AL-ECO-INVALID-AMOUNT", "after-wrap"));
            Assert.AreEqual(2, ((IEnumerable)GetProperty(wrappedSnapshot, "Contributions")).Cast<object>().Count());
            Assert.AreEqual(2, ((IEnumerable)GetProperty(wrappedSnapshot, "Diagnostics")).Cast<object>().Count());

            IList tooMany = CreateRuntimeList(contributionType);
            for (int index = 0; index < 257; index++)
            {
                tooMany.Add(CreateContribution(new ContributionSpec("Food", 0d)));
            }

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                CreateProductionSnapshot("Available", "profile-a", "source-v1", tooMany, CreateRuntimeList(diagnosticType)));
            Assert.IsInstanceOf<ArgumentException>(exception.InnerException);

            IList exactDiagnostics = CreateRuntimeList(diagnosticType);
            for (int index = 0; index < 32; index++)
            {
                exactDiagnostics.Add(CreateDiagnostic($"AL-ECO-EXACT-{index}", $"diagnostics[{index}]"));
            }

            object exactSnapshot = CreateProductionSnapshot(
                "Available",
                "profile-a",
                "source-v3",
                CreateRuntimeList(contributionType),
                exactDiagnostics);
            object[] exactFrozen = ((IEnumerable)GetProperty(exactSnapshot, "Diagnostics")).Cast<object>().ToArray();
            Assert.AreEqual(32, exactFrozen.Length);
            Assert.AreEqual("AL-ECO-EXACT-31", GetProperty(exactFrozen[31], "Code"));

            exactDiagnostics.Add(CreateDiagnostic("AL-ECO-OVER-LIMIT", "diagnostics[32]"));
            object truncatedSnapshot = CreateProductionSnapshot(
                "Available",
                "profile-a",
                "source-v4",
                CreateRuntimeList(contributionType),
                exactDiagnostics);
            object[] truncated = ((IEnumerable)GetProperty(truncatedSnapshot, "Diagnostics")).Cast<object>().ToArray();
            Assert.AreEqual(32, truncated.Length);
            Assert.AreEqual("AL-ECO-DIAGNOSTICS-TRUNCATED", GetProperty(truncated[31], "Code"));

            object boundedDiagnostic = CreateDiagnostic(new string('C', 200), new string('P', 400));
            Assert.AreEqual(96, GetProperty(boundedDiagnostic, "Code").ToString().Length);
            Assert.AreEqual(256, GetProperty(boundedDiagnostic, "RecordPath").ToString().Length);
        }

        [Test]
        public void TestOnlyAuthorityMutationCannotCrossManualSaveContainment()
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateActualSaveService(root);
                Invoke(saveService, "CreateNewSave", EnumValue(GetRuntimeType("AL.Core.RealmId"), "Crownlands"));
                object currentSave = GetProperty(saveService, "CurrentSave");
                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                byte[] primaryBefore = File.ReadAllBytes(primaryPath);
                byte[] backupBefore = File.ReadAllBytes(backupPath);
                SetField(currentSave, "WarzoneCredits", 10);
                LogAssert.Expect(
                    LogType.Log,
                    new Regex("^AL-SAVE-MANUAL-WRITE-CONTAINED:"));
                Invoke(saveService, "Save");

                object creditService =
                    CreateCreditServiceForSaveServiceForTests(saveService);
                AssertStatus(
                    Invoke(creditService, "TryAddCredits", 5),
                    "Applied");

                LogAssert.Expect(
                    LogType.Log,
                    new Regex("^AL-SAVE-MANUAL-WRITE-CONTAINED:"));
                Invoke(saveService, "Save");
                Assert.AreEqual(15, GetField(currentSave, "WarzoneCredits"));
                CollectionAssert.AreEqual(primaryBefore, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(backupPath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacyCallerInventoryMatchesCurrentSource()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "AL", "Scripts");
            string unityRoot = Directory.GetParent(Application.dataPath).FullName;
            string inventoryPath = Path.Combine(unityRoot, "Docs", "Economy_Legacy_Caller_Inventory.md");
            Assert.True(File.Exists(inventoryPath));
            string inventory = File.ReadAllText(inventoryPath);

            string[] creditCallers = FindCallers(scriptsRoot, ".AddCredits(", ".SpendCredits(");
            string[] expectedCreditCallers =
            {
                "Kingdom/Quests/LocalQuestService.cs",
                "Utilities/DemoInitializer.cs"
            };
            CollectionAssert.AreEqual(expectedCreditCallers, creditCallers);

            string[] resourceCallers = FindCallers(scriptsRoot, ".AddResource(", ".ConsumeResource(");
            string[] expectedResourceCallers =
            {
                "Kingdom/Quests/LocalQuestService.cs",
                "Kingdom/Research/LocalResearchService.cs",
                "Services/Local/LocalTrainingService.cs",
                "Utilities/DemoInitializer.cs"
            };
            CollectionAssert.AreEqual(expectedResourceCallers, resourceCallers);

            foreach (string caller in expectedCreditCallers.Concat(expectedResourceCallers).Distinct())
            {
                StringAssert.Contains(caller, inventory.Replace('\\', '/'));
            }
        }

        [Test]
        public void BossLootOwnedEquipmentViewIsDetachedFromPersistedState()
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateActualSaveService(root);
                Invoke(saveService, "CreateNewSave", EnumValue(GetRuntimeType("AL.Core.RealmId"), "Crownlands"));
                object save = GetProperty(saveService, "CurrentSave");
                IList inventory = (IList)GetField(save, "OwnedEquipment");
                object persisted = CreateOwnedEquipment("equipment_snapshot", 2);
                inventory.Add(persisted);

                object bossLoot = CreateBossLootService(saveService);
                IEnumerable snapshot = (IEnumerable)Invoke(bossLoot, "GetOwnedEquipment");
                object detached = snapshot.Cast<object>().Single();
                Assert.AreNotSame(persisted, detached);

                SetField(detached, "Quantity", 999);
                Assert.AreEqual(2, GetField(persisted, "Quantity"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void BossLootRejectsAmbiguousOrOverflowingPersistedInventory()
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateActualSaveService(root);
                Invoke(saveService, "CreateNewSave", EnumValue(GetRuntimeType("AL.Core.RealmId"), "Crownlands"));
                object save = GetProperty(saveService, "CurrentSave");
                IList inventory = (IList)GetField(save, "OwnedEquipment");
                object first = CreateOwnedEquipment("equipment_duplicate", 1);
                object duplicate = CreateOwnedEquipment("equipment_duplicate", 3);
                inventory.Add(first);
                inventory.Add(duplicate);

                object bossLoot = CreateBossLootService(saveService);
                object drop = CreateBossLootDrop("equipment_duplicate", 1);
                LogAssert.Expect(LogType.Error, "AL-EQUIPMENT-INVENTORY-MALFORMED: Owned equipment mutation was rejected without changing persisted state.");
                Assert.False(InvokeOwnedEquipmentMutation(save, drop, "boss_a"));
                Assert.AreEqual(1, GetField(first, "Quantity"));
                Assert.AreEqual(3, GetField(duplicate, "Quantity"));

                inventory.Clear();
                object maximum = CreateOwnedEquipment("equipment_maximum", int.MaxValue);
                inventory.Add(maximum);
                object overflow = CreateBossLootDrop("equipment_maximum", 1);
                LogAssert.Expect(LogType.Error, "AL-EQUIPMENT-QUANTITY-OVERFLOW: Owned equipment mutation was rejected without changing persisted state.");
                Assert.False(InvokeOwnedEquipmentMutation(save, overflow, "boss_a"));
                Assert.AreEqual(int.MaxValue, GetField(maximum, "Quantity"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void BossLootRejectsPersistedEquipmentDefinitionDriftWithoutMutation()
        {
            string root = CreateTempRoot();
            try
            {
                object saveService = CreateActualSaveService(root);
                Invoke(saveService, "CreateNewSave", EnumValue(GetRuntimeType("AL.Core.RealmId"), "Crownlands"));
                object save = GetProperty(saveService, "CurrentSave");
                IList inventory = (IList)GetField(save, "OwnedEquipment");
                object persisted = CreateOwnedEquipment("equipment_drift", 2);
                SetField(persisted, "DisplayName", "Persisted Blade");
                SetField(persisted, "Slot", EnumValue(GetRuntimeType("AL.Core.EquipmentSlot"), "MainHand"));
                SetField(persisted, "AttackBonus", 5);
                SetField(persisted, "DefenseBonus", 1);
                SetField(persisted, "HealthBonus", 3);
                SetField(persisted, "AnnounceWorldDrop", true);
                inventory.Add(persisted);

                object bossLoot = CreateBossLootService(saveService);
                object driftedDrop = CreateBossLootDrop("equipment_drift", 1);
                SetField(driftedDrop, "DisplayName", "Drifted Blade");
                SetField(driftedDrop, "Slot", EnumValue(GetRuntimeType("AL.Core.EquipmentSlot"), "MainHand"));
                SetField(driftedDrop, "AttackBonus", 6);
                SetField(driftedDrop, "DefenseBonus", 1);
                SetField(driftedDrop, "HealthBonus", 3);
                SetField(driftedDrop, "AnnounceWorldDrop", true);

                LogAssert.Expect(
                    LogType.Error,
                    "AL-EQUIPMENT-DEFINITION-DRIFT: Owned equipment mutation was rejected because the persisted definition does not match the awarded definition.");
                Assert.False(InvokeOwnedEquipmentMutation(save, driftedDrop, "boss_new"));
                Assert.AreEqual(2, GetField(persisted, "Quantity"));
                Assert.AreEqual(1L, GetField(persisted, "LastAcquiredTimestamp"));
                Assert.IsNull(GetField(persisted, "SourceBossId"));

                SetField(driftedDrop, "DisplayName", "Persisted Blade");
                SetField(driftedDrop, "AttackBonus", 5);
                Assert.True(InvokeOwnedEquipmentMutation(save, driftedDrop, "boss_new"));
                Assert.AreEqual(3, GetField(persisted, "Quantity"));
                Assert.AreEqual("boss_new", GetField(persisted, "SourceBossId"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestCase(null, "result_a", "boss_a", "InvalidEncounterId", "AL-BOSS-LOOT-ENCOUNTER-ID-INVALID")]
        [TestCase(" ", "result_a", "boss_a", "InvalidEncounterId", "AL-BOSS-LOOT-ENCOUNTER-ID-INVALID")]
        [TestCase("encounter_a", null, "boss_a", "InvalidRewardResultId", "AL-BOSS-LOOT-RESULT-ID-INVALID")]
        [TestCase("encounter_a", "result_a", "", "InvalidBossId", "AL-BOSS-LOOT-BOSS-ID-INVALID")]
        [TestCase("encounter_a", "result_a", "boss_a", "Valid", "")]
        public void BossLootApplicationIdentityRequiresStableNonblankIds(
            string encounterId,
            string rewardResultId,
            string bossId,
            string expectedStatus,
            string expectedDiagnostic)
        {
            Type identityType = GetRuntimeType("AL.Core.Interfaces.BossLootApplicationIdentity");
            object identity = Activator.CreateInstance(identityType);
            SetField(identity, "EncounterId", encounterId);
            SetField(identity, "RewardResultId", rewardResultId);
            SetField(identity, "BossId", bossId);

            Type validatorType = GetRuntimeType("AL.Core.Interfaces.BossLootApplicationIdentityValidator");
            MethodInfo validate = validatorType.GetMethod(
                "Validate",
                BindingFlags.Static | BindingFlags.Public);
            Assert.NotNull(validate);
            object result = validate.Invoke(null, new[] { identity });

            Assert.AreEqual(expectedStatus, GetProperty(result, "Status").ToString());
            Assert.AreEqual(expectedDiagnostic, GetProperty(result, "DiagnosticCode"));
            Assert.AreEqual(expectedStatus == "Valid", GetProperty(result, "IsValid"));
        }

        [Test]
        public void BossLootApplicationAndReplayAreContainedBeforeMutation()
        {
            object save = CreateValidSave();
            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SavedPrimary";
            object credits = CreateCreditService(fixture);
            var notifications = CreateNotificationFixture();
            object service = CreateBossLootService(fixture.Proxy, credits, notifications.Proxy);
            object request = CreateBossLootRequest("encounter_a", "result_a", "boss_a", 25);

            object first = Invoke(service, "RollLoot", request);
            object replay = Invoke(service, "RollLoot", request);

            AssertBossLootContained(first);
            AssertBossLootContained(replay);
            Assert.AreEqual(0, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, ((IList)GetField(save, "AppliedBossLootRewards")).Count);
            Assert.AreEqual(0, fixture.State.SaveCount);
            Assert.IsEmpty(notifications.State.Messages);
        }

        [TestCase("SaveFailedPreviousPreserved")]
        [TestCase("CommitUncertain")]
        public void BossLootContainmentPrecedesDownstreamSaveStatus(
            string saveStatus)
        {
            object save = CreateValidSave();
            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SavedPrimary";
            fixture.State.SaveStatusAfterSave = saveStatus;
            object credits = CreateCreditService(fixture);
            var notifications = CreateNotificationFixture();
            object service = CreateBossLootService(fixture.Proxy, credits, notifications.Proxy);
            object request = CreateBossLootRequest("encounter_b", "result_b", "boss_b", 10);

            object result = Invoke(service, "RollLoot", request);

            AssertBossLootContained(result);
            Assert.AreEqual(0, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, ((IList)GetField(save, "AppliedBossLootRewards")).Count);
            Assert.AreEqual(0, fixture.State.SaveCount);
            Assert.IsEmpty(notifications.State.Messages);
        }

        [Test]
        public void BossLootCommitUncertainRetryNeverUpgradesPendingLedgerToCommitted()
        {
            object save = CreateValidSave();
            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SavedPrimary";
            fixture.State.SaveStatusAfterSave = "CommitUncertain";
            object credits = CreateCreditService(fixture);
            var notifications = CreateNotificationFixture();
            object service = CreateBossLootService(fixture.Proxy, credits, notifications.Proxy);
            object request = CreateBossLootRequest(
                "encounter_uncertain",
                "result_uncertain",
                "boss_uncertain",
                10);

            object first = Invoke(service, "RollLoot", request);
            object retry = Invoke(service, "RollLoot", request);

            AssertBossLootContained(first);
            AssertBossLootContained(retry);
            Assert.AreEqual(0, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, ((IList)GetField(save, "AppliedBossLootRewards")).Count);
            Assert.AreEqual(0, fixture.State.SaveCount);
            Assert.IsEmpty(notifications.State.Messages);
        }

        [Test]
        public void BossLootPrePersistenceExceptionRestoresExactPriorState()
        {
            object save = CreateValidSave();
            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SavedPrimary";
            var notifications = CreateNotificationFixture();
            object service = CreateBossLootService(
                fixture.Proxy,
                CreateThrowingCreditService(),
                notifications.Proxy);

            object result = Invoke(
                service,
                "RollLoot",
                CreateBossLootRequest(
                    "encounter_throw",
                    "result_throw",
                    "boss_throw",
                    10));

            AssertBossLootContained(result);
            Assert.AreEqual(0, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, ((IList)GetField(save, "OwnedEquipment")).Count);
            Assert.AreEqual(0, ((IList)GetField(save, "AppliedBossLootRewards")).Count);
            Assert.AreEqual(0, fixture.State.SaveCount);
            Assert.IsEmpty(notifications.State.Messages);
        }

        [Test]
        public void BossLootApplicationsSharingSaveServiceAreSerialized()
        {
            object save = CreateValidSave();
            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SavedPrimary";
            fixture.State.CurrentSaveDelayMilliseconds = 40;
            object credits = CreateCreditService(fixture);
            var firstNotifications = CreateNotificationFixture();
            var secondNotifications = CreateNotificationFixture();
            object firstService = CreateBossLootService(
                fixture.Proxy,
                credits,
                firstNotifications.Proxy);
            object secondService = CreateBossLootService(
                fixture.Proxy,
                credits,
                secondNotifications.Proxy);
            object request = CreateBossLootRequest(
                "encounter_parallel",
                "result_parallel",
                "boss_parallel",
                10);
            using (var start = new ManualResetEventSlim(false))
            {
                Task<object> first = Task.Run(() =>
                {
                    start.Wait();
                    return Invoke(firstService, "RollLoot", request);
                });
                Task<object> second = Task.Run(() =>
                {
                    start.Wait();
                    return Invoke(secondService, "RollLoot", request);
                });

                start.Set();
                Assert.True(
                    Task.WaitAll(new Task[] { first, second }, TimeSpan.FromSeconds(10)),
                    "Concurrent boss-loot applications did not complete.");

                AssertBossLootContained(first.Result);
                AssertBossLootContained(second.Result);
            }

            Assert.AreEqual(0, fixture.State.MaximumConcurrentCurrentSaveReads);
            Assert.AreEqual(0, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, ((IList)GetField(save, "AppliedBossLootRewards")).Count);
            Assert.AreEqual(0, fixture.State.SaveCount);
            Assert.IsEmpty(firstNotifications.State.Messages);
            Assert.IsEmpty(secondNotifications.State.Messages);
        }

        [Test]
        public void BossLootRejectsConflictingEncounterWithoutMutation()
        {
            object save = CreateValidSave();
            IList ledger = (IList)GetField(save, "AppliedBossLootRewards");
            object applied = Activator.CreateInstance(GetRuntimeType("AL.Data.Runtime.AppliedBossLootRewardState"));
            SetField(applied, "EncounterId", "encounter_c");
            SetField(applied, "RewardResultId", "prior_result");
            SetField(applied, "BossId", "boss_c");
            SetField(applied, "RewardDigest", "sha256:prior");
            SetField(applied, "CommittedTimestamp", 1L);
            ledger.Add(applied);
            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SavedPrimary";
            object credits = CreateCreditService(fixture);
            var notifications = CreateNotificationFixture();
            object service = CreateBossLootService(fixture.Proxy, credits, notifications.Proxy);

            object result = Invoke(
                service,
                "RollLoot",
                CreateBossLootRequest("encounter_c", "new_result", "boss_c", 5));

            AssertBossLootContained(result);
            Assert.AreEqual(0, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(1, ledger.Count);
            Assert.AreEqual(0, fixture.State.SaveCount);
            Assert.IsEmpty(notifications.State.Messages);
        }

        [Test]
        public void WarmasterStateSnapshotIsDetachedFromPersistedState()
        {
            object save = CreateValidSave();
            object warmaster = GetField(save, "Warmaster");
            ((IList)GetField(warmaster, "UnlockedSetIds")).Add("prototype_true_warmaster");
            ((IList)GetField(warmaster, "PurchasedPieceIds")).Add("warmaster_piece_01");
            SetField(warmaster, "Level", 1);
            SetField(warmaster, "Experience", 25);
            var fixture = CreateSaveFixture(save);
            object service = CreateWarmasterService(fixture);

            object snapshot = Invoke(service, "GetState");
            ((IList)GetField(snapshot, "UnlockedSetIds")).Clear();
            ((IList)GetField(snapshot, "PurchasedPieceIds")).Add("warmaster_piece_02");
            SetField(snapshot, "Experience", 999);

            Assert.AreEqual(1, ((IList)GetField(warmaster, "UnlockedSetIds")).Count);
            Assert.AreEqual(1, ((IList)GetField(warmaster, "PurchasedPieceIds")).Count);
            Assert.AreEqual(25, GetField(warmaster, "Experience"));
            Assert.AreEqual(0, fixture.State.SaveCount);
        }

        [TestCase(null, 10)]
        [TestCase("", 10)]
        [TestCase("unknown_piece", 10)]
        [TestCase("warmaster_piece_01", 0)]
        [TestCase("warmaster_piece_01", -10)]
        public void WarmasterPurchaseRejectsInvalidIdentityOrPrice(string pieceId, int cost)
        {
            object save = CreateValidSave();
            SetField(save, "WarzoneCredits", 100);
            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SavedPrimary";
            object service = CreateWarmasterService(fixture);

            Assert.False((bool)Invoke(service, "PurchasePiece", pieceId, cost));

            object warmaster = GetField(save, "Warmaster");
            Assert.AreEqual(100, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, ((IList)GetField(warmaster, "PurchasedPieceIds")).Count);
            Assert.AreEqual(0, GetField(warmaster, "Experience"));
            Assert.AreEqual(0, fixture.State.SaveCount);
        }

        [Test]
        public void WarmasterPurchaseAndDuplicateRemainContained()
        {
            object save = CreateValidSave();
            SetField(save, "WarzoneCredits", 100);
            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SavedPrimary";
            object service = CreateWarmasterService(fixture);

            Assert.False((bool)Invoke(service, "PurchasePiece", "warmaster_piece_01", 10));
            Assert.False((bool)Invoke(service, "PurchasePiece", "warmaster_piece_01", 10));

            object warmaster = GetField(save, "Warmaster");
            Assert.AreEqual(100, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, ((IList)GetField(warmaster, "PurchasedPieceIds")).Count);
            Assert.AreEqual(0, GetField(warmaster, "Level"));
            Assert.AreEqual(0, GetField(warmaster, "Experience"));
            Assert.AreEqual(0, fixture.State.SaveCount);
        }

        [Test]
        public void WarmasterPurchaseRollsBackCreditAndStateWhenSaveFails()
        {
            object save = CreateValidSave();
            SetField(save, "WarzoneCredits", 100);
            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SaveFailedPreviousPreserved";
            object service = CreateWarmasterService(fixture);

            Assert.False((bool)Invoke(service, "PurchasePiece", "warmaster_piece_01", 10));

            object warmaster = GetField(save, "Warmaster");
            Assert.AreEqual(100, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, ((IList)GetField(warmaster, "PurchasedPieceIds")).Count);
            Assert.AreEqual(0, GetField(warmaster, "Level"));
            Assert.AreEqual(0, GetField(warmaster, "Experience"));
            Assert.False((bool)GetField(warmaster, "IsTrueWarmaster"));
            Assert.AreEqual(0, fixture.State.SaveCount);
        }

        [Test]
        public void WarmasterThresholdCannotBeReachedWhileWritesAreContained()
        {
            object save = CreateValidSave();
            SetField(save, "WarzoneCredits", 1000);
            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SavedPrimary";
            object service = CreateWarmasterService(fixture);

            for (int index = 1; index <= 10; index++)
            {
                Assert.False((bool)Invoke(service, "PurchasePiece", $"warmaster_piece_{index:00}", 10));
            }

            object warmaster = GetField(save, "Warmaster");
            Assert.AreEqual(1000, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, Invoke(service, "GetPurchasedPieceCount"));
            Assert.False((bool)Invoke(service, "IsTrueWarmaster"));
            Assert.False((bool)GetField(warmaster, "IsTrueWarmaster"));
            Assert.IsEmpty((IList)GetField(warmaster, "UnlockedSetIds"));
            Assert.IsNull(GetField(warmaster, "EquippedSetId"));
            Assert.AreEqual(0, fixture.State.SaveCount);
        }

        [Test]
        public void WarmasterMalformedStateCannotGrantThresholdOrMutate()
        {
            object save = CreateValidSave();
            SetField(save, "WarzoneCredits", 100);
            object warmaster = GetField(save, "Warmaster");
            IList purchased = (IList)GetField(warmaster, "PurchasedPieceIds");
            for (int index = 0; index < 10; index++)
            {
                purchased.Add("warmaster_piece_01");
            }

            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SavedPrimary";
            object service = CreateWarmasterService(fixture);

            Assert.False((bool)Invoke(service, "IsTrueWarmaster"));
            Assert.AreEqual(0, Invoke(service, "GetPurchasedPieceCount"));
            Assert.False((bool)Invoke(service, "PurchasePiece", "warmaster_piece_02", 10));
            Assert.AreEqual(100, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(10, purchased.Count);
            Assert.AreEqual(0, fixture.State.SaveCount);
        }

        [Test]
        public void BossLootContainmentPersistsAcrossReloadWithoutRewardOrNotification()
        {
            string root = CreateTempRoot();
            try
            {
                object firstSaveService = CreateActualSaveService(root);
                Invoke(
                    firstSaveService,
                    "CreateNewSave",
                    EnumValue(GetRuntimeType("AL.Core.RealmId"), "Crownlands"));
                object firstCredits =
                    CreateCreditServiceForSaveServiceForTests(
                        firstSaveService);
                var firstNotifications = CreateNotificationFixture();
                object firstBossLoot = CreateBossLootService(
                    firstSaveService,
                    firstCredits,
                    firstNotifications.Proxy);
                object request = CreateBossLootRequest(
                    "encounter_reload",
                    "result_reload",
                    "boss_reload",
                    25);

                AssertBossLootContained(Invoke(firstBossLoot, "RollLoot", request));

                object reloadedSaveService = CreateActualSaveService(root);
                Invoke(reloadedSaveService, "Load");
                object reloadedCredits =
                    CreateCreditServiceForSaveServiceForTests(
                        reloadedSaveService);
                var replayNotifications = CreateNotificationFixture();
                object reloadedBossLoot = CreateBossLootService(
                    reloadedSaveService,
                    reloadedCredits,
                    replayNotifications.Proxy);

                AssertBossLootContained(
                    Invoke(reloadedBossLoot, "RollLoot", request));
                object reloadedSave = GetProperty(reloadedSaveService, "CurrentSave");
                Assert.AreEqual(0, GetField(reloadedSave, "WarzoneCredits"));
                Assert.AreEqual(0, ((IList)GetField(reloadedSave, "AppliedBossLootRewards")).Count);
                Assert.IsEmpty(replayNotifications.State.Messages);
                Assert.AreEqual(0, firstNotifications.State.ShowMessageCount);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void BossLootInvalidRequestAndValidNoLootRemainDistinctWhileContained()
        {
            object save = CreateValidSave();
            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SavedPrimary";
            object credits = CreateCreditService(fixture);
            var notifications = CreateNotificationFixture();
            object service = CreateBossLootService(fixture.Proxy, credits, notifications.Proxy);

            AssertApplicationStatus(Invoke(service, "RollLoot", new object[] { null }), "RejectedInvalidRequest");
            Assert.AreEqual(0, fixture.State.SaveCount);
            Assert.AreEqual(0, ((IList)GetField(save, "AppliedBossLootRewards")).Count);

            object noLoot = CreateBossLootRequest(
                "encounter_no_loot",
                "result_no_loot",
                "boss_no_loot",
                0);
            AssertBossLootContained(Invoke(service, "RollLoot", noLoot));
            Assert.AreEqual(0, fixture.State.SaveCount);
            Assert.AreEqual(0, ((IList)GetField(save, "AppliedBossLootRewards")).Count);
            Assert.IsEmpty(notifications.State.Messages);
        }

        [Test]
        public void BossLootContainmentPreventsNotificationBoundaryInvocation()
        {
            object save = CreateValidSave();
            var fixture = CreateSaveFixture(save);
            fixture.State.LastSaveStatus = "SavedPrimary";
            object credits = CreateCreditService(fixture);
            var notifications = CreateNotificationFixture();
            notifications.State.ThrowOnMessage = true;
            object service = CreateBossLootService(fixture.Proxy, credits, notifications.Proxy);

            object result = Invoke(
                service,
                "RollLoot",
                CreateBossLootRequest("encounter_notify", "result_notify", "boss_notify", 5));

            AssertBossLootContained(result);
            Assert.AreEqual(0, GetField(save, "WarzoneCredits"));
            Assert.AreEqual(0, ((IList)GetField(save, "AppliedBossLootRewards")).Count);
            Assert.AreEqual(0, fixture.State.SaveCount);
            Assert.AreEqual(0, notifications.State.ShowMessageCount);
        }

        private static void AssertTryRare(MethodInfo method, object realm, string expectedResource, bool expectedSuccess)
        {
            object[] args = { realm, null };
            Assert.AreEqual(expectedSuccess, method.Invoke(null, args));
            if (expectedSuccess)
            {
                Assert.AreEqual(expectedResource, args[1].ToString());
            }
        }

        private static void AssertDefaultResultIsSafe(string typeName, string collectionProperty)
        {
            object value = Activator.CreateInstance(GetRuntimeType(typeName));
            Assert.NotNull(GetProperty(value, collectionProperty));
            Assert.AreEqual(string.Empty, GetProperty(value, "DiagnosticCode"));
        }

        private static void AssertStatus(object result, string expected)
        {
            Assert.NotNull(result);
            Assert.AreEqual(expected, GetProperty(result, "Status").ToString());
        }

        private static void AssertApplicationStatus(object result, string expected)
        {
            Assert.NotNull(result);
            Assert.AreEqual(expected, GetField(result, "ApplicationStatus").ToString());
        }

        private static void AssertBossLootContained(object result)
        {
            AssertApplicationStatus(result, "RejectedCreditMutation");
            Assert.AreEqual(
                "AL-BOSS-LOOT-PROFILE-NOT-WRITABLE",
                GetField(result, "DiagnosticCode"));
        }

        private static void AssertMutation(
            object result,
            string status,
            long requested,
            long? previous,
            long? current,
            bool changed)
        {
            AssertStatus(result, status);
            Assert.AreEqual(requested, Convert.ToInt64(GetProperty(result, "RequestedAmount")));
            Assert.AreEqual(previous, NullableLong(result, "PreviousBalance"));
            Assert.AreEqual(current, NullableLong(result, "CurrentBalance"));
            Assert.AreEqual(changed, GetProperty(result, "Changed"));
        }

        private static long? NullableLong(object target, string propertyName)
        {
            object value = GetProperty(target, propertyName);
            return value == null ? (long?)null : Convert.ToInt64(value);
        }

        private static string[] DiagnosticCodes(object result)
        {
            return ((IEnumerable)GetProperty(result, "Diagnostics"))
                .Cast<object>()
                .Select(diagnostic => GetProperty(diagnostic, "Code").ToString())
                .ToArray();
        }

        private static string[] BalanceChangeTypes(object tickResult)
        {
            return ((IEnumerable)GetProperty(tickResult, "BalanceChanges"))
                .Cast<object>()
                .Select(change => GetProperty(change, "ResourceType")?.ToString())
                .ToArray();
        }

        private static object CreateValidSave(bool includeOptional = true)
        {
            object save = Activator.CreateInstance(GetRuntimeType("AL.Data.Runtime.SaveGameData"));
            IList resources = CreateRuntimeList(GetRuntimeType("AL.Data.Runtime.ResourceData"));
            foreach (string name in includeOptional ? WalletResourceNames : CoreResourceNames)
            {
                resources.Add(CreateResourceData(name, 100L));
            }

            SetField(save, "Resources", resources);
            SetField(save, "SelectedRealm", EnumValue(GetRuntimeType("AL.Core.RealmId"), "Crownlands"));
            return save;
        }

        private static object CreateResourceData(string resourceTypeName, long amount) =>
            CreateResourceData(Resource(resourceTypeName), amount);

        private static object CreateResourceData(object resourceType, long amount)
        {
            object data = Activator.CreateInstance(GetRuntimeType("AL.Data.Runtime.ResourceData"));
            SetField(data, "Type", resourceType);
            SetField(data, "Amount", amount);
            return data;
        }

        private static object Resource(string name) =>
            EnumValue(GetRuntimeType("AL.Core.ResourceType"), name);

        private static IList Resources(object save) => (IList)GetField(save, "Resources");

        private static object FindResource(object save, string name)
        {
            object result = FindResourceOrDefault(save, name);
            Assert.NotNull(result, $"Expected {name} resource row.");
            return result;
        }

        private static object FindResourceOrDefault(object save, string name)
        {
            IList resources = Resources(save);
            return resources?.Cast<object>()
                .FirstOrDefault(entry => entry != null && GetField(entry, "Type").ToString() == name);
        }

        private static void RemoveResource(object save, string name)
        {
            IList resources = Resources(save);
            object entry = resources.Cast<object>()
                .FirstOrDefault(candidate => candidate != null && GetField(candidate, "Type").ToString() == name);
            if (entry != null)
            {
                resources.Remove(entry);
            }
        }

        private static long ResourceCount(object service, string name) =>
            Convert.ToInt64(Invoke(service, "GetResourceCount", Resource(name)));

        private static WalletSnapshot SnapshotWallet(object save)
        {
            IList wallet = (IList)GetField(save, "Resources");
            if (wallet == null)
            {
                return new WalletSnapshot(null, null);
            }

            var rows = wallet.Cast<object>()
                .Select(row => row == null
                    ? new WalletRow(null, null, null)
                    : new WalletRow(row, GetField(row, "Type"), Convert.ToInt64(GetField(row, "Amount"))))
                .ToArray();
            return new WalletSnapshot(wallet, rows);
        }

        private static void AssertWalletUnchanged(object save, WalletSnapshot expected)
        {
            IList actual = (IList)GetField(save, "Resources");
            if (expected.Wallet == null)
            {
                Assert.Null(actual);
                return;
            }

            Assert.AreSame(expected.Wallet, actual);
            Assert.AreEqual(expected.Rows.Length, actual.Count);
            for (int index = 0; index < expected.Rows.Length; index++)
            {
                WalletRow row = expected.Rows[index];
                Assert.AreSame(row.Reference, actual[index], $"Row reference drift at {index}.");
                if (row.Reference == null)
                {
                    continue;
                }

                Assert.AreEqual(row.Type, GetField(actual[index], "Type"), $"Type drift at {index}.");
                Assert.AreEqual(row.Amount, Convert.ToInt64(GetField(actual[index], "Amount")), $"Amount drift at {index}.");
            }
        }

        private static SaveFixture CreateSaveFixture(object currentSave)
        {
            Type interfaceType = GetRuntimeType("AL.Core.Interfaces.ISaveGameService");
            object proxy = CreateDispatchProxy(interfaceType, typeof(ScriptedSaveServiceProxy));
            var state = new ScriptedSaveService { CurrentSave = currentSave };
            ((ScriptedSaveServiceProxy)proxy).State = state;
            return new SaveFixture(proxy, state);
        }

        private static object CreateResourceService(
            SaveFixture save,
            Func<bool> writable = null,
            object productionProvider = null)
        {
            AuthorityProviderFixture authority =
                CreateBooleanAuthorityProvider(
                    writable ?? new Func<bool>(() => true));
            return CreateResourceServiceWithAuthority(
                save,
                authority.Proxy,
                productionProvider);
        }

        private static object CreateResourceServiceWithAuthority(
            SaveFixture save,
            object authorityProvider,
            object productionProvider = null)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalResourceService");
            Type saveType = GetRuntimeType("AL.Core.Interfaces.ISaveGameService");
            Type gateType = GetRuntimeType(
                "AL.Services.Local.EconomyWriteAuthorityGate");
            Type productionType = GetRuntimeType(
                "AL.Core.Interfaces.IEconomyProductionContributionProvider");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { saveType, gateType, productionType },
                null);
            Assert.NotNull(constructor);
            return constructor.Invoke(
                new[]
                {
                    save.Proxy,
                    CreateWriteAuthorityGate(save.Proxy, authorityProvider),
                    productionProvider
                });
        }

        private static object CreateCreditService(SaveFixture save, Func<bool> writable = null)
        {
            AuthorityProviderFixture authority =
                CreateBooleanAuthorityProvider(
                    writable ?? new Func<bool>(() => true));
            return CreateCreditServiceWithAuthority(save, authority.Proxy);
        }

        private static object CreateCreditServiceWithAuthority(
            SaveFixture save,
            object authorityProvider)
        {
            Type serviceType = GetRuntimeType(
                "AL.Services.Local.LocalWarzoneCreditService");
            Type saveType = GetRuntimeType("AL.Core.Interfaces.ISaveGameService");
            Type gateType = GetRuntimeType(
                "AL.Services.Local.EconomyWriteAuthorityGate");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { saveType, gateType },
                null);
            Assert.NotNull(constructor);
            return constructor.Invoke(
                new[]
                {
                    save.Proxy,
                    CreateWriteAuthorityGate(save.Proxy, authorityProvider)
                });
        }

        private static object CreateCreditServiceForSaveServiceForTests(
            object saveService)
        {
            Type serviceType = GetRuntimeType(
                "AL.Services.Local.LocalWarzoneCreditService");
            Type saveType = GetRuntimeType("AL.Core.Interfaces.ISaveGameService");
            Type gateType = GetRuntimeType(
                "AL.Services.Local.EconomyWriteAuthorityGate");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { saveType, gateType },
                null);
            Assert.NotNull(constructor);
            AuthorityProviderFixture authority =
                CreateBooleanAuthorityProvider(() => true);
            return constructor.Invoke(
                new[]
                {
                    saveService,
                    CreateWriteAuthorityGate(saveService, authority.Proxy)
                });
        }

        private static object CreateWriteAuthorityGate(
            object saveService,
            object authorityProvider)
        {
            Type gateType = GetRuntimeType(
                "AL.Services.Local.EconomyWriteAuthorityGate");
            Type saveType = GetRuntimeType("AL.Core.Interfaces.ISaveGameService");
            Type authorityType = GetRuntimeType(
                "AL.Core.SaveAuthority.IProfileWriteAuthorityProvider");
            ConstructorInfo constructor = gateType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { saveType, authorityType },
                null);
            Assert.NotNull(constructor);
            return constructor.Invoke(
                new[] { saveService, authorityProvider });
        }

        private static AuthorityProviderFixture
            CreateBooleanAuthorityProvider(Func<bool> writable)
        {
            if (writable == null)
                throw new ArgumentNullException(nameof(writable));

            object writableSnapshot = CreateAuthoritySnapshot("Writable");
            object readOnlySnapshot = CreateAuthoritySnapshot("Unavailable");
            return CreateAuthorityProvider(
                null,
                false,
                () => writable() ? writableSnapshot : readOnlySnapshot);
        }

        private static AuthorityProviderFixture CreateAuthorityProviderForScenario(
            string scenario)
        {
            switch (scenario)
            {
                case "MissingProvider":
                    return null;
                case "NullSnapshot":
                    return CreateAuthorityProvider(null);
                case "ThrowingProvider":
                    return CreateAuthorityProvider(null, true);
                case "InvalidSnapshot":
                    return CreateAuthorityProvider(CreateInvalidAuthoritySnapshot());
                default:
                    return CreateAuthorityProvider(
                        CreateAuthoritySnapshot(scenario));
            }
        }

        private static AuthorityProviderFixture CreateAuthorityProvider(
            object snapshot,
            bool throws = false,
            Func<object> snapshotFactory = null)
        {
            Type interfaceType = GetRuntimeType(
                "AL.Core.SaveAuthority.IProfileWriteAuthorityProvider");
            object proxy = CreateDispatchProxy(
                interfaceType,
                typeof(ScriptedAuthorityProviderProxy));
            var state = new ScriptedAuthorityProvider
            {
                Snapshot = snapshot,
                Throw = throws,
                SnapshotFactory = snapshotFactory
            };
            ((ScriptedAuthorityProviderProxy)proxy).State = state;
            return new AuthorityProviderFixture(proxy, state);
        }

        private static object CreateAuthoritySnapshot(string status)
        {
            Type factoryType = GetRuntimeType(
                "AL.Core.SaveAuthority.ProfileWriteAuthoritySnapshotFactory");
            Type statusType = GetRuntimeType(
                "AL.Core.SaveAuthority.ProfileWriteAuthorityStatus");
            Type sourceType = GetRuntimeType(
                "AL.Core.SaveAuthority.ProfileAuthoritySourceGeneration");
            object primary = EnumValue(sourceType, "Primary");
            var diagnostics = new[] { "AL-SAVE-AUTH-TEST-NONWRITABLE" };

            if (status == "Writable")
            {
                MethodInfo writable = factoryType.GetMethod(
                    "Writable",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.NotNull(writable);
                return writable.Invoke(
                    null,
                    new object[]
                    {
                        "alp_0123456789abcdef0123456789abcdef",
                        "0123456789abcdef0000000000000001",
                        new string('a', 64),
                        primary,
                        Array.Empty<string>()
                    });
            }

            if (status == "MigrationRequired")
            {
                MethodInfo migration = factoryType.GetMethod(
                    "MigrationRequired",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.NotNull(migration);
                return migration.Invoke(
                    null,
                    new object[] { primary, diagnostics });
            }

            if (status == "Unavailable")
            {
                MethodInfo unavailable = factoryType.GetMethod(
                    "Unavailable",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.NotNull(unavailable);
                return unavailable.Invoke(
                    null,
                    new object[] { "AL-SAVE-AUTH-TEST-UNAVAILABLE" });
            }

            int saveSchemaVersion = 0;
            int profileInitializationVersion = 0;
            bool hasSource = false;
            object selectedSource = EnumValue(sourceType, "None");
            switch (status)
            {
                case "ForwardSchemaReadOnly":
                    saveSchemaVersion = 3;
                    profileInitializationVersion = 1;
                    hasSource = true;
                    selectedSource = primary;
                    break;
                case "DegradedReadOnly":
                    saveSchemaVersion = 1;
                    profileInitializationVersion = 1;
                    hasSource = true;
                    selectedSource = primary;
                    break;
            }

            MethodInfo nonWritable = factoryType.GetMethod(
                "NonWritable",
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(nonWritable);
            return nonWritable.Invoke(
                null,
                new[]
                {
                    EnumValue(statusType, status),
                    (object)saveSchemaVersion,
                    profileInitializationVersion,
                    hasSource,
                    selectedSource,
                    diagnostics
                });
        }

        private static object CreateInvalidAuthoritySnapshot()
        {
            Type snapshotType = GetRuntimeType(
                "AL.Core.SaveAuthority.ProfileWriteAuthoritySnapshot");
            Type statusType = GetRuntimeType(
                "AL.Core.SaveAuthority.ProfileWriteAuthorityStatus");
            Type sourceType = GetRuntimeType(
                "AL.Core.SaveAuthority.ProfileAuthoritySourceGeneration");
            ConstructorInfo constructor = snapshotType.GetConstructors(
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Single();
            return constructor.Invoke(
                new object[]
                {
                    "invalid-contract",
                    EnumValue(statusType, "Writable"),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    0,
                    0,
                    false,
                    EnumValue(sourceType, "None"),
                    new[] { "AL-SAVE-AUTH-TEST-INVALID" }
                });
        }

        private static object CreateThrowingCreditService()
        {
            Type interfaceType = GetRuntimeType("AL.Core.Interfaces.IWarzoneCreditIntegrityService");
            return CreateDispatchProxy(interfaceType, typeof(ThrowingCreditServiceProxy));
        }

        private static object CreateWarmasterService(SaveFixture save)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalWarmasterService");
            ConstructorInfo constructor = serviceType.GetConstructor(new[]
            {
                GetRuntimeType("AL.Core.Interfaces.ISaveGameService"),
                GetRuntimeType("AL.Core.Interfaces.IWarzoneCreditService")
            });
            Assert.NotNull(constructor);
            return constructor.Invoke(new[] { save.Proxy, CreateCreditService(save) });
        }

        private static ProductionProviderFixture CreateProductionProvider(
            object snapshot,
            bool throws = false,
            Action onBuild = null)
        {
            Type interfaceType = GetRuntimeType("AL.Core.Interfaces.IEconomyProductionContributionProvider");
            object proxy = CreateDispatchProxy(interfaceType, typeof(ScriptedProductionProviderProxy));
            var state = new ScriptedProductionProvider
            {
                Snapshot = snapshot,
                Throw = throws,
                OnBuild = onBuild
            };
            ((ScriptedProductionProviderProxy)proxy).State = state;
            return new ProductionProviderFixture(proxy, state);
        }

        private static object CreateProductionSnapshot(
            string status,
            string revision,
            params ContributionSpec[] contributions)
        {
            return CreateProductionSnapshotForProfile(status, "profile-a", revision, contributions);
        }

        private static object CreateProductionSnapshotForProfile(
            string status,
            string profileIdentity,
            string revision,
            params ContributionSpec[] contributions)
        {
            Type contributionType = GetRuntimeType("AL.Core.Interfaces.EconomyProductionContribution");
            Type diagnosticType = GetRuntimeType("AL.Core.Interfaces.EconomyDiagnostic");
            IList contributionList = CreateRuntimeList(contributionType);
            foreach (ContributionSpec contribution in contributions ?? Array.Empty<ContributionSpec>())
            {
                contributionList.Add(CreateContribution(contribution));
            }

            return CreateProductionSnapshot(status, profileIdentity, revision, contributionList, CreateRuntimeList(diagnosticType));
        }

        private static object CreateProductionSnapshot(
            string status,
            string profileIdentity,
            string revision,
            IList contributions,
            IList diagnostics)
        {
            Type snapshotType = GetRuntimeType("AL.Core.Interfaces.EconomyProductionContributionSnapshot");
            Type statusType = GetRuntimeType("AL.Core.Interfaces.EconomyProductionSourceStatus");
            Type contributionType = GetRuntimeType("AL.Core.Interfaces.EconomyProductionContribution");
            Type diagnosticType = GetRuntimeType("AL.Core.Interfaces.EconomyDiagnostic");
            Type contributionEnumerable = typeof(IEnumerable<>).MakeGenericType(contributionType);
            Type diagnosticEnumerable = typeof(IEnumerable<>).MakeGenericType(diagnosticType);
            ConstructorInfo constructor = snapshotType.GetConstructor(new[]
            {
                statusType,
                typeof(string),
                typeof(string),
                contributionEnumerable,
                diagnosticEnumerable
            });
            Assert.NotNull(constructor);
            return constructor.Invoke(new[]
            {
                EnumValue(statusType, status),
                profileIdentity,
                revision,
                contributions,
                diagnostics
            });
        }

        private static object CreateContribution(ContributionSpec contribution)
        {
            Type contributionType = GetRuntimeType("AL.Core.Interfaces.EconomyProductionContribution");
            ConstructorInfo constructor = contributionType.GetConstructor(new[]
            {
                GetRuntimeType("AL.Core.ResourceType"),
                typeof(double)
            });
            Assert.NotNull(constructor);
            return constructor.Invoke(new[] { contribution.ResourceType, (object)contribution.Amount });
        }

        private static object CreateDiagnostic(string code, string path)
        {
            Type type = GetRuntimeType("AL.Core.Interfaces.EconomyDiagnostic");
            ConstructorInfo constructor = type.GetConstructor(new[] { typeof(string), typeof(string) });
            Assert.NotNull(constructor);
            return constructor.Invoke(new object[] { code, path });
        }

        private static double[] ProductionRemainders(object service) =>
            (double[])GetField(service, "_productionRemainders");

        private static double Remainder(object service, string resourceName)
        {
            int index = Array.IndexOf(WalletResourceNames, resourceName);
            Assert.GreaterOrEqual(index, 0);
            return ProductionRemainders(service)[index];
        }

        private static void AddResourceEventHandler(object service, Action<object, long> callback)
        {
            EventInfo eventInfo = service.GetType().GetEvent("OnResourceChanged", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(eventInfo);
            Type resourceType = GetRuntimeType("AL.Core.ResourceType");
            Type delegateType = typeof(Action<,>).MakeGenericType(resourceType, typeof(long));
            ParameterExpression resource = Expression.Parameter(resourceType, "resource");
            ParameterExpression balance = Expression.Parameter(typeof(long), "balance");
            InvocationExpression invoke = Expression.Invoke(
                Expression.Constant(callback),
                Expression.Convert(resource, typeof(object)),
                balance);
            Delegate handler = Expression.Lambda(delegateType, invoke, resource, balance).Compile();
            eventInfo.AddEventHandler(service, handler);
        }

        private static object CreateDispatchProxy(Type interfaceType, Type proxyType)
        {
            MethodInfo create = typeof(DispatchProxy).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "Create" && method.GetGenericArguments().Length == 2);
            return create.MakeGenericMethod(interfaceType, proxyType).Invoke(null, null);
        }

        private static IList CreateRuntimeList(Type elementType) =>
            (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));

        private static string[] FindCallers(string scriptsRoot, params string[] tokens)
        {
            return Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => tokens.Any(token => File.ReadAllText(path).Contains(token)))
                .Select(path => path.Substring(scriptsRoot.Length + 1).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static object CreateActualSaveService(string root)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(constructor);
            return constructor.Invoke(new object[] { root });
        }

        private static void SetObservedAuthoritySource(
            object saveService,
            string source)
        {
            SetField(saveService, "_hasObservedAuthoritySource", true);
            SetField(
                saveService,
                "_observedAuthoritySource",
                EnumValue(
                    GetRuntimeType(
                        "AL.Core.SaveAuthority.ProfileAuthoritySourceGeneration"),
                    source));
        }

        private static void AssertAuthoritySource(
            object saveService,
            string expected)
        {
            object authority = Invoke(saveService, "GetCurrentAuthority");
            AssertStatus(authority, "MigrationRequired");
            Assert.AreEqual(
                expected,
                GetProperty(authority, "SelectedSourceGeneration").ToString());
        }

        private static object InvokePreparedCandidate(
            object saveService,
            string chapterId)
        {
            Type saveType = GetRuntimeType("AL.Data.Runtime.SaveGameData");
            Type preparationType = GetRuntimeType(
                "AL.Services.Local.SaveCandidateMutationPreparation");
            Type callbackType = typeof(Func<,>).MakeGenericType(
                saveType,
                preparationType);
            ParameterExpression candidate = Expression.Parameter(
                saveType,
                "candidate");
            MethodInfo prepared = preparationType.GetMethod(
                "Prepared",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(prepared);
            Delegate callback = Expression.Lambda(
                    callbackType,
                    Expression.Block(
                        Expression.Assign(
                            Expression.Field(
                                candidate,
                                "CurrentChapterId"),
                            Expression.Constant(chapterId)),
                        Expression.Call(prepared)),
                    candidate)
                .Compile();
            Type storeType = GetRuntimeType(
                "AL.Services.Local.ISaveGameCandidateStore");
            MethodInfo commit = storeType.GetMethod("TryCommitCandidate");
            Assert.NotNull(commit);
            return commit.Invoke(saveService, new object[] { callback });
        }

        private static object CreateRuntimeService(string serviceTypeName, string argumentTypeName, object argument)
        {
            Type serviceType = GetRuntimeType(serviceTypeName);
            ConstructorInfo constructor = serviceType.GetConstructor(new[] { GetRuntimeType(argumentTypeName) });
            Assert.NotNull(constructor);
            return constructor.Invoke(new[] { argument });
        }

        private static void AssertOnlyPublicSaveConstructor(
            string serviceTypeName)
        {
            Type serviceType = GetRuntimeType(serviceTypeName);
            ConstructorInfo[] publicConstructors = serviceType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public);
            Assert.AreEqual(1, publicConstructors.Length, serviceTypeName);
            ParameterInfo[] parameters =
                publicConstructors[0].GetParameters();
            Assert.AreEqual(1, parameters.Length, serviceTypeName);
            Assert.AreEqual(
                GetRuntimeType("AL.Core.Interfaces.ISaveGameService"),
                parameters[0].ParameterType,
                serviceTypeName);
        }

        private static object CreateBossLootService(object saveService) =>
            CreateBossLootService(saveService, null, null);

        private static object CreateBossLootService(
            object saveService,
            object creditService,
            object notificationService)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalBossLootService");
            ConstructorInfo constructor = serviceType.GetConstructor(new[]
            {
                GetRuntimeType("AL.Core.Interfaces.ISaveGameService"),
                GetRuntimeType("AL.Core.Interfaces.IWarzoneCreditService"),
                GetRuntimeType("AL.Core.Interfaces.INotificationService")
            });
            Assert.NotNull(constructor);
            return constructor.Invoke(new[] { saveService, creditService, notificationService });
        }

        private static object CreateBossLootRequest(
            string encounterId,
            string resultId,
            string bossId,
            int credits)
        {
            object request = Activator.CreateInstance(GetRuntimeType("AL.Core.Interfaces.BossLootRequest"));
            SetField(request, "EncounterId", encounterId);
            SetField(request, "RewardResultId", resultId);
            SetField(request, "BossId", bossId);
            SetField(request, "BossName", "Test Boss");
            SetField(request, "WarzoneCreditReward", credits);
            SetField(request, "RandomSeed", 12345);
            return request;
        }

        private static NotificationFixture CreateNotificationFixture()
        {
            Type interfaceType = GetRuntimeType("AL.Core.Interfaces.INotificationService");
            object proxy = CreateDispatchProxy(interfaceType, typeof(ScriptedNotificationServiceProxy));
            var state = new ScriptedNotificationService();
            ((ScriptedNotificationServiceProxy)proxy).State = state;
            return new NotificationFixture(proxy, state);
        }

        private static object CreateOwnedEquipment(string equipmentId, int quantity)
        {
            object equipment = Activator.CreateInstance(GetRuntimeType("AL.Data.Runtime.OwnedEquipmentState"));
            SetField(equipment, "EquipmentId", equipmentId);
            SetField(equipment, "Quantity", quantity);
            SetField(equipment, "FirstAcquiredTimestamp", 1L);
            SetField(equipment, "LastAcquiredTimestamp", 1L);
            return equipment;
        }

        private static object CreateBossLootDrop(string equipmentId, int quantity)
        {
            object drop = Activator.CreateInstance(GetRuntimeType("AL.Core.Interfaces.BossLootDrop"));
            SetField(drop, "EquipmentId", equipmentId);
            SetField(drop, "Quantity", quantity);
            return drop;
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

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
            Assert.NotNull(method, $"Expected method {methodName}.");
            return method.Invoke(target, args);
        }

        private static bool InvokeOwnedEquipmentMutation(
            object save,
            object drop,
            string bossId)
        {
            Type serviceType = GetRuntimeType(
                "AL.Services.Local.LocalBossLootService");
            MethodInfo method = serviceType.GetMethod(
                "TryAddOwnedEquipment",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method, "Expected isolated equipment mutation helper.");
            return (bool)method.Invoke(null, new[] { save, drop, bossId });
        }

        private static object EnumValue(Type enumType, string value) => Enum.Parse(enumType, value);

        private static Type GetRuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(type, $"Expected loaded runtime type {typeName}.");
            return type;
        }

        private static object GetProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, $"Expected property {name}.");
            return property.GetValue(target);
        }

        private static void SetProperty(
            object target,
            string name,
            object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.NotNull(property, $"Expected property {name}.");
            property.SetValue(target, value);
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

        private sealed class WalletSnapshot
        {
            public WalletSnapshot(IList wallet, WalletRow[] rows)
            {
                Wallet = wallet;
                Rows = rows;
            }

            public IList Wallet { get; }
            public WalletRow[] Rows { get; }
        }

        private sealed class WalletRow
        {
            public WalletRow(object reference, object type, long? amount)
            {
                Reference = reference;
                Type = type;
                Amount = amount;
            }

            public object Reference { get; }
            public object Type { get; }
            public long? Amount { get; }
        }

        private sealed class SaveFixture
        {
            public SaveFixture(object proxy, ScriptedSaveService state)
            {
                Proxy = proxy;
                State = state;
            }

            public object Proxy { get; }
            public ScriptedSaveService State { get; }
        }

        private sealed class ProductionProviderFixture
        {
            public ProductionProviderFixture(object proxy, ScriptedProductionProvider state)
            {
                Proxy = proxy;
                State = state;
            }

            public object Proxy { get; }
            public ScriptedProductionProvider State { get; }
        }

        private sealed class AuthorityProviderFixture
        {
            public AuthorityProviderFixture(
                object proxy,
                ScriptedAuthorityProvider state)
            {
                Proxy = proxy;
                State = state;
            }

            public object Proxy { get; }
            public ScriptedAuthorityProvider State { get; }
        }

        private sealed class NotificationFixture
        {
            public NotificationFixture(object proxy, ScriptedNotificationService state)
            {
                Proxy = proxy;
                State = state;
            }

            public object Proxy { get; }
            public ScriptedNotificationService State { get; }
        }

        private sealed class ContributionSpec
        {
            public ContributionSpec(string resourceName, double amount)
                : this(Resource(resourceName), amount)
            {
            }

            public ContributionSpec(object resourceType, double amount)
            {
                ResourceType = resourceType;
                Amount = amount;
            }

            public object ResourceType { get; }
            public double Amount { get; }
        }

        private sealed class ResourceEvent
        {
            public ResourceEvent(string resourceType, long balance)
            {
                ResourceType = resourceType;
                Balance = balance;
            }

            public string ResourceType { get; }
            public long Balance { get; }
            public override string ToString() => $"{ResourceType}:{Balance}";
        }

        public class ScriptedSaveServiceProxy : DispatchProxy
        {
            public ScriptedSaveService State { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args) =>
                State.Invoke(targetMethod, args);
        }

        public sealed class ScriptedSaveService
        {
            public object CurrentSave;
            public int SaveCount;
            public string LastSaveStatus = "None";
            public string SaveStatusAfterSave;
            public int CurrentSaveDelayMilliseconds;
            public int MaximumConcurrentCurrentSaveReads;
            private int _activeCurrentSaveReads;

            public object Invoke(MethodInfo method, object[] args)
            {
                switch (method.Name)
                {
                    case "get_CurrentSave":
                        int active = Interlocked.Increment(ref _activeCurrentSaveReads);
                        int observed;
                        do
                        {
                            observed = MaximumConcurrentCurrentSaveReads;
                            if (active <= observed)
                            {
                                break;
                            }
                        }
                        while (Interlocked.CompareExchange(
                                   ref MaximumConcurrentCurrentSaveReads,
                                   active,
                                   observed) != observed);
                        try
                        {
                            if (CurrentSaveDelayMilliseconds > 0)
                            {
                                Thread.Sleep(CurrentSaveDelayMilliseconds);
                            }

                            return CurrentSave;
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _activeCurrentSaveReads);
                        }
                    case "Save":
                        SaveCount++;
                        if (!string.IsNullOrEmpty(SaveStatusAfterSave))
                        {
                            LastSaveStatus = SaveStatusAfterSave;
                        }
                        return null;
                    case "HasSave":
                        return CurrentSave != null;
                    case "get_LastLoadMessage":
                    case "get_LastSaveMessage":
                        return string.Empty;
                    case "get_LastSaveStatus":
                        return Enum.Parse(method.ReturnType, LastSaveStatus);
                    default:
                        return method.ReturnType == typeof(void)
                            ? null
                            : method.ReturnType.IsValueType
                                ? Activator.CreateInstance(method.ReturnType)
                                : null;
                }
            }
        }

        public class ScriptedAuthorityProviderProxy : DispatchProxy
        {
            public ScriptedAuthorityProvider State { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args) =>
                State.Invoke(targetMethod);
        }

        public sealed class ScriptedAuthorityProvider
        {
            public object Snapshot;
            public Func<object> SnapshotFactory;
            public bool Throw;
            public int CallCount;

            public object Invoke(MethodInfo method)
            {
                if (method.Name != "GetCurrentAuthority")
                {
                    throw new NotSupportedException(method.Name);
                }

                CallCount++;
                if (Throw)
                {
                    throw new InvalidOperationException("authority provider");
                }

                return SnapshotFactory == null
                    ? Snapshot
                    : SnapshotFactory();
            }
        }

        public class ThrowingCreditServiceProxy : DispatchProxy
        {
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == "TryAddCredits")
                {
                    throw new InvalidOperationException("credit mutation");
                }

                return targetMethod.ReturnType == typeof(void)
                    ? null
                    : targetMethod.ReturnType.IsValueType
                        ? Activator.CreateInstance(targetMethod.ReturnType)
                        : null;
            }
        }

        public class ScriptedNotificationServiceProxy : DispatchProxy
        {
            public ScriptedNotificationService State { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args) =>
                State.Invoke(targetMethod, args);
        }

        public sealed class ScriptedNotificationService
        {
            public readonly List<string> Messages = new List<string>();
            public bool ThrowOnMessage;
            public int ShowMessageCount;

            public object Invoke(MethodInfo method, object[] args)
            {
                if (method.Name == "ShowMessage")
                {
                    ShowMessageCount++;
                    if (ThrowOnMessage)
                    {
                        throw new InvalidOperationException("notification");
                    }

                    Messages.Add((string)args[0]);
                }

                return null;
            }
        }

        public class ScriptedProductionProviderProxy : DispatchProxy
        {
            public ScriptedProductionProvider State { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args) =>
                State.Invoke(targetMethod, args);
        }

        public sealed class ScriptedProductionProvider
        {
            public object Snapshot;
            public Action OnBuild;
            public bool Throw;
            public int CallCount;
            public double LastDeltaSeconds;

            public object Invoke(MethodInfo method, object[] args)
            {
                if (method.Name != "BuildContributions")
                {
                    throw new NotSupportedException(method.Name);
                }

                CallCount++;
                LastDeltaSeconds = Convert.ToDouble(args[0]);
                if (Throw)
                {
                    throw new InvalidOperationException("provider");
                }

                OnBuild?.Invoke();
                return Snapshot;
            }
        }
    }
}
