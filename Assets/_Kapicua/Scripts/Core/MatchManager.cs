using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kapicua.Core
{
    /// <summary>
    /// Top-level match controller. Owns round lifecycle across a full 200-point match.
    /// On host: drives all game logic. On clients: mirrors state via NetworkGameManager.
    ///
    /// Seat assignments (fixed for the match):
    ///   Seat 0 = local player (bottom)  — Team A (Nosotros)
    ///   Seat 1 = right opponent          — Team B (Ellos)
    ///   Seat 2 = partner (top)           — Team A (Nosotros)
    ///   Seat 3 = left opponent           — Team B (Ellos)
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Header("References")]
        public RoundManager RoundManager;
        public ScoreManager ScoreManager;
        public TurnManager TurnManager;

        [Header("Match State")]
        public string[] PlayerNames = new string[4];
        public string[] PlayerAvatarUrls = new string[4];
        public int LocalSeat;

        public int CurrentRound { get; private set; }
        public bool MatchActive { get; private set; }
        public int NextLeadSeat { get; private set; }

        // Events
        public event Action<int> OnMatchStarted;   // local seat
        public event Action<int> OnMatchEnded;     // winning team

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            RoundManager.OnRoundEnded += HandleRoundEnded;
            RoundManager.OnMatchEnded += HandleMatchEnded;
            ScoreManager.OnMatchWon += HandleMatchWon;
        }

        void OnDisable()
        {
            RoundManager.OnRoundEnded -= HandleRoundEnded;
            RoundManager.OnMatchEnded -= HandleMatchEnded;
            ScoreManager.OnMatchWon -= HandleMatchWon;
        }

        /// <summary>
        /// Called by host after all 4 players are seated and ready.
        /// </summary>
        public void StartMatch(int localSeat, string[] playerNames, int seed)
        {
            LocalSeat = localSeat;
            PlayerNames = playerNames;
            CurrentRound = 0;
            MatchActive = true;

            ScoreManager.ResetMatch();
            OnMatchStarted?.Invoke(localSeat);

            StartNextRound(seed, 0); // round 1, lead seat TBD by double-6
        }

        void StartNextRound(int seed, int leadSeat)
        {
            CurrentRound++;
            RoundManager.StartRound(CurrentRound, seed, leadSeat);
        }

        void HandleRoundEnded(RoundResult result)
        {
            if (ScoreManager.IsMatchOver()) return;

            // Winner of the round leads the next
            NextLeadSeat = result.WinningSeat >= 0
                ? result.WinningSeat
                : TurnManager.RoundLeadSeat; // tranque: same team leads

            // Generate new seed for next round (host broadcasts this)
            int newSeed = UnityEngine.Random.Range(0, int.MaxValue);
            StartNextRound(newSeed, NextLeadSeat);
        }

        void HandleMatchEnded()
        {
            MatchActive = false;
        }

        void HandleMatchWon(int winningTeam)
        {
            MatchActive = false;
            OnMatchEnded?.Invoke(winningTeam);
        }

        // ─── Local player actions ────────────────────────────────────────────
        public bool TryPlayTile(DominoTile tile, BoardEnd end)
        {
            if (!MatchActive) return false;
            if (TurnManager.CurrentSeat != LocalSeat) return false;
            return RoundManager.PlayTile(LocalSeat, tile, end);
        }

        public void TryPass()
        {
            if (!MatchActive) return;
            if (TurnManager.CurrentSeat != LocalSeat) return;
            RoundManager.PassTurn(LocalSeat);
        }

        public bool IsMyTurn() => MatchActive && TurnManager.CurrentSeat == LocalSeat;

        public List<DominoTile> GetMyHand() =>
            RoundManager.PlayerHands != null ? RoundManager.PlayerHands[LocalSeat] : new List<DominoTile>();

        public List<DominoTile> GetValidPlays()
        {
            var hand = GetMyHand();
            var valid = new List<DominoTile>();
            foreach (var tile in hand)
            {
                if (RoundManager.Board.GetValidEnds(tile).Count > 0)
                    valid.Add(tile);
            }
            return valid;
        }
    }
}
