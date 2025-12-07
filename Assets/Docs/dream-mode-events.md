# Dream Mode Events - Client Guide

**For developers building Cognizer clients**

---

## Event Sequence

Dreams run on a 30-second cycle. Clients receive events in this order:

```
0-18s   perceptReceived (multiple, trickled over time)
20s     mindMoment
20s     sigil
29.9s   clearDisplay
30s     (cycle repeats)
```

---

## Events

### 1. `perceptReceived`

Emitted for each percept as it "replays" from memory.

```javascript
{
  sessionId: 'dream',
  type: 'visual' | 'audio',
  data: {
    emoji: string,
    action?: string,        // visual only
    transcript?: string,    // audio only
    analysis?: string,      // audio only
    // ... other percept fields
  },
  timestamp: string,        // current time
  originalTimestamp: string, // when percept originally occurred
  isDream: true
}
```

**Client Action**: Display percept in feed/list

---

### 2. `mindMoment`

Emitted at 20s mark.

```javascript
{
  cycle: number,
  mindMoment: string,
  sigilPhrase: string,
  kinetic: { pattern: string },
  lighting: { color: string, pattern: string, speed: number },
  visualPercepts: array,
  audioPercepts: array,
  priorMoments: array,
  isDream: true,
  timestamp: string
}
```

**Client Action**: Display mind moment text and metadata

---

### 3. `sigil`

Emitted immediately after mindMoment.

```javascript
{
  cycle: number,
  sigilCode: string,         // Canvas drawing code
  sigilPhrase: string,
  sdf: {                     // Signed distance field (optional)
    width: number,
    height: number,
    data: string            // base64 encoded PNG
  },
  isDream: true,
  timestamp: string
}
```

**Client Action**: Render sigil visualization

---

### 4. `clearDisplay`

Emitted at 29.9s mark (before next cycle).

```javascript
{
  clearPercepts: boolean,
  clearMindMoment: boolean,
  clearSigil: boolean,
  timestamp: string
}
```

**Client Action**: Clear display elements based on flags

---

## Timeline Visualization

```
Second  Event
0       perceptReceived #1
2       perceptReceived #2
5       perceptReceived #3
...
18      perceptReceived #N (last)
20      mindMoment + sigil
29.9    clearDisplay
30      (next cycle begins)
```

---

## Example Client Handler

```javascript
const socket = io('http://localhost:3001');

// Percepts trickle in
socket.on('perceptReceived', ({ type, data, isDream }) => {
  if (isDream) {
    appendPerceptToFeed(type, data);
  }
});

// Mind moment appears
socket.on('mindMoment', ({ mindMoment, sigilPhrase, isDream }) => {
  if (isDream) {
    displayMindMoment(mindMoment, sigilPhrase);
  }
});

// Sigil appears
socket.on('sigil', ({ sigilCode, sdf, isDream }) => {
  if (isDream) {
    renderSigil(sigilCode, sdf);
  }
});

// Clear everything
socket.on('clearDisplay', ({ clearPercepts, clearMindMoment, clearSigil }) => {
  if (clearPercepts) perceptFeed.innerHTML = '';
  if (clearMindMoment) mindMomentDisplay.innerHTML = '';
  if (clearSigil) sigilCanvas.clear();
});
```

---

## Distinguishing Dream from Live

All dream events include `isDream: true`. Use this to:
- Style differently (e.g., dreamy opacity, color)
- Filter events (show/hide dreams)
- Display mode indicator

---

## Notes

- **Percept timing**: Scaled from original timestamps to fit 18s window
- **No session required**: Dream mode broadcasts to all clients
- **Read-only**: Dreams are replays, not interactive
- **Cleanup**: Always handle `clearDisplay` to prevent memory leaks

