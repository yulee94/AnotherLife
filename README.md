# Another Life (AL)

A high-fantasy kingdom war MMO featuring four unique realms, 2.5D kingdom simulation, and 3D champion-based arena combat.

## Project Structure

This is a unified project repository containing both the Android native shell and the Unity gameplay engine.

- **/app**: The Android native shell built with Jetpack Compose, Navigation 3, and Material 3.
- **/unity**: The core game engine built with Unity 2022.3 (LTS).
- **/gradle**: Android build configuration.

## Getting Started

### 📱 Android (Android Studio)
Open the root directory in Android Studio. The `app` module contains the UI for Kingdom management, Battle simulations, and the native wrapper for the Unity engine.

### 🎮 Unity (Unity Editor)
Open the `/unity` subfolder in the Unity Hub. 
- Use the **`ProjectInitializer`** utility to generate initial ScriptableObject data.
- Run the **`Test.unity`** scene to enter the 3D Champion Arena.

## Core Features
- **Narrative Engine**: Multi-chapter storylines for Dwarves, Elves, Humans, and Dark Elves.
- **Kingdom Simulation**: Isometric 2.5D building management and resource production.
- **Champion Arena**: 3D action combat with orbital camera and skill systems.
- **Warzone Map**: Global territory control and realm-wide conflict simulation.
