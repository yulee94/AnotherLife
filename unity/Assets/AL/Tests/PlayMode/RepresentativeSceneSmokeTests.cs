using System.Collections;
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
    }
}
