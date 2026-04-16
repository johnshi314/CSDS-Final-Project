using UnityEngine;
using System;
using System.Collections;
using System.Text;
using UnityEngine.Networking;

namespace NetFlower {
    /// <summary>
    /// Persisted match session data + battle HTTP (stats submit). DontDestroyOnLoad singleton.
    /// Lobby networking lives on <see cref="Backend.Matchmaking"/> in the Lobby scene.
    /// </summary>
    public class Match : MonoBehaviour {

        public static Match PersistentInstance = null;

        public MatchStats matchStats { get; private set; }

        private PlayerMatchStats allyStats;
        private PlayerMatchStats enemyStats;
        private MatchupStats matchupStats;

        string authToken;
        public int dbMatchId;

        /// <summary>Roster from lobby (same order as GridMap red/blue lists). Used by online battle to map agents to player ids.</summary>
        public int[] lobbyRedPlayerIds { get; private set; }
        public int[] lobbyBluePlayerIds { get; private set; }
        public int[] lobbyRedCharacterIds { get; private set; }
        public int[] lobbyBlueCharacterIds { get; private set; }

        public enum TeamSelection { Red, Blue }
        public TeamSelection selectedTeam { get; private set; }

        public enum RequestType { MatchSubmit, PlayerSubmit, MatchupSubmit }

        [Tooltip("REST base for battle stats POSTs, no trailing slash. Should match Login / Matchmaking.")]
        [SerializeField] string httpApiBaseUrl = "https://litecoders.com/api";

        string EffectiveApiBase() => GameApiEndpoints.EffectiveApiBase(httpApiBaseUrl);

        void Awake() {
            if (PersistentInstance != null && PersistentInstance != this) {
                Destroy(gameObject);
                return;
            }
            PersistentInstance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start() {
            authToken = PlayerPrefs.GetString("auth_token", "");
            // authToken = PersistentPlayerPreferences.instance.authToken;
            if (string.IsNullOrEmpty(authToken))
                Debug.LogWarning("[Match] No auth token found. Stats submissions will fail until player logs in.");
            var effective = EffectiveApiBase();
            Debug.Log($"[Match] httpApiBaseUrl=\"{httpApiBaseUrl}\" -> REST calls use \"{effective}\"");
            if (!string.Equals(effective, (httpApiBaseUrl ?? "").Trim().TrimEnd('/'), StringComparison.Ordinal))
                Debug.LogWarning("[Match] API base was normalized (likely missing /api). Align with Login / Matchmaking.");
            if (httpApiBaseUrl.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0)
                Debug.LogWarning("[Match] For production use https://litecoders.com/api on this component.");
        }

        public static Match GetInstance() {
            return PersistentInstance;
        }

        void OnDestroy() {
            if (PersistentInstance == this)
                PersistentInstance = null;
        }

        /// <summary>Called by Matchmaking when lobby is ready to start the battle session.</summary>
        public void CommitFromLobby(int matchId, int[] redIds, int[] blueIds, int[] redCharIds = null, int[] blueCharIds = null) {
            dbMatchId = matchId;
            lobbyRedPlayerIds = redIds != null ? (int[])redIds.Clone() : System.Array.Empty<int>();
            lobbyBluePlayerIds = blueIds != null ? (int[])blueIds.Clone() : System.Array.Empty<int>();
            lobbyRedCharacterIds = redCharIds != null ? (int[])redCharIds.Clone() : System.Array.Empty<int>();
            lobbyBlueCharacterIds = blueCharIds != null ? (int[])blueCharIds.Clone() : System.Array.Empty<int>();
            matchStats = new MatchStats();
            StartMatch(matchId);
            Debug.Log($"[Match] CommitFromLobby match {matchId} red={IdsToStr(redIds)} blue={IdsToStr(blueIds)} redChars={IdsToStr(redCharIds)} blueChars={IdsToStr(blueCharIds)}");
        }

        public void SetSelectedTeam(TeamSelection team) {
            selectedTeam = team;
        }

        static string IdsToStr(int[] ids) {
            if (ids == null || ids.Length == 0) return "";
            return string.Join(",", ids);
        }

        public MatchStats StartMatch(int matchDbId) {
            if (matchStats == null)
                matchStats = new MatchStats();
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

        IEnumerator SubmitMatchUpdateRoutine(string matchJson) {
            yield return SendRequest($"{EffectiveApiBase()}/update-match", matchJson, RequestType.MatchSubmit);
        }

        IEnumerator SubmitMatchupRoutine(string matchupJson) {
            yield return SendRequest($"{EffectiveApiBase()}/submit-matchupstats", matchupJson, RequestType.MatchupSubmit);
        }

        IEnumerator SendRequest(string url, string jsonBody, RequestType requestType) {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            using (UnityWebRequest request = new UnityWebRequest(url, "POST")) {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + authToken);
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) {
                    Debug.Log($"Request succeeded to {url}");
                } else {
                    Debug.LogError($"Request failed ({request.responseCode}): {request.error}");
                }
            }
        }
    }
}
