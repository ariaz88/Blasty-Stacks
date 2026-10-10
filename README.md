# Blasty-Stacks

**Blasty-Stacks** (also developed as *Stacky Warriors 2D*) is an open Unity 2D Android game that combines grid-based block placement with tower-defense and roguelite auto-battler systems.

The project is in active development. An initial Android build has been produced for testing; this repository currently contains the source project rather than a downloadable release.

## Game concept

Place pieces on a grid to create matches, spawn and strengthen units, and defend against enemy waves. Between combat encounters, choose roguelite-style upgrades and progress through stages.

## Current systems

- Grid-based puzzle board and piece placement
- Match resolution connected to player-unit spawning
- Automated player and enemy combat
- Roguelite skill-card upgrades
- Unit, currency, stage, and progression systems
- Persistent local save data using Unity PlayerPrefs
- Main menu, unit-management, HUD, win/lose, and reward UI

## Built with

- **Unity:** 6000.3.21f1 (Unity 6)
- **Render pipeline:** Universal Render Pipeline (URP)
- **Target platform:** Android
- **Language:** C#

## Open the project

1. Clone or download this repository.
2. Open it in Unity Hub with **Unity 6000.3.21f1**.
3. Open `Assets/Scenes/MenuScene.unity` for the main menu, or `Assets/Scenes/StarterScene.unity` for gameplay.
4. Press Play in the Unity Editor.

## Build for Android

1. In Unity, open **File > Build Settings**.
2. Select **Android** and switch the active platform if needed.
3. Configure your device and signing settings.
4. Build and run on an Android device.

> A public APK/release is not published yet. When a stable public build is ready, it will be added through GitHub Releases.

## Contributing

Contributions, feedback, bug reports, and gameplay ideas are welcome.

- Check existing issues before opening a new one.
- Use a clear issue title and include reproduction steps for bugs.
- For code changes, fork the repository and open a focused pull request.
- Keep changes scoped, explain what you changed, and test the relevant Unity scene before submitting.

If you are learning Unity, the project is also intended to be a readable example of combining puzzle, combat, progression, and UI systems in one mobile-game project.

## License

The project-authored source code and documentation are released under the [MIT License](LICENSE). Third-party Unity packages, artwork, audio, fonts, and other assets may be subject to their own licenses and terms.
