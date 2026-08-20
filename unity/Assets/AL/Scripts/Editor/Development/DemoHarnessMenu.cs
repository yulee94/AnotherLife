#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace AL.EditorTools
{
    /// <summary>
    /// Explicit editor/dev entry for the retired DemoInitializer greybox. Production play
    /// from Boot never opens this scene.
    /// </summary>
    public static class DemoHarnessMenu
    {
        public const string ScenePath = "Assets/Test.unity";

        [MenuItem("Another Life/Dev/Open Demo Harness")]
        public static void OpenDemoHarness()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
#endif
