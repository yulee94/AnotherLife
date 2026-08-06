using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using AL.Data.Catalogs;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace AL.Tests.EditMode.AndroidBridge
{
    public sealed class AndroidJniPackageContractTests
    {
        private const string ModuleName =
            "com.unity.modules.androidjni";
        private const string ModuleVersion = "1.0.0";
        private const int MaximumPackageDocumentBytes = 256 * 1024;

        public static void CompileTargetAndControlPlayerScriptsFromCommandLine()
        {
            CompilePlayerScripts(BuildTarget.Android);
            CompilePlayerScripts(BuildTarget.StandaloneWindows64);
        }

        [Test]
        public void AndroidJniModuleIsPinnedAsDirectBuiltInDependency()
        {
            var projectRoot =
                Directory.GetParent(Application.dataPath).FullName;
            var manifest = ReadStrictJson(
                Path.Combine(projectRoot, "Packages", "manifest.json"));
            var packageLockPath = Path.Combine(
                projectRoot,
                "Packages",
                "packages-lock.json");
            var packageLock = ReadStrictJson(packageLockPath);

            var manifestDependencies = RequireProperty(
                manifest,
                "dependencies");
            AssertStrictString(
                RequireProperty(manifestDependencies, ModuleName),
                ModuleVersion);

            var lockDependencies = RequireProperty(
                packageLock,
                "dependencies");
            var moduleLock = RequireProperty(
                lockDependencies,
                ModuleName);
            AssertStrictString(
                RequireProperty(moduleLock, "version"),
                ModuleVersion);
            AssertStrictNumber(
                RequireProperty(moduleLock, "depth"),
                "0");
            AssertStrictString(
                RequireProperty(moduleLock, "source"),
                "builtin");
            AssertEmptyStrictObject(
                RequireProperty(moduleLock, "dependencies"));
            Assert.That(
                TryGetProperty(moduleLock, "url"),
                Is.Null,
                "A built-in module must not resolve through a registry URL.");
            Assert.That(
                HasExactNumericZeroDepth(
                    File.ReadAllBytes(packageLockPath)),
                Is.True,
                "Package-lock depth must be an unquoted numeric 0 token.");
        }

        [Test]
        public void AndroidJniLockRejectsQuotedDepthToken()
        {
            var quotedDepth = Encoding.UTF8.GetBytes(
                "{\"dependencies\":{\"" + ModuleName +
                "\":{\"version\":\"1.0.0\",\"depth\":\"0\"," +
                "\"source\":\"builtin\",\"dependencies\":{}}}}");

            Assert.That(
                HasExactNumericZeroDepth(quotedDepth),
                Is.False);
        }

        private static object ReadStrictJson(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.That(
                bytes.Length,
                Is.LessThanOrEqualTo(MaximumPackageDocumentBytes));
            return ParseStrictJson(bytes);
        }

        private static void CompilePlayerScripts(BuildTarget target)
        {
            var projectRoot =
                Directory.GetParent(Application.dataPath).FullName;
            var outputDirectory = Path.Combine(
                projectRoot,
                "Temp",
                "Issue135PlayerScripts",
                target + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputDirectory);

            PlayerBuildInterface.CompilePlayerScripts(
                new ScriptCompilationSettings
                {
                    group = BuildPipeline.GetBuildTargetGroup(target),
                    target = target,
                    options = ScriptCompilationOptions.None
                },
                outputDirectory);
            Debug.Log(
                "[AL-ANDROID-JNI-COMPILE] target=" + target +
                " output=" + outputDirectory);
        }

        private static bool HasExactNumericZeroDepth(byte[] json)
        {
            try
            {
                var root = ParseStrictJson(json);
                var dependencies = TryGetProperty(root, "dependencies");
                var module = TryGetProperty(dependencies, ModuleName);
                var depth = TryGetProperty(module, "depth");
                var kind = ReadProperty(depth, "Kind");
                var rawValue = ReadProperty(depth, "RawValue") as string;

                return string.Equals(
                           kind?.ToString(),
                           GameDataValueKind.Number.ToString(),
                           StringComparison.Ordinal) &&
                       string.Equals(
                           rawValue,
                           "0",
                           StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static object ParseStrictJson(byte[] json)
        {
            var strictJsonType = typeof(GameDataCatalogContract)
                .Assembly.GetType(
                    "AL.Data.Catalogs.StrictJsonDocument",
                    true);
            var parse = strictJsonType.GetMethod(
                "Parse",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (parse == null)
            {
                throw new MissingMethodException(
                    strictJsonType.FullName,
                    "Parse");
            }

            return parse.Invoke(
                null,
                new object[]
                {
                    json,
                    MaximumPackageDocumentBytes
                });
        }

        private static object RequireProperty(
            object parent,
            string propertyName)
        {
            var value = TryGetProperty(parent, propertyName);
            Assert.That(value, Is.Not.Null, propertyName);
            return value;
        }

        private static void AssertStrictString(
            object value,
            string expected)
        {
            AssertStrictKind(value, GameDataValueKind.String);
            Assert.That(
                ReadProperty(value, "Value"),
                Is.TypeOf<string>().And.EqualTo(expected));
        }

        private static void AssertStrictNumber(
            object value,
            string expectedRawValue)
        {
            AssertStrictKind(value, GameDataValueKind.Number);
            Assert.That(
                ReadProperty(value, "RawValue"),
                Is.TypeOf<string>().And.EqualTo(expectedRawValue));
        }

        private static void AssertEmptyStrictObject(object value)
        {
            AssertStrictKind(value, GameDataValueKind.Object);
            var properties = ReadProperty(value, "Properties") as
                IEnumerable;
            Assert.That(properties, Is.Not.Null);

            var count = 0;
            foreach (var ignored in properties)
            {
                count++;
            }

            Assert.That(count, Is.Zero);
        }

        private static void AssertStrictKind(
            object value,
            GameDataValueKind expected)
        {
            Assert.That(value, Is.Not.Null);
            Assert.That(
                ReadProperty(value, "Kind")?.ToString(),
                Is.EqualTo(expected.ToString()));
        }

        private static object TryGetProperty(
            object parent,
            string propertyName)
        {
            if (parent == null)
            {
                return null;
            }

            var tryGet = parent.GetType().GetMethod(
                "TryGet",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (tryGet == null)
            {
                return null;
            }

            var arguments = new object[] { propertyName, null };
            return (bool)tryGet.Invoke(parent, arguments)
                ? arguments[1]
                : null;
        }

        private static object ReadProperty(
            object source,
            string propertyName)
        {
            for (var type = source?.GetType();
                 type != null;
                 type = type.BaseType)
            {
                var property = type.GetProperty(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property.GetValue(source);
                }
            }

            return null;
        }

    }
}
