using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Kapicua.Networking
{
    /// <summary>
    /// Allocates and connects to Unity Relay for peer-to-peer gameplay
    /// without requiring players to open ports.
    ///
    /// Host: AllocateRelayAsync() → get join code → share via Lobby → StartHost()
    /// Client: JoinRelayAsync(joinCode) → StartClient()
    /// </summary>
    public class RelayManager : MonoBehaviour
    {
        public static RelayManager Instance { get; private set; }

        public string RelayJoinCode { get; private set; }
        public bool IsConnected { get; private set; }

        public event Action OnRelayAllocated;
        public event Action OnRelayJoined;
        public event Action<string> OnError;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ─── HOST ────────────────────────────────────────────────────────────

        /// <summary>
        /// Host allocates a Relay server for up to 3 connections (4 players total).
        /// Returns the join code to share with guests via Lobby.
        /// </summary>
        public async Task<string> AllocateRelayAsync()
        {
            try
            {
                var allocation = await RelayService.Instance.CreateAllocationAsync(3); // 3 guests
                RelayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetHostRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData);

                NetworkManager.Singleton.StartHost();
                IsConnected = true;

                Debug.Log($"[Relay] Allocated. Join code: {RelayJoinCode}");
                OnRelayAllocated?.Invoke();
                return RelayJoinCode;
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Relay allocation failed: {e.Message}");
                return null;
            }
        }

        // ─── CLIENT ──────────────────────────────────────────────────────────

        /// <summary>
        /// Guests join the Relay using the code broadcast by the host via Lobby.
        /// </summary>
        public async Task JoinRelayAsync(string joinCode)
        {
            try
            {
                var joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetClientRelayData(
                    joinAlloc.RelayServer.IpV4,
                    (ushort)joinAlloc.RelayServer.Port,
                    joinAlloc.AllocationIdBytes,
                    joinAlloc.Key,
                    joinAlloc.ConnectionData,
                    joinAlloc.HostConnectionData);

                NetworkManager.Singleton.StartClient();
                IsConnected = true;

                Debug.Log($"[Relay] Joined. Code: {joinCode}");
                OnRelayJoined?.Invoke();
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Relay join failed: {e.Message}");
            }
        }

        public void Disconnect()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
            IsConnected = false;
        }
    }
}
