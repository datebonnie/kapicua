using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
#if FIREBASE_AVAILABLE
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
#endif
#if FACEBOOK_SDK
using Facebook.Unity;
#endif

namespace Kapicua.UI
{
    /// <summary>
    /// Handles authentication on the login screen.
    /// Login screen matches the Kapicua! splash design with four options:
    ///   - Continue with Apple   (Sign in with Apple)
    ///   - Continue with Facebook
    ///   - Continue with Google
    ///   - Continue with Email
    ///
    /// On success, saves user profile to PlayerPrefs and loads MainMenu.
    /// </summary>
    public class LoginManager : MonoBehaviour
    {
        [Header("Buttons")]
        public Button AppleButton;
        public Button FacebookButton;
        public Button GoogleButton;
        public Button EmailButton;
        public Button SignUpButton;
        /// <summary>Optional "Sign up" link at the bottom of the screen — opens the email panel.</summary>
        public Button SignUpLinkButton;

        [Header("Email Panel")]
        public GameObject EmailPanel;
        public TMP_InputField EmailInput;
        public TMP_InputField PasswordInput;
        public Button EmailSubmitButton;
        public Button EmailBackButton;
        public TMP_Text EmailErrorText;

        [Header("Loading")]
        public GameObject LoadingOverlay;
        public TMP_Text StatusText;

#if FIREBASE_AVAILABLE
        private FirebaseAuth _auth;
#endif
        private bool _firebaseReady;

        void Start()
        {
            if (EmailPanel != null) EmailPanel.SetActive(false);
            if (LoadingOverlay != null) LoadingOverlay.SetActive(false);

#if FIREBASE_AVAILABLE
            InitializeFirebase();
#else
            _firebaseReady = true; // dev mode — skip auth
#endif

            AppleButton?.onClick.AddListener(SignInWithApple);
            FacebookButton?.onClick.AddListener(SignInWithFacebook);
            GoogleButton?.onClick.AddListener(SignInWithGoogle);
            EmailButton?.onClick.AddListener(ShowEmailPanel);
            EmailSubmitButton?.onClick.AddListener(SignInWithEmail);
            EmailBackButton?.onClick.AddListener(() => EmailPanel?.SetActive(false));
            SignUpButton?.onClick.AddListener(SignUp);
            SignUpLinkButton?.onClick.AddListener(ShowEmailPanel);
        }

#if FIREBASE_AVAILABLE
        void InitializeFirebase()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result == DependencyStatus.Available)
                {
                    _auth = FirebaseAuth.DefaultInstance;
                    _firebaseReady = true;
                    Debug.Log("[Auth] Firebase initialized.");

                    // Auto-login if user has a saved session
                    if (_auth.CurrentUser != null)
                    {
                        Debug.Log("[Auth] Auto-login: " + _auth.CurrentUser.DisplayName);
                        OnLoginSuccess(_auth.CurrentUser);
                    }
                }
                else
                {
                    SetStatus($"Firebase error: {task.Result}");
                }
            });
        }
#endif

        // ─── SIGN IN METHODS ─────────────────────────────────────────────────

#if FIREBASE_AVAILABLE
        async void SignInWithApple()
        {
            if (!_firebaseReady) return;
            ShowLoading("Connecting...");
            try
            {
                string appleIdToken = await GetAppleIdTokenAsync();
                var credential = OAuthProvider.GetCredential("apple.com", appleIdToken, null, null);
                var user = await _auth.SignInWithCredentialAsync(credential);
                OnLoginSuccess(user);
            }
            catch (Exception e)
            {
                HideLoading();
                SetStatus("Apple sign-in failed. Try another option.");
                Debug.LogWarning($"[Auth] Apple: {e.Message}");
            }
        }

        async void SignInWithGoogle()
        {
            if (!_firebaseReady) return;
            ShowLoading("Connecting...");
            try
            {
                string idToken = await GetGoogleIdTokenAsync();
                var credential = GoogleAuthProvider.GetCredential(idToken, null);
                var user = await _auth.SignInWithCredentialAsync(credential);
                OnLoginSuccess(user);
            }
            catch (Exception e)
            {
                HideLoading();
                SetStatus("Google sign-in failed. Try another option.");
                Debug.LogWarning($"[Auth] Google: {e.Message}");
            }
        }

        async void SignInWithFacebook()
        {
            if (!_firebaseReady) return;
            ShowLoading("Connecting...");
            try
            {
                string accessToken = await GetFacebookAccessTokenAsync();
                var credential = FacebookAuthProvider.GetCredential(accessToken);
                var user = await _auth.SignInWithCredentialAsync(credential);
                OnLoginSuccess(user);
            }
            catch (Exception e)
            {
                HideLoading();
                SetStatus("Facebook sign-in failed. Try another option.");
                Debug.LogWarning($"[Auth] Facebook: {e.Message}");
            }
        }

        void ShowEmailPanel() => EmailPanel?.SetActive(true);

        async void SignInWithEmail()
        {
            if (!_firebaseReady) return;
            string email = EmailInput?.text?.Trim() ?? "";
            string password = PasswordInput?.text ?? "";
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                if (EmailErrorText != null) EmailErrorText.text = "Enter email and password.";
                return;
            }
            ShowLoading("Signing in...");
            try
            {
                var result = await _auth.SignInWithEmailAndPasswordAsync(email, password);
                OnLoginSuccess(result.User);
            }
            catch (Exception e)
            {
                HideLoading();
                if (EmailErrorText != null) EmailErrorText.text = "Invalid email or password.";
                Debug.LogWarning($"[Auth] Email: {e.Message}");
            }
        }

        async void SignUp()
        {
            if (!_firebaseReady) return;
            string email = EmailInput?.text?.Trim() ?? "";
            string password = PasswordInput?.text ?? "";
            if (string.IsNullOrEmpty(email) || password.Length < 6)
            {
                if (EmailErrorText != null) EmailErrorText.text = "Email required. Password must be 6+ chars.";
                return;
            }
            ShowLoading("Creating account...");
            try
            {
                var result = await _auth.CreateUserWithEmailAndPasswordAsync(email, password);
                OnLoginSuccess(result.User);
            }
            catch (Exception e)
            {
                HideLoading();
                if (EmailErrorText != null) EmailErrorText.text = "Account creation failed.";
                Debug.LogWarning($"[Auth] SignUp: {e.Message}");
            }
        }

        // ─── SUCCESS ─────────────────────────────────────────────────────────

        void OnLoginSuccess(FirebaseUser user)
        {
            HideLoading();
            PlayerPrefs.SetString("UserId", user.UserId);
            PlayerPrefs.SetString("DisplayName", user.DisplayName ?? user.Email ?? "Player");
            PlayerPrefs.Save();
            Debug.Log($"[Auth] Logged in as: {user.DisplayName ?? user.Email}");
            SceneManager.LoadScene("02_MainMenu");
        }
#else
        // ─── DEV MODE (no Firebase SDK) ──────────────────────────────────────
        // Buttons bypass auth and go straight to main menu for local testing.

        void SignInWithApple()    => OnLoginSuccess();
        void SignInWithGoogle()   => OnLoginSuccess();
        void SignInWithFacebook() => OnLoginSuccess();
        void ShowEmailPanel()     => EmailPanel?.SetActive(true);

        void SignInWithEmail()
        {
            string email = EmailInput?.text?.Trim() ?? "dev@kapicua.com";
            OnLoginSuccess(email);
        }

        void SignUp() => SignInWithEmail();

        void OnLoginSuccess(string displayName = "Jugador")
        {
            HideLoading();
            PlayerPrefs.SetString("UserId", "dev_user_" + SystemInfo.deviceUniqueIdentifier);
            PlayerPrefs.SetString("DisplayName", displayName);
            PlayerPrefs.Save();
            Debug.Log($"[Auth] Dev login as: {displayName}");
            SceneManager.LoadScene("02_MainMenu");
        }
#endif

        // ─── PLATFORM TOKEN HELPERS ──────────────────────────────────────────
        // These are stubs — each needs the respective native SDK integrated.

        Task<string> GetAppleIdTokenAsync()
        {
            // TODO: Use Apple Sign In plugin (e.g., com.lupidan.apple-signin-unity)
            return Task.FromResult<string>(null);
        }

        Task<string> GetGoogleIdTokenAsync()
        {
            // TODO: Use Google Sign-In plugin
            return Task.FromResult<string>(null);
        }

        Task<string> GetFacebookAccessTokenAsync()
        {
#if FACEBOOK_SDK
            var tcs = new TaskCompletionSource<string>();

            void DoLogin()
            {
                FB.LogInWithReadPermissions(new[] { "public_profile", "email" }, result =>
                {
                    if (result == null || result.Cancelled || !FB.IsLoggedIn)
                        tcs.TrySetException(new Exception(
                            string.IsNullOrEmpty(result?.Error) ? "Facebook login cancelled." : result.Error));
                    else
                        tcs.TrySetResult(AccessToken.CurrentAccessToken.TokenString);
                });
       