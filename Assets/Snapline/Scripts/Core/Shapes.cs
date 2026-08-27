using System;
using System.Collections.Generic;

namespace Snapline.Core
{
    /// <summary>
    /// A placeable polyomino, stored as a bitmask anchored at the top-left of its bounding box.
    ///
    /// Placement masks for every legal anchor on the board are precomputed once at static init.
    /// That turns "can this piece go here" into a single AND, and "can this piece go anywhere"
    /// into a short loop over at most 64 masks — which is what makes the dealer's fairness checks
    /// and the offline simulator cheap enough to run millions of times.
    /// </summary>
    public sealed class ShapeDef
    {
        /// <summary>
        /// Stable save id. NEVER renumber or reorder these: a saved run stores the tray by id,
        /// and a resumed run would deal different pieces if these shifted.
        /// </summary>
        public readonly int Id;
        public readonly string Name;
        public readonly int Width;
        public readonly int Height;
        public readonly int CellCount;

        /// <summary>Occupied cells as (col,row) offsets from the bounding-box corner.</summary>
        public readonly (int Col, int Row)[] Cells;

        /// <summary>Bitmask with the shape anchored at board cell (0,0).</summary>
        public readonly ulong BaseMask;

        /// <summary>One mask per legal anchor position on the board, in anchor-index order.</summary>
        public readonly ulong[] PlacementMasks;

        /// <summary>Board cell index of the anchor for each entry in <see cref="PlacementMasks"/>.</summary>
        public readonly int[] PlacementAnchors;

        internal ShapeDef(int id, string name, string pattern)
        {
            Id = id;
            Name = name;

            string[] rows = pattern.Split('/');
            Height = rows.Length;
            int w = 0;
            foreach (string row in rows) if (row.Length > w) w = row.Length;
            Width = w;

            var cells = new List<(int, int)>();
            ulong mask = 0UL;
            for (int r = 0; r < rows.Length; r++)
            {
                string row = rows[r];
                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] == '.') continue;
                    cells.Add((c, r));
                    mask |= 1UL << (r * Board.Width + c);
                }
            }

            Cells = cells.ToArray();
            CellCount = Cells.Length;
            BaseMask = mask;

            if (CellCount == 0)
                throw new ArgumentException($"Shape '{name}' has no cells.", nameof(pattern));

            // A shape anchored at (row, col) is just BaseMask << (row*8 + col). That identity only
            // holds while col + Width <= 8 — otherwise the shift wraps cells onto the next row —
            // which is exactly the bound enforced here.
            var masks = new List<ulong>();
            var anchors = new List<int>();
            for (int r = 0; r + Height <= Board.Height; r++)
            {
                for (int c = 0; c + Width <= Board.Width; c++)
                {
                    int anchor = r * Board.Width + c;
                    masks.Add(BaseMask << anchor);
                    anchors.Add(anchor);
                }
            }

            PlacementMasks = masks.ToArray();
            PlacementAnchors = anchors.ToArray();
        }
    }

    /// <summary>
    /// The shape catalogue. Ids are permanent (see <see cref="ShapeDef.Id"/>); append new shapes at
    /// the end rather than inserting, or you invalidate every save file in the wild.
    /// </summary>
    public static class Shapes
    {
        public static readonly ShapeDef[] All;

        static Shapes()
        {
            All = new[]
            {
                // --- singles and lines -------------------------------------------------
                new ShapeDef(0,  "Dot",     "X"),
                new ShapeDef(1,  "I2H",     "XX"),
                new ShapeDef(2,  "I2V",     "X/X"),
                new ShapeDef(3,  "I3H",     "XXX"),
                new ShapeDef(4,  "I3V",     "X/X/X"),
                new ShapeDef(5,  "I4H",     "XXXX"),
                new ShapeDef(6,  "I4V",     "X/X/X/X"),
                new ShapeDef(7,  "I5H",     "XXXXX"),
                new ShapeDef(8,  "I5V",     "X/X/X/X/X"),

                // --- blocks ------------------------------------------------------------
                new ShapeDef(9,  "Square2", "XX/XX"),
                new ShapeDef(10, "Square3", "XXX/XXX/XXX"),
                new ShapeDef(11, "Rect3x2", "XXX/XXX"),
                new ShapeDef(12, "Rect2x3", "XX/XX/XX"),

                // --- small L (3 cells in a 2x2 box), all four rotations -----------------
                new ShapeDef(13, "L3_NE",   "XX/.X"),
                new ShapeDef(14, "L3_SE",   ".X/XX"),
                new ShapeDef(15, "L3_SW",   "X./XX"),
                new ShapeDef(16, "L3_NW",   "XX/X."),

                // --- big L (5 cells in a 3x3 box), all four rotations -------------------
                new ShapeDef(17, "L5_NE",   "XXX/..X/..X"),
                new ShapeDef(18, "L5_SE",   "..X/..X/XXX"),
                new ShapeDef(19, "L5_SW",   "X../X../XXX"),
                new ShapeDef(20, "L5_NW",   "XXX/X../X.."),

                // --- T tetromino, all four rotations ------------------------------------
                new ShapeDef(21, "T_N",     "XXX/.X."),
                new ShapeDef(22, "T_E",     "X./XX/X."),
                new ShapeDef(23, "T_S",     ".X./XXX"),
                new ShapeDef(24, "T_W",     ".X/XX/.X"),

                // --- S and Z tetrominoes -------------------------------------------------
                new ShapeDef(25, "S_H",     ".XX/XX."),
                new ShapeDef(26, "S_V",     "X./XX/.X"),
                new ShapeDef(27, "Z_H",     "XX./.XX"),
                new ShapeDef(28, "Z_V",     ".X/XX/X."),

                // --- diagonals: cheap-looking but brutal on a congested board ------------
                new ShapeDef(29, "Diag2_A", "X./.X"),
                new ShapeDef(30, "Diag2_B", ".X/X."),
                new ShapeDef(31, "Diag3_A", "X../.X./..X"),
                new ShapeDef(32, "Diag3_B", "..X/.X./X.."),
            };

            ById = new ShapeDef[All.Length];
            for (int i = 0; i < All.Length; i++)
            {
                ShapeDef s = All[i];
                if (s.Id != i)
                    throw new InvalidOperationException(
                        $"Shape catalogue is misnumbered at index {i} ('{s.Name}' has id {s.Id}). " +
                        "Ids are save-format identifiers and must equal the array index.");
                ById[s.Id] = s;
            }

            // Computed here rather than in a field initializer. Static field initializers run
            // before the static constructor body, so All was still null and this threw
            // TypeInitializationException on the very first touch of the catalogue.
            int max = 0;
            for (int i = 0; i < All.Length; i++)
                if (All[i].CellCount > max) max = All[i].CellCount;
            MaxCellCount = max;
        }

        /// <summary>Catalogue indexed by stable save id. Same contents as <see cref="All"/>.</summary>
        public static readonly ShapeDef[] ById;

        /// <summary>Largest cell count in the catalogue. Normalises the congestion weighting.</summary>
        public static readonly int MaxCellCount;

        public static int Count => All.Length;

        public static ShapeDef Get(int id)
        {
            if (id < 0 || id >= ById.Length)
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown shape id.");
            return ById[id];
        }
    }
}
