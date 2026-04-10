using UnityEngine;
using UnityEngine.Networking;
using NetFlower;

public class PersistentPlayerPreferences : MonoBehaviour
{
    public static PersistentPlayerPreferences instance;
    public Player player;
    public bool isPlayingOnline = false;
    public string characterName; // characterName and characterId are both stored since it's currently undefined which will be used  
    public int characterId;     //  to generate characters when loading a map
    public string authToken;

    public void Awake() {
        if (instance != null) {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

    }

}
