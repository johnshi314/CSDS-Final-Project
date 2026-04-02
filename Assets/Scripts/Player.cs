/***********************************************************************
 * File Name     : Player.cs
 * Author        : Mikey Maldonado
 * Date Created  : 2026-01-31
 * Description   : Data structure representing a player in the game.
 **********************************************************************/
using UnityEngine;

namespace NetFlower {
    public class Player {
        public int Id { get; set; }   // Unique identifier for the player
        public string Name { get; set; } // Display name of the player
        public string IP { get; set; }   // IP address of the player
        public int elo {  get; set; } //player elo
        public Player(int Id, string Name, string IP) {
            this.Id = Id;
            this.Name = Name;
            this.IP = IP;
        }
    }
}
