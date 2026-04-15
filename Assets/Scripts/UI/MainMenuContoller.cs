using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour {
    [Header("Panels")]
    [SerializeField] private GameObject mainButtonsPanel;
    [SerializeField] private GameObject howToPlayPanel;
    [SerializeField] private GameObject playButtonsPanel;

    [Header("Scene Settings")]
    [SerializeField] private string gameplaySceneName = "GameplayTest";
    [SerializeField] private string loginSceneName = "Login";
    [SerializeField] private string characterSelectSceneName = "CharacterSelect_1";

    void Start() {
        // Ensure correct default state
        mainButtonsPanel.SetActive(true);
        howToPlayPanel.SetActive(false);
    }

    // -------------------------
    // BUTTON FUNCTIONS
    // -------------------------

    public void OnPlayPressed() {
        mainButtonsPanel.SetActive(false);
        playButtonsPanel.SetActive(true);
    }

    public void OnPlayOfflinePressed() {
        PersistentPlayerPreferences.instance.isPlayingOnline = false;
        SceneManager.LoadScene(characterSelectSceneName);
        
    }
    
    public void OnPlayOnlinePressed() {
        PersistentPlayerPreferences.instance.isPlayingOnline = true;
        SceneManager.LoadScene(loginSceneName);
        
    }

    public void OnHowToPlayPressed() {
        mainButtonsPanel.SetActive(false);
        howToPlayPanel.SetActive(true);
    }

    public void OnBackPressed() {
        playButtonsPanel.SetActive(false);
        howToPlayPanel.SetActive(false);
        mainButtonsPanel.SetActive(true);
    }

    public void OnQuitPressed() {
        Debug.Log("Quit Game");

        Application.Quit();

        // For editor testing
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
