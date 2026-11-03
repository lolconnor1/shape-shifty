# Shape-Shifty

A low poly 3D game with multiple levels and cutscenes, made for Game Off 2026 on itch.io.

## Getting started

1. Install **Unity 6000.0.23f1** through Unity Hub, with **WebGL Build Support**.
2. Install [Git LFS](https://git-lfs.com/), then clone:
   ```
   git lfs install
   git clone https://github.com/lolconnor1/shape-shifty.git
   ```
3. Open the folder in Unity Hub (**Add → Add project from disk**).

## Project layout

Everything we make lives in `Assets/_Project/`:

| Folder | What goes there |
|---|---|
| `Scenes/Levels` | One scene per level (`Level_01`, `Level_02`, …) |
| `Scenes/Cutscenes` | Cutscene scenes |
| `Timelines` | Timeline assets that drive the cutscenes |
| `Scripts` | C# gameplay code |
| `Prefabs` | Reusable objects |
| `Art/Models`, `Art/Materials`, `Art/Textures` | Low poly models and their materials and textures |
| `Audio/Music`, `Audio/SFX` | Sound |
| `UI` | Menus and HUD |

## Tech

- Universal Render Pipeline (URP)
- Timeline + Cinemachine 3 for cutscenes and cameras
- ProBuilder for blocking out levels
- Input System
- uGUI + TextMeshPro for UI

## Builds

- **WebGL** for playing in the browser on itch.io. It's gzip-compressed with decompression fallback turned on, so it works on itch.io's hosting.
- **Windows** for the downloadable build.

## Working together

- Pull before you start, and commit often.
- Avoid editing the same scene at the same time as someone else. Scene merges are painful. Use prefabs for the parts you're working on.
- Always commit `.meta` files together with their assets.
