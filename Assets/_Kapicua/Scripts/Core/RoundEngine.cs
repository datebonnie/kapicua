using System;
using System.Collections.Generic;
using System.Linq;

namespace Kapicua.Core
{
    public readonly struct Move
    {
        public readonly DominoTile Tile;
        public readonly BoardEnd End;
        public Move(DominoTile tile, BoardEnd end) { Tile = tile; End = end; }
        public override string ToString() => $"{Tile}->{End}";
    }

    public class RoundResult
    {
        /// <summary>0 or 1; -1 for a tied blocked round (no score).</summary>
        public int WinningTeam;
        /// <summary>Player who dominoed, or on a block, the player with the lowest hand on the winning team. -1 on tie.</summary>
        public int WinningPlayer;
        public int Points;
        public bool Capicua;
        public bool Blocked;

        // ── Compatibility aliases (GameRules/ScoreManager call sites) ──
        public RoundEndReason Reason
        {
            get => Blocked ? RoundEndReason.Tranque : RoundEndReason.Domino;
            set => Blocked = value == RoundEndReason.Tranque;
        }
        public int WinningSeat { get => WinningPlayer; set => WinningPlayer = value; }
        public int PointsScored { get => Points; set => Points = value; }
        public bool IsKapicua { get => Capicua; set => Capicua = value; }
    }

    /// <summary>
    /// One round (hand) of 4-player block dominoes. Players 0-3 seated in turn order;
    /// teams are player index % 2. All 28 tiles are dealt, 7 each.
    /// </summary>
    public class RoundEngine
    {
        public const int PlayerCount = 4;
        public const int HandSize = 7;
        static readonly DominoTile DoubleSix = new DominoTile(6, 6);

        public BoardChain Board { get; } = new BoardChain();
        public List<DominoTile>[] Hands { get; } = new List<DominoTile>[PlayerCount];
        public int CurrentPlayer { get; private set; }
        public int ConsecutivePasses { get; private set; }
        /// <summary>Set once the round ends; null while in progress.</summary>
        public RoundResult Result { get; private set; }
        public bool IsOver => Result != null;
        /// <summary>Round 1 of a match must open with the double six.</summary>
        public bool RequireDoubleSixOpening { get; }

        public static int TeamOf(int player) => player % 2;

        /// <param name="startingPlayer">Ignored when requireDoubleSixOpening is true (the 6-6 holder starts).</param>
        public RoundEngine(Random rng, bool requireDoubleSixOpening, int startingPlayer = 0)
        {
            RequireDoubleSixOpening = requireDoubleSixOpening;

            var deck = DominoTile.FullSet();
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
            for (int p = 0; p < PlayerCount; p++)
                Hands[p] = deck.GetRange(p * HandSize, HandSize);

            if (requireDoubleSixOpening)
                CurrentPlayer = Array.FindIndex(Hands, h => h.Contains(DoubleSix));
            else
                CurrentPlayer = startingPlayer;
        }

        public List<Move> GetLegalMoves(int player)
        {
            var moves = new List<Move>();
            if (IsOver || player != CurrentPlayer) return moves;

            foreach (var tile in Hands[player])
            {
                if (Board.IsEmpty)
                {
                    if (RequireDoubleSixOpening && tile != DoubleSix) continue;
                    moves.Add(new Move(tile, BoardEnd.Right));
                    continue;
                }
                if (tile.Has(Board.LeftEnd)) moves.Add(new Move(tile, BoardEnd.Left));
                // Avoid a duplicate move when both ends have the same value.
                if (tile.Has(Board.RightEnd) && !(tile.Has(Board.LeftEnd) && Board.LeftEnd == Board.RightEnd))
                    moves.Add(new Move(tile, BoardEnd.Right));
            }
            return moves;
        }

        public bool MustPass(int player) => GetLegalMoves(player).Count == 0;

        public void Pass(int player)
        {
            EnsureTurn(player);
            if (GetLegalMoves(player).Count > 0)
                throw new InvalidOperationException($"Player {player} has legal moves and cannot pass");

            ConsecutivePasses++;
            if (ConsecutivePasses >= PlayerCount)
                FinishBlocked();
            else
                AdvanceTurn();
        }

        public void Play(int player, Move move)
        {
            EnsureTurn(player);
            if (!Hands[player].Contains(move.Tile))
                throw new InvalidOperationException($"Player {player} does not hold {move.Tile}");
            if (!Board.CanPlaceAt(move.Tile, move.End))
                throw new InvalidOperationException($"Illegal move {move}");
            if (Board.IsEmpty && RequireDoubleSixOpening && move.Tile != DoubleSix)
                throw new InvalidOperationException("The first round must open with the double six");

            bool capicua = Hands[player].Count == 1
                           && !move.Tile.IsDouble
                           && Board.MatchesBothEnds(move.Tile);

            Board.Place(move.Tile, move.End);
            Hands[player].Remove(move.Tile);
            ConsecutivePasses = 0;
            _lastPlayer = player;

            if (Hands[player].Count == 0)
                FinishDomino(player, capicua);
            else
                AdvanceTurn();
        }

        void FinishDomino(int winner, bool capicua)
        {
            int winningTeam = TeamOf(winner);
            int points = 0;
            for (int p = 0; p < PlayerCount; p++)
                if (TeamOf(p) != winningTeam)
                    points += Hands[p].Sum(t => t.PipSum);
            if (capicua) points *= 2;

            Result = new RoundResult
            {
                WinningTeam = winningTeam,
                WinningPlayer = winner,
                Points = points,
                Capicua = capicua,
                Blocked = false,
            };
        }

        void FinishBlocked()
        {
            // House rule: the tranque initiator (last player to place a tile)
            // duels the player to their RIGHT — lower hand total wins for their
            // team. Ties go to the initiator (they closed the game).
            int initiator = _lastPlayer >= 0 ? _lastPlayer : 0;
            int rival = (initiator + 1) % PlayerCount;
            int initiatorPips = Hands[initiator].Sum(t => t.PipSum);
            int rivalPips     = Hands[rival].Sum(t => t.PipSum);
            int winner        = rivalPips < initiatorPips ? rival : initiator;

            int totalPips = 0;
            for (int p = 0; p < PlayerCount; p++)
                totalPips += Hands[p].Sum(t => t.PipSum);

            Result = new RoundResult
            {
                WinningTeam = TeamOf(winner),
                WinningPlayer = winner,
                Points = totalPips,
                Capicua = false,
                Blocked = true,
            };
        }

        int _lastPlayer = -1;

        void AdvanceTurn() => CurrentPlayer = (CurrentPlayer + 1) % PlayerCount;

        void EnsureTurn(int player)
        {
            if (IsOver) throw new InvalidOperationException("Round is over");
            if (player != CurrentPlayer)
                throw new InvalidOperationException($"It is player {CurrentPlayer}'s turn, not player {player}'s");
        }
    }
}
