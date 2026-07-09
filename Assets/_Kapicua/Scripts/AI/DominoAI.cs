using System.Collections.Generic;
using System.Linq;
using Kapicua.Core;

namespace Kapicua.AI
{
    /// <summary>
    /// Heuristic AI: win with capicúa when possible, shed doubles early
    /// (they're a liability in blocked games), otherwise dump the heaviest tile,
    /// breaking ties toward the suit it holds most of.
    /// </summary>
    public static class DominoAI
    {
        public static Move ChooseMove(RoundEngine round, int player)
        {
            var moves = round.GetLegalMoves(player);
            var hand = round.Hands[player];

            // Winning move: prefer capicúa (double points), then any domino.
            if (hand.Count == 1)
            {
                var capicua = moves.Where(m => !m.Tile.IsDouble && round.Board.MatchesBothEnds(m.Tile));
                if (capicua.Any()) return capicua.First();
                return moves[0];
            }

            int[] suitCount = new int[7];
            foreach (var t in hand)
            {
                suitCount[t.High]++;
                if (!t.IsDouble) suitCount[t.Low]++;
            }

            return moves
                .OrderByDescending(m => m.Tile.IsDouble ? 1 : 0)
                .ThenByDescending(m => m.Tile.PipSum)
                // Keep ends on suits we're long in, so we're less likely to pass later.
                .ThenByDescending(m => suitCount[m.Tile.High] + suitCount[m.Tile.Low])
                .First();
        }
    }
}
