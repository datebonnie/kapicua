using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Kapicua.Networking;

namespace Kapicua.UI
{
    /// <summary>
    /// Manages the Private Room tab: create a room, join by code, waiting lobby.
    ///
    /// States:
    ///   Idle → Create/Join selection
    ///   Creating → lobby created, showing invite code
    ///   Joining → code input
    ///   Waiting → lobby with 1-4 player slots, host can start
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        // ─── Panels ──────────────────────────────────────────────────────────
        [Header("Panels")]
        public GameObject IdlePanel;
        public GameObject WaitingPanel;
        public GameObject JoinCodeInputPanel;

        // ─── Idle Panel ───────────────────────────────────────────────────────
        [Header("Idle")]
        public Button CreateRoomButton;
        public Button JoinRoomButton;

        // ─── Invite Code Display ──────────────────────────────────────────────
        [Header("Invite Code")]
        public TMP_Text InviteCodeText;
        public Button CopyCodeButton;
        public TMP_Text CopyFeedbackText;

        // ─── Join Code Input ──────────────────────────────────────────────────
        [Header("Join")]
        public TMP_InputField JoinCodeInput;
        public Button SubmitJoinButton;
        public Button CancelJoinButton;
        public TMP_Text JoinErrorText;

        // ─── Waiting Lobby ────────────────────────────────────────────────────
        [Header("Waiting Lobby")]
        public TMP_Text[] PlayerSlots;        // 4 slots, e.g. "Slot 1", "Waiting..."
        public Image[] PlayerSlotBorders;     // highlight when filled
        public TMP_Text PlayerCountText;
        public Button StartGameButton;        // host only
        public Button LeaveRoomButton;
        public TMP_Text WaitingStatusText;
        public Color FilledSlotColor = new Color(0.2f, 0.8f, 0.4f);
        public Color EmptySlotColor = new Color(0.3f, 0.3f, 0.3f);

        void Start()
        {
            ShowIdle();

            CreateRoomButton?.onClick.AddListener(OnCreateRoom);
            JoinRoomButton?.onClick.AddListener(ShowJoinInput);
            SubmitJoinButton?.onClick.AddListener(OnJoinRoom);
            CancelJoinButton?.onClick.AddListener(ShowIdle);
            CopyCodeButton?.onClick.AddListener(CopyInviteCode);
            StartGameButton?.onClick.AddListener(OnStartGame);
            LeaveRoomButton?.onClick.AddListener(OnLeaveRoom);

            // Subscribe to lobby events
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnLobbyCreated += _ => RefreshLobbyUI();
                LobbyManager.Instance.OnLobbyJoined += _ => RefreshLobbyUI();
                LobbyManager.Instance.OnLobbyUpdated += _ => RefreshLobbyUI();
                LobbyManager.Instance.OnGameStarting += OnGameStarting;
                LobbyManager.Instance.OnError += ShowError;
            }
        }

        // ─── STATE TRANSITIONS ───────────────────────────────────────────────

        void ShowIdle()
        {
            if (IdlePanel != null) IdlePanel.SetActive(true);
            if (WaitingPanel != null) WaitingPanel.SetActive(false);
            if (JoinCodeInputPanel != null) JoinCodeInputPanel.SetActive(false);
            if (InviteCodeText != null) InviteCodeText.text = "";
        }

        void ShowJoinInput()
        {
            if (IdlePanel != null) IdlePanel.SetActive(false);
            if (JoinCodeInputPanel != null) JoinCodeInputPanel.SetActive(true);
            if (JoinErrorText != null) JoinErrorText.text = "";
        }

        void ShowWaiting()
        {
            if (IdlePanel != null) IdlePanel.SetActive(false);
            if (JoinCodeInputPanel != null) JoinCodeInputPanel.SetActive(false);
            if (WaitingPanel != null) WaitingPanel.SetActive(true);
        }

        // ─── ACTIONS ─────────────────────────────────────────────────────────

        async void OnCreateRoom()
        {
            CreateRoomButton.interactable = false;
            string displayName = PlayerPrefs.GetString("DisplayName", "Host");

            await LobbyManager.Instance.InitializeAsync();
            string code = await LobbyManager.Instance.CreateLobbyAsync(displayName);

            if (!string.IsNullOrEmpty(code))
            {
                if (InviteCodeText != null) InviteCodeText.text = code;
                ShowWaiting();
            }
            else
            {
                CreateRoomButton.interactable = true;
            }
        }

        async void OnJoinRoom()
        {
            string code = JoinCodeInput?.text?.Trim().ToUpper() ?? "";
            if (code.Length < 6)
            {
                if (JoinErrorText != null) JoinErrorText.text = "Enter the 6-character code.";
                return;
            }

            SubmitJoinButton.interactable = false;
            string displayName = PlayerPrefs.GetString("DisplayName", "Player");

            await LobbyManager.Instance.InitializeAsync();
            await LobbyManager.Instance.JoinByCodeAsync(code, displayName);

            if (LobbyManager.Instance.CurrentLobby != null)
                ShowWaiting();
            else
                SubmitJoinButton.interactable = true;
        }

        async void OnStartGame()
        {
            if (!LobbyManager.Instance.IsHost) return;
            if (LobbyManager.Instance.GetPlayerCount() < 4)
            {
                if (WaitingStatusText != null) WaitingStatusText.text = "Need 4 players to start!";
                return;
            }

            StartGameButton.interactable = false;
            if (WaitingStatusText != null) WaitingStatusText.text = "Starting...";

            // Allocate relay → broadcast code → all clients join
            string relayCode = await RelayManager.Instance.AllocateRelayAsync();
            if (!string.IsNullOrEmpty(relayCode))
                await LobbyManager.Instance.BroadcastRelayCodeAsync(relayCode);
        }

        async void OnLeaveRoom()
        {
            await LobbyManager.Instance.LeaveLobbyAsync();
            ShowIdle();
        }

        void OnGameStarting()
        {
            SceneManager.LoadScene("03_Game");
        }

        // ─── UI REFRESH ───────────────────────────────────────────────────────

        void RefreshLobbyUI()
        {
            var names = LobbyManager.Instance.GetPlayerDisplayNames();
            int count = names.Count;

            for (int i = 0; i < 4; i++)
            {
                if (PlayerSlots != null && i < PlayerSlots.Length)
                {
                    PlayerSlots[i].text = i < count ? names[i] : "Waiting...";
                }
                if (PlayerSlotBorders != null && i < PlayerSlotBorders.Length)
                {
                    PlayerSlotBorders[i].color = i < count ? FilledSlotColor : EmptySlotColor;
                }
            }

            if (PlayerCountText != null)
                PlayerCountText.text = $"{count}/4 players";

            // Show invite code if we're host
            if (LobbyManager.Instance.IsHost && InviteCodeText != null)
                InviteCodeText.text = LobbyManager.Instance.LobbyCode;

            // Only host sees Start button; enable only when full
            if (StartGameButton != null)
            {
                StartGameButton.gameObject.SetActive(LobbyManager.Instance.IsHost);
                StartGameButton.interactable = count == 4;
            }

            if (WaitingStatusText != null)
            {
                WaitingStatusText.text = count == 4
                    ? (LobbyManager.Instance.IsHost ? "All players ready! Hit Start." : "Waiting for host to start...")
                    : $"Waiting for {4 - count} more player{(4 - count == 1 ? "" : "s")}...";
            }
        }

        void CopyInviteCode()
        {
            string code = LobbyManager.Instance.LobbyCode;
            GUIUtility.systemCopyBuffer = code;
            StartCoroutine(ShowCopyFeedback());
        }

        IEnumerator ShowCopyFeedback()
        {
            if (CopyFeedbackText != null)
            {
                CopyFeedbackText.text = "Copied!";
                CopyFeedbackText.gameObject.SetActive(true);
                yield return new WaitForSeconds(1.5f);
                CopyFeedbackText.gameObject.SetActive(false);
            }
        }

        void ShowError(string error)
        {
            if (JoinErrorText != null) JoinErrorText.text = error;
            if (SubmitJoinButton != null) SubmitJoinButton.interactable = true;
            if (CreateRoomButton != null) CreateRoomButton.interactable = true;
        }
    }
}
