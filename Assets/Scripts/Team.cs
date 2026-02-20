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
        public readonly string Id;              // Unique identifier for the team
        public readonly string Name;            // Display name of the team
        public readonly TeamColor TeamColor;    // If they are a Red or Blue team
        private List<Agent> members;            // Agents in this team
        public IReadOnlyList<Agent> Members => members; // Readonly access to team members

        public Team(string id, TeamColor teamColor, IEnumerable<Agent> members) {
            this.Id = id;
            this.TeamColor = teamColor;
            this.members = new List<Agent>(members);
        }
    }
}