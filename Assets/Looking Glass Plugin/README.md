# Looking Glass Unity Plugin 4.0-alpha

This is a new, lightweight plugin for the Looking Glass, written from the ground up specifically for Unity 6 using the Universal Render Pipeline. It takes advantage of the latest Unity features to enable extremely fast multi-view rendering.

## Requirements

- Looking Glass
- Unity 6+
- Universal Render Pipeline
  - Using Forward or Forward+ rendering
  - RenderGraph enabled
- Unity’s iOS build module (required for iOS builds)

_Looking Glass Unity Plugin 4.0 will not work with HDRP, and prior versions of Unity are not supported._

## Getting Started

The zip includes:

- a `com.unity.render-pipelines.core` folder
- a `com.unity.render-pipelines.universal` folder
- a `jp.keijiro.klak.syphon` folder
- a `jp.keijiro.klak.spout` folder
- the `Looking Glass Unity Plugin 4.0-alpha.unitypackage` file
- this readme

First, make sure your project meets the requirements above, and make sure your project is set to build for iOS if required. If not, upgrade your editor version and render pipeline.

Start by closing your project—errors may occur if you do this while it is open. Copy the `com.unity.render-pipelines.core`, `com.unity.render-pipelines.universal`, `jp.keijiro.klak.syphon`, and `jp.keijiro.klak.spout` folders to `YourProjectFolder/Packages/`. Unity will automatically switch to using these package versions.

Open your project, then go to the menu bar and select `Assets > Import Package > Custom Package`. Choose the `Looking Glass Unity Plugin 4.0-alpha.unitypackage`.

This will import all plugin files into your project, including the `Multiview RP Asset`. There are multiple places Unity stores the render pipeline settings, but these can be quickly updated using the Project Setup tool.

In the project folder, select the `Looking Glass Plugin/Settings/Project Setup` object. In the Inspector, it will list locations that require the render pipeline setting, with buttons to set each one automatically. Click each button to configure the project.

Open the `Looking Glass Plugin/Scenes/Multiview Example Scene` to check if everything is working.

## Using the Plugin

The easiest way to begin is to duplicate the `Multiview Example Scene` and edit its content. The only objects you must keep are the `Hologram Container` under the Orbit Controls object and the `Final Pass Canvas`, which handle rendering 3D content to the Looking Glass. The latter can be hidden to avoid scene clutter.

### Warnings

Do not add your assets to the `Looking Glass Plugin` folder or modify assets or prefabs in it. Upgrading the plugin involves deleting and replacing this folder, and your changes will be lost.

### Live Preview

- Live Preview App
- [Looking Glass Bridge](https://look.glass/bridge)

Live Preview is a standalone app and must be downloaded separately. The apps can be downloaded [here](https://www.notion.so/Looking-Glass-Unity-Plugin-4-0-alpha-User-Guide-1af034f484fb8010832ed2121fc07ba7?pvs=21).

To use Live Preview, ensure Looking Glass Bridge is installed and running. Connect your Looking Glass to your computer using HDMI or USB-C. A green LED should light up; press the adjacent button.

The Looking Glass should now appear as a second display. If using a Looking Glass 16" Portrait device and it appears sideways, set its orientation to 270° (or Portrait Flipped on Windows).

In Unity, make sure you have a `Klak Preview Sender` object in your scene. The `Multiview Example Scene` already includes one; otherwise, add it from `Looking Glass Plugin/Prefabs`.

Open the `lkg-preview-win` or `lkg-preview-mac` app you downloaded above. It will automatically receive and display Looking Glass content from your editor.

If Live Preview remains black or does not load:

- Enter play mode in the Editor
- Make sure Bridge is running—click the Bridge tray icon, go to your display, and click Show Debug View to check
- Restart the Unity editor

Controls:
Esc → Quit

## Calibration Loading

When running an iOS build, the first time it runs on an iPhone or iPad connected to a Looking Glass, you will be prompted to select the calibration (`visual.json`) file from the Looking Glass storage. This appears as a volume starting with `LKG`. After completion, calibration is written to iOS storage and need not be repeated unless moving to a different Looking Glass.

## Upgrading the Plugin

Upgrading the plugin involves deleting the `com.unity.render-pipelines.core`, `com.unity.render-pipelines.universal`, and `Looking Glass Plugin` folders, then repeating the Getting Started steps. It is **highly recommended** to make a backup or commit to version control before upgrading.

