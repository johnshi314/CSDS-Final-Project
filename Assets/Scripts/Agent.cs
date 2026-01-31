using UnityEngine;


public class Agent {
    // Flags for how other agents can pass through this agent
    public struct Tunneling {
        public bool ally;
        public bool non_ally;
    }

    // Data
    int _hp;     // health points
    int _range;  // for movement
    int[] _abilities; // list of ability IDs
    Tunneling _tunneling;

    // Initializer
    public Agent(int hp = 20,
                int range = 3,
                int[] abilities,
                Tunneling tunneling = new Tunneling(){
                    ally=false,
                    non_ally=false
                    }) {
        this.hp = hp;
        this.range = range;
        this.abilities = abilities;
        this._tunneling = tunneling;
    }
    

    public int[] abilities {
        get {
            return this._abilities;
        }
    }

    public int hp {
        get {
            return this._hp;
        }
    }

    public void takeDamage(int damage) {
        this._hp -= damage;
        if (this._hp < 0){
            this._hp = 0;
        }
    }

    
}
