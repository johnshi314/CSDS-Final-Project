/***********************************************************************
* File Name     : TurnOrder.cs
* Author        : Roberto Matthews (template credit: Mikey Maldonado)
* Date Created  : 05 February 2026
* Description   : Contains turn representation and turn management.
**********************************************************************/
using System;
using System.Collections.Generic;
using NetFlower;
using Mono.Cecil.Cil;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
/// Placeholder
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

    /// <summary>
    /// The defined order of turns managed by the game on a turn-by-turn basis.
    /// Subject to change, should not be considered final unless the game's rules are.
    /// </summary>
    public class TurnOrder {
        public Step startStep;      //  Any events that occur at the start of the turn.
        public Phase startPhase;    //  The turn begins.
        public Step actionStep;     //  The team decides what abilities they will use and in what order (time limited).
        public Step confirmStep;    //  The actions of the team are ordered and confirmed for translation into events.
        public Phase mainPhase;     //  The team acts.
        public Step resolveStep;    //  Resolve the actions confirmed in order as events.
        public Phase resolutionPhase;// The teams actions resolve.
        public Step endStep;        //  Any events that occur after resolution but before turn change.
        public Phase endPhase;      // The turn ends and (directly afterward) the next turn begins.
        public Turn orderedTurn;    // The turn defined with the above Steps and Phases
        public static TurnOrder DefaultTurnOrderFactory() {
            TurnOrder turnOrder = new TurnOrder();
            // Start Phase
            turnOrder.startStep = Step.StepFactory("Start Step", "", StepEvent.turnStartEvent);
            List<Step> startPhaseSteps = new List<Step>();
            startPhaseSteps.Add(turnOrder.startStep);
            turnOrder.startPhase = Phase.PhaseFactory("Start Phase", "", startPhaseSteps);
            // Main Phase
            turnOrder.actionStep = Step.StepFactory("Action Step", "", StepEvent.teamActionsEnableEvent);
            turnOrder.confirmStep = Step.StepFactory("Confirm Step", "", StepEvent.teamActionsConfirmEvent);
            List<Step> mainPhaseSteps = new List<Step>();
            mainPhaseSteps.Add(turnOrder.actionStep);
            mainPhaseSteps.Add(turnOrder.confirmStep);
            turnOrder.mainPhase = Phase.PhaseFactory("Main Phase", "", mainPhaseSteps);
            // Resolution Phase
            turnOrder.resolveStep = Step.StepFactory("Resolve Step", "", StepEvent.teamActionsResolveEvent);
            List<Step> resolutionPhaseSteps = new List<Step>();
            resolutionPhaseSteps.Add(turnOrder.resolveStep);
            turnOrder.resolutionPhase = Phase.PhaseFactory("Resolution Phase", "", resolutionPhaseSteps);
            // End Phase
            turnOrder.endStep = Step.StepFactory("Resolution Phase", "", StepEvent.turnEndEvent);
            List<Step> endPhaseSteps = new List<Step>();
            endPhaseSteps.Add(turnOrder.endStep);
            turnOrder.endPhase = Phase.PhaseFactory("Resolution Phase", "", endPhaseSteps);
            return turnOrder;
        }
    }

    /// <summary>
    /// The components of a single turn that will construct the turn order.
    /// </summary>
    public class Turn {
        public Team actingTeam{ get; private set; }         // The team that owns this turn, "Whose turn is it"
        public uint turnNumber{ get; private set; }         // The current turn, "Which turn is it?"
        public List<Phase> phases{ get; private set; }      // The ordered parts of the turn "What can I do now?"
        public static Turn TurnFactory(Team actingTeam, uint turnNumber, List<Phase> phases) {
            Turn turn = new Turn();
            turn.actingTeam = actingTeam;
            turn.turnNumber = turnNumber;
            turn.phases = phases;
            return turn;
        }
    }
    // TODO: Couldn't find a clean way to handle ending turns, 
    // but I believe that is something TurnOrder.cs must handle.

    /// <summary>
    /// A logcial subdivision of a turn.
    /// A phase is a representation of a part of a turn.
    /// It orders related events (Steps) into a discrete logical flow.
    /// </summary>
    public class Phase {
        public String name;
        public String description;      // logical description of the phase's purpose
        public List<Step> steps;        // technical seperation of phase's functions
        public static Phase PhaseFactory(String name, String description, List<Step> steps) {
            Phase phase = new Phase();
            phase.name = name;
            phase.description = description;
            phase.steps = steps;
            return phase;
        }
    }

    /// <summary>
    /// An atomized subdivision of a step.
    /// A step is a representation of an event that can happen during a turn.
    /// It belongs to a phase in the context of it's surrounding steps.
    /// </summary>
    public class Step {
        public String name;
        public String description;      // description of the steps event
        public StepEvent stepEvent;     // facilitate the triggering of the events
        public static Step StepFactory(String name, String description, StepEvent stepEvent) {
            Step step = new Step();
            step.name = name;
            step.description = description;
            step.stepEvent = stepEvent;
            return step;
        }
    }
        // TODO: Determine how to govern and/or restrict events by/to steps.
        // For example, when the turn becomes the ActionStep, the players should be allowed to take actions.
        // Then, this permission should be revoked when the step ends.
        // Would prefer for this to just be a (series of) call(s) that the game manager listens for and/or handles directly.

    public enum StepEvent {
        turnStartEvent,             // any start of turn events (startStep)
        teamActionsEnableEvent,    // a team is enabled to act during (actionStep)
        teamActionsConfirmEvent,   // accept a team's actions and order of action (confirmationStep)
        teamActionsResolveEvent,    // resolve the team actions in order (resolutionStep)
        turnEndEvent                // any end of turn events (endStep)
    }
}