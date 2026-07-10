using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kapicua.Core
{
    /// <summary>
    /// Manages whose turn it is and tracks consecutive passes.
    ///
    /// Dominican rules:
    /// - Play goes clockwise: 0 → 1 → 2 → 3 → 0
    /// - If you have no valid play, you PASS (knock on table / tap in app)
    /// - PASE CORRIDO: If all 3 other players pass in succession (without anyone playing),
    ///   the team that last successfully played earns +30 points, then a new "corrida" begins
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }

        public int CurrentSeat { get; private set; }
        public int RoundLeadSeat { get; private set; }      // who played first this round
        public int LastPlaySeat { get; private set; } = -1; // who last successfully played a tile
        public int ConsecutivePasses { get; private set; }
        public int TotalTurns { get; private set; }

        // Events
        public event Action<int> OnTurnChanged;              // new current seat
        public event Action<int, int> OnTilePlayedBySeat;    // seat, tileIndex
        public event Action<int> OnPassBySeat;               // seat
        public event Action<int> OnPaseCorridoTriggered;     // last play seat

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void StartRound(int leadSeat)
        {
            CurrentSeat = leadSeat;
            RoundLeadSeat = leadSeat;
            LastPlaySeat = -1;
            ConsecutivePasses = 0;
            TotalTurns = 0;
        }

        /// <summary>
        /// Call when the current player successfully plays a tile.
        /// Advances turn to next player.
        /// </summary>
        public void RecordPlay(int seat, int tileIndex)
        {
            if (seat != CurrentSeat)
            {
                Debug.LogWarning($"RecordPlay called for seat {seat} but current is {CurrentSeat}");
                return;
            }

            LastPlaySeat = seat;
            ConsecutivePasses = 0;
            TotalTurns++;

            OnTilePlayedBySeat?.Invoke(seat, tileIndex);
            AdvanceTurn();
        }

        /// <summary>
        /// Call when the current player cannot play and must pass.
        /// If 3 consecutive passes occur, fires OnPaseCorridoTriggered.
        /// </summary>
        public void RecordPass(int seat)
        {
            if (seat != CurrentSeat)
            {
                Debug.LogWarning($"RecordPass called for seat {seat} but current is {CurrentSeat}");
                return;
            }

            ConsecutivePasses++;
            TotalTurns++;

            OnPassBySeat?.Invoke(seat);

            // Pase Corrido: all 3 OTHER players passed in a row
            if (ConsecutivePasses >= 3 && LastPlaySeat != -1)
            {
                OnPaseCorridoTriggered?.Invoke(LastPlaySeat);
                ConsecutivePasses = 0; // Reset counter after bonus awarded
            }

            AdvanceTurn();
        }

        void AdvanceTurn()
        {
            CurrentSeat = GameRules.NextSeat(CurrentSeat);
            OnTurnChanged?.Invoke(CurrentSeat);
        }

        /// <summary>
        /// After a round ends via Domino, the winner of that round leads the next.
        /// After a Tranque, the team that led that round leads again.
        /// </summary>
        public void SetNextRoundLead(int seat)
        {
            CurrentSeat = seat;
            RoundLeadSeat = seat;
            LastPlaySeat = -1;
            ConsecutivePasses = 0;
            TotalTurns = 0;
        }
    }
}
