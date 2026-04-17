using UnityEngine;
using UnityEngine.UI;
//using UnityEngine.UIElements;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/*
Sara Wang
CharDisplayManager.cs

Displays information about a character in Character Selection Screen
Rotates between different characters in a carosel and updates the appropriate UI elements with their information
*/

public class CharDisplayManager : MonoBehaviour
{
    public GameObject[] chardisplay;//list of character objects in the carosel. Must contain the CharDisplayInfo component
    private int selectedIndex = 0;
    //public GameObject test; 
    //public Color[] colors;

    public new TMP_Text name;
    public TMP_Text charDesc;
    public TMP_Text movement;
    public Button readyButton;
    public GameObject lockedCharText; 

    [SerializeField]
    private GameObject[] abilitylist;//List of objects for displaying information of abilities


    //Grid group
    [SerializeField]
    private GameObject grid;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ShowActiveCharacter();
       
        UpdateCharDisplay();

        
        //update grid objects with corresponding information
        int childCount = grid.transform.childCount;
        for(int i = 0; i < childCount; i++)
        {
            
                Transform charIcon = grid.transform.GetChild(i);
                Transform charIconIcon = charIcon.transform.GetChild(0);
                Transform charIconText = charIcon.transform.GetChild(1);
                //Debug.Log(charIcon.name);
                //Debug.Log(charIconText.name);
                Image iconImage = charIconIcon.GetComponent<Image>();
                TMP_Text iconName = charIconText.GetComponent<TMP_Text>();

                CharDisplayInfo charinfo = chardisplay[i].GetComponent<CharDisplayInfo>();

                if (charinfo != null && iconName!= null)
                {
                    iconImage.color = charinfo.colors;
                    iconName.text = charinfo.char_name;

                }
            


        }
    }


    //functions for next and prev buttons
    public void NextChar()//displays the next character in the list. Loops at end
    {
        selectedIndex = (selectedIndex+1)%chardisplay.Length;
        ShowActiveCharacter();

        UpdateCharDisplay();

    }

    public void PrevChar()//displays the next character in the list. Loops at end
    {
        selectedIndex--;
        if (selectedIndex < 0)
        {
            selectedIndex += chardisplay.Length;
        }
        ShowActiveCharacter();

        UpdateCharDisplay();
    }    

    //function to start game goes here

    public void UpdateCharDisplay()
    {
        CharDisplayInfo charinfo = chardisplay[selectedIndex].GetComponent<CharDisplayInfo>();
        if (charinfo != null)
        {
            name.text = charinfo.char_name;
            charDesc.text = charinfo.char_desc;
            movement.text = "Movement: " + charinfo.movement;
            int index = 0;
            foreach(GameObject ability in abilitylist)
            {
                TMP_Text text = ability.transform.Find("Ability Text").GetComponent<TMP_Text>();
                if(text != null & index<abilitylist.Length)
                {
                    if (index<charinfo.ability_desc.Length)
                    {
                        text.text= charinfo.ability_desc[index];
                        ability.SetActive(true);
                    }
                    else
                    {   
                        ability.SetActive(false);
                    }
                    
                }
                
                Image icon = ability.transform.Find("Image").GetComponent<Image>();
                icon.color = charinfo.colors;//placeholder for image replacement
                index++;

                
            }

            if (charinfo.characterunlocked==false)
            {
                //Debug.Log("AAAAAAAAAAAAA");
                readyButton.enabled=false;
                lockedCharText.SetActive(true);
            }
            else
            {
                //Debug.Log("5555555555");
                readyButton.enabled=true;
                lockedCharText.SetActive(false);
            }
        }
    }

    public void UpdateCharDisplay(int selection)
    {   
        selectedIndex = selection;

        //Error Trapping to loop round if there is an invalid index input
        if (selectedIndex < 0)
        {
            selectedIndex += chardisplay.Length;
        }
        else if (selectedIndex>=chardisplay.Length)
        {
            selectedIndex = (selectedIndex+1)%chardisplay.Length;
        }

        ShowActiveCharacter();//enables selected and disables all others
        CharDisplayInfo charinfo = chardisplay[selectedIndex].GetComponent<CharDisplayInfo>();
        if (charinfo != null)
        {
            if (charinfo.characterunlocked!=false)
            {
                readyButton.enabled=false;
                lockedCharText.SetActive(true);
            }
            else
            {
                readyButton.enabled=true;
                lockedCharText.SetActive(false);
                
            }

            name.text = charinfo.char_name;
            charDesc.text = charinfo.char_desc;
            movement.text = "Movement: " + charinfo.movement;
            int index = 0;
            foreach(GameObject ability in abilitylist)
            {
                TMP_Text text = ability.transform.Find("Ability Text").GetComponent<TMP_Text>();
                if(text != null & index<abilitylist.Length)
                {
                    if (index<charinfo.ability_desc.Length)
                    {
                        text.text= charinfo.ability_desc[index];
                        ability.SetActive(true);
                    }
                    else
                    {   
                        ability.SetActive(false);
                    }
                    
                }
                
                Image icon = ability.transform.Find("Image").GetComponent<Image>();
                icon.color = charinfo.colors;//placeholder for image replacement
                index++;

                
            }

         if (charinfo.characterunlocked==false)
            {
                //Debug.Log("AAAAAAAAAAAAA");
                readyButton.enabled=false;
                lockedCharText.SetActive(true);
            }
            else
            {
                //Debug.Log("5555555555");
                readyButton.enabled=true;
                lockedCharText.SetActive(false);
            }   
        }
    }



    public void OnReady() {

        PersistentPlayerPreferences.instance.characterName = chardisplay[selectedIndex].GetComponent<CharDisplayInfo>().internal_name;
        PersistentPlayerPreferences.instance.characterId = chardisplay[selectedIndex].GetComponent<CharDisplayInfo>().char_id;
        if (PersistentPlayerPreferences.instance.isPlayingOnline) {
            SceneManager.LoadScene("Lobby");
        } else {
            SceneManager.LoadScene("GameplayTest");
        }
    }

    private void ShowActiveCharacter() {
        if (chardisplay == null || chardisplay.Length == 0) return;

        // Keep selectedIndex valid before applying active states.
        if (selectedIndex < 0) selectedIndex = 0;
        if (selectedIndex >= chardisplay.Length) selectedIndex = chardisplay.Length - 1;

        // Disable all other displays
        CharDisplayInfo[] allDisplays = FindObjectsByType<CharDisplayInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var allowedRoots = new HashSet<GameObject>();
        for (int i = 0; i < chardisplay.Length; i++) {
            if (chardisplay[i] != null) allowedRoots.Add(chardisplay[i]);
        }
        foreach (var info in allDisplays) {
            if (info != null && info.gameObject != null && allowedRoots.Contains(info.gameObject)) {
                info.gameObject.SetActive(false);
            }
        }

        // Enable only the selected display
        if (chardisplay[selectedIndex] != null) {
            chardisplay[selectedIndex].SetActive(true);
        }
    }
}
