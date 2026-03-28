using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour {
    [Header("Panels")]
    [SerializeField] private GameObject mainButtonsPanel;
    [SerializeField] private GameObject howToPlayPanel;

    [Header("Scene Settings")]
    [SerializeField] private string gameplaySceneName; 

    void Start() {
        // Ensure correct default state
        mainButtonsPanel.SetActive(true);
        howToPlayPanel.SetActive(false);
    }

    // -------------------------
    // BUTTON FUNCTIONS
    // -------------------------

    public void OnPlayPressed() {
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnHowToPlayPressed() {
        mainButtonsPanel.SetActive(false);
        howToPlayPanel.SetActive(true);
    }

    public void OnBackPressed() {
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
