using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Kapicua.Core;

namespace Kapicua.UI
{
    /// <summary>
    /// The Score tab — a scorecard that tracks a live Dominican domino match.
    /// Works both in-app (synced with game) and standalone (manual entry for physical games).
    ///
    /// Layout:
    ///   Header: NOSOTROS [score] vs ELLOS [score]
    ///   Progress bar toward 200
    ///   Round history list
    ///   Manual score entry (for physical games)
    ///   Reset button
    /// </summary>
    public class ScoreTabManager : MonoBehaviour
    {
        [Header("Score Display")]
        public TMP_Text NosotrosScoreText;
        public TMP_Text EllosScoreText;
        public Slider NosotrosProgressBar;
        public Slider EllosProgressBar;
        public TMP_Text NosotrosProgressLabel;
        public TMP_Text EllosProgressLabel;

        [Header("Round History")]
        public Transform RoundListContainer;
        public GameObject RoundRowPrefab;    // Row: Round#, Winner, Points, +Kapicua flag
        public ScrollRect HistoryScroll;

        [Header("Manual Entry")]
        public Button AddNosotrosButton;
        public Button AddEllosButton;
        public TMP_InputField ManualPointsInput;
        public Button ResetButton;

        [Header("Win Banner")]
        public GameObject WinBanner;
        public TMP_Text WinnerText;

        private int _nosotrosScore;
        private int _ellosScore;
        private List<RoundRecord> _history = new List<RoundRecord>();
        private bool _manualMode = false;

        void Start()
        {
            AddNosotrosButton?.onClick.AddListener(() => AddManualPoints(0));
            AddEllosButton?.onClick.AddListener(() => AddManualPoints(1));
            ResetButton?.onClick.AddListener(ResetScorecard);
            if (WinBanner != null) WinBanner.SetActive(false);

            // Hook into live game if ScoreManager exists
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += UpdateScoreDisplay;
                ScoreManager.Instance.OnRoundRecorded += OnRoundRecorded;
                ScoreManager.Instance.OnMatchWon += ShowWinBanner;
                _manualMode = false;
            }
            else
            {
                _manualMode = true; // standalone scorecard mode
            }

            RefreshUI();
        }

        // ─── LIVE GAME CALLBACKS ─────────────────────────────────────────────

        void UpdateScoreDisplay(int nosotros, int ellos)
        {
            _nosotrosScore = nosotros;
            _ellosScore = ellos;
            RefreshUI();
        }

        void OnRoundRecorded(RoundRecord record)
        {
            _history.Add(record);
            AddRoundRow(record);
            ScrollToBottom();
        }

        void ShowWinBanner(int winningTeam)
        {
            if (WinBanner == null) return;
            WinBanner.SetActive(true);
            string winner = winningTeam == 0 ? "NOSOTROS" : "ELLOS";
            if (WinnerText != null) WinnerText.text = $"¡{winner} GANA!";
        }

        // ─── MANUAL MODE ─────────────────────────────────────────────────────

        void AddManualPoints(int team)
        {
            if (!_manualMode) return;
            if (!int.TryParse(ManualPointsInput?.text, out int pts) || pts <= 0) return;

            var record = new RoundRecord
            {
                RoundNumber = _history.Count + 1,
                WinningTeam = team,
                PointsScored = pts,
                TeamAAfter = team == 0 ? _nosotrosScore + pts : _nosotrosScore,
                TeamBAfter = team == 1 ? _ellosScore + pts : _ellosScore
            };

            if (team == 0) _nosotrosScore += pts;
            else _ellosScore += pts;

            _history.Add(record);
            AddRoundRow(record);
            RefreshUI();
            ScrollToBottom();

            if (ManualPointsInput != null) ManualPointsInput.text = "";

            if (_nosotrosScore >= GameRules.SCORE_TO_WIN) ShowWinBanner(0);
            else if (_ellosScore >= GameRules.SCORE_TO_WIN) ShowWinBanner(1);
        }

        void ResetScorecard()
        {
            _nosotrosScore = 0;
            _ellosScore = 0;
            _history.Clear();
            if (WinBanner != null) WinBanner.SetActive(false);

            // Clear round rows
            if (RoundListContainer != null)
            {
                foreach (Transform child in RoundListContainer)
                    Destroy(child.gameObject);
            }
            RefreshUI();
        }

        // ─── UI REFRESH ───────────────────────────────────────────────────────

        void RefreshUI()
        {
            if (NosotrosScoreText != null) NosotrosScoreText.text = _nosotrosScore.ToString();
            if (EllosScoreText != null) EllosScoreText.text = _ellosScore.ToString();

            float target = GameRules.SCORE_TO_WIN;
            if (NosotrosProgressBar != null) NosotrosProgressBar.value = _nosotrosScore / target;
            if (EllosProgressBar != null) EllosProgressBar.value = _ellosScore / target;
            if (NosotrosProgressLabel != null) NosotrosProgressLabel.text = $"{_nosotrosScore}/{GameRules.SCORE_TO_WIN}";
            if (EllosProgressLabel != null) EllosProgressLabel.text = $"{_ellosScore}/{GameRules.SCORE_TO_WIN}";
        }

        void AddRoundRow(RoundRecord record)
        {
            if (RoundListContainer == null || RoundRowPrefab == null) return;
            var row = Instantiate(RoundRowPrefab, RoundListContainer);
            var texts = row.GetComponentsInChildren<TMP_Text>();
            if (texts.Length >= 4)
            {
                texts[0].text = $"Ronda {record.RoundNumber}";
                texts[1].text = record.WinningTeam == 0 ? "NOSOTROS" : "ELLOS";
                texts[2].text = $"+{record.PointsScored}";
                texts[3].text = record.IsKapicua ? "¡KAPICUA! +25" : "";
            }
        }

        void ScrollToBottom()
        {
            if (HistoryScroll != null)
                Canvas.ForceUpdateCanvases();
            if (HistoryScroll != null)
                HistoryScroll.verticalNormalizedPosition = 0f;
        }
    }
}
