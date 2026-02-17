/***********************************************************************
* File Name     : Team.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-05
* Description   : Data structures representing teams in the game.
**********************************************************************/
using UnityEngine;
using System.Collections.Generic;

namespace NetFlower {
    public enum TeamColor {
        Red,
        Blue
    }
    public class Team {
        public string Id { get; private set; }  // Unique identifier for the team
        public string Name;                     // Display name of the team
        public TeamColor TeamColor;             // If they are a Red or Blue team
        public HashSet<Agent> Members;          // Agents in this team

        public Team(string id, TeamColor teamColor, IEnumerable<Agent> members) {
            this.Id = id;
            this.TeamColor = teamColor;
            this.Members = new HashSet<Agent>(members);
        }
    }
}