using UnityEngine;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace NetFlower {
    /// <summary>
    /// Lobby + match lifecycle. Persists across scenes (DontDestroyOnLoad).
    /// Flow: HTTP join-new-lobby → WebSocket …/ws/lobby/{match_id} for live updates.
    /// Production (nginx): REST under https://host/api/*, WS under wss://host/ws/lobby/*.
    /// Set httpApiBaseUrl and lobbyWebSocketBaseUrl in the Inspector per build target.
    /// </summary>
    public class Match : MonoBehaviour {

        public static Match PersistentInstance = null;

        public MatchStats matchStats { get; private set; }

        private PlayerMatchStats allyStats;
        private PlayerMatchStats enemyStats;
        private MatchupStats matchupStats;
        Player player;
        public int dbMatchId;

        public enum TeamSelection { Red, Blue }
        public TeamSelection selectedTeam;

        public enum RequestType { MatchSubmit, PlayerSubmit, MatchupSubmit }

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

        [Tooltip("REST base, no trailing slash. Local: http://localhost:8000. Production: https://litecoders.com/api")]
        [SerializeField] string httpApiBaseUrl = "https://litecoders.com/api";

        [Tooltip("Lobby WebSocket base (no trailing slash), e.g. wss://litecoders.com/ws. Leave empty to derive from httpApiBaseUrl + /ws.")]
        [SerializeField] string lobbyWebSocketBaseUrl = "http://localhost:8000/ws";
        [SerializeField] TMPro.TextMeshProUGUI redTeamText;
        [SerializeField] TMPro.TextMeshProUGUI blueTeamText;

        ClientWebSocket _lobbySocket;
        CancellationTokenSource _lobbyCts;
        readonly ConcurrentQueue<string> _lobbyIncoming = new ConcurrentQueue<string>();
        bool _lobbyPollStarted;
        bool _lobbyPollRunning;
        bool _matchStartRequested;

        void Start() {
            if (PersistentInstance != null) {
                Destroy(gameObject);
                return;
            }
            PersistentInstance = GetComponent<Match>();
            DontDestroyOnLoad(gameObject);

            player = ClientPlayer.clientPlayer;
            if (player == null) {
                Debug.LogError("[Match] ClientPlayer.clientPlayer is null. Set it after login before entering Lobby.");
                return;
            }
            StartCoroutine(JoinNewLobby());
        }

        public static Match GetInstance() {
            return PersistentInstance;
        }

        void Update() {
            if (_lobbyPollStarted && !_lobbyPollRunning) {
                _lobbyPollStarted = false;
                _lobbyPollRunning = true;
                StartCoroutine(PollLobbyUpdates());
            }
            while (_lobbyIncoming.TryDequeue(out var json)) {
                try {
                    var lobbyState = JsonUtility.FromJson<LobbyState>(json);
                    if (lobbyState != null)
                        UpdateGUI(lobbyState);
                } catch (Exception e) {
                    Debug.LogWarning($"[Match] Bad lobby JSON: {e.Message}\n{json}");
                }
            }
        }

        void OnDestroy() {
            DisconnectLobbyWebSocket();
            if (PersistentInstance == this)
                PersistentInstance = null;
        }

        void InitializeMatch(int matchId, int[] redIds, int[] blueIds) {
            dbMatchId = matchId;
            matchStats = new MatchStats();
            StartMatch(matchId);
            Debug.Log($"[Match] Starting match {matchId} red={IdsToStr(redIds)} blue={IdsToStr(blueIds)}");
            // TODO: load gameplay scene when ready
        }

        static string IdsToStr(int[] ids) {
            if (ids == null || ids.Length == 0) return "";
            return string.Join(",", ids);
        }

        public MatchStats StartMatch(int matchDbId) {
            matchStats.matchId = matchDbId;
            matchStats.queueTime = 0f;
            matchStats.startTime = DateTime.UtcNow.ToString("o");
            return matchStats;
        }

        public void EndMatch(string winnerTeamId) {
            if (matchStats == null) return;
            matchStats.endTime = DateTime.UtcNow.ToString("o");
            DateTime start = DateTime.Parse(matchStats.startTime);
            DateTime end = DateTime.Parse(matchStats.endTime);
            matchStats.duration = (float)(end - start).TotalSeconds;
            matchStats.winnerTeamId = winnerTeamId;
            if (allyStats != null)
                allyStats.won = (allyStats.teamId == winnerTeamId);
            if (enemyStats != null)
                enemyStats.won = (enemyStats.teamId == winnerTeamId);
            string matchJson = matchStats.ToJson();
            Debug.Log("Sending match JSON to server: " + matchJson);
            StartCoroutine(SubmitMatchUpdateRoutine(matchJson));
        }

        public MatchupStats RegisterMatchup(string characterAId, string characterBId) {
            var matchup = new MatchupStats(
                matchId: matchStats.matchId,
                characterAId: characterAId,
                characterBId: characterBId
            );
            return matchup;
        }

        public void ResolveMatchup(string winner) {
            if (matchupStats == null) return;
            matchupStats.winnerCharacterId = winner;
            string matchupJson = matchupStats.ToJson();
            StartCoroutine(SubmitMatchupRoutine(matchupJson));
        }

        #region Network — HTTP

        IEnumerator SubmitMatchUpdateRoutine(string matchJson) {
            yield return SendRequest($"{httpApiBaseUrl}/update-match", matchJson, RequestType.MatchSubmit);
        }

        IEnumerator SubmitMatchupRoutine(string matchupJson) {
            yield return SendRequest($"{httpApiBaseUrl}/submit-matchupstats", matchupJson, RequestType.MatchupSubmit);
        }

        IEnumerator SubmitMatchPlayerRoutine(string matchplayerJson) {
            yield return SendRequest($"{httpApiBaseUrl}/submit-playermatchstats", matchplayerJson, RequestType.PlayerSubmit);
        }

        IEnumerator SendRequest(string url, string jsonBody, RequestType requestType) {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            using (UnityWebRequest request = new UnityWebRequest(url, "POST")) {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) {
                    Debug.Log($"Request succeeded to {url}");
                } else {
                    Debug.LogError($"Request failed ({request.responseCode}): {request.error}");
                }
            }
        }

        IEnumerator JoinNewLobby() {
            string url = $"{httpApiBaseUrl}/join-new-lobby?player_id={player.Id}";
            using (UnityWebRequest request = UnityWebRequest.Get(url)) {
                request.downloadHandler = new DownloadHandlerBuffer();
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) {
                    Debug.LogError($"join-new-lobby failed: {request.error}");
                    yield break;
                }
                var response = JsonUtility.FromJson<MatchIdResponse>(request.downloadHandler.text);
                if (response == null) {
                    Debug.LogError("join-new-lobby: bad JSON");
                    yield break;
                }
                dbMatchId = response.match_id;
                Debug.Log($"[Match] Joined lobby match_id={dbMatchId}");
                ConnectLobbyWebSocket();
            }
        }

        /// <summary>Optional polling fallback if WebSocket is unavailable.</summary>
        IEnumerator PollLobbyUpdates() {
            var wait = new WaitForSeconds(0.05f);
            try {
                while (dbMatchId > 0 && enabled) {
                    string url = $"{httpApiBaseUrl}/get-lobby-updates?match_id={dbMatchId}";
                    using (UnityWebRequest request = UnityWebRequest.Get(url)) {
                        request.downloadHandler = new DownloadHandlerBuffer();
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

        void UpdateGUI(LobbyState lobbyState) {
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
                InitializeMatch(dbMatchId, lobbyState.redTeamPlayerIds, lobbyState.blueTeamPlayerIds);
            }
        }

        public void PressJoinRedTeam() {
            selectedTeam = TeamSelection.Red;
            StartCoroutine(SetPlayerTeam());
        }

        public void PressJoinBlueTeam() {
            selectedTeam = TeamSelection.Blue;
            StartCoroutine(SetPlayerTeam());
        }

        IEnumerator SetPlayerTeam() {
            string color = selectedTeam == TeamSelection.Red ? "red" : "blue";
            string url = $"{httpApiBaseUrl}/set-player-team?player_id={player.Id}&match_id={dbMatchId}&team={color}";
            using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, "")) {
                request.downloadHandler = new DownloadHandlerBuffer();
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    Debug.LogError($"set-player-team failed: {request.error}");
            }
        }

        public void PressReadyButton() {
            StartCoroutine(SetReady());
        }

        IEnumerator SetReady() {
            string url = $"{httpApiBaseUrl}/set-ready?player_id={player.Id}&match_id={dbMatchId}";
            using (UnityWebRequest request = UnityWebRequest.Get(url)) {
                request.downloadHandler = new DownloadHandlerBuffer();
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    Debug.LogError($"set-ready failed: {request.error}");
            }
        }

        #endregion

        #region Network — Lobby WebSocket (live pushes)

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
            var uri = new Uri($"{LobbyWebSocketBaseTrimmed()}/lobby/{dbMatchId}?player_id={player.Id}");
            try {
                await _lobbySocket.ConnectAsync(uri, token);
                Debug.Log($"[Match] Lobby WebSocket connected {uri}");
                await ReceiveLobbyLoopAsync(token);
            } catch (OperationCanceledException) { }
            catch (Exception e) {
                Debug.LogWarning($"[Match] Lobby WebSocket error: {e.Message} — falling back to HTTP poll (main thread).");
                _lobbyPollStarted = true;
            } finally {
                try { _lobbySocket?.Dispose(); } catch { }
                _lobbySocket = null;
            }
        }

        /// <summary>Base URL for lobby WebSocket, no trailing slash (…/ws).</summary>
        string LobbyWebSocketBaseTrimmed() {
            var explicitBase = (lobbyWebSocketBaseUrl ?? "").Trim().TrimEnd('/');
            if (!string.IsNullOrEmpty(explicitBase))
                return explicitBase;
            return WebSocketSchemeHostFromHttpApi(httpApiBaseUrl) + "/ws";
        }

        /// <summary>ws(s)://host[:port] from REST base (strips any /api path).</summary>
        static string WebSocketSchemeHostFromHttpApi(string httpApiBase) {
            if (string.IsNullOrWhiteSpace(httpApiBase))
                httpApiBase = "http://localhost:8000";
            if (!Uri.TryCreate(httpApiBase.Trim(), UriKind.Absolute, out var u))
                return "ws://localhost:8000";
            var sch = u.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
            return $"{sch}://{u.Authority}";
        }

        async Task ReceiveLobbyLoopAsync(CancellationToken token) {
            var buffer = new byte[8192];
            while (_lobbySocket != null && _lobbySocket.State == WebSocketState.Open && !token.IsCancellationRequested) {
                var segment = new ArraySegment<byte>(buffer);
                var result = await _lobbySocket.ReceiveAsync(segment, token);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
                if (result.MessageType == WebSocketMessageType.Text && result.Count > 0) {
                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _lobbyIncoming.Enqueue(text);
                }
            }
        }

        #endregion
    }
}
