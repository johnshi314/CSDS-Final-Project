using NetFlower;
using UnityEngine;

public class ClientPlayer : MonoBehaviour
{
    public static ClientPlayer PersistentInstance = null;
    public static Player clientPlayer; // Reference to the Player data structure representing the client player
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (PersistentInstance != null) {
            Destroy(this.gameObject); // Ensure only one instance exists
            return;
        } else {
            PersistentInstance = this.gameObject.GetComponent<ClientPlayer>();
            DontDestroyOnLoad(this.gameObject); // Keep this object alive between scenes
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PressJoinLobby() {
        // Go to "Lobby" scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
    }

    public static ClientPlayer GetInstance() {
        return PersistentInstance;
    }

}
