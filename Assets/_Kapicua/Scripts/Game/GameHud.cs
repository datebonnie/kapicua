using System;
using System.Collections;
using Kapicua.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kapicua.Game
{
    /// <summary>Thin wrapper over the uGUI elements. References are wired by the scene bootstrapper.</summary>
    public class GameHud : MonoBehaviour
    {
        public TMP_Text ScoreText;
        public TMP_Text StatusText;
        public TMP_Text ToastText;
        public TMP_Text BannerText;
        public TMP_Text BannerButtonLabel;

        public GameObject PassRoot;
        public GameObject EndChoiceRoot;
        public GameObject BannerRoot;
        public GameObject ToastRoot;

        public Button PassButton;
        public Button LeftButton;
        public Button RightButton;
        public Button BannerButton;

        Coroutine _toast;

        public void SetScores(int us, int them) =>
            ScoreText.text = $"Nosotros {us} — Ellos {them}   (a {MatchState.TargetScore})";

        public void SetStatus(string text) => StatusText.text = text;

        public void ShowPass(Action onPass)
        {
            PassRoot.SetActive(true);
            PassButton.onClick.RemoveAllListeners();
            PassButton.onClick.AddListener(() =>
            {
                PassRoot.SetActive(false);
                onPass();
            });
        }

        public void ShowEndChoice(Action onLeft, Action onRight)
        {
            EndChoiceRoot.SetActive(true);
            LeftButton.onClick.RemoveAllListeners();
            RightButton.onClick.RemoveAllListeners();
            LeftButton.onClick.AddListener(() => onLeft());
            RightButton.onClick.AddListener(() => onRight());
        }

        public void HideEndChoice() => EndChoiceRoot.SetActive(false);

        public void ShowBanner(string text, string buttonLabel, Action onContinue)
        {
            BannerRoot.SetActive(true);
            BannerText.text = text;
            BannerButtonLabel.text = buttonLabel;
            BannerButton.onClick.RemoveAllListeners();
            BannerButton.onClick.AddListener(() =>
            {
                BannerRoot.SetActive(false);
                onContinue();
            });
        }

        public void Toast(string message)
        {
            if (_toast != null) StopCoroutine(_toast);
            _toast = StartCoroutine(ToastCo(message));
        }

        IEnumerator ToastCo(string message)
        {
            ToastText.text = message;
            ToastRoot.SetActive(true);
            yield return new WaitForSeconds(1.8f);
            ToastRoot.SetActive(false);
            _toast = null;
        }
    }
}
