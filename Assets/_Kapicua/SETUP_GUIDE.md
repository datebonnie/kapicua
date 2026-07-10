# Kapicua! — SDK Setup Guide

Complete these steps in Unity before hitting Play.

---

## 1. Unity Gaming Services (UGS)

**In Unity Editor:**
1. Edit → Project Settings → Services
2. Link to your UGS project (dashboard.unity3d.com)
3. Copy your Project ID — paste it in Project Settings → Services

**Install packages** via Window → Package Manager → Add by name:
```
com.unity.services.authentication
com.unity.services.lobby
com.unity.services.relay
com.unity.netcode.gameobjects
com.unity.transport
```

---

## 2. Firebase SDK

1. Download **Firebase Unity SDK** from firebase.google.com/docs/unity/setup
2. Import these `.unitypackage` files:
   - `FirebaseAuth.unitypackage`
   - `FirebaseStorage.unitypackage`
   - `FirebaseFirestore.unitypackage` (optional, for profiles)
3. Drag `GoogleService-Info.plist` (from your Firebase console) into `Assets/`
4. In Firebase console → Authentication → Sign-in methods, enable:
   - Apple
   - Google
   - Facebook
   - Email/Password

**Apple Sign In (iOS only):**
- Xcode → Signing & Capabilities → Add "Sign in with Apple"
- In Firebase console → Apple provider → enter your Service ID
- Install: `com.lupidan.apple-signin-unity` via Package Manager (UPM git URL)

**Google Sign In:**
- Download `google-services.json` AND `GoogleService-Info.plist` from Firebase
- Install Google Sign-In plugin: https://github.com/googlesamples/google-signin-unity

**Facebook Login:**
- Create app at developers.facebook.com
- Install Facebook SDK for Unity: https://developers.facebook.com/docs/unity/

---

## 3. TextMeshPro

Window → Package Manager → TextMeshPro → Install
Then: Window → TextMeshPro → Import TMP Essential Resources

---

## 4. Scene Setup

Create these scenes in `Assets/_Kapicua/Scenes/`:

| Scene | Contains |
|-------|----------|
| `00_Boot` | SplashScreenManager, More Games logo canvas, Kapicua boot canvas |
| `01_Login` | LoginManager, Firebase initialization |
| `02_MainMenu` | MainMenuManager, tabs (PrivateRoom/Score/Radio) |
| `03_Game` | NetworkGameManager, MatchManager, RoundManager, TurnManager, ScoreManager, GameUIManager, BoardRenderer |

Add all scenes to File → Build Settings → Scenes In Build (in order).

---

## 5. NetworkManager Setup (Scene 03_Game)

1. Create empty GameObject → Add Component → NetworkManager
2. Add component → UnityTransport
3. In NetworkManager inspector:
   - Network Transport = UnityTransport
   - Enable Scene Management = ✅
4. NetworkGameManager must be on a NetworkObject (Add Component → NetworkObject)

---

## 6. Kapicua Radio — Firebase Storage

1. Firebase console → Storage → Create bucket
2. Upload your music files to `radio/` folder: `gs://[bucket]/radio/track01.mp3`
3. Create `radio/manifest.json`:
```json
{
  "tracks": [
    { "fileName": "track01.mp3", "title": "Song Name", "artist": "Artist", "durationSeconds": 215 },
    { "fileName": "track02.mp3", "title": "Song Name", "artist": "Artist", "durationSeconds": 198 }
  ]
}
```
4. Set `StorageBucket` in RadioManager inspector = your bucket name

---

## 7. iOS Build Settings

Edit → Project Settings → Player → iOS tab:
- Bundle Identifier: `com.kapicua.app` (or your choice)
- Signing Team ID: your Apple Developer team ID
- Capabilities: Sign in with Apple ✅, Game Center (optional)
- Target minimum iOS version: 15.0

---

## Script Execution Order (Edit → Project Settings → Script Execution Order)

Add these in order (lower = earlier):
1. `LobbyManager` (-200)
2. `RelayManager` (-150)
3. `NetworkGameManager` (-100)
4. `MatchManager` (-50)
5. `RoundManager` (-40)
6. `TurnManager` (-30)
7. `ScoreManager` (-20)
