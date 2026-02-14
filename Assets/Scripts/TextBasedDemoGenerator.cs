using UnityEngine;
using GameData;
using GameMap;
using System.Runtime.InteropServices;

public class TextBasedDemoGenerator : MonoBehaviour {
    private GameObject map;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        /*Generate a 2x1 Map with 2 tiles and spawnpoints at A and B
         *  [ A ] [ B ]
         */
        bool[,] tiles = { { true, true }, { true, true } }; // Tiles (Where each entry in the outer array is a row of tiles represented by an array of bools)
        Vector2Int[] spawnpoints = { Vector2Int.CeilToInt(new Vector2(0,0)), Vector2Int.CeilToInt(new Vector2(1,0)) }; // Spawnpoints (Zero-indexed)
        map = Map.NewMap(
            "TextBasedDemo", // Name
            tiles, 
            spawnpoints); 

        // Register Agents to map
        



    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
