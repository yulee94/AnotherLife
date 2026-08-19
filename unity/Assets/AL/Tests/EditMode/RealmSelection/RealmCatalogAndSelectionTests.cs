using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.Services.Local;
using AL.UI.RealmSelection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class RealmCatalogAndSelectionTests
    {
        private string _json;
        private RealmCatalogSnapshot _catalog;
        private readonly List<RealmDefinition> _definitions = new List<RealmDefinition>(4);

        [SetUp]
        public void SetUp()
        {
            string path = Path.Combine(Application.dataPath, "AL", "StreamingAssets", "GameData", "realm_specialized.v1.json");
            _json = File.ReadAllText(path);
            RealmCatalogLoadResult result = RealmCatalogRuntime.Parse(_json);
            Assert.That(result.IsSuccess, Is.True, result.TechnicalCode);
            _catalog = result.Snapshot;
            _definitions.Add(CreateDefinition(RealmId.Crownlands));
            _definitions.Add(CreateDefinition(RealmId.Stonehold));
            _definitions.Add(CreateDefinition(RealmId.Eldergrove));
            _definitions.Add(CreateDefinition(RealmId.Umbral));
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _definitions.Count; i++)
                UnityEngine.Object.DestroyImmediate(_definitions[i]);
            _definitions.Clear();
        }

        [Test]
        public void CanonicalCatalogPublishesExactlyFourStableRuntimeMappings()
        {
            Assert.That(_catalog.Version, Is.EqualTo("0.1.0"));
            Assert.That(_catalog.Realms, Has.Count.EqualTo(4));
            RealmCatalogEntry entry;
            Assert.That(_catalog.TryGet(RealmId.Crownlands, out entry), Is.True);
            Assert.That(entry.Id, Is.EqualTo("crownlands"));
            Assert.That(_catalog.TryGet(RealmId.Stonehold, out entry), Is.True);
            Assert.That(_catalog.TryGet(RealmId.Eldergrove, out entry), Is.True);
            Assert.That(_catalog.TryGet(RealmId.Umbral, out entry), Is.True);
        }

        [Test]
        public void PackagedCatalogFilePathBecomesAFileRequestUri()
        {
            string streamingAssetsRoot = Path.Combine(Path.GetTempPath(), "Another Life StreamingAssets");

            string requestUri = RealmCatalogRuntimeHost.BuildRequestUri(streamingAssetsRoot);

            Assert.That(requestUri, Does.StartWith("file:"));
            Assert.That(
                new System.Uri(requestUri).LocalPath,
                Is.EqualTo(Path.GetFullPath(Path.Combine(streamingAssetsRoot, RealmCatalogRuntime.RelativePath))));
        }

        [Test]
        public void PackagedCatalogRequestPreservesRemoteAndAndroidUriRoots()
        {
            Assert.That(
                RealmCatalogRuntimeHost.BuildRequestUri("jar:file:///game.apk!/assets"),
                Is.EqualTo("jar:file:///game.apk!/assets/GameData/realm_specialized.v1.json"));
            Assert.That(
                RealmCatalogRuntimeHost.BuildRequestUri("https://content.example.test/assets"),
                Is.EqualTo("https://content.example.test/assets/GameData/realm_specialized.v1.json"));
        }

        [Test]
        public void EditorCatalogRequestResolvesTheCanonicalSharedSource()
        {
            MethodInfo resolver = typeof(RealmCatalogRuntimeHost).GetMethod(
                "BuildEditorRequestUri",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(resolver, Is.Not.Null, "Editor play needs an explicit shared-source resolver.");
            string requestUri = (string)resolver.Invoke(
                null,
                new object[] { Application.dataPath });
            string expected = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                RealmCatalogRuntime.RelativePath));

            Assert.That(new Uri(requestUri).LocalPath, Is.EqualTo(expected));
            Assert.That(File.Exists(expected), Is.True, "The resolved catalog must be the tracked authority source.");
            Assert.That(
                File.Exists(Path.Combine(Application.streamingAssetsPath, RealmCatalogRuntime.RelativePath)),
                Is.False,
                "The test must retain the reported missing-root reproduction.");
        }

        [Test]
        public void EditorCatalogRequestFailsClosedWhenOnlyAStaleDuplicateExists()
        {
            MethodInfo resolver = typeof(RealmCatalogRuntimeHost).GetMethod(
                "BuildEditorRequestUri",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-RealmCatalogAuthorityTests",
                Guid.NewGuid().ToString("N"));
            string assetsRoot = Path.Combine(root, "Assets");
            string staleDuplicate = Path.Combine(
                assetsRoot,
                "StreamingAssets",
                RealmCatalogRuntime.RelativePath);
            string canonical = Path.Combine(
                assetsRoot,
                "AL",
                "StreamingAssets",
                RealmCatalogRuntime.RelativePath);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(staleDuplicate));
                File.WriteAllText(staleDuplicate, _json);

                string requestUri = (string)resolver.Invoke(null, new object[] { assetsRoot });

                Assert.That(File.Exists(staleDuplicate), Is.True, "The stale valid duplicate must exist for this adversarial fixture.");
                Assert.That(File.Exists(canonical), Is.False, "The authoritative source must remain absent for this fixture.");
                Assert.That(new Uri(requestUri).LocalPath, Is.EqualTo(Path.GetFullPath(canonical)));
                Assert.That(new Uri(requestUri).LocalPath, Is.Not.EqualTo(Path.GetFullPath(staleDuplicate)));
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
        public void MissingMalformedAndOversizeCatalogsFailClosed()
        {
            AssertCatalogRejected(
                RealmCatalogRuntime.Parse(null),
                "AL-REALM-CATALOG-MISSING");
            AssertCatalogRejected(
                RealmCatalogRuntime.Parse(string.Empty),
                "AL-REALM-CATALOG-MISSING");
            AssertCatalogRejected(
                RealmCatalogRuntime.Parse("{\"version\":"),
                "AL-REALM-CATALOG-MALFORMED");
            string utf8Oversize = new string(
                '\u00e9',
                (RealmCatalogRuntime.MaximumByteLength / 2) + 1);
            Assert.That(utf8Oversize.Length, Is.LessThanOrEqualTo(RealmCatalogRuntime.MaximumByteLength));
            Assert.That(Encoding.UTF8.GetByteCount(utf8Oversize), Is.GreaterThan(RealmCatalogRuntime.MaximumByteLength));
            AssertCatalogRejected(
                RealmCatalogRuntime.Parse(utf8Oversize),
                "AL-REALM-CATALOG-OVERSIZE");

            MethodInfo readFailure = typeof(RealmCatalogRuntime).GetMethod(
                "ReadFailure",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(readFailure, Is.Not.Null);
            AssertCatalogRejected(
                (RealmCatalogLoadResult)readFailure.Invoke(null, null),
                "AL-REALM-CATALOG-READ-FAILED");
        }

        [Test]
        public void BoundedDownloadHandlerRejectsOverflowBeforePublishingBytes()
        {
            Type handlerType = typeof(RealmCatalogRuntimeHost).Assembly.GetType(
                "AL.RealmSelection.BoundedRealmCatalogDownloadHandler",
                throwOnError: true);
            object handler = Activator.CreateInstance(
                handlerType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { 8 },
                culture: null);
            try
            {
                MethodInfo receiveData = handlerType.GetMethod(
                    "ReceiveData",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                PropertyInfo isOversize = handlerType.GetProperty(
                    "IsOversize",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo getUtf8Text = handlerType.GetMethod(
                    "GetUtf8Text",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(receiveData, Is.Not.Null);
                Assert.That(isOversize, Is.Not.Null);
                Assert.That(getUtf8Text, Is.Not.Null);

                byte[] exactCapacity = Encoding.UTF8.GetBytes("12345678");
                bool acceptedAtCapacity = (bool)receiveData.Invoke(
                    handler,
                    new object[] { exactCapacity, exactCapacity.Length });
                bool acceptedOverflow = (bool)receiveData.Invoke(
                    handler,
                    new object[] { new byte[] { (byte)'9' }, 1 });

                Assert.That(acceptedAtCapacity, Is.True);
                Assert.That(acceptedOverflow, Is.False);
                Assert.That((bool)isOversize.GetValue(handler), Is.True);
                Assert.That((string)getUtf8Text.Invoke(handler, null), Is.EqualTo("12345678"),
                    "The overflow byte must not be partially published.");
            }
            finally
            {
                (handler as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void RealmSelectionCreatesOneBoundedPresentationCameraWhenNoCameraIsRendering()
        {
            MethodInfo ensure = typeof(RealmSelectionController).GetMethod(
                "EnsurePresentationCamera",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(ensure, Is.Not.Null, "RealmSelection must prevent the Game view's no-camera state.");

            Camera[] existing = UnityEngine.Object.FindObjectsOfType<Camera>();
            var enabled = new bool[existing.Length];
            Camera created = null;
            try
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    enabled[i] = existing[i].enabled;
                    existing[i].enabled = false;
                }

                created = (Camera)ensure.Invoke(null, null);
                Camera repeated = (Camera)ensure.Invoke(null, null);

                Assert.That(created, Is.Not.Null);
                Assert.That(repeated, Is.SameAs(created));
                Assert.That(created.name, Is.EqualTo("RealmSelectionCamera"));
                Assert.That(created.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
                Assert.That(created.cullingMask, Is.Zero);
                Assert.That(created.depth, Is.EqualTo(-100f));
                Assert.That(created.orthographic, Is.True);
                Assert.That(created.CompareTag("MainCamera"), Is.True);
            }
            finally
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created.gameObject);
                }

                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] != null)
                    {
                        existing[i].enabled = enabled[i];
                    }
                }
            }
        }

        [Test]
        public void RealmSelectionIgnoresRenderTextureAndAlternateDisplayCameras()
        {
            MethodInfo ensure = typeof(RealmSelectionController).GetMethod(
                "EnsurePresentationCamera",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Camera[] existing = UnityEngine.Object.FindObjectsOfType<Camera>();
            var enabled = new bool[existing.Length];
            var renderTextureObject = new GameObject("RenderTextureCamera");
            var alternateDisplayObject = new GameObject("AlternateDisplayCamera");
            var renderTexture = new RenderTexture(8, 8, 0);
            Camera created = null;
            try
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    enabled[i] = existing[i].enabled;
                    existing[i].enabled = false;
                }

                renderTextureObject.tag = "MainCamera";
                Camera renderTextureCamera = renderTextureObject.AddComponent<Camera>();
                renderTextureCamera.targetTexture = renderTexture;
                Camera alternateDisplayCamera = alternateDisplayObject.AddComponent<Camera>();
                alternateDisplayCamera.targetDisplay = 1;

                created = (Camera)ensure.Invoke(null, null);

                Assert.That(created, Is.Not.Null);
                Assert.That(created, Is.Not.SameAs(renderTextureCamera));
                Assert.That(created, Is.Not.SameAs(alternateDisplayCamera));
                Assert.That(created.targetTexture, Is.Null);
                Assert.That(created.targetDisplay, Is.Zero);
                Assert.That(created.name, Is.EqualTo("RealmSelectionCamera"));
            }
            finally
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created.gameObject);
                }

                UnityEngine.Object.DestroyImmediate(renderTextureObject);
                UnityEngine.Object.DestroyImmediate(alternateDisplayObject);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] != null)
                    {
                        existing[i].enabled = enabled[i];
                    }
                }
            }
        }

        [Test]
        public void ParserRejectsUnsupportedVersionPolicyAndDuplicateRealm()
        {
            Assert.That(RealmCatalogRuntime.Parse(_json.Replace("\"1.0.0\"", "\"9.0.0\"")).TechnicalCode, Is.EqualTo("AL-REALM-CATALOG-MALFORMED"));
            Assert.That(RealmCatalogRuntime.Parse(_json.Replace("same_realm_only", "cross_realm")).TechnicalCode, Is.EqualTo("AL-REALM-CATALOG-POLICY-MISMATCH"));
            Assert.That(RealmCatalogRuntime.Parse(_json.Replace("\"id\": \"stonehold\"", "\"id\": \"crownlands\"")).TechnicalCode, Is.EqualTo("AL-REALM-CATALOG-MALFORMED"));

            string swappedRuntimeIds = _json
                .Replace("\"legacy_runtime_id\": \"Crownlands\"", "\"legacy_runtime_id\": \"SwapTemporary\"")
                .Replace("\"legacy_runtime_id\": \"Umbral\"", "\"legacy_runtime_id\": \"Crownlands\"")
                .Replace("\"legacy_runtime_id\": \"SwapTemporary\"", "\"legacy_runtime_id\": \"Umbral\"");
            Assert.That(RealmCatalogRuntime.Parse(swappedRuntimeIds).TechnicalCode, Is.EqualTo("AL-REALM-CATALOG-INVALID-REALM"));
        }

        [Test]
        public void ParserRejectsContradictoryLockPolicyAndInvalidGemIdentifiers()
        {
            Assert.That(
                RealmCatalogRuntime.Parse(_json.Replace("not_supported_after_commit", "allow_after_commit")).TechnicalCode,
                Is.EqualTo("AL-REALM-CATALOG-POLICY-MISMATCH"));
            Assert.That(
                RealmCatalogRuntime.Parse(_json.Replace("same_realm_account_storage", "cross_realm_storage")).TechnicalCode,
                Is.EqualTo("AL-REALM-CATALOG-POLICY-MISMATCH"));
            Assert.That(
                RealmCatalogRuntime.Parse(_json.Replace("realm_unselected", "realm_locked")).TechnicalCode,
                Is.EqualTo("AL-REALM-CATALOG-POLICY-MISMATCH"));
            Assert.That(
                RealmCatalogRuntime.Parse(_json.Replace("gem_stonehold_forge", "gem_crownlands_sun")).TechnicalCode,
                Is.EqualTo("AL-REALM-CATALOG-INVALID-REALM"));
            Assert.That(
                RealmCatalogRuntime.Parse(_json.Replace("gem_umbral_ember", "Gem_Umbral_Ember")).TechnicalCode,
                Is.EqualTo("AL-REALM-CATALOG-INVALID-REALM"));
        }

        [Test]
        public void FirstCommitPersistsAndDifferentRealmIsRejectedWithoutSecondSave()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-RealmSelectionTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                LocalSaveGameService save = CreateLocalSave(root);
                var service = new LocalRealmService(save, new FakeGameDataService(_definitions), _catalog);
                RealmSelectionResult first = service.TrySelectRealm(new RealmSelectionRequest("tx_first", RealmId.Stonehold));
                byte[] exactPrimary = File.ReadAllBytes(Path.Combine(root, "save.json"));
                byte[] exactBackup = File.ReadAllBytes(Path.Combine(root, "save.backup.json"));
                SaveGameData published = save.CurrentSave;
                RealmSelectionResult repeated = service.TrySelectRealm(new RealmSelectionRequest("tx_same", RealmId.Stonehold));
                RealmSelectionResult different = service.TrySelectRealm(new RealmSelectionRequest("tx_other", RealmId.Umbral));
                Assert.That(first.Status, Is.EqualTo(RealmSelectionStatus.Committed));
                Assert.That(repeated.Status, Is.EqualTo(RealmSelectionStatus.AlreadyCommittedSameRealm));
                Assert.That(different.Status, Is.EqualTo(RealmSelectionStatus.RejectedDifferentRealm));
                Assert.That(save.CurrentSave, Is.SameAs(published));
                Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.Stonehold));
                CollectionAssert.AreEqual(exactPrimary, File.ReadAllBytes(Path.Combine(root, "save.json")));
                CollectionAssert.AreEqual(exactBackup, File.ReadAllBytes(Path.Combine(root, "save.backup.json")));
                Assert.That(service.Identity.Status, Is.EqualTo(RealmIdentityStatus.CommittedValid));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void InvalidRequestsAndUntypedSaveServiceNeverBecomeAuthoritative()
        {
            var save = new FakeSaveService { FailSave = true };
            var service = new LocalRealmService(save, new FakeGameDataService(_definitions), _catalog);
            Assert.That(service.TrySelectRealm(new RealmSelectionRequest("", RealmId.Crownlands)).Status, Is.EqualTo(RealmSelectionStatus.InvalidTransaction));
            Assert.That(service.TrySelectRealm(new RealmSelectionRequest("tx_none", RealmId.None)).Status, Is.EqualTo(RealmSelectionStatus.InvalidRealm));
            RealmSelectionResult failed = service.TrySelectRealm(new RealmSelectionRequest("tx_fail", RealmId.Eldergrove));
            Assert.That(failed.Status, Is.EqualTo(RealmSelectionStatus.ProfileUnavailable));
            Assert.That(failed.TechnicalCode, Is.EqualTo("AL-REALM-TYPED-CANDIDATE-STORE-UNAVAILABLE"));
            Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.None));
            Assert.That(service.Identity.Status, Is.EqualTo(RealmIdentityStatus.Uncommitted));
        }

        [Test]
        public void MissingRuntimeDefinitionCannotCommitOrProduceSplitCurrentRealmProperties()
        {
            var uncommitted = new FakeSaveService();
            var missingDefinitionService = new LocalRealmService(
                uncommitted,
                new FakeGameDataService(_definitions, RealmId.Umbral),
                _catalog);

            RealmSelectionResult missing = missingDefinitionService.TrySelectRealm(
                new RealmSelectionRequest("tx_missing_definition", RealmId.Umbral));

            Assert.That(missing.Status, Is.EqualTo(RealmSelectionStatus.RealmDefinitionUnavailable));
            Assert.That(uncommitted.SaveCount, Is.Zero);
            Assert.That(uncommitted.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.None));

            var committed = new FakeSaveService(RealmId.Umbral);
            var inconsistentService = new LocalRealmService(
                committed,
                new FakeGameDataService(_definitions, RealmId.Umbral),
                _catalog);

            Assert.That(inconsistentService.Identity.Status, Is.EqualTo(RealmIdentityStatus.CatalogUnavailable));
            Assert.That(inconsistentService.CurrentRealmId, Is.EqualTo(RealmId.None));
            Assert.That(inconsistentService.CurrentRealm, Is.Null);
        }

        [Test]
        public void NullGameDataServiceFailsClosedWithoutMutation()
        {
            var save = new FakeSaveService();
            var service = new LocalRealmService(save, null, _catalog);

            RealmSelectionResult result = service.TrySelectRealm(
                new RealmSelectionRequest("tx_null_game_data", RealmId.Crownlands));

            Assert.That(result.Status, Is.EqualTo(RealmSelectionStatus.RealmDefinitionUnavailable));
            Assert.That(save.SaveCount, Is.Zero);
            Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.None));
        }

        [Test]
        public void SubCharactersMustMatchCommittedAccountRealm()
        {
            var committed = new RealmIdentitySnapshot(RealmIdentityStatus.CommittedValid, RealmId.Umbral, "0.1.0", "test");
            var uncommitted = new RealmIdentitySnapshot(RealmIdentityStatus.Uncommitted, RealmId.None, "0.1.0", "test");
            var invalidNone = new RealmIdentitySnapshot(RealmIdentityStatus.CommittedValid, RealmId.None, "0.1.0", "test");
            var invalidUndefined = new RealmIdentitySnapshot(RealmIdentityStatus.CommittedValid, (RealmId)999, "0.1.0", "test");
            Assert.That(RealmCharacterConstraint.Evaluate(committed, RealmId.Umbral), Is.EqualTo(RealmCharacterEligibility.Allowed));
            Assert.That(RealmCharacterConstraint.Evaluate(committed, RealmId.Crownlands), Is.EqualTo(RealmCharacterEligibility.RejectedDifferentRealm));
            Assert.That(RealmCharacterConstraint.Evaluate(uncommitted, RealmId.Umbral), Is.EqualTo(RealmCharacterEligibility.AccountRealmUnavailable));
            Assert.That(invalidNone.IsCommittedValid, Is.False);
            Assert.That(invalidUndefined.IsCommittedValid, Is.False);
            Assert.That(RealmCharacterConstraint.Evaluate(invalidNone, RealmId.Umbral), Is.EqualTo(RealmCharacterEligibility.AccountRealmUnavailable));
            Assert.That(RealmCharacterConstraint.Evaluate(invalidUndefined, RealmId.Umbral), Is.EqualTo(RealmCharacterEligibility.AccountRealmUnavailable));
        }

        private sealed class FakeSaveService : ISaveGameService
        {
            public FakeSaveService(RealmId selectedRealm = RealmId.None)
            {
                CurrentSave = NewSave();
                CurrentSave.SelectedRealm = selectedRealm;
            }

            public SaveGameData CurrentSave { get; private set; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus { get; private set; }
            public string LastSaveMessage => string.Empty;
            public int SaveCount { get; private set; }
            public bool FailSave { get; set; }
            public void Save() { SaveCount++; LastSaveStatus = FailSave ? SaveOperationStatus.SaveFailedPreviousPreserved : SaveOperationStatus.SavedPrimary; }
            public void Load() { }
            public bool HasSave() => CurrentSave != null;
            public void CreateNewSave(RealmId realmId) { CurrentSave = NewSave(); CurrentSave.SelectedRealm = realmId; Save(); }
            public void DeleteSave() { CurrentSave = null; }
            private static SaveGameData NewSave() => new SaveGameData { SaveFormatId = SaveGameData.CurrentSaveFormatId, SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion, ProfileInitializationVersion = SaveGameData.CurrentProfileInitializationVersion, SelectedRealm = RealmId.None };
        }

        private static LocalSaveGameService CreateLocalSave(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(string) },
                    modifiers: null);
            Assert.NotNull(constructor);
            return (LocalSaveGameService)constructor.Invoke(
                new object[] { root });
        }

        private sealed class FakeGameDataService : IGameDataService
        {
            private readonly IEnumerable<RealmDefinition> _definitions;
            private readonly RealmId _missingRealm;

            public FakeGameDataService(IEnumerable<RealmDefinition> definitions, RealmId missingRealm = RealmId.None)
            {
                _definitions = definitions;
                _missingRealm = missingRealm;
            }

            public RealmDefinition GetRealm(RealmId id)
            {
                if (id == _missingRealm) return null;
                foreach (RealmDefinition definition in _definitions)
                    if (definition.Id == id) return definition;
                return null;
            }

            public IEnumerable<RealmDefinition> GetAllRealms() => _definitions;
            public BuildingDefinition GetBuilding(string id) => null;
            public TroopDefinition GetTroop(string id) => null;
            public ChampionDefinition GetChampion(string id) => null;
            public IEnumerable<ChampionDefinition> GetAllChampions() =>
                System.Linq.Enumerable.Empty<ChampionDefinition>();
            public SkillDefinition GetSkill(string id) => null;
        }

        private static RealmDefinition CreateDefinition(RealmId id)
        {
            var definition = ScriptableObject.CreateInstance<RealmDefinition>();
            definition.Id = id;
            definition.RealmName = id.ToString();
            return definition;
        }

        private static void AssertCatalogRejected(
            RealmCatalogLoadResult result,
            string expectedTechnicalCode)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.TechnicalCode, Is.EqualTo(expectedTechnicalCode));
            Assert.That(result.TechnicalCode, Is.Not.EqualTo("AL-REALM-CATALOG-READY"));
        }
    }
}
