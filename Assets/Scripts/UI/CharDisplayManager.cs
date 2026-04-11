using UnityEngine;
using UnityEngine.UI;
//using UnityEngine.UIElements;
using TMPro;
using System.Collections;

using UnityEngine.SceneManagement;

/*
Sara Wang
CharDisplayManager.cs

Displays information about a character in Character Selection Screen
Rotates between different characters in a carosel and updates the appropriate UI elements with their information
*/

public class CharDisplayManager : MonoBehaviour
{
    public GameObject[] chardisplay;//list of character Gameobjects in the carosel. Must contain the CharDisplayInfo component
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

    public void UpdateCharDisplay(int charindex)
    {
        chardisplay[selectedIndex].SetActive(false);
        selectedIndex = charindex;
        

        //input should always be within range for this to work properly, but in case it is not, these will loop it round
            if (selectedIndex < 0)
            {
                selectedIndex += chardisplay.Length;
            }
            if(selectedIndex >= chardisplay.Length)
            {
                selectedIndex = (selectedIndex+1)%chardisplay.Length;
            }
            
        //updates displayed character in carosel
        chardisplay[selectedIndex].SetActive(true);

        CharDisplayInfo charinfo = chardisplay[selectedIndex].GetComponent<CharDisplayInfo>();
        //updates character information display
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

    public void DisplayChar(int index)
    {
        UpdateCharDisplay(index);
    }

    public void StartGame()
    {
        SceneManager.LoadScene("GameplayTest");
        //selected character = chardisplay[selectedIndex];
        //Somehow have the selected character here correspond to the selected character in the game.
    }
}
