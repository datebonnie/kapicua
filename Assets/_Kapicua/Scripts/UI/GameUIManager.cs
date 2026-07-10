using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Kapicua.Core;
using Kapicua.Networking;

namespace Kapicua.UI
{
    /// <summary>
    /// In-game UI manager. Mirrors the "in game" mockup:
    ///
    ///   TOP:    Partida #, Ronda X/X, score banner (NOSOTROS 120 vs ELLOS 85)
    ///   BOARD:  Domino chain (center table)
    ///   SEATS:  4 player zones (top=partner, left/right=opponents, bottom=you)
    ///   HAND:   Your 7 tiles (bottom, interactive)
    ///   BOTTOM: Chat, Emoji, TU TURNO indicator, ¡DALE! voice button
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        // ─── Score / Header ───────────────────────────────────────────────────
        [Header("Header")]
        public TMP_Text PartidaText;
        public TMP_Text RondaText;
        public TMP_Text NosotrosScoreText;
        public TMP_Text EllosScoreText;

        // ─── Player Zones ─────────────────────────────────────────────────────
        [Header("Player Zones")]
        public PlayerZoneUI[] PlayerZones;  // [0]=bottom(you), [1]=right, [2]=top, [3]=left

        // ─── Your Hand ────────────────────────────────────────────────────────
        [Header("Hand")]
        public Transform HandContainer;
        public GameObject TilePrefab;
        public Color HighlightColor = new Color(1f, 0.87f, 0.2f);
        public Color DefaultTileColor = Color.white;
        private List<TileButton> _handTileButtons = new List<TileButton>();
        private DominoTile? _selectedTile;

        // ─── Board ────────────────────────────────────────────────────────────
        [Header("Board")]
        public BoardRenderer BoardRenderer;  // handles visual tile chain

        // ─── Turn Indicator ───────────────────────────────────────────────────
        [Header("Turn")]
        public GameObject TuTurnoPanel;
        public TMP_Text TurnStatusText;
        public Button PassButton;

        // ─── Chat & Emoji ─────────────────────────────────────────────────────
        [Header("Chat")]
        public Button ChatButton;
        public Button EmojiButton;
        public Button DaleButton;           // ¡DALE! voice reaction
        public GameObject ChatPanel;
        public TMP_InputField ChatInput;
        public Button ChatSendButton;
        public Transform ChatMessageContainer;
        public GameObject ChatMessagePrefab;

        // ─── Emoji Picker ─────────────────────────────────────────────────────
        [Header("Emoji")]
        public GameObject EmojiPanel;
        public Button[] EmojiButtons;

        // ─── Round End ────────────────────────────────────────────────────────
        [Header("Round End")]
        public GameObject RoundEndPanel;
        public TMP_Text RoundResultText;
        public TMP_Text KapicuaBanner;

        void Start()
        {
            // Wire chat
            ChatButton?.onClick.AddListener(ToggleChat);
            ChatSendButton?.onClick.AddListener(SendChat);
            EmojiButton?.onClick.AddListener(ToggleEmoji);
            DaleButton?.onClick.AddListener(SendDale);
            PassButton?.onClick.AddListener(OnPassPressed);

            // Wire emoji buttons
            for (int i = 0; i < EmojiButtons?.Length; i++)
            {
                int idx = i;
                EmojiButtons[i]?.onClick.AddListener(() => SendEmoji(idx));
            }

            // Hide panels
            if (ChatPanel != null) ChatPanel.SetActive(false);
            if (EmojiPanel != null) EmojiPanel.SetActive(false);
            if (RoundEndPanel != null) RoundEndPanel.SetActive(false);
            if (TuTurnoPanel != null) TuTurnoPanel.SetActive(false);

            // Subscribe to game events
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnHandDealt += OnHandDealt;
                NetworkGameManager.Instance.OnTilePlayed += OnTilePlayed;
                NetworkGameManager.Instance.OnPlayerPassed += OnPlayerPassed;
                NetworkGameManager.Instance.OnScoreUpdated += OnScoreUpdated;
                NetworkGameManager.Instance.OnRoundEnded += OnRoundEnded;
                NetworkGameManager.Instance.OnChatMessage += OnChatReceived;
                NetworkGameManager.Instance.OnEmoji += OnEmojiReceived;
            }

            if (TurnManager.Instance != null)
                TurnManager.Instance.OnTurnChanged += OnTurnChanged;

            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnScoreChanged += (a, b) => OnScoreUpdated(a, b);

            // Board end-choice (tile fits both ends → player picks one)
            if (BoardRenderer != null)
                BoardRenderer.OnEndChosen += PlaySelectedTile;
        }

        // ─── HAND MANAGEMENT ─────────────────────────────────────────────────

        void OnHandDealt(int[] tileIndices)
        {
            ClearHand();
            BoardRenderer?.Clear();   // new round → empty chain
            var fullSet = DominoSet.CreateFullSet();

            foreach (int idx in tileIndices)
            {
                var tile = fullSet[idx];
                var go = Instantiate(TilePrefab, HandContainer);
                var btn = go.GetComponent<TileButton>();
                if (btn != null)
                {
                    btn.SetTile(tile);
                    btn.OnTileClicked += OnTileSelected;
                    _handTileButtons.Add(btn);
                }
            }
        }

        void ClearHand()
        {
            foreach (var btn in _handTileButtons)
                if (btn != null) Destroy(btn.gameObject);
            _handTileButtons.Clear();
            _selectedTile = null;
        }

        void OnTileSelected(DominoTile tile)
        {
            if (!MatchManager.Instance.IsMyTurn()) return;

            _selectedTile = tile;

            // Highlight selected tile, dim others
            foreach (var btn in _handTileButtons)
                btn.SetHighlight(btn.Tile == tile);

            // If board is empty or only one valid end, play immediately
            var validEnds = MatchManager.Instance.RoundManager.Board.GetValidEnds(tile);
            if (validEnds.Count == 1)
            {
                PlaySelectedTile(validEnds[0]);
            }
            else if (validEnds.Count > 1)
            {
                // Show end-selection UI (tap left/right end of board)
                BoardRenderer?.HighlightValidEnds(tile);
            }
        }

        public void PlaySelectedTile(BoardEnd end)
        {
            if (!_selectedTile.HasValue) return;
            NetworkGameManager.Instance.LocalPlayTile(_selectedTile.Value.TileIndex, (int)end);
            _selectedTile = null;
        }

        void OnTilePlayed(int seat, int tileIndex, int end)
        {
            // Remove tile from hand UI if it's ours
            if (seat == NetworkGameManager.Instance.GetLocalSeat())
            {
                _handTileButtons.RemoveAll(btn =>
                {
                    if (btn.Tile.TileIndex == tileIndex)
                    {
                        Destroy(btn.gameObject);
                        return true;
                    }
                    return false;
                });
            }

            // Update opponent tile counts
            if (seat < PlayerZones?.Length)
                PlayerZones[seat]?.DecrementTileCount();

            // Update board visual
            BoardRenderer?.AddTile(tileIndex, (BoardEnd)end, seat);
        }

        void OnPlayerPassed(int seat)
        {
            if (seat < PlayerZones?.Length)
                PlayerZones[seat]?.ShowPassIndicator();
        }

        // ─── TURN ─────────────────────────────────────────────────────────────

        void OnTurnChanged(int seat)
        {
            bool myTurn = seat == NetworkGameManager.Instance.GetLocalSeat();
            if (TuTurnoPanel != null) TuTurnoPanel.SetActive(myTurn);

            // Highlight current player zone
            for (int i = 0; i < PlayerZones?.Length; i++)
                PlayerZones[i]?.SetActivePlayer(i == seat);

            // Show/hide pass button
            if (PassButton != null)
            {
                bool canPass = myTurn &&
                               !MatchManager.Instance.RoundManager.Board.HasValidPlay(
                                   MatchManager.Instance.GetMyHand());
                PassButton.gameObject.SetActive(canPass);
            }

            // Highlight valid tiles
            if (myTurn)
            {
                var valid = MatchManager.Instance.GetValidPlays();
                foreach (var btn in _handTileButtons)
                    btn.SetPlayable(valid.Contains(btn.Tile));
            }
        }

        void OnPassPressed()
        {
            NetworkGameManager.Instance.LocalPass();
        }

        // ─── SCORE ────────────────────────────────────────────────────────────

        void OnScoreUpdated(int nosotros, int ellos)
        {
            if (NosotrosScoreText != null) NosotrosScoreText.text = nosotros.ToString();
            if (EllosScoreText != null) EllosScoreText.text = ellos.ToString();
        }

        // ─── ROUND END ────────────────────────────────────────────────────────

        void OnRoundEnded(RoundResultData data)
        {
            if (RoundEndPanel == null) return;
            RoundEndPanel.SetActive(true);

            string winner = data.WinningTeam == 0 ? "NOSOTROS" : "ELLOS";
            if (RoundResultText != null)
                RoundResultText.text = $"{winner} +{data.Points}";
            if (KapicuaBanner != null)
            {
                KapicuaBanner.gameObject.SetActive(data.IsKapicua);
                KapicuaBanner.text = "¡KAPICUA!";
            }

            StartCoroutine(HideRoundEndAfterDelay(3f));
        }

        IEnumerator HideRoundEndAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (RoundEndPanel != null) RoundEndPanel.SetActive(false);
        }

        // ─── CHAT ─────────────────────────────────────────────────────────────

        void ToggleChat()
        {
            if (ChatPanel != null) ChatPanel.SetActive(!ChatPanel.activeSelf);
            if (EmojiPanel != null) EmojiPanel.SetActive(false);
        }

        void ToggleEmoji()
        {
            if (EmojiPanel != null) EmojiPanel.SetActive(!EmojiPanel.activeSelf);
            if (ChatPanel != null) ChatPanel.SetActive(false);
        }

        void SendChat()
        {
            string msg = ChatInput?.text?.Trim() ?? "";
            if (string.IsNullOrEmpty(msg)) return;
            NetworkGameManager.Instance.LocalSendChat(msg);
            if (ChatInput != null) ChatInput.text = "";
        }

        void SendEmoji(int emojiIndex)
        {
            NetworkGameManager.Instance.LocalSendEmoji(emojiIndex);
            if (EmojiPanel != null) EmojiPanel.SetActive(false);
        }

        void SendDale()
        {
            // ¡DALE! = special emoji/reaction (index 99 = DALE)
            NetworkGameManager.Instance.LocalSendEmoji(99);
        }

        void OnChatReceived(string playerName, string message)
        {
            if (ChatMessageContainer == null || ChatMessagePrefab == null) return;
            var go = Instantiate(ChatMessagePrefab, ChatMessageContainer);
            var text = go.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = $"<b>{playerName}:</b> {message}";
        }

        void OnEmojiReceived(string playerName, int emojiIndex)
        {
            // Show emoji floating above the player zone
            // TODO: spawn floating emoji prefab above the sender's player zone
        }
    }

    // ─── HELPER COMPONENTS ───────────────────────────────────────────────────

    /// <summary>UI component for a single tile button in your hand.</summary>
    public class TileButton : MonoBehaviour
    {
        public DominoTile Tile;
        public event Action<DominoTile> OnTileClicked;
        private Button _btn;
        private Image _bg;

        static readonly Color PipColor = new Color(0.10f, 0.10f, 0.13f);

        // (col, row) grid offsets per pip value — same patterns as the 3-D TileView.
        static readonly Vector2[][] PipPatterns =
        {
            new Vector2[0],
            new[] { new Vector2(0, 0) },
            new[] { new Vector2(-1, -1), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(0, 0), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(0, 0), new Vector2(1, -1), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 0), new Vector2(1, 1) },
        };

        // Procedural anti-aliased circle sprite shared by every pip.
        static Sprite _pipSprite;
        static Sprite PipSprite
        {
            get
            {
                if (_pipSprite == null)
                {
                    const int S = 32;
                    var tex = new Texture2D(S, S, TextureFormat.ARGB32, false);
                    float c = (S - 1) / 2f, r = S / 2f - 1f;
                    var px = new Color[S * S];
                    for (int y = 0; y < S; y++)
                        for (int x = 0; x < S; x++)
                        {
                            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                            px[y * S + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d));
                        }
                    tex.SetPixels(px);
                    tex.Apply();
                    _pipSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
                }
                return _pipSprite;
            }
        }

        void Awake()
        {
            _btn = GetComponent<Button>();
            _bg = GetComponent<Image>();
            _btn?.onClick.AddListener(() => OnTileClicked?.Invoke(Tile));
        }

        public void SetTile(DominoTile t