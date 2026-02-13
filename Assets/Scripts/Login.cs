using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Backend {

    /// <summary>
    /// Login class. Placeholder for any login-related functionality, it will let the server
    /// know (particularly for multiplayer mode) who is playing so the server knows their ELO
    /// for matchmaking and can save their game history. It will save a token locally
    /// so the player doesn't have to log in every time.
    /// </summary>
    public class Login : MonoBehaviour
    {
        // Base URL for the HTTP auth server (FastAPI)
        [SerializeField] string authServerBaseUrl = "http://localhost:8000";
        // Relative path to store auth token locally
        [SerializeField] string authTokenPath = "Auth/auth_token.txt";

        public string AuthToken => authToken;
    public int PlayerId => playerId;

    string authToken;
    int playerId = -1;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            LoadAuthData();
        }

        // Update is called once per frame
        void Update()
        {
        }

        public void RegisterPlayer(string password)
        {
            StartCoroutine(RegisterRoutine(password));
        }

        IEnumerator RegisterRoutine(string password)
        {
            string url = $"{authServerBaseUrl.TrimEnd('/')}/register";

            var payload = new RegisterRequest
            {
                password = password
            };

            string json = JsonUtility.ToJson(payload);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Registration failed: {request.error}");
                    yield break;
                }

                string responseText = request.downloadHandler.text;
                var response = JsonUtility.FromJson<RegisterResponse>(responseText);

                if (response == null || response.status != "success")
                {
                    Debug.LogWarning($"Registration failed: {response?.message}");
                    yield break;
                }

                playerId = response.player_id;
                authToken = response.token;

                SaveAuthData();

                Debug.Log($"Registration successful. New Player ID: {playerId}");
            }
        }

        public void LoginPlayer(int playerId, string password)
        {
            StartCoroutine(LoginRoutine(playerId, password));
        }

        IEnumerator LoginRoutine(int playerId, string password)
        {
            string url = $"{authServerBaseUrl.TrimEnd('/')}/login";

            var payload = new LoginRequest
            {
                player_id = playerId,
                password = password
            };

            string json = JsonUtility.ToJson(payload);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Login failed: {request.error}");
                    yield break;
                }

                string responseText = request.downloadHandler.text;
                var response = JsonUtility.FromJson<LoginResponse>(responseText);

                if (response == null || response.status != "success")
                {
                    Debug.LogWarning($"Login failed: {response?.message}");
                    yield break;
                }

                authToken = response.token;

                SaveAuthData();

                Debug.Log($"Login success. Player ID: {response.player_id}");
            }
        }

        void SaveAuthData()
        {
            if (!string.IsNullOrEmpty(authToken))
            {
                PlayerPrefs.SetString("auth_token", authToken);
            }

            if (playerId > 0)
            {
                PlayerPrefs.SetInt("player_id", playerId);
            }

            PlayerPrefs.Save();

            if (!string.IsNullOrEmpty(authToken))
            {
                WriteToPersistentFile(authTokenPath, authToken);
            }

            if (playerId > 0)
            {
                WriteToPersistentFile("Auth/player_id.txt", playerId.ToString());
            }
        }

        void LoadAuthData()
        {
            authToken = PlayerPrefs.GetString("auth_token", string.Empty);
            playerId = PlayerPrefs.GetInt("player_id", -1);

            if (string.IsNullOrEmpty(authToken))
            {
                authToken = ReadFromPersistentFile(authTokenPath);
            }

            if (playerId <= 0)
            {
                string playerIdStr = ReadFromPersistentFile("Auth/player_id.txt");
                if (!string.IsNullOrEmpty(playerIdStr) && int.TryParse(playerIdStr, out int id))
                {
                    playerId = id;
                }
            }
        }

        void WriteToPersistentFile(string relativePath, string content)
        {
            try
            {
                string fullPath = Path.Combine(Application.persistentDataPath, relativePath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fullPath, content);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to write auth data: {ex.Message}");
            }
        }

        string ReadFromPersistentFile(string relativePath)
        {
            try
            {
                string fullPath = Path.Combine(Application.persistentDataPath, relativePath);
                if (File.Exists(fullPath))
                {
                    return File.ReadAllText(fullPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to read auth data: {ex.Message}");
            }

            return string.Empty;
        }

        [Serializable]
        class RegisterRequest
        {
            public string password;
        }

        [Serializable]
        class RegisterResponse
        {
            public string status;
            public string message;
            public int player_id;
            public string token;
        }

        [Serializable]
        class LoginRequest
        {
            public int player_id;
            public string password;
        }

        [Serializable]
        class LoginResponse
        {
            public string status;
            public string message;
            public int player_id;
            public string token;
        }
    }
}
