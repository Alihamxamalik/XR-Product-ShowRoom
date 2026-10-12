# Setup and build

What the project needs, what setup does to it, how to build, how to do all of that from the command line, and what to do when a step goes wrong.

## What the project needs

- Unity **6000.0.81f1** with the Android Build Support module, including its SDK, NDK and OpenJDK.
- A Meta Quest 3 or 3S in developer mode, for device builds.

The packages are declared in `Packages/manifest.json` and pinned in `packages-lock.json`:

| Package | Version | Used for |
|---|---|---|
| Meta XR Core SDK (`com.meta.xr.sdk.core`) | 207.0.0 | Camera rig, passthrough, spatial anchors, the Depth API |
| Meta XR Interaction SDK (`com.meta.xr.sdk.interaction`, `.interaction.ovr`) | 207.0.0 | Grab, poke and ray, with hands and controllers |
| Meta MR Utility Kit (`com.meta.xr.mrutilitykit`) | 207.0.0 | Reading the scanned room, sample rooms for the editor |
| OpenXR (`com.unity.xr.openxr`) | 1.16.1 | The XR provider |
| Unity OpenXR Meta (`com.unity.xr.meta-openxr`) | 2.5.1 | The depth texture the Depth API needs under OpenXR. Brings AR Foundation 6.5.0 and Composition Layers 2.5.0 with it |
| XR Plug-in Management (`com.unity.xr.management`) | 4.5.4 | Loader settings |
| Universal RP | 17.0.4 | Rendering |
| Input System | 1.19.0 | The editor's mouse and keyboard controls |
| Test Framework | 1.6.0 | The play-mode tests |

The Meta packages come from Meta's own registry, `https://npm.developer.oculus.com`, declared as a scoped registry in the manifest. Unity may show a notice about it the first time the project opens.

## Setup

A clone has the files but not everything a machine needs: the active build target, for one, is stored per machine. Setup closes that gap. It is offered in a dialog when the project opens and is also at **MR Showroom > Setup > Run Full Setup**. In order, it:

1. Switches the build target to Android.
2. Imports TextMeshPro's essential resources, if they are missing.
3. Applies the project settings listed below.
4. Runs Meta's Project Setup Tool, **Fix All** and **Apply All** for Android, twice, because some fixes only become possible after others have landed. It then puts back the shadow and OpenXR settings this project depends on, in case the tool changed them, and regenerates the Android manifest if it is out of date.
5. Creates any brand assets, prefabs and scenes that are missing, registers Bootstrap and Showroom as the build's scenes, and opens Showroom.

Several of those steps make Unity recompile and reload scripts. The pipeline remembers where it was and carries on after each reload. Every step only sets values or creates what is missing, so the whole thing is safe to run again.

Setup is offered when the Showroom scene is missing, when the settings haven't been applied, or when the two scenes aren't registered as the build's scenes. The last is how a setup that was cut short shows, because registering them is the final step.

### Menu

| MR Showroom > | What it does |
|---|---|
| Setup > Run Full Setup | Everything above. Keeps scenes and prefabs that already exist |
| Setup > Apply Project Settings Only | Step 3 on its own |
| Setup > Rebuild Scenes and Prefabs (overwrites) | Recreates both scenes, the sneaker's meshes and prefab, its box, and the hotspot, label and hint prefabs from code. Keeps data assets, materials and textures |
| Product > Create Product From Selected Model | See [Products](Products.md) |
| Build > Development APK, Release APK | See [Building](#building) |
| Dev > Render Previews | Renders the product and the panels to a `Previews` folder. See [Testing](Testing.md) |

### Settings that setup applies

`ProjectConfigurator` holds all of these.

**Player**

| Setting | Value | Why |
|---|---|---|
| Company, product, version | Ali Hamza, MR Product Showroom, 0.1.0 | |
| Package id | `com.alihamza.mrshowroom` | |
| Colour space | Linear | |
| Scripting backend, architecture | IL2CPP, ARM64 | Required for Quest |
| Minimum Android API | 29 or higher | Meta's tool raises it to 32. The target API is left to Meta's tool, which currently sets 34 |
| Graphics API | Vulkan only | The Depth API requires it |
| Texture compression | ASTC | |

**Quality and URP**, on the quality level Android uses

| Setting | Value | Why |
|---|---|---|
| MSAA | 4x | Nothing turns it on by default |
| HDR, depth texture, opaque texture | Off | Not needed, and each costs frame time. Post-processing is also off, on the camera |
| Main light shadows | On, 2048, 3 m, one cascade, soft | The product's shadow is the only real-time shadow, and it is always close |
| Additional lights | Off | One directional light |

**XR**

| Setting | Value | Why |
|---|---|---|
| Provider | OpenXR, on Android and desktop | |
| Feature group | Meta XR, with foveation and subsampled layout | |
| Unity OpenXR Meta features | Session and Occlusion only | They supply the depth texture. Passthrough, anchors and the room scan go through Meta's SDK, and Unity's equivalents stay off so the two don't compete |
| Composition Layers Support | On | OpenXR fails the build without it when the Composition Layers package is present |
| Render mode, Android | Single Pass Instanced | Both eyes in one pass. Also required by the Depth API |

**Meta project config**, which ends up in the Android manifest

| Setting | Value |
|---|---|
| Hand tracking | Controllers and hands, high frequency |
| Passthrough | Required |
| Scene support | Required |
| Anchor support | Enabled |
| Boundary visibility | Supported, and suppressed at runtime while passthrough is on |
| System splash screen | Contextual passthrough: the room stays visible while the app loads |
| Target devices | Quest 3, Quest 3S |

The app asks for the spatial data permission itself, with a line explaining why. OVRManager's option to request it on startup is left off on purpose.

## Generated content

The scenes, the prefabs and the stand-in art are created by the builders in `Scripts/Editor`, and they are committed like any other asset, so a clone opens without a rebuild.

- **Kept on every run:** the product definition, the brand theme, the tuning asset, materials and textures. Values tuned in the inspector survive.
- **Recreated by a rebuild:** both scenes, the sneaker's meshes, and all the prefabs. Manual edits to those are lost.

`SceneBuilder.ContentVersion` is raised when the builders change in a way existing scenes need to pick up. The version a project was built with is stored in `Assets/_Project/Scenes/.content-version`, and a project built by an older version is offered a rebuild when it opens.

## Building

| Menu item | Output | Notes |
|---|---|---|
| MR Showroom > Build > Development APK | `Builds/MRShowroom-dev.apk` | Profiler support, and the frame-rate overlay in the headset |
| MR Showroom > Build > Release APK | `Builds/MRShowroom.apk` | |

Both build the scenes registered by setup and print the install command when they finish:

```sh
adb install -r Builds/MRShowroom-dev.apk
```

File > Build And Run works as usual. On the Apple M1 this was developed on, the first build takes about eleven minutes and later ones about two.

Things to know before a store submission:

- The build is signed with Unity's debug key unless a keystore is set in the Player settings. Keystores are ignored by git.
- A development build carries the `INTERNET` permission and, through a developer-tool layer in the Meta SDK, `FOREGROUND_SERVICE` and `FOREGROUND_SERVICE_MEDIA_PROJECTION`. A release build's manifest hasn't been inspected yet.
- Run Meta's checks in Edit > Project Settings > Meta XR and clear anything outstanding.

## Command line

All of these need the project to be closed in the editor: a project can be open in only one Unity at a time.

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.0.81f1/Unity.app/Contents/MacOS/Unity

# Setup. -keepContent leaves existing scenes and prefabs alone, as the dialog does.
# Without it, they are recreated from code.
"$UNITY" -batchmode -projectPath . -logFile setup.log \
  -executeMethod MRShowroom.EditorTools.SetupPipeline.RunBatch -keepContent

# Play-mode tests.
"$UNITY" -batchmode -projectPath . -logFile tests.log \
  -runTests -testPlatform PlayMode -testResults results.xml

# An APK. Add -release for a release build, -buildOut <path> to choose where it goes.
"$UNITY" -batchmode -projectPath . -logFile build.log \
  -executeMethod MRShowroom.EditorTools.BuildTools.BuildBatch

# Preview images of the product and the panels.
"$UNITY" -batchmode -projectPath . -logFile previews.log \
  -executeMethod MRShowroom.EditorTools.PreviewRenderer.RenderBatch -previewOut Previews
```

- None of them needs `-quit`; each exits by itself, with a non-zero code on failure.
- Don't add `-nographics` to the previews, or to a test run that takes screenshots. Both render.
- Unity sometimes crashes while shutting down after a successful run and returns 134. If the log says the work succeeded, it did.

## Troubleshooting

| Symptom | What to do |
|---|---|
| The setup dialog didn't appear | Run **MR Showroom > Setup > Run Full Setup** |
| Unity closed or crashed part-way through setup | Reopen the project. Setup is offered again, and every step is safe to repeat. If it happens again at the same step, see the known issue below |
| "Could not switch to Android" | The Android Build Support module isn't installed for this Unity version. Add it in Unity Hub |
| "TextMeshPro resources did not finish importing" | Window > TextMeshPro > Import TMP Essential Resources, then run setup again |
| A warning that Meta's Project Setup Tool could not be run automatically | A newer SDK moved the entry point. Open Edit > Project Settings > Meta XR and press Fix All, then Apply All, on the Android tab. Then run **Apply Project Settings Only** |
| Play stops with "Scene 'Showroom' couldn't be loaded because it has not been added to the build settings" | Setup didn't finish. Run it |
| Build fails with "The Composition Layers Support feature is required" | **Apply Project Settings Only** switches it back on |
| A dialog says the scenes were built by an older version of the builders | Choose Rebuild, unless you have hand edits to save first |
| A warning that the interaction rig has no "Locomotor" object | A newer Interaction SDK changed its rig. Check in the Showroom scene that thumbstick and microgesture locomotion are off |
| Setup stops with "TextMeshPro has no default font" | Import the essential resources as above, then run setup again |

### Known issue: the editor crashes during the Meta step

On the Mac this project was built on (Apple M1, macOS 26.6), the editor crashed twice on 4 October 2026 while setup was running from the dialog. Both crashes came a few seconds into the Meta Project Setup Tool step, and both were inside the Metal graphics driver, on Unity's rendering thread. The cause isn't known.

The command-line route doesn't draw the editor's windows, and it completed without trouble on a copy of the project. Close Unity and run, from the project folder:

```sh
/Applications/Unity/Hub/Editor/6000.0.81f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . \
  -logFile setup.log -executeMethod MRShowroom.EditorTools.SetupPipeline.RunBatch -keepContent
```

It takes a few minutes and ends with `[MR Showroom] Setup finished` in `setup.log`. Open the project afterwards: with setup complete, the dialog doesn't appear and the Meta step doesn't run in the editor.

## Version control

`.gitignore` keeps `Library`, `Temp`, `Logs`, `UserSettings`, `Builds`, `Previews`, APKs and keystores out of the repository.

`.gitattributes` sends models, textures, audio and fonts through Git LFS. Install it before the first commit. Without it git stores those files as ordinary blobs, and moving them to LFS afterwards means rewriting history:

```sh
brew install git-lfs
git lfs install
```
