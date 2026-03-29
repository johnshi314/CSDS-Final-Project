using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using NetFlower;

namespace NetFlower.Backend {
    /// <summary>
    /// Lobby phase only: join-new-lobby, team / ready HTTP, WebSocket (or poll) for roster updates.
    /// When everyone is ready, commits roster into <see cref="Match"/> for the battle scene.
    /// Put this on your Lobby scene (not necessarily DontDestroyOnLoad).
    /// </summary>
    public class Matchmaking : MonoBehaviour {
        [Serializable]
        public class MatchIdResponse {
            public string status;
            public int match_id;
        }

        /// <summary>JsonUtility-friendly snapshot (arrays, not List).</summary>
        [Serializable]
        public class LobbyState {
            public bool everyoneReady;
            public int[] redTeamPlayerIds;
            public int[] blueTeamPlayerIds;
        }

        [Tooltip("REST base, no trailing slash. Should match Login / Match.")]
        [SerializeField] string httpApiBaseUrl = "https://litecoders.com/api";

        [Tooltip("Lobby WebSocket base (no trailing slash). Empty → derived from HTTP + /ws.")]
        [SerializeField] string lobbyWebSocketBaseUrl = "";

        [SerializeField] TMPro.TextMeshProUGUI redTeamText;
        [SerializeField] TMPro.TextMeshProUGUI blueTeamText;

        Player _player;
        Match _match;

        ClientWebSocket _lobbySocket;
        CancellationTokenSource _lobbyCts;
        readonly ConcurrentQueue<string> _lobbyIncoming = new ConcurrentQueue<string>();
        bool _lobbyPollStarted;
        bool _lobbyPollRunning;
        bool _matchStartRequested;

        string EffectiveApiBase() => GameApiEndpoints.EffectiveApiBase(httpApiBaseUrl);

        void Start() {
            _match = Match.PersistentInstance;
            if (_match == null) {
                Debug.LogError("[Matchmaking] No Match singleton. Add a Match component to a DontDestroyOnLoad object before the Lobby scene.");
                return;
            }

            _player = ClientPlayer.clientPlayer;
            if (_player == null) {
                Debug.LogError("[Matchmaking] ClientPlayer.clientPlayer is null. Log in before entering the Lobby.");
                return;
            }

            var effective = EffectiveApiBase();
            Debug.Log($"[Matchmaking] httpApiBaseUrl=\"{httpApiBaseUrl}\" → REST \"{effective}\"");
            StartCoroutine(JoinNewLobby());
        }

        void Update() {
            if (_lobbyPollStarted && !_lobbyPollRunning) {
                _lobbyPollStarted = false;
                _lobbyPollRunning = true;
                StartCoroutine(PollLobbyUpdates());
            }
            DrainLobbyJsonQueue();
        }

        /// <summary>Process snapshots from WebSocket or HTTP (main thread — safe for TMP).</summary>
        void DrainLobbyJsonQueue() {
            while (_lobbyIncoming.TryDequeue(out var json)) {
                if (string.IsNullOrWhiteSpace(json))
                    continue;
                try {
                    var lobbyState = JsonUtility.FromJson<LobbyState>(json);
                    if (lobbyState != null)
                        UpdateLobbyGui(lobbyState);
                } catch (Exception e) {
                    Debug.LogWarning($"[Matchmaking] Bad lobby JSON: {e.Message}\n{json}");
                }
            }
        }

        void OnDestroy() {
            DisconnectLobbyWebSocket();
        }

        static string IdsToStr(int[] ids) {
            if (ids == null || ids.Length == 0) return "";
            return string.Join(",", ids);
        }

        void UpdateLobbyGui(LobbyState lobbyState) {
            if (redTeamText != null)
                redTeamText.text = "Red Team: " + IdsToStr(lobbyState.redTeamPlayerIds);
            if (blueTeamText != null)
                blueTeamText.text = "Blue Team: " + IdsToStr(lobbyState.blueTeamPlayerIds);

            if (_matchStartRequested)
                return;
            if (lobbyState.everyoneReady
                && lobbyState.redTeamPlayerIds != null && lobbyState.redTeamPlayerIds.Length > 0
                && lobbyState.blueTeamPlayerIds != null && lobbyState.blueTeamPlayerIds.Length > 0) {
                _matchStartRequested = true;
                _match.CommitFromLobby(_match.dbMatchId, lobbyState.redTeamPlayerIds, lobbyState.blueTeamPlayerIds);
            }
        }

        public void PressJoinRedTeam() {
            _match.SetSelectedTeam(Match.TeamSelection.Red);
            StartCoroutine(SetPlayerTeam("red"));
        }

        public void PressJoinBlueTeam() {
            _match.SetSelectedTeam(Match.TeamSelection.Blue);
            StartCoroutine(SetPlayerTeam("blue"));
        }

        IEnumerator SetPlayerTeam(string color) {
            string url = $"{EffectiveApiBase()}/set-player-team?player_id={_player.Id}&match_id={_match.dbMatchId}&team={color}";
            using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, "")) {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    Debug.LogError($"set-player-team failed: {request.error}");
                else
                    yield return FetchLobbySnapshotOnce();
            }
        }

        public void PressReadyButton() {
            StartCoroutine(SetReady());
        }

        IEnumerator SetReady() {
            string url = $"{EffectiveApiBase()}/set-ready?player_id={_player.Id}&match_id={_match.dbMatchId}";
            using (UnityWebRequest request = UnityWebRequest.Get(url)) {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    Debug.LogError($"set-ready failed: {request.error}");
                else
                    yield return FetchLobbySnapshotOnce();
            }
        }

        /// <summary>Pull current lobby JSON from server (same shape as WebSocket pushes). Updates UI on main thread.</summary>
        IEnumerator FetchLobbySnapshotOnce() {
            if (_match == null || _match.dbMatchId <= 0)
                yield break;
            string url = $"{EffectiveApiBase()}/get-lobby-updates?match_id={_match.dbMatchId}";
            using (UnityWebRequest request = UnityWebRequest.Get(url)) {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    yield break;
                var body = request.downloadHandler.text;
                if (!string.IsNullOrEmpty(body))
                    _lobbyIncoming.Enqueue(body);
            }
            DrainLobbyJsonQueue();
        }

        IEnumerator JoinNewLobby() {
            string url = $"{EffectiveApiBase()}/join-new-lobby?player_id={_player.Id}";
            using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, "")) {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) {
                    var errBody = request.downloadHandler != null ? request.downloadHandler.text : "";
                    Debug.LogError($"join-new-lobby failed ({request.responseCode}) {url}: {request.error}\n{errBody}");
                    yield break;
                }
                var response = JsonUtility.FromJson<MatchIdResponse>(request.downloadHandler.text);
                if (response == null) {
                    Debug.LogError("join-new-lobby: bad JSON");
                    yield break;
                }
                _match.dbMatchId = response.match_id;
                Debug.Log($"[Matchmaking] Joined lobby match_id={_match.dbMatchId}");
                ConnectLobbyWebSocket();
                yield return FetchLobbySnapshotOnce();
            }
        }

        IEnumerator PollLobbyUpdates() {
            var wait = new WaitForSeconds(0.05f);
            try {
                while (_match != null && _match.dbMatchId > 0 && enabled) {
                    string url = $"{EffectiveApiBase()}/get-lobby-updates?match_id={_match.dbMatchId}";
                    using (UnityWebRequest request = UnityWebRequest.Get(url)) {
                        request.downloadHandler = new DownloadHandlerBuffer();
                        request.timeout = 10;
                        yield return request.SendWebRequest();
                        if (request.result == UnityWebRequest.Result.Success)
                            _lobbyIncoming.Enqueue(request.downloadHandler.text);
                    }
                    yield return wait;
                }
            } finally {
                _lobbyPollRunning = false;
            }
        }

        void ConnectLobbyWebSocket() {
            DisconnectLobbyWebSocket();
            _lobbyCts = new CancellationTokenSource();
            _lobbySocket = new ClientWebSocket();
            _ = RunLobbyWebSocketAsync();
        }

        void DisconnectLobbyWebSocket() {
            _lobbyCts?.Cancel();
            try {
                _lobbySocket?.Abort();
                _lobbySocket?.Dispose();
            } catch { /* ignore */ }
            _lobbySocket = null;
            _lobbyCts = null;
        }

        async Task RunLobbyWebSocketAsync() {
            var token = _lobbyCts.Token;
            var wsBase = GameApiEndpoints.LobbyWebSocketBaseTrimmed(EffectiveApiBase(), lobbyWebSocketBaseUrl);
            var uri = new Uri($"{wsBase}/lobby/{_match.dbMatchId}?player_id={_player.Id}");
            try {
                await _lobbySocket.ConnectAsync(uri, token);
                Debug.Log($"[Matchmaking] Lobby WebSocket connected {uri}");
                await ReceiveLobbyLoopAsync(token);
            } catch (OperationCanceledException) { }
            catch (Exception e) {
                Debug.LogWarning($"[Matchmaking] Lobby WebSocket error: {e.Message} — falling back to HTTP poll.");
                _lobbyPollStarted = true;
            } finally {
                try { _lobbySocket?.Dispose(); } catch { }
                _lobbySocket = null;
            }
        }

        async Task ReceiveLobbyLoopAsync(CancellationToken token) {
            // Reassemble full text messages (ReceiveAsync can return fragments).
            var chunk = new byte[16384];
            while (_lobbySocket != null && _lobbySocket.State == WebSocketState.Open && !token.IsCancellationRequested) {
                using (var message = new MemoryStream()) {
                    WebSocketReceiveResult result;
                    do {
                        var segment = new ArraySegment<byte>(chunk);
                        result = await _lobbySocket.ReceiveAsync(segment, token);
                        if (result.MessageType == WebSocketMessageType.Close)
                            return;
                        if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
                            message.Write(chunk, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (message.Length > 0) {
                        var text = Encoding.UTF8.GetString(message.ToArray());
                        _lobbyIncoming.Enqueue(text);
                    }
                }
            }
        }
    }
}
