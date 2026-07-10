using System.Collections.Generic;
using UnityEngine;

namespace Kapicua.Core
{
    /// <summary>
    /// Encodes all Dominican Domino rules (Regla de Patio — home/colmado style).
    ///
    /// SCORING SUMMARY (playing to 200):
    /// ─────────────────────────────────────────────────────────────────────
    /// 1. DOMINO (winner plays last tile)
    ///    Winning team scores = sum of opponent team's remaining pip values.
    ///    + KAPICUA BONUS: +30 if last tile matches both open ends (no blanks, no doubles).
    ///
    /// 2. PASE CORRIDO (3 consecutive passes without anyone playing)
    ///    Team that last played earns +30 points.
    ///    Consecutive pases are unlimited; each run of 3 passes = +30.
    ///
    /// 3. TRANQUE (lock — no player can ever play again)
    ///    The player who initiated the tranque (played the locking tile) compares
    ///    hands with the player to their RIGHT. The LOWER pip sum wins the round
    ///    for their team. Tie goes to the initiator (they closed the game).
    ///    Winning team scores = sum of ALL remaining pips (all 4 hands).
    ///
    /// CHAMPION INSIGHT — Joaquín Martínez (12x World Champion, Dominican Republic):
    /// • Track every tile that has been played. After ~14 tiles, you can deduce
    ///   what suits opponents are missing based on their passes.
    /// • A pass reveals the suit of the open end the player couldn't match.
    /// • Force your partner's strong suit by playing tiles that open that end.
    /// • The double-6 opening is sacred — play it immediately, never hold it.
    /// • Kapicua opportunities should be set up 2-3 turns in advance.
    /// ─────────────────────────────────────────────────────────────────────
    /// </summary>
    public static class GameRules
    {
        public const int SCORE_TO_WIN = 200;
        public const int KAPICUA_BONUS = 30;
        public const int PASE_CORRIDO_BONUS = 30;
        public const int CONSECUTIVE_PASSES_FOR_BONUS = 3; // all 3 other players pass in a row

        /// <summary>
        /// Evaluates the result of a completed round and returns a RoundResult.
        /// Called when: (a) a player plays their last tile, or (b) the game is locked.
        /// </summary>
        public static RoundResult EvaluateRound(
            List<DominoTile>[] hands,  // remaining tiles per seat [0..3]
            GameBoard board,
            RoundEndReason reason,
            int winningSeat,           // seat that played the last tile (domino winner, or tranque initiator)
            int leadSeat)              // seat that played first this round
        {
            var result = new RoundResult();
            result.Reason = reason;
            result.WinningSeat = winningSeat;

            // Teams: 0 & 2 = Team A (Nosotros), 1 & 3 = Team B (Ellos)
            int teamARemaining = SumTeamPips(hands, 0);
            int teamBRemaining = SumTeamPips(hands, 1);

            switch (reason)
            {
                case RoundEndReason.Domino:
                    // The team that went out scores the opponents' pip total
                    int winningTeam = winningSeat % 2 == 0 ? 0 : 1;
                    int losingTeam = 1 - winningTeam;
                    result.WinningTeam = winningTeam;
                    result.PointsScored = losingTeam == 0 ? teamARemaining : teamBRemaining;

                    // Kapicua check
                    var lastTile = board.Chain[board.Chain.Count - 1].Tile;
                    if (board.IsKapicua(lastTile))
                    {
                        result.IsKapicua = true;
                        result.PointsScored += KAPICUA_BONUS;
                    }
                    break;

                case RoundEndReason.Tranque:
                    // House rule: the tranque initiator (player who placed the locking
                    // tile) compares hands with the player to their RIGHT.
                    // Lower pip sum wins for their team; tie goes to the initiator.
                    int initiator = winningSeat >= 0 ? winningSeat : leadSeat;
                    int rival = NextSeat(initiator);          // seat to the right
                    int initiatorPips = SumHandPips(hands[initiator]);
                    int rivalPips     = SumHandPips(hands[rival]);
                    int duelWinner    = rivalPips < initiatorPips ? rival : initiator;

                    result.WinningTeam  = GetTeam(duelWinner);
                    result.WinningSeat  = duelWinner;
                    result.PointsScored = teamARemaining + teamBRemaining;
                    break;
            }

            return result;
        }

        /// <summary>Sum of pip values in a single hand.</summary>
        public static int SumHandPips(List<DominoTile> hand)
        {
            int total = 0;
            foreach (var t in hand) total += t.TotalPips;
            return total;
        }

        /// <summary>
        /// Sum of pip values for all tiles remaining in both seats of a team.
        /// Team 0 = seats 0 & 2, Team 1 = seats 1 & 3.
        /// </summary>
        public static int SumTeamPips(List<DominoTile>[] hands, int team)
        {
            int total = 0;
            int seat1 = team == 0 ? 0 : 1;
            int seat2 = team == 0 ? 2 : 3;
            foreach (var t in hands[seat1]) total += t.TotalPips;
            foreach (var t in hands[seat2]) total += t.TotalPips;
            return total;
        }

        /// <summary>
        /// Returns the seat index of the partner of a given seat.
        /// Seats 0 & 2 are partners; seats 1 & 3 are partners.
        /// </summary>
        public static int GetPartnerSeat(int seat) => (seat + 2) % 4;

        /// <summary>
        /// Returns the team (0 or 1) for a given seat.
        /// </summary>
        public static int GetTeam(int seat) => seat % 2;

        /// <summary>
        /// Next seat in clockwise order (0 → 1 → 2 → 3 → 0).
        /// Dominican domino plays counterclockwise in real life but
        /// we standardize to clockwise in code (1 = right of you).
        /// </summary>
        public static int NextSeat(int current) => (current + 1) % 4;

        /// <summary>
        /// Checks if the board is in a fully locked (tranque) state:
        /// no player has any valid move.
        /// </summary>
        public static bool IsTranque(List<DominoTile>[] hands, GameBoard board)
        {
            for (int seat = 0; seat < 4; seat++)
            {
                if (board.HasValidPlay(hands[seat])) return false;
            }
            return true;
        }
    }

    public enum RoundEndReason { Domino, Tranque }
}
