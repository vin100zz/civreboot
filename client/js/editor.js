// Map editor — paints an in-memory terrain grid and saves/loads it via the
// server/Maps/*.json custom map format (see server/Maps/CustomMapFormat.cs).
// Reuses MapRenderer (renderer.js) unmodified: it only ever reads
// state.map.tiles / state.units / state.cities / state.humanPlayerID, all
// optional, so a synthetic state with empty units/cities is enough to get
// full sprite-based terrain rendering, pan and zoom for free.
'use strict';

// Mirrors CustomMapFormat.CharToTerrain (server/Maps/CustomMapFormat.cs) —
// the wire format is row-strings of these characters, one per tile.
const TERRAIN_TYPES = [
  { char: 'D', id: 0,  name: 'Desert' },
  { char: 'P', id: 1,  name: 'Plains' },
  { char: 'G', id: 2,  name: 'Grassland' },
  { char: 'F', id: 3,  name: 'Forest' },
  { char: 'H', id: 4,  name: 'Hills' },
  { char: 'M', id: 5,  name: 'Mountains' },
  { char: 'T', id: 6,  name: 'Tundra' },
  { char: 'A', id: 7,  name: 'Arctic' },
  { char: 'S', id: 8,  name: 'Swamp' },
  { char: 'J', id: 9,  name: 'Jungle' },
  { char: 'W', id: 10, name: 'Water' },
  { char: 'R', id: 11, name: 'River' },
];
const CHAR_TO_TERRAIN = Object.fromEntries(TERRAIN_TYPES.map(t => [t.char, t.id]));
const TERRAIN_TO_CHAR = Object.fromEntries(TERRAIN_TYPES.map(t => [t.id, t.char]));
const WATER = CHAR_TO_TERRAIN['W'];

// Square brushes are centered on the clicked tile; the cross brush is the
// clicked tile plus its 4 orthogonal neighbors (no diagonals).
function squareOffsets(n) {
  const start = -Math.floor(n / 2);
  const offsets = [];
  for (let dy = start; dy < start + n; dy++)
    for (let dx = start; dx < start + n; dx++)
      offsets.push([dx, dy]);
  return offsets;
}
const BRUSHES = [
  { id: '1x1', label: '1x1', offsets: squareOffsets(1) },
  { id: '2x2', label: '2x2', offsets: squareOffsets(2) },
  { id: '4x4', label: '4x4', offsets: squareOffsets(4) },
  { id: '6x6', label: '6x6', offsets: squareOffsets(6) },
  { id: '8x8', label: '8x8', offsets: squareOffsets(8) },
  { id: 'cross', label: 'Cross', offsets: [[0, 0], [1, 0], [-1, 0], [0, 1], [0, -1]] },
];

// --- Editor state -----------------------------------------------------
let grid = makeBlankGrid();
let startPositions = {};
let humanStartPosition = null; // {x, y} or null — the human player's own start tile
let placingHumanStart = false; // true while armed, waiting for the next Shift+click
let currentMapName = null; // null = unsaved new map
let selectedBrush = BRUSHES[0];
let selectedTerrain = TERRAIN_TYPES[10]; // Water

function makeBlankGrid() {
  return Array.from({ length: MAP_H }, () => Array.from({ length: MAP_W }, () => WATER));
}

function setStatus(msg) {
  document.getElementById('status-msg').textContent = msg;
}

function setMapNameDisplay() {
  document.getElementById('editor-map-name').textContent = currentMapName || 'New map';
}

// --- Canvas / renderer --------------------------------------------------
const canvas = document.getElementById('map-canvas');
function resizeCanvas() {
  canvas.width = canvas.offsetWidth;
  canvas.height = canvas.offsetHeight;
  renderer.resize();
}
const renderer = new MapRenderer(canvas);
window.addEventListener('resize', resizeCanvas);
window.addEventListener('load', resizeCanvas);
requestAnimationFrame(resizeCanvas);

function redraw() {
  renderer.setState({
    map: { tiles: grid.map(row => row.map(t => ({ t, i: 0, v: 1 }))) },
    units: [],
    cities: [],
  });
}

// Draws the human-start marker on top of whatever MapRenderer most recently
// rendered. Runs as its own perpetual rAF loop (rather than right after
// redraw()) because MapRenderer also re-renders on its own for pan/zoom,
// which would otherwise wipe the marker until the next terrain edit.
function drawHumanStartMarker() {
  if (!humanStartPosition) return;
  const ts = renderer.tileSize;
  const px = renderer._screenX(humanStartPosition.x);
  const py = renderer._screenY(humanStartPosition.y);
  if (px < -ts || px > canvas.width || py < -ts || py > canvas.height) return;

  const ctx = renderer.ctx;
  ctx.save();
  ctx.beginPath();
  ctx.arc(px + ts / 2, py + ts / 2, ts / 3, 0, Math.PI * 2);
  ctx.fillStyle = '#40ff40';
  ctx.fill();
  ctx.lineWidth = 2;
  ctx.strokeStyle = '#000';
  ctx.stroke();
  ctx.fillStyle = '#000';
  ctx.font = `bold ${Math.max(10, ts * 0.45)}px monospace`;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText('H', px + ts / 2, py + ts / 2 + 1);
  ctx.restore();
}
(function overlayLoop() {
  drawHumanStartMarker();
  requestAnimationFrame(overlayLoop);
})();

// Plain click/drag pans the map (MapRenderer's own built-in behavior, left
// untouched). Shift+click or Shift+drag paints instead. A window-level
// *capturing* mousedown listener runs before MapRenderer's own (bubble-phase)
// listener on the canvas, so calling stopPropagation() here on a shift-click
// stops MapRenderer from ever starting a pan-drag for that gesture.
let shiftPainting = false;

window.addEventListener('mousedown', e => {
  if (e.button !== 0 || !e.shiftKey || e.target !== canvas) return;
  e.preventDefault();
  e.stopPropagation();
  const tile = renderer.clickToTile(e.clientX, e.clientY);
  if (placingHumanStart) {
    setHumanStart(tile.x, tile.y); // one-shot placement, not a drag brush
    return;
  }
  shiftPainting = true;
  paintAt(tile.x, tile.y);
}, { capture: true });

window.addEventListener('mousemove', e => {
  if (!shiftPainting) return;
  const tile = renderer.clickToTile(e.clientX, e.clientY);
  paintAt(tile.x, tile.y);
});

window.addEventListener('mouseup', () => {
  shiftPainting = false;
});

function paintAt(x, y) {
  for (const [dx, dy] of selectedBrush.offsets) {
    const tx = ((x + dx) % MAP_W + MAP_W) % MAP_W;
    const ty = y + dy;
    if (ty < 0 || ty >= MAP_H) continue;
    grid[ty][tx] = selectedTerrain.id;
  }
  redraw();
}

// --- Human start position --------------------------------------------------
function updateHumanStartUI() {
  document.getElementById('humanstart-info').textContent =
    humanStartPosition ? `(${humanStartPosition.x}, ${humanStartPosition.y})` : 'Not set';
  document.getElementById('btn-clear-humanstart').style.display = humanStartPosition ? '' : 'none';
  const setBtn = document.getElementById('btn-set-humanstart');
  setBtn.classList.toggle('armed', placingHumanStart);
  setBtn.textContent = placingHumanStart ? 'Shift+click the map…' : '🏠 Set Start (Shift+click map)';
}

function setHumanStart(x, y) {
  humanStartPosition = { x, y };
  placingHumanStart = false;
  updateHumanStartUI();
  setStatus(`Human start position set at (${x}, ${y}).`);
}

document.getElementById('btn-set-humanstart').addEventListener('click', () => {
  placingHumanStart = !placingHumanStart;
  updateHumanStartUI();
  setStatus(placingHumanStart
    ? 'Shift+click a tile to place the human start position.'
    : 'Cancelled placing the human start position.');
});

document.getElementById('btn-clear-humanstart').addEventListener('click', () => {
  humanStartPosition = null;
  placingHumanStart = false;
  updateHumanStartUI();
  setStatus('Human start position cleared.');
});

// --- Brush palette --------------------------------------------------
const brushList = document.getElementById('brush-list');
BRUSHES.forEach(brush => {
  const btn = document.createElement('button');
  btn.className = 'palette-btn brush-btn';
  btn.textContent = brush.label;
  btn.addEventListener('click', () => {
    selectedBrush = brush;
    [...brushList.children].forEach(c => c.classList.remove('selected'));
    btn.classList.add('selected');
  });
  brushList.appendChild(btn);
});
brushList.firstElementChild.classList.add('selected');

// --- Terrain palette --------------------------------------------------
const terrainList = document.getElementById('terrain-list');
TERRAIN_TYPES.forEach(terrain => {
  const btn = document.createElement('button');
  btn.className = 'palette-btn terrain-btn';
  btn.title = terrain.name;

  const swatch = document.createElement('div');
  swatch.className = 'terrain-swatch';
  if (terrain.id === 11) {
    // River has no standalone sprite (drawn as directional overlays on
    // grassland in-game) — approximate it with a plain color swatch.
    swatch.style.background = TERRAIN_COLORS[11];
  } else {
    const img = document.createElement('img');
    img.src = ASSET_BASE + 'terrain/' + TERRAIN_SPRITE_FILES[terrain.id];
    swatch.appendChild(img);
  }
  btn.appendChild(swatch);

  const label = document.createElement('span');
  label.textContent = terrain.name;
  btn.appendChild(label);

  btn.addEventListener('click', () => {
    selectedTerrain = terrain;
    [...terrainList.children].forEach(c => c.classList.remove('selected'));
    btn.classList.add('selected');
  });
  terrainList.appendChild(btn);
});
terrainList.children[10].classList.add('selected'); // Water, matches initial grid

// --- New / Open / Save / Save As --------------------------------------------------
function rowsToGrid(rows) {
  return Array.from({ length: MAP_H }, (_, y) => {
    const row = rows[y] || '';
    return Array.from({ length: MAP_W }, (_, x) => {
      const c = (row[x] || 'W').toUpperCase();
      return c in CHAR_TO_TERRAIN ? CHAR_TO_TERRAIN[c] : WATER;
    });
  });
}

function gridToRows() {
  return grid.map(row => row.map(t => TERRAIN_TO_CHAR[t] || 'W').join(''));
}

document.getElementById('btn-new-map').addEventListener('click', () => {
  grid = makeBlankGrid();
  startPositions = {};
  humanStartPosition = null;
  placingHumanStart = false;
  currentMapName = null;
  setMapNameDisplay();
  updateHumanStartUI();
  redraw();
  setStatus('New blank map.');
});

const openModal = document.getElementById('open-modal');
const openMapList = document.getElementById('open-map-list');

document.getElementById('btn-open-map').addEventListener('click', async () => {
  const res = await fetch('/api/maps');
  const names = await res.json();
  openMapList.innerHTML = '';
  if (names.length === 0) {
    openMapList.textContent = 'No maps saved yet.';
  } else {
    names.forEach(name => {
      const row = document.createElement('div');
      row.className = 'saveload-slot';
      row.textContent = name;
      row.addEventListener('click', () => openMap(name));
      openMapList.appendChild(row);
    });
  }
  openModal.style.display = 'flex';
});
document.getElementById('open-cancel').addEventListener('click', () => {
  openModal.style.display = 'none';
});

async function openMap(name) {
  const res = await fetch('/api/maps/' + encodeURIComponent(name));
  if (!res.ok) {
    setStatus(`Could not open "${name}".`);
    return;
  }
  const data = await res.json();
  grid = rowsToGrid(data.rows || []);
  startPositions = data.startPositions || {};
  humanStartPosition = Array.isArray(data.humanStartPosition)
    ? { x: data.humanStartPosition[0], y: data.humanStartPosition[1] }
    : null;
  placingHumanStart = false;
  currentMapName = name;
  setMapNameDisplay();
  updateHumanStartUI();
  redraw();
  openModal.style.display = 'none';
  setStatus(`Opened "${name}".`);
}

async function saveMapAs(name) {
  const res = await fetch('/api/maps', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name,
      rows: gridToRows(),
      startPositions,
      humanStartPosition: humanStartPosition ? [humanStartPosition.x, humanStartPosition.y] : undefined,
    }),
  });
  if (!res.ok) {
    setStatus(`Save failed: ${await res.text()}`);
    return false;
  }
  currentMapName = name;
  setMapNameDisplay();
  setStatus(`Saved "${name}".`);
  return true;
}

document.getElementById('btn-save-map').addEventListener('click', () => {
  if (currentMapName) {
    saveMapAs(currentMapName);
  } else {
    openSaveAsModal();
  }
});

const saveAsModal = document.getElementById('saveas-modal');
const saveAsInput = document.getElementById('saveas-name');

function openSaveAsModal() {
  saveAsInput.value = currentMapName || '';
  saveAsModal.style.display = 'flex';
  saveAsInput.focus();
}
document.getElementById('btn-saveas-map').addEventListener('click', openSaveAsModal);
document.getElementById('saveas-cancel').addEventListener('click', () => {
  saveAsModal.style.display = 'none';
});
document.getElementById('saveas-confirm').addEventListener('click', async () => {
  const name = saveAsInput.value.trim();
  if (!name) return;

  const res = await fetch('/api/maps');
  const existing = await res.json();
  if (existing.includes(name) && !confirm(`"${name}" already exists. Overwrite it?`)) return;

  if (await saveMapAs(name)) saveAsModal.style.display = 'none';
});
saveAsInput.addEventListener('keydown', e => {
  if (e.key === 'Enter') document.getElementById('saveas-confirm').click();
});

// --- Init --------------------------------------------------
setMapNameDisplay();
updateHumanStartUI();
redraw();
setStatus('New blank map. Pick a brush and terrain, then Shift+click (or Shift+drag) to paint. Click/drag without Shift scrolls the map.');
