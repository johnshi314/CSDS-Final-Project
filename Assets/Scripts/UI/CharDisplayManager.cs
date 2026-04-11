using UnityEngine;
using UnityEngine.UI;
//using UnityEngine.UIElements;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using System.Security.Cryptography.X509Certificates;

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

    public TMP_Text name;
    public TMP_Text charDesc;
    public TMP_Text movement;
    [SerializeField]
    
    public GameObject[] abilitylist;//List of objects for displaying information of abilities

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (chardisplay != null)
        {
            chardisplay[selectedIndex].SetActive(true);//enables the first char in array 
        }
       

        UpdateCharDisplay();

    }

    // Update is called once per frame
    void Update()
    {
        
    }


    //functions for next and prev buttons
    public void NextChar()//displays the next character in the list. Loops at end
    {
        //Debug.Log("Next Selected, Now disabling" + selectedIndex);
        chardisplay[selectedIndex].SetActive(false);//disables prev char displayed
        selectedIndex = (selectedIndex+1)%chardisplay.Length;
        //Debug.Log("Now enabling" + selectedIndex);
        chardisplay[selectedIndex].SetActive(true);//enables new char for display

        UpdateCharDisplay();

    }

    public void PrevChar()//displays the next character in the list. Loops at end
    {
        //Debug.Log("Prev Selected, Now disabling" + selectedIndex);
        chardisplay[selectedIndex].SetActive(false);//disables prev char displayed
        selectedIndex--;
        if (selectedIndex < 0)
        {
            selectedIndex += chardisplay.Length;
        }
        //Debug.Log("ow enabling" + selectedIndex);
        chardisplay[selectedIndex].SetActive(true);//enables new char for display

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
}
