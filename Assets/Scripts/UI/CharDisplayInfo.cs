using UnityEngine;
using Unity.UI; 
/*
Sara Wang
CharDisplayInfo.cs

Stores information about each character such as states and images for any UI displays

*/

public class CharDisplayInfo : MonoBehaviour
{
    public string char_name;
    public string char_desc;
    
    
    //Stats and abilities
    public int movement;

    public string[] ability_name;
    public string[] ability_desc;
    public Sprite[] ability_icons;

}
