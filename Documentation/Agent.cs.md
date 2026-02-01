# Agent.cs Class API Documentation

## Overview
The `Agent` class represents the game entity that can do actions and have actions done onto them. They have health, movement, and a lis of abilities. It uses a factory pattern for creation and provides methods for combat, movement, and status management.

## Public Properties

| Property | Type | Access | Description |
|----------|------|--------|-------------|
| `Player` | `BackendData.Player` | get | The player controlling this agent |
| `AgentName` | `string` | get | Agent's display name |
| `MaxHP` | `uint` | get | Maximum health points |
| `MaxRange` | `uint` | get | Maximum movement range per turn |
| `Abilities` | `uint[]` | get | Array of ability IDs |
| `CanTunnel` | `Tunneling` | get/set | Defines how other agents can pass through this agent |
| `HP` | `uint` | get | Current health points |

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

### `TakeDamage(int damage)`
Applies damage to the agent, reducing its HP. HP cannot go below 0.

**Parameters:**
- `damage` (int) - Amount of damage to apply

### `KOed()`
Checks if the agent is knocked out (HP <= 0).

**Returns:** `bool` - True if knocked out, false otherwise

### `ResetHP()`
Resets the agent's HP to its maximum value.


## Tunneling Struct

The `Tunneling` struct controls whether other agents can pass through this agent.

**Properties:**
- `Ally` (bool) - Whether allied agents can pass through
- `NonAlly` (bool) - Whether non-allied agents can pass through

---

## Usage Examples

### `NewAgent()` Examples

#### Creating a basic agent
```csharp
// Create a basic agent with defaults
Agent basicAgent = Agent.NewAgent();
```

#### Creating a custom agent
```csharp
// Create a custom agent
Agent customAgent = Agent.NewAgent(
    player: myPlayer,
    agent_name: "Knight",
    hp: 30,
    range: 4,
    abilities: new uint[] { 1, 2, 5 },
    position: new Vector3(10f, 0f, 5f)
);
```

#### Creating an agent as a child
```csharp
// Create an agent as a child of another GameObject
Agent childAgent = Agent.NewAgent(
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
public bool CanWalkPath(Agent agent, WalkablePath path){
    // Check each obstacle in agent's path
    foreach (Agent obstacle in path.GetAgents()) {
        bool sameTeam = GameManager.OnSameTeam(agent, obstacle);
        // Ally and obstacle blocks allies
        if (sameTeam && !obstacle.CanTunnel.Allies)
            return false;
        // Non-ally and obstacle blocks non-allies
        if (!sameTeam && !obstacle.CanTunnel.NonAllies)
            return false;
    }

    // All obstacles can be tunneled
    return true;
}
```

### Tunneling Struct Examples

```csharp
// Create an agent that blocks enemies but allows allies to pass
Agent tank = Agent.NewAgent(
    agent_name: "Tank",
    tunneling: new Tunneling(Ally: true, NonAlly: false)
);

// Modify tunneling after creation
tank.CanTunnel = new Tunneling(Ally: false, NonAlly: false); // Block everyone
```
