using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using LobbyPlayer = Unity.Services.Lobbies.Models.Player;

namespace Kapicua.Networking
{
    /// <summary>
    /// Manages private rooms via Unity Lobby.
    /// 
    /// Flow:
    ///   Host: CreateLobby() → gets 6-char invite code → shares with friends
    ///   Guest: JoinByCode(code) → waits for host to start
    ///   Host: StartGame() → allocates Relay → broadcasts join code → all clients connect
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        [Header("Lobby Config")]
        public int MaxPlayers = 4;
        public float HeartbeatIntervalSeconds = 15f;

        public Lobby CurrentLobby { get; private set; }
        public bool IsHost { get; private set; }
        public string LobbyCode => CurrentLobby?.LobbyCode ?? "";

        // Events
        public event Action<Lobby> OnLobbyCreated;
        public event Action<Lobby> OnLobbyJoined;
        public event Action<Lobby> OnLobbyUpdated;
        public event Action<string> OnPlayerJoined;    // display name
        public event Action<string> OnPlayerLeft;
        public event Action OnGameStarting;
        public event Action<string> OnError;

        private float _heartbeatTimer;
        private float _lobbyPollTimer;
        private const float POLL_INTERVAL = 1.5f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Update()
        {
            HandleHeartbeat();
            HandleLobbyPoll();
        }

        // ─── INITIALIZATION ─────────────────────────────────────────────────

        public async Task InitializeAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return;
            try
            {
                await UnityServices.InitializeAsync();
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[Lobby] Initialized. Player ID: {AuthenticationService.Instance.PlayerId}");
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Init failed: {e.Message}");
            }
        }

        // ─── HOST ────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a private lobby. Players join using the auto-generated LobbyCode.
        /// </summary>
        public async Task<string> CreateLobbyAsync(string hostDisplayName)
        {
            try
            {
                var options = new CreateLobbyOptions
                {
                    IsPrivate = true,
                    Player = BuildPlayer(hostDisplayName),
                    Data = new Dictionary<string, DataObject>
                    {
                        { "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, "") },
                        { "GameStarted", new DataObject(DataObject.VisibilityOptions.Member, "false") }
                    }
                };

                CurrentLobby = await LobbyService.Instance.CreateLobbyAsync("Kapicua Room", MaxPlayers, options);
                IsHost = true;

                Debug.Log($"[Lobby] Created. Code: {CurrentLobby.LobbyCode}");
                OnLobbyCreated?.Invoke(CurrentLobby);
                return CurrentLobby.LobbyCode;
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Create lobby failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Host broadcasts the Relay join code once allocated, then marks game as started.
        /// </summary>
        public async Task BroadcastRelayCodeAsync(string relayCode)
        {
            if (!IsHost || CurrentLobby == null) return;
            try
            {
                await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, relayCode) },
                        { "GameStarted", new DataObject(DataObject.VisibilityOptions.Member, "true") }
                    }
                });
                OnGameStarting?.Invoke();
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Broadcast relay failed: {e.Message}");
            }
        }

        // ─── GUEST ───────────────────────────────────────────────────────────

        /// <summary>
        /// Joins an existing lobby using the invite code shown by the host.
        /// </summary>
        public async Task JoinByCodeAsync(string code, string displayName)
        {
            try
            {
                var options = new JoinLobbyByCodeOptions { Player = BuildPlayer(displayName) };
                CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code.ToUpper(), options);
                IsHost = false;

                Debug.Log($"[Lobby] Joined. ID: {CurrentLobby.Id}");
                OnLobbyJoined?.Invoke(CurrentLobby);
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Join failed: {e.Message}");
            }
        }

        // ─── COMMON ──────────────────────────────────────────────────────────

        public async Task LeaveLobbyAsync()
        {
            if (CurrentLobby == null) return;
            try
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                if (IsHost)
                    await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
                else
                    await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, playerId);

                CurrentLobby = null;
                IsHost = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] Leave failed: {e.Message}");
            }
        }

        public List<string> GetPlayerDisplayNames()
        {
            var names = new List<string>();
            if (CurrentLobby == null) return names;
            foreach (var p in CurrentLobby.Players)
            {
                if (p.Data != null && p.Data.TryGetValue("DisplayName", out var dn))
                    names.Add(dn.Value);
                else
                    names.Add("Player");
            }
            return names;
        }

        public int GetPlayerCount() => CurrentLobby?.Players?.Count ?? 0;
        public bool IsFull() => GetPlayerCount() >= MaxPlayers;

        // ─── HEARTBEAT & POLLING ─────────────────────────────────────────────

        void HandleHeartbeat()
        {
            if (!IsHost || CurrentLobby == null) return;
            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer >= HeartbeatIntervalSeconds)
            {
                _heartbeatTimer = 0;
                LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
            }
        }

        void HandleLobbyPoll()
        {
            if (CurrentLobby == null) return;
            _lobbyPollTimer += Time.deltaTime;
            if (_lobbyPollTimer >= POLL_INTERVAL)
            {
                _lobbyPollTimer = 0;
                PollLobbyAsync();
            }
        }

        async void PollLobbyAsync()
        {
            try
            {
                var updated = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
                CurrentLobby = updated;
                OnLobbyUpdated?.Invoke(updated);

                // Check if game has started (non-host clients look for RelayCode)
                if (!IsHost &&
                    updated.Data != null &&
                    updated.Data.TryGetValue("GameStarted", out var started) &&
                    started.Value == "true")
                {
                    if (updated.Data.TryGetValue("RelayCode", out var rc) && !string.IsNullOrEmpty(rc.Value))
                    {
                        OnGameStarting?.Invoke();
                        await RelayManager.Instance.JoinRelayAsync(rc.Value);
                    }
                }
            }
            catch { /* polling can fail silently */ }
        }

        static LobbyPlayer BuildPlayer(string displayName) => new LobbyPlayer
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "DisplayName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, displayName) }
            }
        };
    }
}
