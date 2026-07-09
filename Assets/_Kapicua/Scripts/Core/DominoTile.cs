using System;
using System.Collections.Generic;

namespace Kapicua.Core
{
    /// <summary>One domino tile. Immutable; High >= Low so (3,5) and (5,3) are the same tile.</summary>
    public readonly struct DominoTile : IEquatable<DominoTile>
    {
        public readonly int High;
        public readonly int Low;

        public DominoTile(int a, int b)
        {
            if (a < 0 || a > 6 || b < 0 || b > 6)
                throw new ArgumentOutOfRangeException(nameof(a), $"Pip values must be 0-6, got ({a},{b})");
            High = Math.Max(a, b);
            Low = Math.Min(a, b);
        }

        public bool IsDouble => High == Low;
        public int PipSum => High + Low;

        public bool Has(int value) => High == value || Low == value;

        /// <summary>The pip value on the opposite half from <paramref name="value"/>.</summary>
        public int Other(int value)
        {
            if (High == value) return Low;
            if (Low == value) return High;
            throw new ArgumentException($"Tile {this} does not contain {value}");
        }

        /// <summary>All 28 tiles of a double-six set.</summary>
        public static List<DominoTile> FullSet()
        {
            var set = new List<DominoTile>(28);
            for (int a = 0; a <= 6; a++)
                for (int b = a; b <= 6; b++)
                    set.Add(new DominoTile(a, b));
            return set;
        }

        public bool Equals(DominoTile other) => High == other.High && Low == other.Low;
        public override bool Equals(object obj) => obj is DominoTile t && Equals(t);
        public override int GetHashCode() => High * 7 + Low;
        public static bool operator ==(DominoTile a, DominoTile b) => a.Equals(b);
        public static bool operator !=(DominoTile a, DominoTile b) => !a.Equals(b);
        public override string ToString() => $"[{High}|{Low}]";
    }
}
