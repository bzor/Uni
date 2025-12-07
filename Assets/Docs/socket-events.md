# Socket Events Reference

This document outlines all WebSocket (Socket.IO) events used in the Cognizer-1 system for real-time communication between clients and the server.

---

## Server Architecture

- **Server File**: `src/fake/server.js`
- **Protocol**: Socket.IO (WebSocket + polling fallback)
- **Port**: 3001 (default, configurable via `PORT` env var)
- **CORS**: Configurable via `CORS_ORIGIN` env var (default: `*`)

---

## Client Events (Client → Server)

These events are emitted by clients and handled by the server.

### 1. `startSession`

**Purpose**: Start a new cognitive session

**Payload**:
```json
{
  "sessionId": "string"
}
```

**Server Response**: Emits `sessionStarted` event

**Behavior**:
- Registers session with SessionManager
- Starts cognitive loop if this is the first active session
- Creates session in database (if `DATABASE_ENABLED=true`)

**Example**:
```javascript
socket.emit('startSession', { sessionId: 'perceptor-1234567890' });
```

---

### 2. `percept`

**Purpose**: Send a percept (audio or visual) to the cognitive engine

**Payload**:
```json
{
  "sessionId": "string",
  "type": "audio" | "visual",
  "data": {
    // Percept-specific data (varies by type)
    "timestamp": "ISO 8601 string"
  },
  "timestamp": "ISO 8601 string"
}
```

**Server Response**: Broadcasts `perceptReceived` event to all clients

**Behavior**:
- Validates session exists and is active
- Updates session activity timestamp
- Adds percept to cognitive engine's buffer
- Increments session percept count

**Example**:
```javascript
socket.emit('percept', {
  sessionId: 'perceptor-1234567890',
  type: 'audio',
  data: {
    transcript: 'Hello world',
    valence: 0.5,
    arousal: 0.3,
    sigilPhrase: 'greeting',
    sigilDrawCalls: [...]
  },
  timestamp: new Date().toISOString()
});
```

---

### 3. `endSession`

**Purpose**: End an active cognitive session

**Payload**:
```json
{
  "sessionId": "string"
}
```

**Server Response**: Emits `sessionEnded` event

**Behavior**:
- Ends session in SessionManager
- Stops cognitive loop if no more active sessions
- Ends session in database (if `DATABASE_ENABLED=true`)

**Example**:
```javascript
socket.emit('endSession', { sessionId: 'perceptor-1234567890' });
```

---

### 4. `ping`

**Purpose**: Check if a session is still valid (keepalive)

**Payload**:
```json
{
  "sessionId": "string"
}
```

**Server Response**: Emits `pong` event

**Behavior**:
- Checks if session exists in SessionManager
- Returns validity status

**Example**:
```javascript
socket.emit('ping', { sessionId: 'perceptor-1234567890' });
```

---

### 5. `getHistory`

**Purpose**: Request historical mind moments

**Payload**: None (empty)

**Server Response**: Emits `history` event

**Behavior**:
- Returns in-memory history of mind moments

**Example**:
```javascript
socket.emit('getHistory');
```

---

### 6. `getCycleStatus`

**Purpose**: Request current cognitive cycle status (for dashboard sync)

**Payload**: None (empty)

**Server Response**: Emits `cycleStatus` event

**Behavior**:
- Returns current cognitive loop state
- Used by dashboard to sync countdown timer

**Example**:
```javascript
socket.emit('getCycleStatus');
```

---

### 7. `getSessionStatus`

**Purpose**: Request current active sessions list

**Payload**: None (empty)

**Server Response**: Emits `sessionsUpdate` event

**Behavior**:
- Returns list of active sessions
- Used by dashboard to display session count

**Example**:
```javascript
socket.emit('getSessionStatus');
```

---

### 8. `disconnect`

**Purpose**: Socket disconnection (automatic, not explicitly emitted by client code)

**Payload**: None

**Behavior**:
- Automatically ends associated session
- Stops cognitive loop if no more active sessions
- Cleans up session tracking data structures

---

## Server Events (Server → Client)

These events are emitted by the server and handled by clients.

### 1. `connect`

**Purpose**: Socket successfully connected

**Payload**: None (Socket.IO built-in event)

**Triggered When**: Client successfully establishes WebSocket connection

**Handled By**: All clients (perceptor-remote, dashboard, perceptor-circumplex)

**Example Handler**:
```javascript
socket.on('connect', () => {
  console.log('Connected to Cognizer');
});
```

---

### 2. `disconnect`

**Purpose**: Socket disconnected

**Payload**: None (Socket.IO built-in event)

**Triggered When**: Connection lost or explicitly closed

**Handled By**: All clients

**Example Handler**:
```javascript
socket.on('disconnect', () => {
  console.log('Disconnected from Cognizer');
});
```

---

### 3. `connect_error`

**Purpose**: Connection error occurred

**Payload**: Error object (Socket.IO built-in event)

**Triggered When**: Connection attempt fails

**Handled By**: All clients

**Example Handler**:
```javascript
socket.on('connect_error', (error) => {
  console.error('Connection error:', error.message);
});
```

---

### 4. `sessionStarted`

**Purpose**: Confirmation that session was started

**Payload**:
```json
{
  "sessionId": "string",
  "startTime": 1234567890,
  "cognitiveCycleMs": 5000
}
```

**Triggered When**: Server processes `startSession` event

**Handled By**: Perceptor-remote

**Example Handler**:
```javascript
socket.on('sessionStarted', (data) => {
  console.log('Session started:', data.sessionId);
});
```

---

### 5. `sessionEnded`

**Purpose**: Confirmation that session was ended

**Payload**:
```json
{
  "sessionId": "string",
  "duration": 60000,
  "perceptCount": 42
}
```

**Triggered When**: Server processes `endSession` event

**Handled By**: Perceptor-remote

---

### 6. `sessionTimeout`

**Purpose**: Session expired due to inactivity

**Payload**:
```json
{
  "sessionId": "string"
}
```

**Triggered When**: SessionManager timeout callback fires

**Broadcast**: To all connected clients

---

### 7. `pong`

**Purpose**: Response to ping keepalive

**Payload**:
```json
{
  "sessionId": "string",
  "valid": true
}
```

**Triggered When**: Server processes `ping` event

---

### 8. `history`

**Purpose**: Historical mind moments data

**Payload**:
```json
{
  "history": [
    {
      "cycle": 42,
      "mindMoment": "string",
      "sigilPhrase": "string",
      "sigilCode": "...",
      "timestamp": "ISO 8601 string"
    }
  ],
  "timestamp": "ISO 8601 string"
}
```

**Triggered When**: Server processes `getHistory` event

---

### 9. `cycleStatus`

**Purpose**: Current cognitive cycle status

**Payload**:
```json
{
  "isRunning": true,
  "intervalMs": 5000,
  "nextCycleAt": 1234567890,
  "state": "COGNIZING"
}
```

**Triggered When**: Server processes `getCycleStatus` event

**Handled By**: Dashboard

---

### 10. `sessionsUpdate`

**Purpose**: Active sessions list update

**Payload**:
```json
{
  "count": 2,
  "sessions": [
    {
      "id": "perceptor-1234567890",
      "startTime": 1234567890,
      "perceptCount": 10
    }
  ]
}
```

**Triggered When**: Server processes `getSessionStatus` event

**Handled By**: Dashboard

---

### 11. `cognitiveState`

**Purpose**: Cognitive engine state changed

**Payload**:
```json
{
  "state": "COGNIZING" | "VISUALIZING" | "AGGREGATING"
}
```

**Triggered When**: Cognitive loop transitions between states

**Broadcast**: To all connected clients

**Possible States**:
- `COGNIZING`: Processing percepts and generating mind moment
- `VISUALIZING`: Generating sigil visualization
- `AGGREGATING`: Collecting percepts before next cycle

**Handled By**: Dashboard

**Example Handler**:
```javascript
socket.on('cognitiveState', ({ state }) => {
  console.log('Cognitive state:', state);
  updateStateDisplay(state);
});
```

---

### 12. `cycleStarted`

**Purpose**: New cognitive cycle began

**Payload**:
```json
{
  "cycle": 42,
  "cognitiveCycleMs": 5000
}
```

**Triggered When**: Cognitive loop starts a new cycle

**Broadcast**: To all connected clients

**Handled By**: Dashboard

**Example Handler**:
```javascript
socket.on('cycleStarted', ({ cycle, cognitiveCycleMs }) => {
  console.log('Cycle started:', cycle);
  nextCycleTime = Date.now() + cognitiveCycleMs;
  startCountdown();
});
```

---

### 13. `cycleCompleted`

**Purpose**: Cognitive cycle completed successfully

**Payload**:
```json
{
  "cycle": 42
}
```

**Triggered When**: Cognitive loop successfully completes a cycle

**Broadcast**: To all connected clients

**Handled By**: Dashboard

---

### 14. `cycleFailed`

**Purpose**: Cognitive cycle failed

**Payload**:
```json
{
  "cycle": 42,
  "error": "string"
}
```

**Triggered When**: Cognitive loop encounters an error

**Broadcast**: To all connected clients

**Handled By**: Dashboard

---

### 15. `perceptReceived`

**Purpose**: Server acknowledged receipt of a percept

**Payload**:
```json
{
  "sessionId": "string",
  "type": "audio" | "visual",
  "data": {
    // Percept-specific data
  },
  "timestamp": "ISO 8601 string"
}
```

**Triggered When**: Server processes `percept` event

**Broadcast**: To all connected clients

**Handled By**: Dashboard (live percept display)

**Example Handler**:
```javascript
socket.on('perceptReceived', (data) => {
  console.log('Percept received:', data.type);
  addPerceptToDisplay(data);
});
```

---

### 16. `mindMoment`

**Purpose**: New mind moment generated

**Payload**:
```json
{
  "cycle": 42,
  "mindMoment": "string (the cognitive reflection)",
  "sigilPhrase": "string (2-3 word essence)",
  "kinetic": 0.5,
  "lighting": {
    "color": "0xffffff",
    "pattern": "IDLE",
    "speed": 0.5
  },
  "visualPercepts": [],
  "audioPercepts": [],
  "priorMoments": [
    {
      "mindMoment": "string",
      "sigilPhrase": "string",
      "sigilCode": "..."
    }
  ],
  "timestamp": "ISO 8601 string"
}
```

**Triggered When**: Cognitive engine generates a new mind moment

**Broadcast**: To all connected clients

**Handled By**: Dashboard

**Notes**:
- This is the core cognitive output
- Contains the reflective text and context from percepts
- Sigil comes separately in `sigil` event
- Prior moments provide historical context

**Example Handler**:
```javascript
socket.on('mindMoment', (data) => {
  console.log('Mind moment:', data.mindMoment);
  updateMomentCard(data);
  displayPercepts(data.visualPercepts, data.audioPercepts);
});
```

---

### 17. `sigil`

**Purpose**: Sigil visualization generated

**Payload**:
```json
{
  "cycle": 42,
  "sigilCode": "string (SVG path data)",
  "sigilPhrase": "string",
  "sdf": {
    "width": 256,
    "height": 256,
    "data": "base64 encoded PNG"
  },
  "timestamp": "ISO 8601 string"
}
```

**Triggered When**: Cognitive engine generates sigil visualization

**Broadcast**: To all connected clients

**Handled By**: Dashboard

**Notes**:
- Contains both vector (sigilCode) and raster (SDF) representations
- SDF data is base64-encoded PNG for immediate display
- Arrives after `mindMoment` event

**Example Handler**:
```javascript
socket.on('sigil', (data) => {
  console.log('Sigil received:', data.sigilPhrase);
  if (data.sdf) {
    displaySDF(data.sdf);
  }
  updateMomentCardWithSigil(data.sigilCode);
});
```

---

### 18. `error`

**Purpose**: Error message from server

**Payload**:
```json
{
  "message": "string"
}
```

**Triggered When**: Server encounters an error processing client request

**Example**: Invalid or expired session

---

## Event Flow Diagrams

### Session Lifecycle

```
Client                          Server
  |                               |
  |-- connect ------------------->|
  |<-- connect --------------------|
  |                               |
  |-- startSession --------------->|
  |                               | (Create session, start loop)
  |<-- sessionStarted -------------|
  |                               |
  |-- percept -------------------->|
  |<-- perceptReceived ------------|
  |                               |
  |                               |--- cognitiveState: COGNIZING
  |<-- cognitiveState -------------|
  |                               |
  |                               |--- cycleStarted
  |<-- cycleStarted ---------------|
  |                               |
  |                               |--- mindMoment (processing)
  |<-- mindMoment -----------------|
  |                               |
  |                               |--- sigil (visualization)
  |<-- sigil ----------------------|
  |                               |
  |                               |--- cognitiveState: AGGREGATING
  |<-- cognitiveState -------------|
  |                               |
  |                               |--- cycleCompleted
  |<-- cycleCompleted -------------|
  |                               |
  |-- endSession ----------------->|
  |                               | (End session, stop loop)
  |<-- sessionEnded ---------------|
  |                               |
  |-- disconnect ----------------->|
  |                               |
```

### Cognitive Cycle

```
Server Cognitive Engine

1. AGGREGATING (waiting for next cycle)
   |
   v
2. Emit: cycleStarted
   |
   v
3. COGNIZING (processing percepts)
   |
   v
4. Emit: cognitiveState (COGNIZING)
   |
   v
5. Generate mind moment
   |
   v
6. Emit: mindMoment
   |
   v
7. VISUALIZING (generating sigil)
   |
   v
8. Emit: cognitiveState (VISUALIZING)
   |
   v
9. Generate sigil
   |
   v
10. Emit: sigil
    |
    v
11. AGGREGATING (back to waiting)
    |
    v
12. Emit: cognitiveState (AGGREGATING)
    |
    v
13. Emit: cycleCompleted
    |
    v
14. Wait for next cycle interval
    |
    v
    (loop back to step 1)
```

---

## Client Implementation Examples

### Perceptor Remote

**Primary Role**: Percept generator (audio + visual streams)

**Emits**:
- `startSession` - When user clicks START
- `percept` - When percept is generated from Gemini
- `endSession` - When user clicks STOP

**Listens**:
- `connect` - Initialize connection
- `sessionStarted` - Confirm session started
- `disconnect` - Handle disconnection

**File**: `web/perceptor-remote/app.js`

---

### Dashboard

**Primary Role**: Read-only observer/monitor

**Emits**:
- `getCycleStatus` - On connect (sync countdown)
- `getSessionStatus` - On connect (sync session list)

**Listens**:
- `connect` - Connection established
- `disconnect` - Connection lost
- `cognitiveState` - Update state display
- `cycleStarted` - Reset countdown, clear percepts
- `perceptReceived` - Display incoming percepts
- `mindMoment` - Display mind moment and context
- `sigil` - Display sigil visualization
- `cycleCompleted` - Cycle finished
- `cycleFailed` - Cycle error
- `cycleStatus` - Sync countdown timer
- `sessionsUpdate` - Update session count

**File**: `web/dashboard/app.js`

---

### Perceptor Circumplex

**Primary Role**: Simple dual-stream testing client

**Emits**: None (doesn't use Cognizer socket integration yet)

**Listens**: None

**File**: `web/perceptor-circumplex/app.js`

**Note**: This client connects directly to Gemini Live API, not the Cognizer server

---

## Environment Variables

**Socket Server Configuration**:
- `PORT` - Server port (default: 3001)
- `CORS_ORIGIN` - CORS allowed origin (default: *)
- `SESSION_TIMEOUT_MS` - Session inactivity timeout (default: 60000)
- `COGNITIVE_CYCLE_MS` - Cognitive loop interval (default: 5000)
- `DATABASE_ENABLED` - Enable database persistence (default: false)

---

## Notes

1. **Broadcast vs Emit**:
   - `io.emit()` - Broadcasts to ALL connected clients
   - `socket.emit()` - Sends to specific client only

2. **Session Management**:
   - Sessions timeout after inactivity (configurable)
   - Multiple clients can observe, but only one starts the session
   - Cognitive loop runs as long as ≥1 session is active

3. **Database Integration**:
   - When `DATABASE_ENABLED=true`, all events are persisted
   - Historical data can be queried via REST API
   - Dashboard uses REST API for history grid

4. **Real-time vs Historical**:
   - Socket events = real-time streaming
   - REST API = historical queries
   - Dashboard uses both for complete experience

---

## Related Documentation

- [Prime Directive](./prime-directive.md) - System overview
- [Perceptor Circumplex v2 Simple](./perceptor-circumplex-v2-simple.md) - Client implementation
- Database Schema: `src/db/migrations/`
- REST API: `src/api/`

---

**Last Updated**: December 1, 2025
