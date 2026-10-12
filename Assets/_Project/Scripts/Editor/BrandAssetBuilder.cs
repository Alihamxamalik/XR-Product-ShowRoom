using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static MRShowroom.EditorTools.BuilderUtil;

namespace MRShowroom.EditorTools
{
    /// <summary>
    /// The AERO brand kit and the small textures the UI and effects are drawn with, all generated
    /// in code so the project is complete without a single bought or borrowed asset. Replace any
    /// of them with real artwork by dropping a file over the generated one.
    ///
    /// AERO is a made-up label: the mark, colours and tagline are original, to keep real brands
    /// out of a portfolio piece.
    /// </summary>
    internal static class BrandAssetBuilder
    {
        public const string BrandDir = ArtDir + "/Brand";
        public const string VfxDir = ArtDir + "/VFX";
        public const string PackagingDir = ArtDir + "/Packaging";
        public const string ThemePath = DataDir + "/Themes/Aero.asset";
        public const string TuningPath = DataDir + "/ShowroomTuning.asset";
        public const string SfxDir = Root + "/Audio/SFX";
        public const string MusicDir = Root + "/Audio/Music";

        /// <summary>The shape sprite's corner, in canvas units, before an image scales it.</summary>
        public const float ShapeCorner = 64f;

        // Brand colours. Nothing pure black and nothing fully saturated: both look pasted on against passthrough.
        public static readonly Color Volt = new Color(0.74f, 0.92f, 0.36f);
        public static readonly Color Ink = new Color(0.075f, 0.09f, 0.115f);

        public sealed class Kit
        {
            public BrandTheme Theme;
            public ShowroomTuning Tuning;
            /// <summary>
            /// A disc, sliced at its radius. Drawn as it is, it is a circle; sliced, it is a rounded
            /// rectangle with whatever corner radius the image asks for. Every panel, button, swatch and
            /// chip uses this one sprite, so a whole panel draws from a single texture.
            /// </summary>
            public Sprite Shape;
            public Sprite Ring;
            public Sprite Mark;
            public Sprite Footprint;
            public Texture2D SoftBlob;
            public Texture2D Reticle;
            /// <summary>A dot and a line with a dark edge of their own, so a part label's pointer shows on any colourway.</summary>
            public Texture2D CalloutDot;
            public Texture2D CalloutLine;
            public Texture2D BoxAlbedo;
            public Texture2D LidAlbedo;
            public Cubemap Reflection;
            public Material UiMaterial;
        }

        public static Kit Build()
        {
            var kit = new Kit
            {
                Shape = MakeSprite(BrandDir + "/UI/Shape.png", () => Draw(128, 128, Disc),
                    new Vector4(ShapeCorner, ShapeCorner, ShapeCorner, ShapeCorner), 100f),
                Ring = MakeSprite(BrandDir + "/UI/Ring.png", () => Draw(128, 128, RingShape), Vector4.zero, 100f),
                Mark = MakeSprite(BrandDir + "/AeroMark.png", () => Draw(256, 256, (u, v) => White(MarkCoverage(u, v))),
                    Vector4.zero, 100f),
                Footprint = MakeSprite(VfxDir + "/Footprint.png", () => Draw(256, 128, FootprintShape), Vector4.zero, 100f),
                SoftBlob = MakeTexture(VfxDir + "/SoftBlob.png", () => Draw(128, 128, SoftBlobShape)),
                Reticle = MakeTexture(VfxDir + "/Reticle.png", () => Draw(256, 256, ReticleShape)),
                CalloutDot = MakeTexture(BrandDir + "/UI/CalloutDot.png", () => Draw(64, 64, CalloutDotShape)),
                CalloutLine = MakeTexture(BrandDir + "/UI/CalloutLine.png", () => Draw(8, 32, CalloutLineShape)),
                BoxAlbedo = MakeTexture(PackagingDir + "/BoxAlbedo.png", () => Draw(256, 256, (u, v) => BoxFace(u, v, Ink, Volt))),
                LidAlbedo = MakeTexture(PackagingDir + "/LidAlbedo.png", () => Draw(256, 256, (u, v) => BoxFace(u, v, Volt, Ink)))
            };

            kit.Reflection = LoadOrCreate(BrandDir + "/StudioReflection.cubemap", StudioReflection);
            kit.UiMaterial = LoadOrCreate(BrandDir + "/UI/UIPanel.mat",
                () => new Material(RequireShader("MRShowroom/UI Panel")));
            kit.Tuning = LoadOrCreate(TuningPath, ScriptableObject.CreateInstance<ShowroomTuning>);
            kit.Theme = MakeTheme(kit);
            AssetDatabase.SaveAssets();
            return kit;
        }

        private static BrandTheme MakeTheme(Kit kit)
        {
            BrandTheme theme = LoadOrCreate(ThemePath, () =>
            {
                var created = ScriptableObject.CreateInstance<BrandTheme>();
                created.brandName = "AERO";
                created.tagline = "Run on air.";
                created.primary = Volt;
                created.onPrimary = Ink;
                created.secondary = Ink;
                created.panel = new Color(Ink.r, Ink.g, Ink.b, 0.84f);
                created.text = new Color(0.96f, 0.97f, 1f);
                created.mutedText = new Color(0.66f, 0.71f, 0.78f);
                created.musicVolume = 0.28f;
                return created;
            });

            // References are filled in on every run, so sounds added later are picked up; values a
            // designer has tuned in the inspector are left as they are.
            theme.logo = theme.logo != null ? theme.logo : kit.Mark;
            theme.font = theme.font != null ? theme.font : RequireFont();
            theme.boxThud = Keep(theme.boxThud, "box_thud");
            theme.lidPop = Keep(theme.lidPop, "lid_pop");
            theme.grab = Keep(theme.grab, "grab");
            theme.release = Keep(theme.release, "release");
            theme.swatchClick = Keep(theme.swatchClick, "swatch_click");
            theme.sweep = Keep(theme.sweep, "sweep");
            theme.explodeWhoosh = Keep(theme.explodeWhoosh, "explode_whoosh");
            theme.reassembleSnap = Keep(theme.reassembleSnap, "reassemble_snap");
            theme.hotspotOpen = Keep(theme.hotspotOpen, "hotspot_open");
            theme.uiPress = Keep(theme.uiPress, "ui_press");
            theme.step = Keep(theme.step, "step");
            theme.placed = Keep(theme.placed, "placed");
            theme.confirm = Keep(theme.confirm, "confirm");
            if (theme.musicBed == null)
            {
                theme.musicBed = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicDir + "/music_bed.wav");
            }

            EditorUtility.SetDirty(theme);
            return theme;
        }

        private static AudioClip Keep(AudioClip current, string name)
        {
            if (current != null)
            {
                return current;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxDir}/{name}.wav");
            if (clip == null)
            {
                Debug.LogWarning($"[MR Showroom] Sound '{name}.wav' not found in {SfxDir}; that sound will be silent.");
            }

            return clip;
        }

        // ---- Texture plumbing ------------------------------------------------------------------

        private static Sprite MakeSprite(string path, Func<Texture2D> draw, Vector4 border, float pixelsPerUnit)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            SaveTexture(path, draw(), true, border, pixelsPerUnit);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Texture2D MakeTexture(string path, Func<Texture2D> draw)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return existing != null ? existing : SaveTexture(path, draw(), false);
        }

        /// <param name="shade">Colour at a point, with u and v running 0-1 across the texture.</param>
        private static Texture2D Draw(int width, int height, Func<float, float, Color> shade)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = shade((x + 0.5f) / width, (y + 0.5f) / height);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Color White(float alpha)
        {
            return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        // Turns a signed distance in pixels into anti-aliased coverage.
        private static float Coverage(float distancePixels)
        {
            return Mathf.Clamp01(0.5f - distancePixels);
        }

        // ---- Shapes ----------------------------------------------------------------------------

        private static Color Disc(float u, float v)
        {
            float radius = new Vector2(u - 0.5f, v - 0.5f).magnitude * 128f;
            return White(Coverage(radius - 62f));
        }

        private static Color RingShape(float u, float v)
        {
            float radius = new Vector2(u - 0.5f, v - 0.5f).magnitude * 128f;
            return White(Coverage(Mathf.Abs(radius - 57f) - 5f));
        }

        private static Color SoftBlobShape(float u, float v)
        {
            float radius = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
            float falloff = Mathf.Clamp01(1f - radius);
            return White(falloff * falloff * (3f - 2f * falloff));
        }

        private static Color ReticleShape(float u, float v)
        {
            float radius = new Vector2(u - 0.5f, v - 0.5f).magnitude * 256f;
            float ring = Coverage(Mathf.Abs(radius - 108f) - 7f);
            float dot = Coverage(radius - 12f);
            float glow = Mathf.Clamp01(1f - radius / 104f) * 0.22f;
            return White(Mathf.Max(Mathf.Max(ring, dot), glow));
        }

        private static Color CalloutDotShape(float u, float v)
        {
            float radius = new Vector2(u - 0.5f, v - 0.5f).magnitude * 64f;
            return Rimmed(Coverage(radius - 19f), Coverage(radius - 30f));
        }

        // The same across the line's width; it is stretched along its length.
        private static Color CalloutLineShape(float u, float v)
        {
            float across = Mathf.Abs(v - 0.5f) * 32f;
            return Rimmed(Coverage(across - 5.5f), Coverage(across - 14f));
        }

        // White inside a dark rim. Tinted by its material, the middle takes the colour and the rim stays dark.
        private static Color Rimmed(float core, float shape)
        {
            Color color = Color.Lerp(Ink, Color.white, core);
            color.a = shape * Mathf.Lerp(0.8f, 1f, core);
            return color;
        }

        // The sole seen from below, soft at the edge and cut by tread lines, running heel to toe along U.
        private static Color FootprintShape(float u, float v)
        {
            const float pad = 0.06f;
            float along = Mathf.InverseLerp(pad, 1f - pad, u);
            // The texture is 2:1 and covers a little more than the sole's length.
            float metresPerV = ShoeProfile.Length / (1f - 2f * pad) * 0.5f;
            float across = (v - 0.5f) * metresPerV - ShoeProfile.CentreZ(along);
            float edge = ShoeProfile.HalfWidth(along) - Mathf.Abs(across);
            if (u < pad || u > 1f - pad)
            {
                edge = -1f;
            }

            float body = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 0.008f));
            float tread = 0.55f + 0.45f * Mathf.SmoothStep(0.2f, 0.5f, Mathf.Abs(Mathf.Sin(along * 34f + across * 60f)));
            return White(body * tread);
        }

        // The AERO mark: three slanted bars, each shorter than the last, like a wing in motion.
        private static float MarkCoverage(float u, float v)
        {
            const float pixels = 256f;
            var p = new Vector2((u - 0.5f) * pixels, (v - 0.5f) * pixels);
            float best = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                float y = 52f - i * 52f;
                float length = 168f - i * 46f;
                // Bars share a right-hand edge and lean forwards.
                var right = new Vector2(92f + y * 0.36f, y);
                var left = new Vector2(right.x - length, y);
                best = Mathf.Min(best, DistanceToSegment(p, left, right) - 17f);
            }

            return Coverage(best);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private static Color BoxFace(float u, float v, Color card, Color print)
        {
            // The mark, small, in the middle of every face.
            float mark = MarkCoverage((u - 0.5f) / 0.42f + 0.5f, (v - 0.5f) / 0.42f + 0.5f);
            bool inside = Mathf.Abs(u - 0.5f) < 0.21f && Mathf.Abs(v - 0.5f) < 0.21f;
            Color color = Color.Lerp(card, print, inside ? mark : 0f);
            color.a = 1f;
            return color;
        }

        // ---- Reflection ------------------------------------------------------------------------

        // A neutral photo studio for the product to reflect: soft light overhead, a bright strip to one
        // side, a darker floor. It matches no particular room, which is the point: it never looks wrong.
        private static Cubemap StudioReflection()
        {
            const int size = 64;
            var cube = new Cubemap(size, TextureFormat.RGBAHalf, true) { name = "StudioReflection" };
            var pixels = new Color[size * size];

            foreach (CubemapFace face in new[]
                     {
                         CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY,
                         CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ
                     })
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float a = (x + 0.5f) / size * 2f - 1f;
                        float b = (y + 0.5f) / size * 2f - 1f;
                        pixels[y * size + x] = StudioLight(FaceDirection(face, a, b));
                    }
                }

                cube.SetPixels(pixels, face);
            }

            cube.Apply(true);
            return cube;
        }

        private static Vector3 FaceDirection(CubemapFace face, float a, float b)
        {
            switch (face)
            {
                case CubemapFace.PositiveX: return new Vector3(1f, -b, -a).normalized;
                case CubemapFace.NegativeX: return new Vector3(-1f, -b, a).normalized;
                case CubemapFace.PositiveY: return new Vector3(a, 1f, b).normalized;
                case CubemapFace.NegativeY: return new Vector3(a, -1f, -b).normalized;
                case CubemapFace.PositiveZ: return new Vector3(a, -b, 1f).normalized;
                default: return new Vector3(-a, -b, -1f).normalized;
            }
        }

        private static Color StudioLight(Vector3 direction)
        {
            // Walls: brighter towards the ceiling.
            float up = direction.y * 0.5f + 0.5f;
            float light = Mathf.Lerp(0.16f, 0.62f, Mathf.SmoothStep(0f, 1f, up));

            // A big soft box overhead and a strip light off to one side.
            float overhead = Mathf.SmoothStep(0.72f, 0.94f, direction.y);
            float strip = Mathf.SmoothStep(0.86f, 0.97f, Vector3.Dot(direction, new Vector3(-0.72f, 0.42f, -0.55f).normalized));
            light += overhead * 1.5f + strip * 1.1f;

            // Very slightly warm below, cool above.
            var tint = Color.Lerp(new Color(1f, 0.97f, 0.93f), new Color(0.95f, 0.98f, 1f), up);
            return new Color(tint.r * light, tint.g * light, tint.b * light, 1f);
        }
    }
}
