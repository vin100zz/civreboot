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
    viewAllCheckbox.checked = state.viewPlayerID === -2;

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
    }

    renderCivs(state);
    updateCityPopups(state);
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
        // e.target can be a Text node (e.g. clicking directly on the "✕"
        // glyph) rather than the button Element — Text has no .closest(),
        // so calling it unguarded throws and silently breaks the listener,
        // which was blocking the close button from working.
        const targetEl = e.target.nodeType === Node.TEXT_NODE ? e.target.parentElement : e.target;
        if (targetEl?.closest('.popup-close')) return;
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
    popup.appendChild(body);

    makeDraggable(popup, header);
    document.body.appendChild(popup);
    cityPopups.set(cityID, popup);

    // Fill in current content (also refreshed by updateCityPopups on every poll).
    refreshCityPopup(popup, city, state);

    // The popup's real size depends on its content (buildings list, etc.), so
    // it can only be measured once filled in — clamp it back on-screen if the
    // click was near the bottom/right edge and it would otherwise open
    // partially (or fully) off-screen.
    clampPopupToViewport(popup);
}

function clampPopupToViewport(popup) {
    const margin = 8;
    const rect = popup.getBoundingClientRect();
    let left = rect.left;
    let top = rect.top;
    if (rect.right > window.innerWidth - margin) left -= rect.right - (window.innerWidth - margin);
    if (rect.bottom > window.innerHeight - margin) top -= rect.bottom - (window.innerHeight - margin);
    left = Math.max(margin, left);
    top = Math.max(margin, top);
    popup.style.left = `${left}px`;
    popup.style.top = `${top}px`;
}

// A small colorable face: a plain circle (any CSS color) with a mouth shape
// indicating mood, and an optional emoji badge for specialist types where a
// literal "profession face" emoji doesn't exist. One <svg> per citizen —
// mirrors the original game's city-view, one icon per citizen.
function citizenFaceSVG(mouth) {
    const mouthPath = mouth === 'happy' ? 'M5 11 Q10 17 15 11'
        : mouth === 'sad' ? 'M5 15.5 Q10 10.5 15 15.5'
        : 'M6 13 L14 13';
    return `<svg viewBox="0 0 20 20" class="citizen-face">` +
        `<circle cx="10" cy="10" r="9" class="citizen-face-bg"/>` +
        `<circle cx="6.6" cy="8" r="1.15" class="citizen-face-feature"/>` +
        `<circle cx="13.4" cy="8" r="1.15" class="citizen-face-feature"/>` +
        `<path d="${mouthPath}" class="citizen-face-feature" fill="none" stroke-width="1.3" stroke-linecap="round"/>` +
        `</svg>`;
}

function buildCitizenIcons(row, count, colorClass, mouth, badgeEmoji, title) {
    for (let i = 0; i < count; i++) {
        const wrap = document.createElement('span');
        wrap.className = `citizen-face-wrap ${colorClass}`;
        wrap.title = title;
        wrap.innerHTML = citizenFaceSVG(mouth);
        if (badgeEmoji) {
            const badge = document.createElement('span');
            badge.className = 'citizen-face-badge';
            badge.textContent = badgeEmoji;
            wrap.appendChild(badge);
        }
        row.appendChild(wrap);
    }
}

// One row, always in this order: happy (cyan, ecstatic) — content (blue,
// neutral) — unhappy (red, sad) — entertainers (white, 🎭) — tax collectors
// (gray, 💰) — scientists (purple, 🔬).
function buildCitizenRow(citizens) {
    const row = document.createElement('div');
    row.className = 'citizen-row';
    buildCitizenIcons(row, citizens.happy, 'happy', 'happy', null, 'Heureux');
    buildCitizenIcons(row, citizens.normal, 'normal', 'neutral', null, 'Content');
    buildCitizenIcons(row, citizens.unhappy, 'unhappy', 'sad', null, 'Mécontent');
    buildCitizenIcons(row, citizens.entertainers, 'entertainer', 'neutral', '🎭', 'Artiste');
    buildCitizenIcons(row, citizens.taxCollectors, 'tax', 'neutral', '💰', 'Collecteur de taxe');
    buildCitizenIcons(row, citizens.scientists, 'science', 'neutral', '🔬', 'Scientifique');
    return row;
}

// A row of small squares depicting produced vs. consumed: consumed amount
// first, then (if there's a surplus) a gap and the surplus squares — all in
// `squareClass`'s color. If consumption exceeds production, the shortfall is
// drawn instead as extra squares in the "deficit" (black) color, no gap.
function buildResourceRow(produced, consumed, squareClass) {
    const row = document.createElement('div');
    row.className = 'resource-row';

    const squares = document.createElement('div');
    squares.className = 'resource-squares';
    const net = produced - consumed;

    const addSquares = (n, cls) => {
        for (let i = 0; i < n; i++) {
            const sq = document.createElement('span');
            sq.className = `resource-square ${cls}`;
            squares.appendChild(sq);
        }
    };

    if (net >= 0) {
        addSquares(consumed, squareClass);
        if (net > 0) {
            const gap = document.createElement('span');
            gap.className = 'square-gap';
            squares.appendChild(gap);
            addSquares(net, squareClass);
        }
    } else {
        addSquares(Math.max(0, produced), squareClass);
        addSquares(-net, 'deficit');
    }
    row.appendChild(squares);

    const balance = document.createElement('span');
    balance.className = 'resource-balance ' + (net >= 0 ? 'positive' : 'negative');
    balance.textContent = net >= 0 ? `(+${net})` : `(${net})`;
    row.appendChild(balance);

    return row;
}

// Mini-map of the 21-tile "fat cross" a city can work (matches game.CityOffsets
// server-side — the city center plus 20 surrounding tiles, missing the 4 corners
// of the 5x5 box), like the original game's city-view radius display. Reuses the
// same sprite images/drawing helpers as the main map renderer (renderer.js) so
// terrain looks identical; terrain/visibility for each tile comes from the
// already-loaded main map (only per-tile yields need the server, since those
// depend on government/corruption/wonders, not just terrain).
const WORKTILE_CELL = 52;
function buildWorkTilesMiniMap(city, state) {
    const wrap = document.createElement('div');
    wrap.className = 'worktiles-wrap';

    const size = WORKTILE_CELL * 5;
    const cvs = document.createElement('canvas');
    cvs.width = size;
    cvs.height = size;
    cvs.className = 'worktiles-canvas';
    const ctx = cvs.getContext('2d');
    const tiles = state.map?.tiles;
    const ts = WORKTILE_CELL;

    (city.workTiles || []).forEach(wt => {
        const px = (wt.dx + 2) * ts;
        const py = (wt.dy + 2) * ts;
        const wx = ((city.x + wt.dx) % 80 + 80) % 80;
        const wy = city.y + wt.dy;
        const tile = (wy >= 0 && wy < 50) ? tiles?.[wy]?.[wx] : null;

        // Fog of war: an unexplored/currently-invisible tile hides everything about
        // it — terrain, improvements, whether it's worked, and its yield.
        if (!tile || tile.v === 0) {
            ctx.fillStyle = '#000';
            ctx.fillRect(px, py, ts, ts);
            return;
        }

        const baseIdx = terrainSpriteIndex(tile.t);
        if (baseIdx === 10) {
            if (!_drawSprite(ctx, TERRAIN_SPRITES[10], px, py, ts, ts)) {
                ctx.fillStyle = TERRAIN_COLORS[10];
                ctx.fillRect(px, py, ts, ts);
            }
        } else {
            _drawGrasslandBase(ctx, px, py, ts);
            if (baseIdx === 11) {
                const cx = px + ts / 2, cy = py + ts / 2;
                if (_riverNeighborIsWater(tiles, wx, wy, 1, 0))  _drawRiverOverlay(ctx, cx, cy, ts, 0);
                if (_riverNeighborIsWater(tiles, wx, wy, 0, -1)) _drawRiverOverlay(ctx, cx, cy, ts, -90);
                if (_riverNeighborIsWater(tiles, wx, wy, 0, 1))  _drawRiverOverlay(ctx, cx, cy, ts, 90);
                if (_riverNeighborIsWater(tiles, wx, wy, -1, 0)) _drawRiverOverlay(ctx, cx, cy, ts, 180);
            } else if (baseIdx !== 2) {
                const overlay = TERRAIN_SPRITES[baseIdx];
                if (!overlay || !_drawSprite(ctx, overlay, px, py, ts, ts)) {
                    ctx.fillStyle = TERRAIN_COLORS[tile.t] ?? '#333';
                    ctx.beginPath();
                    ctx.arc(px + ts / 2, py + ts / 2, ts / 2.6, 0, Math.PI * 2);
                    ctx.fill();
                }
            }
        }
        const resourceSprite = _resourceSprite(tile.t);
        if (resourceSprite) _drawSprite(ctx, resourceSprite, px, py, ts, ts);

        // --- Improvements (same overlays/order as the main map renderer) ---
        const imp = tile.i;
        if (imp & IMP_RAILROAD) _drawSprite(ctx, IMP_SPRITES[IMP_RAILROAD], px, py, ts, ts);
        else if (imp & IMP_ROAD) _drawSprite(ctx, IMP_SPRITES[IMP_ROAD], px, py, ts, ts);
        if (imp & IMP_IRRIG) _drawSprite(ctx, IMP_SPRITES[IMP_IRRIG], px, py, ts, ts);
        if (imp & IMP_MINE) _drawSprite(ctx, IMP_SPRITES[IMP_MINE], px, py, ts, ts);
        if (imp & IMP_POLLUTION) _drawSprite(ctx, IMP_SPRITES[IMP_POLLUTION], px, py, ts, ts);
        if (imp & IMP_FORTRESS) {
            ctx.strokeStyle = '#ffcc00';
            ctx.lineWidth = ts * 0.06;
            ctx.strokeRect(px + ts * 0.12, py + ts * 0.12, ts - ts * 0.24, ts - ts * 0.24);
        }

        if (wt.dx === 0 && wt.dy === 0) {
            // City center — always worked for free, drawn as a dimmed tile with a small
            // house/palace pictogram (drawn with primitives, not a text glyph — emoji/symbol
            // fonts aren't reliably available in a <canvas> context across platforms).
            ctx.fillStyle = 'rgba(0,0,0,0.55)';
            ctx.fillRect(px, py, ts, ts);
            const cx = px + ts / 2, cy = py + ts * 0.4;
            const w = ts * 0.34, h = ts * 0.22;
            ctx.fillStyle = '#f0c040';
            ctx.fillRect(cx - w / 2, cy - h / 2, w, h);
            ctx.beginPath();
            ctx.moveTo(cx - w / 2 - ts * 0.05, cy - h / 2);
            ctx.lineTo(cx, cy - h / 2 - ts * 0.2);
            ctx.lineTo(cx + w / 2 + ts * 0.05, cy - h / 2);
            ctx.closePath();
            ctx.fill();
        } else {
            const borderWidth = wt.worked ? ts * 0.09 : ts * 0.03;
            ctx.strokeStyle = wt.worked ? '#ffe060' : 'rgba(0,0,0,0.5)';
            ctx.lineWidth = borderWidth;
            ctx.strokeRect(px + borderWidth / 2, py + borderWidth / 2, ts - borderWidth, ts - borderWidth);
        }

        // Yield numbers — shown on every tile, including the city center.
        ctx.font = `bold ${Math.round(ts * 0.24)}px monospace`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'alphabetic';
        ctx.lineWidth = ts * 0.07;
        ctx.strokeStyle = '#000';
        const label = `${wt.food}/${wt.shields}/${wt.trade}`;
        ctx.strokeText(label, px + ts / 2, py + ts - ts * 0.1);
        ctx.fillStyle = '#fff';
        ctx.fillText(label, px + ts / 2, py + ts - ts * 0.1);
    });

    ctx.textAlign = 'left';
    ctx.textBaseline = 'alphabetic';

    wrap.appendChild(cvs);
    return wrap;
}

function popupSectionTitle(text) {
    const el = document.createElement('div');
    el.className = 'popup-section-title';
    el.textContent = text;
    return el;
}

function buildCityPopupBody(city, state) {
    const frag = document.createDocumentFragment();
    const owner = state.players?.find(p => p.id === city.playerID);
    const isMine = city.playerID === state.humanPlayerID;

    if (!isMine) {
        const ownerEl = document.createElement('div');
        ownerEl.className = 'city-owner';
        ownerEl.textContent = owner?.nationality ?? `Player ${city.playerID}`;
        frag.appendChild(ownerEl);
    }

    // --- Citizens ---
    if (city.citizens) {
        frag.appendChild(popupSectionTitle('Citoyens'));
        frag.appendChild(buildCitizenRow(city.citizens));
    }

    // --- Worked tiles mini-map ---
    if (city.workTiles?.length) {
        frag.appendChild(popupSectionTitle('Territoire'));
        frag.appendChild(buildWorkTilesMiniMap(city, state));
        const legend = document.createElement('div');
        legend.className = 'popup-subtext worktiles-legend';
        legend.textContent = 'Bordure dorée = case exploitée — chiffres = nourriture / production / commerce';
        frag.appendChild(legend);
    }

    // --- Food ---
    frag.appendChild(popupSectionTitle('Nourriture'));
    frag.appendChild(buildResourceRow(city.food.produced, city.food.consumed, 'food'));
    frag.appendChild(buildProgressRow('Réserve', city.food.stored, Math.max(1, city.food.neededToGrow)));

    // --- Production ---
    frag.appendChild(popupSectionTitle('Production'));
    frag.appendChild(buildResourceRow(city.shields.produced, city.shields.consumed, 'shields'));
    const currentEl = document.createElement('div');
    currentEl.className = 'popup-subtext';
    currentEl.textContent = city.shields.current || '(rien en construction)';
    frag.appendChild(currentEl);
    if (city.shields.cost > 0) {
        frag.appendChild(buildProgressRow('Stock', city.shields.stored, city.shields.cost));
    }
    if (city.improvements?.length) {
        const label = document.createElement('div');
        label.className = 'popup-buildings-label';
        label.textContent = '🏛 Bâtiments';
        frag.appendChild(label);

        const list = document.createElement('ul');
        list.className = 'popup-buildings';
        city.improvements.forEach(name => {
            const li = document.createElement('li');
            li.textContent = name;
            list.appendChild(li);
        });
        frag.appendChild(list);
    }

    // --- Trade ---
    frag.appendChild(popupSectionTitle('Commerce'));
    frag.appendChild(buildResourceRow(city.trade.produced, city.trade.consumed, 'trade'));
    const splitEl = document.createElement('div');
    splitEl.className = 'popup-subtext';
    splitEl.textContent = `💰${city.trade.gold}   🔬${city.trade.science}   🎭${city.trade.luxury}`;
    frag.appendChild(splitEl);

    return frag;
}

function refreshCityPopup(popup, city, state) {
    popup.querySelector('.popup-header span').textContent = `${city.name} (${city.size})`;
    const body = popup.querySelector('.popup-body');
    body.innerHTML = '';
    body.appendChild(buildCityPopupBody(city, state));
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
    // Click a civ's row to look at the map through its fog of war (see setViewMode
    // below) — the currently-spectated civ (if any, i.e. not "Toute la carte") is
    // highlighted via the civ-viewing class.
    const viewingPlayerID = state.viewPlayerID === -2 ? null
        : (state.viewPlayerID === -1 ? state.humanPlayerID : state.viewPlayerID);

    state.players?.forEach(p => {
        if (!p.nationality) return; // skip barbarians (no nationality)

        const row = document.createElement('div');
        row.className = 'civ-row'
            + (p.id === state.humanPlayerID ? ' civ-human' : '')
            + (p.id === viewingPlayerID ? ' civ-viewing' : '');
        row.dataset.playerId = p.id;
        row.addEventListener('click', () => setViewMode(p.id));

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
    // Popup close buttons are a pure client-side UI action (no server round
    // trip) — they must stay clickable even while a game action is in
    // flight, or during auto-play (which keeps busy=true almost
    // continuously) the close button becomes unclickable for its entire run.
    document.querySelectorAll('button:not(.popup-close)').forEach(btn => btn.disabled = b);
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

document.getElementById('btn-endturn').addEventListener('click', () => sendAction({ type: 'endturn' }));

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

// "View map of" — clicking a civilization in the sidebar looks at the map through
// that civ's fog of war (or, via the "Toute la carte" checkbox, with no fog at
// all). Purely a display setting: it never touches whose units/cities you
// control (state.humanPlayerID is untouched server-side).
const viewAllCheckbox = document.getElementById('view-all-checkbox');

async function setViewMode(mode) {
  const r = await fetch('/api/viewmode', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ mode }),
  });
  const state = await r.json();
  updateUI(state);
}

viewAllCheckbox.addEventListener('change', () => {
  setViewMode(viewAllCheckbox.checked ? -2 : -1);
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
