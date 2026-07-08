using UnityEngine;
using UnityEngine.UI;
using AL.Core;
using AL.Core.Interfaces;

namespace AL.Utilities
{
    public class DemoInitializer : MonoBehaviour
    {
        private void Start()
        {
            // 0. Ensure Services are initialized (Plug-and-Play)
            Bootloader.InitializeIfMissing();

            SetupDemoScene();
            Debug.Log("<color=green><b>Welcome to Another Life!</b></color>");
            Debug.Log("Press <b>Play</b> in the Unity Editor to start your journey as a Realm Lord.");
        }

        private void SetupDemoScene()
        {
            // 0. Build Kingdom Visuals
            gameObject.AddComponent<AL.Kingdom.Visuals.KingdomVisualizer>();

            // 1. Ensure Player exists
            GameObject player = GameObject.Find("Player_Champion");
            if (player == null)
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player_Champion";
                player.transform.position = new Vector3(0, 1, 0);

                // Add basic components for the 3D mode
                player.AddComponent<AL.ChampionMode.Control.ChampionController>();
                player.AddComponent<AL.ChampionMode.Control.ChampionCombat>();

                // Add a material color to player
                player.GetComponent<Renderer>().material.color = Color.blue;

                Debug.Log("Created Player Champion (Capsule) for 3D Arena.");
            }

            // 2. Setup Camera
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.GetComponent<AL.ChampionMode.Camera.CameraFollow>() == null)
            {
                var follow = mainCam.gameObject.AddComponent<AL.ChampionMode.Camera.CameraFollow>();
                Debug.Log("Attached CameraFollow to Main Camera.");
            }

            // 3. Create a simple Debug UI for Resources
            CreateDebugUI();
        }

        private void CreateDebugUI()
        {
            GameObject canvasObj = new GameObject("DebugUI_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject textObj = new GameObject("ResourceText");
            textObj.transform.SetParent(canvasObj.transform);
            Text text = textObj.AddComponent<Text>();
            // Fallback font handling for modern Unity versions
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            text.fontSize = 24;
            text.color = Color.yellow;
            text.text = "Initializing Kingdom State...";

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -20);
            rect.sizeDelta = new Vector2(400, 100);

            // Update text in a simple loop
            StartCoroutine(UpdateResourceText(text));
        }

        private System.Collections.IEnumerator UpdateResourceText(Text text)
        {
            while (true)
            {
                var resources = ServiceLocator.Get<IResourceService>();
                if (resources != null)
                {
                    text.text = $"<b>RESOURCES</b>\n" +
                               $"Food: {resources.GetResourceCount(ResourceType.Food)}\n" +
                               $"Wood: {resources.GetResourceCount(ResourceType.Wood)}\n" +
                               $"Stone: {resources.GetResourceCount(ResourceType.Stone)}\n" +
                               $"Gold: {resources.GetResourceCount(ResourceType.Gold)}";
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
