using System.Collections;
using System.Collections.Generic;
using Kapicua.Core;
using Kapicua.Networking;
using UnityEngine;

namespace Kapicua.AI
{
    /// <summary>
    /// Drives AI-controlled seats on the host. Listens for turn changes and,
    /// when an AI seat is up, "thinks" briefly and plays through
    /// NetworkGameManager — the same authoritative path human ServerRpcs use,
    /// so every client (and the host UI) sees AI moves identically.
    ///
    /// Heuristics (mirrors DominoAI): shed doubles early, dump the heaviest
    /// tile, keep ends on suits the hand is long in.
    /// </summary>
    public class AIController : MonoBehaviour
    {
        public MatchManager Match;
        public float MinThinkSeconds = 0.9f;
        public float MaxThinkSeconds = 2.0f;

        NetworkGameManager _net;
        readonly HashSet<int> _aiSeats = new HashSet<int>();
        Coroutine _thinking;
        bool _subscribed;

        /// <summary>Host calls this once the match starts to mark AI seats.</summary>
        public void EnableSeats(NetworkGameManager net, params int[] seats)
        {
            _net = net;
            _aiSeats.Clear();
            foreach (var s in seats) _aiSeats.Add(s);

            if (!_subscribed)
            {
                Match.TurnManager.OnTurnChanged += HandleTurnChanged;
                Match.RoundManager.OnRoundStarted += HandleRoundStarted;
                _subscribed = true;
            }

            // The lead seat never receives an OnTurnChanged for its first move.
            HandleTurnChanged(Match.TurnManager.CurrentSeat);
        }

        void OnDestroy()
        {
            if (_subscribed && Match != null)
            {
                Match.TurnManager.OnTurnChanged -= HandleTurnChanged;
                Match.RoundManager.OnRoundStarted -= HandleRoundStarted;
            }
        }

        void HandleRoundStarted(int roundNumber, List<DominoTile>[] hands)
        {
            // New round: TurnManager.StartRound sets the lead without firing
            // OnTurnChanged, so kick the AI manually if the lead is a bot.
            HandleTurnChanged(Match.TurnManager.CurrentSeat);
        }

        void HandleTurnChanged(int seat)
        {
            if (!_aiSeats.Contains(seat)) return;
            if (_thinking != null) StopCoroutine(_thinking);
            _thinking = StartCoroutine(ThinkAndPlay(seat));
        }

        IEnumerator ThinkAndPlay(int seat)
        {
            yield return new WaitForSeconds(Random.Range(MinThinkSeconds, MaxThinkSeconds));

            var rm = Match.RoundManager;
            if (_net == null || rm == null || !rm.RoundActive) yield break;
            if (Match.TurnManager.CurrentSeat != seat) yield break;

            var hand = rm.PlayerHands[seat];
            var board = rm.Board;

            // Suit counts — prefer keeping ends on suits we hold many of.
            int[] suitCount = new int[7];
            foreach (var t in hand)
            {
                suitCount[t.High]++;
                if (!t.IsDouble) suitCount[t.Low]++;
            }

            if (board.IsEmpty)
            {
                // Opening: highest double first (round 1 demands a double —
                // the round-1 lead always holds 6|6), otherwise heaviest tile.
                DominoTile opener = hand[0];
                int bestScore = int.MinValue;
                foreach (var t in hand)
                {
                    int score = (t.IsDouble ? 1000 : 0) + t.TotalPips;
                    if (score > bestScore) { bestScore = score; opener = t; }
                }
                _net.AIPlayTile(seat, opener.TileIndex, (int)BoardEnd.Left);
                yield break;
            }

            // Regular turn: pick the best playable tile.
            bool found = false;
            DominoTile bestTile = default;
            BoardEnd bestEnd = BoardEnd.Left;
            int best = int.MinValue;

            foreach (var t in hand)
            {
                var ends = board.GetValidEnds(t);
                if (ends.Count == 0) continue;
                int score = (t.IsDouble ? 100 : 0) * 1000
                          + t.TotalPips * 10
                          + suitCount[t.High] + suitCount[t.Low];
                if (score > best)
                {
                    best = score;
                    bestTile = t;
                    bestEnd = ends[Random.Range(0, ends.Count)];
                    found = true;
                }
            }

            if (found)
                _net.AIPlayTile(seat, bestTile.TileIndex, (int)bestEnd);
            else
                _net.AIPass(seat);
        }
    }
}
