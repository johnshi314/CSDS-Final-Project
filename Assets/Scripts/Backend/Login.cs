using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

using NetFlower.UI;
using UnityEditor.PackageManager;

namespace NetFlower.Backend {
    public class Login : MonoBehaviour {
        [SerializeField] string authServerBaseUrl = "http://localhost:8000";
        [SerializeField] UserInput playerIdInput;
        [SerializeField] UserInput passwordInput;
        [SerializeField] GameObject loginMessage;
        [SerializeField] CanvasGroup idCanvasGroup;
        [SerializeField] CanvasGroup registerCanvasGroup;
        [SerializeField] CanvasGroup submitCanvasGroup;
        [SerializeField] CanvasGroup logoutCanvasGroup;
        [SerializeField] CanvasGroup choiceCanvasGroup;
        [SerializeField] long messageClearDelaySeconds = 5;


        Player player;
        string authToken;
        public string AuthToken => authToken;
        public int PlayerId => player?.Id ?? -1;

        // Prevent overlapping requests from multiple clicks.
        bool requestInFlight = false;
        bool clearingMessage = false;

        public enum RequestType { Password = 0, Register = 1, Token = 2 }
        public RequestType CurrentMode { get; private set; }

        void Start() {

            player = new Player(
                Id: -1,
                Name: "Guest",
                IP: "0.0.0.0");
            Debug.Log("Start: Created guest player");

            HideMessage();
            CurrentMode = RequestType.Password;

            LoadAuthData();
            Debug.Log($"Start: Loaded auth data. Token: {authToken}, PlayerId: {player.Id}");


            if (!string.IsNullOrEmpty(authToken)) {
                Debug.Log("Start: Found saved token, attempting auto-login");
                ShowMessage("Restoring Session...");
                HideCanvasGroup(choiceCanvasGroup);
                //CurrentMode = RequestType.Token;
                SwitchTo(RequestType.Token);   
                //Submit(RequestType.Token);
                Submit(RequestType.Token);
            } else {
                Debug.Log("Start: No saved token, showing choice panel");
                ShowCanvasGroup(choiceCanvasGroup);

                // Hide other UI panels while choosing
                HideCanvasGroup(idCanvasGroup);
                HideCanvasGroup(registerCanvasGroup);
                HideCanvasGroup(submitCanvasGroup);
                HideCanvasGroup(logoutCanvasGroup);
            }
        }

        void Update() {
            UIMessage msg = loginMessage.GetComponent<UIMessage>();
            if (!clearingMessage && msg != null && !string.IsNullOrEmpty(msg.ToString())) {
                StartCoroutine(ClearMessageAfterDelay(messageClearDelaySeconds));
            }
        }

        public void OnChooseLogin() {
            HideCanvasGroup(choiceCanvasGroup);
            SwitchToLogin();
        }

        public void OnChooseRegister() {
            HideCanvasGroup(choiceCanvasGroup);
            SwitchToRegister();
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
            if (mode == RequestType.Password) {
                CurrentMode = RequestType.Password;
                ShowCanvasGroup(idCanvasGroup);
                HideCanvasGroup(registerCanvasGroup);
                ShowCanvasGroup(submitCanvasGroup);
                HideCanvasGroup(logoutCanvasGroup);
            } else if (mode == RequestType.Register) {
                CurrentMode = RequestType.Register;
                HideCanvasGroup(idCanvasGroup);
                ShowCanvasGroup(registerCanvasGroup);
                ShowCanvasGroup(submitCanvasGroup);
                HideCanvasGroup(logoutCanvasGroup);
            } else if (mode == RequestType.Token) {
                CurrentMode = RequestType.Token;
                HideCanvasGroup(idCanvasGroup);
                HideCanvasGroup(registerCanvasGroup);
                HideCanvasGroup(submitCanvasGroup);
                ShowCanvasGroup(logoutCanvasGroup);
            }
        }
        public void SwitchToLogin() {
            SwitchTo(RequestType.Password);
        }
        public void SwitchToRegister() {
            SwitchTo(RequestType.Register);
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
        IEnumerator TokenRoutine(string token) {
            string url = $"{authServerBaseUrl.TrimEnd('/')}/verify";
            var payload = new TokenVerifyRequest { token = token };
            yield return SendRequest(url, JsonUtility.ToJson(payload), RequestType.Token);
        }

        IEnumerator RegisterRoutine(string password) {
            string url = $"{authServerBaseUrl.TrimEnd('/')}/register";
            var payload = new RegisterRequest { password = password };
            yield return SendRequest(url, JsonUtility.ToJson(payload), RequestType.Register);
        }

        IEnumerator PasswordRoutine(int playerId, string password) {
            string url = $"{authServerBaseUrl.TrimEnd('/')}/login";
            var payload = new LoginRequest { player_id = playerId, password = password };
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
                    Debug.LogError($"Request Failed ({code}): {detail}");
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
                            player.Id = registerResponse.player_id;
                            authToken = registerResponse.token;
                            SaveAuthData();
                            ShowMessage("Registration successful! You are now logged in.");
                            playerIdInput.SetText(player.Id.ToString());
                            SwitchTo(RequestType.Password);
                        }
                        break;
                    case RequestType.Password:
                        var loginResponse = JsonUtility.FromJson<LoginResponse>(jsonResponse);
                        if (loginResponse.status == "success") {
                            player.Id = loginResponse.player_id;
                            authToken = loginResponse.token;
                            SaveAuthData();
                            ShowMessage("Login successful!");
                            SwitchTo(RequestType.Token);
                        }
                        break;
                    case RequestType.Token:
                        var tokenResponse = JsonUtility.FromJson<TokenVerifyResponse>(jsonResponse);
                        if (tokenResponse.status == "success" && tokenResponse.valid) {
                            ClientPlayer.clientPlayer = player;
                            player.Id = tokenResponse.player_id;
                            SaveAuthData();
                            ShowMessage("Session restored. Welcome back!");
                            SwitchTo(RequestType.Token);
                        } else {
                            // Token was invalid, clear saved data and switch to login.
                            ClearAuthData();
                            ShowMessage("Session expired. Please log in again.");
                            SwitchTo(RequestType.Password);

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
        }
        void SaveAuthData() {
            if (!string.IsNullOrEmpty(authToken)) PlayerPrefs.SetString("auth_token", authToken);
            if (player.Id > 0) PlayerPrefs.SetInt("player_id", player.Id);
            PlayerPrefs.Save();
        }

        void ClearAuthData() {
            authToken = null;
            player.Id = -1;
            PlayerPrefs.DeleteKey("auth_token");
            PlayerPrefs.DeleteKey("player_id");
            PlayerPrefs.Save();
        }

        void LoadAuthData() {
            authToken = PlayerPrefs.GetString("auth_token", string.Empty);
            player.Id = PlayerPrefs.GetInt("player_id", -1);
        }
        #endregion

        [Serializable] class RegisterRequest {
            public string password;
        }
        [Serializable] class RegisterResponse {
            public string status;
            public string message;
            public int player_id;
            public string token;
        }
        [Serializable] class LoginRequest {
            public int player_id;
            public string password;
        }
        [Serializable] class LoginResponse {
            public string status;
            public string message;
            public int player_id;
            public string token;
        }
        [Serializable] class TokenVerifyRequest {
            public string token;
        }
        [Serializable] class TokenVerifyResponse {
            public string status;
            public bool valid;
            public int player_id;
        }
    }
}
