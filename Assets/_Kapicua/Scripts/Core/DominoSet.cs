using System;
using System.Collections.Generic;
using UnityEngine;
using Kapicua.Core;

namespace Kapicua.Core
{
    /// <summary>
    /// The complete double-6 domino set: 28 tiles.
    /// Handles shuffling and dealing.
    /// </summary>
    public static class DominoSet
    {
        public const int TOTAL_TILES = 28;
        public const int TILES_PER_PLAYER = 7;
        public const int NUM_PLAYERS = 4;

        /// <summary>
        /// Builds the canonical ordered set of 28 double-6 domino tiles.
        /// Index matches the standard canonical ordering: [0|0],[0|1],...[6|6]
        /// </summary>
        public static List<DominoTile> CreateFullSet()
        {
            var tiles = new List<DominoTile>(TOTAL_TILES);
            int index = 0;
            for (int a = 0; a <= 6; a++)
            {
                for (int b = a; b <= 6; b++)
                {
                    tiles.Add(new DominoTile { SideA = a, SideB = b, TileIndex = index++ });
                }
            }
            return tiles;  // 28 tiles total
        }

        /// <summary>
        /// Shuffles the tile set using a seeded RNG (seed shared over network for determinism).
        /// </summary>
        public static List<DominoTile> Shuffle(List<DominoTile> tiles, int seed)
        {
            var result = new List<DominoTile>(tiles);
            var rng = new System.Random(seed);
            int n = result.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (result[n], result[k]) = (result[k], result[n]);
            }
            return result;
        }

        /// <summary>
        /// Deals 7 tiles to each of 4 players from a shuffled set.
        /// Returns 4 hands (all 28 tiles are distributed).
        /// </summary>
        public static List<DominoTile>[] Deal(List<DominoTile> shuffled)
        {
            var hands = new List<DominoTile>[NUM_PLAYERS];
            for (int i = 0; i < NUM_PLAYERS; i++)
                hands[i] = new List<DominoTile>();

            for (int i = 0; i < TOTAL_TILES; i++)
                hands[i % NUM_PLAYERS].Add(shuffled[i]);

            return hands;
        }

        /// <summary>
        /// Finds which seat holds the double-6 (or highest double if not present).
        /// This player goes first in the opening round.
        /// </summary>
        public static int FindFirstPlayer(List<DominoTile>[] hands)
        {
            for (int targetDouble = 6; targetDouble >= 0; targetDouble--)
            {
                for (int seat = 0; seat < NUM_PLAYERS; seat++)
                {
                    foreach (var tile in hands[seat])
                    {
                        if (tile.IsDouble && tile.SideA == targetDouble)
                            return seat;
                    }
                }
            }
            return 0; // fallback
        }
    }
}
