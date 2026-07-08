using UnityEngine;

namespace AL.Utilities
{
    public class WorldBuilder : MonoBehaviour
    {
        public void BuildWorld()
        {
            Debug.Log("<color=cyan>[WorldBuilder] Initializing Vertical Slice World...</color>");

            // 1. Create a Directional Light if missing
            if (GameObject.FindObjectOfType<Light>() == null)
            {
                Debug.Log("[WorldBuilder] Spawning Directional Light...");
                GameObject lightObj = new GameObject("Directional Light");
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            // 2. Create the Floor
            if (GameObject.Find("World_Floor") == null)
            {
                Debug.Log("[WorldBuilder] Spawning Floor...");
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "World_Floor";
                floor.transform.localScale = new Vector3(10, 1, 10);

                // Explicitly ensure collider is present (default for primitive plane)
                if (floor.GetComponent<Collider>() == null)
                    floor.AddComponent<MeshCollider>();

                Renderer floorRenderer = floor.GetComponent<Renderer>();
                floorRenderer.material.color = new Color(0.2f, 0.5f, 0.2f);
            }

            // 3. Create or Update Player
            GameObject player = GameObject.Find("Player_Champion");
            if (player == null)
            {
                Debug.Log("[WorldBuilder] Spawning Hero (Champion)...");
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player_Champion";
                // Note: Standard tags like 'Player' must be defined in the Editor first.
                player.transform.position = new Vector3(0, 1.1f, 0);

                player.AddComponent<AL.ChampionMode.Control.ChampionController>();
                player.AddComponent<AL.ChampionMode.Control.ChampionCombat>();

                player.GetComponent<Renderer>().material.color = Color.blue;
            }
            else
            {
                Debug.Log("[WorldBuilder] Hero already exists, resetting position...");
                player.transform.position = new Vector3(0, 1.1f, 0);
            }

            // 4. Scatter Target Dummies
            Debug.Log("[WorldBuilder] Scattering Target Dummies...");
            for (int i = 0; i < 8; i++)
            {
                string dummyName = $"Dummy_{i}";
                if (GameObject.Find(dummyName) != null) continue;

                GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dummy.name = dummyName;

                float x = Random.Range(-30f, 30f);
                float z = Random.Range(-30f, 30f);
                dummy.transform.position = new Vector3(x, 0.5f, z);

                dummy.GetComponent<Renderer>().material.color = Color.red;
            }

            Debug.Log("<color=green>[WorldBuilder] World Build Complete!</color>");
        }
    }
}
