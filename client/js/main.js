'use strict';

const canvas  = document.getElementById('map-canvas');

function resizeCanvas() {
  canvas.width  = canvas.offsetWidth;
  canvas.height = canvas.offsetHeight;
  renderer.resize();
}

const renderer = new MapRenderer(canvas);
window.addEventListener('resize', resizeCanvas);
requestAnimationFrame(resizeCanvas);

const statusEl = document.getElementById('status-msg');
const turnEl   = document.getElementById('turn-info');
const yearEl   = document.getElementById('year-info');
const playerEl = document.getElementById('player-info');
const unitEl   = document.getElementById('unit-info');
const cityList = document.getElementById('city-list');
const civsList = document.getElementById('civs-list');

let busy = false;
let currentActiveUnit = null;

function yearStr(y) { return y < 0 ? `${-y} BC` : `${y} AD`; }

// Unit status flags (UnitStatusEnum)
const STATUS_SENTRY     = 0x01;
const STATUS_FORTIFYING = 0x04;
const STATUS_FORTIFIED  = 0x08;
const STATUS_BUILD_ROAD = 0x02;
const STATUS_BUILD_IRR  = 0x40;
const STATUS_BUILD_MINE = 0x80;
const STATUS_BUILD_MASK = 0xc2;

function unitStatusLabel(unit) {
  const s = unit.status;
  if (s & STATUS_FORTIFIED)  return 'Fortified';
  if (s & STATUS_FORTIFYING) return 'Fortifying';
  if (s & STATUS_SENTRY)     return 'Sleeping';
  if ((s & 0xc0) === 0xc0)   return 'Building Fortress';
  if (s & STATUS_BUILD_IRR)  return 'Irrigating';
  if (s & STATUS_BUILD_MINE) return 'Mining/Clearing';
  if (s & STATUS_BUILD_ROAD) return 'Building Road/Rail';
  return null;
}

function updateUI(state) {
    renderer.setState(state);
    turnEl.textContent = `Turn ${state.turn}`;
    yearEl.textContent = yearStr(state.year);

    const human = state.players?.find(p => p.id === state.humanPlayerID);
    if (human) playerEl.textContent = `${human.nationality} | ${human.coins}💰`;

    const activeUnit = state.units?.find(
        u => u.playerID === state.humanPlayerID && !(u.status & (STATUS_SENTRY | STATUS_FORTIFIED | STATUS_FORTIFYING | STATUS_BUILD_MASK)) && u.moves > 0
    );
    currentActiveUnit = activeUnit || null;

    if (activeUnit) {
        const statusLabel = unitStatusLabel(activeUnit);
        const statusStr = statusLabel ? ` [${statusLabel}]` : '';
        unitEl.textContent = `${activeUnit.name} #${activeUnit.id}${statusStr}\n(${activeUnit.x},${activeUnit.y}) moves:${activeUnit.moves}`;
        document.getElementById('unit-actions').style.display = 'block';
        const isSettler = activeUnit.name === 'Settlers';
        document.getElementById('settler-actions').style.display = isSettler ? 'flex' : 'none';
        document.getElementById('btn-found').style.display = isSettler ? 'none' : 'inline-block';
    } else {
        // Show sleeping/fortified units summary
        const nonActive = state.units?.filter(u =>
            u.playerID === state.humanPlayerID &&
            (u.status & (STATUS_SENTRY | STATUS_FORTIFIED | STATUS_BUILD_MASK))
        ) || [];
        if (nonActive.length) {
            unitEl.textContent = `${nonActive.length} unit(s) sleeping/fortified/building`;
        } else {
            unitEl.textContent = 'No active unit';
        }
        document.getElementById('unit-actions').style.display = 'none';
    }

    renderCivs(state);

    cityList.innerHTML = '';
    const playerByID = new Map((state.players || []).map(p => [p.id, p]));
    const cities = (state.cities || []).slice().sort((a, b) => {
        // Own cities first, then grouped by civilization, then alphabetically.
        const aMine = a.playerID === state.humanPlayerID ? 0 : 1;
        const bMine = b.playerID === state.humanPlayerID ? 0 : 1;
        if (aMine !== bMine) return aMine - bMine;
        if (a.playerID !== b.playerID) return a.playerID - b.playerID;
        return a.name.localeCompare(b.name);
    });
    cities.forEach(c => {
        const li = document.createElement('li');
        const isMine = c.playerID === state.humanPlayerID;
        if (!isMine) li.classList.add('city-foreign');

        const nameEl = document.createElement('div');
        nameEl.className = 'city-name';
        const owner = playerByID.get(c.playerID);
        const ownerLabel = !isMine && owner?.nationality ? ` — ${owner.nationality}` : '';
        nameEl.textContent = `${c.name} (${c.size})${ownerLabel}`;
        li.appendChild(nameEl);

        const statsEl = document.createElement('div');
        statsEl.className = 'city-stats';
        statsEl.textContent = formatCityStats(c);
        li.appendChild(statsEl);

        cityList.appendChild(li);
    });

    updateCityPopups(state);
}

// Shared by the sidebar city list and the draggable city popups.
function formatCityStats(c) {
    const netStr = c.food.net >= 0 ? `+${c.food.net}` : `${c.food.net}`;
    return `🌾 ${c.food.produced}▲ ${c.food.consumed}▼ (${netStr})  [${c.food.stored}/${c.food.neededToGrow}]\n` +
        `🔨 ${c.shields.producedPerTurn}/turn  ${c.shields.current || '—'} [${c.shields.stored}/${c.shields.cost}]\n` +
        `💰${c.trade.gold} 🔬${c.trade.science} 🎭${c.trade.luxury}  👥${c.unitsSupported}`;
}

// --- Draggable city info popups ------------------------------------------
// Keyed by city id. Each entry tracks its DOM element so updateCityPopups()
// can refresh the content (or remove the popup if the city was destroyed)
// on every state poll, and openCityPopup() can re-focus an already-open one
// instead of creating a duplicate.
const cityPopups = new Map();

function makeDraggable(popup, handle) {
    let dragging = false, startX = 0, startY = 0, startLeft = 0, startTop = 0;

    handle.addEventListener('mousedown', e => {
        if (e.target.closest('.popup-close')) return;
        dragging = true;
        startX = e.clientX;
        startY = e.clientY;
        const rect = popup.getBoundingClientRect();
        startLeft = rect.left;
        startTop = rect.top;
        e.preventDefault();
    });

    window.addEventListener('mousemove', e => {
        if (!dragging) return;
        const newLeft = startLeft + (e.clientX - startX);
        const newTop = startTop + (e.clientY - startY);
        popup.style.left = `${Math.max(0, newLeft)}px`;
        popup.style.top = `${Math.max(0, newTop)}px`;
    });

    window.addEventListener('mouseup', () => { dragging = false; });
}

function openCityPopup(cityID, clientX, clientY) {
    const existing = cityPopups.get(cityID);
    if (existing) {
        // Already open — bring it in front and give a little visual nudge
        // rather than opening a duplicate.
        document.body.appendChild(existing);
        return;
    }

    const state = renderer.state;
    const city = state?.cities?.find(c => c.id === cityID);
    if (!city) return;

    const popup = document.createElement('div');
    popup.className = 'city-popup';
    // Cascade slightly so multiple popups opened near the same spot don't
    // stack exactly on top of each other.
    const offset = (cityPopups.size % 6) * 18;
    popup.style.left = `${(clientX ?? 200) + offset}px`;
    popup.style.top = `${(clientY ?? 200) + offset}px`;

    const header = document.createElement('div');
    header.className = 'popup-header';
    const titleEl = document.createElement('span');
    header.appendChild(titleEl);
    const closeBtn = document.createElement('button');
    closeBtn.className = 'popup-close';
    closeBtn.textContent = '✕';
    closeBtn.addEventListener('click', () => {
        popup.remove();
        cityPopups.delete(cityID);
    });
    header.appendChild(closeBtn);
    popup.appendChild(header);

    const body = document.createElement('div');
    body.className = 'popup-body';
    const ownerEl = document.createElement('div');
    ownerEl.className = 'city-owner';
    const statsEl = document.createElement('div');
    body.appendChild(ownerEl);
    body.appendChild(statsEl);
    popup.appendChild(body);

    makeDraggable(popup, header);
    document.body.appendChild(popup);
    cityPopups.set(cityID, popup);

    // Fill in current content (also refreshed by updateCityPopups on every poll).
    refreshCityPopup(popup, city, state);
}

function refreshCityPopup(popup, city, state) {
    const owner = state.players?.find(p => p.id === city.playerID);
    const isMine = city.playerID === state.humanPlayerID;
    popup.querySelector('.popup-header span').textContent = `${city.name} (${city.size})`;
    const ownerEl = popup.querySelector('.city-owner');
    ownerEl.textContent = isMine ? '' : (owner?.nationality ?? `Player ${city.playerID}`);
    ownerEl.style.display = isMine ? 'none' : 'block';
    popup.querySelector('.popup-body > div:last-child').textContent = formatCityStats(city);
}

function updateCityPopups(state) {
    for (const [cityID, popup] of cityPopups) {
        const city = state.cities?.find(c => c.id === cityID);
        if (!city) {
            // City was destroyed/captured — drop the stale popup.
            popup.remove();
            cityPopups.delete(cityID);
            continue;
        }
        refreshCityPopup(popup, city, state);
    }
}

// Builds a "label [====    ] n/total" row. Used for both the overall
// technologies-discovered bar and the current-research bar.
function buildProgressRow(label, value, total, fillClass) {
    const row = document.createElement('div');
    row.className = 'progress-row';

    const labelEl = document.createElement('span');
    labelEl.className = 'progress-label';
    labelEl.textContent = label;
    row.appendChild(labelEl);

    const track = document.createElement('div');
    track.className = 'progress-track';
    const fill = document.createElement('div');
    fill.className = 'progress-fill' + (fillClass ? ' ' + fillClass : '');
    const pct = total > 0 ? Math.min(100, Math.round((value / total) * 100)) : 0;
    fill.style.width = `${pct}%`;
    track.appendChild(fill);
    row.appendChild(track);

    const fraction = document.createElement('span');
    fraction.className = 'progress-fraction';
    fraction.textContent = `${value}/${total}`;
    row.appendChild(fraction);

    return row;
}

function renderCivs(state) {
    if (!civsList) return;
    civsList.innerHTML = '';
    state.players?.forEach(p => {
        if (!p.nationality) return; // skip barbarians (no nationality)

        const row = document.createElement('div');
        row.className = 'civ-row' + (p.id === state.humanPlayerID ? ' civ-human' : '');

        const nameEl = document.createElement('div');
        nameEl.className = 'civ-name';
        nameEl.textContent = `${p.nationality} (${p.governmentName})`;
        row.appendChild(nameEl);

        const statsEl = document.createElement('div');
        statsEl.className = 'civ-stats';
        statsEl.textContent = `💰${p.coins}  Tax${p.taxRate}% Sci${p.scienceRate}% Lux${p.luxuryRate}%`;
        row.appendChild(statsEl);

        const r = p.research;
        if (r) {
            // Overall progress: how many of the 68 real technologies this civ has.
            row.appendChild(buildProgressRow('🔬 Total', r.discoveredCount, r.totalTechCount));

            // AI civs (and the human before their first choice) don't have a named
            // research target in the original game — it's only revealed the instant
            // research completes — so the bar shows progress without a name in that case.
            row.appendChild(buildProgressRow(r.name || 'En cours', r.progress, r.total, 'research-fill'));

            if (r.lastDiscovered) {
                const lastEl = document.createElement('div');
                lastEl.className = 'last-discovered';
                lastEl.textContent = `✓ ${r.lastDiscovered}`;
                row.appendChild(lastEl);
            }
        }

        civsList.appendChild(row);
    });
}

function setStatus(msg) { statusEl.textContent = msg; }

function setBusy(b) {
    busy = b;
    document.querySelectorAll('button').forEach(btn => btn.disabled = b);
}

async function loadState() {
    try {
        const r = await fetch('/api/state');
        const state = await r.json();
        updateUI(state);
        return state;
    } catch (e) {
        setStatus('Erreur: ' + e.message);
        return null;
    }
}

async function sendAction(obj) {
    if (busy) return;
    setBusy(true);
    setStatus(obj.type === 'endturn' ? 'IA en train de jouer…' : '…');
    try {
        const r = await fetch('/api/action', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(obj),
        });
        const state = await r.json();
        updateUI(state);
        setStatus(`Turn ${state.turn} | ok`);
    } catch (e) {
        setStatus('Erreur: ' + e.message);
    } finally {
        setBusy(false);
    }
}

// Polls /api/state until the engine has finished its intro/auto-start
// sequence (~15s) and actually has map + unit data — turn alone doesn't work
// as a readiness signal since it stays 0 until the first End Turn.
async function pollUntilReady() {
    setStatus('Démarrage du jeu…');
    const state = await loadState();
    const humanHasUnits = state?.units?.some(u => u.playerID === state.humanPlayerID);
    if (!state || !state.map || !humanHasUnits) {
        setTimeout(pollUntilReady, 1500);
    } else {
        setStatus(`Turn ${state.turn} | ok`);
        setBusy(false);
    }
}
pollUntilReady();

// Numpad
document.querySelectorAll('#numpad button').forEach(btn => {
    btn.addEventListener('click', () => sendAction({ type: 'move', param: Number(btn.dataset.dir) }));
});

document.getElementById('btn-endturn').addEventListener('click', () => sendAction({ type: 'endturn' }));
document.getElementById('btn-fortify').addEventListener('click', () => sendAction({ type: 'keypress', param: 'f'.charCodeAt(0) }));
document.getElementById('btn-sleep').addEventListener('click',   () => sendAction({ type: 'keypress', param: 's'.charCodeAt(0) }));
document.getElementById('btn-found').addEventListener('click',   () => sendAction({ type: 'found' }));
document.getElementById('btn-irrigate').addEventListener('click',() => sendAction({ type: 'keypress', param: 'i'.charCodeAt(0) }));
document.getElementById('btn-mine').addEventListener('click',    () => sendAction({ type: 'keypress', param: 'm'.charCodeAt(0) }));
document.getElementById('btn-road').addEventListener('click',    () => sendAction({ type: 'keypress', param: 'r'.charCodeAt(0) }));

let autoPlay = false;
let autoPlayRunning = false;

async function autoPlayLoop() {
  if (autoPlayRunning) return;
  autoPlayRunning = true;
  while (autoPlay) {
    await sendAction({ type: 'endturn' });
  }
  autoPlayRunning = false;
  document.getElementById('btn-autoplay').textContent = '▶ Auto-play';
  document.getElementById('btn-autoplay').classList.remove('active');
}

document.getElementById('btn-autoplay').addEventListener('click', () => {
  autoPlay = !autoPlay;
  const btn = document.getElementById('btn-autoplay');
  if (autoPlay) {
    btn.textContent = '⏹ Stop';
    btn.classList.add('active');
    autoPlayLoop();
  } else {
    btn.textContent = '▶ Auto-play';
    btn.classList.remove('active');
  }
});

document.getElementById('btn-reveal').addEventListener('click', async () => {
  const r = await fetch('/api/reveal', { method: 'POST' });
  const state = await r.json();
  updateUI(state);
});

const newGameModal = document.getElementById('newgame-modal');
const ngMapSelect = document.getElementById('ng-map');
const ngLandmassSelect = document.getElementById('ng-landmass');
const ngAgeSelect = document.getElementById('ng-age');

// Custom maps (server/Maps/*.json) are appended to the Map dropdown as
// "custom:<name>" options, fetched once from the server.
let customMapsLoaded = false;
async function loadCustomMapOptions() {
  if (customMapsLoaded) return;
  customMapsLoaded = true;
  try {
    const names = await (await fetch('/api/maps')).json();
    for (const name of names) {
      const opt = document.createElement('option');
      opt.value = `custom:${name}`;
      opt.textContent = name;
      ngMapSelect.appendChild(opt);
    }
  } catch { /* server maps list is optional; Random/Earth still work */ }
}

// Land mass / age only affect procedural generation — grey them out once a
// prebuilt map (Earth or a custom map) is selected, since they're ignored.
function updateMapDependentControls() {
  const isPrebuilt = ngMapSelect.value !== 'random';
  ngLandmassSelect.disabled = isPrebuilt;
  ngAgeSelect.disabled = isPrebuilt;
}
ngMapSelect.addEventListener('change', updateMapDependentControls);

document.getElementById('btn-newgame').addEventListener('click', () => {
  loadCustomMapOptions();
  newGameModal.style.display = 'flex';
});

document.getElementById('ng-cancel').addEventListener('click', () => {
  newGameModal.style.display = 'none';
});

document.getElementById('ng-start').addEventListener('click', async () => {
  const mapValue = ngMapSelect.value;
  const options = {
    difficulty: Number(document.getElementById('ng-difficulty').value),
    landMass: Number(document.getElementById('ng-landmass').value),
    age: Number(document.getElementById('ng-age').value),
    barbarians: document.getElementById('ng-barbarians').checked,
    earth: mapValue === 'earth',
    customMap: mapValue.startsWith('custom:') ? mapValue.slice('custom:'.length) : null,
  };
  newGameModal.style.display = 'none';

  if (autoPlay) {
    autoPlay = false;
    const btn = document.getElementById('btn-autoplay');
    btn.textContent = '▶ Auto-play';
    btn.classList.remove('active');
  }
  setBusy(true);
  setStatus('Nouvelle partie en cours de démarrage…');
  renderer._centered = false; // recenter the view on the new game's starting unit
  try {
    await fetch('/api/newgame', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(options),
    });
  } catch (e) {
    setStatus('Erreur: ' + e.message);
    setBusy(false);
    return;
  }
  pollUntilReady();
});

// Keyboard shortcuts
document.addEventListener('keydown', e => {
    if (busy) return;
    const dir = { Numpad1:1,Numpad2:2,Numpad3:3,Numpad4:4,Numpad5:5,
                  Numpad6:6,Numpad7:7,Numpad8:8,Numpad9:9,
                  ArrowLeft:4,ArrowRight:6,ArrowUp:8,ArrowDown:2 }[e.code];
    if (dir) { sendAction({ type: 'move', param: dir }); e.preventDefault(); return; }

    const key = e.key;

    if (key === ' ')              { sendAction({ type: 'endturn' }); e.preventDefault(); return; }
    if (key === 'b' || key === 'B') { sendAction({ type: 'found' }); e.preventDefault(); return; }

    // Unit commands — only when an active unit exists
    if (currentActiveUnit) {
        if (key === 'f' || key === 'F') { sendAction({ type: 'keypress', param: 'f'.charCodeAt(0) }); e.preventDefault(); return; }
        if (key === 's' || key === 'S') { sendAction({ type: 'keypress', param: 's'.charCodeAt(0) }); e.preventDefault(); return; }
        if (key === 'i' || key === 'I') { sendAction({ type: 'keypress', param: 'i'.charCodeAt(0) }); e.preventDefault(); return; }
        if (key === 'm' || key === 'M') { sendAction({ type: 'keypress', param: 'm'.charCodeAt(0) }); e.preventDefault(); return; }
        if (key === 'r' || key === 'R') { sendAction({ type: 'keypress', param: 'r'.charCodeAt(0) }); e.preventDefault(); return; }
    } else {
        // Map scroll when no active unit
        if (key === 'w') renderer.scroll(0, -2);
        if (key === 's') renderer.scroll(0, 2);
        if (key === 'a') renderer.scroll(-4, 0);
        if (key === 'd') renderer.scroll(4, 0);
    }
});

const TERRAIN_NAMES = [
  'Desert','Plains','Grassland','Forest','Hills','Mountains',
  'Tundra','Arctic','Swamp','Jungle','Ocean','River',
  'Oasis','Horses','Grassland+','Game','Coal','Gold',
  'Game2','Seals','Oil','Gems','Fish','River+'
];

function tileInfo(x, y) {
    if (!renderer.state) return;
    const state = renderer.state;
    const tile = state.map?.tiles[y]?.[x];
    if (!tile) return;

    const lines = [];

    // Terrain
    const tName = TERRAIN_NAMES[tile.t] ?? `type${tile.t}`;
    const imps = [];
    if (tile.i & IMP_IRRIG)    imps.push('Irrigation');
    if (tile.i & IMP_MINE)     imps.push('Mine');
    if (tile.i & IMP_ROAD)     imps.push('Road');
    if (tile.i & IMP_RAILROAD) imps.push('Railroad');
    if (tile.i & IMP_FORTRESS) imps.push('Fortress');
    if (tile.i & IMP_POLLUTION) imps.push('Pollution');
    lines.push(`(${x},${y}) ${tName}${imps.length ? ' | ' + imps.join(', ') : ''}`);

    // Cities
    const cities = state.cities?.filter(c => c.x === x && c.y === y) || [];
    cities.forEach(c => lines.push(`🏙 ${c.name} (size ${c.size})`));

    // Units
    const units = state.units?.filter(u => u.x === x && u.y === y) || [];
    units.forEach(u => {
        const s = u.status;
        let st = '';
        if (s & STATUS_FORTIFIED)  st = ' [Fortified]';
        else if (s & STATUS_FORTIFYING) st = ' [Fortifying]';
        else if (s & STATUS_SENTRY) st = ' [Sleep]';
        else if (s & STATUS_BUILD_IRR)  st = ' [Irrigating]';
        else if (s & STATUS_BUILD_MINE) st = ' [Mining]';
        else if (s & STATUS_BUILD_ROAD) st = ' [Road]';
        const owner = state.players?.find(p => p.id === u.playerID);
        lines.push(`⚔ ${u.name}${st} (${owner?.nationality ?? 'P'+u.playerID})`);
    });

    setStatus(lines.join('\n'));
}

canvas.addEventListener('click', e => {
    if (renderer.consumeDragFlag()) return; // ignore click-to-inspect after a map drag
    const tile = renderer.clickToTile(e.clientX, e.clientY);
    tileInfo(tile.x, tile.y);

    const city = renderer.state?.cities?.find(c => c.x === tile.x && c.y === tile.y);
    if (city) openCityPopup(city.id, e.clientX, e.clientY);
});
