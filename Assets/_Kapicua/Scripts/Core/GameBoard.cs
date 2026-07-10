using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kapicua.Core
{
    /// <summary>
    /// Represents the chain of played domino tiles on the table.
    /// Tracks the two open ends that determine valid plays.
    ///
    /// Dominican rules:
    /// - The chain grows from both ends.
    /// - Doubles are placed crossways (handled visually; logically they still extend one end).
    /// - The first tile sets both open ends (e.g., [6|6] → both ends = 6).
    /// </summary>
    public class GameBoard
    {
        // The chain of tiles in play order
        public List<BoardTile> Chain { get; private set; } = new List<BoardTile>();

        // The two open ends of the chain
        public int LeftEnd { get; private set; } = -1;
        public int RightEnd { get; private set; } = -1;

        public bool IsEmpty => Chain.Count == 0;

        /// <summary>
        /// Places the very first tile of the round. Must be the double-6 (or highest double).
        /// </summary>
        public void PlaceFirstTile(DominoTile tile, int seat)
        {
            if (!IsEmpty) throw new InvalidOperationException("Board already has tiles.");
            Chain.Add(new BoardTile { Tile = tile, PlayedAt = BoardEnd.Both, Seat = seat });
            LeftEnd = tile.SideA;
            RightEnd = tile.SideB;
        }

        /// <summary>
        /// Returns all ends at which a player can legally place the given tile.
        /// Returns an empty list if no valid play exists (player must pass).
        /// </summary>
        public List<BoardEnd> GetValidEnds(DominoTile tile)
        {
            var ends = new List<BoardEnd>();
            if (IsEmpty) return ends;

            bool leftMatch = tile.CanPlayAt(LeftEnd);
            bool rightMatch = tile.CanPlayAt(RightEnd);

            // Avoid duplicates when both ends are equal
            if (leftMatch) ends.Add(BoardEnd.Left);
            if (rightMatch && RightEnd != LeftEnd) ends.Add(BoardEnd.Right);
            if (rightMatch && RightEnd == LeftEnd && !leftMatch) ends.Add(BoardEnd.Right);

            return ends;
        }

        /// <summary>
        /// Returns true if the player has at least one legal play.
        /// </summary>
        public bool HasValidPlay(List<DominoTile> hand)
        {
            if (IsEmpty) return true;
            foreach (var tile in hand)
            {
                if (GetValidEnds(tile).Count > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Places a tile at the specified end of the chain.
        /// </summary>
        public void PlaceTile(DominoTile tile, BoardEnd end, int seat)
        {
            if (IsEmpty) throw new InvalidOperationException("Use PlaceFirstTile for first tile.");

            var placed = new BoardTile { Tile = tile, PlayedAt = end, Seat = seat };
            Chain.Add(placed);

            if (end == BoardEnd.Left)
            {
                LeftEnd = tile.GetNewOpenEnd(LeftEnd);
            }
            else // Right
            {
                RightEnd = tile.GetNewOpenEnd(RightEnd);
            }
        }

        /// <summary>
        /// Checks if the last played tile is a KAPICUA:
        /// - It was the player's last tile (domino play)
        /// - The tile's two sides match BOTH open ends of the chain
        /// - Neither matched end is a blank (0)
        /// - The tile itself is not a double
        ///
        /// Kapicua gives the winning team +30 bonus points (GameRules.KAPICUA_BONUS).
        /// </summary>
        public bool IsKapicua(DominoTile lastTile)
        {
            if (Chain.Count < 2) return false;  // Need at least 2 tiles for both ends to differ
            if (lastTile.IsDouble) return false;  // Doubles don't count for Kapicua
            if (LeftEnd == 0 || RightEnd == 0) return false;  // Blanks nullify Kapicua

            // Both open ends must be the same value, and the tile must match
            return LeftEnd == RightEnd &&
                   lastTile.CanPlayAt(LeftEnd) &&
                   lastTile.CanPlayAt(RightEnd);
        }

        public void Reset()
        {
            Chain.Clear();
            LeftEnd = -1;
            RightEnd = -1;
        }
    }

    [Serializable]
    public class BoardTile
    {
        public DominoTile Tile;
        public BoardEnd PlayedAt;
        public int Seat;  // Which player seat (0-3) placed this tile
    }
}
