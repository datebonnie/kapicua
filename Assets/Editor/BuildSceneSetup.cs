using UnityEngine;
using UnityEditor;

/// <summary>
/// One-time utility to register all Kapicua scenes in the build settings.
/// Run via: Kapicua → Setup Build Scenes
/// </summary>
public static class BuildSceneSetup
{
    [MenuItem("Kapicua/Setup Build Scenes")]
    public static void SetupBuildScenes()
    {
        var scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/_Kapicua/Scenes/00_Boot.unity",   true),
            new EditorBuildSettingsScene("Assets/Scenes/01_Login.unity",           true),
            new EditorBuildSettingsScene("Assets/Scenes/02_MainMenu.unity",        true),
            new EditorBuildSettingsScene("Assets/Scenes/03_Game.unity",            true),
        };

        EditorBuildSettings.scenes = scenes;
        Debug.Log("[Kapicua] Build scenes configured: " + scenes.Length + " scenes registered.");
    }
}
