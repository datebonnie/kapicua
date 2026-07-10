using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Kapicua.Core;

namespace Kapicua.Networking
{
    /// <summary>
    /// Synchronizes game state across all 4 players using Unity Netcode for GameObjects.
    ///
    /// Architecture:
    /// - Host runs the authoritative game logic (MatchManager, RoundManager, etc.)
    /// - Host broadcasts state changes to all clients via ClientRpcs
    /// - Clients send their actions (play tile, pass) to host via ServerRpcs
    /// - All game logic validation happens on host only
    ///
    /// Seat assignment: Players get seats 0-3 in join order.
    ///   Seat 0 = host, Seat 1-3 = clients (join order)
    /// </summary>
    public class NetworkGameManager : NetworkBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        /// <summary>When true, the next game starts solo: host + 3 AI seats.</summary>
        public static bool SoloWithAI = false;

        [Header("References")]
        public MatchManager MatchManager;
        public LobbyManager LobbyManager;
        public RelayManager RelayManager;
        public Kapicua.AI.AIController AIController;

        // Networked state
        private NetworkVariable<int> _currentTurn = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<int> _teamAScore = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<int> _teamBScore = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Local seat for this client
        private int _localSeat = -1;
        private string[] _playerNames = new string[4];

        // Events
        public event Action<int[]> OnHandDealt;                // local hand tile indices
        public event Action<int, int, int> OnTilePlayed;       // seat, tileIndex, end
        public event Action<int> OnPlayerPassed;               // seat
        public event Action<int, int> OnScoreUpdated;          // teamA, teamB
        public event Action<RoundResultData> OnRoundEnded;
        public event Action<int> OnMatchEnded;                 // winning team
        public event Action<string, string> OnChatMessage;     // playerName, message
        public event Action<string, int> OnEmoji;              // playerName, emojiIndex

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            // Solo practice: no lobby/relay — start hosting locally right away.
            if (SoloWithAI && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[Net] Solo practice: starting local host.");
                NetworkManager.Singleton.StartHost();
            }
        }

        public override void OnNetworkSpawn()
        {
            _currentTurn.OnValueChanged += (_, newTurn) => { /* UI reacts to turn change */ };
            _teamAScore.OnValueChanged += (_, _) => OnScoreUpdated?.Invoke(_teamAScore.Value, _teamBScore.Value);
            _teamBScore.OnValueChanged += (_, _) => OnScoreUpdated?.Invoke(_teamAScore.Value, _teamBScore.Value);

            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                // Host relays round lifecycle to everyone (round end popups,
                // fresh hands on auto-started rounds).
                MatchManager.RoundManager.OnRoundEnded += HostHandleRoundEnded;
                MatchManager.RoundManager.OnRoundStarted += HostHandleRoundStarted;
                MatchManager.OnMatchEnded += t => BroadcastMatchEndClientRpc(t);

                // Host already connected before this spawn ran — handle solo now.
                if (SoloWithAI) StartCoroutine(StartSoloSequence());
            }
        }

        // ─── CONNECTION FLOW ─────────────────────────────────────────────────

        void OnClientConnected(ulong clientId)
        {
            int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
            Debug.Log($"[Net] Client {clientId} connected. Total: {playerCount}");

            if (!SoloWithAI && playerCount == 4)
            {
                StartCoroutine(StartGameSequence());
            }
        }

        IEnumerator StartSoloSequence()
        {
            yield return new WaitForSeconds(0.6f);

            _playerNames = new[] { "Tú", "Rubirosa (IA)", "La Doña (IA)", "Don Pablo (IA)" };
            MatchManager.PlayerNames = _playerNames;

            AssignSeatClientRpc(0);   // only the host is connected
            yield return new WaitForSeconds(0.2f);

            int seed = UnityEngine.Random.Range(0, int.MaxValue);
            DealAndStartRoundClientRpc(seed, 0);

            AIController?.EnableSeats(this, 1, 2, 3);
            Debug.Log("[Net] Solo match started: seats 1-3 are AI.");
        }

        // ─── HOST ROUND LIFECYCLE ────────────────────────────────────────────

        void HostHandleRoundEnded(RoundResult result)
        {
            BroadcastRoundEndClientRpc(result.WinningTeam, result.Points, result.Capicua);
        }

        void HostHandleRoundStarted(int roundNumber, List<DominoTile>[] hands)
        {
            // MatchManager auto-starts follow-up rounds locally on the host;
            // refresh the host's hand UI here (round 1's RPC path skips the
            // host via the IsServer guard in DealAndStartRoundClientRpc).
            RaiseLocalHandDealt();
        }

        void RaiseLocalHandDealt()
        {
            var hand = MatchManager.GetMyHand();
            var indices = new int[hand.Count];
            for (int i = 0; i < hand.Count; i++) indices[i] = hand[i].TileIndex;
            OnHandDealt?.Invoke(indices);
        }

        void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[Net] Client {clientId} disconnected.");
            // TODO: handle mid-game disconnect (pause, offer to quit)
        }

        IEnumerator StartGameSequence()
        {
            yield return new WaitForSeconds(1f); // brief countdown

            // Assign seats 0-3 in connection order
            var clients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
            for (int i = 0; i < clients.Count && i < 4; i++)
            {
                AssignSeatClientRpc(i, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { clients[i] }
                    }
                });
            }

            yield return new WaitForSeconds(0.5f);

            int seed = UnityEngine.Random.Range(0, int.MaxValue);
            DealAndStartRoundClientRpc(seed, 0);
        }

        // ─── SERVER RPCS (client → host) ─────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void PlayTileServerRpc(int tileIndex, int end, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            var clientId = rpcParams.Receive.SenderClientId;
            int seat = GetSeatForClient(clientId);

            // Find the tile in the game state and apply it
            var tile = FindTileByIndex(tileIndex, seat);
            if (!tile.HasValue) return;

            var boardEnd = (BoardEnd)end;
            bool success = MatchManager.RoundManager.PlayTile(seat, tile.Value, boardEnd);

            if (success)
            {
                // Sync to all clients
                BroadcastTilePlayedClientRpc(seat, tileIndex, end);
                _currentTurn.Value = MatchManager.TurnManager.CurrentSeat;
                _teamAScore.Value = MatchManager.ScoreManager.TeamAScore;
                _teamBScore.Value = MatchManager.ScoreManager.TeamBScore;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void PassTurnServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            var clientId = rpcParams.Receive.SenderClientId;
            int seat = GetSeatForClient(clientId);
            MatchManager.RoundManager.PassTurn(seat);
            BroadcastPassClientRpc(seat);
            _currentTurn.Value = MatchManager.TurnManager.CurrentSeat;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SendChatServerRpc(string message, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            int seat = GetSeatForClient(clientId);
            string name = _playerNames[seat];
            BroadcastChatClientRpc(name, message);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SendEmojiServerRpc(int emojiIndex, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            int seat = GetSeatForClient(clientId);
            string name = _playerNames[seat];
            BroadcastEmojiClientRpc(name, emojiIndex);
        }

        // ─── CLIENT RPCS (host → all clients) ────────────────────────────────

        [ClientRpc]
        void AssignSeatClientRpc(int seat, ClientRpcParams clientRpcParams = default)
        {
            _localSeat = seat;
            MatchManager.LocalSeat = seat;
            Debug.Log($"[Net] Assigned seat {seat}");
        }

        [ClientRpc]
        void DealAndStartRoundClientRpc(int seed, int leadSeat)
        {
            int roundNumber = MatchManager.CurrentRound + 1;
            MatchManager.RoundManager.StartRound(roundNumber, seed, leadSeat);

            // Clients update their hand UI here; the host's OnRoundStarted
            // handler already did (avoids a double refresh).
            if (!IsServer) RaiseLocalHandDealt();
        }

        [ClientRpc]
        void BroadcastTilePlayedClientRpc(int seat, int tileIndex, int end)
        {
            if (!IsServer) // host already processed this
            {
                var tile = FindTileByIndex(tileIndex, seat);
                if (tile.HasValue)
                    MatchManager.RoundManager.PlayTile(seat, tile.Value, (BoardEnd)end);
            }
            OnTilePlayed?.Invoke(seat, tileIndex, end);
        }

        [ClientRpc]
        void BroadcastPassClientRpc(int seat)
        {
            if (!IsServer) MatchManager.RoundManager.PassTurn(seat);
            OnPlayerPassed?.Invoke(seat);
        }

        [ClientRpc]
        void BroadcastRoundEndClientRpc(int winningTeam, int points, bool isKapicua)
        {
            OnRoundEnded?.Invoke(new RoundResultData
            {
                WinningTeam = winningTeam,
                Points = points,
                IsKapicua = isKapicua
            });
        }

        [ClientRpc]
        void BroadcastMatchEndClientRpc(int winningTeam)
        {
            OnMatchEnded?.Invoke(winningTeam);
        }

        [ClientRpc]
        void BroadcastChatClientRpc(string playerName, string message)
        {
            OnChatMessage?.Invoke(playerName, message);
        }

        [ClientRpc]
        void BroadcastEmojiClientRpc(string playerName, int emojiIndex)
        {
            OnEmoji?.Invoke(playerName, emojiIndex);
        }

        // ─── HELPERS ─────────────────────────────────────────────────────────

        int GetSeatForClient(ulong clientId)
        {
            var clients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
            return clients.IndexOf(clientId);
        }

        DominoTile? FindTileByIndex(int index, int seat)
        {
            if (MatchManager.RoundManager.PlayerHands == null) return null;
            foreach (var tile in MatchManager.RoundManager.PlayerHands[seat])
            {
                if (tile.TileIndex == index) return tile;
            }
            return null;
        }

        // ─── PUBLIC API FOR UI ───────────────────────────────────────────────

        public void LocalPlayTile(int tileIndex, int end)
        {
            if (IsServer)
                PlayTileServerRpc(tileIndex, end);
            else
                PlayTileServerRpc(tileIndex, end);
        }

        public void LocalPass()
        {
            PassTurnServerRpc();
        }

        public void LocalSendChat(string message) => SendChatServerRpc(message);
        public void LocalSendEmoji(int emojiIndex) => SendEmojiServerRpc(emojiIndex);
        public int GetLocalSeat() => _localSeat;

        // ─── AI ACTIONS (host-side only; mirrors the ServerRpc bodies) ───────

        public void AIPlayTile(int seat, int tileIndex, int end)
        {
            if (!IsServer) return;
            var tile = FindTileByIndex(tileIndex, seat);
            if (!tile.HasValue) return;

            bool success = MatchManager.RoundManager.PlayTile(seat, tile.Value, (BoardEnd)end);
            if (success)
            {
                BroadcastTilePlayedClientRpc(seat, tileIndex, end);
                _currentTurn.Value = MatchManager.TurnManager.CurrentSeat;
                _teamAScore.Value = MatchManager.ScoreManager.TeamAScore;
                _teamBScore.Value = MatchManager.ScoreManager.TeamBScore;
            }
            else
            {
                // Defensive: an illegal AI choice must never stall the game.
                Debug.LogWarning($"[AI] Seat {seat} attempted illegal move; passing.");
                AIPass(seat);
            }
        }

        public void AIPass(int seat)
        {
            if (!IsServer) return;
            MatchManager.RoundManager.PassTurn(seat);
            BroadcastPassClientRpc(seat);
            _currentTurn.Value = MatchManager.TurnManager.CurrentSeat;
        }
    }

    [Serializable]
    public struct RoundResultData
    {
        public int WinningTeam;
        public int Points;
        public bool IsKapicua;
    }
}
