using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kapicua.EditorTools
{
    /// <summary>
    /// Copies mastered MP3/WAV/OGG files from the MASTERED desktop folder
    /// into Assets/StreamingAssets/Radio/ so RadioManager can play them locally
    /// without requiring Firebase Storage.
    ///
    /// Run once via:  Kapicua ▸ Copy Music to StreamingAssets
    ///
    /// Safe to re-run — existing files are overwritten, new files are added.
    /// After copying, Unity will reimport; then run  Kapicua ▸ Build All Scenes.
    /// </summary>
    public static class CopyMusicToProject
    {
        // Source folder on Brandon's machine
        const string SourcePath = @"C:\Users\emipe\OneDrive\Desktop\MASTERED";

        // Destination inside the Unity project (becomes StreamingAssets at runtime)
        const string DestRelative = "StreamingAssets/Radio";

        static readonly string[] AudioExtensions = { "*.mp3", "*.wav", "*.ogg", "*.aac", "*.m4a" };

        [MenuItem("Kapicua/Copy Music to StreamingAssets")]
        public static void CopyMusic()
        {
            if (!Directory.Exists(SourcePath))
            {
                Debug.LogError(
                    $"[Radio] MASTERED folder not found at:\n{SourcePath}\n" +
                    $"Check that OneDrive is synced and the path is correct.");
                return;
            }

            string destDir = Path.Combine(Application.dataPath, DestRelative);
            Directory.CreateDirectory(destDir);

            // Collect all audio files (top-level only — don't recurse subdirs)
            var files = new List<string>();
            foreach (var ext in AudioExtensions)
                files.AddRange(Directory.GetFiles(SourcePath, ext, SearchOption.TopDirectoryOnly));

            if (files.Count == 0)
            {
                Debug.LogWarning(
                    $"[Radio] No audio files found in:\n{SourcePath}\n" +
                    $"Supported formats: {string.Join(", ", AudioExtensions)}");
                return;
            }

            int copied = 0;
            foreach (var src in files)
            {
                string fileName = Path.GetFileName(src);
                string dest     = Path.Combine(destDir, fileName);
                File.Copy(src, dest, overwrite: true);
                Debug.Log($"[Radio] ✓  {fileName}");
                copied++;
            }

            // Write a plain-text manifest so RadioManager can list tracks without
            // Directory.GetFiles() (required for iOS where streaming assets are bundled).
            WriteManifest(destDir, files);

            AssetDatabase.Refresh();
            Debug.Log($"[Radio] ✅  Copied {copied} tracks → Assets/{DestRelative}/");
        }

        static void WriteManifest(string destDir, List<string> files)
        {
            // Simple line-delimited file: one filename per line.
            // RadioManager reads this on iOS instead of Directory.GetFiles().
            var lines = new List<string>();
            foreach (var f in files)
                lines.Add(Path.GetFileName(f));

            string manifestPath = Path.Combine(destDir, "manifest.txt");
            File.WriteAllLines(manifestPath, lines);
            Debug.Log($"[Radio] Manifest written: {lines.Count} tracks listed.");
        }
    }
}
