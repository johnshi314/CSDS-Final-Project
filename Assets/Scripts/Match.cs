using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

// Placeholder
namespace NetFlower {
    public class Match : MonoBehaviour {

        // Match stats
        public MatchStats matchStats { get; private set; }

        // Variables for stats
        private PlayerMatchStats allyStats;
        private PlayerMatchStats enemyStats;
        private MatchupStats matchupStats;
        public int dbMatchId;

        //  Sending stats to database
        public enum RequestType { MatchSubmit, PlayerSubmit, MatchupSubmit }

        public void Start() {
            // dbMatch id will hold the next unique matchId from database
            StartCoroutine(CreateMatchRoutine());
        }

        [Serializable]
        public class MatchIdResponse {
            public string status;
            public int match_id;
        }

        void InitializeMatch() {
            //track match stats
            matchStats = new MatchStats();
            StartMatch(dbMatchId);

            // Create parent objects for allies and enemies
            GameObject allies = new GameObject("Allies");
            GameObject enemies = new GameObject("Enemies");

            Player allyPlayer = new Player(Id: 1, Name: "AllyPlayer", IP: "127.0.0.1");
            Player enemyPlayer = new Player(Id: 2, Name: "EnemyPlayer", IP: "127.0.0.1");

            // Create agents
            GameObject newAlly = Agent.NewAgent(
                player: allyPlayer,
                agent_name: "Test Ally 1",
                hp: 30,
                range: 3,
                abilities: null,
                tunneling: Agent.Tunneling.Ally,
                parent: allies,
                position: new Vector3(2, 0, 0)
            );

            // Store stats on newAlly
            Agent allyAgent = newAlly.GetComponent<Agent>();
            allyStats = allyAgent.RegisterPlayer(dbMatchId);

            GameObject newAgent = Agent.NewAgent(
                player: enemyPlayer,
                agent_name: "Test Enemy 1",
                hp: 15,
                range: 2,
                abilities: null,
                tunneling: Agent.Tunneling.Nothing,
                parent: enemies,
                position: new Vector3(0, 0, 0)
            );

            // Store stats on newAgent
            Agent enemyAgent = newAgent.GetComponent<Agent>();
            enemyStats = enemyAgent.RegisterPlayer(dbMatchId);

            // Add sphere mesh to both agents
            MeshFilter enemyMesh = newAgent.AddComponent<MeshFilter>();
            enemyMesh.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            MeshFilter allyMesh = newAlly.AddComponent<MeshFilter>();
            allyMesh.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

            // Make blue material
            Material blueMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            blueMaterial.color = Color.blue;
            // Make red material
            Material redMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            redMaterial.color = Color.red;

            // Apply blue material to ally agent
            MeshRenderer allyRenderer = newAlly.AddComponent<MeshRenderer>();
            allyRenderer.materials = new Material[] { blueMaterial };
            // Apply red material to enemy agent
            MeshRenderer enemyRenderer = newAgent.AddComponent<MeshRenderer>();
            enemyRenderer.materials = new Material[] { redMaterial };

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

        IEnumerator CreateMatchRoutine() {
            string url = "http://localhost:8000/create-match";

            using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, "")) {
                request.downloadHandler = new DownloadHandlerBuffer();

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success) {
                    Debug.LogError($"Create match failed: {request.error}");
                    yield break;
                }

                string json = request.downloadHandler.text;

                MatchIdResponse response = JsonUtility.FromJson<MatchIdResponse>(json);

                dbMatchId = response.match_id;

                Debug.Log("Match ID from server: " + dbMatchId);

                InitializeMatch();
            }
        }

        #endregion
    }
}


