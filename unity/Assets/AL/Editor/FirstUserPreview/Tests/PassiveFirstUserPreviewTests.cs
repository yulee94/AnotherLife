using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AL.EditorTools.FirstUserPreview;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AL.Tests.EditMode.FirstUserPreview
{
    public sealed class PassiveFirstUserPreviewStateTests
    {
        private PassiveFirstUserPreviewWindow _window;

        [SetUp]
        public void SetUp()
        {
            _window = ScriptableObject.CreateInstance<PassiveFirstUserPreviewWindow>();
            _window.Show();
            _window.CreateGUI();
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
            {
                _window.Close();
                _window = null;
            }
        }

        [Test]
        public void InitialStateIsFinishedLoading()
        {
            Assert.That(_window.ScreenForTests, Is.EqualTo(PassiveFirstUserPreviewScreen.FinishedLoading));
        }

        [Test]
        public void PreviewActionAdvancesToChampionHud()
        {
            Assert.That(_window.TryShowChampionHudForTests(), Is.True);
            Assert.That(_window.ScreenForTests, Is.EqualTo(PassiveFirstUserPreviewScreen.ChampionHud));
        }

        [Test]
        public void RepeatedPreviewActionIsIdempotent()
        {
            Assert.That(_window.TryShowChampionHudForTests(), Is.True);
            Assert.That(_window.TryShowChampionHudForTests(), Is.False);
            Assert.That(_window.ScreenForTests, Is.EqualTo(PassiveFirstUserPreviewScreen.ChampionHud));
        }

        [Test]
        public void EscapeSemanticReturnsToFinishedLoading()
        {
            Assert.That(_window.TryShowChampionHudForTests(), Is.True);
            using (KeyDownEvent escape = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None))
            {
                _window.rootVisualElement.SendEvent(escape);
            }

            Assert.That(_window.ScreenForTests, Is.EqualTo(PassiveFirstUserPreviewScreen.FinishedLoading));
        }

        [Test]
        public void NavigationCancelSemanticReturnsToFinishedLoading()
        {
            Assert.That(_window.TryShowChampionHudForTests(), Is.True);
            using (NavigationCancelEvent cancel = NavigationCancelEvent.GetPooled(EventModifiers.None))
            {
                _window.rootVisualElement.SendEvent(cancel);
            }

            Assert.That(_window.ScreenForTests, Is.EqualTo(PassiveFirstUserPreviewScreen.FinishedLoading));
        }

        [Test]
        public void CancelOnInitialScreenIsIdempotent()
        {
            Assert.That(_window.TryReturnToFinishedLoadingForTests(), Is.False);
            Assert.That(_window.ScreenForTests, Is.EqualTo(PassiveFirstUserPreviewScreen.FinishedLoading));
        }

        [Test]
        public void ExplicitLifecycleResetReturnsToFinishedLoading()
        {
            Assert.That(_window.TryShowChampionHudForTests(), Is.True);
            _window.ResetSessionForTests();
            Assert.That(_window.ScreenForTests, Is.EqualTo(PassiveFirstUserPreviewScreen.FinishedLoading));
        }

        [Test]
        public void CreateGuiReconstructionReturnsToFinishedLoading()
        {
            Assert.That(_window.TryShowChampionHudForTests(), Is.True);
            _window.CreateGUI();
            Assert.That(_window.ScreenForTests, Is.EqualTo(PassiveFirstUserPreviewScreen.FinishedLoading));
            Assert.That(_window.ActionForTests, Is.Not.Null);
        }
    }

    public sealed class PassiveFirstUserPreviewWindowTests
    {
        private PassiveFirstUserPreviewWindow _window;

        [SetUp]
        public void SetUp()
        {
            _window = ScriptableObject.CreateInstance<PassiveFirstUserPreviewWindow>();
            _window.Show();
            _window.CreateGUI();
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
            {
                _window.Close();
                _window = null;
            }
        }

        [Test]
        public void ViewerIsDockableEditorWindowNotRuntimeComponent()
        {
            Assert.That(_window, Is.InstanceOf<EditorWindow>());
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(PassiveFirstUserPreviewWindow)), Is.False);
        }

        [Test]
        public void DisclosurePersistsOnFinishedLoadingScreen()
        {
            Label label = _window.rootVisualElement.Q<Label>(PassiveFirstUserPreviewWindow.DisclosureElementName);
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Is.EqualTo(PassiveFirstUserPreviewWindow.DisclosureText));
        }

        [Test]
        public void DisclosurePersistsOnChampionHudScreen()
        {
            Assert.That(_window.TryShowChampionHudForTests(), Is.True);
            Label label = _window.rootVisualElement.Q<Label>(PassiveFirstUserPreviewWindow.DisclosureElementName);
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Is.EqualTo(PassiveFirstUserPreviewWindow.DisclosureText));
        }

        [Test]
        public void FinishedLoadingExposesExactlyOneNamedAction()
        {
            List<Button> actions = _window.rootVisualElement.Query<Button>().ToList();
            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].text, Is.EqualTo(PassiveFirstUserPreviewWindow.ActionText));
            Assert.That(actions[0].name, Is.EqualTo(PassiveFirstUserPreviewWindow.ActionElementName));
        }

        [Test]
        public void ArtworkIncludingBakedStartGamePixelsIsPassive()
        {
            Image image = _window.rootVisualElement.Q<Image>(PassiveFirstUserPreviewWindow.ImageElementName);
            Assert.That(image, Is.Not.Null);
            Assert.That(image.focusable, Is.False);
            Assert.That(image.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(image.Children(), Is.Empty);
        }

        [Test]
        public void CodeOwnedActionIsTheOnlyTabStop()
        {
            VisualElement[] tabStops = _window.rootVisualElement.Query<VisualElement>()
                .ToList()
                .Where(element => element.focusable && element.tabIndex >= 0)
                .ToArray();
            Assert.That(tabStops, Has.Length.EqualTo(1));
            Assert.That(tabStops[0], Is.SameAs(_window.ActionForTests));
        }

        [Test]
        public void InitialFocusIsRequestedAndHasStructuralCue()
        {
            Assert.That(_window.InitialFocusRequestedForTests, Is.True);
            Assert.That(_window.ActionForTests.focusable, Is.True);
            Assert.That(_window.ActionForTests.tabIndex, Is.EqualTo(0));
            Assert.That(
                _window.rootVisualElement.focusController.focusedElement,
                Is.SameAs(_window.ActionForTests));
            Assert.That(
                _window.ActionForTests.style.borderLeftWidth.value,
                Is.EqualTo(PassiveFirstUserPreviewWindow.FocusedBorderWidth));
        }

        [UnityTest]
        public IEnumerator StandardButtonPointerClickablePathAdvancesOnlyOnce()
        {
            _window.Repaint();
            yield return null;

            Button action = _window.ActionForTests;
            Assert.That(action.clickable, Is.Not.Null);
            Assert.That(
                action.clickable.activators.Any(filter => filter.button == MouseButton.LeftMouse),
                Is.True);
            System.Reflection.MethodInfo simulateSingleClick = typeof(Clickable).GetMethod(
                "SimulateSingleClick",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(simulateSingleClick, Is.Not.Null);
            simulateSingleClick.Invoke(action.clickable, new object[] { null, 0 });
            yield return null;

            Assert.That(_window.ScreenForTests, Is.EqualTo(PassiveFirstUserPreviewScreen.ChampionHud));
            Assert.That(_window.TryShowChampionHudForTests(), Is.False);
        }

        [TestCase("Enter")]
        [TestCase("Space")]
        public void StandardButtonNavigationSubmitAdvancesExactlyOnce(string inputName)
        {
            Button action = _window.ActionForTests;
            Assert.That(action.clickable, Is.Not.Null, inputName);
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                action.SendEvent(submit);
            }

            Assert.That(_window.ScreenForTests, Is.EqualTo(PassiveFirstUserPreviewScreen.ChampionHud));
            Assert.That(_window.TryShowChampionHudForTests(), Is.False);
        }

        [Test]
        public void ChampionHudIsPassiveAndExposesNoAction()
        {
            Assert.That(_window.TryShowChampionHudForTests(), Is.True);
            Assert.That(_window.rootVisualElement.Query<Button>().ToList(), Is.Empty);
            Image image = _window.rootVisualElement.Q<Image>(PassiveFirstUserPreviewWindow.ImageElementName);
            Assert.That(image, Is.Not.Null);
            Assert.That(image.pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        [Test]
        public void MissingOrWrongPathAssetFailsClosedWithoutAction()
        {
            _window.AssetLoaderForTests = new RejectingAssetLoader();
            _window.RebuildCurrentScreenForTests();

            Assert.That(_window.rootVisualElement.Q<Label>(PassiveFirstUserPreviewWindow.ErrorElementName), Is.Not.Null);
            Assert.That(_window.rootVisualElement.Query<Button>().ToList(), Is.Empty);
            Assert.That(_window.TryShowChampionHudForTests(), Is.False);
        }

        private sealed class RejectingAssetLoader : IFirstUserPreviewAssetLoader
        {
            public bool TryLoad(string resourceKey, string expectedAssetPath, out Texture2D texture)
            {
                texture = null;
                return false;
            }
        }
    }

    public sealed class PassiveFirstUserPreviewContainmentTests
    {
        private const string WindowSourcePath =
            "Assets/AL/Editor/FirstUserPreview/PassiveFirstUserPreviewWindow.cs";
        private const string ImplementationAsmdefPath =
            "Assets/AL/Editor/FirstUserPreview/AL.FirstUserPreview.Editor.asmdef";

        [TestCase(
            FirstUserPreviewAssetContract.FinishedLoadingKey,
            FirstUserPreviewAssetContract.FinishedLoadingPath,
            FirstUserPreviewAssetContract.FinishedLoadingGuid,
            "7797b38f4ae55caeaa66f30b9964bffebdd05d15ad4809359e342ed4c03d5657",
            2376573L)]
        [TestCase(
            FirstUserPreviewAssetContract.ChampionHudKey,
            FirstUserPreviewAssetContract.ChampionHudPath,
            FirstUserPreviewAssetContract.ChampionHudGuid,
            "7dc0932c3a4d872e7e1890d0044b801f7f64865ca1d8be5b7e4fd7f8e60eb2cc",
            2488480L)]
        public void ApprovedReferenceBytesAndIdentityAreExact(
            string resourceKey,
            string assetPath,
            string expectedGuid,
            string expectedSha256,
            long expectedBytes)
        {
            string absolutePath = AbsoluteAssetPath(assetPath);
            Assert.That(File.Exists(absolutePath), Is.True, assetPath);
            Assert.That(new FileInfo(absolutePath).Length, Is.EqualTo(expectedBytes));
            Assert.That(ComputeSha256(absolutePath), Is.EqualTo(expectedSha256));
            Assert.That(AssetDatabase.AssetPathToGUID(assetPath), Is.EqualTo(expectedGuid));

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(1672));
            Assert.That(texture.height, Is.EqualTo(941));
            var loader = new EditorDefaultResourceFirstUserPreviewAssetLoader();
            Assert.That(loader.TryLoad(resourceKey, assetPath, out Texture2D loadedTexture), Is.True);
            Assert.That(loadedTexture, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(loadedTexture), Is.EqualTo(assetPath));
        }

        [TestCase(FirstUserPreviewAssetContract.FinishedLoadingPath)]
        [TestCase(FirstUserPreviewAssetContract.ChampionHudPath)]
        public void ApprovedReferenceImporterIsEditorReviewOnlyAndUncompressed(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.streamingMipmaps, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.None));
            Assert.That(importer.alphaIsTransparency, Is.False);
            Assert.That(importer.assetBundleName, Is.Null.Or.Empty);
            Assert.That(importer.GetPlatformTextureSettings("Standalone").overridden, Is.False);
            Assert.That(importer.GetPlatformTextureSettings("Android").overridden, Is.False);
        }

        [Test]
        public void ExactEditorDefaultResourceLoaderRejectsWrongExpectedPath()
        {
            var loader = new EditorDefaultResourceFirstUserPreviewAssetLoader();
            Assert.That(
                loader.TryLoad(
                    FirstUserPreviewAssetContract.FinishedLoadingKey,
                    FirstUserPreviewAssetContract.ChampionHudPath,
                    out Texture2D texture),
                Is.False);
            Assert.That(texture, Is.Null);
        }

        [Test]
        public void ImplementationAssemblyIsDependencyFreeEditorOnlyAndNotAutoReferenced()
        {
            string asmdef = File.ReadAllText(AbsoluteAssetPath(ImplementationAsmdefPath));
            Assert.That(asmdef, Does.Contain("\"name\": \"AL.FirstUserPreview.Editor\""));
            Assert.That(asmdef, Does.Contain("\"references\": []"));
            Assert.That(asmdef, Does.Contain("\"includePlatforms\": ["));
            Assert.That(asmdef, Does.Contain("\"Editor\""));
            Assert.That(asmdef, Does.Contain("\"autoReferenced\": false"));
            Assert.That(asmdef, Does.Not.Contain("AL.Runtime"));
            Assert.That(asmdef, Does.Not.Contain("AL.GameDataCatalog"));
        }

        [Test]
        public void PlayerCompilationGraphExcludesPreviewAssemblyAndSources()
        {
            Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            Assert.That(assemblies.Select(assembly => assembly.name), Does.Not.Contain("AL.FirstUserPreview.Editor"));
            Assert.That(
                assemblies.SelectMany(assembly => assembly.sourceFiles ?? Array.Empty<string>()),
                Has.None.Contains("FirstUserPreview"));
        }

        [Test]
        public void EnabledSceneDependencyClosureExcludesPreviewAssetsAndGuids()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            string[] dependencies = AssetDatabase.GetDependencies(scenes, recursive: true);
            Assert.That(dependencies, Does.Not.Contain(FirstUserPreviewAssetContract.FinishedLoadingPath));
            Assert.That(dependencies, Does.Not.Contain(FirstUserPreviewAssetContract.ChampionHudPath));
            Assert.That(dependencies.Select(AssetDatabase.AssetPathToGUID),
                Does.Not.Contain(FirstUserPreviewAssetContract.FinishedLoadingGuid));
            Assert.That(dependencies.Select(AssetDatabase.AssetPathToGUID),
                Does.Not.Contain(FirstUserPreviewAssetContract.ChampionHudGuid));
        }

        [Test]
        public void RuntimeSourcesContainNoPreviewReference()
        {
            string runtimeRoot = Path.Combine(Application.dataPath, "AL", "Scripts");
            string[] offenders = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).IndexOf("FirstUserPreview", StringComparison.Ordinal) >= 0)
                .ToArray();
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void ViewerSourceContainsNoRuntimeOrProductionAuthorityDependency()
        {
            string source = File.ReadAllText(AbsoluteAssetPath(WindowSourcePath));
            string[] forbidden =
            {
                "ChampionArena",
                "Bootloader",
                "ServiceLocator",
                "ISaveGameService",
                "LocalSaveGameService",
                "RealmId",
                "SceneManager",
                "PlayerPrefs",
                "UnityWebRequest",
                "HttpClient",
                "Analytics",
                "EditorApplication.update",
                ".schedule",
                "delayCall",
                "System.Threading",
                "System.Threading.Tasks"
            };
            foreach (string token in forbidden)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        [Test]
        public void ReferenceInventoryIsExactlyTwoUniqueNonBundledEditorDefaultResources()
        {
            string[] expected =
            {
                FirstUserPreviewAssetContract.FinishedLoadingPath,
                FirstUserPreviewAssetContract.ChampionHudPath
            };
            string absoluteRoot = AbsoluteAssetPath(FirstUserPreviewAssetContract.AssetRoot);
            string[] actual = Directory.GetFiles(absoluteRoot, "*.png", SearchOption.TopDirectoryOnly)
                .Select(ToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(expected.OrderBy(path => path, StringComparer.Ordinal).ToArray(), actual);

            string[] guids = actual.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Assert.That(guids, Is.Unique);
            Assert.That(guids, Has.All.Matches<string>(guid => guid.Length == 32));
            Assert.That(actual.Select(AssetImporter.GetAtPath).Select(importer => importer.assetBundleName),
                Has.All.Null.Or.Empty);
            Assert.That(
                actual.SelectMany(path => AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(path))),
                Is.Empty);
        }

        private static string AbsoluteAssetPath(string assetPath)
        {
            string suffix = assetPath == "Assets"
                ? string.Empty
                : assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(Application.dataPath, suffix));
        }

        private static string ToAssetPath(string absolutePath)
        {
            string normalizedAssets = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            string normalized = absolutePath.Replace('\\', '/');
            return "Assets" + normalized.Substring(normalizedAssets.Length);
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] digest = sha256.ComputeHash(stream);
                char[] output = new char[digest.Length * 2];
                const string alphabet = "0123456789abcdef";
                for (int index = 0; index < digest.Length; index++)
                {
                    output[index * 2] = alphabet[digest[index] >> 4];
                    output[index * 2 + 1] = alphabet[digest[index] & 0x0f];
                }

                return new string(output);
            }
        }
    }

    public static class FirstUserPreviewBuildExclusionCli
    {
        private const string BuildRoot = "Builds/Validation/FirstUserPreview";
        private const string LogRoot = "Logs/FirstUserPreviewBuildExclusion";
        private const string ExpectedUnityVersion = "2022.3.62f3";
        private const string FinishedLoadingSha256 =
            "7797b38f4ae55caeaa66f30b9964bffebdd05d15ad4809359e342ed4c03d5657";
        private const string ChampionHudSha256 =
            "7dc0932c3a4d872e7e1890d0044b801f7f64865ca1d8be5b7e4fd7f8e60eb2cc";

        public static void BuildWindowsDevelopment()
        {
            Build("Windows64-Development", BuildTarget.StandaloneWindows64, true, "AnotherLifeUnity.exe");
        }

        public static void BuildWindowsNonDevelopment()
        {
            Build("Windows64-NonDevelopment", BuildTarget.StandaloneWindows64, false, "AnotherLifeUnity.exe");
        }

        public static void BuildAndroidDevelopment()
        {
            Build("Android-Development", BuildTarget.Android, true, "AnotherLifeUnity.apk");
        }

        public static void BuildAndroidNonDevelopment()
        {
            Build("Android-NonDevelopment", BuildTarget.Android, false, "AnotherLifeUnity.apk");
        }

        private static void Build(string profile, BuildTarget target, bool development, string fileName)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDirectory = Path.GetFullPath(Path.Combine(projectRoot, BuildRoot, profile));
            string outputPath = Path.Combine(outputDirectory, fileName);
            string summaryDirectory = Path.GetFullPath(Path.Combine(projectRoot, LogRoot));
            string summaryPath = Path.Combine(summaryDirectory, profile + ".json");
            ValidatePathsAndInvalidatePriorSummary(
                projectRoot,
                profile,
                outputDirectory,
                summaryDirectory,
                summaryPath);

            if (!Application.isBatchMode)
            {
                throw new BuildFailedException("Player exclusion validation must run in batch mode.");
            }

            if (!string.Equals(Application.unityVersion, ExpectedUnityVersion, StringComparison.Ordinal))
            {
                throw new BuildFailedException(
                    "Unity version must be " + ExpectedUnityVersion + " but was " + Application.unityVersion + ".");
            }

            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                throw new BuildFailedException(
                    "Active build target must be " + target + " but was " + EditorUserBuildSettings.activeBuildTarget + ".");
            }

            PrepareFreshValidationOutput(projectRoot, outputDirectory);
            Directory.CreateDirectory(summaryDirectory);

            VerifyApprovedAsset(FirstUserPreviewAssetContract.FinishedLoadingPath,
                FirstUserPreviewAssetContract.FinishedLoadingGuid, FinishedLoadingSha256, 2376573L);
            VerifyApprovedAsset(FirstUserPreviewAssetContract.ChampionHudPath,
                FirstUserPreviewAssetContract.ChampionHudGuid, ChampionHudSha256, 2488480L);
            if (!string.Equals(
                    typeof(PassiveFirstUserPreviewWindow).Assembly.GetName().Name,
                    "AL.FirstUserPreview.Editor",
                    StringComparison.Ordinal))
            {
                throw new BuildFailedException("Preview window type is not isolated in the approved Editor assembly.");
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("Player exclusion validation requires at least one enabled scene.");
            }

            BuildOptions options = BuildOptions.DetailedBuildReport |
                                   BuildOptions.CleanBuildCache |
                                   BuildOptions.StrictMode |
                                   BuildOptions.NoUniqueIdentifier;
            if (development)
            {
                options |= BuildOptions.Development;
            }

            Assembly[] playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            if (playerAssemblies == null || playerAssemblies.Length == 0)
            {
                throw new BuildFailedException("Player compilation inventory is unexpectedly empty.");
            }

            int playerAssemblyMatches = playerAssemblies.Count(assembly =>
                string.Equals(assembly.name, "AL.FirstUserPreview.Editor", StringComparison.Ordinal) ||
                string.Equals(assembly.name, "AL.FirstUserPreview.Editor.Tests", StringComparison.Ordinal));
            int playerSourceMatches = playerAssemblies
                .SelectMany(assembly => assembly.sourceFiles ?? Array.Empty<string>())
                .Count(source => source.IndexOf("FirstUserPreview", StringComparison.OrdinalIgnoreCase) >= 0);
            if (playerAssemblyMatches != 0 || playerSourceMatches != 0)
            {
                throw new BuildFailedException("Preview Editor code entered the Player compilation graph.");
            }

            bool previousAppBundle = EditorUserBuildSettings.buildAppBundle;
            bool previousExportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            BuildReport report;
            try
            {
                if (target == BuildTarget.Android)
                {
                    EditorUserBuildSettings.buildAppBundle = false;
                    EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                }

                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = target,
                    options = options
                });
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousAppBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExportProject;
            }

            int packedContentCount = 0;
            int previewPathMatches = 0;
            int previewGuidMatches = 0;
            foreach (PackedAssets packedAsset in report.packedAssets ?? Array.Empty<PackedAssets>())
            {
                foreach (PackedAssetInfo content in packedAsset.contents ?? Array.Empty<PackedAssetInfo>())
                {
                    packedContentCount++;
                    string sourcePath = (content.sourceAssetPath ?? string.Empty).Replace('\\', '/');
                    string sourceGuid = content.sourceAssetGUID.ToString();
                    if (sourcePath.StartsWith(FirstUserPreviewAssetContract.AssetRoot + "/", StringComparison.Ordinal))
                    {
                        previewPathMatches++;
                    }

                    if (string.Equals(sourceGuid, FirstUserPreviewAssetContract.FinishedLoadingGuid, StringComparison.Ordinal) ||
                        string.Equals(sourceGuid, FirstUserPreviewAssetContract.ChampionHudGuid, StringComparison.Ordinal))
                    {
                        previewGuidMatches++;
                    }
                }
            }

            BuildFile[] files = report.GetFiles() ?? Array.Empty<BuildFile>();
            int outputFileMatches = files.Count(file =>
            {
                string name = Path.GetFileName(file.path ?? string.Empty);
                return name.IndexOf("AL.FirstUserPreview.Editor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       string.Equals(name, "FinishedLoading.png", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(name, "ChampionHud.png", StringComparison.OrdinalIgnoreCase);
            });
            var artifactRoot = new DirectoryInfo(outputDirectory);
            if (artifactRoot.Exists &&
                ((artifactRoot.Attributes & FileAttributes.ReparsePoint) != 0 || ContainsReparsePoint(artifactRoot)))
            {
                throw new BuildFailedException("Player validation output contains an unexpected reparse point.");
            }

            string[] artifactFiles = artifactRoot.Exists
                ? Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories)
                : Array.Empty<string>();
            int artifactFileMatches = artifactFiles.Count(path =>
            {
                string name = Path.GetFileName(path);
                return string.Equals(name, "AL.FirstUserPreview.Editor.dll", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(name, "AL.FirstUserPreview.Editor.Tests.dll", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(name, "FinishedLoading.png", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(name, "ChampionHud.png", StringComparison.OrdinalIgnoreCase);
            });

            var summary = new FirstUserPreviewBuildExclusionSummary
            {
                profile = profile,
                target = target.ToString(),
                development = development,
                unityVersion = Application.unityVersion,
                result = report.summary.result.ToString(),
                outputPath = outputPath.Replace('\\', '/'),
                options = options.ToString(),
                scenePaths = scenes,
                finishedLoadingPath = FirstUserPreviewAssetContract.FinishedLoadingPath,
                finishedLoadingGuid = FirstUserPreviewAssetContract.FinishedLoadingGuid,
                finishedLoadingSha256 = FinishedLoadingSha256,
                championHudPath = FirstUserPreviewAssetContract.ChampionHudPath,
                championHudGuid = FirstUserPreviewAssetContract.ChampionHudGuid,
                championHudSha256 = ChampionHudSha256,
                totalTime = report.summary.totalTime.ToString("c", CultureInfo.InvariantCulture),
                totalSize = report.summary.totalSize.ToString(CultureInfo.InvariantCulture),
                warningCount = report.summary.totalWarnings,
                errorCount = report.summary.totalErrors,
                packedContentCount = packedContentCount,
                outputFileCount = files.Length,
                artifactFileCount = artifactFiles.Length,
                playerAssemblyCount = playerAssemblies.Length,
                previewPathMatches = previewPathMatches,
                previewGuidMatches = previewGuidMatches,
                outputFileMatches = outputFileMatches,
                artifactFileMatches = artifactFileMatches,
                playerAssemblyMatches = playerAssemblyMatches,
                playerSourceMatches = playerSourceMatches
            };
            File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, prettyPrint: true));

            if (report.summary.result != BuildResult.Succeeded ||
                report.summary.totalErrors != 0 ||
                !File.Exists(outputPath) ||
                packedContentCount == 0 ||
                files.Length == 0 ||
                previewPathMatches != 0 ||
                previewGuidMatches != 0 ||
                outputFileMatches != 0 ||
                artifactFileMatches != 0 ||
                playerAssemblyMatches != 0 ||
                playerSourceMatches != 0)
            {
                throw new BuildFailedException(
                    "First-user preview Player exclusion failed. Summary: " + summaryPath);
            }

            Debug.Log(
                "[AL-FIRST-USER-PREVIEW-BUILD-EXCLUSION] " + profile +
                " succeeded; packed=" + packedContentCount.ToString(CultureInfo.InvariantCulture) +
                "; pathMatches=0; guidMatches=0; outputMatches=0; assemblyMatches=0; sourceMatches=0; summary=" +
                summaryPath);
        }

        private static void ValidatePathsAndInvalidatePriorSummary(
            string projectRoot,
            string profile,
            string outputDirectory,
            string summaryDirectory,
            string summaryPath)
        {
            string expectedOutputParent = Path.GetFullPath(Path.Combine(projectRoot, BuildRoot));
            string actualOutputParent = Directory.GetParent(outputDirectory)?.FullName;
            string expectedSummaryDirectory = Path.GetFullPath(Path.Combine(projectRoot, LogRoot));
            if (!string.Equals(actualOutputParent, expectedOutputParent, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileName(outputDirectory), profile, StringComparison.Ordinal) ||
                !string.Equals(summaryDirectory, expectedSummaryDirectory, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileName(summaryPath), profile + ".json", StringComparison.Ordinal))
            {
                throw new BuildFailedException("Refusing to prepare an unexpected Player validation path.");
            }

            EnsureExistingPathComponentsAreNotReparsePoints(projectRoot, expectedOutputParent);
            EnsureExistingPathComponentsAreNotReparsePoints(projectRoot, outputDirectory);
            EnsureExistingPathComponentsAreNotReparsePoints(projectRoot, summaryDirectory);
            if (File.Exists(summaryPath))
            {
                var summary = new FileInfo(summaryPath);
                if ((summary.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new BuildFailedException("Refusing to invalidate a reparse-point validation summary.");
                }

                File.Delete(summaryPath);
            }
        }

        private static void PrepareFreshValidationOutput(string projectRoot, string outputDirectory)
        {
            EnsureExistingPathComponentsAreNotReparsePoints(projectRoot, outputDirectory);
            if (Directory.Exists(outputDirectory))
            {
                var root = new DirectoryInfo(outputDirectory);
                if ((root.Attributes & FileAttributes.ReparsePoint) != 0 || ContainsReparsePoint(root))
                {
                    throw new BuildFailedException("Refusing to clean a Player validation path containing a reparse point.");
                }

                Directory.Delete(outputDirectory, recursive: true);
            }

            Directory.CreateDirectory(outputDirectory);
        }

        private static void EnsureExistingPathComponentsAreNotReparsePoints(
            string projectRoot,
            string candidatePath)
        {
            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(candidatePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) &&
                !candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new BuildFailedException("Validation path escapes the Unity project root.");
            }

            var current = new DirectoryInfo(root);
            if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new BuildFailedException("Unity project root is missing or is a reparse point.");
            }

            string relative = candidate.Length == root.Length
                ? string.Empty
                : candidate.Substring(root.Length + 1);
            foreach (string segment in relative.Split(
                         new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = new DirectoryInfo(Path.Combine(current.FullName, segment));
                if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new BuildFailedException("Validation path contains a reparse-point ancestor.");
                }
            }
        }

        private static bool ContainsReparsePoint(DirectoryInfo root)
        {
            var pending = new Stack<DirectoryInfo>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                foreach (FileSystemInfo entry in pending.Pop().EnumerateFileSystemInfos())
                {
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return true;
                    }

                    if (entry is DirectoryInfo directory)
                    {
                        pending.Push(directory);
                    }
                }
            }

            return false;
        }

        private static void VerifyApprovedAsset(
            string assetPath,
            string expectedGuid,
            string expectedSha256,
            long expectedBytes)
        {
            string actualGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.Equals(actualGuid, expectedGuid, StringComparison.Ordinal) ||
                !string.Equals(AssetDatabase.GUIDToAssetPath(expectedGuid), assetPath, StringComparison.Ordinal))
            {
                throw new BuildFailedException("Approved preview asset GUID/path mismatch: " + assetPath);
            }

            string absolutePath = Path.Combine(
                Application.dataPath,
                assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
            var file = new FileInfo(absolutePath);
            if (!file.Exists || file.Length != expectedBytes ||
                !string.Equals(ComputeSha256(absolutePath), expectedSha256, StringComparison.Ordinal))
            {
                throw new BuildFailedException("Approved preview asset bytes changed: " + assetPath);
            }
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] digest = sha256.ComputeHash(stream);
                char[] output = new char[digest.Length * 2];
                const string alphabet = "0123456789abcdef";
                for (int index = 0; index < digest.Length; index++)
                {
                    output[index * 2] = alphabet[digest[index] >> 4];
                    output[index * 2 + 1] = alphabet[digest[index] & 0x0f];
                }

                return new string(output);
            }
        }

        [Serializable]
        private sealed class FirstUserPreviewBuildExclusionSummary
        {
            public string profile;
            public string target;
            public bool development;
            public string unityVersion;
            public string result;
            public string outputPath;
            public string options;
            public string[] scenePaths;
            public string finishedLoadingPath;
            public string finishedLoadingGuid;
            public string finishedLoadingSha256;
            public string championHudPath;
            public string championHudGuid;
            public string championHudSha256;
            public string totalTime;
            public string totalSize;
            public int warningCount;
            public int errorCount;
            public int packedContentCount;
            public int outputFileCount;
            public int artifactFileCount;
            public int playerAssemblyCount;
            public int previewPathMatches;
            public int previewGuidMatches;
            public int outputFileMatches;
            public int artifactFileMatches;
            public int playerAssemblyMatches;
            public int playerSourceMatches;
        }
    }
}
