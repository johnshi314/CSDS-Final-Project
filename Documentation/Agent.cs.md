# Agent.cs Class API Documentation

## Overview
The `Agent` class represents the game entity that can do actions and have actions done onto them. They have health, movement, and a lis of abilities. It uses a factory pattern for creation and provides methods for combat, movement, and status management.

## Public Properties

| Property | Type  | Description |
|----------|------|-------------|
| `Player` | `BackendData.Player` | The player controlling this agent |
| `AgentName` | `string` | Agent's display name |
| `MaxHP` | `uint` | Maximum health points |
| `MaxRange` | `uint` | Maximum movement range per turn |
| `CanTunnel` | `Tunneling` | How other agents can pass through this agent |
| `Abilities` | `uint[]` | Array of ability IDs |
| `HP` | `uint` | Current health points |

## Static Factory Method

### `NewAgent()`
Creates a new Agent GameObject with specified properties.

**Parameters:**
- `player` (BackendData.Player) - The player controlling this agent (default: null)
- `agent_name` (string) - Display name (default: "MissingNo.")
- `hp` (uint) - Maximum health points (default: 20)
- `range` (uint) - Maximum movement range (default: 3)
- `abilities` (uint[]) - Array of ability IDs (default: null)
- `tunneling` (Tunneling) - Tunneling capabilities (default: Ally=true, NonAlly=false)
- `gameObjectName` (string) - GameObject name (default: auto-generated)
- `parent` (GameObject) - Parent object (default: null)
- `position` (Vector3) - Initial position (default: Vector3.zero)

**Returns:** The newly created `Agent` component

## Public Methods

### `OnTurnStart()`
A method that should be called at the start of the agent’s turn.

### `OnTurnEnd()`
A method that should be called at the end of the agent’s turn.

### `TakeDamage(int damage)`
Applies damage to the agent, reducing its HP. HP cannot go below 0.

**Parameters:**
- `damage` (int) - Amount of damage to apply

### `Heal(int amount)`
Increase agent’s current HP by the amount up to agent’s maximum HP.

**Parameters:**
- `amount` (int) - Amount of healing to apply


### `KOed()`
Checks if the agent is knocked out (HP <= 0).

**Returns:** `bool` - True if knocked out, false otherwise

### `ResetHP()`
Resets the agent's HP to its maximum value.

### `CanUseAbility(Ability ability)`
Checks if the ability is not on cooldown.

**Parameters:**
- `ability` (Ability) - Ability of this current agent.

**Returns:** `bool` - True if the current cooldown of the ability is 0.

### `UseAbility(Ability ability, Tile target)`
Applies the effects of the ability on the specified target.

**Parameters:**
- `ability` (Ability) - Ability of this current agent.
- `target` (Tile) - The tile the ability will target.


## Tunneling Enum (Flags)

The `Tunneling` struct controls whether other agents can pass through this agent.

**Properties:**
- `Ally` (1 << 0) - Whether allied agents can pass through
- `NonAlly` (1 << 1) - Whether non-allied agents can pass through

---

## Usage Examples

### `NewAgent()` Examples

```csharp
// Create a GameObject with default Agent component
GameObject basicAgent = Agent.NewAgent(agent_name: "New Agent");

// Create a GameObject, with Agent component, as a child of another GameObject
GameObject childAgent = Agent.NewAgent(
    agent_name: "Scout",
    parent: parentGameObject,
    position: new Vector3(5f, 0f, 5f)
);
```

### `TakeDamage()` Examples

```csharp
Agent enemy = Agent.NewAgent(agent_name: "Goblin", hp: 15);
enemy.TakeDamage(5);  // HP is now 10
enemy.TakeDamage(20); // HP is now 0 (not negative)
```

### `KOed()` Examples

```csharp
Agent fighter = Agent.NewAgent(hp: 10);
fighter.TakeDamage(5);
if (!fighter.KOed()) {
    Debug.Log("Fighter is still standing!");
}

fighter.TakeDamage(10);
if (fighter.KOed()) {
    Debug.Log("Fighter is knocked out!");
}
```

### `ResetHP()` Examples

```csharp
Agent healer = Agent.NewAgent(hp: 20);
healer.TakeDamage(15); // HP is now 5
healer.ResetHP(); // HP is back to 20
```

### `CanTunnel` Examples

```csharp
// Ally and obstacle blocks allies
if (SameTeam(me, other) && !(other.CanTunnel & Tunneling.Ally))
Debug.Log("I can't move past my ally")
// Non-ally and obstacle blocks non-allies
if (!SameTeam(me, other) && !(other.CanTunnel & Tunneling.NonAlly))
Debug.Log("I can't move past this enemy")
```

### Tunneling Examples

```csharp
// Make new agent with ally tunnelling
Agent tank = Agent.NewAgent(tunneling: Tunneling.Ally).GetComponent<Agent>();
tank.CanTunnel = Tunneling.Nothing;    // No one can tunnel
tank.CanTunnel = Tunneling.Everything; // Everyone can tunnel
tank.CanTunnel = Tunneling.NonAlly;    // Enemies can tunnel
```
