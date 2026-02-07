/***********************************************************************
* File Name     : Tile.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-05
* Description   : Data structures representing tiles on a map.
**********************************************************************/
using UnityEngine;
using GameData;

namespace GameMap {
    
    /// <summary>
    /// Struct representing the logical position of a tile on the grid.
    /// </summary>
    public struct GridPos { public int X, Y; }

    /// <summary>
    /// Enum representing different types of tiles.
    /// </summary>
    public enum TileType {
        Walkable,   // Walkable tile
        Obstacle    // Non-walkable tile
    }

    public class Tile {
        public TileType Type;
        public Agent Occupant; // The optional agent occupying this tile
    }
}
