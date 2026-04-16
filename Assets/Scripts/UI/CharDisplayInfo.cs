using UnityEngine;
using UnityEngine.UI;

/*
Sara Wang
CharDisplayInfo.cs

Stores information about each character such as states and images for any UI displays

*/

public class CharDisplayInfo : MonoBehaviour
{
    //character Info
    public string char_name;
    public string internal_name;
    [TextArea(6,15)]
    public string char_desc;
    public int char_id;
    public Image char_icon;
    public Color colors; //placeholder for character image and ability icons
    
    //Stats and abilities
    public int movement;
    
    [SerializeField]
    public bool characterunlocked;

    public string[] ability_name;
    [TextArea(6,15)]
    public string[] ability_desc;
    public Sprite[] ability_icons;

}
