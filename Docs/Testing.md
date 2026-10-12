# Testing

What is checked automatically, how to look at the experience without a headset, and what still has to be checked on one.

## Automated tests

Nineteen play-mode tests live in `Assets/_Project/Tests/PlayMode`.

| Class | Tests | What it checks |
|---|---|---|
| `SurfaceMathTests` | 9 | The rules for choosing a table and a spot on it, with no room and no headset: the nearest table wins, measured to its edge; tables at the wrong height or too far away are ignored; a small table gets its centre; a large one keeps the product within reach; the product faces the right way; the pedestal comes out at waist height |
| `PlacementTests` | 4 | The real Showroom scene against MRUK's sample rooms: with one table the product lands on it, with several it lands on the nearest, with none it goes on a pedestal on the floor, and a ray pointed at the floor finds a surface facing up |
| `ShowroomFlowTests` | 5 | A launch from the Bootstrap scene places the product on the sample room's desk and plays the hook; the guided flow runs from Inspect to free exploration and restarts; the two-hand pull opens the exploded view only past its threshold, and pushing two parts together closes it; every hotspot card opens in front of the product; the analytics record matches what happened |
| `PhaseScreenshots` | 1 | Not a test of behaviour. It saves pictures, and is skipped unless asked for (see below) |

The scene tests load the real scenes, drive the product through `GrabTarget` and the panels through their buttons, and the flow tests run at eight times normal speed. With no headset attached the XR SDK logs errors of its own, so only exceptions and failed assertions count as failures.

They catch broken wiring: missing references, components that throw, a flow that stalls, a rule that picks the wrong table. They can't check tracking, rendering, real hand or controller input, passthrough, permissions, anchors or frame rate.

### Running them

In the editor: Window > General > Test Runner, the PlayMode tab, Run All.

From the command line, with the project closed in the editor:

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.0.81f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -projectPath . -logFile tests.log \
  -runTests -testPlatform PlayMode -testResults results.xml
```

## Looking at it without a headset

### A picture of every phase

`PhaseScreenshots` plays the flow and saves two pictures at each of thirteen moments, one from where the user stands (`_eye`) and one from over their shoulder (`_wide`). It runs only when the environment variable `MRSHOWROOM_SHOTS` names a folder, which in practice means from the command line:

```sh
MRSHOWROOM_SHOTS="$PWD/Previews/phases" "$UNITY" -batchmode -projectPath . -logFile shots.log \
  -runTests -testPlatform PlayMode -testFilter MRShowroom.Tests.PhaseScreenshots -testResults shots.xml
```

The moments are: the box dropping, the product rising, the "Pick it up" hint, the hotspot dots, a hotspot card for the nearest dot and for the furthest, the colour panel, a colour mid-sweep, the Night Run colourway, the "Pull it apart" hint, the exploded view, the reveal walk and the end card.

Look at these after any change to layout, labels, hints or lighting. Two real bugs were found this way that no assertion would have caught: a label pointing at the wrong part, and a hotspot card opening inside the sneaker. The pictures in this documentation are crops of the `_wide` ones.

### Preview renders

**MR Showroom > Dev > Render Previews** renders the product from five angles, a second colourway, the exploded view, the box, the ghost hands and each panel to a `Previews` folder next to `Assets`, without entering play mode. It is the quick check after changing generated art.

### Sample rooms

Without a headset the product is placed in one of MRUK's sample rooms. To try another, select `MixedReality/Services` in the Showroom scene and change `Editor Room` on its `Surface Finder`: 0 is an office with one desk, 1 a living room with more than one table, 2 a bedroom with none, and -1 loads no room, which exercises the pedestal fallback. The M key then lets you place by pointing with the mouse.

## On the headset

Nothing has been run on a device yet. This is the design document's test checklist, with what the editor already covers and what to look for.

| Check | Covered in the editor | What to look for on the headset |
|---|---|---|
| The product lands on the right table in a room with one, two and no tables | Yes, with sample rooms | The same in real rooms, including a desk against a wall and a low coffee table, which should be ignored |
| Room access refused | No. The editor always has access | A pedestal about 60 cm in front. **Move** then works by touching a surface and pinching |
| Room never scanned | No | "Point and pinch", with **Scan my room** and **Place in front** on the card. The scan button opens the headset's room setup, and the product is placed when it returns |
| The product stays on the table after recentring | No | Hold the Meta button to recentre. The product should not move with the view |
| Hands hide the product when reaching round it | No | A hand in front covers the sneaker with a soft edge, and the sneaker doesn't flicker where it meets the table |
| Colour sweep, explode and reassemble work with hands and with controllers | The logic, yes. Real input, no | Every swatch and button responds to a poke and to a ray. The pull-apart needs 25 cm and doesn't fire while resizing in the Inspect phase |
| A dropped product returns to the table | The spring, yes | Let go from anywhere, and with tracking lost mid-hold. It should glide home, not fall |
| 90 fps in the busiest moment, the exploded view | No | See [Performance](#performance) |
| Works in dim and bright rooms | No | The sneaker shouldn't glow in a dim room or go dull in a bright one. The light and ambient colours are set in `SceneBuilder.BuildLight` and `ApplyLighting` |
| Pause and resume | No | Take the headset off, and open the Meta menu. Timers, animation and sound stop, and carry on afterwards |
| Five first-time users finish without help | No | Watch them, then read the analytics: `explodeByGuide` and `reachedEndCard` |

Also worth checking in that first session, because they are specific to how this project is built:

- **Passthrough shows at all.** The build enables OpenXR's Composition Layers Support, which the Composition Layers package requires, alongside Meta's passthrough layer. The combination hasn't been seen running.
- **Panels over the real room** are translucent, not hazy, and text stays readable against a bright wall.
- **The shadow** falls on the real table and fades out before the edge of its invisible plane.
- **The spatial anchor** is created. The log line starting `[Placement]` gives the method and position.
- **Grabbing feels right.** No hand poses are authored for the sneaker, so the hand closes with the SDK's defaults.
- **The label lines** in the exploded view are drawn over the parts. Check they are comfortable to look at in stereo.

The editor's mouse and keyboard controls have not been tried by hand yet either. The tests drive the same code paths, but not through the mouse.

### Logs from the device

```sh
adb logcat -s Unity
```

Lines to look for start with `[Placement]`, `[SurfaceFinder]`, `[XRDisplaySetup]` and `[Analytics]`.

## Performance

The budget from the design document, for a Quest 3S with passthrough and depth occlusion on:

| Metric | Budget |
|---|---|
| Frame time | 11.1 ms or less, for a steady 90 Hz. 72 Hz is the fallback |
| Draw calls | 60 or fewer |
| Triangles | 100 thousand or fewer |
| Materials on the product | 2 or 3 |
| Real-time lights | 1 directional |
| Post-processing | Off |

A development build shows frame rate, frame time, the worst frame, draw calls, batches and triangles in the headset, low and to the left of the view. Meta's OVR Metrics Tool, Unity's profiler and the Frame Debugger give the detail.

If the exploded view is over budget, these are the levers, cheapest first:

1. `Occlusion Toggle` > `Off While Exploded`, on `MixedReality/Services`. It drops depth occlusion while the product is apart.
2. `Occlusion Toggle` > `Soft Occlusion` off, for hard occlusion, which is cheaper.
3. Stronger foveation in `XRDisplaySetup`, or less MSAA in `ProjectConfigurator`.

The first two are fields on a scene object. To make a change permanent, set the default in the script and rebuild.
