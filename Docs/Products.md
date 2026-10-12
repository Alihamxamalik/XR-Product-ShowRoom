# Products

How a product is described, how to add one, and how to replace the stand-in art and audio that ship with the project.

## What a product is made of

| Piece | Asset | What it holds |
|---|---|---|
| Product definition | `Data/Products/AeroPulse.asset` | Name, price, colours, hotspots, how it comes apart. The showroom reads everything from here |
| Product prefab | `Prefabs/Product/AeroPulse.prefab` | The model, split into parts, grabbable as a whole and part by part |
| Packaging | `Prefabs/Product/AeroBox.prefab` | The box it arrives in during the hook. Optional |
| Brand theme | `Data/Themes/Aero.asset` | Logo, colours and sounds, shared by every product of the brand |
| Tuning | `Data/ShowroomTuning.asset` | Pacing and thresholds, shared by every product |

Paths are relative to `Assets/_Project`. Nothing in the runtime code refers to the AERO Pulse by name.

## The AERO Pulse

The sneaker that ships is a stand-in, generated in code by `ShoeMeshBuilder` so the project is complete without a bought or borrowed model. It is 29 cm long and has eight parts on three materials:

| Part | Label in the exploded view | Material | Colour region |
|---|---|---|---|
| `Outsole` | Rubber outsole | Rubber | none |
| `Midsole` | Foam midsole | Foam | Sole |
| `Plate` | Carbon plate | Rubber | none |
| `Insole` | Cushioned insole | Rubber | none |
| `Upper` | Knit upper | Fabric | Upper, with a side stripe in Sole |
| `HeelCounter` | Heel counter | Foam | Sole |
| `Tongue` | Padded tongue | Fabric | Upper |
| `Laces` | Flat laces | Fabric | Laces |

The regions are painted into the meshes' vertex colours. The untinted parts take their colour from a four-cell palette texture.

## Adding a product

The intent is that a new product is a new model and one data asset, with no code changes.

### 1. Prepare the model

- **One mesh per part.** Split the model in Blender (or similar) into the pieces that should come apart. Each needs a MeshFilter and MeshRenderer after import. Skinned meshes aren't supported.
- **Real size, in metres,** with Y up.
- **Origin at the centre of the base.** The origin is the point that rests on the user's table.
- **The side the user should see first faces -Z.** The product is placed with its +Z pointing away from the user.
- **Budget:** the design document allows 20 to 40 thousand triangles and two or three materials sharing one texture set.

### 2. Create the prefab and the definition

Select the model, either the FBX in the Project window or an instance in a scene, and choose **MR Showroom > Product > Create Product From Selected Model**. For a model called `Watch` it creates:

- `Prefabs/Product/Watch.prefab`, grabbable as a whole (one hand moves and turns it, two resize it) and part by part.
- `Art/Products/Watch/Materials/`, a copy of each of the model's materials on the Product Lit shader, keeping its texture and colour.
- `Data/Products/Watch.asset`, a definition with one exploded-view entry per part, each moving straight out from the middle as a starting point.

Running it again replaces the prefab, after asking, and keeps the definition and the materials.

### 3. Paint the tint mask

The Product Lit shader recolours up to three regions. A region is a channel of the tint mask: red, green or blue. The mask is the `Tint Mask` texture multiplied by the mesh's vertex colours, so either can be used, or both.

- Paint a texture in the model's UVs: red where the first region is, green for the second, blue for the third, black where the colour must not change. Assign it to `Tint Mask` on the new materials.
- Or paint vertex colours in the same way and leave the texture white.

Until there is a mask, every pixel belongs to all three regions and the third one wins, so paint the mask before judging the colours. Three regions is a deliberate limit: it keeps the whole product on one shader and one texture set.

### 4. Fill in the definition

Open the definition in the inspector. The sections below cover each field.

### 5. Show it

Select `Product` in the Showroom scene and assign the definition to its `Product Controller`. Press Play.

A scene rebuild wires the AERO Pulse again, because `SceneBuilder` loads the definition at `ProductAssetBuilder.DefinitionPath`. To make another product the permanent default, change what `SceneBuilder.BuildShowroom` assigns. `ProductController.Load(definition)` swaps the product at runtime; no test exercises it yet.

## The definition, field by field

### Identity

| Field | Meaning |
|---|---|
| `id` | Written into the analytics record |
| `displayName` | Shown on the colour panel and the end card |
| `tagline`, `price` | Shown on the end card |
| `ctaUrl` | The page **Buy now** opens. Leave empty for the mock confirmation |

### Model

| Field | Meaning |
|---|---|
| `prefab` | The product prefab |
| `packaging` | The box for the hook. Leave empty to skip the unboxing |
| `sweepAxis` | The direction a colour change travels, in the product's own space. The AERO Pulse uses left: toe to heel |
| `footprint`, `footprintSize` | The sprite left behind during the reveal walk, and its length and width in metres |

### Customisation

Each **colour slot** is one region:

| Field | Meaning |
|---|---|
| `name` | The row label on the colour panel |
| `shaderProperty` | `_TintR`, `_TintG` or `_TintB`: the mask channel this slot drives |
| `colors` | The swatches, each with a name (it goes into analytics) and a colour |
| `metallic`, `smoothness` | The region's surface when no finish applies to it |

`finishes` are surface options (matte, metallic) for the one slot named by `finishSlot`. `presets` are the brand's colourways: an index into each slot's colours, in slot order, plus a finish. **The first preset is the look the product starts in** and returns to on Play again.

The colour panel has room for three slots of five colours, two finishes and three presets. Anything beyond that isn't shown. With fewer, the unused buttons are hidden, and with one finish or none the Finish row disappears.

Choose colours a brand would ship: soft and a little dusty. Fully saturated colours and pure black look pasted on against a real room.

### Hotspots

| Field | Meaning |
|---|---|
| `title`, `text` | The card: a title and one short line |
| `partName` | The part the dot rides on, so it stays with that part. Empty means the product itself |
| `localPosition` | Where the dot sits, in the product's own space with the product assembled, in metres. Put it a few millimetres off the surface |
| `icon` | Optional sprite on the card |

The card always opens towards the viewer, in front of the product, so a dot can go anywhere, including underneath.

### Exploded view

One entry per part:

| Field | Meaning |
|---|---|
| `partName` | The part's name in the prefab |
| `label` | The name shown beside it. Empty means no label |
| `direction`, `distance` | Which way it moves, in the product's own space, and how far in metres |
| `labelHeight` | The height of the label above the part's origin, in metres. Usually the middle of the part |
| `grabbable` | Whether the part can be lifted out once the product is apart |

Two things to keep in mind:

- **Nothing may sink into the user's table.** The AERO Pulse leaves its outsole where it is and lifts everything else.
- **Labels place themselves.** Each sits level with its part, beyond everything at that height, on the side away from the colour panel, with a line to its own part. If two would collide, the upper one is lifted clear. `labelHeight` is the only thing to tune.

## Packaging

`AeroBox.prefab` is built by `ProductAssetBuilder.BuildBox`. A box of your own needs a `PackagingBox` component on its root, with the origin at the centre of the base, and these references:

- `body`, which sinks away as the product rises;
- `lid`, which pops off;
- `glow`, a flat soft light under the box (optional);
- `grab`, a `GrabTarget`, so that touching the box skips the unboxing (optional).

The simplest route is to duplicate `AeroBox.prefab` and change its textures.

## Replacing the stand-ins

Everything generated can be replaced without touching the builders, because they never overwrite an asset that already exists.

| Stand-in | Where it is | How to replace it |
|---|---|---|
| The sneaker | `Art/Products/AeroPulse`, `Prefabs/Product/AeroPulse.prefab` | Bring in a real model as described above |
| Logo | `Art/Brand/AeroMark.png` | Drop a file over it. White on transparent: it is tinted by the brand colour |
| Box artwork | `Art/Packaging/BoxAlbedo.png`, `LidAlbedo.png` | Drop files over them |
| Sounds | `Audio/SFX/*.wav`, `Audio/Music/music_bed.wav` | Replace the files and keep the names, or assign other clips on the brand theme |
| Brand colours | `Data/Themes/Aero.asset` | Edit in the inspector, then rebuild the scenes so the panels pick the colours up. Materials made with the old colour are kept (the hover rim on the product materials, `BoxGlow`, `CalloutDot`): change those by hand, or delete them before rebuilding |
| Typeface | TextMeshPro's default font | Change the default font asset in the TMP settings, then rebuild |

**MR Showroom > Setup > Rebuild Scenes and Prefabs** regenerates the sneaker's meshes, the prefabs and both scenes. It keeps data assets, materials and textures.

## Known limitations

- A scene rebuild assigns the AERO Pulse again (see step 5).
- The brand theme has a `font` field, but the builders use TextMeshPro's default font and don't read it yet.
- `ProductController.Load` is not covered by a test.
- Hand poses for holding the product aren't authored. Grabbing uses the Interaction SDK's defaults.
