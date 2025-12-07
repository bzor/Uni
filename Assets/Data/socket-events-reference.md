# Socket Events Reference

Complete reference for Cognizer-1 WebSocket events.

---

## Architecture

**Unified 60-second consciousness cycle** with 6 phases:

```
PERCEPTS   (0-35s)   → Sensory input window
SPOOL      (35-37s)  → Transition buffer
SIGILIN    (37-40s)  → Emit mind moment + sigil
SIGILHOLD  (40-55s)  → Display pause
SIGILOUT   (55-58s)  → Fade out
RESET      (58-60s)  → Cleanup
```

**Two modes, identical timing:**
- **LIVE**: Real-time LLM processing of percepts
- **DREAM**: Replay historical mind moments from database

---

## Connection

```javascript
const socket = io('wss://server:3001');
// Read-only clients: just listen to broadcasts
// Interactive clients: emit percepts + listen
```

---

## Client → Server Events

### `startSession`
Start a cognitive session (interactive clients only).

```javascript
socket.emit('startSession', { sessionId: 'unique-id' });
```

**Response**: `sessionStarted`

---

### `percept`
Submit sensory input (visual or audio).

```javascript
socket.emit('percept', {
  sessionId: 'unique-id',
  type: 'visual' | 'audio',
  data: {
    // Visual fields:
    emoji, action, description, sigilPhrase, drawCalls, timestamp
    
    // Audio fields:
    emoji, transcript, analysis, tone, sentiment, 
    confidence, sigilPhrase, sigilDrawCalls, timestamp
  }
});
```

Server auto-generates 256×256px PNG from `drawCalls`/`sigilDrawCalls`.

---

### `endSession`
End your session.

```javascript
socket.emit('endSession', { sessionId: 'unique-id' });
```

---

### `ping`
Keepalive.

```javascript
socket.emit('ping', { sessionId: 'unique-id' });
```

**Response**: `pong`

---

### `getCycleStatus`
Request current cycle info.

```javascript
socket.emit('getCycleStatus');
```

**Response**: `cycleStatus`

---

### `getSessionStatus`
Request active sessions count.

```javascript
socket.emit('getSessionStatus');
```

**Response**: `sessionsUpdate`

---

### `getHistory`
Request mind moment history.

```javascript
socket.emit('getHistory');
```

**Response**: `history`

---

## Server → Client Broadcasts

All connected clients receive these events.

---

### `phase`
**NEW** - Emitted at start of each cycle phase.

```javascript
{
  phase: 'PERCEPTS' | 'SPOOL' | 'SIGILIN' | 'SIGILHOLD' | 'SIGILOUT' | 'RESET',
  startTime: '2025-12-07T...',
  duration: 35000,        // milliseconds
  cycleNumber: 142,
  isDream: false
}
```

**Use for**: UI timing, animations, progress bars.

---

### `cognitiveState`
State machine transitions.

```javascript
{
  state: 'IDLE' | 'AGGREGATING' | 'COGNIZING' | 'VISUALIZING' | 'DREAMING'
}
```

**States:**
- `IDLE` - No sessions, not dreaming
- `DREAMING` - Replaying historical moments
- `AGGREGATING` - LIVE mode, collecting percepts
- `COGNIZING` - LIVE mode, LLM processing
- `VISUALIZING` - LIVE mode, sigil generation

---

### `perceptReceived`
Percept acknowledged and processed.

```javascript
{
  sessionId: 'abc123' | 'dream',
  type: 'visual' | 'audio',
  data: {
    emoji: '🎨',
    description: '...',
    sigilPhrase: '...',
    pngData: 'base64...',   // 256×256px PNG
    pngWidth: 256,
    pngHeight: 256,
    timestamp: '...',
    // ... other percept fields
  },
  timestamp: '2025-12-07T...',
  originalTimestamp: '...',  // Only in DREAM mode
  isDream: false
}
```

---

### `mindMoment`
**Core output** - LLM-generated consciousness reflection.

Emitted during **SIGILIN phase** (37s mark).

```javascript
{
  cycle: 142,
  mindMoment: "I observe shifting patterns...",
  sigilPhrase: "Emergent harmony in chaos",
  kinetic: "SLOW_SWAY",
  lighting: {
    color: [100, 150, 200],
    pattern: "SMOOTH_WAVES",
    speed: 0.5
  },
  visualPercepts: [...],   // Includes PNG data
  audioPercepts: [...],    // Includes PNG data
  priorMoments: [...],     // 3 recent mind moments
  timestamp: '2025-12-07T...',
  isDream: false
}
```

---

### `sigil`
Visual representation of mind moment.

Emitted immediately after `mindMoment`.

```javascript
{
  cycle: 142,
  sigilCode: "ctx.fillStyle='#fff';ctx.arc(256,256,100,0,Math.PI*2);...",
  sigilPhrase: "Emergent harmony in chaos",
  png: {
    data: 'base64...',     // 512×512px PNG
    width: 512,
    height: 512
  },
  sdf: {                   // Signed distance field (optional)
    data: 'base64...',     // Float32Array
    width: 256,
    height: 256
  },
  timestamp: '2025-12-07T...',
  isDream: false
}
```

---

### `cycleStarted`
New cognitive cycle begins.

```javascript
{
  cycle: 142,
  visualPerceptCount: 3,
  audioPerceptCount: 2,
  timestamp: '2025-12-07T...'
}
```

---

### `cycleCompleted`
Cycle finished successfully.

```javascript
{
  cycle: 142,
  duration: 4500,           // milliseconds
  mindMomentLength: 234,    // characters
  sigilPhraseLength: 24,
  timestamp: '2025-12-07T...'
}
```

---

### `cycleFailed`
Cycle encountered error.

```javascript
{
  cycle: 142,
  error: "LLM timeout",
  timestamp: '2025-12-07T...'
}
```

---

### `sigilFailed`
Sigil generation failed.

```javascript
{
  cycle: 142,
  error: "Canvas rendering error",
  timestamp: '2025-12-07T...'
}
```

---

### `clearDisplay`
**DREAM mode only** - Clear UI elements.

```javascript
{
  clearPercepts: true,
  clearMindMoment: true,
  clearSigil: true
}
```

---

### `sessionsUpdate`
Active session count changed.

```javascript
{
  count: 2,
  sessions: [
    { id: 'session1', status: 'active' },
    { id: 'session2', status: 'active' }
  ]
}
```

---

### `sessionTimeout`
Session expired due to inactivity.

```javascript
{
  sessionId: 'abc123'
}
```

---

## Client-Specific Responses

These only go to the requesting client.

---

### `sessionStarted`
```javascript
{
  sessionId: 'unique-id',
  startTime: '2025-12-07T...'
}
```

---

### `sessionEnded`
```javascript
{
  sessionId: 'unique-id',
  endTime: '2025-12-07T...',
  perceptCount: 15
}
```

---

### `pong`
```javascript
{
  sessionId: 'unique-id',
  timestamp: '2025-12-07T...'
}
```

---

### `cycleStatus`
```javascript
{
  isRunning: true,
  mode: 'LIVE' | 'DREAM',
  intervalMs: 60000,
  state: 'AGGREGATING',
  nextCycleAt: '2025-12-07T...' | null,
  msUntilNextCycle: 45000 | null
}
```

---

### `history`
```javascript
{
  sessionId: 'request-id',
  history: [
    {
      cycle: 140,
      mindMoment: '...',
      sigilPhrase: '...',
      timestamp: '...'
      // ... other fields
    }
  ]
}
```

---

### `error`
```javascript
{
  message: 'Invalid session'
}
```

---

## Event Flow Examples

### DREAM Mode (60s cycle)

```
0s:   phase { PERCEPTS, 35000ms }
      → perceptReceived (multiple, dispersed)
35s:  phase { SPOOL, 2000ms }
37s:  phase { SIGILIN, 3000ms }
      → mindMoment { isDream: true }
      → sigil { isDream: true }
40s:  phase { SIGILHOLD, 15000ms }
55s:  phase { SIGILOUT, 3000ms }
58s:  phase { RESET, 2000ms }
60s:  [repeat]
```

### LIVE Mode (60s cycle with A/B buffering)

```
0s:   phase { PERCEPTS, 35000ms }
      [percepts accumulate]
35s:  [Dump percepts → LLM processing starts]
      phase { SPOOL, 2000ms }
37s:  phase { SIGILIN, 3000ms }
      → mindMoment { cycle: N-1 }   // Previous cycle
      → sigil { cycle: N-1 }
40s:  phase { SIGILHOLD, 15000ms }
55s:  phase { SIGILOUT, 3000ms }
58s:  phase { RESET, 2000ms }
60s:  [repeat with cycle N ready]
```

**Key**: LIVE displays **previous cycle's result** while **current cycle processes in background**.

---

## Implementation Notes

### For Read-Only Clients

```javascript
const socket = io('wss://server:3001');

socket.on('phase', ({ phase, duration }) => {
  updatePhaseUI(phase, duration);
});

socket.on('perceptReceived', ({ data }) => {
  displayPercept(data.pngData);
});

socket.on('mindMoment', ({ mindMoment, sigilPhrase }) => {
  displayText(mindMoment, sigilPhrase);
});

socket.on('sigil', ({ png }) => {
  displaySigil(png.data);
});
```

### For Interactive Clients

Same as read-only, plus:

```javascript
// Start session
socket.emit('startSession', { sessionId: generateId() });

// Send percepts
socket.emit('percept', {
  sessionId: mySessionId,
  type: 'visual',
  data: { /* percept data */ }
});

// Keepalive every 30s
setInterval(() => {
  socket.emit('ping', { sessionId: mySessionId });
}, 30000);

// End session on disconnect
window.addEventListener('beforeunload', () => {
  socket.emit('endSession', { sessionId: mySessionId });
});
```

---

## Key Concepts

- **Static Timing**: Phases run on fixed schedule regardless of LLM speed
- **Broadcast Everything**: All clients see all events from all sessions
- **PNG Embedded**: Images pre-rendered as base64, ready to display
- **`isDream` Flag**: Distinguish between LIVE and DREAM events
- **No Auth for Read-Only**: Connect and listen without authentication
- **60s Rhythm**: Sync UI to 6-phase cycle for smooth experience

---

**Last Updated**: December 7, 2025  
**Version**: Cognizer-1 (unified consciousness loop with static timing)
