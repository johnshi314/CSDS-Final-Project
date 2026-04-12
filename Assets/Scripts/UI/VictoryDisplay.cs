using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

namespace NetFlower.UI {
    /// <summary>
    /// Displays the winning team text on the victory scene.
    /// Looks for TextMeshPro objects named "RedTeamWin" and "BlueTeamWin".
    /// </summary>
    public class VictoryDisplay : MonoBehaviour {
        private TextMeshProUGUI redTeamWinText;
        private TextMeshProUGUI blueTeamWinText;

        void Start() {
            // Find the TextMeshPro objects by name
            redTeamWinText = GameObject.Find("RedTeamWin")?.GetComponent<TextMeshProUGUI>();
            blueTeamWinText = GameObject.Find("BlueTeamWin")?.GetComponent<TextMeshProUGUI>();

            if (redTeamWinText == null) {
                Debug.LogError("[VictoryDisplay] Could not find 'RedTeamWin' TextMeshProUGUI object!");
            }
            if (blueTeamWinText == null) {
                Debug.LogError("[VictoryDisplay] Could not find 'BlueTeamWin' TextMeshProUGUI object!");
            }

            // Get the winning team from PlayerPrefs
            if (PlayerPrefs.HasKey("WinningTeam")) {
                string winningTeam = PlayerPrefs.GetString("WinningTeam");
                DisplayVictory(winningTeam);
                Debug.Log($"[VictoryDisplay] Displaying victory for {winningTeam} team");
            } else {
                Debug.LogWarning("[VictoryDisplay] No winning team found in PlayerPrefs!");
            }
        }

        /// <summary>
        /// Shows the appropriate victory text based on the winning team.
        /// </summary>
        private void DisplayVictory(string winningTeam) {
            if (winningTeam == "Red") {
                // Show Red team win, hide Blue team win
                if (redTeamWinText != null) {
                    redTeamWinText.gameObject.SetActive(true);
                }
                if (blueTeamWinText != null) {
                    blueTeamWinText.gameObject.SetActive(false);
                }
            } else if (winningTeam == "Blue") {
                // Show Blue team win, hide Red team win
                if (redTeamWinText != null) {
                    redTeamWinText.gameObject.SetActive(false);
                }
                if (blueTeamWinText != null) {
                    blueTeamWinText.gameObject.SetActive(true);
                }
            }
        }

        // Let player go back to main menu after win screen
        public void OnBackToMenuPressed() {
            SceneManager.LoadScene("MainMenu");
        }

    }
}
