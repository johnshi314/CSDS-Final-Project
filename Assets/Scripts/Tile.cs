/***********************************************************************
* File Name     : Tile.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-05
* Description   : Data structures representing tiles on a map.
**********************************************************************/
using UnityEngine;
using System;

namespace NetFlower {
    /// <summary>
    /// Enum representing different types of tiles.
    /// </summary>
    public enum TileType {
        Walkable,   // Walkable tile
        Obstacle    // Non-walkable tile
    }

    [Serializable]
    public class Tile {
        public Map Map { get; private set; } // Reference to the map this tile belongs to
        public Vector2Int Position { get; private set; } // The (x, y) position of the tile on the map
        public TileType Type { get; private set; } // The type of the tile (walkable or obstacle)
        public bool IsWalkable {
            get {
                return Type == TileType.Walkable;
            }
        }
        public Tile(Map map, Vector2Int position, bool isWalkable) {
            this.Map = map;
            this.Position = position;
            this.Type = isWalkable ? TileType.Walkable : TileType.Obstacle;
        }
    }
}
