using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.UI;

using NetFlower.UI;

namespace NetFlower.Backend {
    public class Login : MonoBehaviour {
        [Tooltip("REST API base, no trailing slash. Production: https://litecoders.com/api\n" +
                 "IMPORTANT: The value on your scene/prefab overrides this script default. If you still see requests to localhost:8000, change this field on the Login object in the Inspector.")]
        [FormerlySerializedAs("authServerBaseUrl")]
        [SerializeField] string httpApiBaseUrl = "https://litecoders.com/api";
        [SerializeField] UserInput playerIdInput;
        [SerializeField] UserInput passwordInput;
        [SerializeField] GameObject loginMessage;
        [Tooltip("Player ID row only. Shown for Login; hidden for Register (password-only).")]
        [SerializeField] CanvasGroup idCanvasGroup;
        [SerializeField] CanvasGroup submitCanvasGroup;
        [SerializeField] CanvasGroup logoutCanvasGroup;
        [Tooltip("If false, id/submit stay hidden until OnChooseLogin / OnChooseRegister (separate choice screen).")]
        [SerializeField] bool showCredentialFormAtStartup = true;
        [Header("Auth screens")]
        [Tooltip("Login / register UI (hidden while session is active).")]
        [SerializeField] CanvasGroup loginCG;
        [Tooltip("Shown after successful login, register, or token restore.")]
        [SerializeField] CanvasGroup loggedInCG;
        [SerializeField] long messageClearDelaySeconds = 5;

        [Header("Login / Register tabs (side-by-side buttons)")]
        [Tooltip("Assign the Login tab Button (or its root) on the prefab.")]
        [SerializeField] GameObject loginTabObject;
        [Tooltip("Assign the Register tab Button (or its root) on the prefab.")]
        [SerializeField] GameObject registerTabObject;
        [SerializeField] Color tabSelectedColor = new Color(0.18f, 0.18f, 0.22f, 1f);
        [SerializeField] Color tabUnselectedColor = new Color(0.72f, 0.72f, 0.76f, 1f);

        Player player;
        string authToken;
        public string AuthToken => authToken;
        public int PlayerId => player?.Id ?? -1;

        // Prevent overlapping requests from multiple clicks.
        bool requestInFlight = false;
        bool clearingMessage = false;

        public enum RequestType { Password = 0, Register = 1, Token = 2 }
        public RequestType CurrentMode { get; private set; }

        void Awake() {
            WireTabButton(loginTabObject, SwitchToLogin);
            WireTabButton(registerTabObject, SwitchToRegister);
        }

        void Start() {
            Debug.Log($"[Login] httpApiBaseUrl (from scene/prefab) = \"{httpApiBaseUrl}\"");
            if (httpApiBaseUrl.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0
                || httpApiBaseUrl.IndexOf("127.0.0.1", StringComparison.OrdinalIgnoreCase) >= 0) {
                Debug.LogWarning(
                    "[Login] API base is local. For litecoders.com production, set Http Api Base Url on this component to: https://litecoders.com/api");
            }

            player = new Player(
                Id: -1,
                Name: "Guest",
                IP: "0.0.0.0");
            Debug.Log("Start: Created guest player");

            HideMessage();
            CurrentMode = RequestType.Password;

            LoadAuthData();
            Debug.Log($"Start: Loaded auth data. Token: {authToken}, PlayerId: {player.Id}");

            ShowLoginScreen();

            if (!string.IsNullOrEmpty(authToken)) {
                Debug.Log("Start: Found saved token, attempting auto-login");
                ShowMessage("Restoring Session...");
                // Keep loginCG visible until verify succeeds; only switch inner layout.
                SwitchTo(RequestType.Token);
                Submit(RequestType.Token);
            } else {
                Debug.Log("Start: No saved token");
                HideCanvasGroup(logoutCanvasGroup);
                if (showCredentialFormAtStartup)
                    SetLoginRegisterMode(RequestType.Password);
                else {
                    HideCanvasGroup(idCanvasGroup);
                    HideCanvasGroup(submitCanvasGroup);
                }
            }

            RefreshLoginRegisterTabColors();
        }

        void Update() {
            UIMessage msg = loginMessage.GetComponent<UIMessage>();
            if (!clearingMessage && msg != null && !string.IsNullOrEmpty(msg.ToString())) {
                StartCoroutine(ClearMessageAfterDelay(messageClearDelaySeconds));
            }
        }

        public void OnChooseLogin() {
            EnterCredentialsLayout(RequestType.Password);
        }

        public void OnChooseRegister() {
            EnterCredentialsLayout(RequestType.Register);
        }

        #region UI Management
        public void HideSelf() {
            CanvasGroup thisCG = this.GetComponent<CanvasGroup>();
            HideCanvasGroup(thisCG);
        }
        public void ShowSelf() {
            CanvasGroup thisCG = this.GetComponent<CanvasGroup>();
            ShowCanvasGroup(thisCG);
        }
        static void HideCanvasGroup(CanvasGroup cg) {
            if (cg != null) {
                cg.alpha = 0;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }
        static void ShowCanvasGroup(CanvasGroup cg) {
            if (cg != null) {
                cg.alpha = 1;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        void ShowLoginScreen() {
            if (loginCG != null)
                ShowCanvasGroup(loginCG);
            if (loggedInCG != null)
                HideCanvasGroup(loggedInCG);
        }

        void ShowLoggedInScreen() {
            if (loginCG != null)
                HideCanvasGroup(loginCG);
            if (loggedInCG != null)
                ShowCanvasGroup(loggedInCG);
        }
        public void ShowMessage() {
            CanvasGroup messageCG = loginMessage.GetComponent<CanvasGroup>();
            ShowCanvasGroup(messageCG);
        }
        public void HideMessage() {
            CanvasGroup messageCG = loginMessage.GetComponent<CanvasGroup>();
            HideCanvasGroup(messageCG);
        }
        public void ClearMessage() {
            UIMessage msg = loginMessage.GetComponent<UIMessage>();
            if (msg != null) {
                msg.SetMessage("");
            }
        }
        public void ShowMessage(string message) {
            UIMessage msg = loginMessage.GetComponent<UIMessage>();
            if (msg != null) {
                msg.SetMessage(message);
                CanvasGroup messageCG = loginMessage.GetComponent<CanvasGroup>();
                ShowCanvasGroup(messageCG);
            }
        }
        public void SwitchTo(RequestType mode) {
            if (mode == RequestType.Password || mode == RequestType.Register) {
                SetLoginRegisterMode(mode);
            } else if (mode == RequestType.Token) {
                CurrentMode = RequestType.Token;
                HideCanvasGroup(idCanvasGroup);
                HideCanvasGroup(submitCanvasGroup);
                ShowCanvasGroup(logoutCanvasGroup);
            }
        }

        static void ApplyLoginRegisterCanvasLayout(RequestType mode, CanvasGroup playerIdRowCg, CanvasGroup submitCg, CanvasGroup logoutCg) {
            if (mode == RequestType.Password)
                ShowCanvasGroup(playerIdRowCg);
            else
                HideCanvasGroup(playerIdRowCg);
            ShowCanvasGroup(submitCg);
            HideCanvasGroup(logoutCg);
        }

        /// <summary>Credential form + tab tint. Does not call <see cref="SwitchTo"/> (avoids recursion with tab refresh).</summary>
        void SetLoginRegisterMode(RequestType mode) {
            if (mode != RequestType.Password && mode != RequestType.Register)
                return;
            CurrentMode = mode;
            ApplyLoginRegisterCanvasLayout(mode, idCanvasGroup, submitCanvasGroup, logoutCanvasGroup);
            RefreshLoginRegisterTabColors();
        }

        /// <summary>First entry from the choice screen: show credential panels and set mode + tab colors.</summary>
        void EnterCredentialsLayout(RequestType mode) {
            ShowLoginScreen();
            SetLoginRegisterMode(mode);
        }

        public void SwitchToLogin() {
            SetLoginRegisterMode(RequestType.Password);
        }

        public void SwitchToRegister() {
            SetLoginRegisterMode(RequestType.Register);
        }

        void RefreshLoginRegisterTabColors() {
            ApplyTabVisual(loginTabObject, CurrentMode == RequestType.Password);
            ApplyTabVisual(registerTabObject, CurrentMode == RequestType.Register);
        }

        static Button FindButtonOnTab(GameObject tab) {
            if (tab == null)
                return null;
            return tab.GetComponent<Button>() ?? tab.GetComponentInParent<Button>();
        }

        void WireTabButton(GameObject tab, UnityEngine.Events.UnityAction handler) {
            Button btn = FindButtonOnTab(tab);
            if (btn == null) {
                if (tab != null)
                    Debug.LogWarning($"[Login] Tab '{tab.name}' has no Button (self or parent); wire OnClick manually or assign the Button object.");
                return;
            }
            btn.onClick.AddListener(handler);
        }

        void ApplyTabVisual(GameObject tab, bool selected) {
            if (tab == null)
                return;
            Color c = selected ? tabSelectedColor : tabUnselectedColor;
            var button = FindButtonOnTab(tab);
            if (button != null) {
                ColorBlock cb = button.colors;
                cb.normalColor = c;
                cb.highlightedColor = c;
                cb.selectedColor = c;
                cb.pressedColor = c;
                cb.disabledColor = c;
                button.colors = cb;
                var g = button.targetGraphic;
                if (g != null)
                    g.color = Color.white;
                return;
            }
            var graphic = tab.GetComponent<Graphic>() ?? tab.GetComponentInChildren<Graphic>(true);
            if (graphic != null)
                graphic.color = c;
        }
        #endregion

        #region Button Callbacks
        public void ButtonSubmit() {
            Submit(CurrentMode);
        }
        public void Submit(RequestType mode) {
            if (requestInFlight)
                return;
            if (mode == RequestType.Password) {
                string playerIdStr = playerIdInput.GetText();
                string password = passwordInput.GetText();

                if (!int.TryParse(playerIdStr, out int id) || id <= 0 || password.Length < 8) {
                    Debug.LogWarning("Invalid Input.");
                    ShowMessage("Please enter valid player ID and password (8+ characters).");
                    return;
                }
                StartCoroutine(PasswordRoutine(id, password));
            } else if (mode == RequestType.Register) {
                string password = passwordInput.GetText();
                if (string.IsNullOrEmpty(password) || password.Length < 8) {
                    Debug.LogWarning("Password too short.");
                    ShowMessage("Password must be at least 8 characters long.");
                    return;
                }
                StartCoroutine(RegisterRoutine(password));
            } else if (mode == RequestType.Token) {
                if (string.IsNullOrEmpty(authToken)) {
                    Debug.LogWarning("No auth token available for verification.");
                    ShowMessage("No session found. Please log in.");
                    SwitchTo(RequestType.Password);
                    ShowLoginScreen();
                    return;
                }
                StartCoroutine(TokenRoutine(authToken));
            }
        }

        IEnumerator ClearMessageAfterDelay(long seconds) {
            clearingMessage = true;
            DateTime startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(seconds)) {
                yield return null;
            }
            ClearMessage();
            clearingMessage = false;
        }
        #endregion

        #region Network Requests
        IEnumerator TokenRoutine(string authTokenValue) {
            string url = $"{httpApiBaseUrl.TrimEnd('/')}/verify";
            var payload = new TokenVerifyRequest { authToken = authTokenValue };
            yield return SendRequest(url, JsonUtility.ToJson(payload), RequestType.Token);
        }

        IEnumerator RegisterRoutine(string password) {
            string url = $"{httpApiBaseUrl.TrimEnd('/')}/register";
            var payload = new RegisterRequest { password = password };
            yield return SendRequest(url, JsonUtility.ToJson(payload), RequestType.Register);
        }

        IEnumerator PasswordRoutine(int playerId, string password) {
            string url = $"{httpApiBaseUrl.TrimEnd('/')}/login";
            var payload = new LoginRequest { playerId = playerId, password = password };
            yield return SendRequest(url, JsonUtility.ToJson(payload), RequestType.Password);
        }

        // Shared helper to prevent code duplication and handle errors safely without freezing the Editor.
        IEnumerator SendRequest(string url, string jsonBody, RequestType requestType) {
            requestInFlight = true;

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST")) {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");

                // Prevent infinite hang if something goes wrong at network level.
                request.timeout = 10;

                yield return request.SendWebRequest();

                long code = request.responseCode;
                string body = request.downloadHandler != null ? request.downloadHandler.text : null;

                if (request.result == UnityWebRequest.Result.Success) {
                    HandleSuccess(body, requestType);
                }
                else if (request.result == UnityWebRequest.Result.ProtocolError && code == 401) {
                    // IMPORTANT: 401 is an expected auth failure, not a "hard" engine error.
                    // Using LogError can pause the Editor if Console "Error Pause" is enabled,
                    // making it look like Unity froze.
                    Debug.LogWarning($"Auth failed (401): {body}");
                    ShowMessage("Authentication failed. Please check your credentials and try again.");

                    // Optional: if you have an error label, update it here rather than logging.
                    // loginErrorText.text = "Invalid player ID or password";
                    // loginErrorText.gameObject.SetActive(true);

                    // Optional: keep UI responsive by clearing current selection if needed,
                    // but doing this unconditionally can be annoying for UX.
                    if (UnityEngine.EventSystems.EventSystem.current != null) {
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                    }
                } else {
                    string detail = !string.IsNullOrEmpty(body) ? body : request.error;
                    Debug.LogError($"Request Failed ({code}) {url}: {detail}");
                    ShowMessage("An error occurred while communicating with the server. Please try again later.");

                    // If you want to reduce EventSystem weirdness after real failures:
                    if (UnityEngine.EventSystems.EventSystem.current != null) {
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                    }
                }
            }

            requestInFlight = false;

            // Yielding here ensures Unity finishes the network cleanup before the next frame
            yield return null;
        }

        void HandleSuccess(string jsonResponse, RequestType requestType) {
            try {
                switch (requestType) {
                    case RequestType.Register:
                        var registerResponse = JsonUtility.FromJson<RegisterResponse>(jsonResponse);
                        if (registerResponse.status == "success") {
                            player.Id = registerResponse.playerId;
                            player.Name = $"Player #{player.Id}";
                            authToken = registerResponse.authToken;
                            SaveAuthData();
                            ShowMessage("Registration successful! You are now logged in.");
                            playerIdInput.SetText(player.Id.ToString());
                            SwitchTo(RequestType.Token);
                            ShowLoggedInScreen();
                        }
                        break;
                    case RequestType.Password:
                        var loginResponse = JsonUtility.FromJson<LoginResponse>(jsonResponse);
                        if (loginResponse.status == "success") {
                            player.Id = loginResponse.playerId;
                            player.Name = $"Player #{player.Id}";
                            authToken = loginResponse.authToken;
                            SaveAuthData();
                            ShowMessage("Login successful!");
                            SwitchTo(RequestType.Token);
                            ShowLoggedInScreen();
                        }
                        break;
                    case RequestType.Token:
                        var tokenResponse = JsonUtility.FromJson<TokenVerifyResponse>(jsonResponse);
                        if (tokenResponse.status == "success" && tokenResponse.valid) {
                            player.Id = tokenResponse.playerId;
                            player.Name = $"Player #{player.Id}";
                            SaveAuthData();
                            ShowMessage("Session restored. Welcome back!");
                            SwitchTo(RequestType.Token);
                            ShowLoggedInScreen();
                        } else {
                            // Token was invalid, clear saved data and switch to login.
                            ClearAuthData();
                            ShowMessage("Session expired. Please log in again.");
                            SwitchTo(RequestType.Password);
                            ShowLoginScreen();

                            Debug.LogWarning("Saved auth token was invalid. Please log in again.");

                            // Optional: keep UI responsive by clearing current selection if needed,
                            // but doing this unconditionally can be annoying for UX.
                            if (UnityEngine.EventSystems.EventSystem.current != null) {
                                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                            }
                        }
                        break;
                }
            } catch (Exception ex) {
                Debug.LogError($"JSON Parse Error: {ex.Message}");
            }
        }
        #endregion

        #region Data Persistence
        public void Logout() {
            ClearAuthData();
            ShowMessage("Logged out successfully.");
            SwitchTo(RequestType.Password);
            ShowLoginScreen();
        }

        public void SelectCharacter() {
            // Go to "Lobby" scene
            UnityEngine.SceneManagement.SceneManager.LoadScene("CharacterSelect_1");
        }

        void SaveAuthData() {
            if (!string.IsNullOrEmpty(authToken)) PlayerPrefs.SetString("auth_token", authToken);
            if (player.Id > 0) PlayerPrefs.SetInt("player_id", player.Id);
            PlayerPrefs.Save();
            // Left existing PlayerPrefs code, now also communicates with PersistentPlayerPreferences
            PersistentPlayerPreferences.instance.player = player;
        }

        void ClearAuthData() {
            authToken = null;
            player.Id = -1;
            player.Name = "Guest";
            PlayerPrefs.DeleteKey("auth_token");
            PlayerPrefs.DeleteKey("player_id");
            PlayerPrefs.Save();
            // Left existing PlayerPrefs code, now also communicates with PersistentPlayerPreferences
            PersistentPlayerPreferences.instance.authToken = authToken;
            PersistentPlayerPreferences.instance.player = null;
        }

        void LoadAuthData() {
            authToken = PlayerPrefs.GetString("auth_token", string.Empty);
            player.Id = PlayerPrefs.GetInt("player_id", -1);
            // uncomment this code to use PersistentPlayerPreferences instead of PlayerPrefs
            /*
            if (PersistentPlayerPreferences.instance.player != null) {
                authToken = PersistentPlayerPreferences.instance.authToken;
                player = PersistentPlayerPreferences.instance.player;
            } else {
                authToken = string.Empty;
                player.Id = -1;
            }
            */
            
        }
        #endregion

        [Serializable] class RegisterRequest {
            public string password;
        }
        [Serializable] class RegisterResponse {
            public string status;
            public string message;
            public int playerId;
            public string authToken;
        }
        [Serializable] class LoginRequest {
            public int playerId;
            public string password;
        }
        [Serializable] class LoginResponse {
            public string status;
            public string message;
            public int playerId;
            public string authToken;
        }
        [Serializable] class TokenVerifyRequest {
            public string authToken;
        }
        [Serializable] class TokenVerifyResponse {
            public string status;
            public bool valid;
            public int playerId;
        }
    }
}
