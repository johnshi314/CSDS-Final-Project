using UnityEngine;

namespace NetFlower {
    /// <summary>
    /// Manages turns during a game.
    /// </summary>
    public class TurnManager : MonoBehaviour {
         // Start is called once before the first execution of Update after the MonoBehaviour is created
        static void Start() { }

        // Update is called once per frame
        static void Update() { }

        // mxm166667: Suggested adding the mapManager for top-level context management.
        // [Header("References")]
        // public MapManager mapManager;

        // mxm166667: Commented out for now as it is not yet implemented.
        // TODO: Figure out the proper way to tick effects and let agents know what turn it is (or take full control in this class?)
        // I think it is okay to let Agents keep track of their own effects now as long as the turnorder is the only one handling when those calls occur.
        // I'm open to suggestions on how to handle this.
        // /// <summary>
        // /// Transition to the next turn.
        // /// </summary>
        // public void toNextTurn() { 
        //     if (mapManager == null || !mapManager.HasActiveMap) return;
        //     var map = mapManager.ActiveMap;
        //     int turnNumber = currentTurn != null ? (int)currentTurn.turnNumber : 0;
        //     map.TickEffects(turnNumber); // tile-bound (Terrain) effects
        //     foreach (var agent in map.GetRegisteredAgents())
        //         agent.TickEffects(turnNumber); // agent-bound (Damage/Heal/Status) effects
        // }

        [Header("Current State")]
        public Turn currentTurn{ get; private set; }    // The current turn. Can proceed to a new turn.
        public Phase currentPhase{ get; private set; }  // The current phase. Can proceed to the next in turn.
        public Step currentStep{ get; private set; }    // The current step. Can proceed to the next in phase.
        public void toNextTurn(){}          //  Transition to the next turn.   
        public void toNextPhase(){}         //  Transition to the next phase.
        public void toNextStep(){}          //  Transition to the next step.
    }
}
