using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Kapicua.UI
{
    /// <summary>
    /// Manages the two-screen splash sequence:
    ///   1. "More Games" studio logo (1.5s fade in, 1.5s hold, 0.5s fade out)
    ///   2. Kapicua! boot scene (2s hold, fade to login)
    ///
    /// Attach to the 00_Boot scene root.
    /// </summary>
    public class SplashScreenManager : MonoBehaviour
    {
        [Header("More Games Logo")]
        public CanvasGroup MoreGamesLogo;
        public float MoreGamesHoldSeconds = 1.5f;

        [Header("Kapicua Boot")]
        public CanvasGroup KapicuaBootCanvas;
        public float KapicuaHoldSeconds = 2f;

        [Header("Fade")]
        public float FadeInDuration = 0.6f;
        public float FadeOutDuration = 0.4f;

        [Header("Audio")]
        public AudioSource IntroAudio;

        void Start()
        {
            if (MoreGamesLogo != null) MoreGamesLogo.alpha = 0;
            if (KapicuaBootCanvas != null) KapicuaBootCanvas.alpha = 0;
            StartCoroutine(RunSplashSequence());
        }

        IEnumerator RunSplashSequence()
        {
            // ── More Games logo ──
            if (MoreGamesLogo != null)
            {
                yield return Fade(MoreGamesLogo, 0, 1, FadeInDuration);
                yield return new WaitForSeconds(MoreGamesHoldSeconds);
                yield return Fade(MoreGamesLogo, 1, 0, FadeOutDuration);
            }

            // ── Kapicua! boot screen ──
            if (KapicuaBootCanvas != null)
            {
                if (IntroAudio != null) IntroAudio.Play();
                yield return Fade(KapicuaBootCanvas, 0, 1, FadeInDuration);
                yield return new WaitForSeconds(KapicuaHoldSeconds);
                yield return Fade(KapicuaBootCanvas, 1, 0, FadeOutDuration);
            }

            // ── Load login scene ──
            SceneManager.LoadScene("01_Login");
        }

        IEnumerator Fade(CanvasGroup cg, float from, float to, float duration)
        {
            float elapsed = 0;
            cg.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            cg.alpha = to;
        }
    }
}
