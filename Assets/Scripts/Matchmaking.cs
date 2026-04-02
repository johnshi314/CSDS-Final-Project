using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetFlower {
    public class Matchmaking {
        private List<Player> queue = new List<Player>();

        private float baseRange = 50f;
        private float expansionRate = 20f;
        private float currentRange;

        private int playersPerMatch = 2;

        public Matchmaking(float baseRange = 50f, float expansionRate = 20f, int playersPerMatch = 2) {
            this.baseRange = baseRange;
            this.expansionRate = expansionRate;
            this.playersPerMatch = playersPerMatch;

            currentRange = baseRange;
        }

        public void AddPlayer(Player player) {
            queue.Add(player);
        }

        public List<List<Player>> TryMatch() {
            List<List<Player>> matches = new List<List<Player>>();
            HashSet<Player> used = new HashSet<Player>();

            // Sort by elo
            queue.Sort((a, b) => a.elo.CompareTo(b.elo));

            for (int i = 0; i < queue.Count; i++) {
                var p1 = queue[i];
                if (used.Contains(p1)) continue;

                List<Player> group = new List<Player> { p1 };

                // look at players after current player in list and create group if elo is in range and not yet used
                for (int j = i + 1; j < queue.Count; j++) {
                    var p2 = queue[j];
                    if (used.Contains(p2)) continue;

                    if (Math.Abs(p1.elo - p2.elo) <= currentRange) {
                        group.Add(p2);
                    }

                    if (group.Count == playersPerMatch)
                        break;
                }

                // add group to match list if it contains correct number of players per match
                if (group.Count == playersPerMatch) {
                    matches.Add(group);
                    foreach (var p in group)
                        used.Add(p);
                }
            }

            // Remove used players from queue
            queue.RemoveAll(p => used.Contains(p));

            // Adjust range
            if (matches.Count == 0) {
                // expand range if no matches found
                currentRange += expansionRate;
            } else {
                // keep current range if matches found
                currentRange = baseRange;
            }

            // return list of matched groups
            return matches;
        }
    }
}
