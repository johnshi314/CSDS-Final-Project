/***********************************************************************
 * File Name     : Player.cs
 * Author        : Mikey Maldonado
 * Date Created  : 2026-01-31
 * Description   : Data structure representing a player in the backend.
 **********************************************************************/
using UnityEngine;

namespace Backend.Data {
    public class Player {
        public string ID { get; private set; }   // Unique identifier for the player
        public string Name { get; private set; } // Display name of the player
        public string IP { get; private set; }   // IP address of the player
    }
}
