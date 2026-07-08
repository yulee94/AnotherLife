using UnityEngine;
using AL.ChampionMode.Control;

public class SceneDoctor : MonoBehaviour
{
    void LateUpdate()
    {
        // 1. Find the Hero
        GameObject hero = GameObject.Find("Player_Champion");
        if (hero == null) return;

        // 2. SAFETY CHECK: If the hero is too high or too low, teleport him back!
        if (hero.transform.position.y > 10 || hero.transform.position.y < -5)
        {
            Debug.LogWarning("HERO LOST IN SPACE! Teleporting back to ground...");
            hero.transform.position = new Vector3(0, 1.1f, 0);

            // If he has a physical brain, we need to reset his velocity too
            var controller = hero.GetComponent<CharacterController>();
            if (controller != null)
            {
                // We briefly disable it to teleport
                controller.enabled = false;
                hero.transform.position = new Vector3(0, 1.1f, 0);
                controller.enabled = true;
            }
        }

        // 3. Find the Camera and FORCE it to follow
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            // We force the camera to be 10 meters behind and 5 meters above the hero
            Vector3 targetPos = hero.transform.position + new Vector3(0, 5, -10);
            mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, targetPos, Time.deltaTime * 5f);
            mainCam.transform.LookAt(hero.transform.position + Vector3.up * 1.5f);

            // Draw the line again so we can see it
            Debug.DrawLine(mainCam.transform.position, hero.transform.position, Color.yellow);
        }
    }
}