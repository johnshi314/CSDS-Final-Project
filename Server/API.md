# NetFlower HTTP and WebSocket API Reference

This document describes how to call the API from tools like Postman and from game clients.

**Examples** below use `<details>` blocks so they stay collapsible in Markdown preview (GitHub, VS Code, Cursor). Open the summary line to see JSON or text. Plain **` ```json `** blocks are used where a single example is short.

## Base URLs

Use one of these base URLs depending on deployment:

- Local (default): [http://localhost:8000](http://localhost:8000)
- Production (nginx): [https://litecoders.com/api](https://litecoders.com/api)

Notes:

- Routes are mounted with `API_PREFIX` when configured.
- In production, the public prefix is typically `/api`.

## Authentication

Protected endpoints require a valid JWT.

Supported auth inputs:

- HTTP `Authorization` header: `Authorization: Bearer <token>`
- HTTP cookie fallback: `auth_token` cookie

Resolution behavior in server code:

1. Read Bearer token if present.
2. Otherwise read `auth_token` cookie.
3. Reject with 401 if missing/invalid/expired.

WebSocket auth supports:

- Query parameter `authToken`, for example: `?authToken=<jwt>`
- Cookie fallback `auth_token`

## Quick Postman Rules

- For JSON endpoints: Body -> raw -> JSON
- Do not use form-data for Pydantic JSON models
- Do not wrap the whole JSON payload in quotes
- For protected endpoints, add `Authorization: Bearer <authToken>`
- Lobby control messages are JSON objects with an `action` field

## Endpoint Reference

### GET /health

- Auth required: No
- Query params: None
- JSON body: None
- Purpose: Liveness probe

<details>
<summary><strong>Example: 200 response</strong></summary>

```json
{
  "status": "ok"
}
```

</details>

---

### POST /register

- Auth required: No
- JSON body: Yes (`username`, `password`; password min 8 characters)

<details>
<summary><strong>Example: request body</strong></summary>

```json
{
  "username": "player_one",
  "password": "password123"
}
```

</details>

<details>
<summary><strong>Example: success response</strong></summary>

```json
{
  "status": "success",
  "message": "Registration successful",
  "playerId": 42,
  "authToken": "<jwt>",
  "username": "player_one"
}
```

</details>

---

### POST /login

- Auth required: No
- JSON body: Yes (`username`, `password`)

<details>
<summary><strong>Example: request body</strong></summary>

```json
{
  "username": "player_one",
  "password": "password123"
}
```

</details>

<details>
<summary><strong>Example: success response</strong></summary>

```json
{
  "status": "success",
  "message": "Login successful",
  "playerId": 42,
  "authToken": "<jwt>",
  "username": "player_one"
}
```

</details>

---

### POST /verify

- Auth required: No
- JSON body: Yes (`authToken`)

Why POST and not GET: the token is sensitive; query strings leak into logs and history.

<details>
<summary><strong>Example: request body</strong></summary>

```json
{
  "authToken": "<jwt>"
}
```

</details>

<details>
<summary><strong>Example: valid token response</strong></summary>

```json
{
  "status": "success",
  "valid": true,
  "playerId": 42
}
```

</details>

<details>
<summary><strong>Example: invalid token response</strong></summary>

```json
{
  "status": "error",
  "valid": false,
  "message": "Invalid or expired token"
}
```

</details>

---

### POST /submit-playermatchstats

- Auth required: Yes
- JSON body: Yes

<details>
<summary><strong>Example: request body</strong></summary>

```json
{
  "matchId": 123,
  "playerId": 42,
  "characterId": "Elf",
  "teamId": "RedTeam",
  "damageDealt": 200,
  "damageTaken": 95,
  "turnsTaken": 9,
  "won": true,
  "disconnected": false
}
```

</details>

`playerId` is optional; if sent, it must match the authenticated user.

---

### POST /submit-matchupstats

- Auth required: Yes
- JSON body: Yes

<details>
<summary><strong>Example: request body</strong></summary>

```json
{
  "matchId": 123,
  "characterAId": "Elf",
  "characterBId": "Harpy",
  "winnerCharacterId": "Elf"
}
```

</details>

---

### POST /submit-abilityusagestats

- Auth required: Yes
- JSON body: Yes

<details>
<summary><strong>Example: request body</strong></summary>

```json
{
  "characterId": "Elf",
  "playerId": 42,
  "damageDone": 42,
  "downtime": 1,
  "abilityName": "Arrow Rain"
}
```

</details>

---

### POST /create-match

- Auth required: Yes
- JSON body: None

<details>
<summary><strong>Example: success response</strong></summary>

```json
{
  "status": "success",
  "match_id": 456
}
```

</details>

---

### POST /update-match

- Auth required: Yes
- JSON body: Yes

<details>
<summary><strong>Example: request body</strong></summary>

```json
{
  "matchId": 123,
  "endTime": "2026-04-15T18:30:00.000Z",
  "duration": 540.5,
  "winnerTeamId": "Red"
}
```

</details>

<details>
<summary><strong>Example: success response</strong></summary>

```json
{
  "status": "success",
  "message": "Match updated successfully"
}
```

</details>

---

## WebSocket Reference

Paths below assume `WS_PREFIX=/ws` (default). In production they are under the same host as the API, e.g. `wss://litecoders.com/ws/...`.

### WS `/ws/lobby/{match_id}`

- Auth required: Yes
- Path param: `match_id`
- Auth: `?authToken=<jwt>` or `auth_token` cookie

Behavior:

- First server message is the current lobby snapshot JSON
- Server pushes subsequent lobby updates
- Server may send heartbeat JSON periodically

<details>
<summary><strong>Example: connection URL (local)</strong></summary>

```
ws://localhost:8000/ws/lobby/111?authToken=<jwt>
```

</details>

---

### WS `/ws/lobby-control`

- Auth required: Yes
- Auth: `?authToken=<jwt>` or `auth_token` cookie

Client sends JSON objects with an `action` field.

<details>
<summary><strong>Example: join new lobby</strong></summary>

Request (client -> server):

```json
{
  "action": "joinNewLobby",
  "maxPlayers": 8
}
```

Example server message:

```json
{
  "type": "joinedLobby",
  "matchId": 111
}
```

</details>

<details>
<summary><strong>Example: subscribe to lobby</strong></summary>

```json
{
  "action": "subscribeLobby",
  "matchId": 111
}
```

Server responds with `{"type":"subscribed","matchId":111}` and snapshot updates.

</details>

<details>
<summary><strong>Example: set team / ready / leave / snapshot</strong></summary>

```json
{ "action": "setTeam", "team": "red" }
```

```json
{ "action": "setReady" }
```

```json
{ "action": "leaveLobby" }
```

```json
{ "action": "snapshot" }
```

</details>

Error frame (JSON):

```json
{
  "type": "error",
  "detail": "<message>"
}
```

---

### WS `/ws/battle/{match_id}`

- Auth required: Yes
- Auth: `?authToken=<jwt>` or `auth_token` cookie

**Server -> client** (text frames, pipe-separated):

| Message | Example |
|--------|---------|
| Authenticated | `you|42` |
| Battle config accepted | `ack|start|2|30` |
| Slot claim ack | `ack|claim|0|42` |
| All slots claimed | `battle_ready` |
| New turn | `newTurn|0|5|1713201234.567` (slot, sync turn, unix end time) |
| Timer tick | `tick|0|25` (slot, seconds left) |
| Relay from peer | `relay|15|moveu|r0|3|4` |
| Error | `err|not your turn` |

**Client -> server** (text):

- `start\|<num_agents>\|<turn_seconds>`
- `claim\|<slot>` or `claim\|<slot>\|npc`
- `pass`
- `relay\|move\|<slot>\|<tx>\|<ty>` (legacy)
- `relay\|ability\|<slot>\|<idx>\|<tx>\|<ty>` (legacy)
- `relay\|moveu\|<unit_id>\|<tx>\|<ty>`
- `relay\|abilityu\|<unit_id>\|<idx>\|<tx>\|<ty>`

<details>
<summary><strong>Example: connection URL (local)</strong></summary>

```
ws://localhost:8000/ws/battle/111?authToken=<jwt>
```

</details>

<details>
<summary><strong>Example: relay lines</strong></summary>

```
relay|moveu|r0|5|7
relay|abilityu|r0|0|5|7
```

</details>

---

## Minimal cURL Examples

Local base: `http://localhost:8000` (no `/api` prefix when `API_PREFIX` is unset).

**Register:**

```bash
curl -s -X POST "http://localhost:8000/register" \
  -H "Content-Type: application/json" \
  -d '{"username":"player_one","password":"password123"}'
```

**Login:**

```bash
curl -s -X POST "http://localhost:8000/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"player_one","password":"password123"}'
```

**Verify:**

```bash
curl -s -X POST "http://localhost:8000/verify" \
  -H "Content-Type: application/json" \
  -d '{"authToken":"YOUR_JWT_HERE"}'
```

**Protected example** (Bearer token):

```bash
curl -s -X POST "http://localhost:8000/submit-playermatchstats" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_HERE" \
  -d '{"matchId":123,"characterId":"Elf","teamId":"RedTeam","damageDealt":0,"damageTaken":0,"turnsTaken":0,"won":false,"disconnected":false}'
```

If production uses path prefix `/api`, prepend it to paths (e.g. `https://litecoders.com/api/login`).
