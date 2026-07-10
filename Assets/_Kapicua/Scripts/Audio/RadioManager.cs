using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if FIREBASE_AVAILABLE
using Firebase.Storage;
using Firebase.Extensions;
#endif

namespace Kapicua.Audio
{
    /// <summary>
    /// Kapicua Radio — synchronized music player for all 4 players.
    ///
    /// Rules (by design):
    ///   ✅ Play (auto-starts with match)
    ///   ✅ Volume control per player (local only)
    ///   ✅ Mute toggle per player (local only)
    ///   ❌ No skipping
    ///   ❌ No song selection
    ///   ❌ No replaying
    ///   ❌ No seeking
    ///
    /// Synchronization:
    ///   The host picks a shuffled playlist order and the match start UTC timestamp.
    ///   All clients derive the current song and playback position from:
    ///     currentSongIndex = (floor(elapsedSeconds / songDuration))
    ///     playbackPosition = elapsedSeconds % songDuration
    ///   This ensures all players hear the same moment of the same song.
    ///
    /// Songs are stored in Firebase Storage under gs://[bucket]/radio/
    /// </summary>
    public class RadioManager : MonoBehaviour
    {
        public static RadioManager Instance { get; private set; }

        [Header("UI")]
        public TMP_Text NowPlayingText;
        public TMP_Text ArtistText;
        public Slider VolumeSlider;
        public Button MuteButton;
        public Image MuteIcon;
        public Sprite MuteOnSprite;
        public Sprite MuteOffSprite;
        public GameObject LoadingIndicator;

        [Header("Audio")]
        public AudioSource MusicSource;

        [Header("Firebase")]
        public string StorageBucket = "kapicua-app.appspot.com";
        [Tooltip("Subfolder in Firebase Storage AND StreamingAssets. Must match CopyMusicToProject destination.")]
        public string RadioFolder = "Radio";

        // Playlist metadata (loaded from Firebase Storage)
        private List<RadioTrack> _playlist = new List<RadioTrack>();
        private int _currentTrackIndex = -1;

        // Sync parameters (set by host, received by all clients)
        private long _syncStartTimestampMs;   // UTC ms when playlist started
        private int[] _shuffleOrder;          // host-determined shuffle order

        private bool _isMuted;
        private float _volume = 0.8f;
        private bool _isLoaded;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (VolumeSlider != null)
            {
                VolumeSlider.value = _volume;
                VolumeSlider.onValueChanged.AddListener(SetVolume);
            }
            MuteButton?.onClick.AddListener(ToggleMute);
            if (LoadingIndicator != null) LoadingIndicator.SetActive(false);
        }

        // ─── SYNC ENTRY POINTS ────────────────────────────────────────────────

        /// <summary>
        /// Called on HOST: loads playlist, shuffles, broadcasts sync data.
        /// </summary>
        public async Task InitializeAsHostAsync()
        {
            await LoadPlaylistMetadataAsync();
            _shuffleOrder = GenerateShuffleOrder(_playlist.Count);
            _syncStartTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // Host broadcasts _syncStartTimestampMs and _shuffleOrder via NetworkGameManager
            StartSyncedPlayback(_syncStartTimestampMs, _shuffleOrder);
        }

        /// <summary>
        /// Called on CLIENT: receives sync data from host and starts at the correct position.
        /// </summary>
        public async Task InitializeAsClientAsync(long syncStartMs, int[] shuffleOrder)
        {
            await LoadPlaylistMetadataAsync();
            _shuffleOrder = shuffleOrder;
            StartSyncedPlayback(syncStartMs, shuffleOrder);
        }

        void StartSyncedPlayback(long syncStartMs, int[] shuffleOrder)
        {
            _syncStartTimestampMs = syncStartMs;
            _shuffleOrder = shuffleOrder;
            StartCoroutine(SyncedPlaybackLoop());
        }

        IEnumerator SyncedPlaybackLoop()
        {
            while (true)
            {
                // Calculate where we should be right now
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long elapsedMs = nowMs - _syncStartTimestampMs;
                float elapsedSec = elapsedMs / 1000f;

                // Find current track (assumes all tracks are same length for simplicity;
                // in production, track durations are in metadata)
                int trackIndex = GetCurrentTrackIndex(elapsedSec);
                float positionInTrack = GetPositionInTrack(elapsedSec);

                if (trackIndex != _currentTrackIndex)
                {
                    _currentTrackIndex = trackIndex;
                    yield return StartCoroutine(LoadAndPlayTrack(trackIndex, positionInTrack));
                }
                else if (MusicSource != null && Mathf.Abs(MusicSource.time - positionInTrack) > 1f)
                {
                    // Drift correction
                    MusicSource.time = positionInTrack;
                }

                yield return new WaitForSeconds(5f); // check sync every 5 seconds
            }
        }

        int GetCurrentTrackIndex(float elapsedSeconds)
        {
            if (_playlist.Count == 0) return 0;
            float cumulative = 0;
            for (int i = 0; i < _shuffleOrder.Length; i++)
            {
                float duration = _playlist[_shuffleOrder[i]].DurationSeconds;
                if (elapsedSeconds < cumulative + duration)
                    return i;
                cumulative += duration;
            }
            // Loop back through playlist
            return (int)(elapsedSeconds / AverageDuration()) % _shuffleOrder.Length;
        }

        float GetPositionInTrack(float elapsedSeconds)
        {
            if (_playlist.Count == 0) return 0;
            float cumulative = 0;
            for (int i = 0; i < _shuffleOrder.Length; i++)
            {
                float duration = _playlist[_shuffleOrder[i]].DurationSeconds;
                if (elapsedSeconds < cumulative + duration)
                    return elapsedSeconds - cumulative;
                cumulative += duration;
            }
            return 0;
        }

        float AverageDuration()
        {
            if (_playlist.Count == 0) return 180f;
            float total = 0;
            foreach (var t in _playlist) total += t.DurationSeconds;
            return total / _playlist.Count;
        }

        IEnumerator LoadAndPlayTrack(int shuffleIndex, float startPosition)
        {
            if (_playlist.Count == 0) yield break;
            int trackIndex = _shuffleOrder[shuffleIndex % _shuffleOrder.Length];
            var track = _playlist[trackIndex];

            if (LoadingIndicator != null) LoadingIndicator.SetActive(true);
            if (NowPlayingText != null) NowPlayingText.text = track.Title;
            if (ArtistText != null) ArtistText.text = track.Artist;

            AudioClip clip = null;
            bool done = false;

#if FIREBASE_AVAILABLE
            // Download from Firebase Storage
            var storage = FirebaseStorage.DefaultInstance;
            var storageRef = storage.GetReferenceFromUrl($"gs://{StorageBucket}/{RadioFolder}/{track.FileName}");
            string error = null;

            storageRef.GetDownloadUrlAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    StartCoroutine(DownloadAudioClip(task.Result.ToString(), startPosition, c =>
                    {
                        clip = c;
                        done = true;
                    }));
                }
                else
                {
                    error = task.Exception?.Message;
                    done = true;
                }
            });
#else
            // Load from StreamingAssets/Radio/ (set via Kapicua ▸ Copy Music to StreamingAssets)
            string localPath = System.IO.Path.Combine(
                Application.streamingAssetsPath, RadioFolder, track.FileName);

            if (System.IO.File.Exists(localPath))
            {
                // file:// prefix works on both Editor, macOS, and iOS
                string url = "file://" + localPath.Replace("\\", "/");
                StartCoroutine(DownloadAudioClip(url, startPosition, c =>
                {
                    clip = c;
                    done = true;
                }));
            }
            else
            {
                Debug.LogWarning($"[Radio] Track not found in StreamingAssets: {track.FileName}\n" +
                                 $"Run: Kapicua ▸ Copy Music to StreamingAssets");
                done = true;
            }
#endif

            yield return new WaitUntil(() => done);

            if (LoadingIndicator != null) LoadingIndicator.SetActive(false);

            if (clip != null && MusicSource != null)
            {
                MusicSource.clip = clip;
                MusicSource.time = Mathf.Min(startPosition, clip.length - 0.1f);
                MusicSource.volume = _isMuted ? 0 : _volume;
                MusicSource.Play();
            }
        }

        IEnumerator DownloadAudioClip(string url, float startPos, Action<AudioClip> callback)
        {
            using var req = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(req);
                callback(clip);
            }
            else
            {
                Debug.LogWarning($"[Radio] Failed to load track: {req.error}");
                callback(null);
            }
        }

        // ─── PLAYLIST METADATA ────────────────────────────────────────────────

        async Task LoadPlaylistMetadataAsync()
        {
#if FIREBASE_AVAILABLE
            // TODO: fetch gs://[bucket]/[RadioFolder]/manifest.json and deserialise into _playlist
            _playlist = new List<RadioTrack>
            {
                new RadioTrack { FileName = "placeholder.mp3", Title = "Loading…", Artist = "Kapicua Radio", DurationSeconds = 180 }
            };
            await Task.CompletedTask;
#else
            // ── StreamingAssets local scan ────────────────────────────────
            // Primary: read the manifest.txt written by CopyMusicToProject
            // Fallback: Directory.GetFiles() (Editor & macOS; not available on iOS at runtime)
            _playlist = new List<RadioTrack>();

            string radioDir      = System.IO.Path.Combine(Application.streamingAssetsPath, RadioFolder);
            string manifestPath  = System.IO.Path.Combine(radioDir, "manifest.txt");

            List<string> fileNames = new List<string>();

            if (System.IO.File.Exists(manifestPath))
            {
                // Read manifest line-by-line
                foreach (var line in System.IO.File.ReadAllLines(manifestPath))
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && trimmed != "manifest.txt")
                        fileNames.Add(trimmed);
                }
                Debug.Log($"[Radio] Manifest found: {fileNames.Count} tracks.");
            }
            else if (System.IO.Directory.Exists(radioDir))
            {
                // Fallback for Editor / macOS builds
                foreach (var ext in new[] { "*.mp3", "*.wav", "*.ogg" })
                    foreach (var f in System.IO.Directory.GetFiles(radioDir, ext))
                        fileNames.Add(System.IO.Path.GetFileName(f));
                Debug.Log($"[Radio] Directory scan: {fileNames.Count} tracks.");
            }
            else
            {
                Debug.LogWarning(
                    $"[Radio] StreamingAssets/Radio/ not found.\n" +
                    $"Run: Kapicua ▸ Copy Music to StreamingAssets");
            }

            foreach (var fileName in fileNames)
            {
                string title = System.IO.Path.GetFileNameWithoutExtension(fileName);
                _playlist.Add(new RadioTrack
                {
                    FileName        = fileName,
                    Title           = title,
                    Artist          = "Kapicua Radio",
                    DurationSeconds = 200f   // fallback estimate; accurate timing isn't needed for sync
                });
            }

            // Shuffle the playlist on first load if no host sync data has arrived yet
            if (_playlist.Count > 1 && _shuffleOrder == null)
                _shuffleOrder = GenerateShuffleOrder(_playlist.Count);

            await Task.CompletedTask;
#endif
        }

        int[] GenerateShuffleOrder(int count)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            var rng = new System.Random((int)DateTime.UtcNow.Ticks);
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        // ─── VOLUME / MUTE (local only) ──────────────────────────────────────

        public void SetVolume(float value)
        {
            _volume = value;
            if (!_isMuted && MusicSource != null)
                MusicSource.volume = value;
        }

        public void ToggleMute()
        {
            _isMuted = !_isMuted;
            if (MusicSource != null)
                MusicSource.volume = _isMuted ? 0 : _volume;
            if (MuteIcon != null)
                MuteIcon.sprite = _isMuted ? MuteOnSprite : MuteOffSprite;
        }
    }

    [Serializable]
    public class RadioTrack
    {
        public string FileName;
        public string Title;
        public string Artist;
        public float DurationSeconds;
    }
}
