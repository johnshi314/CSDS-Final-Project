using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements; 
using UnityEditor.UIElements;

/*
Sara Wang
CharDisplayInfo.cs

Stores information about each character such as states and images for any UI displays

*/

public class CharDisplayInfo : MonoBehaviour 
{
    //character Info
    public string char_name;
    [TextArea(6,15)]
    public string char_desc;
    public Image char_icon;
    public Color colors; //placeholder for character image and ability icons
    
    //Stats and abilities
    public int movement;
    

    public string[] ability_name;
    [TextArea(6,15)]
    public string[] ability_desc;
    public Sprite[] ability_icons;

}
