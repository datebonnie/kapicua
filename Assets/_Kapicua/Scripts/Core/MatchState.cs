using System.Collections.Generic;

namespace Kapicua.Core
{
    /// <summary>Cross-round match state: team scores up to the target, and who leads the next round.</summary>
    public class MatchState
    {
        public const int TargetScore = 200;

        public int[] TeamScores { get; } = new int[2];
        public List<RoundResult> History { get; } = new List<RoundResult>();
        /// <summary>Leader of the next round. Round 1 ignores this (6-6 holder opens).</summary>
        public int NextStartingPlayer { get; private set; }
        public int RoundsPlayed => History.Count;
        public bool IsFirstRound => History.Count == 0;

        public bool IsOver => TeamScores[0] >= TargetScore || TeamScores[1] >= TargetScore;
        public int WinningTeam => !IsOver ? -1 : (TeamScores[0] >= TargetScore ? 0 : 1);

        public void ApplyRound(RoundResult result, int roundStartingPlayer)
        {
            History.Add(result);
            if (result.WinningTeam >= 0)
            {
                TeamScores[result.WinningTeam] += result.Points;
                NextStartingPlayer = result.WinningPlayer;
            }
            else
            {
                // Tied blocked round: lead passes to the next seat.
                NextStartingPlayer = (roundStartingPlayer + 1) % RoundEngine.PlayerCount;
            }
        }
    }
}
