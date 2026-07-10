using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kapicua.Core
{
    /// <summary>
    /// Orchestrates a single round of Dominican Dominoes.
    /// Handles: dealing, turn flow, move validation, end conditions, scoring.
    ///
    /// Lifecycle:
    ///   StartRound() → [players take turns: PlayTile() or PassTurn()] → round ends → OnRoundEnded
    /// </summary>
    public class RoundManager : MonoBehaviour
    {
        public static RoundManager Instance { get; private set; }

        [Header("References")]
        public TurnManager TurnManager;
        public ScoreManager ScoreManager;

        // Current round state
        public int RoundNumber { get; private set; }
        public List<DominoTile>[] PlayerHands { get; private set; }
        public GameBoard Board { get; private set; }
        public bool RoundActive { get; private set; }

        // Events
        public event Action<int, List<DominoTile>[]> OnRoundStarted;   // round#, hands
        public event Action<int, DominoTile, BoardEnd> OnTilePlaced;   // seat, tile, end
        public event Action<RoundResult> OnRoundEnded;
        public event Action OnMatchEnded;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Board = new GameBoard();
        }

        void OnEnable()
        {
            TurnManager.OnPaseCorridoTriggered += HandlePaseCorrido;
        }

        void OnDisable()
        {
            TurnManager.OnPaseCorridoTriggered -= HandlePaseCorrido;
        }

        /// <summary>
        /// Begins a new round. leadSeat: who plays first.
        /// On round 1, leadSeat = holder of double-6.
        /// On subsequent rounds, leadSeat = winner of previous round.
        /// </summary>
        public void StartRound(int roundNumber, int seed, int leadSeat)
        {
            RoundNumber = roundNumber;
            Board.Reset();

            // Deal tiles
            var fullSet = DominoSet.CreateFullSet();
            var shuffled = DominoSet.Shuffle(fullSet, seed);
            PlayerHands = DominoSet.Deal(shuffled);

            // Round 1: player with double-6 must lead
            if (roundNumber == 1)
                leadSeat = DominoSet.FindFirstPlayer(PlayerHands);

            TurnManager.StartRound(leadSeat);
            RoundActive = true;

            OnRoundStarted?.Invoke(roundNumber, PlayerHands);
        }

        /// <summary>
        /// Attempts to play a tile from the current player's hand.
        /// Returns true if the move was valid and applied.
        /// </summary>
        public bool PlayTile(int seat, DominoTile tile, BoardEnd end)
        {
            if (!RoundActive) return false;
            if (seat != TurnManager.CurrentSeat) return false;
            if (!PlayerHands[seat].Contains(tile)) return false;

            // Validate the move
            if (Board.IsEmpty)
            {
                // First tile of round must be played at Both ends
                if (roundNumber == 1 && !tile.IsDouble) return false;
                Board.PlaceFirstTile(tile, seat);
            }
            else
            {
                var validEnds = Board.GetValidEnds(tile);
                if (validEnds.Count == 0) return false;
                if (!validEnds.Contains(end) && !(validEnds.Count == 1)) return false;
                // If only one valid end, force it
                var actualEnd = validEnds.Count == 1 ? validEnds[0] : end;
                Board.PlaceTile(tile, actualEnd, seat);
                end = actualEnd;
            }

            PlayerHands[seat].Remove(tile);
            TurnManager.RecordPlay(seat, tile.TileIndex);
            OnTilePlaced?.Invoke(seat, tile, end);

            // Check win condition: player played their last tile
            if (PlayerHands[seat].Count == 0)
            {
                EndRound(RoundEndReason.Domino, seat);
                return true;
            }

            // Check tranque after every play — the player who just placed the
            // locking tile is the tranque initiator (== TurnManager.LastPlaySeat)
            if (GameRules.IsTranque(PlayerHands, Board))
            {
                EndRound(RoundEndReason.Tranque, TurnManager.LastPlaySeat);
            }

            return true;
        }

        // Store roundNumber in field for use in PlayTile
        private int roundNumber;

        /// <summary>
        /// Current player cannot play — passes their turn.
        /// </summary>
        public void PassTurn(int seat)
        {
            if (!RoundActive) return;
            if (seat != TurnManager.CurrentSeat) return;
            if (Board.HasValidPlay(PlayerHands[seat]))
            {
                Debug.LogWarning($"Seat {seat} tried to pass but has valid plays!");
                return;
            }

            TurnManager.RecordPass(seat);

            // After a pass, check tranque again — initiator is still the last
            // player who actually placed a tile
            if (GameRules.IsTranque(PlayerHands, Board))
                EndRound(RoundEndReason.Tranque, TurnManager.LastPlaySeat);
        }

        void HandlePaseCorrido(int lastPlaySeat)
        {
            // Award bonus without ending the round
            ScoreManager.AwardPaseCorridoBonus(lastPlaySeat);

            if (ScoreManager.IsMatchOver())
            {
                RoundActive = false;
                OnMatchEnded?.Invoke();
            }
        }

        void EndRound(RoundEndReason reason, int winningSeat)
        {
            RoundActive = false;
            var result = GameRules.EvaluateRound(
                PlayerHands, Board, reason, win