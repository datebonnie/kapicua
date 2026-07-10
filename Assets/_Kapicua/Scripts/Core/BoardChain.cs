using System;
using System.Collections.Generic;

namespace Kapicua.Core
{
    public enum BoardEnd { Left, Right, Both }

    /// <summary>A tile as placed on the chain, ordered left-to-right.</summary>
    public readonly struct PlacedTile
    {
        public readonly DominoTile Tile;
        /// <summary>Pip value facing the left neighbour (equal to OutwardValue for the leftmost tile's left side, etc.).</summary>
        public readonly int LeftValue;
        public readonly int RightValue;

        public PlacedTile(DominoTile tile, int leftValue, int rightValue)
        {
            Tile = tile;
            LeftValue = leftValue;
            RightValue = rightValue;
        }
    }

    /// <summary>The line of played tiles with two open ends.</summary>
    public class BoardChain
    {
        readonly List<PlacedTile> _tiles = new List<PlacedTile>();

        public IReadOnlyList<PlacedTile> Tiles => _tiles;
        public bool IsEmpty => _tiles.Count == 0;
        public int LeftEnd => _tiles[0].LeftValue;
        public int RightEnd => _tiles[_tiles.Count - 1].RightValue;

        public bool CanPlace(DominoTile tile) =>
            IsEmpty || tile.Has(LeftEnd) || tile.Has(RightEnd);

        public bool CanPlaceAt(DominoTile tile, BoardEnd end) =>
            IsEmpty || tile.Has(end == BoardEnd.Left ? LeftEnd : RightEnd);

        /// <summary>True when the tile could legally go on either open end (the capicúa condition, pre-placement).</summary>
        public bool MatchesBothEnds(DominoTile tile) =>
            !IsEmpty && tile.Has(LeftEnd) && tile.Has(RightEnd);

        public void Place(DominoTile tile, BoardEnd end)
        {
            if (IsEmpty)
            {
                _tiles.Add(new PlacedTile(tile, tile.High, tile.Low));
                return;
            }

            if (end == BoardEnd.Left)
            {
                if (!tile.Has(LeftEnd))
                    throw new InvalidOperationException($"{tile} cannot be placed on left end {LeftEnd}");
                // The side matching LeftEnd faces right (inward); the other pip becomes the new left end.
                _tiles.Insert(0, new PlacedTile(tile, tile.Other(LeftEnd), LeftEnd));
            }
            else
            {
                if (!tile.Has(RightEnd))
                    throw new InvalidOperationException($"{tile} cannot be placed on right end {RightEnd}");
                _tiles.Add(new PlacedTile(tile, RightEnd, tile.Other(RightEnd)));
            }
        }
    }
}
