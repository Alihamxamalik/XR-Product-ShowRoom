using System;
using System.Collections.Generic;
using UnityEngine;

namespace MRShowroom.EditorTools
{
    /// <summary>
    /// The shape of the AERO Pulse, as a handful of curves along the shoe's length (0 = heel,
    /// 1 = toe). Shared by the mesh builder and by the footprint texture, so the print the shoe
    /// leaves matches its sole.
    /// </summary>
    internal static class ShoeProfile
    {
        public const float Length = 0.29f;
        public const float OutsoleThickness = 0.006f;

        private static readonly Vector2[] Width =
        {
            new Vector2(0.14f, 0.0370f), new Vector2(0.30f, 0.0365f), new Vector2(0.44f, 0.0330f),
            new Vector2(0.60f, 0.0430f), new Vector2(0.72f, 0.0470f), new Vector2(0.80f, 0.0465f)
        };

        private static readonly Vector2[] Midsole =
        {
            new Vector2(0f, 0.030f), new Vector2(0.30f, 0.034f), new Vector2(0.55f, 0.026f),
            new Vector2(0.80f, 0.018f), new Vector2(1f, 0.013f)
        };

        private static readonly Vector2[] Roof =
        {
            new Vector2(0f, 0.095f), new Vector2(0.10f, 0.098f), new Vector2(0.30f, 0.088f),
            new Vector2(0.52f, 0.078f), new Vector2(0.66f, 0.062f), new Vector2(0.80f, 0.047f)
        };

        private static readonly Vector2[] Collar =
        {
            new Vector2(0f, 0.098f), new Vector2(0.05f, 0.103f), new Vector2(0.16f, 0.083f),
            new Vector2(0.28f, 0.070f), new Vector2(0.40f, 0.078f), new Vector2(0.55f, 0.080f)
        };

        private static readonly Vector2[] Opening =
        {
            new Vector2(0.008f, 0f), new Vector2(0.03f, 0.016f), new Vector2(0.10f, 0.026f),
            new Vector2(0.20f, 0.028f), new Vector2(0.34f, 0.024f), new Vector2(0.46f, 0.016f),
            new Vector2(0.55f, 0.010f)
        };

        public static float X(float u) => (u - 0.5f) * Length;

        /// <summary>Half the sole's width: rounded heel, narrow waist, wide ball, rounded toe.</summary>
        public static float HalfWidth(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.14f)
            {
                float t = 1f - u / 0.14f;
                return 0.037f * Mathf.Sqrt(Mathf.Max(1f - t * t, 0f));
            }

            if (u > 0.80f)
            {
                float t = (u - 0.80f) / 0.20f;
                return 0.0465f * Mathf.Sqrt(Mathf.Max(1f - t * t, 0f));
            }

            return Sample(Width, u);
        }

        /// <summary>The sole curves gently inwards, like a foot.</summary>
        public static float CentreZ(float u) => 0.006f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u));

        /// <summary>Height of the underside: flat, with a rocker at the heel and spring at the toe.</summary>
        public static float GroundY(float u)
        {
            float heel = Mathf.Max(0f, (0.07f - u) / 0.07f);
            float toe = Mathf.Max(0f, (u - 0.70f) / 0.30f);
            return 0.004f * heel * heel + 0.017f * toe * toe;
        }

        public static float MidsoleThickness(float u) => Sample(Midsole, u);

        /// <summary>Where the upper starts: the top of the midsole.</summary>
        public static float UpperBaseY(float u) => GroundY(u) + OutsoleThickness + MidsoleThickness(u);

        /// <summary>Height of the upper above its base where it is closed over the foot.</summary>
        public static float RoofHeight(float u)
        {
            if (u > 0.80f)
            {
                float t = (u - 0.80f) / 0.20f;
                return 0.047f * Mathf.Sqrt(Mathf.Max(1f - t * t, 0f));
            }

            return Sample(Roof, u);
        }

        /// <summary>Height of the collar's rim above the upper's base.</summary>
        public static float CollarHeight(float u) => Sample(Collar, u);

        /// <summary>Half the width of the opening the foot goes into.</summary>
        public static float OpeningHalfWidth(float u) => Sample(Opening, u);

        /// <summary>
        /// 1 where the upper is open at the top (the collar), 0 where it is closed over the foot (the toe
        /// box). At the heel it stays a collar all the way round; the opening just narrows to nothing.
        /// </summary>
        public static float Openness(float u)
        {
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.44f, 0.54f, u));
        }

        // Catmull-Rom through the keys, held flat beyond the ends.
        private static float Sample(Vector2[] keys, float u)
        {
            if (u <= keys[0].x)
            {
                return keys[0].y;
            }

            int last = keys.Length - 1;
            if (u >= keys[last].x)
            {
                return keys[last].y;
            }

            int i = 0;
            while (i < last - 1 && u > keys[i + 1].x)
            {
                i++;
            }

            float t = (u - keys[i].x) / (keys[i + 1].x - keys[i].x);
            float p0 = keys[Mathf.Max(i - 1, 0)].y;
            float p1 = keys[i].y;
            float p2 = keys[i + 1].y;
            float p3 = keys[Mathf.Min(i + 2, last)].y;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t
                           + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);
        }
    }

    /// <summary>
    /// Builds the AERO Pulse out of code: a stand-in sneaker that is already split into the parts
    /// the exploded view needs (outsole, midsole, plate, insole, upper, heel counter, tongue,
    /// laces), with the tint regions painted into vertex colours. It lets the whole experience run
    /// before a licensed model exists; the real model replaces it through the product definition.
    ///
    /// Axes: +X towards the toe, +Y up, origin on the ground under the middle of the shoe.
    /// Vertex colour R = upper, G = sole, B = laces (the three tint regions).
    /// </summary>
    internal static class ShoeMeshBuilder
    {
        public sealed class Part
        {
            public string Name;
            public Mesh Mesh;
            /// <summary>Which of the product's materials it uses: 0 fabric, 1 foam, 2 rubber.</summary>
            public int Material;
        }

        public const int Fabric = 0;
        public const int Foam = 1;
        public const int Rubber = 2;

        private const int Stations = 56;
        private const int RingPoints = 23;
        private const int WallPoints = 7;

        private static readonly Color NoTint = new Color(0f, 0f, 0f, 1f);
        private static readonly Color UpperTint = new Color(1f, 0f, 0f, 1f);
        private static readonly Color SoleTint = new Color(0f, 1f, 0f, 1f);
        private static readonly Color LaceTint = new Color(0f, 0f, 1f, 1f);

        // The rubber material looks its base colour up in a four-cell palette texture; these are the cells.
        private static readonly Vector2 PaletteOutsole = new Vector2(0.125f, 0.5f);
        private static readonly Vector2 PalettePlate = new Vector2(0.375f, 0.5f);
        private static readonly Vector2 PaletteInsole = new Vector2(0.625f, 0.5f);

        public static List<Part> Build()
        {
            return new List<Part>
            {
                new Part { Name = "Outsole", Mesh = Outsole(), Material = Rubber },
                new Part { Name = "Midsole", Mesh = MidsoleMesh(), Material = Foam },
                new Part { Name = "Plate", Mesh = Plate(), Material = Rubber },
                new Part { Name = "Insole", Mesh = Insole(), Material = Rubber },
                new Part { Name = "Upper", Mesh = Upper(), Material = Fabric },
                new Part { Name = "HeelCounter", Mesh = HeelCounter(), Material = Foam },
                new Part { Name = "Tongue", Mesh = Tongue(), Material = Fabric },
                new Part { Name = "Laces", Mesh = Laces(), Material = Fabric }
            };
        }

        // ---- Parts -----------------------------------------------------------------------------

        private static Mesh Outsole()
        {
            var data = new MeshData();
            Func<float, float> bottom = ShoeProfile.GroundY;
            Func<float, float> top = u => ShoeProfile.GroundY(u) + ShoeProfile.OutsoleThickness;
            AddSoleSlab(data, bottom, top, 0f, 0f, 1f, NoTint, PaletteOutsole);
            return data.ToMesh("Outsole");
        }

        private static Mesh MidsoleMesh()
        {
            var data = new MeshData();
            Func<float, float> bottom = u => ShoeProfile.GroundY(u) + ShoeProfile.OutsoleThickness;
            Func<float, float> top = ShoeProfile.UpperBaseY;
            Func<float, float> middle = u => (bottom(u) + top(u)) * 0.5f;

            // A sidewall that bulges at mid-height reads as foam rather than as a cut block.
            Vector3 inside = new Vector3(0f, 0.02f, 0f);
            var wall = new List<Vector3[]>
            {
                Outline(bottom, 0.0015f, 0f, 1f), Outline(middle, -0.0028f, 0f, 1f), Outline(top, 0.0005f, 0f, 1f)
            };
            data.AddGrid(wall, SoleTint, inside, Vector2.zero, Stations / 2);
            data.AddGrid(Faces(bottom, 0.0015f, 0f, 1f), SoleTint, inside, Vector2.zero);
            data.AddGrid(Faces(top, 0.0005f, 0f, 1f), SoleTint, inside, Vector2.zero);
            return data.ToMesh("Midsole");
        }

        private static Mesh Plate()
        {
            var data = new MeshData();
            // A spoon-shaped plate through the middle of the midsole, a little shy of its edges.
            Func<float, float> bottom = u => ShoeProfile.GroundY(u) + ShoeProfile.OutsoleThickness
                                             + ShoeProfile.MidsoleThickness(u) * 0.42f - 0.003f * Mathf.Sin(Mathf.PI * u);
            Func<float, float> top = u => bottom(u) + 0.0022f;
            AddSoleSlab(data, bottom, top, 0.007f, 0.06f, 0.95f, NoTint, PalettePlate);
            return data.ToMesh("Plate");
        }

        private static Mesh Insole()
        {
            var data = new MeshData();
            Func<float, float> bottom = u => ShoeProfile.UpperBaseY(u) + 0.0008f;
            Func<float, float> top = u => ShoeProfile.UpperBaseY(u) + 0.0048f;
            AddSoleSlab(data, bottom, top, 0.0075f, 0.035f, 0.965f, NoTint, PaletteInsole);
            return data.ToMesh("Insole");
        }

        private static Mesh Upper()
        {
            var data = new MeshData();
            var rings = new List<Vector3[]>(Stations + 1);
            for (int i = 0; i <= Stations; i++)
            {
                rings.Add(UpperRing(Spaced(i, Stations), 0f));
            }

            data.AddGrid(rings, UpperTint, new Vector3(0f, 0.06f, 0f), Vector2.zero, WallPoints / 2, weave: true);

            // The brand stripe on each side: a wing that is broad towards the toe and tapers to the heel.
            foreach (int side in new[] { -1, 1 })
            {
                var stripe = new List<Vector3[]>();
                const int columns = 12;
                for (int c = 0; c <= columns; c++)
                {
                    float t = c / (float)columns;
                    float u = Mathf.Lerp(0.68f, 0.36f, t);
                    float centre = Mathf.Lerp(0.30f, 0.66f, t);
                    float half = Mathf.Lerp(0.12f, 0.025f, t * t);
                    stripe.Add(new[]
                    {
                        WallPoint(u, centre - half, side, 0.0013f),
                        WallPoint(u, centre + half, side, 0.0013f)
                    });
                }

                data.AddGrid(stripe, SoleTint, new Vector3(0f, 0.06f, 0f), Vector2.zero);
            }

            return data.ToMesh("Upper");
        }

        private static Mesh HeelCounter()
        {
            var data = new MeshData();
            const int columns = 24;
            const int rows = 5;
            var rings = new List<Vector3[]>(columns + 1);
            for (int c = 0; c <= columns; c++)
            {
                // Round the back of the heel: from one side, through the heel tip, to the other.
                float t = Mathf.Cos(Mathf.PI * c / columns);
                int side = t >= 0f ? -1 : 1;
                float around = Mathf.Abs(t);
                float u = 0.27f * around * around + 0.004f;
                // Tall at the back, sweeping down as it comes forward.
                float reach = Mathf.Lerp(0.72f, 0.22f, around * around);

                var ring = new Vector3[rows];
                for (int r = 0; r < rows; r++)
                {
                    ring[r] = WallPoint(u, reach * r / (rows - 1), side, 0.0018f);
                }

                rings.Add(ring);
            }

            // A thin shell seen from both sides once the product is apart.
            data.AddGrid(rings, SoleTint, new Vector3(-0.08f, 0.05f, 0f), Vector2.zero);
            data.AddGrid(rings, SoleTint, new Vector3(-0.5f, 0.05f, 0f), Vector2.zero);
            return data.ToMesh("HeelCounter");
        }

        private static Mesh Tongue()
        {
            var data = new MeshData();
            const int along = 14;
            const int across = 7;
            var rings = new List<Vector3[]>(along + 1);
            for (int a = 0; a <= along; a++)
            {
                float s = a / (float)along;
                float u = Mathf.Lerp(0.585f, 0.33f, s);
                float half = Mathf.Lerp(0.013f, 0.025f, s);
                var ring = new Vector3[across];
                for (int c = 0; c < across; c++)
                {
                    float r = c / (float)(across - 1) * 2f - 1f;
                    ring[c] = new Vector3(ShoeProfile.X(u) - 0.012f * s * s, TongueY(u, r),
                        ShoeProfile.CentreZ(u) + half * r);
                }

                rings.Add(ring);
            }

            data.AddSlab(rings, 0.0045f, UpperTint, weave: true);
            return data.ToMesh("Tongue");
        }

        private static Mesh Laces()
        {
            var data = new MeshData();
            const int across = 9;
            float[] rows = { 0.365f, 0.41f, 0.455f, 0.50f, 0.545f };
            foreach (float u in rows)
            {
                // Each lace starts and ends on the upper itself, at the shoulder on either side of the opening.
                Vector3[] ring = UpperRing(u, 0f);
                Vector3 near = ring[WallPoints - 1];
                Vector3 far = ring[RingPoints - WallPoints];

                var front = new Vector3[across];
                var back = new Vector3[across];
                for (int c = 0; c < across; c++)
                {
                    float t = c / (float)(across - 1);
                    float r = t * 2f - 1f;
                    // Over the tongue in the middle, down onto the upper at each end.
                    float over = TongueY(u, r * 0.8f) + 0.0035f;
                    float end = Mathf.Lerp(near.y, far.y, t) + 0.002f;
                    float y = Mathf.Lerp(over, end, Mathf.Pow(Mathf.Abs(r), 2.5f));
                    float z = Mathf.Lerp(near.z, far.z, t);
                    front[c] = new Vector3(ShoeProfile.X(u) + 0.0028f, y, z);
                    back[c] = new Vector3(ShoeProfile.X(u) - 0.0028f, y, z);
                }

                data.AddSlab(new List<Vector3[]> { back, front }, 0.0026f, LaceTint, weave: false);
            }

            return data.ToMesh("Laces");
        }

        // ---- Shape helpers ---------------------------------------------------------------------

        // Stations bunch up towards the heel and toe, where the outline turns fastest.
        private static float Spaced(int index, int count)
        {
            return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * index / count);
        }

        /// <summary>The top of the tongue at a point across it (r = -1 to 1).</summary>
        private static float TongueY(float u, float r)
        {
            // It lies on the closed part of the upper at the front and stands a little proud of the collar
            // at the back, following the shoe's own line in between.
            float s = Mathf.InverseLerp(0.585f, 0.33f, u);
            float low = ShoeProfile.RoofHeight(u) + 0.0025f;
            float high = ShoeProfile.CollarHeight(u) + 0.015f;
            return ShoeProfile.UpperBaseY(u) + Mathf.Lerp(low, high, Mathf.SmoothStep(0f, 1f, s)) - 0.009f * r * r;
        }

        /// <summary>
        /// One cross-section of the upper: up one side wall, over the top, down the other. Where the
        /// upper is open the top points fold down inside to make the collar and the cavity the foot goes
        /// into; where it is closed the same points lie along the roof.
        /// </summary>
        private static Vector3[] UpperRing(float u, float outset)
        {
            float baseY = ShoeProfile.UpperBaseY(u);
            float half = Mathf.Max(ShoeProfile.HalfWidth(u) - 0.003f, 0f) + outset;
            float roof = ShoeProfile.RoofHeight(u);
            float rim = ShoeProfile.CollarHeight(u);
            float gap = ShoeProfile.OpeningHalfWidth(u);
            float open = ShoeProfile.Openness(u);
            float floor = 0.006f;

            // The opening can't be wider than the shoe.
            gap = Mathf.Min(gap, Mathf.Max(half - 0.008f, 0f));
            float padded = Mathf.Min(gap + 0.006f, half);

            var ring = new Vector3[RingPoints];
            for (int j = 0; j < RingPoints; j++)
            {
                // Closed: a boxy arch (a superellipse), with the points bunched as they are when open.
                float angle = ClosedAngle(j);
                const float power = 2f / 2.6f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                var closed = new Vector2(-half * Mathf.Sign(cos) * Mathf.Pow(Mathf.Abs(cos), power),
                    roof * Mathf.Pow(Mathf.Abs(sin), power));

                Vector2 opened = OpenPoint(j, half, rim, gap, padded, floor);
                Vector2 point = Vector2.Lerp(closed, opened, open);
                ring[j] = new Vector3(ShoeProfile.X(u), baseY + point.y, ShoeProfile.CentreZ(u) + point.x);
            }

            return ring;
        }

        private static float ClosedAngle(int j)
        {
            int topStart = WallPoints;
            int topEnd = RingPoints - WallPoints - 1;
            if (j < topStart)
            {
                return Mathf.Lerp(0f, 0.36f * Mathf.PI, j / (float)(WallPoints - 1));
            }

            if (j > topEnd)
            {
                return Mathf.Lerp(0.64f * Mathf.PI, Mathf.PI, (j - topEnd - 1) / (float)(WallPoints - 1));
            }

            return Mathf.Lerp(0.40f * Mathf.PI, 0.60f * Mathf.PI, (j - topStart) / (float)(topEnd - topStart));
        }

        // x = across (negative = the -Z side), y = height above the upper's base.
        private static Vector2 OpenPoint(int j, float half, float rim, float gap, float padded, float floor)
        {
            int mirrored = RingPoints - 1 - j;
            bool far = j > RingPoints / 2;
            int k = far ? mirrored : j;
            float sign = far ? 1f : -1f;

            Vector2 point;
            if (k < WallPoints)
            {
                point = Wall(k / (float)(WallPoints - 1), half, rim, padded);
            }
            else
            {
                switch (k - WallPoints)
                {
                    case 0:
                        point = new Vector2(gap + 0.003f, rim + 0.002f);
                        break;
                    case 1:
                        point = new Vector2(gap, rim - 0.004f);
                        break;
                    case 2:
                        point = new Vector2(gap * 0.95f, Mathf.Lerp(floor, rim, 0.35f));
                        break;
                    case 3:
                        point = new Vector2(gap * 0.7f, floor);
                        break;
                    default:
                        point = new Vector2(0f, floor);
                        break;
                }
            }

            return new Vector2(point.x * sign, point.y);
        }

        // The side wall: straight up from the sole, curving in towards the rim. s = 0 at the sole, 1 at the rim.
        private static Vector2 Wall(float s, float half, float rim, float padded)
        {
            float across = half - (half - padded) * Mathf.Pow(s, 2.2f);
            float height = rim * (1f - Mathf.Pow(1f - s, 1.6f));
            return new Vector2(across, height);
        }

        /// <summary>A point on the outside of the upper's side wall, pushed out a little: for things laid over it.</summary>
        private static Vector3 WallPoint(float u, float s, int side, float outset)
        {
            Vector3[] ring = UpperRing(u, outset);
            float index = Mathf.Clamp01(s) * (WallPoints - 1);
            int low = Mathf.Min((int)index, WallPoints - 2);
            float t = index - low;

            Vector3 a = side < 0 ? ring[low] : ring[RingPoints - 1 - low];
            Vector3 b = side < 0 ? ring[low + 1] : ring[RingPoints - 2 - low];
            return Vector3.Lerp(a, b, t);
        }

        /// <summary>The sole's outline as a closed loop at a given height: up one side, back down the other.</summary>
        private static Vector3[] Outline(Func<float, float> height, float inset, float from, float to)
        {
            // One extra point joins the end of the loop back to its start. Where the outline has been
            // pushed outwards it doesn't come to a point at the heel, and would otherwise be left open there.
            int around = (Stations + 1) * 2;
            var loop = new Vector3[around + 1];
            for (int i = 0; i <= Stations; i++)
            {
                float t = Spaced(i, Stations);
                float u = Mathf.Lerp(from, to, t);
                // A shortened sole (the plate, the insole) follows the full outline in the middle and is
                // rounded off at its own two ends the way the full sole is at the heel and toe.
                float half = Mathf.Max(Mathf.Min(ShoeProfile.HalfWidth(u), ShoeProfile.HalfWidth(t) * 1.3f) - inset, 0f);
                float x = ShoeProfile.X(u);
                float z = ShoeProfile.CentreZ(u);
                float y = height(u);
                loop[i] = new Vector3(x, y, z - half);
                loop[around - 1 - i] = new Vector3(x, y, z + half);
            }

            loop[around] = loop[0];
            return loop;
        }

        /// <summary>The same outline as a strip of left-right pairs, for filling in the flat faces.</summary>
        private static List<Vector3[]> Faces(Func<float, float> height, float inset, float from, float to)
        {
            Vector3[] loop = Outline(height, inset, from, to);
            var strip = new List<Vector3[]>(Stations + 1);
            int around = (Stations + 1) * 2;
            for (int i = 0; i <= Stations; i++)
            {
                strip.Add(new[] { loop[i], loop[around - 1 - i] });
            }

            return strip;
        }

        private static void AddSoleSlab(MeshData data, Func<float, float> bottom, Func<float, float> top, float inset,
            float from, float to, Color tint, Vector2 uv)
        {
            float mid = (from + to) * 0.5f;
            var inside = new Vector3(ShoeProfile.X(mid), (bottom(mid) + top(mid)) * 0.5f, ShoeProfile.CentreZ(mid));
            // Judge the wall's facing from the middle of one side; at the heel tip the outline is a point.
            data.AddGrid(new List<Vector3[]> { Outline(bottom, inset, from, to), Outline(top, inset, from, to) },
                tint, inside, uv, Stations / 2);
            data.AddGrid(Faces(bottom, inset, from, to), tint, inside, uv);
            data.AddGrid(Faces(top, inset, from, to), tint, inside, uv);
        }

        // ---- Mesh assembly ---------------------------------------------------------------------

        private sealed class MeshData
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<Color> _colors = new List<Color>();
            private readonly List<int> _triangles = new List<int>();

            /// <summary>
            /// Adds a sheet of quads between successive rings. Each call has its own vertices, so the
            /// sheet is smooth within itself and has a hard edge against its neighbours.
            /// </summary>
            /// <param name="inside">A point on the back side of the sheet; the faces are wound to look away from it.</param>
            /// <param name="uv">UV for every vertex, unless <paramref name="weave"/> is set.</param>
            /// <param name="testPoint">Which point along the ring to judge the facing from.</param>
            /// <param name="weave">Lay UVs out across the sheet, for the fabric weave.</param>
            public void AddGrid(List<Vector3[]> rings, Color color, Vector3 inside, Vector2 uv, int testPoint = 0,
                bool weave = false)
            {
                int ringCount = rings.Count;
                int points = rings[0].Length;
                int start = _vertices.Count;

                for (int r = 0; r < ringCount; r++)
                {
                    for (int p = 0; p < points; p++)
                    {
                        _vertices.Add(rings[r][p]);
                        _colors.Add(color);
                        _uvs.Add(weave ? new Vector2(r / (float)(ringCount - 1), p / (float)(points - 1)) : uv);
                    }
                }

                bool flip = FacesInwards(rings, inside, Mathf.Clamp(testPoint, 0, points - 2));
                for (int r = 0; r < ringCount - 1; r++)
                {
                    for (int p = 0; p < points - 1; p++)
                    {
                        int a = start + r * points + p;
                        int b = a + 1;
                        int c = a + points;
                        int d = c + 1;
                        if (flip)
                        {
                            _triangles.AddRange(new[] { a, c, b, b, c, d });
                        }
                        else
                        {
                            _triangles.AddRange(new[] { a, b, c, b, d, c });
                        }
                    }
                }
            }

            /// <summary>A sheet with thickness: top, underside and edge.</summary>
            public void AddSlab(List<Vector3[]> top, float thickness, Color color, bool weave)
            {
                var bottom = new List<Vector3[]>(top.Count);
                Vector3 centre = Vector3.zero;
                int count = 0;
                foreach (Vector3[] ring in top)
                {
                    var lower = new Vector3[ring.Length];
                    for (int i = 0; i < ring.Length; i++)
                    {
                        lower[i] = ring[i] + Vector3.down * thickness;
                        centre += ring[i];
                        count++;
                    }

                    bottom.Add(lower);
                }

                centre = centre / count + Vector3.down * (thickness * 0.5f);
                int middle = top[0].Length / 2;
                AddGrid(top, color, centre, Vector2.zero, middle, weave);
                AddGrid(bottom, color, centre, Vector2.zero, middle, weave);
                AddGrid(new List<Vector3[]> { Perimeter(top), Perimeter(bottom) }, color, centre, Vector2.zero);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                mesh.SetUVs(0, _uvs);
                mesh.SetColors(_colors);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            // The outer edge of a sheet, as a closed loop.
            private static Vector3[] Perimeter(List<Vector3[]> rings)
            {
                var loop = new List<Vector3>();
                int last = rings.Count - 1;
                int points = rings[0].Length;

                for (int p = 0; p < points; p++)
                {
                    loop.Add(rings[0][p]);
                }

                for (int r = 1; r <= last; r++)
                {
                    loop.Add(rings[r][points - 1]);
                }

                for (int p = points - 2; p >= 0; p--)
                {
                    loop.Add(rings[last][p]);
                }

                for (int r = last - 1; r >= 0; r--)
                {
                    loop.Add(rings[r][0]);
                }

                return loop.ToArray();
            }

            // Looks at one quad near the middle of the sheet and asks which way it faces.
            private static bool FacesInwards(List<Vector3[]> rings, Vector3 inside, int point)
            {
                int ring = Mathf.Clamp(rings.Count / 2 - 1, 0, rings.Count - 2);
                Vector3 a = rings[ring][point];
                Vector3 b = rings[ring][point + 1];
                Vector3 c = rings[ring + 1][point];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Vector3 outward = (a + b + c) / 3f - inside;
                return Vector3.Dot(normal, outward) < 0f;
            }
        }
    }
}
