using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public class RepresentativeSceneSmokeTests
    {
        [UnityTest]
        public IEnumerator RepresentativeTestSceneLoadsAndRunsWithoutUnexpectedErrors()
        {
            LogAssert.ignoreFailingMessages = false;
            ExpectRepresentativeSceneStartupLogs();

#if UNITY_EDITOR
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Test.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
            Assert.NotNull(load, "Expected Assets/Test.unity to exist for editor-only PlayMode smoke testing.");
#else
            AsyncOperation load = SceneManager.LoadSceneAsync("Test", LoadSceneMode.Single);
            Assert.NotNull(load, "Expected Test scene to be loadable in PlayMode.");
#endif

            while (!load.isDone)
            {
                yield return null;
            }

            Assert.AreEqual("Test", SceneManager.GetActiveScene().name);

            // Give bootstrapping, service registration, and initial scene behaviours time to run.
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.5f);

            LogAssert.NoUnexpectedReceived();
        }

        private static void ExpectRepresentativeSceneStartupLogs()
        {
            LogAssert.Expect(LogType.Log, "[Bootloader] Services missing. Initializing Offline Stack...");
            LogAssert.Expect(LogType.Log, "<color=cyan>[Bootloader] Offline Services Initialized Successfully.</color>");
            LogAssert.Expect(LogType.Log, new Regex(@"Offline progress applied for \d+ seconds\."));
            LogAssert.Expect(LogType.Log, new Regex(@"Game saved safely to .+save\.json\."));
            LogAssert.Expect(LogType.Log, "Game loaded from the primary save.");
            LogAssert.Expect(LogType.Log, "Created Player Champion (Capsule) for 3D Arena.");
            LogAssert.Expect(LogType.Log, "Arena targets spawned. Use WASD, mouse click, and Space to fight.");
            LogAssert.Expect(LogType.Log, "<color=green><b>Welcome to Another Life!</b></color>");
            LogAssert.Expect(LogType.Log, "Press <b>Play</b> in the Unity Editor to start your journey as a Realm Lord.");
            LogAssert.Expect(LogType.Log, "Visualizing Kingdom for Realm: Crownlands");
        }
    }
}
