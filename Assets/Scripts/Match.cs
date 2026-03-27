using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using Unity.VisualScripting;

// Placeholder
namespace NetFlower {
    public class Match : MonoBehaviour {

        // Static reference to the current match instance
        public static Match PersistentInstance = null;

        // Match stats
        public MatchStats matchStats { get; private set; }

        // Variables for stats
        private PlayerMatchStats allyStats;
        private PlayerMatchStats enemyStats;
        private MatchupStats matchupStats;
        Player player; // TODO: Make sure the player loggd in's id is set here
        public int dbMatchId;


        public enum TeamSelection { Red, Blue }
        public TeamSelection selectedTeam;

        //  Sending stats to database
        public enum RequestType { MatchSubmit, PlayerSubmit, MatchupSubmit }

        [Serializable]
        public class MatchIdResponse {
            public string status;
            public int match_id;
        }

        public class LobbyState {
            public bool everyoneReady;
            public List<int> redTeamPlayerIds;
            public List<int> blueTeamPlayerIds;
        }


        private TMPro.TextMeshProUGUI RedTeamText; // Reference to a UI text element to display match info
        private TMPro.TextMeshProUGUI BlueTeamText; // Reference to a UI text element to display match info

        void Start() {
            this.player = ClientPlayer.clientPlayer;
            if (PersistentInstance != null) {
                Destroy(this.gameObject); // Ensure only one instance exists
                return;
            } else {
                PersistentInstance = this.gameObject.GetComponent<Match>();
                DontDestroyOnLoad(this.gameObject); // Keep this object alive between scenes
            }
            StartCoroutine(JoinNewLobby());
        }

        public static Match GetInstance() {
            return PersistentInstance;
        }

        void InitializeMatch(int dbMatchId,
                             int allyPlayerIds,
                             int enemyPlayerIds) {
            this.dbMatchId = dbMatchId;

             // Example of recording a matchup
            //track match stats
            matchStats = new MatchStats();
            StartMatch(dbMatchId);

            EndMatch("ally");
        }

        // Record match data at start and end
        public MatchStats StartMatch(int dbMatchId) {
            matchStats.matchId = dbMatchId;
            matchStats.queueTime = 0f;
            matchStats.startTime = DateTime.UtcNow.ToString("o");

            return matchStats;
        }

        public void EndMatch(string winnerTeamId) {

            matchStats.endTime = DateTime.UtcNow.ToString("o");

            // Parse strings to DateTime to compute duration
            DateTime start = DateTime.Parse(matchStats.startTime);
            DateTime end = DateTime.Parse(matchStats.endTime);

            matchStats.duration = (float)(end - start).TotalSeconds;

            matchStats.winnerTeamId = winnerTeamId;

            // Update individual player stats
            if (allyStats != null)
                allyStats.won = (allyStats.teamId == winnerTeamId);

            if (enemyStats != null)
                enemyStats.won = (enemyStats.teamId == winnerTeamId);

            // Test adding match to database
            string matchJson = matchStats.ToJson();
            Debug.Log("Sending match JSON to server: " + matchJson);
            // Start coroutine to submit JSON to backend
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

            // record winner and add to database
            matchupStats.winnerCharacterId = winner;

            string matchupJson = matchupStats.ToJson();
            Debug.Log("Sending match JSON to server: " + matchupJson);
            // Start coroutine to submit JSON to backend
            StartCoroutine(SubmitMatchupRoutine(matchupJson));
        }

        #region Network Requests

        IEnumerator SubmitMatchUpdateRoutine(string matchJson) {
            string url = "http://localhost:8000/update-match";
            yield return SendRequest(url, matchJson, RequestType.MatchSubmit);
        }

        IEnumerator SubmitMatchupRoutine(string matchupJson) {
            string url = "http://localhost:8000/submit-matchupstats";
            yield return SendRequest(url, matchupJson, RequestType.MatchupSubmit);
        }

        IEnumerator SubmitMatchPlayerRoutine(string matchplayerJson) {
            string url = "http://localhost:8000/submit-playermatchstats";
            yield return SendRequest(url, matchplayerJson, RequestType.PlayerSubmit);
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
                Debug.Log($"Server response text: {request.downloadHandler.text}");


                if (request.result == UnityWebRequest.Result.Success) {
                    Debug.Log($"Request succeeded to {url}");
                    Debug.Log($"Server response: {request.downloadHandler.text}");
                } else {
                    Debug.LogError($"Request failed ({request.responseCode}): {request.error}");
                }
            }
        }



        // Listen to Server Push
        IEnumerator JoinNewLobby() {
            string url = "http://localhost:8000/join-new-lobby?player_id=" + player.Id;
            using (UnityWebRequest request = UnityWebRequest.Get(url)) {
                request.downloadHandler = new DownloadHandlerBuffer();

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success) {
                    Debug.LogError($"Server push failed: {request.error}");
                    yield break;
                }
                string json = request.downloadHandler.text;

                MatchIdResponse response = JsonUtility.FromJson<MatchIdResponse>(json);
                dbMatchId = response.match_id;

                Debug.Log("Received server push: " + json);

                // get some matchid form the servers
            }
        }

        // Listen to Server Push (put in update unction, timeout 1second?)
        IEnumerator ListenForServerPush() {
            string url = "http://localhost:8000/get-lobby-updates";
            using (UnityWebRequest request = UnityWebRequest.Get(url)) {
                request.downloadHandler = new DownloadHandlerBuffer();

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success) {
                    Debug.LogError($"Server push failed: {request.error}");
                    yield break;
                }
                string json = request.downloadHandler.text;
                Debug.Log("Received server push: " + json);

                LobbyState lobbyState = JsonUtility.FromJson<LobbyState>(json);

                // Lobby state:
                // Red team player IDs
                // Blue team player IDs
                UpdateGUI(lobbyState); // Update the GUI with the latest lobby state
            }
        }

        void UpdateGUI(LobbyState lobbyState) {
            // Update the GUI with the latest match and player stats
            // This could involve updating health bars, ability cooldowns, player names, etc.

            // Update the texts
            RedTeamText.text = "Red Team: " + string.Join(", ", lobbyState.redTeamPlayerIds);
            BlueTeamText.text = "Blue Team: " + string.Join(", ", lobbyState.blueTeamPlayerIds);

            if (lobbyState.everyoneReady) {
                InitializeMatch(dbMatchId,
                lobbyState.redTeamPlayerIds[0],
                lobbyState.blueTeamPlayerIds[0]);

                Match currentMatch = this;

                // Change scene and pass the match object
            }
            
        }

        // Player presses the "Join Red Team" button
        public void PressJoinRedTeam() {
            selectedTeam = TeamSelection.Red;
            StartCoroutine(SetPlayerTeam());
        }
        
        // Player presses the "Join Blue Team" button
        public void PressJoinBlueTeam() {
            selectedTeam = TeamSelection.Blue;
            StartCoroutine(SetPlayerTeam());
        }

        // Player selects team hey want to be on in team select age
        IEnumerator SetPlayerTeam() {
            string colorSelectedTeam = selectedTeam == TeamSelection.Red ? "red" : "blue";

            string url = "http://localhost:8000/set-player-team?player_id=" + player.Id + "&match_id=" + dbMatchId + "&team=" + colorSelectedTeam;

            using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, "")) {
                request.downloadHandler = new DownloadHandlerBuffer();

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success) {
                    Debug.LogError($"Select team failed: {request.error}");
                    yield break;
                }
                string json = request.downloadHandler.text;
                Debug.Log("Match ID from server: " + dbMatchId);
            }
        }


        public void PressReadyButton() {
            StartCoroutine(SetReady());
        }

        IEnumerator SetReady() {
            string url = "http://localhost:8000/set-ready?player_id=" + player.Id + "&match_id=" + dbMatchId;
            using (UnityWebRequest request = UnityWebRequest.Get(url)) {
                request.downloadHandler = new DownloadHandlerBuffer();

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success) {
                    Debug.LogError($"Server push failed: {request.error}");
                    yield break;
                }
                string json = request.downloadHandler.text;
                Debug.Log("Received server push: " + json);

                // get some matchid form the servers
                MatchIdResponse response = JsonUtility.FromJson<MatchIdResponse>(json);
            }
        }
        #endregion
    }
}


