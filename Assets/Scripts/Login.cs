using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

using GameData;
using GameUI;

namespace Backend {
    public class Login : MonoBehaviour {
        [SerializeField] string authServerBaseUrl = "http://localhost:8000";
        [SerializeField] string authTokenPath = "Auth/auth_token.txt";
        [SerializeField] UserInput playerIdInput;
        [SerializeField] UserInput passwordInput;

        Player player;
        string authToken;
        public string AuthToken => authToken;
        public int PlayerId => player?.Id ?? -1;

        // Prevent overlapping requests from multiple clicks.
        bool requestInFlight = false;

        void Start() {
            player = new Player {
                Id = -1,
                Name = "Guest",
                IP = ""
            };
            LoadAuthData();
        }

        public void ButtonRegister() {
            if (requestInFlight) return;

            string password = passwordInput.GetText();
            if (string.IsNullOrEmpty(password) || password.Length < 8) {
                Debug.LogWarning("Password too short.");
                return;
            }
            StartCoroutine(RegisterRoutine(password));
        }

        public void ButtonLogin() {
            if (requestInFlight) return;

            string playerIdStr = playerIdInput.GetText();
            string password = passwordInput.GetText();

            if (!int.TryParse(playerIdStr, out int id) || id <= 0 || password.Length < 8) {
                Debug.LogWarning("Invalid Input.");
                return;
            }
            StartCoroutine(LoginRoutine(id, password));
        }

        IEnumerator RegisterRoutine(string password) {
            string url = $"{authServerBaseUrl.TrimEnd('/')}/register";
            var payload = new RegisterRequest { password = password };
            yield return SendRequest(url, JsonUtility.ToJson(payload), true);
        }

        IEnumerator LoginRoutine(int playerId, string password) {
            string url = $"{authServerBaseUrl.TrimEnd('/')}/login";
            var payload = new LoginRequest { player_id = playerId, password = password };
            yield return SendRequest(url, JsonUtility.ToJson(payload), false);
        }

        // Shared helper to prevent code duplication and handle errors safely
        IEnumerator SendRequest(string url, string jsonBody, bool isRegister) {
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
                    HandleSuccess(body, isRegister);
                }
                else if (request.result == UnityWebRequest.Result.ProtocolError && code == 401) {
                    // IMPORTANT: 401 is an expected auth failure, not a "hard" engine error.
                    // Using LogError can pause the Editor if Console "Error Pause" is enabled,
                    // making it look like Unity froze.
                    Debug.LogWarning($"Auth failed (401): {body}");

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

        void HandleSuccess(string jsonResponse, bool isRegister) {
            try {
                if (isRegister) {
                    var response = JsonUtility.FromJson<RegisterResponse>(jsonResponse);
                    if (response.status == "success") {
                        player.Id = response.player_id;
                        authToken = response.token;
                        SaveAuthData();
                    }
                } else {
                    var response = JsonUtility.FromJson<LoginResponse>(jsonResponse);
                    if (response.status == "success") {
                        player.Id = response.player_id;
                        authToken = response.token;
                        SaveAuthData();
                    }
                }
            }
            catch (Exception ex) {
                Debug.LogError($"JSON Parse Error: {ex.Message}");
            }
        }

        #region Data Persistence
        void SaveAuthData() {
            if (!string.IsNullOrEmpty(authToken)) PlayerPrefs.SetString("auth_token", authToken);
            if (player.Id > 0) PlayerPrefs.SetInt("player_id", player.Id);
            PlayerPrefs.Save();

            WriteToPersistentFile(authTokenPath, authToken);
            WriteToPersistentFile("Auth/player_id.txt", player.Id.ToString());
        }

        void LoadAuthData() {
            authToken = PlayerPrefs.GetString("auth_token", ReadFromPersistentFile(authTokenPath));
            player.Id = PlayerPrefs.GetInt("player_id", -1);
            if (player.Id <= 0) {
                int.TryParse(ReadFromPersistentFile("Auth/player_id.txt"), out int id);
                player.Id = id != 0 ? id : -1;
            }
        }

        void WriteToPersistentFile(string relativePath, string content) {
            // If content is empty/null, don't attempt a write.
            if (string.IsNullOrEmpty(content)) return;

            try {
                string fullPath = Path.Combine(Application.persistentDataPath, relativePath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                File.WriteAllText(fullPath, content);
            }
            catch (Exception ex) {
                Debug.LogWarning($"Write fail: {ex.Message}");
            }
        }

        string ReadFromPersistentFile(string relativePath) {
            string fullPath = Path.Combine(Application.persistentDataPath, relativePath);
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
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
    }
}
