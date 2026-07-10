using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kapicua.Core
{
    /// <summary>
    /// Tracks match score and round history for both teams.
    /// Team 0 = "Nosotros" (us), Team 1 = "Ellos" (them).
    /// First to SCORE_TO_WIN (200) wins the match.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Match Score")]
        public int TeamAScore;  // Nosotros
        public int TeamBScore;  // Ellos

        public List<RoundRecord> RoundHistory = new List<RoundRecord>();

        // Events
        public event Action<int, int> OnScoreChanged;       // (teamAScore, teamBScore)
        public event Action<RoundRecord> OnRoundRecorded;
        public event Action<int> OnMatchWon;                // winning team (0 or 1)

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ResetMatch()
        {
            TeamAScore = 0;
            TeamBScore = 0;
            RoundHistory.Clear();
            OnScoreChanged?.Invoke(0, 0);
        }

        /// <summary>
        /// Records a completed round and updates cumulative match score.
        /// </summary>
        public void RecordRound(RoundResult result, int roundNumber)
        {
            var record = new RoundRecord
            {
                RoundNumber = roundNumber,
                Reason = result.Reason,
                WinningTeam = result.WinningTeam,
                PointsScored = result.PointsScored,
                IsKapicua = result.IsKapicua,
                TeamARunning = TeamAScore,
                TeamBRunning = TeamBScore
            };

            if (result.WinningTeam == 0)
                TeamAScore += result.PointsScored;
            else
                TeamBScore += result.PointsScored;

            record.TeamAAfter = TeamAScore;
            record.TeamBAfter = TeamBScore;
            RoundHistory.Add(record);

            OnRoundRecorded?.Invoke(record);
            OnScoreChanged?.Invoke(TeamAScore, TeamBScore);

            // Check win condition
            if (TeamAScore >= GameRules.SCORE_TO_WIN)
                OnMatchWon?.Invoke(0);
            else if (TeamBScore >= GameRules.SCORE_TO_WIN)
                OnMatchWon?.Invoke(1);
        }

        /// <summary>
        /// Awards Pase Corrido bonus (all 3 others passed) to the team of the last player who played.
        /// </summary>
        public void AwardPaseCorridoBonus(int lastPlaySeat)
        {
            int team = GameRules.GetTeam(lastPlaySeat);
            var bonusResult = new RoundResult
            {
                Reason = RoundEndReason.Domino,
                WinningTeam = team,
                PointsScored = GameRules.PASE_CORRIDO_BONUS,
                IsKapicua = false
            };
            RecordRound(bonusResult, RoundHistory.Count + 1);
        }

        public bool IsMatchOver() =>
            TeamAScore >= GameRules.SCORE_TO_WIN || TeamBScore >= GameRules.SCORE_TO_WIN;

        public int GetLeadingTeam() => TeamAScore >= TeamBScore ? 0 : 1;
    }

    [Serializable]
    public class RoundRecord
    {
        public int RoundNumber;
        public RoundEndReason Reason;
        public int WinningTeam;
        public int PointsScored;
        public bool IsKapicua;
        public int TeamARunning;
        public int TeamBRunning;
        public int TeamAAfter;
        public int TeamBAfter;
    }
}
