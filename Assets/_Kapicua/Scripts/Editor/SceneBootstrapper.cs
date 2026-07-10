using System.IO;
using Kapicua.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kapicua.EditorTools
{
    /// <summary>
    /// Populates the existing 02_Game scene from code:
    /// table, lighting, camera, hand anchors, game manager, and HUD.
    /// Run via menu: Kapicua > Build Game Scene. Safe to re-run (clears and rebuilds the scene).
    /// </summary>
    public static class SceneBootstrapper
    {
        const string ScenePath = "Assets/Scenes/03_Game.unity";
        const string BootScenePath = "Assets/_Kapicua/Scenes/00_Boot.unity";
        const string LoginScenePath = "Assets/Scenes/01_Login.unity";
        const string MainMenuScenePath = "Assets/Scenes/02_MainMenu.unity";
        const string MaterialsDir = "Assets/_Kapicua/Materials";

        [MenuItem("Kapicua/Build Game Scene")]
        public static void Build()
        {
            // Open the existing game scene and wipe its contents so it keeps its GUID
            // (02_MainMenu starts it) while we repopulate it with single-player AI mode.
            // For the multiplayer build, use: Kapicua > Build All Scenes instead.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                Object.DestroyImmediate(root);

            BuildTable();
            BuildLighting();
            BuildCamera();

            var board = new GameObject("BoardRoot").AddComponent<BoardLayoutView>();

            var humanHand = CreateAnchor("HumanHandAnchor", new Vector3(0, 0.9f, -6.9f), new Vector3(-50, 0, 0))
                .gameObject.AddComponent<HandView>();
            var rightHand = CreateAnchor("RightHandAnchor", new Vector3(8.6f, 0.15f, 0), new Vector3(0, 90, 0))
                .gameObject.AddComponent<OpponentHandView>();
            var topHand = CreateAnchor("TopHandAnchor", new Vector3(0, 0.15f, 5.6f), Vector3.zero)
                .gameObject.AddComponent<OpponentHandView>();
            var leftHand = CreateAnchor("LeftHandAnchor", new Vector3(-8.6f, 0.15f, 0), new Vector3(0, 90, 0))
                .gameObject.AddComponent<OpponentHandView>();

            var hud = BuildHud();

            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            gm.Board = board;
            gm.HumanHand = humanHand;
            gm.RightHand = rightHand;
            gm.TopHand = topHand;
            gm.LeftHand = leftHand;
            gm.Hud = hud;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Sync build settings: 00_Boot → 01_Login → 02_MainMenu → 03_Game.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(LoginScenePath, true),
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            Debug.Log($"[Kapicua] Single-player game scene built: {ScenePath}");
        }

        static void BuildTable()
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.position = new Vector3(0, -0.25f, 0);
            table.transform.localScale = new Vector3(22f, 0.5f, 13.5f);

            Directory.CreateDirectory(MaterialsDir);
            var felt = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/TableFelt.mat");
            if (felt == null)
            {
                felt = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    color = new Color(0.05f, 0.35f, 0.18f),
                };
                felt.SetFloat("_Smoothness", 0.15f);
                AssetDatabase.CreateAsset(felt, $"{MaterialsDir}/TableFelt.mat");
            }
            table.GetComponent<MeshRenderer>().sharedMaterial = felt;
        }

        static void BuildLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55, -35, 0);
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.47f);
        }

        static void BuildCamera()
        {
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            camGO.transform.position = new Vector3(0, 14f, -8.5f);
            camGO.transform.rotation = Quaternion.Euler(58, 0, 0);
            cam.fieldOfView = 50;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.11f);
        }

        static Transform CreateAnchor(string name, Vector3 pos, Vector3 euler)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            return go.transform;
        }

        static GameHud BuildHud()
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(null);

            var hud = canvasGO.AddComponent<GameHud>();

            hud.ScoreText = CreateText(canvasGO.transform, "ScoreText", "Nosotros 0 — Ellos 0",
                34, TextAlignmentOptions.Left,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -18), new Vector2(700, 48));

            hud.StatusText = CreateText(canvasGO.transform, "StatusText", "",
                34, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(900, 48));

            hud.ToastText = CreateText(canvasGO.transform, "ToastText", "",
                42, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1100, 60));
            hud.ToastText.color = new Color(1f, 0.85f, 0.35f);
            hud.ToastRoot = hud.ToastText.gameObject;
            hud.ToastRoot.SetActive(false);

            hud.PassButton = CreateButton(canvasGO.transform, "PassButton", "PASO", 40,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 60), new Vector2(280, 96),
                new Color(0.8f, 0.35f, 0.15f));
            hud.PassRoot = hud.PassButton.gameObject;
            hud.PassRoot.SetActive(false);

            var choice = new GameObject("EndChoice", typeof(RectTransform));
            choice.transform.SetParent(canvasGO.transform, false);
            var choiceRt = (RectTransform)choice.transform;
            SetRect(choiceRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(700, 110));
            hud.LeftButton = CreateButton(choice.transform, "LeftButton", "◀ IZQUIERDA", 32,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-170, 0), new Vector2(320, 100),
                new Color(0.15f, 0.35f, 0.6f));
            hud.RightButton = CreateButton(choice.transform, "RightButton", "DERECHA ▶", 32,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(170, 0), new Vector2(320, 100),
                new Color(0.15f, 0.35f, 0.6f));
            hud.EndChoiceRoot = choice;
            choice.SetActive(false);

            var banner = new GameObject("Banner", typeof(RectTransform), typeof(Image));
            banner.transform.SetParent(canvasGO.transform, false);
            banner.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.88f);
            SetRect((RectTransform)banner.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 420));
            hud.BannerText = CreateText(banner.transform, "BannerText", "",
                52, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(950, 240));
            hud.BannerButton = CreateButton(banner.transform, "BannerButton", "Continuar", 34,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(380, 100),
                new Color(0.15f, 0.5f, 0.25f));
            hud.BannerButtonLabel = hud.BannerButton.GetComponentInChildren<TMP_Text>();
            hud.BannerRoot = banner;
            banner.SetActive(false);

            return hud;
        }

        static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
            TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            SetRect(rt, anchorMin, anchorMax, anchoredPos, sizeDelta);
            // Keep the pivot consistent with the anchor so offsets behave predictably.
            rt.pivot = anchorMin;
            return tmp;
        }

        static Button CreateButton(Transform parent, string name, string label, float fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var rt = (RectTransform)go.transform;
            SetRect(rt, anchorMin, anchorMax, anchoredPos, sizeDelta);
            rt.pivot = anchorMin;

            var text = CreateText(go.transform, "Label", label, fontSize, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, sizeDelta);
            text.color = Color.white;
            return button;
        }

        static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }
    }
}
