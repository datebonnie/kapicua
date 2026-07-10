using System;
using UnityEngine;

namespace Kapicua.Core
{
    /// <summary>
    /// Represents a single domino tile with two pip values (A | B).
    /// Dominican domino uses a double-6 set: 28 tiles, values 0-0 through 6-6.
    /// </summary>
    [Serializable]
    public struct DominoTile : IEquatable<DominoTile>
    {
        public int SideA;
        public int SideB;

        /// <summary>Index in the canonical 28-tile set (0..27). Used for network serialization.</summary>
        public int TileIndex;

        public bool IsDouble => SideA == SideB;
        public int TotalPips => SideA + SideB;

        // ── Engine-dialect API (BoardChain / RoundEngine / DominoAI / TileView) ──

        /// <summary>
        /// Creates a tile from two pip values, computing the canonical TileIndex
        /// (same ordering as DominoSet.CreateFullSet: [0|0],[0|1],...,[6|6]).
        /// </summary>
        public DominoTile(int a, int b)
        {
            SideA = a;
            SideB = b;
            int lo = Math.Min(a, b), hi = Math.Max(a, b);
            TileIndex = lo * 7 - lo * (lo - 1) / 2 + (hi - lo);
        }

        public int High => Math.Max(SideA, SideB);
        public int Low => Math.Min(SideA, SideB);
        /// <summary>Alias of TotalPips.</summary>
        public int PipSum => SideA + SideB;

        /// <summary>True if either side shows the given pip value.</summary>
        public bool Has(int value) => SideA == value || SideB == value;

        /// <summary>Given one side's pip value, returns the other side's value.</summary>
        public int Other(int value) => GetNewOpenEnd(value);

        /// <summary>The canonical 28-tile double-six set.</summary>
        public static System.Collections.Generic.List<DominoTile> FullSet() => DominoSet.CreateFullSet();

        /// <summary>
        /// Returns true if this tile can be placed at an open end with the given value.
        /// </summary>
        public bool CanPlayAt(int openEnd)
        {
            return SideA == openEnd || SideB == openEnd;
        }

        /// <summary>
        /// When placing this tile at 'openEnd', returns the value that will become the new open end.
        /// Call only after confirming CanPlayAt returns true.
        /// </summary>
        public int GetNewOpenEnd(int openEnd)
        {
            if (SideA == openEnd) return SideB;
            if (SideB == openEnd) return SideA;
            throw new InvalidOperationException($"Tile {this} cannot be placed at end {openEnd}");
        }

        public DominoTile Canonical()
        {
            if (SideA <= SideB) return this;
            return new DominoTile { SideA = SideB, SideB = SideA, TileIndex = TileIndex };
        }

        public bool Equals(DominoTile other) => TileIndex == other.TileIndex;
        public override bool Equals(object obj) => obj is DominoTile t && Equals(t);
        public override int GetHashCode() => TileIndex;
        public static bool operator ==(DominoTile a, DominoTile b) => a.Equals(b);
        public static bool operator !=(DominoTile a, DominoTile b) => !a.Equals(b);
        public override string ToString() => $"[{SideA}|{SideB}]";
    }
}
