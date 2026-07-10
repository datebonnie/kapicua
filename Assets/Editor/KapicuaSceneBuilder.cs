using System.Collections.Generic;
using System.IO;
using Kapicua.AI;
using Kapicua.Audio;
using Kapicua.Core;
using Kapicua.Game;
using Kapicua.Networking;
using Kapicua.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kapicua.EditorTools
{
    /// <summary>
    /// Builds all 4 Kapicua scenes from code and registers them in Build Settings.
    ///
    /// Run once via:  Kapicua ▸ Build All Scenes
    ///
    /// Safe to re-run at any time — each scene is cleared before being rebuilt,
    /// so GUIDs are preserved and source-control diffs remain clean.
    ///
    /// After running, Unity may need a moment to reimport; then press Play.
    /// </summary>
    public static class KapicuaSceneBuilder
    {
        // ── Scene paths ──────────────────────────────────────────────────────
        const string BootScene     = "Assets/_Kapicua/Scenes/00_Boot.unity";
        const string LoginScene    = "Assets/Scenes/01_Login.unity";
        const string MainMenuScene = "Assets/Scenes/02_MainMenu.unity";
        const string GameScene     = "Assets/Scenes/03_Game.unity";
        const string MaterialsDir  = "Assets/_Kapicua/Materials";
        const string PrefabsDir    = "Assets/_Kapicua/Prefabs";
        const string MoreGamesArt  = "Assets/Art/More Games screen.png";
        const string LoginBgArt    = "Assets/Art/background for login screen.png";
        const string InGameBgArt   = "Assets/Art/in game bachground.png";
        const string AppIconArt    = "Assets/Art/app icon.png";
        const string RoundedRectUI = "Assets/Art/UI/rounded_rect.png";
        const string FadeBottomUI  = "Assets/Art/UI/fade_bottom.png";
        const string IconApple     = "Assets/Art/UI/icon_apple.png";
        const string IconFacebook  = "Assets/Art/UI/icon_facebook.png";
        const string IconGoogle    = "Assets/Art/UI/icon_google.png";
        const string IconEmail     = "Assets/Art/UI/icon_email.png";

        // ── Brand palette ────────────────────────────────────────────────────
        static readonly Color Gold       = new Color(0.98f, 0.73f, 0.20f, 1f);
        static readonly Color Dark       = new Color(0.08f, 0.08f, 0.10f, 1f);
        static readonly Color PanelBg    = new Color(0.12f, 0.12f, 0.16f, 0.96f);
        static readonly Color RowBg      = new Color(0.17f, 0.17f, 0.21f, 1f);
        static readonly Color BtnDark    = new Color(0.20f, 0.20f, 0.26f, 1f);
        static readonly Color BtnGreen   = new Color(0.13f, 0.52f, 0.28f, 1f);
        static readonly Color BtnRed     = new Color(0.60f, 0.16f, 0.16f, 1f);
        static readonly Color TextWh     = Color.white;
        static readonly Color TextMuted  = new Color(0.62f, 0.62f, 0.65f, 1f);

        // ═══════════════════════════════════════════════════════════════════
        //  MENU ENTRY POINT
        // ═══════════════════════════════════════════════════════════════════

        [MenuItem("Kapicua/Build All Scenes")]
        public static void BuildAll()
        {
            BuildBoot();
            BuildLogin();
            BuildMainMenu();
            BuildGame();
            RegisterBuildScenes();
            SetAppIcon();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Kapicua] ✅ All 4 scenes built and registered.");
        }

        static void SetAppIcon()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconArt);
            if (tex == null)
            {
                Debug.LogWarning($"[Kapicua] App icon not found at {AppIconArt} — skipping.");
                return;
            }
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown,
                new[] { tex }, IconKind.Application);
            Debug.Log("[Kapicua] App icon set.");
        }

        static void RegisterBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScene,     true),
                new EditorBuildSettingsScene(LoginScene,    true),
                new EditorBuildSettingsScene(MainMenuScene, true),
                new EditorBuildSettingsScene(GameScene,     true),
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        //  00_BOOT  ─  Splash sequence: studio logo → Kapicua boot
        // ═══════════════════════════════════════════════════════════════════

        static void BuildBoot()
        {
            var scene = OpenAndClear(BootScene);

            // Camera (black background — splash draws over it)
            var camGO = Obj("Main Camera"); camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGO.AddComponent<AudioListener>();

            // Intro audio source (SplashScreenManager plays it)
            var audioGO = Obj("IntroAudio");
            var introAudio = audioGO.AddComponent<AudioSource>();
            introAudio.playOnAwake = false;

            // ── More Games studio logo canvas ──────────────────────────────
            var studioCanvas = MakeCanvas("MoreGamesCanvas");
            SetRect(RT(studioCanvas), V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            var studioBg = studioCanvas.AddComponent<Image>();
            studioBg.color = Color.black;
            var studioCG = studioCanvas.AddComponent<CanvasGroup>();
            studioCG.alpha = 0f;

            // Studio splash art (falls back to text if the PNG is missing)
            if (FullScreenArt(studioCanvas.transform, "LogoImage", MoreGamesArt, 1170f / 2532f) == null)
            {
                var logoTxt = MakeTMP(studioCanvas.transform, "LogoText", "MORE GAMES STUDIO", 56,
                    TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,0), V2(900,80));
                logoTxt.color = Color.white;
                logoTxt.fontStyle = FontStyles.Bold;
            }

            // ── Wire SplashScreenManager ───────────────────────────────────
            // KapicuaBootCanvas intentionally left null: after the studio splash
            // we go straight to the login scene (its art carries the branding).
            var splash = studioCanvas.AddComponent<SplashScreenManager>();
            splash.MoreGamesLogo   = studioCG;
            splash.IntroAudio      = introAudio;

            Save(scene, BootScene);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  01_LOGIN  ─  Four auth buttons + email panel
        // ═══════════════════════════════════════════════════════════════════

        static void BuildLogin()
        {
            var scene = OpenAndClear(LoginScene);

            MakeCam(Dark);
            MakeEventSystem();

            var canvas = MakeCanvas("Canvas");

            // Background fill — deep navy like the mock
            Panel(canvas.transform, "Background", V2(0,0), V2(1,1), V2(0,0), V2(0,0),
                new Color(0.043f, 0.055f, 0.10f));

            // ── Top art region (masked, art covers it) ────────────────────
            var artRegion = new GameObject("ArtRegion", typeof(RectTransform), typeof(RectMask2D));
            artRegion.transform.SetParent(canvas.transform, false);
            SetRect(RT(artRegion), V2(0, 0.44f), V2(1, 1), V2(0,0), V2(0,0));
            var loginArt = FullScreenArt(artRegion.transform, "BackgroundArt", LoginBgArt, 941f / 1672f);
            if (loginArt == null)
            {
                MakeTMP(canvas.transform, "AppLogo", "KAPICUA!", 96,
                    TextAlignmentOptions.Center, V2(0.5f,0.78f), V2(0.5f,0.78f), V2(0,0), V2(1000,120))
                    .color = Gold;
            }
            else
            {
                // Pin the art to the TOP of the masked region so the title stays
                // visible and only the bottom (table) is cropped — matches the mock.
                var artRT = (RectTransform)loginArt.transform;
                artRT.anchorMin = artRT.anchorMax = artRT.pivot = new Vector2(0.5f, 1f);
                artRT.anchoredPosition = Vector2.zero;

                // Gradient fade over the bottom of the art → dissolves into the bg navy
                var fadeSprite = LoadSprite(FadeBottomUI);
                if (fadeSprite != null)
                {
                    var fadeGO = new GameObject("BottomFade", typeof(RectTransform), typeof(Image));
                    fadeGO.transform.SetParent(artRegion.transform, false);
                    var fadeImg = fadeGO.GetComponent<Image>();
                    fadeImg.sprite = fadeSprite;
                    fadeImg.color = new Color(0.043f, 0.055f, 0.10f);  // must match Background
                    fadeImg.raycastTarget = false;
                    // Span the bottom 320px of the art region
                    SetRect(RT(fadeGO), V2(0, 0), V2(1, 0), V2(0, 160), V2(0, 320));
                }
            }

            // ── Welcome copy ──────────────────────────────────────────────
            MakeTMP(canvas.transform, "WelcomeTitle", "WELCOME TO KAPICUA!", 42,
                TextAlignmentOptions.Center, V2(0.5f,0.415f), V2(0.5f,0.415f), V2(0,0), V2(1000,56))
                .fontStyle = FontStyles.Bold;
            MakeTMP(canvas.transform, "WelcomeTagline", "Play with friends. Keep the score. Live the culture.", 26,
                TextAlignmentOptions.Center, V2(0.5f,0.388f), V2(0.5f,0.388f), V2(0,0), V2(1000,40))
                .color = TextMuted;

            // ── Auth buttons (rounded, icons cropped from the mock) ───────
            var btnSize = V2(900, 92);
            var darkBtn = new Color(0.10f, 0.12f, 0.19f);
            var appleBtn  = RoundedBtn(canvas.transform, "AppleButton",    "Continue with Apple",    32, V2(0.5f,0.345f), btnSize, Color.white,                        Color.black, IconApple);
            var faceBtn   = RoundedBtn(canvas.transform, "FacebookButton", "Continue with Facebook", 32, V2(0.5f,0.281f), btnSize, new Color(0.24f,0.365f,0.66f),      Color.white, IconFacebook);
            var googleBtn = RoundedBtn(canvas.transform, "GoogleButton",   "Continue with Google",   32, V2(0.5f,0.217f), btnSize, darkBtn,                            Color.white, IconGoogle);

            // "or" divider
            var divColor = new Color(1f, 1f, 1f, 0.18f);
            Panel(canvas.transform, "DividerL", V2(0.5f,0.170f), V2(0.5f,0.170f), V2(-255,0), V2(300,2), divColor);
            Panel(canvas.transform, "DividerR", V2(0.5f,0.170f), V2(0.5f,0.170f), V2( 255,0), V2(300,2), divColor);
            MakeTMP(canvas.transform, "DividerOr", "or", 26,
                TextAlignmentOptions.Center, V2(0.5f,0.170f), V2(0.5f,0.170f), V2(0,0), V2(120,36))
                .color = TextMuted;

            var emailBtn  = RoundedBtn(canvas.transform, "EmailButton",    "Continue with Email",    32, V2(0.5f,0.123f), btnSize, darkBtn, Color.white, IconEmail);

            // ── Sign-up link ──────────────────────────────────────────────
            var signUpLinkGO = new GameObject("SignUpLink", typeof(RectTransform), typeof(Button));
            signUpLinkGO.transform.SetParent(canvas.transform, false);
            SetRect(RT(signUpLinkGO), V2(0.5f,0.058f), V2(0.5f,0.058f), V2(0,0), V2(700,48));
            var signUpTmp = signUpLinkGO.AddComponent<TextMeshProUGUI>();
            signUpTmp.text = "Don't have an account? <color=#F9BA33>Sign up</color>";
            signUpTmp.fontSize = 30;
            signUpTmp.alignment = TextAlignmentOptions.Center;
            signUpTmp.color = TextWh;
            var signUpLink = signUpLinkGO.GetComponent<Button>();
            signUpLink.targetGraphic = signUpTmp;

            // ── Email panel (hidden) ──────────────────────────────────────
            var emailPanel = Panel(canvas.transform, "EmailPanel",
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,0), V2(620,500), PanelBg);
            emailPanel.SetActive(false);

            MakeTMP(emailPanel.transform, "PanelTitle", "Iniciar sesión", 40,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,195), V2(540,55))
                .color = Gold;

            var emailInput  = MakeInputField(emailPanel.transform, "EmailInput",    "Email",      V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0, 100), V2(520,66));
            var passInput   = MakeInputField(emailPanel.transform, "PasswordInput", "Contraseña", V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,  20), V2(520,66));
            passInput.contentType = TMP_InputField.ContentType.Password;

            var submitBtn  = Btn(emailPanel.transform, "EmailSubmitButton", "Entrar",        32, V2(0.5f,0.5f), V2(0.5f,0.5f), V2( 135,-70), V2(240,66), BtnGreen);
            var signUpBtn  = Btn(emailPanel.transform, "SignUpButton",      "Crear cuenta",  30, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(-135,-70), V2(240,66), BtnDark);
            var backBtn    = Btn(emailPanel.transform, "EmailBackButton",   "← Atrás",       26, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,-155),   V2(200,50), Color.clear);

            var errText = MakeTMP(emailPanel.transform, "EmailErrorText", "", 22,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,-205), V2(520,40));
            errText.color = new Color(1f, 0.38f, 0.38f);

            // ── Loading overlay (hidden) ──────────────────────────────────
            var loadingOverlay = Panel(canvas.transform, "LoadingOverlay",
                V2(0,0), V2(1,1), V2(0,0), V2(0,0), new Color(0,0,0,0.78f));
            loadingOverlay.SetActive(false);
            var statusText = MakeTMP(loadingOverlay.transform, "StatusText", "Conectando...", 40,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,0), V2(700,60));

            // ── Wire LoginManager ─────────────────────────────────────────
            var loginMgr = canvas.AddComponent<LoginManager>();
            loginMgr.AppleButton        = appleBtn;
            loginMgr.FacebookButton     = faceBtn;
            loginMgr.GoogleButton       = googleBtn;
            loginMgr.EmailButton        = emailBtn;
            loginMgr.SignUpButton       = signUpBtn;
            loginMgr.EmailPanel         = emailPanel;
            loginMgr.EmailInput         = emailInput;
            loginMgr.PasswordInput      = passInput;
            loginMgr.EmailSubmitButton  = submitBtn;
            loginMgr.EmailBackButton    = backBtn;
            loginMgr.EmailErrorText     = errText;
            loginMgr.LoadingOverlay     = loadingOverlay;
            loginMgr.StatusText         = statusText;
            loginMgr.SignUpLinkButton   = signUpLink;

            Save(scene, LoginScene);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  02_MAINMENU  ─  3-tab layout: Sala | Score | Radio
        // ═══════════════════════════════════════════════════════════════════

        static void BuildMainMenu()
        {
            var scene = OpenAndClear(MainMenuScene);

            MakeCam(Dark);
            MakeEventSystem();

            // Background managers (no UI, just MonoBehaviours)
            new GameObject("LobbyManager").AddComponent<LobbyManager>();
            new GameObject("RelayManager").AddComponent<RelayManager>();

            // Shared audio source for the radio tab
            var radioAudioGO = Obj("RadioAudio");
            var musicSrc = radioAudioGO.AddComponent<AudioSource>();
            musicSrc.playOnAwake = false;
            musicSrc.loop = false;

            var canvas = MakeCanvas("Canvas");

            // ── Background ────────────────────────────────────────────────
            Panel(canvas.transform, "Background", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Dark);

            // ── Header bar ────────────────────────────────────────────────
            var header = Panel(canvas.transform, "Header",
                V2(0,1), V2(1,1), V2(0,0), V2(0,-60),
                new Color(0.06f,0.06f,0.09f));
            var displayNameTxt = MakeTMP(header.transform, "DisplayNameText", "Jugador", 28,
                TextAlignmentOptions.Left, V2(0,0), V2(1,1), V2(0,0), V2(-40,0));
            displayNameTxt.margin = new Vector4(18,0,0,0);

            // ── Tab bar (bottom) ──────────────────────────────────────────
            var tabBar = Panel(canvas.transform, "TabBar",
                V2(0,0), V2(1,0), V2(0,0), V2(0,88),
                new Color(0.06f,0.06f,0.09f));

            // Each tab occupies 1/3 of the width
            var privateRoomTab = Btn(tabBar.transform, "PrivateRoomTab", "SALA", 24,
                V2(0,0), V2(0.333f,1), V2(0,0), V2(0,0), Color.clear);
            var privateInd = Panel(privateRoomTab.transform, "Indicator",
                V2(0.08f,0), V2(0.92f,0), V2(0,0), V2(0,4), Gold);

            var scoreTab = Btn(tabBar.transform, "ScoreTab", "SCORE", 24,
                V2(0.333f,0), V2(0.666f,1), V2(0,0), V2(0,0), Color.clear);
            var scoreInd = Panel(scoreTab.transform, "Indicator",
                V2(0.08f,0), V2(0.92f,0), V2(0,0), V2(0,4), TextMuted);

            var radioTab = Btn(tabBar.transform, "RadioTab", "RADIO", 24,
                V2(0.666f,0), V2(1,1), V2(0,0), V2(0,0), Color.clear);
            var radioInd = Panel(radioTab.transform, "Indicator",
                V2(0.08f,0), V2(0.92f,0), V2(0,0), V2(0,4), TextMuted);

            // ── Content area (between header and tab bar) ─────────────────
            var content = new GameObject("ContentArea", typeof(RectTransform));
            content.transform.SetParent(canvas.transform, false);
            SetRect(RT(content), V2(0,0), V2(1,1), V2(0,88), V2(0,-148));

            // ── Tab panels ────────────────────────────────────────────────
            var roomPanel  = BuildRoomPanel(content.transform);
            var scorePanel = BuildScorePanel(content.transform);
            var radioPanel = BuildRadioPanel(content.transform, musicSrc);
            scorePanel.SetActive(false);
            radioPanel.SetActive(false);

            // ── Wire MainMenuManager ──────────────────────────────────────
            var mainMenu = canvas.AddComponent<MainMenuManager>();
            mainMenu.DisplayNameText      = displayNameTxt;
            mainMenu.PrivateRoomTab       = privateRoomTab;
            mainMenu.ScoreTab             = scoreTab;
            mainMenu.RadioTab             = radioTab;
            mainMenu.PrivateRoomPanel     = roomPanel;
            mainMenu.ScorePanel           = scorePanel;
            mainMenu.RadioPanel           = radioPanel;
            mainMenu.PrivateRoomIndicator = privateInd.GetComponent<Image>();
            mainMenu.ScoreIndicator       = scoreInd.GetComponent<Image>();
            mainMenu.RadioIndicator       = radioInd.GetComponent<Image>();

            Save(scene, MainMenuScene);
        }

        // ── Private Room panel ────────────────────────────────────────────────

        static GameObject BuildRoomPanel(Transform parent)
        {
            var root = new GameObject("PrivateRoomPanel", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            SetRect(RT(root), V2(0,0), V2(1,1), V2(0,0), V2(0,0));

            // ─ Idle panel ────────────────────────────────────────────────
            var idle = new GameObject("IdlePanel", typeof(RectTransform));
            idle.transform.SetParent(root.transform, false);
            SetRect(RT(idle), V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,0), V2(720,320));

            MakeTMP(idle.transform, "Title", "SALA PRIVADA", 52,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,100), V2(640,72))
                .color = Gold;
            var createBtn = Btn(idle.transform, "CreateRoomButton", "CREAR SALA", 38,
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,10), V2(480,76), Gold);
            createBtn.GetComponentInChildren<TMP_Text>().color = Color.black;
            Btn(idle.transform, "JoinRoomButton", "UNIRME CON CÓDIGO", 32,
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,-80), V2(480,68), BtnDark);
            var practiceBtn = Btn(idle.transform, "PracticeButton", "PRÁCTICA VS IA", 32,
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,-170), V2(480,68), BtnGreen);
            practiceBtn.gameObject.AddComponent<PracticeModeButton>();

            // ─ Join code input panel ──────────────────────────────────────
            var joinPanel = Panel(root.transform, "JoinCodeInputPanel",
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,0), V2(640,340), PanelBg);
            joinPanel.SetActive(false);

            MakeTMP(joinPanel.transform, "Title", "CÓDIGO DE SALA", 38,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,110), V2(560,55))
                .color = Gold;

            var joinInput = MakeInputField(joinPanel.transform, "JoinCodeInput", "XXXXXX",
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,30), V2(380,76));
            joinInput.characterLimit = 6;

            var submitJoin = Btn(joinPanel.transform, "SubmitJoinButton", "ENTRAR", 32,
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(105,-65), V2(230,68), BtnGreen);
            var cancelJoin = Btn(joinPanel.transform, "CancelJoinButton", "CANCELAR", 28,
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(-135,-65), V2(230,68), BtnDark);

            var joinErr = MakeTMP(joinPanel.transform, "JoinErrorText", "", 22,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,-140), V2(540,40));
            joinErr.color = new Color(1f, 0.38f, 0.38f);

            // ─ Waiting lobby panel ────────────────────────────────────────
            var wait = Panel(root.transform, "WaitingPanel",
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,0), V2(740,580), PanelBg);
            wait.SetActive(false);

            // Invite code
            MakeTMP(wait.transform, "CodeLabel", "CÓDIGO DE INVITACIÓN", 22,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,235), V2(620,34))
                .color = TextMuted;
            var inviteCodeTxt = MakeTMP(wait.transform, "InviteCodeText", "------", 72,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(-55,175), V2(400,90));
            inviteCodeTxt.color = Gold;
            inviteCodeTxt.characterSpacing = 10;
            var copyBtn = Btn(wait.transform, "CopyCodeButton", "COPIAR", 22,
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(230,175), V2(130,50), BtnDark);
            var copyFeedback = MakeTMP(wait.transform, "CopyFeedbackText", "¡Copiado!", 22,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,130), V2(200,36));
            copyFeedback.color = BtnGreen;
            copyFeedback.gameObject.SetActive(false);

            // 4 player slot rows
            var playerSlots   = new TMP_Text[4];
            var slotBorders   = new Image[4];
            float[] slotY     = { 70f, 18f, -34f, -86f };
            string[] defaults = { "Esperando...", "Esperando...", "Esperando...", "Esperando..." };
            for (int i = 0; i < 4; i++)
            {
                var slotRow = Panel(wait.transform, $"Slot{i + 1}",
                    V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0, slotY[i]), V2(600,44),
                    new Color(0.18f,0.18f,0.22f));
                slotBorders[i]  = slotRow.GetComponent<Image>();
                playerSlots[i]  = MakeTMP(slotRow.transform, "Name", defaults[i], 27,
                    TextAlignmentOptions.Center, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            }

            var countTxt = MakeTMP(wait.transform, "PlayerCountText", "0 / 4 jugadores", 24,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,-115), V2(400,40));
            countTxt.color = TextMuted;

            var startBtn = Btn(wait.transform, "StartGameButton", "¡EMPEZAR!", 40,
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,-175), V2(390,76), BtnGreen);
            startBtn.interactable = false;
            startBtn.gameObject.SetActive(false); // only host sees this

            var leaveBtn = Btn(wait.transform, "LeaveRoomButton", "Salir de la sala", 26,
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,-240), V2(280,52), BtnDark);

            var waitStatus = MakeTMP(wait.transform, "WaitingStatusText", "Esperando jugadores...", 24,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,-275), V2(600,40));
            waitStatus.color = TextMuted;

            // ── Wire RoomManager onto panel root ──────────────────────────
            var rm = root.AddComponent<RoomManager>();
            rm.IdlePanel          = idle;
            rm.WaitingPanel       = wait;
            rm.JoinCodeInputPanel = joinPanel;
            rm.CreateRoomButton   = createBtn;
            rm.JoinRoomButton     = idle.transform.Find("JoinRoomButton").GetComponent<Button>();
            rm.InviteCodeText     = inviteCodeTxt;
            rm.CopyCodeButton     = copyBtn;
            rm.CopyFeedbackText   = copyFeedback;
            rm.JoinCodeInput      = joinInput;
            rm.SubmitJoinButton   = submitJoin;
            rm.CancelJoinButton   = cancelJoin;
            rm.JoinErrorText      = joinErr;
            rm.PlayerSlots        = playerSlots;
            rm.PlayerSlotBorders  = slotBorders;
            rm.PlayerCountText    = countTxt;
            rm.StartGameButton    = startBtn;
            rm.LeaveRoomButton    = leaveBtn;
            rm.WaitingStatusText  = waitStatus;

            return root;
        }

        // ── Score panel ───────────────────────────────────────────────────────

        static GameObject BuildScorePanel(Transform parent)
        {
            var root = new GameObject("ScorePanel", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            SetRect(RT(root), V2(0,0), V2(1,1), V2(0,0), V2(0,0));

            // Score numbers
            MakeTMP(root.transform, "NosLabel", "NOSOTROS", 26,
                TextAlignmentOptions.Center, V2(0.22f,0.82f), V2(0.22f,0.82f), V2(0,0), V2(260,40))
                .color = TextMuted;
            var nosTxt = MakeTMP(root.transform, "NosotrosScore", "0", 100,
                TextAlignmentOptions.Center, V2(0.22f,0.70f), V2(0.22f,0.70f), V2(0,0), V2(220,128));
            nosTxt.color = Gold;

            MakeTMP(root.transform, "EllosLabel", "ELLOS", 26,
                TextAlignmentOptions.Center, V2(0.78f,0.82f), V2(0.78f,0.82f), V2(0,0), V2(220,40))
                .color = TextMuted;
            var ellosTxt = MakeTMP(root.transform, "EllosScore", "0", 100,
                TextAlignmentOptions.Center, V2(0.78f,0.70f), V2(0.78f,0.70f), V2(0,0), V2(220,128));
            ellosTxt.color = TextMuted;

            // Progress bars
            var nosProg  = MakeSlider(root.transform, "NosotrosProgress",  V2(0.05f,0.56f), V2(0.45f,0.56f), V2(0,0), V2(0,20));
            var ellosProg = MakeSlider(root.transform, "EllosProgress",     V2(0.55f,0.56f), V2(0.95f,0.56f), V2(0,0), V2(0,20));

            var nosProgLbl = MakeTMP(root.transform, "NosotrosProgressLabel", "0/200", 20,
                TextAlignmentOptions.Center, V2(0.25f,0.52f), V2(0.25f,0.52f), V2(0,0), V2(200,32));
            nosProgLbl.color = TextMuted;
            var ellosProgLbl = MakeTMP(root.transform, "EllosProgressLabel", "0/200", 20,
                TextAlignmentOptions.Center, V2(0.75f,0.52f), V2(0.75f,0.52f), V2(0,0), V2(200,32));
            ellosProgLbl.color = TextMuted;

            // Manual entry row
            var manualInput = MakeInputField(root.transform, "ManualPointsInput", "Puntos",
                V2(0.5f,0.18f), V2(0.5f,0.18f), V2(-185,0), V2(190,58));
            manualInput.contentType = TMP_InputField.ContentType.DecimalNumber;

            var addNosBtn  = Btn(root.transform, "AddNosotrosButton", "+ NOS", 22,
                V2(0.5f,0.18f), V2(0.5f,0.18f), V2(35,0),  V2(180,58), BtnGreen);
            var addEllosBtn = Btn(root.transform, "AddEllosButton",   "+ ELLOS", 22,
                V2(0.5f,0.18f), V2(0.5f,0.18f), V2(225,0), V2(180,58), BtnDark);
            var resetBtn   = Btn(root.transform, "ResetButton", "REINICIAR", 22,
                V2(0.5f,0.07f), V2(0.5f,0.07f), V2(0,0), V2(220,52), BtnRed);

            // Win banner (hidden)
            var winBanner = Panel(root.transform, "WinBanner",
                V2(0,0), V2(1,1), V2(0,0), V2(0,0), new Color(0,0,0,0.88f));
            winBanner.SetActive(false);
            var winnerTxt = MakeTMP(winBanner.transform, "WinnerText", "¡NOSOTROS GANA!", 80,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,0), V2(1000,110));
            winnerTxt.color = Gold;

            // ── Wire ScoreTabManager ──────────────────────────────────────
            var sm = root.AddComponent<ScoreTabManager>();
            sm.NosotrosScoreText   = nosTxt;
            sm.EllosScoreText      = ellosTxt;
            sm.NosotrosProgressBar = nosProg;
            sm.EllosProgressBar    = ellosProg;
            sm.NosotrosProgressLabel = nosProgLbl;
            sm.EllosProgressLabel  = ellosProgLbl;
            sm.AddNosotrosButton   = addNosBtn;
            sm.AddEllosButton      = addEllosBtn;
            sm.ManualPointsInput   = manualInput;
            sm.ResetButton         = resetBtn;
            sm.WinBanner           = winBanner;
            sm.WinnerText          = winnerTxt;

            return root;
        }

        // ── Radio panel ───────────────────────────────────────────────────────

        static GameObject BuildRadioPanel(Transform parent, AudioSource musicSrc)
        {
            var root = new GameObject("RadioPanel", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            SetRect(RT(root), V2(0,0), V2(1,1), V2(0,0), V2(0,0));

            Panel(root.transform, "Card", V2(0.08f,0.1f), V2(0.92f,0.9f), V2(0,0), V2(0,0), PanelBg);

            MakeTMP(root.transform, "RadioTitle", "KAPICUA RADIO", 44,
                TextAlignmentOptions.Center, V2(0.5f,0.78f), V2(0.5f,0.78f), V2(0,0), V2(720,60))
                .color = Gold;

            var nowPlaying = MakeTMP(root.transform, "NowPlayingText", "Cargando...", 32,
                TextAlignmentOptions.Center, V2(0.5f,0.60f), V2(0.5f,0.60f), V2(0,0), V2(720,50));
            var artistTxt = MakeTMP(root.transform, "ArtistText", "", 26,
                TextAlignmentOptions.Center, V2(0.5f,0.52f), V2(0.5f,0.52f), V2(0,0), V2(720,42));
            artistTxt.color = TextMuted;

            // Volume slider + mute
            var volSlider = MakeSlider(root.transform, "VolumeSlider",
                V2(0.12f,0.38f), V2(0.80f,0.38f), V2(0,0), V2(0,24));
            volSlider.value = 0.8f;
            var muteBtn = Btn(root.transform, "MuteButton", "🔊", 34,
                V2(0.87f,0.38f), V2(0.87f,0.38f), V2(0,0), V2(58,58), Color.clear);

            // Loading indicator
            var loadingGO = new GameObject("LoadingIndicator", typeof(RectTransform));
            loadingGO.transform.SetParent(root.transform, false);
            SetRect(RT(loadingGO), V2(0.5f,0.28f), V2(0.5f,0.28f), V2(0,0), V2(300,44));
            var loadTxt = loadingGO.AddComponent<TextMeshProUGUI>();
            loadTxt.text = "Cargando pista...";
            loadTxt.fontSize = 24;
            loadTxt.alignment = TextAlignmentOptions.Center;
            loadTxt.color = TextMuted;
            loadingGO.SetActive(false);

            // ── Wire RadioManager ─────────────────────────────────────────
            var rm = root.AddComponent<RadioManager>();
            rm.NowPlayingText   = nowPlaying;
            rm.ArtistText       = artistTxt;
            rm.VolumeSlider     = volSlider;
            rm.MuteButton       = muteBtn;
            rm.LoadingIndicator = loadingGO;
            rm.MusicSource      = musicSrc;
            rm.RadioFolder      = "Radio";   // matches CopyMusicToProject destination

            return root;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  03_GAME  ─  NetworkManager + 3-D table + 2-D HUD
        // ═══════════════════════════════════════════════════════════════════

        static void BuildGame()
        {
            var scene = OpenAndClear(GameScene);

            // ── NetworkManager ────────────────────────────────────────────
            var nmGO      = new GameObject("NetworkManager");
            var netMgr    = nmGO.AddComponent<NetworkManager>();
            var transport = nmGO.AddComponent<UnityTransport>();
            // Assign transport via SerializedObject so it survives serialisation
            using (var so = new SerializedObject(netMgr))
            {
                var cfg = so.FindProperty("NetworkConfig");
                var transportProp = cfg?.FindPropertyRelative("NetworkTransport");
                if (transportProp != null)
                {
                    transportProp.objectReferenceValue = transport;
                    so.ApplyModifiedProperties();
                }
            }

            // ── 3-D environment (table, sun, camera) ──────────────────────
            Build3DEnv();

            // ── Ambience backdrop behind the 3-D table ────────────────────
            // Screen Space – Camera canvas at far plane: table renders in front.
            var mainCam = GameObject.Find("Main Camera")?.GetComponent<Camera>();
            if (mainCam != null)
            {
                var bgCanvasGO = new GameObject("BackgroundCanvas", typeof(Canvas));
                var bgCanvas = bgCanvasGO.GetComponent<Canvas>();
                bgCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                bgCanvas.worldCamera = mainCam;
                bgCanvas.planeDistance = mainCam.farClipPlane * 0.9f;
                var bgScaler = bgCanvasGO.AddComponent<CanvasScaler>();
                bgScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                bgScaler.referenceResolution = new Vector2(1080, 1920);
                FullScreenArt(bgCanvasGO.transform, "InGameBackground", InGameBgArt, 941f / 1672f);
            }

            // ── Board renderer (3-D domino chain on the table) ────────────
            var boardRoot = Obj("BoardRoot");
            boardRoot.transform.position = Vector3.zero;
            var layoutView    = boardRoot.AddComponent<BoardLayoutView>();
            var boardRenderer = boardRoot.AddComponent<BoardRenderer>();
            boardRenderer.LayoutView = layoutView;

            // ── Runtime manager bundle ────────────────────────────────────
            var mgrsGO    = Obj("Managers");
            mgrsGO.AddComponent<NetworkObject>();   // required: NetworkGameManager is a NetworkBehaviour
            var matchMgr  = mgrsGO.AddComponent<MatchManager>();
            var roundMgr  = mgrsGO.AddComponent<RoundManager>();
            var scoreMgr  = mgrsGO.AddComponent<ScoreManager>();
            var turnMgr   = mgrsGO.AddComponent<TurnManager>();
            var netGameMgr = mgrsGO.AddComponent<NetworkGameManager>();

            // Wire cross-references between managers
            matchMgr.RoundManager = roundMgr;
            matchMgr.ScoreManager = scoreMgr;
            matchMgr.TurnManager  = turnMgr;
            roundMgr.TurnManager  = turnMgr;
            roundMgr.ScoreManager = scoreMgr;
            netGameMgr.MatchManager = matchMgr;

            // AI seats for solo practice (host-side)
            var aiCtrl = mgrsGO.AddComponent<AIController>();
            aiCtrl.Match = matchMgr;
            netGameMgr.AIController = aiCtrl;

            // ── Radio audio ───────────────────────────────────────────────
            var radioAudio = Obj("RadioAudio");
            var musicSrc   = radioAudio.AddComponent<AudioSource>();
            musicSrc.playOnAwake = false;
            var radioMgr = mgrsGO.AddComponent<RadioManager>();
            radioMgr.MusicSource = musicSrc;
            radioMgr.RadioFolder = "Radio";

            // ── In-game HUD canvas ────────────────────────────────────────
            MakeEventSystem();
            var canvas = MakeCanvas("GameCanvas");
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);

            // Header strip
            var header = Panel(canvas.transform, "Header",
                V2(0,1), V2(1,1), V2(0,0), V2(0,-70),
                new Color(0,0,0,0.72f));
            var partidaTxt = MakeTMP(header.transform, "PartidaText", "Partida 1", 24,
                TextAlignmentOptions.Left, V2(0,0), V2(0.33f,1), V2(14,0), V2(0,0));
            var rondaTxt = MakeTMP(header.transform, "RondaText", "Ronda 1", 24,
                TextAlignmentOptions.Center, V2(0.33f,0), V2(0.67f,1), V2(0,0), V2(0,0));
            var nosTxt = MakeTMP(header.transform, "NosotrosScore", "NOS  0", 26,
                TextAlignmentOptions.Right, V2(0.67f,0), V2(1,1), V2(-12,0), V2(0,0));
            nosTxt.color = Gold;
            var ellosTxt = MakeTMP(header.transform, "EllosScore", "0  ELLOS", 26,
                TextAlignmentOptions.Left, V2(1,0), V2(1,1), V2(8,0), V2(200,0));

            // Player zones (4 zones around the board)
            var zonesGO = new GameObject("PlayerZones", typeof(RectTransform));
            zonesGO.transform.SetParent(canvas.transform, false);
            SetRect(RT(zonesGO), V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            var zones    = new PlayerZoneUI[4];
            Vector2[] zonePivots = { V2(0.5f,0.05f), V2(0.95f,0.5f), V2(0.5f,0.92f), V2(0.05f,0.5f) };
            string[] zoneNames   = { "ZoneBottom (You)", "ZoneRight", "ZoneTop (Partner)", "ZoneLeft" };
            for (int i = 0; i < 4; i++)
            {
                var zGO = Panel(zonesGO.transform, zoneNames[i],
                    zonePivots[i], zonePivots[i], V2(0,0), V2(280,100), new Color(0,0,0,0.6f));
                zones[i] = zGO.AddComponent<PlayerZoneUI>();
                zones[i].PlayerNameText = MakeTMP(zGO.transform, "Name",
                    (i == 0) ? "Tú" : (i == 2) ? "Compañero" : "Rival", 24,
                    TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,16), V2(260,36));
                zones[i].TileCountText = MakeTMP(zGO.transform, "Tiles", "7", 32,
                    TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,-18), V2(80,42));
                var activeInd = Panel(zGO.transform, "ActiveIndicator",
                    V2(0,0), V2(1,0), V2(0,0), V2(0,4), Color.clear);
                zones[i].ActiveIndicator = activeInd.GetComponent<Image>();
                var passInd = new GameObject("PassIndicator", typeof(RectTransform));
                passInd.transform.SetParent(zGO.transform, false);
                SetRect(RT(passInd), V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,0), V2(140,44));
                MakeTMP(passInd.transform, "PassText", "PASO", 28,
                    TextAlignmentOptions.Center, V2(0,0), V2(1,1), V2(0,0), V2(0,0))
                    .color = new Color(1f, 0.38f, 0.38f);
                passInd.SetActive(false);
                zones[i].PassIndicatorObj = passInd;
            }

            // TU TURNO panel
            var tuTurnoPanel = Panel(canvas.transform, "TuTurnoPanel",
                V2(0.5f,0.15f), V2(0.5f,0.15f), V2(0,0), V2(340,72), Gold);
            tuTurnoPanel.SetActive(false);
            var tuTurnoTxt = MakeTMP(tuTurnoPanel.transform, "TuTurnoText", "¡TU TURNO!", 36,
                TextAlignmentOptions.Center, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            tuTurnoTxt.color = Color.black;

            // Pass button
            var passBtn = Btn(canvas.transform, "PassButton", "PASO", 32,
                V2(0.82f,0.15f), V2(0.82f,0.15f), V2(0,0), V2(170,68), BtnRed);
            passBtn.gameObject.SetActive(false);

            // Your hand container (bottom strip)
            var handGO = new GameObject("HandContainer", typeof(RectTransform));
            handGO.transform.SetParent(canvas.transform, false);
            SetRect(RT(handGO), V2(0,0), V2(1,0), V2(0,8), V2(0,140));
            var hLayout = handGO.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.spacing = 6;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;

            // Round-end overlay
            var roundEnd = Panel(canvas.transform, "RoundEndPanel",
                V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,0), V2(720,320), new Color(0,0,0,0.92f));
            roundEnd.SetActive(false);
            var roundResult = MakeTMP(roundEnd.transform, "RoundResultText", "NOSOTROS +25", 56,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,50), V2(640,76));
            roundResult.color = Gold;
            var kapicuaBanner = MakeTMP(roundEnd.transform, "KapicuaBanner", "¡KAPICUA!", 40,
                TextAlignmentOptions.Center, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,-40), V2(520,56));
            kapicuaBanner.color = new Color(1f, 0.38f, 0.38f);
            kapicuaBanner.gameObject.SetActive(false);

            // Chat panel
            var chatPanel = Panel(canvas.transform, "ChatPanel",
                V2(0,0), V2(0.38f,0.48f), V2(0,148), V2(0,0), new Color(0,0,0,0.86f));
            chatPanel.SetActive(false);
            var chatMsgContainer = new GameObject("ChatMessages", typeof(RectTransform));
            chatMsgContainer.transform.SetParent(chatPanel.transform, false);
            SetRect(RT(chatMsgContainer), V2(0,0.15f), V2(1,1), V2(4,4), V2(-8,-4));
            var chatVL = chatMsgContainer.AddComponent<VerticalLayoutGroup>();
            chatVL.childAlignment = TextAnchor.LowerLeft;
            chatVL.spacing = 2;

            var chatInput = MakeInputField(chatPanel.transform, "ChatInput", "Escribe...",
                V2(0,0), V2(0.75f,0.15f), V2(0,0), V2(0,0));
            var chatSendBtn = Btn(chatPanel.transform, "ChatSendButton", "↑", 30,
                V2(0.75f,0), V2(1,0.15f), V2(0,0), V2(0,0), Gold);
            chatSendBtn.GetComponentInChildren<TMP_Text>().color = Color.black;

            // Bottom action buttons
            var chatBtn  = Btn(canvas.transform, "ChatButton",  "💬", 28, V2(0,0), V2(0,0), V2(20,155),   V2(64,64), BtnDark);
            var emojiBtn = Btn(canvas.transform, "EmojiButton", "😄", 28, V2(0,0), V2(0,0), V2(94,155),   V2(64,64), BtnDark);
            var daleBtn  = Btn(canvas.transform, "DaleButton",  "¡DALE!", 22, V2(1,0), V2(1,0), V2(-20,155), V2(128,64), Gold);
            daleBtn.GetComponentInChildren<TMP_Text>().color = Color.black;

            // Emoji panel
            var emojiPanel = Panel(canvas.transform, "EmojiPanel",
                V2(0,0), V2(0,0), V2(18,228), V2(288,120), BtnDark);
            emojiPanel.SetActive(false);
            string[] emojis = { "🔥", "😂", "💪", "👊" };
            var emojiButtons = new Button[emojis.Length];
            for (int i = 0; i < emojis.Length; i++)
                emojiButtons[i] = Btn(emojiPanel.transform, $"Emoji{i}", emojis[i], 32,
                    V2(0,0), V2(0,0), V2(10 + i * 68, 10), V2(60,100), Color.clear);

            // ── Tile prefab (simple coloured rect + TileButton) ───────────
            Directory.CreateDirectory(PrefabsDir);
            const string tilePrefabPath = PrefabsDir + "/TilePrefab.prefab";
            var existingTilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(tilePrefabPath);
            if (existingTilePrefab == null)
            {
                var tGO = new GameObject("TilePrefab", typeof(RectTransform), typeof(Image), typeof(Button));
                SetRect((RectTransform)tGO.transform, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,0), V2(76,126));
                tGO.GetComponent<Image>().color = Color.white;
                tGO.AddComponent<TileButton>();
                // Pip faces are drawn at runtime by TileButton.BuildFace().
                existingTilePrefab = PrefabUtility.SaveAsPrefabAsset(tGO, tilePrefabPath);
                Object.DestroyImmediate(tGO);
            }

            // ── Chat message prefab (one TMP line per message) ────────────
            const string chatPrefabPath = PrefabsDir + "/ChatMessage.prefab";
            var chatMsgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(chatPrefabPath);
            if (chatMsgPrefab == null)
            {
                var cGO = new GameObject("ChatMessage", typeof(RectTransform));
                var le = cGO.AddComponent<LayoutElement>();
                le.minHeight = 34;
                var cTxt = cGO.AddComponent<TextMeshProUGUI>();
                cTxt.fontSize = 22;
                cTxt.enableWordWrapping = true;
                cTxt.text = "Jugador: mensaje";
                chatMsgPrefab = PrefabUtility.SaveAsPrefabAsset(cGO, chatPrefabPath);
                Object.DestroyImmediate(cGO);
            }

            // ── Board end-choice buttons (hidden until needed) ────────────
            var leftEndBtn  = Btn(canvas.transform, "LeftEndButton",  "◀", 40,
                V2(0, 0.5f), V2(0, 0.5f), V2(80, 0),  V2(120, 120), new Color(0, 0, 0, 0.6f));
            var rightEndBtn = Btn(canvas.transform, "RightEndButton", "▶", 40,
                V2(1, 0.5f), V2(1, 0.5f), V2(-80, 0), V2(120, 120), new Color(0, 0, 0, 0.6f));
            leftEndBtn.gameObject.SetActive(false);
            rightEndBtn.gameObject.SetActive(false);
            boardRenderer.LeftEndButton  = leftEndBtn;
            boardRenderer.RightEndButton = rightEndBtn;

            // ── Wire GameUIManager ────────────────────────────────────────
            var gameUI = mgrsGO.AddComponent<GameUIManager>();
            gameUI.BoardRenderer        = boardRenderer;
            gameUI.PartidaText          = partidaTxt;
            gameUI.RondaText            = rondaTxt;
            gameUI.NosotrosScoreText    = nosTxt;
            gameUI.EllosScoreText       = ellosTxt;
            gameUI.PlayerZones          = zones;
            gameUI.HandContainer        = handGO.transform;
            gameUI.TilePrefab           = existingTilePrefab;
            gameUI.TuTurnoPanel         = tuTurnoPanel;
            gameUI.TurnStatusText       = tuTurnoTxt;
            gameUI.PassButton           = passBtn;
            gameUI.RoundEndPanel        = roundEnd;
            gameUI.RoundResultText      = roundResult;
            gameUI.KapicuaBanner        = kapicuaBanner;
            gameUI.ChatPanel            = chatPanel;
            gameUI.ChatInput            = chatInput;
            gameUI.ChatSendButton       = chatSendBtn;
            gameUI.ChatMessageContainer = chatMsgContainer.transform;
            gameUI.ChatMessagePrefab    = chatMsgPrefab;
            gameUI.ChatButton           = chatBtn;
            gameUI.EmojiButton          = emojiBtn;
            gameUI.DaleButton           = daleBtn;
            gameUI.EmojiPanel           = emojiPanel;
            gameUI.EmojiButtons         = emojiButtons;

            Save(scene, GameScene);
        }

        static void Build3DEnv()
        {
            // Green felt table
            Directory.CreateDirectory(MaterialsDir);
            var felt = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/TableFelt.mat");
            if (felt == null)
            {
                felt = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                felt.color = new Color(0.05f, 0.35f, 0.18f);
                felt.SetFloat("_Smoothness", 0.15f);
                AssetDatabase.CreateAsset(felt, $"{MaterialsDir}/TableFelt.mat");
            }
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.position  = new Vector3(0, -0.25f, 0);
            table.transform.localScale = new Vector3(22f, 0.5f, 13.5f);
            table.GetComponent<MeshRenderer>().sharedMaterial = felt;

            // Directional sun
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type      = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55, -35, 0);
            sun.intensity = 1.15f;
            sun.color     = new Color(1f, 0.96f, 0.9f);
            sun.shadows   = LightShadows.Soft;

            // Overhead camera
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam   = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            camGO.transform.position = new Vector3(0, 14f, -8.5f);
            camGO.transform.rotation = Quaternion.Euler(58, 0, 0);
            cam.fieldOfView    = 50;
            cam.clearFlags     = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.11f);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════════

        static UnityEngine.SceneManagement.Scene OpenAndClear(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                Object.DestroyImmediate(root);
            return scene;
        }

        /// <summary>Loads a PNG as a Sprite, forcing the importer to Sprite mode if needed.</summary>
        static Sprite LoadSprite(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
            {
                Debug.LogWarning($"[Kapicua] Art asset not found: {path} — skipping.");
                return null;
            }
            if (imp.textureType != TextureImporterType.Sprite)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.maxTextureSize = 4096;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Loads the rounded-rect sprite with a 9-slice border so corners never stretch.</summary>
        static Sprite LoadSpriteSliced(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
            {
                Debug.LogWarning($"[Kapicua] UI sprite not found: {path}");
                return null;
            }
            var border = new Vector4(24, 24, 24, 24);
            if (imp.textureType != TextureImporterType.Sprite || imp.spriteBorder != border)
            {
                imp.textureType      = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spriteBorder     = border;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Rounded-corner button with an optional left icon and centred label.</summary>
        static Button RoundedBtn(Transform parent, string name, string label, float fontSize,
            Vector2 anchor, Vector2 size, Color bgColor, Color textColor, string iconPath)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            var rounded = LoadSpriteSliced(RoundedRectUI);
            if (rounded != null)
            {
                img.sprite = rounded;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 0.8f;
            }
            img.color = bgColor;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            SetRect(RT(go), anchor, anchor, V2(0,0), size);

            if (!string.IsNullOrEmpty(iconPath))
            {
                var iconSprite = LoadSprite(iconPath);
                if (iconSprite != null)
                {
                    var iGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iGO.transform.SetParent(go.transform, false);
                    var iImg = iGO.GetComponent<Image>();
                    iImg.sprite = iconSprite;
                    iImg.preserveAspect = true;
                    iImg.raycastTarget = false;
                    SetRect(RT(iGO), V2(0,0.5f), V2(0,0.5f), V2(170,0), V2(52,52));
                }
            }

            var lGO = new GameObject("Label", typeof(RectTransform));
            lGO.transform.SetParent(go.transform, false);
            var tmp = lGO.AddComponent<TextMeshProUGUI>();
            tmp.text          = label;
            tmp.fontSize      = fontSize;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.color         = textColor;
            tmp.raycastTarget = false;
            tmp.fontStyle     = FontStyles.Bold;
            SetRect(RT(lGO), V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            return btn;
        }

        /// <summary>Full-screen art that covers the parent (crops overflow, no stretch).</summary>
        static GameObject FullScreenArt(Transform parent, string name, string assetPath, float aspect)
        {
            var sprite = LoadSprite(assetPath);
            if (sprite == null) return null;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, V2(0.5f,0.5f), V2(0.5f,0.5f), V2(0,0), V2(0,0));
            go.GetComponent<Image>().sprite = sprite;
            var fit = go.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = aspect;
            return go;
        }

        static void Save(UnityEngine.SceneManagement.Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        // ── GameObject shortcuts ─────────────────────────────────────────────

        static GameObject Obj(string name) => new GameObject(name);

        static void MakeCam(Color bg)
        {
            var g = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = g.AddComponent<Camera>();
            g.AddComponent<AudioListener>();
            c.clearFlags     = CameraClearFlags.SolidColor;
            c.backgroundColor = bg;
        }

        static void MakeEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        static GameObject MakeCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var cs = go.GetComponent<CanvasScaler>();
            cs.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution  = new Vector2(1080, 1920);
            cs.matchWidthOrHeight   = 0.5f;
            return go;
        }

        // ── UI element factories ─────────────────────────────────────────────

        static GameObject Panel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            SetRect(RT(go), anchorMin, anchorMax, anchoredPos, sizeDelta);
            return go;
        }

        static Button Btn(Transform parent, string name, string label, float fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            SetRect(RT(go), anchorMin, anchorMax, anchoredPos, sizeDelta);

            // Label
            var lGO = new GameObject("Label", typeof(RectTransform));
            lGO.transform.SetParent(go.transform, false);
            var tmp = lGO.AddComponent<TextMeshProUGUI>();
            tmp.text              = label;
            tmp.fontSize          = fontSize;
            tmp.alignment         = TextAlignmentOptions.Center;
            tmp.color             = TextWh;
            tmp.raycastTarget     = false;
            tmp.fontStyle         = FontStyles.Bold;
            SetRect(RT(lGO), V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            return btn;
        }

        static TextMeshProUGUI MakeTMP(Transform parent, string name, string text, float size,
            TextAlignmentOptions align,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text          = text;
            tmp.fontSize      = size;
            tmp.alignment     = align;
            tmp.color         = TextWh;
            tmp.enableWordWrapping = false;
            tmp.overflowMode  = TextOverflowModes.Overflow;
            SetRect(RT(go), anchorMin, anchorMax, anchoredPos, sizeDelta);
            return tmp;
        }

        static TMP_InputField MakeInputField(Transform parent, string name, string placeholder,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.17f, 0.17f, 0.22f);
            SetRect(RT(go), anchorMin, anchorMax, anchoredPos, sizeDelta);

            // Placeholder
            var phGO = new GameObject("Placeholder", typeof(RectTransform));
            phGO.transform.SetParent(go.transform, false);
            var phTmp = phGO.AddComponent<TextMeshProUGUI>();
            phTmp.text      = placeholder;
            phTmp.fontSize  = 28;
            phTmp.color     = TextMuted;
            phTmp.fontStyle = FontStyles.Italic;
            SetRect(RT(phGO), V2(0,0), V2(1,1), V2(8,0), V2(-16,0));

            // Text
            var txtGO = new GameObject("Text", typeof(RectTransform));
            txtGO.transform.SetParent(go.transform, false);
            var txtTmp = txtGO.AddComponent<TextMeshProUGUI>();
            txtTmp.text    = "";
            txtTmp.fontSize = 28;
            txtTmp.color   = TextWh;
            SetRect(RT(txtGO), V2(0,0), V2(1,1), V2(8,0), V2(-16,0));

            var field = go.AddComponent<TMP_InputField>();
            field.textViewport  = RT(txtGO);
            field.textComponent = txtTmp;
            field.placeholder   = phTmp;
            return field;
        }

        static Slider MakeSlider(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect(RT(go), anchorMin, anchorMax, anchoredPos, sizeDelta);

            var bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            bg.GetComponent<Image>().color = BtnDark;
            SetRect(RT(bg), V2(0,0.25f), V2(1,0.75f), V2(0,0), V2(0,0));

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            SetRect(RT(fillArea), V2(0,0.25f), V2(1,0.75f), V2(-5,0), V2(-10,0));
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            fill.GetComponent<Image>().color = Gold;
            SetRect(RT(fill), V2(0,0), V2(0,1), V2(0,0), V2(10,0));

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            SetRect(RT(handleArea), V2(0,0), V2(1,1), V2(10,0), V2(-20,0));
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<Image>().color = Color.white;
            SetRect(RT(handle), V2(0,0), V2(0,1), V2(0,0), V2(24,0));

            var slider = go.AddComponent<Slider>();
            slider.fillRect      = RT(fill);
            slider.handleRect    = RT(handle);
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction     = Slider.Direction.LeftToRight;
            slider.minValue      = 0f;
            slider.maxValue      = 1f;
            slider.value         = 1f;
            return slider;
        }

        static void SetRect(RectTransform rt,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = sizeDelta;
        }

        // Auto-adds RectTransform if the GO was created without one (non-UI objects).
        static RectTransform RT(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            return rt != null ? rt : go.AddComponent<RectTransform>();
        }

        static Vector2 V2(float x, float y) => new Vector2(x, y);
    }
}
