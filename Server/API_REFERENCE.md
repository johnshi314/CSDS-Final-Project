# NetFlower HTTP and WebSocket API Reference

This document describes how to call the API from tools like Postman and from game clients.

## Base URLs

Use one of these base URLs depending on deployment:

- Local (default): http://localhost:8000
- Production (nginx): https://litecoders.com/api

Notes:

- Routes are mounted with API_PREFIX when configured.
- In production, the public prefix is typically /api.

## Authentication

Protected endpoints require a valid JWT.

Supported auth inputs:

- HTTP Authorization header: Authorization: Bearer <authToken>
- HTTP cookie fallback: auth_token cookie

Resolution behavior in server code:

1. Read Bearer token if present.
2. Otherwise read auth_token cookie.
3. Reject with 401 if missing/invalid/expired.

WebSocket auth supports:

- Query parameter authToken, for example: ?authToken=<jwt>
- Cookie fallback auth_token

## Quick Postman Rules

- For JSON endpoints: Body -> raw -> JSON
- Do not use form-data for Pydantic JSON models
- Do not wrap the whole JSON payload in quotes
- For protected endpoints, add Authorization header with Bearer authToken*
- Lobby endpoints now take JSON request bodies rather than query strings


## Endpoint Reference

### GET /health

- Auth required: No
- Query params: None
- JSON body: None
- Purpose: Liveness probe

Response example:

{
  "status": "ok"
}

### POST /register

- Auth required: No
- Query params: None
- JSON body required: Yes

Request JSON:

{
  "password": "string_min_8_chars"
}

Success response fields:

- status
- message
- playerId
- authToken

### POST /login

- Auth required: No
- Query params: None
- JSON body required: Yes

Request JSON:

{
  "playerId": 15,
  "password": "your_password"
}

Success response fields:

- status
- message
- playerId
- authToken

### POST /verify

- Auth required: No
- Query params: None
- JSON body required: Yes

Request JSON:

{
  "authToken": "jwt_here"
}

Response fields:

- status
- valid
- playerId (when valid)

Why POST and not GET: the authToken is sensitive. Sending it in a GET query string increases risk of token leakage in logs and browser history.

### POST /submit-playermatchstats

- Auth required: Yes
- Query params: None
- JSON body required: Yes

Request JSON fields:

- matchId
- playerId (optional for client; server validates if present and enforces authenticated player)
- characterId
- teamId
- damageDealt
- damageTaken
- turnsTaken
- won
- disconnected

Request schema:

{
  "matchId": 123,
  "playerId": 15,
  "characterId": "Elf",
  "teamId": "red",
  "damageDealt": 200,
  "damageTaken": 95,
  "turnsTaken": 9,
  "won": true,
  "disconnected": false
}

### POST /submit-matchupstats

- Auth required: Yes
- Query params: None
- JSON body required: Yes

Request JSON fields:

- matchId
- characterAId
- characterBId
- winnerCharacterId

Request schema:

{
  "matchId": 123,
  "characterAId": "Elf",
  "characterBId": "Harpy",
  "winnerCharacterId": "Elf"
}

### POST /submit-abilityusagestats

- Auth required: Yes
- Query params: None
- JSON body required: Yes

Request JSON fields:

- characterId
- playerId (optional for client; server validates if present and enforces authenticated player)
- damageDone
- downtime
- abilityName

Request schema:

{
  "characterId": "Elf",
  "playerId": 15,
  "damageDone": 42,
  "downtime": 1,
  "abilityName": "Arrow Rain"
}

### POST /create-match

- Auth required: Yes
- Query params: None
- JSON body: None

### POST /update-match

- Auth required: Yes
- Query params: None
- JSON body required: Yes

Request JSON fields:

- matchId
- endTime
- duration
- winnerTeamId

## WebSocket Reference

### WS /ws/lobby/{match_id}

- Auth required: Yes
- Path param:
  - match_id
- Auth input:
  - preferred: query authToken, for example /ws/lobby/111?authToken=<jwt>
  - fallback: auth_token cookie

Behavior:

- First server message is the current lobby snapshot JSON
- Server pushes subsequent lobby updates
- Server sends heartbeat JSON messages periodically

### WS /ws/lobby-control

- Auth required: Yes
- Path params: None
- Auth input:
  - preferred: query authToken, for example /ws/lobby-control?authToken=<jwt>
  - fallback: auth_token cookie

Client message format:

- JSON object with an action field

Supported actions:

- joinNewLobby
  - request: {"action":"joinNewLobby","maxPlayers":8}
  - response: {"type":"joinedLobby","matchId":<id>}
- subscribeLobby
  - request: {"action":"subscribeLobby","matchId":111}
  - response: {"type":"subscribed","matchId":111} and snapshot JSON
- setTeam
  - request: {"action":"setTeam","team":"red"}
  - side effect: broadcasts updated lobby snapshot
- setReady
  - request: {"action":"setReady"}
  - side effect: broadcasts updated lobby snapshot
- leaveLobby
  - request: {"action":"leaveLobby"}
  - response: {"type":"leftLobby","matchId":111}
- snapshot
  - request: {"action":"snapshot"}
  - response: current lobby snapshot JSON

Error response format:

- {"type":"error","detail":"..."}

### WS /ws/battle/{match_id}

- Auth required: Yes
- Path param:
  - match_id
- Auth input:
  - preferred: query authToken, for example /ws/battle/111?authToken=<jwt>
  - fallback: auth_token cookie

Server text frames:

- you|<playerId>
- turn|<playerId>
- said|<playerId>|<msg>
- epoch|<n>

Client text frames:

- During your turn, send a text payload; server records and relays it in said|... next step.

## Minimal cURL Examples

Register:

curl -X POST "http://localhost:8000/register" \
  -H "Content-Type: application/json" \
  -d '{"password":"password123"}'

Login:

curl -X POST "http://localhost:8000/login" \
  -H "Content-Type: application/json" \
  -d '{"playerId":15,"password":"password123"}'

Verify (protected):

curl -X POST "http://localhost:8000/verify" \
  -H "Content-Type: application/json" \
  -d '{"authToken":"YOUR_JWT"}'
