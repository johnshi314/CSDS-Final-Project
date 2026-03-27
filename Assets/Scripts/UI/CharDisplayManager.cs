using UnityEngine;
/*
Sara Wang
CharDisplayManager.cs

Displays information about a character in Character Selection Screen

*/
public class CharDisplayManager : MonoBehaviour
{
    public GameObject[] chardisplay;
    private int selectedIndex = 0;
    public GameObject test;
    public Color[] colors;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (chardisplay != null)
        {
            chardisplay[selectedIndex].SetActive(true);//enables the first char in array 
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    //functions for next and prev buttons
    public void NextChar()//displays the next character in the list. Loops at end
    {
        Debug.Log("Next Selected, Now disabling" + selectedIndex);
        chardisplay[selectedIndex].SetActive(false);//disables prev char displayed
        selectedIndex = (selectedIndex+1)%chardisplay.Length;
        Debug.Log("Now enabling" + selectedIndex);
        chardisplay[selectedIndex].SetActive(true);//enables new char

    }

    public void PrevChar()//displays the next character in the list. Loops at end
    {
        Debug.Log("Prev Selected, Now disabling" + selectedIndex);
        chardisplay[selectedIndex].SetActive(false);//disables prev char displayed
        selectedIndex--;
        if (selectedIndex < 0)
        {
            selectedIndex += chardisplay.Length;
        }
        Debug.Log("ow enabling" + selectedIndex);
        chardisplay[selectedIndex].SetActive(true);//enables new char

    }    

    //function to start game goes here
}
